using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace libfivesharp {
  /// <summary>
  /// Turns the GameObject hierarchy into a libfive CSG tree. Each node applies its <see cref="op"/> to
  /// its enabled child LFShapes; each node's local transform positions its result inside the parent.
  /// The root node (no LFShape parent) is meshed in its own local space and drawn with an ordinary
  /// MeshFilter/MeshRenderer, so it works with every render pipeline and moving the root never
  /// re-meshes. Changes anywhere in the hierarchy mark the path to the root dirty; meshing happens on
  /// a worker thread (see <see cref="AsyncRender"/>).
  ///
  /// In the editor (edit mode only) child nodes are also meshed so they can be picked and outlined in
  /// the Scene view; toggle that with <see cref="PreviewChildren"/> (Tools > libfive menu).
  /// Coordinates follow libfive: Z is "up" for cylinders, cones, pyramids and extrusions.
  /// </summary>
  [ExecuteAlways]
  [DisallowMultipleComponent]
  [AddComponentMenu("libfive/Shape")]
  [HelpURL("https://github.com/zalo/libfive-unity")]
  public class LFShape : MonoBehaviour {
    // Numbering is serialized into scenes: 0-99 primitives, 100-199 unary, 200+ n-ary. Never renumber.
    public enum LibFive_Operation : int {
      /// <summary>Groups its children (union) and applies this node's transform. No children = empty.</summary>
      Transform = 0,
      // Primitives (unit-sized, centered on the node)
      Circle = 1,
      Sphere = 2,
      Box = 3,
      Cylinder = 4,
      Torus = 5,
      Cone = 6,
      Pyramid = 7,
      RoundedBox = 8,
      Gyroid = 9,
      HalfSpace = 10,
      Text = 11,
      // Unary (applied to the union of the children)
      Inverse = 106,
      Mirror = 107,
      Shell = 108,
      Offset = 109,
      Twist = 110,
      Taper = 111,
      Revolve = 112,
      Extrude = 113,
      RepeatPolar = 114,
      // N-ary
      Union = 209,
      Intersection = 210,
      Difference = 211,
      Blend = 212,
      Morph = 213,
      Clearance = 214,
      BlendDifference = 215,
      Loft = 216,
    }

    [Tooltip("The operation this node applies to its children (or the primitive it represents).")]
    public LibFive_Operation op = LibFive_Operation.Sphere;

    [Tooltip("Operation parameter: Shell/Offset/Clearance distance, Blend smoothness, Morph factor, " +
             "Twist radians, Taper end scale, Extrude height, RepeatPolar count, RoundedBox rounding (0-1), " +
             "Gyroid thickness. Resets to a sensible default when the operation changes.")]
    public float amount = 0.1f;

    [Tooltip("Text rendered by the Text operation (character height 1, 2D: add an Extrude parent).")]
    public string text = "libfive";

    [SerializeField, HideInInspector] LibFive_Operation lastOp = (LibFive_Operation)(-1);

    [Header("Mesh")]
    [Tooltip("Material for the root mesh. Empty uses the render pipeline's default material.")]
    public Material material;

    [Tooltip("Edge length of the cube (centered on this node) that gets meshed. Geometry outside it is clipped.")]
    [Min(0.01f)]
    public float boundsSize = 2.5f;

    [Tooltip("Edges whose faces meet at more than this angle (degrees) get hard, split normals. 180 = fully smooth.")]
    [Range(0.001f, 180f)]
    public float vertexSplittingAngle = 180f;

    [Tooltip("Octree cells per unit of length (the smallest feature libfive resolves is 1/resolution units). " +
             "Meshing cost grows roughly with the cube of this value.")]
    [Range(1f, 256f)]
    public float resolution = 8f;

    [Tooltip("Mesh on a worker thread and swap the result in when it is ready, instead of blocking Update.")]
    public bool AsyncRender = true;

    /// <summary>The evaluated tree in the parent's space (root: in this node's local space). Owned by this node.</summary>
    [System.NonSerialized] public LFTree tree;
    /// <summary>The meshed result (root: local space; child: parent space). Owned by this node.</summary>
    [System.NonSerialized] public Mesh cachedMesh;

    /// <summary>Editor only: mesh child nodes (in edit mode) so they can be clicked and outlined in the Scene view.</summary>
    public static bool PreviewChildren = true;

    LFMeshJob job;
    LFTree retiredTree;
    bool dirty = true;
    LFShape parentShape;
    bool parentResolved;
    Vector3 lastLocalPosition, lastLocalScale;
    Quaternion lastLocalRotation;
    MeshFilter meshFilter;
    MeshRenderer meshRenderer;
    readonly List<LFTree> childTrees = new List<LFTree>();

    /// <summary>Wall-clock milliseconds the last meshing job took (libfive + normal generation).</summary>
    public double LastMeshMilliseconds { get; private set; }
    /// <summary>True while a meshing job for this node is in flight.</summary>
    public bool IsMeshing { get { return job != null; } }
    public bool IsDirty { get { return dirty; } }

    /// <summary>The LFShape on the parent GameObject, or null if this node is a root.</summary>
    public LFShape ParentShape {
      get {
        if (!parentResolved) {
          parentShape = transform.parent != null ? transform.parent.GetComponent<LFShape>() : null;
          parentResolved = true;
        }
        return parentShape;
      }
    }
    public bool IsRoot { get { return ParentShape == null; } }
    public LFShape Root {
      get {
        LFShape s = this;
        while (s.ParentShape != null) s = s.ParentShape;
        return s;
      }
    }

    /// <summary>The region that gets meshed, in the space of <see cref="cachedMesh"/>.</summary>
    public Bounds LocalBounds {
      get {
        Vector3 center = IsRoot ? Vector3.zero : transform.localPosition;
        return new Bounds(center, Vector3.one * boundsSize);
      }
    }

    #region Unity messages
    void OnEnable() {
      parentResolved = false;
      CaptureTransform();
      EnsureMesh();
      MarkDirty();
      UpdateRenderer(true);
    }

    void OnDisable() {
      CancelJob();
      if (meshRenderer != null && meshFilter != null && meshFilter.sharedMesh == cachedMesh) meshRenderer.enabled = false;
      LFShape parent = ParentShape;
      if (parent != null) parent.MarkDirty();
    }

    void OnDestroy() {
      CancelJob();
      if (tree != null) { tree.Dispose(); tree = null; }
      if (cachedMesh != null) {
        if (Application.isPlaying) Destroy(cachedMesh); else DestroyImmediate(cachedMesh);
        cachedMesh = null;
      }
    }

    void OnValidate() {
      if (op != lastOp) {
        amount = DefaultAmount(op);
        lastOp = op;
      }
      if (boundsSize < 0.01f) boundsSize = 0.01f;
      MarkDirty();
      UpdateRenderer(false);
    }

    void OnTransformChildrenChanged() { MarkDirty(); }

    void OnTransformParentChanged() {
      parentResolved = false;
      CaptureTransform();
      MarkDirty();
      UpdateRenderer(true);
    }

    void Update() {
      if (!IsRoot && TransformChanged()) {
        CaptureTransform();
        MarkDirty();
      }

      if (job != null) {
        if (!AsyncRender || job.IsCompleted) FinishJob();
        else RequestEditorUpdate();
      }

      if (dirty && job == null) {
        Evaluate();
        if (IsRoot || ShouldPreviewChild()) StartJob();
        else if (cachedMesh != null && cachedMesh.vertexCount > 0) LFMeshBuilder.Clear(cachedMesh);
      }
    }
    #endregion

    #region Public API
    /// <summary>Flags this node and every ancestor for re-evaluation on the next Update.</summary>
    public void MarkDirty() {
      for (LFShape s = this; s != null; s = s.ParentShape) s.dirty = true;
      RequestEditorUpdate();
    }

    /// <summary>Re-evaluates and re-meshes this node right now, synchronously.</summary>
    public void RebuildNow() {
      CancelJob();
      MarkDirty();
      Evaluate();
      if (tree == null) { LFMeshBuilder.Clear(cachedMesh); UpdateRenderer(true); return; }
      EnsureMesh();
      using (var sync = LFMeshJob.Schedule(tree, LocalBounds, resolution)) {
        sync.Complete(cachedMesh, vertexSplittingAngle);
        LastMeshMilliseconds = sync.ElapsedMilliseconds;
      }
      UpdateRenderer(true);
    }

    /// <summary>
    /// Returns this node's tree (in parent space; root: local space), rebuilding it if anything below
    /// changed. Children are evaluated recursively. Returns null for an empty node. The result is owned
    /// by this node: do not dispose it.
    /// </summary>
    public LFTree Evaluate() {
      if (!dirty && tree != null && !tree.IsDisposed) return tree;

      LFTree result = null;
      using (LFContext.Push()) {
        LFTree built = BuildTree();
        if (built != null) {
          if (IsRoot) {
            // Always produce a handle this node owns (BuildTree may return a child's tree unchanged),
            // and let libfive fold constants / deduplicate before meshing.
            result = built.Optimized();
          } else {
            result = LFMath.Transform(built, LocalMatrix());
          }
          LFContext.Active.Detach(result);
        }
      }
      ReplaceTree(result);
      dirty = false;
      return tree;
    }

    /// <summary>The <see cref="amount"/> value a freshly chosen operation starts with.</summary>
    public static float DefaultAmount(LibFive_Operation operation) {
      switch (operation) {
        case LibFive_Operation.RoundedBox: return 0.25f;
        case LibFive_Operation.Gyroid: return 0.05f;
        case LibFive_Operation.Shell: return 0.025f;
        case LibFive_Operation.Offset: return 0.1f;
        case LibFive_Operation.Twist: return 1.0f;
        case LibFive_Operation.Taper: return 0.5f;
        case LibFive_Operation.Extrude: return 1.0f;
        case LibFive_Operation.RepeatPolar: return 6f;
        case LibFive_Operation.Blend: return 0.25f;
        case LibFive_Operation.Morph: return 0.5f;
        case LibFive_Operation.Clearance: return 0.1f;
        case LibFive_Operation.BlendDifference: return 0.25f;
        default: return 0f;
      }
    }

    /// <summary>True if the operation reads <see cref="amount"/>.</summary>
    public static bool UsesAmount(LibFive_Operation operation) {
      switch (operation) {
        case LibFive_Operation.RoundedBox:
        case LibFive_Operation.Gyroid:
        case LibFive_Operation.Shell:
        case LibFive_Operation.Offset:
        case LibFive_Operation.Twist:
        case LibFive_Operation.Taper:
        case LibFive_Operation.Extrude:
        case LibFive_Operation.RepeatPolar:
        case LibFive_Operation.Blend:
        case LibFive_Operation.Morph:
        case LibFive_Operation.Clearance:
        case LibFive_Operation.BlendDifference:
          return true;
        default:
          return false;
      }
    }

    /// <summary>Inspector label for <see cref="amount"/> under the given operation.</summary>
    public static string AmountLabel(LibFive_Operation operation) {
      switch (operation) {
        case LibFive_Operation.RoundedBox: return "Rounding (0-1)";
        case LibFive_Operation.Gyroid: return "Thickness";
        case LibFive_Operation.Shell: return "Thickness";
        case LibFive_Operation.Offset: return "Distance";
        case LibFive_Operation.Twist: return "Radians";
        case LibFive_Operation.Taper: return "End Scale";
        case LibFive_Operation.Extrude: return "Height";
        case LibFive_Operation.RepeatPolar: return "Count";
        case LibFive_Operation.Blend: return "Smoothness";
        case LibFive_Operation.Morph: return "Morph (0-1)";
        case LibFive_Operation.Clearance: return "Gap";
        case LibFive_Operation.BlendDifference: return "Smoothness";
        default: return "Amount";
      }
    }
    #endregion

    #region Tree construction
    LFTree BuildTree() {
      int code = (int)op;
      if (op == LibFive_Operation.Transform) {
        CollectChildTrees();
        return UnionOfChildren();
      }
      if (code < 100) return BuildPrimitive();

      CollectChildTrees();
      if (code < 200) {
        LFTree input = UnionOfChildren();
        return input == null ? null : ApplyUnary(input);
      }
      return childTrees.Count == 0 ? null : ApplyNary();
    }

    LFTree BuildPrimitive() {
      const float h = 0.5f;
      switch (op) {
        case LibFive_Operation.Circle: return LFMath.Circle(h);
        case LibFive_Operation.Sphere: return LFMath.Sphere(h);
        case LibFive_Operation.Box: return LFMath.Box(-Vector3.one * h, Vector3.one * h);
        case LibFive_Operation.Cylinder: return LFMath.Cylinder(h, 1f, new Vector3(0f, 0f, -h));
        case LibFive_Operation.Torus: return LFMath.Torus(0.35f, 0.15f);
        case LibFive_Operation.Cone: return LFMath.Cone(h, 1f, new Vector3(0f, 0f, -h));
        case LibFive_Operation.Pyramid: return LFMath.Pyramid(new Vector2(-h, -h), new Vector2(h, h), -h, 1f);
        case LibFive_Operation.RoundedBox: return LFMath.RoundedBox(-Vector3.one * h, Vector3.one * h, Mathf.Clamp01(amount));
        case LibFive_Operation.Gyroid: return LFMath.GyroidCells(1f, amount);
        case LibFive_Operation.HalfSpace: return LFMath.HalfSpace(new Vector3(0f, 0f, 1f));
        case LibFive_Operation.Text: return LFMath.Text(text ?? "");
        default: return null;
      }
    }

    LFTree ApplyUnary(LFTree input) {
      switch (op) {
        case LibFive_Operation.Inverse: return LFMath.Inverse(input);
        case LibFive_Operation.Mirror: return LFMath.SymmetricX(input);
        case LibFive_Operation.Shell: return LFMath.Shell(input, amount);
        case LibFive_Operation.Offset: return LFMath.Offset(input, amount);
        case LibFive_Operation.Twist: return LFMath.Twirl(input, LFMath.Axis.Z, amount, boundsSize * 0.5f, default(LFVec3), true);
        case LibFive_Operation.Taper: return LFMath.TaperXYZ(input, new Vector3(0f, 0f, -0.5f), 1f, amount);
        case LibFive_Operation.Revolve: return LFMath.RevolveY(input);
        case LibFive_Operation.Extrude: return LFMath.Extrude(input, -amount * 0.5f, amount * 0.5f);
        case LibFive_Operation.RepeatPolar: return LFMath.ArrayPolar(input, Mathf.Max(1, Mathf.RoundToInt(amount)));
        default: return input;
      }
    }

    LFTree ApplyNary() {
      LFTree first = childTrees[0];
      if (childTrees.Count == 1) {
        // Blend/Morph/etc. of a single shape is the shape itself.
        return first;
      }
      switch (op) {
        case LibFive_Operation.Union: return LFMath.Union(childTrees.ToArray());
        case LibFive_Operation.Intersection: return LFMath.Intersection(childTrees.ToArray());
        case LibFive_Operation.Difference: return LFMath.Difference(childTrees.ToArray());
        case LibFive_Operation.Blend: return LFMath.Blend(amount, childTrees.ToArray());
        case LibFive_Operation.Morph: return LFMath.Morph(first, childTrees[1], amount);
        case LibFive_Operation.Clearance: return LFMath.Clearance(first, UnionOfRest(), amount);
        case LibFive_Operation.BlendDifference: return LFMath.BlendDifference(first, UnionOfRest(), amount);
        case LibFive_Operation.Loft: return LFMath.Loft(first, childTrees[1], -0.5f, 0.5f);
        default: return LFMath.Union(childTrees.ToArray());
      }
    }

    void CollectChildTrees() {
      childTrees.Clear();
      Transform t = transform;
      for (int i = 0; i < t.childCount; i++) {
        LFShape child = t.GetChild(i).GetComponent<LFShape>();
        if (child == null || !child.isActiveAndEnabled) continue;
        LFTree childTree = child.Evaluate();
        if (childTree != null) childTrees.Add(childTree);
      }
    }

    LFTree UnionOfChildren() {
      if (childTrees.Count == 0) return null;
      LFTree acc = childTrees[0];
      for (int i = 1; i < childTrees.Count; i++) acc = LFMath.Union(acc, childTrees[i]);
      return acc;
    }

    LFTree UnionOfRest() {
      LFTree acc = childTrees[1];
      for (int i = 2; i < childTrees.Count; i++) acc = LFMath.Union(acc, childTrees[i]);
      return acc;
    }

    Matrix4x4 LocalMatrix() {
      Vector3 s = transform.localScale;
      // A zero scale would make the inverse singular; clamp to something tiny instead.
      const float eps = 1e-5f;
      if (Mathf.Abs(s.x) < eps) s.x = eps;
      if (Mathf.Abs(s.y) < eps) s.y = eps;
      if (Mathf.Abs(s.z) < eps) s.z = eps;
      return Matrix4x4.TRS(transform.localPosition, transform.localRotation, s);
    }

    void ReplaceTree(LFTree newTree) {
      if (tree != null && !ReferenceEquals(tree, newTree)) {
        if (job != null && ReferenceEquals(job.Tree, tree)) {
          // A meshing job still reads the old tree; free it once the job is done.
          if (retiredTree != null) retiredTree.Dispose();
          retiredTree = tree;
        } else {
          tree.Dispose();
        }
      }
      tree = newTree;
    }
    #endregion

    #region Meshing
    void StartJob() {
      EnsureMesh();
      if (tree == null) {
        LFMeshBuilder.Clear(cachedMesh);
        UpdateRenderer(true);
        return;
      }
      job = LFMeshJob.Schedule(tree, LocalBounds, resolution);
      if (!AsyncRender) FinishJob();
      else RequestEditorUpdate();
    }

    void FinishJob() {
      EnsureMesh();
      job.Complete(cachedMesh, vertexSplittingAngle);
      LastMeshMilliseconds = job.ElapsedMilliseconds;
      job.Dispose();
      job = null;
      if (retiredTree != null) { retiredTree.Dispose(); retiredTree = null; }
      UpdateRenderer(true);
      RepaintEditor();
    }

    void CancelJob() {
      if (job != null) { job.Dispose(); job = null; }
      if (retiredTree != null) { retiredTree.Dispose(); retiredTree = null; }
    }

    void EnsureMesh() {
      if (cachedMesh != null) return;
      cachedMesh = new Mesh { name = "libfive " + gameObject.name, hideFlags = HideFlags.DontSave, indexFormat = IndexFormat.UInt32 };
      cachedMesh.MarkDynamic();
    }

    /// <param name="mayAddComponents">False when called from OnValidate, where AddComponent is not allowed.</param>
    void UpdateRenderer(bool mayAddComponents) {
      if (meshFilter == null) meshFilter = GetComponent<MeshFilter>();
      if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();

      if (IsRoot && isActiveAndEnabled) {
        if (mayAddComponents) {
          if (meshFilter == null) meshFilter = gameObject.AddComponent<MeshFilter>();
          if (meshRenderer == null) meshRenderer = gameObject.AddComponent<MeshRenderer>();
        }
        if (meshFilter == null || meshRenderer == null) return;
        EnsureMesh();
        if (meshFilter.sharedMesh != cachedMesh) meshFilter.sharedMesh = cachedMesh;
        Material wanted = material != null ? material : DefaultMaterial;
        if (meshRenderer.sharedMaterial != wanted) meshRenderer.sharedMaterial = wanted;
        if (!meshRenderer.enabled) meshRenderer.enabled = true;
      } else if (meshFilter != null && meshRenderer != null && meshFilter.sharedMesh == cachedMesh && cachedMesh != null) {
        // This node used to be a root: stop drawing its (parent-space) mesh.
        meshRenderer.enabled = false;
      }
    }

    static Material defaultMaterial;
    /// <summary>The render pipeline's default lit material (built-in: Standard).</summary>
    public static Material DefaultMaterial {
      get {
        if (defaultMaterial != null) return defaultMaterial;
        RenderPipelineAsset pipeline = GraphicsSettings.currentRenderPipeline;
        if (pipeline != null && pipeline.defaultMaterial != null) {
          defaultMaterial = pipeline.defaultMaterial;
        } else {
          Shader shader = Shader.Find("Standard");
          if (shader == null) shader = Shader.Find("Diffuse");
          defaultMaterial = new Material(shader) { name = "libfive Default", hideFlags = HideFlags.HideAndDontSave };
        }
        return defaultMaterial;
      }
    }

    static bool ShouldPreviewChild() {
#if UNITY_EDITOR
      return PreviewChildren && !Application.isPlaying;
#else
      return false;
#endif
    }
    #endregion

    #region Change tracking
    void CaptureTransform() {
      lastLocalPosition = transform.localPosition;
      lastLocalRotation = transform.localRotation;
      lastLocalScale = transform.localScale;
    }

    bool TransformChanged() {
      return transform.localPosition != lastLocalPosition
          || transform.localRotation != lastLocalRotation
          || transform.localScale != lastLocalScale;
    }

    static void RequestEditorUpdate() {
#if UNITY_EDITOR
      if (!Application.isPlaying) EditorApplication.QueuePlayerLoopUpdate();
#endif
    }

    static void RepaintEditor() {
#if UNITY_EDITOR
      if (!Application.isPlaying) SceneView.RepaintAll();
#endif
    }
    #endregion
  }
}
