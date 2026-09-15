using NUnit.Framework;
using UnityEngine;
using libfivesharp.libFiveInternal;

namespace libfivesharp.Tests {
  /// <summary>
  /// Exercises the native binding layer and LFMath against the real libfive plugin. These tests are
  /// skipped (Assert.Ignore) on platforms where the plugin binary is missing.
  /// </summary>
  public class LFNativeTests {
    const float Tolerance = 1e-4f;

    [SetUp]
    public void RequireNative() {
      if (!LFNative.IsAvailable) Assert.Ignore("Native libfive plugin is not available on this platform.");
    }

    [Test]
    public void VersionStringsAreReadable() {
      Assert.IsNotNull(LFNative.Revision);
      Assert.IsTrue(LFOpcode.ResolvedFromNative);
    }

    [Test]
    public void OpcodesMatchTheDefaultEnum() {
      // The binary shipped with this repo is built without LIBFIVE_PACKED_OPCODES.
      Assert.AreEqual((int)libfive_opcode.OP_MIN, LFOpcode.Min);
      Assert.AreEqual((int)libfive_opcode.OP_MAX, LFOpcode.Max);
      Assert.AreEqual((int)libfive_opcode.OP_ABS, LFOpcode.Abs);
      Assert.AreEqual((int)libfive_opcode.OP_NTH_ROOT, LFOpcode.NthRoot);
      Assert.AreEqual(2, libfive.libfive_opcode_args(LFOpcode.Min));
      Assert.AreEqual(1, libfive.libfive_opcode_args(LFOpcode.Sqrt));
    }

    [Test]
    public void ConstantsAndVariablesRoundTrip() {
      using (LFContext.Push()) {
        LFTree c = 2.5f;
        Assert.IsTrue(c.TryGetConstant(out float value));
        Assert.AreEqual(2.5f, value, Tolerance);
        Assert.IsFalse(c.IsVar);

        LFTree v = LFTree.Var();
        Assert.IsTrue(v.IsVar);
        Assert.IsFalse(LFTree.x.IsVar);
        Assert.AreEqual(3f, LFTree.x.Eval(new Vector3(3f, 4f, 5f)), Tolerance);
        Assert.AreEqual(4f, LFTree.y.Eval(new Vector3(3f, 4f, 5f)), Tolerance);
        Assert.AreEqual(5f, LFTree.z.Eval(new Vector3(3f, 4f, 5f)), Tolerance);
      }
    }

    [Test]
    public void OperatorsEvaluateCorrectly() {
      using (LFContext.Push()) {
        LFTree f = (LFTree.x + 1f) * 2f - LFTree.y / 4f;
        Assert.AreEqual((3f + 1f) * 2f - 8f / 4f, f.Eval(new Vector3(3f, 8f, 0f)), Tolerance);
        Assert.AreEqual(-3f, (-LFTree.x).Eval(new Vector3(3f, 0f, 0f)), Tolerance);
        Assert.AreEqual(1f, (LFTree.x % 2f).Eval(new Vector3(7f, 0f, 0f)), Tolerance);
        Assert.AreEqual(Mathf.Sqrt(9f), LFMath.Sqrt(LFTree.x).Eval(new Vector3(9f, 0f, 0f)), Tolerance);
        Assert.AreEqual(2f, LFMath.Min(LFTree.x, LFTree.y).Eval(new Vector3(5f, 2f, 0f)), Tolerance);
        Assert.AreEqual(5f, LFMath.Max(LFTree.x, LFTree.y).Eval(new Vector3(5f, 2f, 0f)), Tolerance);
        Assert.AreEqual(2f, LFMath.NthRoot(LFTree.x, 3f).Eval(new Vector3(8f, 0f, 0f)), Tolerance);
      }
    }

    [Test]
    public void SphereIsASignedDistanceField() {
      using (LFContext.Push()) {
        LFTree sphere = LFMath.Sphere(1f);
        Assert.AreEqual(-1f, sphere.Eval(Vector3.zero), Tolerance);
        Assert.AreEqual(0f, sphere.Eval(new Vector3(1f, 0f, 0f)), Tolerance);
        Assert.AreEqual(1f, sphere.Eval(new Vector3(2f, 0f, 0f)), Tolerance);

        LFTree moved = LFMath.Sphere(0.5f, new Vector3(1f, 2f, 3f));
        Assert.AreEqual(-0.5f, moved.Eval(new Vector3(1f, 2f, 3f)), Tolerance);

        Vector3 grad = sphere.Gradient(new Vector3(0f, 0f, 2f));
        Assert.AreEqual(1f, grad.z, 1e-3f);
        Assert.AreEqual(0f, grad.x, 1e-3f);
      }
    }

    [Test]
    public void CsgOperationsCombineFields() {
      using (LFContext.Push()) {
        LFTree a = LFMath.Sphere(1f);
        LFTree b = LFMath.Sphere(1f, new Vector3(1f, 0f, 0f));
        Vector3 p = new Vector3(1.5f, 0f, 0f);
        Assert.AreEqual(Mathf.Min(a.Eval(p), b.Eval(p)), LFMath.Union(a, b).Eval(p), Tolerance);
        Assert.AreEqual(Mathf.Max(a.Eval(p), b.Eval(p)), LFMath.Intersection(a, b).Eval(p), Tolerance);
        Assert.AreEqual(Mathf.Max(a.Eval(p), -b.Eval(p)), LFMath.Difference(a, b).Eval(p), Tolerance);
        Assert.AreEqual(-a.Eval(p), LFMath.Inverse(a).Eval(p), Tolerance);
        Assert.AreEqual(a.Eval(p) - 0.25f, LFMath.Offset(a, 0.25f).Eval(p), Tolerance);
        // A single-shape union is the shape itself; an empty argument list is an error.
        Assert.AreSame(a, LFMath.Union(a));
        Assert.Throws<System.ArgumentException>(() => LFMath.Union());
      }
    }

    [Test]
    public void StdlibShapesAreExposed() {
      using (LFContext.Push()) {
        Assert.Less(LFMath.Box(-Vector3.one, Vector3.one).Eval(Vector3.zero), 0f);
        Assert.Greater(LFMath.Box(-Vector3.one, Vector3.one).Eval(new Vector3(2f, 0f, 0f)), 0f);
        Assert.Less(LFMath.Cylinder(0.5f, 1f).Eval(new Vector3(0f, 0f, 0.5f)), 0f);
        Assert.Greater(LFMath.Cylinder(0.5f, 1f).Eval(new Vector3(0f, 0f, -0.5f)), 0f);
        Assert.Less(LFMath.Torus(1f, 0.25f).Eval(new Vector3(1f, 0f, 0f)), 0f);
        Assert.Greater(LFMath.Torus(1f, 0.25f).Eval(Vector3.zero), 0f);
        Assert.Less(LFMath.Cone(1f, 1f).Eval(new Vector3(0f, 0f, 0.1f)), 0f);
        Assert.Less(LFMath.RoundedBox(-Vector3.one, Vector3.one, 0.5f).Eval(Vector3.zero), 0f);
        Assert.Less(LFMath.Extrude(LFMath.Circle(1f), -1f, 1f).Eval(Vector3.zero), 0f);
        Assert.Greater(LFMath.Extrude(LFMath.Circle(1f), -1f, 1f).Eval(new Vector3(0f, 0f, 2f)), 0f);
        Assert.IsTrue(LFMath.Emptiness().Eval(Vector3.zero) > 1e30f);
        Assert.IsNotNull(LFMath.Text("libfive"));
        // Transforms
        LFTree s = LFMath.Sphere(1f);
        Assert.AreEqual(-1f, LFMath.Move(s, new Vector3(0f, 0f, 5f)).Eval(new Vector3(0f, 0f, 5f)), Tolerance);
        Assert.AreEqual(-1f, s.Transform(Matrix4x4.Translate(new Vector3(2f, 0f, 0f))).Eval(new Vector3(2f, 0f, 0f)), Tolerance);
        Assert.AreEqual(s.Eval(new Vector3(0.3f, 0f, 0f)), LFMath.ReflectX(s).Eval(new Vector3(-0.3f, 0f, 0f)), Tolerance);
        Assert.AreEqual(0f, LFMath.Scale(s, new Vector3(2f, 2f, 2f)).Eval(new Vector3(2f, 0f, 0f)), Tolerance);
        Assert.AreEqual(-1f, LFMath.ArrayX(s, 3, 5f).Eval(new Vector3(10f, 0f, 0f)), Tolerance);
      }
    }

    [Test]
    public void IntervalEvaluationBoundsTheRegion() {
      using (LFContext.Push()) {
        LFTree sphere = LFMath.Sphere(1f);
        Assert.IsTrue(sphere.Eval(new Bounds(Vector3.zero, Vector3.one * 0.5f)).IsInside);
        Assert.IsTrue(sphere.Eval(new Bounds(new Vector3(5f, 0f, 0f), Vector3.one)).IsOutside);
        LFInterval straddling = sphere.Eval(new Bounds(Vector3.zero, Vector3.one * 4f));
        Assert.IsFalse(straddling.IsInside);
        Assert.IsFalse(straddling.IsOutside);
      }
    }

    [Test]
    public void PrintOptimizeAndRemapWork() {
      using (LFContext.Push()) {
        LFTree f = LFTree.x + 1f;
        StringAssert.Contains("x", f.ToString());
        LFTree g = f.Remap(LFTree.y, LFTree.x, LFTree.z);
        Assert.AreEqual(5f, g.Eval(new Vector3(0f, 4f, 0f)), Tolerance);
        LFTree folded = (new LFTree(1f) + 2f).Optimized();
        Assert.IsTrue(folded.TryGetConstant(out float v));
        Assert.AreEqual(3f, v, Tolerance);
      }
    }

    [Test]
    public void SaveAndLoadRoundTrip() {
      string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "libfive-unity-test.frep");
      using (LFContext.Push()) {
        LFTree shape = LFMath.Sphere(0.75f);
        Assert.IsTrue(shape.Save(path));
        LFTree loaded = LFTree.Load(path);
        Assert.IsNotNull(loaded);
        Assert.AreEqual(shape.Eval(new Vector3(0.2f, 0.1f, 0.3f)), loaded.Eval(new Vector3(0.2f, 0.1f, 0.3f)), Tolerance);
      }
      System.IO.File.Delete(path);
    }

    [Test]
    public void ContextReleasesTreesAndRestoresPrior() {
      Assert.IsNull(LFContext.Active);
      LFTree escaped;
      using (Context outer = LFContext.Push()) {
        Assert.AreSame(outer, LFContext.Active);
        using (Context inner = LFContext.Push()) {
          Assert.AreSame(inner, LFContext.Active);
          Assert.AreSame(outer, inner.Prior);
          LFTree temp = LFMath.Sphere(1f);
          escaped = LFContext.Active.Detach(LFMath.Box(-Vector3.one, Vector3.one));
          Assert.Greater(inner.Count, 0);
          inner.Dispose();
          Assert.IsTrue(temp.IsDisposed);
          Assert.IsFalse(escaped.IsDisposed);
        }
        Assert.AreSame(outer, LFContext.Active);
      }
      Assert.IsNull(LFContext.Active);
      Assert.IsFalse(escaped.IsDisposed);
      escaped.Dispose();
      escaped.Dispose(); // idempotent
      Assert.IsTrue(escaped.IsDisposed);
      Assert.Throws<System.ObjectDisposedException>(() => escaped.Eval(Vector3.zero));
    }

    [Test]
    public void RenderMeshProducesVerticesOnTheSurface() {
      var mesh = new Mesh();
      try {
        using (LFContext.Push()) {
          LFTree sphere = LFMath.Sphere(1f);
          Assert.IsTrue(sphere.RenderMesh(mesh, new Bounds(Vector3.zero, Vector3.one * 2.5f), 16f));
          Assert.Greater(mesh.vertexCount, 100);
          Assert.Greater(mesh.GetIndexCount(0), 300);
          var verts = mesh.vertices;
          var normals = mesh.normals;
          Assert.AreEqual(verts.Length, normals.Length);
          // libfive reserves vertex 0 as an unreferenced placeholder at the origin, so only check
          // vertices that triangles actually use.
          var referenced = new bool[verts.Length];
          foreach (int i in mesh.GetIndices(0)) referenced[i] = true;
          int checkedCount = 0;
          for (int i = 0; i < verts.Length; i++) {
            if (!referenced[i]) continue;
            checkedCount++;
            Assert.AreEqual(1f, verts[i].magnitude, 0.1f, "vertex off the sphere surface");
            Assert.AreEqual(1f, normals[i].magnitude, 1e-3f);
            Assert.Greater(Vector3.Dot(normals[i], verts[i].normalized), 0.8f, "normal should point outward");
          }
          Assert.Greater(checkedCount, 100);
          // Empty region -> false and an empty mesh.
          Assert.IsFalse(sphere.RenderMesh(mesh, new Bounds(new Vector3(10f, 0f, 0f), Vector3.one), 8f));
          Assert.AreEqual(0, mesh.vertexCount);
        }
      } finally {
        Object.DestroyImmediate(mesh);
      }
    }

    [Test]
    public void MeshJobRunsOffTheMainThread() {
      var mesh = new Mesh();
      try {
        using (LFContext.Push()) {
          LFTree box = LFMath.Box(-Vector3.one * 0.5f, Vector3.one * 0.5f);
          using (LFMeshJob job = LFMeshJob.Schedule(box, new Bounds(Vector3.zero, Vector3.one * 2f), 8f)) {
            Assert.IsTrue(job.Complete(mesh, 30f));
            Assert.IsTrue(job.IsFinished);
            Assert.Greater(mesh.vertexCount, 0);
          }
          // Hard edges: every normal of a box meshed with 30 degree splitting is axis aligned.
          foreach (Vector3 n in mesh.normals) {
            float m = Mathf.Max(Mathf.Abs(n.x), Mathf.Abs(n.y), Mathf.Abs(n.z));
            Assert.AreEqual(1f, m, 0.05f, "box normal should be axis aligned: " + n);
          }
        }
      } finally {
        Object.DestroyImmediate(mesh);
      }
    }

    static void RequireFeatureNormals() {
      if (!LFNative.SupportsFeatureNormals) Assert.Ignore("Plugin binary lacks the libfive-unity gradient helpers.");
    }

    [Test]
    public void BatchedGradientsMatchPointwiseEvaluation() {
      RequireFeatureNormals();
      using (LFContext.Push()) {
        LFTree shape = LFMath.Blend(0.3f, LFMath.Sphere(1f), LFMath.Box(new Vector3(0.5f, -0.2f, -0.2f), new Vector3(1.5f, 0.2f, 0.2f)));
        var points = new Vector3[700];
        var rng = new System.Random(7);
        for (int i = 0; i < points.Length; i++) {
          points[i] = new Vector3((float)rng.NextDouble() * 4f - 2f, (float)rng.NextDouble() * 4f - 2f, (float)rng.NextDouble() * 4f - 2f);
        }
        Vector3[] batched = shape.Gradient(points);
        Assert.AreEqual(points.Length, batched.Length);
        for (int i = 0; i < points.Length; i++) {
          Vector3 single = shape.Gradient(points[i]);
          Assert.AreEqual(single.x, batched[i].x, 1e-4f);
          Assert.AreEqual(single.y, batched[i].y, 1e-4f);
          Assert.AreEqual(single.z, batched[i].z, 1e-4f);
        }
      }
    }

    [Test]
    public void FeatureNormalsAreExactOnASphere() {
      RequireFeatureNormals();
      var mesh = new Mesh();
      try {
        using (LFContext.Push()) {
          LFTree sphere = LFMath.Sphere(1f);
          Assert.IsTrue(sphere.RenderMesh(mesh, new Bounds(Vector3.zero, Vector3.one * 2.5f), 16f, 180f, true));
          Assert.IsTrue(LFMeshBuilder.LastUsedFeatureNormals);
          var verts = mesh.vertices;
          var normals = mesh.normals;
          var referenced = new bool[verts.Length];
          foreach (int i in mesh.GetIndices(0)) referenced[i] = true;
          float worst = 1f;
          for (int i = 0; i < verts.Length; i++) {
            if (!referenced[i]) continue;
            worst = Mathf.Min(worst, Vector3.Dot(normals[i], verts[i].normalized));
          }
          // Analytic normals are radial to well under a degree; averaged face normals are not.
          Assert.Greater(worst, 0.9995f, "feature normal deviates from the radial direction");
        }
      } finally {
        Object.DestroyImmediate(mesh);
      }
    }

    [Test]
    public void FeatureNormalsSplitARotatedBoxAlongItsRealCreases() {
      RequireFeatureNormals();
      var mesh = new Mesh();
      try {
        using (LFContext.Push()) {
          // A box that is not axis aligned: dual contouring produces skinny triangles along its edges,
          // whose face normals are unreliable. The analytic gradient is not.
          LFTree box = LFMath.Box(-Vector3.one * 0.5f, Vector3.one * 0.5f).RotateX(0.4f).RotateY(0.7f);
          using (LFMeshJob job = LFMeshJob.Schedule(box, new Bounds(Vector3.zero, Vector3.one * 2f), 12f, false, true)) {
            Assert.IsTrue(job.UsesFeatureNormals);
            Assert.IsTrue(job.Complete(mesh, 30f));
            Assert.Greater(job.GradientMilliseconds, 0.0);
          }
          var verts = mesh.vertices;
          var normals = mesh.normals;
          var referenced = new bool[verts.Length];
          foreach (int i in mesh.GetIndices(0)) referenced[i] = true;

          var directions = new System.Collections.Generic.List<Vector3>();
          for (int i = 0; i < verts.Length; i++) {
            if (!referenced[i]) continue;
            Vector3 n = normals[i];
            Assert.AreEqual(1f, n.magnitude, 1e-3f);
            // Stepping off the surface along the normal must stay on the same face: the field's own
            // gradient there agrees with the normal (within 1 degree).
            Vector3 g = box.Gradient(verts[i] + 0.02f * n).normalized;
            Assert.Greater(Vector3.Dot(g, n), 0.9998f, "normal disagrees with the field gradient at vertex " + i);
            bool known = false;
            foreach (Vector3 d in directions) if (Vector3.Dot(d, n) > 0.999f) { known = true; break; }
            if (!known) directions.Add(n);
          }
          Assert.AreEqual(6, directions.Count, "a box has exactly six distinct normals");
        }
      } finally {
        Object.DestroyImmediate(mesh);
      }
    }

    [Test]
    public void RenderSliceReturnsClosedContours() {
      using (LFContext.Push()) {
        Vector2[][] contours = LFMath.Circle(1f).RenderSlice(new Rect(-2f, -2f, 4f, 4f), 0f, 16f);
        Assert.AreEqual(1, contours.Length);
        Assert.Greater(contours[0].Length, 8);
        foreach (Vector2 p in contours[0]) Assert.AreEqual(1f, p.magnitude, 0.1f);
      }
    }
  }
}
