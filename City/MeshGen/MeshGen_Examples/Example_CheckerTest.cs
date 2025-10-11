using UnityEngine;
using ProcGen;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class Example_CheckerTest : MonoBehaviour
{
    public Material material; // assign a checker texture (set Tiling = 1,1 and WrapMode = Repeat)

    void Start()
    {
        var b = new MeshBuilder();

        // === Floor (2x2 meters) at Y = 0
        var f0 = new Vector3(0, 0, 0);
        var f1 = new Vector3(2, 0, 0);
        var f2 = new Vector3(0, 0, 2);
        var f3 = new Vector3(2, 0, 2);
        b.AddQuadByPos(f0, f1, f2, f3);

        // === Vertical wall (2x2 meters) rising from the floor edge
        var w0 = f2;
        var w1 = f3;
        var w2 = f2 + Vector3.up * 2;
        var w3 = f3 + Vector3.up * 2;
        b.AddQuadByPos(w0, w1, w2, w3);

        // === Sloped ramp (2 meters long, 1 meter rise)
        var r0 = f1;
        var r1 = new Vector3(4, 0, 0);
        var r2 = new Vector3(2, 1, 2);
        var r3 = new Vector3(4, 1, 2);
        b.AddQuadByPos(r0, r1, r2, r3);

        // === Tilted/rotated quad connected to ramp top
        var t0 = r2;
        var t1 = r3;
        var t2 = r2 + new Vector3(0.5f, 0.5f, 1f);
        var t3 = r3 + new Vector3(0.5f, 0.5f, 1f);
        b.AddQuadByPos(t0, t1, t2, t3);

        // Build mesh with UVs
        var mesh = b.ToMesh(metersPerTile: 1f);
        GetComponent<MeshFilter>().sharedMesh = mesh;

        if (material != null)
            GetComponent<MeshRenderer>().sharedMaterial = material;
    }
}
