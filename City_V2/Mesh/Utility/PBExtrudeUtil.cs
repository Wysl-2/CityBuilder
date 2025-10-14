using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.ProBuilder.MeshOperations;

public enum AngleRef
{
    FromPlane,  // 0° = flat in the face plane, 90° = straight up along the face normal
    FromNormal  // 0° = along the face normal, 90° = flat in the face plane
}

public static class PBExtrudeUtil
{
    // ---------- Shared frame ----------
    /// Build an edge-local frame on a reference face:
    ///   n = face normal, t = edge tangent (a->b), outward = n x t (in the face plane, "to the outside").
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

    // ---------- Core ----------
    /// Internal core: do an edge extrude with 'rise' along n, then translate by 'inPlane' (combination of outward and t).
    static Edge[] CoreExtrude(ProBuilderMesh pb, Edge edge, float rise, Vector3 inPlane,
                              bool group, bool manifold, bool apply, RefreshMask refreshMask)
    {
        var newEdges = ExtrudeElements.Extrude(pb, new[] { edge }, rise, group, manifold);
        if (inPlane.sqrMagnitude > 0f)
            VertexPositioning.TranslateVertices(pb, newEdges, inPlane);

        if (apply)
        {
            pb.ToMesh();
            pb.Refresh(refreshMask);
        }
        return newEdges;
    }

    // ---------- 1) Angle + length ----------
    /// Extrude with an angle and a total length along that sloped direction.
    /// angleRef=FromPlane means θ is measured from the face plane; FromNormal is measured from the normal.
    public static Edge[] ExtrudeEdgeByAngleAndLength(
        ProBuilderMesh pb,
        Edge edge,
        Face referenceFace,
        float angleDeg,
        float length,
        AngleRef angleRef = AngleRef.FromPlane,
        bool flipOutward = false,
        bool group = true,
        bool manifold = true,
        bool apply = true,
        RefreshMask refreshMask = RefreshMask.All)
    {
        EdgeFrame(pb, edge, referenceFace, out var n, out var t, out var outward);
        if (flipOutward) outward = -outward;

        float r = Mathf.Clamp(angleDeg, 0.0001f, 89.999f) * Mathf.Deg2Rad;

        float rise, lateral;
        if (angleRef == AngleRef.FromPlane)
        {
            // length is along the sloped direction; decompose into n/outward
            rise   = length * Mathf.Sin(r);
            lateral= length * Mathf.Cos(r);
        }
        else // FromNormal
        {
            rise   = length * Mathf.Cos(r);
            lateral= length * Mathf.Sin(r);
        }

        Vector3 inPlane = outward * lateral;
        return CoreExtrude(pb, edge, rise, inPlane, group, manifold, apply, refreshMask);
    }

    // ---------- 2) Direction + distance ----------
    /// Extrude in a given direction (Self/World) by a distance.
    /// If clampToEdgePlane, we remove the along-edge (tangent) component so the extrusion lives in the plane spanned by n and outward.
    public static Edge[] ExtrudeEdgeByDirection(
        ProBuilderMesh pb,
        Edge edge,
        Face referenceFace,
        Vector3 direction,
        float distance,
        Space space = Space.Self,
        bool clampToEdgePlane = true,
        bool flipOutward = false,
        bool group = true,
        bool manifold = true,
        bool apply = true,
        RefreshMask refreshMask = RefreshMask.All)
    {
        EdgeFrame(pb, edge, referenceFace, out var n, out var t, out var outward);
        if (flipOutward) outward = -outward;

        // Bring direction into mesh-local space if provided in world space.
        Vector3 dirLocal = (space == Space.World)
            ? pb.transform.InverseTransformDirection(direction)
            : direction;

        // Optionally remove the tangent (along-edge) component to avoid shearing along the edge.
        if (clampToEdgePlane)
            dirLocal -= Vector3.Dot(dirLocal, t) * t;

        if (dirLocal.sqrMagnitude < 1e-12f)
            dirLocal = outward; // fallback

        Vector3 delta = dirLocal.normalized * distance;

        float rise = Vector3.Dot(delta, n);
        float tOff = Vector3.Dot(delta, t);
        float lat  = Vector3.Dot(delta, outward);

        // Keep the in-plane part (outward + optional tangent if not clamped)
        Vector3 inPlane = outward * lat + (clampToEdgePlane ? Vector3.zero : t * tOff);

        return CoreExtrude(pb, edge, rise, inPlane, group, manifold, apply, refreshMask);
    }

    // ---------- 3) XYZ offsets ----------
    /// Extrude by literal XYZ offsets. 'offset' is an object- or world-space displacement for the new rim.
    /// We take the normal component as 'rise'; the remainder is applied in-plane (outward + optional tangent).
    public static Edge[] ExtrudeEdgeByAxisOffsets(
        ProBuilderMesh pb,
        Edge edge,
        Face referenceFace,
        Vector3 offset,
        Space space = Space.Self,
        bool projectToEdgePlane = false,  // if true, drop the tangent component
        bool flipOutward = false,
        bool group = true,
        bool manifold = true,
        bool apply = true,
        RefreshMask refreshMask = RefreshMask.All)
    {
        EdgeFrame(pb, edge, referenceFace, out var n, out var t, out var outward);
        if (flipOutward) outward = -outward;

        Vector3 offLocal = (space == Space.World)
            ? pb.transform.InverseTransformVector(offset)
            : offset;

        float rise = Vector3.Dot(offLocal, n);

        // Decompose the in-plane component (could include tangent).
        float lat  = Vector3.Dot(offLocal, outward);
        float tOff = Vector3.Dot(offLocal, t);

        if (projectToEdgePlane) tOff = 0f; // keep the “ramp” purely in the n–outward plane

        Vector3 inPlane = outward * lat + t * tOff;

        return CoreExtrude(pb, edge, rise, inPlane, group, manifold, apply, refreshMask);
    }

    // ---------- Thin wrappers to keep old API available ----------

    /// (Fixed) Angle measured FROM THE PLANE. 'rise' is along the face normal.
    /// lateral is computed as rise / tan(angle) (cotangent), which matches "angle from plane".
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

        float r = Mathf.Clamp(angleFromPlaneDeg, 0.0001f, 89.999f) * Mathf.Deg2Rad;
        float lateral = rise / Mathf.Tan(r); // cotangent

        return CoreExtrude(pb, edge, rise, outward * lateral, group, manifold, apply, refreshMask);
    }

    /// Same as before: explicit normal rise + outward lateral.
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

        return CoreExtrude(pb, edge, rise, outward * lateral, group, manifold, apply, refreshMask);
    }
}