using UnityEngine;

[CreateAssetMenu(fileName = "RoadSystemConfig", menuName = "Scriptable Objects/RoadSystemConfig")]
public sealed class RoadSystemConfig : ScriptableObject
{
    [Header("Shared defaults")]
    public Material defaultMaterial;
    public float roadHeight = 0f;

    [Header("Intersection defaults")]
    public Vector2 defaultIntersectionSize = new(30f, 30f); // X = width, Y = length

    [Header("Road defaults")]
    public RoadGeometryConfig defaultRoad = RoadGeometryConfig.Default();

    public CurbGutter curb = CurbGutter.Default();

    [Header("Default Corner Footpath Size (per-corner)")]
    public CornerSizeSet defaultCornerSizes = CornerGeometryConfig.Default().sizes;

    [Header("Default Footpaths (per-side)")]
    public FootpathGeometryConfig defaultFootpaths = FootpathGeometryConfig.Default();
}