using System.Collections.Generic;
using UnityEngine;
using System.Runtime.CompilerServices;

namespace ProcGen
{
    /// <summary>
    /// Utilities for mapping distances in METERS to UVs.
    /// Keep your material Tiling at (1,1) and WrapMode = Repeat for 1m-per-UV by default.
    /// </summary>
    public static class UVMapping
    {
        const float EPS = 1e-12f;

        // --------------------------
        // Axis/origin selection
        // --------------------------
        public enum QuadAxisMode
        {
            Edge01,        // U follows v0->v1
            LongestEdge,   // U follows the longest edge
            PCA,           // U follows the quad's principal in-plane axis (stable on skewed quads)
            WorldX,        // U from world X projected onto plane
            WorldY,        // U from world Y projected onto plane
            WorldZ         // U from world Z projected onto plane
        }

        public enum QuadOrigin
        {
            V0,        // origin at v0
            Centroid   // origin at average of v0..v3
        }

        /// <summary>
        /// How to lock the plane frame to world axes when building a per-plane UV basis.
        /// </summary>
        public enum PlaneAxisMode
        {
            LeastAlignedWorld, // (default) U from world axis least aligned with n
            WorldX,            // U along projected world +X
            WorldY,            // U along projected world +Y
            WorldZ             // U along projected world +Z
        }

        // --------------------------
        // Basic conversions
        // --------------------------
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 FromMeters(
            float uMeters, float vMeters,
            float metersPerTileU = 1f, float metersPerTileV = 1f,
            float uOffset = 0f, float vOffset = 0f)
        {
            float u = uMeters / Mathf.Max(1e-6f, metersPerTileU) + uOffset;
            float v = vMeters / Mathf.Max(1e-6f, metersPerTileV) + vOffset;
            return new Vector2(u, v);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ToU(float meters, float metersPerTileU = 1f, float uOffset = 0f)
        {
            return meters / Mathf.Max(1e-6f, metersPerTileU) + uOffset;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ToV(float meters, float metersPerTileV = 1f, float vOffset = 0f)
        {
            return meters / Mathf.Max(1e-6f, metersPerTileV) + vOffset;
        }

        // --------------------------
        // Planar mapping (axis pair)
        // --------------------------
        /// <summary>
        /// Planar UVs from a local position projected onto (uAxis, vAxis).
        /// Axes are orthonormalized on their plane and coerced to a right-handed frame.
        /// Uses origin at (0,0,0).
        /// </summary>
        public static Vector2 Planar(
            Vector3 localPos,
            Vector3 uAxis, Vector3 vAxis,
            float metersPerTileU = 1f, float metersPerTileV = 1f,
            float uOffset = 0f, float vOffset = 0f)
        {
            Vector3 n = Vector3.Cross(uAxis, vAxis);
            if (n.sqrMagnitude < EPS)
            {
                n = Vector3.up;
                if (Vector3.Cross(uAxis, n).sqrMagnitude < EPS) n = Vector3.right;
            }
            n.Normalize();

            OrthonormalizeOnPlane(ref uAxis, ref vAxis, n);

            float uMeters = Vector3.Dot(localPos, uAxis);
            float vMeters = Vector3.Dot(localPos, vAxis);
            return FromMeters(uMeters, vMeters, metersPerTileU, metersPerTileV, uOffset, vOffset);
        }

        /// <summary>
        /// Planar UVs from a position relative to an origin, projected onto (uAxis, vAxis).
        /// Axes are orthonormalized on the plane and made right-handed.
        /// </summary>
        public static Vector2 Planar(
            Vector3 pos, Vector3 origin,
            Vector3 uAxis, Vector3 vAxis,
            float metersPerTileU = 1f, float metersPerTileV = 1f,
            float uOffset = 0f, float vOffset = 0f)
        {
            Vector3 d = pos - origin;

            Vector3 n = Vector3.Cross(uAxis, vAxis);
            if (n.sqrMagnitude < EPS)
            {
                n = Vector3.up;
                if (Vector3.Cross(uAxis, n).sqrMagnitude < EPS) n = Vector3.right;
            }
            n.Normalize();

            OrthonormalizeOnPlane(ref uAxis, ref vAxis, n);

            float uMeters = Vector3.Dot(d, uAxis);
            float vMeters = Vector3.Dot(d, vAxis);
            return FromMeters(uMeters, vMeters, metersPerTileU, metersPerTileV, uOffset, vOffset);
        }

        // --------------------------
        // NEW: Per-plane frame builder with world-axis locking
        // --------------------------
        /// <summary>
        /// Build an orthonormal, right-handed (U,V) basis for a plane with normal n.
        /// By default it tries to align **V to world +Z** if possible (great for faces extruded along Z),
        /// otherwise it locks **U** based on <paramref name="axisMode"/>. You can also snap in-plane rotation.
        /// </summary>
        /// <param name="n">Unit (or any nonzero) plane normal.</param>
        /// <param name="U">Resulting in-plane unit U axis.</param>
        /// <param name="V">Resulting in-plane unit V axis (right-handed w.r.t n).</param>
        /// <param name="axisMode">How to lock the frame to world axes when V can't be +Z.</param>
        /// <param name="rotationSnapDeg">Optional snap (e.g., 90) of the frame around n; 0 = none.</param>
        /// <param name="preferVWorldZ">If true (default), try to make V = projection(+Z) first.</param>
        public static void BuildPlaneFrame(
            Vector3 n,
            out Vector3 U, out Vector3 V,
            PlaneAxisMode axisMode = PlaneAxisMode.LeastAlignedWorld,
            float rotationSnapDeg = 0f,
            bool preferVWorldZ = true)
        {
            if (n.sqrMagnitude < EPS) n = Vector3.up;
            n.Normalize();

            // 1) Try to align V to world +Z if requested and possible.
            //    This makes vertical/side faces (created by Z extrusions) get V ~ +Z.
            if (preferVWorldZ)
            {
                Vector3 vCand = ProjectOntoPlane(Vector3.forward, n);
                if (vCand.sqrMagnitude > 1e-10f)
                {
                    V = vCand.normalized;
                    U = Vector3.Cross(V, n).normalized; // right-handed (U x V) ~ n
                    SnapInPlane(ref U, ref V, n, rotationSnapDeg);
                    return;
                }
            }

            // 2) Otherwise, pick U from a world axis according to the mode.
            Vector3 a;
            switch (axisMode)
            {
                case PlaneAxisMode.WorldX: a = Vector3.right;  break;
                case PlaneAxisMode.WorldY: a = Vector3.up;     break;
                case PlaneAxisMode.WorldZ: a = Vector3.forward;break;
                default:
                    // Axis least aligned with n
                    float ax = Mathf.Abs(Vector3.Dot(n, Vector3.right));
                    float ay = Mathf.Abs(Vector3.Dot(n, Vector3.up));
                    float az = Mathf.Abs(Vector3.Dot(n, Vector3.forward));
                    a = (ax <= ay && ax <= az) ? Vector3.right :
                        (ay <= ax && ay <= az) ? Vector3.up : Vector3.forward;
                    break;
            }

            U = ProjectOntoPlane(a, n);
            if (U.sqrMagnitude < 1e-10f)
            {
                // If chosen axis is nearly parallel to n, fallback to a different one
                a = (a == Vector3.right) ? Vector3.up : Vector3.right;
                U = ProjectOntoPlane(a, n);
            }
            if (U.sqrMagnitude < EPS) U = Vector3.Cross(n, Vector3.up);
            if (U.sqrMagnitude < EPS) U = Vector3.Cross(n, Vector3.right);
            U.Normalize();

            V = Vector3.Cross(n, U).normalized; // right-handed

            // Optional rotation snap around n (keeps U,V in plane)
            SnapInPlane(ref U, ref V, n, rotationSnapDeg);
        }

        // --------------------------
        // Quad-aligned mapping
        // --------------------------

        /// <summary>
        /// Compute UVs for a quad using a stable, right-handed frame aligned to the quad's plane.
        /// Select U-axis via QuadAxisMode; optionally follow v0->v1 winding for consistent direction.
        /// </summary>
        public static void QuadPlanarUVs(
            Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3,
            out Vector2 uv0, out Vector2 uv1, out Vector2 uv2, out Vector2 uv3,
            float metersPerTileU = 1f, float metersPerTileV = 1f,
            float uOffset = 0f, float vOffset = 0f,
            QuadAxisMode axisMode = QuadAxisMode.PCA,
            QuadOrigin originMode = QuadOrigin.V0,
            bool followWinding = true)
        {
            BuildPlanarFrameFromQuad(
                v0, v1, v2, v3,
                out var origin, out var u, out var v,
                axisMode, originMode, followWinding);

            uv0 = Planar(v0, origin, u, v, metersPerTileU, metersPerTileV, uOffset, vOffset);
            uv1 = Planar(v1, origin, u, v, metersPerTileU, metersPerTileV, uOffset, vOffset);
            uv2 = Planar(v2, origin, u, v, metersPerTileU, metersPerTileV, uOffset, vOffset);
            uv3 = Planar(v3, origin, u, v, metersPerTileU, metersPerTileV, uOffset, vOffset);
        }

        // Version with rotation in degrees
        public static void QuadPlanarUVs(
            Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3,
            out Vector2 uv0, out Vector2 uv1, out Vector2 uv2, out Vector2 uv3,
            float metersPerTileU = 1f, float metersPerTileV = 1f,
            float uOffset = 0f, float vOffset = 0f,
            QuadAxisMode axisMode = QuadAxisMode.PCA,
            QuadOrigin originMode = QuadOrigin.V0,
            bool followWinding = true,
            float rotationDegrees = 0f)
        {
            BuildPlanarFrameFromQuad(
                v0, v1, v2, v3,
                out var origin, out var U, out var V,
                axisMode, originMode, followWinding);

            // rotate the mapping frame inside the plane
            RotateAxesInPlane(ref U, ref V, rotationDegrees);

            uv0 = Planar(v0, origin, U, V, metersPerTileU, metersPerTileV, uOffset, vOffset);
            uv1 = Planar(v1, origin, U, V, metersPerTileU, metersPerTileV, uOffset, vOffset);
            uv2 = Planar(v2, origin, U, V, metersPerTileU, metersPerTileV, uOffset, vOffset);
            uv3 = Planar(v3, origin, U, V, metersPerTileU, metersPerTileV, uOffset, vOffset);
        }

        /// <summary>
        /// Build an orthonormal, right-handed frame (origin, U, V) on the quad.
        /// U is picked by axisMode; V is derived so that cross(U,V) aligns with the face normal.
        /// </summary>
        public static void BuildPlanarFrameFromQuad(
            Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3,
            out Vector3 origin, out Vector3 U, out Vector3 V,
            QuadAxisMode axisMode = QuadAxisMode.PCA,
            QuadOrigin originMode = QuadOrigin.V0,
            bool followWinding = true)
        {
            Vector3 n = FaceNormalFromQuad(v0, v1, v2, v3);

            // Choose origin
            origin = (originMode == QuadOrigin.Centroid)
                   ? 0.25f * (v0 + v1 + v2 + v3)
                   : v0;

            // Choose U based on the mode
            Vector3 uHint;
            switch (axisMode)
            {
                case QuadAxisMode.Edge01:
                    uHint = v1 - v0; break;

                case QuadAxisMode.LongestEdge:
                    {
                        Vector3 e01 = v1 - v0, e12 = v2 - v1, e23 = v3 - v2, e30 = v0 - v3;
                        float l01 = e01.sqrMagnitude, l12 = e12.sqrMagnitude, l23 = e23.sqrMagnitude, l30 = e30.sqrMagnitude;
                        uHint = (l01 >= l12 && l01 >= l23 && l01 >= l30) ? e01 :
                                (l12 >= l23 && l12 >= l30) ? e12 :
                                (l23 >= l30) ? e23 : e30;
                        break;
                    }

                case QuadAxisMode.WorldX: uHint = Vector3.right;   break;
                case QuadAxisMode.WorldY: uHint = Vector3.up;      break;
                case QuadAxisMode.WorldZ: uHint = Vector3.forward; break;

                case QuadAxisMode.PCA:
                default:
                    uHint = PCA_UAxisOnPlane(v0, v1, v2, v3, n);
                    break;
            }

            // Build orthonormal, right-handed frame
            U = uHint;
            V = Vector3.Cross(n, uHint);
            OrthonormalizeOnPlane(ref U, ref V, n);

            // Optional: make U increase along v0->v1 to stabilize direction
            if (followWinding && Vector3.Dot(U, v1 - v0) < 0f)
            {
                U = -U;
                // keep right-handedness
                if (Vector3.Dot(Vector3.Cross(U, V), n) < 0f) V = -V;
            }
        }

        // --------------------------
        // Helpers
        // --------------------------

        static Vector3 ProjectOntoPlane(Vector3 v, Vector3 n)
        {
            return v - Vector3.Dot(v, n) * n;
        }

        /// <summary>
        /// Orthonormalize u and v on the plane with normal n, and enforce a right-handed frame.
        /// </summary>
        static void OrthonormalizeOnPlane(ref Vector3 u, ref Vector3 v, Vector3 n)
        {
            // Project to plane
            u -= Vector3.Dot(u, n) * n;
            v -= Vector3.Dot(v, n) * n;

            // Fallbacks if degenerate
            if (u.sqrMagnitude < EPS)
            {
                u = Vector3.Cross(n, Vector3.up);
                if (u.sqrMagnitude < EPS) u = Vector3.Cross(n, Vector3.right);
            }
            u.Normalize();

            // Gram–Schmidt for v
            v -= Vector3.Dot(v, u) * u;
            if (v.sqrMagnitude < EPS) v = Vector3.Cross(n, u);
            v.Normalize();

            // Ensure right-handedness: cross(u,v) should align with n
            if (Vector3.Dot(Vector3.Cross(u, v), n) < 0f) v = -v;
        }

        /// <summary>
        /// Rotate U,V inside the plane by 'degrees'. Keeps right-handedness.
        /// </summary>
        static void RotateAxesInPlane(ref Vector3 U, ref Vector3 V, float degrees)
        {
            if (Mathf.Abs(degrees) < 1e-6f) return;
            float r = degrees * Mathf.Deg2Rad;
            float c = Mathf.Cos(r), s = Mathf.Sin(r);
            Vector3 U2 =  c * U + s * V;
            Vector3 V2 = -s * U + c * V;
            U = U2; V = V2;
        }

        static void SnapInPlane(ref Vector3 U, ref Vector3 V, Vector3 n, float snapDeg)
        {
            if (snapDeg <= 0f) return;

            // Use projected world X as reference; fallback to projected world Z
            Vector3 refDir = ProjectOntoPlane(Vector3.right, n);
            if (refDir.sqrMagnitude < 1e-10f) refDir = ProjectOntoPlane(Vector3.forward, n);
            if (refDir.sqrMagnitude < 1e-10f) return; // no-op if plane ~ perpendicular

            refDir.Normalize();

            float angle = Mathf.Atan2(Vector3.Dot(V, refDir), Vector3.Dot(U, refDir)) * Mathf.Rad2Deg;
            float snapped = Mathf.Round(angle / snapDeg) * snapDeg;
            float delta = snapped - angle;
            RotateAxesInPlane(ref U, ref V, delta);
        }

        /// <summary>
        /// Stable face normal for a quad (averaged tri normals with fallbacks).
        /// </summary>
        static Vector3 FaceNormalFromQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            Vector3 n = Vector3.Cross(b - a, c - a) + Vector3.Cross(d - c, a - c);
            if (n.sqrMagnitude < EPS) n = Vector3.Cross(b - a, d - a); // diagonal fallback
            if (n.sqrMagnitude < EPS) n = Vector3.up;                  // last resort
            return n.normalized;
        }

        /// <summary>
        /// Compute U as the principal axis (major eigenvector) of the quad projected into its plane.
        /// </summary>
        static Vector3 PCA_UAxisOnPlane(Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3, Vector3 n)
        {
            // Build any orthonormal basis (u0,v0) for the plane
            BuildBasisFromNormal(n, out var u0, out var v0axis);

            // Project points to 2D in that basis (relative to centroid for numerical stability)
            Vector3 c = 0.25f * (v0 + v1 + v2 + v3);
            Vector2 p0 = new Vector2(Vector3.Dot(v0 - c, u0), Vector3.Dot(v0 - c, v0axis));
            Vector2 p1 = new Vector2(Vector3.Dot(v1 - c, u0), Vector3.Dot(v1 - c, v0axis));
            Vector2 p2 = new Vector2(Vector2.Dot(new Vector2(Vector3.Dot(v2 - c, u0), Vector3.Dot(v2 - c, v0axis)), Vector2.right), Vector3.Dot(v2 - c, v0axis)); // keep p2.x via Dot to avoid typos
            // Simpler and consistent:
            p2 = new Vector2(Vector3.Dot(v2 - c, u0), Vector3.Dot(v2 - c, v0axis));
            Vector2 p3 = new Vector2(Vector3.Dot(v3 - c, u0), Vector3.Dot(v3 - c, v0axis));

            // 2x2 covariance of the 4 points
            float meanX = (p0.x + p1.x + p2.x + p3.x) * 0.25f;
            float meanY = (p0.y + p1.y + p2.y + p3.y) * 0.25f;

            float a = 0f, b = 0f, c2 = 0f; // cov = [a b; b c2]
            AccumCov(p0, meanX, meanY, ref a, ref b, ref c2);
            AccumCov(p1, meanX, meanY, ref a, ref b, ref c2);
            AccumCov(p2, meanX, meanY, ref a, ref b, ref c2);
            AccumCov(p3, meanX, meanY, ref a, ref b, ref c2);

            // Normalize by N (not strictly necessary for eigenvector)
            a *= 0.25f; b *= 0.25f; c2 *= 0.25f;

            // Major eigenvector of symmetric 2x2
            Vector2 e = EigenVectorMajor2x2(a, b, c2);

            // Map back to 3D plane: U = e.x * u0 + e.y * v0
            Vector3 U = e.x * u0 + e.y * v0axis;
            if (U.sqrMagnitude < EPS) U = u0; // fallback
            return U;
        }

        static void AccumCov(in Vector2 p, float mx, float my, ref float a, ref float b, ref float c)
        {
            float dx = p.x - mx, dy = p.y - my;
            a += dx * dx;
            b += dx * dy;
            c += dy * dy;
        }

        /// <summary>
        /// Major eigenvector of a symmetric 2x2 matrix [a b; b c]. Returns a unit vector.
        /// </summary>
        static Vector2 EigenVectorMajor2x2(float a, float b, float c)
        {
            // If off-diagonal is ~0, choose axis by larger variance
            if (Mathf.Abs(b) < 1e-12f)
            {
                return (a >= c) ? new Vector2(1f, 0f) : new Vector2(0f, 1f);
            }

            float tau = a + c;
            float delta = Mathf.Sqrt((a - c) * (a - c) + 4f * b * b);
            float lambda = 0.5f * (tau + delta); // major eigenvalue

            // Solve (A - λI) v = 0 => [a-λ b; b c-λ] [x;y] = 0
            Vector2 v = new Vector2(b, lambda - a);
            if (v.sqrMagnitude < EPS)
                v = new Vector2(lambda - c, b); // alternative form

            v.Normalize();
            return v;
        }

        /// <summary>
        /// Build an arbitrary orthonormal basis for a plane with normal n.
        /// </summary>
        static void BuildBasisFromNormal(Vector3 n, out Vector3 u, out Vector3 v)
        {
            if (n.sqrMagnitude < EPS) n = Vector3.up;
            n.Normalize();

            // choose seed least aligned with n for stability
            float rx = Mathf.Abs(Vector3.Dot(n, Vector3.right));
            float ry = Mathf.Abs(Vector3.Dot(n, Vector3.up));
            float rz = Mathf.Abs(Vector3.Dot(n, Vector3.forward));
            Vector3 a = (rx < ry && rx < rz) ? Vector3.right : (rz < ry ? Vector3.forward : Vector3.up);
            u = (a - Vector3.Dot(a, n) * n).normalized;
            v = Vector3.Cross(n, u).normalized;
        }

        // --------------------------
        // Extras
        // --------------------------

        /// <summary>
        /// Given left-to-right x-positions (meters) of a cross-section, compute cumulative lateral distances (meters).
        /// Useful for road-like ribbons where U should grow monotonically across bands.
        /// </summary>
        public static void ComputeLateralDistances(IList<float> xMeters, out float[] cumulative, out float totalWidth)
        {
            int n = xMeters != null ? xMeters.Count : 0;
            cumulative = new float[Mathf.Max(0, n)];
            if (n == 0) { totalWidth = 0f; return; }

            float acc = 0f;
            cumulative[0] = 0f;
            for (int i = 1; i < n; i++)
            {
                acc += Mathf.Abs(xMeters[i] - xMeters[i - 1]);
                cumulative[i] = acc;
            }
            totalWidth = acc;
        }

        /// <summary>
        /// If you’re using a 0..1 normalized U (e.g., an atlas row) and want to inset to avoid bleeding,
        /// remap to [shrink, 1-shrink]. Not usually needed for meter UVs, but handy for band-atlas setups.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ApplyAtlasShrinkU01(float u01, float shrink01)
        {
            return Mathf.Lerp(shrink01, 1f - shrink01, Mathf.Clamp01(u01));
        }

        /// <summary>Convenience: convert tiles-per-meter (legacy) to meters-per-tile.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float MetersPerTileFromTilesPerMeter(float tilesPerMeter)
        {
            return tilesPerMeter > 0f ? (1f / tilesPerMeter) : 1f;
        }
    }
}
