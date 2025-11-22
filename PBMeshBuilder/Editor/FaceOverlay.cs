using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEditor.ProBuilder; // SelectionPicker
using UnityEngine.Rendering;  // CompareFunction

[CustomEditor(typeof(ProBuilderMesh))]
public class FaceOverlay : Editor
{
    ProBuilderMesh pb;
    FaceSinkTag sinkTag;
    Mesh unityMesh;
    Transform tr;

    // map from order-independent face vertex set -> FaceRecord index
    Dictionary<string,int> faceKeyToRecord;

    // selection state
    Face selectedPBFace;             // the picked PB Face
    int[] selectedDistinct;          // sorted distinct mesh indices of that face
    Vector3[] selectedWorldVerts;    // world-space points (closed loop) matching draw order
    int[] selectedOrderedIdx;        // mesh indices matching selectedWorldVerts order
    int? selectedRecordIndex;        // index into sinkTag.faces for the picked face

    // overlay mode
    bool showFaceOrder = false;      // false = show mesh indices; true = show v0..vN (creation order)

    void OnEnable()
    {
        pb       = (ProBuilderMesh)target;
        sinkTag  = pb ? pb.GetComponentInParent<FaceSinkTag>() : null;
        var mf   = pb ? pb.GetComponent<MeshFilter>() : null;
        unityMesh = mf ? mf.sharedMesh : null;
        tr        = pb ? pb.transform : null;

        BuildFaceMap();
    }

    void OnDisable()
    {
        faceKeyToRecord   = null;
        selectedPBFace    = null;
        selectedDistinct  = null;
        selectedWorldVerts= null;
        selectedOrderedIdx= null;
        selectedRecordIndex = null;
    }

    void BuildFaceMap()
    {
        if (sinkTag == null || sinkTag.faces == null)
        {
            faceKeyToRecord = null;
            return;
        }

        faceKeyToRecord = new Dictionary<string,int>(sinkTag.faces.Count);
        for (int i = 0; i < sinkTag.faces.Count; i++)
        {
            var rec = sinkTag.faces[i];
            if (rec.vertexIndices == null || rec.vertexIndices.Length < 3) continue;

            var arr = (int[])rec.vertexIndices.Clone();
            Array.Sort(arr);
            faceKeyToRecord[string.Join(",", arr)] = i;
        }
    }

    void OnSceneGUI()
    {
        if (pb == null || sinkTag == null || faceKeyToRecord == null || unityMesh == null) return;

        int id = GUIUtility.GetControlID(FocusType.Passive);
        var e  = Event.current;

        // ----- selection -----
        if (e.GetTypeForControl(id) == EventType.MouseDown && e.button == 0 && !e.alt)
        {
            var sv = SceneView.currentDrawingSceneView ?? SceneView.lastActiveSceneView;
            if (sv?.camera == null) return;

            // GUI -> screen pixels
            Vector2 screenPos = UnityEditor.HandleUtility.GUIPointToScreenPixelCoordinate(e.mousePosition);

            var face = SelectionPicker.PickFace(sv.camera, screenPos, pb);
            if (face != null && face.distinctIndexes != null && face.distinctIndexes.Count >= 3)
            {
                // order-independent key
                var arr = new int[face.distinctIndexes.Count];
                for (int i = 0; i < arr.Length; i++) arr[i] = face.distinctIndexes[i];
                Array.Sort(arr);
                string key = string.Join(",", arr);

                if (faceKeyToRecord.TryGetValue(key, out int recIdx))
                {
                    selectedPBFace      = face;
                    selectedDistinct    = arr;
                    selectedRecordIndex = recIdx;

                    BuildOrderedLoopAndWorldVerts();

                    GUIUtility.hotControl = id;
                    e.Use();
                    SceneView.RepaintAll();
                }
                else
                {
                    Debug.Log($"[FaceOverlay] PB face not in FaceSink map. key={key}");
                }
            }
        }
        else if (e.GetTypeForControl(id) == EventType.MouseUp && GUIUtility.hotControl == id)
        {
            GUIUtility.hotControl = 0;
            e.Use();
        }

        // ----- draw overlay (geometry) -----
        if (Event.current.type == EventType.Repaint &&
            selectedWorldVerts != null && selectedWorldVerts.Length >= 3)
        {
            var prevZ = Handles.zTest;
            Handles.zTest = CompareFunction.LessEqual;

            // Outline
            Handles.DrawAAPolyLine(3f, selectedWorldVerts);

            // Vertex dots + labels (mesh-index or face-order)
            for (int i = 0; i < selectedOrderedIdx.Length; i++)
            {
                int meshIndex = selectedOrderedIdx[i];
                Vector3 p     = selectedWorldVerts[i];

                float size = UnityEditor.HandleUtility.GetHandleSize(p) * 0.045f;
                Handles.DotHandleCap(0, p, Quaternion.identity, size, EventType.Repaint);

                string labelText = MakeVertexLabel(meshIndex);

                Handles.BeginGUI();
                var guiPos = UnityEditor.HandleUtility.WorldToGUIPoint(p);
                var rect   = new Rect(guiPos + new Vector2(10, -14), new Vector2(260, 18));
                GUI.Label(rect, labelText);
                Handles.EndGUI();
            }

            Handles.zTest = prevZ;
        }

        // ----- draw overlay (panel & toggle) -----
        DrawModePanel();
    }

    string MakeVertexLabel(int meshIndex)
    {
        if (!showFaceOrder || selectedRecordIndex == null)
        {
            // Mesh index mode
            return $"v{meshIndex}";
        }

        // Face-order mode: find position of this mesh index inside the FaceRecord's vertexIndices
        var rec = sinkTag.faces[selectedRecordIndex.Value];
        int local = Array.IndexOf(rec.vertexIndices, meshIndex);

        if (local >= 0)
            return $"v{local}  (mesh {meshIndex})";  // shows both for clarity
        else
            return $"mesh {meshIndex}";              // fallback if not found
    }

    // Build a nice drawing order (closed loop) + world positions for the picked face
    void BuildOrderedLoopAndWorldVerts()
    {
        if (selectedDistinct == null || selectedDistinct.Length < 3 || unityMesh == null || tr == null)
        {
            selectedWorldVerts = null;
            selectedOrderedIdx = null;
            return;
        }

        var verts = unityMesh.vertices;

        // get local-space points by these mesh indices
        var localPts = new List<Vector3>(selectedDistinct.Length);
        foreach (var vi in selectedDistinct)
            localPts.Add(verts[vi]);

        // best-fit normal & plane basis
        Vector3 n = ComputeNormal(localPts);
        OrthonormalBasis(n, out Vector3 U, out Vector3 V);
        Vector3 centroid = Vector3.zero;
        foreach (var p in localPts) centroid += p;
        centroid /= localPts.Count;

        // order CCW around centroid in (U,V) plane
        var tmp = new List<(int idx, float angle, Vector3 lp)>(localPts.Count);
        for (int i = 0; i < localPts.Count; i++)
        {
            Vector3 d = localPts[i] - centroid;
            float x = Vector3.Dot(d, U);
            float y = Vector3.Dot(d, V);
            float ang = Mathf.Atan2(y, x);
            tmp.Add((selectedDistinct[i], ang, localPts[i]));
        }
        tmp.Sort((a,b) => a.angle.CompareTo(b.angle));

        // bake arrays
        selectedOrderedIdx = new int[tmp.Count];
        selectedWorldVerts = new Vector3[tmp.Count + 1];

        for (int i = 0; i < tmp.Count; i++)
        {
            selectedOrderedIdx[i] = tmp[i].idx;
            selectedWorldVerts[i] = tr.TransformPoint(tmp[i].lp);
        }
        selectedWorldVerts[tmp.Count] = selectedWorldVerts[0]; // close loop
    }

    // Small floating panel with a toggle button
    void DrawModePanel()
    {
        Handles.BeginGUI();

        const float w = 210f, h = 58f;
        var rect = new Rect(12, 12, w, h);
        GUILayout.BeginArea(rect, EditorStyles.helpBox);
        GUILayout.Label("Face Overlay", EditorStyles.boldLabel);

        var prev = showFaceOrder;
        showFaceOrder = GUILayout.Toggle(showFaceOrder,
            showFaceOrder ? "Labels: Face Order (v0..)" : "Labels: Mesh Indices");

        if (prev != showFaceOrder)
            SceneView.RepaintAll();

        GUILayout.EndArea();
        Handles.EndGUI();
    }

    // Robust face normal
    static Vector3 ComputeNormal(List<Vector3> pts)
    {
        if (pts.Count < 3) return Vector3.up;

        Vector3 n = Vector3.zero;
        for (int i = 0; i < pts.Count; i++)
        {
            Vector3 a = pts[i];
            Vector3 b = pts[(i + 1) % pts.Count];
            n.x += (a.y - b.y) * (a.z + b.z);
            n.y += (a.z - b.z) * (a.x + b.x);
            n.z += (a.x - b.x) * (a.y + b.y);
        }
        if (n.sqrMagnitude < 1e-12f)
            n = Vector3.Cross(pts[1] - pts[0], pts[2] - pts[0]);

        return n.sqrMagnitude > 0 ? n.normalized : Vector3.up;
    }

    // Build in-plane basis (U,V) from normal N
    static void OrthonormalBasis(Vector3 N, out Vector3 U, out Vector3 V)
    {
        Vector3 n = N.sqrMagnitude > 0 ? N.normalized : Vector3.up;
        Vector3 refAxis = Mathf.Abs(n.y) < 0.999f ? Vector3.up : Vector3.right;
        U = Vector3.Normalize(Vector3.Cross(refAxis, n));
        V = Vector3.Cross(n, U);
    }
}
