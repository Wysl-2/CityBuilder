using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor; // for Handles.Label
#endif

public struct KnotPoint
{
    Vector3 Position;
    float YRotation;
}

[ExecuteAlways]
public class TrackArcHelper : MonoBehaviour
{
    [Header("Arc Definition")]
    [Min(0f)] public float Radius = 10f;

    [Tooltip("Use this object's forward as the start heading (XZ projected). If false, use StartHeadingDeg.")]
    public bool UseTransformForward = true;

    [Tooltip("Start tangent heading in degrees (0° = +X, 90° = +Z). Only used if UseTransformForward=false.")]
    public float StartHeadingDeg = 0f;

    [Tooltip("Arc sweep (degrees). Positive = CCW (left turn), Negative = CW (right turn).")]
    public float SweepAngleDeg = 90f;

    [Header("Resolution Mode")]
    [Tooltip("If ON, points are spaced ~TargetSpacing meters apart regardless of radius. If OFF, uses a fixed Segments count.")]
    public bool UseFixedSpacing = true;

    [Tooltip("Desired spacing (meters) between consecutive points when UseFixedSpacing is ON.")]
    [Min(0.01f)] public float TargetSpacing = 1f;

    [Tooltip("Minimum segments when using fixed spacing (safety floor).")]
    [Min(1)] public int MinSegments = 1;

    [Tooltip("Maximum segments when using fixed spacing (safety cap). Set very high to effectively disable.")]
    [Min(1)] public int MaxSegments = 2048;

    [Tooltip("If OFF, uses this exact segment count instead of spacing.")]
    [Min(1)] public int Segments = 16;

    [Header("Debug / Display")]
    public bool DrawCenter = true;
    public bool DrawPoints = true;
    public bool DrawLabels = false;
    public Color ArcColor = Color.green;
    public Color PointColor = Color.red;
    public Color CenterColor = Color.cyan;
    public float PointGizmoSize = 0.075f;

      [Header("Output (read-only)")]
    [SerializeField] private Vector3 _worldCenter;
    [SerializeField] private List<Vector3> _points = new List<Vector3>();
    [SerializeField] private List<float> _anglesRad = new List<float>();
    [SerializeField] private List<float> _progressDeg = new List<float>();

    // ADDED: world-space yaw for start/end knots (degrees, 0=+Z, 90=+X)
    [SerializeField] public float StartKnotYDeg;
    [SerializeField] public float EndKnotYDeg;

    public Vector3 StartPoint => transform.position;
    public Vector3 WorldCenter => _worldCenter;
    public IReadOnlyList<Vector3> Points => _points;
    public IReadOnlyList<float> AnglesRad => _anglesRad;
    public IReadOnlyList<float> ProgressDeg => _progressDeg;

    // OPTIONAL: convenience to fetch end point
    public Vector3 EndPoint => (_points != null && _points.Count > 0) ? _points[_points.Count - 1] : StartPoint;

    void OnValidate()
    {
        Radius = Mathf.Max(0f, Radius);
        TargetSpacing = Mathf.Max(0.01f, TargetSpacing);
        MinSegments = Mathf.Max(1, MinSegments);
        MaxSegments = Mathf.Max(MinSegments, MaxSegments);
        Segments = Mathf.Max(1, Segments);
        Rebuild();
    }

    void Update() => Rebuild();

    void Rebuild()
    {
        _points.Clear();
        _anglesRad.Clear();
        _progressDeg.Clear();

        if (Radius <= 0f)
        {
            _worldCenter = StartPoint;
            _points.Add(StartPoint);
            _anglesRad.Add(0f);
            _progressDeg.Add(0f);

            // ADDED: start/end yaw are the same if no arc
            Vector3 fwd0 = ProjectToXZ(transform.forward);
            StartKnotYDeg = EndKnotYDeg = YawDegFromDir(fwd0);
            return;
        }

        // 1) Start tangent in XZ (this IS your start knot direction)
        Vector3 fwdWorld = UseTransformForward ? ProjectToXZ(transform.forward) : DirFromHeadingDeg(StartHeadingDeg);
        if (fwdWorld.sqrMagnitude < 1e-10f) fwdWorld = Vector3.right;
        fwdWorld.Normalize();

        // ADDED: compute start knot yaw now
        StartKnotYDeg = YawDegFromDir(fwdWorld);

        // 2) Center from start point, radius, and sweep sign
        Vector3 left = new Vector3(-fwdWorld.z, 0f, fwdWorld.x);
        bool ccw = SweepAngleDeg >= 0f;
        Vector3 normal = ccw ? -left : left;
        _worldCenter = StartPoint + normal * Radius;

        // 3) Determine segment count
        float sweepRad = Mathf.Deg2Rad * SweepAngleDeg;
        float arcLen = Mathf.Abs(sweepRad) * Radius;

        int segCount;
        if (Mathf.Approximately(sweepRad, 0f))
        {
            segCount = 1;
        }
        else if (UseFixedSpacing)
        {
            segCount = Mathf.CeilToInt(arcLen / TargetSpacing);
            segCount = Mathf.Clamp(segCount, MinSegments, MaxSegments);
        }
        else
        {
            segCount = Segments;
        }

        // 4) Generate points
        int count = segCount + 1;
        Vector3 r0 = StartPoint - _worldCenter;
        float a0 = Mathf.Atan2(r0.z, r0.x);

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / segCount;     // 0..1
            float a = a0 - sweepRad * t;       // absolute polar angle
            Vector3 p = new Vector3(
                _worldCenter.x + Mathf.Cos(a) * Radius,
                _worldCenter.y,
                _worldCenter.z + Mathf.Sin(a) * Radius
            );

            _points.Add(p);
            _anglesRad.Add(NormalizeAngleRad(a));
            _progressDeg.Add(t * SweepAngleDeg);
        }

        // ADDED: end knot yaw = start yaw rotated by sweep (world space)
        Vector3 endDir = Quaternion.AngleAxis(SweepAngleDeg, Vector3.up) * fwdWorld;
        EndKnotYDeg = YawDegFromDir(endDir);
    }

    static Vector3 ProjectToXZ(Vector3 v) { v.y = 0f; return v; }

    static Vector3 DirFromHeadingDeg(float deg)
    {
        float rad = Mathf.Deg2Rad * deg;
        return new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad));
    }

    static float NormalizeAngleRad(float a)
    {
        while (a <= -Mathf.PI) a += 2f * Mathf.PI;
        while (a >  Mathf.PI)  a -= 2f * Mathf.PI;
        return a;
    }

    // ADDED: convert world direction -> yaw degrees where 0°=+Z, 90°=+X, 180°=-Z, -90°/270°=+X
    static float YawDegFromDir(Vector3 dir)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude < 1e-12f) return 0f;
        float deg = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg; // note: x,z order matches your convention
        // normalize to [0,360)
        if (deg < 0f) deg += 360f;
        return deg;
    }

    void OnDrawGizmos()
    {
        if (_points == null || _points.Count == 0) return;

        // Arc
        Color old = Gizmos.color;
        Gizmos.color = ArcColor;
        for (int i = 0; i < _points.Count - 1; i++)
            Gizmos.DrawLine(_points[i], _points[i + 1]);

        // Start & center
        Gizmos.color = PointColor;
        Gizmos.DrawWireSphere(StartPoint, PointGizmoSize * 1.25f);

        if (DrawCenter)
        {
            Gizmos.color = CenterColor;
            Gizmos.DrawWireSphere(_worldCenter, PointGizmoSize * 1.25f);
        }

        // Points
        if (DrawPoints)
        {
            Gizmos.color = PointColor;
            foreach (var p in _points) Gizmos.DrawWireSphere(p, PointGizmoSize);
        }
        Gizmos.color = old;

        #if UNITY_EDITOR
        if (DrawLabels)
        {
            var sv = SceneView.lastActiveSceneView ?? SceneView.currentDrawingSceneView;
            if (sv != null && sv.camera != null)
            {
                for (int i = 0; i < _points.Count; i++)
                {
                    string label = $"i={i}\nθ={_anglesRad[i]*Mathf.Rad2Deg:0.#}°\nΔ={_progressDeg[i]:+0.#;-0.#;0}°";
                    Vector3 pos = _points[i];
                    Vector3 camDir = (pos - sv.camera.transform.position).normalized;
                    pos -= camDir * 0.03f;
                    Handles.Label(pos, label, new GUIStyle(EditorStyles.boldLabel){ normal = { textColor = Color.yellow }});
                }
            }
        }
        #endif
    }
}
