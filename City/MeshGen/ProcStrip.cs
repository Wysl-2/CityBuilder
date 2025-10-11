// // ProcStrip.cs
// using UnityEngine;
// using System.Collections.Generic;
// using ProcGen;

// [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
// public class ProcStrip : MonoBehaviour {
//     public List<Transform> path = new(); // ordered points in scene
//     public float width = 6f;
//     public bool loop;
//     public float uvTilesPerMeter = 0.25f; // tweak texture repeats

//     void Awake() { Build(); }
//     void OnValidate() { if (Application.isPlaying) Build(); }

//     void Build() {
//         if (path == null || path.Count < 2) return;
//         var mb = new SimpleMeshBuilder();

//         // Collect world points and cumulative distances
//         var pts = new List<Vector3>(path.Count);
//         foreach (var t in path) if (t) pts.Add(t.position);
//         if (pts.Count < 2) return;

//         float halfW = width * 0.5f;
//         float accLen = 0f;

//         Vector3 up = Vector3.up;
//         Vector3 prevLeft = default, prevRight = default;
//         bool first = true;

//         for (int i = 0; i < pts.Count - 1 + (loop ? 1 : 0); i++) {
//             Vector3 a = pts[i % pts.Count];
//             Vector3 b = pts[(i + 1) % pts.Count];
//             Vector3 t = (b - a);
//             float segLen = t.magnitude;
//             if (segLen < 1e-4f) continue;
//             t /= segLen;
//             Vector3 s = Vector3.Normalize(Vector3.Cross(up, t)); // left/right
//             Vector3 left  = a - s * halfW;
//             Vector3 right = a + s * halfW;

//             // Add two verts at this station
//             int baseIndex = mb.vertices.Count;
//             mb.vertices.Add(left);
//             mb.vertices.Add(right);
//             mb.normals.Add(up); mb.normals.Add(up);

//             float vTex = accLen * uvTilesPerMeter;
//             mb.uvs.Add(new Vector2(0f, vTex));
//             mb.uvs.Add(new Vector2(1f, vTex));

//             if (!first) {
//                 // stitch quad from previous pair to current pair
//                 int i0 = baseIndex - 2; // prev left
//                 int i1 = baseIndex - 1; // prev right
//                 int i2 = baseIndex;     // curr left
//                 int i3 = baseIndex + 1; // curr right
//                 mb.AddQuad(i0, i1, i2, i3);
//             }
//             first = false;
//             accLen += segLen;

//             // On the last iteration for non-loop, also add the end pair at 'b'
//             if (!loop && i == pts.Count - 2) {
//                 Vector3 leftB  = b - s * halfW;
//                 Vector3 rightB = b + s * halfW;
//                 int bi = mb.vertices.Count;
//                 mb.vertices.Add(leftB);
//                 mb.vertices.Add(rightB);
//                 mb.normals.Add(up); mb.normals.Add(up);
//                 float vTexB = accLen * uvTilesPerMeter;
//                 mb.uvs.Add(new Vector2(0f, vTexB));
//                 mb.uvs.Add(new Vector2(1f, vTexB));
//                 // stitch last quad
//                 mb.AddQuad(bi - 2, bi - 1, bi, bi + 1);
//             }
//         }

//         GetComponent<MeshFilter>().sharedMesh = mb.ToMesh(false);
//     }
// }
