using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using MeshBuilder.Primitives;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.ProBuilder.MeshOperations;

public enum Side { South, East, North, West }
public enum CornerId { SW, SE, NE, NW }

public class ProceduralIntersection : MonoBehaviour
{
    public Material material;
    [Header("Dimensions")]
    public Vector2 Size;
    public float RoadHeight;
    [Header("Connections")]
    public bool ConnectedNorth;
    public bool ConnectedSouth;
    public bool ConnectedEast;
    public bool ConnectedWest;

    [Header("Intersection Profile Settings")]
    public IntersectionGeometryConfig intersectionConfig = IntersectionGeometryConfig.Default();

    // [Header("Corner Geometry (Inspector)")]
    // public CornerGeometryConfig cornerGeometry = CornerGeometryConfig.Default();


    [SerializeField] public IntersectionModel Model;

    void OnValidate()
    {

    }


    void Start()
    {

        Model = new IntersectionModel(Size, RoadHeight,
            ConnectedNorth, ConnectedEast, ConnectedSouth, ConnectedWest,
            intersectionConfig);

        if (material)
            GetComponent<MeshRenderer>().sharedMaterial = material;

        PBMeshBuilder builder = new PBMeshBuilder();
        var sinkTag = gameObject.AddComponent<FaceSinkTag>();
        builder.Sink = sinkTag;

        if (Model.CornerSW.exists) CornerModule.CreateCorner(builder, Model, Model.CornerSW, CornerId.SW, transform);
        if (Model.CornerSE.exists) CornerModule.CreateCorner(builder, Model, Model.CornerSE, CornerId.SE, transform);
        if (Model.CornerNE.exists) CornerModule.CreateCorner(builder, Model, Model.CornerNE, CornerId.NE, transform);
        if (Model.CornerNW.exists) CornerModule.CreateCorner(builder, Model, Model.CornerNW, CornerId.NW, transform);

        FootpathModule.CreateFootpath(builder, transform, Model, Model.config.footpaths.For(Side.South, Model.config.curb));

        Material[] materials = new Material[] { material };
        builder.Build(materials, this.transform);

        Debug.Log(sinkTag.faces.Count);

    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);

        // Draw outline
        Gizmos.DrawLine(new Vector3(0, 0, 0), new Vector3(Size.x, 0, 0));
        Gizmos.DrawLine(new Vector3(Size.x, 0, 0), new Vector3(Size.x, 0, Size.y));
        Gizmos.DrawLine(new Vector3(Size.x, 0, Size.y), new Vector3(0, 0, Size.y));
        Gizmos.DrawLine(new Vector3(0, 0, Size.y), new Vector3(0, 0, 0));

        //Draw Apex positions if mesh is built
        if (Model != null)
        {
            Gizmos.DrawWireSphere(Model.CornerSW.apex, 0.1f);
            Gizmos.DrawWireSphere(Model.CornerSE.apex, 0.1f);
            Gizmos.DrawWireSphere(Model.CornerNW.apex, 0.1f);
            Gizmos.DrawWireSphere(Model.CornerNE.apex, 0.1f);
        }
    }
}



public enum RoadTopology { Plaza, DeadEnd, I, L, T, X }

[System.Serializable]
public sealed class IntersectionModel
{
    // -- Config --
    public IntersectionGeometryConfig config;
    // -- Inputs --
    public readonly Vector2 Size;
    public readonly bool ConnectedNorth, ConnectedEast, ConnectedSouth, ConnectedWest;

    // -- Data --
    public readonly RoadTopology RoadTopology;
    public CornerModel CornerSE;
    public CornerModel CornerSW;
    public CornerModel CornerNE;
    public CornerModel CornerNW;



    public float RoadHeight;

    public IntersectionModel(
        Vector2 size, float roadHeight,
         bool N, bool E, bool S, bool W,
        IntersectionGeometryConfig config)
    {
        this.config = config;
        ConnectedNorth = N; ConnectedEast = E; ConnectedSouth = S; ConnectedWest = W;
        RoadTopology = Classify(ConnectedNorth, ConnectedEast, ConnectedSouth, ConnectedWest);
        Size = size;
        RoadHeight = roadHeight;

        // local intersection corner origins/positions on XZ (y = 0)
        Vector3 oSW = new Vector3(0f, 0f, 0f);
        Vector3 oSE = new Vector3(Size.x, 0f, 0f);
        Vector3 oNE = new Vector3(Size.x, 0f, Size.y);
        Vector3 oNW = new Vector3(0f, 0f, Size.y);

        CornerSW = InitCornerModel(CornerId.SW, oSW, config.corners.For(CornerId.SW, config.curb), S && W);
        CornerSE = InitCornerModel(CornerId.SE, oSE, config.corners.For(CornerId.SE, config.curb), S && E);
        CornerNE = InitCornerModel(CornerId.NE, oNE, config.corners.For(CornerId.NE, config.curb), N && E);
        CornerNW = InitCornerModel(CornerId.NW, oNW, config.corners.For(CornerId.NW, config.curb), N && W);

    }
    
    private CornerModel InitCornerModel(CornerId id, Vector3 origin, CornerGeometry geo, bool exists)
    {
        var (sx, sz) = CornerMath.InwardSigns(id);
        var (ax, az) = geo.ApexOffsets();
        var apex = CornerMath.ComputeApexFromOrigin(origin, id, geo, RoadHeight);
        // var apex = origin + new Vector3(sx * ax, RoadHeight, sz * az);

        var (a, b) = Topology.AdjacentOf(id); // clockwise adjacent sides
        return new CornerModel(id, exists, origin, apex, a, b, geo);
    }

    private static RoadTopology Classify(bool N, bool E, bool S, bool W)
    {
        int cnt = (N ? 1 : 0) + (E ? 1 : 0) + (S ? 1 : 0) + (W ? 1 : 0);
        if (cnt == 4) return RoadTopology.X;
        if (cnt == 0) return RoadTopology.Plaza;
        if (cnt == 1) return RoadTopology.DeadEnd;
        if (cnt == 2) return ((N && S) || (E && W)) ? RoadTopology.I : RoadTopology.L;
        return RoadTopology.T; // cnt == 3
    }
}

// -------- Data Containers -------------------------------------------

// ---- Intersection Global Values ----
[System.Serializable]
public struct IntersectionGeometryConfig
{
    [Header("Shared curb/gutter (applies to corners + footpaths)")]
    public CurbGutter curb;

    [Header("Corner settings")]
    public CornerGeometryConfig corners;

    [Header("Footpath settings")]
    public FootpathGeometryConfig footpaths;

    public static IntersectionGeometryConfig Default() => new IntersectionGeometryConfig
    {
        curb = CurbGutter.Default(),
        corners = CornerGeometryConfig.Default(),
        footpaths = FootpathGeometryConfig.Default()
    };
}

[System.Serializable]
public struct CurbGutter
{
    public float skirtOut;
    public float skirtDown;
    public float gutterDepth;
    public float gutterWidth;

    public static CurbGutter Default() => new CurbGutter
    {
        skirtOut = 0.35f,
        skirtDown = 0.05f,
        gutterDepth = 0.5f,
        gutterWidth = 0.5f
    };
}




// --- Corner Geometry Properties ---
[System.Serializable]
public struct CornerSize
{
    public float xSize;
    public float zSize;
}

[System.Serializable]
public struct CornerSizeSet
{
    public CornerSize SW, SE, NE, NW;
    public CornerSize Get(CornerId id) => id switch {
        CornerId.SW => SW, CornerId.SE => SE, CornerId.NE => NE, CornerId.NW => NW, _ => SW
    };
    public static CornerSizeSet FromSingle(CornerSize s) => new CornerSizeSet { SW=s, SE=s, NE=s, NW=s };
}

[System.Serializable]
public struct CornerGeometryConfig
{
    [Header("Footpath Sizes (per corner)")]
    public CornerSizeSet sizes;

    public static CornerGeometryConfig Default()
    {
        return new CornerGeometryConfig
        {
            sizes = CornerSizeSet.FromSingle(new CornerSize { xSize = 3f, zSize = 3f }),
        };
    }

    public CornerGeometry For(CornerId id, in CurbGutter curb) =>
        new CornerGeometry(sizes.Get(id), curb);

}

public readonly struct CornerGeometry
{
    // Footpath
    public readonly float xSize;
    public readonly float zSize;
    // Curb / Gutter (shared values copied in)
    public readonly CurbGutter curb;

    public CornerGeometry(CornerSize sz, CurbGutter curb)
    {
        xSize = sz.xSize; zSize = sz.zSize;
        this.curb = curb;
    }

    // Handy derived values used in multiple places
    public (float ax, float az) ApexOffsets() =>
        (xSize + curb.skirtOut + curb.gutterWidth, zSize + curb.skirtOut + curb.gutterWidth);
}

// ---- Footpath Values ----
// Runtime view used by builders (immutable w.r.t. curb)
[Serializable]
public struct FootpathGeometryConfig
{
    [Header("Footpath depth (per side)")]
    public FootpathDepthSet depths;

    [Header("Footpath extension (per side)")]
    public FootpathExtendLengthsSet extends;

    public static FootpathGeometryConfig Default() =>
        new FootpathGeometryConfig
        {
            depths = FootpathDepthSet.FromSingle(3f),
            extends = FootpathExtendLengthsSet.FromSingle(new FootpathExtendLengths
            {
                LeftExtend = 1.5f,
                RightExtend = 1.5f
            })
        };

    public FootpathGeometry For(Side side, in CurbGutter curb)
    {
        return new FootpathGeometry(
            depths.Get(side),
            curb,
            extends.Get(side)
        );
    }
}

[Serializable]
public struct FootpathGeometry
{
    public float depth;
    public readonly CurbGutter curb;
    public FootpathExtendLengths extend; // new: holds left/right extend

    public FootpathGeometry(float depth, in CurbGutter curb, FootpathExtendLengths extend)
    {
        this.depth = depth;
        this.curb = curb;
        this.extend = extend;
    }
}

[Serializable]
public struct FootpathDepthSet
{
    public float South, East, North, West;

    public float Get(Side side) => side switch
    {
        Side.South => South,
        Side.East => East,
        Side.North => North,
        Side.West => West,
        _ => South
    };

    public static FootpathDepthSet FromSingle(float d) =>
        new FootpathDepthSet { South = d, East = d, North = d, West = d };
}

// ---- Footpath Extension Values ----
[System.Serializable]
public struct FootpathExtendLengths
{
    public float LeftExtend;
    public float RightExtend;
}

[System.Serializable]
public struct FootpathExtendLengthsSet
{
    public FootpathExtendLengths South;
    public FootpathExtendLengths East;
    public FootpathExtendLengths North;
    public FootpathExtendLengths West;

    public FootpathExtendLengths Get(Side side) => side switch
    {
        Side.South => South,
        Side.East  => East,
        Side.North => North,
        Side.West  => West,
        _          => South
    };

    public static FootpathExtendLengthsSet FromSingle(FootpathExtendLengths f) =>
        new FootpathExtendLengthsSet { South = f, East = f, North = f, West = f };
}


// -- Geometry Modules --
// ---- Corner Model ----
[System.Serializable]
public struct CornerModel
{
    public CornerGeometry geometry;

    public readonly CornerId id;
    public readonly bool exists;
    public readonly Vector3 origin;
    public readonly Vector3 apex;
    public readonly Side adjA;
    public readonly Side adjB;

    public CornerModel(CornerId id, bool exists, Vector3 origin, Vector3 apex, Side a, Side b, CornerGeometry geometry)
    {
        this.id = id; this.exists = exists; this.origin = origin; this.apex = apex; adjA = a; adjB = b; this.geometry = geometry;
    }

    public bool IsAdjacentTo(Side s) => s == adjA || s == adjB;
    public Side OppA => Topology.Opposite(adjA);
    public Side OppB => Topology.Opposite(adjB);
    public bool CanMeetFootpathOn(Side s) => s == OppA || s == OppB;
}
public static class CornerModule
{
    // ---------------------- Public entrypoint ----------------------

    public static void CreateCorner(PBMeshBuilder builder, IntersectionModel intersectionModel, CornerModel corner, CornerId cornerId, Transform transform)
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
                gutterApronX[1], gutterApronX[2], Vector3.right, gutterWidth, intersectionModel.RoadHeight
            );

            var gutterSkirtZ = ExtrusionUtil.ExtrudeEdgeOutToWorldY(
                gutterApronZ[1], gutterApronZ[2], Vector3.forward, gutterWidth, intersectionModel.RoadHeight
            );

            var gutterSkirtCap = new Vector3[] { gutterSkirtX[3], gutterSkirtX[2], gutterSkirtZ[1], gutterSkirtZ[0], };

            Vector3 apexLocal = new Vector3(
                qLocal.Vertices[2].x + skirtOut + gutterWidth, intersectionModel.RoadHeight, qLocal.Vertices[2].z + skirtOut + gutterWidth
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
}

// --- Footpath ---
public struct FootpathModel
{
    public FootpathGeometry geometry;
    public bool Exists;
}
public static class FootpathModule
{
    public static void CreateFootpath(PBMeshBuilder builder, Transform transform, IntersectionModel intersectionModel, FootpathGeometry geo)
    {
        float xMid = intersectionModel.Size.x * 0.5f;
        float xL   = xMid - geo.extend.LeftExtend;
        float xR = xMid + geo.extend.RightExtend;
        
        float z0 = 0f;
        float z1 = geo.depth;
        float y  = transform.position.y;

        Vector3[][] BuildGeometry()
        {
            Vector3[] path = new Vector3[]
            {
                new Vector3(xL, y, z0),
                new Vector3(xR, y, z0),
                new Vector3(xR, y, z1),

                new Vector3(xL, y, z1),
            };

            return new Vector3[][] { path };
        }

        var geometry = BuildGeometry();

        var pathWorld = VertexOperations.TranslateMany(geometry, transform.position);

        builder.AddQuadFace(pathWorld[0]);

    }
}


// --- Topology Util ---
public static class Topology
{
    public static Side Opposite(Side s) => s switch
    {
        Side.North => Side.South,
        Side.South => Side.North,
        Side.East => Side.West,
        _ => Side.East, // West
    };

    public static bool AreAdjacent(Side a, Side b) =>
        a != b && a != Opposite(b);

    public static bool IsVertical(Side s) => s == Side.West || s == Side.East;
    public static bool IsHorizontal(Side s) => s == Side.North || s == Side.South;

    public static CornerId CornerOf(Side a, Side b)
    {
        // Normalize ordering so mapping is easy
        var s1 = a; var s2 = b;
        if (IsVertical(s1) && IsHorizontal(s2)) { /* ok */ }
        else if (IsHorizontal(s1) && IsVertical(s2)) { var tmp = s1; s1 = s2; s2 = tmp; }
        else throw new System.ArgumentException("Sides must be perpendicular.");

        return (s1, s2) switch
        {
            (Side.West, Side.South) => CornerId.SW,
            (Side.West, Side.North) => CornerId.NW,
            (Side.East, Side.South) => CornerId.SE,
            (Side.East, Side.North) => CornerId.NE,
            _ => throw new System.ArgumentException("Unexpected sides.")
        };
    }

    public static (Side a, Side b) AdjacentOf(CornerId id)
    {
        // Return the two rectangle edges that meet at this corner, in clockwise order.
        return id switch
        {
            CornerId.SW => (Side.South, Side.West),
            CornerId.SE => (Side.South, Side.East),
            CornerId.NE => (Side.North, Side.East),
            CornerId.NW => (Side.North, Side.West),
            _ => throw new System.ArgumentOutOfRangeException(nameof(id), id, null)
        };
    }
}

static class CornerMath
{
    public static (int sx, int sz) InwardSigns(CornerId id) => id switch
    {
        CornerId.SW => (+1, +1),
        CornerId.SE => (-1, +1),
        CornerId.NE => (-1, -1),
        CornerId.NW => (+1, -1),
        _ => (+1, +1)
    };

    public static Vector3 ComputeApexFromOrigin(Vector3 origin, CornerId id, CornerGeometry gp, float roadHeight)
    {
        float ax = gp.xSize + gp.curb.skirtOut + gp.curb.gutterWidth;
        float az = gp.zSize + gp.curb.skirtOut + gp.curb.gutterWidth;
        var (sx, sz) = InwardSigns(id);
        return origin + new Vector3(sx * ax, roadHeight, sz * az);
    }
}


