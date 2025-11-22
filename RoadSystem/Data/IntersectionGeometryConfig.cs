using UnityEngine;

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