using UnityEngine;
using UnityEngine.Splines;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class SplineFromArcSimple : MonoBehaviour
{
    public TrackArcHelper Arc;          // auto-assigned if left null
    public SplineContainer Container;   // auto-created if left null
    [Tooltip("Max sweep per cubic piece (deg). 90 is a good default; 60 for extra tight.")]
    [Range(10f, 180f)] public float MaxDegreesPerSegment = 90f;
    public bool AutoRebuild = true;

    void OnValidate() { if (AutoRebuild) Rebuild(); }
    void Update()     { if (AutoRebuild) Rebuild(); }

    [ContextMenu("Rebuild Now")]
    public void Rebuild()
    {
        if (Arc == null) Arc = GetComponent<TrackArcHelper>();
        if (Arc == null) { Debug.LogWarning("[SplineFromArcSimple] No TrackArcHelper found."); return; }

        if (Container == null)
        {
            Container = GetComponent<SplineContainer>();
            if (Container == null) Container = gameObject.AddComponent<SplineContainer>();
        }

        // --- Read arc from helper (CW-positive) ---
        Vector3 C  = Arc.WorldCenter;   // center (world)
        Vector3 P0 = Arc.StartPoint;    // start point (world)
        float   R  = Arc.Radius;
        float   sweepDeg = Arc.SweepAngleDeg;         // signed, CW positive
        float   sweepRad = Mathf.Deg2Rad * sweepDeg;

        // Degenerate: single knot at start
        if (R <= 1e-6f || Mathf.Abs(sweepRad) < 1e-9f)
        {
            var s0  = new Spline();
            var tr0 = Container.transform;
            var k0  = new BezierKnot(
                tr0.InverseTransformPoint(P0),
                Vector3.zero,
                Vector3.zero,
                Quaternion.identity
            );
            s0.Add(k0);
            ReplaceOrAddSpline(Container, s0);
            SetAllKnotsBroken(s0);
            #if UNITY_EDITOR
            SceneView.RepaintAll();
            #endif
            return;
        }

        // Start polar angle about center (Arc uses CW parameterization)
        Vector3 r0 = P0 - C;
        float a0 = Mathf.Atan2(r0.z, r0.x);      // world XZ → angle

        // Segmentation
        int segCount = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(sweepDeg) / Mathf.Max(1f, MaxDegreesPerSegment)));
        float segSweepRad = sweepRad / segCount; // signed per-segment
        bool cw = sweepDeg >= 0f;                // CW-positive

        // Precompute knot positions p[0..N], tangent dirs t[0..N], and per-segment k[0..N-1]
        var p = new Vector3[segCount + 1];
        var t = new Vector3[segCount + 1];   // circle tangent dir at knot i (WORLD)
        var k = new float[segCount];         // per-segment factor

        for (int i = 0; i <= segCount; i++)
        {
            float ai = a0 - i * segSweepRad; // CW if sweep>0
            Vector3 pi = new Vector3(C.x + Mathf.Cos(ai) * R, C.y, C.z + Mathf.Sin(ai) * R);
            p[i] = pi;

            // Circle tangent in WORLD: left = (-rz,0,rx); CW uses right=-left, CCW uses left
            Vector3 ri = pi - C;
            Vector3 left = new Vector3(-ri.z, 0f, ri.x);
            t[i] = (cw ? -left : left).normalized;
        }

        float absSeg = Mathf.Abs(segSweepRad);
        float kCommon = (absSeg > 1e-9f) ? (4f / 3f) * Mathf.Tan(absSeg * 0.25f) : 0f;
        for (int i = 0; i < segCount; i++) k[i] = kCommon;

        // Build spline: set BOTH handles at interior knots and apply knot rotation (LOCAL)
        var spline = new Spline();
        var tr = Container.transform;   // container transform

        for (int i = 0; i <= segCount; i++)
        {
            Vector3 posW = p[i];

            // Desired WORLD handle offsets that make the arc exact
            Vector3 tinW  = Vector3.zero;
            Vector3 toutW = Vector3.zero;

            if (i > 0)          tinW  = -t[i] * (k[i - 1] * R);
            if (i < segCount)   toutW =  t[i] * (k[i] * R);

            // Knot rotation should face along the tangent direction (WORLD)
            Quaternion worldRot = Quaternion.LookRotation(t[i] == Vector3.zero ? Vector3.forward : t[i], Vector3.up);
            // Convert to LOCAL rotation for the container
            Quaternion localRot = Quaternion.Inverse(tr.rotation) * worldRot;

            // Convert WORLD offsets to LOCAL vectors/points
            Vector3 posL   = tr.InverseTransformPoint(posW);
            Vector3 tinL   = tr.InverseTransformVector(tinW);
            Vector3 toutL  = tr.InverseTransformVector(toutW);

            // Counter-rotate LOCAL tangents by the knot's LOCAL rotation so that,
            // after Unity applies knot rotation, the world handles end up at tinW/toutW.
            if (tinL  != Vector3.zero) tinL  = Quaternion.Inverse(localRot) * tinL;
            if (toutL != Vector3.zero) toutL = Quaternion.Inverse(localRot) * toutL;

            var knot = new BezierKnot(posL, tinL, toutL, localRot);
            spline.Add(knot);
        }

        ReplaceOrAddSpline(Container, spline);
        SetAllKnotsBroken(spline);

        #if UNITY_EDITOR
        SceneView.RepaintAll();
        #endif
    }

    static void SetAllKnotsBroken(Spline s)
    {
        for (int i = 0; i < s.Count; i++)
            s.SetTangentMode(i, TangentMode.Broken);
    }

    // Works with Splines 2.x (read-only Splines list) and older API
    static void ReplaceOrAddSpline(SplineContainer c, Spline src)
    {
        if (c == null || src == null) return;

        try
        {
            if (c.Splines != null && c.Splines.Count > 0)
            {
                var dst = c.Splines[0];
                dst.Clear();
                for (int i = 0; i < src.Count; i++)
                    dst.Add(src[i]);
                return;
            }
            c.AddSpline(src);
            return;
        }
        catch
        {
            // Older API fallback
        }

        var t = c.GetType();
        var propSpline = t.GetProperty("Spline", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        if (propSpline != null && propSpline.CanWrite)
        {
            propSpline.SetValue(c, src);
            return;
        }

        var addMethod = t.GetMethod("AddSpline", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance, null, new[] { typeof(Spline) }, null);
        if (addMethod != null) addMethod.Invoke(c, new object[] { src });
    }
}
