using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.ProBuilder.MeshOperations;
using MeshBuilder.Primitives;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class PBMeshBuilder_Example : MonoBehaviour
{
    public Material material1;
    public Material material2;

    void Start()
    {
        var builder = new PBMeshBuilder();

        // Quad Row #1 at Z=0 (uvGroup=1)
        // Quad 1: Base quad at origin
        Quad quad = new Quad(
            vertices: MeshPrimitives.CreateQuad(new Vector2(1, 1), new Vector3(0.5f, 0, 0.5f), PlaneOrientation.XZ, Winding.CW),
            winding: Winding.CW,
            submeshIndex: 0
        );
        builder.AddQuadFace(quad);


        Quad extrudedQuad = new Quad(
            vertices: quad.ExtrudeEdge(1, Vector3.up, 1),
            winding: Winding.CW,
            submeshIndex: 1
        );
        builder.AddQuadFace(extrudedQuad);



        Material[] materials = new Material[] { material1, material2 };
        var pb = builder.Build(materials);

        // Optional: Auto-generate UVs to verify
        //UVEditing.AutoUnwrap(pb, pb.faces, true);

        // Debug vertex count
        Debug.Log($"Vertex count: {pb.vertexCount}");
    }
}