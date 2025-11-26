using UnityEngine;
using System;

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
    //public readonly SiteFrame frame;

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
        //SiteFrame frame, 
        in CornerModel leftCorner, in CornerModel rightCorner,
        Side leftAdjSide, Side rightAdjSide)
    {
        this.side = side; this.exists = exists; this.geometry = geometry;
        this.edgeLength = edgeLength;
        this.edgeMid = edgeLength * 0.5f;
        this.edgeOrigin = edgeOrigin;
        this.edgeRight = edgeRight;
        this.edgeInward = edgeInward;
        // this.frame = frame;
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