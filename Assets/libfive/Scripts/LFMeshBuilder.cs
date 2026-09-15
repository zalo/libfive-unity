using System;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using libfivesharp.libFiveInternal;

namespace libfivesharp {
  /// <summary>
  /// Turns raw triangle soup (from libfive or any Unity Mesh) into a Unity Mesh with positions and
  /// smoothing-group normals, entirely in Burst-compiled jobs and without managed garbage.
  ///
  /// Normals: each vertex's incident triangles are grouped into smoothing groups. Two triangles that
  /// share an edge at the vertex are in the same group when the angle between their corner normals is
  /// below <c>splitAngle</c>; groups are connected components of that relation, so a gently curved fan
  /// stays smooth even when its far ends differ by more than the threshold. Each extra group gets a
  /// duplicated vertex. Group normals are angle-weighted averages of the member corner normals.
  /// With splitAngle >= 180 nothing is split and you get ordinary smooth normals.
  ///
  /// Corner normals come from one of two sources:
  /// <list type="bullet">
  /// <item><b>Feature (analytic) normals</b>: the gradient of the distance field, evaluated for every
  /// triangle corner at a point nudged towards the triangle's centroid (see
  /// <c>libfive_unity_mesh_corner_gradients</c>). On a smooth patch all corners around a vertex agree to
  /// within curvature, and across a crease they differ by the true dihedral angle of the underlying
  /// primitives, so the split decision follows the shape rather than the noisy face normals of dual
  /// contouring's small or skinny triangles. Smooth normals are exact instead of averaged.</item>
  /// <item><b>Geometric normals</b>: the face normal of the triangle, used for arbitrary meshes
  /// (<see cref="RecalculateNormals"/>) and for corners whose gradient is zero or not finite.</item>
  /// </list>
  ///
  /// Input vertices that no triangle references are kept (with an up-facing normal) so indices never
  /// need remapping; libfive always emits one such placeholder at index 0.
  /// </summary>
  public static class LFMeshBuilder {
    /// <summary>Maximum triangles around one vertex that participate in clustering; extra ones join group 0.</summary>
    public const int MaxValence = 64;

    [StructLayout(LayoutKind.Sequential)]
    public struct Vertex {
      public float3 position;
      public float3 normal;
    }

    static readonly VertexAttributeDescriptor[] VertexLayout = {
      new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3, 0),
      new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3, 0),
    };

    const MeshUpdateFlags ApplyFlags =
      MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontResetBoneBounds;

    /// <summary>Statistics from the last Build call on this thread.</summary>
    public static int LastInputVertexCount, LastOutputVertexCount, LastTriangleCount;
    /// <summary>True if the last Build used analytic corner gradients rather than face normals.</summary>
    public static bool LastUsedFeatureNormals;

    /// <summary>
    /// Builds <paramref name="target"/> from a libfive_mesh pointer (as returned by
    /// libfive_tree_render_mesh) with geometric normals. Does not free the native mesh. Returns false
    /// and clears the mesh when the pointer is null or the mesh is empty.
    /// </summary>
    public static bool Build(IntPtr nativeMesh, Mesh target, Bounds bounds, float splitAngle) {
      return Build(nativeMesh, IntPtr.Zero, target, bounds, splitAngle);
    }

    /// <summary>
    /// Builds <paramref name="target"/> from a libfive_mesh pointer plus optional per-corner gradients
    /// (3 * tri_count libfive_vec3, as returned by libfive_unity_mesh_corner_gradients; pass
    /// IntPtr.Zero for geometric normals). Frees neither buffer.
    /// </summary>
    public static unsafe bool Build(IntPtr nativeMesh, IntPtr cornerGradients, Mesh target, Bounds bounds, float splitAngle) {
      if (target == null) throw new ArgumentNullException(nameof(target));
      if (nativeMesh == IntPtr.Zero) { Clear(target); return false; }

      libfive_mesh m = *(libfive_mesh*)nativeMesh;
      int vertexCount = (int)m.vert_count;
      int triangleCount = (int)m.tri_count;
      if (vertexCount <= 0 || triangleCount <= 0 || m.verts == IntPtr.Zero || m.tris == IntPtr.Zero) {
        Clear(target);
        return false;
      }

      NativeArray<float3> positions = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<float3>(
        (void*)m.verts, vertexCount, Allocator.None);
      NativeArray<uint> indices = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<uint>(
        (void*)m.tris, triangleCount * 3, Allocator.None);
      NativeArray<float3> gradients = default(NativeArray<float3>);
      if (cornerGradients != IntPtr.Zero) {
        gradients = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<float3>(
          (void*)cornerGradients, triangleCount * 3, Allocator.None);
      }
#if ENABLE_UNITY_COLLECTIONS_CHECKS
      AtomicSafetyHandle positionsHandle = AtomicSafetyHandle.Create();
      AtomicSafetyHandle indicesHandle = AtomicSafetyHandle.Create();
      AtomicSafetyHandle gradientsHandle = AtomicSafetyHandle.Create();
      NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref positions, positionsHandle);
      NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref indices, indicesHandle);
      if (gradients.IsCreated) NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref gradients, gradientsHandle);
#endif
      try {
        return Build(positions, indices, gradients, target, bounds, splitAngle);
      } finally {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        AtomicSafetyHandle.Release(positionsHandle);
        AtomicSafetyHandle.Release(indicesHandle);
        AtomicSafetyHandle.Release(gradientsHandle);
#endif
      }
    }

    /// <summary>
    /// Builds <paramref name="target"/> from positions and a flat triangle index list. The inputs are
    /// only read and remain owned by the caller. Returns false and clears the mesh if the input is empty
    /// or contains out-of-range indices.
    /// </summary>
    public static bool Build(NativeArray<float3> positions, NativeArray<uint> indices, Mesh target, Bounds bounds, float splitAngle) {
      return Build(positions, indices, default(NativeArray<float3>), target, bounds, splitAngle);
    }

    /// <summary>
    /// As <see cref="Build(NativeArray{float3}, NativeArray{uint}, Mesh, Bounds, float)"/>, with an optional
    /// analytic gradient per corner (<paramref name="cornerGradients"/>[c] belongs to indices[c]; pass a
    /// default/uncreated array for geometric normals). Corners whose gradient is zero or not finite use
    /// their face normal.
    /// </summary>
    public static bool Build(NativeArray<float3> positions, NativeArray<uint> indices, NativeArray<float3> cornerGradients,
                             Mesh target, Bounds bounds, float splitAngle) {
      if (target == null) throw new ArgumentNullException(nameof(target));
      int vertexCount = positions.Length;
      int cornerCount = indices.Length - (indices.Length % 3);
      int triangleCount = cornerCount / 3;
      if (vertexCount == 0 || triangleCount == 0) { Clear(target); return false; }
      bool useGradients = cornerGradients.IsCreated && cornerGradients.Length >= cornerCount;

      // cos of the split angle; anything below -1 means "never split".
      float cosThreshold = splitAngle >= 180f ? -2f : math.cos(math.radians(math.max(splitAngle, 0f)));

      var start = new NativeArray<int>(vertexCount + 1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
      var cursor = new NativeArray<int>(vertexCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
      var adjacency = new NativeArray<int>(cornerCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
      var status = new NativeArray<int>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
      var faceNormals = new NativeArray<float3>(triangleCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
      var cornerNormals = new NativeArray<float3>(cornerCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
      var cornerGroup = new NativeArray<int>(cornerCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
      var groupCount = new NativeArray<int>(vertexCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
      var extraBase = new NativeArray<int>(vertexCount + 1, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

      try {
        // Phase 1: vertex -> incident corner lists (also validates the indices).
        new BuildAdjacencyJob {
          indices = indices, cornerCount = cornerCount, vertexCount = vertexCount,
          start = start, cursor = cursor, adjacency = adjacency, status = status
        }.Schedule().Complete();
        if (status[0] != 0) {
          Debug.LogError("LFMeshBuilder: triangle index out of range; mesh discarded.");
          Clear(target);
          return false;
        }

        // Phase 2: corner normals (analytic or geometric), smoothing groups per vertex, and output
        // vertex numbering.
        JobHandle h = new FaceNormalsJob { positions = positions, indices = indices, faceNormals = faceNormals }
          .Schedule(triangleCount, 256);
        if (useGradients) {
          h = new CornerNormalsFromGradientsJob { gradients = cornerGradients, faceNormals = faceNormals, cornerNormals = cornerNormals }
            .Schedule(cornerCount, 512, h);
        } else {
          h = new CornerNormalsFromFacesJob { faceNormals = faceNormals, cornerNormals = cornerNormals }
            .Schedule(cornerCount, 512, h);
        }
        h = new ClusterJob {
          indices = indices, cornerNormals = cornerNormals, start = start, adjacency = adjacency,
          cosThreshold = cosThreshold, cornerGroup = cornerGroup, groupCount = groupCount
        }.Schedule(vertexCount, 64, h);
        h = new PrefixSumJob { groupCount = groupCount, extraBase = extraBase }.Schedule(h);
        h.Complete();

        int outputVertexCount = vertexCount + extraBase[vertexCount];

        // Phase 3: write straight into the Mesh's buffers.
        Mesh.MeshDataArray dataArray = Mesh.AllocateWritableMeshData(1);
        Mesh.MeshData data = dataArray[0];
        data.SetVertexBufferParams(outputVertexCount, VertexLayout);
        data.SetIndexBufferParams(cornerCount, IndexFormat.UInt32);
        NativeArray<Vertex> outVertices = data.GetVertexData<Vertex>(0);
        NativeArray<uint> outIndices = data.GetIndexData<uint>();

        // The two jobs only read the shared inputs, but they are chained (and each handle is
        // completed explicitly) so the collections safety system registers both as finished
        // before the caller disposes the input arrays: completing only a combined handle leaves
        // the readers marked as pending.
        JobHandle remap = new RemapIndicesJob {
          indices = indices, cornerGroup = cornerGroup, extraBase = extraBase, vertexCount = vertexCount,
          outIndices = outIndices
        }.Schedule(cornerCount, 512);
        JobHandle write = new WriteVerticesJob {
          positions = positions, indices = indices, cornerNormals = cornerNormals, start = start,
          adjacency = adjacency, cornerGroup = cornerGroup, extraBase = extraBase, vertexCount = vertexCount,
          outVertices = outVertices
        }.Schedule(vertexCount, 64, remap);
        write.Complete();
        remap.Complete();

        data.subMeshCount = 1;
        data.SetSubMesh(0, new SubMeshDescriptor(0, cornerCount, MeshTopology.Triangles) {
          bounds = bounds, firstVertex = 0, vertexCount = outputVertexCount
        }, ApplyFlags);
        Mesh.ApplyAndDisposeWritableMeshData(dataArray, target, ApplyFlags);
        target.bounds = bounds;

        LastInputVertexCount = vertexCount;
        LastOutputVertexCount = outputVertexCount;
        LastTriangleCount = triangleCount;
        LastUsedFeatureNormals = useGradients;
        return true;
      } finally {
        start.Dispose();
        cursor.Dispose();
        adjacency.Dispose();
        status.Dispose();
        faceNormals.Dispose();
        cornerNormals.Dispose();
        cornerGroup.Dispose();
        groupCount.Dispose();
        extraBase.Dispose();
      }
    }

    /// <summary>Empties the mesh without allocating.</summary>
    public static void Clear(Mesh target) {
      if (target == null) return;
      target.Clear(false);
      target.bounds = new Bounds();
    }

    /// <summary>
    /// Rebuilds the normals of an existing triangle mesh (submesh 0), splitting vertices along edges
    /// sharper than <paramref name="splitAngle"/> degrees. Only positions survive: other vertex
    /// attributes (UVs, colors, ...) are dropped. Use Unity's parameterless RecalculateNormals for
    /// plain smooth normals on an arbitrary mesh.
    /// </summary>
    public static void RecalculateNormals(this Mesh mesh, float splitAngle) {
      if (mesh == null) throw new ArgumentNullException(nameof(mesh));
      if (mesh.vertexCount == 0) return;
      if (mesh.subMeshCount != 1 || mesh.GetTopology(0) != MeshTopology.Triangles) {
        Debug.LogWarning("LFMeshBuilder.RecalculateNormals only supports a single triangle submesh; falling back to smooth normals.", mesh);
        mesh.RecalculateNormals();
        return;
      }

      NativeArray<float3> positions;
      NativeArray<uint> indices;
      using (Mesh.MeshDataArray readOnly = Mesh.AcquireReadOnlyMeshData(mesh)) {
        Mesh.MeshData d = readOnly[0];
        positions = new NativeArray<float3>(d.vertexCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        d.GetVertices(positions.Reinterpret<Vector3>());
        int indexCount = d.GetSubMesh(0).indexCount;
        var signedIndices = new NativeArray<int>(indexCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        d.GetIndices(signedIndices, 0);
        indices = signedIndices.Reinterpret<uint>();
      }
      try {
        Build(positions, indices, mesh, mesh.bounds, splitAngle);
      } finally {
        positions.Dispose();
        indices.Dispose();
      }
    }

    // ------------------------------------------------------------------------------------------------
    // Jobs

    [BurstCompile(FloatMode = FloatMode.Fast)]
    struct BuildAdjacencyJob : IJob {
      [ReadOnly] public NativeArray<uint> indices;
      public int cornerCount, vertexCount;
      /// <summary>CSR offsets: corners of vertex v are adjacency[start[v] .. start[v+1]).</summary>
      public NativeArray<int> start;
      public NativeArray<int> cursor;
      [WriteOnly] public NativeArray<int> adjacency;
      /// <summary>status[0] = 1 if an index is out of range.</summary>
      public NativeArray<int> status;

      public void Execute() {
        for (int c = 0; c < cornerCount; c++) {
          uint v = indices[c];
          if (v >= (uint)vertexCount) { status[0] = 1; return; }
          start[(int)v + 1] += 1;
        }
        for (int v = 0; v < vertexCount; v++) {
          start[v + 1] += start[v];
          cursor[v] = start[v];
        }
        for (int c = 0; c < cornerCount; c++) {
          int v = (int)indices[c];
          adjacency[cursor[v]] = c;
          cursor[v] += 1;
        }
      }
    }

    [BurstCompile(FloatMode = FloatMode.Fast)]
    struct FaceNormalsJob : IJobParallelFor {
      [ReadOnly] public NativeArray<float3> positions;
      [ReadOnly] public NativeArray<uint> indices;
      /// <summary>Unit face normals; zero for degenerate triangles.</summary>
      [WriteOnly] public NativeArray<float3> faceNormals;

      public void Execute(int t) {
        float3 a = positions[(int)indices[3 * t]];
        float3 b = positions[(int)indices[3 * t + 1]];
        float3 c = positions[(int)indices[3 * t + 2]];
        float3 n = math.cross(b - a, c - a);
        float len = math.length(n);
        faceNormals[t] = len > 1e-20f ? n / len : float3.zero;
      }
    }

    /// <summary>Geometric corner normals: every corner takes its triangle's face normal.</summary>
    [BurstCompile]
    struct CornerNormalsFromFacesJob : IJobParallelFor {
      [ReadOnly] public NativeArray<float3> faceNormals;
      [WriteOnly] public NativeArray<float3> cornerNormals;

      public void Execute(int c) { cornerNormals[c] = faceNormals[c / 3]; }
    }

    /// <summary>
    /// Analytic corner normals: the normalized field gradient sampled for that corner, falling back to
    /// the face normal where the gradient is zero, NaN or infinite (e.g. exactly on a min/max tie).
    /// </summary>
    [BurstCompile(FloatMode = FloatMode.Fast)]
    struct CornerNormalsFromGradientsJob : IJobParallelFor {
      [ReadOnly] public NativeArray<float3> gradients;
      [ReadOnly] public NativeArray<float3> faceNormals;
      [WriteOnly] public NativeArray<float3> cornerNormals;

      public void Execute(int c) {
        float3 g = gradients[c];
        float len2 = math.lengthsq(g);
        // NaN compares false on both sides; +inf fails the upper bound.
        bool valid = len2 > 1e-24f && len2 < float.MaxValue;
        cornerNormals[c] = valid ? g * math.rsqrt(len2) : faceNormals[c / 3];
      }
    }

    /// <summary>Per vertex: union-find over incident triangles connected by smooth shared edges.</summary>
    [BurstCompile(FloatMode = FloatMode.Fast)]
    unsafe struct ClusterJob : IJobParallelFor {
      [ReadOnly] public NativeArray<uint> indices;
      [ReadOnly] public NativeArray<float3> cornerNormals;
      [ReadOnly] public NativeArray<int> start;
      [ReadOnly] public NativeArray<int> adjacency;
      public float cosThreshold;
      /// <summary>Smoothing group of each corner, numbered from 0 per vertex. Each corner is written once, by its vertex.</summary>
      [NativeDisableParallelForRestriction] [WriteOnly] public NativeArray<int> cornerGroup;
      [WriteOnly] public NativeArray<int> groupCount;

      public void Execute(int v) {
        int begin = start[v], end = start[v + 1];
        int k = end - begin;
        if (k == 0) { groupCount[v] = 1; return; }
        int kk = math.min(k, MaxValence);

        int* parent = stackalloc int[MaxValence];
        for (int i = 0; i < kk; i++) parent[i] = i;

        for (int i = 0; i < kk; i++) {
          int ci = adjacency[begin + i];
          int ti = ci / 3, li = ci - 3 * ti;
          uint iA = indices[3 * ti + (li + 1) % 3];
          uint iB = indices[3 * ti + (li + 2) % 3];
          float3 ni = cornerNormals[ci];
          bool degenerateI = math.lengthsq(ni) == 0f;

          for (int j = i + 1; j < kk; j++) {
            int cj = adjacency[begin + j];
            int tj = cj / 3, lj = cj - 3 * tj;
            uint jA = indices[3 * tj + (lj + 1) % 3];
            uint jB = indices[3 * tj + (lj + 2) % 3];
            bool sharesEdge = iA == jA || iA == jB || iB == jA || iB == jB;
            if (!sharesEdge) continue;

            float3 nj = cornerNormals[cj];
            bool smooth = degenerateI || math.lengthsq(nj) == 0f || math.dot(ni, nj) >= cosThreshold;
            if (!smooth) continue;

            int ri = Find(parent, i), rj = Find(parent, j);
            if (ri != rj) parent[ri] = rj;
          }
        }

        int* groupOfRoot = stackalloc int[MaxValence];
        for (int i = 0; i < kk; i++) groupOfRoot[i] = -1;
        int next = 0;
        for (int i = 0; i < kk; i++) {
          int r = Find(parent, i);
          int g = groupOfRoot[r];
          if (g < 0) { g = next++; groupOfRoot[r] = g; }
          cornerGroup[adjacency[begin + i]] = g;
        }
        for (int i = kk; i < k; i++) cornerGroup[adjacency[begin + i]] = 0;
        groupCount[v] = next;
      }

      static int Find(int* parent, int i) {
        while (parent[i] != i) {
          parent[i] = parent[parent[i]];
          i = parent[i];
        }
        return i;
      }
    }

    [BurstCompile]
    struct PrefixSumJob : IJob {
      [ReadOnly] public NativeArray<int> groupCount;
      /// <summary>extraBase[v] = number of duplicated vertices created by vertices before v; extraBase[n] = total.</summary>
      [WriteOnly] public NativeArray<int> extraBase;

      public void Execute() {
        int n = groupCount.Length;
        int sum = 0;
        extraBase[0] = 0;
        for (int v = 0; v < n; v++) {
          sum += math.max(groupCount[v] - 1, 0);
          extraBase[v + 1] = sum;
        }
      }
    }

    [BurstCompile]
    struct RemapIndicesJob : IJobParallelFor {
      [ReadOnly] public NativeArray<uint> indices;
      [ReadOnly] public NativeArray<int> cornerGroup;
      [ReadOnly] public NativeArray<int> extraBase;
      public int vertexCount;
      [WriteOnly] public NativeArray<uint> outIndices;

      public void Execute(int c) {
        uint v = indices[c];
        int g = cornerGroup[c];
        outIndices[c] = g == 0 ? v : (uint)(vertexCount + extraBase[(int)v] + g - 1);
      }
    }

    /// <summary>Per vertex: angle-weighted group normals, written to the original slot (group 0) and the duplicates.</summary>
    [BurstCompile(FloatMode = FloatMode.Fast)]
    unsafe struct WriteVerticesJob : IJobParallelFor {
      [ReadOnly] public NativeArray<float3> positions;
      [ReadOnly] public NativeArray<uint> indices;
      [ReadOnly] public NativeArray<float3> cornerNormals;
      [ReadOnly] public NativeArray<int> start;
      [ReadOnly] public NativeArray<int> adjacency;
      [ReadOnly] public NativeArray<int> cornerGroup;
      [ReadOnly] public NativeArray<int> extraBase;
      public int vertexCount;
      /// <summary>Each output vertex is written exactly once, by the vertex it duplicates.</summary>
      [NativeDisableParallelForRestriction] [WriteOnly] public NativeArray<Vertex> outVertices;

      public void Execute(int v) {
        float3 p = positions[v];
        int begin = start[v], end = start[v + 1];
        int groups = extraBase[v + 1] - extraBase[v] + 1;

        float3* acc = stackalloc float3[MaxValence];
        float3* plain = stackalloc float3[MaxValence];
        for (int g = 0; g < groups; g++) { acc[g] = float3.zero; plain[g] = float3.zero; }

        for (int i = begin; i < end; i++) {
          int c = adjacency[i];
          int t = c / 3, l = c - 3 * t;
          float3 n = cornerNormals[c];
          if (math.lengthsq(n) == 0f) continue;
          int g = cornerGroup[c];
          plain[g] += n;
          float3 b = positions[(int)indices[3 * t + (l + 1) % 3]];
          float3 d = positions[(int)indices[3 * t + (l + 2) % 3]];
          float3 e1 = b - p, e2 = d - p;
          float l1 = math.length(e1), l2 = math.length(e2);
          if (l1 <= 0f || l2 <= 0f) continue;
          float angle = math.acos(math.clamp(math.dot(e1, e2) / (l1 * l2), -1f, 1f));
          acc[g] += n * angle;
        }

        for (int g = 0; g < groups; g++) {
          float3 n = acc[g];
          float len = math.length(n);
          if (len <= 1e-20f) {
            // Every triangle in the group is degenerate (zero angle at this corner): the corner
            // normals themselves are still meaningful, in particular analytic gradients.
            n = plain[g];
            len = math.length(n);
          }
          n = len > 1e-20f ? n / len : new float3(0f, 1f, 0f);
          int outIndex = g == 0 ? v : vertexCount + extraBase[v] + g - 1;
          outVertices[outIndex] = new Vertex { position = p, normal = n };
        }
      }
    }
  }
}
