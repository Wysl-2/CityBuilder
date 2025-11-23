// Editor/RoadSystemAuthoringWindow.cs
using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement;

public class RoadSystemAuthoringWindow : EditorWindow
{
    RoadSystemConfig config;

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
        if (GUILayout.Button("Create Road System Root + First Intersection", GUILayout.Height(32)))
            CreateRootAndFirst();
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

        // First node
        var first = new GameObject("Intersection");
        Undo.RegisterCreatedObjectUndo(first, "Create First Intersection");
        first.transform.SetParent(rootGO.transform, false);

        var pi = first.AddComponent<ProceduralIntersection>();
        pi.Size       = config.defaultSize;
        pi.RoadHeight = config.roadHeight;
        pi.material   = config.defaultMaterial;

        // Apply manager defaults if present
        if (rsm.config) pi.ApplySharedDefaults(rsm.config);

        // Build
        pi.Rebuild();

        // Dirty + undo
        Undo.CollapseUndoOperations(group);
        EditorUtility.SetDirty(rootGO);
        EditorUtility.SetDirty(first);
        if (!Application.isPlaying) EditorSceneManager.MarkSceneDirty(rootGO.scene);
        // Keep selection on the new root for quick iteration
        Selection.activeGameObject = rootGO;
    }
}
