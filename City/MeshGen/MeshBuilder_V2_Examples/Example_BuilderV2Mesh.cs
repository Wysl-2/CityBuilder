using System;
using System.Collections;
using System.Collections.Generic;
using ProcGen;
using UnityEngine;

public class Example_BuilderV2Mesh : MonoBehaviour
{
    public Material material;

    void Start()
    {
        var tag = GetComponent<MeshAuthoringTag>() ?? gameObject.AddComponent<MeshAuthoringTag>();
        var sink = new MeshAuthoringTagSink(tag);
        var b = new MeshBuilder_V2 { AuthoringSink = sink };
        // UV Variables
        Vector2 uv0, uv1, uv2, uv3;

        // Simple Quad on XZ plane (flat)
        Quad_V2 quad = new Quad_V2(
            v0: new Vector3(0, 0, 0),
            v1: new Vector3(1, 0, 0),
            v2: new Vector3(1, 0, 1),
            v3: new Vector3(0, 0, 1)
        );
        b.AddQuadByPos(quad);

        Vector3 dir = DirectionUtil.DirectionBetweenByAngle(Vector3.forward, Vector3.up, 45); 
        Quad_V2 forwardExtrusion = quad.CreateQuadFromExtrusion(QuadEdge.V2V3, dir, 1);

        b.AddQuadByPos(forwardExtrusion);

        Quad_V2 forwardExtrusion2 = forwardExtrusion.CreateQuadFromExtrusion(QuadEdge.V2V3, Vector3.forward, 1);
        b.AddQuadByPos(forwardExtrusion2);


        Quad_V2 backExtrusion = quad.CreateQuadFromExtrusion(QuadEdge.V0V1, Vector3.back, 1);
        b.AddQuadByPos(backExtrusion);



        var mesh = b.ToMesh();
        GetComponent<MeshFilter>().sharedMesh = mesh;
        var col = GetComponent<MeshCollider>();
        if (col) col.sharedMesh = mesh;
        if (material != null) GetComponent<MeshRenderer>().sharedMaterial = material;
    }
}


        // UVMapping.QuadPlanarUVs(
        //     angledQuad.v0, angledQuad.v1, angledQuad.v2, angledQuad.v3,
        //     out uv0, out uv1, out uv2, out uv3,
        //     metersPerTileU: 1f,                 // 1m per UV along U
        //     metersPerTileV: 1f,                 // 1m per UV along V
        //     uOffset: 0f, vOffset: 0f,           // slide pattern if you need alignment
        //     axisMode: UVMapping.QuadAxisMode.Edge01, // U follows seam v0->v1
        //     originMode: UVMapping.QuadOrigin.V0,     // tile seam anchored at v0
        //     followWinding: true,                      // keep U direction consistent with v0->v1
        //     rotationDegrees: 0f                       // rotate UVs within the face if desired
        // );


namespace ProcGen
{
    public static class DirectionUtil
    {
        const float EPS = 1e-8f;

        /// <summary>
        /// Step from 'fromDir' toward 'toDir' by 'angleDeg' along the shortest arc.
        /// Returns a normalized direction. If angleDeg >= angle(from,to), returns toDir normalized.
        /// </summary>
        public static Vector3 DirectionBetweenByAngle(Vector3 fromDir, Vector3 toDir, float angleDeg)
        {
            // Handle degenerate inputs
            bool aZero = fromDir.sqrMagnitude < EPS;
            bool bZero = toDir.sqrMagnitude   < EPS;
            if (aZero && bZero) return Vector3.zero;
            if (aZero) return toDir.normalized;
            if (bZero) return fromDir.normalized;

            Vector3 a = fromDir.normalized;
            Vector3 b = toDir.normalized;

            // Angle between (0..π)
            float cos = Mathf.Clamp(Vector3.Dot(a, b), -1f, 1f);
            float theta = Mathf.Acos(cos); // radians
            if (theta < 1e-6f) return a;   // already aligned

            // Clamp requested step to available arc
            float stepRad = Mathf.Clamp(angleDeg, 0f, theta * Mathf.Rad2Deg) * Mathf.Deg2Rad;
            float t = stepRad / theta;

            // Slerp for great-circle interpolation; Lerp fallback for tiny angles
            return (theta < 1e-3f) ? Vector3.Lerp(a, b, t).normalized
                                   : Vector3.Slerp(a, b, t).normalized;
        }
    }
}