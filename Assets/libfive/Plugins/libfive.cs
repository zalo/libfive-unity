// Raw P/Invoke bindings for libfive's C API (libfive/include/libfive.h).
//
// These mirror the header one-to-one and do no lifetime management; use the
// managed wrappers in libfivesharp (LFTree, LFMath, Context) instead unless you
// need something they don't expose.
//
// Conventions:
//   * libfive_tree, libfive_evaluator and the result structs are opaque IntPtrs.
//   * Every tree handle returned by these functions is a *new reference* that
//     must eventually be released with libfive_tree_delete. Passing a handle as
//     an argument never transfers ownership (the library increments the refcount
//     internally), so it is always safe to delete your handle afterwards.
//   * C `bool` is one byte; every bool crossing the boundary is marshalled as I1.
//   * Structs use the natural (C) layout. Do not add Pack = 1: libfive_contour
//     and friends are read from arrays with the C stride.
namespace libfivesharp.libFiveInternal {
  using System;
  using System.Runtime.InteropServices;

  [StructLayout(LayoutKind.Sequential)] public struct libfive_interval { public float lower, upper; }
  [StructLayout(LayoutKind.Sequential)] public struct libfive_region2 { public libfive_interval X, Y; }
  [StructLayout(LayoutKind.Sequential)] public struct libfive_region3 { public libfive_interval X, Y, Z; }

  [StructLayout(LayoutKind.Sequential)] public struct libfive_vec2 { public float x, y; }
  [StructLayout(LayoutKind.Sequential)] public struct libfive_vec3 { public float x, y, z; }
  [StructLayout(LayoutKind.Sequential)] public struct libfive_vec4 { public float x, y, z, w; }
  [StructLayout(LayoutKind.Sequential)] public struct libfive_tri { public UInt32 a, b, c; }

  /// <summary>A single 2D contour: `count` libfive_vec2 points.</summary>
  [StructLayout(LayoutKind.Sequential)]
  public struct libfive_contour {
    public IntPtr pts; // libfive_vec2*
    public UInt32 count;
  }

  /// <summary>A set of 2D contours: `count` libfive_contour structs.</summary>
  [StructLayout(LayoutKind.Sequential)]
  public struct libfive_contours {
    public IntPtr cs; // libfive_contour*
    public UInt32 count;
  }

  /// <summary>A single contour of 3D points.</summary>
  [StructLayout(LayoutKind.Sequential)]
  public struct libfive_contour3 {
    public IntPtr pts; // libfive_vec3*
    public UInt32 count;
  }

  /// <summary>A set of 3D contours.</summary>
  [StructLayout(LayoutKind.Sequential)]
  public struct libfive_contours3 {
    public IntPtr cs; // libfive_contour3*
    public UInt32 count;
  }

  /// <summary>An indexed triangle mesh: vert_count libfive_vec3 and tri_count libfive_tri.</summary>
  [StructLayout(LayoutKind.Sequential)]
  public struct libfive_mesh {
    public IntPtr verts; // libfive_vec3*
    public IntPtr tris;  // libfive_tri*
    public UInt32 tri_count;
    public UInt32 vert_count;
  }

  /// <summary>Alternate mesh format: polygons as index runs separated by -1.</summary>
  [StructLayout(LayoutKind.Sequential)]
  public struct libfive_mesh_coords {
    public IntPtr verts; // libfive_vec3*
    public UInt32 vert_count;
    public IntPtr coord_indices; // int32_t*
    public UInt32 coord_index_count;
  }

  /// <summary>Row-major occupancy bitmap of width * height C bools (one byte each).</summary>
  [StructLayout(LayoutKind.Sequential)]
  public struct libfive_pixels {
    public IntPtr pixels; // bool*
    public UInt32 width;
    public UInt32 height;
  }

  /// <summary>Maps free-variable ids (from libfive_tree_id) to values.
  /// Build this from pinned memory you own; do NOT call libfive_vars_delete on it.</summary>
  [StructLayout(LayoutKind.Sequential)]
  public struct libfive_vars {
    public IntPtr vars;   // void* const*
    public IntPtr values; // float*
    public UInt32 size;
  }

  /// <summary>
  /// Opcode values as compiled into libfive by default (LIBFIVE_PACKED_OPCODES off).
  /// These are only a fallback: libfivesharp.LFOpcode resolves the real values at
  /// runtime through libfive_opcode_enum so a mismatched binary can't silently
  /// build the wrong tree.
  /// </summary>
  public enum libfive_opcode : int {
    INVALID = 0,
    CONSTANT = 1,
    VAR_X = 2,
    VAR_Y = 3,
    VAR_Z = 4,
    VAR_FREE = 5,
    CONST_VAR = 6,
    OP_SQUARE = 7,
    OP_SQRT = 8,
    OP_NEG = 9,
    OP_SIN = 10,
    OP_COS = 11,
    OP_TAN = 12,
    OP_ASIN = 13,
    OP_ACOS = 14,
    OP_ATAN = 15,
    OP_EXP = 16,
    OP_ADD = 17,
    OP_MUL = 18,
    OP_MIN = 19,
    OP_MAX = 20,
    OP_SUB = 21,
    OP_DIV = 22,
    OP_ATAN2 = 23,
    OP_POW = 24,
    OP_NTH_ROOT = 25,
    OP_MOD = 26,
    OP_NANFILL = 27,
    OP_ABS = 28,
    OP_RECIP = 29,
    OP_LOG = 30,
    OP_COMPARE = 31,
    ORACLE = 32,
    LAST_OP = 33
  }

  public static class libfive {
    /// <summary>
    /// Plugin name passed to DllImport. Unity resolves it to libfive.dll,
    /// libfive.dylib or libfive.so under Assets/libfive/Plugins.
    /// </summary>
    public const string LibraryName = "libfive";

    #region Result deallocation
    [DllImport(LibraryName, EntryPoint = "libfive_contours_delete")]
    public static extern void libfive_contours_delete(IntPtr cs);

    [DllImport(LibraryName, EntryPoint = "libfive_contours3_delete")]
    public static extern void libfive_contours3_delete(IntPtr cs);

    [DllImport(LibraryName, EntryPoint = "libfive_mesh_delete")]
    public static extern void libfive_mesh_delete(IntPtr m);

    [DllImport(LibraryName, EntryPoint = "libfive_mesh_coords_delete")]
    public static extern void libfive_mesh_coords_delete(IntPtr m);

    [DllImport(LibraryName, EntryPoint = "libfive_pixels_delete")]
    public static extern void libfive_pixels_delete(IntPtr ps);
    #endregion

    #region Opcodes
    /// <summary>Scheme-style opcode name ("min", "nth-root", "var-x", ...) to opcode value, or -1.</summary>
    [DllImport(LibraryName, EntryPoint = "libfive_opcode_enum", CharSet = CharSet.Ansi)]
    public static extern int libfive_opcode_enum([MarshalAs(UnmanagedType.LPStr)] string op);

    /// <summary>Number of arguments for an opcode (0, 1, 2) or -1 if invalid.</summary>
    [DllImport(LibraryName, EntryPoint = "libfive_opcode_args")]
    public static extern int libfive_opcode_args(int op);
    #endregion

    #region Tree construction
    [DllImport(LibraryName, EntryPoint = "libfive_tree_x")]
    public static extern IntPtr libfive_tree_x();

    [DllImport(LibraryName, EntryPoint = "libfive_tree_y")]
    public static extern IntPtr libfive_tree_y();

    [DllImport(LibraryName, EntryPoint = "libfive_tree_z")]
    public static extern IntPtr libfive_tree_z();

    /// <summary>A new free variable (each call is a distinct variable).</summary>
    [DllImport(LibraryName, EntryPoint = "libfive_tree_var")]
    public static extern IntPtr libfive_tree_var();

    [DllImport(LibraryName, EntryPoint = "libfive_tree_is_var")]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool libfive_tree_is_var(IntPtr t);

    [DllImport(LibraryName, EntryPoint = "libfive_tree_const")]
    public static extern IntPtr libfive_tree_const(float f);

    /// <summary>If t is a constant, returns it and sets success to 1; otherwise returns 0 with success 0.</summary>
    [DllImport(LibraryName, EntryPoint = "libfive_tree_get_const")]
    public static extern float libfive_tree_get_const(IntPtr t, out byte success);

    /// <summary>Tree from a zero-argument opcode; NULL if the opcode is invalid.</summary>
    [DllImport(LibraryName, EntryPoint = "libfive_tree_nullary")]
    public static extern IntPtr libfive_tree_nullary(int op);

    /// <summary>Tree from a one-argument opcode; NULL if the opcode or argument is invalid.</summary>
    [DllImport(LibraryName, EntryPoint = "libfive_tree_unary")]
    public static extern IntPtr libfive_tree_unary(int op, IntPtr a);

    /// <summary>Tree from a two-argument opcode; NULL if the opcode or arguments are invalid.</summary>
    [DllImport(LibraryName, EntryPoint = "libfive_tree_binary")]
    public static extern IntPtr libfive_tree_binary(int op, IntPtr a, IntPtr b);

    /// <summary>Unique id for a tree node (mainly for identifying free variables).</summary>
    [DllImport(LibraryName, EntryPoint = "libfive_tree_id")]
    public static extern IntPtr libfive_tree_id(IntPtr t);

    /// <summary>Releases one reference to the tree.</summary>
    [DllImport(LibraryName, EntryPoint = "libfive_tree_delete")]
    public static extern void libfive_tree_delete(IntPtr ptr);

    /// <summary>q(x,y,z) = p(x'(x,y,z), y'(x,y,z), z'(x,y,z)).</summary>
    [DllImport(LibraryName, EntryPoint = "libfive_tree_remap")]
    public static extern IntPtr libfive_tree_remap(IntPtr p, IntPtr x, IntPtr y, IntPtr z);

    /// <summary>Returns an optimized (constant-folded, deduplicated) copy of the tree.</summary>
    [DllImport(LibraryName, EntryPoint = "libfive_tree_optimized")]
    public static extern IntPtr libfive_tree_optimized(IntPtr t);
    #endregion

    #region Evaluation
    /// <summary>Evaluates the tree at a point (free variables are treated as zero).</summary>
    [DllImport(LibraryName, EntryPoint = "libfive_tree_eval_f")]
    public static extern float libfive_tree_eval_f(IntPtr t, libfive_vec3 p);

    /// <summary>Interval-evaluates the tree over a region.</summary>
    [DllImport(LibraryName, EntryPoint = "libfive_tree_eval_r")]
    public static extern libfive_interval libfive_tree_eval_r(IntPtr t, libfive_region3 r);

    /// <summary>Partial derivatives (d/dx, d/dy, d/dz) at a point.</summary>
    [DllImport(LibraryName, EntryPoint = "libfive_tree_eval_d")]
    public static extern libfive_vec3 libfive_tree_eval_d(IntPtr t, libfive_vec3 p);
    #endregion

    #region Serialization / printing
    /// <summary>Serializes a tree to a file (format is not archival).</summary>
    [DllImport(LibraryName, EntryPoint = "libfive_tree_save", CharSet = CharSet.Ansi)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool libfive_tree_save(IntPtr ptr, [MarshalAs(UnmanagedType.LPStr)] string filename);

    /// <summary>Deserializes a tree from a file; NULL on failure.</summary>
    [DllImport(LibraryName, EntryPoint = "libfive_tree_load", CharSet = CharSet.Ansi)]
    public static extern IntPtr libfive_tree_load([MarshalAs(UnmanagedType.LPStr)] string filename);

    /// <summary>Scheme-style string for the tree. Free the result with libfive_free_str.</summary>
    [DllImport(LibraryName, EntryPoint = "libfive_tree_print")]
    public static extern IntPtr libfive_tree_print(IntPtr t);

    [DllImport(LibraryName, EntryPoint = "libfive_free_str")]
    public static extern void libfive_free_str(IntPtr ptr);
    #endregion

    #region Rendering
    // In every render function `res` is the number of octree cells per unit
    // length (libfive sets min_feature = 1/res), i.e. higher = finer.

    /// <summary>2D contours of the z-slice. Free with libfive_contours_delete.</summary>
    [DllImport(LibraryName, EntryPoint = "libfive_tree_render_slice")]
    public static extern IntPtr libfive_tree_render_slice(IntPtr tree, libfive_region2 R, float z, float res);

    /// <summary>Same as render_slice but with 3D points. Free with libfive_contours3_delete.</summary>
    [DllImport(LibraryName, EntryPoint = "libfive_tree_render_slice3")]
    public static extern IntPtr libfive_tree_render_slice3(IntPtr tree, libfive_region2 R, float z, float res);

    /// <summary>Renders a slice and saves it as SVG.</summary>
    [DllImport(LibraryName, EntryPoint = "libfive_tree_save_slice", CharSet = CharSet.Ansi)]
    public static extern void libfive_tree_save_slice(IntPtr tree, libfive_region2 R, float z, float res,
                                                      [MarshalAs(UnmanagedType.LPStr)] string f);

    /// <summary>Multithreaded dual-contouring mesh. Returns NULL for an empty mesh. Free with libfive_mesh_delete.</summary>
    [DllImport(LibraryName, EntryPoint = "libfive_tree_render_mesh")]
    public static extern IntPtr libfive_tree_render_mesh(IntPtr tree, libfive_region3 R, float res);

    /// <summary>Single-threaded variant of libfive_tree_render_mesh.</summary>
    [DllImport(LibraryName, EntryPoint = "libfive_tree_render_mesh_st")]
    public static extern IntPtr libfive_tree_render_mesh_st(IntPtr tree, libfive_region3 R, float res);

    /// <summary>Mesh in the -1-separated polygon format. Free with libfive_mesh_coords_delete.</summary>
    [DllImport(LibraryName, EntryPoint = "libfive_tree_render_mesh_coords")]
    public static extern IntPtr libfive_tree_render_mesh_coords(IntPtr tree, libfive_region3 R, float res);

    /// <summary>Renders and saves a binary STL.</summary>
    [DllImport(LibraryName, EntryPoint = "libfive_tree_save_mesh", CharSet = CharSet.Ansi)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool libfive_tree_save_mesh(IntPtr tree, libfive_region3 R, float res,
                                                     [MarshalAs(UnmanagedType.LPStr)] string f);

    /// <summary>Renders (single-threaded) with a prebuilt evaluator and saves a binary STL.</summary>
    [DllImport(LibraryName, EntryPoint = "libfive_evaluator_save_mesh", CharSet = CharSet.Ansi)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool libfive_evaluator_save_mesh(IntPtr evaluator, libfive_region3 R,
                                                          [MarshalAs(UnmanagedType.LPStr)] string f);

    /// <summary>Renders several trees into one STL. `trees` must be NULL-terminated.
    /// quality q collapses octree cells when the QEF error is below 10^-q.</summary>
    [DllImport(LibraryName, EntryPoint = "libfive_tree_save_meshes", CharSet = CharSet.Ansi)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool libfive_tree_save_meshes(IntPtr[] trees, libfive_region3 R, float res, float quality,
                                                       [MarshalAs(UnmanagedType.LPStr)] string f);

    /// <summary>Occupancy bitmap of a z-slice. Free with libfive_pixels_delete.</summary>
    [DllImport(LibraryName, EntryPoint = "libfive_tree_render_pixels")]
    public static extern IntPtr libfive_tree_render_pixels(IntPtr tree, libfive_region2 R, float z, float res);
    #endregion

    #region Evaluators
    [DllImport(LibraryName, EntryPoint = "libfive_tree_evaluator")]
    public static extern IntPtr libfive_tree_evaluator(IntPtr tree, libfive_vars vars);

    [DllImport(LibraryName, EntryPoint = "libfive_evaluator_update_vars")]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool libfive_evaluator_update_vars(IntPtr evaluator, libfive_vars vars);

    [DllImport(LibraryName, EntryPoint = "libfive_evaluator_delete")]
    public static extern void libfive_evaluator_delete(IntPtr evaluator);
    #endregion

    #region libfive-unity helpers (native/shim/libfive_unity.cpp, compiled into the plugin)
    /// <summary>Gradient of the tree at <paramref name="count"/> points (libfive_vec3 arrays). Returns the number evaluated.</summary>
    [DllImport(LibraryName, EntryPoint = "libfive_unity_gradients")]
    public static extern uint libfive_unity_gradients(IntPtr tree, IntPtr points, uint count, IntPtr outGradients);

    /// <summary>
    /// Gradient at every triangle corner of a libfive_mesh, sampled <paramref name="nudge"/> of the way from the
    /// corner towards the triangle centroid. Returns 3 * tri_count libfive_vec3 (free with libfive_unity_free) or null.
    /// </summary>
    [DllImport(LibraryName, EntryPoint = "libfive_unity_mesh_corner_gradients")]
    public static extern IntPtr libfive_unity_mesh_corner_gradients(IntPtr tree, IntPtr mesh, float nudge);

    /// <summary>
    /// Two-offset variant: 2 * 3 * tri_count libfive_vec3, the first 3 * tri_count sampled at <paramref name="offsetA"/>
    /// towards each triangle's centroid, the second at <paramref name="offsetB"/>. Free with libfive_unity_free.
    /// </summary>
    [DllImport(LibraryName, EntryPoint = "libfive_unity_mesh_corner_gradients2")]
    public static extern IntPtr libfive_unity_mesh_corner_gradients2(IntPtr tree, IntPtr mesh, float offsetA, float offsetB);

    [DllImport(LibraryName, EntryPoint = "libfive_unity_free")]
    public static extern void libfive_unity_free(IntPtr p);
    #endregion

    #region Version info (static strings; do not free)
    [DllImport(LibraryName, EntryPoint = "libfive_git_version")]
    public static extern IntPtr libfive_git_version();

    [DllImport(LibraryName, EntryPoint = "libfive_git_revision")]
    public static extern IntPtr libfive_git_revision();

    [DllImport(LibraryName, EntryPoint = "libfive_git_branch")]
    public static extern IntPtr libfive_git_branch();
    #endregion
  }
}
