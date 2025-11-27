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

        // Intersections: safe to push full defaults, they’re tile-based
        var intersections = GetComponentsInChildren<ProceduralIntersection>(true);
        foreach (var i in intersections)
        {
            if (!i) continue;
            i.ApplySharedDefaults(config);          // your existing method
            i.RoadHeight = config.roadHeight;
            i.material   = config.defaultMaterial;
#if UNITY_EDITOR
            EditorUtility.SetDirty(i);
#endif
        }

        // Roads: only update shared properties, DO NOT touch length/width/footpathDepth
        var roads = GetComponentsInChildren<ProceduralRoad>(true);
        foreach (var r in roads)
        {
            if (!r) continue;
            r.RoadHeight = config.roadHeight;
            r.material   = config.defaultMaterial;
            r.curb       = config.curb;
#if UNITY_EDITOR
            EditorUtility.SetDirty(r);
#endif
        }

#if UNITY_EDITOR
        if (!Application.isPlaying)
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying && autoApplyOnValidate)
            ApplyDefaultsToChildren();
    }
#endif

    public bool TryConnectIntersectionAt(Vector3 worldPos, float epsilon, out ProceduralIntersection hit, out Side side)
    {
        hit = null;
        side = default;

        var intersections = GetComponentsInChildren<ProceduralIntersection>(true);
        foreach (var pi in intersections)
        {
            if (!pi) continue;

            foreach (Side s in System.Enum.GetValues(typeof(Side)))
            {
                Vector3 sidePos = pi.GetSideWorldMid(s);
                if (Vector3.SqrMagnitude(sidePos - worldPos) <= epsilon * epsilon)
                {
                    hit = pi;
                    side = s;
                    return true;
                }
            }
        }

        return false;
    }

    public bool TryFindRoadAt(
    Vector3 worldPos,
    float epsilon,
    out ProceduralRoad road,
    out bool isFrontEnd)
{
    road      = null;
    isFrontEnd = false;

    float eps2 = epsilon * epsilon;
    var roads  = GetComponentsInChildren<ProceduralRoad>(true);

    foreach (var r in roads)
    {
        bool alongZ = (r.Axis == RoadAxis.Z);

        Vector3 backLocal  = Vector3.zero;
        Vector3 frontLocal = alongZ
            ? new Vector3(0f,       0f, r.length)
            : new Vector3(r.length, 0f, 0f);

        Vector3 backWorld  = r.transform.TransformPoint(backLocal);
        Vector3 frontWorld = r.transform.TransformPoint(frontLocal);

        if ((worldPos - backWorld).sqrMagnitude <= eps2)
        {
            road       = r;
            isFrontEnd = false;
            return true;
        }

        if ((worldPos - frontWorld).sqrMagnitude <= eps2)
        {
            road       = r;
            isFrontEnd = true;
            return true;
        }
    }

    return false;
}

public void AutoConnectIntersectionToRoads(ProceduralIntersection pi, float epsilon)
{
    // For each side, see if a road endpoint sits there.
    foreach (var side in new[] { Side.North, Side.East, Side.South, Side.West })
    {
        var mid = pi.GetSideWorldMid(side);

        if (TryFindRoadAt(mid, epsilon, out var road, out _))
        {
            switch (side)
            {
                case Side.North: pi.ConnectedNorth = true; break;
                case Side.East:  pi.ConnectedEast  = true; break;
                case Side.South: pi.ConnectedSouth = true; break;
                case Side.West:  pi.ConnectedWest  = true; break;
            }
        }
    }
}

}

