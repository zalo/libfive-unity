using System;
using System.Collections.Generic;

namespace libfivesharp {
  /// <summary>
  /// Holds the thread's active <see cref="Context"/>. Every <see cref="LFTree"/> created while a
  /// context is active is registered with it and released when the context is disposed.
  /// </summary>
  public static class LFContext {
    [ThreadStatic] static Context _active;

    /// <summary>
    /// The active context for the current thread (null if none). Assigning a freshly
    /// constructed context here is redundant (the constructor already activates it) but
    /// supported so the historical <c>using (LFContext.Active = new Context())</c> idiom keeps working.
    /// </summary>
    public static Context Active {
      get { return _active; }
      set { _active = value; }
    }

    /// <summary>Creates and activates a new context. Dispose it to release every tree built inside.</summary>
    public static Context Push() { return new Context(); }
  }

  /// <summary>
  /// A scope that owns the native lifetime of every <see cref="LFTree"/> created inside it.
  /// Contexts nest: disposing one restores whichever context was active when it was created.
  /// <code>
  /// using (LFContext.Push()) {
  ///   LFTree shape = LFMath.Sphere(1f);      // registered with the context
  ///   shape.RenderMesh(mesh, bounds, 16f);
  /// }                                          // shape and every temporary freed here
  /// </code>
  /// Use <see cref="Detach"/> to hand a tree out of the scope.
  /// </summary>
  public sealed class Context : IDisposable {
    readonly Context prior;
    readonly List<LFTree> trees = new List<LFTree>(64);
    bool disposed;

    /// <summary>The context that was active when this one was created (restored on dispose).</summary>
    public Context Prior { get { return prior; } }

    /// <summary>Number of trees currently owned by this context.</summary>
    public int Count { get { return trees.Count; } }

    public bool IsDisposed { get { return disposed; } }

    public Context() {
      prior = LFContext.Active;
      LFContext.Active = this;
    }

    /// <summary>Registers a tree so it is released when this context is disposed.</summary>
    public void Add(LFTree tree) {
      if (disposed) throw new ObjectDisposedException(nameof(Context));
      if (tree != null) trees.Add(tree);
    }

    /// <summary>Removes a tree from this context so it survives disposal. Returns true if it was registered.</summary>
    public bool Remove(LFTree tree) {
      if (tree == null) return false;
      // Results are usually the most recently created tree, so scan from the end.
      for (int i = trees.Count - 1; i >= 0; i--) {
        if (ReferenceEquals(trees[i], tree)) {
          int last = trees.Count - 1;
          trees[i] = trees[last];
          trees.RemoveAt(last);
          return true;
        }
      }
      return false;
    }

    /// <summary>Removes <paramref name="tree"/> from this context and returns it; the caller now owns it.</summary>
    public LFTree Detach(LFTree tree) {
      Remove(tree);
      return tree;
    }

    /// <summary>Releases every registered tree and reactivates the prior context.</summary>
    public void Dispose() {
      if (disposed) return;
      disposed = true;
      for (int i = 0; i < trees.Count; i++) {
        trees[i].Dispose();
      }
      trees.Clear();
      if (ReferenceEquals(LFContext.Active, this)) LFContext.Active = prior;
    }

    // Names used by earlier versions of this wrapper.
    public void AddTreeToContext(LFTree tree) { Add(tree); }
    public void RemoveTreeFromContext(LFTree tree) { Remove(tree); }
  }
}
