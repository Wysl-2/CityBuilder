using System;
using UnityEngine;

public static class VertexOperations
{
    /// <summary>
    /// Translates a set of vertices by an offset, optionally in-place.
    /// </summary>
    public static Vector3[] Translate(Vector3[] vertices, Vector3 offset, bool inPlace = false)
    {
        if (vertices == null || vertices.Length == 0) throw new ArgumentException("Vertices array cannot be null or empty.", nameof(vertices));
        
        Vector3[] result = inPlace ? vertices : new Vector3[vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
            result[i] = vertices[i] + offset;
        return result;
    }

    /// <summary>
    /// Rotates a set of vertices around a pivot using a quaternion, optionally in-place.
    /// </summary>
    public static Vector3[] Rotate(Vector3[] vertices, Quaternion rotation, Vector3 pivot, bool inPlace = false)
    {
        if (vertices == null || vertices.Length == 0) throw new ArgumentException("Vertices array cannot be null or empty.", nameof(vertices));
        
        Vector3[] result = inPlace ? vertices : new Vector3[vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 offset = vertices[i] - pivot;
            result[i] = pivot + rotation * offset;
        }
        return result;
    }

    /// <summary>
    /// Rotates a set of vertices around a pivot using Euler angles, optionally in-place.
    /// </summary>
    public static Vector3[] Rotate(Vector3[] vertices, Vector3 eulerAngles, Vector3 pivot, bool inPlace = false)
    {
        return Rotate(vertices, Quaternion.Euler(eulerAngles), pivot, inPlace);
    }

    /// <summary>
    /// Scales a set of vertices relative to a pivot, optionally in-place.
    /// </summary>
    public static Vector3[] Scale(Vector3[] vertices, Vector3 scale, Vector3 pivot, bool inPlace = false)
    {
        if (vertices == null || vertices.Length == 0) throw new ArgumentException("Vertices array cannot be null or empty.", nameof(vertices));
        if (scale.x == 0 || scale.y == 0 || scale.z == 0) throw new ArgumentException("Scale cannot have zero components.", nameof(scale));
        
        Vector3[] result = inPlace ? vertices : new Vector3[vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 offset = vertices[i] - pivot;
            result[i] = pivot + new Vector3(offset.x * scale.x, offset.y * scale.y, offset.z * scale.z);
        }
        return result;
    }

    /// <summary>
    /// Applies a combined transformation (scale, rotate, translate) via a matrix, optionally in-place.
    /// </summary>
    public static Vector3[] Transform(Vector3[] vertices, Matrix4x4 matrix, bool inPlace = false)
    {
        if (vertices == null || vertices.Length == 0) throw new ArgumentException("Vertices array cannot be null or empty.", nameof(vertices));

        Vector3[] result = inPlace ? vertices : new Vector3[vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
            result[i] = matrix.MultiplyPoint3x4(vertices[i]);
        return result;
    }
    
    // --------------------------------------------------------------------------
    // ------- Methods for operating on multiple arrays of vertices at once ------

    // Rotation
      public static Vector3[][] RotateMany(Vector3[][] vertexSets, Quaternion rotation, Vector3 pivot, bool inPlace = false)
    {
        if (vertexSets == null || vertexSets.Length == 0)
            throw new ArgumentException("Vertex set collection cannot be null or empty.", nameof(vertexSets));

        Vector3[][] result = inPlace ? vertexSets : new Vector3[vertexSets.Length][];

        for (int s = 0; s < vertexSets.Length; s++)
        {
            var set = vertexSets[s];
            if (set == null || set.Length == 0)
                throw new ArgumentException($"Vertex set at index {s} cannot be null or empty.", nameof(vertexSets));

            if (inPlace)
            {
                // Mutate the existing inner array
                Rotate(set, rotation, pivot, inPlace: true);
            }
            else
            {
                // Produce a rotated copy of the inner array
                result[s] = Rotate(set, rotation, pivot, inPlace: false);
            }
        }

        return result;
    }

    /// <summary>
    /// Rotates multiple vertex arrays around a pivot using Euler angles.
    /// </summary>
    public static Vector3[][] RotateMany(Vector3[][] vertexSets, Vector3 eulerAngles, Vector3 pivot, bool inPlace = false)
        => RotateMany(vertexSets, Quaternion.Euler(eulerAngles), pivot, inPlace);

    // Translations
    /// <summary>
    /// Translates multiple vertex arrays by the same offset. Optionally in-place.
    /// </summary>
    public static Vector3[][] TranslateMany(Vector3[][] vertexSets, Vector3 offset, bool inPlace = false)
    {
        if (vertexSets == null || vertexSets.Length == 0)
            throw new ArgumentException("Vertex set collection cannot be null or empty.", nameof(vertexSets));

        Vector3[][] result = inPlace ? vertexSets : new Vector3[vertexSets.Length][];

        for (int s = 0; s < vertexSets.Length; s++)
        {
            var set = vertexSets[s];
            if (set == null || set.Length == 0)
                throw new ArgumentException($"Vertex set at index {s} cannot be null or empty.", nameof(vertexSets));

            if (inPlace)
            {
                Translate(set, offset, inPlace: true);
            }
            else
            {
                result[s] = Translate(set, offset, inPlace: false);
            }
        }

        return result;
    }

    /// <summary>
    /// Translates a collection of vertex sets so that their local origin (0,0,0)
    /// moves to a given world-space point. Optionally operates in-place.
    /// </summary>
    /// <param name="vertexSets">Array of vertex arrays (each vertex set represents a sub-mesh or shape).</param>
    /// <param name="targetPoint">World-space coordinate to move each set’s origin to.</param>
    /// <param name="inPlace">If true, modifies vertexSets directly.</param>
    /// <returns>A new translated array of vertex sets, or the same reference if inPlace is true.</returns>
    public static Vector3[][] TranslateManyToPoint(Vector3[][] vertexSets, Vector3 targetPoint, bool inPlace = false)
    {
        if (vertexSets == null || vertexSets.Length == 0)
            throw new ArgumentException("Vertex set collection cannot be null or empty.", nameof(vertexSets));

        Vector3[][] result = inPlace ? vertexSets : new Vector3[vertexSets.Length][];

        for (int s = 0; s < vertexSets.Length; s++)
        {
            var set = vertexSets[s];
            if (set == null || set.Length == 0)
                throw new ArgumentException($"Vertex set at index {s} cannot be null or empty.", nameof(vertexSets));

            // Assume the "origin" of this set is the position of its first vertex (index 0).
            // You could also compute the centroid if preferred.
            Vector3 origin = set[0];
            Vector3 offset = targetPoint - origin;

            if (inPlace)
            {
                Translate(set, offset, inPlace: true);
            }
            else
            {
                result[s] = Translate(set, offset, inPlace: false);
            }
        }

        return result;
    }
}