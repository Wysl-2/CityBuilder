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

        var nodes = GetComponentsInChildren<ProceduralIntersection>(true);
        foreach (var n in nodes)
        {
            if (!n) continue;
            n.ApplySharedDefaults(config);
#if UNITY_EDITOR
            EditorUtility.SetDirty(n);
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
