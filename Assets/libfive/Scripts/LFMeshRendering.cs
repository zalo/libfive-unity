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
      public NativeArray<IntPtr> result;

      public void Execute() {
        if (tree == IntPtr.Zero) { result[0] = IntPtr.Zero; return; }
        result[0] = singleThreaded != 0
          ? libfive.libfive_tree_render_mesh_st(tree, region, resolution)
          : libfive.libfive_tree_render_mesh(tree, region, resolution);
      }
    }

    LFTree tree;
    JobHandle handle;
    NativeArray<IntPtr> result;
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

    LFMeshJob() { }

    /// <summary>Starts meshing <paramref name="tree"/> inside <paramref name="bounds"/> at <paramref name="resolution"/> cells per unit.</summary>
    public static LFMeshJob Schedule(LFTree tree, Bounds bounds, float resolution, bool singleThreaded = false) {
      if (tree == null) throw new ArgumentNullException(nameof(tree));
      if (tree.IsDisposed) throw new ObjectDisposedException(nameof(tree));
      var job = new LFMeshJob {
        tree = tree,
        Bounds = bounds,
        Resolution = resolution,
        result = new NativeArray<IntPtr>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory)
      };
      job.stopwatch.Start();
      job.handle = new RenderJob {
        tree = tree.Handle,
        region = LFTree.ToRegion(bounds),
        resolution = resolution,
        singleThreaded = singleThreaded ? 1 : 0,
        result = job.result
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
      result[0] = IntPtr.Zero;
      try {
        return target != null && LFMeshBuilder.Build(nativeMesh, target, Bounds, vertexSplittingAngle);
      } finally {
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
        if (nativeMesh != IntPtr.Zero) libfive.libfive_mesh_delete(nativeMesh);
      }
      if (result.IsCreated) result.Dispose();
      GC.KeepAlive(tree);
      tree = null;
    }
  }
}
