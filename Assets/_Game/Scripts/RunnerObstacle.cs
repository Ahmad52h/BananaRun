using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
[RequireComponent(typeof(Rigidbody))]
public class RunnerObstacle : MonoBehaviour
{
    private bool touched;

    private void Awake()
    {
        GetComponent<BoxCollider>().isTrigger = true;

        Rigidbody body = GetComponent<Rigidbody>();
        body.useGravity = false;
        body.isKinematic = true;
    }

    private void OnEnable()
    {
        touched = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (touched)
            return;

        PlayerHealth health =
            other.GetComponentInParent<PlayerHealth>();

        if (health == null || health.IsDead)
            return;

        touched = true;
        health.TakeHit();
    }
}