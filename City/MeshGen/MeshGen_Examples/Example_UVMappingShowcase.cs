using UnityEngine;
using ProcGen;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class Example_UVMappingShowcase : MonoBehaviour
{
    public Material material;         // assign a checker (1x1m) material

    void Start()
    {
        var b = new MeshBuilder();    // position-only welding

        // ===== Base floor (3m x 2m), Y = 0 =====
        //  z
        //  ^   f2----f3
        //  |   |      |
        //  |   f0----f1  -> x
        // (0,0)-(3,0) by (0,2)
        var f0 = new Vector3(0, 0, 0);
        var f1 = new Vector3(3, 0, 0);
        var f2 = new Vector3(0, 0, 2);
        var f3 = new Vector3(3, 0, 2);
        b.AddQuadByPos(f0, f1, f2, f3);

        // ===== Second floor patch (coplanar with base, 1m x 1.5m) sharing edge along z=2 =====
        // Extends forward in +z
        var p0 = f2;                        // (0,0,2)
        var p1 = f3;                        // (3,0,2)
        var p2 = new Vector3(0, 0, 3.5f);
        var p3 = new Vector3(1, 0, 3.5f);   // just a smaller patch on the left side
        b.AddQuadByPos(p0, p3, p2, f2);     // CW winding toward +Y

        // ===== Vertical wall (3m x 2m), rises from the base floor’s far edge (z = 2) =====
        float wallH = 2f;
        var w0 = f2;                    // (0,0,2)
        var w1 = f3;                    // (3,0,2)
        var w2 = f2 + Vector3.up * wallH;
        var w3 = f3 + Vector3.up * wallH;
        b.AddQuadByPos(w0, w1, w2, w3); // faces toward +Z

        // ===== Slanted ramp (2m long in Z, 1.5m rise), connected to the base along z=2 =====
        // This creates a diagonal plane so we can verify the checker isn’t stretched.
        var r0 = new Vector3(0, 0, 2);      // shared with f2
        var r1 = new Vector3(1.5f, 0, 2);   // a 1.5m wide ramp segment
        var r2 = new Vector3(0, 1.5f, 4);   // up 1.5m, forward 2m
        var r3 = new Vector3(1.5f, 1.5f, 4);
        b.AddQuadByPos(r0, r1, r2, r3);     // sloped face

        // ===== Tilted/rotated quad (non-axis plane), connected to ramp’s top edge =====
        // Lean it slightly and rotate around Y for a truly arbitrary orientation.
        Vector3 t0 = r2;                               // (0,1.5,4)
        Vector3 t1 = r3;                               // (1.5,1.5,4)
        Vector3 t2 = t0 + new Vector3(0.5f, 0.7f, 1f); // up & forward & a bit right
        Vector3 t3 = t1 + new Vector3(0.5f, 0.7f, 1f);
        b.AddQuadByPos(t0, t1, t2, t3);

        // ===== Bake and assign =====
        var mesh = b.ToMesh(metersPerTile: 1f);  // your plane-grouped UV bake
        GetComponent<MeshFilter>().sharedMesh = mesh;

        if (material != null)
            GetComponent<MeshRenderer>().sharedMaterial = material;
    }
}
