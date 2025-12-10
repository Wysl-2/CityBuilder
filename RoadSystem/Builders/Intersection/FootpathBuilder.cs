using System.Collections.Generic;
using UnityEngine;

public static class FootpathModule
{
    public static void CreateFootpath(PBMeshBuilder builder, Transform transform, IntersectionModel model, Side side)
    {
        var fp = model.GetFootpath(side);
        if (!fp.exists) return;

        var geo  = fp.geometry;
        var curb = geo.curb;

        // ---- Compute extends from corners (existing logic) --------------------
        static float ParamAlongEdge(in FootpathModel fpm, in Vector3 p)
        {
            float t = Vector3.Dot(p - fpm.edgeOrigin, fpm.edgeRight);
            return Mathf.Clamp(t, 0f, fpm.edgeLength);
        }

        float tL = fp.leftCorner.exists  || fp.leftAdjExists  ? ParamAlongEdge(fp, fp.leftCorner.apex)  : 0f;
        float tR = fp.rightCorner.exists || fp.rightAdjExists ? ParamAlongEdge(fp, fp.rightCorner.apex) : fp.edgeLength;

        float mid         = fp.edgeMid;
        float leftExtend  = Mathf.Max(0f, mid - tL);
        float rightExtend = Mathf.Max(0f, tR - mid);

        float xL = mid - leftExtend;
        float xR = mid + rightExtend;

        float z0 = 0f;        // outer edge
        float z1 = geo.depth; // inner edge toward roadway
        float y  = 0f;        // local y; world Y added at final translate

        // ---- 0) Footpath slab (+Z inward) ------------------------------------
        var pathBase = new Vector3[]
        {
            new Vector3(xL, y, z0), // v0
            new Vector3(xR, y, z0), // v1
            new Vector3(xR, y, z1), // v2  (inner/top edge v2->v3)
            new Vector3(xL, y, z1), // v3
        };

        // ---- Curb skirt: extrude inner edge out (+Z) and down ----------------
        var curbSkirt = ExtrusionUtil.ExtrudeEdgeOutDown(
            pathBase[2], pathBase[3], Vector3.forward, curb.skirtOut, curb.skirtDown);

        // ---- Gutter apron: continue down from skirt’s inner edge -------------
        var gutterApron = ExtrusionUtil.ExtrudeEdgeOutDown(
            curbSkirt[1], curbSkirt[2], Vector3.forward, 0f, curb.gutterDepth);

        // ---- Gutter skirt to road Y: push by gutterWidth, snap to world Y ----
        var gutterSkirtToRoad = ExtrusionUtil.ExtrudeEdgeOutToWorldY(
            gutterApron[1], gutterApron[2], Vector3.forward, curb.gutterWidth, model.RoadHeight);

        // ---- Extend only slab to meet outward corners ------------------------
        float join = curb.skirtOut + curb.gutterWidth;
        bool extendLeftSide  = fp.leftAdjExists;
        bool extendRightSide = fp.rightAdjExists;

        var path = (Vector3[])pathBase.Clone();
        if (extendLeftSide)
        {
            path[0].x = Mathf.Max(0f,          path[0].x - join);
            path[3].x = Mathf.Max(0f,          path[3].x - join);
        }
        if (extendRightSide)
        {
            path[1].x = Mathf.Min(fp.edgeLength, path[1].x + join);
            path[2].x = Mathf.Min(fp.edgeLength, path[2].x + join);
        }

        // --- Placement / recentering -----------------------------------------
        var size = model.Size;
        var (rot, tx) = PlacementFor(side, size);
        var localRotation = rot;
        var centerOffset  = new Vector3(-size.x * 0.5f, 0f, -size.y * 0.5f);

        List<Vector3[]> Place(List<Vector3[]> src)
        {
            var rotated      = VertexOperations.RotateMany(src, localRotation, Vector3.zero);
            var placedLocal  = VertexOperations.TranslateMany(rotated, tx);
            return VertexOperations.TranslateMany(placedLocal, centerOffset);
        }

        // --- Footpath surface -------------------------------------------------
        {
            var footpathFaces = new List<Vector3[]> { path };
            builder.AddFaces(Place(footpathFaces), RoadSurfaceMasks.Footpath);
        }

        // --- Curb face --------------------------------------------------------
        {
            var curbFaces = new List<Vector3[]> { curbSkirt };
            builder.AddFaces(Place(curbFaces), RoadSurfaceMasks.CurbFace);
        }

        // --- Gutter drop ------------------------------------------------------
        {
            var gutterDropFaces = new List<Vector3[]> { gutterApron };
            builder.AddFaces(Place(gutterDropFaces), RoadSurfaceMasks.GutterDrop);
        }

        // --- Gutter run (to road) --------------------------------------------
        {
            var gutterRunFaces = new List<Vector3[]> { gutterSkirtToRoad };
            builder.AddFaces(Place(gutterRunFaces), RoadSurfaceMasks.GutterRun);
        }
    }

    // -- Static Helpers --
    // Match CornerModule’s convention (negative yaw). Return Quaternion + tx.
    private static (Quaternion rot, Vector3 tx) PlacementFor(Side side, Vector2 size) => side switch
    {
        Side.South => (Quaternion.Euler(0f,   0f, 0f), new Vector3(0f,      0f, 0f)),
        Side.East  => (Quaternion.Euler(0f, -90f, 0f), new Vector3(size.x, 0f, 0f)),
        Side.North => (Quaternion.Euler(0f,-180f, 0f), new Vector3(size.x, 0f, size.y)),
        _          => (Quaternion.Euler(0f,-270f, 0f), new Vector3(0f,      0f, size.y)), // West
    };
}
