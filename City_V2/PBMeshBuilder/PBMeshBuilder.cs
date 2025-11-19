using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.ProBuilder;

public enum Winding { CCW, CW }

/// <summary>
/// Builds a ProBuilderMesh with vertex deduplication by position (within epsilon) and normal (within tolerance).
/// Supports UV island control via uvGroup, ensuring vertices are shared within the same uvGroup.
/// </summary>
public class PBMeshBuilder
{
    private struct PositionGroupKey
    {
        public Vector3 Position;
        public int Group; // Now represents uvGroup
    }

    private sealed class PositionGroupKeyComparer : IEqualityComparer<PositionGroupKey>
    {
        private readonly float eps;
        public PositionGroupKeyComparer(float epsilon) => eps = Mathf.Max(1e-9f, epsilon);
        public bool Equals(PositionGroupKey a, PositionGroupKey b) =>
            a.Group == b.Group &&
            Mathf.Abs(a.Position.x - b.Position.x) <= eps &&
            Mathf.Abs(a.Position.y - b.Position.y) <= eps &&
            Mathf.Abs(a.Position.z - b.Position.z) <= eps;
        public int GetHashCode(PositionGroupKey v)
        {
            int xi = Mathf.RoundToInt(v.Position.x / eps);
            int yi = Mathf.RoundToInt(v.Position.y / eps);
            int zi = Mathf.RoundToInt(v.Position.z / eps);
            return (xi * 73856093 ^ yi * 19349663 ^ zi * 83492791) ^ v.Group.GetHashCode();
        }
    }

    private struct NormalBin { public Vector3 n; public int index; }

    private readonly float posEpsilon;
    private readonly float normalTolCos;
    private readonly Dictionary<PositionGroupKey, List<NormalBin>> bins;
    private readonly List<Vector3> positions = new();
    private readonly List<Face> faces = new();
    // Track which uvGroups use each vertex to apply disconnectedVertices at boundaries
    private readonly Dictionary<int, HashSet<int>> vertexToUvGroups = new();

    public IReadOnlyList<Vector3> Positions => positions;
    public IReadOnlyList<Face> Faces => faces;

    // Authoring tag
    public IFaceSink? Sink { get; set; }

    public PBMeshBuilder(float positionEpsilon = 1e-5f, float normalToleranceDegrees = 5f, IFaceSink? sink = null)
    {
        if (positionEpsilon <= 0 || positionEpsilon > 0.1f)
            throw new ArgumentException("positionEpsilon must be positive and reasonable (e.g., <= 0.1)", nameof(positionEpsilon));
        posEpsilon = positionEpsilon;
        normalTolCos = Mathf.Cos(Mathf.Deg2Rad * Mathf.Max(0f, normalToleranceDegrees));
        bins = new Dictionary<PositionGroupKey, List<NormalBin>>(new PositionGroupKeyComparer(positionEpsilon));
        Sink = sink;
    }

    /// <summary>
    /// Clears internal state for reuse.
    /// </summary>
    public void Clear()
    {
        bins.Clear();
        positions.Clear();
        faces.Clear();
        vertexToUvGroups.Clear();
    }

    /// <summary>
    /// Adds a triangle face with optional UV seam control.
    /// </summary>
    /// <param name="disconnectedVertices">Vertex indices (0-2) to isolate for UV seams at uvGroup boundaries.</param>
    /// <param name="uvGroup">UV island group; vertices are shared within the same uvGroup if normals match.</param>
    public Face AddTriangleFace(Vector3 a, Vector3 b, Vector3 c,
                    Winding winding = Winding.CW, int smoothingGroup = 0, int submeshIndex = 0,
                    int[] disconnectedVertices = null, int uvGroup = 0)
    {
        if (smoothingGroup < 0) Debug.LogWarning($"Negative smoothingGroup {smoothingGroup} may be invalid.");
        if (submeshIndex < 0) Debug.LogWarning($"Negative submeshIndex {submeshIndex} may be invalid.");

        var faceN = ComputeFaceNormal(a, b, c);
        if (faceN == Vector3.zero)
        {
            Debug.LogWarning("Degenerate triangle detected; using default normal (0,1,0).");
            faceN = Vector3.up;
        }

        bool[] isDisconnected = new bool[3];
        if (disconnectedVertices != null)
        {
            foreach (int vIdx in disconnectedVertices)
            {
                if (vIdx >= 0 && vIdx < 3)
                    isDisconnected[vIdx] = true;
                else
                    Debug.LogWarning($"Invalid disconnected vertex index {vIdx} for triangle face.");
            }
        }

        int i0 = AddVertex(a, faceN, isDisconnected[0], uvGroup);
        int i1 = AddVertex(b, faceN, isDisconnected[1], uvGroup);
        int i2 = AddVertex(c, faceN, isDisconnected[2], uvGroup);

        int[] tri = (winding == Winding.CCW)
            ? new[] { i0, i1, i2 }
            : new[] { i0, i2, i1 };

        var f = new Face(tri)
        {
            smoothingGroup = smoothingGroup,
            submeshIndex = submeshIndex
        };
        faces.Add(f);

        Sink?.OnFaceAdded(FaceType.Tri, new[] { a, b, c }, winding, tri);

        return f;
    }
    // -- Method Overloads
    /// <param name="vertices">Array of 4 vertices in order: bottom-left, bottom-right, top-right, top-left (e.g., (0,0,0), (1,0,0), (1,0,1), (0,0,1) for XZ quad).</param>
    public Face AddTriangleFace(Vector3[] vertices, Winding winding = Winding.CW,
                int smoothingGroup = 0, int submeshIndex = 0, int[] disconnectedVertices = null, int uvGroup = 0)
    {
        if (vertices == null || vertices.Length != 3) throw new ArgumentException("Triangle requires exactly 3 vertices.", nameof(vertices));
        // Forward vertices to original method
        return AddTriangleFace(vertices[0], vertices[1], vertices[2], winding,
                                smoothingGroup, submeshIndex, disconnectedVertices, uvGroup);
    }
       public Face AddTriangleFace(Triangle tri)
    {
        if (tri.Vertices == null || tri.Vertices.Length != 3) throw new ArgumentException("Triangle requires exactly 3 vertices.", nameof(tri.Vertices));
        // Forward to original method
        return AddTriangleFace(tri.Vertices[0], tri.Vertices[1], tri.Vertices[2], tri.Winding,
                            tri.SmoothingGroup, tri.SubmeshIndex, tri.DisconnectedVertices, tri.UVGroup);
    }

    /// <summary>
    /// Adds a quad face, triangulated along 0-2 or 1-3, with optional UV seam control.
    /// </summary>
    /// <param name="disconnectedVertices">Vertex indices (0-3) to isolate for UV seams at uvGroup boundaries.</param>
    /// <param name="uvGroup">UV island group; vertices are shared within the same uvGroup if normals match.</param>
    public Face AddQuadFace(Vector3 a, Vector3 b, Vector3 c, Vector3 d,
                    Winding winding = Winding.CW, bool diag02 = true, int smoothingGroup = 0, int submeshIndex = 0,
                    int[] disconnectedVertices = null, int uvGroup = 0)
    {
        if (smoothingGroup < 0) Debug.LogWarning($"Negative smoothingGroup {smoothingGroup} may be invalid.");
        if (submeshIndex < 0) Debug.LogWarning($"Negative submeshIndex {submeshIndex} may be invalid.");

        var n1 = ComputeFaceNormal(a, b, c);
        var n2 = ComputeFaceNormal(a, c, d);
        var faceN = ((n1 + n2).normalized.sqrMagnitude > 0f) ? (n1 + n2).normalized : Vector3.up;
        if (faceN == Vector3.up) Debug.LogWarning("Degenerate quad detected; using default normal (0,1,0).");

        bool[] isDisconnected = new bool[4];
        if (disconnectedVertices != null)
        {
            foreach (int vIdx in disconnectedVertices)
            {
                if (vIdx >= 0 && vIdx < 4)
                    isDisconnected[vIdx] = true;
                else
                    Debug.LogWarning($"Invalid disconnected vertex index {vIdx} for quad face.");
            }
        }

        int i0 = AddVertex(a, faceN, isDisconnected[0], uvGroup);
        int i1 = AddVertex(b, faceN, isDisconnected[1], uvGroup);
        int i2 = AddVertex(c, faceN, isDisconnected[2], uvGroup);
        int i3 = AddVertex(d, faceN, isDisconnected[3], uvGroup);

        int[] t0, t1;
        if (diag02)
        {
            t0 = (winding == Winding.CCW) ? new[] { i0, i1, i2 } : new[] { i0, i2, i1 };
            t1 = (winding == Winding.CCW) ? new[] { i0, i2, i3 } : new[] { i0, i3, i2 };
        }
        else
        {
            t0 = (winding == Winding.CCW) ? new[] { i0, i1, i3 } : new[] { i0, i3, i1 };
            t1 = (winding == Winding.CCW) ? new[] { i1, i2, i3 } : new[] { i1, i3, i2 };
        }

        var f = new Face(new int[] { t0[0], t0[1], t0[2], t1[0], t1[1], t1[2] })
        {
            smoothingGroup = smoothingGroup,
            submeshIndex = submeshIndex
        };
        faces.Add(f);

        Sink?.OnFaceAdded(FaceType.Quad, new[] { a, b, c, d }, winding, new[] { i0, i1, i2, i3 });

        return f;
    }
    // -- Method Overloads
    public Face AddQuadFace(Vector3[] vertices, Winding winding = Winding.CW, bool diag02 = true,
                    int smoothingGroup = 0, int submeshIndex = 0, int[] disconnectedVertices = null, int uvGroup = 0)
    {
        if (vertices == null || vertices.Length != 4) throw new ArgumentException("Quad requires exactly 4 vertices.", nameof(vertices));
        // Forward to original method
        return AddQuadFace(vertices[0], vertices[1], vertices[2], vertices[3], winding, diag02,
                    smoothingGroup, submeshIndex, disconnectedVertices, uvGroup);
    }
    public Face AddQuadFace(Quad quad)
    {
        if (quad.Vertices == null || quad.Vertices.Length != 4) throw new ArgumentException("Quad requires exactly 4 vertices.", nameof(quad.Vertices));
        // Forward to original method
        return AddQuadFace(quad.Vertices[0], quad.Vertices[1], quad.Vertices[2], quad.Vertices[3], quad.Winding, quad.Diag02,
                            quad.SmoothingGroup, quad.SubmeshIndex, quad.DisconnectedVertices, quad.UVGroup);
    }

    /// <summary>
    /// Builds and returns a ProBuilderMesh with the collected vertices and faces.
    /// </summary>
    public ProBuilderMesh Build(Material[] materials = null, Transform parent = null, bool refresh = true)
    {
        var pb = ProBuilderMesh.Create(positions.ToArray(), faces.ToArray());

        if(parent != null)
        {
            var t = pb.transform;
            t.SetParent(parent);
        }

        var groups = new SharedVertex[positions.Count];
        for (int i = 0; i < positions.Count; i++)
            groups[i] = new SharedVertex(new[] { i });
        pb.sharedVertices = groups;

        if (materials != null && materials.Length > 0)
        {
            var facesBySubmesh = new Dictionary<int, List<Face>>();
            foreach (var face in faces)
            {
                int submeshIndex = face.submeshIndex;
                if (submeshIndex >= materials.Length)
                    Debug.LogError($"submeshIndex {submeshIndex} exceeds materials array length ({materials.Length}).");
                if (!facesBySubmesh.ContainsKey(submeshIndex))
                    facesBySubmesh[submeshIndex] = new List<Face>();
                facesBySubmesh[submeshIndex].Add(face);
            }

            foreach (var kvp in facesBySubmesh)
            {
                int submeshIndex = kvp.Key;
                var submeshFaces = kvp.Value;
                Material material = submeshIndex < materials.Length ? materials[submeshIndex] : null;
                if (material != null)
                    pb.SetMaterial(submeshFaces, material);
                else
                    Debug.LogWarning($"No material provided for submeshIndex {submeshIndex}. Faces will use default material.");
            }

            var renderer = pb.GetComponent<MeshRenderer>();
            renderer.sharedMaterials = materials;
        }

        if (refresh)
        {
            pb.ToMesh();
            pb.Refresh(RefreshMask.All);
        }

        bins.Clear();
        vertexToUvGroups.Clear();
        return pb;
    }

    private static Vector3 ComputeFaceNormal(in Vector3 a, in Vector3 b, in Vector3 c)
    {
        var n = Vector3.Cross(b - a, c - a);
        var mag = n.magnitude;
        return (mag > 1e-20f) ? (n / mag) : Vector3.zero;
    }

    private int AddVertex(Vector3 p, Vector3 faceNormal, bool forceNew, int uvGroup)
    {
        var n = (faceNormal.sqrMagnitude > 0f) ? faceNormal.normalized : Vector3.up;
        var key = new PositionGroupKey { Position = p, Group = uvGroup }; // Always use uvGroup

        if (!bins.TryGetValue(key, out var list))
        {
            list = new List<NormalBin>();
            bins[key] = list;
        }

        for (int i = 0; i < list.Count; i++)
        {
            var b = list[i];
            if (Mathf.Abs(Vector3.Dot(b.n, n)) >= normalTolCos)
            {
                // Check if this vertex is shared across different uvGroups and needs disconnection
                if (forceNew && vertexToUvGroups.TryGetValue(b.index, out var groups) && groups.Contains(uvGroup))
                {
                    // Vertex is used by this uvGroup; share unless explicitly disconnected across groups
                    if (groups.Count > 1) continue; // Used by other uvGroups, force new vertex
                    return b.index; // Safe to share within this uvGroup
                }
                return b.index;
            }
        }

        int idx = positions.Count;
        positions.Add(p);
        list.Add(new NormalBin { n = n, index = idx });

        // Track uvGroup usage for this vertex
        if (!vertexToUvGroups.ContainsKey(idx))
            vertexToUvGroups[idx] = new HashSet<int>();
        vertexToUvGroups[idx].Add(uvGroup);

        return idx;
    }
}

public struct Quad
{
    public Vector3[] Vertices { get; set; }
    public Winding Winding { get; }
    public bool Diag02 { get; }
    public int SubmeshIndex { get; }
    public int SmoothingGroup { get; }
    public int UVGroup { get; }
    public int[] DisconnectedVertices { get; }

    /// <summary>
    /// Creates a quad with specified vertices and properties.
    /// </summary>
    public Quad(Vector3[] vertices, Winding winding = Winding.CW, bool diag02 = true, int submeshIndex = 0, int smoothingGroup = 0, int uvGroup = 0, int[] disconnectedVertices = null)
    {
        if (vertices == null || vertices.Length != 4) throw new ArgumentException("Quad requires exactly 4 vertices.", nameof(vertices));

        Vertices = vertices;
        Winding = winding;
        Diag02 = diag02;
        SubmeshIndex = submeshIndex;
        SmoothingGroup = smoothingGroup;
        UVGroup = uvGroup;
        DisconnectedVertices = disconnectedVertices;
    }

    public Vector3[] ExtrudeEdge(int edgeIndex, Vector3 direction, float distance)
    {
        if (edgeIndex < 0 || edgeIndex > 3)
            throw new ArgumentException("Edge index must be between 0 and 3.", nameof(edgeIndex));
        if (direction == Vector3.zero)
            throw new ArgumentException("Direction cannot be zero.", nameof(direction));

        Vector3 a = Vertices[edgeIndex];
        Vector3 b = Vertices[(edgeIndex + 1) % 4];
        Vector3 c = a + direction.normalized * distance;
        Vector3 d = b + direction.normalized * distance;

        if (Winding == Winding.CW)
            return new Vector3[] { a, c, d, b }; // Fixed: v0, v0+offset, v1+offset, v1
        else
            return new Vector3[] { a, b, d, c }; // CCW: v0, v1, v1+offset, v0+offset
    }

    public Vector3[] ExtrudeEdgeOffset(int edgeIndex, Vector3 offset)
    {
        if (edgeIndex < 0 || edgeIndex > 3)
            throw new ArgumentException("Edge index must be between 0 and 3.", nameof(edgeIndex));

        Vector3 a = Vertices[edgeIndex];
        Vector3 b = Vertices[(edgeIndex + 1) % 4];
        Vector3 a2 = a + offset;
        Vector3 b2 = b + offset;

        return (Winding == Winding.CW)
            ? new[] { a, a2, b2, b }
            : new[] { a, b, b2, a2 };
    }

    public Vector3[] ExtrudeEdgeOutAndVertical(
    int edgeIndex,
    Vector3 outward,
    float outDistance,
    float verticalAmount,
    Vector3 upAxis = default)
    {
        if (edgeIndex < 0 || edgeIndex > 3)
            throw new ArgumentException("Edge index must be between 0 and 3.", nameof(edgeIndex));

        if (upAxis == default) upAxis = Vector3.up;
        var up = upAxis.normalized;

        // Project outward onto plane perpendicular to up so it's purely horizontal
        var outwardProj = outward - Vector3.Dot(outward, up) * up;
        var sqrMag = outwardProj.sqrMagnitude;
        if (sqrMag < 1e-12f)
            throw new ArgumentException("Outward must have a non-zero horizontal component.", nameof(outward));

        var outDir = outwardProj / Mathf.Sqrt(sqrMag);

        // Edge endpoints (top edge)
        Vector3 a = Vertices[edgeIndex];
        Vector3 b = Vertices[(edgeIndex + 1) % 4];

        // Build the offset (horizontal out + vertical)
        Vector3 offset = outDir * outDistance + up * verticalAmount;

        // Bottom edge (extruded)
        Vector3 a2 = a + offset;
        Vector3 b2 = b + offset;

        // Match your existing winding convention for side faces
        if (Winding == Winding.CW)
            return new[] { a, a2, b2, b };   // t0, b0, b1, t1
        else
            return new[] { a, b, b2, a2 };   // t0, t1, b1, b0
    }

    /// <summary>
    /// Convenience: extrude "out and DOWN" by positive distances (downAmount >= 0).
    /// Equivalent to ExtrudeEdgeOutAndVertical(edgeIndex, outward, outAmount, -downAmount).
    /// </summary>
    public Vector3[] ExtrudeEdgeOutDown(
        int edgeIndex,
        Vector3 outward,
        float outAmount,
        float downAmount,
        Vector3 upAxis = default)
    {
        if (downAmount < 0f)
            throw new ArgumentException("downAmount should be non-negative; use ExtrudeEdgeOutAndVertical for signed values.", nameof(downAmount));

        return ExtrudeEdgeOutAndVertical(edgeIndex, outward, outAmount, -downAmount, upAxis);
    }

public Vector3[] ExtrudeEdgeOutHeight(int edgeIndex, float outAmount, float heightAmount)
{
    if (edgeIndex < 0 || edgeIndex > 3)
        throw new ArgumentException("Edge index must be between 0 and 3.", nameof(edgeIndex));

    // Edge endpoints
    Vector3 a = Vertices[edgeIndex];
    Vector3 b = Vertices[(edgeIndex + 1) % 4];

    // Face normal (from the first three vertices as stored).
    // This may point up or down depending on Winding; that's fine—we use it consistently.
    Vector3 n = Vector3.Cross(Vertices[1] - Vertices[0], Vertices[2] - Vertices[0]);
    float nLen = n.magnitude;
    if (nLen < 1e-12f) throw new InvalidOperationException("Degenerate quad: normal is zero.");
    n /= nLen;

    // Edge (boundary) direction
    Vector3 t = b - a;
    float tLen = t.magnitude;
    if (tLen < 1e-12f) throw new InvalidOperationException("Degenerate edge: zero length.");
    t /= tLen;

    // Outward in the quad's plane, away from polygon interior.
    // For a CCW polygon, outward = cross(n, t). For CW, outward = cross(t, n).
    Vector3 outward = (Winding == Winding.CCW) ? Vector3.Cross(n, t) : Vector3.Cross(t, n);
    float oLen = outward.magnitude;
    if (oLen < 1e-12f) throw new InvalidOperationException("Cannot compute outward direction (degenerate).");
    outward /= oLen;

    // Final offset for the extrusion
    Vector3 offset = outward * outAmount + n * heightAmount;

    // Bottom edge (extruded)
    Vector3 a2 = a + offset;
    Vector3 b2 = b + offset;

    // Return with ordering consistent with this quad's Winding
    return (Winding == Winding.CW)
        ? new[] { a, a2, b2, b }   // t0, b0, b1, t1
        : new[] { a, b, b2, a2 };  // t0, t1, b1, b0
}

}

public struct Triangle
{
    public Vector3[] Vertices;
    public Winding Winding;
    public int SubmeshIndex;
    public int SmoothingGroup;
    public int UVGroup;
    public int[] DisconnectedVertices { get; }

    /// <summary>
    /// Creates a triangle with specified vertices and properties.
    /// </summary>
    public Triangle(Vector3[] vertices, Winding winding = Winding.CW, int submeshIndex = 0, int smoothingGroup = 0, int uvGroup = 0, int[] disconnectedVertices = null)
    {
        if (vertices == null || vertices.Length != 3) throw new ArgumentException("Triangle requires exactly 3 vertices.", nameof(vertices));

        Vertices = vertices;
        Winding = winding;
        SubmeshIndex = submeshIndex;
        SmoothingGroup = smoothingGroup;
        UVGroup = uvGroup;
        DisconnectedVertices = disconnectedVertices;
    }
}