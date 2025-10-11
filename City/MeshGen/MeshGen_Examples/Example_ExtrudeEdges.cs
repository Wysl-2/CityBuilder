using UnityEngine;
using ProcGen;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class Example_ExtrudeEdges : MonoBehaviour
{
    public Material material;

    void Start()
    {
        var b = new MeshBuilder();

        var floor = Quad.FromXZRect(new Vector3(0, 0, 0), widthX: 4f, depthZ: 3f, y: 0f);
        b.Add(floor);

        {
            // Southern edge is Edge01 (v0->v1) for this rect
            var(a01, b01) = floor.Edge01;
            // Make a 0.5 m vertical skirt that faces outward (-Z)
            var skirtSouth = Quad.FromEdgeExtrudeFacing(a01, b01, Vector3.down * 0.5f, outwardHint: Vector3.back);
            b.Add(skirtSouth); // or: b.Add(skirtSouth, Vector3.back);
        }

        {
            // Eastern edge is Edge01 (v1->v3) for this rect
            var(a01, b01) = floor.Edge13;
            // Make a 0.5 m vertical skirt that faces outward (-Z)
            var skirtEast = Quad.FromEdgeExtrudeFacing(a01, b01, Vector3.down * 0.5f, outwardHint: Vector3.right);
            b.Add(skirtEast); // or: b.Add(skirtSouth, Vector3.back);
        }


        var mesh = b.ToMesh(metersPerTile: 1f); // ShareByPlane by default
        GetComponent<MeshFilter>().sharedMesh = mesh;
        if (material != null) GetComponent<MeshRenderer>().sharedMaterial = material;
    }
}
