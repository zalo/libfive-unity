using System;
using System.Diagnostics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;
using libfivesharp.libFiveInternal;

namespace libfivesharp {
  /// <summary>
  /// Meshes an <see cref="LFTree"/> on a job-system worker thread (libfive itself fans out to more
  /// threads internally), then uploads the result to a Unity Mesh on the main thread via
  /// <see cref="LFMeshBuilder"/>. Typical use:
  /// <code>
  /// job = LFMeshJob.Schedule(tree, bounds, resolution);
  /// ...later, once job.IsCompleted (or immediately, to block)...
  /// job.Complete(mesh, splitAngle);
  /// job.Dispose();
  /// </code>
  /// The tree is borrowed: it must stay alive and undisposed until <see cref="Complete"/> or
  /// <see cref="Dispose"/> has been called. Always dispose the job, even if you never complete it.
  /// </summary>
  public sealed class LFMeshJob : IDisposable {
    struct RenderJob : IJob {
      // A raw native handle: the job system rejects pointer-sized fields unless told the caller
      // guarantees their lifetime (LFMeshJob keeps the LFTree alive until Complete/Dispose).
      [NativeDisableUnsafePtrRestriction] public IntPtr tree;
      public libfive_region3 region;
      public float resolution;
      public int singleThreaded;
      /// <summary>1 to also evaluate per-corner gradients for feature-based normals.</summary>
      public int gradients;
      public float sampleOffset;
      /// <summary>[0] libfive_mesh*, [1] corner gradients (libfive_vec3*, from libfive_unity_mesh_corner_gradients) or null.</summary>
      public NativeArray<IntPtr> result;
      /// <summary>Stopwatch ticks spent in [0] meshing and [1] gradient evaluation.</summary>
      public NativeArray<long> ticks;

      public void Execute() {
        if (tree == IntPtr.Zero) { result[0] = IntPtr.Zero; result[1] = IntPtr.Zero; return; }
        long t0 = Stopwatch.GetTimestamp();
        IntPtr mesh = singleThreaded != 0
          ? libfive.libfive_tree_render_mesh_st(tree, region, resolution)
          : libfive.libfive_tree_render_mesh(tree, region, resolution);
        long t1 = Stopwatch.GetTimestamp();
        result[0] = mesh;
        result[1] = gradients != 0 && mesh != IntPtr.Zero
          ? libfive.libfive_unity_mesh_corner_gradients2(tree, mesh, sampleOffset, 2f * sampleOffset)
          : IntPtr.Zero;
        ticks[0] = t1 - t0;
        ticks[1] = Stopwatch.GetTimestamp() - t1;
      }
    }

    /// <summary>
    /// Where along the corner-to-centroid segment the first gradient sample for feature normals is
    /// taken (0 = at the vertex, 1 = at the centroid); the second sample is taken at twice this offset
    /// and the two are extrapolated back to the vertex (see <see cref="LFMeshBuilder"/>). Large enough
    /// to land clearly on one side of a crease that the vertex sits on, small enough that the gradient
    /// varies linearly between the two samples on curved surfaces.
    /// </summary>
    public static float CornerSampleOffset = 0.15f;

    LFTree tree;
    JobHandle handle;
    NativeArray<IntPtr> result;
    NativeArray<long> ticks;
    readonly Stopwatch stopwatch = new Stopwatch();
    bool finished, disposed;

    /// <summary>The tree being meshed (borrowed).</summary>
    public LFTree Tree { get { return tree; } }
    public Bounds Bounds { get; private set; }
    public float Resolution { get; private set; }
    /// <summary>True once the worker has produced a result (or the job was completed/disposed).</summary>
    public bool IsCompleted { get { return finished || disposed || handle.IsCompleted; } }
    /// <summary>True after <see cref="Complete"/> has consumed the result.</summary>
    public bool IsFinished { get { return finished; } }
    /// <summary>Wall-clock time from scheduling to completion, in milliseconds.</summary>
    public double ElapsedMilliseconds { get { return stopwatch.Elapsed.TotalMilliseconds; } }
    /// <summary>True if this job evaluates analytic corner gradients (plugin supports it and it was requested).</summary>
    public bool UsesFeatureNormals { get; private set; }
    /// <summary>Time libfive spent meshing (valid after completion).</summary>
    public double RenderMilliseconds { get { return finished ? ticks[0] * 1000.0 / Stopwatch.Frequency : 0; } }
    /// <summary>Time spent evaluating corner gradients on the worker (valid after completion; 0 without feature normals).</summary>
    public double GradientMilliseconds { get { return finished ? ticks[1] * 1000.0 / Stopwatch.Frequency : 0; } }
    /// <summary>Main-thread time spent building the Unity mesh in <see cref="Complete"/>.</summary>
    public double BuildMilliseconds { get; private set; }

    LFMeshJob() { }

    /// <summary>Starts meshing <paramref name="tree"/> inside <paramref name="bounds"/> at <paramref name="resolution"/> cells per unit.</summary>
    /// <param name="featureNormals">Also evaluate the field gradient per triangle corner so
    /// <see cref="Complete"/> can split normals along the shape's real creases (see <see cref="LFMeshBuilder"/>).
    /// Ignored when the plugin binary lacks the helper.</param>
    public static LFMeshJob Schedule(LFTree tree, Bounds bounds, float resolution, bool singleThreaded = false, bool featureNormals = true) {
      if (tree == null) throw new ArgumentNullException(nameof(tree));
      if (tree.IsDisposed) throw new ObjectDisposedException(nameof(tree));
      bool gradients = featureNormals && LFNative.SupportsFeatureNormals;
      var job = new LFMeshJob {
        tree = tree,
        Bounds = bounds,
        Resolution = resolution,
        UsesFeatureNormals = gradients,
        result = new NativeArray<IntPtr>(2, Allocator.Persistent, NativeArrayOptions.ClearMemory),
        ticks = new NativeArray<long>(2, Allocator.Persistent, NativeArrayOptions.ClearMemory)
      };
      job.stopwatch.Start();
      job.handle = new RenderJob {
        tree = tree.Handle,
        region = LFTree.ToRegion(bounds),
        resolution = resolution,
        singleThreaded = singleThreaded ? 1 : 0,
        gradients = gradients ? 1 : 0,
        sampleOffset = CornerSampleOffset,
        result = job.result,
        ticks = job.ticks
      }.Schedule();
      JobHandle.ScheduleBatchedJobs();
      return job;
    }

    /// <summary>
    /// Blocks until the worker is done (if it isn't already), builds <paramref name="target"/> from the
    /// result and frees the native mesh. Returns false if the shape was empty inside the bounds (the
    /// mesh is cleared) or the job was already consumed.
    /// </summary>
    public bool Complete(Mesh target, float vertexSplittingAngle = 180f) {
      if (disposed) throw new ObjectDisposedException(nameof(LFMeshJob));
      if (finished) return false;
      handle.Complete();
      finished = true;
      stopwatch.Stop();
      IntPtr nativeMesh = result[0];
      IntPtr gradients = result[1];
      result[0] = IntPtr.Zero;
      result[1] = IntPtr.Zero;
      long buildStart = Stopwatch.GetTimestamp();
      try {
        return target != null && LFMeshBuilder.Build(nativeMesh, gradients, 2, target, Bounds, vertexSplittingAngle);
      } finally {
        BuildMilliseconds = (Stopwatch.GetTimestamp() - buildStart) * 1000.0 / Stopwatch.Frequency;
        if (gradients != IntPtr.Zero) libfive.libfive_unity_free(gradients);
        if (nativeMesh != IntPtr.Zero) libfive.libfive_mesh_delete(nativeMesh);
        GC.KeepAlive(tree);
      }
    }

    /// <summary>Waits for the worker if needed, discards any unconsumed result and frees resources.</summary>
    public void Dispose() {
      if (disposed) return;
      disposed = true;
      if (!finished) {
        handle.Complete();
        finished = true;
        stopwatch.Stop();
        IntPtr nativeMesh = result[0];
        IntPtr gradients = result[1];
        if (gradients != IntPtr.Zero) libfive.libfive_unity_free(gradients);
        if (nativeMesh != IntPtr.Zero) libfive.libfive_mesh_delete(nativeMesh);
      }
      if (result.IsCreated) result.Dispose();
      if (ticks.IsCreated) ticks.Dispose();
      GC.KeepAlive(tree);
      tree = null;
    }
  }
}
