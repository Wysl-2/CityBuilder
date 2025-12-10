using System;
using System.Collections.Generic;
using UnityEngine;

public static class CornerModule
{
    public static void CreateCorner(
        PBMeshBuilder builder,
        IntersectionModel model,
        CornerModel corner,
        CornerId cornerId,
        Transform transform)
    {
        if (!corner.exists) return;

        if (corner.type == CornerType.InwardFacing)
        {
            CreateInwardCorner(builder, model, corner, cornerId, transform);
        }
        else
        {
            CreateOutwardCorner(builder, model, corner, cornerId, transform);
        }
    }

    // CCW quad on XZ at y: v0..v3 = (x0,z0)->(x1,z0)->(x1,z1)->(x0,z1)
    static Vector3[] MakeQuadXZ(float x0, float x1, float z0, float z1, float y) => new[]
    {
        new Vector3(x0,y,z0), new Vector3(x1,y,z0), new Vector3(x1,y,z1), new Vector3(x0,y,z1)
    };

    public static void CreateInwardCorner(
        PBMeshBuilder builder,
        IntersectionModel intersectionModel,
        CornerModel corner,
        CornerId cornerId,
        Transform transform)
    {
        bool swapXZ = cornerId == CornerId.NW || cornerId == CornerId.SE;
        float sx = swapXZ ? corner.geometry.zSize : corner.geometry.xSize;
        float sz = swapXZ ? corner.geometry.xSize : corner.geometry.zSize;

        float skirtOut    = corner.geometry.curb.skirtOut;
        float skirtDown   = corner.geometry.curb.skirtDown;
        float gutterDepth = corner.geometry.curb.gutterDepth;
        float gutterWidth = corner.geometry.curb.gutterWidth;
        float roadY       = intersectionModel.RoadHeight;

        List<Vector3[]> BuildGeometry()
        {
            var faces = new List<Vector3[]>(capacity: 11);

            // Canonical local: +x/+z are inward
            var qLocal = MakeQuadXZ(0f, sx, 0f, sz, 0f);

            // Skirts from inner edges:
            var e1a = qLocal[1];  // start of edge 1 (v1)
            var e1b = qLocal[2];  // end   of edge 1 (v2)
            var e2a = qLocal[2];  // start of edge 2 (v2)
            var e2b = qLocal[3];  // end   of edge 2 (v3)

            var skirtX = ExtrusionUtil.ExtrudeEdgeOutDown(
                e1a, e1b, Vector3.right,   skirtOut, skirtDown);
            var skirtZ = ExtrusionUtil.ExtrudeEdgeOutDown(
                e2a, e2b, Vector3.forward, skirtOut, skirtDown);

            // Footpath triangle wedge between skirts
            var triangleCap = new[]
            {
                qLocal[2], skirtX[2], skirtZ[1]
            };

            // Gutter aprons (step down but still inward)
            var gutterApronX = ExtrusionUtil.ExtrudeEdgeOutDown(
                skirtX[1], skirtX[2], Vector3.right,  0, gutterDepth);
            var gutterApronZ = ExtrusionUtil.ExtrudeEdgeOutDown(
                skirtZ[1], skirtZ[2], Vector3.forward, 0, gutterDepth);

            var cornerQuadCap = new[]
            {
                triangleCap[2], triangleCap[1], gutterApronX[2], gutterApronZ[1]
            };

            // Skirts from gutter apron outwards to road height
            var gutterSkirtX = ExtrusionUtil.ExtrudeEdgeOutToWorldY(
                gutterApronX[1], gutterApronX[2], Vector3.right,  gutterWidth, roadY);

            var gutterSkirtZ = ExtrusionUtil.ExtrudeEdgeOutToWorldY(
                gutterApronZ[1], gutterApronZ[2], Vector3.forward, gutterWidth, roadY);

            var gutterSkirtCap = new[]
            {
                gutterSkirtX[3], gutterSkirtX[2], gutterSkirtZ[1], gutterSkirtZ[0],
            };

            // Road triangle at road height (wedge that meets the straight road)
            Vector3 apexLocal = new Vector3(
                qLocal[2].x + skirtOut + gutterWidth,
                roadY,
                qLocal[2].z + skirtOut + gutterWidth);

            var roadTriangleCap = new[]
            {
                gutterSkirtCap[2], gutterSkirtCap[1], apexLocal
            };

            faces.Add(qLocal);           //  0 - footpath slab
            faces.Add(skirtX);           //  1 - curb skirt (X)
            faces.Add(skirtZ);           //  2 - curb skirt (Z)
            faces.Add(triangleCap);      //  3 - footpath wedge
            faces.Add(gutterApronX);     //  4 - gutter apron (X)
            faces.Add(gutterApronZ);     //  5 - gutter apron (Z)
            faces.Add(cornerQuadCap);    //  6 - transition down into gutter
            faces.Add(gutterSkirtX);     //  7 - gutter run (X)
            faces.Add(gutterSkirtZ);     //  8 - gutter run (Z)
            faces.Add(gutterSkirtCap);   //  9 - gutter run cap at road height
            faces.Add(roadTriangleCap);  // 10 - road wedge

            return faces;
        }

        var cornerGeometry = BuildGeometry();
        var size           = intersectionModel.Size;

        List<Vector3[]> ApplyRotationAndTranslationFor(CornerId id, List<Vector3[]> geo)
        {
            Vector3 euler, tx;

            switch (id)
            {
                case CornerId.SW: euler = new Vector3(0,   0, 0);  tx = new Vector3(0,      0, 0);      break;
                case CornerId.SE: euler = new Vector3(0, -90, 0);  tx = new Vector3(size.x, 0, 0);      break;
                case CornerId.NE: euler = new Vector3(0,-180, 0);  tx = new Vector3(size.x, 0, size.y); break;
                case CornerId.NW: euler = new Vector3(0,-270, 0);  tx = new Vector3(0,      0, size.y); break;
                default: throw new ArgumentException("Invalid cornerId.", nameof(id));
            }

            var q       = Quaternion.Euler(euler);
            var rotated = VertexOperations.RotateMany(geo, q, Vector3.zero);
            var shifted = VertexOperations.TranslateMany(rotated, tx);
            return shifted;
        }

        // --- Split into semantic groups ----------------------------------

        // Footpath surface: top slab + footpath wedge
        var footpathFaces = new List<Vector3[]>
        {
            cornerGeometry[0], // qLocal
            cornerGeometry[3], // triangleCap
        };

        // Curb face: sloped skirts from footpath
        var curbFaces = new List<Vector3[]>
        {
            cornerGeometry[1], // skirtX
            cornerGeometry[2], // skirtZ
        };

        // Gutter drop: apron + transition down into gutter
        var gutterDropFaces = new List<Vector3[]>
        {
            cornerGeometry[4], // gutterApronX
            cornerGeometry[5], // gutterApronZ
            cornerGeometry[6], // cornerQuadCap
        };

        // Gutter run: skirts up to road and their cap
        var gutterRunFaces = new List<Vector3[]>
        {
            cornerGeometry[7], // gutterSkirtX
            cornerGeometry[8], // gutterSkirtZ
            cornerGeometry[9], // gutterSkirtCap
        };

        // Road carriageway: road wedge
        var roadFaces = new List<Vector3[]>
        {
            cornerGeometry[10], // roadTriangleCap
        };

        // Local helper to apply rotation/translation and recenter
        List<Vector3[]> Place(List<Vector3[]> src)
        {
            var placed       = ApplyRotationAndTranslationFor(cornerId, src);
            var centerOffset = new Vector3(-size.x * 0.5f, 0f, -size.y * 0.5f);
            return VertexOperations.TranslateMany(placed, centerOffset);
        }

        // Add groups with correct surface masks (vertex colors)
        builder.AddFaces(Place(footpathFaces),   RoadSurfaceMasks.Footpath);
        builder.AddFaces(Place(curbFaces),       RoadSurfaceMasks.CurbFace);
        builder.AddFaces(Place(gutterDropFaces), RoadSurfaceMasks.GutterDrop);
        builder.AddFaces(Place(gutterRunFaces),  RoadSurfaceMasks.GutterRun);
        builder.AddFaces(Place(roadFaces),       RoadSurfaceMasks.Road);
    }

    public static void CreateOutwardCorner(
        PBMeshBuilder builder,
        IntersectionModel model,
        CornerModel corner,
        CornerId cornerId,
        Transform transform)
    {
        // Build in canonical local where +X and +Z go "inward" from the corner origin.
        bool swapXZ = cornerId == CornerId.NW || cornerId == CornerId.SE;
        float sx = swapXZ ? corner.geometry.zSize : corner.geometry.xSize;
        float sz = swapXZ ? corner.geometry.xSize : corner.geometry.zSize;

        var   curb   = model.config.curb;
        float y      = 0f;
        float offset = curb.skirtOut + curb.gutterWidth;

        // Footpath pad filling the empty L-wedge:
        var pad = new Vector3[]
        {
            new Vector3(0,  y, 0),
            new Vector3(sx, y, 0),
            new Vector3(sx, y, sz),
            new Vector3(0,  y, sz)
        };

        // Footpath triangle extending towards the curb/gutter region
        var footpathCap = new Vector3[]
        {
            pad[2],
            new Vector3(pad[2].x + offset, pad[2].y, pad[2].z),
            new Vector3(pad[2].x,          pad[2].y, pad[2].z + offset),
        };

        // Curb skirt (down + out from footpath cap)
        Vector3[] curbSkirt = new Vector3[]
        {
            new Vector3(footpathCap[1].x,                 footpathCap[1].y - curb.skirtDown, footpathCap[1].z + curb.skirtOut),
            new Vector3(footpathCap[2].x + curb.skirtOut, footpathCap[2].y - curb.skirtDown, footpathCap[2].z),
            footpathCap[2],
            footpathCap[1],
        };

        // Gutter apron (further down from curb skirt)
        Vector3[] gutterApron = new Vector3[]
        {
            curbSkirt[0],
            new Vector3(curbSkirt[0].x, curbSkirt[0].y - curb.gutterDepth, curbSkirt[0].z),
            new Vector3(curbSkirt[1].x, curbSkirt[1].y - curb.gutterDepth, curbSkirt[1].z),
            curbSkirt[1]
        };

        // Apex at road height where gutter meets road
        Vector3 apexLocal = new Vector3(
            pad[2].x + curb.skirtOut + curb.gutterWidth,
            model.RoadHeight,
            pad[2].z + curb.skirtOut + curb.gutterWidth
        );

        // Triangle bridging gutter apron up to road apex
        Vector3[] gutterCap = new Vector3[]
        {
            gutterApron[2],
            gutterApron[1],
            apexLocal,
        };

        // Group faces by semantic surface type
        var footpathFaces = new List<Vector3[]>
        {
            pad,
            footpathCap
        };

        var curbFaces = new List<Vector3[]>
        {
            curbSkirt
        };

        var gutterDropFaces = new List<Vector3[]>
        {
            gutterApron
        };

        var gutterRunFaces = new List<Vector3[]>
        {
            gutterCap
        };

        // Rotation (Quaternion) + translation
        Vector3 euler, tx;
        var size = model.Size;

        switch (cornerId)
        {
            case CornerId.SW: euler = new Vector3(0,   0, 0);  tx = new Vector3(0,      0, 0);      break;
            case CornerId.SE: euler = new Vector3(0, -90, 0);  tx = new Vector3(size.x, 0, 0);      break;
            case CornerId.NE: euler = new Vector3(0,-180, 0);  tx = new Vector3(size.x, 0, size.y); break;
            default:          euler = new Vector3(0,-270, 0);  tx = new Vector3(0,      0, size.y); break; // NW
        }

        var localRotation = Quaternion.Euler(euler);

        // Helper to rotate/translate/recenter a group of faces
        List<Vector3[]> Place(List<Vector3[]> src)
        {
            var rotated      = VertexOperations.RotateMany(src, localRotation, Vector3.zero);
            var placedLocal  = VertexOperations.TranslateMany(rotated, tx);
            var centerOffset = new Vector3(-size.x * 0.5f, 0f, -size.y * 0.5f);
            return VertexOperations.TranslateMany(placedLocal, centerOffset);
        }

        // Apply transforms and add with appropriate surface masks
        builder.AddFaces(Place(footpathFaces),   RoadSurfaceMasks.Footpath);
        builder.AddFaces(Place(curbFaces),       RoadSurfaceMasks.CurbFace);
        builder.AddFaces(Place(gutterDropFaces), RoadSurfaceMasks.GutterDrop);
        builder.AddFaces(Place(gutterRunFaces),  RoadSurfaceMasks.GutterRun);
    }
}
