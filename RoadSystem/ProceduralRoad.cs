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

    [Header("Footpath / Curb")]
    public float footpathDepth = 1.5f;
    public CurbGutter curb = CurbGutter.Default();

    [Header("Material")]
    public Material material;

    [SerializeField, HideInInspector] private ProBuilderMesh _builtPB;

    [ContextMenu("Rebuild Now")]
    public void Rebuild()
    {
        ClearBuilt();

        // Clamp to sane values
        width         = Mathf.Max(0.01f, width);
        length        = Mathf.Max(0.01f, length);
        footpathDepth = Mathf.Clamp(footpathDepth, 0f, width * 0.5f);

        var builder = new PBMeshBuilder();
        var sinkTag = gameObject.AddComponent<FaceSinkTag>();
        builder.Sink = sinkTag;

        // 1) Footpaths + curb/gutter (local space, pivot = back edge centre)
        RoadFootpathModule.Create(
            builder,
            width,
            length,
            RoadHeight,
            footpathDepth,
            curb,
            Axis
        );

        // 2) Carriageway in the remaining centre area
        var carriageFaces = new List<Vector3[]>();

        // Total inset from each outer edge before the road surface starts
        float inset = footpathDepth + curb.skirtOut + curb.gutterWidth;

        if (Axis == RoadAxis.Z)
        {
            // Road runs along +Z, width along X, pivot at (0,0,0) at back centre.
            float halfW = width * 0.5f;

            float innerLeft  = -halfW + inset;
            float innerRight =  halfW - inset;

            // Optional safety clamp in case width is too small:
            if (innerRight > innerLeft)
                carriageFaces.Add(QuadXZ(innerLeft, innerRight, 0f, length, RoadHeight));
        }
        else // Axis == RoadAxis.X
        {
            // Road runs along +X, width along Z, pivot at (0,0,0) at back centre.
            float halfW = width * 0.5f;

            float innerBackZ  = -halfW + inset;
            float innerFrontZ =  halfW - inset;

            float x0 = 0f;
            float x1 = length;

            if (innerFrontZ > innerBackZ)
                carriageFaces.Add(QuadXZ(x0, x1, innerBackZ, innerFrontZ, RoadHeight));
        }

        // Tag carriageway faces as Road
        if (carriageFaces.Count > 0)
            builder.AddFaces(carriageFaces, RoadSurfaceMasks.Road);

        var mats = material ? new[] { material } : null;
        _builtPB = builder.Build(mats, transform);

        ApplyCollider();

    #if UNITY_EDITOR
        EditorUtility.SetDirty(this);
    #endif
    }


    public void OnDrawGizmos()
    {
        if (!enabled) return;

        Gizmos.color  = Color.red;
        Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);

        Vector3 bl, br, fl, fr; // back-left, back-right, front-left, front-right (local)

        if (Axis == RoadAxis.Z)
        {
            // Road extends along +Z, pivot at back centre (0,0,0).
            float halfW = width * 0.5f;

            bl = new Vector3(-halfW, 0f, 0f);
            br = new Vector3( halfW, 0f, 0f);
            fl = new Vector3(-halfW, 0f, length);
            fr = new Vector3( halfW, 0f, length);
        }
        else // RoadAxis.X
        {
            // Road extends along +X, width along Z, pivot at back centre (0,0,0).
            float halfW = width * 0.5f;

            bl = new Vector3(0f,      0f, -halfW);
            br = new Vector3(0f,      0f,  halfW);
            fl = new Vector3(length,  0f, -halfW);
            fr = new Vector3(length,  0f,  halfW);
        }

        // Draw rectangle
        Gizmos.DrawLine(bl, br);
        Gizmos.DrawLine(br, fr);
        Gizmos.DrawLine(fr, fl);
        Gizmos.DrawLine(fl, bl);
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
