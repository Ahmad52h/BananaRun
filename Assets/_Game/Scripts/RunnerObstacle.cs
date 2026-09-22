using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
[RequireComponent(typeof(Rigidbody))]
public class RunnerObstacle : MonoBehaviour
{
    [SerializeField] private bool breakable;

    private bool touched;
    private bool broken;

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
        broken = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (touched || broken)
            return;

        PlayerHealth health =
            other.GetComponentInParent<PlayerHealth>();

        if (health == null || health.IsRunOver)
            return;

        touched = true;
        health.TakeHit();
    }

    public void TryBreak()
    {
        if (!breakable || broken)
            return;

        broken = true;
        gameObject.SetActive(false);
    }
}