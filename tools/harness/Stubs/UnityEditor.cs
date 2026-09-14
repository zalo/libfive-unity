// Compile-only UnityEditor stand-ins for the editor scripts.
using System;
using UnityEngine;

namespace UnityEditor {
  public class CustomEditor : Attribute { public CustomEditor(Type t) { } }
  public class CanEditMultipleObjects : Attribute { }
  public class InitializeOnLoadAttribute : Attribute { }
  public class MenuItem : Attribute { public MenuItem(string p) { } public MenuItem(string p, bool v) { } public MenuItem(string p, bool v, int prio) { } }
  public class DrawGizmo : Attribute { public DrawGizmo(GizmoType t) { } public DrawGizmo(GizmoType t, Type forType) { } }
  [Flags] public enum GizmoType { Pickable = 1, NotInSelectionHierarchy = 2, NonSelected = 32, Selected = 4, Active = 8, InSelectionHierarchy = 16 }
  public enum MessageType { None, Info, Warning, Error }

  public class Editor : ScriptableObject {
    public UnityEngine.Object target;
    public UnityEngine.Object[] targets = new UnityEngine.Object[0];
    public SerializedObject serializedObject = new SerializedObject();
    public virtual void OnInspectorGUI() { }
  }
  public class ScriptableObject : UnityEngine.Object { }
  public class SerializedObject {
    public void Update() { }
    public SerializedProperty FindProperty(string n) => new SerializedProperty();
    public bool ApplyModifiedProperties() => false;
  }
  public class SerializedProperty { public int intValue; public bool hasMultipleDifferentValues; public string tooltip = ""; }
  public class GUIContent { public GUIContent(string a) { } public GUIContent(string a, string b) { } }
  public class GUIStyle { }
  public static class EditorStyles { public static GUIStyle boldLabel = new GUIStyle(); }
  public static class EditorGUILayout {
    public static void PropertyField(SerializedProperty p) { }
    public static void PropertyField(SerializedProperty p, GUIContent c) { }
    public static void Space() { }
    public static void LabelField(string s, GUIStyle st) { }
    public static void HelpBox(string s, MessageType t) { }
    public class HorizontalScope : IDisposable { public void Dispose() { } }
  }
  public static class EditorGUI { public class DisabledScope : IDisposable { public DisabledScope(bool d) { } public void Dispose() { } } }
  public static class EditorUtility { public static string SaveFilePanel(string a, string b, string c, string d) => null; }
  public static class EditorPrefs { public static bool GetBool(string k, bool d) => d; public static void SetBool(string k, bool v) { } }
  public static class Undo {
    public static event Action undoRedoPerformed;
    public static void RegisterCreatedObjectUndo(UnityEngine.Object o, string n) { }
    internal static void Fire() => undoRedoPerformed?.Invoke();
  }
  public static class Menu { public static void SetChecked(string p, bool v) { } }
  public class MenuCommand { public UnityEngine.Object context; }
  public static class GameObjectUtility { public static void SetParentAndAlign(GameObject go, GameObject parent) { } }
  public static class Selection { public static GameObject activeGameObject; }
  public static class EditorApplication { public static void QueuePlayerLoopUpdate() { } }
  public class SceneView { public static void RepaintAll() { } }
}

namespace UnityEngine {
  public static class GUILayout { public static bool Button(string s) => false; }
}
