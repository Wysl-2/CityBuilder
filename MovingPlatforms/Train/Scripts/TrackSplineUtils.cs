// Assets/MovingPlatforms/Train/Scripts/Util/TrackSharedUtils.cs
using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;
using Unity.Profiling;

public static class TrackSplineUtils
{
    #if UNITY_EDITOR
    static readonly ProfilerMarker kReplace        = new ProfilerMarker("Track/Util/ReplaceOrAddSpline");
    static readonly ProfilerMarker kEqualCheck     = new ProfilerMarker("Track/Util/SplineApproximatelyEqual");
    static readonly ProfilerMarker kCopyKnots      = new ProfilerMarker("Track/Util/CopyKnots");
    #endif

    /// Sets all tangent modes to Broken so explicit handles are respected.
    public static void SetAllKnotsBroken(Spline s)
    {
        if (s == null) return;
        for (int i = 0; i < s.Count; i++)
            s.SetTangentMode(i, TangentMode.Broken);
    }

    // --- NEW: approximate comparer (tolerates float jitter) ---
    static bool SplineApproximatelyEqual(
    Spline a, Spline b,
    float posEps = 1e-6f,
    float tanEps = 1e-6f,
    float rotDotEps = 1e-4f,
    bool checkClosed = true)
    {
        if (a == null || b == null) return false;
        if (checkClosed && a.Closed != b.Closed) return false;
        int count = a.Count;
        if (count != b.Count) return false;

        float posEpsSq = posEps * posEps;
        float tanEpsSq = tanEps * tanEps;

        for (int i = 0; i < count; i++)
        {
            var ka = a[i];
            var kb = b[i];

            // float3 comparisons (math.lengthsq)
            if (math.lengthsq(ka.Position   - kb.Position)   > posEpsSq) return false;
            if (math.lengthsq(ka.TangentIn  - kb.TangentIn)  > tanEpsSq) return false;
            if (math.lengthsq(ka.TangentOut - kb.TangentOut) > tanEpsSq) return false;

            // quaternion comparison via 4D dot (use absolute to ignore hemisphere)
            float d = math.abs(math.dot(ka.Rotation.value, kb.Rotation.value)); // dot in R^4
            if (1f - d > rotDotEps) return false;
        }
        return true;
    }

    /// Replace (or add) the first spline in a container. Splines 2.x only.
    public static Spline ReplaceOrAddSpline(SplineContainer c, Spline src)
    {
        if (!c || src == null) return null;

    #if UNITY_EDITOR
        using (kReplace.Auto())
    #endif
        {
            Spline dst;
            var list = c.Splines;

            if (list != null && list.Count > 0)
            {
                dst = list[0];

    #if UNITY_EDITOR
                using (kEqualCheck.Auto())
    #endif
                {
                    if (SplineApproximatelyEqual(dst, src))
                        return dst;
                }

                dst.Clear();
            }
            else
            {
                dst = new Spline();
    #if UNITY_EDITOR
                UnityEditor.Undo.RecordObject(c, "Track: Add Spline");
    #endif
                c.AddSpline(dst);
            }

    #if UNITY_EDITOR
            using (kCopyKnots.Auto())
    #endif
            {
                for (int i = 0; i < src.Count; i++)
                    dst.Add(src[i]);
            }

            SetAllKnotsBroken(dst);

    #if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(c);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(c.gameObject.scene);
            if (UnityEditor.PrefabUtility.IsPartOfPrefabInstance(c))
                UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(c);
    #endif
            return dst;
        }
    }


    /// Add a knot to `target` given WORLD inputs (pos/handles/forward) and a container transform.
    /// Handles proper local rotation and counter-rotation of handles.
    public static void AddKnotFromWorld(
        Spline target, Transform container,
        Vector3 posW, Vector3 tangentInW, Vector3 tangentOutW, Vector3 forwardW)
    {
        if (target == null || container == null) return;

        // Robust forward
        var fwd = forwardW; fwd.y = 0f;
        if (fwd.sqrMagnitude < 1e-10f) fwd = Vector3.forward;
        fwd.Normalize();

        // Build local rotation that looks along world forward
        Quaternion knotWorldRot = Quaternion.LookRotation(fwd, Vector3.up);
        Quaternion knotLocalRot = Quaternion.Inverse(container.rotation) * knotWorldRot;

        // Convert to container local
        Vector3 posL  = container.InverseTransformPoint(posW);
        Vector3 tinL  = container.InverseTransformVector(tangentInW);
        Vector3 toutL = container.InverseTransformVector(tangentOutW);

        // Counter-rotate handles so Unity re-applies via knot rotation
        if (tinL  != Vector3.zero)  tinL  = Quaternion.Inverse(knotLocalRot) * tinL;
        if (toutL != Vector3.zero)  toutL = Quaternion.Inverse(knotLocalRot) * toutL;

        target.Add(new BezierKnot(posL, tinL, toutL, knotLocalRot));
    }
}

public static class TrackMathUtils
{
    public const float Epsilon = 1e-10f;

    public static Vector3 ProjectToXZ(Vector3 v) { v.y = 0f; return v; }

    public static Vector3 SafeForwardXZ(Vector3 forward)
    {
        var f = forward; f.y = 0f;
        if (f.sqrMagnitude < Epsilon) f = Vector3.forward;
        return f.normalized;
    }

    public static float NormalizeAngleRad(float a)
    {
        while (a <= -Mathf.PI) a += 2f * Mathf.PI;
        while (a >   Mathf.PI) a -= 2f * Mathf.PI;
        return a;
    }

    public static float YawDegFromDir(Vector3 dir)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude < Epsilon) return 0f;
        float deg = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        if (deg < 0f) deg += 360f;
        return deg;
    }

    public static bool NearlyZero(Vector3 v, float eps = Epsilon) => v.sqrMagnitude <= eps;
    public static float ClampMin(float v, float min) => (v < min) ? min : v;
}
