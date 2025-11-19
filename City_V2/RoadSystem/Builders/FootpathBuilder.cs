using UnityEngine;

public static class FootpathModule
{
    public static void CreateFootpath(PBMeshBuilder builder, Transform transform, IntersectionModel model, Side side)
    {
        var fp = model.GetFootpath(side);
        if (!fp.exists) return;

        var geo = fp.geometry;
        var curb = geo.curb;

        // ---- Compute extends from corners (your existing logic) ---------------
        static float ParamAlongEdge(in FootpathModel fpm, in Vector3 p)
        {
            float t = Vector3.Dot(p - fpm.edgeOrigin, fpm.edgeRight);
            return Mathf.Clamp(t, 0f, fpm.edgeLength);
        }

        float tL = fp.leftCorner.exists || fp.leftAdjExists ? ParamAlongEdge(fp, fp.leftCorner.apex) : 0f;
        float tR = fp.rightCorner.exists || fp.rightAdjExists ? ParamAlongEdge(fp, fp.rightCorner.apex) : fp.edgeLength;

        float mid = fp.edgeMid;
        float leftExtend = Mathf.Max(0f, mid - tL);
        float rightExtend = Mathf.Max(0f, tR - mid);

        float xL = mid - leftExtend;
        float xR = mid + rightExtend;

        float z0 = 0f;                 // outer edge
        float z1 = geo.depth;          // inner edge toward roadway
        float y = 0f;                 // build at y=0; world Y comes from final translation

        // ---- 0) Footpath slab (canonical; +Z points inward) -------------------
        var pathBase = new Quad(new[]
        {
            new Vector3(xL, y, z0), // v0
            new Vector3(xR, y, z0), // v1
            new Vector3(xR, y, z1), // v2  <-- inner/top edge (edgeIndex = 2: v2->v3)
            new Vector3(xL, y, z1), // v3
        });

        // ---- 1) Curb skirt: extrude inner edge out (+Z) and down -------------
        // Correct Quad usage: edgeIndex in [0..3], outward dir (horizontal), outAmount, downAmount
        var curbSkirt = new Quad(vertices: pathBase.ExtrudeEdgeOutDown(2, Vector3.forward, curb.skirtOut, curb.skirtDown));

        // ---- 2) Gutter apron: continue down from skirt’s inner outer-edge ----
        // Do another Quad extrusion with outAmount = 0 and DOWN = gutterDepth.
        // Use the skirt’s *same* top/inner edge index (still 2: its v2->v3)
        var gutterApron = ExtrusionUtil.ExtrudeEdgeOutDown(
                curbSkirt.Vertices[1], curbSkirt.Vertices[2], Vector3.forward, 0, curb.gutterDepth
            );


        // ---- 3) Gutter skirt to road Y: push horizontally by gutterWidth and snap to world Y
        // Use apronVerts[1] and apronVerts[2] as the edge endpoints.
        var gutterSkirtToRoad = ExtrusionUtil.ExtrudeEdgeOutToWorldY(
                gutterApron[1], gutterApron[2], Vector3.forward, curb.gutterWidth, model.RoadHeight
            );

        // --- 2) After curb/gutter: extend only the slab to meet outward corners --
        float join = curb.skirtOut + curb.gutterWidth;

        // Heuristic for “outward corner”: corner geometry absent but adjacent footpath exists.
        bool extendLeftSide = fp.leftAdjExists;
        bool extendRightSide = fp.rightAdjExists;

        // Copy the slab verts & push only the X on the sides that need joining.
        var path = (Vector3[])pathBase.Vertices.Clone();

        if (extendLeftSide)
        {
            path[0].x = Mathf.Max(0f, path[0].x - join);
            path[3].x = Mathf.Max(0f, path[3].x - join);
        }
        if (extendRightSide)
        {
            path[1].x = Mathf.Min(fp.edgeLength, path[1].x + join);
            path[2].x = Mathf.Min(fp.edgeLength, path[2].x + join);
        }


        // ---- Collect and place ------------------------------------------------
        Vector3[][] sets = {
            path,
            curbSkirt.Vertices,
            gutterApron,
            gutterSkirtToRoad
        };

        var (rotY, tx) = PlacementFor(side, model.Size);
        var rotated = VertexOperations.RotateMany(sets, new Vector3(0f, rotY, 0f), Vector3.zero);
        var placedLocal = VertexOperations.TranslateMany(rotated, tx);
        var placedWorld = VertexOperations.TranslateMany(placedLocal, transform.position);

        builder.AddQuadFace(placedWorld[0]); // path
        builder.AddQuadFace(placedWorld[1]); // curb skirt
        builder.AddQuadFace(placedWorld[2]); // gutter apron
        builder.AddQuadFace(placedWorld[3]); // gutter skirt to road
    }

    // Match CornerModule’s convention (clockwise/negative yaw & same translations)
    private static (float rotYdeg, Vector3 tx) PlacementFor(Side side, Vector2 size) => side switch
    {
        Side.South => (0f, new Vector3(0f, 0f, 0f)),
        Side.East => (-90f, new Vector3(size.x, 0f, 0f)),
        Side.North => (-180f, new Vector3(size.x, 0f, size.y)),
        _ => (-270f, new Vector3(0f, 0f, size.y)), // West
    };
}