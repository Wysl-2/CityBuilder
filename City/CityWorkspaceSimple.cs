// // CityWorkspaceSimple.cs
// // Manual, rectangles-only City Builder workspace: one big Area,
// // plus BuildingPlots, RoadBands, and (now) IntersectionAreas.
// // PREVIEW generation instantiates ProcBuilding / ProcRoad / ProcIntersection.
// // Baking is handled by the editor script.

// using System;
// using System.Collections.Generic;
// using UnityEngine;

// [DisallowMultipleComponent]
// public class CityWorkspaceSimple : MonoBehaviour
// {
//     [Header("Main Area (meters, XZ)")]
//     public float areaY = 0f;
//     public Vector2 areaSize = new Vector2(200, 200); // X width, Z depth

//     [Header("Ground")]
//     public bool makeGround = true;
//     public Material groundMaterial;

//     [Header("Ground Grid (subdivision + cull)")]
//     [Tooltip("Grid cell size in meters (smaller = cleaner edges, more tris).")]
//     public float groundCellSize = 2f;

//     [Tooltip("Generate a MeshCollider for the ground.")]
//     public bool groundAddMeshCollider = true;

//     [Tooltip("If true, collider also removes cells that merely INTERSECT reserved regions (no ground collider under roads/buildings/intersections).")]
//     public bool groundColliderRemoveIntersections = true;

//     [Tooltip("Carve away ground under the full road cross-section (for grid culling).")]
//     public bool groundCarveUnderRoads = true;

//     [Tooltip("Carve away ground under building plots (for grid culling).")]
//     public bool groundCarveUnderBuildings = false;

//     [Tooltip("Carve away ground under intersection areas (for grid culling).")]
//     public bool groundCarveUnderIntersections = true;

//     [Tooltip("Extra clearance (meters) around the road cross-section when carving.")]
//     public float groundRoadsMargin = 0.05f;

//     [Tooltip("Extra clearance (meters) around building plots when carving.")]
//     public float groundBuildingsMargin = 0.02f;

//     [Tooltip("Extra clearance (meters) around intersection areas when carving.")]
//     public float groundIntersectionsMargin = 0.02f;

//     public Vector2 groundUVMeters = new Vector2(1, 1); // 1m tiles by default

//     // -------------------- Authoring Types --------------------

//     [Serializable]
//     public class BuildingPlot
//     {
//         public string name = "Plot";
//         [Tooltip("Local-space center (x,z) within the Area")]
//         public Vector2 center = Vector2.zero;
//         [Tooltip("Local-space size (x,z)")]
//         public Vector2 size = new Vector2(12, 16);

//         [Header("Building")]
//         public bool randomizeHeight = true;
//         public float heightMin = 8f;
//         public float heightMax = 24f;
//         public float fixedHeight = 12f;
//         public Material material;
//         public Vector2 wallUVMeters = new Vector2(1, 1);
//         public Vector2 roofUVMeters = new Vector2(1, 1);
//     }

//     [Serializable]
//     public class RoadBand
//     {
//         public string name = "Road";
//         [Tooltip("Local-space center (x,z) within the Area")]
//         public Vector2 center = Vector2.zero;
//         [Tooltip("Local-space size (x,z). The road spans the LONG axis end-to-end.")]
//         public Vector2 size = new Vector2(80, 12);
//         public Material material;

//         [Header("Cross-Section Widths (meters)")]
//         [Min(0.1f)] public float carriagewayWidth = 6f;
//         [Min(0.0f)] public float gutterWidth = 0.5f;
//         [Min(0.0f)] public float curbWidth = 0.2f;
//         [Min(0.1f)] public float footpathWidth = 2.0f;

//         [Header("Heights (meters)")]
//         [Min(0.0f)] public float carriagewaySetIn = 0.04f; // lane edges below footpath plane
//         [Min(0.0f)] public float gutterDepth = 0.05f;      // gutter bottom below footpath plane (0)
//         [Min(0.0f)] public float curbHeight  = 0.15f;      // shaping; footpath remains at 0
//         [Range(0f, 0.08f)] public float camberSlope = 0.02f; // drop per meter from lane edges to center

//         [Header("UV (meters per tile)")]
//         public float metersPerTileU = 1f;
//         public float metersPerTileV = 1f;
//         public float uOffset = 0f;
//         public float vOffset = 0f;
//     }

//     // -------------------- Side Profile (per-connection cross section) --------------------
//     [Serializable]
//     public struct SideProfile
//     {
//         [Header("Widths (m)")]
//         [Min(0.1f)] public float carriagewayWidth;
//         [Min(0.0f)] public float gutterWidth;
//         [Min(0.0f)] public float curbWidth;
//         [Min(0.1f)] public float footpathWidth;

//         [Header("Heights (m)")]
//         [Min(0.0f)] public float carriagewaySetIn; // lane below footpath plane
//         [Min(0.0f)] public float gutterDepth;
//         [Min(0.0f)] public float curbHeight;

//         // Sum of the outward ring from carriageway -> gutter -> curb -> footpath
//         public float ReserveRing => gutterWidth + curbWidth + footpathWidth;

//         public static SideProfile FromUniform(
//             float carrW, float gutW, float curbW, float footW,
//             float carrSetIn, float gutDepth, float curbH)
//         {
//             return new SideProfile
//             {
//                 carriagewayWidth = carrW,
//                 gutterWidth      = gutW,
//                 curbWidth        = curbW,
//                 footpathWidth    = footW,
//                 carriagewaySetIn = carrSetIn,
//                 gutterDepth      = gutDepth,
//                 curbHeight       = curbH
//             };
//         }
//     }

//     [Serializable]
//     public class IntersectionArea
//     {
//         public string name = "Intersection";

//         [Tooltip("Local-space center (x,z) within the Area")]
//         public Vector2 center = Vector2.zero;

//         [Tooltip("Local-space size (x,z)")]
//         public Vector2 size = new Vector2(16, 16);

//         public Material material;

//         [Header("Connections (carriageway)")]
//         public bool connectNorth = true;
//         public bool connectEast  = true;
//         public bool connectSouth = true;
//         public bool connectWest  = true;

//         // -------- Uniform (fallback) cross-section (kept for backward compatibility) --------
//         [Header("Uniform Cross-Section (fallback) — used unless per-side override is enabled")]
//         [Min(0.1f)] public float carriagewayWidth = 6f; // total lane width inside the hub
//         [Min(0.0f)] public float gutterWidth = 0.5f;
//         [Min(0.0f)] public float curbWidth = 0.2f;
//         [Min(0.1f)] public float footpathWidth = 2.0f;

//         [Header("Uniform Heights (m)")]
//         [Min(0.0f)] public float carriagewaySetIn = 0.04f; // lane baseline below footpath plane
//         [Min(0.0f)] public float gutterDepth = 0.05f;
//         [Min(0.0f)] public float curbHeight  = 0.15f;

//         // -------- Per-side overrides --------
//         [Header("Per-Side Overrides (optional)")]
//         public bool useNorthOverride = false;
//         public bool useEastOverride  = false;
//         public bool useSouthOverride = false;
//         public bool useWestOverride  = false;

//         public SideProfile northOverride;
//         public SideProfile eastOverride;
//         public SideProfile southOverride;
//         public SideProfile westOverride;

//         // -------- UV & Collider (unchanged) --------
//         [Header("UV (meters per tile)")]
//         public float metersPerTileU = 1f;
//         public float metersPerTileV = 1f;
//         public float uOffset = 0f;
//         public float vOffset = 0f;

//         [Header("Collider")]
//         public bool addMeshCollider = true;
//         public bool colliderConvex  = false;
//         public PhysicMaterial physicsMaterial;

//         // -------- Helpers to get the effective (resolved) profile per side --------
//         public SideProfile UniformProfile =>
//             SideProfile.FromUniform(carriagewayWidth, gutterWidth, curbWidth, footpathWidth,
//                                     carriagewaySetIn, gutterDepth, curbHeight);

//         public SideProfile ProfileNorth => useNorthOverride ? northOverride : UniformProfile;
//         public SideProfile ProfileEast  => useEastOverride  ? eastOverride  : UniformProfile;
//         public SideProfile ProfileSouth => useSouthOverride ? southOverride : UniformProfile;
//         public SideProfile ProfileWest  => useWestOverride  ? westOverride  : UniformProfile;

//         public float ReserveNorth => ProfileNorth.ReserveRing;
//         public float ReserveEast  => ProfileEast.ReserveRing;
//         public float ReserveSouth => ProfileSouth.ReserveRing;
//         public float ReserveWest  => ProfileWest.ReserveRing;
//     }

//     // -------------------- Contents --------------------

//     [Header("Contents")]
//     public List<BuildingPlot> buildingPlots = new();
//     public List<RoadBand> roadBands = new();

//     [Tooltip("Manual rectangle-based intersections (recommended).")]
//     public List<IntersectionArea> intersections = new();

//     [Header("Preview Roots (auto)")]
//     public Transform previewRoot;
//     public Transform groundRoot;
//     public Transform buildingRoot;
//     public Transform roadRoot;
//     public Transform intersectionRoot;

//     // -------- Public API called by editor --------
//     public void ClearPreview()
//     {
//         if (previewRoot) DestroyImmediate(previewRoot.gameObject);
//         previewRoot = groundRoot = buildingRoot = roadRoot = intersectionRoot = null;
//     }

//     public void GeneratePreview()
//     {
//         ClearPreview();
//         previewRoot = new GameObject("PREVIEW").transform;
//         previewRoot.SetParent(transform, false);

//         if (makeGround)
//         {
//             groundRoot = new GameObject("GROUND").transform;
//             groundRoot.SetParent(previewRoot, false);
//             CreateGround();
//         }

//         buildingRoot = new GameObject("BUILDINGS").transform;
//         buildingRoot.SetParent(previewRoot, false);
//         CreateBuildings();

//         roadRoot = new GameObject("ROADS").transform;
//         roadRoot.SetParent(previewRoot, false);
//         CreateRoads();

//         intersectionRoot = new GameObject("INTERSECTIONS").transform;
//         intersectionRoot.SetParent(previewRoot, false);
//         CreateIntersections();
//     }

//     // --------- Preview Builders ----------
//     void CreateGround()
//     {
//         var go = new GameObject("AreaGroundGrid");
//         go.transform.SetParent(groundRoot, false);
//         go.transform.position = transform.position;

//         var gg = go.AddComponent<ProcGroundGrid>();
//         gg.workspace = this;                 // let it read bands/plots/intersections
//         gg.areaY    = areaY;
//         gg.areaSize = areaSize;
//         gg.uvMeters = groundUVMeters;
//         gg.material = groundMaterial;

//         // from workspace fields (now exposed in Inspector)
//         gg.cellSize                    = Mathf.Max(0.1f, groundCellSize);
//         gg.addMeshCollider             = groundAddMeshCollider;
//         gg.colliderRemoveIntersections = groundColliderRemoveIntersections;

//         gg.carveUnderRoads          = groundCarveUnderRoads;
//         gg.carveUnderBuildings      = groundCarveUnderBuildings;
//         gg.carveUnderIntersections  = groundCarveUnderIntersections;
//         gg.roadsMargin              = Mathf.Max(0f, groundRoadsMargin);
//         gg.buildingsMargin          = Mathf.Max(0f, groundBuildingsMargin);
//         gg.intersectionsMargin      = Mathf.Max(0f, groundIntersectionsMargin);

//         gg.Build();
//     }

//     void CreateBuildings()
//     {
//         foreach (var p in buildingPlots)
//         {
//             Vector2 half = new Vector2(Mathf.Max(0.01f, p.size.x * 0.5f), Mathf.Max(0.01f, p.size.y * 0.5f));
//             Vector3 c = LocalXZToWorld(p.center, areaY);

//             Vector3 bl = c + new Vector3(-half.x, 0, -half.y);
//             Vector3 br = c + new Vector3(+half.x, 0, -half.y);
//             Vector3 tr = c + new Vector3(+half.x, 0, +half.y);
//             Vector3 tl = c + new Vector3(-half.x, 0, +half.y);

//             var go = new GameObject(string.IsNullOrEmpty(p.name) ? "Building" : p.name);
//             go.transform.SetParent(buildingRoot, false);

//             var pb = go.AddComponent<ProcBuilding>();
//             pb.material = p.material;
//             pb.randomizeHeight = p.randomizeHeight;
//             pb.heightMin = p.heightMin;
//             pb.heightMax = p.heightMax;
//             pb.fixedHeight = p.fixedHeight;

//             pb.wallMetersPerTileU = Mathf.Max(0.001f, p.wallUVMeters.x);
//             pb.wallMetersPerTileV = Mathf.Max(0.001f, p.wallUVMeters.y);
//             pb.roofMetersPerTileU = Mathf.Max(0.001f, p.roofUVMeters.x);
//             pb.roofMetersPerTileV = Mathf.Max(0.001f, p.roofUVMeters.y);

//             // footprint in workspace local space
//             pb.footprint = new List<Vector3>
//             {
//                 transform.InverseTransformPoint(bl),
//                 transform.InverseTransformPoint(br),
//                 transform.InverseTransformPoint(tr),
//                 transform.InverseTransformPoint(tl),
//             };

//             pb.Build();
//             go.isStatic = true;
//         }
//     }

//     void CreateRoads()
//     {
//         foreach (var r in roadBands)
//         {
//             Vector2 size = new Vector2(Mathf.Max(0.01f, r.size.x), Mathf.Max(0.01f, r.size.y));
//             bool longX = size.x >= size.y;

//             // endpoints along the long axis, on the area’s Y plane
//             Vector3 c = LocalXZToWorld(r.center, areaY);
//             Vector3 a, b;
//             if (longX)
//             {
//                 float hx = size.x * 0.5f;
//                 a = c + new Vector3(-hx, 0, 0);
//                 b = c + new Vector3(+hx, 0, 0);
//             }
//             else
//             {
//                 float hz = size.y * 0.5f;
//                 a = c + new Vector3(0, 0, -hz);
//                 b = c + new Vector3(0, 0, +hz);
//             }

//             var go = new GameObject(string.IsNullOrEmpty(r.name) ? "Road" : r.name);
//             go.transform.SetParent(roadRoot, false);

//             // path points as children for ProcRoad
//             var tA = new GameObject("A").transform; tA.SetParent(go.transform, false); tA.position = a;
//             var tB = new GameObject("B").transform; tB.SetParent(go.transform, false); tB.position = b;

//             var pr = go.AddComponent<ProcRoad>();
//             pr.path = new List<Transform> { tA, tB };
//             pr.loop = false;
//             pr.material = r.material;

//             // widths
//             pr.carriagewayWidth = r.carriagewayWidth;
//             pr.gutterWidth = r.gutterWidth;
//             pr.curbWidth = r.curbWidth;
//             pr.footpathWidth = r.footpathWidth;

//             // heights (footpath plane is 0; these shape the dip)
//             pr.carriagewaySetIn = r.carriagewaySetIn;  // pass set-in depth
//             pr.gutterDepth = r.gutterDepth;
//             pr.curbHeight  = r.curbHeight;
//             pr.camberSlope = r.camberSlope;

//             // UVs (meters-based)
//             pr.metersPerTileU = r.metersPerTileU;
//             pr.metersPerTileV = r.metersPerTileV;
//             pr.uOffset = r.uOffset;
//             pr.vOffset = r.vOffset;

//             pr.Build();
//             go.isStatic = true;
//         }
//     }

//     void CreateIntersections()
//     {
//         foreach (var ia in intersections)
//         {
//             Vector3 wpos = LocalXZToWorld(ia.center, areaY);

//             var go = new GameObject(string.IsNullOrEmpty(ia.name) ? "Intersection" : ia.name);
//             go.transform.SetParent(intersectionRoot, false);
//             go.transform.position = wpos;

//             var pi = go.AddComponent<ProcIntersection>(); // manual-rect version (temp)
//             // Rect (in local workspace space)
//             pi.center = ia.center;
//             pi.size   = new Vector2(
//                 Mathf.Max(0.1f, ia.size.x),
//                 Mathf.Max(0.1f, ia.size.y)
//             );
//             pi.areaY  = areaY;

//             // Connections
//             pi.connectNorth = ia.connectNorth;
//             pi.connectEast  = ia.connectEast;
//             pi.connectSouth = ia.connectSouth;
//             pi.connectWest  = ia.connectWest;

//             // Widths (temp generator uses uniform values)
//             pi.carriagewayWidth = ia.carriagewayWidth;
//             pi.gutterWidth      = ia.gutterWidth;
//             pi.curbWidth        = ia.curbWidth;
//             pi.footpathWidth    = ia.footpathWidth;

//             // Heights
//             pi.carriagewaySetIn = ia.carriagewaySetIn;
//             pi.gutterDepth      = ia.gutterDepth;
//             pi.curbHeight       = ia.curbHeight;

//             // Per-side overrides
//             pi.useNorthOverride = ia.useNorthOverride;
//             pi.useEastOverride  = ia.useEastOverride;
//             pi.useSouthOverride = ia.useSouthOverride;
//             pi.useWestOverride  = ia.useWestOverride;

//             pi.northOverride = ia.northOverride;
//             pi.eastOverride  = ia.eastOverride;
//             pi.southOverride = ia.southOverride;
//             pi.westOverride  = ia.westOverride;

//             // UVs
//             pi.metersPerTileU = ia.metersPerTileU;
//             pi.metersPerTileV = ia.metersPerTileV;
//             pi.uOffset = ia.uOffset;
//             pi.vOffset = ia.vOffset;

//             // Material & collider
//             pi.material = ia.material;
//             pi.useMeshCollider = ia.addMeshCollider;
//             pi.colliderConvex  = ia.colliderConvex;
//             pi.physicsMaterial = ia.physicsMaterial;

//             pi.Build();
//             go.isStatic = true;
//         }
//     }

//     // ---------- Helpers ----------
//     public Vector3 LocalXZToWorld(Vector2 xz, float y) =>
//         transform.TransformPoint(new Vector3(xz.x, y, xz.y));
// }
