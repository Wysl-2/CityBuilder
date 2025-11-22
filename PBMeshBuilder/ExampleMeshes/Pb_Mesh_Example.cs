using System.Linq;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.ProBuilder.MeshOperations;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class PBMesh_Extrude_Example : MonoBehaviour
{
    public Material baseMat;

    [Header("Size")]
    public Vector2 size = new(10, 6); // XZ plane (y = 0)

    void Start()
    {
        var pb = GetComponent<ProBuilderMesh>() ?? gameObject.AddComponent<ProBuilderMesh>();

        // Build quad directly (XZ plane, +Y normal)
        float hx = size.x * 0.5f, hy = size.y * 0.5f;
        var verts = new[]
        {
            new Vector3(-hx, 0, -hy),
            new Vector3( hx, 0, -hy),
            new Vector3( hx, 0,  hy),
            new Vector3(-hx, 0,  hy),
        };
        // +Y normal (when verts are on XZ like you have)
        var face = new Face(new[] { 0, 2, 1, 0, 3, 2 });


        // APPLY geometry to THIS object
        pb.RebuildWithPositionsAndFaces(verts, new[] { face });
        pb.ToMesh();
        pb.Refresh(RefreshMask.All);

        // Material (renderer is guaranteed by RequireComponent)
        if (baseMat) GetComponent<MeshRenderer>().sharedMaterial = baseMat;

        // Pick "north" edge = max Z
        var baseFace = pb.faces[0];
        var edges = ElementSelection.GetPerimeterEdges(pb, new[] { baseFace }).ToList();
        var P = pb.positions;
        float maxZ = P.Max(v => v.z);
        const float eps = 1e-4f;
        var north = edges.First(e =>
            Mathf.Abs(P[e.a].z - maxZ) < eps &&
            Mathf.Abs(P[e.b].z - maxZ) < eps);

        // Extrude by angle-from-plane and slope length
        float angleFromPlaneDeg = 35f;
        float slopeLength = 3f;

        PBExtrudeUtil.ExtrudeEdgeByAngleAndLength(
            pb, north, baseFace,
            angleDeg: angleFromPlaneDeg,
            length:   slopeLength,
            angleRef: AngleRef.FromPlane,
            flipOutward: true,
            apply: true
        );
    }
}





