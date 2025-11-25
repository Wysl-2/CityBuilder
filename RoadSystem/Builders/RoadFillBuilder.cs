using System.Collections.Generic;
using UnityEngine;

public static class RoadFillModule
{
    // CCW quad on xz at y
    private static Vector3[] QuadXZ(float x0, float x1, float z0, float z1, float y)
    {
        // Order to get up-facing normals: NE, NW, SW, SE (consistent with your plaza)
        return new[]
        {
            new Vector3(x1, y, z1), // NE
            new Vector3(x0, y, z1), // NW
            new Vector3(x0, y, z0), // SW
            new Vector3(x1, y, z0), // SE
        };
    }

    public static void CreateRoadFill(PBMeshBuilder builder, IntersectionModel m, Transform t)
    {
        // Inner offsets from each boundary using apexes (works for both inward/outward corners)
        float xL = Mathf.Max(m.CornerSW.apex.x, m.CornerNW.apex.x);
        float xR = Mathf.Min(m.CornerSE.apex.x, m.CornerNE.apex.x);
        float zB = Mathf.Max(m.CornerSW.apex.z, m.CornerSE.apex.z);
        float zT = Mathf.Min(m.CornerNW.apex.z, m.CornerNE.apex.z);
        float RH = m.RoadHeight;

        // Guard: degenerate cases (can happen with tiny configs or extreme values)
        if (xL >= xR || zB >= zT)
        {
            Debug.LogWarning($"RoadFill degenerate rectangle: xL={xL} xR={xR} zB={zB} zT={zT}");
            return;
        }

        var faces = new List<Vector3[]>();

        // 1) Center rectangle (Plaza core) — always present
        faces.Add(QuadXZ(xL, xR, zB, zT, RH));

        // 2) Edge bands only where a road connects to the outside world.
        // They extend to the boundary but exclude the corner squares.
        if (m.ConnectedSouth) faces.Add(QuadXZ(xL, xR, 0f, zB, RH));           // South band
        if (m.ConnectedNorth) faces.Add(QuadXZ(xL, xR, zT, m.Size.y, RH));     // North band
        if (m.ConnectedWest)  faces.Add(QuadXZ(0f, xL, zB, zT, RH));           // West band
        if (m.ConnectedEast)  faces.Add(QuadXZ(xR, m.Size.x, zB, zT, RH));     // East band

        var centerOffset = new Vector3(-m.Size.x * 0.5f, 0f, -m.Size.y * 0.5f);
        var centered = VertexOperations.TranslateMany(faces, centerOffset);

        builder.AddFaces(centered);
    }
}
