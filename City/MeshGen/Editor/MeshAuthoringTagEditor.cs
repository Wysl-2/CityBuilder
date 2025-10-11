#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using ProcGen; // MeshAuthoringTag, Winding, QuadSplit

[CustomEditor(typeof(MeshAuthoringTag))]
public class MeshAuthoringTagEditor : Editor
{
    int  _selectedFace = -1;
    int  _decimals     = 3;   // decimals for display/copy
    bool _collapsed    = false;

    const float PanelX = 10f;
    const float PanelY = 10f;
    const float PanelW = 460f;

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        var tag = (MeshAuthoringTag)target;
        if (!tag) return;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Authoring Debug", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Faces", tag.faces != null ? tag.faces.Count.ToString() : "0");
        EditorGUILayout.LabelField("Final Tris", tag.finalTriToBuilderTri?.Count.ToString() ?? "0");
    }

void OnSceneGUI()
{
    var tag = (MeshAuthoringTag)target;
    if (!tag || tag.mesh == null || tag.faces == null || tag.faces.Count == 0)
        return;

    var tr = tag.transform;
    var e  = Event.current;

    HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

    // --- Compute overlay rect from the *current* selection
    //     (only to ignore clicks on the panel)
    bool hadSelection = _selectedFace >= 0 && _selectedFace < tag.faces.Count;
    bool hadQuad      = hadSelection && tag.faces[_selectedFace].kind == MeshAuthoringTag.FaceKind.Quad;
    Rect overlayRect  = hadSelection ? GetOverlayRect(hadQuad, _collapsed) : Rect.zero;

    // --- Handle clicks first
    if (e.type == EventType.MouseDown && e.button == 0 && !e.alt)
    {
        // Ignore clicks on the overlay itself
        if (!hadSelection || !overlayRect.Contains(e.mousePosition))
        {
            var ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            int finalTri = RaycastMeshWorld(tag.mesh, tr.localToWorldMatrix, ray);

            if (finalTri >= 0 && finalTri < tag.finalTriToBuilderTri.Count)
            {
                int builderTri = tag.finalTriToBuilderTri[finalTri];
                if (builderTri >= 0 && builderTri < tag.builderTriToFace.Count)
                {
                    int faceIdx = tag.builderTriToFace[builderTri];
                    _selectedFace = (faceIdx >= 0 && faceIdx < tag.faces.Count) ? faceIdx : -1;
                }
            }
            else
            {
                // Miss → deselect
                _selectedFace = -1;
            }

            e.Use();
            SceneView.RepaintAll();
            return; // <<<<<< IMPORTANT: don't draw using stale hasSelection
        }
    }

    // --- After input: recompute selection validity
    if (_selectedFace >= tag.faces.Count) _selectedFace = -1;
    bool hasSelection = _selectedFace >= 0 && _selectedFace < tag.faces.Count;
    if (!hasSelection) return; // nothing to draw

    var f = tag.faces[_selectedFace];
    bool isQuad = f.kind == MeshAuthoringTag.FaceKind.Quad;
    Rect panelRect = GetOverlayRect(isQuad, _collapsed);

    // --- Panel
    Handles.BeginGUI();
    GUILayout.BeginArea(panelRect, GUIContent.none, EditorStyles.helpBox);
    {
        bool expanded = EditorGUILayout.Foldout(!_collapsed, "Mesh Face Picker (Scene View)", true);
        _collapsed = !expanded;

        GUILayout.BeginHorizontal();
        GUILayout.Label(tag.gameObject.name, EditorStyles.miniLabel);
        if (expanded)
        {
            GUILayout.FlexibleSpace();
            GUILayout.Label("Decimals", GUILayout.Width(60));
            _decimals = Mathf.Clamp(EditorGUILayout.IntField(_decimals, GUILayout.Width(30)), 0, 6);
        }
        GUILayout.EndHorizontal();

        if (expanded)
        {
            GUILayout.Label($"Selected Face: {_selectedFace}  [{f.kind}]");
            if (isQuad) GUILayout.Label($"Winding: {f.winding}   Split: {f.split}", EditorStyles.miniLabel);
            else        GUILayout.Label("(Triangle)", EditorStyles.miniLabel);

            GUILayout.BeginHorizontal();
            GUI.enabled = _selectedFace > 0;
            if (GUILayout.Button("Prev", GUILayout.Width(60)))
            {
                _selectedFace = Mathf.Max(0, _selectedFace - 1);
                SceneView.RepaintAll();
            }
            GUI.enabled = _selectedFace < tag.faces.Count - 1;
            if (GUILayout.Button("Next", GUILayout.Width(60)))
            {
                _selectedFace = Mathf.Min(tag.faces.Count - 1, _selectedFace + 1);
                SceneView.RepaintAll();
            }
            GUI.enabled = true;

            if (GUILayout.Button("Frame", GUILayout.Width(60)))
            {
                var b = GetFaceBoundsWorld(tr, f);
                SceneView.lastActiveSceneView?.Frame(b, false);
            }

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Copy All", GUILayout.Width(80)))
                EditorGUIUtility.systemCopyBuffer = BuildFaceClipboard(f, tr, _decimals);
            GUILayout.EndHorizontal();

            ShowVertexLineCopyable("v0", f.v0, tr, _decimals);
            ShowVertexLineCopyable("v1", f.v1, tr, _decimals);
            ShowVertexLineCopyable("v2", f.v2, tr, _decimals);
            if (isQuad) ShowVertexLineCopyable("v3", f.v3, tr, _decimals);
        }
        else
        {
            GUILayout.Label($"Face {_selectedFace} [{f.kind}]", EditorStyles.miniLabel);
        }
    }
    GUILayout.EndArea();
    Handles.EndGUI();

    // --- Scene overlay
    DrawFaceOverlay(tr, f);
}





    // ---------- Layout ----------
    // Compute panel rect without indexing into faces
    Rect GetOverlayRect(bool isQuad, bool collapsed)
    {
        if (collapsed)
            return new Rect(PanelX, PanelY, PanelW, 64f);

        int vertLines = isQuad ? 4 : 3;
        float baseH   = 120f;
        float perLine = 22f;
        float H       = baseH + vertLines * perLine;
        return new Rect(PanelX, PanelY, PanelW, H);
    }

    // ---------- UI helpers ----------
    static void ShowVertexLineCopyable(string name, Vector3 local, Transform tr, int decimals)
    {
        Vector3 world = tr.TransformPoint(local);
        string line = $"{name}: L {Fmt(local, decimals)}   W {Fmt(world, decimals)}";

        GUILayout.BeginHorizontal();
        EditorGUILayout.SelectableLabel(line, EditorStyles.textField, GUILayout.Height(18));
        if (GUILayout.Button("Copy", GUILayout.Width(50)))
            EditorGUIUtility.systemCopyBuffer = line;
        GUILayout.EndHorizontal();
    }

    static string BuildFaceClipboard(MeshAuthoringTag.FaceRecord f, Transform tr, int d)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Face [{f.kind}]  Winding={f.winding}  Split={f.split}");
        sb.AppendLine($"v0: L {Fmt(f.v0, d)}   W {Fmt(tr.TransformPoint(f.v0), d)}");
        sb.AppendLine($"v1: L {Fmt(f.v1, d)}   W {Fmt(tr.TransformPoint(f.v1), d)}");
        sb.AppendLine($"v2: L {Fmt(f.v2, d)}   W {Fmt(tr.TransformPoint(f.v2), d)}");
        if (f.kind == MeshAuthoringTag.FaceKind.Quad)
            sb.AppendLine($"v3: L {Fmt(f.v3, d)}   W {Fmt(tr.TransformPoint(f.v3), d)}");
        return sb.ToString();
    }

    static string Fmt(Vector3 v, int d)
        => $"({v.x.ToString($"F{d}")}, {v.y.ToString($"F{d}")}, {v.z.ToString($"F{d}")})";

    // ---------- Bounds / drawing ----------
    static Bounds GetFaceBoundsWorld(Transform tr, MeshAuthoringTag.FaceRecord f)
    {
        var b = new Bounds(tr.TransformPoint(f.v0), Vector3.zero);
        b.Encapsulate(tr.TransformPoint(f.v1));
        b.Encapsulate(tr.TransformPoint(f.v2));
        if (f.kind == MeshAuthoringTag.FaceKind.Quad) b.Encapsulate(tr.TransformPoint(f.v3));
        return b;
    }

    static void DrawFaceOverlay(Transform tr, MeshAuthoringTag.FaceRecord f)
    {
        Vector3 w0 = tr.TransformPoint(f.v0);
        Vector3 w1 = tr.TransformPoint(f.v1);
        Vector3 w2 = tr.TransformPoint(f.v2);
        Vector3 w3 = tr.TransformPoint(f.v3);

        float r = HandleUtility.GetHandleSize((w0 + w1 + w2) / 3f) * 0.035f;

        Handles.color = new Color(1f, 0.85f, 0.2f, 0.9f);
        if (f.kind == MeshAuthoringTag.FaceKind.Quad)
        {
            Handles.DrawLine(w0, w1);
            Handles.DrawLine(w1, w2);
            Handles.DrawLine(w2, w3);
            Handles.DrawLine(w3, w0);
            if (f.split == QuadSplit.Diag02) Handles.DrawDottedLine(w0, w2, 2f);
            else                              Handles.DrawDottedLine(w1, w3, 2f);
        }
        else
        {
            Handles.DrawLine(w0, w1);
            Handles.DrawLine(w1, w2);
            Handles.DrawLine(w2, w0);
        }

        DrawVertexDotWithLabel(w0, r, "v0", new Color(0.95f, 0.25f, 0.25f));
        DrawVertexDotWithLabel(w1, r, "v1", new Color(0.25f, 0.85f, 0.25f));
        DrawVertexDotWithLabel(w2, r, "v2", new Color(0.25f, 0.55f, 1.0f));
        if (f.kind == MeshAuthoringTag.FaceKind.Quad)
            DrawVertexDotWithLabel(w3, r, "v3", new Color(0.75f, 0.35f, 0.95f));

        var sv = SceneView.lastActiveSceneView;
        if (sv && sv.camera)
        {
            var mid = (w0 + w1) * 0.5f;
            var dir = Vector3.ProjectOnPlane(w1 - w0, sv.camera.transform.forward);
            if (dir.sqrMagnitude > 1e-6f)
            {
                float size = HandleUtility.GetHandleSize(mid) * 0.12f;
                Handles.color = new Color(1f, 0.6f, 0.1f, 0.9f);
                Handles.ArrowHandleCap(0, mid, Quaternion.LookRotation(dir.normalized, Vector3.up), size, EventType.Repaint);
            }
        }
    }

    static void DrawVertexDotWithLabel(Vector3 p, float r, string label, Color c)
    {
        Handles.color = c;
        Handles.SphereHandleCap(0, p, Quaternion.identity, r * 2f, EventType.Repaint);

        var sv = SceneView.lastActiveSceneView;
        var up = (sv && sv.camera) ? sv.camera.transform.up : Vector3.up;

        Handles.color = Color.white;
        Handles.Label(p + up * (r * 2.2f),
            label,
            new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = Color.white } });
    }

    // ---------- Raycast mesh in world space (no collider required) ----------
    static int RaycastMeshWorld(Mesh mesh, Matrix4x4 l2w, Ray ray)
    {
        if (mesh == null) return -1;

        var verts = mesh.vertices;
        var tris  = mesh.triangles;

        float bestT = float.PositiveInfinity;
        int   bestTri = -1;

        Matrix4x4 w2l = l2w.inverse;
        Ray lray = new Ray(w2l.MultiplyPoint(ray.origin), w2l.MultiplyVector(ray.direction));

        for (int t = 0; t < tris.Length; t += 3)
        {
            Vector3 a = verts[tris[t+0]];
            Vector3 b = verts[tris[t+1]];
            Vector3 c = verts[tris[t+2]];

            if (RayTriangle(lray, a, b, c, out float dist) && dist < bestT)
            {
                bestT = dist;
                bestTri = t / 3; // final triangle index
            }
        }
        return bestTri;
    }

    // Möller–Trumbore
    static bool RayTriangle(Ray r, Vector3 a, Vector3 b, Vector3 c, out float t)
    {
        t = 0f;
        const float EPS = 1e-6f;
        Vector3 e1 = b - a, e2 = c - a;
        Vector3 p  = Vector3.Cross(r.direction, e2);
        float det  = Vector3.Dot(e1, p);
        if (det > -EPS && det < EPS) return false;
        float inv = 1f / det;
        Vector3 s  = r.origin - a;
        float u = inv * Vector3.Dot(s, p);
        if (u < 0f || u > 1f) return false;
        Vector3 q = Vector3.Cross(s, e1);
        float v = inv * Vector3.Dot(r.direction, q);
        if (v < 0f || u + v > 1f) return false;
        t = inv * Vector3.Dot(e2, q);
        return t > EPS;
    }
}
#endif
