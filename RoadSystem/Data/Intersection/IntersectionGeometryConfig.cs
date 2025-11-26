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

