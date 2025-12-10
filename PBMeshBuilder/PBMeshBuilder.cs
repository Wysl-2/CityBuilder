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
    private readonly List<Color> colors = new();

    // Track which uvGroups use each vertex to apply disconnectedVertices at boundaries
    private readonly Dictionary<int, HashSet<int>> vertexToUvGroups = new();

    // Exposed properties (readonly lists)
    public IReadOnlyList<Vector3> Positions => positions;
    public IReadOnlyList<Face> Faces => faces;
    public IReadOnlyList<Color> Colors => colors;

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
        colors.Clear();
    }

    /// <summary>
    /// Adds a triangle face with optional UV seam control.
    /// </summary>
    /// <param name="disconnectedVertices">Vertex indices (0-2) to isolate for UV seams at uvGroup boundaries.</param>
    /// <param name="uvGroup">UV island group; vertices are shared within the same uvGroup if normals match.</param>
    public Face AddTriangleFace(
    Vector3 a, Vector3 b, Vector3 c,
    Winding winding = Winding.CW, int smoothingGroup = 0, int submeshIndex = 0,
    int[] disconnectedVertices = null, int uvGroup = 0,
    Color? vertexColor = null)
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

        var col = vertexColor ?? Color.white;

        int i0 = AddVertex(a, faceN, isDisconnected[0], uvGroup, col);
        int i1 = AddVertex(b, faceN, isDisconnected[1], uvGroup, col);
        int i2 = AddVertex(c, faceN, isDisconnected[2], uvGroup, col);

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
    public Face AddTriangleFace(
    Vector3[] vertices,
    Winding winding = Winding.CW,
    int smoothingGroup = 0, int submeshIndex = 0,
    int[] disconnectedVertices = null, int uvGroup = 0,
    Color? vertexColor = null)
    {
        if (vertices == null || vertices.Length != 3)
            throw new ArgumentException("Triangle requires exactly 3 vertices.", nameof(vertices));

        return AddTriangleFace(vertices[0], vertices[1], vertices[2], winding,
                            smoothingGroup, submeshIndex, disconnectedVertices, uvGroup, vertexColor);
    }


    /// <summary>
    /// Adds a quad face, triangulated along 0-2 or 1-3, with optional UV seam control.
    /// </summary>
    /// <param name="disconnectedVertices">Vertex indices (0-3) to isolate for UV seams at uvGroup boundaries.</param>
    /// <param name="uvGroup">UV island group; vertices are shared within the same uvGroup if normals match.</param>
    public Face AddQuadFace(
    Vector3 a, Vector3 b, Vector3 c, Vector3 d,
    Winding winding = Winding.CW, bool diag02 = true,
    int smoothingGroup = 0, int submeshIndex = 0,
    int[] disconnectedVertices = null, int uvGroup = 0,
    Color? vertexColor = null)
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

        var col = vertexColor ?? Color.white;

        int i0 = AddVertex(a, faceN, isDisconnected[0], uvGroup, col);
        int i1 = AddVertex(b, faceN, isDisconnected[1], uvGroup, col);
        int i2 = AddVertex(c, faceN, isDisconnected[2], uvGroup, col);
        int i3 = AddVertex(d, faceN, isDisconnected[3], uvGroup, col);

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
    public Face AddQuadFace(
    Vector3[] vertices,
    Winding winding = Winding.CW, bool diag02 = true,
    int smoothingGroup = 0, int submeshIndex = 0,
    int[] disconnectedVertices = null, int uvGroup = 0,
    Color? vertexColor = null)
    {
        if (vertices == null || vertices.Length != 4)
            throw new ArgumentException("Quad requires exactly 4 vertices.", nameof(vertices));

        return AddQuadFace(vertices[0], vertices[1], vertices[2], vertices[3],
                        winding, diag02, smoothingGroup, submeshIndex, disconnectedVertices, uvGroup, vertexColor);
    }


    /// <summary>
    /// Method to add a collection of faces to the builder. Determine if face is Quad or Tri based on array length.
    /// </summary>
    public void AddFaces(List<Vector3[]> faces, Color? vertexColor = null, int uvGroup = 0)
    {
        if (faces == null) return;

        var col = vertexColor ?? Color.white;

        for (int i = 0; i < faces.Count; i++)
        {
            var f = faces[i];
            if (f == null) continue;

            switch (f.Length)
            {
                case 3:
                    AddTriangleFace(f, Winding.CW, 0, 0, null, uvGroup, col);
                    break;

                case 4:
                    AddQuadFace(f, Winding.CW, true, 0, 0, null, uvGroup, col);
                    break;

                default:
                    Debug.LogWarning($"Face {i} has {f.Length} verts; only 3 or 4 supported.");
                    break;
            }
        }
    }


    /// <summary>
    /// Method to add a vertex of a face to the vertex list
    /// </summary>
    private int AddVertex(Vector3 p, Vector3 faceNormal, bool forceNew, int uvGroup, Color vertexColor)
    {
        var n = (faceNormal.sqrMagnitude > 0f) ? faceNormal.normalized : Vector3.up;
        var key = new PositionGroupKey { Position = p, Group = uvGroup };

        if (!bins.TryGetValue(key, out var list))
        {
            list = new List<NormalBin>();
            bins[key] = list;
        }

        for (int i = 0; i < list.Count; i++)
        {
            var b = list[i];

            // normal compatibility
            if (Mathf.Abs(Vector3.Dot(b.n, n)) >= normalTolCos)
            {
                // if vertex color differs, don't reuse this vertex
                if (colors[b.index] != vertexColor)
                    continue;

                // existing uvGroup seam logic
                if (forceNew && vertexToUvGroups.TryGetValue(b.index, out var groups) && groups.Contains(uvGroup))
                {
                    if (groups.Count > 1)
                        continue;           // used by other uvGroups, force new
                    return b.index;          // safe to share within this uvGroup
                }
                return b.index;
            }
        }

        int idx = positions.Count;
        positions.Add(p);
        colors.Add(vertexColor);   // NEW

        list.Add(new NormalBin { n = n, index = idx });

        if (!vertexToUvGroups.ContainsKey(idx))
            vertexToUvGroups[idx] = new HashSet<int>();
        vertexToUvGroups[idx].Add(uvGroup);

        return idx;
    }


    /// <summary>
    /// Builds and returns a ProBuilderMesh with the collected vertices and faces.
    /// </summary>
    /// 
    public ProBuilderMesh Build(Material[] materials = null, Transform host = null, bool refresh = true)
    {
        ProBuilderMesh pb;

        if (host != null)
        {
            // Build / reuse the ProBuilderMesh component directly on the host GameObject
            pb = host.GetComponent<ProBuilderMesh>();
            if (pb == null)
                pb = host.gameObject.AddComponent<ProBuilderMesh>();

            // Clear any existing geometry
            pb.Clear();

            // Assign positions and faces from the builder
            pb.positions = new System.Collections.Generic.List<Vector3>(positions);
            pb.faces     = new System.Collections.Generic.List<Face>(faces);

            if (colors.Count == positions.Count)
                pb.colors = new List<Color>(colors);
            else
                Debug.LogWarning($"PBMeshBuilder: colors.Count ({colors.Count}) != positions.Count ({positions.Count}).");
        }
        else
        {
            // Fallback: keep old behaviour when no host is supplied
            pb = ProBuilderMesh.Create(positions.ToArray(), faces.ToArray());

            if (colors.Count == positions.Count)
                pb.colors = new List<Color>(colors);
            else
                Debug.LogWarning($"PBMeshBuilder: colors.Count ({colors.Count}) != positions.Count ({positions.Count}).");
        }

        // Set sharedVertices so every vertex is its own shared index
        var groups = new SharedVertex[positions.Count];
        for (int i = 0; i < positions.Count; i++)
            groups[i] = new SharedVertex(new[] { i });
        pb.sharedVertices = groups;

        // Materials / submeshes
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

    // -- Static Helpers
    private static Vector3 ComputeFaceNormal(in Vector3 a, in Vector3 b, in Vector3 c)
    {
        var n = Vector3.Cross(b - a, c - a);
        var mag = n.magnitude;
        return (mag > 1e-20f) ? (n / mag) : Vector3.zero;
    }

}

