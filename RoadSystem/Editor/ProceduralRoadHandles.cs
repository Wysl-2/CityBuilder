using UnityEditor;
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

        // Back is origin
        Vector3 back = Vector3.zero;

        DrawSpawnButton(pr, t.TransformPoint(front), alongZ ? Vector3.forward : Vector3.right);
        DrawSpawnButton(pr, t.TransformPoint(back),  alongZ ? Vector3.back    : Vector3.left);
    }

    void DrawSpawnButton(ProceduralRoad pr, Vector3 worldPos, Vector3 facingDir)
    {
        Handles.color = Color.green;
        if (Handles.Button(worldPos, Quaternion.LookRotation(facingDir, Vector3.up), 0.6f, 0.6f, Handles.CubeHandleCap))
        {
            SpawnAdjacent(pr, facingDir);
        }
    }

    void SpawnAdjacent(ProceduralRoad src, Vector3 dir)
    {
        Undo.IncrementCurrentGroup();
        var group = Undo.GetCurrentGroup();

        var root = FindFirstObjectByType<RoadSystemManager>();
        var cfg  = root ? root.config : null;

        var pieceType = RoadSystemAuthoringWindow.CurrentPieceType;

        // ---- ROAD CASE (unchanged behaviour) ----
        if (pieceType == RoadPieceType.Road)
        {
            float len = src.length;
            Vector3 localDelta = dir.normalized * len;
            Vector3 worldDelta = src.transform.TransformVector(localDelta);

            var go = new GameObject("Road");
            Undo.RegisterCreatedObjectUndo(go, "Create Road");

            go.transform.SetParent(root ? root.transform : src.transform.parent, false);
            go.transform.position = src.transform.position + worldDelta;
            go.transform.rotation = src.transform.rotation;

            var pr = go.AddComponent<ProceduralRoad>();
            pr.length     = src.length;
            pr.width      = src.width;
            pr.RoadHeight = src.RoadHeight;
            pr.material   = src.material;
            pr.Axis       = src.Axis;

            pr.Rebuild();

            Undo.CollapseUndoOperations(group);
            EditorUtility.SetDirty(src);
            EditorUtility.SetDirty(pr.gameObject);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(go.scene);
            return;
        }

        // ---- INTERSECTION CASE (centered pivot logic) ----

        bool alongZ = src.Axis == RoadAxis.Z;

        bool isFrontHandle =
            (alongZ  && dir == Vector3.forward) ||
            (!alongZ && dir == Vector3.right);

        // Road handle local positions (unchanged)
        Vector3 localFront = alongZ
            ? new Vector3(0f, 0f, src.length)
            : new Vector3(src.length, 0f, 0f);

        Vector3 localBack = Vector3.zero;
        Vector3 localHandle = isFrontHandle ? localFront : localBack;
        Vector3 worldHandlePos = src.transform.TransformPoint(localHandle);

        // --- Create intersection ---
        var goInt = new GameObject("Intersection");
        Undo.RegisterCreatedObjectUndo(goInt, "Create Intersection");

        goInt.transform.SetParent(root ? root.transform : src.transform.parent, worldPositionStays:false);
        goInt.transform.rotation = src.transform.rotation;

        var pi = goInt.AddComponent<ProceduralIntersection>();

        if (cfg)
        {
            pi.Size       = cfg.defaultSize;
            pi.RoadHeight = cfg.roadHeight;
            pi.material   = cfg.defaultMaterial;
            pi.ApplySharedDefaults(cfg);
        }
        else
        {
            pi.Size       = new Vector2(src.width, src.length);
            pi.RoadHeight = src.RoadHeight;
            pi.material   = src.material;
        }

        // --- Center-pivot intersection side midpoints ---
        Vector2 size = pi.Size;
        float hx = size.x * 0.5f;
        float hz = size.y * 0.5f;

        Vector3 localSideMid;

        if (alongZ)
        {
            if (isFrontHandle)
            {
                // Road extends +Z → connects to SOUTH side (z = -hz)
                localSideMid = new Vector3(0f, 0f, -hz);
            }
            else
            {
                // Back handle → NORTH side (z = +hz)
                localSideMid = new Vector3(0f, 0f,  hz);
            }
        }
        else // Axis == X
        {
            if (isFrontHandle)
            {
                // Road extends +X → connects to WEST side (x = -hx)
                localSideMid = new Vector3(-hx, 0f, 0f);
            }
            else
            {
                // Back handle → EAST side (x = +hx)
                localSideMid = new Vector3( hx, 0f, 0f);
            }
        }

        // Position intersection so side midpoint snaps to road handle
        Vector3 worldOffset = goInt.transform.rotation * localSideMid;
        goInt.transform.position = worldHandlePos - worldOffset;

        // --- Connection flags ---
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

        pi.Rebuild();

        Undo.CollapseUndoOperations(group);
        EditorUtility.SetDirty(src);
        EditorUtility.SetDirty(pi.gameObject);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(goInt.scene);
    }




}
