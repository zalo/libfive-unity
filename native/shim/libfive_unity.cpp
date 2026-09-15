/*
 *  libfive-unity native helpers, compiled into the libfive plugin binary (see native/CMakeLists.txt).
 *
 *  Batched gradient evaluation for feature-based normal splitting. The stock C API only offers a
 *  single-point libfive_tree_eval_d that constructs a fresh evaluator per call, which is far too slow
 *  to run once per mesh corner; these entry points evaluate LIBFIVE_EVAL_ARRAY_SIZE points per tape
 *  pass and spread the work over several threads.
 *
 *  This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
 */
#include <algorithm>
#include <cstdint>
#include <cstdlib>
#include <thread>
#include <vector>

#include "libfive.h"
#include "libfive/tree/tree.hpp"
#include "libfive/eval/eval_deriv_array.hpp"
#include "libfive/eval/eval_array_size.hpp"

#if defined(_MSC_VER)
#define LIBFIVE_UNITY_EXPORT extern "C" __declspec(dllexport)
#else
#define LIBFIVE_UNITY_EXPORT extern "C" __attribute__((visibility("default")))
#endif

namespace {

using libfive::DerivArrayEvaluator;
using libfive::Tree;

// Evaluates the gradient of `tree` at points[begin, end) into out[begin, end) on the calling thread.
void gradients_range(const Tree& tree, const libfive_vec3* points, libfive_vec3* out,
                     uint32_t begin, uint32_t end)
{
    DerivArrayEvaluator eval(tree);
    for (uint32_t start = begin; start < end; start += LIBFIVE_EVAL_ARRAY_SIZE) {
        const size_t n = std::min<size_t>(LIBFIVE_EVAL_ARRAY_SIZE, end - start);
        for (size_t i = 0; i < n; ++i) {
            const libfive_vec3& p = points[start + i];
            eval.set(Eigen::Vector3f(p.x, p.y, p.z), i);
        }
        auto d = eval.derivs(n);  // rows 0..2: d/dx, d/dy, d/dz; row 3: value
        for (size_t i = 0; i < n; ++i) {
            out[start + i] = {d(0, i), d(1, i), d(2, i)};
        }
    }
}

// Splits [0, count) across up to `max_threads` threads (one evaluator each).
void gradients_parallel(const Tree& tree, const libfive_vec3* points, libfive_vec3* out,
                        uint32_t count, unsigned max_threads)
{
    const uint32_t min_per_thread = 4 * LIBFIVE_EVAL_ARRAY_SIZE;
    unsigned hw = std::thread::hardware_concurrency();
    unsigned threads = std::max(1u, std::min({max_threads, hw == 0 ? 1u : hw,
                                              (unsigned)(count / min_per_thread)}));
    if (threads <= 1) {
        gradients_range(tree, points, out, 0, count);
        return;
    }
    std::vector<std::thread> pool;
    pool.reserve(threads);
    const uint32_t chunk = (count + threads - 1) / threads;
    for (unsigned t = 0; t < threads; ++t) {
        const uint32_t begin = t * chunk;
        const uint32_t end = std::min(count, begin + chunk);
        if (begin >= end) break;
        pool.emplace_back([&tree, points, out, begin, end] {
            gradients_range(tree, points, out, begin, end);
        });
    }
    for (auto& th : pool) th.join();
}

}  // namespace

/*
 *  Writes the gradient of `t` at each of `count` points into `out` (raw, not normalized; NaN or zero
 *  where the field is not differentiable). Returns the number of points evaluated. Safe to call from
 *  any thread; `t` must stay alive for the duration of the call.
 */
LIBFIVE_UNITY_EXPORT uint32_t libfive_unity_gradients(libfive_tree t, const libfive_vec3* points,
                                                      uint32_t count, libfive_vec3* out)
{
    if (t == nullptr || count == 0 || points == nullptr || out == nullptr) return 0;
    gradients_parallel(Tree(t), points, out, count, 8);
    return count;
}

/*
 *  For every triangle corner of `mesh` (3 * tri_count entries, in index order) evaluates the gradient
 *  of `t` at the corner position moved `nudge` of the way towards the triangle's centroid, so the
 *  sample lies strictly inside the face the triangle belongs to even when the vertex sits on a crease.
 *  Returns a malloc'd array (release with libfive_unity_free) or NULL if the mesh is empty.
 */
LIBFIVE_UNITY_EXPORT libfive_vec3* libfive_unity_mesh_corner_gradients(libfive_tree t,
                                                                        const libfive_mesh* mesh,
                                                                        float nudge)
{
    if (t == nullptr || mesh == nullptr || mesh->tri_count == 0 || mesh->verts == nullptr ||
        mesh->tris == nullptr) {
        return nullptr;
    }
    const uint32_t corners = mesh->tri_count * 3;
    auto* points = static_cast<libfive_vec3*>(std::malloc(sizeof(libfive_vec3) * corners));
    auto* out = static_cast<libfive_vec3*>(std::malloc(sizeof(libfive_vec3) * corners));
    if (points == nullptr || out == nullptr) {
        std::free(points);
        std::free(out);
        return nullptr;
    }
    for (uint32_t tri = 0; tri < mesh->tri_count; ++tri) {
        const libfive_tri& idx = mesh->tris[tri];
        const uint32_t ids[3] = {idx.a, idx.b, idx.c};
        libfive_vec3 v[3];
        for (int k = 0; k < 3; ++k) {
            v[k] = ids[k] < mesh->vert_count ? mesh->verts[ids[k]] : libfive_vec3{0.f, 0.f, 0.f};
        }
        const libfive_vec3 c = {(v[0].x + v[1].x + v[2].x) / 3.f, (v[0].y + v[1].y + v[2].y) / 3.f,
                                (v[0].z + v[1].z + v[2].z) / 3.f};
        for (int k = 0; k < 3; ++k) {
            points[3 * tri + k] = {v[k].x + nudge * (c.x - v[k].x), v[k].y + nudge * (c.y - v[k].y),
                                   v[k].z + nudge * (c.z - v[k].z)};
        }
    }
    gradients_parallel(Tree(t), points, out, corners, 8);
    std::free(points);
    return out;
}

/*
 *  Two-offset variant for discontinuity detection: returns 2 * 3 * tri_count gradients, the first
 *  3 * tri_count sampled `offset_a` of the way from each corner towards its triangle's centroid and the
 *  second 3 * tri_count sampled at `offset_b`. On a smooth surface the gradient varies linearly with the
 *  offset (curvature), so the caller can extrapolate the gradient at the vertex itself; across a crease
 *  the two samples of a corner agree with each other but not with the neighbouring corner's.
 *  Release with libfive_unity_free. Returns NULL if the mesh is empty.
 */
LIBFIVE_UNITY_EXPORT libfive_vec3* libfive_unity_mesh_corner_gradients2(libfive_tree t,
                                                                         const libfive_mesh* mesh,
                                                                         float offset_a, float offset_b)
{
    if (t == nullptr || mesh == nullptr || mesh->tri_count == 0 || mesh->verts == nullptr ||
        mesh->tris == nullptr) {
        return nullptr;
    }
    const uint32_t corners = mesh->tri_count * 3;
    const uint32_t total = corners * 2;
    auto* points = static_cast<libfive_vec3*>(std::malloc(sizeof(libfive_vec3) * total));
    auto* out = static_cast<libfive_vec3*>(std::malloc(sizeof(libfive_vec3) * total));
    if (points == nullptr || out == nullptr) {
        std::free(points);
        std::free(out);
        return nullptr;
    }
    for (uint32_t tri = 0; tri < mesh->tri_count; ++tri) {
        const libfive_tri& idx = mesh->tris[tri];
        const uint32_t ids[3] = {idx.a, idx.b, idx.c};
        libfive_vec3 v[3];
        for (int k = 0; k < 3; ++k) {
            v[k] = ids[k] < mesh->vert_count ? mesh->verts[ids[k]] : libfive_vec3{0.f, 0.f, 0.f};
        }
        const libfive_vec3 c = {(v[0].x + v[1].x + v[2].x) / 3.f, (v[0].y + v[1].y + v[2].y) / 3.f,
                                (v[0].z + v[1].z + v[2].z) / 3.f};
        for (int k = 0; k < 3; ++k) {
            const uint32_t i = 3 * tri + k;
            points[i] = {v[k].x + offset_a * (c.x - v[k].x), v[k].y + offset_a * (c.y - v[k].y),
                         v[k].z + offset_a * (c.z - v[k].z)};
            points[corners + i] = {v[k].x + offset_b * (c.x - v[k].x), v[k].y + offset_b * (c.y - v[k].y),
                                   v[k].z + offset_b * (c.z - v[k].z)};
        }
    }
    gradients_parallel(Tree(t), points, out, total, 8);
    std::free(points);
    return out;
}

LIBFIVE_UNITY_EXPORT void libfive_unity_free(void* p) { std::free(p); }
