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

    // Creation overrides (editor-session only)
    // These are now the single source of truth for dimensions.
    static Vector2 intersectionSizeOverride = Vector2.zero;
    static float   roadWidthOverride        = 0f;
    static float   roadLengthOverride       = 0f;
    static float   roadFootpathDepthOverride = 0f;

    // Track seeding from config
    static RoadSystemConfig lastSeedConfig;
    static bool overridesSeededFromConfig = false;

    [MenuItem("Tools/CityBuilder/Road System Authoring")]
    static void Open() => GetWindow<RoadSystemAuthoringWindow>("Road System");

    void OnGUI()
    {
        EditorGUILayout.LabelField("Road System Config", EditorStyles.boldLabel);

        var newConfig = (RoadSystemConfig)EditorGUILayout.ObjectField(
            "Config Asset", config, typeof(RoadSystemConfig), false
        );

        // Detect config change to seed overrides once per config
        if (newConfig != config)
        {
            config = newConfig;

            if (config)
            {
                SeedOverridesFromConfig(config);
                lastSeedConfig = config;
                overridesSeededFromConfig = true;
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Create New Config Asset"))
            {
                var created = CreateConfigAsset();
                if (created)
                {
                    config = created;
                    SeedOverridesFromConfig(config);
                    lastSeedConfig = config;
                    overridesSeededFromConfig = true;
                }
            }

            using (new EditorGUI.DisabledScope(!config))
            {
                if (GUILayout.Button("Open Config"))
                    Selection.activeObject = config;
            }
        }

        EditorGUILayout.Space(10);

        if (!config)
        {
            EditorGUILayout.HelpBox(
                "Assign or create a RoadSystemConfig asset.\n\n" +
                "- Global defaults (road height, curb, base sizes, materials) are edited on the asset.\n" +
                "- This window only controls creation-time overrides for new pieces.",
                MessageType.Info
            );
            return;
        }

        // ---------- CREATION OVERRIDES ----------
        EditorGUILayout.LabelField("Creation Overrides", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "These values are what new pieces will actually use.\n" +
            "They are initially seeded from the config, but after that this window is the source of truth.",
            MessageType.None
        );

        EditorGUILayout.Space(4);

        // Intersection
        EditorGUILayout.LabelField("Intersection", EditorStyles.boldLabel);
        intersectionSizeOverride = EditorGUILayout.Vector2Field(
            "Size (X,Z)", intersectionSizeOverride
        );

        EditorGUILayout.Space(4);

        // Road
        EditorGUILayout.LabelField("Road", EditorStyles.boldLabel);
        roadWidthOverride = EditorGUILayout.FloatField("Width", roadWidthOverride);
        roadLengthOverride = EditorGUILayout.FloatField("Length", roadLengthOverride);
        roadFootpathDepthOverride = EditorGUILayout.FloatField(
            "Footpath Depth",
            roadFootpathDepthOverride
        );

        EditorGUILayout.Space(12);

        // ---------- CREATE ROOT ----------
        using (new EditorGUI.DisabledScope(!config))
        {
            if (GUILayout.Button("Create Road System Root", GUILayout.Height(32)))
                CreateRootAndFirst();
        }

        EditorGUILayout.Space(8);

        // ---------- PIECE TYPE SELECTION ----------
        EditorGUILayout.LabelField("Piece Type for New Pieces", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            int selected = pieceType == RoadPieceType.Road ? 0 : 1;

            EditorGUI.BeginChangeCheck();
            selected = GUILayout.Toolbar(selected, new[] { "Road", "Intersection" });
            if (EditorGUI.EndChangeCheck())
            {
                pieceType = (RoadPieceType)selected;
                CurrentPieceType = pieceType;
            }
        }
    }

    // -------- SEEDING FROM CONFIG --------

    static void SeedOverridesFromConfig(RoadSystemConfig cfg)
    {
        if (!cfg) return;

        // Only seed when overrides are effectively unset, so user edits are preserved
        if (intersectionSizeOverride == Vector2.zero)
            intersectionSizeOverride = cfg.defaultIntersectionSize;

        if (roadWidthOverride <= 0f)
            roadWidthOverride = cfg.defaultRoad.width;

        if (roadLengthOverride <= 0f)
            roadLengthOverride = cfg.defaultRoad.length;

        if (roadFootpathDepthOverride <= 0f)
            roadFootpathDepthOverride = cfg.defaultRoad.footpathDepth;
    }

    // -------- STATIC HELPERS USED BY HANDLE SCRIPTS --------
    // These no longer look at config for dimensional values; they just reflect the window.

    public static Vector2 GetIntersectionSize()
        => intersectionSizeOverride;

    public static float GetRoadWidth()
        => roadWidthOverride;

    public static float GetRoadLength()
        => roadLengthOverride;

    public static float GetRoadFootpathDepth()
        => roadFootpathDepthOverride;

    // -------- CONFIG ASSET CREATION + ROOT CREATION --------

    RoadSystemConfig CreateConfigAsset()
    {
        var path = EditorUtility.SaveFilePanelInProject(
            "Create RoadSystemConfig",
            "RoadSystemConfig",
            "asset",
            "Choose a folder and name for the RoadSystemConfig asset."
        );
        if (string.IsNullOrEmpty(path)) return null;

        var asset = ScriptableObject.CreateInstance<RoadSystemConfig>();
        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorGUIUtility.PingObject(asset);
        return asset;
    }

    void CreateRootAndFirst()
    {
        if (!config)
        {
            Debug.LogError("RoadSystemAuthoringWindow: Cannot create root without a config.");
            return;
        }

        // Ensure overrides are seeded at least once
        if (!overridesSeededFromConfig || lastSeedConfig != config)
        {
            SeedOverridesFromConfig(config);
            lastSeedConfig = config;
            overridesSeededFromConfig = true;
        }

        Undo.IncrementCurrentGroup();
        var group = Undo.GetCurrentGroup();

        // Root
        var rootGO = new GameObject("RoadSystem_Root");
        Undo.RegisterCreatedObjectUndo(rootGO, "Create RoadSystem Root");
        var rsm = rootGO.AddComponent<RoadSystemManager>();

        // Assign config to manager
        rsm.config = config;

        // First piece
        string firstName = pieceType.ToString();
        var first = new GameObject(firstName);
        Undo.RegisterCreatedObjectUndo(first, $"Create First {pieceType}");
        first.transform.SetParent(rootGO.transform, false);

        switch (pieceType)
        {
            case RoadPieceType.Road:
            {
                var road = first.AddComponent<ProceduralRoad>();

                // Use authoring overrides as the single source of truth
                road.width         = Mathf.Max(0.01f, roadWidthOverride);
                road.length        = Mathf.Max(0.01f, roadLengthOverride);
                road.footpathDepth = Mathf.Max(0f,    roadFootpathDepthOverride);

                // Shared values from config (no overrides)
                road.RoadHeight    = config.roadHeight;
                road.material      = config.defaultMaterial;
                road.curb          = config.curb;

                road.Rebuild();
                break;
            }

            case RoadPieceType.Intersection:
            {
                var pi = first.AddComponent<ProceduralIntersection>();

                var size = intersectionSizeOverride;
                if (size == Vector2.zero && config.defaultIntersectionSize != Vector2.zero)
                    size = config.defaultIntersectionSize;

                pi.Size       = size;
                pi.RoadHeight = config.roadHeight;
                pi.material   = config.defaultMaterial;

                pi.ApplySharedDefaults(config);
                pi.Rebuild();
                break;
            }
        }

        Undo.CollapseUndoOperations(group);
        EditorUtility.SetDirty(rootGO);
        EditorUtility.SetDirty(first);
        if (!Application.isPlaying)
            EditorSceneManager.MarkSceneDirty(rootGO.scene);

        Selection.activeGameObject = rootGO;
    }
}
