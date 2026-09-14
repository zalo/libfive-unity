// Minimal UnityEngine stand-ins so the libfive-unity sources compile and (partly) run under .NET.
// Only the API surface used by the project is implemented; MonoBehaviour plumbing is compile-only.
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;

namespace UnityEngine {
  // ---------------------------------------------------------------- attributes
  public class TooltipAttribute : Attribute { public TooltipAttribute(string t) { } }
  public class HeaderAttribute : Attribute { public HeaderAttribute(string t) { } }
  public class RangeAttribute : Attribute { public RangeAttribute(float a, float b) { } }
  public class MinAttribute : Attribute { public MinAttribute(float a) { } }
  public class SerializeField : Attribute { }
  public class HideInInspector : Attribute { }
  public class ExecuteAlways : Attribute { }
  public class ExecuteInEditMode : Attribute { }
  public class DisallowMultipleComponent : Attribute { }
  public class AddComponentMenu : Attribute { public AddComponentMenu(string s) { } }
  public class HelpURLAttribute : Attribute { public HelpURLAttribute(string s) { } }
  public class RequireComponent : Attribute { public RequireComponent(Type a) { } public RequireComponent(Type a, Type b) { } }

  // ---------------------------------------------------------------- math
  public struct Vector2 {
    public float x, y;
    public Vector2(float x, float y) { this.x = x; this.y = y; }
    public float magnitude => MathF.Sqrt(x * x + y * y);
    public override string ToString() => $"({x}, {y})";
  }

  public struct Vector3 {
    public float x, y, z;
    public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
    public static Vector3 zero => new Vector3(0, 0, 0);
    public static Vector3 one => new Vector3(1, 1, 1);
    public static Vector3 back => new Vector3(0, 0, -1);
    public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.x + b.x, a.y + b.y, a.z + b.z);
    public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.x - b.x, a.y - b.y, a.z - b.z);
    public static Vector3 operator -(Vector3 a) => new Vector3(-a.x, -a.y, -a.z);
    public static Vector3 operator *(Vector3 a, float s) => new Vector3(a.x * s, a.y * s, a.z * s);
    public static Vector3 operator *(float s, Vector3 a) => a * s;
    public static Vector3 operator /(Vector3 a, float s) => new Vector3(a.x / s, a.y / s, a.z / s);
    public static bool operator ==(Vector3 a, Vector3 b) { var d = a - b; return d.x * d.x + d.y * d.y + d.z * d.z < 1e-10f; }
    public static bool operator !=(Vector3 a, Vector3 b) => !(a == b);
    public override bool Equals(object o) => o is Vector3 v && this == v;
    public override int GetHashCode() => 0;
    public float magnitude => MathF.Sqrt(x * x + y * y + z * z);
    public Vector3 normalized { get { float m = magnitude; return m > 1e-12f ? this / m : zero; } }
    public static float Dot(Vector3 a, Vector3 b) => a.x * b.x + a.y * b.y + a.z * b.z;
    public static Vector3 Cross(Vector3 a, Vector3 b) => new Vector3(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x);
    public override string ToString() => $"({x}, {y}, {z})";
  }

  public struct Quaternion {
    public float x, y, z, w;
    public Quaternion(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; }
    public static Quaternion identity => new Quaternion(0, 0, 0, 1);
    public static bool operator ==(Quaternion a, Quaternion b) => MathF.Abs(a.x * b.x + a.y * b.y + a.z * b.z + a.w * b.w) > 0.999999f;
    public static bool operator !=(Quaternion a, Quaternion b) => !(a == b);
    public override bool Equals(object o) => o is Quaternion q && this == q;
    public override int GetHashCode() => 0;
  }

  public struct Matrix4x4 {
    public float m00, m01, m02, m03, m10, m11, m12, m13, m20, m21, m22, m23, m30, m31, m32, m33;
    public static Matrix4x4 identity { get { var m = new Matrix4x4(); m.m00 = m.m11 = m.m22 = m.m33 = 1; return m; } }
    float Get(int r, int c) => r switch {
      0 => c switch { 0 => m00, 1 => m01, 2 => m02, _ => m03 },
      1 => c switch { 0 => m10, 1 => m11, 2 => m12, _ => m13 },
      2 => c switch { 0 => m20, 1 => m21, 2 => m22, _ => m23 },
      _ => c switch { 0 => m30, 1 => m31, 2 => m32, _ => m33 } };
    void Set(int r, int c, float v) {
      switch (r * 4 + c) {
        case 0: m00 = v; break; case 1: m01 = v; break; case 2: m02 = v; break; case 3: m03 = v; break;
        case 4: m10 = v; break; case 5: m11 = v; break; case 6: m12 = v; break; case 7: m13 = v; break;
        case 8: m20 = v; break; case 9: m21 = v; break; case 10: m22 = v; break; case 11: m23 = v; break;
        case 12: m30 = v; break; case 13: m31 = v; break; case 14: m32 = v; break; default: m33 = v; break;
      }
    }
    public static Matrix4x4 operator *(Matrix4x4 a, Matrix4x4 b) {
      var r = new Matrix4x4();
      for (int i = 0; i < 4; i++) for (int j = 0; j < 4; j++) { float s = 0; for (int k = 0; k < 4; k++) s += a.Get(i, k) * b.Get(k, j); r.Set(i, j, s); }
      return r;
    }
    public static Matrix4x4 Translate(Vector3 t) { var m = identity; m.m03 = t.x; m.m13 = t.y; m.m23 = t.z; return m; }
    public static Matrix4x4 Scale(Vector3 s) { var m = identity; m.m00 = s.x; m.m11 = s.y; m.m22 = s.z; return m; }
    public static Matrix4x4 Rotate(Quaternion q) {
      float x = q.x, y = q.y, z = q.z, w = q.w;
      var m = identity;
      m.m00 = 1 - 2 * (y * y + z * z); m.m01 = 2 * (x * y - z * w); m.m02 = 2 * (x * z + y * w);
      m.m10 = 2 * (x * y + z * w); m.m11 = 1 - 2 * (x * x + z * z); m.m12 = 2 * (y * z - x * w);
      m.m20 = 2 * (x * z - y * w); m.m21 = 2 * (y * z + x * w); m.m22 = 1 - 2 * (x * x + y * y);
      return m;
    }
    public static Matrix4x4 TRS(Vector3 t, Quaternion r, Vector3 s) => Translate(t) * Rotate(r) * Scale(s);
    public Vector3 MultiplyPoint3x4(Vector3 p) => new Vector3(
      m00 * p.x + m01 * p.y + m02 * p.z + m03,
      m10 * p.x + m11 * p.y + m12 * p.z + m13,
      m20 * p.x + m21 * p.y + m22 * p.z + m23);
    public float determinant {
      get {
        double[,] a = ToArray();
        return (float)Det(a, 4);
      }
    }
    double[,] ToArray() { var a = new double[4, 4]; for (int i = 0; i < 4; i++) for (int j = 0; j < 4; j++) a[i, j] = Get(i, j); return a; }
    static double Det(double[,] a, int n) {
      // Gaussian elimination
      var m = (double[,])a.Clone(); double det = 1;
      for (int i = 0; i < n; i++) {
        int p = i; for (int r = i + 1; r < n; r++) if (Math.Abs(m[r, i]) > Math.Abs(m[p, i])) p = r;
        if (Math.Abs(m[p, i]) < 1e-300) return 0;
        if (p != i) { for (int c = 0; c < n; c++) { var t = m[i, c]; m[i, c] = m[p, c]; m[p, c] = t; } det = -det; }
        det *= m[i, i];
        for (int r = i + 1; r < n; r++) { double f = m[r, i] / m[i, i]; for (int c = i; c < n; c++) m[r, c] -= f * m[i, c]; }
      }
      return det;
    }
    public Matrix4x4 inverse {
      get {
        var a = ToArray(); var inv = new double[4, 4]; for (int i = 0; i < 4; i++) inv[i, i] = 1;
        for (int i = 0; i < 4; i++) {
          int p = i; for (int r = i + 1; r < 4; r++) if (Math.Abs(a[r, i]) > Math.Abs(a[p, i])) p = r;
          if (Math.Abs(a[p, i]) < 1e-300) return new Matrix4x4(); // singular -> zero (Unity returns garbage too)
          if (p != i) for (int c = 0; c < 4; c++) { (a[i, c], a[p, c]) = (a[p, c], a[i, c]); (inv[i, c], inv[p, c]) = (inv[p, c], inv[i, c]); }
          double d = a[i, i]; for (int c = 0; c < 4; c++) { a[i, c] /= d; inv[i, c] /= d; }
          for (int r = 0; r < 4; r++) if (r != i) { double f = a[r, i]; for (int c = 0; c < 4; c++) { a[r, c] -= f * a[i, c]; inv[r, c] -= f * inv[i, c]; } }
        }
        var m = new Matrix4x4(); for (int i = 0; i < 4; i++) for (int j = 0; j < 4; j++) m.Set(i, j, (float)inv[i, j]);
        return m;
      }
    }
  }

  public struct Bounds {
    public Vector3 center, size;
    public Bounds(Vector3 c, Vector3 s) { center = c; size = s; }
    public Vector3 min => center - size * 0.5f;
    public Vector3 max => center + size * 0.5f;
  }

  public struct Rect {
    public float x, y, width, height;
    public Rect(float x, float y, float w, float h) { this.x = x; this.y = y; width = w; height = h; }
    public float xMin => x; public float xMax => x + width; public float yMin => y; public float yMax => y + height;
  }

  public struct Color { public float r, g, b, a; public Color(float r, float g, float b, float a) { this.r = r; this.g = g; this.b = b; this.a = a; } }

  public static class Mathf {
    public const float PI = MathF.PI;
    public static float Abs(float v) => MathF.Abs(v);
    public static float Sqrt(float v) => MathF.Sqrt(v);
    public static float Sin(float v) => MathF.Sin(v);
    public static float Cos(float v) => MathF.Cos(v);
    public static float Min(float a, float b) => MathF.Min(a, b);
    public static float Max(float a, float b) => MathF.Max(a, b);
    public static float Max(float a, float b, float c) => MathF.Max(a, MathF.Max(b, c));
    public static int Max(int a, int b) => Math.Max(a, b);
    public static float Clamp01(float v) => v < 0 ? 0 : v > 1 ? 1 : v;
    public static int RoundToInt(float v) => (int)MathF.Round(v);
  }

  public static class Time { public static float time => (float)Environment.TickCount64 / 1000f; }
  public static class Application { public static bool isPlaying => false; }
  public enum HideFlags { None, HideAndDontSave = 61, DontSave = 52 }
  public enum LogType { Error, Assert, Warning, Log, Exception }

  public static class Debug {
    public static void Log(object m, Object ctx = null) => Console.WriteLine("[Log] " + m);
    public static void LogWarning(object m, Object ctx = null) => Console.WriteLine("[Warning] " + m);
    public static void LogError(object m, Object ctx = null) => Console.WriteLine("[Error] " + m);
  }

  // ---------------------------------------------------------------- objects
  public class Object {
    public string name = "";
    public HideFlags hideFlags;
    public static void Destroy(Object o) { }
    public static void DestroyImmediate(Object o) { }
    public static implicit operator bool(Object o) => o != null;
    public static T[] FindObjectsByType<T>(FindObjectsInactive i, FindObjectsSortMode s) where T : Object => new T[0];
  }
  public enum FindObjectsInactive { Exclude, Include }
  public enum FindObjectsSortMode { None, InstanceID }

  public class Component : Object {
    public Transform transform { get; set; } = new Transform();
    public GameObject gameObject { get; set; } = new GameObject();
    public T GetComponent<T>() where T : Component => transform.GetComponent<T>();
    public bool isActiveAndEnabled => true;
  }
  public class Behaviour : Component { public bool enabled = true; }
  public class MonoBehaviour : Behaviour { }
  public class Transform : Component {
    public Transform parent;
    public int childCount => 0;
    public Transform GetChild(int i) => throw new NotImplementedException();
    public Vector3 localPosition, localScale = Vector3.one;
    public Quaternion localRotation = Quaternion.identity;
    public Matrix4x4 localToWorldMatrix => Matrix4x4.identity;
    public new T GetComponent<T>() where T : Component => null;
  }
  public class GameObject : Object {
    public GameObject() { }
    public GameObject(string n) { name = n; }
    public int layer;
    public T AddComponent<T>() where T : Component, new() => new T();
    public T GetComponent<T>() where T : Component => null;
  }
  public class Shader : Object { public static Shader Find(string n) => new Shader(); }
  public class Material : Object { public Material(Shader s) { } }
  public class MeshFilter : Component { public Mesh sharedMesh; }
  public class Renderer : Component { public Material sharedMaterial; public bool enabled = true; }
  public class MeshRenderer : Renderer { }
  public static class GUIUtility { public static string systemCopyBuffer; }
  public static class Gizmos {
    public static Color color; public static Matrix4x4 matrix;
    public static void DrawWireCube(Vector3 c, Vector3 s) { }
    public static void DrawWireMesh(Mesh m) { }
    public static void DrawMesh(Mesh m) { }
  }

  public enum MeshTopology { Triangles = 0, Quads = 2, Lines = 3, LineStrip = 4, Points = 5 }

  // ---------------------------------------------------------------- Mesh (records data for tests)
  public class Mesh : Object {
    internal Vector3[] positions = new Vector3[0];
    internal Vector3[] normalData = new Vector3[0];
    internal uint[] indices = new uint[0];
    public Rendering.IndexFormat indexFormat;
    public Bounds bounds;
    public int subMeshCount = 1;

    public int vertexCount => positions.Length;
    public Vector3[] vertices => (Vector3[])positions.Clone();
    public Vector3[] normals => (Vector3[])normalData.Clone();
    public void MarkDynamic() { }
    public void Clear(bool keepLayout = true) { positions = new Vector3[0]; normalData = new Vector3[0]; indices = new uint[0]; }
    public MeshTopology GetTopology(int s) => MeshTopology.Triangles;
    public uint GetIndexCount(int s) => (uint)indices.Length;
    public int[] GetIndices(int s) => indices.Select(i => (int)i).ToArray();
    public void GetIndices(List<int> list, int s, bool applyBaseVertex = true) { list.Clear(); list.AddRange(indices.Select(i => (int)i)); }
    public void GetVertices(List<Vector3> list) { list.Clear(); list.AddRange(positions); }
    public void RecalculateNormals() { normalData = positions.Select(p => new Vector3(0, 1, 0)).ToArray(); }

    public class MeshData {
      internal Mesh source;
      internal object vertexBuffer;
      internal int vertexCapacity;
      internal NativeArray<uint> indexBuffer;
      internal Rendering.SubMeshDescriptor subMesh;
      public int subMeshCount { get; set; }
      public int vertexCount => source != null ? source.positions.Length : vertexCapacity;
      public void SetVertexBufferParams(int count, params Rendering.VertexAttributeDescriptor[] attrs) { vertexCapacity = count; vertexBuffer = null; }
      public void SetIndexBufferParams(int count, Rendering.IndexFormat fmt) {
        if (fmt != Rendering.IndexFormat.UInt32) throw new NotSupportedException("stub supports UInt32 only");
        indexBuffer = new NativeArray<uint>(count, Allocator.TempJob);
      }
      public NativeArray<T> GetVertexData<T>(int stream = 0) where T : struct {
        if (vertexBuffer == null) vertexBuffer = new NativeArray<T>(vertexCapacity, Allocator.TempJob);
        return (NativeArray<T>)vertexBuffer;
      }
      public NativeArray<T> GetIndexData<T>() where T : struct => indexBuffer.Reinterpret<T>();
      public void SetSubMesh(int i, Rendering.SubMeshDescriptor d, Rendering.MeshUpdateFlags f) { subMesh = d; }
      public Rendering.SubMeshDescriptor GetSubMesh(int i) => new Rendering.SubMeshDescriptor(0, source.indices.Length, MeshTopology.Triangles);
      public void GetVertices(NativeArray<Vector3> outVerts) { for (int i = 0; i < source.positions.Length; i++) outVerts[i] = source.positions[i]; }
      public void GetIndices(NativeArray<int> outIdx, int submesh, bool applyBaseVertex = true) { for (int i = 0; i < source.indices.Length; i++) outIdx[i] = (int)source.indices[i]; }
    }

    public struct MeshDataArray : IDisposable {
      internal MeshData[] items;
      public MeshData this[int i] => items[i];
      public int Length => items.Length;
      public void Dispose() { }
    }

    public static MeshDataArray AllocateWritableMeshData(int n) {
      var a = new MeshDataArray { items = new MeshData[n] };
      for (int i = 0; i < n; i++) a.items[i] = new MeshData();
      return a;
    }
    public static MeshDataArray AcquireReadOnlyMeshData(Mesh m) => new MeshDataArray { items = new[] { new MeshData { source = m } } };

    public static void ApplyAndDisposeWritableMeshData(MeshDataArray arr, Mesh mesh, Rendering.MeshUpdateFlags flags) {
      var d = arr[0];
      if (d.vertexBuffer is NativeArray<libfivesharp.LFMeshBuilder.Vertex> va) {
        mesh.positions = new Vector3[va.Length];
        mesh.normalData = new Vector3[va.Length];
        for (int i = 0; i < va.Length; i++) {
          var v = va[i];
          mesh.positions[i] = new Vector3(v.position.x, v.position.y, v.position.z);
          mesh.normalData[i] = new Vector3(v.normal.x, v.normal.y, v.normal.z);
        }
      } else throw new NotSupportedException("stub only understands LFMeshBuilder.Vertex buffers");
      mesh.indices = d.indexBuffer.ToArray();
      mesh.subMeshCount = d.subMeshCount;
      mesh.bounds = d.subMesh.bounds;
    }
  }

  namespace Rendering {
    public enum IndexFormat { UInt16, UInt32 }
    public enum VertexAttribute { Position, Normal, Tangent, Color, TexCoord0 }
    public enum VertexAttributeFormat { Float32, Float16 }
    [Flags] public enum MeshUpdateFlags { Default = 0, DontValidateIndices = 1, DontResetBoneBounds = 2, DontNotifyMeshUsers = 4, DontRecalculateBounds = 8 }
    public struct VertexAttributeDescriptor {
      public VertexAttributeDescriptor(VertexAttribute a, VertexAttributeFormat f, int dim, int stream) { }
    }
    public struct SubMeshDescriptor {
      public int indexStart, indexCount, firstVertex, vertexCount; public MeshTopology topology; public Bounds bounds;
      public SubMeshDescriptor(int start, int count, MeshTopology t) { indexStart = start; indexCount = count; topology = t; firstVertex = 0; vertexCount = 0; bounds = new Bounds(); }
    }
    public class RenderPipelineAsset : Object { public virtual Material defaultMaterial => null; }
    public static class GraphicsSettings { public static RenderPipelineAsset currentRenderPipeline => null; }
  }

  namespace TestTools {
    public static class LogAssert {
      public static void Expect(LogType t, System.Text.RegularExpressions.Regex r) { }
    }
  }
}
