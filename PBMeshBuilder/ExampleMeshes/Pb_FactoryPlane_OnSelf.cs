// using System.Linq;
// using UnityEngine;
// using UnityEngine.ProBuilder;
// using UnityEngine.ProBuilder.MeshOperations;

// [RequireComponent(typeof(MeshFilter))]
// [RequireComponent(typeof(MeshRenderer))]
// public class PB_FactoryPlane_OnSelf : MonoBehaviour
// {
//     public float width = 2, depth = 2;
//     public Axis axis = Axis.Up;   // Up => plane on XZ with +Y normal
//     public Material mat;

//     void Start()
//     {
//         // Ensure a PB mesh on THIS object
//         var host = GetComponent<ProBuilderMesh>() ?? gameObject.AddComponent<ProBuilderMesh>();

//         // 1) Use ProBuilder factory to build a temporary plane
//         var tmp = ShapeGenerator.GeneratePlane(PivotLocation.FirstCorner, width, depth, 0, 0, axis);

//         // 2) Copy positions & faces into THIS object's ProBuilderMesh
//         var positions = tmp.positions.ToArray();
//         var faces = tmp.faces.Select(f => new Face(f.indexes.ToArray())
//         {
//             manualUV       = f.manualUV,
//             uv             = new AutoUnwrapSettings(f.uv),
//             submeshIndex   = f.submeshIndex,
//             smoothingGroup = f.smoothingGroup,
//             textureGroup   = f.textureGroup
//         }).ToArray();

//         host.RebuildWithPositionsAndFaces(positions, faces);
//         host.ToMesh();
//         host.Refresh(RefreshMask.All);

//         // Optional: apply a material on THIS object
//         if (mat) GetComponent<MeshRenderer>().sharedMaterial = mat;

//         // 3) Clean up the factory object
//         DestroyImmediate(tmp.gameObject);
//     }
// }
