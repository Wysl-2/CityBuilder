// // ProcIntersection.cs (temporary flat-plane version, fixed winding)
// // Generates a single flat quad (size.x by size.y) centered on this transform,
// // at carriageway depth (-carriagewaySetIn). Object Y is pinned to areaY.
// // This version adds SideProfile-compatible fields (uniform + per-side overrides)
// // so the editor/visualization can resolve per-side widths, but mesh stays flat.

// using System;
// using UnityEngine;
// using ProcGen;
// using SP = CityWorkspaceSimple.SideProfile; // reuse the same SideProfile type used by the workspace

// [DisallowMultipleComponent]
// [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
// public class ProcIntersection : MonoBehaviour
// {
//     [Header("Intersection Rect (size only; centered on this transform)")]
//     [Tooltip("Kept for compatibility but IGNORED by the temp mesh.")]
//     public Vector2 center = Vector2.zero; // not used by temp mesh
//     public Vector2 size   = new Vector2(8, 8);

//     [Header("Workspace plane")]
//     public float areaY = 0f; // local Y=0 aligns to this via transform

//     [Header("Per-Side Carriageway Connections (unused in temp mesh)")]
//     public bool connectNorth = true; // +Z
//     public bool connectEast  = true; // +X
//     public bool connectSouth = true; // -Z
//     public bool connectWest  = true; // -X

//     // ------- Uniform cross-section (fallback) -------
//     [Header("Uniform Cross-Section (fallback)")]
//     [Min(0.1f)] public float carriagewayWidth = 6.0f;
//     [Min(0.0f)] public float gutterWidth      = 0.5f;
//     [Min(0.0f)] public float curbWidth        = 0.2f;
//     [Min(0.1f)] public float footpathWidth    = 2.0f;

//     [Header("Uniform Heights (meters)")]
//     [Min(0.0f)] public float carriagewaySetIn = 0.04f; // used by temp mesh
//     [Min(0.0f)] public float gutterDepth      = 0.05f;
//     [Min(0.0f)] public float curbHeight       = 0.15f;

//     // ------- Per-side overrides (optional) -------
//     [Header("Per-Side Overrides (optional)")]
//     public bool useNorthOverride = false;
//     public bool useEastOverride  = false;
//     public bool useSouthOverride  = false;
//     public bool useWestOverride   = false;

//     public SP northOverride;
//     public SP eastOverride;
//     public SP southOverride;
//     public SP westOverride;

//     // -------- Resolved profiles (uniform fallback unless override enabled) --------
//     public SP UniformProfile =>
//         SP.FromUniform(carriagewayWidth, gutterWidth, curbWidth, footpathWidth,
//                        carriagewaySetIn, gutterDepth, curbHeight);

//     public SP ProfileNorth => useNorthOverride ? northOverride : UniformProfile;
//     public SP ProfileEast  => useEastOverride  ? eastOverride  : UniformProfile;
//     public SP ProfileSouth => useSouthOverride ? southOverride : UniformProfile;
//     public SP ProfileWest  => useWestOverride  ? westOverride  : UniformProfile;

//     // Quick access to per-side ring reserves (useful for scene gizmos)
//     public float ReserveNorth => ProfileNorth.ReserveRing;
//     public float ReserveEast  => ProfileEast.ReserveRing;
//     public float ReserveSouth => ProfileSouth.ReserveRing;
//     public float ReserveWest  => ProfileWest.ReserveRing;

//     [Header("UV (meters per tile)")]
//     public float metersPerTileU = 1f;
//     public float metersPerTileV = 1f;
//     public float uOffset = 0f;
//     public float vOffset = 0f;

//     [Header("Rendering")]
//     public Material material;

//     [Header("Collider")]
//     public bool useMeshCollider = true;
//     public bool colliderConvex  = false;
//     public PhysicMaterial physicsMaterial;

//     void Awake()      { Build(); }
//     void OnValidate() { if (enabled && gameObject.activeInHierarchy) Build(); }

//     public void Build()
//     {
//         // Align local Y=0 to the workspace plane
//         var lp = transform.localPosition;
//         transform.localPosition = new Vector3(lp.x, areaY, lp.z);

//         var mf = GetComponent<MeshFilter>();
//         var mr = GetComponent<MeshRenderer>();
//         mf.sharedMesh = null;

//         if (!mr.sharedMaterial)
//             mr.sharedMaterial = material ? material : new Material(Shader.Find("Standard"));
//         else if (material) mr.sharedMaterial = material;

//         float hx = Mathf.Max(0.01f, size.x * 0.5f);
//         float hz = Mathf.Max(0.01f, size.y * 0.5f);

//         // NOTE: Keep mesh generation unchanged — still a flat plane.
//         // We continue to use the uniform carriagewaySetIn for Z height.
//         float yLane = -Mathf.Max(0f, carriagewaySetIn);

//         var mb = new SimpleMeshBuilder();

//         // Vertex order [bl, br, tl, tr] to match AddQuad's CW winding
//         int v0 = mb.vertices.Count;
//         Vector3 bl = new Vector3(-hx, yLane, -hz);
//         Vector3 br = new Vector3(+hx, yLane, -hz);
//         Vector3 tl = new Vector3(-hx, yLane, +hz);
//         Vector3 tr = new Vector3(+hx, yLane, +hz);

//         mb.vertices.Add(bl);
//         mb.vertices.Add(br);
//         mb.vertices.Add(tl);
//         mb.vertices.Add(tr);

//         mb.normals.Add(Vector3.up); mb.normals.Add(Vector3.up);
//         mb.normals.Add(Vector3.up); mb.normals.Add(Vector3.up);

//         // UVs from local XZ (meters-based)
//         mb.uvs.Add(UVMapping.FromMeters(bl.x, bl.z, metersPerTileU, metersPerTileV, uOffset, vOffset));
//         mb.uvs.Add(UVMapping.FromMeters(br.x, br.z, metersPerTileU, metersPerTileV, uOffset, vOffset));
//         mb.uvs.Add(UVMapping.FromMeters(tl.x, tl.z, metersPerTileU, metersPerTileV, uOffset, vOffset));
//         mb.uvs.Add(UVMapping.FromMeters(tr.x, tr.z, metersPerTileU, metersPerTileV, uOffset, vOffset));

//         // CW triangles via AddQuad
//         mb.AddQuad(v0 + 0, v0 + 1, v0 + 2, v0 + 3);

//         var mesh = mb.ToMesh(false);
//         mf.sharedMesh = mesh;

//         EnsureMeshCollider(mesh);
//         gameObject.isStatic = true;
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
//         mc.sharedMesh = null;
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
