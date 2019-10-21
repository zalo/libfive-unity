using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;
using Unity.Jobs;
using Unity.Collections;

public static class LFNormalCalculation {
  //[Unity.Burst.BurstCompile]
  public struct SplitNormalsJob : IJobParallelFor {
    [ReadOnly]
    public NativeArray<Vector3Int> triangles;
    [ReadOnly]
    public NativeArray<Vector3> vertices, normals;
    [ReadOnly]
    public float vertexSplittingAngle;

    [NativeDisableParallelForRestriction]
    public NativeArray<byte> triangleIndicesToSplit;

    public void Execute(int i) {
      Vector3 normal = Vector3.Cross(vertices[triangles[i].x] - vertices[triangles[i].y],
                                     vertices[triangles[i].x] - vertices[triangles[i].z]);

      bool xSplit = Vector3.Angle(normals[triangles[i].x], normal) > vertexSplittingAngle;
      bool ySplit = Vector3.Angle(normals[triangles[i].y], normal) > vertexSplittingAngle;
      bool zSplit = Vector3.Angle(normals[triangles[i].z], normal) > vertexSplittingAngle;

      if (xSplit) triangleIndicesToSplit[(i * 3)] = 255; //X is a malcontent
      if (ySplit) triangleIndicesToSplit[(i * 3) + 1] = 255; //Y is a malcontent
      if (zSplit) triangleIndicesToSplit[(i * 3) + 2] = 255; //Z is a malcontent
    }
  }

  public static void RecalculateNormals(this Mesh toFill, float vertexSplittingAngle = 180f) {
    Profiler.BeginSample("LF Calculate Mesh Normals", toFill);
    toFill.RecalculateNormals();

    if (vertexSplittingAngle < 180f) {
      NativeArray<Vector3> vertices = new NativeArray<Vector3>(toFill.vertices, Allocator.TempJob);
      int[] tris = toFill.triangles; NativeArray<Vector3Int> triangles = new NativeArray<Vector3Int>(tris.Length / 3, Allocator.TempJob);
      for (int i = 0; i < triangles.Length; i++) triangles[i] = new Vector3Int(tris[i * 3], tris[(i * 3) + 1], tris[(i * 3) + 2]);
      NativeArray<byte> triangleIndicesToSplit = new NativeArray<byte>(new byte[tris.Length], Allocator.TempJob);
      NativeArray<Vector3> normals = new NativeArray<Vector3>(toFill.normals, Allocator.TempJob);

      if (vertices.Length > 0 && triangles.Length > 0 && normals.Length > 0) {
        JobHandle splitNormals = new SplitNormalsJob() {
          vertices = vertices,
          triangles = triangles,
          normals = normals,
          vertexSplittingAngle = vertexSplittingAngle,
          triangleIndicesToSplit = triangleIndicesToSplit
        }.Schedule(triangles.Length, 64);
        splitNormals.Complete();

        //Split the splittable vertices
        List<Vector3> splitVertices = new List<Vector3>(vertices);
        for (int i = 0; i < tris.Length; i++) {
          if (triangleIndicesToSplit[i] == 255) {
            splitVertices.Add(vertices[tris[i]]);
            tris[i] = splitVertices.Count - 1;
          }
        }
        toFill.Clear();
        toFill.SetVertices(splitVertices);
        toFill.triangles = tris;
        toFill.RecalculateNormals();

        vertices.Dispose();
        triangles.Dispose();
        normals.Dispose();
        triangleIndicesToSplit.Dispose();
      }
    }
    Profiler.EndSample();
  }
}
