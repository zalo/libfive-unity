using UnityEngine;
using System.Collections;
using UnityEditor;
using System.IO;
using System.Text;

namespace libfivesharp {
  [CustomEditor(typeof(LFShape))]
  public class LFShapeEditor : Editor {
    public override void OnInspectorGUI() {
      DrawDefaultInspector();

      if (GUILayout.Button("Save STL")) {
        LFShape shape = (LFShape)target;
        string pathString = Path.Combine(Directory.GetParent(Application.dataPath).ToString(), shape.gameObject.name + " - " + System.DateTime.Now.ToShortTimeString().Replace(":", ".") + ".stl");
        Mesh toWrite = shape.tree.RenderMesh(new Bounds(shape.transform.position, Vector3.one * shape.boundsSize), shape.resolution + 0.001f);
        string stlString = LFMeshRendering.createSTL(toWrite);
        try {
          File.WriteAllText(pathString, stlString);
          Debug.Log("Saved STL at: " + pathString);
        } catch (System.Exception e) {
          Debug.LogError("Failed to save STL! - " + e);
        }
      }
    }
  }
}
