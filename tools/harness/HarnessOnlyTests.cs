// Extra checks that only run in the .NET harness (they lean on GC behaviour / process memory,
// which is not meaningful inside Unity's test runner).
using System;
using NUnit.Framework;
using UnityEngine;

namespace libfivesharp.Tests {
  public class HarnessOnlyTests {
    [SetUp]
    public void RequireNative() {
      if (!LFNative.IsAvailable) Assert.Ignore("Native libfive plugin is not available.");
    }

    [Test]
    public void TreesWithoutAContextAreReleasedByFinalizersWithoutCrashing() {
      Assert.IsNull(LFContext.Active);
      for (int round = 0; round < 20; round++) {
        for (int i = 0; i < 2000; i++) {
          LFTree t = LFMath.Sphere(1f) + 0.1f * LFTree.x;
          if ((i & 255) == 0) Assert.Less(t.Eval(Vector3.zero), 0f);
        }
        GC.Collect();
        GC.WaitForPendingFinalizers();
      }
    }


    /// <summary>Prints the cost of feature normals relative to plain meshing. Not an assertion-heavy test.</summary>
    [Test]
    public void ProfileFeatureNormals() {
      if (!LFNative.SupportsFeatureNormals) Assert.Ignore("Plugin binary lacks the libfive-unity gradient helpers.");
      var mesh = new Mesh();
      var bounds = new Bounds(Vector3.zero, Vector3.one * 3f);
      using (LFContext.Push()) {
        LFTree bore = LFMath.Cylinder(0.6f, 2f, new Vector3(0, 0, -1));
        LFTree shape = LFMath.Difference(LFMath.Sphere(1f), bore, bore.ReflectXZ(), bore.ReflectYZ());
        shape = LFMath.Blend(0.2f, shape, LFMath.Torus(1.1f, 0.1f).RotateX(Mathf.PI / 2));
        shape = LFMath.Difference(shape, LFMath.Box(-Vector3.one * 0.5f, Vector3.one * 0.5f).RotateX(0.4f).RotateY(0.7f));
        Console.WriteLine("    res  feature  triangles   libfive   gradients   build(harness, no Burst)  gradient/libfive");
        foreach (float res in new[] { 16f, 32f, 64f }) {
          foreach (bool feature in new[] { false, true }) {
            double render = double.MaxValue, grads = double.MaxValue, build = double.MaxValue;
            int tris = 0;
            for (int rep = 0; rep < 3; rep++) {
              using (LFMeshJob job = LFMeshJob.Schedule(shape, bounds, res, false, feature)) {
                Assert.IsTrue(job.Complete(mesh, 30f));
                render = Math.Min(render, job.RenderMilliseconds);
                grads = Math.Min(grads, job.GradientMilliseconds);
                build = Math.Min(build, job.BuildMilliseconds);
                tris = LFMeshBuilder.LastTriangleCount;
              }
            }
            Console.WriteLine($"    {res,3}  {feature,-7}  {tris,9:N0}  {render,7:F1}ms  {grads,8:F1}ms  {build,10:F1}ms               {(feature ? grads / render * 100 : 0),5:F0}%");
          }
        }
      }
    }

    [Test]
    public void RepeatedMeshingDoesNotLeakNativeMemory() {
      var mesh = new Mesh();
      var bounds = new Bounds(Vector3.zero, Vector3.one * 2.5f);
      long Rss() { GC.Collect(); GC.WaitForPendingFinalizers(); return System.Diagnostics.Process.GetCurrentProcess().WorkingSet64; }

      // Warm up (thread pools, allocator arenas) before measuring.
      for (int i = 0; i < 10; i++) {
        using (LFContext.Push()) LFMath.Difference(LFMath.Sphere(1f), LFMath.Box(-Vector3.one * 0.5f, Vector3.one * 0.5f)).RenderMesh(mesh, bounds, 20f, 30f);
      }
      int iterations = int.TryParse(Environment.GetEnvironmentVariable("LF_STRESS_ITERATIONS"), out int n) ? n : 150;
      long before = Rss();
      for (int i = 0; i < iterations; i++) {
        using (LFContext.Push()) {
          LFTree shape = LFMath.Difference(LFMath.Sphere(1f), LFMath.Box(-Vector3.one * 0.5f, Vector3.one * 0.5f));
          using (LFMeshJob job = LFMeshJob.Schedule(shape, bounds, 20f)) {
            Assert.IsTrue(job.Complete(mesh, 30f));
          }
        }
      }
      long after = Rss();
      Console.WriteLine($"    working set: {before / 1e6:F1} MB -> {after / 1e6:F1} MB over {iterations} meshings ({mesh.vertexCount} verts each)");
      Assert.Less(after - before, 64L * 1024 * 1024, "working set grew by more than 64 MB");
    }

    [Test]
    public void ContextDisposalReleasesEveryTemporary() {
      Context ctx = LFContext.Push();
      LFTree result;
      try {
        result = ctx.Detach(LFMath.Union(LFMath.Sphere(1f), LFMath.Box(Vector3.zero, Vector3.one), LFMath.Torus(1f, 0.2f)));
        Assert.Greater(ctx.Count, 10);
      } finally {
        ctx.Dispose();
      }
      Assert.AreEqual(0, ctx.Count);
      Assert.IsFalse(result.IsDisposed);
      Assert.Less(result.Eval(Vector3.zero), 0f);
      result.Dispose();
    }
  }
}
