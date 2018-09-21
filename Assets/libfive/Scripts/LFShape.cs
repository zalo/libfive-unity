using System.Collections.Generic;
using UnityEngine;
using libfivesharp;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
public class LFShape : MonoBehaviour {
  public LFTree tree;
  public LibFive_Operation op;

  [Header("Mesh Rendering Properties")]
  [Range(1f, 10f)]
  public float boundsSize = 2.5f;
  [Range(0.001f, 180f)]
  public float vertexSplittingAngle = 180f;
  [Tooltip("Corresponds to Powers of 2")]
  [Range(4f, 64f)]
  public float resolution = 8f;

  Material defaultMat;
  [System.NonSerialized]
  public Mesh cachedMesh;

  private void OnValidate() {
    transform.hasChanged = true;
    if(transform.parent != null) transform.parent.hasChanged = true;
  }

  private void OnEnable() {
    Camera.onPreCull -= Draw;
    Camera.onPreCull += Draw;
    if (transform.parent != null) transform.parent.hasChanged = true;
  }
  private void OnDisable() {
    Camera.onPreCull -= Draw;
    if (transform.parent != null) transform.parent.hasChanged = true;
  }

  bool isRootNode;
  private void Draw(Camera camera) {
    if (transform.hasChanged && transform.parent != null) transform.parent.hasChanged = true;
    if (defaultMat == null) defaultMat = new Material(Shader.Find("Diffuse"));
    if (cachedMesh == null) { cachedMesh = new Mesh(); }
    isRootNode = transform.parent == null || transform.parent.GetComponent<LFShape>() == null;
    if (tree == null || transform.hasChanged || cachedMesh.vertexCount == 0) {
      Evaluate();
      tree.RenderMesh(cachedMesh, new Bounds(transform.position, Vector3.one * boundsSize), resolution + 0.001f, vertexSplittingAngle);
    }
    if (cachedMesh.vertexCount > 0 && defaultMat && isRootNode) {
      Graphics.DrawMesh(cachedMesh, Matrix4x4.identity, defaultMat, gameObject.layer, camera);
    }
  }

  private bool isSelected = false;
  private void OnDrawGizmosSelected() {
#if UNITY_EDITOR
    if (isSelected != (isSelected = gameObject == Selection.activeGameObject)) transform.hasChanged = true;
    Gizmos.color = new Color(0.368f, 0.466f, 0.607f, 0.251f);
    if (isSelected && cachedMesh != null && cachedMesh.vertexCount > 0) {
      if (transform.parent != null && transform.parent.GetComponent<LFShape>()) Gizmos.matrix = transform.parent.localToWorldMatrix;
      Gizmos.DrawWireCube(transform.position, Vector3.one * boundsSize);
      Gizmos.DrawWireMesh(cachedMesh);
    }
#endif
  }

  public LFTree Evaluate() {
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
}

#if UNITY_EDITOR
//This allows the nodes in the model to be pickable!
public class LFShapeGizmoDrawer {
  [DrawGizmo(GizmoType.Pickable | GizmoType.NonSelected)]
  static void DrawGizmoForMyScript(LFShape src, GizmoType gizmoType) {
    if (src.cachedMesh != null && src.cachedMesh.vertexCount > 0) {
      Gizmos.color = new Color(0f, 0f, 0f, 0f);
      if (src.transform.parent != null && src.transform.parent.GetComponent<LFShape>()) Gizmos.matrix = src.transform.parent.localToWorldMatrix;
      Gizmos.DrawMesh(src.cachedMesh);
    }
  }
}
#endif
