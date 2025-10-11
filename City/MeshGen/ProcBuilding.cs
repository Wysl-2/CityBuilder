// // ProcBuilding.cs  — full replacement (mesh collider + flat/gable roofs; roof faces UP; meter-based UVs)
// // Requires: ProcGen.SimpleMeshBuilder and ProcGen.UVMapping (your existing utils)
// using UnityEngine;
// using System.Collections.Generic;
// using ProcGen;

// [DisallowMultipleComponent]
// [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
// public class ProcBuilding : MonoBehaviour
// {
//     [Tooltip("Footprint in LOCAL XZ, no self-intersections. CW or CCW is fine.")]
//     public List<Vector3> footprint = new List<Vector3> {
//         new Vector3(-5,0,-4),
//         new Vector3( 5,0,-4),
//         new Vector3( 5,0, 4),
//         new Vector3(-5,0, 4),
//     };

//     [Header("Height (meters)")]
//     public bool  randomizeHeight = true;
//     public float heightMin = 6f;
//     public float heightMax = 18f;
//     public float fixedHeight = 10f;

//     [Header("Walls UV (meters per tile)")]
//     // Walls: U = meters along perimeter, V = meters up
//     public float  wallMetersPerTileU = 1f;
//     public float  wallMetersPerTileV = 1f;
//     public Vector2 wallUVOffset = Vector2.zero;

//     [Header("Roof")]
//     public RoofStyle roofStyle = RoofStyle.Flat; // Flat, GableAlongX, GableAlongZ
//     public float roofHeight = 2.0f;              // ridge rise for gables
//     public float eaveOverhang = 0.3f;            // horizontal overhang for gables
//     public bool  fillGableEnds = true;
//     [Tooltip("Tiny lift to avoid z-fighting with wall top (e.g. 0.001).")]
//     public float roofLiftEpsilon = 0.0f;
//     [Tooltip("Duplicate reversed faces (or use a two-sided shader).")]
//     public bool  roofDoubleSided = false;

//     [Header("Roof UV (meters per tile)")]
//     // Flat: planar XZ. Gable: U along ridge, V along slope length.
//     public float  roofMetersPerTileU = 1f;
//     public float  roofMetersPerTileV = 1f;
//     public Vector2 roofUVOffset = Vector2.zero;

//     [Header("Rendering (optional)")]
//     public Material material;  // assign to avoid pink if nothing else sets it

//     [Header("Collider")]
//     public bool useMeshCollider = true;   // add/update MeshCollider from generated mesh
//     public bool colliderConvex  = false;  // keep false for static buildings
//     public PhysicMaterial physicsMaterial;

//     public enum RoofStyle { Flat, GableAlongX, GableAlongZ }

//     void Awake()      { Build(); }
//     void OnValidate() { if (Application.isPlaying) Build(); }

//     public void Build()
//     {
//         var mf = GetComponent<MeshFilter>();
//         mf.sharedMesh = null;

//         if (footprint == null || footprint.Count < 3)
//         {
//             EnsureMeshCollider(null);
//             return;
//         }

//         float h = randomizeHeight ? Random.Range(heightMin, heightMax) : fixedHeight;
//         int n = footprint.Count;

//         var mb = new SimpleMeshBuilder();

//         // -------- Walls --------
//         float uAcc = 0f; // perimeter distance in meters
//         for (int i = 0; i < n; i++)
//         {
//             Vector3 a = footprint[i];
//             Vector3 b = footprint[(i + 1) % n];
//             Vector3 edge = b - a;
//             float edgeLen = edge.magnitude;
//             if (edgeLen < 1e-5f) continue;

//             // Approx outward normal (Y up)
//             Vector3 na = Vector3.Normalize(Vector3.Cross(Vector3.up, edge));

//             int v0 = mb.vertices.Count;
//             // lower ring
//             mb.vertices.Add(new Vector3(a.x, 0f, a.z));
//             mb.vertices.Add(new Vector3(b.x, 0f, b.z));
//             // upper ring
//             mb.vertices.Add(new Vector3(a.x, h, a.z));
//             mb.vertices.Add(new Vector3(b.x, h, b.z));

//             // Wall UVs in meters
//             mb.uvs.Add(UVMapping.FromMeters(uAcc,               0f, wallMetersPerTileU, wallMetersPerTileV, wallUVOffset.x, wallUVOffset.y));
//             mb.uvs.Add(UVMapping.FromMeters(uAcc + edgeLen,     0f, wallMetersPerTileU, wallMetersPerTileV, wallUVOffset.x, wallUVOffset.y));
//             mb.uvs.Add(UVMapping.FromMeters(uAcc,               h,  wallMetersPerTileU, wallMetersPerTileV, wallUVOffset.x, wallUVOffset.y));
//             mb.uvs.Add(UVMapping.FromMeters(uAcc + edgeLen,     h,  wallMetersPerTileU, wallMetersPerTileV, wallUVOffset.x, wallUVOffset.y));

//             // Flat shading for crisp corners
//             mb.normals.Add(na); mb.normals.Add(na); mb.normals.Add(na); mb.normals.Add(na);

//             // Two tris for the wall quad (MeshBuilder uses CW)
//             mb.AddQuad(v0 + 0, v0 + 1, v0 + 2, v0 + 3);

//             uAcc += edgeLen;
//         }

//         // -------- Roof --------
//         float roofY = h + Mathf.Max(0f, roofLiftEpsilon);

//         // Building center in XZ (for outward-facing gable end triangles)
//         Vector3 center = ComputeXZCenter(footprint); center.y = roofY;

//         if ((roofStyle == RoofStyle.GableAlongX || roofStyle == RoofStyle.GableAlongZ)
//             && IsAxisAlignedRectangle(footprint, out float minX, out float maxX, out float minZ, out float maxZ))
//         {
//             if (roofStyle == RoofStyle.GableAlongX) BuildGableAlongX(mb, roofY, minX, maxX, minZ, maxZ, center);
//             else                                    BuildGableAlongZ(mb, roofY, minX, maxX, minZ, maxZ, center);
//         }
//         else
//         {
//             AddFlatRoofFacingUp(mb, roofY); // works for any simple polygon
//         }

//         // -------- Assign --------
//         var mesh = mb.ToMesh(false);
//         mf.sharedMesh = mesh;

//         var mr = GetComponent<MeshRenderer>();
//         if (material != null) mr.sharedMaterial = material;
//         else                  mr.sharedMaterial = new Material(Shader.Find("Standard"));

//         // -------- MeshCollider --------
//         EnsureMeshCollider(mesh);
//     }

//     // =====================================================================
//     // Flat roof (any polygon). Ensures triangles face UP in Unity (CW).
//     // =====================================================================
//     void AddFlatRoofFacingUp(SimpleMeshBuilder mb, float y)
//     {
//         int n = footprint.Count;

//         // Build 2D poly and a vertex order that's CCW (for stable ear clipping)
//         var poly2 = new List<Vector2>(n);
//         for (int i = 0; i < n; i++) poly2.Add(new Vector2(footprint[i].x, footprint[i].z));

//         // Create an ordered copy and indices: ensure CCW
//         List<int> order = new List<int>(n);
//         if (IsCCW(poly2)) { for (int i = 0; i < n; i++) order.Add(i); }
//         else              { for (int i = n - 1; i >= 0; i--) order.Add(i); }

//         var orderedPoly = new List<Vector2>(n);
//         for (int i = 0; i < n; i++) orderedPoly.Add(new Vector2(footprint[order[i]].x, footprint[order[i]].z));

//         // Ear-clip returns CCW triangles in this "ordered" index space.
//         var tris = EarClipTriangulateCCW(orderedPoly);

//         // Add roof vertices in the same "ordered" order
//         int baseIdx = mb.vertices.Count;
//         for (int i = 0; i < n; i++)
//         {
//             Vector3 p = footprint[order[i]];
//             var vtx = new Vector3(p.x, y, p.z);
//             mb.vertices.Add(vtx);
//             mb.normals.Add(Vector3.up);

//             // Planar meters UV
//             mb.uvs.Add(UVMapping.FromMeters(
//                 p.x, p.z,
//                 roofMetersPerTileU, roofMetersPerTileV,
//                 roofUVOffset.x, roofUVOffset.y
//             ));
//         }

//         // Emit CW triangles so the roof faces UP in Unity
//         foreach (var t in tris)
//         {
//             // CCW -> CW: (x, z, y)
//             mb.triangles.Add(baseIdx + t.x);
//             mb.triangles.Add(baseIdx + t.z);
//             mb.triangles.Add(baseIdx + t.y);

//             if (roofDoubleSided)
//             {
//                 // Back face (flip)
//                 mb.triangles.Add(baseIdx + t.y);
//                 mb.triangles.Add(baseIdx + t.z);
//                 mb.triangles.Add(baseIdx + t.x);
//             }
//         }
//     }

//     // =====================================================================
//     // Gable X: ridge along X (slopes in ±Z). Rectangle footprints only.
//     // =====================================================================
//     void BuildGableAlongX(SimpleMeshBuilder mb, float wallTopY, float minX, float maxX, float minZ, float maxZ, Vector3 center)
//     {
//         float cxZ      = 0.5f * (minZ + maxZ);
//         float halfSpan = 0.5f * (maxZ - minZ);
//         float e        = Mathf.Max(0f, eaveOverhang);
//         float ridgeY   = wallTopY + Mathf.Max(0f, roofHeight);

//         float zEaveL = minZ - e, zEaveR = maxZ + e, zRidge = cxZ;

//         float slopeLen = Mathf.Sqrt(halfSpan * halfSpan + Mathf.Max(roofHeight, 0f) * Mathf.Max(roofHeight, 0f));
//         float vEdge    = UVMapping.ToV(slopeLen, roofMetersPerTileV, roofUVOffset.y);
//         float uMin     = UVMapping.ToU(minX - e, roofMetersPerTileU, roofUVOffset.x);
//         float uMax     = UVMapping.ToU(maxX + e, roofMetersPerTileU, roofUVOffset.x);

//         // Left slope (toward minZ)
//         AddQuadWithUVNormals(mb,
//             new Vector3(minX - e, wallTopY, zEaveL),
//             new Vector3(maxX + e, wallTopY, zEaveL),
//             new Vector3(minX - e, ridgeY,   zRidge),
//             new Vector3(maxX + e, ridgeY,   zRidge),
//             new Vector2(uMin, vEdge), new Vector2(uMax, vEdge),
//             new Vector2(uMin, 0f),    new Vector2(uMax, 0f)
//         );

//         // Right slope (toward maxZ)
//         AddQuadWithUVNormals(mb,
//             new Vector3(minX - e, ridgeY,   zRidge),
//             new Vector3(maxX + e, ridgeY,   zRidge),
//             new Vector3(minX - e, wallTopY, zEaveR),
//             new Vector3(maxX + e, wallTopY, zEaveR),
//             new Vector2(uMin, 0f),   new Vector2(uMax, 0f),
//             new Vector2(uMin, vEdge), new Vector2(uMax, vEdge)
//         );

//         if (fillGableEnds)
//         {
//             // Front (minX) — ensure OUTWARD CW using building center
//             AddTriCWOutward(mb,
//                 new Vector3(minX - e, wallTopY, zEaveL),
//                 new Vector3(minX - e, ridgeY,   zRidge),
//                 new Vector3(minX - e, wallTopY, zEaveR),
//                 center,
//                 new Vector2(UVMapping.ToU(zEaveL - minZ, roofMetersPerTileU, roofUVOffset.x), 0f),
//                 new Vector2(UVMapping.ToU(zRidge  - minZ, roofMetersPerTileU, roofUVOffset.x), UVMapping.ToV(ridgeY - wallTopY, roofMetersPerTileV, roofUVOffset.y)),
//                 new Vector2(UVMapping.ToU(zEaveR - minZ, roofMetersPerTileU, roofUVOffset.x), 0f)
//             );

//             // Back (maxX)
//             AddTriCWOutward(mb,
//                 new Vector3(maxX + e, wallTopY, zEaveR),
//                 new Vector3(maxX + e, ridgeY,   zRidge),
//                 new Vector3(maxX + e, wallTopY, zEaveL),
//                 center,
//                 new Vector2(UVMapping.ToU(zEaveR - minZ, roofMetersPerTileU, roofUVOffset.x), 0f),
//                 new Vector2(UVMapping.ToU(zRidge  - minZ, roofMetersPerTileU, roofUVOffset.x), UVMapping.ToV(ridgeY - wallTopY, roofMetersPerTileV, roofUVOffset.y)),
//                 new Vector2(UVMapping.ToU(zEaveL - minZ, roofMetersPerTileU, roofUVOffset.x), 0f)
//             );
//         }

//         if (roofDoubleSided) MakeLastFacesDoubleSided(mb);
//     }

//     // =====================================================================
//     // Gable Z: ridge along Z (slopes in ±X). Rectangle footprints only.
//     // =====================================================================
//     void BuildGableAlongZ(SimpleMeshBuilder mb, float wallTopY, float minX, float maxX, float minZ, float maxZ, Vector3 center)
//     {
//         float cxX      = 0.5f * (minX + maxX);
//         float halfSpan = 0.5f * (maxX - minX);
//         float e        = Mathf.Max(0f, eaveOverhang);
//         float ridgeY   = wallTopY + Mathf.Max(0f, roofHeight);

//         float xEaveL = minX - e, xEaveR = maxX + e, xRidge = cxX;

//         float slopeLen = Mathf.Sqrt(halfSpan * halfSpan + Mathf.Max(roofHeight, 0f) * Mathf.Max(roofHeight, 0f));
//         float vEdge    = UVMapping.ToV(slopeLen, roofMetersPerTileV, roofUVOffset.y);
//         float uMin     = UVMapping.ToU(minZ - e, roofMetersPerTileU, roofUVOffset.x);
//         float uMax     = UVMapping.ToU(maxZ + e, roofMetersPerTileU, roofUVOffset.x);

//         // Left slope (toward minX)
//         AddQuadWithUVNormals(mb,
//             new Vector3(xEaveL, wallTopY, minZ - e),
//             new Vector3(xRidge,  ridgeY,  minZ - e),
//             new Vector3(xEaveL, wallTopY, maxZ + e),
//             new Vector3(xRidge,  ridgeY,  maxZ + e),
//             new Vector2(uMin, vEdge), new Vector2(uMin, 0f),
//             new Vector2(uMax, vEdge), new Vector2(uMax, 0f)
//         );

//         // Right slope (toward maxX)
//         AddQuadWithUVNormals(mb,
//             new Vector3(xRidge,  ridgeY,  minZ - e),
//             new Vector3(xEaveR, wallTopY, minZ - e),
//             new Vector3(xRidge,  ridgeY,  maxZ + e),
//             new Vector3(xEaveR, wallTopY, maxZ + e),
//             new Vector2(uMin, 0f),  new Vector2(uMin, vEdge),
//             new Vector2(uMax, 0f),  new Vector2(uMax, vEdge)
//         );

//         if (fillGableEnds)
//         {
//             // MinZ gable
//             AddTriCWOutward(mb,
//                 new Vector3(xEaveL, wallTopY, minZ - e),
//                 new Vector3(xRidge,  ridgeY,  minZ - e),
//                 new Vector3(xEaveR, wallTopY, minZ - e),
//                 center,
//                 new Vector2(UVMapping.ToU(xEaveL - minX, roofMetersPerTileU, roofUVOffset.x), 0f),
//                 new Vector2(UVMapping.ToU(xRidge  - minX, roofMetersPerTileU, roofUVOffset.x), UVMapping.ToV(ridgeY - wallTopY, roofMetersPerTileV, roofUVOffset.y)),
//                 new Vector2(UVMapping.ToU(xEaveR - minX, roofMetersPerTileU, roofUVOffset.x), 0f)
//             );

//             // MaxZ gable
//             AddTriCWOutward(mb,
//                 new Vector3(xEaveR, wallTopY, maxZ + e),
//                 new Vector3(xRidge,  ridgeY,  maxZ + e),
//                 new Vector3(xEaveL, wallTopY, maxZ + e),
//                 center,
//                 new Vector2(UVMapping.ToU(xEaveR - minX, roofMetersPerTileU, roofUVOffset.x), 0f),
//                 new Vector2(UVMapping.ToU(xRidge  - minX, roofMetersPerTileU, roofUVOffset.x), UVMapping.ToV(ridgeY - wallTopY, roofMetersPerTileV, roofUVOffset.y)),
//                 new Vector2(UVMapping.ToU(xEaveL - minX, roofMetersPerTileU, roofUVOffset.x), 0f)
//             );
//         }

//         if (roofDoubleSided) MakeLastFacesDoubleSided(mb);
//     }

//     // =====================================================================
//     // Helpers
//     // =====================================================================
//     static Vector3 ComputeXZCenter(List<Vector3> pts)
//     {
//         Vector3 c = Vector3.zero;
//         if (pts == null || pts.Count == 0) return c;
//         for (int i = 0; i < pts.Count; i++) { c.x += pts[i].x; c.z += pts[i].z; }
//         c.x /= pts.Count; c.z /= pts.Count; return c;
//     }

//     static bool IsAxisAlignedRectangle(List<Vector3> poly, out float minX, out float maxX, out float minZ, out float maxZ)
//     {
//         minX = maxX = minZ = maxZ = 0f;
//         if (poly == null || poly.Count != 4) return false;

//         const float eps = 1e-3f;
//         minX = maxX = poly[0].x;
//         minZ = maxZ = poly[0].z;
//         for (int i = 1; i < 4; i++)
//         {
//             minX = Mathf.Min(minX, poly[i].x);
//             maxX = Mathf.Max(maxX, poly[i].x);
//             minZ = Mathf.Min(minZ, poly[i].z);
//             maxZ = Mathf.Max(maxZ, poly[i].z);
//         }
//         // All edges axis-aligned in XZ
//         for (int i = 0; i < 4; i++)
//         {
//             Vector3 a = poly[i];
//             Vector3 b = poly[(i + 1) % 4];
//             bool axisAligned = Mathf.Abs(a.x - b.x) < eps || Mathf.Abs(a.z - b.z) < eps;
//             if (!axisAligned) return false;
//         }
//         return true;
//     }

//     static bool IsCCW(List<Vector2> poly)
//     {
//         // Shoelace area: >0 => CCW
//         double area2 = 0;
//         int n = poly.Count;
//         for (int i = 0; i < n; i++)
//         {
//             var a = poly[i];
//             var b = poly[(i + 1) % n];
//             area2 += (double)a.x * b.y - (double)b.x * a.y;
//         }
//         return area2 > 0;
//     }

//     // Ear-clip that expects CCW and emits CCW triangles (indices into given list)
//     static List<Vector3Int> EarClipTriangulateCCW(List<Vector2> poly)
//     {
//         var result = new List<Vector3Int>();
//         int n = poly.Count;
//         var idx = new List<int>(n);
//         for (int i = 0; i < n; i++) idx.Add(i);

//         int guard = 0;
//         while (idx.Count > 3 && guard++ < 10000)
//         {
//             bool clipped = false;
//             for (int ii = 0; ii < idx.Count; ii++)
//             {
//                 int i0 = idx[(ii + idx.Count - 1) % idx.Count];
//                 int i1 = idx[ii];
//                 int i2 = idx[(ii + 1) % idx.Count];

//                 Vector2 a = poly[i0], b = poly[i1], c = poly[i2];

//                 // Convex for CCW: left turn => cross(ab, bc) > 0
//                 Vector2 ab = b - a, bc = c - b;
//                 float cross = ab.x * bc.y - ab.y * bc.x;
//                 if (cross <= 0f) continue;

//                 // Check no other vertex inside ear
//                 bool anyInside = false;
//                 for (int j = 0; j < idx.Count; j++)
//                 {
//                     int k = idx[j];
//                     if (k == i0 || k == i1 || k == i2) continue;
//                     if (PointInTriCCW(poly[k], a, b, c)) { anyInside = true; break; }
//                 }
//                 if (anyInside) continue;

//                 result.Add(new Vector3Int(i0, i1, i2)); // CCW
//                 idx.RemoveAt(ii);
//                 clipped = true;
//             }
//             if (!clipped) break;
//         }
//         if (idx.Count == 3) result.Add(new Vector3Int(idx[0], idx[1], idx[2]));
//         return result;
//     }

//     static bool PointInTriCCW(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
//     {
//         return IsLeft(a, b, p) && IsLeft(b, c, p) && IsLeft(c, a, p);
//     }
//     static bool IsLeft(Vector2 a, Vector2 b, Vector2 p)
//     {
//         Vector2 ab = b - a, ap = p - a;
//         return (ab.x * ap.y - ab.y * ap.x) >= 0f;
//     }

//     // Emit a quad with flat normal matching the tri winding used by MeshBuilder.AddQuad
//     static void AddQuadWithUVNormals(SimpleMeshBuilder mb,
//                                      Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3,
//                                      Vector2 uv0, Vector2 uv1, Vector2 uv2, Vector2 uv3)
//     {
//         int baseIdx = mb.vertices.Count;

//         mb.vertices.Add(p0); mb.vertices.Add(p1); mb.vertices.Add(p2); mb.vertices.Add(p3);
//         mb.uvs.Add(uv0);     mb.uvs.Add(uv1);     mb.uvs.Add(uv2);     mb.uvs.Add(uv3);

//         // Normal from first tri (p0,p2,p1) to match AddQuad's winding
//         Vector3 n = Vector3.Cross(p2 - p0, p1 - p0).normalized;
//         mb.normals.Add(n); mb.normals.Add(n); mb.normals.Add(n); mb.normals.Add(n);

//         mb.AddQuad(baseIdx + 0, baseIdx + 1, baseIdx + 2, baseIdx + 3);
//     }

//     // Emit a triangle with CW winding whose normal faces OUTWARD from 'center'
//     static void AddTriCWOutward(SimpleMeshBuilder mb,
//                                 Vector3 p0, Vector3 p1, Vector3 p2,
//                                 Vector3 center,
//                                 Vector2 uv0, Vector2 uv1, Vector2 uv2)
//     {
//         // Start with CW orientation (0,2,1)
//         Vector3 nCW = Vector3.Cross(p2 - p0, p1 - p0).normalized;
//         Vector3 centroid = (p0 + p1 + p2) / 3f;
//         Vector3 outward = (centroid - center); outward.y = 0f;

//         // If CW normal points inward, swap p1/p2 (and UVs) so CW faces outward
//         if (Vector3.Dot(nCW, outward) < 0f)
//         {
//             var tmpP = p1; p1 = p2; p2 = tmpP;
//             var tmpU = uv1; uv1 = uv2; uv2 = tmpU;
//             nCW = Vector3.Cross(p2 - p0, p1 - p0).normalized;
//         }

//         int baseIdx = mb.vertices.Count;
//         mb.vertices.Add(p0); mb.vertices.Add(p1); mb.vertices.Add(p2);
//         mb.uvs.Add(uv0);     mb.uvs.Add(uv1);     mb.uvs.Add(uv2);
//         mb.normals.Add(nCW); mb.normals.Add(nCW); mb.normals.Add(nCW);

//         // CW indices (front-facing in Unity)
//         mb.triangles.Add(baseIdx + 0);
//         mb.triangles.Add(baseIdx + 2);
//         mb.triangles.Add(baseIdx + 1);
//     }

//     static void MakeLastFacesDoubleSided(SimpleMeshBuilder mb)
//     {
//         // Stub: if you need selective duplication, track triangle ranges and append flipped copies.
//         // Prefer using a two-sided shader instead for performance.
//     }

//     // --- MeshCollider setup/refresh ---
//     void EnsureMeshCollider(Mesh mesh)
//     {
//         var mc = GetComponent<MeshCollider>();

//         if (!useMeshCollider)
//         {
//             if (mc) DestroyImmediate(mc);
//             return;
//         }

//         if (!mc) mc = gameObject.AddComponent<MeshCollider>();

//         // Force recook on rebuild
//         mc.sharedMesh = null;
//         mc.sharedMesh = mesh;

//         mc.convex = colliderConvex; // keep false for static building
// #if UNITY_2020_2_OR_NEWER
//         mc.cookingOptions = MeshColliderCookingOptions.EnableMeshCleaning |
//                             MeshColliderCookingOptions.WeldColocatedVertices |
//                             MeshColliderCookingOptions.UseFastMidphase;
// #endif
//         if (physicsMaterial) mc.sharedMaterial = physicsMaterial;
//     }
// }
