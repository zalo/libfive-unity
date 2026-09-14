libfive-unity
=============

### A C#/Unity wrapper for the [libfive](https://github.com/libfive/libfive) implicit-surface CAD kernel.

Build shapes as signed-distance expression trees with operator overloading and libfive's full
standard library (CSG, blends, primitives, transforms, arrays, text), mesh them with libfive's
dual contouring on a worker thread, and edit CSG trees directly in the Unity hierarchy.

<img src="https://i.imgur.com/XR8HxoL.gif"> <img src="https://i.imgur.com/BuKbAwc.gif">

## Installing

* **Unity Package Manager**: *Add package from git URL* →
  `https://github.com/zalo/libfive-unity.git?path=/Assets/libfive#v1.0.0` (pick a tag from the
  [releases](https://github.com/zalo/libfive-unity/releases)).
* **Zip**: download `libfive-unity-vX.Y.Z.zip` from the latest release and unzip it into your
  project's `Assets` folder.
* **Clone**: this repository is itself a Unity project with the example scene.

## Requirements

* **Unity 6000.3 LTS** (Unity 6.3) or newer. Burst, Collections and Mathematics are pulled in by
  `Packages/manifest.json`.
* A native `libfive` plugin binary for your platform under `Assets/libfive/Plugins/`:
  Windows x86_64, macOS (universal arm64 + x86_64) and Linux x86_64 are supported.
  They are produced by the **Native libfive** GitHub Actions workflow (see below); the `LFShape`
  inspector tells you if the binary for your platform is missing.

## Editing shapes in the hierarchy

Add **GameObject ▸ libfive ▸ Shape**. Each `LFShape` node applies its operation to its enabled child
`LFShape` nodes, and each node's transform places its result inside its parent. The root node (the
one without an `LFShape` parent) gets a `MeshFilter`/`MeshRenderer`, so it renders in every pipeline
(Built-in, URP, HDRP), casts shadows, and can be moved, rotated and scaled without re-meshing.

| Kind | Operations | Notes |
| --- | --- | --- |
| Primitives (unit size, centered) | Circle, Sphere, Box, Cylinder, Torus, Cone, Pyramid, RoundedBox, Gyroid, HalfSpace, Text | libfive is Z-up: Cylinder/Cone/Pyramid extrude along the node's local Z. Circle and Text are 2D (infinite along Z); put them under an **Extrude** node. |
| Unary (applied to the union of the children) | Transform (group), Inverse, Mirror, Shell, Offset, Twist, Taper, Revolve, Extrude, RepeatPolar | |
| N-ary | Union, Intersection, Difference, Blend, Morph, Clearance, BlendDifference, Loft | Difference/Clearance subtract everything after the first child from the first child. |

`Amount` is the operation's parameter (shell thickness, blend smoothness, twist radians, extrude
height, repeat count, ...); it resets to a sensible default when you change the operation.

Mesh settings on every node: **Bounds Size** (the cube that gets meshed), **Resolution** (octree
cells per unit; cost grows roughly with its cube), **Vertex Splitting Angle** (edges sharper than this
get hard normals; 180 = smooth) and **Async Render** (mesh on a worker thread and swap the result in
when ready). In edit mode child nodes are meshed too so you can click them in the Scene view; toggle
that under **Tools ▸ libfive**. The inspector shows vertex/triangle counts and the last meshing time,
and can export a binary STL or copy the tree as text.

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

* **Trees**: `LFTree.x/y/z`, constants (implicit from `float`), `+ - * / %`, unary `-`, and everything
  in `LFMath` (`Sqrt`, `Min`, `Max`, `Atan2`, ...). `tree.Eval(point)`, `tree.Gradient(point)`,
  `tree.Eval(bounds)` (interval arithmetic), `tree.ToString()`, `Save`/`Load`, `Optimized()`, `Remap`.
* **Standard library** (matches libfive Studio): primitives (`Sphere`, `Box`, `BoxExact`, `RoundedBox`,
  `Cylinder`, `Cone`, `Pyramid`, `Torus`, `Gyroid`, `HalfSpace`, `Circle`, `Ring`, `Polygon`,
  `Rectangle`, `Triangle`, `Text`, ...), CSG (`Union`, `Intersection`, `Difference`, `Offset`,
  `Clearance`, `Shell`, `Blend`, `BlendExpt`, `BlendRough`, `BlendDifference`, `Morph`, `Loft`),
  transforms (`Move`, `Transform(Matrix4x4)`, `Rotate*`, `Scale*`, `Reflect*`, `Symmetric*`,
  `Taper*`, `ShearXY`, `Repel`, `Attract`, `RevolveY`, `Twirl`), and `ArrayX/XY/XYZ/Polar`.
  Every numeric argument also accepts trees, so shapes can be parameterized by `LFTree.Var()`.
* **Meshing**: `tree.RenderMesh(mesh, bounds, resolution, splitAngle)` (synchronous, no garbage), or
  `LFMeshJob.Schedule(tree, bounds, resolution)` then `job.Complete(mesh, splitAngle)` /
  `job.Dispose()` once `job.IsCompleted` to keep the frame rate smooth. `resolution` is octree cells
  per unit of length. `tree.RenderSlice(rect, z, resolution)` returns 2D contours,
  `tree.SaveMesh(path, bounds, resolution)` writes an STL through libfive.
* **Normals**: `LFMeshBuilder` turns any triangle soup into a mesh with smoothing-group normals
  (Burst jobs, zero managed allocations); `mesh.RecalculateNormals(splitAngle)` applies it to an
  existing mesh.
* **Lifetimes**: `LFTree` owns one native reference. Build inside `using (LFContext.Push())` and
  `LFContext.Active.Detach(tree)` anything that must outlive the block, or `Dispose()` trees yourself
  (double disposal is safe; the finalizer is a fallback). `LFMeshJob` borrows its tree: keep the tree
  alive until the job is completed or disposed.
* Low-level P/Invoke declarations live in `libfivesharp.libFiveInternal` (`libfive`, `libfive_stdlib`).

See `Assets/libfive/Examples/LibFiveExample.cs` for a runtime example and
`Assets/libfive/Tests/Editor/` for executable documentation of the API.

## Native binaries

`native/` builds libfive (pinned commit in `.github/workflows/native.yml`) with its standard library
folded into a single shared library, statically linked against libpng/zlib (and libstdc++ on Linux)
and without `-march=native`, so the binaries run on any x86_64 machine.

* **CI**: the **Native libfive** workflow runs on every push to every branch and uploads the three
  binaries as run artifacts (`plugin-windows-x64`, `plugin-macos-universal`, `plugin-linux-x64`).
  On pushes to `master` it also commits the binaries to the repo, bumps the patch version in
  `Assets/libfive/package.json`, tags `vX.Y.Z` and publishes a GitHub Release with
  `libfive-unity-vX.Y.Z.zip` (drop-in `Assets/libfive` folder) plus one zip per platform.
  To copy artifacts in by hand: `python native/install_plugin.py --platform <windows-x64|macos-universal|linux-x64> --binary <file>`.
* **Locally**: clone libfive to `native/libfive`, install [vcpkg](https://github.com/microsoft/vcpkg),
  then `native/build.sh x64-linux` / `native/build.sh arm64-osx` or `native\build.ps1` on Windows.
  See the scripts for details.

## Continuous integration

* Both workflows run on every push to every branch (and on pull requests from forks).
* **Unity** workflow: EditMode tests (`Assets/libfive/Tests`) and standalone player builds via
  [GameCI](https://game.ci), uploaded as run artifacts. It needs a Unity license in the repository
  secrets (`UNITY_LICENSE`, or `UNITY_EMAIL` + `UNITY_PASSWORD` [+ `UNITY_SERIAL`]) and skips itself
  with a notice otherwise.
* `tools/harness`: a small .NET project that compiles all the C# against Unity API stubs and runs the
  tests headlessly against a locally built `libfive.so` (`dotnet run -c Release`). Handy for checking
  bindings and the normal generation without opening Unity; not a replacement for the Editor.

## Notes

* The wrapper is not thread-safe: build trees on the main thread. Meshing jobs run on workers, and
  libfive itself fans out across cores.
* libfive's mesher can produce non-manifold output and always reserves vertex 0 as an unused
  placeholder; `LFMeshBuilder` tolerates both.
* Windows 32-bit is no longer supported (the old `five.dll` binaries were removed; the plugin is now
  named `libfive`).

libfive and this wrapper are licensed under the Mozilla Public License 2.0 (see `LICENSE`).
