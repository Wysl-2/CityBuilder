// Assets/RoadSystem/ProceduralRoad.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.ProBuilder;

#if UNITY_EDITOR
using UnityEditor;
#endif

public enum RoadAxis { Z, X } // Z = North/South, X = East/West

public class ProceduralRoad : MonoBehaviour
{
    [Header("Dimensions")]
    // public Vector2 Size = new(12, 12); // X = width, Y = length
    public float length;
    public float width;
    public float   RoadHeight = 0f;
    public RoadAxis Axis = RoadAxis.Z;

    [Header("Material")]
    public Material material;

    [SerializeField, HideInInspector] private ProBuilderMesh _builtPB;

    [ContextMenu("Rebuild Now")]
    public void Rebuild()
    {
        ClearBuilt();

        List<Vector3[]> faces;

        if (Axis == RoadAxis.Z)
        {
            // Pivot centered on back edge
            float x0 = -width * 0.5f;
            float x1 =  width * 0.5f;
            float z0 = 0f;
            float z1 = length;

            faces = new List<Vector3[]> { QuadXZ(x0, x1, z0, z1, RoadHeight) };
        }
        else // Axis == X
        {
            // Pivot centered on back edge, road extends +X
            float x0 = 0f;
            float x1 = length;
            float z0 = -width * 0.5f;
            float z1 =  width * 0.5f;

            faces = new List<Vector3[]> { QuadXZ(x0, x1, z0, z1, RoadHeight) };
        }

        // // Apply world rotation & translation
        // var worldRot = transform ? transform.rotation : Quaternion.identity;
        // faces = VertexOperations.RotateMany(faces, worldRot, Vector3.zero);
        // faces = VertexOperations.TranslateMany(faces, transform ? transform.position : Vector3.zero);

        // Build PB mesh (unchanged)
        var builder = new PBMeshBuilder();
        var sinkTag = gameObject.AddComponent<FaceSinkTag>();
        builder.Sink = sinkTag;
        builder.AddFaces(faces);

        var mats = material ? new[] { material } : null;
        _builtPB = builder.Build(mats, transform);

        ApplyCollider();

    #if UNITY_EDITOR
        EditorUtility.SetDirty(this);
        if (!Application.isPlaying)
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
    #endif
    }


    [ContextMenu("Clear Built")]
    public void ClearBuilt()
    {
        var sink = GetComponent<FaceSinkTag>();
        if (sink)
        {
            #if UNITY_EDITOR
            if (!Application.isPlaying) DestroyImmediate(sink);
            else Destroy(sink);
            #else
            Destroy(sink);
            #endif
        }

        var pbs = GetComponentsInChildren<ProBuilderMesh>(true);
        foreach (var pb in pbs)
        {
            if (pb.gameObject == gameObject) continue;
            #if UNITY_EDITOR
            if (!Application.isPlaying) DestroyImmediate(pb.gameObject);
            else Destroy(pb.gameObject);
            #else
            Destroy(pb.gameObject);
            #endif
        }

        var cols = GetComponentsInChildren<MeshCollider>(true);
        foreach (var col in cols)
        {
            if (col.gameObject == gameObject) continue;
            #if UNITY_EDITOR
            if (!Application.isPlaying) DestroyImmediate(col);
            else Destroy(col);
            #else
            Destroy(col);
            #endif
        }
    }

    void Start()
    {
        if (Application.isPlaying) Rebuild();
    }

    private void ApplyCollider()
    {
        if (!_builtPB) return;

        _builtPB.ToMesh();
        _builtPB.Refresh();

        var mf = _builtPB.GetComponent<MeshFilter>();
        if (!mf || !mf.sharedMesh) return;

        var col = _builtPB.GetComponent<MeshCollider>();
        if (!col) col = _builtPB.gameObject.AddComponent<MeshCollider>();

        col.sharedMesh = null;
        col.sharedMesh = mf.sharedMesh;
        col.convex = false;
    }

    // CCW quad on xz at y (NE, NW, SW, SE) -> up-facing
    private static Vector3[] QuadXZ(float x0, float x1, float z0, float z1, float y)
        => new[]
        {
            new Vector3(x1, y, z1),
            new Vector3(x0, y, z1),
            new Vector3(x0, y, z0),
            new Vector3(x1, y, z0),
        };
}
