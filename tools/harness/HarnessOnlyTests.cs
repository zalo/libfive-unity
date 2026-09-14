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
