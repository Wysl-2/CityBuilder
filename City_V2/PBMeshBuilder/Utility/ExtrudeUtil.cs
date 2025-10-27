using System;
using UnityEngine;

public static class ExtrusionUtil
{
    /// <summary>
    /// Extrudes a side quad from an edge (a->b) by moving "outward" horizontally
    /// and "down" along the chosen up axis. Returns the 4 vertices of the side face.
    /// 
    /// Ordering matches the provided winding:
    /// - CW:  [a, a2, b2, b]  (top0, bottom0, bottom1, top1)
    /// - CCW: [a, b,  b2, a2] (top0, top1,   bottom1, bottom0)
    /// </summary>
    public static Vector3[] ExtrudeEdgeOutDown(
        Vector3 a,
        Vector3 b,
        Vector3 outward,
        float outAmount,
        float downAmount,
        Vector3 upAxis = default,
        Winding winding = Winding.CW)
    {
        if (downAmount < 0f)
            throw new ArgumentException("downAmount should be non-negative; use ExtrudeEdgeOutAndVertical for signed values.", nameof(downAmount));

        return ExtrudeEdgeOutAndVertical(a, b, outward, outAmount, -downAmount, upAxis, winding);
    }

    /// <summary>
    /// Extrudes a side quad from an edge (a->b) by moving "outward" horizontally
    /// and by a signed vertical amount along the chosen up axis.
    /// Positive verticalAmount moves in +up, negative moves in -up (down).
    /// </summary>
    public static Vector3[] ExtrudeEdgeOutAndVertical(
        Vector3 a,
        Vector3 b,
        Vector3 outward,
        float outAmount,
        float verticalAmount,
        Vector3 upAxis = default,
        Winding winding = Winding.CW)
    {
        if (a == b)
            throw new ArgumentException("Edge is degenerate: a and b are identical.", nameof(a));

        // Choose up axis (default to world up)
        if (upAxis == default) upAxis = Vector3.up;
        var up = upAxis.normalized;

        // Ensure "outward" is purely horizontal (remove vertical component)
        var outwardProj = outward - Vector3.Dot(outward, up) * up;
        var sqrMag = outwardProj.sqrMagnitude;
        if (sqrMag < 1e-12f)
            throw new ArgumentException("Outward must have a non-zero horizontal component.", nameof(outward));

        var outDir = outwardProj / Mathf.Sqrt(sqrMag);

        // Final offset: horizontal outward + vertical shift
        Vector3 offset = outDir * outAmount + up * verticalAmount;

        // Bottom edge (extruded)
        Vector3 a2 = a + offset;
        Vector3 b2 = b + offset;

        // Return with ordering consistent with requested winding
        return (winding == Winding.CW)
            ? new[] { a, a2, b2, b }   // t0, b0, b1, t1
            : new[] { a, b, b2, a2 };  // t0, t1, b1, b0
    }

    public static Vector3[] ExtrudeEdgeOutToWorldY(
    Vector3 a,
    Vector3 b,
    Vector3 outward,
    float outAmount,
    float targetWorldY,
    Vector3 upAxis = default,
    Winding winding = Winding.CW)
{
    if (a == b)
        throw new ArgumentException("Edge is degenerate: a and b are identical.", nameof(a));

    // Choose up axis (defaults to world up)
    if (upAxis == default) upAxis = Vector3.up;
    var up = upAxis.normalized;

    // Project outward onto the horizontal plane (orthogonal to 'up')
    var outwardProj = outward - Vector3.Dot(outward, up) * up;
    var sqrMag = outwardProj.sqrMagnitude;
    if (sqrMag < 1e-12f)
        throw new ArgumentException("Outward must have a non-zero horizontal component.", nameof(outward));
    var outDir = outwardProj / Mathf.Sqrt(sqrMag);

    // Per-vertex vertical shift so that the far edge lands exactly at targetWorldY
    // Note: Vector3.Dot(p, up) == p.y when up == Vector3.up
    float aVert = targetWorldY - Vector3.Dot(a, up);
    float bVert = targetWorldY - Vector3.Dot(b, up);

    Vector3 a2 = a + outDir * outAmount + up * aVert;
    Vector3 b2 = b + outDir * outAmount + up * bVert;

    return (winding == Winding.CW)
        ? new[] { a, a2, b2, b }   // t0, b0, b1, t1
        : new[] { a, b, b2, a2 };  // t0, t1, b1, b0
}
}