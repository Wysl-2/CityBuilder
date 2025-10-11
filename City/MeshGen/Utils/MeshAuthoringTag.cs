using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ProcGen;

[DisallowMultipleComponent]
public class MeshAuthoringTag : MonoBehaviour
{
    public Mesh mesh;

    public enum FaceKind { Tri, Quad }

    [System.Serializable]
    public class FaceRecord
    {
        public FaceKind kind;
        public Winding winding;
        public QuadSplit split;
        public Vector3 v0, v1, v2, v3;  // v3 unused for tris
        public int builderTriStart;
        public int builderTriCount;     // 1 for tri, 2 for quad
    }

    public List<FaceRecord> faces = new();
    public List<int> builderTriToFace = new();         // builder tri -> face idx
    public List<Vector3Int> builderTriCornerUse = new(); // which quad corners a builder tri used
    public List<int> finalTriToBuilderTri = new();     // baked tri -> builder tri

    public int vertexCount, triangleCount; // sanity
}

public interface IMeshAuthoringSink
{
    void OnBakeBegin();
    void OnTriAdded(int builderTriIndex, Vector3 a, Vector3 b, Vector3 c);
    void OnQuadAdded(int builderTriStart, Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3,
                     Winding winding, QuadSplit split, Vector3Int tri0CornerUse, Vector3Int tri1CornerUse);
    void OnFinalTriangleEmitted(int finalTriIndex, int builderTriIndex);
    void OnBakeEnd(Mesh mesh);
}


public sealed class MeshAuthoringTagSink : IMeshAuthoringSink
{
    readonly MeshAuthoringTag tag;
    public MeshAuthoringTagSink(MeshAuthoringTag tag) { this.tag = tag; }

    // Helpers that fill with sentinels (so we can detect "unclaimed")
    static void EnsureSizeInt(List<int> list, int size, int fill = -1)
    { while (list.Count < size) list.Add(fill); }

    static void EnsureSizeVec3Int(List<Vector3Int> list, int size, int fill = -1)
    {
        while (list.Count < size) list.Add(new Vector3Int(fill, fill, fill));
    }

    public void OnBakeBegin()
    {
        // Don’t clear author-time data here (faces, builderTriToFace, builderTriCornerUse)
        tag.finalTriToBuilderTri.Clear();
    }

    public void OnTriAdded(int triIdx, Vector3 a, Vector3 b, Vector3 c)
    {
        // If this builder triangle already belongs to a quad, skip making a tri face
        if (triIdx < tag.builderTriToFace.Count &&
            tag.builderTriToFace[triIdx] >= 0 &&
            tag.faces[tag.builderTriToFace[triIdx]].kind == MeshAuthoringTag.FaceKind.Quad)
        {
            // (optional) ensure a default corner-use is present
            EnsureSizeVec3Int(tag.builderTriCornerUse, triIdx + 1);
            if (tag.builderTriCornerUse[triIdx].x < 0)
                tag.builderTriCornerUse[triIdx] = new Vector3Int(0, 1, 2);
            return;
        }

        var f = new MeshAuthoringTag.FaceRecord
        {
            kind = MeshAuthoringTag.FaceKind.Tri,
            winding = Winding.CCW,                 // author-order is (a,b,c)
            split = QuadSplit.Diag02,
            v0 = a, v1 = b, v2 = c, v3 = Vector3.zero,
            builderTriStart = triIdx,
            builderTriCount = 1
        };
        int face = tag.faces.Count; tag.faces.Add(f);

        EnsureSizeInt(tag.builderTriToFace, triIdx + 1, -1);
        tag.builderTriToFace[triIdx] = face;

        EnsureSizeVec3Int(tag.builderTriCornerUse, triIdx + 1);
        tag.builderTriCornerUse[triIdx] = new Vector3Int(0, 1, 2);
    }

    public void OnQuadAdded(
        int triStart, Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3,
        Winding winding, QuadSplit split, Vector3Int t0, Vector3Int t1)
    {
        var f = new MeshAuthoringTag.FaceRecord
        {
            kind = MeshAuthoringTag.FaceKind.Quad,
            winding = winding,
            split = split,
            v0 = v0, v1 = v1, v2 = v2, v3 = v3,
            builderTriStart = triStart,
            builderTriCount = 2
        };
        int face = tag.faces.Count; tag.faces.Add(f);

        // Claim the two builder triangles for this quad
        EnsureSizeInt(tag.builderTriToFace, triStart + 2, -1);
        tag.builderTriToFace[triStart + 0] = face;
        tag.builderTriToFace[triStart + 1] = face;

        EnsureSizeVec3Int(tag.builderTriCornerUse, triStart + 2);
        tag.builderTriCornerUse[triStart + 0] = t0;
        tag.builderTriCornerUse[triStart + 1] = t1;
    }

    public void OnFinalTriangleEmitted(int finalTriIndex, int builderTriIndex)
    {
        EnsureSizeInt(tag.finalTriToBuilderTri, finalTriIndex + 1, -1);
        tag.finalTriToBuilderTri[finalTriIndex] = builderTriIndex;
    }

    public void OnBakeEnd(Mesh mesh)
    {
        tag.mesh = mesh;
        tag.vertexCount = mesh.vertexCount;
        tag.triangleCount = mesh.triangles.Length / 3;
    }
}
