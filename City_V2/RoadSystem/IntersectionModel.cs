using UnityEngine;

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
        CornerSW = InitCornerModel(CornerId.SW, oSW, config.corners.For(CornerId.SW, config.curb));
        CornerSE = InitCornerModel(CornerId.SE, oSE, config.corners.For(CornerId.SE, config.curb));
        CornerNE = InitCornerModel(CornerId.NE, oNE, config.corners.For(CornerId.NE, config.curb));
        CornerNW = InitCornerModel(CornerId.NW, oNW, config.corners.For(CornerId.NW, config.curb));

        // Initalize Footpath Data Models
        FootSouth = InitFootpathModel(Side.South);
        FootEast  = InitFootpathModel(Side.East);
        FootNorth = InitFootpathModel(Side.North);
        FootWest = InitFootpathModel(Side.West);
        WireUpFootpathAdjacency();
    }

    private CornerModel InitCornerModel(CornerId id, Vector3 origin, CornerGeometry geo)
    {
        var apex = CornerMath.ComputeApexFromOrigin(origin, id, geo, RoadHeight);
        var (a, b) = Topology.AdjacentOf(id);

        bool roadA = IsConnected(a);
        bool roadB = IsConnected(b);

        CornerType type;
        bool exists;

        if (roadA && roadB) { type = CornerType.InwardFacing; exists = true; }
        else if (!roadA && !roadB) { type = CornerType.OutwardFacing; exists = true; }
        else { type = CornerType.InwardFacing; exists = false; } // mixed: no special corner geometry

        return new CornerModel(id, exists, type, origin, apex, a, b, geo);
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

        // var frame = new SiteFrame();

        return new FootpathModel(
            side: side,
            exists: exists,
            geometry: geo,
            edgeLength: edgeLength,
            edgeOrigin: edgeOrigin,
            edgeRight: edgeRight,
            edgeInward: edgeInward,
            // frame: frame,
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