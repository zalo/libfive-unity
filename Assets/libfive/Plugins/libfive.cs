namespace libfivesharp.libFiveInternal {
  using System;
  using System.Runtime.InteropServices;

  public struct libfive_interval { public float lower, upper; };
  public struct libfive_region2  { public libfive_interval X, Y; };
  public struct libfive_region3  { public libfive_interval X, Y, Z; };

  public struct libfive_vec2 { public float x, y; };
  public struct libfive_vec3 { public float x, y, z; };
  public struct libfive_vec4 { public float x, y, z, w; };
  public struct libfive_tri  { public UInt32 a, b, c; };

  [StructLayout(LayoutKind.Sequential, Pack = 1)]
  public struct libfive_contour {
    public IntPtr pts; //libfive_vec2*
    public UInt32 count;
  };

  [StructLayout(LayoutKind.Sequential, Pack = 1)]
  public struct libfive_contours {
    public IntPtr cs; //libfive_contour*
    public UInt32 count;
  };

  [StructLayout(LayoutKind.Sequential, Pack = 1)]
  public struct libfive_mesh {
    public IntPtr verts; //libfive_vec3*
    public IntPtr tris; //libfive_tri*
    public UInt32 tri_count;
    public UInt32 vert_count;
  };

  [StructLayout(LayoutKind.Sequential, Pack = 1)]
  public struct libfive_pixels {
    public IntPtr pixels; //bool*
    public UInt32 width;
    public UInt32 height;
  };

  public class libfive {

    /// <summary>Frees a libfive_contours data structure</summary>
    [DllImport("five", EntryPoint = "libfive_contours_delete")]
    public static extern void libfive_contours_delete(IntPtr cs);

    /// <summary>Frees a libfive_mesh data structure</summary>
    [DllImport("five", EntryPoint = "libfive_mesh_delete")]
    public static extern void libfive_mesh_delete(IntPtr m);

    /// <summary>Frees a libfive_pixels data structure</summary>
    [DllImport("five", EntryPoint = "libfive_pixels_delete")]
    public static extern void libfive_pixels_delete(IntPtr ps);

    /// <summary>Takes a string description of an op-code ('min', 'max', etc) and 
    /// returns the Kernel::Opcode value, or -1 if no such value exists.</summary>
    [DllImport("five", EntryPoint = "libfive_opcode_enum", CharSet = CharSet.Ansi)]
    public static extern int libfive_opcode_enum(string op);

    /// <summary>Returns the number of arguments for the given opcode 
    /// (either 0, 1, 2, or -1 if the opcode is invalid)</summary>
    [DllImport("five", EntryPoint = "libfive_opcode_args")]
    public static extern int libfive_opcode_args(int op);


    /// <summary>Creates a basic libfive_tree x variable,
    /// one of the building blocks of custom functions</summary>
    [DllImport("five", EntryPoint = "libfive_tree_x")]
    public static extern IntPtr libfive_tree_x();

    /// <summary>Creates a basic libfive_tree y variable,
    /// one of the building blocks of custom functions</summary>
    [DllImport("five", EntryPoint = "libfive_tree_y")]
    public static extern IntPtr libfive_tree_y();

    /// <summary>Creates a basic libfive_tree z variable,
    /// one of the building blocks of custom functions</summary>
    [DllImport("five", EntryPoint = "libfive_tree_z")]
    public static extern IntPtr libfive_tree_z();


    /// <summary>Creates a basic libfive_tree variable,
    /// one of the building blocks of custom functions</summary>
    [DllImport("five", EntryPoint = "libfive_tree_var")]
    public static extern IntPtr libfive_tree_var();

    /// <summary>Returns whether the given tree is a variable 
    /// (vs. a constant)</summary>
    [DllImport("five", EntryPoint = "libfive_tree_is_var")]
    public static extern bool libfive_tree_is_var(IntPtr tree);


    /// <summary>Creates a basic libfive_tree constant,
    /// one of the building blocks of custom functions</summary>
    [DllImport("five", EntryPoint = "libfive_tree_const")]
    public static extern IntPtr libfive_tree_const(float constantValue);

    /// <summary>Returns the constant that this tree represents</summary>
    [DllImport("five", EntryPoint = "libfive_tree_get_const")]
    public static extern float libfive_tree_get_const(IntPtr tree, ref bool success);


    /// <summary>Converts this tree's variables into constants</summary>
    [DllImport("five", EntryPoint = "libfive_tree_constant_vars")]
    public static extern IntPtr libfive_tree_constant_vars(IntPtr tree);


    /// <summary>Creates a tree from a nonary operation code</summary>
    [DllImport("five", EntryPoint = "libfive_tree_nonary")]
    public static extern IntPtr libfive_tree_nonary(int op);

    /// <summary>Creates a new tree by applying an operation code to a tree</summary>
    [DllImport("five", EntryPoint = "libfive_tree_unary")]
    public static extern IntPtr libfive_tree_unary(int op, IntPtr a);

    /// <summary>Creates a new tree by operating tree "A" onto tree "B"</summary>
    [DllImport("five", EntryPoint = "libfive_tree_binary")]
    public static extern IntPtr libfive_tree_binary(int op, IntPtr a, IntPtr b);


    /// <summary>Gets this tree's ID</summary>
    [DllImport("five", EntryPoint = "libfive_tree_id")]
    public static extern IntPtr libfive_tree_id(IntPtr t);


    /// <summary>Evaluates the tree's function at point p</summary>
    [DllImport("five", EntryPoint = "libfive_tree_eval_f")]
    public static extern float libfive_tree_eval_f(IntPtr t, libfive_vec3 p);

    /// <summary>Evaluates the tree inside of region r</summary>
    [DllImport("five", EntryPoint = "libfive_tree_eval_r")]
    public static extern libfive_interval libfive_tree_eval_r(IntPtr t, libfive_region3 r);

    /// <summary>Evaluates the gradient of the tree at point p</summary>
    [DllImport("five", EntryPoint = "libfive_tree_eval_d")]
    public static extern libfive_vec3 libfive_tree_eval_d(IntPtr t, libfive_vec3 p);


    /// <summary>Returns whether tree "A" is equal to tree "B"</summary>
    [DllImport("five", EntryPoint = "libfive_tree_eq")]
    public static extern bool libfive_tree_eq(IntPtr a, IntPtr b);


    /// <summary>Frees a libfive_tree data structure</summary>
    [DllImport("five", EntryPoint = "libfive_tree_delete")]
    public static extern void libfive_tree_delete(IntPtr t);


    /// <summary>Serializes and saves a tree</summary>
    [DllImport("five", EntryPoint = "libfive_tree_save", CharSet = CharSet.Ansi)]
    public static extern void libfive_tree_save(IntPtr tree, string filename);

    /// <summary>Loads and deserializes a tree structure</summary>
    [DllImport("five", EntryPoint = "libfive_tree_load", CharSet = CharSet.Ansi)]
    public static extern IntPtr libfive_tree_load(string filename);

    /// <summary>Remaps the coordinate space of a tree</summary>
    [DllImport("five", EntryPoint = "libfive_tree_remap")]
    public static extern IntPtr libfive_tree_remap(IntPtr p, IntPtr x, IntPtr y, IntPtr z);


    /// <summary>Returns a C string representing the tree in Scheme style 
    /// (e.g. "(+ 1 2 x y)" ) The caller is responsible for freeing the 
    /// string with free()</summary>
    [DllImport("five", EntryPoint = "libfive_tree_print", CharSet = CharSet.Ansi)]
    public static extern IntPtr libfive_tree_print(IntPtr t);


    /// <summary>
    /// Renders a tree to a set of contours.   
    /// R is a region that will be subdivided into an octree.  
    /// For clean triangles, it should be near-cubical, but that 
    /// isn't a hard requirement.   
    /// res should be approximately half the model's smallest feature 
    /// size; subdivision halts when all sides of the region are below it.  
    /// The returned struct must be freed with libfive_contours_delete.
    /// </summary>
    [DllImport("five", EntryPoint = "libfive_tree_render_slice")]
    public static extern IntPtr libfive_tree_render_slice(IntPtr tree, libfive_region2 R, float z, float res);

    /// <summary>Renders and saves a slice to a file.  See argument 
    /// details in libfive_tree_render_slice.</summary>
    [DllImport("five", EntryPoint = "libfive_tree_save_slice", CharSet = CharSet.Ansi)]
    public static extern void libfive_tree_save_slice(IntPtr tree, libfive_region2 R, float z, float res, string f);


    /// <summary>
    /// Renders a tree to a set of triangles 
    /// 
    /// R is a region that will be subdivided into an octree.For clean 
    /// triangles, it should be near-cubical, but that isn't a hard requirement.  
    /// 
    /// res should be approximately half the model's smallest feature size; 
    /// subdivision halts when all sides of the region are below it. 
    /// 
    /// The returned struct must be freed with libfive_mesh_delete.
    /// </summary>
    [DllImport("five", EntryPoint = "libfive_tree_render_mesh")]
    public static extern IntPtr libfive_tree_render_mesh(IntPtr tree, libfive_region3 R, float res);

    /// <summary>
    /// Renders and saves a mesh to a file.  
    /// 
    /// Returns true on success, false otherwise.  
    /// See argument details in libfive_tree_render_mesh.
    /// </summary>
    [DllImport("five", EntryPoint = "libfive_tree_save_mesh", CharSet = CharSet.Ansi)]
    public static extern bool libfive_tree_save_mesh(IntPtr tree, libfive_region3 R, float res, string f);


    /// <summary>
    /// Renders a 2D slice of pixels at the given Z height.  
    /// 
    /// The returned struct must be freed with libfive_pixels_delete.
    /// </summary>
    [DllImport("five", EntryPoint = "libfive_tree_render_pixels")]
    public static extern IntPtr libfive_tree_render_pixels(IntPtr tree, libfive_region2 R, float z, float res);


    /// <summary>Returns the human-readable tag associated with this 
    /// build, or the empty string if there is no such tag</summary>
    [DllImport("five", EntryPoint = "libfive_git_version", CharSet = CharSet.Ansi)]
    public static extern IntPtr libfive_git_version();

    /// <summary>Returns the 7-character git hash associated with this build, 
    /// with a trailing '+' if there are local (uncommitted) modifications</summary>
    [DllImport("five", EntryPoint = "libfive_git_revision", CharSet = CharSet.Ansi)]
    public static extern IntPtr libfive_git_revision();

    /// <summary>Returns the name of the branch associated with this build</summary>
    [DllImport("five", EntryPoint = "libfive_git_branch", CharSet = CharSet.Ansi)]
    public static extern IntPtr libfive_git_branch();
  }

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
    ORACLE = 32
  };

}