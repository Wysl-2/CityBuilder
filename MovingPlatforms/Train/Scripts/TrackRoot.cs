using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class TrackRoot : MonoBehaviour
{
    // ===== Loop config =====
    public bool IsClosedLoop = false;

    [Tooltip("Max allowed end→start positional gap (meters) when validating a closed loop.")]
    public float G0PositionTolerance = 0.01f;

    [Tooltip("Max allowed end→start forward-heading delta (degrees) when validating a closed loop.")]
    public float G1AngleToleranceDeg = 1.0f;

    [SerializeField] private List<GameObject> _segmentObjects = new List<GameObject>();

    /// Ordered list of segment GameObjects (serialized).
    public List<GameObject> SegmentObjects => _segmentObjects;

    /// Helper: get segment component by index (null if missing).
    public ITrackSegment GetSegment(int index)
    {
        if (index < 0 || index >= _segmentObjects.Count) return null;
        var go = _segmentObjects[index];
        return go ? go.GetComponent<ITrackSegment>() : null;
    }

    public int Count => _segmentObjects.Count;

    public GameObject GetObject(int index) =>
        (index >= 0 && index < _segmentObjects.Count) ? _segmentObjects[index] : null;

    public int IndexOf(GameObject go) => _segmentObjects.IndexOf(go);

    public void Add(GameObject go)
    {
        if (go != null && !_segmentObjects.Contains(go))
            _segmentObjects.Add(go);
    }

    public void Insert(int index, GameObject go)
    {
        if (go == null) return;
        index = Mathf.Clamp(index, 0, _segmentObjects.Count);
        if (!_segmentObjects.Contains(go))
            _segmentObjects.Insert(index, go);
    }

    public void RemoveAt(int index)
    {
        if (index < 0 || index >= _segmentObjects.Count) return;
        _segmentObjects.RemoveAt(index);
    }

    public void Clear() => _segmentObjects.Clear();

    // --- Loop closure ------------------ //
    public bool TryGetFirst(out ITrackSegment seg)
    {
        seg = null;
        if (Count <= 0) return false;
        seg = GetSegment(0);
        return seg != null;
    }

    public bool TryGetLast(out ITrackSegment seg)
    {
        seg = null;
        if (Count <= 0) return false;
        seg = GetSegment(Count - 1);
        return seg != null;
    }

    /// <summary>
    /// Returns (gapMeters, angleDeltaDeg) between last.End and first.Start.
    /// If segments are missing, returns (float.PositiveInfinity, float.PositiveInfinity).
    /// </summary>
    public (float gap, float angDeg) GetLoopClosureError()
    {
        if (!TryGetFirst(out var first) || !TryGetLast(out var last))
            return (float.PositiveInfinity, float.PositiveInfinity);

        var p0 = first.StartPoint;
        var p1 = last.EndPoint;

        // Position gap (XZ or full 3D? Use full 3D to be strict.)
        float gap = Vector3.Distance(p1, p0);

        // Heading delta (yaw) between last.End forward and first.Start forward
        Vector3 fA = last.EndRotation * Vector3.forward;
        Vector3 fB = first.StartRotation * Vector3.forward;
        fA.y = 0f; fB.y = 0f;
        if (fA.sqrMagnitude < 1e-8f || fB.sqrMagnitude < 1e-8f)
            return (gap, 180f);

        fA.Normalize(); fB.Normalize();
        float ang = Vector3.SignedAngle(fA, fB, Vector3.up);
        float angAbs = Mathf.Abs(ang);
        return (gap, angAbs);
    }

    public bool IsLoopWithinTolerance()
    {
        var (gap, ang) = GetLoopClosureError();
        return gap <= G0PositionTolerance && ang <= G1AngleToleranceDeg;
    }

}
