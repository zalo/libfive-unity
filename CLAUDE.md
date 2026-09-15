# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

A C#/Unity wrapper around [libfive](https://github.com/libfive/libfive), an implicit-surface (signed distance field) CAD kernel. Shapes are expression trees built through P/Invoke into a native `libfive` shared library (libfive's core C API plus its standard library, folded into one binary by `native/CMakeLists.txt`), meshed with libfive's dual contouring, and turned into Unity meshes with Burst jobs. Everything Unity-side lives under `Assets/libfive/` in the `libfivesharp` namespace; the Unity project targets **Unity 6000.3 LTS** (`ProjectSettings/ProjectVersion.txt`).

## Commands

There is no Unity installation on the development machine; Unity itself only runs in CI (GameCI, `.github/workflows/unity.yml`, needs license secrets). What can run locally:

```bash
# Build the native library on Linux (needs Eigen + Boost headers, libpng; see native/build.sh for the vcpkg flow)
git clone --depth 1 https://github.com/libfive/libfive /tmp/libfive
cmake -S native -B native/build/x64-linux -DLIBFIVE_SOURCE_DIR=/tmp/libfive -DCMAKE_BUILD_TYPE=Release \
      -DBoost_INCLUDE_DIR=<boost headers> && cmake --build native/build/x64-linux --target libfive -j
# -> native/build/x64-linux/libfive/libfive/src/libfive.so

# Compile every C# file against Unity API stubs and run all [Test]s (incl. native bindings) headlessly
cd tools/harness && dotnet run -c Release                 # picks up the .so above if present
cd tools/harness && dotnet run -c Release Sphere          # only tests whose name contains "Sphere"
cd tools/harness && dotnet build -c Release -p:PlayerBuild=true   # runtime assembly only, no UNITY_EDITOR
```

The harness (`tools/harness/`) is the fastest feedback loop: it catches compile errors, marshalling mistakes, normal-generation regressions and pointer or reference-type fields in job structs, but its stubs approximate Unity (jobs run synchronously, `NativeArray` is a byte buffer), so Unity-specific API usage still has to be right by construction. When adding Unity API calls, add matching members to `tools/harness/Stubs/` so the harness keeps compiling. Real Unity tests live in `Assets/libfive/Tests/Editor/` (EditMode, NUnit); native-dependent tests `Assert.Ignore` when the plugin binary is missing.

Native binaries are produced by the **Native libfive** workflow (`.github/workflows/native.yml`), which runs on every push to every branch and uploads `plugin-*` artifacts. On pushes to `master` its `release` job commits the binaries into `Assets/libfive/Plugins/<Platform>/...` (with `.meta` files via `native/install_plugin.py`), bumps the patch version in `Assets/libfive/package.json` (the UPM manifest), tags `vX.Y.Z` with a `[skip ci]` commit, and publishes a GitHub Release with zips. The libfive commit and the vcpkg baseline are pinned in the workflow and in `native/vcpkg.json`; bump both together. Two caches keep the jobs fast: vcpkg's binary cache (dependencies; keyed on `native/vcpkg.json` + triplets, restored/saved explicitly so it survives failed builds) and sccache with the GitHub Actions cache backend (libfive object files; Windows builds with Ninja so the launcher applies). A cold build is ~50 min per platform, a warm one a few minutes; changing `native/vcpkg.json` or the vcpkg commit invalidates the dependency cache. The **Unity** workflow (GameCI tests + player builds) also runs on every push but skips itself unless Unity license secrets exist.

## Architecture

**Bindings** (`Assets/libfive/Plugins/libfive.cs`, `libfive_stdlib.cs`, namespace `libfivesharp.libFiveInternal`): 1:1 `DllImport("libfive")` declarations for `libfive.h` and `libfive_stdlib.h`. Rules that matter: C `bool` is marshalled as `I1`; structs use the natural layout (no `Pack=1`, `libfive_contour` is read from arrays with the C stride); every returned tree is a new reference that must be released with `libfive_tree_delete`, and passing a tree as an argument never transfers ownership (libfive increments refcounts internally, atomically). `libfive_tree_nonary` is now `libfive_tree_nullary` upstream.

**Managed layer** (`Assets/libfive/Scripts/`):
- `LFTree.cs`: `LFNative` (availability/version probe), `LFOpcode` (opcode values resolved at startup with `libfive_opcode_enum`, so a libfive built with different numbering still works), `LFVec2/LFVec3` (tree-valued vectors, implicit from `Vector2/3`), `LFTree` (one native reference; double-dispose safe; finalizer fallback; `Eval`, `Gradient`, interval `Eval(Bounds)`, `Remap`, `Optimized`, `ToString`, `Save/Load`, `RenderMesh`, `RenderSlice`, `RenderPixels`), and `LFMath` (ops, CSG, primitives, transforms, arrays, text; primitives/CSG/transforms call the stdlib so they match libfive Studio). Every native call in `LFMath` goes through `LFTree.From(result, args...)`, which `GC.KeepAlive`s the argument trees: outside a `Context`, a temporary `LFTree` could otherwise be finalized (and its native node freed) before the native call reads its handle. Keep that pattern for new wrappers.
- `LFContext.cs`: `Context` is a `[ThreadStatic]` scope that owns every `LFTree` created inside it. `using (LFContext.Push()) { ...; return LFContext.Active.Detach(result); }` is the idiom; the old `using (LFContext.Active = new Context())` still works.
- `LFMeshBuilder.cs`: Burst jobs that turn positions + indices (from a `libfive_mesh*` viewed as `NativeArray` without copying, or any Mesh) into a Unity mesh via `Mesh.AllocateWritableMeshData`. Normals are smoothing groups: per vertex, incident triangles are union-found along shared edges whose face-normal angle is below the split angle, each extra group duplicates the vertex, and group normals are angle-weighted. `mesh.RecalculateNormals(float)` is the extension entry point. libfive always emits an unreferenced placeholder vertex 0; it is kept.
- `LFMeshRendering.cs`: `LFMeshJob` runs `libfive_tree_render_mesh` in an `IJob` (result pointer in a persistent `NativeArray<IntPtr>`), `Complete(mesh, angle)` builds the mesh and frees the native one. The job *borrows* its tree: callers must not dispose the tree until `Complete`/`Dispose`.
- `LFMeshExport.cs`: binary/ASCII STL (culture-invariant, flips winding for mirroring transforms).
- `LFShape.cs`: `[ExecuteAlways]` hierarchy node. Each node's `Evaluate()` returns its tree in *parent* space (local TRS applied via `LFMath.Transform`); the root returns root-local space and is drawn by a `MeshFilter`/`MeshRenderer` it adds itself, so root transforms never re-mesh. Dirty tracking: `MarkDirty()` walks up to the root; triggers are inspector edits (`OnValidate`), child transform changes (compared exactly each `Update`), hierarchy/enable changes, and undo (hooked in the editor assembly). A node never replaces its `tree` while an `LFMeshJob` references it (`retiredTree`). Child nodes are meshed only in the editor, in edit mode, for pickable gizmos (`LFShape.PreviewChildren`). `LibFive_Operation` values are serialized into scenes: 0–99 primitives, 100–199 unary (applied to the union of children; `Transform`=0 is a plain group), 200+ n-ary. Add new ops at the end of a band, never renumber, and extend `BuildPrimitive`/`ApplyUnary`/`ApplyNary` plus `DefaultAmount`/`UsesAmount`/`AmountLabel`.
- `Editor/LFShapeEditor.cs` (assembly `libfivesharp.Editor`): custom inspector, pickable/outline gizmos (`[DrawGizmo]`), undo hook, `Tools ▸ libfive` and `GameObject ▸ libfive` menus.

Assembly definitions: `Assets/libfive/libfivesharp.asmdef` (runtime, unsafe code, references Burst/Collections/Mathematics), `Scripts/Editor/libfivesharp.Editor.asmdef`, `Tests/Editor/libfivesharp.Tests.asmdef`. The managed assemblies are deliberately *not* named `libfive`: Unity refuses a native plugin and a managed assembly with the same filename (`libfive.dll`); `native/check_assembly_names.py` enforces this in the Unity and release jobs.

## Conventions and gotchas

- libfive is Z-up (`Cylinder`, `Cone`, `Extrude` work along Z); Unity meshes are uploaded without axis conversion and render correctly because the handedness flip preserves front faces. STL export writes coordinates as-is.
- `resolution` everywhere means octree cells per unit length (libfive's `min_feature = 1/res`), not feature size.
- Every asset under `Assets/` needs a `.meta` with a stable GUID (the example scene references `LFShape.cs.meta`'s GUID). New files: generate a `.meta` (see `native/install_plugin.py` for the plugin/folder templates) rather than letting Unity churn the repo.
- Job structs may only hold `IntPtr`/pointer fields when marked `[NativeDisableUnsafePtrRestriction]`; the job system throws on schedule otherwise (the harness stubs replicate this check).
- Native crashes come from lifetime mistakes: never call `libfive_tree_delete` on a handle you did not receive as a return value, never read a `libfive_mesh*` after `libfive_mesh_delete`, and never dispose an `LFTree` that a scheduled `LFMeshJob` still borrows.
