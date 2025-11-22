using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]  // 👈 makes Update/Start run in Edit Mode as well
public class TrackCornerHelper : MonoBehaviour
{
    [SerializeField] private float radius;
    [SerializeField] private float resolution;
    [SerializeField] private Vector3 relativePos;

    [SerializeField] private List<Vector3> points = new List<Vector3>();

    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        points.Clear();
        if (resolution < 3) return; // need at least a triangle

        float step = 2f * Mathf.PI / resolution;
        for (int i = 0; i < resolution; i++)
        {
            float angle = i * step;
            float x = Mathf.Cos(angle) * radius;
            float z = Mathf.Sin(angle) * radius;
            points.Add(transform.position + relativePos + new Vector3(x, 0f, z));
        }
    }

    void OnDrawGizmos()
    {
        if (points == null || points.Count < 2) return;

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position + relativePos, 0.1f);

        for (int i = 0; i < points.Count; i++)
        {
            Gizmos.color = Color.green;
            Vector3 current = points[i];
            Vector3 next = points[(i + 1) % points.Count]; // loop around
            Gizmos.DrawLine(current, next);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(current, 0.1f);
        }
    }
}
