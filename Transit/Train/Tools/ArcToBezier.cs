// using System;
// using System.Collections.Generic;
// using UnityEngine;

// public static class ArcToBezier
// {
//     /// <summary>
//     /// A single cubic Bézier segment approximating part of the arc.
//     /// p0--p1--p2--p3 are the cubic control points (2D).
//     /// </summary>
//     public struct Cubic
//     {
//         public Vector2 p0, p1, p2, p3;
//         public Cubic(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3)
//         { this.p0 = p0; this.p1 = p1; this.p2 = p2; this.p3 = p3; }
//     }

//     /// <summary>
//     /// Approximate a circular arc with one or more cubic Bézier segments.
//     /// The arc is the circle through 'start' and 'end' with given 'center'.
//     /// Direction is counterclockwise by default; set clockwise=true for CW.
//     /// 'maxDegrees' controls segmentation (90° is a good default).
//     ///
//     /// Notes:
//     /// - Provide points in the plane you care about (e.g., Unity XZ → use (x,z)).
//     /// - If 'start' and 'end' are not exactly the same radius, 'end' is projected
//     ///   onto the circle defined by 'start' to avoid tiny numeric drift.
//     /// </summary>
//     public static List<Cubic> Approximate(
//         Vector2 center,
//         Vector2 start,
//         Vector2 end,
//         bool clockwise = false,
//         float maxDegrees = 90f)
//     {
//         var result = new List<Cubic>();

//         // Guard: degenerate radius or identical points
//         float r0 = (start - center).magnitude;
//         float r1 = (end   - center).magnitude;
//         if (r0 <= Mathf.Epsilon || r1 <= Mathf.Epsilon)
//             return result;

//         // Project 'end' to match radius of 'start' (prevents small mismatches)
//         float r = r0;
//         if (!Mathf.Approximately(r0, r1))
//             end = center + (end - center).normalized * r;

//         // Angles at center
//         float a0 = Mathf.Atan2(start.y - center.y, start.x - center.x);
//         float a1 = Mathf.Atan2(end.y   - center.y, end.x   - center.x);

//         // Sweep in requested direction
//         float sweep = SweepAngle(a0, a1, clockwise); // signed, |sweep| in (0, 2π]

//         // Segmenting
//         float maxRad = Mathf.Deg2Rad * Mathf.Clamp(Mathf.Abs(maxDegrees), 1f, 179f);
//         int segments = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(sweep) / maxRad));
//         float segSweep = sweep / segments; // signed per-segment sweep

//         for (int i = 0; i < segments; i++)
//         {
//             float t0 = a0 + i * segSweep;
//             float t1 = t0 + segSweep;

//             // Endpoints on the circle
//             Vector2 P0 = center + new Vector2(Mathf.Cos(t0), Mathf.Sin(t0)) * r;
//             Vector2 P3 = center + new Vector2(Mathf.Cos(t1), Mathf.Sin(t1)) * r;

//             // Unit tangents at endpoints (CCW tangents by definition)
//             // For CW segments, (t1 - t0) is negative, which naturally flips k.
//             Vector2 T0 = new Vector2(-Mathf.Sin(t0), Mathf.Cos(t0));
//             Vector2 T1 = new Vector2(-Mathf.Sin(t1), Mathf.Cos(t1));

//             // Cubic Bézier arc factor: k = 4/3 * tan(Δθ/4)
//             float k = (4f / 3f) * Mathf.Tan((t1 - t0) / 4f);

//             Vector2 P1 = P0 + (T0 * (k * r));
//             Vector2 P2 = P3 - (T1 * (k * r));

//             result.Add(new Cubic(P0, P1, P2, P3));
//         }

//         return result;
//     }

//     /// <summary>
//     /// Signed sweep from a0 to a1 following the chosen direction.
//     /// CCW  → sweep in (0, +2π]; CW → sweep in [−2π, 0).
//     /// If a0 == a1, returns 2π (CCW) or −2π (CW): full circle.
//     /// </summary>
//     private static float SweepAngle(float a0, float a1, bool clockwise)
//     {
//         const float TwoPi = 2f * Mathf.PI;

//         // Normalize to [0, 2π)
//         a0 %= TwoPi; if (a0 < 0f) a0 += TwoPi;
//         a1 %= TwoPi; if (a1 < 0f) a1 += TwoPi;

//         float ccw = a1 - a0;
//         if (ccw < 0f) ccw += TwoPi; // now in [0, 2π)

//         if (!clockwise)
//         {
//             // CCW. If zero angle, interpret as full circle.
//             return (ccw <= Mathf.Epsilon) ? TwoPi : ccw;
//         }
//         else
//         {
//             // CW is the negative complement. If zero, interpret as -2π (full circle).
//             float cw = ccw - TwoPi; // in (−2π, 0]
//             return (Mathf.Abs(ccw) <= Mathf.Epsilon) ? -TwoPi : cw;
//         }
//     }
// }
