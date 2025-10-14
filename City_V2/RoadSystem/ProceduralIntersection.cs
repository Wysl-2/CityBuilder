using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.ProBuilder.MeshOperations;

public class ProceduralIntersection : MonoBehaviour
{
    public Material material;
    [Header("Dimensions")]
    public Vector2 Size;
    [Header("Connections")]
    public bool ConnectedNorth;
    public bool ConnectedSouth;
    public bool ConnectedEast;
    public bool ConnectedWest;

    private IntersectionModel Model;

    // Start is called before the first frame update
    void Start()
    {
        Model = new IntersectionModel(Size, ConnectedNorth, ConnectedEast, ConnectedSouth, ConnectedWest);

        if (material)
            GetComponent<MeshRenderer>().sharedMaterial = material;

        var pbMesh = GetComponent<ProBuilderMesh>() ?? gameObject.AddComponent<ProBuilderMesh>();

        CornerModule.CreateCorner(pbMesh, Model.CornerSW);

    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);

        // Draw outline
        Gizmos.DrawLine(new Vector3(0,0,0), new Vector3(Size.x, 0, 0));
        Gizmos.DrawLine(new Vector3(Size.x, 0, 0), new Vector3(Size.x, 0, Size.y));
        Gizmos.DrawLine(new Vector3(Size.x, 0, Size.y), new Vector3(0, 0, Size.y));
        Gizmos.DrawLine(new Vector3(0, 0, Size.y), new Vector3(0,0,0));
    }
}

public enum RoadTopology { Plaza, DeadEnd, I, L, T, X }

public sealed class IntersectionModel
{
    // -- Inputs --
    public readonly Vector2 Size;
    public readonly bool ConnectedNorth, ConnectedEast, ConnectedSouth, ConnectedWest;

    // -- Data --
    public readonly RoadTopology Topology;
    public CornerInfo CornerSE;
    public CornerInfo CornerSW;
    public CornerInfo CornerNE;
    public CornerInfo CornerNW;

    public IntersectionModel(Vector2 size, bool ConnectedNorth, bool ConnectedEast, bool ConnectedSouth, bool ConnectedWest)
    {
        this.Size = size;
        this.ConnectedNorth = ConnectedNorth;
        this.ConnectedEast = ConnectedEast;
        this.ConnectedSouth = ConnectedSouth;
        this.ConnectedWest = ConnectedWest;

        this.Topology = Classify(ConnectedNorth, ConnectedEast, ConnectedSouth, ConnectedWest);

        // Local corner origins on XZ (y = 0)
        Vector3 oSW = new Vector3(0f,        0f, 0f);
        Vector3 oSE = new Vector3(Size.x,    0f, 0f);
        Vector3 oNE = new Vector3(Size.x,    0f, Size.y);
        Vector3 oNW = new Vector3(0f,        0f, Size.y);

        // If you later add an apron inset, update these instead of using Origin directly.
        Vector3 aSW = oSW; // Apex (inner point) — placeholder = Origin
        Vector3 aSE = oSE;
        Vector3 aNE = oNE;
        Vector3 aNW = oNW;

        // Exists = both adjacent sides connected
        bool exSW = ConnectedSouth && ConnectedWest;
        bool exSE = ConnectedSouth && ConnectedEast;
        bool exNE = ConnectedNorth && ConnectedEast;
        bool exNW = ConnectedNorth && ConnectedWest;

        // Adjacent sides in CLOCKWISE order around each corner:
        // SW: West -> South, SE: South -> East, NE: East -> North, NW: North -> West
        CornerSW = new CornerInfo(CornerId.SW, exSW, oSW, aSW, Side.West,  Side.South, 2, 2);
        CornerSE = new CornerInfo(CornerId.SE, exSE, oSE, aSE, Side.South, Side.East,  2, 2);
        CornerNE = new CornerInfo(CornerId.NE, exNE, oNE, aNE, Side.East,  Side.North, 2, 2);
        CornerNW = new CornerInfo(CornerId.NW, exNW, oNW, aNW, Side.North, Side.West,  2, 2);
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

// --- Data Containers ---
public enum Side { South, East, North, West }
public enum CornerId { SW, SE, NE, NW }
[System.Serializable]
public readonly struct CornerInfo
{
    public CornerId Id          { get; }
    public bool     Exists      { get; }   // both adjacent Connected
    public Vector3  Origin      { get; }   // local rect corner (y=0)
    public Vector3  Apex        { get; }   // inner apex on apron plane
    public Side     AdjA        { get; }   // clockwise adjacent edges
    public Side AdjB { get; }

    public float X { get; } // Size along x axis (relative to frame when corner mesh is created)
    public float Z { get; } // Size along z axis (relative to frame when corner mesh is created)


    public CornerInfo(CornerId id, bool exists, Vector3 origin, Vector3 apex, Side a, Side b, float x, float z)
    {
        Id = id; Exists = exists; Origin = origin; Apex = apex; AdjA = a; AdjB = b; X = x; Z = z;
    }

    public bool IsAdjacentTo(Side s) => s == AdjA || s == AdjB;
    public Side OppA => Topology.Opposite(AdjA);
    public Side OppB => Topology.Opposite(AdjB);
    public bool CanMeetFootpathOn(Side s) => s == OppA || s == OppB;
}

// -- Geometry Modules --
public static class CornerModule
{
    // Local frame mapped to world by orientation
    struct Frame { public Vector3 O, R, U, F; } // origin, Right, Up, Forward

    // static Frame MakeFrame(Vector3 origin, Facing facing)
    // {
    //     var U = Vector3.up;
    //     Vector3 R, F;
    //     switch (facing)
    //     {
    //         case Facing.North: R = Vector3.right;  F = Vector3.forward; break; // SW corner
    //         case Facing.East:  R = Vector3.left;   F = Vector3.forward; break; // SE corner
    //         case Facing.South: R = Vector3.left;   F = Vector3.back;    break; // NE corner
    //         default:           R = Vector3.right;  F = Vector3.back;    break; // West -> NW corner
    //     }
    //     return new Frame { O = origin, R = R, U = U, F = F };
    // }

    public static void CreateCorner(ProBuilderMesh pbMesh, CornerInfo info)
    {
        // Build a local frame for this corner
        // var f = MakeFrame(info.Origin, FacingUtil.CornerFacingFor(info.Id));  // f.O, f.R, f.F, f.U

        // // Place verts in that frame (width along R, height along F)
        // float w = info.X, h = info.Z;
        // var verts = new[]
        // {
        //     f.O,                      // v0
        //     f.O + f.R * w,            // v1
        //     f.O + f.R * w + f.F * h,  // v2
        //     f.O + f.F * h,            // v3
        // };

        // // One Face composed of two triangles (winding sets normal direction)
        // var quad = new Face(new int[] { 0, 2, 1, 0, 3, 2 }); // +Y on XZ

        // pbMesh.RebuildWithPositionsAndFaces(verts, new[] { quad });
        // pbMesh.ToMesh();
        // pbMesh.Refresh(RefreshMask.All);

        // // Base face & perimeter
        // var baseFace = pbMesh.faces[0];
        // var edges = ElementSelection.GetPerimeterEdges(pbMesh, new[] { baseFace }).ToList();
        // var P = pbMesh.positions;
        // const float eps = 1e-4f;

        // float maxZ = P.Max(p => p.z);
        // float maxX = P.Max(p => p.x);

        // // Edges: north (max Z), east (max X)
        // var north = edges.First(e =>
        //     Mathf.Abs(P[e.a].z - maxZ) < eps && Mathf.Abs(P[e.b].z - maxZ) < eps);
        // var east = edges.First(e =>
        //     Mathf.Abs(P[e.a].x - maxX) < eps && Mathf.Abs(P[e.b].x - maxX) < eps);

        // // Offsets (note: negative rise = slope down)
        // float heightOffset = -0.25f;
        // float lengthOffset = 0.50f;

        // // Reference frames before extruding (for scoring rim edges later)
        // PBExtrudeUtil.EdgeFrame(pbMesh, north, baseFace, out var nN, out var tN, out var outN);
        // PBExtrudeUtil.EdgeFrame(pbMesh, east, baseFace, out var nE, out var tE, out var outE);

        // var northMidBefore = 0.5f * (P[north.a] + P[north.b]);
        // var eastMidBefore = 0.5f * (P[east.a] + P[east.b]);

        // // Ensure outward pushes +Z for north, +X for east
        // bool flipNorth = Vector3.Dot(outN, Vector3.forward) < 0f;  // want +Z
        // bool flipEast = Vector3.Dot(outE, Vector3.right) < 0f;   // want +X
        // var outNUsed = flipNorth ? -outN : outN;
        // var outEUsed = flipEast ? -outE : outE;

        // // --- North extrusion ---
        // var newEdgesNorth = PBExtrudeUtil.ExtrudeEdgeWithOffsets(
        //     pbMesh, north, baseFace,
        //     rise: heightOffset,
        //     lateral: lengthOffset,
        //     flipOutward: flipNorth,
        //     apply: true
        // );

        // // Find the NORTH top rim edge: parallel to tN and farthest along +outNUsed
        // P = pbMesh.positions; // refresh
        // var northTop = newEdgesNorth
        //     .Where(e =>
        //     {
        //         var te = (P[e.b] - P[e.a]).normalized;
        //         return Mathf.Abs(Vector3.Dot(te, tN)) > 0.99f;
        //     })
        //     .Select(e => new
        //     {
        //         e,
        //         score = Vector3.Dot(0.5f * (P[e.a] + P[e.b]) - northMidBefore, outNUsed)
        //     })
        //     .OrderByDescending(x => x.score)
        //     .First().e;

        // // Choose the rim endpoint nearer the NE corner (higher X)
        // int northRimCornerIdx = (P[northTop.a].x >= P[northTop.b].x) ? northTop.a : northTop.b;

        // // --- East extrusion ---
        // var newEdgesEast = PBExtrudeUtil.ExtrudeEdgeWithOffsets(
        //     pbMesh, east, baseFace,
        //     rise: heightOffset,
        //     lateral: lengthOffset,
        //     flipOutward: flipEast,
        //     apply: true
        // );

        // // Find the EAST top rim edge: parallel to tE and farthest along +outEUsed
        // P = pbMesh.positions; // refresh
        // var eastTop = newEdgesEast
        //     .Where(e =>
        //     {
        //         var te = (P[e.b] - P[e.a]).normalized;
        //         return Mathf.Abs(Vector3.Dot(te, tE)) > 0.99f;
        //     })
        //     .Select(e => new
        //     {
        //         e,
        //         score = Vector3.Dot(0.5f * (P[e.a] + P[e.b]) - eastMidBefore, outEUsed)
        //     })
        //     .OrderByDescending(x => x.score)
        //     .First().e;

        // // Choose the rim endpoint nearer the NE corner (higher Z)
        // int eastRimCornerIdx = (P[eastTop.a].z >= P[eastTop.b].z) ? eastTop.a : eastTop.b;

        // // Shared original NE corner index (from initial quad layout it is 2)
        // int cornerIdx = 2;

        // // Build a triangle face: (corner → northRim → eastRim), fix winding to face outward-ish
        // Vector3 outwardAvg = (outNUsed + outEUsed).normalized;
        // int a = cornerIdx, b = northRimCornerIdx, c = eastRimCornerIdx;

        // // Ensure winding aligns with outward direction (optional but nice)
        // var Ntri = Vector3.Cross(P[b] - P[a], P[c] - P[a]).normalized;
        // if (Vector3.Dot(Ntri, outwardAvg) < 0f) { (b, c) = (c, b); }

        // var tri = AppendElements.CreatePolygon(pbMesh, new int[] { a, b, c }, unordered: false);
        // pbMesh.ToMesh();
        // pbMesh.Refresh(RefreshMask.All);
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

// public static class FacingUtil
// {
//     /// Returns world-space basis vectors for a given corner facing.
//     /// right = local +X, forward = local +Z, up = world +Y
//     public static void GetAxes(Facing facing, out Vector3 right, out Vector3 forward, out Vector3 up)
//     {
//         up = Vector3.up;
//         switch (facing)
//         {
//             case Facing.North: right = Vector3.right; forward = Vector3.forward; break;
//             case Facing.East: right = Vector3.back; forward = Vector3.right; break;
//             case Facing.South: right = Vector3.left; forward = Vector3.back; break;
//             default: /* West*/ right = Vector3.forward; forward = Vector3.left; break;
//         }
//     }

//     // Corner-specific utils
//     public static Facing CornerFacingFor(CornerId id) => id switch
//     {
//         CornerId.SW => Facing.North,
//         CornerId.SE => Facing.East,
//         CornerId.NE => Facing.South,
//         CornerId.NW => Facing.West,
//         _ => Facing.North
//     };
// }
