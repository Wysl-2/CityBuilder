using UnityEngine;

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

public readonly struct RoadOrientation
{
    public readonly float yawDeg;
    public readonly bool mirror; // kept for future-proofing; not used now

    public RoadOrientation(float yawDeg, bool mirror = false)
    {
        // normalize to [-180, 180) for sanity
        yawDeg = Mathf.Repeat(yawDeg + 180f, 360f) - 180f;
        this.yawDeg = yawDeg;
        this.mirror = mirror;
    }
}

// public static class RoadOrientationUtil
// {
//     // Map actual connections -> yaw that rotates CANONICAL to ACTUAL.
//     public static RoadOrientation OrientationFor(bool N, bool E, bool S, bool W)
//     {
//         int cnt = (N ? 1 : 0) + (E ? 1 : 0) + (S ? 1 : 0) + (W ? 1 : 0);

//         switch (cnt)
//         {
//             case 0: // Plaza
//                 return new RoadOrientation(0f);

//             case 1: // DeadEnd: canonical = S connected
//                 if (S) return new RoadOrientation(0f);     // canonical
//                 if (E) return new RoadOrientation(-90f);
//                 if (N) return new RoadOrientation(-180f);
//                 /*W*/ return new RoadOrientation(-270f);

//             case 2:
//                 // Either "I" (opposites) or "L" (adjacent)
//                 if ((N && S) && !(E || W))
//                     return new RoadOrientation(0f);        // I canonical: N+S
//                 if ((E && W) && !(N || S))
//                     return new RoadOrientation(-90f);      // rotate vertical to horizontal

//                 // L-shape canonical = S+E
//                 if (S && E) return new RoadOrientation(0f);      // canonical
//                 if (E && N) return new RoadOrientation(-90f);
//                 if (N && W) return new RoadOrientation(-180f);
//                 /*W && S*/ return new RoadOrientation(-270f);

//             case 3: // T-shape canonical = S+E+W (missing N)
//                 if (!N) return new RoadOrientation(0f);          // canonical
//                 if (!W) return new RoadOrientation(-90f);         // missing W => rotate 90 cw
//                 if (!S) return new RoadOrientation(-180f);        // missing S
//                 /* !E */ return new RoadOrientation(-270f);       // missing E

//             case 4: // X
//                 return new RoadOrientation(0f);

//             default:
//                 return new RoadOrientation(0f);
//         }
//     }
// }
