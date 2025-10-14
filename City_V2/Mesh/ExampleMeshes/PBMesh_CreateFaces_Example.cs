using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.ProBuilder.MeshOperations;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class PBMeshBuilder_Example : MonoBehaviour
{
    public Material material;

    void Start()
    {
        var builder = new PBMeshBuilder();

        // Quad #1 on XZ plane (y=0), coords (0..1)
        builder.AddQuadFace(
            new Vector3(0, 0, 0),
            new Vector3(1, 0, 0),
            new Vector3(1, 0, 1),
            new Vector3(0, 0, 1)
        );

        // Quad #2 to +X, reuses the middle edge (1,0,0)-(1,0,1) automatically
        builder.AddQuadFace(
            new Vector3(1, 0, 0),
            new Vector3(1, 1, 0),
            new Vector3(1, 1, 1),
            new Vector3(1, 0, 1),
            Winding.CCW
        );

        builder.AddTriangleFace(
            new Vector3(0, 0, 1),
            new Vector3(1, 0, 1),
            new Vector3(0.5f, 0, 2)
        );

        var pb = builder.Build(gameObject, material);

        pb.ToMesh();
        pb.Refresh(RefreshMask.All);
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