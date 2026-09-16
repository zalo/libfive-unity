// Stand-ins for Unity.Collections / Unity.Jobs / Unity.Burst / Unity.Mathematics.
// NativeArray aliases a shared byte[] so Reinterpret<> and job writes behave like the real thing;
// jobs run synchronously on Schedule().
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Unity.Collections {
  public enum Allocator { Invalid, None, Temp, TempJob, Persistent }
  public enum NativeArrayOptions { UninitializedMemory, ClearMemory }
  public class ReadOnlyAttribute : Attribute { }
  public class WriteOnlyAttribute : Attribute { }

  public struct NativeArray<T> : IDisposable where T : struct {
    internal byte[] bytes;
    internal int length;
    static readonly int Size = Unsafe.SizeOf<T>();

    public NativeArray(int length, Allocator allocator, NativeArrayOptions options = NativeArrayOptions.ClearMemory) {
      bytes = new byte[length * Size];
      this.length = length;
    }
    public NativeArray(T[] source, Allocator allocator) : this(source.Length, allocator) {
      for (int i = 0; i < source.Length; i++) this[i] = source[i];
    }
    public int Length => length;
    public bool IsCreated => bytes != null;
    public T this[int i] {
      get { if ((uint)i >= (uint)length) throw new IndexOutOfRangeException($"NativeArray index {i} out of range {length}"); return MemoryMarshal.Cast<byte, T>(bytes.AsSpan())[i]; }
      set { if ((uint)i >= (uint)length) throw new IndexOutOfRangeException($"NativeArray index {i} out of range {length}"); MemoryMarshal.Cast<byte, T>(bytes.AsSpan())[i] = value; }
    }
    public NativeArray<U> Reinterpret<U>() where U : struct {
      int us = Unsafe.SizeOf<U>();
      return new NativeArray<U> { bytes = bytes, length = bytes.Length / us };
    }
    public T[] ToArray() { var a = new T[length]; for (int i = 0; i < length; i++) a[i] = this[i]; return a; }
    public void Dispose() { bytes = null; length = 0; }
  }

  namespace LowLevel.Unsafe {
    public struct AtomicSafetyHandle {
      public static AtomicSafetyHandle Create() => new AtomicSafetyHandle();
      public static void Release(AtomicSafetyHandle h) { }
    }
    public static unsafe class NativeArrayUnsafeUtility {
      public static NativeArray<T> ConvertExistingDataToNativeArray<T>(void* ptr, int length, Allocator allocator) where T : struct {
        var arr = new NativeArray<T>(length, allocator);
        Marshal.Copy((IntPtr)ptr, arr.bytes, 0, arr.bytes.Length);
        return arr;
      }
      public static void SetAtomicSafetyHandle<T>(ref NativeArray<T> arr, AtomicSafetyHandle h) where T : struct { }
    }
    public class NativeDisableParallelForRestrictionAttribute : Attribute { }
    public class NativeDisableUnsafePtrRestrictionAttribute : Attribute { }
  }
}

namespace Unity.Jobs {
  public interface IJob { void Execute(); }
  public interface IJobParallelFor { void Execute(int index); }
  public struct JobHandle {
    public bool IsCompleted => true;
    public void Complete() { }
    public static JobHandle CombineDependencies(JobHandle a, JobHandle b) => default;
    public static void ScheduleBatchedJobs() { }
  }
  /// <summary>Mirrors the job system's reflection check: pointer-sized fields must be opted in.</summary>
  public static class JobFieldCheck {
    public static void Validate(Type jobType) {
      foreach (var f in jobType.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)) {
        bool pointerLike = f.FieldType == typeof(IntPtr) || f.FieldType == typeof(UIntPtr) || f.FieldType.IsPointer;
        if (pointerLike && !f.IsDefined(typeof(Unity.Collections.LowLevel.Unsafe.NativeDisableUnsafePtrRestrictionAttribute), false))
          throw new InvalidOperationException($"{jobType.Name}.{f.Name} uses unsafe Pointers which is not allowed. Add [NativeDisableUnsafePtrRestriction].");
        if (f.FieldType.IsClass && f.FieldType != typeof(string))
          throw new InvalidOperationException($"{jobType.Name}.{f.Name} is a reference type, which is not allowed in a job.");
      }
    }
  }
  public static class IJobExtensions {
    public static JobHandle Schedule<T>(this T job, JobHandle deps = default) where T : struct, IJob { JobFieldCheck.Validate(typeof(T)); job.Execute(); return default; }
    public static void Run<T>(this T job) where T : struct, IJob { JobFieldCheck.Validate(typeof(T)); job.Execute(); }
  }
  public static class IJobParallelForExtensions {
    public static JobHandle Schedule<T>(this T job, int length, int batch, JobHandle deps = default) where T : struct, IJobParallelFor {
      JobFieldCheck.Validate(typeof(T));
      for (int i = 0; i < length; i++) job.Execute(i);
      return default;
    }
  }
}

namespace Unity.Burst {
  public enum FloatMode { Default, Strict, Deterministic, Fast }
  public enum FloatPrecision { Standard, High, Medium, Low }
  public class BurstCompileAttribute : Attribute {
    public FloatMode FloatMode { get; set; }
    public FloatPrecision FloatPrecision { get; set; }
    public bool CompileSynchronously { get; set; }
  }
}

namespace Unity.Mathematics {
  public struct float3 {
    public float x, y, z;
    public float3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
    public static float3 zero => new float3(0, 0, 0);
    public static float3 operator +(float3 a, float3 b) => new float3(a.x + b.x, a.y + b.y, a.z + b.z);
    public static float3 operator -(float3 a, float3 b) => new float3(a.x - b.x, a.y - b.y, a.z - b.z);
    public static float3 operator *(float3 a, float s) => new float3(a.x * s, a.y * s, a.z * s);
    public static float3 operator *(float s, float3 a) => a * s;
    public static float3 operator /(float3 a, float s) => new float3(a.x / s, a.y / s, a.z / s);
    public override string ToString() => $"float3({x}, {y}, {z})";
  }
  public static class math {
    public const float PI = MathF.PI;
    public static float3 cross(float3 a, float3 b) => new float3(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x);
    public static float dot(float3 a, float3 b) => a.x * b.x + a.y * b.y + a.z * b.z;
    public static float lengthsq(float3 a) => dot(a, a);
    public static float rsqrt(float x) => 1f / MathF.Sqrt(x);
    public static float length(float3 a) => MathF.Sqrt(dot(a, a));
    public static float3 normalize(float3 a) => a / length(a);
    public static float cos(float v) => MathF.Cos(v);
    public static float sin(float v) => MathF.Sin(v);
    public static float acos(float v) => MathF.Acos(v);
    public static float radians(float d) => d * (MathF.PI / 180f);
    public static int min(int a, int b) => Math.Min(a, b);
    public static int max(int a, int b) => Math.Max(a, b);
    public static float min(float a, float b) => MathF.Min(a, b);
    public static float max(float a, float b) => MathF.Max(a, b);
    public static float clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;
  }
}
