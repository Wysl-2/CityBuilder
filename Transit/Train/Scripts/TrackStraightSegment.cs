// Assets/MovingPlatforms/Train/Scripts/TrackStraightSegment.cs
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;
using Unity.Profiling;



[ExecuteAlways]
[RequireComponent(typeof(SplineContainer))]
public class TrackStraightSegment : MonoBehaviour, ITrackSegment
{
    #if UNITY_EDITOR
    static readonly ProfilerMarker kRebuild      = new ProfilerMarker("Track/Straight/Rebuild");
    static readonly ProfilerMarker kComputeLine  = new ProfilerMarker("Track/Straight/ComputeEndpoints");
    static readonly ProfilerMarker kWriteLine    = new ProfilerMarker("Track/Straight/WriteSpline");
    #endif

    [System.NonSerialized] private Spline _scratch;
    private Spline BeginScratch()
    {
        if (_scratch == null) _scratch = new Spline();
        else _scratch.Clear();
        return _scratch;
    }

    public enum BuildMode { Endpoints, Length }
    // ------------ Authoring ------------
    [Header("Definition")]
    public BuildMode Mode = BuildMode.Length;

    [Tooltip("When Mode = Endpoints, these world positions define the line.")]
    public Vector3 StartPosition;

    [Tooltip("When Mode = Endpoints, these world positions define the line.")]
    public Vector3 EndPosition;

    [Tooltip("When Mode = Length, the line starts at this transform's position and goes along +forward.")]
    [Min(0f)] public float Length = 10f;   // authoring length (keep name as requested)

    [Tooltip("Rebuild automatically in Edit/Play Mode when values change.")]
    public bool AutoRebuild = true;
    [Tooltip("Also rebuild automatically when in Play Mode.")]
    public bool AutoRebuildInPlay = false;


    // ------------ Outputs / internals ------------
    [Header("Output (read-only)")]
    [SerializeField] private Vector3 _startW;
    [SerializeField] private Vector3 _endW;
    [SerializeField] private float _computedLength;

    private SplineContainer _container;

    // ---- Identity / versioning (for baking/provenance) ----
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
    public string SegmentType => "Straight";
    public int ParamVersion => 1;

    // ------------ ITrackSegment surface ------------
    public Vector3 StartWorld => _startW;
    public Vector3 EndWorld => _endW;

    public Vector3 StartPoint => _startW;
    public Vector3 EndPoint => _endW;

    public Quaternion StartRotation
    {
        get
        {
            var d = (_endW - _startW);
            if (d.sqrMagnitude < TrackMathUtils.Epsilon) return Quaternion.identity;
            return Quaternion.LookRotation(d.normalized, Vector3.up);
        }
    }
    public Quaternion EndRotation => StartRotation;

    // Interface Length = computed distance (not the authoring Length field)
    float ITrackSegment.Length => _computedLength;

    public Bounds WorldBounds
    {
        get
        {
            var b = new Bounds(_startW, Vector3.zero);
            b.Encapsulate(_endW);
            b.Expand(new Vector3(0.05f, 0.05f, 0.05f)); // tiny thickness
            return b;
        }
    }

    // ------------ Lifecycle ------------
    void Awake()
    {
        _container = GetComponent<SplineContainer>();
        if (string.IsNullOrEmpty(_segmentGuid)) _segmentGuid = Guid.NewGuid().ToString("N");
    }

    void OnValidate()
    {
        if (_container == null) _container = GetComponent<SplineContainer>();
        Length = Mathf.Max(0f, Length);

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

        // In Play Mode, rebuild only if opted-in and the transform actually changed
        if (Application.isPlaying && AutoRebuildInPlay && transform.hasChanged)
        {
            transform.hasChanged = false;
            Rebuild();
        }
    }

    // ------------ Build ------------
    [ContextMenu("Rebuild Now")]
    public void Rebuild()
    {
    #if UNITY_EDITOR
        using (kRebuild.Auto())
    #endif
        {
    #if UNITY_EDITOR
            using (kComputeLine.Auto())
    #endif
            ComputeLineEndpoints();

    #if UNITY_EDITOR
            using (kWriteLine.Auto())
    #endif
            WriteSplineForLine();
        }
    }


    void ComputeLineEndpoints()
    {
        if (Mode == BuildMode.Endpoints)
        {
            _startW = StartPosition;
            _endW = EndPosition;
        }
        else
        {
            _startW = transform.position;
            var dir = TrackMathUtils.SafeForwardXZ(transform.forward);
            _endW = _startW + dir * Length;
        }

        _computedLength = Vector3.Distance(_startW, _endW);
    }

    void WriteSplineForLine()
    {
        if (_container == null) return;

        var tr = _container.transform;
        var spline = BeginScratch();

        Vector3 d = _endW - _startW;

        // Degenerate: single knot
        if (d.sqrMagnitude < TrackMathUtils.Epsilon)
        {
            TrackSplineUtils.AddKnotFromWorld(
                spline, tr,
                _startW,
                Vector3.zero, Vector3.zero,
                TrackMathUtils.SafeForwardXZ(transform.forward));

            _ = TrackSplineUtils.ReplaceOrAddSpline(_container, spline);
            return;
        }

        Vector3 fwd = d.normalized;

        // Start knot (zero tangents)
        TrackSplineUtils.AddKnotFromWorld(
            spline, tr,
            _startW,
            Vector3.zero, Vector3.zero,
            fwd);

        // End knot (zero tangents)
        TrackSplineUtils.AddKnotFromWorld(
            spline, tr,
            _endW,
            Vector3.zero, Vector3.zero,
            fwd);

        _ = TrackSplineUtils.ReplaceOrAddSpline(_container, spline);
    }

    // ------------ ITrackSegment: spline / knots / sampling ------------
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
            for (int i = 0; i < s.Count; i++)
                yield return s[i];
        }
    }

    public Vector3 SamplePosition01(float t)
    {
        t = Mathf.Clamp01(t);
        return Vector3.Lerp(_startW, _endW, t);
    }

    public Vector3 SampleTangent01(float t)
    {
        var d = (_endW - _startW);
        if (d.sqrMagnitude < TrackMathUtils.Epsilon) return Vector3.forward;
        return d.normalized;
    }

    public int ComputeParamHash()
    {
        unchecked
        {
            int h = 17;
            h = h * 31 + ParamVersion;
            h = h * 31 + Mode.GetHashCode();
            h = h * 31 + StartPosition.GetHashCode();
            h = h * 31 + EndPosition.GetHashCode();
            h = h * 31 + Length.GetHashCode(); // authoring Length
            return h;
        }
    }
    
[System.NonSerialized] int _lastGeomHash;

int ComputeGeomHash()
{
    unchecked
    {
        int h = 17;
        h = h * 31 + ParamVersion; // <— include versioning so future changes invalidate

        // Always include endpoints (post ComputeLineEndpoints)
        Vector3 a = _startW, b = _endW;
        h = h * 31 + Mathf.RoundToInt(a.x * 10000f);
        h = h * 31 + Mathf.RoundToInt(a.y * 10000f);
        h = h * 31 + Mathf.RoundToInt(a.z * 10000f);
        h = h * 31 + Mathf.RoundToInt(b.x * 10000f);
        h = h * 31 + Mathf.RoundToInt(b.y * 10000f);
        h = h * 31 + Mathf.RoundToInt(b.z * 10000f);

        // NEW: when authoring by world endpoints, container transform affects local knots
        if (Mode == BuildMode.Endpoints)
        {
            var tr = _container ? _container.transform : transform;

            // Position (mm precision)
            var p = tr.position;
            h = h * 31 + Mathf.RoundToInt(p.x * 1000f);
            h = h * 31 + Mathf.RoundToInt(p.y * 1000f);
            h = h * 31 + Mathf.RoundToInt(p.z * 1000f);

            // Rotation (0.1° precision)
            var e = tr.rotation.eulerAngles;
            h = h * 31 + Mathf.RoundToInt(e.x * 10f);
            h = h * 31 + Mathf.RoundToInt(e.y * 10f);
            h = h * 31 + Mathf.RoundToInt(e.z * 10f);

            // Scale (0.1% precision) — only if you allow non-unit scale
            var s = tr.lossyScale;
            h = h * 31 + Mathf.RoundToInt(s.x * 1000f);
            h = h * 31 + Mathf.RoundToInt(s.y * 1000f);
            h = h * 31 + Mathf.RoundToInt(s.z * 1000f);
        }

        return h;
    }
}

}
