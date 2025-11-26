using UnityEngine;


// ---- Corner Model ----

[System.Serializable]
public struct CornerModel
{
    public CornerGeometry geometry;

    public readonly CornerId id;
    public readonly CornerType type;
    public readonly bool exists;
    public readonly Vector3 origin;
    public readonly Vector3 apex;
    public readonly Side adjA;
    public readonly Side adjB;

        public CornerModel(
        CornerId id, bool exists, CornerType type,
        Vector3 origin, Vector3 apex, Side a, Side b, CornerGeometry geometry)
    {
        this.id = id; this.exists = exists; this.type = type;
        this.origin = origin; this.apex = apex; adjA = a; adjB = b; this.geometry = geometry;
    }

    // public bool IsAdjacentTo(Side s) => s == adjA || s == adjB;
    // public Side OppA => Topology.Opposite(adjA);
    // public Side OppB => Topology.Opposite(adjB);
    // public bool CanMeetFootpathOn(Side s) => s == OppA || s == OppB;
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