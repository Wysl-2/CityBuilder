using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.ProBuilder;

#if UNITY_EDITOR
using UnityEditor;
#endif


public class ProceduralIntersection : MonoBehaviour
{
    public Material material;

    [Header("Dimensions")]
    public Vector2 Size;
    public float   RoadHeight;

    [Header("Connections")]
    public bool ConnectedNorth;
    public bool ConnectedSouth;
    public bool ConnectedEast;
    public bool ConnectedWest;

    [Header("Intersection Profile Settings")]
    public IntersectionGeometryConfig intersectionConfig = IntersectionGeometryConfig.Default();

    // in ProceduralIntersection.cs (inside class)
    [Header("Shared Defaults & Overrides")]
    [SerializeField] private bool overrideCornerSizes = false;
    [SerializeField] private CornerSizeSet cornerSizesOverride; // only used if overrideCornerSizes = true

    [HideInInspector] public IntersectionModel Model;

    [SerializeField, HideInInspector] private ProBuilderMesh _builtPB;

    // Manual entry points
    [ContextMenu("Rebuild Now")]   // optional: right-click component → Rebuild Now
    public void Rebuild()
    {
        ClearBuilt();

        Model = new IntersectionModel(
            Size, RoadHeight,
            ConnectedNorth, ConnectedEast, ConnectedSouth, ConnectedWest,
            intersectionConfig);

        if (material)
        {
            var mr = GetComponent<MeshRenderer>();
            if (mr) mr.sharedMaterial = material;
        }

        var builder = new PBMeshBuilder();
        var sinkTag = gameObject.AddComponent<FaceSinkTag>();
        builder.Sink = sinkTag;

        if (Model.CornerSW.exists) CornerModule.CreateCorner(builder, Model, Model.CornerSW, CornerId.SW, transform);
        if (Model.CornerSE.exists) CornerModule.CreateCorner(builder, Model, Model.CornerSE, CornerId.SE, transform);
        if (Model.CornerNE.exists) CornerModule.CreateCorner(builder, Model, Model.CornerNE, CornerId.NE, transform);
        if (Model.CornerNW.exists) CornerModule.CreateCorner(builder, Model, Model.CornerNW, CornerId.NW, transform);

        FootpathModule.CreateFootpath(builder, transform, Model, Side.South);
        FootpathModule.CreateFootpath(builder, transform, Model, Side.East);
        FootpathModule.CreateFootpath(builder, transform, Model, Side.North);
        FootpathModule.CreateFootpath(builder, transform, Model, Side.West);

        RoadFillModule.CreateRoadFill(builder, Model, transform);

        var mats = material ? new[] { material } : null;
        _builtPB = builder.Build(mats, this.transform);

        ApplyCollider();

        #if UNITY_EDITOR
        EditorUtility.SetDirty(this);
        if (!Application.isPlaying) UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        #endif
    }

    [ContextMenu("Clear Built")]   // optional: right-click component → Clear Built
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
        // Keep runtime behavior if you want:
        if (Application.isPlaying) Rebuild();
    }

    void OnDrawGizmos()
    {
        // optional guard to avoid noise on disabled objects
        if (this == null || !enabled) return;

        Gizmos.color  = Color.red;
        Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);

        var a = new Vector3(0f,      0f,      0f);
        var b = new Vector3(Size.x,  0f,      0f);
        var c = new Vector3(Size.x,  0f,      Size.y);
        var d = new Vector3(0f,      0f,      Size.y);

        Gizmos.DrawLine(a, b);
        Gizmos.DrawLine(b, c);
        Gizmos.DrawLine(c, d);
        Gizmos.DrawLine(d, a);

        if (Model != null)
        {
            Gizmos.DrawWireSphere(Model.CornerSW.apex, 0.10f);
            Gizmos.DrawWireSphere(Model.CornerSE.apex, 0.10f);
            Gizmos.DrawWireSphere(Model.CornerNW.apex, 0.10f);
            Gizmos.DrawWireSphere(Model.CornerNE.apex, 0.10f);
        }
    }

    private void ApplyCollider()
    {
        if (!_builtPB) return;

        // Bake PB to MeshFilter.sharedMesh
        _builtPB.ToMesh();
        _builtPB.Refresh();

        var mf = _builtPB.GetComponent<MeshFilter>();
        if (!mf || !mf.sharedMesh) return;

        // Ensure/refresh collider on the SAME child that holds the mesh
        var col = _builtPB.GetComponent<MeshCollider>();
        if (!col) col = _builtPB.gameObject.AddComponent<MeshCollider>();

        col.sharedMesh = null;            // force PhysX recook
        col.sharedMesh = mf.sharedMesh;
        col.convex = false;               // roads/intersections are non-convex

    }

    
    public void ApplySharedDefaults(RoadSystemConfig cfg)
    {
        // Always inherit RoadHeight + Curb/Gutter
        RoadHeight = cfg.roadHeight;

        var ic = intersectionConfig;
        ic.curb = cfg.curb;

        // Corner sizes: inherit unless overridden here
        if (!overrideCornerSizes)
        {
            var corners = ic.corners;
            corners.sizes = cfg.defaultCornerSizes;
            ic.corners = corners;
        }
        else
        {
            var corners = ic.corners;
            corners.sizes = cornerSizesOverride;
            ic.corners = corners;
        }

        intersectionConfig = ic;
    }
}










