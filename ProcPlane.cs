// // ProcPlane.cs — flat, parametric plane in XZ with meter-based UVs + MeshCollider
// // Requires: ProcGen.SimpleMeshBuilder and ProcGen.UVMapping (your existing utils)
// using UnityEngine;
// using ProcGen;

// [DisallowMultipleComponent]
// [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
// public class ProcPlane : MonoBehaviour
// {
//     public enum Pivot { Center, BottomLeft }

//     [Header("Dimensions (meters)")]
//     [Min(0.01f)] public float width  = 10f;
//     [Min(0.01f)] public float depth  = 10f;
//     public float y = 0f;                 // plane height (Y)
//     public Pivot pivot = Pivot.Center;

//     [Header("Resolution (segments)")]
//     [Min(1)] public int segmentsX = 10;  // quads across width
//     [Min(1)] public int segmentsZ = 10;  // quads across depth

//     [Header("UV Mapping (meters per tile)")]
//     // For a 1m checker, keep these at 1; material tiling (1,1), Wrap=Repeat, scale=(1,1,1)
//     public float metersPerTileU = 1f;    // U maps local X (meters)
//     public float metersPerTileV = 1f;    // V maps local Z (meters)
//     public float uOffset = 0f;
//     public float vOffset = 0f;

//     [Header("Rendering (optional)")]
//     public Material material;            // assign to avoid pink if nothing else sets it

//     [Header("Collider")]
//     public bool useMeshCollider = true;  // add/update a MeshCollider from the generated mesh
//     public bool colliderConvex  = false; // keep false for static world geometry
//     public PhysicMaterial physicsMaterial;

//     void Awake()      { Build(); }
//     void OnValidate() { if (Application.isPlaying) Build(); }

//     public void Build()
//     {
//         // Clamp resolution defensively
//         int sx = Mathf.Max(1, segmentsX);
//         int sz = Mathf.Max(1, segmentsZ);
//         float w = Mathf.Max(0.01f, width);
//         float d = Mathf.Max(0.01f, depth);

//         var mb = new SimpleMeshBuilder();

//         // Origin/pivot
//         float startX = (pivot == Pivot.Center) ? -w * 0.5f : 0f;
//         float startZ = (pivot == Pivot.Center) ? -d * 0.5f : 0f;
//         float stepX  = w / sx;
//         float stepZ  = d / sz;

//         // ----- Vertices / Normals / UVs -----
//         for (int iz = 0; iz <= sz; iz++)
//         {
//             float z = startZ + iz * stepZ;
//             for (int ix = 0; ix <= sx; ix++)
//             {
//                 float x = startX + ix * stepX;

//                 mb.vertices.Add(new Vector3(x, y, z));
//                 mb.normals.Add(Vector3.up);

//                 // Meter-based UVs (X -> U, Z -> V)
//                 mb.uvs.Add(UVMapping.FromMeters(
//                     x, z,
//                     metersPerTileU, metersPerTileV,
//                     uOffset, vOffset
//                 ));
//             }
//         }

//         // ----- Indices (CW so plane faces UP in Unity) -----
//         int stride = sx + 1;
//         for (int iz = 0; iz < sz; iz++)
//         {
//             for (int ix = 0; ix < sx; ix++)
//             {
//                 int i0 =  iz      * stride + ix;     // lower-left
//                 int i1 =  i0 + 1;                    // lower-right
//                 int i2 = (iz + 1) * stride + ix;     // upper-left
//                 int i3 =  i2 + 1;                    // upper-right
//                 mb.AddQuad(i0, i1, i2, i3);          // AddQuad emits CW from above
//             }
//         }

//         // ----- Assign Mesh -----
//         var mesh = mb.ToMesh(false);
//         var mf = GetComponent<MeshFilter>();
//         mf.sharedMesh = mesh;

//         // Optional material
//         if (material != null)
//             GetComponent<MeshRenderer>().sharedMaterial = material;

//         // ----- MeshCollider -----
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

//         // Force recook on rebuild
//         mc.sharedMesh = null;
//         mc.sharedMesh = mesh;

//         mc.convex = colliderConvex; // leave false for static level geometry

// #if UNITY_2020_2_OR_NEWER
//         mc.cookingOptions = MeshColliderCookingOptions.EnableMeshCleaning |
//                             MeshColliderCookingOptions.WeldColocatedVertices |
//                             MeshColliderCookingOptions.UseFastMidphase;
// #endif
//         if (physicsMaterial) mc.sharedMaterial = physicsMaterial;
//     }
// }
