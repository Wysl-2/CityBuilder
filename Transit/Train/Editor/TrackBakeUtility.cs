// Assets/MovingPlatforms/Train/Editor/TrackBakeUtility.cs
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics; // quaternion/float3 and math.mul
using Unity.Profiling;


public static class TrackBakeUtility
{
    #if UNITY_EDITOR
    static readonly ProfilerMarker kBakeFull    = new ProfilerMarker("Track/Bake/Full");
    static readonly ProfilerMarker kBakeCollect = new ProfilerMarker("Track/Bake/Collect");   // gather segments/knots
    static readonly ProfilerMarker kBakeEmit    = new ProfilerMarker("Track/Bake/Emit");      // write to SplineContainer
    static readonly ProfilerMarker kBakeFinish  = new ProfilerMarker("Track/Bake/Finalize");  // close flags/dirty/prefab ops
    // If you also build meshes during bake:
    // static readonly ProfilerMarker kBakeMesh = new ProfilerMarker("Track/Bake/Mesh");
    #endif
    private const string kBakedChildName = "BakedTrack";

    [MenuItem("Tools/Track/Bake Full Track (Selected TrackRoot)")]
    public static void BakeFullFromSelection()
    {
        var root = GetSelectedTrackRoot();
        if (root == null)
        {
            EditorUtility.DisplayDialog("Track Bake", "Select a GameObject with a TrackRoot component.", "OK");
            return;
        }

        BakeFull(root);
    }

    // NEW: Bake a range using current selection to pick [start..end]
    [MenuItem("Tools/Track/Bake Range (Use Selection)")]
    public static void BakeRangeFromSelection()
    {
        // Find a TrackRoot from selection (root or any child)
        var root = GetSelectedTrackRoot();
        if (root == null)
        {
            EditorUtility.DisplayDialog("Track Bake (Range)", "Select a TrackRoot or one/more of its segment GameObjects.", "OK");
            return;
        }

        // Gather selected segment indices
        var selected = Selection.gameObjects;
        if (selected == null || selected.Length == 0)
        {
            EditorUtility.DisplayDialog("Track Bake (Range)", "Select one or more segment GameObjects to define the range.", "OK");
            return;
        }

        var list = root.SegmentObjects;
        if (list == null || list.Count == 0)
        {
            EditorUtility.DisplayDialog("Track Bake (Range)", "TrackRoot has no segments.", "OK");
            return;
        }

        int minIdx = int.MaxValue;
        int maxIdx = int.MinValue;
        foreach (var go in selected)
        {
            if (!go) continue;
            // Resolve to segment GO if child component was selected
            var segGO = go.GetComponentInParent<Transform>()?.gameObject;
            int idx = list.IndexOf(segGO);
            if (idx < 0)
            {
                // Not directly a segment—try walking up to any parent in list
                var t = go.transform;
                while (t != null && idx < 0)
                {
                    idx = list.IndexOf(t.gameObject);
                    t = t.parent;
                }
            }
            if (idx >= 0)
            {
                if (idx < minIdx) minIdx = idx;
                if (idx > maxIdx) maxIdx = idx;
            }
        }

        if (minIdx == int.MaxValue || maxIdx == int.MinValue)
        {
            EditorUtility.DisplayDialog("Track Bake (Range)", "No selected objects map to segments under the TrackRoot.", "OK");
            return;
        }

        BakeRange(root, minIdx, maxIdx);
    }

    /// <summary>
    /// Bake all segments under this TrackRoot into a single SplineContainer child.
    /// Joins (including loop join when closed) are merged so the baked join uses
    /// prev.end.tangentIn + next.start.tangentOut.
    /// </summary>
    public static GameObject BakeFull(TrackRoot root)
{
#if UNITY_EDITOR
    using (kBakeFull.Auto())
#endif
    {
        if (root == null)
        {
            Debug.LogError("[TrackBake] Root is null.");
            return null;
        }

        // ------------------------------- Closed-loop validation -------------------------------
        if (root.IsClosedLoop)
        {
            if (root.Count < 2)
            {
                EditorUtility.DisplayDialog(
                    "Track Bake (Closed Loop)",
                    "Track is marked as Closed Loop but has fewer than 2 segments.",
                    "OK");
                return null;
            }

            var (gap, ang) = root.GetLoopClosureError();
            bool ok = root.IsLoopWithinTolerance();
            if (!ok)
            {
                EditorUtility.DisplayDialog(
                    "Track Bake (Closed Loop Invalid)",
                    $"The track is marked as a Closed Loop but the endpoints are not within tolerance.\n\n" +
                    $"Gap:   {gap:0.###} m  (tolerance {root.G0PositionTolerance:0.###})\n" +
                    $"Angle: {ang:0.###}° (tolerance {root.G1AngleToleranceDeg:0.###})\n\n" +
                    "Please adjust the segments and try again.",
                    "OK");
                return null;
            }
        }

        // Ensure/refresh baked target
        var bakedGO = EnsureChild(root.gameObject, kBakedChildName);
        var bakedContainer = bakedGO.GetComponent<SplineContainer>();
        if (bakedContainer == null) bakedContainer = Undo.AddComponent<SplineContainer>(bakedGO);

        // Make sure we have exactly one spline to write into
        Spline targetSpline = EnsureSingleSpline(bakedContainer);
        targetSpline.Clear();

        var dstTr = bakedContainer.transform;

        List<List<WorldKnot>> allSegWorldKnots;
        List<WorldKnot> bakedWorld;
        bool isClosed = root.IsClosedLoop && root.Count >= 2;

#if UNITY_EDITOR
        using (kBakeCollect.Auto())
#endif
        {
            // 1) Gather world-space knots for each segment
            allSegWorldKnots = new List<List<WorldKnot>>();
            for (int i = 0; i < root.Count; i++)
            {
                var seg = root.GetSegment(i);
                if (seg == null)
                {
                    allSegWorldKnots.Add(new List<WorldKnot>());
                    continue;
                }

                if (!ExtractWorldKnots(seg, out var _srcTr, out var wkList) || wkList.Count == 0)
                    wkList = SynthesizeStraightWorld(seg);

                allSegWorldKnots.Add(wkList);
            }

            if (allSegWorldKnots.Count == 0)
            {
                Debug.LogWarning("[TrackBake] No segments to bake.");
                return bakedGO;
            }

            // 2) Build baked world-knot list with join merging (and loop handling)
            bakedWorld = new List<WorldKnot>(256);

            for (int i = 0; i < allSegWorldKnots.Count; i++)
            {
                var curr = allSegWorldKnots[i];
                if (curr.Count == 0) continue;

                if (i == 0)
                {
                    // First segment: add 0..(n-2)
                    int end = Mathf.Max(0, curr.Count - 1);
                    for (int k = 0; k < end; k++)
                        bakedWorld.Add(curr[k]);
                }
                else
                {
                    // Join with previous
                    var prev = allSegWorldKnots[i - 1];
                    if (prev.Count > 0)
                    {
                        var merged = MergeJoin(prev[prev.Count - 1], curr[0]);
                        bakedWorld.Add(merged);
                    }

                    // Add internal knots of current: 1..(n-2)
                    int last = curr.Count - 1;
                    for (int k = 1; k < last; k++)
                        bakedWorld.Add(curr[k]);

                    // If last segment and track is OPEN, add its final knot (n-1)
                    if (i == allSegWorldKnots.Count - 1 && !isClosed)
                        bakedWorld.Add(curr[last]);
                }
            }

            // Loop close bookkeeping
            if (isClosed)
            {
                var firstList = allSegWorldKnots[0];
                var lastList  = allSegWorldKnots[allSegWorldKnots.Count - 1];

                if (firstList.Count > 0 && lastList.Count > 0 && bakedWorld.Count > 0)
                {
                    var loopMerged = MergeJoin(lastList[lastList.Count - 1], firstList[0]);
                    bakedWorld[0] = loopMerged;
                    targetSpline.Closed = true;
                }
                else
                {
                    targetSpline.Closed = false;
                }
            }
            else
            {
                targetSpline.Closed = false;
            }
        }

#if UNITY_EDITOR
        using (kBakeEmit.Auto())
#endif
        {
            // 3) Write bakedWorld into the target spline (convert to baked-local)
            foreach (var wk in bakedWorld)
                AddWorldKnotToTarget(wk, dstTr, targetSpline);
        }

#if UNITY_EDITOR
        using (kBakeFinish.Auto())
#endif
        {
            SetAllKnotsBroken(targetSpline);

            EditorUtility.SetDirty(bakedContainer);
            EditorUtility.SetDirty(bakedGO);
            Debug.Log($"[TrackBake] Baked {root.Count} segment(s) into '{bakedGO.name}'. Closed={targetSpline.Closed}  Knots={targetSpline.Count}");
            Selection.activeObject = bakedGO;
        }

        return bakedGO;
    }
}


    // NEW: Bake a range [startIndex..endIndex] as an OPEN spline (no loop close).
public static GameObject BakeRange(TrackRoot root, int startIndex, int endIndex)
{
#if UNITY_EDITOR
    using (kBakeFull.Auto()) // reuse top-level marker name or make a kBakeRange if you prefer
#endif
    {
        if (root == null)
        {
            Debug.LogError("[TrackBake] Root is null.");
            return null;
        }
        if (root.Count == 0)
        {
            EditorUtility.DisplayDialog("Track Bake (Range)", "Track has no segments.", "OK");
            return null;
        }

        // Clamp & validate
        startIndex = Mathf.Clamp(startIndex, 0, root.Count - 1);
        endIndex   = Mathf.Clamp(endIndex,   0, root.Count - 1);
        if (endIndex < startIndex)
        {
            int t = startIndex; startIndex = endIndex; endIndex = t;
        }

        // Ensure/refresh baked target
        string childName = $"{kBakedChildName}_{startIndex:000}_{endIndex:000}";
        var bakedGO = EnsureChild(root.gameObject, childName);
        var bakedContainer = bakedGO.GetComponent<SplineContainer>();
        if (bakedContainer == null) bakedContainer = Undo.AddComponent<SplineContainer>(bakedGO);

        Spline targetSpline = EnsureSingleSpline(bakedContainer);
        targetSpline.Clear();
        targetSpline.Closed = false; // Range is always open

        var dstTr = bakedContainer.transform;

        List<List<WorldKnot>> segWorld;
        List<WorldKnot> bakedWorld;

#if UNITY_EDITOR
        using (kBakeCollect.Auto())
#endif
        {
            // Gather per-segment world knots ONLY for the range
            segWorld = new List<List<WorldKnot>>();
            for (int i = startIndex; i <= endIndex; i++)
            {
                var seg = root.GetSegment(i);
                if (seg == null)
                {
                    segWorld.Add(new List<WorldKnot>());
                    continue;
                }

                if (!ExtractWorldKnots(seg, out var _srcTr, out var wkList) || wkList.Count == 0)
                    wkList = SynthesizeStraightWorld(seg);

                segWorld.Add(wkList);
            }

            if (segWorld.Count == 0)
            {
                Debug.LogWarning("[TrackBake] Range produced no data.");
                return bakedGO;
            }

            // Merge joins (no loop merge for range)
            bakedWorld = new List<WorldKnot>(128);

            for (int i = 0; i < segWorld.Count; i++)
            {
                var curr = segWorld[i];
                if (curr.Count == 0) continue;

                if (i == 0)
                {
                    int end = Mathf.Max(0, curr.Count - 1);
                    for (int k = 0; k < end; k++)
                        bakedWorld.Add(curr[k]);
                }
                else
                {
                    var prev = segWorld[i - 1];
                    if (prev.Count > 0)
                    {
                        var merged = MergeJoin(prev[prev.Count - 1], curr[0]);
                        bakedWorld.Add(merged);
                    }

                    int last = curr.Count - 1;
                    for (int k = 1; k < last; k++)
                        bakedWorld.Add(curr[k]);

                    if (i == segWorld.Count - 1)
                        bakedWorld.Add(curr[last]); // include final knot at end of range
                }
            }
        }

#if UNITY_EDITOR
        using (kBakeEmit.Auto())
#endif
        {
            foreach (var wk in bakedWorld)
                AddWorldKnotToTarget(wk, dstTr, targetSpline);
        }

#if UNITY_EDITOR
        using (kBakeFinish.Auto())
#endif
        {
            SetAllKnotsBroken(targetSpline);

            EditorUtility.SetDirty(bakedContainer);
            EditorUtility.SetDirty(bakedGO);
            Debug.Log($"[TrackBake] Baked RANGE [{startIndex}..{endIndex}] into '{bakedGO.name}'. Knots={targetSpline.Count}");
            Selection.activeObject = bakedGO;
        }

        return bakedGO;
    }
}


    // =====================================================================
    // World-knot representation + conversions
    // =====================================================================

    private struct WorldKnot
    {
        public Vector3 posW;
        public Quaternion rotW;
        public Vector3 tinW;   // world-space incoming handle vector
        public Vector3 toutW;  // world-space outgoing handle vector
    }

    /// <summary>
    /// Pull the segment's knots (in its own container-local space) and convert to world-space.
    /// </summary>
    private static bool ExtractWorldKnots(ITrackSegment seg, out Transform srcTr, out List<WorldKnot> worldKnots)
    {
        worldKnots = new List<WorldKnot>();
        srcTr = null;

        if (!seg.TryGetSpline(out var sourceContainer, out int _index) || sourceContainer == null)
            return false;

        srcTr = sourceContainer.transform;
        var knotsEnum = seg.GetKnotsLocal();
        if (knotsEnum == null) return false;

        foreach (var k in knotsEnum)
        {
            // Knot rotation (Unity quaternion from math.quaternion)
            Quaternion kRotUnity = ToUnityQuaternion(k.Rotation);

            // World rotation of the knot
            Quaternion worldRot = srcTr.rotation * kRotUnity;

            // Position: local -> world
            Vector3 posW = srcTr.TransformPoint((Vector3)k.Position);

            // Tangents: rotate by knot rotation in source local, then to world
            Vector3 tinRotLcl  = RotateBy(k.Rotation, (Vector3)k.TangentIn);
            Vector3 toutRotLcl = RotateBy(k.Rotation, (Vector3)k.TangentOut);

            Vector3 tinW  = srcTr.TransformVector(tinRotLcl);
            Vector3 toutW = srcTr.TransformVector(toutRotLcl);

            worldKnots.Add(new WorldKnot
            {
                posW  = posW,
                rotW  = worldRot,
                tinW  = tinW,
                toutW = toutW
            });
        }

        return true;
    }

    /// <summary>
    /// Synthetic two-knot straight if a segment does not expose knots.
    /// </summary>
    private static List<WorldKnot> SynthesizeStraightWorld(ITrackSegment seg)
    {
        var list = new List<WorldKnot>(2);
        Vector3 p0 = seg.StartPoint;
        Vector3 p1 = seg.EndPoint;

        Vector3 dir = (p1 - p0);
        if (dir.sqrMagnitude < 1e-10f)
        {
            // Single point
            list.Add(new WorldKnot
            {
                posW = p0,
                rotW = Quaternion.identity,
                tinW = Vector3.zero,
                toutW = Vector3.zero
            });
            return list;
        }

        dir.Normalize();
        Quaternion rot = Quaternion.LookRotation(dir, Vector3.up);

        list.Add(new WorldKnot
        {
            posW = p0,
            rotW = rot,
            tinW = Vector3.zero,
            toutW = Vector3.zero
        });
        list.Add(new WorldKnot
        {
            posW = p1,
            rotW = rot,
            tinW = Vector3.zero,
            toutW = Vector3.zero
        });
        return list;
    }

    /// <summary>
    /// Merge the overlapping join between prevEnd and nextStart into a single world-space knot.
    /// tangentIn comes from prevEnd.tinW; tangentOut comes from nextStart.toutW.
    /// Rotation is chosen from handles (bisector heuristic) with robust fallbacks.
    /// </summary>
    private static WorldKnot MergeJoin(WorldKnot prevEnd, WorldKnot nextStart)
    {
        // Pick position (snap to previous end; average if drift is large)
        Vector3 p = prevEnd.posW;
        if ((nextStart.posW - prevEnd.posW).sqrMagnitude > 1e-6f)
            p = Vector3.Lerp(prevEnd.posW, nextStart.posW, 0.5f);

        Vector3 inVec  = prevEnd.tinW;
        Vector3 outVec = nextStart.toutW;

        bool hasIn  = inVec.sqrMagnitude  > 1e-10f;
        bool hasOut = outVec.sqrMagnitude > 1e-10f;

        // Rotation heuristic:
        // - If both handles: aim along bisector of directions (incoming direction is -inVec)
        // - Else if one handle: copy the corresponding segment rotation
        // - Else: keep previous end rotation
        Quaternion rot;
        if (hasIn && hasOut)
        {
            Vector3 din  = (-inVec).normalized;
            Vector3 dout = (outVec).normalized;
            Vector3 d = din + dout;
            if (d.sqrMagnitude < 1e-12f) d = dout; // opposite handles: pick outgoing
            rot = Quaternion.LookRotation(d.normalized, Vector3.up);
        }
        else if (hasOut)
        {
            rot = nextStart.rotW;
        }
        else if (hasIn)
        {
            rot = prevEnd.rotW;
        }
        else
        {
            rot = prevEnd.rotW;
        }

        return new WorldKnot
        {
            posW  = p,
            rotW  = rot,
            tinW  = inVec,
            toutW = outVec
        };
    }

    /// <summary>
    /// Add a world-space knot to the baked (dst) container/local spline,
    /// converting pos/rot/tangents to baked-local and counter-rotating the handles.
    /// </summary>
    private static void AddWorldKnotToTarget(WorldKnot wk, Transform dstTr, Spline target)
    {
        Quaternion localRot = Quaternion.Inverse(dstTr.rotation) * wk.rotW;

        Vector3 posL  = dstTr.InverseTransformPoint(wk.posW);
        Vector3 tinL  = dstTr.InverseTransformVector(wk.tinW);
        Vector3 toutL = dstTr.InverseTransformVector(wk.toutW);

        if (tinL  != Vector3.zero)  tinL  = Quaternion.Inverse(localRot) * tinL;
        if (toutL != Vector3.zero)  toutL = Quaternion.Inverse(localRot) * toutL;

        target.Add(new BezierKnot(posL, tinL, toutL, localRot));
    }

    // =====================================================================
    // Boilerplate / Utilities
    // =====================================================================

    private static TrackRoot GetSelectedTrackRoot()
    {
        var go = Selection.activeGameObject;
        return go ? go.GetComponentInParent<TrackRoot>() : null;
    }

    private static GameObject EnsureChild(GameObject parent, string childName)
    {
        Transform t = parent.transform.Find(childName);
        GameObject child;
        if (t == null)
        {
            child = new GameObject(childName);
            Undo.RegisterCreatedObjectUndo(child, "Create BakedTrack");
            Undo.SetTransformParent(child.transform, parent.transform, "Parent BakedTrack");
            child.transform.localPosition = Vector3.zero;
            child.transform.localRotation = Quaternion.identity;
            child.transform.localScale = Vector3.one;
        }
        else
        {
            child = t.gameObject;
        }
        return child;
    }

    private static Spline EnsureSingleSpline(SplineContainer container)
    {
        if (container.Splines != null && container.Splines.Count > 0)
            return container.Splines[0];

        var s = new Spline();
        container.AddSpline(s);
        return s;
    }

    private static void SetAllKnotsBroken(Spline s)
    {
        for (int i = 0; i < s.Count; i++)
            s.SetTangentMode(i, TangentMode.Broken);
    }

    // -------- math helpers --------

    private static Quaternion ToUnityQuaternion(quaternion q)
    {
        return new Quaternion(q.value.x, q.value.y, q.value.z, q.value.w);
    }

    private static Vector3 RotateBy(quaternion q, Vector3 v)
    {
        float3 r = math.mul(q, new float3(v.x, v.y, v.z));
        return new Vector3(r.x, r.y, r.z);
    }
}
