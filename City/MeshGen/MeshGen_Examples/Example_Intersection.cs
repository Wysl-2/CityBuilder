using UnityEngine;
using ProcGen; // your MeshBuilder namespace

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class Example_Intersection : MonoBehaviour
{
    public Material material;

    [Header("Intersection Params")]
    public float x;
    public float z;
    [Header("Connections")]
    public bool connectedNorth;
    public bool connectedSouth;
    public bool connectedEast;
    public bool connectedWest;

    [Header("Corner Params")]
    public float floorWidth = 2f;
    public float floorDepth = 2f;
    public float extend = 0.1f;
    public float drop = 0.1f;
    public float gutterDepth = 0.2f;
    public float apronExtend = 0.2f;
    public float apronMeetY = -0.05f;

    [Header("UVs")]
    public float metersPerTile = 1f;

    void Start()
    {
        var b = new MeshBuilder();
        var P = new CornerParams
        {
            floorWidth = floorWidth,
            floorDepth = floorDepth,
            extend = extend,
            drop = drop,
            gutterDepth = gutterDepth,
            apronExtend = apronExtend,
            apronMeetY = apronMeetY
        };

        // Bounds
        float X = Mathf.Abs(x);
        float Z = Mathf.Abs(z);

        // Local-space corners (y = 0 plane)
        Vector3 p0 = new Vector3(0f, 0f, 0f);
        Vector3 p1 = new Vector3(X, 0f, 0f);
        Vector3 p2 = new Vector3(X, 0f, Z);
        Vector3 p3 = new Vector3(0f, 0f, Z);

        // -- Corner Apex Points --
        float widthReach = floorWidth + extend + apronExtend;
        float depthReach = floorDepth + extend + apronExtend;
        // SW
        float swApex_x = widthReach;
        float swApex_z = depthReach;
        Vector3 swApex = new Vector3(swApex_x, apronMeetY, swApex_z);
        // NW
        float nwApex_x = depthReach;
        float nwApex_z = Z - widthReach;
        Vector3 nwApex = new Vector3(nwApex_x, apronMeetY, nwApex_z);
        // NE
        float neApex_x = X - widthReach;
        float neApex_z = Z - depthReach;
        Vector3 neApex = new Vector3(neApex_x, apronMeetY, neApex_z);
        // SE
        float seApex_x = X - depthReach;
        float seApex_z = widthReach;
        Vector3 seApex = new Vector3(seApex_x, apronMeetY, seApex_z);

        // --- Build corners conditionally (nullable so we can guard later) ---
        CornerModule.CornerRims? rSW = null, rSE = null, rNE = null, rNW = null;

        if (connectedSouth && connectedWest)
            rSW = CornerModule.AddOutwardCorner(b, p0, Facing.North, P);   // SW

        if (connectedNorth && connectedWest)
            rNW = CornerModule.AddOutwardCorner(b, p3, Facing.East, P);   // NW

        if (connectedSouth && connectedEast)
            rSE = CornerModule.AddOutwardCorner(b, p1, Facing.West, P);   // SE

        if (connectedNorth && connectedEast)
            rNE = CornerModule.AddOutwardCorner(b, p2, Facing.South, P);   // NE


        // --- Footpaths: add along edges with NO road connection ---
        var corners = GetCornerPositions();
        float footpathY = 0f;                  // same plane as your road floor
        float footpathDepth = P.floorDepth;    // how far to push inward

        if (!connectedWest)
            FootpathModule.AddFootpath(b, corners, P, Facing.West, footpathDepth, footpathY);

        if (!connectedEast)
            FootpathModule.AddFootpath(b, corners, P, Facing.East, footpathDepth, footpathY);

        if (!connectedNorth)
            FootpathModule.AddFootpath(b, corners, P, Facing.North, footpathDepth, footpathY);

        if (!connectedSouth)
            FootpathModule.AddFootpath(b, corners, P, Facing.South, footpathDepth, footpathY);

            // === Carriageway fill (y = apronMeetY) =================================
        // helper
        static Vector3? Maybe(bool cond, Vector3 p) => cond ? p : (Vector3?)null;

        // which corners exist (same rules as your mesh)
        bool hasSW = connectedSouth && connectedWest;
        bool hasNW = connectedNorth && connectedWest;
        bool hasNE = connectedNorth && connectedEast;
        bool hasSE = connectedSouth && connectedEast;

        // Footpath meet points
        // SW corner → North/East footpaths
        Vector3? SW_to_N = Maybe(hasSW && !connectedNorth, new Vector3(swApex.x, apronMeetY, Z - depthReach));
        Vector3? SW_to_E = Maybe(hasSW && !connectedEast,  new Vector3(X - depthReach, apronMeetY, swApex.z));

        // NW corner → East/South footpaths
        Vector3? NW_to_E = Maybe(hasNW && !connectedEast,  new Vector3(X - depthReach, apronMeetY, nwApex.z));
        Vector3? NW_to_S = Maybe(hasNW && !connectedSouth, new Vector3(nwApex.x, apronMeetY, depthReach));

        // NE corner → South/West footpaths
        Vector3? NE_to_S = Maybe(hasNE && !connectedSouth, new Vector3(neApex.x, apronMeetY, depthReach));
        Vector3? NE_to_W = Maybe(hasNE && !connectedWest,  new Vector3(depthReach, apronMeetY, neApex.z));

        // SE corner → West/North footpaths
        Vector3? SE_to_W = Maybe(hasSE && !connectedWest,  new Vector3(depthReach, apronMeetY, seApex.z));
        Vector3? SE_to_N = Maybe(hasSE && !connectedNorth, new Vector3(seApex.x, apronMeetY, Z - depthReach));

        // Projected Edge points for corners and footpath meet points
        // SOUTH edge (z = 0) — from SE/SW apex + their meets toward W/E
        Vector3? south_seApex = hasSE ? (Vector3?)EdgePoint(seApex, Facing.South, apronMeetY) : null;
        Vector3? south_swApex = hasSW ? (Vector3?)EdgePoint(swApex, Facing.South, apronMeetY) : null;
        Vector3? south_seToW  = EdgePointIf(SE_to_W, Facing.South, apronMeetY);
        Vector3? south_swToE  = EdgePointIf(SW_to_E, Facing.South, apronMeetY);

        // NORTH edge (z = Z) — from NE/NW apex + their meets toward W/E
        Vector3? north_neApex = hasNE ? (Vector3?)EdgePoint(neApex, Facing.North, apronMeetY) : null;
        Vector3? north_nwApex = hasNW ? (Vector3?)EdgePoint(nwApex, Facing.North, apronMeetY) : null;
        Vector3? north_neToW  = EdgePointIf(NE_to_W, Facing.North, apronMeetY);
        Vector3? north_nwToE  = EdgePointIf(NW_to_E, Facing.North, apronMeetY);

        // WEST edge (x = 0) — from SW/NW apex + their meets toward N/S
        Vector3? west_swApex = hasSW ? (Vector3?)EdgePoint(swApex, Facing.West, apronMeetY) : null;
        Vector3? west_nwApex = hasNW ? (Vector3?)EdgePoint(nwApex, Facing.West, apronMeetY) : null;
        Vector3? west_swToN  = EdgePointIf(SW_to_N, Facing.West, apronMeetY);
        Vector3? west_nwToS  = EdgePointIf(NW_to_S, Facing.West, apronMeetY);

        // EAST edge (x = X) — from SE/NE apex + their meets toward N/S
        Vector3? east_seApex = hasSE ? (Vector3?)EdgePoint(seApex, Facing.East, apronMeetY) : null;
        Vector3? east_neApex = hasNE ? (Vector3?)EdgePoint(neApex, Facing.East, apronMeetY) : null;
        Vector3? east_seToN  = EdgePointIf(SE_to_N, Facing.East, apronMeetY);
        Vector3? east_neToS  = EdgePointIf(NE_to_S, Facing.East, apronMeetY);


        // --- Strips (corner↔corner OR corner↔footpath) @ y = apronMeetY ---------

        // Always face the quad upward regardless of side/order.
        void AddQuadFacingUp(Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3)
        {
            Vector3 n = Vector3.Cross(v1 - v0, v3 - v0);
            var w = (n.y >= 0f) ? Winding.CCW : Winding.CW;
            b.AddQuadByPos(v0, v1, v2, v3, w);
        }

        // Same as before but nullable-friendly for edge-projected points.
        void AddStripUp(Vector3? mA, Vector3 pA, Vector3? mB, Vector3 pB)
        {
            if (!(mA.HasValue && mB.HasValue)) return;
            AddQuadFacingUp(mA.Value, pA, mB.Value, pB);
        }

        // WEST edge (x=0): SW/NW or corner→footpath
        if (connectedWest && rSW.HasValue && rNW.HasValue)
        {
            AddQuadFacingUp(rSW.Value.rimZ.a, rSW.Value.apex,
                            rNW.Value.rimZ.a, rNW.Value.apex);
        }
        else
        {
            if (hasSW && SW_to_N.HasValue) AddStripUp(west_swApex, swApex, west_swToN, SW_to_N.Value);
            if (hasNW && NW_to_S.HasValue) AddStripUp(west_nwApex, nwApex, west_nwToS, NW_to_S.Value);
        }

        // EAST edge (x=X): SE/NE or corner→footpath
        if (connectedEast && rSE.HasValue && rNE.HasValue)
        {
            AddQuadFacingUp(rSE.Value.rimZ.a, rSE.Value.apex,
                            rNE.Value.rimZ.a, rNE.Value.apex);
        }
        else
        {
            if (hasSE && SE_to_N.HasValue) AddStripUp(east_seApex, seApex, east_seToN, SE_to_N.Value);
            if (hasNE && NE_to_S.HasValue) AddStripUp(east_neApex, neApex, east_neToS, NE_to_S.Value);
        }

        // NORTH edge (z=Z): NW/NE or corner→footpath
        if (connectedNorth && rNW.HasValue && rNE.HasValue)
        {
            AddQuadFacingUp(rNW.Value.rimX.a, rNW.Value.apex,
                            rNE.Value.rimX.a, rNE.Value.apex);
        }
        else
        {
            if (hasNE && NE_to_W.HasValue) AddStripUp(north_neApex, neApex, north_neToW, NE_to_W.Value);
            if (hasNW && NW_to_E.HasValue) AddStripUp(north_nwApex, nwApex, north_nwToE, NW_to_E.Value);
        }

        // SOUTH edge (z=0): SW/SE or corner→footpath
        if (connectedSouth && rSW.HasValue && rSE.HasValue)
        {
            AddQuadFacingUp(rSW.Value.rimX.a, rSW.Value.apex,
                            rSE.Value.rimX.a, rSE.Value.apex);
        }
        else
        {
            if (hasSE && SE_to_W.HasValue) AddStripUp(south_seApex, seApex, south_seToW, SE_to_W.Value);
            if (hasSW && SW_to_E.HasValue) AddStripUp(south_swApex, swApex, south_swToE, SW_to_E.Value);
        }

        // -- Center Fill --
        // All Four Corners Exist:
        if (rSW.HasValue && rSE.HasValue && rNE.HasValue && rNW.HasValue)
            b.AddQuadByPos(rSW.Value.apex, rSE.Value.apex,
                        rNW.Value.apex, rNE.Value.apex);


        var mesh = b.ToMesh(metersPerTile: metersPerTile);
        GetComponent<MeshFilter>().sharedMesh = mesh;
        if (material != null) GetComponent<MeshRenderer>().sharedMaterial = material;
    }

    public CornerPositions GetCornerPositions()
    {
        float X = Mathf.Abs(x), Z = Mathf.Abs(z);
        return new CornerPositions
        {
            SW = new Vector3(0f, 0f, 0f),
            SE = new Vector3(X, 0f, 0f),
            NE = new Vector3(X, 0f, Z),
            NW = new Vector3(0f, 0f, Z)
        };
    }

    // Projects a local-space point `p` to the specified edge of the intersection.
    // `y` sets the output height. `inset` = 0 hits the outer edge; use `depthReach` for the footpath line.
    private Vector3 ProjectToEdge(Vector3 p, Facing edge, float y, float inset = 0f)
    {
        float X = Mathf.Abs(x);
        float Z = Mathf.Abs(z);

        switch (edge)
        {
            case Facing.South: // z = 0 + inset
                return new Vector3(Mathf.Clamp(p.x, 0f, X), y, inset);

            case Facing.North: // z = Z - inset
                return new Vector3(Mathf.Clamp(p.x, 0f, X), y, Z - inset);

            case Facing.West:  // x = 0 + inset
                return new Vector3(inset, y, Mathf.Clamp(p.z, 0f, Z));

            default: // Facing.East: x = X - inset
                return new Vector3(X - inset, y, Mathf.Clamp(p.z, 0f, Z));
        }
    }

    // Returns the projected point on the specified edge at y.
    private Vector3 EdgePoint(Vector3 p, Facing edge, float y) =>
        ProjectToEdge(p, edge, y, 0f);

    // Returns null if source is null; otherwise the projected point on the edge at y.
    private Vector3? EdgePointIf(Vector3? p, Facing edge, float y) =>
        p.HasValue ? ProjectToEdge(p.Value, edge, y, 0f) : (Vector3?)null;


    void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);

        float X = Mathf.Abs(x);
        float Z = Mathf.Abs(z);

        // -- Outline --
        var corners = GetCornerPositions();
        Gizmos.DrawLine(corners.SW, corners.SE);
        Gizmos.DrawLine(corners.SE, corners.NE);
        Gizmos.DrawLine(corners.NE, corners.NW);
        Gizmos.DrawLine(corners.NW, corners.SW);

        // -- Corner Apex Points --
        Vector3 ApexForCorner(Vector3 cornerOrigin, Facing cornerFacing,
                      float widthReach, float depthReach, float y)
        {
            FacingUtil.GetAxes(cornerFacing, out var R, out var F, out _);
            var p = cornerOrigin + R * widthReach + F * depthReach;
            p.y = y;
            return p;
        }

        float widthReach = floorWidth + extend + apronExtend;
        float depthReach = floorDepth + extend + apronExtend;

        var swApex = ApexForCorner(corners.SW, Facing.North, widthReach, depthReach, apronMeetY);
        var nwApex = ApexForCorner(corners.NW, Facing.East, widthReach, depthReach, apronMeetY);
        var neApex = ApexForCorner(corners.NE, Facing.South, widthReach, depthReach, apronMeetY);
        var seApex = ApexForCorner(corners.SE, Facing.West, widthReach, depthReach, apronMeetY);

        // Draw only if that corner would be generated
        if (connectedSouth && connectedWest) Gizmos.DrawWireSphere(swApex, 0.1f); // SW
        if (connectedNorth && connectedWest) Gizmos.DrawWireSphere(nwApex, 0.1f); // NW
        if (connectedNorth && connectedEast) Gizmos.DrawWireSphere(neApex, 0.1f); // NE
        if (connectedSouth && connectedEast) Gizmos.DrawWireSphere(seApex, 0.1f); // SE

        // -- Footpath Reach Markings --
        float yMark = apronMeetY;

        // WEST (x = depthReach)
        if (!connectedWest)
        {
            Vector3 west_start = new Vector3(depthReach, yMark, 0f);
            Vector3 west_end = new Vector3(depthReach, yMark, Z);
            Gizmos.DrawLine(west_start, west_end);
        }

        // EAST (x = X - depthReach)
        if (!connectedEast)
        {
            Vector3 east_start = new Vector3(X - depthReach, yMark, 0f);
            Vector3 east_end = new Vector3(X - depthReach, yMark, Z);
            Gizmos.DrawLine(east_start, east_end);
        }

        // SOUTH (z = depthReach)
        if (!connectedSouth)
        {
            Vector3 south_start = new Vector3(0f, yMark, depthReach);
            Vector3 south_end = new Vector3(X, yMark, depthReach);
            Gizmos.DrawLine(south_start, south_end);
        }

        // NORTH (z = Z - depthReach)
        if (!connectedNorth)
        {
            Vector3 north_start = new Vector3(0f, yMark, Z - depthReach);
            Vector3 north_end = new Vector3(X, yMark, Z - depthReach);
            Gizmos.DrawLine(north_start, north_end);
        }

        // -- Apex → Footpath measurement lines ---------------------------------
        static Vector3? Maybe(bool cond, Vector3 p) => cond ? p : (Vector3?)null;


        // Which corners exist (same rules you use for mesh gen)
        bool hasSW = connectedSouth && connectedWest;
        bool hasNW = connectedNorth && connectedWest;
        bool hasNE = connectedNorth && connectedEast;
        bool hasSE = connectedSouth && connectedEast;

        // SW corner → North/East footpaths
        Vector3? SW_to_N = Maybe(hasSW && !connectedNorth, new Vector3(swApex.x, apronMeetY, Z - depthReach));
        Vector3? SW_to_E = Maybe(hasSW && !connectedEast, new Vector3(X - depthReach, apronMeetY, swApex.z));

        // NW corner → East/South footpaths
        Vector3? NW_to_E = Maybe(hasNW && !connectedEast, new Vector3(X - depthReach, apronMeetY, nwApex.z));
        Vector3? NW_to_S = Maybe(hasNW && !connectedSouth, new Vector3(nwApex.x, apronMeetY, depthReach));

        // NE corner → South/West footpaths
        Vector3? NE_to_S = Maybe(hasNE && !connectedSouth, new Vector3(neApex.x, apronMeetY, depthReach));
        Vector3? NE_to_W = Maybe(hasNE && !connectedWest, new Vector3(depthReach, apronMeetY, neApex.z));

        // SE corner → West/North footpaths
        Vector3? SE_to_W = Maybe(hasSE && !connectedWest, new Vector3(depthReach, apronMeetY, seApex.z));
        Vector3? SE_to_N = Maybe(hasSE && !connectedNorth, new Vector3(seApex.x, apronMeetY, Z - depthReach));

        // draw if present
        void DrawIf(Vector3 apex, Vector3? meet)
        {
            if (meet.HasValue) Gizmos.DrawLine(apex, meet.Value);
        }

        DrawIf(swApex, SW_to_N);
        DrawIf(swApex, SW_to_E);

        DrawIf(nwApex, NW_to_E);
        DrawIf(nwApex, NW_to_S);

        DrawIf(neApex, NE_to_S);
        DrawIf(neApex, NE_to_W);

        DrawIf(seApex, SE_to_W);
        DrawIf(seApex, SE_to_N);

        // --- draw wrappers (keep gizmo drawing separate from data funcs) ---
        void DrawEdgePoint(Vector3 p, Facing edge, float y, float r = 0.07f)
        {
            var m = EdgePoint(p, edge, y);
            Gizmos.DrawWireSphere(m, r);
        }
        void DrawEdgePointIf(Vector3? p, Facing edge, float y, float r = 0.07f)
        {
            if (p.HasValue)
            {
                var m = EdgePoint(p.Value, edge, y);
                Gizmos.DrawWireSphere(m, r);
            }
        }

        // SOUTH edge (z = 0)
        if (hasSE) DrawEdgePoint(seApex, Facing.South, yMark);
        if (hasSW) DrawEdgePoint(swApex, Facing.South, yMark);
        DrawEdgePointIf(SE_to_W, Facing.South, yMark);
        DrawEdgePointIf(SW_to_E, Facing.South, yMark);

        // NORTH edge (z = Z)
        if (hasNE) DrawEdgePoint(neApex, Facing.North, yMark);
        if (hasNW) DrawEdgePoint(nwApex, Facing.North, yMark);
        DrawEdgePointIf(NE_to_W, Facing.North, yMark);
        DrawEdgePointIf(NW_to_E, Facing.North, yMark);

        // WEST edge (x = 0)
        if (hasSW) DrawEdgePoint(swApex, Facing.West, yMark);
        if (hasNW) DrawEdgePoint(nwApex, Facing.West, yMark);
        DrawEdgePointIf(SW_to_N, Facing.West, yMark);
        DrawEdgePointIf(NW_to_S, Facing.West, yMark);

        // EAST edge (x = X)
        if (hasSE) DrawEdgePoint(seApex, Facing.East, yMark);
        if (hasNE) DrawEdgePoint(neApex, Facing.East, yMark);
        DrawEdgePointIf(SE_to_N, Facing.East, yMark);
        DrawEdgePointIf(NE_to_S, Facing.East, yMark);
    }
    
}

public struct CornerPositions
{
    public Vector3 NW;
    public Vector3 NE;
    public Vector3 SW;
    public Vector3 SE;
}


public enum Facing { North, East, South, West }

public struct CornerParams
{
    public float floorWidth;   // e.g. 2
    public float floorDepth;   // e.g. 2
    public float extend;       // skirt/apron horizontal extend
    public float drop;         // skirt drop
    public float gutterDepth;  // curtain drop
    public float apronExtend;  // apron horizontal extend
    public float apronMeetY;   // world-space Y the far apron edge meets
}

public static class FootpathModule
{
    /// <summary>
    /// Builds a footpath strip along an edge (A->B).
    /// You can limit the run by giving cap points that lie on the "cap line"
    /// (defined by capInset/capY). If no caps are provided, the whole edge is used.
    ///
    /// Notes:
    /// - A and B are derived from the intersection rectangle and the given Facing.
    /// - capAtStart trims near A; capAtEnd trims near B.
    /// - If you want to trim based on the apron footprint, pass capY = model.ApronY
    ///   and capInset = (e.g.) model.DepthReach or your chosen reach value.
    /// - All t-mapping is done in A->B direction (not min/max), so it works for
    ///   both increasing and decreasing axis edges.
    /// </summary>
    public static void AddFootpath(
        MeshBuilder b,
        CornerPositions corners,
        CornerParams P,
        Facing edge,
        float depth,                 // footpath depth (outer->inner offset magnitude)
        float y,                     // footpath floor plane
        Vector3? capAtStart = null,  // trims near A  (optional)
        Vector3? capAtEnd   = null,  // trims near B  (optional)
        float capInset = float.NaN,  // inset of the cap line (defaults to |depth|)
        float capY     = float.NaN   // Y of the cap line (defaults to y)
    )
    {
        const float EPS = 1e-5f;

        // --- Resolve A (start) and B (end) on the OUTER edge at floor Y ---
        Vector3 A, B;
        switch (edge)
        {
            case Facing.North: A = corners.NW; B = corners.NE; break; // runs +X at z=Z
            case Facing.East:  A = corners.NE; B = corners.SE; break; // runs -Z at x=X (A.z > B.z)
            case Facing.South: A = corners.SE; B = corners.SW; break; // runs -X at z=0  (A.x > B.x)
            default:           A = corners.SW; B = corners.NW; break; // West: runs +Z at x=0
        }
        A.y = y; B.y = y;

        bool vertical = Mathf.Abs(A.x - B.x) <= EPS;
        Vector3 edgeDir = (B - A).sqrMagnitude > EPS ? (B - A).normalized : Vector3.right;
        Vector3 inDir   = -Vector3.Cross(edgeDir, Vector3.up).normalized; // inward (outer -> inner)

        // --- Cap line definition (defaults to inner footpath floor line) ---
        float capLineInset = float.IsNaN(capInset) ? Mathf.Abs(depth) : Mathf.Abs(capInset);
        float capLineY     = float.IsNaN(capY)     ? y                : capY;

        // --- Project a point to the CAP line *for this edge*, then map to t in A->B space ---
        Vector3 ProjectToCapLine(Vector3 p)
        {
            if (vertical)
            {
                // Edge at x = const (0 or X). For West: cap x = A.x + inset; for East: cap x = A.x - inset
                bool isWestEdge = edge == Facing.West; // West edge has smaller x
                float xi = isWestEdge ? (A.x + capLineInset) : (A.x - capLineInset);
                // Clamp along the varying (z) axis between A and B (not min/max ambiguously)
                float zi = Mathf.Clamp(p.z, Mathf.Min(A.z, B.z), Mathf.Max(A.z, B.z));
                return new Vector3(xi, capLineY, zi);
            }
            else
            {
                // Edge at z = const (0 or Z). For South: cap z = A.z + inset; for North: cap z = A.z - inset
                bool isSouthEdge = edge == Facing.South; // South edge has smaller z
                float zi = isSouthEdge ? (A.z + capLineInset) : (A.z - capLineInset);
                // Clamp along the varying (x) axis between A and B
                float xi = Mathf.Clamp(p.x, Mathf.Min(A.x, B.x), Mathf.Max(A.x, B.x));
                return new Vector3(xi, capLineY, zi);
            }
        }

        float? tStart = null, tEnd = null;

        if (capAtStart.HasValue)
        {
            var c = ProjectToCapLine(capAtStart.Value);
            tStart = vertical
                ? Mathf.InverseLerp(A.z, B.z, c.z)  // use A/B to preserve direction
                : Mathf.InverseLerp(A.x, B.x, c.x);
        }
        if (capAtEnd.HasValue)
        {
            var c = ProjectToCapLine(capAtEnd.Value);
            tEnd = vertical
                ? Mathf.InverseLerp(A.z, B.z, c.z)
                : Mathf.InverseLerp(A.x, B.x, c.x);
        }

        // Normalize t-range
        if (tStart.HasValue && tEnd.HasValue && tStart.Value > tEnd.Value)
            (tStart, tEnd) = (tEnd, tStart);

        float t0 = Mathf.Clamp01(tStart ?? 0f);
        float t1 = Mathf.Clamp01(tEnd   ?? 1f);

        // Degenerate span? nothing to emit
        if (t1 - t0 <= EPS) return;

        // --- Trimmed outer edge A->B ---
        Vector3 AT = Vector3.Lerp(A, B, t0);
        Vector3 BT = Vector3.Lerp(A, B, t1);

        // --- Build geometry stack ---
        float d = Mathf.Abs(depth);

        // Floor band: outer -> inner offset
        Vector3 inward = inDir * d;
        Vector3 Ain = AT + inward;
        Vector3 Bin = BT + inward;

        // Floor
        b.Add(Quad.FromEdgeExtrudeFacing(AT, BT, inward, outwardHint: Vector3.up));

        // Skirt (lip)
        Vector3 skirtOff = inDir * Mathf.Abs(P.extend) - Vector3.up * Mathf.Abs(P.drop);
        Vector3 sA = Ain + skirtOff;
        Vector3 sB = Bin + skirtOff;
        b.Add(Quad.FromEdgeExtrudeFacing(Ain, Bin, skirtOff, outwardHint: inDir));

        // Curtain (vertical faces to gutter)
        Vector3 curtainDown = -Vector3.up * Mathf.Abs(P.gutterDepth);
        Vector3 cA = sA + curtainDown;
        Vector3 cB = sB + curtainDown;
        b.Add(Quad.FromEdgeExtrudeFacing(sA, sB, curtainDown, outwardHint: inDir));

        // Apron (extend inward + rise to meet apron plane)
        float rise = P.apronMeetY - cA.y; // cA/cB share y
        Vector3 apronOff = inDir * Mathf.Abs(P.apronExtend) + Vector3.up * rise;
        b.Add(Quad.FromEdgeExtrudeFacing(cA, cB, apronOff, outwardHint: Vector3.up));
    }

    /// <summary>
    /// Trim by normalized params on the OUTER edge (0..1 along A->B).
    /// This avoids computing cap points. Internally synthesizes caps on the cap line.
    /// </summary>
    public static void AddFootpathT(
        MeshBuilder b,
        CornerPositions corners,
        CornerParams P,
        Facing edge,
        float depth,
        float y,
        float? tStart, float? tEnd,
        float capInset = float.NaN,
        float capY     = float.NaN
    )
    {
        // Build canonical outer A/B for this edge
        Vector3 A, B;
        switch (edge)
        {
            case Facing.North: A = corners.NW; B = corners.NE; break;
            case Facing.East:  A = corners.NE; B = corners.SE; break;
            case Facing.South: A = corners.SE; B = corners.SW; break;
            default:           A = corners.SW; B = corners.NW; break;
        }
        A.y = y; B.y = y;

        // Synthesize cap points at the same t along outer edge (they will be projected to the cap line)
        Vector3? capStart = tStart.HasValue ? Vector3.Lerp(A, B, Mathf.Clamp01(tStart.Value)) : (Vector3?)null;
        Vector3? capEnd   = tEnd.HasValue   ? Vector3.Lerp(A, B, Mathf.Clamp01(tEnd.Value))   : (Vector3?)null;

        AddFootpath(b, corners, P, edge, depth, y, capStart, capEnd, capInset, capY);
    }

    /// <summary>
    /// Trim by distances (meters) from the OUTER edge ends (near A, near B).
    /// </summary>
    public static void AddFootpathMeters(
        MeshBuilder b,
        CornerPositions corners,
        CornerParams P,
        Facing edge,
        float depth,
        float y,
        float? startMeters, float? endMeters,
        float capInset = float.NaN,
        float capY     = float.NaN
    )
    {
        // Build A/B
        Vector3 A, B;
        switch (edge)
        {
            case Facing.North: A = corners.NW; B = corners.NE; break;
            case Facing.East:  A = corners.NE; B = corners.SE; break;
            case Facing.South: A = corners.SE; B = corners.SW; break;
            default:           A = corners.SW; B = corners.NW; break;
        }
        A.y = y; B.y = y;

        float len = Mathf.Max(Vector3.Distance(A, B), 1e-5f);
        float? t0 = startMeters.HasValue ? Mathf.Clamp01(startMeters.Value / len) : (float?)null;
        float? t1 = endMeters.HasValue   ? 1f - Mathf.Clamp01(endMeters.Value   / len) : (float?)null;

        AddFootpathT(b, corners, P, edge, depth, y, t0, t1, capInset, capY);
    }
}


public static class CornerModule
{
    // Local frame mapped to world by orientation
    struct Frame { public Vector3 O, R, U, F; } // origin, Right, Up, Forward

    static Frame MakeFrame(Vector3 origin, Facing facing)
    {
        var U = Vector3.up;
        Vector3 R, F;
        switch (facing)
        {
            case Facing.North: R = Vector3.right; F = Vector3.forward; break;
            case Facing.East: R = Vector3.back; F = Vector3.right; break;
            case Facing.South: R = Vector3.left; F = Vector3.back; break;
            default: R = Vector3.forward; F = Vector3.left; break; // West
        }
        return new Frame { O = origin, R = R, U = U, F = F };
    }

    static Vector3 LW(in Frame f, float x, float y, float z) => f.O + f.R * x + f.U * y + f.F * z; // local->world

    static void AddTriFacing(MeshBuilder b, Vector3 a, Vector3 c, Vector3 d, Vector3 preferNormal)
    {
        // CW order is (a,c,d) consistent with MeshBuilder's AddQuad convention
        var nCW = Vector3.Cross(c - a, d - a);
        var w = (Vector3.Dot(nCW, preferNormal) >= 0f) ? Winding.CW : Winding.CCW;
        b.AddTriByPos(a, c, d, w);
    }

    /// Build one outward corner in the given facing, writing faces into 'b' (no perimeter fills).
    public static CornerRims AddOutwardCorner(MeshBuilder b, Vector3 origin, Facing facing, in CornerParams P)
    {
        var f = MakeFrame(origin, facing);

        // ----- Floor
        var v0 = LW(f, 0, 0, 0);
        var v1 = LW(f, P.floorWidth, 0, 0);
        var v2 = LW(f, 0, 0, P.floorDepth);
        var v3 = LW(f, P.floorWidth, 0, P.floorDepth);
        b.Add(new Quad(v0, v1, v2, v3)); // uses CW convention

        // Edges (north = +Forward side, east = +Right side)
        var aN = v2; var bN = v3;
        var aE = v1; var bE = v3;

        // Offsets
        var northOffset = f.F * Mathf.Abs(P.extend) - f.U * Mathf.Abs(P.drop);
        var eastOffset = f.R * Mathf.Abs(P.extend) - f.U * Mathf.Abs(P.drop);
        var down = -f.U * Mathf.Abs(P.gutterDepth);

        // Skirts
        b.Add(Quad.FromEdgeExtrudeFacing(aN, bN, northOffset, outwardHint: f.F));
        b.Add(Quad.FromEdgeExtrudeFacing(aE, bE, eastOffset, outwardHint: f.R));

        // Corner cap (down-facing tri)
        var pTop = v3;
        var pNorthBottom = bN + northOffset;
        var pEastBottom = bE + eastOffset;
        AddTriFacing(b, pTop, pNorthBottom, pEastBottom, preferNormal: f.U);

        // Vertical curtains
        var northBottomA = aN + northOffset;
        var northBottomB = bN + northOffset;
        var eastBottomA = aE + eastOffset;
        var eastBottomB = bE + eastOffset;

        b.Add(Quad.FromEdgeExtrudeFacing(northBottomA, northBottomB, down, outwardHint: f.F));
        b.Add(Quad.FromEdgeExtrudeFacing(eastBottomA, eastBottomB, down, outwardHint: f.R));
        var outwardDiag = (f.R + f.F).normalized;
        b.Add(Quad.FromEdgeExtrudeFacing(pNorthBottom, pEastBottom, down, outwardHint: outwardDiag));

        // Aprons (sloped)
        var nA0 = northBottomA + down;
        var nB0 = northBottomB + down;
        var eA0 = eastBottomA + down;
        var eB0 = eastBottomB + down;
        var dA0 = pNorthBottom + down;
        var dB0 = pEastBottom + down;

        float riseN = P.apronMeetY - nA0.y;
        float riseE = P.apronMeetY - eA0.y;
        float riseD = P.apronMeetY - dA0.y;

        var offN = f.F * Mathf.Abs(P.apronExtend) + f.U * riseN;
        var offE = f.R * Mathf.Abs(P.apronExtend) + f.U * riseE;
        var offD = outwardDiag * Mathf.Abs(P.apronExtend) + f.U * riseD;

        var northApron = Quad.FromEdgeExtrudeFacing(nA0, nB0, offN, outwardHint: f.U);
        var eastApron = Quad.FromEdgeExtrudeFacing(eA0, eB0, offE, outwardHint: f.U);
        var diagApron = Quad.FromEdgeExtrudeFacing(dA0, dB0, offD, outwardHint: f.U);
        b.Add(northApron); b.Add(eastApron); b.Add(diagApron);

        // Wedge triangles between aprons
        var nB1 = nB0 + offN;
        var eB1 = eB0 + offE;
        var dA1 = dA0 + offD;
        var dB1 = dB0 + offD;
        AddTriFacing(b, dA0, nB1, dA1, f.U);
        AddTriFacing(b, dB0, dB1, eB1, f.U);

        // Outer rim -> apex (to square corner)
        var outerNorthDiag = new Edge(nB1, dA1);
        var outerDiagSpan = new Edge(dA1, dB1);
        var outerDiagEast = new Edge(dB1, eB1);

        // Compute apex in the local frame (max along Right/Forward), then set world Y
        float projR(Vector3 p) => Vector3.Dot(p - f.O, f.R);
        float projF(Vector3 p) => Vector3.Dot(p - f.O, f.F);
        float aR = Mathf.Max(projR(nB1), projR(eB1), projR(dA1), projR(dB1));
        float aF = Mathf.Max(projF(nB1), projF(eB1), projF(dA1), projF(dB1));
        var apex = f.O + f.R * aR + f.F * aF; apex.y = P.apronMeetY;

        AddTriFacing(b, outerNorthDiag.a, outerNorthDiag.b, apex, f.U);
        AddTriFacing(b, outerDiagSpan.a, outerDiagSpan.b, apex, f.U);
        AddTriFacing(b, outerDiagEast.a, outerDiagEast.b, apex, f.U);

        // --- Build the two straight outer rim edges from the aprons (far edges) ---

        // Far edge endpoints of each apron
        var nA1 = nA0 + offN;   // north apron far edge, west end
        var eA1 = eA0 + offE;   // east apron  far edge, south end

        // Ensure they live exactly on the cap plane (y = apronMeetY)
        nA1.y = nB1.y = eA1.y = eB1.y = P.apronMeetY;

        // Edges straight along the aprons' far rims
        var edgeF = new Edge(nA1, nB1); // constant local F (north-facing apron)
        var edgeR = new Edge(eA1, eB1); // constant local R (east-facing apron)

        // Map to world-constant Z vs X (depends on 'facing')
        Edge rimZ, rimX;
        if (Mathf.Abs(edgeF.a.z - edgeF.b.z) <= 1e-4f)
        {
            // edgeF is horizontal in world (constant Z)
            rimZ = edgeF;
            rimX = edgeR;
        }
        else
        {
            // edgeR is horizontal in world (constant Z)
            rimZ = edgeR;
            rimX = edgeF;
        }

        return new CornerRims
        {
            rimZ = rimZ,   // world-constant-Z outer apron edge (two far-edge points)
            rimX = rimX,   // world-constant-X outer apron edge (two far-edge points)
            apex = apex    // inner apex on the cap plane
        };

    }

    public struct CornerRims
    {
        public Edge rimZ;   // straight edge with constant world Z; a = outer end, b = apex
        public Edge rimX;   // straight edge with constant world X; a = outer end, b = apex
        public Vector3 apex;
    }

}

public static class FacingUtil
{
    /// Returns world-space basis vectors for a given corner facing.
    /// right = local +X, forward = local +Z, up = world +Y
    public static void GetAxes(Facing facing, out Vector3 right, out Vector3 forward, out Vector3 up)
    {
        up = Vector3.up;
        switch (facing)
        {
            case Facing.North: right = Vector3.right;   forward = Vector3.forward; break;
            case Facing.East:  right = Vector3.back;    forward = Vector3.right;   break;
            case Facing.South: right = Vector3.left;    forward = Vector3.back;    break;
            default: /* West*/ right = Vector3.forward; forward = Vector3.left;    break;
        }
    }
}

