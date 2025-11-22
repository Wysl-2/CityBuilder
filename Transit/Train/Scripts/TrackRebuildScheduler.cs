#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Unity.Profiling;

[InitializeOnLoad]
public static class TrackRebuildScheduler
{
    #if UNITY_EDITOR
    static readonly ProfilerMarker kSchedUpdate = new ProfilerMarker("Track/Scheduler/Update");
    static readonly ProfilerMarker kSchedSort   = new ProfilerMarker("Track/Scheduler/Sort");
    static readonly ProfilerMarker kSchedRebld  = new ProfilerMarker("Track/Scheduler/RebuildOne");
    static readonly ProfilerMarker kSchedPaint  = new ProfilerMarker("Track/Scheduler/Repaint");
    #endif

    static readonly HashSet<ITrackSegment> s_pending = new HashSet<ITrackSegment>();
    static double s_nextRun = -1;
    const double kDebounceSeconds = 0.05; // ~50ms
    static bool s_repaintRequested;
    static bool s_isSaving;

    static TrackRebuildScheduler()
    {
        EditorApplication.update += Update;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;

        // Pause/flush while saving, so Ctrl+S clears the asterisk and stays cleared
        UnityEditor.SceneManagement.EditorSceneManager.sceneSaving += OnSceneSaving;
        UnityEditor.SceneManagement.EditorSceneManager.sceneSaved  += OnSceneSaved;
    }

    // ----------------------------------------------------------------------
    // Public API
    // ----------------------------------------------------------------------
    public static void RequestRebuild(ITrackSegment seg, bool requestSceneRepaint = true)
    {
        if (s_isSaving) return;                  // ignore requests during save
        if (!IsAlive(seg)) return;
        s_pending.Add(seg);
        s_nextRun = EditorApplication.timeSinceStartup + kDebounceSeconds;
        if (requestSceneRepaint) s_repaintRequested = true;
    }

    public static void RequestRebuildAll(TrackRoot root, bool requestSceneRepaint = true)
    {
        if (s_isSaving) return;
        if (root == null) return;
        for (int i = 0; i < root.Count; i++)
        {
            var seg = root.GetSegment(i);
            if (IsAlive(seg)) s_pending.Add(seg);
        }
        s_nextRun = EditorApplication.timeSinceStartup + kDebounceSeconds;
        if (requestSceneRepaint) s_repaintRequested = true;
    }

    // Force immediate run (useful after big editor actions)
    public static void RunNow()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || s_isSaving)
            return; // strictly edit-mode, not during save
        s_nextRun = EditorApplication.timeSinceStartup;
        Update();
    }

    // ----------------------------------------------------------------------
    // Internal
    // ----------------------------------------------------------------------
    static void Update()
    {
    #if UNITY_EDITOR
        using (kSchedUpdate.Auto())
    #endif
        {
            if (s_nextRun < 0 || EditorApplication.timeSinceStartup < s_nextRun) return;
            s_nextRun = -1;

            if (s_pending.Count > 0)
            {
                var list = s_pending.ToList();
                s_pending.Clear();

                // Prune destroyed/invalid references before sorting
                list.RemoveAll(seg => (seg as Component) == null);

                // Only sort when it matters (common case is a single item)
                if (list.Count > 1)
                {
    #if UNITY_EDITOR
                    using (kSchedSort.Auto())
    #endif
                    {
                        list.Sort(CompareBySiblingIndex);
                    }
                }

                foreach (var seg in list)
                {
                    if (seg == null) continue;
    #if UNITY_EDITOR
                    using (kSchedRebld.Auto())
    #endif
                    {
                        try { seg.Rebuild(); }
                        catch (Exception e) { Debug.LogException(e); }
                    }
                }
            }

            if (s_repaintRequested)
            {
                s_repaintRequested = false;
    #if UNITY_EDITOR
                using (kSchedPaint.Auto())
    #endif
                {
                    SceneView.RepaintAll();
                }
            }
        }
    }


    static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        Flush(); // drop stale references/timers on any transition
    }

    static void OnBeforeAssemblyReload()
    {
        Flush();
    }

    static void OnSceneSaving(UnityEngine.SceneManagement.Scene scene, string path)
    {
        s_isSaving = true;
    }

    static void OnSceneSaved(UnityEngine.SceneManagement.Scene scene)
    {
        s_isSaving = false;
        Flush(); // ensure we don't immediately re-dirty right after a save
    }

    static void Flush()
    {
        s_pending.Clear();
        s_nextRun = -1;
        s_repaintRequested = false;
    }

    static void PrunePending()
    {
        s_pending.RemoveWhere(seg => !IsAlive(seg));
    }

    static bool IsAlive(ITrackSegment seg)
    {
        // Unity “fake null” semantics: destroyed UnityEngine.Objects compare to null
        var uo = seg as UnityEngine.Object;
        return uo != null;
    }

    static int CompareBySiblingIndex(ITrackSegment a, ITrackSegment b)
    {
        // Alive checks first
        var ua = a as UnityEngine.Object;
        var ub = b as UnityEngine.Object;
        bool aAlive = ua != null, bAlive = ub != null;
        if (!aAlive && !bAlive) return 0;
        if (!aAlive) return -1;
        if (!bAlive) return 1;

        var ca = a as Component;
        var cb = b as Component;
        if (ca == null || cb == null) return 0;

        var ta = ca.transform;
        var tb = cb.transform;
        if (ta == null || tb == null) return 0;

        if (ta.parent == tb.parent)
            return ta.GetSiblingIndex().CompareTo(tb.GetSiblingIndex());

        return string.Compare(ta.name, tb.name, StringComparison.Ordinal);
    }
}
#endif
