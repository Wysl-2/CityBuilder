using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProcGen
{
    public enum Winding { CW, CCW }
    public enum QuadSplit { Diag02, Diag13, Auto }

    /// <summary>
    /// Minimal V2: holds vertices and triangle indices; can bake to a Mesh.
    /// Authoring is positions-first; welding happens in ToMesh().
    /// </summary>
    public class MeshBuilder_V2
    {
        public IMeshAuthoringSink AuthoringSink { get; set; } = null;

        public readonly List<Vector3> vertices = new();
        public readonly List<int> triangles = new();

        static readonly Vector2 UnsetUV = new(float.NaN, float.NaN);
        private readonly List<Vector2> _cornerUVs = new(); // length == triangles.Count

        /// <summary>Add a vertex, returns its index.</summary>
        public int AddVertex(Vector3 p)
        {
            int idx = vertices.Count;
            vertices.Add(p);
            return idx;
        }

        /// <summary>Add a triangle (indices reference the vertices list).</summary>
        public void AddTri(int i0, int i1, int i2)
        {
            int builderTriIndex = triangles.Count / 3;

            triangles.Add(i0); triangles.Add(i1); triangles.Add(i2);
            _cornerUVs.Add(UnsetUV); _cornerUVs.Add(UnsetUV); _cornerUVs.Add(UnsetUV);

            AuthoringSink?.OnTriAdded(builderTriIndex, vertices[i0], vertices[i1], vertices[i2]);
        }

        public void AddTri(int i0, int i1, int i2, Vector2 uv0, Vector2 uv1, Vector2 uv2)
        {
            int builderTriIndex = triangles.Count / 3;


            triangles.Add(i0); triangles.Add(i1); triangles.Add(i2);
            _cornerUVs.Add(uv0); _cornerUVs.Add(uv1); _cornerUVs.Add(uv2);

            AuthoringSink?.OnTriAdded(builderTriIndex, vertices[i0], vertices[i1], vertices[i2]);
        }

        /// <summary>Convenience: add a triangle by positions (no welding at author-time).</summary>
        public void AddTriByPos(Vector3 a, Vector3 b, Vector3 c, Vector2 uv0, Vector2 uv1, Vector2 uv2)
        {
            int i0 = AddVertex(a), i1 = AddVertex(b), i2 = AddVertex(c);
            AddTri(i0, i1, i2, uv0, uv1, uv2);
        }
        // No-UV convenience version
        public void AddTriByPos(Vector3 a, Vector3 b, Vector3 c)
        {
            int i0 = AddVertex(a), i1 = AddVertex(b), i2 = AddVertex(c);
            AddTri(i0, i1, i2); // pushes UnsetUVs
        }

        /// <summary>
        /// Add a quad by indices. Winding and diagonal split are independent.
        /// If split is Auto (indices path lacks positions), falls back to Diag02.
        /// </summary>
        public void AddQuad(int v0, int v1, int v2, int v3,
                    Winding winding = Winding.CW,
                    QuadSplit split = QuadSplit.Diag02)
        {
            if (split == QuadSplit.Auto) split = QuadSplit.Diag02;

            int triStart = triangles.Count / 3;
            var uses = QuadCornerUses(split, winding);
            AuthoringSink?.OnQuadAdded(triStart,
                vertices[v0], vertices[v1], vertices[v2], vertices[v3],
                winding, split, uses[0], uses[1]);

            // …your existing AddTri(...) pairs unchanged…
            if (split == QuadSplit.Diag02) {
                if (winding == Winding.CW) { AddTri(v0, v2, v1); AddTri(v0, v3, v2); }
                else                       { AddTri(v0, v1, v2); AddTri(v0, v2, v3); }
            } else {
                if (winding == Winding.CW) { AddTri(v1, v3, v2); AddTri(v1, v0, v3); }
                else                       { AddTri(v1, v2, v3); AddTri(v1, v3, v0); }
            }
        }

        /// <summary>
        /// Add a quad by positions. Uses four shared vertices per quad (no intra-quad duplication).
        /// Can auto-pick the healthier diagonal using squared area.
        /// </summary>
        public void AddQuadByPos(
            Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3,
            Vector2 uv0, Vector2 uv1, Vector2 uv2, Vector2 uv3,
            Winding winding = Winding.CW, QuadSplit split = QuadSplit.Diag02)
        {
            if (split == QuadSplit.Auto)
            {
                Vector3 a01 = v1 - v0, a02 = v2 - v0, a03 = v3 - v0;
                Vector3 b12 = v2 - v1, b13 = v3 - v1, b10 = v0 - v1;
                float min02 = Mathf.Min(Vector3.Cross(a01, a02).sqrMagnitude,
                                        Vector3.Cross(a02, a03).sqrMagnitude);
                float min13 = Mathf.Min(Vector3.Cross(b12, b13).sqrMagnitude,
                                        Vector3.Cross(b13, b10).sqrMagnitude);
                split = (min02 >= min13) ? QuadSplit.Diag02 : QuadSplit.Diag13;
            }

            int i0 = AddVertex(v0), i1 = AddVertex(v1), i2 = AddVertex(v2), i3 = AddVertex(v3);

            int triStart = triangles.Count / 3;
            var uses = QuadCornerUses(split, winding);
            AuthoringSink?.OnQuadAdded(triStart, v0, v1, v2, v3, winding, split, uses[0], uses[1]);

            if (split == QuadSplit.Diag02)
            {
                if (winding == Winding.CW)
                { AddTri(i0, i2, i1, uv0, uv2, uv1); AddTri(i0, i3, i2, uv0, uv3, uv2); }
                else
                { AddTri(i0, i1, i2, uv0, uv1, uv2); AddTri(i0, i2, i3, uv0, uv2, uv3); }
            }
            else // Diag13
            {
                if (winding == Winding.CW)
                { AddTri(i1, i3, i2, uv1, uv3, uv2); AddTri(i1, i0, i3, uv1, uv0, uv3); }
                else
                { AddTri(i1, i2, i3, uv1, uv2, uv3); AddTri(i1, i3, i0, uv1, uv3, uv0); }
            }
        }
        // No-UV version (with Auto split heuristic)
        public void AddQuadByPos(
            Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3,
            Winding winding = Winding.CW, QuadSplit split = QuadSplit.Diag02)
        {
            if (split == QuadSplit.Auto)
            {
                Vector3 a01 = v1 - v0, a02 = v2 - v0, a03 = v3 - v0;
                Vector3 b12 = v2 - v1, b13 = v3 - v1, b10 = v0 - v1;
                float min02 = Mathf.Min(Vector3.Cross(a01, a02).sqrMagnitude,
                                        Vector3.Cross(a02, a03).sqrMagnitude);
                float min13 = Mathf.Min(Vector3.Cross(b12, b13).sqrMagnitude,
                                        Vector3.Cross(b13, b10).sqrMagnitude);
                split = (min02 >= min13) ? QuadSplit.Diag02 : QuadSplit.Diag13;
            }

            int i0 = AddVertex(v0), i1 = AddVertex(v1), i2 = AddVertex(v2), i3 = AddVertex(v3);

            int triStart = triangles.Count / 3;
            var uses = QuadCornerUses(split, winding);
            AuthoringSink?.OnQuadAdded(triStart, v0, v1, v2, v3, winding, split, uses[0], uses[1]);

            if (split == QuadSplit.Diag02)
            {
                if (winding == Winding.CW)
                { AddTri(i0, i2, i1); AddTri(i0, i3, i2); }
                else
                { AddTri(i0, i1, i2); AddTri(i0, i2, i3); }
            }
            else // Diag13
            {
                if (winding == Winding.CW)
                { AddTri(i1, i3, i2); AddTri(i1, i0, i3); }
                else
                { AddTri(i1, i2, i3); AddTri(i1, i3, i0); }
            }
        }
        // Version uses Quad_V2 struct for convenience
       public void AddQuadByPos(in Quad_V2 q,
          Vector2 uv0, Vector2 uv1, Vector2 uv2, Vector2 uv3,
          Winding winding = Winding.CW, QuadSplit split = QuadSplit.Diag02)
          => AddQuadByPos(q.v0, q.v1, q.v2, q.v3, uv0, uv1, uv2, uv3, winding, split);
        // No-UV version using Quad_V2 (convenience)
        public void AddQuadByPos(in Quad_V2 q,
            Winding winding = Winding.CW, QuadSplit split = QuadSplit.Diag02)
            => AddQuadByPos(q.v0, q.v1, q.v2, q.v3, winding, split);

        // Quad helpers
        static Vector3Int[] QuadCornerUses(QuadSplit split, Winding winding)
        {
            if (split == QuadSplit.Diag02)
                return (winding == Winding.CW)
                    ? new[] { new Vector3Int(0,2,1), new Vector3Int(0,3,2) } // (i0,i2,i1), (i0,i3,i2)
                    : new[] { new Vector3Int(0,1,2), new Vector3Int(0,2,3) }; // (i0,i1,i2), (i0,i2,i3)
            // split == Diag13
            return (winding == Winding.CW)
                ? new[] { new Vector3Int(1,3,2), new Vector3Int(1,0,3) }     // (i1,i3,i2), (i1,i0,i3)
                : new[] { new Vector3Int(1,2,3), new Vector3Int(1,3,0) };     // (i1,i2,i3), (i1,i3,i0)
        }

        // --- Bake ---
        /// <summary>
        /// Build a Unity Mesh with welding by coplanar plane & in-plane UV-quantized positions.
        /// minCrossSqr is |cross|^2 (i.e., (2*area)^2): faces below are skipped.
        /// </summary>
        public Mesh ToMesh(
            float normalQuantize = 1e-4f,   // plane normal quantization
            float distQuantize = 1e-3f,     // plane distance quantization (meters)
            float uvQuantize = 1e-5f,       // in-plane weld tolerance (meters)
            bool weldAcrossOppositeFacing = false, // weld front/back sheet if true
            float minCrossSqr = 0f,         // skip faces with |cross|^2 below this
            float metersPerTileU = 1f,      // 1 UV == this many meters along U
            float metersPerTileV = 1f,      // 1 UV == this many meters along V
            bool snapUVOriginToWholeMeters = true, // align tiles globally per plane

            // --- NEW: control the per-plane UV frame locking ---
            UVMapping.PlaneAxisMode planeAxisMode = UVMapping.PlaneAxisMode.LeastAlignedWorld,
            float planeRotationSnapDegrees = 0f,
            bool preferVWorldZ = true // default: try to align V to world +Z (nice for Z extrusions)
        )
        {
            AuthoringSink?.OnBakeBegin();
            int finalTriCounter = 0;

            if (normalQuantize <= 0f) normalQuantize = 1e-4f;
            if (distQuantize <= 0f) distQuantize = 1e-3f;
            if (uvQuantize <= 0f) uvQuantize = 1e-5f;
            if (metersPerTileU <= 0f) metersPerTileU = 1f;
            if (metersPerTileV <= 0f) metersPerTileV = 1f;

            // When using manual (authored) UVs, we quantize in *UV units* for weld keys
            const float manualUVQuantizeUnits = 1e-5f;

            var outVerts = new List<Vector3>(vertices.Count);
            var outUVs   = new List<Vector2>(vertices.Count);
            var outTris  = new List<int>(triangles.Count);

            // Per-plane frame (keyed by quantized plane): origin,U,V
            var planeFrames =
                new Dictionary<(int nx, int ny, int nz, int d), (Vector3 origin, Vector3 U, Vector3 V)>(128);

            // Weld map:
            // (plane, sideBit, in-plane qU,qV, manualFlag, qU_manual, qV_manual) -> vertex index
            // manualFlag=0 for default/generated UVs; =1 when a manual per-corner UV is present
            var welded =
                new Dictionary<(int nx, int ny, int nz, int d, int side, int qu, int qv, int mflag, int mu, int mv), int>(triangles.Count);

            static int MaxAbsComponent(Vector3 v)
            {
                float ax = Mathf.Abs(v.x), ay = Mathf.Abs(v.y), az = Mathf.Abs(v.z);
                return (ax > ay) ? ((ax > az) ? 0 : 2) : ((ay > az) ? 1 : 2);
            }

            // Ensure _cornerUVs has at least one entry per triangle corner (or UnsetUV)
            if (_cornerUVs.Count < triangles.Count)
            {
                int missing = triangles.Count - _cornerUVs.Count;
                for (int i = 0; i < missing; i++) _cornerUVs.Add(UnsetUV);
            }

            for (int t = 0; t < triangles.Count; t += 3)
            {
                int i0 = triangles[t + 0], i1 = triangles[t + 1], i2 = triangles[t + 2];
                Vector3 p0 = vertices[i0], p1 = vertices[i1], p2 = vertices[i2];

                Vector3 n = Vector3.Cross(p1 - p0, p2 - p0);
                float nSq = n.sqrMagnitude;
                if (nSq <= 0f || (minCrossSqr > 0f && nSq < minCrossSqr)) continue;
                n *= 1.0f / Mathf.Sqrt(nSq);

                // Canonicalize normal (largest component positive) so opposite-facing sheets share a plane key (unless opted out)
                int maxc = MaxAbsComponent(n);
                float comp = (maxc == 0 ? n.x : (maxc == 1 ? n.y : n.z));
                int sideBit = 0;
                if (comp < 0f) { n = -n; sideBit = 1; }
                if (weldAcrossOppositeFacing) sideBit = 0;

                // Quantized plane key
                int qnx = Mathf.RoundToInt(n.x / normalQuantize);
                int qny = Mathf.RoundToInt(n.y / normalQuantize);
                int qnz = Mathf.RoundToInt(n.z / normalQuantize);
                float dist = Vector3.Dot(n, p0);
                int qd = Mathf.RoundToInt(dist / distQuantize);
                var planeKey = (nx: qnx, ny: qny, nz: qnz, d: qd);

                // Get/build frame; anchor to quantized plane distance
                if (!planeFrames.TryGetValue(planeKey, out var frame))
                {
                    // --- NEW: use UVMapping.BuildPlaneFrame to lock to world axes (V prefers +Z) ---
                    UVMapping.BuildPlaneFrame(
                        n,
                        out var U, out var V,
                        planeAxisMode,
                        planeRotationSnapDegrees,
                        preferVWorldZ
                    );

                    Vector3 origin = n * (planeKey.d * distQuantize); // lies on this quantized plane

                    if (snapUVOriginToWholeMeters)
                    {
                        // Snap in UV-space so world origin maps to whole-UV meters on this plane
                        float uAtWorld0 = Vector3.Dot(-origin, U);
                        float vAtWorld0 = Vector3.Dot(-origin, V);
                        float uSnapMeters = Mathf.Round(uAtWorld0 / metersPerTileU) * metersPerTileU;
                        float vSnapMeters = Mathf.Round(vAtWorld0 / metersPerTileV) * metersPerTileV;
                        origin += U * (uAtWorld0 - uSnapMeters) + V * (vAtWorld0 - vSnapMeters);
                        // Note: origin remains on the same plane (shifted only within the plane)
                    }

                    frame = (origin, U, V);
                    planeFrames.Add(planeKey, frame);
                }

                // Manual per-corner UVs (if author provided them)
                Vector2 mu0 = _cornerUVs[t + 0];
                Vector2 mu1 = _cornerUVs[t + 1];
                Vector2 mu2 = _cornerUVs[t + 2];
                bool m0 = !float.IsNaN(mu0.x);
                bool m1 = !float.IsNaN(mu1.x);
                bool m2 = !float.IsNaN(mu2.x);

                int Emit(Vector3 p, Vector2 manualUV, bool hasManualUV)
                {
                    // Quantized in-plane position for welding (so co-planar identical points share verts)
                    Vector3 dvec = p - frame.origin;
                    int qu = Mathf.RoundToInt(Vector3.Dot(dvec, frame.U) / uvQuantize);
                    int qv = Mathf.RoundToInt(Vector3.Dot(dvec, frame.V) / uvQuantize);

                    // If a manual UV is provided for this corner, extend the weld key so that
                    // different manual UVs at the same in-plane position do NOT weld together.
                    int mflag = hasManualUV ? 1 : 0;
                    int mu = hasManualUV ? Mathf.RoundToInt(manualUV.x / manualUVQuantizeUnits) : 0;
                    int mv = hasManualUV ? Mathf.RoundToInt(manualUV.y / manualUVQuantizeUnits) : 0;

                    var key = (planeKey.nx, planeKey.ny, planeKey.nz, planeKey.d, sideBit, qu, qv, mflag, mu, mv);
                    if (welded.TryGetValue(key, out int widx)) return widx;

                    int idx = outVerts.Count;
                    outVerts.Add(p);

                    if (hasManualUV)
                    {
                        outUVs.Add(manualUV);
                    }
                    else
                    {
                        // Create meter-true planar UVs using the plane frame
                        Vector2 uv = UVMapping.Planar(
                            dvec,
                            frame.U, frame.V,
                            metersPerTileU, metersPerTileV,
                            0f, 0f // offsets handled by snapped origin
                        );
                        outUVs.Add(uv);
                    }

                    welded.Add(key, idx);
                    return idx;
                }

                int j0 = Emit(p0, mu0, m0);
                int j1 = Emit(p1, mu1, m1);
                int j2 = Emit(p2, mu2, m2);

                if (j0 == j1 || j1 == j2 || j2 == j0) continue; // skip degens after weld

                outTris.Add(j0); outTris.Add(j1); outTris.Add(j2);

                AuthoringSink?.OnFinalTriangleEmitted(finalTriCounter, t / 3);
                finalTriCounter++;
            }

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
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            AuthoringSink?.OnBakeEnd(mesh);
            return mesh;
        }




        public void Clear()
        {
            vertices.Clear();
            triangles.Clear();
            _cornerUVs.Clear();
        }
    }
    
    public enum QuadEdge { V0V1, V1V2, V2V3, V3V0 }

    public struct Quad_V2
    {
        public Vector3 v0, v1, v2, v3;

        public Quad_V2(Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3)
        { this.v0 = v0; this.v1 = v1; this.v2 = v2; this.v3 = v3; }

        static Vector3 FaceNormal(in Vector3 a, in Vector3 b, in Vector3 c)
        {
            var n = Vector3.Cross(b - a, c - a);
            return n.sqrMagnitude > 1e-12f ? n.normalized : Vector3.up;
        }

        static void FlipWinding(ref Quad_V2 q)
        {
            (q.v0, q.v1) = (q.v1, q.v0);
            (q.v2, q.v3) = (q.v3, q.v2);
        }

        public Quad_V2 CreateQuadFromExtrusion(QuadEdge edge, Vector3 direction, float distance)
        {
            Vector3 a, b;
            switch (edge)
            {
                case QuadEdge.V0V1: a = v0; b = v1; break;
                case QuadEdge.V1V2: a = v1; b = v2; break;
                case QuadEdge.V2V3: a = v2; b = v3; break;
                case QuadEdge.V3V0: a = v3; b = v0; break;
                default: throw new ArgumentOutOfRangeException(nameof(edge));
            }

            Vector3 dirN = direction.sqrMagnitude > 1e-12f ? direction.normalized : Vector3.up;
            Vector3 e    = dirN * distance;
            Vector3 a2   = a + e;
            Vector3 b2   = b + e;

            // candidate new quad (keeps seam order a->b)
            var q = new Quad_V2(a, b, b2, a2);

            // --- decide desired facing
            Vector3 nSrc = FaceNormal(v0, v1, v2);           // source face normal from vertex order
            Vector3 t    = (b - a).normalized;               // edge tangent along the seam
            float outOfPlane = Mathf.Abs(Vector3.Dot(dirN, nSrc));

            Vector3 nDesired;
            if (outOfPlane > 0.5f)
            {
                // WALL: opposite of the constructed quad's normal (which is Cross(t,dirN))
                nDesired = Vector3.Cross(dirN, t).normalized;   // <-- changed (forces the flip)
            }
            else
            {
                // RIBBON: keep same facing as source face (you can change this policy if needed)
                nDesired = nSrc;
            }

            // flip winding if the actual normal doesn’t match what we want
            Vector3 nNew = FaceNormal(q.v0, q.v1, q.v2);
            if (Vector3.Dot(nNew, nDesired) < 0f)
                FlipWinding(ref q);

            return q;
        }

    }

}
