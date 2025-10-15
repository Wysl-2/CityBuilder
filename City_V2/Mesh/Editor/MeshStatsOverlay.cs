#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

namespace ProcGen.EditorTools
{
    [InitializeOnLoad]
    public static class MinimalMeshStatsOverlay
    {
        const string MenuPath = "Window/ProcGen/Minimal Mesh Stats Overlay";
        const string PrefVisible = "ProcGen.MinimalMeshStatsOverlay.Visible";

        static bool _visible;
        static Mesh _mesh;
        static Renderer _renderer;
        static GameObject _go;

        // Cached stats
        static int _lastMeshId = 0;
        static string _meshName = "<none>";
        static int _vertexCount, _subMeshCount, _indexCount, _triCount;
        static IndexFormat _indexFormat;
        static bool _hasNormals, _hasTangents, _hasColors, _isReadable;
        static readonly bool[] _uvs = new bool[8];
        static Vector3 _boundsCenterLocal, _boundsSizeLocal;

        static MinimalMeshStatsOverlay()
        {
            _visible = EditorPrefs.GetBool(PrefVisible, true);
            SceneView.duringSceneGui += OnSceneGUI;
            Selection.selectionChanged += RepaintAll;
        }

        [MenuItem(MenuPath)]
        public static void Toggle()
        {
            _visible = !_visible;
            EditorPrefs.SetBool(PrefVisible, _visible);
            RepaintAll();
        }

        [MenuItem(MenuPath, true)]
        public static bool ToggleValidate()
        {
            Menu.SetChecked(MenuPath, _visible);
            return true;
        }

        static void RepaintAll() => SceneView.RepaintAll();

        static void OnSceneGUI(SceneView sv)
        {
            if (!_visible) return;

            // Find selected mesh (MeshFilter or SkinnedMeshRenderer)
            _go = Selection.activeGameObject;
            _mesh = null;
            _renderer = null;

            if (_go)
            {
                var mf = _go.GetComponent<MeshFilter>();
                if (mf && mf.sharedMesh) { _mesh = mf.sharedMesh; _renderer = _go.GetComponent<Renderer>(); }
                else
                {
                    var sk = _go.GetComponent<SkinnedMeshRenderer>();
                    if (sk && sk.sharedMesh) { _mesh = sk.sharedMesh; _renderer = sk; }
                }
            }

            // Update cached stats only when the mesh reference changes
            if (_mesh && _mesh.GetInstanceID() != _lastMeshId)
            {
                CacheStats(_mesh);
                _lastMeshId = _mesh.GetInstanceID();
            }
            else if (_mesh == null && _lastMeshId != 0)
            {
                _lastMeshId = 0;
            }

            // ---- Bottom-right panel ----
            const int lineH = 16;
            int lines = (_mesh ? 8 : 1) + 1; // +1 for header
            float panelW = 360f;
            float panelH = 10 + lineH * lines;
            float pad = 8f;

            var view = sv.position; // SceneView rect (GUI coordinates)
            var rect = new Rect(
                view.width  - panelW - pad,
                view.height - panelH - pad,
                panelW,
                panelH
            );

            Handles.BeginGUI();
            GUI.Box(rect, GUIContent.none); // simple background

            var x = rect.x + 8;
            var y = rect.y + 6;
            var lh = lineH;
            var mini = EditorStyles.miniLabel;
            var bold = EditorStyles.boldLabel;

            // Header
            GUI.Label(new Rect(x, y, rect.width - 16, lh), "Minimal Mesh Stats", bold);
            y += lh;

            if (!EditorApplication.isPlaying)
            {
                GUI.Label(new Rect(x, y, rect.width - 16, lh),
                    "Enter Play Mode to see runtime-generated meshes.", mini);
                y += lh;
            }

            if (_mesh)
            {
                // World bounds can change while playing; fetch each frame (cheap)
                Bounds wb = _renderer ? _renderer.bounds : default;

                GUI.Label(new Rect(x, y, rect.width - 16, lh), $"GO: {_go.name}", mini); y += lh;
                GUI.Label(new Rect(x, y, rect.width - 16, lh), $"Mesh: {_meshName}", mini); y += lh;
                GUI.Label(new Rect(x, y, rect.width - 16, lh), $"Verts {_vertexCount}   Tris {_triCount}   Indices {_indexCount}   Sub {_subMeshCount}   ({_indexFormat})", mini); y += lh;
                GUI.Label(new Rect(x, y, rect.width - 16, lh), $"Readable {_isReadable}   Normals {_hasNormals}   Tangents {_hasTangents}   Colors {_hasColors}", mini); y += lh;

                // UV summary (only show present channels)
                string uvStr = "";
                for (int i = 0; i < 8; i++) if (_uvs[i]) uvStr += (uvStr.Length == 0 ? "" : ", ") + "uv" + (i == 0 ? "" : (i + 1).ToString());
                if (string.IsNullOrEmpty(uvStr)) uvStr = "none";
                GUI.Label(new Rect(x, y, rect.width - 16, lh), $"UVs: {uvStr}", mini); y += lh;

                GUI.Label(new Rect(x, y, rect.width - 16, lh), $"Bounds Local  C:{_boundsCenterLocal}  S:{_boundsSizeLocal}", mini); y += lh;
                GUI.Label(new Rect(x, y, rect.width - 16, lh), $"Bounds World  C:{wb.center}  S:{wb.size}", mini); y += lh;
            }
            else
            {
                GUI.Label(new Rect(x, y, rect.width - 16, lh), "Select a GameObject with a MeshFilter / SkinnedMeshRenderer.", mini);
            }

            Handles.EndGUI();
        }

        static void CacheStats(Mesh m)
        {
            _meshName = string.IsNullOrEmpty(m.name) ? "<unnamed>" : m.name;

            _vertexCount  = m.vertexCount;
            _subMeshCount = m.subMeshCount;
            _indexFormat  = m.indexFormat;

            long idx = 0;
            for (int s = 0; s < _subMeshCount; s++)
                idx += (long)m.GetIndexCount(s);
            _indexCount = (int)Mathf.Min(int.MaxValue, idx);
            _triCount   = _indexCount / 3;

            _isReadable  = m.isReadable;

#if UNITY_2019_1_OR_NEWER
            _hasNormals  = m.HasVertexAttribute(VertexAttribute.Normal);
            _hasTangents = m.HasVertexAttribute(VertexAttribute.Tangent);
            _hasColors   = m.HasVertexAttribute(VertexAttribute.Color);
            for (int i = 0; i < 8; i++)
            {
                var attr = (VertexAttribute)((int)VertexAttribute.TexCoord0 + i);
                _uvs[i] = m.HasVertexAttribute(attr);
            }
#else
            _hasNormals  = m.normals != null && m.normals.Length == _vertexCount;
            _hasTangents = m.tangents != null && m.tangents.Length == _vertexCount;
            _hasColors   = m.colors != null && m.colors.Length == _vertexCount;
            for (int i = 0; i < 8; i++) _uvs[i] = false;
            _uvs[0] = m.uv != null && m.uv.Length > 0;
            _uvs[1] = m.uv2 != null && m.uv2.Length > 0;
            _uvs[2] = m.uv3 != null && m.uv3.Length > 0;
            _uvs[3] = m.uv4 != null && m.uv4.Length > 0;
#endif

            var b = m.bounds;
            _boundsCenterLocal = b.center;
            _boundsSizeLocal   = b.size;

            // Optional: long mem = Profiler.GetRuntimeMemorySizeLong(m);
        }
    }
}
#endif
