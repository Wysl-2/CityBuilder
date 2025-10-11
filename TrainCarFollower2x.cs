// Assets/MovingPlatforms/Train/Scripts/TrainCarFollower2x.cs
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;           // float3, math
using UnityEngine.Splines;         // Splines 2.x
using KinematicCharacterController;

[RequireComponent(typeof(PhysicsMover))]
[RequireComponent(typeof(Rigidbody))]
public class TrainCarFollower2x : MonoBehaviour, IMoverController
{
    [Header("Track (Splines 2.x)")]
    [Tooltip("Closed-loop SplineContainer to follow (positions are evaluated in WORLD space).")]
    public SplineContainer Track;

    [Tooltip("Index into Track.Splines (0 if only one spline).")]
    public int SplineIndex = 0;

    [Header("Motion")]
    [Tooltip("Meters per second along the track (world-space).")]
    public float Speed = 5f;

    [Tooltip("Start distance along loop (meters).")]
    public float StartDistance = 0f;

    [Tooltip("If true, wraps distance to create an infinite loop.")]
    public bool Loop = true;

    [Header("Pose / Offsets")]
    [Tooltip("Up axis for rotation/orientation.")]
    public Vector3 Up = Vector3.up;

    [Tooltip("Lateral offset in meters (relative to right = cross(Up, tangent)).")]
    public float LateralOffset = 0f;

    [Tooltip("Vertical offset in meters above the spline.")]
    public float VerticalOffset = 0f;

    [Header("Sampling / Accuracy")]
    [Range(32, 4096)]
    [Tooltip("Arc-length samples (higher = more accurate constant speed). 512–1024 is typical.")]
    public int Samples = 512;

    // Components / refs
    private PhysicsMover _mover;
    private Rigidbody _rb;
    private Transform _trackTx;
    private Spline _spline;

    // Arc-length table (WORLD-space samples)
    private readonly List<float> _arc = new();
    private readonly List<Vector3> _pts = new();

    private float _totalLength;         // world meters
    private float _dist;                // current distance along loop (world meters)

    void OnValidate()
    {
        if (Samples < 32) Samples = 32;
        if (Up.sqrMagnitude < 1e-6f) Up = Vector3.up;
        Up.Normalize();
    }

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.isKinematic = true;
        _rb.interpolation = RigidbodyInterpolation.None;

        _mover = GetComponent<PhysicsMover>();
        _mover.MoverController = this;

        ResolveSpline2x();
        BakeArcLengthTable2x();

        _dist = _totalLength > 0f ? Mathf.Repeat(StartDistance, _totalLength) : 0f;
        // DO NOT call SetPositionAndRotation here—PhysicsMover may not be fully initialized yet.
    }

    void Start()
    {
        // By Start(), PhysicsMover.Awake() has definitely run.
        SnapImmediate();
    }

    // ----------------- IMoverController -----------------

    public void UpdateMovement(out Vector3 goalPos, out Quaternion goalRot, float dt)
    {
        if (_spline == null || _totalLength <= 1e-6f)
        {
            goalPos = transform.position;
            goalRot = transform.rotation;
            return;
        }

        // Advance distance in world meters
        _dist += Speed * dt;
        _dist = Loop ? Mathf.Repeat(_dist, _totalLength) : Mathf.Clamp(_dist, 0f, _totalLength);

        // Evaluate pose by distance (WORLD space)
        EvaluateByDistance2x(_dist, out var posWS, out var tanWS);

        // Apply world-space offsets
        var rightWS = Vector3.Cross(Up, tanWS).normalized;
        posWS += rightWS * LateralOffset + Up * VerticalOffset;

        goalPos = posWS;
        goalRot = Quaternion.LookRotation(tanWS, Up);
    }

    // Not needed for continuous motion
    public void Teleport(Vector3 goalPosition, Quaternion goalRotation) { }

    // ----------------- Setup / Baking -----------------

    void ResolveSpline2x()
    {
        if (!Track)
        {
            Debug.LogError("[TrainCarFollower2x] No SplineContainer assigned.");
            return;
        }

        var list = Track.Splines;
        if (list == null || list.Count == 0)
        {
            Debug.LogError("[TrainCarFollower2x] SplineContainer has no splines.");
            return;
        }

        SplineIndex = Mathf.Clamp(SplineIndex, 0, list.Count - 1);
        _spline = list[SplineIndex];
        _trackTx = Track.transform;

        if (_spline == null)
            Debug.LogError("[TrainCarFollower2x] Selected spline is null.");
    }

    void BakeArcLengthTable2x()
    {
        _arc.Clear();
        _pts.Clear();
        _totalLength = 0f;

        if (_spline == null) return;

        _pts.Capacity = Samples + 1;
        _arc.Capacity = Samples + 1;

        Vector3 prev = EvalWorldPosition2x(_spline, 0f);
        _pts.Add(prev);
        _arc.Add(0f);

        float len = 0f;
        for (int i = 1; i <= Samples; i++)
        {
            float t = (float)i / Samples;
            Vector3 p = EvalWorldPosition2x(_spline, t);
            len += Vector3.Distance(prev, p);
            _pts.Add(p);
            _arc.Add(len);
            prev = p;
        }

        _totalLength = len;

        // If you want to auto-force Loop = true for closed splines:
        // if (_spline.Closed) Loop = true;
    }

    void SnapImmediate()
    {
        if (_mover == null) return;

        EvaluateByDistance2x(_dist, out var posWS, out var tanWS);
        var rotWS = Quaternion.LookRotation(tanWS, Up);

        // Extra safety: ensure Rigidbody & mover are valid before calling into KCC
        if (_rb != null)
            transform.SetPositionAndRotation(posWS, rotWS);

        _mover.SetPositionAndRotation(posWS, rotWS);
    }

    // ----------------- Distance → Pose (WORLD space) -----------------

    void EvaluateByDistance2x(float distance, out Vector3 posWS, out Vector3 tanWS)
    {
        int last = _arc.Count - 1;

        if (last <= 0)
        {
            posWS = transform.position;
            tanWS = transform.forward;
            return;
        }

        if (distance <= 0f)
        {
            posWS = _pts[0];
            tanWS = SegmentTangent(0);
            return;
        }

        if (distance >= _arc[last])
        {
            posWS = _pts[last];
            tanWS = SegmentTangent(last - 1);
            return;
        }

        // Binary search in arc-length table
        int lo = 0, hi = last;
        while (lo < hi)
        {
            int mid = (lo + hi) >> 1;
            if (_arc[mid] < distance) lo = mid + 1;
            else hi = mid;
        }

        int i1 = lo;
        int i0 = i1 - 1;

        float d0 = _arc[i0];
        float d1 = _arc[i1];
        float segLen = Mathf.Max(1e-6f, d1 - d0);
        float a = (distance - d0) / segLen;

        Vector3 p0 = _pts[i0];
        Vector3 p1 = _pts[i1];

        posWS = Vector3.Lerp(p0, p1, a);

        // Tangent from neighbor difference; fallback to world eval if degenerate
        Vector3 diff = p1 - p0;
        if (diff.sqrMagnitude < 1e-12f)
        {
            float t = (float)i1 / Samples;
            EvaluateWorldRaw2x(_spline, t, out _, out var tanW);
            tanWS = tanW;
        }
        else
        {
            tanWS = diff.normalized;
        }
    }

    Vector3 SegmentTangent(int i)
    {
        i = Mathf.Clamp(i, 0, _pts.Count - 2);
        return SafeNorm(_pts[i + 1] - _pts[i]);
    }

    static Vector3 SafeNorm(in Vector3 v)
        => v.sqrMagnitude > 1e-12f ? v.normalized : Vector3.forward;

    // ----------------- Splines 2.x WORLD-space evaluators -----------------

    static Vector3 ToV3(float3 f) => new Vector3(f.x, f.y, f.z);

    Vector3 EvalWorldPosition2x(Spline s, float t01)
    {
        SplineUtility.Evaluate(s, Mathf.Clamp01(t01), out float3 lp, out _, out _);
        return _trackTx ? _trackTx.TransformPoint(ToV3(lp)) : ToV3(lp);
    }

    void EvaluateWorldRaw2x(Spline s, float t01, out Vector3 posWS, out Vector3 tanWS)
    {
        SplineUtility.Evaluate(s, Mathf.Clamp01(t01), out float3 lp, out float3 ltan, out _);
        if (_trackTx)
        {
            posWS = _trackTx.TransformPoint(ToV3(lp));
            tanWS = _trackTx.TransformDirection(ToV3(ltan));
        }
        else
        {
            posWS = ToV3(lp);
            tanWS = ToV3(ltan);
        }
        tanWS = SafeNorm(tanWS);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (!Track) return;

        // Keep baked data in edit mode so you can visualize
        if (!Application.isPlaying)
        {
            ResolveSpline2x();
            BakeArcLengthTable2x();
        }

        if (_pts.Count < 2) return;

        Gizmos.color = new Color(0.15f, 1f, 0.15f, 0.85f);
        for (int i = 0; i < _pts.Count - 1; i++)
            Gizmos.DrawLine(_pts[i], _pts[i + 1]);

        // Current forward at _dist
        EvaluateByDistance2x(_dist, out var posWS, out var fwdWS);
        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(posWS, fwdWS * 1.0f);
    }
#endif
}
