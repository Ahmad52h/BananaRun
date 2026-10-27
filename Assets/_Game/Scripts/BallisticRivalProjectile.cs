using UnityEngine;

/// <summary>A spinning held fruit, then a fixed ballistic arc with swept collision checks.</summary>
public sealed class BallisticRivalProjectile : MonoBehaviour
{
    [SerializeField, Min(0.01f)] private float radius = 0.22f;
    [SerializeField, Min(0.1f)] private float gravity = 18f;
    [SerializeField] private Vector3 spinDegreesPerSecond = new Vector3(210f, 330f, 120f);
    [SerializeField] private AudioClip obstacleHitSound;
    [SerializeField, Range(0f, 1f)] private float obstacleHitVolume = 1f;
    private readonly RaycastHit[] hits = new RaycastHit[32];
    private readonly Collider[] overlaps = new Collider[16];
    private RivalRunner owner;
    private PlayerHealth target;
    private PlayerHealth playerOwner;
    private RivalRunner rivalTarget;
    private Transform shooter;
    private Vector3 launchPosition, initialVelocity;
    private bool spent;
    private bool forwardShot;

    public bool IsLaunched { get; private set; }
    public bool IsPlayerShot => playerOwner != null;
    public Vector3 TargetPoint { get; private set; }
    public Vector3 LaunchPosition => launchPosition;
    public Vector3 InitialVelocity => initialVelocity;
    public Vector3 Gravity => Vector3.down * gravity;
    public float FlightTime { get; private set; }
    public float ElapsedTime { get; private set; }

    public static Vector3 CalculateLaunchVelocity(Vector3 start, Vector3 end, Vector3 acceleration, float duration) =>
        (end - start - 0.5f * acceleration * duration * duration) / Mathf.Max(0.05f, duration);

    public Vector3 PositionAt(float time) => launchPosition + initialVelocity * time + 0.5f * Gravity * time * time;

    public void Launch(RivalRunner thrower, PlayerHealth player, Vector3 destination, float duration)
    {
        if (IsLaunched || spent) return;
        owner = thrower;
        target = player;
        shooter = thrower != null ? thrower.transform : null;
        LaunchArc(destination, duration);
    }

    public void LaunchFromPlayer(PlayerHealth thrower, RivalRunner rival, Vector3 destination, float duration)
    {
        if (IsLaunched || spent) return;
        playerOwner = thrower;
        rivalTarget = rival;
        shooter = thrower != null ? thrower.transform : null;
        LaunchArc(destination, duration);
    }

    public void LaunchForward(PlayerHealth thrower, Vector3 destination, float duration)
    {
        if (IsLaunched || spent) return;
        playerOwner = thrower;
        shooter = thrower != null ? thrower.transform : null;
        forwardShot = true;
        LaunchArc(destination, duration);
    }

    private void LaunchArc(Vector3 destination, float duration)
    {
        TargetPoint = destination;
        FlightTime = Mathf.Max(0.25f, duration);
        transform.SetParent(null, true);
        launchPosition = transform.position;
        initialVelocity = CalculateLaunchVelocity(launchPosition, destination, Gravity, FlightTime);
        IsLaunched = true;
    }

    private void Update()
    {
        if (spent) return;
        transform.Rotate(spinDegreesPerSecond * Time.deltaTime, Space.Self);
        if (!IsLaunched) return;
        bool invalidShot = IsPlayerShot
            ? playerOwner.IsRunOver || (!forwardShot && (rivalTarget == null || rivalTarget.HasFinished))
            : owner == null || owner.HasFinished || target == null || target.IsRunOver;
        if (shooter == null || invalidShot)
        {
            Retire();
            return;
        }
        int count = Physics.OverlapSphereNonAlloc(transform.position, radius, overlaps,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
        for (int i = 0; i < count; i++)
            if (CanHit(overlaps[i])) { Hit(overlaps[i]); return; }

        float endTime = Mathf.Min(ElapsedTime + Time.deltaTime, FlightTime + 0.75f);
        // Subdivide the parabola so fast fruit cannot tunnel through a runner or obstacle.
        while (ElapsedTime < endTime)
        {
            float nextTime = Mathf.Min(endTime, ElapsedTime + 0.02f);
            Vector3 nextPosition = PositionAt(nextTime);
            Vector3 step = nextPosition - transform.position;
            float distance = step.magnitude;
            int hitCount = distance > 0.00001f ? Physics.SphereCastNonAlloc(transform.position, radius,
                step / distance, hits, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide) : 0;
            Collider first = null;
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < hitCount; i++)
                if (hits[i].distance < nearest && CanHit(hits[i].collider))
                { first = hits[i].collider; nearest = hits[i].distance; }
            if (first != null) { Hit(first); return; }
            transform.position = nextPosition;
            ElapsedTime = nextTime;
        }
        if (ElapsedTime >= FlightTime + 0.75f) Retire();
    }

    private bool CanHit(Collider collider)
    {
        if (collider == null || !collider.enabled || collider.transform.IsChildOf(transform) ||
            (shooter != null && collider.transform.IsChildOf(shooter))) return false;
        return (target != null && collider.GetComponentInParent<PlayerHealth>() == target) ||
            (rivalTarget != null && collider.GetComponentInParent<RivalRunner>() == rivalTarget) || !collider.isTrigger ||
            collider.GetComponentInParent<RunnerObstacle>() != null;
    }

    private void Hit(Collider collider)
    {
        if (target != null && collider.GetComponentInParent<PlayerHealth>() == target) target.TakeProjectileHit();
        else if (IsPlayerShot && collider.GetComponentInParent<RivalRunner>() is RivalRunner hitRival) hitRival.TakeProjectileHit();
        else if (IsPlayerShot)
        {
            var obstacle = collider.GetComponentInParent<RunnerObstacle>();
            if (obstacle != null && obstacle.TryBreak() && obstacleHitSound != null)
                AudioSource.PlayClipAtPoint(obstacleHitSound, collider.transform.position, obstacleHitVolume);
        }
        Retire();
    }

    private void Retire()
    {
        spent = true;
        Destroy(gameObject);
    }
}
