using UnityEngine;

namespace libfivesharp {
  public class LibFiveExample : MonoBehaviour {
    [Range(10f, 30f)]
    public float resolution = 15;

    [Tooltip("Splits the shared vertices on the output libfive mesh and selectively " +
      "rejoins them to visually preserve the sharp-edged details of the output model " +
      "in rendering.")]
    public bool sharpEdges = false;

    void Update() {
      MeshFilter filter;
      if ((filter = GetComponent<MeshFilter>()) != null) {
        GenerateMesh(filter.mesh, sharpEdges);
      }
    }

    void GenerateMesh(Mesh meshToFill, bool sharpEdges = true) {
      using (LFContext.Active = new Context()) {
        float innerRadius = 0.6f + (Mathf.Sin(Time.time) * 0.05f);
        LFTree cylinder = LFMath.Cylinder(innerRadius, 2f, Vector3.back);
        LFTree toRender = LFMath.Difference(
          LFMath.Sphere(1f),
          cylinder,
          LFMath.ReflectXZ(cylinder),
          LFMath.ReflectYZ(cylinder));

        toRender.RenderMesh(meshToFill, new Bounds(Vector3.zero, Vector3.one * 3.1f), resolution, 20f);

        if (sharpEdges) meshToFill.RecalculateNormals(25f);
      }
    }
  }
}
