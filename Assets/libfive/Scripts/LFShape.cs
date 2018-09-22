using System.Collections.Generic;
using UnityEngine;
using libfivesharp;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace libfivesharp {
  [ExecuteInEditMode]
  public class LFShape : MonoBehaviour {
    [Tooltip("The operation that this node will apply to its children in the Scene Hierarchy.")]
    public LibFive_Operation op;

    [Header("Mesh Rendering Properties")]

    [Tooltip("The material that the mesh will be rendered with.")]
    public Material material;
    [Tooltip("The axis-aligned bounds this tree will render in.")]
    [Range(1f, 10f)]
    public float boundsSize = 2.5f;
    [Tooltip("The angle between a vertex normal and a triangle normal required to " +
      "split the vertex into a sharp edge.  180 is `No Splitting`/`Smooth all my " +
      "normals` and is very fast.")]
    [Range(0.001f, 180f)]
    public float vertexSplittingAngle = 180f;
    [Tooltip("Corresponds to Powers of 2")]
    [Range(4f, 64f)]
    public float resolution = 8f;

    [Header("Experimental")]
    [Tooltip("Allows for a multi-frame delay for rendering; decouples rendering and framerate." +
      "Unity will complain about allocations more than a few frames old.")]
    public bool renderOnAnotherThread = false;

    [System.NonSerialized]
    public Mesh cachedMesh;
    [System.NonSerialized]
    public LFTree tree;

    //Refresh the mesh when its inspector has changed
    private void OnValidate() {
      transform.hasChanged = true;
      if (transform.parent != null) transform.parent.hasChanged = true;
      if (transform.parent == null) transform.hasChanged = true;
    }

    //Subscribe to Camera callbacks for drawing an refresh the mesh OnEnable
    private void OnEnable() {
      Camera.onPreCull -= Draw;
      Camera.onPreCull += Draw;
      if (transform.parent != null) transform.parent.hasChanged = true;
      if (transform.parent == null) transform.hasChanged = true;
    }
    private void OnDisable() {
      Camera.onPreCull -= Draw;
      if (transform.parent != null) transform.parent.hasChanged = true;
      if (transform.parent == null) transform.hasChanged = true;
    }

    //Check if the mesh needs to be refreshed
    LFMeshRendering.RenderJobPayload payload;
    private void Update() {
      UnityEngine.Profiling.Profiler.BeginSample("Update LibFive Mesh", this);
      if (transform.hasChanged && transform.parent != null) transform.parent.hasChanged = true;
      if (material == null) material = new Material(Shader.Find("Diffuse"));
      if (cachedMesh == null) { cachedMesh = new Mesh(); cachedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32; }
      isRootNode = transform.parent == null || transform.parent.GetComponent<LFShape>() == null;

      if (payload != null && !payload.handle.Equals(default(Unity.Jobs.JobHandle)) && (!renderOnAnotherThread || payload.handle.IsCompleted)) {
        payload.handle.Complete();
        LFMeshRendering.CompleteRenderLibFiveMesh(ref payload, cachedMesh, vertexSplittingAngle);
        payload.handle = default(Unity.Jobs.JobHandle);
      } else if ((tree == null || transform.hasChanged || cachedMesh.vertexCount == 0) && (payload == null || payload.handle.IsCompleted)) {
        Evaluate();
        //tree.RenderMesh(cachedMesh, new Bounds(transform.position, Vector3.one * boundsSize), resolution + 0.001f, vertexSplittingAngle);
        if (payload == null) payload = new LFMeshRendering.RenderJobPayload(ref tree);
        LFMeshRendering.ScheduleRenderLibFiveMesh(tree, new Bounds(transform.position, Vector3.one * boundsSize), resolution + 0.001f, ref payload);
        if (!renderOnAnotherThread) Update();
      }
      UnityEngine.Profiling.Profiler.EndSample();
    }

    bool isRootNode;
    private void Draw(Camera camera) {
      UnityEngine.Profiling.Profiler.BeginSample("Draw LibFive Mesh", this);
      if (cachedMesh == null) { cachedMesh = new Mesh(); cachedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32; }
      if (cachedMesh.vertexCount > 0 && material && isRootNode) {
        Graphics.DrawMesh(cachedMesh, Matrix4x4.identity, material, gameObject.layer, camera);
      }
      UnityEngine.Profiling.Profiler.EndSample();
    }

    private bool isSelected = false;
    private void OnDrawGizmosSelected() {
#if UNITY_EDITOR
      UnityEngine.Profiling.Profiler.BeginSample("Draw LibFive Wireframe Gizmo", this);
      if (isSelected != (isSelected = gameObject == Selection.activeGameObject)) transform.hasChanged = true;
      Gizmos.color = new Color(0.368f, 0.466f, 0.607f, 0.251f);
      if (isSelected && cachedMesh != null && cachedMesh.vertexCount > 0) {
        if (transform.parent != null && transform.parent.GetComponent<LFShape>()) Gizmos.matrix = transform.parent.localToWorldMatrix;
        Gizmos.DrawWireCube(transform.position, Vector3.one * boundsSize);
        Gizmos.DrawWireMesh(cachedMesh);
      }
      UnityEngine.Profiling.Profiler.EndSample();
#endif
    }

    public LFTree Evaluate() {
      UnityEngine.Profiling.Profiler.BeginSample("Evaluate LibFive Tree", this);
      using (LFContext.Active = new Context()) {
        if (op < LibFive_Operation.Transform) {
          tree = evaluateNonary(op);
        } else if (op < LibFive_Operation.Union && transform.childCount > 0) {
          LFShape childShape = transform.GetChild(0).GetComponent<LFShape>();
          if (childShape != null && childShape.isActiveAndEnabled) tree = evaluateUnary(op, childShape.Evaluate());
        } else {
          List<LFTree> trees = new List<LFTree>(transform.childCount);
          for (int i = 0; i < transform.childCount; i++) {
            LFShape childShape = transform.GetChild(i).GetComponent<LFShape>();
            if (childShape != null && childShape.isActiveAndEnabled) trees.Add(childShape.Evaluate());
          }
          tree = evaluateNnary(op, trees.ToArray());
        }
        transform.hasChanged = false;
        if (isRootNode && tree != null) {
          tree = LFMath.Transform(tree, transform.localToWorldMatrix);
        } else {
          tree = LFMath.Transform(tree, transform.parent.worldToLocalMatrix * transform.localToWorldMatrix);
        }

        //This prevents this node's tree from being disposed of
        LFContext.Active.RemoveTreeFromContext(tree);
      }
      UnityEngine.Profiling.Profiler.EndSample();
      return tree;
    }

    LFTree evaluateNonary(LibFive_Operation op) {
      if (op == LibFive_Operation.Circle) {
        return LFMath.Circle(0.5f);
      } else if (op == LibFive_Operation.Sphere) {
        return LFMath.Sphere(0.5f);
      } else if (op == LibFive_Operation.Box) {
        return LFMath.Box(-Vector3.one * 0.5f, Vector3.one * 0.5f);
      } else if (op == LibFive_Operation.Cylinder) {
        return LFMath.Cylinder(0.5f, 1f, Vector3.back * 0.5f);
      }
      return tree;
    }

    LFTree evaluateUnary(LibFive_Operation op, LFTree tree) {
      if (tree != null) {
        if (op == LibFive_Operation.Transform) {
          return tree;
        } else if (op == LibFive_Operation.Inverse) {
          return -tree;
        } else if (op == LibFive_Operation.Mirror) {
          return LFMath.SymmetricX(tree);
        } else if (op == LibFive_Operation.Shell) {
          return LFMath.Shell(tree, 0.025f);
        }
      }
      return this.tree;
    }

    LFTree evaluateNnary(LibFive_Operation op, params LFTree[] trees) {
      if (trees.Length > 0 || trees[0] == null) {
        if (trees.Length == 1) return trees[0];
        if (op == LibFive_Operation.Union) {
          return LFMath.Union(trees);
        } else if (op == LibFive_Operation.Intersection) {
          return LFMath.Intersection(trees);
        } else if (op == LibFive_Operation.Difference) {
          return LFMath.Difference(trees);
        }
      }
      return tree;
    }

    public enum LibFive_Operation {
      //Nonary
      Circle,
      Sphere,
      Box,
      Cylinder,
      //Unary
      Transform,
      Inverse,
      Mirror,
      Shell,
      //Binary
      Union,
      Intersection,
      Difference,
    }

    private void OnDestroy() {
      if (payload != null) {
        unsafe { Unity.Collections.LowLevel.Unsafe.UnsafeUtility.Free((payload.libFiveMeshPtr), Unity.Collections.Allocator.Persistent); }
      }
    }
  }

#if UNITY_EDITOR
  //This allows the nodes in the model to be pickable!
  public class LFShapeGizmoDrawer {
    [DrawGizmo(GizmoType.Pickable | GizmoType.NonSelected)]
    static void DrawGizmoForMyScript(LFShape src, GizmoType gizmoType) {
      UnityEngine.Profiling.Profiler.BeginSample("Draw Pickable LibFive Gizmos", src);
      if (src.cachedMesh != null && src.cachedMesh.vertexCount > 0) {
        Gizmos.color = new Color(0f, 0f, 0f, 0f);
        if (src.transform.parent != null && src.transform.parent.GetComponent<LFShape>()) Gizmos.matrix = src.transform.parent.localToWorldMatrix;
        Gizmos.DrawMesh(src.cachedMesh);
      }
      UnityEngine.Profiling.Profiler.EndSample();
    }
  }
#endif
}
