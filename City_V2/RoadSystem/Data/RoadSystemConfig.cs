using UnityEngine;

[CreateAssetMenu(fileName = "RoadSystemConfig", menuName = "Scriptable Objects/RoadSystemConfig")]
public sealed class RoadSystemConfig : ScriptableObject
{
    [Header("Defaults applied to all children")]
    public float roadHeight = 0f;
    public CurbGutter curb = CurbGutter.Default();

    [Header("Default Corner Footpath Size (per-corner)")]
    public CornerSizeSet defaultCornerSizes = CornerGeometryConfig.Default().sizes;
}