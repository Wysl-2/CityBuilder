using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProcGen
{
    public enum BakeMode { PerTriangleDup, ShareByPlane }
    // public enum Winding  { CW, CCW }

    /// <summary>
    /// Minimal mesh authoring helper:
    /// - Position-only welding while authoring (quantized)
    /// - Tri/Quad adding by indices or positions (now with Winding)
    /// - Planar, meter-true UVs on bake (1 UV == metersPerTile meters)
    /// - Vertex sharing within coplanar patches to reduce vertex count (ShareByPlane)
    /// </summary>
    public class MeshBuilder
    {
        // ===== Authoring buffers (indices reference 'vertices') =====
        public readonly List<Vector3> vertices = new();
        public readonly List<int>     triangles = new();

        // Position-only welding (quantized)
        readonly Dictionary<(long, long, long), int> _weld = new();
        readonly float _quantize;

        public MeshBuilder(float quantize = 1e-5f)
        {
            _quantize = Mathf.Max(quantize, 1e-9f);
        }

        static (long, long, long) Q(Vector3 p, float q)
            => ((long)Mathf.Round(p.x / q), (long)Mathf.Round(p.y / q), (long)Mathf.Round(p.z / q));

        public int GetOrAddVertex(Vector3 position)
        {
            var key = Q(position, _quantize);
            if (_weld.TryGetValue(key, out int idx)) return idx;
            idx = vertices.Count;
            vertices.Add(position);
            _weld.Add(key, idx);
            return idx;
        }

        // ===== Topology (with winding control) =====
        public void AddTri(int i0, int i1, int i2)
        {
            triangles.Add(i0); triangles.Add(i1); triangles.Add(i2);
        }

        public void AddTri(int i0, int i1, int i2, Winding winding)
        {
            if (winding == Winding.CW) AddTri(i0, i1, i2);
            else                       AddTri(i0, i2, i1);
        }

        /// <summary> Adds a quad using CW by default (Unity front-face). </summary>
        public void AddQuad(int v0, int v1, int v2, int v3)
        {
            AddQuad(v0, v1, v2, v3, Winding.CW);
        }

        /// <summary> Adds a quad with explicit winding. </summary>
        public void AddQuad(int v0, int v1, int v2, int v3, Winding winding)
        {
            if (winding == Winding.CW)
            {
                // Tri 1: v0, v2, v1
                // Tri 2: v2, v3, v1
                AddTri(v0, v2, v1);
                AddTri(v2, v3, v1);
            }
            else
            {
                // Flipped winding
                // Tri 1: v0, v1, v2
                // Tri 2: v2, v1, v3
                AddTri(v0, v1, v2);
                AddTri(v2, v1, v3);
            }
        }

        // Convenience: add by positions (welding applied automatically)
        public void AddTriByPos(Vector3 a, Vector3 b, Vector3 c)
        {
            AddTri(GetOrAddVertex(a), GetOrAddVertex(b), GetOrAddVertex(c));
        }

        public void AddTriByPos(Vector3 a, Vector3 b, Vector3 c, Winding winding)
        {
            AddTri(GetOrAddVertex(a), GetOrAddVertex(b), GetOrAddVertex(c), winding);
        }

        public void AddQuadByPos(Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3)
        {
            AddQuad(GetOrAddVertex(v0), GetOrAddVertex(v1), GetOrAddVertex(v2), GetOrAddVertex(v3), Winding.CW);
        }

        public void AddQuadByPos(Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3, Winding winding)
        {
            AddQuad(GetOrAddVertex(v0), GetOrAddVertex(v1), GetOrAddVertex(v2), GetOrAddVertex(v3), winding);
        }

        public void AddQuadByPosRobust(Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3, Vector3 outwardHint)
        {
            const float EPS = 1e-10f;

            float Area2(Vector3 a, Vector3 b, Vector3 c)
                => Vector3.Cross(b - a, c - a).sqrMagnitude;

            // Choose the healthier diagonal
            float q02 = Mathf.Min(Area2(v0, v1, v2), Area2(v0, v2, v3)); // split by 0–2
            float q13 = Mathf.Min(Area2(v1, v2, v3), Area2(v1, v3, v0)); // split by 1–3
            bool use02 = (q02 >= q13) && (q02 > EPS);

            // Per-triangle facing enforcement
            void AddTriFacingLocal(Vector3 a, Vector3 b, Vector3 c)
            {
                Vector3 n = Vector3.Cross(b - a, c - a);
                if (Vector3.Dot(n, outwardHint) >= 0f)
                    AddTriByPos(a, b, c);
                else
                    AddTriByPos(a, c, b); // flip
            }

            if (use02)
            {
                // Split along v0–v2
                AddTriFacingLocal(v0, v1, v2);
                AddTriFacingLocal(v0, v2, v3);
            }
            else
            {
                // Split along v1–v3
                AddTriFacingLocal(v1, v2, v3);
                AddTriFacingLocal(v1, v3, v0);
            }
        }

        /// <summary>
        /// Picks winding so the face normal points roughly toward outwardHint.
        /// </summary>
        public void AddQuadByPosFacing(Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3, Vector3 outwardHint)
        {
            // Normal if we were to use CW (consistent with our AddQuad(CW) convention)
            Vector3 nCW = Vector3.Cross(v2 - v0, v1 - v0);
            var winding = Vector3.Dot(nCW, outwardHint) >= 0f ? Winding.CW : Winding.CCW;
            AddQuadByPos(v0, v1, v2, v3, winding);
        }

        // ===== Bake to Mesh with planar, meter-true UVs =====
        /// <param name="metersPerTile">1 UV unit corresponds to this many meters on the surface.</param>
        /// <param name="mode">PerTriangleDup (original) or ShareByPlane (reuses baked verts within a plane).</param>
        /// <param name="normalQuantize">Quantization step for plane normal components.</param>
        /// <param name="distQuantize">Quantization step for plane distance (meters).</param>
        /// <param name="minFaceArea">Degeneracy threshold (squared area) to skip tiny triangles.</param>
        /// <param name="snapPlaneOriginToWholeMeters">
        /// If true, snaps the chosen plane origin to whole meters for global texture alignment across disconnected patches.
        /// </param>
        public Mesh ToMesh(
            float metersPerTile = 1f,
            BakeMode mode = BakeMode.ShareByPlane,
            float normalQuantize = 1e-4f,
            float distQuantize   = 1e-3f,
            float minFaceArea    = 1e-12f,
            bool  snapPlaneOriginToWholeMeters = false)
        {
            // Output buffers; reserve roughly triangle count for indices
            var outVerts = new List<Vector3>(triangles.Count);
            var outUVs   = new List<Vector2>(triangles.Count);
            var outTris  = new List<int>(triangles.Count);

            // ---- Plane grouping (quantized) ----
            var planeFrames = new Dictionary<(int, int, int, int), (Vector3 originOnPlane, Vector3 U, Vector3 V)>(128);

            // For vertex sharing within a plane (ShareByPlane only):
            Dictionary<(int, int, int, int, int), int> bakedIndex = null;
            if (mode == BakeMode.ShareByPlane)
                bakedIndex = new Dictionary<(int, int, int, int, int), int>(triangles.Count);

            // ----- Local helpers -----
            (int nx, int ny, int nz, int d) PlaneKey(Vector3 n, Vector3 p)
            {
                n.Normalize();

                // Canonicalize normal to avoid (+n,+d) vs (-n,-d)
                if (n.y < 0f || (Mathf.Approximately(n.y, 0f) && (n.z < 0f || (Mathf.Approximately(n.z, 0f) && n.x < 0f))))
                    n = -n;

                float dist = Vector3.Dot(n, p);
                int qnx = Mathf.RoundToInt(n.x / normalQuantize);
                int qny = Mathf.RoundToInt(n.y / normalQuantize);
                int qnz = Mathf.RoundToInt(n.z / normalQuantize);
                int qd  = Mathf.RoundToInt(dist / distQuantize);
                return (qnx, qny, qnz, qd);
            }

            void BuildPlaneFrame(Vector3 n, out Vector3 U, out Vector3 V)
            {
                n.Normalize();

                // Use the world axis least aligned with n to seed U for a stable frame
                float rx = Mathf.Abs(Vector3.Dot(n, Vector3.right));
                float ry = Mathf.Abs(Vector3.Dot(n, Vector3.up));
                float rz = Mathf.Abs(Vector3.Dot(n, Vector3.forward));
                Vector3 a = (rx < ry && rx < rz) ? Vector3.right :
                            (rz < ry)            ? Vector3.forward :
                                                   Vector3.up;

                U = (a - Vector3.Dot(a, n) * n).normalized;
                V = Vector3.Cross(n, U).normalized; // right-handed
            }

            Vector2 ProjectUV(Vector3 worldPos, in (Vector3 origin, Vector3 U, Vector3 V) frame)
            {
                return UVMapping.Planar(worldPos - frame.origin, frame.U, frame.V, metersPerTile, metersPerTile);
            }

            // ----- Main triangle loop -----
            for (int t = 0; t < triangles.Count; t += 3)
            {
                int i0 = triangles[t + 0], i1 = triangles[t + 1], i2 = triangles[t + 2];
                Vector3 p0 = vertices[i0],   p1 = vertices[i1],   p2 = vertices[i2];

                // Face normal & degeneracy check
                Vector3 n = Vector3.Cross(p1 - p0, p2 - p0);
                if (n.sqrMagnitude < minFaceArea) continue;
                n.Normalize();

                var key = PlaneKey(n, p0);

                // Get or create shared frame for this plane
                if (!planeFrames.TryGetValue(key, out var frame))
                {
                    BuildPlaneFrame(n, out var U, out var V);

                    float dist = Vector3.Dot(n, p0);
                    Vector3 originOnPlane = n * dist;

                    if (snapPlaneOriginToWholeMeters)
                    {
                        originOnPlane = new Vector3(
                            Mathf.Round(originOnPlane.x),
                            Mathf.Round(originOnPlane.y),
                            Mathf.Round(originOnPlane.z));
                    }

                    frame = (originOnPlane, U, V);
                    planeFrames.Add(key, frame);
                }

                // Emit indices for the triangle, reusing baked verts within the plane if enabled
                int EmitCorner(int iSrc, Vector3 p)
                {
                    if (mode == BakeMode.PerTriangleDup)
                    {
                        int idx = outVerts.Count;
                        outVerts.Add(p);
                        outUVs.Add(ProjectUV(p, frame));
                        return idx;
                    }
                    else // ShareByPlane
                    {
                        var k = (key.Item1, key.Item2, key.Item3, key.Item4, iSrc);
                        if (bakedIndex!.TryGetValue(k, out int idx)) return idx;

                        idx = outVerts.Count;
                        outVerts.Add(p);
                        outUVs.Add(ProjectUV(p, frame));
                        bakedIndex.Add(k, idx);
                        return idx;
                    }
                }

                int j0 = EmitCorner(i0, p0);
                int j1 = EmitCorner(i1, p1);
                int j2 = EmitCorner(i2, p2);

                outTris.Add(j0); outTris.Add(j1); outTris.Add(j2);
            }

            // ----- Build Mesh -----
            var mesh = new Mesh
            {
                indexFormat = (outVerts.Count > 65534
                    ? UnityEngine.Rendering.IndexFormat.UInt32
                    : UnityEngine.Rendering.IndexFormat.UInt16)
            };
            mesh.SetVertices(outVerts);
            mesh.SetUVs(0, outUVs);
            mesh.SetTriangles(outTris, 0, true);
            mesh.RecalculateNormals();
            // mesh.RecalculateTangents(); // if you use normal maps
            mesh.RecalculateBounds();
            return mesh;
        }

        public void Clear()
        {
            vertices.Clear();
            triangles.Clear();
            _weld.Clear();
        }
    }

    // ===== Convenience value types =====
    public readonly struct Quad
    {
        public readonly Vector3 v0, v1, v2, v3;
        public Quad(Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3)
        { this.v0 = v0; this.v1 = v1; this.v2 = v2; this.v3 = v3; }

        public Quad WithWindingCW()  => new Quad(v0, v1, v2, v3);
        public Quad WithWindingCCW() => new Quad(v0, v2, v1, v3);

        /// <summary>Flip winding so the quad's CW normal points toward 'outwardHint'.</summary>
        public Quad Facing(Vector3 outwardHint)
        {
            Vector3 nCW = Vector3.Cross(v2 - v0, v1 - v0);
            return Vector3.Dot(nCW, outwardHint) >= 0f ? WithWindingCW() : WithWindingCCW();
        }

        public Quad Transformed(Matrix4x4 m) =>
            new Quad(m.MultiplyPoint3x4(v0), m.MultiplyPoint3x4(v1),
                     m.MultiplyPoint3x4(v2), m.MultiplyPoint3x4(v3));

        public (Vector3 a, Vector3 b) Edge01 => (v0, v1);
        public (Vector3 a, Vector3 b) Edge23 => (v2, v3);
        public (Vector3 a, Vector3 b) Edge02 => (v0, v2);
        public (Vector3 a, Vector3 b) Edge13 => (v1, v3);

        public static Quad FromXZRect(Vector3 origin, float widthX, float depthZ, float y = 0f)
        {
            var v0 = origin + new Vector3(0, y, 0);
            var v1 = origin + new Vector3(widthX, y, 0);
            var v2 = origin + new Vector3(0, y, depthZ);
            var v3 = origin + new Vector3(widthX, y, depthZ);
            return new Quad(v0, v1, v2, v3);
        }

        public static Quad FromEdgeExtrude(Vector3 a, Vector3 b, Vector3 offset)
        {
            // quad across the edge a–b extruded by 'offset'
            return new Quad(a, b, a + offset, b + offset);
        }

        public static Quad FromEdgeExtrudeFacing(Vector3 a, Vector3 b, Vector3 offset, Vector3 outwardHint)
        {
            return FromEdgeExtrude(a, b, offset).Facing(outwardHint);
        }
    }

    public readonly struct Tri
    {
        public readonly Vector3 v0, v1, v2;
        public Tri(Vector3 v0, Vector3 v1, Vector3 v2) { this.v0 = v0; this.v1 = v1; this.v2 = v2; }

        public Tri Transformed(Matrix4x4 m) =>
            new Tri(m.MultiplyPoint3x4(v0), m.MultiplyPoint3x4(v1), m.MultiplyPoint3x4(v2));
    }

    public readonly struct Edge
    {
        public readonly Vector3 a, b;
        public Edge(Vector3 a, Vector3 b) { this.a = a; this.b = b; }

        public Quad Extrude(Vector3 offset) => Quad.FromEdgeExtrude(a, b, offset);
        public Vector3 Direction => (b - a);
        public float Length => Direction.magnitude;
    }

    // ===== Helper extensions =====
    public static class MeshBuilderExtensions
    {
        public static void Add(this MeshBuilder b, in Quad q)
            => b.AddQuadByPos(q.v0, q.v1, q.v2, q.v3);

        public static void Add(this MeshBuilder b, in Tri t)
            => b.AddTriByPos(t.v0, t.v1, t.v2);

        /// <summary>Add a quad choosing winding so it faces 'outwardHint'.</summary>
        public static void Add(this MeshBuilder b, in Quad q, Vector3 outwardHint)
        {
            var qf = q.Facing(outwardHint);
            b.AddQuadByPos(qf.v0, qf.v1, qf.v2, qf.v3);
        }

        public static void AddRange(this MeshBuilder b, ReadOnlySpan<Quad> quads)
        {
            for (int i = 0; i < quads.Length; i++) b.Add(quads[i]);
        }
    }
}
