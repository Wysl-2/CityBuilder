// Editor/RoadSystemAuthoringWindow.cs
using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement;

public enum RoadPieceType
{
    Road,
    Intersection
}

public class RoadSystemAuthoringWindow : EditorWindow
{
    RoadSystemConfig config;
    RoadPieceType pieceType;

    public static RoadPieceType CurrentPieceType { get; private set; } = RoadPieceType.Road;


    [MenuItem("Tools/CityBuilder/Road System Authoring")]
    static void Open() => GetWindow<RoadSystemAuthoringWindow>("Road System");

    void OnGUI()
    {
        EditorGUILayout.LabelField("Road System Config", EditorStyles.boldLabel);
        config = (RoadSystemConfig)EditorGUILayout.ObjectField("Config Asset", config, typeof(RoadSystemConfig), false);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Create New Config Asset"))
                config = CreateConfigAsset();

            using (new EditorGUI.DisabledScope(!config))
                if (GUILayout.Button("Open Config")) Selection.activeObject = config;
        }

        EditorGUILayout.Space(10);

        if (config)
        {
            EditorGUILayout.LabelField("Config Defaults", EditorStyles.boldLabel);
            config.defaultSize = EditorGUILayout.Vector2Field("Default Size", config.defaultSize);
            config.roadHeight  = EditorGUILayout.FloatField("Road Height", config.roadHeight);
            config.defaultMaterial = (Material)EditorGUILayout.ObjectField("Material", config.defaultMaterial, typeof(Material), false);

            if (GUI.changed)
                EditorUtility.SetDirty(config);
        }
        else
        {
            EditorGUILayout.HelpBox("Assign or create a RoadSystemConfig asset to edit settings.", MessageType.Info);
        }

    EditorGUILayout.Space(12);

    using (new EditorGUI.DisabledScope(!config))
    {
        if (GUILayout.Button("Create Road System Root", GUILayout.Height(32)))
            CreateRootAndFirst();
    }

    EditorGUILayout.LabelField("Piece Type", EditorStyles.boldLabel);

    using (new EditorGUILayout.HorizontalScope())
    {
        int selected = pieceType == RoadPieceType.Road ? 0 : 1;

        EditorGUI.BeginChangeCheck();
        selected = GUILayout.Toolbar(selected, new[] { "Road", "Intersection" });
        if (EditorGUI.EndChangeCheck())
        {
            pieceType = (RoadPieceType)selected;
            CurrentPieceType = pieceType;   // <<< keep static in sync
        }
    }
}

    RoadSystemConfig CreateConfigAsset()
    {
        var path = EditorUtility.SaveFilePanelInProject(
            "Create RoadSystemConfig", "RoadSystemConfig", "asset",
            "Choose a folder and name for the RoadSystemConfig asset.");
        if (string.IsNullOrEmpty(path)) return config;

        var asset = ScriptableObject.CreateInstance<RoadSystemConfig>();
        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorGUIUtility.PingObject(asset);
        return asset;
    }

    void CreateRootAndFirst()
    {
        Undo.IncrementCurrentGroup();
        var group = Undo.GetCurrentGroup();

        // Root
        var rootGO = new GameObject("RoadSystem_Root");
        Undo.RegisterCreatedObjectUndo(rootGO, "Create RoadSystem Root");
        var rsm = rootGO.AddComponent<RoadSystemManager>();

        // Assign config to manager (so OnValidate can auto-apply)
        rsm.config = config;

        // First piece (Road or Intersection depending on pieceType)
        string firstName = pieceType.ToString();            // "Road" or "Intersection"
        var first = new GameObject(firstName);
        Undo.RegisterCreatedObjectUndo(first, $"Create First {pieceType}");
        first.transform.SetParent(rootGO.transform, false);

        switch (pieceType)
        {
            case RoadPieceType.Road:
            {
                var road = first.AddComponent<ProceduralRoad>();

                // Use config defaults for dimensions & material
                // Interpret defaultSize.x as width, defaultSize.y as length
                road.width      = config.defaultSize.x;
                road.length     = config.defaultSize.y;
                road.RoadHeight = config.roadHeight;
                road.material   = config.defaultMaterial;

                // Axis default (RoadAxis.Z) is fine, or set explicitly if you want
                // road.Axis = RoadAxis.Z;

                road.Rebuild();
                break;
            }

            case RoadPieceType.Intersection:
            {
                var pi = first.AddComponent<ProceduralIntersection>();

                // Use intersection defaults from config
                pi.Size       = config.defaultSize;
                pi.RoadHeight = config.roadHeight;
                pi.material   = config.defaultMaterial;

                // Apply shared defaults (curb, corner sizes, footpaths, etc.)
                if (rsm.config)
                    pi.ApplySharedDefaults(rsm.config);

                pi.Rebuild();
                break;
            }
        }

        // Dirty + undo
        Undo.CollapseUndoOperations(group);
        EditorUtility.SetDirty(rootGO);
        EditorUtility.SetDirty(first);
        if (!Application.isPlaying)
            EditorSceneManager.MarkSceneDirty(rootGO.scene);

        // Keep selection on the new root for quick iteration
        Selection.activeGameObject = rootGO;
    }

}
