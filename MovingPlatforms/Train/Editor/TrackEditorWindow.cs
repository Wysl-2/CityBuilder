using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class TrackEditorWindow : EditorWindow
{
    [MenuItem("Tools/Track Editor")]
    public static void OpenWindow()
    {
        GetWindow<TrackEditorWindow>("Track Editor");
    }

    // ---------------- Serialized/editor fields ----------------
    [SerializeField] private TrackRoot trackRoot;

    [Header("Prefabs (optional)")]
    [SerializeField] private GameObject arcPrefab;
    [SerializeField] private GameObject straightPrefab;

    // Foldout state per segment GO
    private static readonly Dictionary<int, bool> s_foldouts = new Dictionary<int, bool>();

    // Scroll position for segment list
    private Vector2 scrollPos;

    // Loop readout UI state (inspector section)
    private Vector2 loopReadoutScroll;
    private int loopReadoutDecimals = 3;

    // -------- Scene overlay controls ----------
    [SerializeField] private bool showSceneOverlay = true;
    [SerializeField] private int overlayDecimals = 3;
    [SerializeField] private float overlayWidth = 360f;
    [SerializeField] private float overlayTopRightPaddingX = 12f;
    [SerializeField] private float overlayTopRightPaddingY = 12f;

    // register the SceneView callback
    void OnEnable()
    {
        SceneView.duringSceneGui -= OnSceneGUIOverlay;
        SceneView.duringSceneGui += OnSceneGUIOverlay;
    }

    void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUIOverlay;
    }

    // ---------------- GUI ----------------
    void OnGUI()
    {
        // Try to auto-pick a TrackRoot from selection if none assigned
        if (trackRoot == null && Selection.activeGameObject != null)
            trackRoot = Selection.activeGameObject.GetComponentInParent<TrackRoot>();

        using (new EditorGUILayout.VerticalScope("box"))
        {
            GUILayout.Label("Track Root", EditorStyles.boldLabel);
            trackRoot = (TrackRoot)EditorGUILayout.ObjectField("Root", trackRoot, typeof(TrackRoot), true);

            if (trackRoot == null)
            {
                EditorGUILayout.HelpBox("Assign a TrackRoot (GameObject with TrackRoot component).", MessageType.Info);
                if (GUILayout.Button("Create TrackRoot"))
                {
                    var go = new GameObject("TrackRoot");
                    Undo.RegisterCreatedObjectUndo(go, "Create TrackRoot");
                    trackRoot = go.AddComponent<TrackRoot>();
                    Selection.activeGameObject = go;
                }
                return;
            }
        }

        using (new EditorGUILayout.VerticalScope("box"))
        {
            GUILayout.Label("Prefabs (optional)", EditorStyles.boldLabel);
            arcPrefab = (GameObject)EditorGUILayout.ObjectField("Arc Prefab", arcPrefab, typeof(GameObject), false);
            straightPrefab = (GameObject)EditorGUILayout.ObjectField("Straight Prefab", straightPrefab, typeof(GameObject), false);
        }

        using (new EditorGUILayout.VerticalScope("box"))
        {
            GUILayout.Label("Loop", EditorStyles.boldLabel);

            trackRoot.IsClosedLoop = EditorGUILayout.Toggle("Closed Loop", trackRoot.IsClosedLoop);
            trackRoot.G0PositionTolerance = EditorGUILayout.FloatField("G0 Tolerance (m)", trackRoot.G0PositionTolerance);
            trackRoot.G1AngleToleranceDeg = EditorGUILayout.FloatField("G1 Tolerance (deg)", trackRoot.G1AngleToleranceDeg);
            //trackRoot.CloseAnchor = (TrackRoot.LoopAnchor)EditorGUILayout.EnumPopup("Close Anchor", trackRoot.CloseAnchor);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Validate Loop"))
                {
                    var (gap, ang) = trackRoot.GetLoopClosureError();
                    bool ok = trackRoot.IsLoopWithinTolerance();
                    EditorUtility.DisplayDialog("Loop Validation",
                        $"Gap: {gap:0.###} m\nAngle: {ang:0.###}°\n\n" +
                        (ok ? "Within tolerance ✅" : "Outside tolerance ❌"),
                        "OK");
                }

                // GUI.enabled = trackRoot.IsClosedLoop && trackRoot.Count >= 2;
                // if (GUILayout.Button("Snap To Close"))
                // {
                //     Undo.RecordObject(trackRoot.transform, "Snap Loop");
                //     trackRoot.SnapEndsToClose();
                // }
                // GUI.enabled = true;
            }

            // Inline status
            if (trackRoot.IsClosedLoop && trackRoot.Count >= 2)
            {
                var (gap, ang) = trackRoot.GetLoopClosureError();
                var ok = trackRoot.IsLoopWithinTolerance();
                var msg = $"Gap {gap:0.###} m, Angle {ang:0.###}° — " + (ok ? "OK" : "Needs snap");
                EditorGUILayout.HelpBox(msg, ok ? MessageType.Info : MessageType.Warning);
            }
        }

        // Overlay controls
        using (new EditorGUILayout.VerticalScope("box"))
        {
            GUILayout.Label("Scene Overlay", EditorStyles.boldLabel);
            showSceneOverlay = EditorGUILayout.Toggle("Show Overlay", showSceneOverlay);
            overlayDecimals = Mathf.Clamp(EditorGUILayout.IntField("Decimals", overlayDecimals), 0, 6);

            using (new EditorGUILayout.HorizontalScope())
            {
                overlayWidth = Mathf.Max(220f, EditorGUILayout.FloatField("Panel Width", overlayWidth));
                if (GUILayout.Button("Reset Width", GUILayout.Width(100)))
                    overlayWidth = 360f;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                overlayTopRightPaddingX = EditorGUILayout.FloatField("Padding X", overlayTopRightPaddingX);
                overlayTopRightPaddingY = EditorGUILayout.FloatField("Padding Y", overlayTopRightPaddingY);
            }

            EditorGUILayout.HelpBox("The overlay appears in the Scene view (top-right). It shows the first segment’s start, the last segment’s end, deltas, distances, and heading delta.", MessageType.None);
        }

        GUILayout.Space(6);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Add Arc"))      CreateAndAppend(typeof(TrackArcSegment), arcPrefab);
            if (GUILayout.Button("Add Straight")) CreateAndAppend(typeof(TrackStraightSegment), straightPrefab);
        }

        GUILayout.Space(8);

        // -------- SCROLL VIEW START --------
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.ExpandHeight(true));
        DrawSegmentListGUI();
        EditorGUILayout.EndScrollView();
        // -------- SCROLL VIEW END ----------

        GUILayout.Space(8);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Rebuild All")) RebuildAll();
            if (GUILayout.Button("Snap Chain"))  SnapChainFrom(0);
            if (GUILayout.Button("Auto-Rename")) AutoRename();
            if (GUILayout.Button("Clear All"))   ClearAll();

            GUILayout.FlexibleSpace();

            // bake from the window against the current TrackRoot
            GUI.enabled = trackRoot != null;
            if (GUILayout.Button("Bake Full Track", GUILayout.Width(140)))
            {
                TrackBakeUtility.BakeFull(trackRoot);
            }
            GUI.enabled = true;
        }
    }

    // ---------------- List GUI (with inline property editors) ----------------
    void DrawSegmentListGUI()
    {
        var list = trackRoot.SegmentObjects;
        GUILayout.Label($"Segments: {list.Count}", EditorStyles.miniBoldLabel);

        if (list.Count == 0)
        {
            EditorGUILayout.HelpBox("No segments yet. Use Add Arc / Add Straight.", MessageType.None);
            return;
        }

        for (int i = 0; i < list.Count; i++)
        {
            var go = list[i];
            using (new EditorGUILayout.VerticalScope("box"))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label($"[{i}]", GUILayout.Width(30));
                    var newGo = (GameObject)EditorGUILayout.ObjectField(go, typeof(GameObject), true);
                    if (newGo != go)
                    {
                        Undo.RecordObject(trackRoot, "Replace Segment");
                        list[i] = newGo;
                        EditorUtility.SetDirty(trackRoot);
                        go = newGo;
                    }

                    if (GUILayout.Button("Ping", GUILayout.Width(44)) && go != null)
                        EditorGUIUtility.PingObject(go);

                    if (GUILayout.Button("Sel", GUILayout.Width(40)) && go != null)
                        Selection.activeGameObject = go;
                }

                var seg = (go ? go.GetComponent<ITrackSegment>() : null);
                if (seg == null)
                {
                    EditorGUILayout.HelpBox("Missing ITrackSegment component.", MessageType.Warning);
                }
                else
                {
                    // Move / Insert / Remove row actions
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUI.enabled = i > 0;
                        if (GUILayout.Button("▲ Move Up", GUILayout.Width(90))) { Move(i, i - 1); return; }
                        GUI.enabled = i < list.Count - 1;
                        if (GUILayout.Button("▼ Move Down", GUILayout.Width(90))) { Move(i, i + 1); return; }
                        GUI.enabled = true;

                        if (GUILayout.Button("+ Arc After"))      { InsertAfter(i, typeof(TrackArcSegment), arcPrefab); return; }
                        if (GUILayout.Button("+ Straight After")) { InsertAfter(i, typeof(TrackStraightSegment), straightPrefab); return; }
                        if (GUILayout.Button("Remove", GUILayout.Width(72)))    { RemoveAt(i); return; }
                    }

                    // Inline property editor (foldout)
                    int id = go.GetInstanceID();
                    bool open = s_foldouts.TryGetValue(id, out var st) ? st : false;
                    open = EditorGUILayout.Foldout(open, "Segment Properties", true);
                    s_foldouts[id] = open;

                    if (open)
                    {
                        EditorGUI.indentLevel++;
                        DrawInlinePropertiesForSegment(i, go, seg);
                        EditorGUI.indentLevel--;
                    }
                }
            }
        }
    }

    // ---------------- Scene overlay ----------------
    void OnSceneGUIOverlay(SceneView sv)
    {
        if (!showSceneOverlay) return;
        if (trackRoot == null) return;

        // Resolve endpoints; if we can’t, hide the overlay.
        if (!TryGetLoopEndpoints(out var firstStart, out var lastEnd, out var headingDeltaDeg))
            return;

        // Prepare numbers
        Vector3 delta = firstStart - lastEnd;
        Vector2 deltaXZ = new Vector2(delta.x, delta.z);
        float dist3D = delta.magnitude;
        float distXZ = deltaXZ.magnitude;

        // panel rect (top-right)
        var size = new Vector2(overlayWidth, 190f);
        var viewRect = sv.position; // includes tabs; for GUI we use current view size via Handles.BeginGUI
        Handles.BeginGUI();
        {
            // Compute panel position using current Game view size
            var screen = GUIUtility.GUIToScreenPoint(Vector2.zero); // not reliable here; instead anchor to right using position.width
            float x = sv.position.width - size.x - overlayTopRightPaddingX;
            float y = overlayTopRightPaddingY;
            var r = new Rect(x, y, size.x, size.y);

            GUILayout.BeginArea(r, EditorStyles.helpBox);
            {
                GUILayout.Label("Loop Endpoints (Scene Overlay)", EditorStyles.boldLabel);

                DrawRow("First Start (world):", FormatV3(firstStart, overlayDecimals));
                DrawRow("Last End (world):",   FormatV3(lastEnd, overlayDecimals));

                GUILayout.Space(2);
                DrawRow("Δ (Start − End):",    FormatV3(delta, overlayDecimals));
                DrawRow("ΔXZ (x,z only):",     $"({Round(delta.x, overlayDecimals)}, {Round(delta.z, overlayDecimals)})");

                GUILayout.Space(2);
                DrawRow("Distance (3D):",      Round(dist3D, overlayDecimals).ToString());
                DrawRow("Distance (XZ):",      Round(distXZ, overlayDecimals).ToString());
                DrawRow("Heading Δ (deg):",    Round(headingDeltaDeg, overlayDecimals).ToString());
            }
            GUILayout.EndArea();
        }
        Handles.EndGUI();

        // Keep Scene view fresh
        sv.Repaint();
    }

    static void DrawRow(string label, string value)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Label(label, GUILayout.Width(150));
            GUILayout.Label(value, EditorStyles.textField);
        }
    }

    bool TryGetLoopEndpoints(out Vector3 firstStart, out Vector3 lastEnd, out float headingDeltaDeg)
    {
        firstStart = lastEnd = default;
        headingDeltaDeg = 0f;

        if (trackRoot == null || trackRoot.Count == 0) return false;
        if (!trackRoot.TryGetFirst(out var first) || !trackRoot.TryGetLast(out var last) || first == null || last == null)
            return false;

        firstStart = first.StartPoint; // world
        lastEnd = last.EndPoint;       // world

        Vector3 fA = (last.EndRotation * Vector3.forward); fA.y = 0f;
        Vector3 fB = (first.StartRotation * Vector3.forward); fB.y = 0f;

        if (fA.sqrMagnitude < 1e-8f || fB.sqrMagnitude < 1e-8f)
            headingDeltaDeg = 180f;
        else
            headingDeltaDeg = Mathf.Abs(Vector3.SignedAngle(fA.normalized, fB.normalized, Vector3.up));

        return true;
    }

    // ---------------- Loop inspector readout (kept, optional) ----------------
    void DrawLoopEndpointsReadout()
    {
        using (new EditorGUILayout.VerticalScope("box"))
        {
            GUILayout.Label("Loop Endpoints Readout", EditorStyles.boldLabel);

            if (trackRoot == null || trackRoot.Count == 0)
            {
                EditorGUILayout.HelpBox("No segments to inspect. Add segments to see loop endpoints.", MessageType.Info);
                return;
            }

            if (!trackRoot.TryGetFirst(out var first) || !trackRoot.TryGetLast(out var last) || first == null || last == null)
            {
                EditorGUILayout.HelpBox("Could not resolve first/last segments.", MessageType.Warning);
                return;
            }

            var start = first.StartPoint; // world
            var end = last.EndPoint;      // world
            var delta = start - end;      // “nudge last by this to match first”
            var deltaXZ = new Vector2(delta.x, delta.z);
            float dist3D = delta.magnitude;
            float distXZ = deltaXZ.magnitude;

            loopReadoutDecimals = Mathf.Clamp(EditorGUILayout.IntField("Decimals", loopReadoutDecimals), 0, 6);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("First Start (world):", GUILayout.Width(150));
                EditorGUILayout.SelectableLabel(FormatV3(start, loopReadoutDecimals), EditorStyles.textField, GUILayout.Height(18));
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Last End (world):", GUILayout.Width(150));
                EditorGUILayout.SelectableLabel(FormatV3(end, loopReadoutDecimals), EditorStyles.textField, GUILayout.Height(18));
            }

            GUILayout.Space(4);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Δ (Start − End):", GUILayout.Width(150));
                EditorGUILayout.SelectableLabel(FormatV3(delta, loopReadoutDecimals), EditorStyles.textField, GUILayout.Height(18));
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("ΔXZ (x,z only):", GUILayout.Width(150));
                EditorGUILayout.SelectableLabel($"({Round(delta.x, loopReadoutDecimals)}, {Round(delta.z, loopReadoutDecimals)})",
                    EditorStyles.textField, GUILayout.Height(18));
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Distance (3D):", GUILayout.Width(150));
                EditorGUILayout.SelectableLabel(Round(dist3D, loopReadoutDecimals).ToString(), EditorStyles.textField, GUILayout.Height(18));
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Distance (XZ):", GUILayout.Width(150));
                EditorGUILayout.SelectableLabel(Round(distXZ, loopReadoutDecimals).ToString(), EditorStyles.textField, GUILayout.Height(18));
            }

            {
                Vector3 fA = (last.EndRotation * Vector3.forward); fA.y = 0f;
                Vector3 fB = (first.StartRotation * Vector3.forward); fB.y = 0f;
                float ang = (fA.sqrMagnitude < 1e-8f || fB.sqrMagnitude < 1e-8f) ? 180f
                            : Mathf.Abs(Vector3.SignedAngle(fA.normalized, fB.normalized, Vector3.up));

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Heading Δ (deg):", GUILayout.Width(150));
                    EditorGUILayout.SelectableLabel(Round(ang, loopReadoutDecimals).ToString(), EditorStyles.textField, GUILayout.Height(18));
                }
            }
        }
    }

    // ---------------- Utilities ----------------
    static string FormatV3(Vector3 v, int dec)
        => $"({Round(v.x, dec)}, {Round(v.y, dec)}, {Round(v.z, dec)})";

    static float Round(float v, int dec) => (float)System.Math.Round(v, dec);

    void DrawInlinePropertiesForSegment(int index, GameObject go, ITrackSegment seg)
    {
        // Arc segment: expose Radius and SweepAngleDeg (default 0 if missing)
        var arc = go.GetComponent<TrackArcSegment>();
        if (arc != null)
        {
            float radius = SafeFloatField("Radius", TryGet(() => arc.Radius, 0f), min: 0f);
            float sweep  = SafeFloatField("Sweep Angle (deg)", TryGet(() => arc.SweepAngleDeg, 0f));

            if (!Mathf.Approximately(radius, arc.Radius) || !Mathf.Approximately(sweep, arc.SweepAngleDeg))
            {
                Undo.RecordObject(arc, "Edit Arc Properties");
                arc.Radius = Mathf.Max(0f, radius);
                arc.SweepAngleDeg = sweep;
                EditorUtility.SetDirty(arc);

                // Rebuild this segment & snap downstream
                seg.Rebuild();
                SnapChainFrom(index);
            }

            return;
        }

        // Straight segment: expose Length (default 0 if missing)
        var straight = go.GetComponent<TrackStraightSegment>();
        if (straight != null)
        {
            float length = SafeFloatField("Length", TryGet(() => straight.Length, 0f), min: 0f);

            if (!Mathf.Approximately(length, straight.Length))
            {
                Undo.RecordObject(straight, "Edit Straight Length");
                straight.Length = Mathf.Max(0f, length);
                EditorUtility.SetDirty(straight);

                // Rebuild this segment & snap downstream
                seg.Rebuild();
                SnapChainFrom(index);
            }

            return;
        }

        // Unknown type that still implements ITrackSegment
        EditorGUILayout.LabelField("(No inline editable properties found for this segment type.)", EditorStyles.miniLabel);
    }

    float SafeFloatField(string label, float value, float? min = null)
    {
        float v = EditorGUILayout.FloatField(label, value);
        if (min.HasValue) v = Mathf.Max(min.Value, v);
        return v;
    }

    static T TryGet<T>(System.Func<T> getter, T fallback)
    {
        try { return getter(); }
        catch { return fallback; }
    }

    // ---------------- Create / Insert ----------------
    void CreateAndAppend(System.Type segmentType, GameObject prefab)
    {
        if (trackRoot == null) return;

        GameObject go = InstantiateSegmentGO(segmentType, prefab);
        if (!go) return;

        Undo.SetTransformParent(go.transform, trackRoot.transform, "Parent to TrackRoot");

        // Snap to end of last (if any), else origin
        if (trackRoot.Count > 0)
        {
            var lastSeg = trackRoot.GetSegment(trackRoot.Count - 1);
            if (lastSeg != null)
            {
                go.transform.position = lastSeg.EndPoint;
                go.transform.rotation = lastSeg.EndRotation;
            }
        }
        else
        {
            go.transform.position = Vector3.zero;
            go.transform.rotation = Quaternion.identity;
        }

        Undo.RecordObject(trackRoot, "Add Segment");
        trackRoot.Add(go);
        EditorUtility.SetDirty(trackRoot);

        var newSeg = go.GetComponent<ITrackSegment>();
        newSeg?.Rebuild();

        Selection.activeGameObject = go;
    }

    void InsertAfter(int index, System.Type segmentType, GameObject prefab)
    {
        if (trackRoot == null) return;

        GameObject go = InstantiateSegmentGO(segmentType, prefab);
        if (!go) return;

        Undo.SetTransformParent(go.transform, trackRoot.transform, "Parent to TrackRoot");

        // Place at the end of the segment at 'index'
        var atSeg = trackRoot.GetSegment(index);
        if (atSeg != null)
        {
            go.transform.position = atSeg.EndPoint;
            go.transform.rotation = atSeg.EndRotation;
        }
        else
        {
            go.transform.position = Vector3.zero;
            go.transform.rotation = Quaternion.identity;
        }

        Undo.RecordObject(trackRoot, "Insert Segment");
        trackRoot.Insert(index + 1, go);
        EditorUtility.SetDirty(trackRoot);

        var newSeg = go.GetComponent<ITrackSegment>();
        newSeg?.Rebuild();

        // Resnap all segments after the inserted one to keep continuity
        SnapChainFrom(index + 1);

        Selection.activeGameObject = go;
    }

    GameObject InstantiateSegmentGO(System.Type segmentType, GameObject prefab)
    {
        GameObject go;
        if (prefab == null)
        {
            go = new GameObject($"Track_{segmentType.Name}");
            Undo.RegisterCreatedObjectUndo(go, $"Create {segmentType.Name}");
            if (go.GetComponent(segmentType) == null) go.AddComponent(segmentType);
        }
        else
        {
            go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            if (!go) return null;
            Undo.RegisterCreatedObjectUndo(go, $"Create {segmentType.Name}");
            go.name = $"Track_{segmentType.Name}";
            if (go.GetComponent(segmentType) == null) go.AddComponent(segmentType);
        }

        if (go.GetComponent<ITrackSegment>() == null)
        {
            Debug.LogError($"Prefab/Missing component does not implement ITrackSegment: {segmentType.Name}", go);
            return null;
        }

        return go;
    }

    // ---------------- Remove / Move / Clear ----------------
    void RemoveAt(int index)
    {
        var obj = trackRoot.GetObject(index);
        Undo.RecordObject(trackRoot, "Remove Segment");
        trackRoot.RemoveAt(index);
        EditorUtility.SetDirty(trackRoot);

        if (obj != null)
            Undo.DestroyObjectImmediate(obj);

        SnapChainFrom(Mathf.Max(0, index - 1));
    }

    void Move(int from, int to)
    {
        if (from == to) return;
        var list = trackRoot.SegmentObjects;
        if (from < 0 || from >= list.Count) return;
        to = Mathf.Clamp(to, 0, list.Count - 1);

        Undo.RecordObject(trackRoot, "Reorder Segment");
        var item = list[from];
        list.RemoveAt(from);
        list.Insert(to, item);
        EditorUtility.SetDirty(trackRoot);

        SnapChainFrom(Mathf.Min(from, to));
    }

    void ClearAll()
    {
        if (trackRoot == null) return;

        Undo.RecordObject(trackRoot, "Clear All Segments");
        var list = new List<GameObject>(trackRoot.SegmentObjects);
        trackRoot.Clear();
        EditorUtility.SetDirty(trackRoot);

        foreach (var go in list)
            if (go != null) Undo.DestroyObjectImmediate(go);
    }

    // ---------------- Utilities: Snap / Rebuild / Rename ----------------
    void SnapChainFrom(int startIndexInclusive)
    {
        var count = trackRoot.Count;
        if (count <= 1) return;

        startIndexInclusive = Mathf.Clamp(startIndexInclusive, 0, count - 1);

        for (int i = startIndexInclusive + 1; i < count; i++)
        {
            var prev = trackRoot.GetSegment(i - 1);
            var currGO = trackRoot.GetObject(i);
            var curr = trackRoot.GetSegment(i);

            if (prev == null || currGO == null || curr == null) continue;

            Undo.RecordObject(currGO.transform, "Snap Segment");
            currGO.transform.position = prev.EndPoint;
            currGO.transform.rotation = prev.EndRotation;

            // Must rebuild now so next iteration reads a correct prev.End*
            curr.Rebuild();
        }

        #if UNITY_EDITOR
        // Single repaint for the whole chain operation
        SceneView.RepaintAll();
        #endif
    }

    void RebuildAll()
    {
        #if UNITY_EDITOR
        TrackRebuildScheduler.RequestRebuildAll(trackRoot);
        TrackRebuildScheduler.RunNow(); // optional: do it immediately
        #else
        for (int i = 0; i < trackRoot.Count; i++)
            trackRoot.GetSegment(i)?.Rebuild();
        #endif
    }

    void AutoRename()
    {
        var list = trackRoot.SegmentObjects;
        for (int i = 0; i < list.Count; i++)
        {
            var go = list[i];
            if (!go) continue;

            string tag = go.GetComponent<TrackArcSegment>() != null ? "Arc" :
                         go.GetComponent<TrackStraightSegment>() != null ? "Straight" : "Seg";

            Undo.RecordObject(go, "Rename Segment");
            go.name = $"Track_{i:000}_{tag}";
            EditorUtility.SetDirty(go);
        }
    }
}
