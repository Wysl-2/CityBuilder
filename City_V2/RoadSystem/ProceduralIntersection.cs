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

        FootpathModule.CreateFootpath(builder, transform, Model, Side.South);
        FootpathModule.CreateFootpath(builder, transform, Model, Side.East);
        FootpathModule.CreateFootpath(builder, transform, Model, Side.North);
        FootpathModule.CreateFootpath(builder, transform, Model, Side.West);

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

    public FootpathModel FootSouth;
    public FootpathModel FootEast;
    public FootpathModel FootNorth;
    public FootpathModel FootWest;


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

        // Initalize Corner Data Models
        CornerSW = InitCornerModel(CornerId.SW, oSW, config.corners.For(CornerId.SW, config.curb), S && W);
        CornerSE = InitCornerModel(CornerId.SE, oSE, config.corners.For(CornerId.SE, config.curb), S && E);
        CornerNE = InitCornerModel(CornerId.NE, oNE, config.corners.For(CornerId.NE, config.curb), N && E);
        CornerNW = InitCornerModel(CornerId.NW, oNW, config.corners.For(CornerId.NW, config.curb), N && W);

        // Initalize Footpath Data Models
        FootSouth = InitFootpathModel(Side.South);
        FootEast  = InitFootpathModel(Side.East);
        FootNorth = InitFootpathModel(Side.North);
        FootWest = InitFootpathModel(Side.West);
        WireUpFootpathAdjacency();


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
    
    private FootpathModel InitFootpathModel(Side side)
    {
        bool exists = !IsConnected(side);
        var geo = config.footpaths.For(side, in config.curb);

        Vector3 edgeOrigin, edgeRight, edgeInward;
        float edgeLength;
        CornerModel leftCorner, rightCorner;
        Side leftAdjSide, rightAdjSide;

        switch (side)
        {
            case Side.South:
                edgeOrigin = new Vector3(0f, 0f, 0f);
                edgeRight  = Vector3.right;
                edgeInward = Vector3.forward;
                edgeLength = Size.x;
                leftCorner  = CornerSW;
                rightCorner = CornerSE;
                leftAdjSide = Side.West;
                rightAdjSide= Side.East;
                break;

            case Side.East:
                edgeOrigin = new Vector3(Size.x, 0f, 0f);
                edgeRight  = Vector3.forward;
                edgeInward = Vector3.left;
                edgeLength = Size.y;
                leftCorner  = CornerSE;
                rightCorner = CornerNE;
                leftAdjSide = Side.South;
                rightAdjSide= Side.North;
                break;

            case Side.North:
                edgeOrigin = new Vector3(Size.x, 0f, Size.y);
                edgeRight  = Vector3.left;
                edgeInward = Vector3.back;
                edgeLength = Size.x;
                leftCorner  = CornerNE;
                rightCorner = CornerNW;
                leftAdjSide = Side.East;
                rightAdjSide= Side.West;
                break;

            default: // West
                edgeOrigin = new Vector3(0f, 0f, Size.y);
                edgeRight  = Vector3.back;
                edgeInward = Vector3.right;
                edgeLength = Size.y;
                leftCorner  = CornerNW;
                rightCorner = CornerSW;
                leftAdjSide = Side.North;
                rightAdjSide= Side.South;
                break;
        }

        var frame = new SiteFrame();

        return new FootpathModel(
            side: side,
            exists: exists,
            geometry: geo,
            edgeLength: edgeLength,
            edgeOrigin: edgeOrigin,
            edgeRight: edgeRight,
            edgeInward: edgeInward,
            frame: frame,
            leftCorner: in leftCorner,
            rightCorner: in rightCorner,
            leftAdjSide: leftAdjSide,
            rightAdjSide: rightAdjSide
        );
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

    private bool IsConnected(Side s) => s switch
    {
        Side.South => ConnectedSouth,
        Side.East => ConnectedEast,
        Side.North => ConnectedNorth,
        Side.West => ConnectedWest,
        _ => false
    };

    public FootpathModel GetFootpath(Side side) => side switch
    {
        Side.South => FootSouth,
        Side.East => FootEast,
        Side.North => FootNorth,
        Side.West => FootWest,
        _ => FootSouth
    };

    public CornerModel GetCorner(CornerId id) => id switch
    {
        CornerId.SW => CornerSW,
        CornerId.SE => CornerSE,
        CornerId.NE => CornerNE,
        CornerId.NW => CornerNW,
        _ => CornerSW
    };

    public (CornerModel left, CornerModel right) GetCornersForSide(Side side) => side switch
    {
        Side.South => (CornerSW, CornerSE),
        Side.East => (CornerSE, CornerNE),
        Side.North => (CornerNE, CornerNW),
        Side.West => (CornerNW, CornerSW),
        _ => (CornerSW, CornerSE)
    };
    
    private void WireUpFootpathAdjacency()
    {
        // convenience lookup
        bool Exists(Side s) => GetFootpath(s).exists;

        FootSouth = FootSouth.WithAdjacency(
            leftExists:  Exists(FootSouth.leftAdjSide),   // West
            rightExists: Exists(FootSouth.rightAdjSide)); // East

        FootEast = FootEast.WithAdjacency(
            leftExists:  Exists(FootEast.leftAdjSide),    // South
            rightExists: Exists(FootEast.rightAdjSide));  // North

        FootNorth = FootNorth.WithAdjacency(
            leftExists:  Exists(FootNorth.leftAdjSide),   // East
            rightExists: Exists(FootNorth.rightAdjSide)); // West

        FootWest = FootWest.WithAdjacency(
            leftExists:  Exists(FootWest.leftAdjSide),    // North
            rightExists: Exists(FootWest.rightAdjSide));  // South
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

public struct SiteFrame
{
  
}

// --- Footpath ---
public struct FootpathModel
{
    public readonly Side side;
    public readonly bool exists;
    public readonly FootpathGeometry geometry;

    // Edge info in intersection-local
    public readonly float edgeLength;   // Size.x or Size.y depending on side
    public readonly float edgeMid;      // edgeLength * 0.5f
    public readonly Vector3 edgeOrigin; // start of the edge in local
    public readonly Vector3 edgeRight;  // along-edge dir (unit)
    public readonly Vector3 edgeInward; // into the block (unit)
    public readonly SiteFrame frame;

    // Stitching helpers
    public readonly CornerModel leftCorner;
    public readonly CornerModel rightCorner;

    // NEW: adjacency
    public readonly Side leftAdjSide;
    public readonly Side rightAdjSide;
    public bool leftAdjExists;   // derived, set after all footpaths are built
    public bool rightAdjExists;  // derived, set after all footpaths are built

    public FootpathModel(
        Side side, bool exists, FootpathGeometry geometry,
        float edgeLength, Vector3 edgeOrigin, Vector3 edgeRight, Vector3 edgeInward,
        SiteFrame frame, in CornerModel leftCorner, in CornerModel rightCorner,
        Side leftAdjSide, Side rightAdjSide)
    {
        this.side = side; this.exists = exists; this.geometry = geometry;
        this.edgeLength = edgeLength;
        this.edgeMid = edgeLength * 0.5f;
        this.edgeOrigin = edgeOrigin;
        this.edgeRight = edgeRight;
        this.edgeInward = edgeInward;
        this.frame = frame;
        this.leftCorner = leftCorner;
        this.rightCorner = rightCorner;

        this.leftAdjSide = leftAdjSide;
        this.rightAdjSide = rightAdjSide;
        this.leftAdjExists = false;
        this.rightAdjExists = false;
    }

    public FootpathModel WithAdjacency(bool leftExists, bool rightExists)
    {
        var f = this; // copy
        f.leftAdjExists = leftExists;
        f.rightAdjExists = rightExists;
        return f;
    }
}
public static class FootpathModule
{
    public static void CreateFootpath(PBMeshBuilder builder, Transform transform, IntersectionModel model, Side side)
    {
        var fp = model.GetFootpath(side);
        if (!fp.exists) return;

        var geo  = fp.geometry;
        var curb = geo.curb;

        // ---- Compute extends from corners (your existing logic) ---------------
        static float ParamAlongEdge(in FootpathModel fpm, in Vector3 p)
        {
            float t = Vector3.Dot(p - fpm.edgeOrigin, fpm.edgeRight);
            return Mathf.Clamp(t, 0f, fpm.edgeLength);
        }

        float tL = fp.leftCorner.exists || fp.leftAdjExists ? ParamAlongEdge(fp, fp.leftCorner.apex) : 0f;
        float tR = fp.rightCorner.exists || fp.rightAdjExists ? ParamAlongEdge(fp, fp.rightCorner.apex) : fp.edgeLength;

        float mid         = fp.edgeMid;
        float leftExtend  = Mathf.Max(0f, mid - tL);
        float rightExtend = Mathf.Max(0f, tR  - mid);

        float xL = mid - leftExtend;
        float xR = mid + rightExtend;

        float z0 = 0f;                 // outer edge
        float z1 = geo.depth;          // inner edge toward roadway
        float y  = 0f;                 // build at y=0; world Y comes from final translation

        // ---- 0) Footpath slab (canonical; +Z points inward) -------------------
        var path = new Quad(new[]
        {
            new Vector3(xL, y, z0), // v0
            new Vector3(xR, y, z0), // v1
            new Vector3(xR, y, z1), // v2  <-- inner/top edge (edgeIndex = 2: v2->v3)
            new Vector3(xL, y, z1), // v3
        });

        // ---- 1) Curb skirt: extrude inner edge out (+Z) and down -------------
        // Correct Quad usage: edgeIndex in [0..3], outward dir (horizontal), outAmount, downAmount
        var curbSkirt = new Quad(vertices: path.ExtrudeEdgeOutDown(2, Vector3.forward, curb.skirtOut, curb.skirtDown));

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


        // ---- Collect and place ------------------------------------------------
        Vector3[][] sets = {
            path.Vertices,
            curbSkirt.Vertices,
            gutterApron,
            gutterSkirtToRoad
        };

        var (rotY, tx) = PlacementFor(side, model.Size);
        var rotated     = VertexOperations.RotateMany(sets, new Vector3(0f, rotY, 0f), Vector3.zero);
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
        Side.South => (   0f, new Vector3(0f,     0f, 0f)),
        Side.East  => ( -90f, new Vector3(size.x, 0f, 0f)),
        Side.North => (-180f, new Vector3(size.x, 0f, size.y)),
        _          => (-270f, new Vector3(0f,     0f, size.y)), // West
    };
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


