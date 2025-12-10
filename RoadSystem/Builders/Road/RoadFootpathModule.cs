using System.Collections.Generic;
using UnityEngine;

public static class RoadFootpathModule
{
    public static void Create(
        PBMeshBuilder builder,
        float width,
        float length,
        float roadHeight,
        float footpathDepth,
        CurbGutter curb,
        RoadAxis axis)
    {
        // Build both sides in road-local coordinates.
        BuildSide(builder, width, length, roadHeight, footpathDepth, curb, axis, isLeft: true);
        BuildSide(builder, width, length, roadHeight, footpathDepth, curb, axis, isLeft: false);
    }

    /// <summary>
    /// Builds one side (left or right) footpath + curb + gutter using the same profile as FootpathModule:
    /// - Footpath depth = walkable area
    /// - Then skirtOut and gutterWidth push further toward the carriageway
    /// - skirtDown and gutterDepth define vertical drops
    /// The final carriageway start is at (footpathDepth + skirtOut + gutterWidth) from the road's outer edge.
    /// </summary>
    private static void BuildSide(
        PBMeshBuilder builder,
        float width,
        float length,
        float roadHeight,
        float footpathDepth,
        CurbGutter curb,
        RoadAxis axis,
        bool isLeft)
    {
        if (footpathDepth <= 0f)
            return;

        // --- 1) Define a simple "footpath-local" frame (u along road, v toward carriageway) ---

        // In this local frame:
        //  u: 0..length  (along road direction)
        //  v: 0..footpathDepth (+ skirtOut + gutterWidth via extrusions) (toward road centre)
        //
        // This matches the intersection FootpathModule scheme where:
        //  xL..xR = along edge, z0..z1 = depth toward roadway.

        float u0 = 0f;
        float u1 = length;
        float vOuter = 0f;
        float vInner = footpathDepth;

        float y0 = 0f; // Footpath module builds around y=0 then uses ExtrusionUtil to hit roadHeight.

        // Footpath top slab (pathBase), same vertex pattern as intersection footpath:
        // v0: outer-back, v1: inner-back, v2: inner-front, v3: outer-front
        var pathBase = new Vector3[]
        {
            new Vector3(u0, y0, vOuter),
            new Vector3(u1, y0, vOuter),
            new Vector3(u1, y0, vInner), // inner/top edge v2->v3
            new Vector3(u0, y0, vInner),
        };

        // --- 2) Curb + gutter using ExtrusionUtil (same as intersection) -------

        // 2.1 Skirt: extrude inner edge (v2->v3) "out" and down
        // For our footpath-local frame, "outward" is +v (Vector3.forward). 
        var curbSkirt = ExtrusionUtil.ExtrudeEdgeOutDown(
            pathBase[2], pathBase[3],
            Vector3.forward,
            curb.skirtOut,
            curb.skirtDown
        );

        // 2.2 Gutter apron: continue down from skirt’s inner edge, no extra horizontal
        var gutterApron = ExtrusionUtil.ExtrudeEdgeOutDown(
            curbSkirt[1], curbSkirt[2],
            Vector3.forward,
            0f,
            curb.gutterDepth
        );

        // 2.3 Gutter skirt to road: push by gutterWidth, snap down to roadHeight
        var gutterSkirtToRoad = ExtrusionUtil.ExtrudeEdgeOutToWorldY(
            gutterApron[1], gutterApron[2],
            Vector3.forward,
            curb.gutterWidth,
            roadHeight
        );

        // --- Split into semantic groups (footpath-local) -----------------------

        var localFootpathFaces = new List<Vector3[]>
        {
            pathBase
        };

        var localCurbFaces = new List<Vector3[]>
        {
            curbSkirt
        };

        var localGutterDropFaces = new List<Vector3[]>
        {
            gutterApron
        };

        var localGutterRunFaces = new List<Vector3[]>
        {
            gutterSkirtToRoad
        };

        // --- 3) Map from footpath-local (u,v,y) to road-local (x,y,z) ----------

        float halfW = width * 0.5f;

        Vector3 outerOrigin;   // road-local position of the outer-back corner (u=0, v=0)
        Vector3 alongDir;      // road-local direction along the road (u increasing)
        Vector3 inwardDir;     // road-local direction toward road centre (v increasing)

        if (axis == RoadAxis.Z)
        {
            // Road along +Z, width along X, pivot at back centre (0,0,0).
            alongDir = Vector3.forward;

            if (isLeft)
            {
                // Left side: outer edge at x = -halfW, v goes +X toward centre.
                outerOrigin = new Vector3(-halfW, 0f, 0f);
                inwardDir   = Vector3.right;
            }
            else
            {
                // Right side: outer edge at x = +halfW, v goes -X toward centre.
                outerOrigin = new Vector3(+halfW, 0f, 0f);
                inwardDir   = Vector3.left;
            }
        }
        else // RoadAxis.X
        {
            // Road along +X, width along Z, pivot at back centre (0,0,0).
            alongDir = Vector3.right;

            if (isLeft)
            {
                // Left side: outer edge at z = -halfW, v goes +Z toward centre.
                outerOrigin = new Vector3(0f, 0f, -halfW);
                inwardDir   = Vector3.forward;
            }
            else
            {
                // Right side: outer edge at z = +halfW, v goes -Z toward centre.
                outerOrigin = new Vector3(0f, 0f, +halfW);
                inwardDir   = Vector3.back;
            }
        }

        // Helper to map a set of faces from (u,v,y) to (x,y,z) in road-local space
        List<Vector3[]> MapFaces(List<Vector3[]> src)
        {
            var mappedFaces = new List<Vector3[]>(src.Count);

            foreach (var face in src)
            {
                var mapped = new Vector3[face.Length];
                for (int i = 0; i < face.Length; i++)
                {
                    float u = face[i].x;
                    float v = face[i].z;
                    float y = face[i].y;

                    Vector3 pos = outerOrigin + alongDir * u + inwardDir * v;
                    pos.y = y;

                    mapped[i] = pos;
                }

                // Flip winding for left side so normals point up.
                if (isLeft)
                    System.Array.Reverse(mapped);

                mappedFaces.Add(mapped);
            }

            return mappedFaces;
        }

        // --- 4) Map each semantic group and submit with correct masks ----------

        var mappedFootpath      = MapFaces(localFootpathFaces);
        var mappedCurbFaces     = MapFaces(localCurbFaces);
        var mappedGutterDrop    = MapFaces(localGutterDropFaces);
        var mappedGutterRun     = MapFaces(localGutterRunFaces);

        builder.AddFaces(mappedFootpath,   RoadSurfaceMasks.Footpath);
        builder.AddFaces(mappedCurbFaces,  RoadSurfaceMasks.CurbFace);
        builder.AddFaces(mappedGutterDrop, RoadSurfaceMasks.GutterDrop);
        builder.AddFaces(mappedGutterRun,  RoadSurfaceMasks.GutterRun);
    }
}
