// // ProcRoad.cs  (footpath @ Y=0; carriageway set-in; crown dip; meters-based UVs + MeshCollider)
// // Requires: ProcGen.SimpleMeshBuilder and ProcGen.UVMapping
// using UnityEngine;
// using System.Collections.Generic;
// using ProcGen;

// [DisallowMultipleComponent]
// [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
// public class ProcRoad : MonoBehaviour
// {
//     [Header("Path")]
//     public List<Transform> path = new List<Transform>(); // ordered points in scene
//     public bool loop;

//     [Header("Widths (meters)")]
//     [Min(0.1f)] public float carriagewayWidth = 6.0f;  // total lane width (edge to edge)
//     [Min(0.0f)] public float gutterWidth      = 0.5f;  // band outside lane
//     [Min(0.0f)] public float curbWidth        = 0.2f;  // small horizontal top of kerb before footpath
//     [Min(0.1f)] public float footpathWidth    = 2.0f;  // sidewalk

//     [Header("Heights (meters)")]
//     [Min(0.0f)] public float carriagewaySetIn = 0.04f; // lane edges sit this far below footpath plane
//     [Min(0.0f)] public float gutterDepth      = 0.05f; // gutter bottom below footpath plane
//     [Min(0.0f)] public float curbHeight       = 0.15f; // retained for shaping (footpath stays at 0)
//     [Range(0f, 0.08f)] public float camberSlope = 0.02f; // additional drop per meter from lane edges to center

//     [Header("UV Mapping (meters per tile)")]
//     public float metersPerTileU = 1f; // across the width
//     public float metersPerTileV = 1f; // along the length
//     public float uOffset = 0f;
//     public float vOffset = 0f;

//     [Header("Rendering (optional)")]
//     public Material material;

//     [Header("Collider")]
//     public bool useMeshCollider = true;
//     public bool colliderConvex  = false;
//     public PhysicMaterial physicsMaterial;

//     void Awake()      { Build(); }
//     void OnValidate() { if (Application.isPlaying) Build(); }

//     struct SecPt { public float x, y; public SecPt(float X, float Y){ x=X; y=Y; } }

//     public void Build()
//     {
//         var mf = GetComponent<MeshFilter>();
//         mf.sharedMesh = null;

//         if (path == null || path.Count < 2)
//         {
//             EnsureMeshCollider(null);
//             return;
//         }

//         // Collect world points
//         var pts = new List<Vector3>(path.Count);
//         foreach (var t in path) if (t) pts.Add(t.position);
//         if (pts.Count < 2)
//         {
//             EnsureMeshCollider(null);
//             return;
//         }

//         // ---- Cross-section LEFT -> RIGHT (x across, y up). Footpath top = 0 ----
//         float halfCarriage = Mathf.Max(0.001f, carriagewayWidth * 0.5f);

//         // Small bevel right before the gutter to avoid razor-sharp normal
//         float bevelDown = Mathf.Clamp(Mathf.Min(gutterDepth, curbHeight) * 0.5f, 0.005f, 0.03f);

//         // Build section: include a CENTER vertex so camber can actually dip
//         var section = new List<SecPt>();

//         // LEFT side: outer sidewalk -> inner sidewalk/kerb top -> bevel -> gutter bottom -> lane edge
//         {
//             float xSidewalkOuter = -(halfCarriage + gutterWidth + curbWidth + footpathWidth);

//             section.Add(new SecPt(xSidewalkOuter, 0f));                           // outer sidewalk edge (0)
//             section.Add(new SecPt(-(halfCarriage + gutterWidth + curbWidth), 0f));// inner sidewalk edge / kerb top (0)
//             section.Add(new SecPt(-(halfCarriage + gutterWidth), -bevelDown));    // small bevel down before gutter
//             section.Add(new SecPt(-(halfCarriage + gutterWidth), -gutterDepth));  // gutter bottom (negative)
//             section.Add(new SecPt(-halfCarriage, 0f));                            // lane edge (baseline handled later)
//         }

//         // CENTER (needed for crown dip via camber)
//         section.Add(new SecPt(0f, 0f));

//         // RIGHT side: lane edge -> gutter bottom -> bevel -> inner sidewalk -> outer sidewalk
//         {
//             float xSidewalkOuter = +(halfCarriage + gutterWidth + curbWidth + footpathWidth);

//             section.Add(new SecPt(+halfCarriage, 0f));                            // lane edge (baseline handled later)
//             section.Add(new SecPt(+(halfCarriage + gutterWidth), -gutterDepth));  // gutter bottom
//             section.Add(new SecPt(+(halfCarriage + gutterWidth), -bevelDown));    // bevel back up
//             section.Add(new SecPt(+(halfCarriage + gutterWidth + curbWidth), 0f));// inner sidewalk / kerb top (0)
//             section.Add(new SecPt(xSidewalkOuter, 0f));                           // outer sidewalk edge (0)
//         }

//         int secCount = section.Count;

//         // Lateral distances for U (in METERS)
//         var xs = new List<float>(secCount);
//         for (int i = 0; i < secCount; i++) xs.Add(section[i].x);
//         float[] lateralDist; float totalWidthMeters;
//         UVMapping.ComputeLateralDistances(xs, out lateralDist, out totalWidthMeters);

//         var mb = new SimpleMeshBuilder();

//         Vector3 up = Vector3.up;
//         float vAcc = 0f; // along-length distance in meters
//         bool firstStation = true;

//         // Iterate stations along the path
//         for (int i = 0; i < pts.Count - 1 + (loop ? 1 : 0); i++)
//         {
//             Vector3 a = pts[i % pts.Count];
//             Vector3 b = pts[(i + 1) % pts.Count];

//             Vector3 t = b - a; // tangent
//             float segLen = t.magnitude;
//             if (segLen < 1e-4f) continue;
//             t /= segLen;
//             Vector3 s = Vector3.Normalize(Vector3.Cross(up, t)); // right direction

//             // Create this station’s ring
//             int ringStart = mb.vertices.Count;

//             for (int k = 0; k < secCount; k++)
//             {
//                 float x = section[k].x;

//                 // Baseline set-in: only the carriageway (|x| <= halfCarriage) sits below 0
//                 float baseSetIn = (Mathf.Abs(x) <= halfCarriage) ? -carriagewaySetIn : 0f;

//                 // Camber drop from lane edges towards center (0 at |x|=halfCarriage, max at x=0)
//                 float dxInsideLane = Mathf.Max(0f, halfCarriage - Mathf.Abs(x));
//                 float yCamber = -camberSlope * dxInsideLane;

//                 // Section local offset (bevels, gutters, sidewalks already absolute relative to 0)
//                 float y = baseSetIn + yCamber + section[k].y;

//                 Vector3 p = a + s * x + up * y;
//                 mb.vertices.Add(p);
//                 mb.normals.Add(up); // simple up normals

//                 // UVs in METERS -> UVs
//                 float u = UVMapping.ToU(lateralDist[k], metersPerTileU, uOffset);
//                 float v = UVMapping.ToV(vAcc,           metersPerTileV, vOffset);
//                 mb.uvs.Add(new Vector2(u, v));
//             }

//             // Stitch to previous ring
//             if (!firstStation)
//             {
//                 int prevRingStart = ringStart - secCount;
//                 for (int k = 0; k < secCount - 1; k++)
//                 {
//                     int i0 = prevRingStart + k;
//                     int i1 = prevRingStart + k + 1;
//                     int i2 = ringStart     + k;
//                     int i3 = ringStart     + k + 1;
//                     mb.AddQuad(i0, i1, i2, i3);
//                 }
//             }

//             firstStation = false;
//             vAcc += segLen;

//             // Add final ring for open paths
//             if (!loop && i == pts.Count - 2)
//             {
//                 int ring2Start = mb.vertices.Count;

//                 for (int k = 0; k < secCount; k++)
//                 {
//                     float x = section[k].x;
//                     float baseSetIn = (Mathf.Abs(x) <= halfCarriage) ? -carriagewaySetIn : 0f;
//                     float dxInsideLane = Mathf.Max(0f, halfCarriage - Mathf.Abs(x));
//                     float yCamber = -camberSlope * dxInsideLane;
//                     float y = baseSetIn + yCamber + section[k].y;

//                     Vector3 p = b + s * x + up * y;
//                     mb.vertices.Add(p);
//                     mb.normals.Add(up);

//                     float u = UVMapping.ToU(lateralDist[k], metersPerTileU, uOffset);
//                     float v = UVMapping.ToV(vAcc,           metersPerTileV, vOffset);
//                     mb.uvs.Add(new Vector2(u, v));
//                 }

//                 // Stitch last segment
//                 for (int k = 0; k < secCount - 1; k++)
//                 {
//                     int i0 = ring2Start - secCount + k;
//                     int i1 = ring2Start - secCount + k + 1;
//                     int i2 = ring2Start + k;
//                     int i3 = ring2Start + k + 1;
//                     mb.AddQuad(i0, i1, i2, i3);
//                 }
//             }
//         }

//         var mesh = mb.ToMesh(false);
//         mf.sharedMesh = mesh;

//         if (material != null)
//             GetComponent<MeshRenderer>().sharedMaterial = material;

//         EnsureMeshCollider(mesh);
//     }

//     void EnsureMeshCollider(Mesh mesh)
//     {
//         var mc = GetComponent<MeshCollider>();

//         if (!useMeshCollider)
//         {
//             if (mc) DestroyImmediate(mc);
//             return;
//         }

//         if (!mc) mc = gameObject.AddComponent<MeshCollider>();

//         mc.sharedMesh = null; // force recook
//         mc.sharedMesh = mesh;

//         mc.convex = colliderConvex;

// #if UNITY_2020_2_OR_NEWER
//         mc.cookingOptions = MeshColliderCookingOptions.EnableMeshCleaning |
//                             MeshColliderCookingOptions.WeldColocatedVertices |
//                             MeshColliderCookingOptions.UseFastMidphase;
// #endif
//         if (physicsMaterial) mc.sharedMaterial = physicsMaterial;
//     }
// }
