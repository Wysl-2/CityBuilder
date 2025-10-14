using System.Collections.Generic;
using UnityEngine;
using UnityEngine.ProBuilder;

public enum Winding { CCW, CW }

/// Dedupes by (position, plane-within-tolerance), collects faces, and builds a ProBuilderMesh.
public class PBMeshBuilder
{
    private sealed class Vec3Approx : IEqualityComparer<Vector3>
    {
        private readonly float eps;
        public Vec3Approx(float epsilon) => eps = Mathf.Max(1e-9f, epsilon);
        public bool Equals(Vector3 a, Vector3 b) =>
            Mathf.Abs(a.x - b.x) <= eps &&
            Mathf.Abs(a.y - b.y) <= eps &&
            Mathf.Abs(a.z - b.z) <= eps;
        public int GetHashCode(Vector3 v)
        {
            int xi = Mathf.RoundToInt(v.x / eps);
            int yi = Mathf.RoundToInt(v.y / eps);
            int zi = Mathf.RoundToInt(v.z / eps);
            return xi * 73856093 ^ yi * 19349663 ^ zi * 83492791;
        }
    }

    private struct NormalBin { public Vector3 n; public int index; }

    private readonly float posEpsilon;
    private readonly float normalTolCos;
    private readonly Dictionary<Vector3, List<NormalBin>> bins;
    private readonly List<Vector3> positions = new();
    private readonly List<Face> faces = new();

    public IReadOnlyList<Vector3> Positions => positions;
    public IReadOnlyList<Face> Faces => faces;

    public PBMeshBuilder(float positionEpsilon = 1e-5f, float normalToleranceDegrees = 1f)
    {
        posEpsilon = positionEpsilon;
        normalTolCos = Mathf.Cos(Mathf.Deg2Rad * Mathf.Max(0f, normalToleranceDegrees));
        bins = new Dictionary<Vector3, List<NormalBin>>(new Vec3Approx(positionEpsilon));
    }

    // ---------- public API ----------

    public Face AddTriangleFace(Vector3 a, Vector3 b, Vector3 c, Winding winding = Winding.CW)
    {
        var faceN = ComputeFaceNormal(a, b, c);
        if (faceN == Vector3.zero) faceN = Vector3.up;

        int i0 = AddVertex(a, faceN);
        int i1 = AddVertex(b, faceN);
        int i2 = AddVertex(c, faceN);

        int[] tri = (winding == Winding.CCW)
            ? new[] { i0, i1, i2 }
            : new[] { i0, i2, i1 };

        var f = new Face(tri);
        faces.Add(f);
        return f;
    }

    /// Quad triangulated either along 0-2 (default) or 1-3.
    public Face AddQuadFace(Vector3 a, Vector3 b, Vector3 c, Vector3 d,
                            Winding winding = Winding.CW, bool diag02 = true, int smoothingGroup = 0)
    {
        var faceN = ComputeFaceNormal(a, b, c);
        if (faceN == Vector3.zero) faceN = Vector3.up;

        int i0 = AddVertex(a, faceN);
        int i1 = AddVertex(b, faceN);
        int i2 = AddVertex(c, faceN);
        int i3 = AddVertex(d, faceN);

        int[] t0, t1;
        if (diag02)
        {
            // CCW: (0,1,2) & (0,2,3)
            t0 = (winding == Winding.CCW) ? new[] { i0, i1, i2 } : new[] { i0, i2, i1 };
            t1 = (winding == Winding.CCW) ? new[] { i0, i2, i3 } : new[] { i0, i3, i2 };
        }
        else
        {
            // CCW: (0,1,3) & (1,2,3)
            t0 = (winding == Winding.CCW) ? new[] { i0, i1, i3 } : new[] { i0, i3, i1 };
            t1 = (winding == Winding.CCW) ? new[] { i1, i2, i3 } : new[] { i1, i3, i2 };
        }

        var f = new Face(new int[] { t0[0], t0[1], t0[2],  t1[0], t1[1], t1[2] })
        {
            smoothingGroup = smoothingGroup
        };
        faces.Add(f);
        return f;
    }

    public ProBuilderMesh Build(GameObject host, Material material = null, bool refresh = true)
    {
        var pb = ProBuilderMesh.Create(positions.ToArray(), faces.ToArray());

        // Identity shared groups: keep same-position/different-plane verts distinct.
        var groups = new SharedVertex[positions.Count];
        for (int i = 0; i < positions.Count; i++)
            groups[i] = new SharedVertex(new[] { i });
        pb.sharedVertices = groups;

        if (material) pb.SetMaterial(pb.faces, material);
        if (refresh) { pb.ToMesh(); pb.Refresh(RefreshMask.All); }
        return pb;
    }

    // ---------- internals ----------

    private static Vector3 ComputeFaceNormal(in Vector3 a, in Vector3 b, in Vector3 c)
    {
        var n = Vector3.Cross(b - a, c - a);
        var mag = n.magnitude;
        return (mag > 1e-20f) ? (n / mag) : Vector3.zero;
    }

    // Weld only when planes are parallel (±n) within tolerance.
    private int AddVertex(Vector3 p, Vector3 faceNormal)
    {
        var n = (faceNormal.sqrMagnitude > 0f) ? faceNormal.normalized : Vector3.up;

        if (!bins.TryGetValue(p, out var list))
        {
            list = new List<NormalBin>();
            bins[p] = list;
        }

        for (int i = 0; i < list.Count; i++)
        {
            var b = list[i];
            // abs(dot) → consider ±n parallel (same plane regardless of CW/CCW)
            if (Mathf.Abs(Vector3.Dot(b.n, n)) >= normalTolCos)
                return b.index;
        }

        int idx = positions.Count;
        positions.Add(p);
        list.Add(new NormalBin { n = n, index = idx });
        return idx;
    }
}
