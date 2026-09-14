using UnityEditor;
using UnityEngine;

namespace libfivesharp {
  [CustomEditor(typeof(LFShape))]
  [CanEditMultipleObjects]
  public class LFShapeEditor : Editor {
    SerializedProperty opProp, amountProp, textProp, materialProp, boundsProp, angleProp, resolutionProp, asyncProp;

    void OnEnable() {
      opProp = serializedObject.FindProperty("op");
      amountProp = serializedObject.FindProperty("amount");
      textProp = serializedObject.FindProperty("text");
      materialProp = serializedObject.FindProperty("material");
      boundsProp = serializedObject.FindProperty("boundsSize");
      angleProp = serializedObject.FindProperty("vertexSplittingAngle");
      resolutionProp = serializedObject.FindProperty("resolution");
      asyncProp = serializedObject.FindProperty("AsyncRender");
    }

    public override void OnInspectorGUI() {
      serializedObject.Update();
      var shape = (LFShape)target;

      EditorGUILayout.PropertyField(opProp);
      var op = (LFShape.LibFive_Operation)opProp.intValue;
      if (!opProp.hasMultipleDifferentValues) {
        if (LFShape.UsesAmount(op)) {
          EditorGUILayout.PropertyField(amountProp, new GUIContent(LFShape.AmountLabel(op), amountProp.tooltip));
        }
        if (op == LFShape.LibFive_Operation.Text) EditorGUILayout.PropertyField(textProp);
        if ((int)op >= 100 && shape.transform.childCount == 0) {
          EditorGUILayout.HelpBox("This operation works on child LFShape nodes; add children under this object.", MessageType.Info);
        }
      }

      EditorGUILayout.Space();
      EditorGUILayout.LabelField("Mesh", EditorStyles.boldLabel);
      if (shape.IsRoot) EditorGUILayout.PropertyField(materialProp);
      EditorGUILayout.PropertyField(boundsProp);
      EditorGUILayout.PropertyField(angleProp);
      EditorGUILayout.PropertyField(resolutionProp);
      EditorGUILayout.PropertyField(asyncProp);
      serializedObject.ApplyModifiedProperties();

      EditorGUILayout.Space();
      string info;
      if (!LFNative.IsAvailable) {
        info = "Native libfive plugin not found for this platform. Run the 'Native libfive' workflow or build native/ and copy the binary into Assets/libfive/Plugins.";
        EditorGUILayout.HelpBox(info, MessageType.Error);
      } else {
        int verts = shape.cachedMesh != null ? shape.cachedMesh.vertexCount : 0;
        int tris = shape.cachedMesh != null && shape.cachedMesh.subMeshCount > 0 ? (int)shape.cachedMesh.GetIndexCount(0) / 3 : 0;
        string state = shape.IsMeshing ? "meshing…" : (shape.IsDirty ? "pending" : "up to date");
        info = string.Format("{0:N0} vertices, {1:N0} triangles, last mesh {2:F1} ms ({3})\nlibfive {4}{5}",
          verts, tris, shape.LastMeshMilliseconds, state,
          string.IsNullOrEmpty(LFNative.Version) || LFNative.Version == "N/A" ? "" : LFNative.Version + " ",
          LFNative.Revision);
        EditorGUILayout.HelpBox(info, MessageType.None);
      }

      using (new EditorGUILayout.HorizontalScope()) {
        if (GUILayout.Button("Rebuild")) {
          foreach (Object t in targets) ((LFShape)t).RebuildNow();
        }
        using (new EditorGUI.DisabledScope(shape.cachedMesh == null || shape.cachedMesh.vertexCount == 0)) {
          if (GUILayout.Button("Export STL…")) ExportStl(shape);
        }
        using (new EditorGUI.DisabledScope(shape.tree == null)) {
          if (GUILayout.Button("Copy Tree")) {
            string text = shape.tree.ToString();
            GUIUtility.systemCopyBuffer = text;
            Debug.Log(shape.gameObject.name + ": " + text, shape);
          }
        }
      }
    }

    static void ExportStl(LFShape shape) {
      string path = EditorUtility.SaveFilePanel("Export STL", "", shape.gameObject.name + ".stl", "stl");
      if (string.IsNullOrEmpty(path)) return;
      // Meshes live in this node's space (root) or the parent's space (child); export in world space.
      Matrix4x4 toWorld = shape.IsRoot || shape.transform.parent == null
        ? shape.transform.localToWorldMatrix
        : shape.transform.parent.localToWorldMatrix;
      try {
        LFMeshExport.WriteBinaryStl(shape.cachedMesh, path, toWorld, "libfive-unity " + shape.gameObject.name);
        Debug.Log("Saved STL to " + path, shape);
      } catch (System.Exception e) {
        Debug.LogError("Failed to save STL: " + e.Message, shape);
      }
    }
  }

  /// <summary>Scene-view gizmos: pickable invisible meshes for child nodes, outlines when selected.</summary>
  static class LFShapeGizmos {
    static readonly Color Outline = new Color(0.368f, 0.466f, 0.607f, 0.6f);

    [DrawGizmo(GizmoType.Pickable | GizmoType.NonSelected | GizmoType.Selected | GizmoType.Active, typeof(LFShape))]
    static void Draw(LFShape shape, GizmoType type) {
      bool selected = (type & (GizmoType.Selected | GizmoType.Active)) != 0;
      if (shape.IsRoot) {
        if (!selected) return;
        Gizmos.matrix = shape.transform.localToWorldMatrix;
        Gizmos.color = Outline;
        Bounds b = shape.LocalBounds;
        Gizmos.DrawWireCube(b.center, b.size);
        return;
      }

      Mesh mesh = shape.cachedMesh;
      if (mesh == null || mesh.vertexCount == 0 || shape.transform.parent == null) return;
      Gizmos.matrix = shape.transform.parent.localToWorldMatrix;
      if (selected) {
        Gizmos.color = Outline;
        Gizmos.DrawWireMesh(mesh);
        Bounds b = shape.LocalBounds;
        Gizmos.DrawWireCube(b.center, b.size);
      } else {
        // Invisible, but lets a click in the Scene view select this node.
        Gizmos.color = new Color(0f, 0f, 0f, 0f);
        Gizmos.DrawMesh(mesh);
      }
    }
  }

  [InitializeOnLoad]
  static class LFShapeEditorHooks {
    const string PreviewChildrenKey = "libfive.PreviewChildren";
    const string PreviewMenu = "Tools/libfive/Preview Child Nodes In Scene View";

    static LFShapeEditorHooks() {
      LFShape.PreviewChildren = EditorPrefs.GetBool(PreviewChildrenKey, true);
      Undo.undoRedoPerformed -= MarkAllDirty;
      Undo.undoRedoPerformed += MarkAllDirty;
    }

    static void MarkAllDirty() {
      foreach (LFShape s in Object.FindObjectsByType<LFShape>(FindObjectsInactive.Include, FindObjectsSortMode.None)) s.MarkDirty();
    }

    [MenuItem(PreviewMenu)]
    static void TogglePreviewChildren() {
      LFShape.PreviewChildren = !LFShape.PreviewChildren;
      EditorPrefs.SetBool(PreviewChildrenKey, LFShape.PreviewChildren);
      MarkAllDirty();
    }

    [MenuItem(PreviewMenu, true)]
    static bool ValidatePreviewChildren() {
      Menu.SetChecked(PreviewMenu, LFShape.PreviewChildren);
      return true;
    }

    [MenuItem("Tools/libfive/Rebuild All Shapes")]
    static void RebuildAll() {
      foreach (LFShape s in Object.FindObjectsByType<LFShape>(FindObjectsInactive.Include, FindObjectsSortMode.None)) {
        if (s.IsRoot) s.RebuildNow();
      }
    }

    [MenuItem("Tools/libfive/Log Native Version")]
    static void LogVersion() {
      Debug.Log(LFNative.IsAvailable
        ? "libfive " + LFNative.Version + " " + LFNative.Revision + " (" + LFNative.Branch + "), opcodes resolved from native: " + LFOpcode.ResolvedFromNative
        : "libfive native plugin not available on this platform.");
    }

    [MenuItem("GameObject/libfive/Shape", false, 10)]
    static void CreateShape(MenuCommand command) {
      var go = new GameObject("libfive Shape");
      GameObjectUtility.SetParentAndAlign(go, command.context as GameObject);
      Undo.RegisterCreatedObjectUndo(go, "Create libfive Shape");
      var shape = go.AddComponent<LFShape>();
      shape.op = LFShape.LibFive_Operation.Sphere;
      Selection.activeGameObject = go;
    }

    [MenuItem("GameObject/libfive/CSG Group (Union)", false, 11)]
    static void CreateGroup(MenuCommand command) {
      var go = new GameObject("libfive Union");
      GameObjectUtility.SetParentAndAlign(go, command.context as GameObject);
      Undo.RegisterCreatedObjectUndo(go, "Create libfive Group");
      var shape = go.AddComponent<LFShape>();
      shape.op = LFShape.LibFive_Operation.Union;
      Selection.activeGameObject = go;
    }
  }
}
