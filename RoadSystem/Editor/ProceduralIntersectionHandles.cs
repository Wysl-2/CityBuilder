// Editor/ProceduralIntersectionHandles.cs
using UnityEditor; using UnityEngine;

[CustomEditor(typeof(ProceduralIntersection))]
public class ProceduralIntersectionHandles : Editor
{
    void OnSceneGUI()
    {
        var pi = (ProceduralIntersection)target;
        var t  = pi.transform;
        var size = pi.Size;

        // Local midpoints of sides, then transform to world:
        Vector3 localMidSouth = new Vector3(size.x * 0.5f, 0f, 0f);
        Vector3 localMidEast  = new Vector3(size.x, 0f, size.y * 0.5f);
        Vector3 localMidNorth = new Vector3(size.x * 0.5f, 0f, size.y);
        Vector3 localMidWest  = new Vector3(0f, 0f, size.y * 0.5f);

        DrawSpawnButton(pi, t.TransformPoint(localMidSouth), Vector3.back,  Side.South);
        DrawSpawnButton(pi, t.TransformPoint(localMidEast),  Vector3.right, Side.East);
        DrawSpawnButton(pi, t.TransformPoint(localMidNorth), Vector3.forward,Side.North);
        DrawSpawnButton(pi, t.TransformPoint(localMidWest),  Vector3.left,  Side.West);
    }

    void DrawSpawnButton(ProceduralIntersection pi, Vector3 worldPos, Vector3 facingDir, Side side)
    {
        Handles.color = Color.cyan;
        if (Handles.Button(worldPos, Quaternion.LookRotation(facingDir, Vector3.up), 0.6f, 0.6f, Handles.CubeHandleCap))
        {
            SpawnAdjacent(pi, side);
        }
    }

    void SpawnAdjacent(ProceduralIntersection src, Side side)
    {
        Undo.IncrementCurrentGroup();
        var group = Undo.GetCurrentGroup();

        var root = FindFirstObjectByType<RoadSystemManager>();
        var cfg  = root ? root.config : null;

        var size = src.Size;

        // local offset one tile away
        Vector3 localDelta = side switch
        {
            Side.South => new Vector3(0f, 0f, -size.y),
            Side.East  => new Vector3(size.x, 0f, 0f),
            Side.North => new Vector3(0f, 0f,  size.y),
            _          => new Vector3(-size.x,0f, 0f),
        };

        // convert to world space relative to source rotation
        Vector3 worldDelta = src.transform.TransformVector(localDelta);

        var go = new GameObject("Intersection");
        Undo.RegisterCreatedObjectUndo(go, "Create Intersection");

        go.transform.SetParent(root ? root.transform : src.transform.parent, worldPositionStays:false);
        go.transform.position = src.transform.position + worldDelta;
        go.transform.rotation = src.transform.rotation;

        var pi = go.AddComponent<ProceduralIntersection>();
        pi.Size       = src.Size;
        pi.RoadHeight = src.RoadHeight;
        pi.material   = src.material;

        if (cfg) pi.ApplySharedDefaults(cfg);

        switch (side)
        {
            case Side.South: src.ConnectedSouth = true; pi.ConnectedNorth = true; break;
            case Side.East:  src.ConnectedEast  = true; pi.ConnectedWest  = true; break;
            case Side.North: src.ConnectedNorth = true; pi.ConnectedSouth = true; break;
            case Side.West:  src.ConnectedWest  = true; pi.ConnectedEast  = true; break;
        }

        src.Rebuild();
        pi.Rebuild();

        Undo.CollapseUndoOperations(group);
        EditorUtility.SetDirty(src);
        EditorUtility.SetDirty(pi.gameObject);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(go.scene);
    }

}
