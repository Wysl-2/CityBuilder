using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using KinematicCharacterController;

public class SimplePlatformMover : MonoBehaviour, IMoverController
{
    public Transform pointA;
    public Transform pointB;
    public float speed = 2f;

    PhysicsMover mover;
    Vector3 A, B;
    float t;      // 0..1 along A->B
    int dir = 1;  // +1 going to B, -1 going to A

    void Awake()
    {
        mover = GetComponent<PhysicsMover>();
        mover.MoverController = this; // KCC will call UpdateMovement on us

        var rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;        // required for PhysicsMover platforms
        rb.interpolation = RigidbodyInterpolation.None;

        A = pointA ? pointA.position : transform.position;
        B = pointB ? pointB.position : transform.position + Vector3.right * 3f;
    }

    public void UpdateMovement(out Vector3 goalPos, out Quaternion goalRot, float dt)
    {
        float distance = Vector3.Distance(A, B);
        float duration = Mathf.Max(0.0001f, distance / Mathf.Max(0.0001f, speed));
        t += (dt / duration) * dir;

        if (t >= 1f) { t = 1f; dir = -1; }
        if (t <= 0f) { t = 0f; dir = +1; }

        goalPos = Vector3.Lerp(A, B, t);
        goalRot = transform.rotation; // no rotation; translate only
    }
}
