using System.Collections;
using System.Collections.Generic;
using ProcGen;
using UnityEngine;

public enum Side { South, East, North, West }
public enum CornerId { SW, SE, NE, NW }
public class Example_Intersection_2 : MonoBehaviour
{
    public Material material;

    [Header("Intersection Size")]
    public float SizeX;
    public float SizeY;

    [Header("Connections")]
    public bool ConnectedNorth;
    public bool ConnectedSouth;
    public bool ConnectedEast;
    public bool ConnectedWest;

    [Header("Corner / Apron Params")]
    public float floorWidth   = 2f;
    public float floorDepth   = 2f;
    public float extend       = 0.1f;
    public float apronExtend  = 0.2f;
    public float apronMeetY   = -0.05f;
    public float drop = 0.1f;
    public float gutterDepth = 0.2f;

    [Header("UVs")]
    public float metersPerTile = 1f;

    // Hold the model so other methods (mesh build, gizmos) can query it
    private IntersectionModel _model;

   void Start()
{
    // --- Build model (unchanged) ---
    float widthReach = Mathf.Abs(floorWidth + extend + apronExtend);
    float depthReach = Mathf.Abs(floorDepth + extend + apronExtend);

    _model = new IntersectionModel(
        x: SizeX,
        z: SizeY,
        ConnectedNorth: ConnectedNorth,
        ConnectedEast:  ConnectedEast,
        ConnectedSouth: ConnectedSouth,
        ConnectedWest:  ConnectedWest,
        widthReach: widthReach,
        depthReach: depthReach,
        apronY: apronMeetY
    );

    var b = new MeshBuilder();

    var P = new CornerParams {
        floorWidth   = floorWidth,
        floorDepth   = floorDepth,
        extend       = extend,
        drop         = drop,
        gutterDepth  = gutterDepth,
        apronExtend  = apronExtend,
        apronMeetY   = apronMeetY
    };

    // --- FOOTPATHS (now capped by FootpathCorner apexes) ---
    var cornersRect = new CornerPositions {
        SW = new Vector3(0f,       0f,        0f),
        SE = new Vector3(_model.X, 0f,        0f),
        NE = new Vector3(_model.X, 0f,        _model.Z),
        NW = new Vector3(0f,       0f,        _model.Z)
    };

    Facing Face(Side s) => s switch {
        Side.North => Facing.North,
        Side.East  => Facing.East,
        Side.South => Facing.South,
        _          => Facing.West
    };

    // Which sides get a footpath?
    bool fpN = !_model.ConnNorth;
    bool fpE = !_model.ConnEast;
    bool fpS = !_model.ConnSouth;
    bool fpW = !_model.ConnWest;

    // Footpath plane & inset used by the module
    float footpathY     = 0f;            // footpath floor plane
    float footpathDepth = P.floorDepth;  // depth passed to AddFootpath

    // NORTH (NW->NE): cap ends ONLY if the corresponding footpath corner exists
    if (fpN)
    {
        Vector3? capStart = _model.FP_NW.Exists ? _model.FP_NW.Apex : (Vector3?)null;
        Vector3? capEnd   = _model.FP_NE.Exists ? _model.FP_NE.Apex : (Vector3?)null;

        FootpathModule.AddFootpath(
            b, cornersRect, P, Facing.North,
            footpathDepth, footpathY,
            capStart, capEnd,
            capY: _model.ApronY
        );
    }

    // EAST (NE->SE)
    if (fpE)
    {
        Vector3? capStart = _model.FP_NE.Exists ? _model.FP_NE.Apex : (Vector3?)null;
        Vector3? capEnd   = _model.FP_SE.Exists ? _model.FP_SE.Apex : (Vector3?)null;

        FootpathModule.AddFootpath(
            b, cornersRect, P, Facing.East,
            footpathDepth, footpathY,
            capStart, capEnd,
            capY: _model.ApronY
        );
    }

    // SOUTH (SE->SW)
    if (fpS)
    {
        Vector3? capStart = _model.FP_SE.Exists ? _model.FP_SE.Apex : (Vector3?)null;
        Vector3? capEnd   = _model.FP_SW.Exists ? _model.FP_SW.Apex : (Vector3?)null;

        FootpathModule.AddFootpath(
            b, cornersRect, P, Facing.South,
            footpathDepth, footpathY,
            capStart, capEnd,
            capY: _model.ApronY
        );
    }

    // WEST (SW->NW)
    if (fpW)
    {
        Vector3? capStart = _model.FP_SW.Exists ? _model.FP_SW.Apex : (Vector3?)null;
        Vector3? capEnd   = _model.FP_NW.Exists ? _model.FP_NW.Apex : (Vector3?)null;

        FootpathModule.AddFootpath(
            b, cornersRect, P, Facing.West,
            footpathDepth, footpathY,
            capStart, capEnd,
            capY: _model.ApronY
        );
    }

    // --- CORNERS (unchanged: only when both adjacent sides are connected) ---
    Vector3 pSW = new Vector3(0f,       0f,        0f);
    Vector3 pSE = new Vector3(_model.X, 0f,        0f);
    Vector3 pNE = new Vector3(_model.X, 0f,        _model.Z);
    Vector3 pNW = new Vector3(0f,       0f,        _model.Z);

    if (_model.SW.Exists) CornerModule.AddOutwardCorner(b, pSW, Facing.North, P);
    if (_model.SE.Exists) CornerModule.AddOutwardCorner(b, pSE, Facing.West,  P);
    if (_model.NE.Exists) CornerModule.AddOutwardCorner(b, pNE, Facing.South, P);
    if (_model.NW.Exists) CornerModule.AddOutwardCorner(b, pNW, Facing.East,  P);
    
    // --- ROAD INFILL (center + strips) ---
    RoadFillModule.AddRoad(b, _model);

    // --- Finish ---
        var mesh = b.ToMesh(metersPerTile: metersPerTile);
    GetComponent<MeshFilter>().sharedMesh = mesh;
    if (material != null) GetComponent<MeshRenderer>().sharedMaterial = material;
}

    void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);

        // Draw gizmos in playmode:
        if (_model == null) return;

        // Draw outline
        Gizmos.DrawLine(_model.SW.Origin, _model.SE.Origin);
        Gizmos.DrawLine(_model.SE.Origin, _model.NE.Origin);
        Gizmos.DrawLine(_model.NE.Origin, _model.NW.Origin);
        Gizmos.DrawLine(_model.NW.Origin, _model.SW.Origin);

        
        // Visualize apexes if corners exist
        float r = 0.08f;
        if (_model.SW.Exists) Gizmos.DrawWireSphere(_model.SW.Apex, r);
        if (_model.SE.Exists) Gizmos.DrawWireSphere(_model.SE.Apex, r);
        if (_model.NE.Exists) Gizmos.DrawWireSphere(_model.NE.Apex, r);
        if (_model.NW.Exists) Gizmos.DrawWireSphere(_model.NW.Apex, r);
    }
}

[System.Serializable]
public readonly struct CornerInfo
{
    public CornerId Id          { get; }
    public bool     Exists      { get; }   // both adjacent Connected
    public Vector3  Origin      { get; }   // local rect corner (y=0)
    public Vector3  Apex        { get; }   // inner apex on apron plane
    public Side     AdjA        { get; }   // clockwise adjacent edges
    public Side     AdjB        { get; }

    public CornerInfo(CornerId id, bool exists, Vector3 origin, Vector3 apex, Side a, Side b)
    {
        Id = id; Exists = exists; Origin = origin; Apex = apex; AdjA = a; AdjB = b;
    }

    public bool IsAdjacentTo(Side s) => s == AdjA || s == AdjB;
    public Side OppA => Topology.Opposite(AdjA);
    public Side OppB => Topology.Opposite(AdjB);
    public bool CanMeetFootpathOn(Side s) => s == OppA || s == OppB;
}

[System.Serializable]
public readonly struct FootpathCornerInfo
{
    // Which rectangle corner this is (SW/SE/NE/NW)
    public CornerId Corner { get; }
    public bool Exists { get; }
    public Side SideA { get; }
    public Side SideB { get; }
    public Vector3 Origin { get; }
    public Vector3 Apex { get; }

    public FootpathCornerInfo(CornerId corner, bool exists, Side sideA, Side sideB, Vector3 origin, Vector3 apex)
    {
        // Basic validation: sides must be adjacent (not same, not opposite)
        if (!Topology.AreAdjacent(sideA, sideB))
            throw new System.ArgumentException("SideA and SideB must be adjacent (perpendicular).");

        // Optional strong check: the sides must map to the provided corner
        var expectedCorner = Topology.CornerOf(sideA, sideB);
        if (expectedCorner != corner)
            throw new System.ArgumentException($"Sides ({sideA},{sideB}) map to {expectedCorner}, not {corner}.");

        Corner = corner;
        Exists = exists;
        SideA = sideA;
        SideB = sideB;
        Origin = origin;
        Apex = apex;
    }

    // Convenience
    public bool UsesSide(Side s) => s == SideA || s == SideB;
    public (Side a, Side b) Sides => (SideA, SideB);
}



[System.Serializable]
public readonly struct FootpathInfo
{
    public Side     Side       { get; }  // which edge
    public bool     Exists     { get; }  // !Connected
    public float    Inset      { get; }  // depthReach
    public float    Y          { get; }  // apron plane Y
    public Vector3  A          { get; }  // segment endpoint (local)
    public Vector3  B          { get; }
    public CornerId NearCorner { get; }  // along-order
    public CornerId FarCorner  { get; }

    public FootpathInfo(Side side, bool exists, float inset, float y,
                        Vector3 a, Vector3 b, CornerId near, CornerId far)
    {
        Side = side; Exists = exists; Inset = inset; Y = y; A = a; B = b; NearCorner = near; FarCorner = far;
    }

    public bool IsOppositeOf(in CornerInfo c) => c.CanMeetFootpathOn(Side);
}

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


public sealed class IntersectionModel
{
    // ---- Inputs (local space) ----
    public readonly float X;   // width  along +X
    public readonly float Z;   // depth  along +Z
    public readonly bool ConnNorth, ConnEast, ConnSouth, ConnWest;

    // Geometric Spacing
    public readonly float WidthReach;   // floorWidth + extend + apronExtend
    public readonly float DepthReach;   // floorDepth + extend + apronExtend
    public readonly float ApronY;       // y of apron plane

    // ---- Precomputed results ----
    public readonly CornerInfo SW, SE, NE, NW;
    public readonly FootpathInfo FP_S, FP_E, FP_N, FP_W;
    // Footpath/inner corner set (exists when both adjacent sides have footpaths = both NOT connected)
    public readonly FootpathCornerInfo FP_SW, FP_SE, FP_NE, FP_NW;

    public IntersectionModel(
    float x, float z,
    bool ConnectedNorth, bool ConnectedEast, bool ConnectedSouth, bool ConnectedWest,
    float widthReach, float depthReach, float apronY)
    {
        X = Mathf.Abs(x); Z = Mathf.Abs(z);
        ConnNorth = ConnectedNorth; ConnEast = ConnectedEast; ConnSouth = ConnectedSouth; ConnWest = ConnectedWest;
        WidthReach = Mathf.Abs(widthReach);
        DepthReach = Mathf.Abs(depthReach);
        ApronY = apronY;

        // --- Corner existence logic ---
        // "Apron/outward corner" exists when both adjacent sides ARE connected.
        bool hasSW = ConnSouth && ConnWest;
        bool hasSE = ConnSouth && ConnEast;
        bool hasNE = ConnNorth && ConnEast;
        bool hasNW = ConnNorth && ConnWest;

        // "Footpath/inner corner" exists when both adjacent sides have footpaths (i.e., both NOT connected).
        bool fpHasSW = !ConnSouth && !ConnWest;
        bool fpHasSE = !ConnSouth && !ConnEast;
        bool fpHasNE = !ConnNorth && !ConnEast;
        bool fpHasNW = !ConnNorth && !ConnWest;

        // --- Outer rectangle origins (y = 0) ---
        Vector3 oSW = new Vector3(0, 0, 0);
        Vector3 oSE = new Vector3(X, 0, 0);
        Vector3 oNE = new Vector3(X, 0, Z);
        Vector3 oNW = new Vector3(0, 0, Z);

        // --- Apron plane apexes (deepest inward footprint, y = ApronY) ---
        Vector3 aSW = new Vector3(WidthReach, ApronY, DepthReach);
        Vector3 aSE = new Vector3(X - DepthReach, ApronY, WidthReach);
        Vector3 aNE = new Vector3(X - WidthReach, ApronY, Z - DepthReach);
        Vector3 aNW = new Vector3(DepthReach, ApronY, Z - WidthReach);

        // --- Build both corner types ---
        SW = BuildCorner(CornerId.SW, hasSW, oSW, aSW);
        SE = BuildCorner(CornerId.SE, hasSE, oSE, aSE);
        NE = BuildCorner(CornerId.NE, hasNE, oNE, aNE);
        NW = BuildCorner(CornerId.NW, hasNW, oNW, aNW);

        FP_SW = BuildFootpathCorner(CornerId.SW, fpHasSW, oSW, aSW);
        FP_SE = BuildFootpathCorner(CornerId.SE, fpHasSE, oSE, aSE);
        FP_NE = BuildFootpathCorner(CornerId.NE, fpHasNE, oNE, aNE);
        FP_NW = BuildFootpathCorner(CornerId.NW, fpHasNW, oNW, aNW);

        // --- Per-side footpath lines on the apron plane inset by DepthReach ---
        FP_S = BuildFootpath(Side.South, !ConnSouth);
        FP_E = BuildFootpath(Side.East, !ConnEast);
        FP_N = BuildFootpath(Side.North, !ConnNorth);
        FP_W = BuildFootpath(Side.West, !ConnWest);
    }

    // ---------- Minimal helpers ----------
    /// Project a local point to an edge line at height y.
    /// inset = 0 hits the outer border; use DepthReach to hit the footpath reach line.
    public Vector3 ProjectToEdge(Vector3 p, Side edge, float y, float inset = 0f)
    {
        switch (edge)
        {
            case Side.South: return new Vector3(Mathf.Clamp(p.x, 0f, X), y, inset);
            case Side.North: return new Vector3(Mathf.Clamp(p.x, 0f, X), y, Z - inset);
            case Side.West: return new Vector3(inset, y, Mathf.Clamp(p.z, 0f, Z));
            default: return new Vector3(X - inset, y, Mathf.Clamp(p.z, 0f, Z)); // East
        }
    }

    /// Axis-aligned segment for the given edge at height y (outer if inset=0).
    public (Vector3 a, Vector3 b) EdgeLine(Side edge, float y, float inset = 0f)
    {
        return edge switch
        {
            Side.South => (new Vector3(0f, y, inset), new Vector3(X, y, inset)),
            Side.North => (new Vector3(0f, y, Z - inset), new Vector3(X, y, Z - inset)),
            Side.West => (new Vector3(inset, y, 0f), new Vector3(inset, y, Z)),
            _ => (new Vector3(X - inset, y, 0f), new Vector3(X - inset, y, Z)), // East
        };
    }

    // ---------- Internals ----------
    private FootpathInfo BuildFootpath(Side side, bool exists)
    {
        var (a, b) = EdgeLine(side, ApronY, DepthReach);
        // decide near/far by side direction
        var (near, far) = side switch
        {
            Side.South => (CornerId.SW, CornerId.SE),
            Side.North => (CornerId.NW, CornerId.NE),
            Side.West => (CornerId.SW, CornerId.NW),
            _ => (CornerId.SE, CornerId.NE), // East
        };
        return new FootpathInfo(side, exists, DepthReach, ApronY, a, b, near, far);
    }

    private FootpathCornerInfo BuildFootpathCorner(CornerId id, bool exists, Vector3 origin, Vector3 apex)
    {
        var (sideA, sideB) = Topology.AdjacentOf(id); // clockwise
        return new FootpathCornerInfo(id, exists, sideA, sideB, origin, apex);
    }

    private CornerInfo BuildCorner(CornerId id, bool exists, Vector3 origin, Vector3 apex)
    {
        var (adjA, adjB) = Topology.AdjacentOf(id); // clockwise
        return new CornerInfo(id, exists, origin, apex, adjA, adjB);
    }
    
    // ---------- Convenience (optional) ----------
    /// Inner corner on the footpath FLOOR line (useful if you want caps at floor depth instead of apron apex).
    public Vector3 GetFootpathInnerCorner(CornerId id, float footpathDepth, float footpathY)
    {
        footpathDepth = Mathf.Abs(footpathDepth);
        return id switch
        {
            CornerId.SW => new Vector3( footpathDepth, footpathY,  footpathDepth),
            CornerId.SE => new Vector3( X - footpathDepth, footpathY,  footpathDepth),
            CornerId.NE => new Vector3( X - footpathDepth, footpathY,  Z - footpathDepth),
            _           => new Vector3( footpathDepth, footpathY,  Z - footpathDepth), // NW
        };
    }
}


// --- Geometry Modules ---
public enum RoadTopology { Plaza, DeadEnd, I, L, T, X }

public static class RoadFillModule
{
    const float EPS = 1e-5f;

    public static void AddRoad(MeshBuilder b, IntersectionModel m)
    {
        var topo = Classify(m);

        switch (topo)
        {
            case RoadTopology.X: BuildX(b, m); break;
            case RoadTopology.I: BuildI(b, m); break;

            // TODO: add remaining recipes following the planner
            case RoadTopology.L: /* BuildL(b, m); */ break;
            case RoadTopology.T: /* BuildT(b, m); */ break;
            case RoadTopology.DeadEnd: /* BuildDeadEnd(b, m); */ break;
            case RoadTopology.Plaza:
            default: break;
        }
    }

    private static RoadTopology Classify(IntersectionModel m)
    {
        bool N = m.ConnNorth, E = m.ConnEast, S = m.ConnSouth, W = m.ConnWest;
        int cnt = (N ? 1 : 0) + (E ? 1 : 0) + (S ? 1 : 0) + (W ? 1 : 0);

        if (cnt == 4) return RoadTopology.X;
        if (cnt == 0) return RoadTopology.Plaza;
        if (cnt == 1) return RoadTopology.DeadEnd;

        if (cnt == 2) return ((N && S) || (E && W)) ? RoadTopology.I : RoadTopology.L;

        return RoadTopology.T; // cnt == 3
    }

    // ---------- X (4-way) ----------
    private static void BuildX(MeshBuilder b, IntersectionModel m)
    {
        // In an X-junction all four apron corners should exist
        if (!(m.NW.Exists && m.NE.Exists && m.SE.Exists && m.SW.Exists)) return;

        // Four apron apexes (pin to apron plane for perfect coplanarity)
        Vector3 aNW = SetY(m.NW.Apex, m.ApronY);
        Vector3 aNE = SetY(m.NE.Apex, m.ApronY);
        Vector3 aSE = SetY(m.SE.Apex, m.ApronY);
        Vector3 aSW = SetY(m.SW.Apex, m.ApronY);

        // Center quad — ring order NW -> NE -> SE -> SW (convex), force upward-facing
        b.AddQuadByPosRobust(aNW, aNE, aSE, aSW, Vector3.up);

    }

    // ---------- I (straight) ----------
    private static void BuildI(MeshBuilder b, IntersectionModel m)
    {

    }

    // ---------- L (2-Way/Turn) ----------
    private static void BuildL(MeshBuilder b, IntersectionModel m)
    {

    }

    // ---------- T (3-Way) ----------
    private static void BuildT(MeshBuilder b, IntersectionModel m)
    {

    }

    // ---------- DeadEnd ----------
    private static void BuildDeadEnd(MeshBuilder b, IntersectionModel m)
    {

    }

    // ---------- Plaza ----------
    private static void BuildPlaza(MeshBuilder b, IntersectionModel m)
    {

    }

    // ---------- Helpers / Utility -----------
    private static Vector3 SetY(Vector3 v, float y) => new Vector3(v.x, y, v.z);


}

