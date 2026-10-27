using UnityEngine;

public class CoconutProjectile : MonoBehaviour
{
    [SerializeField, Min(1f)] private float speed = 25f;
    [SerializeField, Min(1f)] private float maxDistance = 30f;
    [SerializeField, Min(0.01f)] private float radius = 0.25f;
    [SerializeField] private LayerMask obstacleMask;
    [SerializeField] private Vector3 spinDegreesPerSecond = new Vector3(210f, 330f, 120f);
    [Header("Smash Audio")]
    [SerializeField] private AudioClip smashBoxSound;
    [SerializeField, Range(0f, 1f)] private float smashVolume = 1f;

    private PlayerHealth owner;
    private float travelled;

    public void Initialize(PlayerHealth player)
    {
        owner = player;
    }

    private void Update()
    {
        transform.Rotate(spinDegreesPerSecond * Time.deltaTime, Space.Self);
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
        var rival = other.GetComponentInParent<RivalRunner>();
        if (rival != null) rival.TakeProjectileHit();
        var health = other.GetComponentInParent<PlayerHealth>();
        if (health != null && health != owner) health.TakeProjectileHit();
        RunnerObstacle obstacle =
            other.GetComponentInParent<RunnerObstacle>();

        if (obstacle != null)
        {
            if (obstacle.TryBreak() && smashBoxSound != null)
                AudioSource.PlayClipAtPoint(smashBoxSound, obstacle.transform.position, smashVolume);
        }

        Destroy(gameObject);
    }
}
