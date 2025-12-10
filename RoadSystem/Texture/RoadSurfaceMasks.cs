using UnityEngine;

public static class RoadSurfaceMasks
{
    private static Color Encode(RoadSurfaceType type)
    {
        // Store ID in red channel as 0..1, keep others free for future use
        float r = (float)(byte)type / 255f;
        return new Color(r, 0f, 0f, 1f);
    }

    public static readonly Color Road        = Encode(RoadSurfaceType.Road);
    public static readonly Color Footpath    = Encode(RoadSurfaceType.Footpath);
    public static readonly Color CurbFace    = Encode(RoadSurfaceType.CurbFace);
    public static readonly Color GutterDrop  = Encode(RoadSurfaceType.GutterDrop);
    public static readonly Color GutterRun   = Encode(RoadSurfaceType.GutterRun);
}

public enum RoadSurfaceType : byte
{
    Road        = 1,
    Footpath    = 2,
    CurbFace    = 3,
    GutterDrop  = 4,
    GutterRun   = 5
}