using System;
using UnityEngine;

[Serializable]
public struct RoadGeometryConfig
{
    [Header("Road dimensions")]
    public float width;
    public float length;

    public static RoadGeometryConfig Default() => new RoadGeometryConfig
    {
        width  = 30f,
        length = 60f
    };
}
