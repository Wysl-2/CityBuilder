using System.Collections.Generic;
using UnityEngine;

public enum FaceType { Quad, Tri }

[System.Serializable]
public struct FaceRecord
{
    public FaceType type;
    public Vector3[] vertices;
    public int[] vertexIndices;
    public Winding winding;

    public FaceRecord(FaceType type, Vector3[] vertices, Winding winding, int[] vertexIndices)
    {
        if (vertices == null || (type == FaceType.Quad && vertices.Length != 4) || (type == FaceType.Tri && vertices.Length != 3))
            throw new System.ArgumentException("Invalid vertices for face type.");
        if (vertexIndices == null || vertexIndices.Length != vertices.Length)
            throw new System.ArgumentException("Invalid vertex indices.");
        this.type = type;
        this.vertices = vertices;
        this.winding = winding;
        this.vertexIndices = vertexIndices;
    }
}

public interface IFaceSink
{
    void OnFaceAdded(FaceType type, Vector3[] vertices, Winding winding, int[] vertexIndices);
}


public class FaceSinkTag : MonoBehaviour, IFaceSink
{
    [SerializeField] public List<FaceRecord> faces = new List<FaceRecord>();

    public void OnFaceAdded(FaceType type, Vector3[] vertices, Winding winding, int[] vertexIndices)
    {
        FaceRecord record = new FaceRecord(type, vertices, winding, vertexIndices);
        faces.Add(record);
    }
}

public class FaceSink : IFaceSink
{
    public List<FaceRecord> Faces { get; } = new List<FaceRecord>();

    public void OnFaceAdded(FaceType type, Vector3[] vertices, Winding winding, int[] vertexIndices)
    {
        FaceRecord record = new FaceRecord(type, vertices, winding, vertexIndices);
        Faces.Add(record);
    }
}