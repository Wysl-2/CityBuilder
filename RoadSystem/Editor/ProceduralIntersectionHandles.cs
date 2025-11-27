// Editor/ProceduralIntersectionHandles.cs
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(ProceduralIntersection))]
public class ProceduralIntersectionHandles : Editor
{
    void OnSceneGUI()
    {
        var pi   = (ProceduralIntersection)target;
        var t    = pi.transform;
        var size = pi.Size;

        float hx = size.x * 0.5f;
        float hz = size.y * 0.5f;

        // Local midpoints of sides, in centered frame (-hx..+hx, -hz..+hz)
        Vector3 localMidSouth = new Vector3( 0f, 0f, -hz);
        Vector3 localMidEast  = new Vector3( hx, 0f,  0f);
        Vector3 localMidNorth = new Vector3( 0f, 0f,  hz);
        Vector3 localMidWest  = new Vector3(-hx, 0f,  0f);

        DrawSpawnButton(pi, t.TransformPoint(localMidSouth), Vector3.back,   Side.South);
        DrawSpawnButton(pi, t.TransformPoint(localMidEast),  Vector3.right,  Side.East);
        DrawSpawnButton(pi, t.TransformPoint(localMidNorth), Vector3.forward,Side.North);
        DrawSpawnButton(pi, t.TransformPoint(localMidWest),  Vector3.left,   Side.West);
    }

    void DrawSpawnButton(ProceduralIntersection pi, Vector3 worldPos, Vector3 facingDir, Side side)
    {
        Handles.color = Color.cyan;
        if (Handles.Button(worldPos,
                           Quaternion.LookRotation(facingDir, Vector3.up),
                           0.6f, 0.6f,
                           Handles.CubeHandleCap))
        {
            SpawnAdjacent(pi, side);
        }
    }

    void SpawnAdjacent(ProceduralIntersection src, Side side)
    {
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();

        var root = Object.FindFirstObjectByType<RoadSystemManager>();
        var cfg  = root ? root.config : null;

        var size = src.Size;

        var pieceType = RoadSystemAuthoringWindow.CurrentPieceType;

        // --------------------------------------------------------------------
        // INTERSECTION CASE – tile another intersection
        // --------------------------------------------------------------------
        if (pieceType == RoadPieceType.Intersection)
        {
            Vector3 localDelta = side switch
            {
                Side.South => new Vector3( 0f,     0f, -size.y),
                Side.East  => new Vector3( size.x, 0f,  0f),
                Side.North => new Vector3( 0f,     0f,  size.y),
                _          => new Vector3(-size.x, 0f,  0f), // West
            };

            Vector3 worldDelta = src.transform.TransformVector(localDelta);

            var go = new GameObject("Intersection");
            Undo.RegisterCreatedObjectUndo(go, "Create Intersection");

            go.transform.SetParent(root ? root.transform : src.transform.parent, worldPositionStays:false);
            go.transform.position = src.transform.position + worldDelta;
            go.transform.rotation = src.transform.rotation;

            var pi = go.AddComponent<ProceduralIntersection>();

            if (cfg)
            {
                // Size: authoring overrides → config default
                pi.Size       = RoadSystemAuthoringWindow.GetIntersectionSize();
                pi.RoadHeight = cfg.roadHeight;
                pi.material   = cfg.defaultMaterial;
                pi.ApplySharedDefaults(cfg);
            }
            else
            {
                // Fallback: copy from source
                pi.Size       = src.Size;
                pi.RoadHeight = src.RoadHeight;
                pi.material   = src.material;
            }

            // Connection flags between the two intersections
            switch (side)
            {
                case Side.South:
                    src.ConnectedSouth = true;
                    pi.ConnectedNorth  = true;
                    break;
                case Side.East:
                    src.ConnectedEast  = true;
                    pi.ConnectedWest   = true;
                    break;
                case Side.North:
                    src.ConnectedNorth = true;
                    pi.ConnectedSouth  = true;
                    break;
                case Side.West:
                    src.ConnectedWest  = true;
                    pi.ConnectedEast   = true;
                    break;
            }

             // Build topology for src + new intersection
            src.Rebuild();

            // Now auto-connect the *new* intersection to any roads whose endpoints
            // line up with its side midpoints
            if (root != null)
            {
                const float epsilon = 0.05f;
                root.AutoConnectIntersectionToRoads(pi, epsilon);
            }

            // Finally rebuild the new intersection with all connection flags applied
            pi.Rebuild();

            Undo.CollapseUndoOperations(group);
            EditorUtility.SetDirty(src);
            EditorUtility.SetDirty(pi.gameObject);
            EditorSceneManager.MarkSceneDirty(go.scene);
            return;
        }

        // --------------------------------------------------------------------
        // ROAD CASE – spawn a road from an intersection side
        // --------------------------------------------------------------------

        float hx = size.x * 0.5f;
        float hz = size.y * 0.5f;

        // local midpoints of each side (match OnSceneGUI)
        Vector3 localMid = side switch
        {
            Side.South => new Vector3( 0f, 0f, -hz),
            Side.East  => new Vector3( hx, 0f,  0f),
            Side.North => new Vector3( 0f, 0f,  hz),
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
        roadGO.transform.rotation = Quaternion.LookRotation(worldOut, src.transform.up);

        var pr = roadGO.AddComponent<ProceduralRoad>();

        if (cfg)
        {
            // Geometry from authoring overrides → config defaults
            pr.width         = RoadSystemAuthoringWindow.GetRoadWidth();
            pr.length        = RoadSystemAuthoringWindow.GetRoadLength();
            pr.footpathDepth = RoadSystemAuthoringWindow.GetRoadFootpathDepth();

            // Shared config values
            pr.RoadHeight    = cfg.roadHeight;
            pr.material      = cfg.defaultMaterial;
            pr.curb          = cfg.curb;
        }
        else
        {
            // Fallback from intersection size / shared props
            pr.width         = size.x;
            pr.length        = size.y;

            pr.RoadHeight    = src.RoadHeight;
            pr.material      = src.material;
            // pr.footpathDepth / pr.curb stay at component defaults
        }

        // ProceduralRoad builds geometry along +Z from the back edge pivot
        pr.Axis = RoadAxis.Z;

        // Mark this side as connected on the *source* intersection
        switch (side)
        {
            case Side.North: src.ConnectedNorth = true; break;
            case Side.East:  src.ConnectedEast  = true; break;
            case Side.South: src.ConnectedSouth = true; break;
            case Side.West:  src.ConnectedWest  = true; break;
        }

        src.Rebuild();
        pr.Rebuild();

        // Auto-connect the far end of the new road if it meets another intersection
        if (root)
        {
            bool alongZRoad = (pr.Axis == RoadAxis.Z);

            Vector3 backLocal  = Vector3.zero;
            Vector3 frontLocal = alongZRoad
                ? new Vector3(0f,       0f, pr.length)
                : new Vector3(pr.length, 0f, 0f);

            Vector3 frontWorld = pr.transform.TransformPoint(frontLocal);
            const float epsilon = 0.05f;

            if (root.TryConnectIntersectionAt(frontWorld, epsilon,
                                              out var otherInt, out var otherSide))
            {
                if (otherInt != src)
                {
                    switch (otherSide)
                    {
                        case Side.North: otherInt.ConnectedNorth = true; break;
                        case Side.East:  otherInt.ConnectedEast  = true; break;
                        case Side.South: otherInt.ConnectedSouth = true; break;
                        case Side.West:  otherInt.ConnectedWest  = true; break;
                    }

                    otherInt.Rebuild();
                    EditorUtility.SetDirty(otherInt.gameObject);
                }
            }
        }

        Undo.CollapseUndoOperations(group);
        EditorUtility.SetDirty(src);
        EditorUtility.SetDirty(pr.gameObject);
        EditorSceneManager.MarkSceneDirty(roadGO.scene);
    }
}
