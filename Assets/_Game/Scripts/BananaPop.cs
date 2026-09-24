using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class BananaPop : MonoBehaviour
{
    private Rigidbody body;
    private Vector3 start;
    private Vector3 destination;

    private float elapsed;
    private float duration;
    private float arcHeight;
    private bool initialized;

    public void Initialize(
        Vector3 target,
        float popDuration = 0.45f,
        float height = 0.45f)
    {
        body = GetComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        body.interpolation = RigidbodyInterpolation.Interpolate;

        start = body.position;
        destination = target;
        duration = Mathf.Max(0.05f, popDuration);
        arcHeight = Mathf.Max(0f, height);

        elapsed = 0f;
        initialized = true;
    }

    private void FixedUpdate()
    {
        if (!initialized)
            return;

        elapsed += Time.fixedDeltaTime;
        float t = Mathf.Clamp01(elapsed / duration);

        Vector3 position = Vector3.Lerp(start, destination, t);
        position.y += Mathf.Sin(t * Mathf.PI) * arcHeight;

        body.MovePosition(position);

        if (t >= 1f)
        {
            initialized = false;
            enabled = false;
        }
    }
}