using UnityEngine;

namespace libfivesharp {
  /// <summary>
  /// Scripted (non-hierarchy) use of the wrapper: builds a shape each frame and meshes it on a worker
  /// thread, swapping the mesh in when the job finishes so the frame rate is never blocked.
  /// </summary>
  [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
  public class LibFiveExample : MonoBehaviour {
    [Tooltip("Octree cells per unit of length.")]
    [Range(4f, 64f)]
    public float resolution = 15f;

    [Tooltip("Split vertex normals along edges sharper than 25 degrees so the cut edges stay crisp.")]
    public bool sharpEdges = true;

    [Tooltip("Animate the cylinder radius.")]
    public bool animate = true;

    Mesh mesh;
    LFTree shape;
    LFMeshJob job;

    void OnEnable() {
      if (mesh == null) {
        mesh = new Mesh { name = "libfive example", hideFlags = HideFlags.DontSave };
        mesh.MarkDynamic();
      }
      GetComponent<MeshFilter>().sharedMesh = mesh;
      if (GetComponent<MeshRenderer>().sharedMaterial == null) GetComponent<MeshRenderer>().sharedMaterial = LFShape.DefaultMaterial;
    }

    void OnDisable() {
      if (job != null) { job.Dispose(); job = null; }
      if (shape != null) { shape.Dispose(); shape = null; }
    }

    void OnDestroy() {
      if (mesh != null) Destroy(mesh);
    }

    void Update() {
      // Pick up the previous frame's result (or block for it, if we were asked to re-mesh every frame).
      if (job != null && job.IsCompleted) {
        job.Complete(mesh, sharpEdges ? 25f : 180f);
        job.Dispose();
        job = null;
        shape.Dispose();
        shape = null;
      }

      // One job in flight at a time; skip frames while libfive is still busy.
      if (job == null && (animate || mesh.vertexCount == 0)) {
        shape = BuildShape(0.6f + Mathf.Sin(Time.time) * 0.05f);
        job = LFMeshJob.Schedule(shape, new Bounds(Vector3.zero, Vector3.one * 3.1f), resolution);
      }
    }

    /// <summary>A sphere with three perpendicular cylinders bored through it.</summary>
    static LFTree BuildShape(float boreRadius) {
      using (LFContext.Push()) {
        LFTree bore = LFMath.Cylinder(boreRadius, 2f, new Vector3(0f, 0f, -1f));
        LFTree result = LFMath.Difference(
          LFMath.Sphere(1f),
          bore,
          LFMath.ReflectXZ(bore),
          LFMath.ReflectYZ(bore));
        // Everything else built in this block is freed when it ends; the result survives.
        return LFContext.Active.Detach(result);
      }
    }
  }
}
