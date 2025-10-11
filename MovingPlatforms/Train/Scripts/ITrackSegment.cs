using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

public interface ITrackSegment
{
    // ---- Authoring contract you already rely on ----
    Vector3 StartPoint { get; }
    Quaternion StartRotation { get; }
    Vector3 EndPoint { get; }
    Quaternion EndRotation { get; }
    void Rebuild();

    // ---- NEW: geometry & metadata for baking/streaming ----
    // Total arclength of this segment, in meters.
    float Length { get; }

    // World-space conservative bounds (useful for streaming / culling).
    Bounds WorldBounds { get; }

    // Stable identity and basic change tracking.
    string SegmentGuid { get; }     // assign-once stable ID
    string SegmentType { get; }     // e.g., "Arc", "Straight"
    int    ParamVersion { get; }    // bump on param schema change
    int    ComputeParamHash();      // hash of current authoring parameters

    // Optional direct access to the underlying spline (first/only curve).
    // Return true if available; false if this segment doesn’t back with a SplineContainer.
    bool TryGetSpline(out SplineContainer container, out int splineIndex);

    // Portable knot access (first/only curve) – can return null if not present.
    IEnumerable<BezierKnot> GetKnotsLocal();

    // Lightweight sampling surface in [0..1] across the segment.
    Vector3 SamplePosition01(float t);
    Vector3 SampleTangent01(float t);
}
