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
        float hx = size.x * 0.5f;
        float hz = size.y * 0.5f;

        // Local midpoints of sides, in centered frame (-hx..+hx, -hz..+hz)
        Vector3 localMidSouth = new Vector3(0f,  0f, -hz);
        Vector3 localMidEast  = new Vector3(hx,  0f,  0f);
        Vector3 localMidNorth = new Vector3(0f,  0f,  hz);
        Vector3 localMidWest  = new Vector3(-hx, 0f,  0f);

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

// Editor/ProceduralIntersectionHandles.cs

void SpawnAdjacent(ProceduralIntersection src, Side side)
{
    Undo.IncrementCurrentGroup();
    var group = Undo.GetCurrentGroup();

    var root = FindFirstObjectByType<RoadSystemManager>();
    var cfg  = root ? root.config : null;

    var size = src.Size;

    // --- decide what to spawn based on authoring window ---
    var pieceType = RoadSystemAuthoringWindow.CurrentPieceType;

    if (pieceType == RoadPieceType.Intersection)
    {
        // ORIGINAL INTERSECTION-TO-INTERSECTION BEHAVIOUR
        Vector3 localDelta = side switch
        {
            Side.South => new Vector3(0f,      0f, -size.y),
            Side.East  => new Vector3(size.x,  0f,  0f),
            Side.North => new Vector3(0f,      0f,  size.y),
            _          => new Vector3(-size.x, 0f,  0f),
        };

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
        return;
    }

    // --- ROAD CASE: pivot at side midpoint, no extra tile offset ---

    float hx = size.x * 0.5f;
    float hz = size.y * 0.5f;

    // local midpoints of each side (match OnSceneGUI)
    Vector3 localMid = side switch
    {
        Side.South => new Vector3(0f,  0f, -hz),
        Side.East  => new Vector3(hx,  0f,  0f),
        Side.North => new Vector3(0f,  0f,  hz),
        _          => new Vector3(-hx, 0f,  0f), // West
    };

    // outward direction in local space
    Vector3 localOut = side switch
    {
        Side.South => Vector3.back,
        Side.East  => Vector3.right,
        Side.North => Vector3.forward,
        _          => Vector3.left,
    };

    Vector3 worldMid = src.transform.TransformPoint(localMid);
    Vector3 worldOut = src.transform.TransformDirection(localOut).normalized;

    var roadGO = new GameObject("Road");
    Undo.RegisterCreatedObjectUndo(roadGO, "Create Road");

    roadGO.transform.SetParent(root ? root.transform : src.transform.parent, worldPositionStays:false);
    roadGO.transform.position = worldMid;

    // Align road so its length extends along worldOut
    roadGO.transform.rotation = Quaternion.LookRotation(worldOut, src.transform.up);

    var pr = roadGO.AddComponent<ProceduralRoad>();

    if (cfg)
    {
        pr.width         = cfg.defaultRoad.width;
        pr.length        = cfg.defaultRoad.length;
        pr.footpathDepth = cfg.defaultRoad.footpathDepth;
        pr.RoadHeight    = cfg.roadHeight;
        pr.material      = cfg.defaultMaterial;
        pr.curb          = cfg.curb;
    }
    else
    {
        // fallback from intersection dimensions / material
        pr.width         = size.x;
        pr.length        = size.y;
        pr.footpathDepth = cfg ? cfg.defaultRoad.footpathDepth : pr.footpathDepth; // or some fallback
        pr.RoadHeight    = src.RoadHeight;
        pr.material      = src.material;
        pr.curb          = cfg ? cfg.curb : pr.curb;
    }

    // Our ProceduralRoad builds geometry along +Z from the back edge pivot
    pr.Axis = RoadAxis.Z;

    pr.Rebuild();

    Undo.CollapseUndoOperations(group);
    EditorUtility.SetDirty(src);
    EditorUtility.SetDirty(pr.gameObject);
    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(roadGO.scene);
}


}
