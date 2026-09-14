using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace libfivesharp {
  /// <summary>STL export for meshes produced by libfive (or any triangle mesh).</summary>
  public static class LFMeshExport {
    static readonly List<Vector3> vertexScratch = new List<Vector3>();
    static readonly List<int> indexScratch = new List<int>();

    /// <summary>
    /// Writes a binary STL. <paramref name="transform"/> is applied to every vertex (pass
    /// localToWorldMatrix to export in world space, or Matrix4x4.identity); mirroring transforms flip
    /// the winding so face orientation stays correct.
    /// </summary>
    public static void WriteBinaryStl(Mesh mesh, string path, Matrix4x4 transform, string header = "libfive-unity") {
      if (mesh == null) throw new ArgumentNullException(nameof(mesh));
      if (string.IsNullOrEmpty(path)) throw new ArgumentException("Path is empty.", nameof(path));

      GetTriangles(mesh, out List<Vector3> vertices, out List<int> indices);
      bool flip = transform.determinant < 0f;
      int triangleCount = indices.Count / 3;

      using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write))
      using (var writer = new BinaryWriter(stream)) {
        var headerBytes = new byte[80];
        Encoding.ASCII.GetBytes(header ?? "", 0, Math.Min(header?.Length ?? 0, 80), headerBytes, 0);
        writer.Write(headerBytes);
        writer.Write((uint)triangleCount);
        for (int i = 0; i < triangleCount; i++) {
          Vector3 a = transform.MultiplyPoint3x4(vertices[indices[3 * i]]);
          Vector3 b = transform.MultiplyPoint3x4(vertices[indices[3 * i + 1]]);
          Vector3 c = transform.MultiplyPoint3x4(vertices[indices[3 * i + 2]]);
          if (flip) { Vector3 t = b; b = c; c = t; }
          Vector3 n = Vector3.Cross(b - a, c - a).normalized;
          Write(writer, n); Write(writer, a); Write(writer, b); Write(writer, c);
          writer.Write((ushort)0);
        }
      }
    }

    /// <summary>Writes an ASCII STL with culture-invariant number formatting.</summary>
    public static void WriteAsciiStl(Mesh mesh, string path, Matrix4x4 transform, string solidName = "libfive") {
      File.WriteAllText(path, ToAsciiStl(mesh, transform, solidName), Encoding.ASCII);
    }

    /// <summary>Builds the text of an ASCII STL.</summary>
    public static string ToAsciiStl(Mesh mesh, Matrix4x4 transform, string solidName = "libfive") {
      if (mesh == null) throw new ArgumentNullException(nameof(mesh));
      GetTriangles(mesh, out List<Vector3> vertices, out List<int> indices);
      bool flip = transform.determinant < 0f;
      CultureInfo inv = CultureInfo.InvariantCulture;

      var sb = new StringBuilder(indices.Count * 40);
      sb.Append("solid ").Append(solidName ?? "").Append('\n');
      for (int i = 0; i + 2 < indices.Count; i += 3) {
        Vector3 a = transform.MultiplyPoint3x4(vertices[indices[i]]);
        Vector3 b = transform.MultiplyPoint3x4(vertices[indices[i + 1]]);
        Vector3 c = transform.MultiplyPoint3x4(vertices[indices[i + 2]]);
        if (flip) { Vector3 t = b; b = c; c = t; }
        Vector3 n = Vector3.Cross(b - a, c - a).normalized;
        sb.Append("  facet normal ").Append(n.x.ToString("R", inv)).Append(' ').Append(n.y.ToString("R", inv)).Append(' ').Append(n.z.ToString("R", inv)).Append('\n');
        sb.Append("    outer loop\n");
        AppendVertex(sb, a, inv); AppendVertex(sb, b, inv); AppendVertex(sb, c, inv);
        sb.Append("    endloop\n  endfacet\n");
      }
      sb.Append("endsolid ").Append(solidName ?? "").Append('\n');
      return sb.ToString();
    }

    static void AppendVertex(StringBuilder sb, Vector3 v, CultureInfo inv) {
      sb.Append("      vertex ").Append(v.x.ToString("R", inv)).Append(' ').Append(v.y.ToString("R", inv)).Append(' ').Append(v.z.ToString("R", inv)).Append('\n');
    }

    static void Write(BinaryWriter w, Vector3 v) { w.Write(v.x); w.Write(v.y); w.Write(v.z); }

    static readonly List<int> submeshScratch = new List<int>();

    static void GetTriangles(Mesh mesh, out List<Vector3> vertices, out List<int> indices) {
      vertexScratch.Clear();
      indexScratch.Clear();
      mesh.GetVertices(vertexScratch);
      for (int s = 0; s < mesh.subMeshCount; s++) {
        if (mesh.GetTopology(s) != MeshTopology.Triangles) continue;
        submeshScratch.Clear();
        mesh.GetIndices(submeshScratch, s, true);
        indexScratch.AddRange(submeshScratch);
      }
      vertices = vertexScratch;
      indices = indexScratch;
    }
  }
}
