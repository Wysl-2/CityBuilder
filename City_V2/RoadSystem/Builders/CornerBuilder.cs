
using UnityEngine;

public static class CornerModule
{
    public static void CreateCorner(PBMeshBuilder builder, IntersectionModel model, CornerModel corner, CornerId cornerId, Transform transform)
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

    public static void CreateInwardCorner(PBMeshBuilder builder, IntersectionModel intersectionModel, CornerModel corner, CornerId cornerId, Transform transform)
    {

        bool swapXZ = cornerId == CornerId.NW || cornerId == CornerId.SE;
        float sx = swapXZ ? corner.geometry.zSize : corner.geometry.xSize;
        float sz = swapXZ ? corner.geometry.xSize : corner.geometry.zSize;

        float skirtOut = corner.geometry.curb.skirtOut;
        float skirtDown = corner.geometry.curb.skirtDown;
        float gutterDepth = corner.geometry.curb.gutterDepth;
        float gutterWidth = corner.geometry.curb.gutterWidth;
        float roadY = intersectionModel.RoadHeight;

        //Vector3 apexLocal = new Vector3(sx + skirtOut + gutterWidth, roadY, sz + skirtOut + gutterWidth);

        Vector3[][] BuildGeometry()
        {

            // Canonical local: +x/+z are inward -- invert vertex placement for NW and SE corners
            var qLocal = new Quad(vertices: new[]
            {
                new Vector3(0,0,0),
                new Vector3(sx,0,0),
                new Vector3(sx,0,sz),
                new Vector3(0,0,sz),
            });

            var skirtX = new Quad(vertices: qLocal.ExtrudeEdgeOutDown(1, Vector3.right, skirtOut, skirtDown));
            var skirtZ = new Quad(vertices: qLocal.ExtrudeEdgeOutDown(2, Vector3.forward, skirtOut, skirtDown));
            var triangleCap = new Vector3[] { qLocal.Vertices[2], skirtX.Vertices[2], skirtZ.Vertices[1] };

            var gutterApronX = ExtrusionUtil.ExtrudeEdgeOutDown(
                skirtX.Vertices[1], skirtX.Vertices[2], Vector3.right, 0, gutterDepth
            );
            var gutterApronZ = ExtrusionUtil.ExtrudeEdgeOutDown(
                skirtZ.Vertices[1], skirtZ.Vertices[2], Vector3.forward, 0, gutterDepth
            );
            var cornerQuadCap = new Vector3[] { triangleCap[2], triangleCap[1], gutterApronX[2], gutterApronZ[1] };

            var gutterSkirtX = ExtrusionUtil.ExtrudeEdgeOutToWorldY(
                gutterApronX[1], gutterApronX[2], Vector3.right, gutterWidth, roadY
            );

            var gutterSkirtZ = ExtrusionUtil.ExtrudeEdgeOutToWorldY(
                gutterApronZ[1], gutterApronZ[2], Vector3.forward, gutterWidth, roadY
            );

            var gutterSkirtCap = new Vector3[] { gutterSkirtX[3], gutterSkirtX[2], gutterSkirtZ[1], gutterSkirtZ[0], };

            Vector3 apexLocal = new Vector3(
                qLocal.Vertices[2].x + skirtOut + gutterWidth, roadY, qLocal.Vertices[2].z + skirtOut + gutterWidth
            );
            var roadTriangleCap = new Vector3[] { gutterSkirtCap[2], gutterSkirtCap[1], apexLocal };


            return new Vector3[][] { qLocal.Vertices, skirtX.Vertices, skirtZ.Vertices, triangleCap, gutterApronX, gutterApronZ, cornerQuadCap, gutterSkirtX, gutterSkirtZ, gutterSkirtCap, roadTriangleCap };
        }
        var cornerGeometry = BuildGeometry();

        Vector3[][] ApplyRotationAndTranslationFor(CornerId id, Vector3[][] geo)
        {
            Vector3 rot, tx;
            var size = intersectionModel.Size;

            switch (id)
            {
                case CornerId.SW:
                    rot = new Vector3(0, 0, 0);
                    tx = new Vector3(0, 0, 0);
                    break;
                case CornerId.SE:
                    rot = new Vector3(0, -90, 0);
                    tx = new Vector3(size.x, 0, 0);
                    break;
                case CornerId.NE:
                    rot = new Vector3(0, -180, 0);
                    tx = new Vector3(size.x, 0, size.y);
                    break;
                case CornerId.NW:
                    rot = new Vector3(0, -270, 0);
                    tx = new Vector3(0, 0, size.y);
                    break;
                default:
                    throw new ArgumentException("Invalid cornerId.", nameof(id));
            }

            var rotated = VertexOperations.RotateMany(geo, rot, Vector3.zero);
            return VertexOperations.TranslateMany(rotated, tx);
        }

        var finalCornerGeometry = ApplyRotationAndTranslationFor(cornerId, cornerGeometry);

        finalCornerGeometry = VertexOperations.TranslateMany(finalCornerGeometry, transform.position);

        builder.AddQuadFace(finalCornerGeometry[0]);
        builder.AddQuadFace(finalCornerGeometry[1]);
        builder.AddQuadFace(finalCornerGeometry[2]);
        builder.AddTriangleFace(finalCornerGeometry[3]);

        builder.AddQuadFace(finalCornerGeometry[4]); // Gutter Apron X
        builder.AddQuadFace(finalCornerGeometry[5]); // Gutter Apron Z
        builder.AddQuadFace(finalCornerGeometry[6]); // Corner Quad Cap
        builder.AddQuadFace(finalCornerGeometry[7]); // Gutter Skirt X
        builder.AddQuadFace(finalCornerGeometry[8]); // Gutter Skirt Z
        builder.AddQuadFace(finalCornerGeometry[9]); // Gutter Skirt Cap
        builder.AddTriangleFace(finalCornerGeometry[10]); // Road Triangle Cap
    }

    public static void CreateOutwardCorner(PBMeshBuilder builder, IntersectionModel model, CornerModel corner, CornerId cornerId, Transform transform)
    {
        // Use the per-corner footpath sizes you already store in CornerGeometry.
        // Build in canonical local where +X and +Z go "inward" from the corner origin.
        bool swapXZ = cornerId == CornerId.NW || cornerId == CornerId.SE;
        float sx = swapXZ ? corner.geometry.zSize : corner.geometry.xSize;
        float sz = swapXZ ? corner.geometry.xSize : corner.geometry.zSize;

        var curb = model.config.curb;

        float y = 0f;

        // A simple footpath corner pad filling the empty L-wedge:
        // Here we start with a rectangle [0..sx] x [0..sz] at y=0.
        // (We’ll trim/shape and add curb/gutter in the next step.)
        var pad = new Vector3[]
        {
            new Vector3(0,  y, 0),
            new Vector3(sx, y, 0),
            new Vector3(sx, y, sz),
            new Vector3(0,  y, sz)
        };

        float offset = model.config.curb.skirtOut + model.config.curb.gutterWidth;

        var footpathCap = new Vector3[]
        {
            pad[2],
            new Vector3(pad[2].x + offset, pad[2].y, pad[2].z),
            new Vector3(pad[2].x, pad[2].y, pad[2].z + offset),
        };

        Vector3[] curbSkirt = new Vector3[]
        {
            new Vector3(footpathCap[1].x, footpathCap[1].y - curb.skirtDown, footpathCap[1].z + curb.skirtOut),
            new Vector3(footpathCap[2].x + curb.skirtOut,  footpathCap[2].y - curb.skirtDown, footpathCap[2].z),
            footpathCap[2],
            footpathCap[1],
        };

        Vector3[] gutterApron = new Vector3[]
        {
            curbSkirt[0],
            new Vector3(curbSkirt[0].x,  curbSkirt[0].y - curb.gutterDepth, curbSkirt[0].z),
            new Vector3(curbSkirt[1].x,  curbSkirt[1].y - curb.gutterDepth, curbSkirt[1].z),
            curbSkirt[1]
        };


        Vector3 apexLocal = new Vector3(
                pad[2].x + curb.skirtOut + curb.gutterWidth, model.RoadHeight, pad[2].z + curb.skirtOut + curb.gutterWidth
            );
        Vector3[] gutterCap = new Vector3[]
        {

            gutterApron[2],
            gutterApron[1],
            apexLocal,


        };

        Vector3[][] sets = { pad, footpathCap, curbSkirt, gutterApron, gutterCap };

        // Rotate/translate the same way you do in the inward corner:
        Vector3 rot, tx;
        var size = model.Size;
        switch (cornerId)
        {
            case CornerId.SW: rot = new Vector3(0,   0, 0); tx = new Vector3(0,     0, 0);       break;
            case CornerId.SE: rot = new Vector3(0, -90, 0); tx = new Vector3(size.x, 0, 0);       break;
            case CornerId.NE: rot = new Vector3(0,-180, 0); tx = new Vector3(size.x, 0, size.y);  break;
            default:          rot = new Vector3(0,-270, 0); tx = new Vector3(0,     0, size.y);  break; // NW
        }

        var rotated = VertexOperations.RotateMany(sets, rot, Vector3.zero);
        var placedLocal = VertexOperations.TranslateMany(rotated, tx);
        var placedWorld = VertexOperations.TranslateMany(placedLocal, transform.position);

        builder.AddQuadFace(placedWorld[0]);
        builder.AddTriangleFace(placedWorld[1]);
        builder.AddQuadFace(placedWorld[2]);
        builder.AddQuadFace(placedWorld[3]);
        builder.AddTriangleFace(placedWorld[4]);
    }
}