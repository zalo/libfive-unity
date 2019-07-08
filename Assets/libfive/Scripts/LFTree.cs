using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using libfivesharp.libFiveInternal;

namespace libfivesharp {
  ///<summary>A libfive tree which can represent a constant number, a variable, or a shape.</summary>
  public class LFTree : IDisposable {
    public IntPtr tree;
    ///<summary>Creates a libfive tree from a pointer (and adds it to the active context)</summary>
    public LFTree(IntPtr tree) {
      this.tree = tree;
      if (LFContext.Active != null) LFContext.Active.AddTreeToContext(this);
    }
    public LFTree(float value) {
      tree = libfive.libfive_tree_const(value);
      if (LFContext.Active != null) LFContext.Active.AddTreeToContext(this);
    }

    /// <summary>Creates a basic libfive_tree x variable,
    /// one of the building blocks of custom functions</summary>
    public static LFTree x { get { return new LFTree(libfive.libfive_tree_x()); } }
    /// <summary>Creates a basic libfive_tree y variable,
    /// one of the building blocks of custom functions</summary>
    public static LFTree y { get { return new LFTree(libfive.libfive_tree_y()); } }
    /// <summary>Creates a basic libfive_tree z variable,
    /// one of the building blocks of custom functions</summary>
    public static LFTree z { get { return new LFTree(libfive.libfive_tree_z()); } }

    public static LFTree FreeVar { get { return new LFTree(libfive.libfive_tree_nonary((int)libfive_opcode.VAR_FREE)); } }
    public static LFTree ConstVar { get { return new LFTree(libfive.libfive_tree_nonary((int)libfive_opcode.CONST_VAR)); } }

    #region Operator Overloads

    public static implicit operator IntPtr(LFTree tree) {
      return tree.tree;
    }
    public static implicit operator LFTree(float value) {
      return new LFTree(libfive.libfive_tree_const(value));
    }
    public static LFTree operator +(LFTree first, LFTree second) {
      return new LFTree(libfive.libfive_tree_binary((int)libfive_opcode.OP_ADD, first.tree, second.tree));
    }
    public static LFTree operator -(LFTree first, LFTree second) {
      return new LFTree(libfive.libfive_tree_binary((int)libfive_opcode.OP_SUB, first.tree, second.tree));
    }
    public static LFTree operator -(LFTree a) { return new LFTree(libfive.libfive_tree_unary((int)libfive_opcode.OP_NEG, a.tree)); }
    public static LFTree operator *(LFTree first, LFTree second) {
      return new LFTree(libfive.libfive_tree_binary((int)libfive_opcode.OP_MUL, first.tree, second.tree));
    }
    public static LFTree operator /(LFTree first, LFTree second) {
      return new LFTree(libfive.libfive_tree_binary((int)libfive_opcode.OP_DIV, first.tree, second.tree));
    }
    public static LFTree operator %(LFTree first, LFTree second) {
      return new LFTree(libfive.libfive_tree_binary((int)libfive_opcode.OP_MOD, first.tree, second.tree));
    }

    #endregion

    public IntPtr id { get { return libfive.libfive_tree_id(tree); } }

    /// <summary>
    /// Renders a tree to a set of triangles 
    /// 
    /// R is a region that will be subdivided into an octree.  For clean 
    /// triangles, it should be near-cubical, but that isn't a hard requirement.  
    /// 
    /// res should be approximately half the model's smallest feature size; 
    /// subdivision halts when all sides of the region are below it. 
    /// </summary>
    public void RenderMesh(Mesh meshToFill, Bounds bounds, float resolution = 12.0f, float vertexSplittingAngle = 180f) {
      libfive_region3 bound = new libfive_region3();
      bound.X.lower = bounds.min.x;
      bound.Y.lower = bounds.min.y;
      bound.Z.lower = bounds.min.z;
      bound.X.upper = bounds.max.x;
      bound.Y.upper = bounds.max.y;
      bound.Z.upper = bounds.max.z;
      IntPtr libFiveMeshPtr = libfive.libfive_tree_render_mesh(tree, bound, resolution);

      //Marshal the mesh into vertex and triangle arrays and assign to unity mesh
      libfive_mesh libFiveMesh = (libfive_mesh)Marshal.PtrToStructure(libFiveMeshPtr, typeof(libfive_mesh));

      float[] vertexFloats = new float[libFiveMesh.vert_count * 3];
      int[] triangleIndices = new int[libFiveMesh.tri_count * 3];
      Marshal.Copy(libFiveMesh.verts, vertexFloats, 0, vertexFloats.Length);
      Marshal.Copy(libFiveMesh.tris, triangleIndices, 0, triangleIndices.Length);

      List<Vector3> vertices = new List<Vector3>(vertexFloats.Length * 2 / 3);
      for (int i = 0; i < vertexFloats.Length / 3; i++) {
        vertices.Add(new Vector3(
          vertexFloats[(i * 3)],
          vertexFloats[(i * 3) + 1],
          vertexFloats[(i * 3) + 2]));
      }

      //The original indices were UInt32, so cast to and from those since Unity still needs them as ints
      for (int i = 0; i < triangleIndices.Length; i++) triangleIndices[i] = (int)((UInt32)triangleIndices[i]);

      meshToFill.Clear(); meshToFill.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
      meshToFill.SetVertices(vertices);
      meshToFill.SetTriangles(triangleIndices, 0);
      meshToFill.RecalculateBounds();
      meshToFill.RecalculateNormals(vertexSplittingAngle);

      libfive.libfive_mesh_delete(libFiveMeshPtr);
    }

    /// <summary>
    /// Renders a tree to a set of triangles 
    /// 
    /// R is a region that will be subdivided into an octree.  For clean 
    /// triangles, it should be near-cubical, but that isn't a hard requirement.  
    /// 
    /// res should be approximately half the model's smallest feature size; 
    /// subdivision halts when all sides of the region are below it. 
    /// </summary>
    public Mesh RenderMesh(Bounds bounds, float resolution = 12.0f, float vertexSplittingAngle = 180f) {
      Mesh toRender = new Mesh(); toRender.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
      RenderMesh(toRender, bounds, resolution, vertexSplittingAngle);
      return toRender;
    }

    /// <summary>Remaps the coordinate space of a tree</summary>
    public LFTree Remap(LFTree x, LFTree y, LFTree z) {
      return new LFTree(libfive.libfive_tree_remap(tree, x, y, z));
    }

    ~LFTree() { Dispose(false); }
    public void Dispose() { Dispose(true); GC.SuppressFinalize(this); }
    protected void Dispose(Boolean itIsSafeToAlsoFreeManagedObjects) {
      //Free unmanaged resources
      libfive.libfive_tree_delete(tree);

      //Free managed resources too, but only if I'm being called from Dispose
      //(If I'm being called from Finalize then the objects might not exist anymore)
      if (itIsSafeToAlsoFreeManagedObjects) {
        //Free managed objects here
      }
    }
  }

  public static class LFMath {
    #region Unary Ops
    public static LFTree Atan(LFTree tree) {
      return new LFTree(libfive.libfive_tree_unary((int)libfive_opcode.OP_ATAN, tree));
    }
    public static LFTree Square(LFTree tree) {
      return new LFTree(libfive.libfive_tree_unary((int)libfive_opcode.OP_SQUARE, tree));
    }
    public static LFTree Sqrt(LFTree tree) {
      return new LFTree(libfive.libfive_tree_unary((int)libfive_opcode.OP_SQRT, tree));
    }
    public static LFTree Sin(LFTree tree) {
      return new LFTree(libfive.libfive_tree_unary((int)libfive_opcode.OP_SIN, tree));
    }
    public static LFTree Cos(LFTree tree) {
      return new LFTree(libfive.libfive_tree_unary((int)libfive_opcode.OP_COS, tree));
    }
    public static LFTree Tan(LFTree tree) {
      return new LFTree(libfive.libfive_tree_unary((int)libfive_opcode.OP_TAN, tree));
    }
    public static LFTree ASin(LFTree tree) {
      return new LFTree(libfive.libfive_tree_unary((int)libfive_opcode.OP_ASIN, tree));
    }
    public static LFTree ACos(LFTree tree) {
      return new LFTree(libfive.libfive_tree_unary((int)libfive_opcode.OP_ACOS, tree));
    }
    public static LFTree ATan(LFTree tree) {
      return new LFTree(libfive.libfive_tree_unary((int)libfive_opcode.OP_ATAN, tree));
    }
    public static LFTree Exp(LFTree tree) {
      return new LFTree(libfive.libfive_tree_unary((int)libfive_opcode.OP_EXP, tree));
    }
    public static LFTree Abs(LFTree tree) {
      return new LFTree(libfive.libfive_tree_unary((int)libfive_opcode.OP_ABS, tree));
    }
    public static LFTree Log(LFTree tree) {
      return new LFTree(libfive.libfive_tree_unary((int)libfive_opcode.OP_LOG, tree));
    }
    public static LFTree Reciporical(LFTree tree) {
      return new LFTree(libfive.libfive_tree_unary((int)libfive_opcode.OP_RECIP, tree));
    }
    #endregion

    #region Binary Ops
    public static LFTree Min(LFTree first, LFTree second) {
      return new LFTree(libfive.libfive_tree_binary((int)libfive_opcode.OP_MIN, first, second));
    }
    public static LFTree Max(LFTree first, LFTree second) {
      return new LFTree(libfive.libfive_tree_binary((int)libfive_opcode.OP_MAX, first, second));
    }
    public static LFTree Atan2(LFTree first, LFTree second) {
      return new LFTree(libfive.libfive_tree_binary((int)libfive_opcode.OP_ATAN2, first, second));
    }
    public static LFTree Pow(LFTree first, LFTree second) {
      return new LFTree(libfive.libfive_tree_binary((int)libfive_opcode.OP_POW, first, second));
    }
    public static LFTree nthRoot(LFTree first, LFTree second) {
      return new LFTree(libfive.libfive_tree_binary((int)libfive_opcode.OP_NTH_ROOT, first, second));
    }
    public static LFTree NaNFill(LFTree first, LFTree second) {
      return new LFTree(libfive.libfive_tree_binary((int)libfive_opcode.OP_NANFILL, first, second));
    }
    public static LFTree Compare(LFTree first, LFTree second) {
      return new LFTree(libfive.libfive_tree_binary((int)libfive_opcode.OP_COMPARE, first, second));
    }
    #endregion

    #region Shape Ops

    /// <summary>Creates a sphere centered on the origin</summary>
    public static LFTree Circle(float radius) {
      LFTree x = LFTree.x, y = LFTree.y;
      return Sqrt((Square(x)) + (Square(y))) - radius;
    }

    /// <summary>Creates a sphere</summary>
    public static LFTree Sphere(float radius, Vector3 center) {
      LFTree x = LFTree.x, y = LFTree.y, z = LFTree.z;
      return Sqrt(Square(x - center.x) + Square(y - center.y) + Square(z - center.z)) - radius;
    }
    /// <summary>Creates a sphere centered on the origin</summary>
    public static LFTree Sphere(float radius) {
      return Sphere(radius, Vector3.zero);
    }

    /// <summary>Creates an ellipsoid</summary>
    public static LFTree Ellipsoid(float radius, Vector3 focusA, Vector3 focusB) {
      LFTree x = LFTree.x, y = LFTree.y, z = LFTree.z;
      return Sqrt(Square(x - focusA.x) + Square(y - focusA.y) + Square(z - focusA.z)) + 
             Sqrt(Square(x - focusB.x) + Square(y - focusB.y) + Square(z - focusB.z)) - radius;
    }

    /// <summary>Creates a box with corners at "lower" and "upper"</summary>
    public static LFTree Box(Vector3 lower, Vector3 upper) {
      return Max(Max(
                 Max(lower.x - LFTree.x,
                     LFTree.x - upper.x),
                 Max(lower.y - LFTree.y,
                     LFTree.y - upper.y)),
                 Max(lower.z - LFTree.z,
                     LFTree.z - upper.z));
    }

    /// <summary>Extrudes a 2D shape on the xy plane along the z axis</summary>
    public static LFTree Cylinder(float radius, float height, Vector3 basePosition) {
      return Extrude(Move(Circle(radius), basePosition), basePosition.z, basePosition.z + height);
    }

    /// <summary>Extrudes a 2D shape on the xy plane along the z axis</summary>
    public static LFTree Extrude(LFTree a, float lowerZ, float upperZ) {
      LFTree z = LFTree.z;
      return Max(a, Max(lowerZ - z, z - upperZ));
    }

    /// <summary>Returns a shell of a shape with the given offset</summary>
    public static LFTree Shell(LFTree t, float offset) {
      return Abs(t) - offset;
    }

    #endregion

    #region Transformations
    //Reference: https://github.com/libfive/libfive/blob/master/libfive/bind/transforms.scm

    /// <summary>Translates this shape</summary>
    public static LFTree Move(LFTree t, Vector3 translation) {
      return t.Remap(LFTree.x - translation.x, LFTree.y - translation.y, LFTree.z - translation.z);
    }

    public static LFTree Transform(LFTree t, Matrix4x4 matrix) {
      Matrix4x4 invert = matrix.inverse;
      LFTree x = LFTree.x, y = LFTree.y, z = LFTree.z;
      return t.Remap(
            invert.m00 * x + invert.m01 * y + invert.m02 * z + invert.m03,
            invert.m10 * x + invert.m11 * y + invert.m12 * z + invert.m13,
            invert.m20 * x + invert.m21 * y + invert.m22 * z + invert.m23);
    }

    /// <summary>Reflect the given shape about the x origin or an optional offset"</summary>
    public static LFTree ReflectX(this LFTree t, float offset = 0f) {
      return t.Remap((2f * offset) - LFTree.x, LFTree.y, LFTree.z);
    }
    /// <summary>Reflect the given shape about the y origin or an optional offset"</summary>
    public static LFTree ReflectY(this LFTree t, float offset = 0f) {
      return t.Remap(LFTree.x, (2f * offset) - LFTree.y, LFTree.z);
    }
    /// <summary>Reflect the given shape about the z origin or an optional offset"</summary>
    public static LFTree ReflectZ(this LFTree t, float offset = 0f) {
      return t.Remap(LFTree.x, LFTree.y, (2f * offset) - LFTree.z);
    }

    /// <summary>Moves the given shape across the plane Y=X</summary>
    public static LFTree ReflectXY(this LFTree t, float offset = 0f) {
      return t.Remap(LFTree.y, LFTree.x, LFTree.z);
    }
    /// <summary>Moves the given shape across the plane Y=Z</summary>
    public static LFTree ReflectYZ(this LFTree t, float offset = 0f) {
      return t.Remap(LFTree.x, LFTree.z, LFTree.y);
    }
    /// <summary>Moves the given shape across the plane X=Z</summary>
    public static LFTree ReflectXZ(this LFTree t, float offset = 0f) {
      return t.Remap(LFTree.z, LFTree.y, LFTree.x);
    }

    /// <summary>Clip the given shape at the x origin,
    /// and duplicate the remaining shape reflected
    /// on the other side of the origin</summary>
    public static LFTree SymmetricX(this LFTree t) {
      return t.Remap(Abs(LFTree.x), LFTree.y, LFTree.z);
    }
    /// <summary>Clip the given shape at the y origin,
    /// and duplicate the remaining shape reflected
    /// on the other side of the origin</summary>
    public static LFTree SymmetricY(this LFTree t) {
      return t.Remap(LFTree.x, Abs(LFTree.y), LFTree.z);
    }
    /// <summary>Clip the given shape at the z origin,
    /// and duplicate the remaining shape reflected
    /// on the other side of the origin</summary>
    public static LFTree SymmetricZ(this LFTree t) {
      return t.Remap(LFTree.x, LFTree.y, Abs(LFTree.z));
    }

    #endregion

    #region CSG
    //Reference: https://github.com/libfive/libfive/blob/master/libfive/bind/csg.scm

    /// <summary>Returns the union of any number of shapes</summary>
    public static LFTree Union(params LFTree[] shapes) {
      LFTree unionedTree = shapes[0];
      if(shapes.Length>1) for(int i = 1; i < shapes.Length; i++) unionedTree = Min(unionedTree, shapes[i]);
      return unionedTree;
    }

    /// <summary>Returns the intersection of any number of shapes</summary>
    public static LFTree Intersection(params LFTree[] shapes) {
      LFTree intersectedTree = shapes[0];
      for (int i = 1; i < shapes.Length; i++) intersectedTree = Max(intersectedTree, shapes[i]);
      return intersectedTree;
    }

    /// <summary>Returns a shape that's the inverse of the input shape</summary>
    public static LFTree Inverse(LFTree shape) { return -shape; }

    /// <summary>Subtracts any number of shapes from the first argument</summary>
    public static LFTree Difference(params LFTree[] shapes) {
      if (shapes.Length == 0) Debug.LogError("Difference can't be called without arguments!");
      if (shapes.Length == 1) return shapes[0];
      LFTree[] subtractingShapes = new LFTree[shapes.Length - 1];
      for (int i = 1; i < shapes.Length; i++) subtractingShapes[i - 1] = shapes[i];
      return Intersection(shapes[0], Inverse(Union(subtractingShapes)));
    }

    /// <summary>Returns the blend of two shapes</summary>
    public static LFTree Blend(LFTree a, LFTree b, float blendAmount = 0.5f) {
      return Union(a, b, (Sqrt(Abs(a))) + (Sqrt(Abs(b))) - blendAmount);
    }
    /// <summary>Returns the blend of any number of shapes</summary>
    public static LFTree Blend(float blendAmount = 0.5f, params LFTree[] shapes) {
      LFTree blendedTree = shapes[0];
      for (int i = 1; i < shapes.Length; i++) blendedTree = Blend(blendedTree, shapes[i], blendAmount);
      return blendedTree;
    }

    #endregion
  }
}
