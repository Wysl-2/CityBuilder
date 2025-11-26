// Assets/CityBuilder/RoadSystem/RoadSystemManager.cs
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

public sealed class RoadSystemManager : MonoBehaviour
{
    public RoadSystemConfig config;
    public bool autoApplyOnValidate = true;

    [ContextMenu("Apply Defaults → Children")]
   public void ApplyDefaultsToChildren()
    {
        if (!config) return;

        // Intersections
        var intersections = GetComponentsInChildren<ProceduralIntersection>(true);
        foreach (var n in intersections)
        {
            if (!n) continue;
            n.ApplySharedDefaults(config);
    #if UNITY_EDITOR
            EditorUtility.SetDirty(n);
    #endif
        }

        // Roads
        var roads = GetComponentsInChildren<ProceduralRoad>(true);
        foreach (var r in roads)
        {
            if (!r) continue;
            r.width         = config.defaultRoad.width;
            r.length        = config.defaultRoad.length;
            r.footpathDepth = config.defaultRoad.footpathDepth;
            r.RoadHeight    = config.roadHeight;
            r.material      = config.defaultMaterial;
            r.curb          = config.curb;
    #if UNITY_EDITOR
            EditorUtility.SetDirty(r);
    #endif
        }

    #if UNITY_EDITOR
        if (!Application.isPlaying)
            EditorSceneManager.MarkSceneDirty(gameObject.scene);
    #endif
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying && autoApplyOnValidate)
            ApplyDefaultsToChildren();
    }
#endif
}
