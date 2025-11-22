// SplineMinRadiusValidator.cs
#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

[ExecuteAlways]
public class SplineMinRadiusValidator : MonoBehaviour
{
    [Header("Target")]
    public SplineContainer Container;

    [Header("Limits")]
    [Tooltip("Any local radius below this (meters) will be flagged.")]
    public float MinRadius = 20f;

    [Tooltip("Paint near-limit segments in a warning color. E.g., within 10% of the limit.")]
    [Range(0f, 0.5f)] public float WarningBand = 0.10f;

    [Header("Sampling")]
    [Tooltip("Samples per segment (positions evaluated with SplineUtility).")]
    [Range(8, 512)] public int SamplesPerSegment = 64;

    [Header("Gizmo Display")]
    public bool DrawCurve = true;
    public bool DrawViolations = true;
    public float GizmoWidth = 4f;
    public Color OkColor = new Color(0f, 1f, 0f, 0.9f);
    public Color WarnColor = new Color(1f, 0.75f, 0f, 0.95f);
    public Color FailColor = new Color(1f, 0f, 0f, 0.95f);

    [Header("Logging")]
    public bool LogReportOnValidate = true;

    // ------------------- UI -------------------
    [ContextMenu("Validate Now")]
    public void ValidateNow()
    {
        if (Container == null) { Debug.LogWarning($"[{name}] No SplineContainer assigned."); return; }

        var results = new List<SegResult>();
        IterateSplines(Container, (spline, tr, idx) =>
        {
            ValidateSplineBySampling(spline, tr, idx, results);
        });

        if (!LogReportOnValidate) return;

        if (results.Count == 0) { Debug.Log($"[{name}] No segments found."); return; }

        int fails = 0, warns = 0, oks = 0;
        foreach (var r in results) { if (r.Status == SegStatus.Fail) fails++; else if (r.Status == SegStatus.Warn) warns++; else oks++; }
        Debug.Log($"[{name}] Validation: OK={oks}, WARN={warns}, FAIL={fails}, Samples/seg={SamplesPerSegment}, Rmin={MinRadius:0.##}m, warn≤{(1f+WarningBand)*MinRadius:0.##}m.");

        foreach (var r in results)
        {
            string line = $"Spline#{r.SplineIndex} Seg#{r.SegmentIndex}: minR≈{r.MinRadius:0.00} m at t≈{r.TatMin:0.000}";
            if (r.Status == SegStatus.Fail) Debug.LogWarning($"[{name}] FAIL  {line}");
            else if (r.Status == SegStatus.Warn) Debug.Log($"[{name}] WARN  {line}");
            else Debug.Log($"[{name}] OK    {line}");
        }
    }

    void OnDrawGizmos()
    {
        if (!DrawCurve || Container == null) return;

        // Always draw exactly what Unity evaluates (matches Scene preview even while dragging).
        IterateSplines(Container, (spline, tr, splineIndex) =>
        {
            int segCount = GetSegmentCount(spline);
            if (segCount <= 0) return;

            int stepsPerSeg = Mathf.Clamp(SamplesPerSegment, 8, 512);
            float warnCut = MinRadius * (1f + WarningBand);

            // Draw the whole spline as a polyline sampled by SplineUtility
            for (int seg = 0; seg < segCount; seg++)
            {
                // For closed splines, Unity indexes segments circularly.
                float tSegStart = seg / (float)segCount;
                Vector3 prev = EvaluateWorld(spline, tr, tSegStart);

                for (int s = 1; s <= stepsPerSeg; s++)
                {
                    float t = (seg + s / (float)stepsPerSeg) / segCount;
                    Vector3 curr = EvaluateWorld(spline, tr, t);

                    // Base (OK) pass
                    Handles.color = OkColor;
                    Handles.DrawAAPolyLine(GizmoWidth, prev, curr);
                    prev = curr;
                }

                if (DrawViolations)
                {
                    // Overlay with warning/fail colors per micro-segment using local 3-point radius
                    prev = EvaluateWorld(spline, tr, tSegStart);
                    for (int s = 1; s <= stepsPerSeg; s++)
                    {
                        float t0 = (seg + (s - 1) / (float)stepsPerSeg) / segCount;
                        float tm = (seg + (s - 0.5f) / (float)stepsPerSeg) / segCount;
                        float t1 = (seg + s / (float)stepsPerSeg) / segCount;

                        Vector3 a = EvaluateWorld(spline, tr, t0);
                        Vector3 b = EvaluateWorld(spline, tr, tm);
                        Vector3 c = EvaluateWorld(spline, tr, t1);

                        float R = ThreePointRadiusXZ(a, b, c);

                        Color col = OkColor;
                        if (R < MinRadius) col = FailColor;
                        else if (R < warnCut) col = WarnColor;

                        Handles.color = col;
                        Vector3 p0 = a;
                        Vector3 p1 = EvaluateWorld(spline, tr, t1);
                        Handles.DrawAAPolyLine(GizmoWidth, p0, p1);
                    }
                }
            }
        });
    }

    // ------------------- Validation (sampling-based) -------------------

    void ValidateSplineBySampling(Spline spline, Transform tr, int splineIndex, List<SegResult> outResults)
    {
        int segCount = GetSegmentCount(spline);
        if (segCount <= 0) return;

        float warnCut = MinRadius * (1f + WarningBand);
        int stepsPerSeg = Mathf.Clamp(SamplesPerSegment, 8, 512);

        for (int seg = 0; seg < segCount; seg++)
        {
            float minR = float.PositiveInfinity;
            float tAt = 0f;

            for (int s = 1; s <= stepsPerSeg; s++)
            {
                float t0 = (seg + (s - 1) / (float)stepsPerSeg) / segCount;
                float tm = (seg + (s - 0.5f) / (float)stepsPerSeg) / segCount;
                float t1 = (seg + s / (float)stepsPerSeg) / segCount;

                Vector3 a = EvaluateWorld(spline, tr, t0);
                Vector3 b = EvaluateWorld(spline, tr, tm);
                Vector3 c = EvaluateWorld(spline, tr, t1);

                float R = ThreePointRadiusXZ(a, b, c);
                if (R < minR) { minR = R; tAt = tm; }
            }

            SegStatus status = SegStatus.Ok;
            if (minR < MinRadius) status = SegStatus.Fail;
            else if (minR < warnCut) status = SegStatus.Warn;

            outResults.Add(new SegResult
            {
                SplineIndex = splineIndex,
                SegmentIndex = seg,
                MinRadius = minR,
                TatMin = tAt,
                Status = status
            });
        }
    }

    // ------------------- Sampling helpers -------------------

    static int GetSegmentCount(Spline s)
    {
        // For Bezier splines: open => Count-1 segments, closed => Count segments (wrap last->first).
        return s.Closed ? s.Count : Mathf.Max(0, s.Count - 1);
    }

    static Vector3 EvaluateWorld(Spline s, Transform tr, float tGlobal01)
    {
        // Unity’s evaluator: t in [0,1] across the whole spline (segments distributed uniformly).
        // We ask for local-space position, then transform to world (respects parent scale/rotate/translate).
        Vector3 localPos = SplineUtility.EvaluatePosition(s, tGlobal01);
        return tr.TransformPoint(localPos);
    }

    // 3-point circle radius in XZ (robust for small segments)
    static float ThreePointRadiusXZ(Vector3 a3, Vector3 b3, Vector3 c3)
    {
        Vector2 a = new Vector2(a3.x, a3.z);
        Vector2 b = new Vector2(b3.x, b3.z);
        Vector2 c = new Vector2(c3.x, c3.z);
        float A = (b - a).magnitude;
        float B = (c - b).magnitude;
        float C = (a - c).magnitude;
        float s = 0.5f * (A + B + C);
        float area2 = s * (s - A) * (s - B) * (s - C);
        if (area2 <= 1e-12f) return float.PositiveInfinity;
        float area = Mathf.Sqrt(area2);
        return (A * B * C) / (4f * area);
    }

    // ------------------- Container iteration -------------------

    delegate void SplineVisitor(Spline s, Transform tr, int splineIndex);
    static void IterateSplines(SplineContainer c, SplineVisitor visit)
    {
        var tr = c.transform;
        var t = c.GetType();
        var propSplines = t.GetProperty("Splines", BindingFlags.Public | BindingFlags.Instance);
        if (propSplines != null)
        {
            var list = propSplines.GetValue(c, null) as System.Collections.IList;
            if (list != null) { for (int i = 0; i < list.Count; i++) visit((Spline)list[i], tr, i); return; }
        }
        if (c.Spline != null) visit(c.Spline, tr, 0);
    }

    // Result bucket
    struct SegResult
    {
        public int SplineIndex;
        public int SegmentIndex;
        public float MinRadius;
        public float TatMin;
        public SegStatus Status;
    }
    enum SegStatus { Ok, Warn, Fail }
}
#endif
