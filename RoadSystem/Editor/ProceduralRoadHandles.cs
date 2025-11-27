// Editor/ProceduralRoadHandles.cs
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(ProceduralRoad))]
public class ProceduralRoadHandles : Editor
{
    void OnSceneGUI()
    {
        var pr = (ProceduralRoad)target;
        var t  = pr.transform;

        float width  = pr.width;
        float length = pr.length;

        bool alongZ = pr.Axis == RoadAxis.Z;

        // The forward handle sits at the end of the length axis.
        Vector3 front = alongZ
            ? new Vector3(0f, 0f, length)
            : new Vector3(length, 0f, 0f);

        // Back is origin (pivot at back edge centre)
        Vector3 back = Vector3.zero;

        DrawSpawnButton(pr, t.TransformPoint(front), alongZ ? Vector3.forward : Vector3.right);
        DrawSpawnButton(pr, t.TransformPoint(back),  alongZ ? Vector3.back    : Vector3.left);
    }

    void DrawSpawnButton(ProceduralRoad pr, Vector3 worldPos, Vector3 facingDir)
    {
        Handles.color = Color.green;
        if (Handles.Button(worldPos,
                           Quaternion.LookRotation(facingDir, Vector3.up),
                           0.6f, 0.6f,
                           Handles.CubeHandleCap))
        {
            SpawnAdjacent(pr, facingDir);
        }
    }

    void SpawnAdjacent(ProceduralRoad src, Vector3 dir)
    {
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();

        var root = Object.FindFirstObjectByType<RoadSystemManager>();
        var cfg  = root ? root.config : null;

        var pieceType = RoadSystemAuthoringWindow.CurrentPieceType;

        // --------------------------------------------------------------------
        // ROAD CASE – extend a road chain
        // --------------------------------------------------------------------
        if (pieceType == RoadPieceType.Road)
        {
            float len = src.length;
            Vector3 localDelta = dir.normalized * len;
            Vector3 worldDelta = src.transform.TransformVector(localDelta);

            var go = new GameObject("Road");
            Undo.RegisterCreatedObjectUndo(go, "Create Road");

            go.transform.SetParent(root ? root.transform : src.transform.parent, worldPositionStays:false);
            go.transform.position = src.transform.position + worldDelta;
            go.transform.rotation = src.transform.rotation;

            var pr = go.AddComponent<ProceduralRoad>();

            if (cfg)
            {
                // Geometry from authoring overrides → config defaults
                pr.width         = RoadSystemAuthoringWindow.GetRoadWidth();
                pr.length        = RoadSystemAuthoringWindow.GetRoadLength();
                pr.footpathDepth = RoadSystemAuthoringWindow.GetRoadFootpathDepth();

                // Shared values from config
                pr.RoadHeight    = cfg.roadHeight;
                pr.material      = cfg.defaultMaterial;
                pr.curb          = cfg.curb;
            }
            else
            {
                // Fallback: clone from source road
                pr.width         = src.width;
                pr.length        = src.length;
                pr.footpathDepth = src.footpathDepth;
                pr.RoadHeight    = src.RoadHeight;
                pr.material      = src.material;
                pr.curb          = src.curb;
            }

            pr.Axis = src.Axis;

            pr.Rebuild();

            // Auto-connect both ends to any existing intersections
            if (root)
            {
                bool alongZRoad = (pr.Axis == RoadAxis.Z);

                Vector3 backLocal  = Vector3.zero;
                Vector3 frontLocal = alongZRoad
                    ? new Vector3(0f,       0f, pr.length)
                    : new Vector3(pr.length, 0f, 0f);

                Vector3 backWorld  = pr.transform.TransformPoint(backLocal);
                Vector3 frontWorld = pr.transform.TransformPoint(frontLocal);

                const float epsilon = 0.05f;

                // Back end
                if (root.TryConnectIntersectionAt(backWorld, epsilon,
                                                  out var backInt, out var backSide))
                {
                    switch (backSide)
                    {
                        case Side.North: backInt.ConnectedNorth = true; break;
                        case Side.East:  backInt.ConnectedEast  = true; break;
                        case Side.South: backInt.ConnectedSouth = true; break;
                        case Side.West:  backInt.ConnectedWest  = true; break;
                    }
                    backInt.Rebuild();
                    EditorUtility.SetDirty(backInt.gameObject);
                }

                // Front end
                if (root.TryConnectIntersectionAt(frontWorld, epsilon,
                                                  out var frontInt, out var frontSide))
                {
                    switch (frontSide)
                    {
                        case Side.North: frontInt.ConnectedNorth = true; break;
                        case Side.East:  frontInt.ConnectedEast  = true; break;
                        case Side.South: frontInt.ConnectedSouth = true; break;
                        case Side.West:  frontInt.ConnectedWest  = true; break;
                    }
                    frontInt.Rebuild();
                    EditorUtility.SetDirty(frontInt.gameObject);
                }
            }

            Undo.CollapseUndoOperations(group);
            EditorUtility.SetDirty(src);
            EditorUtility.SetDirty(pr.gameObject);
            EditorSceneManager.MarkSceneDirty(go.scene);
            return;
        }

        // --------------------------------------------------------------------
        // INTERSECTION CASE – attach an intersection at a road end
        // --------------------------------------------------------------------

        bool alongZ = src.Axis == RoadAxis.Z;

        bool isFrontHandle =
            (alongZ  && dir == Vector3.forward) ||
            (!alongZ && dir == Vector3.right);

        // Road handle local positions (back = origin, front = +length along axis)
        Vector3 localFront = alongZ
            ? new Vector3(0f, 0f, src.length)
            : new Vector3(src.length, 0f, 0f);

        Vector3 localBack   = Vector3.zero;
        Vector3 localHandle = isFrontHandle ? localFront : localBack;
        Vector3 worldHandlePos = src.transform.TransformPoint(localHandle);

        var goInt = new GameObject("Intersection");
        Undo.RegisterCreatedObjectUndo(goInt, "Create Intersection");

        goInt.transform.SetParent(root ? root.transform : src.transform.parent, worldPositionStays:false);
        goInt.transform.rotation = src.transform.rotation;

        var pi = goInt.AddComponent<ProceduralIntersection>();

        if (cfg)
        {
            // Intersection size: authoring overrides → config default
            pi.Size       = RoadSystemAuthoringWindow.GetIntersectionSize();
            pi.RoadHeight = cfg.roadHeight;
            pi.material   = cfg.defaultMaterial;
            pi.ApplySharedDefaults(cfg);
        }
        else
        {
            // Fallback: approximate from road
            pi.Size       = new Vector2(src.width, src.length);
            pi.RoadHeight = src.RoadHeight;
            pi.material   = src.material;
        }

        Vector2 sizeInt = pi.Size;
        float hx = sizeInt.x * 0.5f;
        float hz = sizeInt.y * 0.5f;

        // Which side of the intersection touches this road?
        Vector3 localSideMid;
        if (alongZ)
        {
            // Road’s +Z end touches SOUTH; 0 end touches NORTH.
            localSideMid = isFrontHandle
                ? new Vector3(0f, 0f, -hz) // South midpoint
                : new Vector3(0f, 0f,  hz); // North midpoint
        }
        else
        {
            // Road’s +X end touches WEST; 0 end touches EAST.
            localSideMid = isFrontHandle
                ? new Vector3(-hx, 0f, 0f) // West midpoint
                : new Vector3( hx, 0f, 0f); // East midpoint
        }

        // Place intersection so that side midpoint lands exactly on road handle
        Vector3 worldOffset = goInt.transform.rotation * localSideMid;
        goInt.transform.position = worldHandlePos - worldOffset;

        // Mark connection flag on the new intersection
        if (alongZ)
        {
            if (isFrontHandle) pi.ConnectedSouth = true;
            else               pi.ConnectedNorth = true;
        }
        else
        {
            if (isFrontHandle) pi.ConnectedWest = true;
            else               pi.ConnectedEast = true;
        }

        if (root != null)
        {
            const float epsilon = 0.05f;
            root.AutoConnectIntersectionToRoads(pi, epsilon);
        }

        // Now rebuild with all connections applied
        pi.Rebuild();

        Undo.CollapseUndoOperations(group);
        EditorUtility.SetDirty(src);
        EditorUtility.SetDirty(pi.gameObject);
        EditorSceneManager.MarkSceneDirty(goInt.scene);
    }
}
