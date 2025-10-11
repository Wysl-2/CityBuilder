// Assets/MovingPlatforms/Train/Scripts/TrackArcSegment.cs
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;
using Unity.Profiling;

[ExecuteAlways]
[RequireComponent(typeof(SplineContainer))]
public class TrackArcSegment : MonoBehaviour, ITrackSegment
{
    #if UNITY_EDITOR
    static readonly ProfilerMarker kRebuild    = new ProfilerMarker("Track/Arc/Rebuild");
    static readonly ProfilerMarker kBuildGeom  = new ProfilerMarker("Track/Arc/BuildGeometry");
    static readonly ProfilerMarker kWriteArc   = new ProfilerMarker("Track/Arc/WriteSpline");
    #endif

    [System.NonSerialized] private Spline _scratch;
    private Spline BeginScratch()
    {
        if (_scratch == null) _scratch = new Spline();
        else _scratch.Clear();
        return _scratch;
    }
    // -------- Authoring --------
    [Header("Arc Definition")]
    [Min(0f)] public float Radius = 10f;

    [Tooltip("Arc sweep (degrees). Positive = CW (right turn), Negative = CCW (left turn).")]
    public float SweepAngleDeg = 90f;

    [Header("Spline Build")]
    [Tooltip("Max sweep per cubic piece (deg). 90 is a good default; 60 for extra tight.")]
    [Range(10f, 180f)] public float MaxDegreesPerSegment = 90f;

    [Tooltip("Rebuild automatically in Edit/Play Mode when values change.")]
    public bool AutoRebuild = true;
    [Tooltip("Also rebuild automatically when in Play Mode.")]
    public bool AutoRebuildInPlay = false;

    // -------- Outputs / internals --------
    [Header("Output (read-only)")]
    [SerializeField] private Vector3 _worldCenter;
    [SerializeField] private Vector3 _endPoint;

    [SerializeField] public float StartKnotYDeg;
    [SerializeField] public float EndKnotYDeg;

    private SplineContainer _container;

    // Identity / versioning (for bake provenance)
    [SerializeField] private string _segmentGuid;
    public string SegmentGuid
    {
        get
        {
            if (string.IsNullOrEmpty(_segmentGuid))
                _segmentGuid = Guid.NewGuid().ToString("N");
            return _segmentGuid;
        }
    }
    public string SegmentType => "Arc";
    public int ParamVersion => 1;

    // -------- ITrackSegment surface --------
    public Vector3 StartPoint => transform.position;
    public Quaternion StartRotation => Quaternion.Euler(0f, StartKnotYDeg, 0f);
    public Vector3 EndPoint => _endPoint;
    public Quaternion EndRotation => Quaternion.Euler(0f, EndKnotYDeg, 0f);

    // Arclength: |sweep| * R
    public float Length => Mathf.Abs(Mathf.Deg2Rad * SweepAngleDeg) * Mathf.Max(0f, Radius);

    public Bounds WorldBounds
    {
        get
        {
            // Conservative: encapsulate start/end and a square around center with radius
            var b = new Bounds(StartPoint, Vector3.zero);
            b.Encapsulate(EndPoint);
            b.Encapsulate(_worldCenter + new Vector3(+Radius, 0f, 0f));
            b.Encapsulate(_worldCenter + new Vector3(-Radius, 0f, 0f));
            b.Encapsulate(_worldCenter + new Vector3(0f, 0f, +Radius));
            b.Encapsulate(_worldCenter + new Vector3(0f, 0f, -Radius));
            return b;
        }
    }

    // -------- Lifecycle --------
    void Awake()
    {
        _container = GetComponent<SplineContainer>();
        if (string.IsNullOrEmpty(_segmentGuid)) _segmentGuid = Guid.NewGuid().ToString("N");
    }

    void OnValidate()
    {
        Radius = Mathf.Max(0f, Radius);
        if (_container == null) _container = GetComponent<SplineContainer>();

#if UNITY_EDITOR
        if (!Application.isPlaying && AutoRebuild)
            TrackRebuildScheduler.RequestRebuild(this);
#endif
    }

    void Update()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying && AutoRebuild)
        {
            if (transform.hasChanged)
            {
                transform.hasChanged = false; // consume the change
                TrackRebuildScheduler.RequestRebuild(this, requestSceneRepaint: false);
            }
            // Don’t request if nothing moved/rotated/scaled this frame
            return;
        }
#endif

        if (Application.isPlaying && AutoRebuildInPlay && transform.hasChanged)
        {
            transform.hasChanged = false;
            Rebuild();
        }
    }

    // -------- Build --------
    [ContextMenu("Rebuild Now")]
    public void Rebuild()
    {
        if (_container == null) _container = GetComponent<SplineContainer>();
    #if UNITY_EDITOR
        using (kRebuild.Auto())
    #endif
        {
    #if UNITY_EDITOR
            using (kBuildGeom.Auto())
    #endif
            BuildArcGeometry();

            int gh = ComputeGeomHash();
            if (gh == _lastGeomHash) return;
            _lastGeomHash = gh;

    #if UNITY_EDITOR
            using (kWriteArc.Auto())
    #endif
            WriteSplineAlignedToArc();
        }
    }



    void WriteSplineAlignedToArc()
    {
        var tr = _container.transform;
        var spline = BeginScratch();

        // Degenerate
        if (Radius <= 1e-6f || Mathf.Abs(SweepAngleDeg) < 1e-9f)
        {
            TrackSplineUtils.AddKnotFromWorld(
                spline, tr,
                StartPoint,
                Vector3.zero, Vector3.zero,
                TrackMathUtils.SafeForwardXZ(transform.forward));
            TrackSplineUtils.ReplaceOrAddSpline(_container, spline);
            return;
        }

        Vector3 C = _worldCenter;
        float R = Mathf.Max(0f, Radius);
        float deg = SweepAngleDeg;
        float sweepRad = Mathf.Deg2Rad * deg;

        // segments & per-segment sweep
        int segCount = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(deg) / Mathf.Max(1f, MaxDegreesPerSegment)));
        float segSweepRad = sweepRad / segCount;

        // start angle from center to start point
        Vector3 r0 = StartPoint - C;
        float a = Mathf.Atan2(r0.z, r0.x);
        float cosA = Mathf.Cos(a), sinA = Mathf.Sin(a);

        // rotation step (advance by -segSweepRad each knot; positive deg = CW in your convention)
        float step = -segSweepRad;
        float cosS = Mathf.Cos(step), sinS = Mathf.Sin(step);

        // cubic handle scale for circular arc
        float absSeg = Mathf.Abs(segSweepRad);
        float k = (absSeg > 1e-9f) ? (4f / 3f) * Mathf.Tan(absSeg * 0.25f) : 0f;
        float kR = k * R;

        bool cw = deg >= 0f; // positive = right turn (CW)

        for (int i = 0; i <= segCount; i++)
        {
            // unit radius in XZ
            Vector3 ri = new Vector3(cosA, 0f, sinA);
            Vector3 posW = C + ri * R;

            // tangent direction at this knot (unit)
            Vector3 left = new Vector3(-ri.z, 0f, ri.x);
            Vector3 tDir = cw ? -left : left;

            Vector3 tinW = (i > 0) ? -tDir * kR : Vector3.zero; // incoming
            Vector3 toutW = (i < segCount) ? tDir * kR : Vector3.zero; // outgoing

            TrackSplineUtils.AddKnotFromWorld(spline, tr, posW, tinW, toutW, tDir);

            // advance angle for next knot (avoid extra trig calls)
            if (i < segCount)
            {
                float cosN = cosA * cosS - sinA * sinS;
                float sinN = sinA * cosS + cosA * sinS;
                cosA = cosN; sinA = sinN;

                // periodic re-normalize to limit drift
                if ((i & 31) == 31) // every 32 steps
                {
                    float invLen = 1.0f / Mathf.Sqrt(cosA * cosA + sinA * sinA);
                    cosA *= invLen; sinA *= invLen;
                }
            }
        }

        _ = TrackSplineUtils.ReplaceOrAddSpline(_container, spline);
    }

    // -------- Geometry (analytic) --------
    void BuildArcGeometry()
    {
        // Degenerate: radius 0 → a single point segment
        if (Radius <= 1e-6f || Mathf.Abs(SweepAngleDeg) < 1e-9f)
        {
            _worldCenter = StartPoint;
            _endPoint = StartPoint;
            var fwd0 = TrackMathUtils.SafeForwardXZ(transform.forward);
            StartKnotYDeg = TrackMathUtils.YawDegFromDir(fwd0);
            EndKnotYDeg = StartKnotYDeg;
            return;
        }

        // Start forward projected to XZ
        Vector3 fwdWorld = TrackMathUtils.SafeForwardXZ(transform.forward);
        StartKnotYDeg = TrackMathUtils.YawDegFromDir(fwdWorld);

        // Left vector in XZ plane, center is to left/right depending on sweep sign
        Vector3 left = new Vector3(-fwdWorld.z, 0f, fwdWorld.x);
        bool ccw = SweepAngleDeg >= 0f;
        Vector3 normal = ccw ? -left : left;
        _worldCenter = StartPoint + normal * Radius;

        // Compute end position by rotating the start radius by -sweep around center
        float sweepRad = Mathf.Deg2Rad * SweepAngleDeg;
        Vector3 r0 = StartPoint - _worldCenter;
        float a0 = Mathf.Atan2(r0.z, r0.x);
        float a1 = a0 - sweepRad;

        _endPoint = new Vector3(
            _worldCenter.x + Mathf.Cos(a1) * Radius,
            _worldCenter.y,
            _worldCenter.z + Mathf.Sin(a1) * Radius
        );

        // End yaw = rotate start forward by sweep
        Vector3 endFwd = (Quaternion.AngleAxis(SweepAngleDeg, Vector3.up) * fwdWorld).normalized;
        EndKnotYDeg = TrackMathUtils.YawDegFromDir(endFwd);
    }

    // -------- ITrackSegment: spline / knots / sampling --------
    public bool TryGetSpline(out SplineContainer container, out int splineIndex)
    {
        container = _container;
        splineIndex = 0;
        return container != null && container.Splines != null && container.Splines.Count > 0;
    }

    public IEnumerable<BezierKnot> GetKnotsLocal()
    {
        if (TryGetSpline(out var c, out var idx))
        {
            var s = c.Splines[idx];
            for (int i = 0; i < s.Count; i++) yield return s[i];
        }
    }

    public Vector3 SamplePosition01(float t)
    {
        t = Mathf.Clamp01(t);
        if (TryGetSpline(out var c, out var idx))
        {
            var s = c.Splines[idx];
            return c.transform.TransformPoint(s.EvaluatePosition(t));
        }

        // Analytic fallback
        if (Radius <= 1e-6f) return StartPoint;
        Vector3 r0 = StartPoint - _worldCenter;
        float a0 = Mathf.Atan2(r0.z, r0.x);
        float a = a0 - Mathf.Deg2Rad * SweepAngleDeg * t;
        return new Vector3(
            _worldCenter.x + Mathf.Cos(a) * Radius,
            _worldCenter.y,
            _worldCenter.z + Mathf.Sin(a) * Radius
        );
    }

    public Vector3 SampleTangent01(float t)
    {
        t = Mathf.Clamp01(t);
        if (TryGetSpline(out var c, out var idx))
        {
            var s = c.Splines[idx];
            var tan = s.EvaluateTangent(t);
            return (c.transform.TransformVector(tan)).normalized;
        }

        // Analytic fallback
        if (Radius <= 1e-6f) return TrackMathUtils.SafeForwardXZ(transform.forward);
        Vector3 dir0 = TrackMathUtils.SafeForwardXZ(transform.forward);
        return (Quaternion.AngleAxis(SweepAngleDeg * t, Vector3.up) * dir0).normalized;
    }

    public int ComputeParamHash()
    {
        unchecked
        {
            int h = 17;
            h = h * 31 + ParamVersion;
            h = h * 31 + Radius.GetHashCode();
            h = h * 31 + SweepAngleDeg.GetHashCode();
            return h;
        }
    }
    
    [System.NonSerialized] int _lastGeomHash;

    int ComputeGeomHash()
    {
        unchecked
        {
            int h = 17;
            h = h * 31 + ParamVersion;
            // Quantize the core drivers of geometry
            h = h * 31 + Mathf.RoundToInt(Radius * 10000f);
            h = h * 31 + Mathf.RoundToInt(SweepAngleDeg * 100f);
            // Include start yaw (affects arc orientation), and center
            h = h * 31 + Mathf.RoundToInt(StartKnotYDeg * 100f);
            h = h * 31 + Mathf.RoundToInt(_worldCenter.x * 10000f);
            h = h * 31 + Mathf.RoundToInt(_worldCenter.y * 10000f);
            h = h * 31 + Mathf.RoundToInt(_worldCenter.z * 10000f);
            return h;
        }
    }
}
