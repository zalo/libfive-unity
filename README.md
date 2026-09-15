# libfive-unity

**Signed-distance CSG for Unity, powered by [libfive](https://github.com/libfive/libfive).**

[![Native libfive](https://github.com/zalo/libfive-unity/actions/workflows/native.yml/badge.svg)](https://github.com/zalo/libfive-unity/actions/workflows/native.yml)
[![Unity](https://github.com/zalo/libfive-unity/actions/workflows/unity.yml/badge.svg)](https://github.com/zalo/libfive-unity/actions/workflows/unity.yml)
[![Release](https://img.shields.io/github/v/release/zalo/libfive-unity?label=release)](https://github.com/zalo/libfive-unity/releases/latest)
[![License: MPL-2.0](https://img.shields.io/badge/license-MPL--2.0-blue.svg)](LICENSE)

Build shapes as expression trees with operator overloading and libfive's full standard library
(CSG, blends, primitives, transforms, arrays, text), mesh them with libfive's dual contouring on a
worker thread, and edit CSG trees directly in the Unity hierarchy.

<img src="https://i.imgur.com/XR8HxoL.gif"> <img src="https://i.imgur.com/BuKbAwc.gif">

## Highlights

- **Complete bindings** for the current libfive C API and its standard library, so every shape
  and operation from libfive Studio is one call away.
- **Hierarchy editor**: an `LFShape` component per node, live re-meshing in edit mode, pickable
  gizmos, undo, STL export, and rendering through a plain `MeshRenderer` in every pipeline.
- **Fast, allocation-free meshing**: libfive runs on a worker thread; Burst jobs build the Unity
  mesh with zero-copy views of the native buffers and no per-frame garbage.
- **Crisp normals from the field itself**: normals come from the analytic gradient of the distance
  field, sampled twice per triangle corner and extrapolated to the vertex, so curvature cancels out.
  Creases are split where the shape really has them, tightly curved surfaces never split, and smooth
  normals are exact. Costs about a fifth of the meshing time.
- **Safe lifetimes**: every tree is a single native reference with scoped disposal, double-dispose
  protection, and a finalizer backstop.
- **Cross-platform binaries** for Windows x64, macOS (universal) and Linux x64, built, tested and
  released automatically by GitHub Actions.

## Installation

Requires **Unity 6000.3 LTS** (Unity 6.3) or newer. Burst, Collections and Mathematics are
declared as package dependencies and installed automatically.

| Method | How |
| --- | --- |
| Unity Package Manager | *Window ▸ Package Manager ▸ + ▸ Add package from git URL* and paste `https://github.com/zalo/libfive-unity.git?path=/Assets/libfive`. Append `#vX.Y.Z` to pin a [release](https://github.com/zalo/libfive-unity/releases). |
| Zip | Download `libfive-unity-vX.Y.Z.zip` from the [latest release](https://github.com/zalo/libfive-unity/releases/latest) and unzip it into your project's `Assets` folder. |
| Clone | This repository is itself a Unity project containing the example scene. |

Release tags and `master` include the native plugin binaries under `Assets/libfive/Plugins/`.
Feature branches do not; grab them from the **Native libfive** workflow artifacts or build them
yourself (see [Native binaries](#native-binaries)). The `LFShape` inspector tells you when the
binary for your platform is missing.

## Editing shapes in the hierarchy

Add **GameObject ▸ libfive ▸ Shape**. Each `LFShape` node applies its operation to its enabled child
nodes, and each node's transform places its result inside its parent. The root node (the one without
an `LFShape` parent) gets a `MeshFilter` and `MeshRenderer`, so it renders in Built-in, URP and HDRP,
casts shadows, and can be moved, rotated and scaled without re-meshing.

| Kind | Operations | Notes |
| --- | --- | --- |
| Primitives (unit size, centered) | Circle, Sphere, Box, Cylinder, Torus, Cone, Pyramid, RoundedBox, Gyroid, HalfSpace, Text | libfive is Z-up: Cylinder, Cone and Pyramid extrude along the node's local Z. Circle and Text are 2D (infinite along Z); put them under an **Extrude** node. |
| Unary (applied to the union of the children) | Transform (group), Inverse, Mirror, Shell, Offset, Twist, Taper, Revolve, Extrude, RepeatPolar | |
| N-ary | Union, Intersection, Difference, Blend, Morph, Clearance, BlendDifference, Loft | Difference and Clearance subtract every later child from the first one. |

**Amount** is the operation's parameter (shell thickness, blend smoothness, twist radians, extrude
height, repeat count, ...). It resets to a sensible default when you change the operation, and the
inspector labels it accordingly.

Mesh settings on every node:

- **Bounds Size**: edge length of the cube that gets meshed. Geometry outside is clipped.
- **Resolution**: octree cells per unit of length. Cost grows roughly with its cube.
- **Vertex Splitting Angle**: creases sharper than this get hard normals, shallower ones stay smooth.
  With feature normals this is the crease angle itself, independent of resolution and curvature
  (default 10). 180 never splits.
- **Feature Normals**: derive normals and the split decision from the field's gradient instead of the
  mesh's face normals (on by default; see below).
- **Async Render**: mesh on a worker thread and swap the result in when ready.

In edit mode child nodes are meshed too so you can click and outline them in the Scene view; toggle
this under **Tools ▸ libfive**. The inspector shows vertex and triangle counts and the last meshing
time, and offers **Rebuild**, **Export STL…** and **Copy Tree** (libfive's text form of the shape).

## Scripting

```csharp
using libfivesharp;

Mesh mesh = new Mesh();
using (LFContext.Push()) {                       // frees every tree built inside when the block ends
  LFTree bore = LFMath.Cylinder(0.6f, 2f, new Vector3(0, 0, -1));
  LFTree shape = LFMath.Difference(LFMath.Sphere(1f), bore, bore.ReflectXZ(), bore.ReflectYZ());
  shape = LFMath.Blend(0.2f, shape, LFMath.Torus(1.1f, 0.1f).RotateX(Mathf.PI / 2));
  shape.RenderMesh(mesh, new Bounds(Vector3.zero, Vector3.one * 3f), resolution: 20f, vertexSplittingAngle: 30f);
}
```

To keep the frame rate smooth, schedule the meshing instead of blocking on it:

```csharp
LFMeshJob job = LFMeshJob.Schedule(shape, bounds, resolution: 20f);   // shape must stay alive until Complete/Dispose
// ...later, e.g. in Update():
if (job.IsCompleted) {
  job.Complete(mesh, vertexSplittingAngle: 30f);
  job.Dispose();
}
```

### API overview

- **Trees**: `LFTree.x`, `LFTree.y`, `LFTree.z`, constants (implicit from `float`), free variables
  (`LFTree.Var()`), the operators `+ - * / %` and unary `-`, and the math in `LFMath` (`Sqrt`, `Min`,
  `Max`, `Atan2`, ...). Inspect a tree with `Eval(point)`, `Gradient(point)`, interval `Eval(bounds)`,
  `ToString()`, `Save`/`Load`, `Optimized()` and `Remap`.
- **Standard library** (the same stdlib libfive Studio uses):
  primitives `Sphere`, `Box`, `BoxExact`, `RoundedBox`, `Cylinder`, `Cone`, `Pyramid`, `Torus`,
  `Gyroid`, `HalfSpace`, `Ellipsoid`, `Circle`, `Ring`, `Polygon`, `Rectangle`, `Triangle`, `Text`;
  CSG `Union`, `Intersection`, `Difference`, `Offset`, `Clearance`, `Shell`, `Blend`, `BlendExpt`,
  `BlendRough`, `BlendDifference`, `Morph`, `Loft`; transforms `Move`, `Transform(Matrix4x4)`,
  `Rotate*`, `Scale*`, `Reflect*`, `Symmetric*`, `Taper*`, `ShearXY`, `Repel`, `Attract`, `RevolveY`,
  `Twirl`, `Extrude`; and `ArrayX`/`ArrayXY`/`ArrayXYZ`/`ArrayPolar`. Every numeric argument also
  accepts a tree, so shapes can be parameterized.
- **Meshing**: `tree.RenderMesh(mesh, bounds, resolution, splitAngle)` synchronously, or `LFMeshJob`
  as above. `tree.SaveMesh(path, bounds, resolution)` writes an STL through libfive,
  `tree.RenderSlice(rect, z, resolution)` returns 2D contours, `tree.RenderPixels(...)` a bitmap.
  `resolution` is always octree cells per unit of length.
- **Normals**: `LFMeshBuilder` turns any triangle soup into a mesh with smoothing-group normals, and
  `mesh.RecalculateNormals(splitAngle)` applies the same algorithm to an existing mesh. With
  *feature normals* (the default for libfive meshes) the per-corner normal is the field's gradient,
  evaluated by a batched helper compiled into the plugin (`native/shim`) at two points along the
  corner-to-centroid segment and extrapolated linearly back to the vertex. That cancels the curvature
  term, so on a smooth patch every corner around a vertex reports the same normal however coarse the
  mesh, while across a crease neighbouring corners differ by the true dihedral angle. The split angle
  is therefore a real crease-angle threshold, not a tuning knob, and the split decision no longer
  depends on the noisy face normals of dual contouring's skinny triangles. Corners whose gradient is
  undefined fall back to the face normal, and plugins built without the helper fall back to geometric
  normals entirely (`LFNative.SupportsFeatureNormals`). `tree.Gradient(Vector3[])` exposes the same
  batched evaluation.
- **Export**: `LFMeshExport.WriteBinaryStl` / `WriteAsciiStl` write a Unity mesh with a transform
  applied, fixing the winding for mirroring transforms.
- **Lifetimes**: an `LFTree` owns one native reference. Build inside `using (LFContext.Push())` and
  `LFContext.Active.Detach(tree)` anything that must outlive the block, or `Dispose()` trees yourself.
  `LFMeshJob` borrows its tree; keep the tree alive until the job is completed or disposed.
- **Raw bindings**: `libfivesharp.libFiveInternal.libfive` and `libfive_stdlib` mirror `libfive.h`
  and `libfive_stdlib.h` one to one, for anything the managed layer does not wrap.

`Assets/libfive/Examples/LibFiveExample.cs` is a runtime example, and the EditMode tests in
`Assets/libfive/Tests/Editor/` double as executable documentation.

## Native binaries

`native/` builds libfive at a pinned commit with its standard library folded into a single shared
library per platform. The binaries link libpng and zlib statically (plus the C runtime on Windows and
libstdc++ on Linux) and are compiled without `-march=native`, so they run on any x86_64 machine and
on Apple silicon.

| Platform | File | Notes |
| --- | --- | --- |
| Windows | `Plugins/Windows/x86_64/libfive.dll` | x64, static CRT, no VC++ redistributable needed |
| macOS | `Plugins/macOS/libfive.dylib` | universal arm64 + x86_64, macOS 11+, ad-hoc signed |
| Linux | `Plugins/Linux/x86_64/libfive.so` | depends only on glibc and libm |

To build locally, clone libfive to `native/libfive`, install [vcpkg](https://github.com/microsoft/vcpkg),
and run `native/build.sh <triplet>` (Linux/macOS) or `native\build.ps1` from a Developer PowerShell
(Windows). `python native/install_plugin.py --platform <windows-x64|macos-universal|linux-x64> --binary <file>`
copies a binary into place with the right `.meta`. The libfive commit and the vcpkg baseline are pinned
in `.github/workflows/native.yml` and `native/vcpkg.json`; bump them together.

## Continuous integration and releases

Both workflows run on every push to every branch and on pull requests from forks.

- **Native libfive** builds the three binaries and uploads them as run artifacts
  (`plugin-windows-x64`, `plugin-macos-universal`, `plugin-linux-x64`). vcpkg's binary cache and
  sccache keep warm builds to a couple of minutes; only the first build after a dependency change is slow.
- **Unity** runs the EditMode tests and builds Windows, macOS and Linux players with
  [GameCI](https://game.ci), uploading the players and the test results as artifacts. It needs a
  Unity license in the repository secrets (`UNITY_LICENSE`, or `UNITY_EMAIL` + `UNITY_PASSWORD`
  [+ `UNITY_SERIAL`]) and skips itself with a notice otherwise.
- **Releases are automatic.** Every push to `master` that passes the native builds commits the fresh
  binaries into `Assets/libfive/Plugins/`, bumps the patch version in `Assets/libfive/package.json`,
  tags `vX.Y.Z` and publishes a GitHub Release with `libfive-unity-vX.Y.Z.zip` (a drop-in
  `Assets/libfive` folder) plus one zip per platform.

## Developing without Unity

`tools/harness/` is a small .NET project that compiles all the C# against Unity API stubs and runs the
test suite headlessly, including the native tests against a locally built `libfive.so`:

```bash
cd tools/harness && dotnet run -c Release          # all tests
cd tools/harness && dotnet run -c Release Sphere   # tests whose name contains "Sphere"
```

It catches compile errors, marshalling mistakes and normal-generation regressions in seconds. Its stubs
only approximate Unity (jobs run synchronously), so the real EditMode tests in CI remain the final word.

## Project layout

```
Assets/libfive/
  Plugins/          libfive.cs, libfive_stdlib.cs (P/Invoke) and the native binaries per platform
  Scripts/          LFTree (trees, LFMath), LFContext, LFMeshBuilder, LFMeshRendering, LFMeshExport, LFShape
  Scripts/Editor/   LFShape inspector, gizmos, menus
  Tests/Editor/     EditMode tests (NUnit)
  Examples/         LibFiveExample scene and script
  package.json      UPM manifest
native/             CMake wrapper, vcpkg manifest, build scripts, plugin installer, CI checks
tools/harness/      headless .NET test harness with Unity API stubs
.github/workflows/  native.yml (binaries + releases), unity.yml (tests + players)
```

## Upgrading from the original wrapper

- The native plugin is now called `libfive` on every platform; the old Windows-only `five.dll`
  binaries (including 32-bit) are gone. Delete stale copies from `Assets/libfive/Plugins/`.
- `LibFive_Operation` values are unchanged, so existing scenes keep their shapes. New operations were
  appended to each band.
- `using (LFContext.Active = new Context())` still works; `using (LFContext.Push())` is the shorter
  form. A finalizer frees trees you forget, but explicit scopes keep memory flat.
- The bindings track the current `libfive.h`: `libfive_tree_nonary` became `libfive_tree_nullary`,
  and bool-returning functions are marshalled correctly. Code that used the raw bindings directly
  should be rechecked against the new declarations.

## Notes

- Build trees on the main thread. Meshing jobs run on worker threads, and libfive itself fans out
  across all cores.
- libfive's mesher can produce non-manifold output and always emits an unused placeholder vertex 0;
  `LFMeshBuilder` handles both.
- Coordinates are passed to Unity as-is (libfive is Z-up, right-handed). Meshes render correctly because
  the handedness flip preserves front faces; rotate the root node if you want Y-up.

## License and credits

libfive is written by [Matt Keeter](https://www.mattkeeter.com/projects/libfive/). Both libfive and
this wrapper are licensed under the [Mozilla Public License 2.0](LICENSE).
