// // ProcGroundGrid.cs
// // Subdivided ground with quad-culling under reserved areas (roads/buildings/intersections).
// // UVs in meters (1 UV == 1 m by default). Optional MeshCollider.
// // Can read reserved rectangles from CityGridWorkspace or from a manual list.

// using System;
// using System.Collections.Generic;
// using UnityEngine;
// using ProcGen; // uses SimpleMeshBuilder + UVMapping

// [DisallowMultipleComponent]
// [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
// public class ProcGroundGrid : MonoBehaviour
// {
//     [Header("Area (local XZ, meters)")]
//     public float areaY = 0f;
//     public Vector2 areaSize = new Vector2(200, 200); // centered on this transform

//     [Header("Subdivision")]
//     [Min(0.1f)] public float cellSize = 2f; // grid resolution (meters)

//     [Header("UV (meters per tile)")]
//     public Vector2 uvMeters = new Vector2(1, 1);

//     [Header("Material (optional)")]
//     public Material material;

//     [Header("Collider")]
//     public bool addMeshCollider = true;
//     [Tooltip("If true, collider also removes cells that merely INTERSECT reserved regions (no ground collider under roads/buildings/intersections).")]
//     public bool colliderRemoveIntersections = true;
//     public PhysicMaterial physicsMaterial;
//     public bool colliderConvex = false;

//     [Header("Collider Edge Handling")]
//     [Tooltip("Use N×N point sampling per cell; drop collider only if inside coverage ≥ threshold.")]
//     public bool colliderUseCoverage = true;

//     [Range(0f, 1f)]
//     public float colliderInsideThreshold = 0.7f; // 70% default

//     [Range(1, 5)]
//     public int colliderSamplesPerAxis = 3;       // 3×3 samples per cell

//     [Tooltip("Inflate reserved rectangles when testing coverage (meters).")]
//     public float colliderInflate = 0f;

//     [Header("Reserved Sources")]
//     public CityWorkspaceSimple workspace; // optional: if set, pulls bands/plots/intersections from it
//     public bool carveUnderRoads = true;
//     public bool carveUnderBuildings = false;
//     public bool carveUnderIntersections = true;     // NEW
//     public float roadsMargin = 0.05f;               // extra clearance around road cross-section
//     public float buildingsMargin = 0.02f;           // extra clearance around building plots
//     public float intersectionsMargin = 0.02f;       // NEW: clearance around intersection rects

//     [Header("Manual Reserved Rects (local XZ)")]
//     public List<ManualRect> manualReserved = new();

//     [Serializable]
//     public class ManualRect
//     {
//         public Vector2 center = Vector2.zero;
//         public Vector2 size = new Vector2(10, 10);
//     }

//     struct RectXZ
//     {
//         public float minX, maxX, minZ, maxZ;
//         public RectXZ(float minX, float maxX, float minZ, float maxZ)
//         { this.minX = minX; this.maxX = maxX; this.minZ = minZ; this.maxZ = maxZ; }
//     }

//     // keep references to destroy old generated meshes in editor (prevents leaks on repeated builds)
//     Mesh _lastRenderMesh, _lastColliderMesh;

//     void Awake() { Build(); }
//     void OnValidate() { if (enabled && gameObject.activeInHierarchy) Build(); }

//     public void Build()
//     {
//         // Ensure this object sits at the desired Y; vertices stay local at y=0.
//         var lp = transform.localPosition;
//         transform.localPosition = new Vector3(lp.x, areaY, lp.z);

//         var mf = GetComponent<MeshFilter>();
//         var mr = GetComponent<MeshRenderer>();
//         if (!mr) mr = gameObject.AddComponent<MeshRenderer>();
//         if (material != null) mr.sharedMaterial = material;
//         else mr.sharedMaterial = new Material(Shader.Find("Standard"));

//         // Clean up old generated meshes (Editor only) to avoid leaking
// #if UNITY_EDITOR
//         if (_lastRenderMesh) { DestroyImmediate(_lastRenderMesh); _lastRenderMesh = null; }
//         if (_lastColliderMesh) { DestroyImmediate(_lastColliderMesh); _lastColliderMesh = null; }
// #endif
//         mf.sharedMesh = null;

//         // 1) Collect reserved rectangles in THIS object's local XZ space
//         var reserved = new List<RectXZ>();
//         CollectReservedRects(reserved);

//         // 2) Build grid lines from area bounds at cellSize step
//         float minX = -areaSize.x * 0.5f;
//         float maxX = +areaSize.x * 0.5f;
//         float minZ = -areaSize.y * 0.5f;
//         float maxZ = +areaSize.y * 0.5f;

//         var xs = new List<float>();
//         var zs = new List<float>();

//         for (float x = minX; x < maxX - 1e-5f; x += cellSize) xs.Add(x);
//         xs.Add(maxX);
//         for (float z = minZ; z < maxZ - 1e-5f; z += cellSize) zs.Add(z);
//         zs.Add(maxZ);

//         // 3) Emit quads for cells not fully inside any reserved rect (render mesh).
//         //    Collider uses a gentler/smarter rule.
//         var mbRender = new SimpleMeshBuilder();
//         var mbCollider = addMeshCollider ? new SimpleMeshBuilder() : null;

//         for (int xi = 0; xi < xs.Count - 1; xi++)
//         {
//             float x0 = xs[xi];
//             float x1 = xs[xi + 1];
//             for (int zi = 0; zi < zs.Count - 1; zi++)
//             {
//                 float z0 = zs[zi];
//                 float z1 = zs[zi + 1];

//                 if (x1 - x0 <= 1e-4f || z1 - z0 <= 1e-4f) continue;

//                 RectXZ cell = new RectXZ(x0, x1, z0, z1);

//                 bool fullyInside = IsFullyInsideAny(cell, reserved);
//                 if (!fullyInside)
//                 {
//                     // RENDER: keep
//                     AddQuad(mbRender, x0, z0, x1, z1);

//                     // COLLIDER: keep unless coverage over threshold (or center inside, depending on settings)
//                     if (mbCollider != null)
//                     {
//                         bool dropForCollider = false;

//                         if (colliderRemoveIntersections)
//                         {
//                             if (colliderUseCoverage)
//                             {
//                                 float coverage = EstimateCoverage(cell, reserved, colliderSamplesPerAxis, colliderInflate);
//                                 dropForCollider = coverage >= colliderInsideThreshold;
//                             }
//                             else
//                             {
//                                 // fallback: center test
//                                 dropForCollider = CenterInsideAny(cell, reserved, inflate: colliderInflate);
//                             }
//                         }

//                         if (!dropForCollider)
//                             AddQuad(mbCollider, x0, z0, x1, z1);
//                     }
//                 }
//             }
//         }

//         // 4) Assign render mesh + material
//         var renderMesh = mbRender.ToMesh(false);
//         mf.sharedMesh = renderMesh;
//         _lastRenderMesh = renderMesh;

//         // 5) MeshCollider (optional)
//         if (addMeshCollider)
//         {
//             var collMesh = (mbCollider != null) ? mbCollider.ToMesh(false) : renderMesh;
//             _lastColliderMesh = (mbCollider != null) ? collMesh : null; // track only if it's separate

//             RefreshCollider(collMesh, physicsMaterial, colliderConvex);
//         }
//         else
//         {
//             var mc = GetComponent<MeshCollider>();
//             if (mc) DestroyImmediate(mc);
//         }

//         gameObject.isStatic = true;
//     }

//     // ---------- Reserved collection ----------
//     void CollectReservedRects(List<RectXZ> outRects)
//     {
//         outRects.Clear();

//         // From workspace (if provided)
//         if (workspace)
//         {
//             // Roads -> full cross-section rectangles
//             if (carveUnderRoads && workspace.roadBands != null)
//             {
//                 foreach (var r in workspace.roadBands)
//                 {
//                     if (r.size.x <= 0 || r.size.y <= 0) continue;
//                     bool longX = r.size.x >= r.size.y;

//                     float halfL = (longX ? r.size.x : r.size.y) * 0.5f;
//                     float halfCross = 0.5f * (r.carriagewayWidth + 2f * (r.gutterWidth + r.curbWidth + r.footpathWidth));
//                     float halfShort = halfCross + Mathf.Max(0f, roadsMargin);

//                     // Road center in WORLD, then to THIS local
//                     Vector3 worldC = workspace.LocalXZToWorld(r.center, workspace.areaY);
//                     Vector3 localC = transform.InverseTransformPoint(worldC);
//                     float cx = localC.x, cz = localC.z;

//                     if (longX)
//                         outRects.Add(new RectXZ(cx - halfL, cx + halfL, cz - halfShort, cz + halfShort));
//                     else
//                         outRects.Add(new RectXZ(cx - halfShort, cx + halfShort, cz - halfL, cz + halfL));
//                 }
//             }

//             // Buildings -> plot rectangles (optional)
//             if (carveUnderBuildings && workspace.buildingPlots != null)
//             {
//                 foreach (var p in workspace.buildingPlots)
//                 {
//                     if (p.size.x <= 0 || p.size.y <= 0) continue;
//                     Vector3 worldC = workspace.LocalXZToWorld(p.center, workspace.areaY);
//                     Vector3 localC = transform.InverseTransformPoint(worldC);
//                     float halfX = p.size.x * 0.5f + Mathf.Max(0f, buildingsMargin);
//                     float halfZ = p.size.y * 0.5f + Mathf.Max(0f, buildingsMargin);
//                     outRects.Add(new RectXZ(localC.x - halfX, localC.x + halfX,
//                                             localC.z - halfZ, localC.z + halfZ));
//                 }
//             }

//             // Intersections -> area rectangles (optional)
//             if (carveUnderIntersections && workspace.intersections != null)
//             {
//                 foreach (var ia in workspace.intersections)
//                 {
//                     if (ia.size.x <= 0 || ia.size.y <= 0) continue;
//                     Vector3 worldC = workspace.LocalXZToWorld(ia.center, workspace.areaY);
//                     Vector3 localC = transform.InverseTransformPoint(worldC);
//                     float halfX = ia.size.x * 0.5f + Mathf.Max(0f, intersectionsMargin);
//                     float halfZ = ia.size.y * 0.5f + Mathf.Max(0f, intersectionsMargin);
//                     outRects.Add(new RectXZ(localC.x - halfX, localC.x + halfX,
//                                             localC.z - halfZ, localC.z + halfZ));
//                 }
//             }
//         }

//         // Manual rectangles (already in this object's local space)
//         if (manualReserved != null)
//         {
//             foreach (var m in manualReserved)
//             {
//                 if (m.size.x <= 0 || m.size.y <= 0) continue;
//                 float halfX = m.size.x * 0.5f;
//                 float halfZ = m.size.y * 0.5f;
//                 outRects.Add(new RectXZ(m.center.x - halfX, m.center.x + halfX,
//                                         m.center.y - halfZ, m.center.y + halfZ));
//             }
//         }

//         // Clip to area bounds
//         float minX = -areaSize.x * 0.5f, maxX = +areaSize.x * 0.5f;
//         float minZ = -areaSize.y * 0.5f, maxZ = +areaSize.y * 0.5f;
//         for (int i = outRects.Count - 1; i >= 0; i--)
//         {
//             var r = outRects[i];
//             r.minX = Mathf.Clamp(r.minX, minX, maxX);
//             r.maxX = Mathf.Clamp(r.maxX, minX, maxX);
//             r.minZ = Mathf.Clamp(r.minZ, minZ, maxZ);
//             r.maxZ = Mathf.Clamp(r.maxZ, minZ, maxZ);
//             if (r.maxX <= r.minX || r.maxZ <= r.minZ) outRects.RemoveAt(i);
//             else outRects[i] = r;
//         }
//     }

//     // ---------- Emit a single ground quad (LOCAL space) ----------
//     void AddQuad(SimpleMeshBuilder mb, float x0, float z0, float x1, float z1)
//     {
//         // Vertices in LOCAL SPACE; object sits at areaY via transform.localPosition (set in Build()).
//         Vector3 bl = new Vector3(x0, 0f, z0);
//         Vector3 br = new Vector3(x1, 0f, z0);
//         Vector3 tl = new Vector3(x0, 0f, z1);
//         Vector3 tr = new Vector3(x1, 0f, z1);

//         int v0 = mb.vertices.Count;
//         mb.vertices.Add(bl);
//         mb.vertices.Add(br);
//         mb.vertices.Add(tl);
//         mb.vertices.Add(tr);

//         mb.normals.Add(Vector3.up); mb.normals.Add(Vector3.up);
//         mb.normals.Add(Vector3.up); mb.normals.Add(Vector3.up);

//         // UVs in meters: use LOCAL XZ as meter coords
//         mb.uvs.Add(UVMapping.FromMeters(x0, z0, uvMeters.x, uvMeters.y, 0, 0));
//         mb.uvs.Add(UVMapping.FromMeters(x1, z0, uvMeters.x, uvMeters.y, 0, 0));
//         mb.uvs.Add(UVMapping.FromMeters(x0, z1, uvMeters.x, uvMeters.y, 0, 0));
//         mb.uvs.Add(UVMapping.FromMeters(x1, z1, uvMeters.x, uvMeters.y, 0, 0));

//         mb.AddQuad(v0 + 0, v0 + 1, v0 + 2, v0 + 3);
//     }

//     // ---------- MeshCollider refresh ----------
//     void RefreshCollider(Mesh colliderMesh, PhysicMaterial material = null, bool convex = false)
//     {
//         var mc = GetComponent<MeshCollider>();
//         if (!mc) mc = gameObject.AddComponent<MeshCollider>();

//         // Force recook even if same instance
//         mc.sharedMesh = null;
//         mc.sharedMesh = colliderMesh;

// #if UNITY_2020_2_OR_NEWER
//         mc.cookingOptions = MeshColliderCookingOptions.EnableMeshCleaning |
//                             MeshColliderCookingOptions.WeldColocatedVertices |
//                             MeshColliderCookingOptions.UseFastMidphase;
// #endif
//         mc.convex = convex;
//         if (material) mc.sharedMaterial = material;

//         // Optional: pre-bake for faster playmode loads
// #if UNITY_EDITOR
//         try { Physics.BakeMesh(colliderMesh.GetInstanceID(), convex); }
//         catch { /* ignore if unsupported */ }
// #endif
//     }

//     // ---------- Geometry tests ----------
//     static bool IsFullyInsideAny(RectXZ cell, List<RectXZ> rects)
//     {
//         for (int i = 0; i < rects.Count; i++)
//             if (FullyInside(cell, rects[i])) return true;
//         return false;
//     }

//     static bool CenterInsideAny(RectXZ cell, List<RectXZ> rects, float inflate = 0f)
//     {
//         float cx = 0.5f * (cell.minX + cell.maxX);
//         float cz = 0.5f * (cell.minZ + cell.maxZ);
//         for (int i = 0; i < rects.Count; i++)
//         {
//             var r = rects[i];
//             if (cx >= r.minX - inflate && cx <= r.maxX + inflate &&
//                 cz >= r.minZ - inflate && cz <= r.maxZ + inflate)
//                 return true;
//         }
//         return false;
//     }

//     static bool FullyInside(RectXZ a, RectXZ b)
//     {
//         return a.minX >= b.minX && a.maxX <= b.maxX &&
//                a.minZ >= b.minZ && a.maxZ <= b.maxZ;
//     }

//     static bool Overlaps(RectXZ a, RectXZ b)
//     {
//         return (a.minX < b.maxX) && (a.maxX > b.minX) &&
//                (a.minZ < b.maxZ) && (a.maxZ > b.minZ);
//     }

//     static bool PointInsideAny(float x, float z, List<RectXZ> rects, float inflate)
//     {
//         for (int i = 0; i < rects.Count; i++)
//         {
//             var r = rects[i];
//             if (x >= r.minX - inflate && x <= r.maxX + inflate &&
//                 z >= r.minZ - inflate && z <= r.maxZ + inflate)
//                 return true;
//         }
//         return false;
//     }

//     static float EstimateCoverage(RectXZ cell, List<RectXZ> rects, int samplesPerAxis, float inflate)
//     {
//         samplesPerAxis = Mathf.Clamp(samplesPerAxis, 1, 5);
//         if (rects == null || rects.Count == 0) return 0f;

//         int inside = 0, total = samplesPerAxis * samplesPerAxis;

//         // jitter samples to cell centers of subcells
//         for (int sx = 0; sx < samplesPerAxis; sx++)
//         {
//             float tx = (sx + 0.5f) / samplesPerAxis;
//             float x  = Mathf.Lerp(cell.minX, cell.maxX, tx);

//             for (int sz = 0; sz < samplesPerAxis; sz++)
//             {
//                 float tz = (sz + 0.5f) / samplesPerAxis;
//                 float z  = Mathf.Lerp(cell.minZ, cell.maxZ, tz);

//                 if (PointInsideAny(x, z, rects, inflate))
//                     inside++;
//             }
//         }
//         return (float)inside / total;
//     }
// }
