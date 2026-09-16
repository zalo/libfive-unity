using System;
using System.Runtime.InteropServices;
using UnityEngine;
using libfivesharp.libFiveInternal;

namespace libfivesharp {
  /// <summary>Information about the loaded native libfive binary.</summary>
  public static class LFNative {
    static bool probed, available;
    static bool helpersProbed, helpers;
    static string version = "", revision = "", branch = "";

    /// <summary>True if the native library could be loaded and called.</summary>
    public static bool IsAvailable { get { Probe(); return available; } }
    /// <summary>Git tag of the native build, or "" / "N/A".</summary>
    public static string Version { get { Probe(); return version; } }
    /// <summary>Short git hash of the native build (trailing '+' if it had local edits).</summary>
    public static string Revision { get { Probe(); return revision; } }
    public static string Branch { get { Probe(); return branch; } }

    /// <summary>
    /// True if the plugin binary includes the libfive-unity helpers (batched gradients), which enable
    /// feature-based normal splitting. Older binaries fall back to geometric face normals.
    /// </summary>
    public static bool SupportsFeatureNormals {
      get {
        if (!helpersProbed) {
          helpersProbed = true;
          if (IsAvailable) {
            try {
              libfive.libfive_unity_gradients(IntPtr.Zero, IntPtr.Zero, 0, IntPtr.Zero);
              helpers = true;
            } catch (EntryPointNotFoundException) {
              helpers = false;
            }
          }
        }
        return helpers;
      }
    }

    static void Probe() {
      if (probed) return;
      probed = true;
      try {
        version = Marshal.PtrToStringAnsi(libfive.libfive_git_version()) ?? "";
        revision = Marshal.PtrToStringAnsi(libfive.libfive_git_revision()) ?? "";
        branch = Marshal.PtrToStringAnsi(libfive.libfive_git_branch()) ?? "";
        available = true;
      } catch (DllNotFoundException) {
        available = false;
      } catch (EntryPointNotFoundException) {
        available = false;
      }
    }

    /// <summary>Throws a descriptive exception if the native library is missing.</summary>
    public static void Require() {
      if (!IsAvailable) {
        throw new DllNotFoundException(
          "The native libfive plugin (" + libfive.LibraryName + ") is not available for this platform. " +
          "Run the 'Native libfive' GitHub Actions workflow or build native/CMakeLists.txt and place the " +
          "result under Assets/libfive/Plugins.");
      }
    }
  }

  /// <summary>
  /// Opcode values resolved from the loaded native library at startup (via libfive_opcode_enum),
  /// so a libfive built with different opcode numbering still produces correct trees.
  /// Falls back to <see cref="libfive_opcode"/> if the library is unavailable.
  /// </summary>
  public static class LFOpcode {
    public static readonly int Constant, VarX, VarY, VarZ, VarFree, ConstVar;
    public static readonly int Square, Sqrt, Neg, Sin, Cos, Tan, Asin, Acos, Atan, Exp, Abs, Log, Recip;
    public static readonly int Add, Mul, Min, Max, Sub, Div, Atan2, Pow, NthRoot, Mod, NanFill, Compare;

    /// <summary>True if the values came from the native library rather than the fallback enum.</summary>
    public static readonly bool ResolvedFromNative;

    static LFOpcode() {
      ResolvedFromNative = LFNative.IsAvailable;
      Constant = Resolve("constant", libfive_opcode.CONSTANT);
      VarX = Resolve("var-x", libfive_opcode.VAR_X);
      VarY = Resolve("var-y", libfive_opcode.VAR_Y);
      VarZ = Resolve("var-z", libfive_opcode.VAR_Z);
      VarFree = Resolve("var-free", libfive_opcode.VAR_FREE);
      ConstVar = Resolve("const-var", libfive_opcode.CONST_VAR);
      Square = Resolve("square", libfive_opcode.OP_SQUARE);
      Sqrt = Resolve("sqrt", libfive_opcode.OP_SQRT);
      Neg = Resolve("neg", libfive_opcode.OP_NEG);
      Sin = Resolve("sin", libfive_opcode.OP_SIN);
      Cos = Resolve("cos", libfive_opcode.OP_COS);
      Tan = Resolve("tan", libfive_opcode.OP_TAN);
      Asin = Resolve("asin", libfive_opcode.OP_ASIN);
      Acos = Resolve("acos", libfive_opcode.OP_ACOS);
      Atan = Resolve("atan", libfive_opcode.OP_ATAN);
      Exp = Resolve("exp", libfive_opcode.OP_EXP);
      Abs = Resolve("abs", libfive_opcode.OP_ABS);
      Log = Resolve("log", libfive_opcode.OP_LOG);
      Recip = Resolve("recip", libfive_opcode.OP_RECIP);
      Add = Resolve("add", libfive_opcode.OP_ADD);
      Mul = Resolve("mul", libfive_opcode.OP_MUL);
      Min = Resolve("min", libfive_opcode.OP_MIN);
      Max = Resolve("max", libfive_opcode.OP_MAX);
      Sub = Resolve("sub", libfive_opcode.OP_SUB);
      Div = Resolve("div", libfive_opcode.OP_DIV);
      Atan2 = Resolve("atan2", libfive_opcode.OP_ATAN2);
      Pow = Resolve("pow", libfive_opcode.OP_POW);
      NthRoot = Resolve("nth-root", libfive_opcode.OP_NTH_ROOT);
      Mod = Resolve("mod", libfive_opcode.OP_MOD);
      NanFill = Resolve("nanfill", libfive_opcode.OP_NANFILL);
      Compare = Resolve("compare", libfive_opcode.OP_COMPARE);
    }

    static int Resolve(string scmName, libfive_opcode fallback) {
      if (!ResolvedFromNative) return (int)fallback;
      int value = libfive.libfive_opcode_enum(scmName);
      return value >= 0 ? value : (int)fallback;
    }
  }

  /// <summary>Result of interval evaluation: the value over a region is guaranteed to lie in [Lower, Upper].</summary>
  public struct LFInterval {
    public float Lower, Upper;
    public LFInterval(float lower, float upper) { Lower = lower; Upper = upper; }
    /// <summary>True if the region is entirely inside the shape.</summary>
    public bool IsInside { get { return Upper < 0f; } }
    /// <summary>True if the region is entirely outside the shape.</summary>
    public bool IsOutside { get { return Lower > 0f; } }
    public override string ToString() { return "[" + Lower + ", " + Upper + "]"; }
  }

  /// <summary>Two tree-valued components. Implicitly convertible from Vector2 (components become constants).</summary>
  public struct LFVec2 {
    public LFTree x, y;
    public LFVec2(LFTree x, LFTree y) { this.x = x; this.y = y; }
    public LFVec2(float x, float y) { this.x = new LFTree(x); this.y = new LFTree(y); }
    public static implicit operator LFVec2(Vector2 v) { return new LFVec2(v.x, v.y); }
    public static implicit operator LFVec2(Vector3 v) { return new LFVec2(v.x, v.y); }
    /// <summary>Replaces null components with constant zero trees.</summary>
    public LFVec2 Resolved() { return new LFVec2(x ?? new LFTree(0f), y ?? new LFTree(0f)); }
    internal tvec2 Native { get { return new tvec2 { x = x.Handle, y = y.Handle }; } }
  }

  /// <summary>Three tree-valued components. Implicitly convertible from Vector3 (components become constants).</summary>
  public struct LFVec3 {
    public LFTree x, y, z;
    public LFVec3(LFTree x, LFTree y, LFTree z) { this.x = x; this.y = y; this.z = z; }
    public LFVec3(float x, float y, float z) { this.x = new LFTree(x); this.y = new LFTree(y); this.z = new LFTree(z); }
    public static implicit operator LFVec3(Vector3 v) { return new LFVec3(v.x, v.y, v.z); }
    /// <summary>Replaces null components with constant zero trees.</summary>
    public LFVec3 Resolved() { return new LFVec3(x ?? new LFTree(0f), y ?? new LFTree(0f), z ?? new LFTree(0f)); }
    internal tvec3 Native { get { return new tvec3 { x = x.Handle, y = y.Handle, z = z.Handle }; } }
  }

  /// <summary>
  /// A libfive expression tree: a constant, a coordinate variable, a free variable, or a shape
  /// (a function f(x,y,z) whose zero isosurface is the shape's boundary, negative inside).
  ///
  /// Each instance owns one native reference. Instances created while a <see cref="Context"/> is
  /// active are released when that context is disposed; otherwise call <see cref="Dispose"/> or let
  /// the finalizer do it. Operators and <see cref="LFMath"/> build new trees without modifying inputs.
  /// Not thread-safe: build trees on one thread (rendering the result on a worker is fine).
  /// </summary>
  public sealed class LFTree : IDisposable {
    IntPtr handle;

    /// <summary>The native libfive_tree pointer (IntPtr.Zero after disposal).</summary>
    public IntPtr Handle { get { return handle; } }
    /// <summary>Old name of <see cref="Handle"/>.</summary>
    public IntPtr tree { get { return handle; } }
    public bool IsDisposed { get { return handle == IntPtr.Zero; } }

    /// <summary>Wraps a native handle (a new reference the caller received from libfive) and registers it with the active context.</summary>
    public LFTree(IntPtr nativeTree) {
      if (nativeTree == IntPtr.Zero) {
        LFNative.Require();
        throw new InvalidOperationException("libfive returned a null tree (invalid opcode or argument).");
      }
      handle = nativeTree;
      Context active = LFContext.Active;
      if (active != null) active.Add(this);
    }

    /// <summary>A constant.</summary>
    public LFTree(float value) : this(libfive.libfive_tree_const(value)) { }

    /// <summary>Wraps a handle, returning null instead of throwing when it is IntPtr.Zero.</summary>
    public static LFTree TryWrap(IntPtr nativeTree) {
      return nativeTree == IntPtr.Zero ? null : new LFTree(nativeTree);
    }

    /// <summary>Wraps a result handle while keeping the argument trees alive across the native call.</summary>
    internal static LFTree From(IntPtr result, LFTree a) {
      GC.KeepAlive(a);
      return new LFTree(result);
    }
    internal static LFTree From(IntPtr result, LFTree a, LFTree b) {
      GC.KeepAlive(a); GC.KeepAlive(b);
      return new LFTree(result);
    }
    internal static LFTree From(IntPtr result, LFTree a, LFTree b, LFTree c) {
      GC.KeepAlive(a); GC.KeepAlive(b); GC.KeepAlive(c);
      return new LFTree(result);
    }
    internal static LFTree From(IntPtr result, LFTree a, LFTree b, LFTree c, LFTree d) {
      GC.KeepAlive(a); GC.KeepAlive(b); GC.KeepAlive(c); GC.KeepAlive(d);
      return new LFTree(result);
    }
    internal static LFTree From(IntPtr result, LFTree a, LFVec3 v) {
      GC.KeepAlive(a); GC.KeepAlive(v.x); GC.KeepAlive(v.y); GC.KeepAlive(v.z);
      return new LFTree(result);
    }
    internal static LFTree From(IntPtr result, LFTree a, LFTree b, LFVec3 v) {
      GC.KeepAlive(a); GC.KeepAlive(b); GC.KeepAlive(v.x); GC.KeepAlive(v.y); GC.KeepAlive(v.z);
      return new LFTree(result);
    }
    internal static LFTree From(IntPtr result, LFTree a, LFTree b, LFTree c, LFVec3 v) {
      GC.KeepAlive(a); GC.KeepAlive(b); GC.KeepAlive(c); GC.KeepAlive(v.x); GC.KeepAlive(v.y); GC.KeepAlive(v.z);
      return new LFTree(result);
    }
    internal static LFTree From(IntPtr result, LFVec3 u, LFVec3 v) {
      GC.KeepAlive(u.x); GC.KeepAlive(u.y); GC.KeepAlive(u.z); GC.KeepAlive(v.x); GC.KeepAlive(v.y); GC.KeepAlive(v.z);
      return new LFTree(result);
    }
    internal static LFTree From(IntPtr result, LFVec3 u, LFVec3 v, LFTree a) {
      GC.KeepAlive(u.x); GC.KeepAlive(u.y); GC.KeepAlive(u.z); GC.KeepAlive(v.x); GC.KeepAlive(v.y); GC.KeepAlive(v.z); GC.KeepAlive(a);
      return new LFTree(result);
    }
    internal static LFTree From(IntPtr result, LFTree a, LFTree b, LFVec3 u, LFVec3 v) {
      GC.KeepAlive(a); GC.KeepAlive(b);
      GC.KeepAlive(u.x); GC.KeepAlive(u.y); GC.KeepAlive(u.z); GC.KeepAlive(v.x); GC.KeepAlive(v.y); GC.KeepAlive(v.z);
      return new LFTree(result);
    }
    internal static LFTree From(IntPtr result, LFTree a, LFVec3 u, LFVec3 v) {
      GC.KeepAlive(a);
      GC.KeepAlive(u.x); GC.KeepAlive(u.y); GC.KeepAlive(u.z); GC.KeepAlive(v.x); GC.KeepAlive(v.y); GC.KeepAlive(v.z);
      return new LFTree(result);
    }
    internal static LFTree From(IntPtr result, LFTree a, LFVec2 v) {
      GC.KeepAlive(a); GC.KeepAlive(v.x); GC.KeepAlive(v.y);
      return new LFTree(result);
    }
    internal static LFTree From(IntPtr result, LFTree a, LFTree b, LFVec2 v) {
      GC.KeepAlive(a); GC.KeepAlive(b); GC.KeepAlive(v.x); GC.KeepAlive(v.y);
      return new LFTree(result);
    }
    internal static LFTree From(IntPtr result, LFVec2 u, LFVec2 v) {
      GC.KeepAlive(u.x); GC.KeepAlive(u.y); GC.KeepAlive(v.x); GC.KeepAlive(v.y);
      return new LFTree(result);
    }
    internal static LFTree From(IntPtr result, LFVec2 u, LFVec2 v, LFVec2 w) {
      GC.KeepAlive(u.x); GC.KeepAlive(u.y); GC.KeepAlive(v.x); GC.KeepAlive(v.y); GC.KeepAlive(w.x); GC.KeepAlive(w.y);
      return new LFTree(result);
    }
    internal static LFTree From(IntPtr result, LFVec2 u, LFVec2 v, LFTree a) {
      GC.KeepAlive(u.x); GC.KeepAlive(u.y); GC.KeepAlive(v.x); GC.KeepAlive(v.y); GC.KeepAlive(a);
      return new LFTree(result);
    }
    internal static LFTree From(IntPtr result, LFVec2 u, LFVec2 v, LFTree a, LFTree b) {
      GC.KeepAlive(u.x); GC.KeepAlive(u.y); GC.KeepAlive(v.x); GC.KeepAlive(v.y); GC.KeepAlive(a); GC.KeepAlive(b);
      return new LFTree(result);
    }
    internal static LFTree From(IntPtr result, LFTree a, LFVec2 v, LFTree b, LFTree c, LFTree d) {
      GC.KeepAlive(a); GC.KeepAlive(v.x); GC.KeepAlive(v.y); GC.KeepAlive(b); GC.KeepAlive(c); GC.KeepAlive(d);
      return new LFTree(result);
    }
    internal static LFTree From(IntPtr result, LFTree a, LFVec3 v, LFTree b, LFTree c, LFTree d) {
      GC.KeepAlive(a); GC.KeepAlive(v.x); GC.KeepAlive(v.y); GC.KeepAlive(v.z); GC.KeepAlive(b); GC.KeepAlive(c); GC.KeepAlive(d);
      return new LFTree(result);
    }
    internal static LFTree From(IntPtr result, LFTree a, LFVec3 v, LFTree b, LFTree c) {
      GC.KeepAlive(a); GC.KeepAlive(v.x); GC.KeepAlive(v.y); GC.KeepAlive(v.z); GC.KeepAlive(b); GC.KeepAlive(c);
      return new LFTree(result);
    }

    #region Variables and constants
    /// <summary>The X coordinate variable.</summary>
    public static LFTree x { get { return new LFTree(libfive.libfive_tree_x()); } }
    /// <summary>The Y coordinate variable.</summary>
    public static LFTree y { get { return new LFTree(libfive.libfive_tree_y()); } }
    /// <summary>The Z coordinate variable.</summary>
    public static LFTree z { get { return new LFTree(libfive.libfive_tree_z()); } }

    /// <summary>A new free variable (distinct from every other call). Evaluates as 0 unless bound via an evaluator.</summary>
    public static LFTree Var() { return new LFTree(libfive.libfive_tree_var()); }
    /// <summary>Old name for <see cref="Var"/>.</summary>
    public static LFTree FreeVar { get { return Var(); } }

    public static LFTree Constant(float value) { return new LFTree(value); }

    /// <summary>True if this tree is a free variable.</summary>
    public bool IsVar { get { ThrowIfDisposed(); return libfive.libfive_tree_is_var(handle); } }

    /// <summary>If this tree is a constant, returns true and its value.</summary>
    public bool TryGetConstant(out float value) {
      ThrowIfDisposed();
      byte ok;
      value = libfive.libfive_tree_get_const(handle, out ok);
      return ok != 0;
    }

    /// <summary>Unique id of this node; use it as the key when binding free variables.</summary>
    public IntPtr Id { get { ThrowIfDisposed(); return libfive.libfive_tree_id(handle); } }
    /// <summary>Old name for <see cref="Id"/>.</summary>
    public IntPtr id { get { return Id; } }
    #endregion

    #region Operators
    public static implicit operator IntPtr(LFTree tree) {
      if (tree == null) return IntPtr.Zero;
      return tree.handle;
    }
    public static implicit operator LFTree(float value) { return new LFTree(value); }

    public static LFTree operator +(LFTree a, LFTree b) { return From(libfive.libfive_tree_binary(LFOpcode.Add, a.Handle, b.Handle), a, b); }
    public static LFTree operator -(LFTree a, LFTree b) { return From(libfive.libfive_tree_binary(LFOpcode.Sub, a.Handle, b.Handle), a, b); }
    public static LFTree operator *(LFTree a, LFTree b) { return From(libfive.libfive_tree_binary(LFOpcode.Mul, a.Handle, b.Handle), a, b); }
    public static LFTree operator /(LFTree a, LFTree b) { return From(libfive.libfive_tree_binary(LFOpcode.Div, a.Handle, b.Handle), a, b); }
    public static LFTree operator %(LFTree a, LFTree b) { return From(libfive.libfive_tree_binary(LFOpcode.Mod, a.Handle, b.Handle), a, b); }
    public static LFTree operator -(LFTree a) { return From(libfive.libfive_tree_unary(LFOpcode.Neg, a.Handle), a); }
    #endregion

    #region Evaluation
    /// <summary>Evaluates f(p). Negative means inside the shape.</summary>
    public float Eval(Vector3 p) {
      ThrowIfDisposed();
      return libfive.libfive_tree_eval_f(handle, new libfive_vec3 { x = p.x, y = p.y, z = p.z });
    }

    /// <summary>Gradient (df/dx, df/dy, df/dz) at p. For a distance field this is the surface normal direction.</summary>
    public Vector3 Gradient(Vector3 p) {
      ThrowIfDisposed();
      libfive_vec3 d = libfive.libfive_tree_eval_d(handle, new libfive_vec3 { x = p.x, y = p.y, z = p.z });
      return new Vector3(d.x, d.y, d.z);
    }

    /// <summary>Interval arithmetic bound of f over a region.</summary>
    public LFInterval Eval(Bounds region) {
      ThrowIfDisposed();
      libfive_interval r = libfive.libfive_tree_eval_r(handle, ToRegion(region));
      return new LFInterval(r.lower, r.upper);
    }
    #endregion

    #region Transformation and introspection
    /// <summary>q(x,y,z) = this(x', y', z').</summary>
    public LFTree Remap(LFTree newX, LFTree newY, LFTree newZ) {
      ThrowIfDisposed();
      return From(libfive.libfive_tree_remap(handle, newX.Handle, newY.Handle, newZ.Handle), this, newX, newY, newZ);
    }

    /// <summary>A constant-folded, deduplicated copy of this tree (cheaper to evaluate).</summary>
    public LFTree Optimized() {
      ThrowIfDisposed();
      return From(libfive.libfive_tree_optimized(handle), this);
    }

    /// <summary>Scheme-style representation, e.g. "(- (sqrt (+ (square x) (square y))) 1)".</summary>
    public override string ToString() {
      if (handle == IntPtr.Zero) return "<disposed LFTree>";
      IntPtr str = libfive.libfive_tree_print(handle);
      if (str == IntPtr.Zero) return "";
      try { return Marshal.PtrToStringAnsi(str); }
      finally { libfive.libfive_free_str(str); }
    }

    /// <summary>Serializes this tree to a file (libfive's non-archival binary format).</summary>
    public bool Save(string path) {
      ThrowIfDisposed();
      return libfive.libfive_tree_save(handle, path);
    }

    /// <summary>Loads a tree saved with <see cref="Save"/>; null on failure.</summary>
    public static LFTree Load(string path) {
      return TryWrap(libfive.libfive_tree_load(path));
    }
    #endregion

    #region Rendering
    public static libfive_region3 ToRegion(Bounds bounds) {
      Vector3 min = bounds.min, max = bounds.max;
      return new libfive_region3 {
        X = new libfive_interval { lower = min.x, upper = max.x },
        Y = new libfive_interval { lower = min.y, upper = max.y },
        Z = new libfive_interval { lower = min.z, upper = max.z }
      };
    }

    public static libfive_region2 ToRegion(Rect rect) {
      return new libfive_region2 {
        X = new libfive_interval { lower = rect.xMin, upper = rect.xMax },
        Y = new libfive_interval { lower = rect.yMin, upper = rect.yMax }
      };
    }

    /// <summary>
    /// Runs libfive's mesher and returns the raw libfive_mesh pointer (IntPtr.Zero if the shape is
    /// empty in the region). The caller must free it with libfive_mesh_delete. Safe to call from a
    /// worker thread as long as the tree is not disposed meanwhile.
    /// </summary>
    /// <param name="resolution">Octree cells per unit length (min feature size = 1/resolution).</param>
    public IntPtr RenderMeshNative(Bounds bounds, float resolution, bool singleThreaded = false) {
      ThrowIfDisposed();
      libfive_region3 region = ToRegion(bounds);
      return singleThreaded
        ? libfive.libfive_tree_render_mesh_st(handle, region, resolution)
        : libfive.libfive_tree_render_mesh(handle, region, resolution);
    }

    /// <summary>
    /// Meshes this shape inside <paramref name="bounds"/> into <paramref name="target"/> (positions and
    /// normals; no garbage is allocated). Returns false and clears the mesh if the shape is empty there.
    /// </summary>
    /// <param name="resolution">Octree cells per unit length; e.g. 16 gives ~1/16 unit cells.</param>
    /// <param name="vertexSplittingAngle">Edges whose faces meet at more than this angle (degrees) get
    /// split normals. 180 keeps everything smooth.</param>
    /// <param name="featureNormals">Derive normals (and the split decision) from the field's analytic
    /// gradient sampled per triangle corner instead of from face normals. See <see cref="LFMeshBuilder"/>.</param>
    public bool RenderMesh(Mesh target, Bounds bounds, float resolution = 12f, float vertexSplittingAngle = 180f, bool featureNormals = true) {
      if (target == null) throw new ArgumentNullException(nameof(target));
      IntPtr nativeMesh = RenderMeshNative(bounds, resolution);
      IntPtr gradients = IntPtr.Zero;
      try {
        if (featureNormals && nativeMesh != IntPtr.Zero && LFNative.SupportsFeatureNormals) {
          gradients = libfive.libfive_unity_mesh_corner_gradients2(handle, nativeMesh, LFMeshJob.CornerSampleOffset, 2f * LFMeshJob.CornerSampleOffset);
        }
        return LFMeshBuilder.Build(nativeMesh, gradients, 2, target, bounds, vertexSplittingAngle);
      } finally {
        if (gradients != IntPtr.Zero) libfive.libfive_unity_free(gradients);
        if (nativeMesh != IntPtr.Zero) libfive.libfive_mesh_delete(nativeMesh);
      }
    }

    /// <summary>Convenience overload that allocates a new Mesh.</summary>
    public Mesh RenderMesh(Bounds bounds, float resolution = 12f, float vertexSplittingAngle = 180f, bool featureNormals = true) {
      Mesh mesh = new Mesh { name = "libfive mesh" };
      RenderMesh(mesh, bounds, resolution, vertexSplittingAngle, featureNormals);
      return mesh;
    }

    /// <summary>
    /// Gradient of the field at many points in one native call (raw, not normalized). Falls back to
    /// per-point evaluation when the plugin lacks the batched helper.
    /// </summary>
    public unsafe Vector3[] Gradient(Vector3[] points) {
      ThrowIfDisposed();
      if (points == null) throw new ArgumentNullException(nameof(points));
      var result = new Vector3[points.Length];
      if (points.Length == 0) return result;
      if (!LFNative.SupportsFeatureNormals) {
        for (int i = 0; i < points.Length; i++) result[i] = Gradient(points[i]);
        return result;
      }
      fixed (Vector3* src = points)
      fixed (Vector3* dst = result) {
        libfive.libfive_unity_gradients(handle, (IntPtr)src, (uint)points.Length, (IntPtr)dst);
      }
      return result;
    }

    /// <summary>Renders directly to a binary STL file using libfive's own writer.</summary>
    public bool SaveMesh(string path, Bounds bounds, float resolution) {
      ThrowIfDisposed();
      return libfive.libfive_tree_save_mesh(handle, ToRegion(bounds), resolution, path);
    }

    /// <summary>Renders the z = <paramref name="z"/> slice as closed 2D contours (one array per loop).</summary>
    public Vector2[][] RenderSlice(Rect region, float z, float resolution) {
      ThrowIfDisposed();
      IntPtr ptr = libfive.libfive_tree_render_slice(handle, ToRegion(region), z, resolution);
      if (ptr == IntPtr.Zero) return new Vector2[0][];
      try {
        libfive_contours contours = Marshal.PtrToStructure<libfive_contours>(ptr);
        int contourCount = (int)contours.count;
        var result = new Vector2[contourCount][];
        int contourStride = Marshal.SizeOf<libfive_contour>();
        for (int i = 0; i < contourCount; i++) {
          libfive_contour c = Marshal.PtrToStructure<libfive_contour>(contours.cs + i * contourStride);
          int pointCount = (int)c.count;
          var pts = new Vector2[pointCount];
          if (pointCount > 0) {
            var floats = new float[pointCount * 2];
            Marshal.Copy(c.pts, floats, 0, floats.Length);
            for (int j = 0; j < pointCount; j++) pts[j] = new Vector2(floats[2 * j], floats[2 * j + 1]);
          }
          result[i] = pts;
        }
        return result;
      } finally {
        libfive.libfive_contours_delete(ptr);
      }
    }

    /// <summary>Renders an occupancy bitmap of the z-slice: true where the shape is inside.</summary>
    public bool[,] RenderPixels(Rect region, float z, float resolution) {
      ThrowIfDisposed();
      IntPtr ptr = libfive.libfive_tree_render_pixels(handle, ToRegion(region), z, resolution);
      if (ptr == IntPtr.Zero) return new bool[0, 0];
      try {
        libfive_pixels px = Marshal.PtrToStructure<libfive_pixels>(ptr);
        int width = (int)px.width, height = (int)px.height;
        var bytes = new byte[width * height];
        Marshal.Copy(px.pixels, bytes, 0, bytes.Length);
        var result = new bool[height, width];
        for (int yI = 0; yI < height; yI++)
          for (int xI = 0; xI < width; xI++)
            result[yI, xI] = bytes[yI * width + xI] != 0;
        return result;
      } finally {
        libfive.libfive_pixels_delete(ptr);
      }
    }
    #endregion

    #region Lifetime
    void ThrowIfDisposed() {
      if (handle == IntPtr.Zero) throw new ObjectDisposedException(nameof(LFTree));
    }

    ~LFTree() { Release(); }

    /// <summary>Releases the native reference. Safe to call more than once; also safe if a Context releases it later.</summary>
    public void Dispose() {
      Release();
      GC.SuppressFinalize(this);
    }

    void Release() {
      IntPtr h = handle;
      if (h == IntPtr.Zero) return;
      handle = IntPtr.Zero;
      libfive.libfive_tree_delete(h);
    }
    #endregion
  }

  /// <summary>
  /// Shape construction library: math ops, primitives, CSG and transforms. Primitives, CSG and most
  /// transforms call libfive's standard library (libfive/stdlib) so they match libfive Studio exactly.
  /// Every float/Vector argument may also be given as trees (via <see cref="LFTree"/>/<see cref="LFVec3"/>)
  /// to parameterize shapes with free variables. libfive is Z-up: "Cylinder" extrudes along +Z.
  /// </summary>
  public static class LFMath {
    static IntPtr H(LFTree t) {
      if (t == null) throw new ArgumentNullException("tree");
      return t.Handle;
    }
    static LFVec3 V(LFVec3 v) { return v.Resolved(); }
    static LFVec2 V(LFVec2 v) { return v.Resolved(); }

    #region Unary ops
    public static LFTree Square(LFTree t) { return LFTree.From(libfive.libfive_tree_unary(LFOpcode.Square, H(t)), t); }
    public static LFTree Sqrt(LFTree t) { return LFTree.From(libfive.libfive_tree_unary(LFOpcode.Sqrt, H(t)), t); }
    public static LFTree Neg(LFTree t) { return LFTree.From(libfive.libfive_tree_unary(LFOpcode.Neg, H(t)), t); }
    public static LFTree Sin(LFTree t) { return LFTree.From(libfive.libfive_tree_unary(LFOpcode.Sin, H(t)), t); }
    public static LFTree Cos(LFTree t) { return LFTree.From(libfive.libfive_tree_unary(LFOpcode.Cos, H(t)), t); }
    public static LFTree Tan(LFTree t) { return LFTree.From(libfive.libfive_tree_unary(LFOpcode.Tan, H(t)), t); }
    public static LFTree Asin(LFTree t) { return LFTree.From(libfive.libfive_tree_unary(LFOpcode.Asin, H(t)), t); }
    public static LFTree Acos(LFTree t) { return LFTree.From(libfive.libfive_tree_unary(LFOpcode.Acos, H(t)), t); }
    public static LFTree Atan(LFTree t) { return LFTree.From(libfive.libfive_tree_unary(LFOpcode.Atan, H(t)), t); }
    public static LFTree Exp(LFTree t) { return LFTree.From(libfive.libfive_tree_unary(LFOpcode.Exp, H(t)), t); }
    public static LFTree Abs(LFTree t) { return LFTree.From(libfive.libfive_tree_unary(LFOpcode.Abs, H(t)), t); }
    public static LFTree Log(LFTree t) { return LFTree.From(libfive.libfive_tree_unary(LFOpcode.Log, H(t)), t); }
    public static LFTree Reciprocal(LFTree t) { return LFTree.From(libfive.libfive_tree_unary(LFOpcode.Recip, H(t)), t); }

    // Names used by earlier versions of this wrapper.
    public static LFTree ASin(LFTree t) { return Asin(t); }
    public static LFTree ACos(LFTree t) { return Acos(t); }
    public static LFTree ATan(LFTree t) { return Atan(t); }
    public static LFTree Reciporical(LFTree t) { return Reciprocal(t); }
    #endregion

    #region Binary ops
    public static LFTree Min(LFTree a, LFTree b) { return LFTree.From(libfive.libfive_tree_binary(LFOpcode.Min, H(a), H(b)), a, b); }
    public static LFTree Max(LFTree a, LFTree b) { return LFTree.From(libfive.libfive_tree_binary(LFOpcode.Max, H(a), H(b)), a, b); }
    public static LFTree Atan2(LFTree y, LFTree x) { return LFTree.From(libfive.libfive_tree_binary(LFOpcode.Atan2, H(y), H(x)), y, x); }
    public static LFTree Pow(LFTree a, LFTree b) { return LFTree.From(libfive.libfive_tree_binary(LFOpcode.Pow, H(a), H(b)), a, b); }
    public static LFTree NthRoot(LFTree a, LFTree n) { return LFTree.From(libfive.libfive_tree_binary(LFOpcode.NthRoot, H(a), H(n)), a, n); }
    public static LFTree Mod(LFTree a, LFTree b) { return LFTree.From(libfive.libfive_tree_binary(LFOpcode.Mod, H(a), H(b)), a, b); }
    /// <summary>a where a is not NaN, otherwise b.</summary>
    public static LFTree NanFill(LFTree a, LFTree b) { return LFTree.From(libfive.libfive_tree_binary(LFOpcode.NanFill, H(a), H(b)), a, b); }
    /// <summary>-1, 0 or 1 as a is less than, equal to, or greater than b.</summary>
    public static LFTree Compare(LFTree a, LFTree b) { return LFTree.From(libfive.libfive_tree_binary(LFOpcode.Compare, H(a), H(b)), a, b); }
    public static LFTree nthRoot(LFTree a, LFTree n) { return NthRoot(a, n); }
    #endregion

    #region CSG
    /// <summary>Union (min) of any number of shapes. Null entries are skipped.</summary>
    public static LFTree Union(params LFTree[] shapes) { return Fold(shapes, "Union", (a, b) => LFTree.From(libfive_stdlib._union(a.Handle, b.Handle), a, b)); }
    /// <summary>Intersection (max) of any number of shapes. Null entries are skipped.</summary>
    public static LFTree Intersection(params LFTree[] shapes) { return Fold(shapes, "Intersection", (a, b) => LFTree.From(libfive_stdlib.intersection(a.Handle, b.Handle), a, b)); }
    /// <summary>Everything that is not in the shape.</summary>
    public static LFTree Inverse(this LFTree shape) { return LFTree.From(libfive_stdlib.inverse(H(shape)), shape); }

    /// <summary>The first shape minus the union of the rest.</summary>
    public static LFTree Difference(params LFTree[] shapes) {
      if (shapes == null) throw new ArgumentNullException(nameof(shapes));
      LFTree first = null;
      int firstIndex = -1;
      for (int i = 0; i < shapes.Length; i++) if (shapes[i] != null) { first = shapes[i]; firstIndex = i; break; }
      if (first == null) throw new ArgumentException("Difference needs at least one non-null shape.", nameof(shapes));
      LFTree subtrahend = null;
      for (int i = firstIndex + 1; i < shapes.Length; i++) {
        if (shapes[i] == null) continue;
        subtrahend = subtrahend == null ? shapes[i] : Union(subtrahend, shapes[i]);
      }
      if (subtrahend == null) return first;
      return LFTree.From(libfive_stdlib.difference(first.Handle, subtrahend.Handle), first, subtrahend);
    }
    public static LFTree Difference(LFTree a, LFTree b) { return LFTree.From(libfive_stdlib.difference(H(a), H(b)), a, b); }

    /// <summary>Expands (positive) or shrinks (negative) a shape.</summary>
    public static LFTree Offset(this LFTree shape, LFTree distance) { return LFTree.From(libfive_stdlib.offset(H(shape), H(distance)), shape, distance); }
    /// <summary>a minus (b expanded by <paramref name="gap"/>).</summary>
    public static LFTree Clearance(LFTree a, LFTree b, LFTree gap) { return LFTree.From(libfive_stdlib.clearance(H(a), H(b), H(gap)), a, b, gap); }
    /// <summary>A hollow shell of the given thickness inside the shape's surface.</summary>
    public static LFTree Shell(this LFTree shape, LFTree thickness) { return LFTree.From(libfive_stdlib.shell(H(shape), H(thickness)), shape, thickness); }

    /// <summary>libfive's standard blend (exponential, unit-scaled): smoothness 0..1 fills the crease between a and b.</summary>
    public static LFTree Blend(LFTree a, LFTree b, LFTree smoothness) { return LFTree.From(libfive_stdlib.blend_expt_unit(H(a), H(b), H(smoothness)), a, b, smoothness); }
    /// <summary>Blends any number of shapes pairwise with the same smoothness.</summary>
    public static LFTree Blend(float smoothness, params LFTree[] shapes) {
      LFTree m = new LFTree(smoothness);
      return Fold(shapes, "Blend", (a, b) => Blend(a, b, m));
    }
    /// <summary>Exponential blend with a raw exponent m (larger = sharper).</summary>
    public static LFTree BlendExpt(LFTree a, LFTree b, LFTree m) { return LFTree.From(libfive_stdlib.blend_expt(H(a), H(b), H(m)), a, b, m); }
    /// <summary>The fast sqrt-based blend used by earlier versions of this wrapper (does not preserve gradients).</summary>
    public static LFTree BlendRough(LFTree a, LFTree b, LFTree m) { return LFTree.From(libfive_stdlib.blend_rough(H(a), H(b), H(m)), a, b, m); }
    /// <summary>Smoothly subtracts b (optionally offset by o) from a.</summary>
    public static LFTree BlendDifference(LFTree a, LFTree b, LFTree smoothness, LFTree o = null) {
      o = o ?? new LFTree(0f);
      return LFTree.From(libfive_stdlib.blend_difference(H(a), H(b), H(smoothness), H(o)), a, b, smoothness, o);
    }
    /// <summary>Linear morph: m = 0 gives a, m = 1 gives b.</summary>
    public static LFTree Morph(LFTree a, LFTree b, LFTree m) { return LFTree.From(libfive_stdlib.morph(H(a), H(b), H(m)), a, b, m); }
    /// <summary>Lofts between two 2D (XY) shapes: a at zmin, b at zmax.</summary>
    public static LFTree Loft(LFTree a, LFTree b, LFTree zmin, LFTree zmax) { return LFTree.From(libfive_stdlib.loft(H(a), H(b), H(zmin), H(zmax)), a, b, zmin, zmax); }
    /// <summary>Loft with the XY position sliding from lower.xy (at lower.z) to upper.xy (at upper.z).</summary>
    public static LFTree LoftBetween(LFTree a, LFTree b, LFVec3 lower, LFVec3 upper) {
      lower = V(lower); upper = V(upper);
      return LFTree.From(libfive_stdlib.loft_between(H(a), H(b), lower.Native, upper.Native), a, b, lower, upper);
    }

    static LFTree Fold(LFTree[] shapes, string name, Func<LFTree, LFTree, LFTree> op) {
      if (shapes == null) throw new ArgumentNullException(nameof(shapes));
      LFTree acc = null;
      for (int i = 0; i < shapes.Length; i++) {
        if (shapes[i] == null) continue;
        acc = acc == null ? shapes[i] : op(acc, shapes[i]);
      }
      if (acc == null) throw new ArgumentException(name + " needs at least one non-null shape.", nameof(shapes));
      return acc;
    }
    #endregion

    #region 2D shapes (infinite along Z)
    public static LFTree Circle(LFTree radius, LFVec2 center = default(LFVec2)) {
      center = V(center);
      return LFTree.From(libfive_stdlib.circle(H(radius), center.Native), radius, center);
    }
    public static LFTree Ring(LFTree outerRadius, LFTree innerRadius, LFVec2 center = default(LFVec2)) {
      center = V(center);
      return LFTree.From(libfive_stdlib.ring(H(outerRadius), H(innerRadius), center.Native), outerRadius, innerRadius, center);
    }
    /// <summary>Regular polygon with <paramref name="sides"/> sides and center-to-vertex distance <paramref name="radius"/>.</summary>
    public static LFTree Polygon(LFTree radius, int sides, LFVec2 center = default(LFVec2)) {
      center = V(center);
      return LFTree.From(libfive_stdlib.polygon(H(radius), sides, center.Native), radius, center);
    }
    public static LFTree Rectangle(LFVec2 a, LFVec2 b) {
      a = V(a); b = V(b);
      return LFTree.From(libfive_stdlib.rectangle(a.Native, b.Native), a, b);
    }
    public static LFTree RoundedRectangle(LFVec2 a, LFVec2 b, LFTree radius) {
      a = V(a); b = V(b);
      return LFTree.From(libfive_stdlib.rounded_rectangle(a.Native, b.Native, H(radius)), a, b, radius);
    }
    public static LFTree RectangleExact(LFVec2 a, LFVec2 b) {
      a = V(a); b = V(b);
      return LFTree.From(libfive_stdlib.rectangle_exact(a.Native, b.Native), a, b);
    }
    public static LFTree RectangleCenteredExact(LFVec2 size, LFVec2 center = default(LFVec2)) {
      size = V(size); center = V(center);
      return LFTree.From(libfive_stdlib.rectangle_centered_exact(size.Native, center.Native), size, center);
    }
    public static LFTree Triangle(LFVec2 a, LFVec2 b, LFVec2 c) {
      a = V(a); b = V(b); c = V(c);
      return LFTree.From(libfive_stdlib.triangle(a.Native, b.Native, c.Native), a, b, c);
    }
    #endregion

    #region 3D shapes
    /// <summary>Axis-aligned box from corner <paramref name="lower"/> to <paramref name="upper"/> (mitered field: stays creased when offset).</summary>
    public static LFTree Box(LFVec3 lower, LFVec3 upper) {
      lower = V(lower); upper = V(upper);
      return LFTree.From(libfive_stdlib.box_mitered(lower.Native, upper.Native), lower, upper);
    }
    public static LFTree BoxCentered(LFVec3 size, LFVec3 center = default(LFVec3)) {
      size = V(size); center = V(center);
      return LFTree.From(libfive_stdlib.box_mitered_centered(size.Native, center.Native), size, center);
    }
    /// <summary>Box with a true Euclidean distance field (rounds when offset).</summary>
    public static LFTree BoxExact(LFVec3 lower, LFVec3 upper) {
      lower = V(lower); upper = V(upper);
      return LFTree.From(libfive_stdlib.box_exact(lower.Native, upper.Native), lower, upper);
    }
    public static LFTree BoxExactCentered(LFVec3 size, LFVec3 center = default(LFVec3)) {
      size = V(size); center = V(center);
      return LFTree.From(libfive_stdlib.box_exact_centered(size.Native, center.Native), size, center);
    }
    /// <summary>Rounded box; <paramref name="rounding"/> is a 0..1 fraction of the smallest half-extent.</summary>
    public static LFTree RoundedBox(LFVec3 lower, LFVec3 upper, LFTree rounding) {
      lower = V(lower); upper = V(upper);
      return LFTree.From(libfive_stdlib.rounded_box(lower.Native, upper.Native, H(rounding)), lower, upper, rounding);
    }
    public static LFTree Sphere(LFTree radius, LFVec3 center = default(LFVec3)) {
      center = V(center);
      return LFTree.From(libfive_stdlib.sphere(H(radius), center.Native), radius, center);
    }
    /// <summary>Everything on the side of the plane opposite <paramref name="normal"/>, through <paramref name="point"/>.</summary>
    public static LFTree HalfSpace(LFVec3 normal, LFVec3 point = default(LFVec3)) {
      normal = V(normal); point = V(point);
      return LFTree.From(libfive_stdlib.half_space(normal.Native, point.Native), normal, point);
    }
    /// <summary>Cylinder of the given radius extruded along +Z for <paramref name="height"/> from <paramref name="basePosition"/>.</summary>
    public static LFTree Cylinder(LFTree radius, LFTree height, LFVec3 basePosition = default(LFVec3)) {
      basePosition = V(basePosition);
      return LFTree.From(libfive_stdlib.cylinder_z(H(radius), H(height), basePosition.Native), radius, height, basePosition);
    }
    /// <summary>Cone along +Z defined by its slope angle (radians) and height.</summary>
    public static LFTree ConeAngle(LFTree angle, LFTree height, LFVec3 basePosition = default(LFVec3)) {
      basePosition = V(basePosition);
      return LFTree.From(libfive_stdlib.cone_ang_z(H(angle), H(height), basePosition.Native), angle, height, basePosition);
    }
    /// <summary>Cone along +Z with the given base radius and height.</summary>
    public static LFTree Cone(LFTree radius, LFTree height, LFVec3 basePosition = default(LFVec3)) {
      basePosition = V(basePosition);
      return LFTree.From(libfive_stdlib.cone_z(H(radius), H(height), basePosition.Native), radius, height, basePosition);
    }
    /// <summary>Pyramid over the XY rectangle a..b, starting at zmin and rising by height.</summary>
    public static LFTree Pyramid(LFVec2 a, LFVec2 b, LFTree zmin, LFTree height) {
      a = V(a); b = V(b);
      return LFTree.From(libfive_stdlib.pyramid_z(a.Native, b.Native, H(zmin), H(height)), a, b, zmin, height);
    }
    /// <summary>Torus around Z: <paramref name="majorRadius"/> to the tube center, <paramref name="minorRadius"/> tube radius.</summary>
    public static LFTree Torus(LFTree majorRadius, LFTree minorRadius, LFVec3 center = default(LFVec3)) {
      center = V(center);
      return LFTree.From(libfive_stdlib.torus_z(H(majorRadius), H(minorRadius), center.Native), majorRadius, minorRadius, center);
    }
    /// <summary>Volume-filling gyroid. libfive's period parameter: one cell spans (2π)²/period units.</summary>
    public static LFTree Gyroid(LFVec3 period, LFTree thickness) {
      period = V(period);
      return LFTree.From(libfive_stdlib.gyroid(period.Native, H(thickness)), thickness, period);
    }
    /// <summary>Gyroid whose cells repeat every <paramref name="cellSize"/> units.</summary>
    public static LFTree GyroidCells(float cellSize, LFTree thickness) {
      float p = (2f * Mathf.PI) * (2f * Mathf.PI) / cellSize;
      return Gyroid(new Vector3(p, p, p), thickness);
    }
    /// <summary>A shape that is empty everywhere.</summary>
    public static LFTree Emptiness() { return new LFTree(libfive_stdlib.emptiness()); }

    /// <summary>Extrudes a 2D (XY) shape between zmin and zmax.</summary>
    public static LFTree Extrude(this LFTree shape, LFTree zmin, LFTree zmax) { return LFTree.From(libfive_stdlib.extrude_z(H(shape), H(zmin), H(zmax)), shape, zmin, zmax); }

    /// <summary>Repeats a shape <paramref name="count"/> times along X with spacing <paramref name="dx"/>.</summary>
    public static LFTree ArrayX(this LFTree shape, int count, LFTree dx) { return LFTree.From(libfive_stdlib.array_x(H(shape), count, H(dx)), shape, dx); }
    public static LFTree ArrayXY(this LFTree shape, int nx, int ny, LFVec2 delta) {
      delta = V(delta);
      return LFTree.From(libfive_stdlib.array_xy(H(shape), nx, ny, delta.Native), shape, delta);
    }
    public static LFTree ArrayXYZ(this LFTree shape, int nx, int ny, int nz, LFVec3 delta) {
      delta = V(delta);
      return LFTree.From(libfive_stdlib.array_xyz(H(shape), nx, ny, nz, delta.Native), shape, delta);
    }
    /// <summary>Repeats a shape <paramref name="count"/> times around Z about <paramref name="center"/>.</summary>
    public static LFTree ArrayPolar(this LFTree shape, int count, LFVec2 center = default(LFVec2)) {
      center = V(center);
      return LFTree.From(libfive_stdlib.array_polar_z(H(shape), count, center.Native), shape, center);
    }

    /// <summary>Ellipsoid as the locus of points whose summed distance to the two foci is <paramref name="radius"/>.</summary>
    public static LFTree Ellipsoid(float radius, Vector3 focusA, Vector3 focusB) {
      LFTree x = LFTree.x, y = LFTree.y, z = LFTree.z;
      return Sqrt(Square(x - focusA.x) + Square(y - focusA.y) + Square(z - focusA.z)) +
             Sqrt(Square(x - focusB.x) + Square(y - focusB.y) + Square(z - focusB.z)) - radius;
    }

    /// <summary>Text in libfive's built-in f-rep font (character height 1), as a 2D shape.</summary>
    public static LFTree Text(string text, LFVec2 position = default(LFVec2)) {
      position = V(position);
      return LFTree.From(libfive_stdlib.text(text ?? "", position.Native), null, position);
    }
    #endregion

    #region Transforms
    public static LFTree Move(this LFTree shape, LFVec3 offset) {
      offset = V(offset);
      return LFTree.From(libfive_stdlib.move(H(shape), offset.Native), shape, offset);
    }

    /// <summary>Applies an arbitrary affine matrix (the shape's points p map to M·p).</summary>
    public static LFTree Transform(this LFTree shape, Matrix4x4 matrix) {
      Matrix4x4 inv = matrix.inverse;
      LFTree x = LFTree.x, y = LFTree.y, z = LFTree.z;
      return shape.Remap(
        inv.m00 * x + inv.m01 * y + inv.m02 * z + inv.m03,
        inv.m10 * x + inv.m11 * y + inv.m12 * z + inv.m13,
        inv.m20 * x + inv.m21 * y + inv.m22 * z + inv.m23);
    }

    /// <summary>Rotates by a quaternion about <paramref name="center"/>.</summary>
    public static LFTree Rotate(this LFTree shape, Quaternion rotation, Vector3 center = default(Vector3)) {
      Matrix4x4 m = Matrix4x4.Translate(center) * Matrix4x4.Rotate(rotation) * Matrix4x4.Translate(-center);
      return Transform(shape, m);
    }

    public static LFTree ReflectX(this LFTree shape, LFTree offset = null) { offset = offset ?? new LFTree(0f); return LFTree.From(libfive_stdlib.reflect_x(H(shape), H(offset)), shape, offset); }
    public static LFTree ReflectY(this LFTree shape, LFTree offset = null) { offset = offset ?? new LFTree(0f); return LFTree.From(libfive_stdlib.reflect_y(H(shape), H(offset)), shape, offset); }
    public static LFTree ReflectZ(this LFTree shape, LFTree offset = null) { offset = offset ?? new LFTree(0f); return LFTree.From(libfive_stdlib.reflect_z(H(shape), H(offset)), shape, offset); }
    /// <summary>Reflects across the plane X = Y (swaps x and y).</summary>
    public static LFTree ReflectXY(this LFTree shape) { return LFTree.From(libfive_stdlib.reflect_xy(H(shape)), shape); }
    /// <summary>Reflects across the plane Y = Z (swaps y and z).</summary>
    public static LFTree ReflectYZ(this LFTree shape) { return LFTree.From(libfive_stdlib.reflect_yz(H(shape)), shape); }
    /// <summary>Reflects across the plane X = Z (swaps x and z).</summary>
    public static LFTree ReflectXZ(this LFTree shape) { return LFTree.From(libfive_stdlib.reflect_xz(H(shape)), shape); }

    /// <summary>Keeps the x ≥ 0 half and mirrors it to x &lt; 0.</summary>
    public static LFTree SymmetricX(this LFTree shape) { return LFTree.From(libfive_stdlib.symmetric_x(H(shape)), shape); }
    public static LFTree SymmetricY(this LFTree shape) { return LFTree.From(libfive_stdlib.symmetric_y(H(shape)), shape); }
    public static LFTree SymmetricZ(this LFTree shape) { return LFTree.From(libfive_stdlib.symmetric_z(H(shape)), shape); }

    public static LFTree ScaleX(this LFTree shape, LFTree sx, LFTree x0 = null) { x0 = x0 ?? new LFTree(0f); return LFTree.From(libfive_stdlib.scale_x(H(shape), H(sx), H(x0)), shape, sx, x0); }
    public static LFTree ScaleY(this LFTree shape, LFTree sy, LFTree y0 = null) { y0 = y0 ?? new LFTree(0f); return LFTree.From(libfive_stdlib.scale_y(H(shape), H(sy), H(y0)), shape, sy, y0); }
    public static LFTree ScaleZ(this LFTree shape, LFTree sz, LFTree z0 = null) { z0 = z0 ?? new LFTree(0f); return LFTree.From(libfive_stdlib.scale_z(H(shape), H(sz), H(z0)), shape, sz, z0); }
    public static LFTree Scale(this LFTree shape, LFVec3 scale, LFVec3 center = default(LFVec3)) {
      scale = V(scale); center = V(center);
      return LFTree.From(libfive_stdlib.scale_xyz(H(shape), scale.Native, center.Native), shape, scale, center);
    }

    /// <summary>Rotates about the X axis by <paramref name="radians"/> around <paramref name="center"/>.</summary>
    public static LFTree RotateX(this LFTree shape, LFTree radians, LFVec3 center = default(LFVec3)) {
      center = V(center);
      return LFTree.From(libfive_stdlib.rotate_x(H(shape), H(radians), center.Native), shape, radians, center);
    }
    public static LFTree RotateY(this LFTree shape, LFTree radians, LFVec3 center = default(LFVec3)) {
      center = V(center);
      return LFTree.From(libfive_stdlib.rotate_y(H(shape), H(radians), center.Native), shape, radians, center);
    }
    public static LFTree RotateZ(this LFTree shape, LFTree radians, LFVec3 center = default(LFVec3)) {
      center = V(center);
      return LFTree.From(libfive_stdlib.rotate_z(H(shape), H(radians), center.Native), shape, radians, center);
    }

    /// <summary>Scales X as a function of Y: width × baseScale at base.y, width × scale at base.y + height.</summary>
    public static LFTree TaperXY(this LFTree shape, LFVec2 basePoint, LFTree height, LFTree scale, LFTree baseScale = null) {
      basePoint = V(basePoint); baseScale = baseScale ?? new LFTree(1f);
      return LFTree.From(libfive_stdlib.taper_x_y(H(shape), basePoint.Native, H(height), H(scale), H(baseScale)), shape, basePoint, height, scale, baseScale);
    }
    /// <summary>Scales XY as a function of Z: × baseScale at base.z, × scale at base.z + height.</summary>
    public static LFTree TaperXYZ(this LFTree shape, LFVec3 basePoint, LFTree height, LFTree scale, LFTree baseScale = null) {
      basePoint = V(basePoint); baseScale = baseScale ?? new LFTree(1f);
      return LFTree.From(libfive_stdlib.taper_xy_z(H(shape), basePoint.Native, H(height), H(scale), H(baseScale)), shape, basePoint, height, scale, baseScale);
    }
    /// <summary>Shears X as a function of Y: offset baseOffset at base.y, <paramref name="offset"/> at base.y + height.</summary>
    public static LFTree ShearXY(this LFTree shape, LFVec2 basePoint, LFTree height, LFTree offset, LFTree baseOffset = null) {
      basePoint = V(basePoint); baseOffset = baseOffset ?? new LFTree(0f);
      return LFTree.From(libfive_stdlib.shear_x_y(H(shape), basePoint.Native, H(height), H(offset), H(baseOffset)), shape, basePoint, height, offset, baseOffset);
    }

    public enum Axis { None, X, Y, Z, XY, YZ, XZ }

    /// <summary>Pushes the shape away from a point (Axis.None), a plane (X/Y/Z) or a line (XY/YZ/XZ) within <paramref name="radius"/>.</summary>
    public static LFTree Repel(this LFTree shape, LFVec3 locus, LFTree radius, LFTree exaggerate = null, Axis axis = Axis.None) {
      locus = V(locus); exaggerate = exaggerate ?? new LFTree(1f);
      IntPtr r;
      switch (axis) {
        case Axis.X: r = libfive_stdlib.repel_x(H(shape), locus.Native, H(radius), H(exaggerate)); break;
        case Axis.Y: r = libfive_stdlib.repel_y(H(shape), locus.Native, H(radius), H(exaggerate)); break;
        case Axis.Z: r = libfive_stdlib.repel_z(H(shape), locus.Native, H(radius), H(exaggerate)); break;
        case Axis.XY: r = libfive_stdlib.repel_xy(H(shape), locus.Native, H(radius), H(exaggerate)); break;
        case Axis.YZ: r = libfive_stdlib.repel_yz(H(shape), locus.Native, H(radius), H(exaggerate)); break;
        case Axis.XZ: r = libfive_stdlib.repel_xz(H(shape), locus.Native, H(radius), H(exaggerate)); break;
        default: r = libfive_stdlib.repel(H(shape), locus.Native, H(radius), H(exaggerate)); break;
      }
      return LFTree.From(r, shape, locus, radius, exaggerate);
    }

    /// <summary>Pulls the shape toward a point (Axis.None), a plane (X/Y/Z) or a line (XY/YZ/XZ) within <paramref name="radius"/>.</summary>
    public static LFTree Attract(this LFTree shape, LFVec3 locus, LFTree radius, LFTree exaggerate = null, Axis axis = Axis.None) {
      locus = V(locus); exaggerate = exaggerate ?? new LFTree(1f);
      IntPtr r;
      switch (axis) {
        case Axis.X: r = libfive_stdlib.attract_x(H(shape), locus.Native, H(radius), H(exaggerate)); break;
        case Axis.Y: r = libfive_stdlib.attract_y(H(shape), locus.Native, H(radius), H(exaggerate)); break;
        case Axis.Z: r = libfive_stdlib.attract_z(H(shape), locus.Native, H(radius), H(exaggerate)); break;
        case Axis.XY: r = libfive_stdlib.attract_xy(H(shape), locus.Native, H(radius), H(exaggerate)); break;
        case Axis.YZ: r = libfive_stdlib.attract_yz(H(shape), locus.Native, H(radius), H(exaggerate)); break;
        case Axis.XZ: r = libfive_stdlib.attract_xz(H(shape), locus.Native, H(radius), H(exaggerate)); break;
        default: r = libfive_stdlib.attract(H(shape), locus.Native, H(radius), H(exaggerate)); break;
      }
      return LFTree.From(r, shape, locus, radius, exaggerate);
    }

    /// <summary>Revolves a 2D (XY) shape around the line x = x0 parallel to the Y axis.</summary>
    public static LFTree RevolveY(this LFTree shape, LFTree x0 = null) { x0 = x0 ?? new LFTree(0f); return LFTree.From(libfive_stdlib.revolve_y(H(shape), H(x0)), shape, x0); }

    /// <summary>Twists the shape around an axis through <paramref name="center"/>; amount in radians, falling off over <paramref name="radius"/>.</summary>
    public static LFTree Twirl(this LFTree shape, Axis axis, LFTree amount, LFTree radius, LFVec3 center = default(LFVec3), bool aboutFullAxis = false) {
      center = V(center);
      IntPtr r;
      switch (axis) {
        case Axis.X: r = aboutFullAxis ? libfive_stdlib.twirl_axis_x(H(shape), H(amount), H(radius), center.Native) : libfive_stdlib.twirl_x(H(shape), H(amount), H(radius), center.Native); break;
        case Axis.Y: r = aboutFullAxis ? libfive_stdlib.twirl_axis_y(H(shape), H(amount), H(radius), center.Native) : libfive_stdlib.twirl_y(H(shape), H(amount), H(radius), center.Native); break;
        case Axis.Z: r = aboutFullAxis ? libfive_stdlib.twirl_axis_z(H(shape), H(amount), H(radius), center.Native) : libfive_stdlib.twirl_z(H(shape), H(amount), H(radius), center.Native); break;
        default: throw new ArgumentException("Twirl needs a single axis (X, Y or Z).", nameof(axis));
      }
      return LFTree.From(r, shape, amount, radius, center);
    }
    #endregion
  }
}
