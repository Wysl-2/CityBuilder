using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.ProBuilder.MeshOperations;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class PBMeshBuilder_Example : MonoBehaviour
{
    public Material material1;
    public Material material2;

   void Start()
{
    var builder = new PBMeshBuilder();

    // Quad Row #1 at Z = 0 (seamGroup=1)
    builder.AddQuadFace(
        new Vector3(0, 0, 0),  // v0
        new Vector3(1, 0, 0),  // v1
        new Vector3(1, 0, 1),  // v2
        new Vector3(0, 0, 1),  // v3
        submeshIndex: 0,
        disconnectedVertices: new[] { 1, 2, 3 }, // Disconnect verts at Z=1: (1,0,1), (0,0,1)
        uvGroup: 0
    );

    builder.AddQuadFace(
        new Vector3(1, 0, 0),  // v0
        new Vector3(2, 0, 0),  // v1
        new Vector3(2, 0, 1),  // v2
        new Vector3(1, 0, 1),  // v3
        submeshIndex: 0,
        //disconnectedVertices: new[] { 2, 3 }, // Disconnect verts at Z=1: (2,0,1), (1,0,1)
        uvGroup: 0
    );

    builder.AddQuadFace(
        new Vector3(2, 0, 0),  // v0
        new Vector3(3, 0, 0),  // v1
        new Vector3(3, 0, 1),  // v2
        new Vector3(2, 0, 1),  // v3
        submeshIndex: 0,
        //disconnectedVertices: new[] { 2, 3 }, // Disconnect verts at Z=1: (3,0,1), (2,0,1)
        uvGroup: 0
    );

    // Quad Row #2 at Z = 1
    // Quad 4: Group with Row 1, seam along X=1
    builder.AddQuadFace(
        new Vector3(0, 0, 1),  // v0
        new Vector3(1, 0, 1),  // v1
        new Vector3(1, 0, 2),  // v2
        new Vector3(0, 0, 2),  // v3
        submeshIndex: 1,
        //disconnectedVertices: new[] { 1, 2 }, // Disconnect verts at X=1: (1,0,1), (1,0,2)
        uvGroup: 0
    );

    // Quad 5 and 6: Separate group, seam along Z=1
    builder.AddQuadFace(
        new Vector3(1, 0, 1),  // v0
        new Vector3(2, 0, 1),  // v1
        new Vector3(2, 0, 2),  // v2
        new Vector3(1, 0, 2),  // v3
        submeshIndex: 1,
        //disconnectedVertices: new[] { 0, 1 }, // Disconnect verts at Z=1: (1,0,1), (2,0,1)
        uvGroup: 0
    );

    builder.AddQuadFace(
        new Vector3(2, 0, 1),  // v0
        new Vector3(3, 0, 1),  // v1
        new Vector3(3, 0, 2),  // v2
        new Vector3(2, 0, 2),  // v3
        submeshIndex: 1,
        //disconnectedVertices: new[] { }, // Disconnect verts at Z=1: (2,0,1), (3,0,1)
        uvGroup: 0
    );

    Material[] materials = new Material[] { material1, material2 };
    var pb = builder.Build(materials);

    // Optional: Auto-generate UVs to verify
    //UVEditing.AutoUnwrap(pb, pb.faces, true);
}
}