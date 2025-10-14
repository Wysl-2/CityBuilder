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

        // Quad Row #1 at Z = 0
        builder.AddQuadFace(
            new Vector3(0, 0, 0),
            new Vector3(1, 0, 0),
            new Vector3(1, 0, 1),
            new Vector3(0, 0, 1),
            submeshIndex: 0
        );

        builder.AddQuadFace(
            new Vector3(1, 0, 0),
            new Vector3(2, 0, 0),
            new Vector3(2, 0, 1),
            new Vector3(1, 0, 1),
            submeshIndex: 0
        );

        builder.AddQuadFace(
            new Vector3(2, 0, 0),
            new Vector3(3, 0, 0),
            new Vector3(3, 0, 1),
            new Vector3(2, 0, 1),
            submeshIndex: 0
        );

        // Quad Row #2 at Z = 1
        builder.AddQuadFace(
            new Vector3(0, 0, 1),
            new Vector3(1, 0, 1),
            new Vector3(1, 0, 2),
            new Vector3(0, 0, 2),
            submeshIndex: 1
        );

        builder.AddQuadFace(
            new Vector3(1, 0, 1),
            new Vector3(2, 0, 1),
            new Vector3(2, 0, 2),
            new Vector3(1, 0, 2),
            submeshIndex: 1
        );

        builder.AddQuadFace(
            new Vector3(2, 0, 1),
            new Vector3(3, 0, 1),
            new Vector3(3, 0, 2),
            new Vector3(2, 0, 2),
            submeshIndex: 1
        );

        Material[] materials = new Material[] { material1, material2 };
        var pb = builder.Build(materials);

    }
}


        // builder.AddQuadFace(
        //     new Vector3(1, 0, 0),
        //     new Vector3(1, 1, 0),
        //     new Vector3(1, 1, 1),
        //     new Vector3(1, 0, 1),
        //     Vector3.up
        // );
        
        //         builder.AddQuadFace(
        //     new Vector3(1,0,0),
        //     new Vector3(2,0,0),
        //     new Vector3(2,0,1),
        //     new Vector3(1,0,1),
        //     Vector3.up
        // );