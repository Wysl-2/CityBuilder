
using System;
using System.Collections.Generic;
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

    // CCW quad on XZ at y: v0..v3 = (x0,z0)->(x1,z0)->(x1,z1)->(x0,z1)
    static Vector3[] MakeQuadXZ(float x0, float x1, float z0, float z1, float y) => new[]
    {
        new Vector3(x0,y,z0), new Vector3(x1,y,z0), new Vector3(x1,y,z1), new Vector3(x0,y,z1)
    };

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

        List<Vector3[]> BuildGeometry()
        {

            var faces = new List<Vector3[]>(capacity: 11);

            // Canonical local: +x/+z are inward -- invert vertex placement for NW and SE corners
            var qLocal = MakeQuadXZ(0f, sx, 0f, sz, 0f);

            // Skirts from inner edges:
            var e1a = qLocal[1];  // start of edge 1 (v1)
            var e1b = qLocal[2];  // end of edge 1 (v2)
            var e2a = qLocal[2];  // start of edge 2 (v2)
            var e2b = qLocal[3];  // end of edge 2 (v3)
            var skirtX = ExtrusionUtil.ExtrudeEdgeOutDown(e1a, e1b, Vector3.right,   skirtOut, skirtDown);
            var skirtZ = ExtrusionUtil.ExtrudeEdgeOutDown(e2a, e2b, Vector3.forward, skirtOut, skirtDown);

            var triangleCap = new Vector3[] { qLocal[2], skirtX[2], skirtZ[1] };

            var gutterApronX = ExtrusionUtil.ExtrudeEdgeOutDown(
                skirtX[1], skirtX[2], Vector3.right, 0, gutterDepth
            );
            var gutterApronZ = ExtrusionUtil.ExtrudeEdgeOutDown(
                skirtZ[1], skirtZ[2], Vector3.forward, 0, gutterDepth
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
                qLocal[2].x + skirtOut + gutterWidth, roadY, qLocal[2].z + skirtOut + gutterWidth
            );
            var roadTriangleCap = new Vector3[] { gutterSkirtCap[2], gutterSkirtCap[1], apexLocal };

            faces.Add(qLocal);
            faces.Add(skirtX);
            faces.Add(skirtZ);
            faces.Add(triangleCap);
            faces.Add(gutterApronX);
            faces.Add(gutterApronZ);
            faces.Add(cornerQuadCap);
            faces.Add(gutterSkirtX);
            faces.Add(gutterSkirtZ);
            faces.Add(gutterSkirtCap);
            faces.Add(roadTriangleCap);

            return faces;

        }
        var cornerGeometry = BuildGeometry();

        var size = intersectionModel.Size;
        List<Vector3[]> ApplyRotationAndTranslationFor(CornerId id, List<Vector3[]> geo)
        {
            Vector3 euler, tx;

            switch (id)
            {
                case CornerId.SW: euler = new Vector3(0,   0, 0); tx = new Vector3(0,      0, 0);       break;
                case CornerId.SE: euler = new Vector3(0, -90, 0); tx = new Vector3(size.x, 0, 0);       break;
                case CornerId.NE: euler = new Vector3(0,-180, 0); tx = new Vector3(size.x, 0, size.y);  break;
                case CornerId.NW: euler = new Vector3(0,-270, 0); tx = new Vector3(0,      0, size.y);  break;
                default: throw new ArgumentException("Invalid cornerId.", nameof(id));
            }

            var q        = Quaternion.Euler(euler);                // Quaternion, not Euler vector
            var rotated  = VertexOperations.RotateMany(geo, q, Vector3.zero);
            var shifted  = VertexOperations.TranslateMany(rotated, tx);
            return shifted;
        }


        // var placedLocal  = ApplyRotationAndTranslationFor(cornerId, cornerGeometry);

        // // NEW: recenter from SW-origin (0..Size) to pivot-at-center
        // var centerOffset = new Vector3(-size.x * 0.5f, 0f, -size.y * 0.5f);
        // var centeredLocal = VertexOperations.TranslateMany(placedLocal, centerOffset);

        // // --- worldRotation: align the entire corner to the GameObject’s rotation
        // var worldRotation  = transform.rotation;
        // var rotatedWorld  = VertexOperations.RotateMany(centeredLocal, worldRotation , Vector3.zero);

        // var placedWorld  = VertexOperations.TranslateMany(rotatedWorld, transform.position);

        var placedLocal  = ApplyRotationAndTranslationFor(cornerId, cornerGeometry);

        // Recenter from SW-origin (0..Size) to pivot-at-center; stay in local space
        var centerOffset   = new Vector3(-size.x * 0.5f, 0f, -size.y * 0.5f);
        var centeredLocal  = VertexOperations.TranslateMany(placedLocal, centerOffset);

        builder.AddFaces(centeredLocal);

    }

    public static void CreateOutwardCorner(PBMeshBuilder builder, IntersectionModel model, CornerModel corner, CornerId cornerId, Transform transform)
    {
        // Use the per-corner footpath sizes you already store in CornerGeometry.
        // Build in canonical local where +X and +Z go "inward" from the corner origin.
        bool swapXZ = cornerId == CornerId.NW || cornerId == CornerId.SE;
        float sx = swapXZ ? corner.geometry.zSize : corner.geometry.xSize;
        float sz = swapXZ ? corner.geometry.xSize : corner.geometry.zSize;

        var curb = model.config.curb;
        float offset = model.config.curb.skirtOut + model.config.curb.gutterWidth;
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

        var faces = new List<Vector3[]>(capacity: 5)
        {
            pad, footpathCap, curbSkirt, gutterApron, gutterCap
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

        // // 1) rotate/translate into its intersection slot (localRotation)
        // var localRotation = Quaternion.Euler(euler);
        // var rotated       = VertexOperations.RotateMany(faces, localRotation, Vector3.zero);
        // var placedLocal   = VertexOperations.TranslateMany(rotated, tx);

        // // NEW: recenter from SW-origin (0..Size) to pivot-at-center
        // var centerOffset = new Vector3(-size.x * 0.5f, 0f, -size.y * 0.5f);
        // var centeredLocal = VertexOperations.TranslateMany(placedLocal, centerOffset);

        // // worldRotation: align to GameObject
        // var worldRotation = transform.rotation;
        // var rotatedWorld = VertexOperations.RotateMany(centeredLocal, worldRotation, Vector3.zero);

        // // translate to world
        // var placedWorld = VertexOperations.TranslateMany(rotatedWorld, transform.position);

        var localRotation = Quaternion.Euler(euler);
        var rotated       = VertexOperations.RotateMany(faces, localRotation, Vector3.zero);
        var placedLocal   = VertexOperations.TranslateMany(rotated, tx);

        // Recenter to pivot-at-center, remain in local space
        var centerOffset   = new Vector3(-size.x * 0.5f, 0f, -size.y * 0.5f);
        var centeredLocal  = VertexOperations.TranslateMany(placedLocal, centerOffset);

        builder.AddFaces(centeredLocal);
    }
}