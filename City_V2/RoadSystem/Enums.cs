

public enum Side { South, East, North, West }
public enum CornerId { SW, SE, NE, NW }

public enum CornerType
{
    InwardFacing,   // both adjacent sides are connected (roads)
    OutwardFacing   // both adjacent sides are NOT connected (footpaths)
}

public enum RoadTopology { Plaza, DeadEnd, I, L, T, X }
