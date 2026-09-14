using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace libfivesharp.Tests {
  /// <summary>Pure managed tests for the normal generation; they do not need the native plugin.</summary>
  public class LFMeshBuilderTests {
    static readonly float3[] CubeVertices = {
      new float3(-1, -1, -1), new float3(1, -1, -1), new float3(1, 1, -1), new float3(-1, 1, -1),
      new float3(-1, -1, 1), new float3(1, -1, 1), new float3(1, 1, 1), new float3(-1, 1, 1),
    };

    // Unity winding (clockwise when seen from outside): normal = cross(b - a, c - a) points outward.
    static readonly uint[] CubeIndices = {
      0, 2, 1, 0, 3, 2, // -Z
      4, 5, 6, 4, 6, 7, // +Z
      0, 1, 5, 0, 5, 4, // -Y
      3, 7, 6, 3, 6, 2, // +Y
      0, 4, 7, 0, 7, 3, // -X
      1, 2, 6, 1, 6, 5, // +X
    };

    static bool Build(float3[] positions, uint[] indices, Mesh mesh, float splitAngle) {
      using (var p = new NativeArray<float3>(positions, Allocator.TempJob))
      using (var i = new NativeArray<uint>(indices, Allocator.TempJob)) {
        return LFMeshBuilder.Build(p, i, mesh, new Bounds(Vector3.zero, Vector3.one * 2f), splitAngle);
      }
    }

    [Test]
    public void SmoothCubeKeepsEightVertices() {
      var mesh = new Mesh();
      try {
        Assert.IsTrue(Build(CubeVertices, CubeIndices, mesh, 180f));
        Assert.AreEqual(8, mesh.vertexCount);
        Assert.AreEqual(36, mesh.GetIndexCount(0));
        var verts = mesh.vertices;
        var normals = mesh.normals;
        for (int i = 0; i < verts.Length; i++) {
          // Smooth cube normals point along the corner diagonal.
          Assert.AreEqual(1f, normals[i].magnitude, 1e-4f);
          Assert.Greater(Vector3.Dot(normals[i], verts[i].normalized), 0.99f);
        }
      } finally { Object.DestroyImmediate(mesh); }
    }

    [Test]
    public void SharpCubeSplitsIntoTwentyFourVerticesWithFaceNormals() {
      var mesh = new Mesh();
      try {
        Assert.IsTrue(Build(CubeVertices, CubeIndices, mesh, 30f));
        Assert.AreEqual(24, mesh.vertexCount);
        Assert.AreEqual(36, mesh.GetIndexCount(0));
        var verts = mesh.vertices;
        var normals = mesh.normals;
        var indices = mesh.GetIndices(0);
        for (int t = 0; t < indices.Length; t += 3) {
          Vector3 a = verts[indices[t]], b = verts[indices[t + 1]], c = verts[indices[t + 2]];
          Vector3 faceNormal = Vector3.Cross(b - a, c - a).normalized;
          for (int k = 0; k < 3; k++) {
            Assert.AreEqual(1f, Vector3.Dot(normals[indices[t + k]], faceNormal), 1e-4f, "corner normal should equal its face normal");
          }
        }
        // Positions of the duplicates match the originals.
        foreach (Vector3 v in verts) Assert.AreEqual(Mathf.Sqrt(3f), v.magnitude, 1e-4f);
      } finally { Object.DestroyImmediate(mesh); }
    }

    [Test]
    public void GentlyCurvedFanStaysConnectedAcrossTheThreshold() {
      // A fan around a center vertex whose faces tilt 20 degrees per step: neighbours are smooth
      // (20 < 30) even though the first and last differ by far more than the threshold, so the
      // center vertex must stay a single smoothing group.
      const int steps = 8;
      var positions = new float3[steps + 1];
      positions[0] = float3.zero;
      var indices = new uint[steps * 3];
      for (int i = 0; i < steps; i++) {
        float ang = i * math.PI * 2f / steps;
        positions[i + 1] = new float3(math.cos(ang), math.sin(ang), 0.2f);
      }
      for (int i = 0; i < steps; i++) {
        indices[3 * i] = 0;
        indices[3 * i + 1] = (uint)(i + 1);
        indices[3 * i + 2] = (uint)((i + 1) % steps + 1);
      }
      var mesh = new Mesh();
      try {
        Assert.IsTrue(Build(positions, indices, mesh, 30f));
        Assert.AreEqual(steps + 1, mesh.vertexCount, "no vertex should have been split");
      } finally { Object.DestroyImmediate(mesh); }
    }

    [Test]
    public void DegenerateTrianglesDoNotProduceNaNs() {
      var positions = new[] { new float3(0, 0, 0), new float3(1, 0, 0), new float3(0, 1, 0), new float3(0, 0, 0) };
      var indices = new uint[] { 0, 2, 1, 0, 3, 1 }; // second triangle has zero area
      var mesh = new Mesh();
      try {
        Assert.IsTrue(Build(positions, indices, mesh, 30f));
        foreach (Vector3 n in mesh.normals) {
          Assert.IsFalse(float.IsNaN(n.x) || float.IsNaN(n.y) || float.IsNaN(n.z));
          Assert.AreEqual(1f, n.magnitude, 1e-4f);
        }
      } finally { Object.DestroyImmediate(mesh); }
    }

    [Test]
    public void OutOfRangeIndicesAreRejected() {
      var positions = new[] { new float3(0, 0, 0), new float3(1, 0, 0), new float3(0, 1, 0) };
      var indices = new uint[] { 0, 1, 7 };
      var mesh = new Mesh();
      try {
        UnityEngine.TestTools.LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("index out of range"));
        Assert.IsFalse(Build(positions, indices, mesh, 30f));
        Assert.AreEqual(0, mesh.vertexCount);
      } finally { Object.DestroyImmediate(mesh); }
    }

    [Test]
    public void EmptyInputClearsTheMesh() {
      var mesh = new Mesh();
      try {
        Assert.IsTrue(Build(CubeVertices, CubeIndices, mesh, 180f));
        Assert.IsFalse(Build(new float3[0], new uint[0], mesh, 180f));
        Assert.AreEqual(0, mesh.vertexCount);
      } finally { Object.DestroyImmediate(mesh); }
    }

    [Test]
    public void RecalculateNormalsExtensionSplitsAnExistingMesh() {
      var mesh = new Mesh();
      try {
        Assert.IsTrue(Build(CubeVertices, CubeIndices, mesh, 180f));
        Assert.AreEqual(8, mesh.vertexCount);
        mesh.RecalculateNormals(30f);
        Assert.AreEqual(24, mesh.vertexCount);
        mesh.RecalculateNormals(180f);
        // Already split vertices are separate vertices, so the count stays; normals become per-face-fan.
        Assert.AreEqual(24, mesh.vertexCount);
      } finally { Object.DestroyImmediate(mesh); }
    }
  }
}
