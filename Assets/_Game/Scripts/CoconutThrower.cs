using UnityEngine;

[RequireComponent(typeof(PlayerHealth))]
public class CoconutThrower : MonoBehaviour
{
    [SerializeField] private CoconutProjectile projectilePrefab;
    [SerializeField] private BallisticRivalProjectile competitiveProjectilePrefab;
    [SerializeField] private RivalRunner rival;
    [SerializeField, Min(1f)] private float rivalThrowRange = 24f;
    [SerializeField, Min(0.25f)] private float rivalFlightTime = 0.85f;
    [SerializeField, Min(0f)] private float throwCooldown = 0.65f;
    [SerializeField, Min(1f)] private float forwardThrowDistance = 18f;
    [SerializeField, Min(0.25f)] private float forwardFlightTime = 0.65f;

    [SerializeField]
    private Vector3 spawnOffset = new Vector3(0f, 1f, 0.7f);

    [Header("Ammo Settings")]
    [SerializeField] private int maxCoconuts = 5;
    [SerializeField, Min(0)] private int startingCoconuts = 2;

    private int currentCoconuts = 0;
    private PlayerHealth health;
    private RunnerAnimation runnerAnimation;
    private float nextThrow;
    private readonly Collider[] forwardObstacles = new Collider[64];
    private RunnerMotor motor;

    public bool HasCoconut => currentCoconuts > 0;
    public int CurrentCoconuts => currentCoconuts;
    public int MaxCoconuts => maxCoconuts;
    public event System.Action<int> CountChanged;

    public bool CanThrow =>
        !RaceStartSequence.IsHolding &&
        HasCoconut &&
        health != null &&
        !health.IsRunOver &&
        !health.IsProjectileStunned &&
        Time.time >= nextThrow && (runnerAnimation == null || (!runnerAnimation.IsReactingToHit && !runnerAnimation.IsVineControlled)) &&
        (projectilePrefab != null || competitiveProjectilePrefab != null);

    private void Awake()
    {
        health = GetComponent<PlayerHealth>();
        runnerAnimation = GetComponent<RunnerAnimation>();
        motor = GetComponent<RunnerMotor>();
        currentCoconuts = Mathf.Clamp(startingCoconuts, 0, maxCoconuts);
    }

    private void Start()
    {
        if (rival != null) return;
        foreach (var competitor in FindObjectsByType<RivalRunner>())
            if (competitor.Player == GetComponent<RunnerMotor>()) { rival = competitor; break; }
    }

    public bool TryCollect()
    {
        if (health == null || health.IsRunOver || currentCoconuts >= maxCoconuts)
            return false;

        currentCoconuts++;
        CountChanged?.Invoke(currentCoconuts);
        return true;
    }

    public void Throw()
    {
        if (!CanThrow)
            return;

        currentCoconuts--;
        nextThrow = Time.time + throwCooldown;
        CountChanged?.Invoke(currentCoconuts);

        // تشغيل أنيميشن الرمي باليدين تلقائياً دون تشويه
        if (runnerAnimation != null)
        {
            runnerAnimation.TriggerThrow(0.5f);
        }

        if (competitiveProjectilePrefab != null)
        {
            var fruit = Instantiate(competitiveProjectilePrefab, transform.position + spawnOffset, Quaternion.identity);
            if (rival != null && rival.isActiveAndEnabled && !rival.HasFinished &&
                Mathf.Abs(rival.transform.position.z - transform.position.z) <= rivalThrowRange)
            {
                var capsule = rival.GetComponent<CharacterController>();
                Vector3 destination = new Vector3(rival.ActiveLaneWorldX,
                    rival.transform.position.y + capsule.center.y,
                    rival.transform.position.z + rival.CurrentSpeed * rivalFlightTime);
                if (destination.z > fruit.transform.position.z + 0.5f)
                {
                    fruit.LaunchFromPlayer(health, rival, destination, rivalFlightTime);
                    return;
                }
            }
            fruit.LaunchForward(health, ForwardTarget(), forwardFlightTime);
            return;
        }
        if (projectilePrefab == null)
        {
            currentCoconuts++;
            CountChanged?.Invoke(currentCoconuts);
            return;
        }
        CoconutProjectile projectile = Instantiate(
            projectilePrefab,
            transform.position + spawnOffset,
            Quaternion.identity
        );

        projectile.Initialize(health);
    }

    private Vector3 ForwardTarget()
    {
        float laneX = motor != null ? motor.ActiveLaneWorldX : transform.position.x;
        Vector3 origin = transform.position + spawnOffset;
        Vector3 center = new Vector3(laneX, origin.y, transform.position.z + forwardThrowDistance * 0.5f);
        int count = Physics.OverlapBoxNonAlloc(center, new Vector3(0.65f, 1.5f, forwardThrowDistance * 0.5f),
            forwardObstacles, Quaternion.identity, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
        Collider nearest = null;
        for (int i = 0; i < count; i++)
        {
            var obstacle = forwardObstacles[i].GetComponentInParent<RunnerObstacle>();
            if (obstacle == null || !obstacle.CanBreakFromProjectile || forwardObstacles[i].bounds.min.z <= origin.z) continue;
            if (nearest == null || forwardObstacles[i].bounds.min.z < nearest.bounds.min.z) nearest = forwardObstacles[i];
        }
        if (nearest != null)
        {
            var bounds = nearest.bounds;
            return new Vector3(Mathf.Clamp(laneX, bounds.min.x, bounds.max.x),
                Mathf.Clamp(origin.y, bounds.min.y + 0.05f, bounds.max.y - 0.05f), bounds.center.z);
        }
        return new Vector3(laneX, origin.y, transform.position.z + forwardThrowDistance);
    }
}
