using System.Linq;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.ProBuilder.MeshOperations;

public class Pb_Mesh_TwoStepExtrude : MonoBehaviour
{
    [Header("Base plane (XZ if Axis.Up)")]
    public Vector2 size = new(10, 6);
    public Axis planeAxis = Axis.Up;          // Up => plane lies on XZ
    public Material baseMat;

    [Header("First extrude (from plane's north edge)")]
    [Range(1f, 89f)] public float firstAngleFromPlaneDeg = 45f; // 45° slope from base plane
    public float firstRise = 2f;                                // vertical component along face normal
    public bool firstFlipOutward = false;                       // flip lateral direction if needed
    public Material firstRimMat;

    [Header("Second extrude (flat, from top rim of first)")]
    public float flatLateral = 1.5f;         // meters, outward only
    public float riseEpsilon = 0.0001f;      // tiny rise so ProBuilder forms geometry
    public bool secondFlipOutward = false;
    public Material secondRimMat;

    void Start()
    {
        // 1) Create the PB plane (GameObject comes with ProBuilderMesh + MeshFilter + MeshRenderer).
        var pb = ShapeGenerator.GeneratePlane(PivotLocation.Center, size.x, size.y, 0, 0, planeAxis);
        pb.transform.SetParent(transform, false);
        if (baseMat) pb.GetComponent<MeshRenderer>().sharedMaterial = baseMat;

        // We'll assume planeAxis == Axis.Up (plane on XZ). If not, change the "north" pick below.
        var baseFace = pb.faces[0];

        // 2) Pick the "north" border edge (max Z) of the plane.
        var edges = ElementSelection.GetPerimeterEdges(pb, new[] { baseFace }).ToList();
        var P = pb.positions;
        float maxZ = P.Max(v => v.z);
        const float eps = 1e-4f;
        var north = edges.First(e => Mathf.Abs(P[e.a].z - maxZ) < eps && Mathf.Abs(P[e.b].z - maxZ) < eps);

        // Cache reference frame (before we change topology).
        var nBase = Math.Normal(pb, baseFace).normalized;                 // face normal (+Y if Axis.Up)
        var tBase = (P[north.b] - P[north.a]).normalized;                 // edge tangent
        var northMidBefore = 0.5f * (P[north.a] + P[north.b]);            // origin for comparisons

        // 3) FIRST: angled extrude from the plane's north edge.
        var newEdges1 = PBExtrudeUtil.ExtrudeEdgeAtAngle(
            pb, north, baseFace, firstAngleFromPlaneDeg, firstRise,
            flipOutward: firstFlipOutward, apply: true
        );

        // Optionally tint the first sloped face.
        if (firstRimMat)
        {
            // Faces roughly facing "outward" from that north edge.
            PBExtrudeUtil.EdgeFrame(pb, north, baseFace, out _, out _, out var outward1);
            var sloped1 = pb.faces.FirstOrDefault(f => Vector3.Dot(Math.Normal(pb, f).normalized, outward1) > 0.6f);
            if (sloped1 != null) pb.SetMaterial(new[] { sloped1 }, firstRimMat);
        }

        // 4) Choose the TOP RIM edge from the first extrusion:
        //    - parallel to original edge (dot with tBase near 1)
        //    - farthest along +nBase from the original edge mid.
        P = pb.positions; // refresh handle to positions after topology change
        var topCandidates = newEdges1
            .Where(e =>
            {
                var te = (P[e.b] - P[e.a]).normalized;
                return Mathf.Abs(Vector3.Dot(te, tBase)) > 0.99f; // parallel to original edge
            })
            .Select(e => new { e, score = Vector3.Dot((0.5f * (P[e.a] + P[e.b]) - northMidBefore), nBase) })
            .OrderByDescending(x => x.score)
            .ToList();

        if (topCandidates.Count == 0)
        {
            Debug.LogWarning("Could not find a top rim edge from the first extrusion.");
            return;
        }
        var topRim = topCandidates[0].e;

        // 5) SECOND: flat extrude outward from that top rim (lateral only).
        //    Use a tiny riseEpsilon so ProBuilder forms new geometry; result is effectively flat.
        var newEdges2 = PBExtrudeUtil.ExtrudeEdgeWithOffsets(
            pb, topRim, baseFace, rise: riseEpsilon, lateral: flatLateral,
            flipOutward: secondFlipOutward, apply: true
        );

        // Optionally tint the second sloped/flat face (faces roughly facing the outward2 direction).
        if (secondRimMat)
        {
            PBExtrudeUtil.EdgeFrame(pb, topRim, baseFace, out _, out _, out var outward2);
            var sloped2 = pb.faces.FirstOrDefault(f => Vector3.Dot(Math.Normal(pb, f).normalized, outward2) > 0.6f);
            if (sloped2 != null) pb.SetMaterial(new[] { sloped2 }, secondRimMat);
        }

        // Done. (Utilities have already applied ToMesh/Refresh.)
    }
}


// using UnityEngine;
// using UnityEngine.ProBuilder;
// using UnityEngine.ProBuilder.MeshOperations;

public static class PBExtrudeUtil
{
    /// Build an edge-local frame on a reference face:
    ///   n = face normal, t = edge tangent (a->b), outward = n x t (lateral, lies in the face's plane).
    public static void EdgeFrame(ProBuilderMesh pb, Edge edge, Face referenceFace,
                                 out Vector3 n, out Vector3 t, out Vector3 outward)
    {
        var p = pb.positions;
        n = Math.Normal(pb, referenceFace).normalized;
        t = (p[edge.b] - p[edge.a]);
        if (t.sqrMagnitude < 1e-12f) t = Vector3.right;
        t.Normalize();
        outward = Vector3.Cross(n, t).normalized;
    }

    /// Extrude 'edge' so the new side has 'angleFromPlaneDeg' (0..90) relative to 'referenceFace' plane.
    /// rise = vertical component along the face normal.
    /// If the lateral push goes the wrong way, set flipOutward = true.
    /// If apply = true (default), ToMesh() + Refresh(All) are called for you.
    public static Edge[] ExtrudeEdgeAtAngle(
        ProBuilderMesh pb,
        Edge edge,
        Face referenceFace,
        float angleFromPlaneDeg,
        float rise,
        bool flipOutward = false,
        bool group = true,
        bool manifold = true,
        bool apply = true,
        RefreshMask refreshMask = RefreshMask.All)
    {
        EdgeFrame(pb, edge, referenceFace, out var n, out var t, out var outward);
        if (flipOutward) outward = -outward;

        // 1) Topology: extrude along normal by 'rise'
        var newEdges = ExtrudeElements.Extrude(pb, new[] { edge }, rise, group, manifold);

        // 2) Lateral shove to hit angle: lateral = tan(angle) * rise
        float rad = Mathf.Deg2Rad * Mathf.Clamp(angleFromPlaneDeg, 0.01f, 89.99f);
        float lateral = Mathf.Tan(rad) * rise;
        VertexPositioning.TranslateVertices(pb, newEdges, outward * lateral);

        if (apply)
        {
            pb.ToMesh();
            pb.Refresh(refreshMask);
        }

        return newEdges;
    }

    /// Extrude 'edge' with explicit offsets:
    ///   rise = along face normal (meters), lateral = along 'outward' in the face's plane (meters).
    ///   Pass a tiny rise (e.g., 0.0001) for an effectively flat step.
    public static Edge[] ExtrudeEdgeWithOffsets(
        ProBuilderMesh pb,
        Edge edge,
        Face referenceFace,
        float rise,
        float lateral,
        bool flipOutward = false,
        bool group = true,
        bool manifold = true,
        bool apply = true,
        RefreshMask refreshMask = RefreshMask.All)
    {
        EdgeFrame(pb, edge, referenceFace, out var n, out var t, out var outward);
        if (flipOutward) outward = -outward;

        var newEdges = ExtrudeElements.Extrude(pb, new[] { edge }, rise, group, manifold);
        VertexPositioning.TranslateVertices(pb, newEdges, outward * lateral);

        if (apply)
        {
            pb.ToMesh();
            pb.Refresh(refreshMask);
        }

        return newEdges;
    }
}
