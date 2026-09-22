using UnityEngine;

public class CoconutProjectile : MonoBehaviour
{
    [SerializeField, Min(1f)] private float speed = 25f;
    [SerializeField, Min(1f)] private float maxDistance = 30f;
    [SerializeField, Min(0.01f)] private float radius = 0.25f;
    [SerializeField] private LayerMask obstacleMask;

    private PlayerHealth owner;
    private float travelled;

    public void Initialize(PlayerHealth player)
    {
        owner = player;
    }

    private void Update()
    {
        if (owner == null || owner.IsRunOver)
        {
            Destroy(gameObject);
            return;
        }

        // Handle spawning very close to an obstacle.
        Collider[] overlaps = Physics.OverlapSphere(
            transform.position,
            radius,
            obstacleMask,
            QueryTriggerInteraction.Collide
        );

        if (overlaps.Length > 0)
        {
            Hit(overlaps[0]);
            return;
        }

        float step = Mathf.Min(
            speed * Time.deltaTime,
            maxDistance - travelled
        );

        if (Physics.SphereCast(
            transform.position,
            radius,
            Vector3.forward,
            out RaycastHit hit,
            step,
            obstacleMask,
            QueryTriggerInteraction.Collide))
        {
            Hit(hit.collider);
            return;
        }

        transform.position += Vector3.forward * step;
        travelled += step;

        if (travelled >= maxDistance)
        {
            Destroy(gameObject);
        }
    }

    private void Hit(Collider other)
    {
        RunnerObstacle obstacle =
            other.GetComponentInParent<RunnerObstacle>();

        if (obstacle != null)
        {
            obstacle.TryBreak();
        }

        Destroy(gameObject);
    }
}