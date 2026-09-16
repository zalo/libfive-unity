// Raw P/Invoke bindings for libfive's standard library
// (libfive/stdlib/libfive_stdlib.h), which the native build folds into the
// same libfive binary as the core API (see native/CMakeLists.txt).
//
// Every argument that is conceptually a float (tfloat) or vector (tvec2/tvec3)
// is passed as libfive_tree handles so shapes can be parameterized by free
// variables. Ownership follows the core API: arguments are borrowed, results are
// new references that must be released with libfive_tree_delete.
//
// Optional arguments in the header (suffix __0 / __1) have no defaults in C;
// the LFMath wrapper supplies them.
namespace libfivesharp.libFiveInternal {
  using System;
  using System.Runtime.InteropServices;

  [StructLayout(LayoutKind.Sequential)] public struct tvec2 { public IntPtr x, y; }
  [StructLayout(LayoutKind.Sequential)] public struct tvec3 { public IntPtr x, y, z; }

  public static class libfive_stdlib {
    const string Lib = libfive.LibraryName;

    #region csg
    [DllImport(Lib, EntryPoint = "_union")] public static extern IntPtr _union(IntPtr a, IntPtr b);
    [DllImport(Lib, EntryPoint = "intersection")] public static extern IntPtr intersection(IntPtr a, IntPtr b);
    [DllImport(Lib, EntryPoint = "inverse")] public static extern IntPtr inverse(IntPtr a);
    [DllImport(Lib, EntryPoint = "difference")] public static extern IntPtr difference(IntPtr a, IntPtr b);
    /// <summary>Positive offsets expand, negative shrink.</summary>
    [DllImport(Lib, EntryPoint = "offset")] public static extern IntPtr offset(IntPtr a, IntPtr o);
    /// <summary>a minus (b expanded by offset).</summary>
    [DllImport(Lib, EntryPoint = "clearance")] public static extern IntPtr clearance(IntPtr a, IntPtr b, IntPtr offset);
    [DllImport(Lib, EntryPoint = "shell")] public static extern IntPtr shell(IntPtr a, IntPtr offset);
    [DllImport(Lib, EntryPoint = "blend_expt")] public static extern IntPtr blend_expt(IntPtr a, IntPtr b, IntPtr m);
    /// <summary>Exponential blend with m rescaled so 0..1 behaves like blend_rough. This is libfive's "blend".</summary>
    [DllImport(Lib, EntryPoint = "blend_expt_unit")] public static extern IntPtr blend_expt_unit(IntPtr a, IntPtr b, IntPtr m);
    [DllImport(Lib, EntryPoint = "blend_rough")] public static extern IntPtr blend_rough(IntPtr a, IntPtr b, IntPtr m);
    /// <summary>Blended subtraction of b (offset by o) from a with smoothness m.</summary>
    [DllImport(Lib, EntryPoint = "blend_difference")] public static extern IntPtr blend_difference(IntPtr a, IntPtr b, IntPtr m, IntPtr o);
    /// <summary>m = 0 gives a, m = 1 gives b.</summary>
    [DllImport(Lib, EntryPoint = "morph")] public static extern IntPtr morph(IntPtr a, IntPtr b, IntPtr m);
    /// <summary>Loft between 2D shapes a (at zmin) and b (at zmax).</summary>
    [DllImport(Lib, EntryPoint = "loft")] public static extern IntPtr loft(IntPtr a, IntPtr b, IntPtr zmin, IntPtr zmax);
    [DllImport(Lib, EntryPoint = "loft_between")] public static extern IntPtr loft_between(IntPtr a, IntPtr b, tvec3 lower, tvec3 upper);
    #endregion

    #region shapes
    [DllImport(Lib, EntryPoint = "circle")] public static extern IntPtr circle(IntPtr r, tvec2 center);
    [DllImport(Lib, EntryPoint = "ring")] public static extern IntPtr ring(IntPtr ro, IntPtr ri, tvec2 center);
    [DllImport(Lib, EntryPoint = "polygon")] public static extern IntPtr polygon(IntPtr r, int n, tvec2 center);
    [DllImport(Lib, EntryPoint = "rectangle")] public static extern IntPtr rectangle(tvec2 a, tvec2 b);
    [DllImport(Lib, EntryPoint = "rounded_rectangle")] public static extern IntPtr rounded_rectangle(tvec2 a, tvec2 b, IntPtr r);
    [DllImport(Lib, EntryPoint = "rectangle_exact")] public static extern IntPtr rectangle_exact(tvec2 a, tvec2 b);
    [DllImport(Lib, EntryPoint = "rectangle_centered_exact")] public static extern IntPtr rectangle_centered_exact(tvec2 size, tvec2 center);
    [DllImport(Lib, EntryPoint = "triangle")] public static extern IntPtr triangle(tvec2 a, tvec2 b, tvec2 c);

    /// <summary>Box from corners; edges stay creased when offset.</summary>
    [DllImport(Lib, EntryPoint = "box_mitered")] public static extern IntPtr box_mitered(tvec3 a, tvec3 b);
    [DllImport(Lib, EntryPoint = "box_mitered_centered")] public static extern IntPtr box_mitered_centered(tvec3 size, tvec3 center);
    /// <summary>Box with a Euclidean distance field (rounds when offset).</summary>
    [DllImport(Lib, EntryPoint = "box_exact_centered")] public static extern IntPtr box_exact_centered(tvec3 size, tvec3 center);
    [DllImport(Lib, EntryPoint = "box_exact")] public static extern IntPtr box_exact(tvec3 a, tvec3 b);
    /// <summary>r is a 0..1 fraction of the smallest half-extent.</summary>
    [DllImport(Lib, EntryPoint = "rounded_box")] public static extern IntPtr rounded_box(tvec3 a, tvec3 b, IntPtr r);
    [DllImport(Lib, EntryPoint = "sphere")] public static extern IntPtr sphere(IntPtr radius, tvec3 center);
    [DllImport(Lib, EntryPoint = "half_space")] public static extern IntPtr half_space(tvec3 norm, tvec3 point);
    /// <summary>Cylinder along +Z from base.</summary>
    [DllImport(Lib, EntryPoint = "cylinder_z")] public static extern IntPtr cylinder_z(IntPtr r, IntPtr h, tvec3 base_);
    [DllImport(Lib, EntryPoint = "cone_ang_z")] public static extern IntPtr cone_ang_z(IntPtr angle, IntPtr height, tvec3 base_);
    [DllImport(Lib, EntryPoint = "cone_z")] public static extern IntPtr cone_z(IntPtr radius, IntPtr height, tvec3 base_);
    [DllImport(Lib, EntryPoint = "pyramid_z")] public static extern IntPtr pyramid_z(tvec2 a, tvec2 b, IntPtr zmin, IntPtr height);
    [DllImport(Lib, EntryPoint = "torus_z")] public static extern IntPtr torus_z(IntPtr ro, IntPtr ri, tvec3 center);
    [DllImport(Lib, EntryPoint = "gyroid")] public static extern IntPtr gyroid(tvec3 period, IntPtr thickness);
    /// <summary>A shape that is empty everywhere (+infinity).</summary>
    [DllImport(Lib, EntryPoint = "emptiness")] public static extern IntPtr emptiness();

    [DllImport(Lib, EntryPoint = "array_x")] public static extern IntPtr array_x(IntPtr shape, int nx, IntPtr dx);
    [DllImport(Lib, EntryPoint = "array_xy")] public static extern IntPtr array_xy(IntPtr shape, int nx, int ny, tvec2 delta);
    [DllImport(Lib, EntryPoint = "array_xyz")] public static extern IntPtr array_xyz(IntPtr shape, int nx, int ny, int nz, tvec3 delta);
    [DllImport(Lib, EntryPoint = "array_polar_z")] public static extern IntPtr array_polar_z(IntPtr shape, int n, tvec2 center);
    [DllImport(Lib, EntryPoint = "extrude_z")] public static extern IntPtr extrude_z(IntPtr t, IntPtr zmin, IntPtr zmax);
    #endregion

    #region transforms
    [DllImport(Lib, EntryPoint = "move")] public static extern IntPtr move(IntPtr t, tvec3 offset);
    [DllImport(Lib, EntryPoint = "reflect_x")] public static extern IntPtr reflect_x(IntPtr t, IntPtr x0);
    [DllImport(Lib, EntryPoint = "reflect_y")] public static extern IntPtr reflect_y(IntPtr t, IntPtr y0);
    [DllImport(Lib, EntryPoint = "reflect_z")] public static extern IntPtr reflect_z(IntPtr t, IntPtr z0);
    [DllImport(Lib, EntryPoint = "reflect_xy")] public static extern IntPtr reflect_xy(IntPtr t);
    [DllImport(Lib, EntryPoint = "reflect_yz")] public static extern IntPtr reflect_yz(IntPtr t);
    [DllImport(Lib, EntryPoint = "reflect_xz")] public static extern IntPtr reflect_xz(IntPtr t);
    [DllImport(Lib, EntryPoint = "symmetric_x")] public static extern IntPtr symmetric_x(IntPtr t);
    [DllImport(Lib, EntryPoint = "symmetric_y")] public static extern IntPtr symmetric_y(IntPtr t);
    [DllImport(Lib, EntryPoint = "symmetric_z")] public static extern IntPtr symmetric_z(IntPtr t);
    [DllImport(Lib, EntryPoint = "scale_x")] public static extern IntPtr scale_x(IntPtr t, IntPtr sx, IntPtr x0);
    [DllImport(Lib, EntryPoint = "scale_y")] public static extern IntPtr scale_y(IntPtr t, IntPtr sy, IntPtr y0);
    [DllImport(Lib, EntryPoint = "scale_z")] public static extern IntPtr scale_z(IntPtr t, IntPtr sz, IntPtr z0);
    [DllImport(Lib, EntryPoint = "scale_xyz")] public static extern IntPtr scale_xyz(IntPtr t, tvec3 s, tvec3 center);
    /// <summary>Angle in radians.</summary>
    [DllImport(Lib, EntryPoint = "rotate_x")] public static extern IntPtr rotate_x(IntPtr t, IntPtr angle, tvec3 center);
    [DllImport(Lib, EntryPoint = "rotate_y")] public static extern IntPtr rotate_y(IntPtr t, IntPtr angle, tvec3 center);
    [DllImport(Lib, EntryPoint = "rotate_z")] public static extern IntPtr rotate_z(IntPtr t, IntPtr angle, tvec3 center);
    [DllImport(Lib, EntryPoint = "taper_x_y")] public static extern IntPtr taper_x_y(IntPtr shape, tvec2 base_, IntPtr h, IntPtr scale, IntPtr base_scale);
    [DllImport(Lib, EntryPoint = "taper_xy_z")] public static extern IntPtr taper_xy_z(IntPtr shape, tvec3 base_, IntPtr height, IntPtr scale, IntPtr base_scale);
    [DllImport(Lib, EntryPoint = "shear_x_y")] public static extern IntPtr shear_x_y(IntPtr t, tvec2 base_, IntPtr height, IntPtr offset, IntPtr base_offset);
    [DllImport(Lib, EntryPoint = "repel")] public static extern IntPtr repel(IntPtr shape, tvec3 locus, IntPtr radius, IntPtr exaggerate);
    [DllImport(Lib, EntryPoint = "repel_x")] public static extern IntPtr repel_x(IntPtr shape, tvec3 locus, IntPtr radius, IntPtr exaggerate);
    [DllImport(Lib, EntryPoint = "repel_y")] public static extern IntPtr repel_y(IntPtr shape, tvec3 locus, IntPtr radius, IntPtr exaggerate);
    [DllImport(Lib, EntryPoint = "repel_z")] public static extern IntPtr repel_z(IntPtr shape, tvec3 locus, IntPtr radius, IntPtr exaggerate);
    [DllImport(Lib, EntryPoint = "repel_xy")] public static extern IntPtr repel_xy(IntPtr shape, tvec3 locus, IntPtr radius, IntPtr exaggerate);
    [DllImport(Lib, EntryPoint = "repel_yz")] public static extern IntPtr repel_yz(IntPtr shape, tvec3 locus, IntPtr radius, IntPtr exaggerate);
    [DllImport(Lib, EntryPoint = "repel_xz")] public static extern IntPtr repel_xz(IntPtr shape, tvec3 locus, IntPtr radius, IntPtr exaggerate);
    [DllImport(Lib, EntryPoint = "attract")] public static extern IntPtr attract(IntPtr shape, tvec3 locus, IntPtr radius, IntPtr exaggerate);
    [DllImport(Lib, EntryPoint = "attract_x")] public static extern IntPtr attract_x(IntPtr shape, tvec3 locus, IntPtr radius, IntPtr exaggerate);
    [DllImport(Lib, EntryPoint = "attract_y")] public static extern IntPtr attract_y(IntPtr shape, tvec3 locus, IntPtr radius, IntPtr exaggerate);
    [DllImport(Lib, EntryPoint = "attract_z")] public static extern IntPtr attract_z(IntPtr shape, tvec3 locus, IntPtr radius, IntPtr exaggerate);
    [DllImport(Lib, EntryPoint = "attract_xy")] public static extern IntPtr attract_xy(IntPtr shape, tvec3 locus, IntPtr radius, IntPtr exaggerate);
    [DllImport(Lib, EntryPoint = "attract_yz")] public static extern IntPtr attract_yz(IntPtr shape, tvec3 locus, IntPtr radius, IntPtr exaggerate);
    [DllImport(Lib, EntryPoint = "attract_xz")] public static extern IntPtr attract_xz(IntPtr shape, tvec3 locus, IntPtr radius, IntPtr exaggerate);
    /// <summary>Revolves an XY shape about the line x = x0 parallel to Y.</summary>
    [DllImport(Lib, EntryPoint = "revolve_y")] public static extern IntPtr revolve_y(IntPtr shape, IntPtr x0);
    [DllImport(Lib, EntryPoint = "twirl_x")] public static extern IntPtr twirl_x(IntPtr shape, IntPtr amount, IntPtr radius, tvec3 center);
    [DllImport(Lib, EntryPoint = "twirl_axis_x")] public static extern IntPtr twirl_axis_x(IntPtr shape, IntPtr amount, IntPtr radius, tvec3 center);
    [DllImport(Lib, EntryPoint = "twirl_y")] public static extern IntPtr twirl_y(IntPtr shape, IntPtr amount, IntPtr radius, tvec3 center);
    [DllImport(Lib, EntryPoint = "twirl_axis_y")] public static extern IntPtr twirl_axis_y(IntPtr shape, IntPtr amount, IntPtr radius, tvec3 center);
    [DllImport(Lib, EntryPoint = "twirl_z")] public static extern IntPtr twirl_z(IntPtr shape, IntPtr amount, IntPtr radius, tvec3 center);
    [DllImport(Lib, EntryPoint = "twirl_axis_z")] public static extern IntPtr twirl_axis_z(IntPtr shape, IntPtr amount, IntPtr radius, tvec3 center);
    #endregion

    #region text
    /// <summary>Renders text in libfive's built-in f-rep font (character height 1) at pos.</summary>
    [DllImport(Lib, EntryPoint = "text", CharSet = CharSet.Ansi)]
    public static extern IntPtr text([MarshalAs(UnmanagedType.LPStr)] string txt, tvec2 pos);
    #endregion
  }
}
