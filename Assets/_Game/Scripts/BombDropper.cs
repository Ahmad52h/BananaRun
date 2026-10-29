using UnityEngine;

/// <summary>Secondary ammo, independent of coconut inventory and throw cooldown.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerHealth), typeof(RunnerMotor))]
public sealed class BombDropper : MonoBehaviour
{
    [SerializeField] private RottenBananaBomb trapPrefab;
    [SerializeField, Min(1)] private int maxBombs = 3;
    [SerializeField, Min(0)] private int startingBombs;
    [SerializeField, Min(1f)] private float dropDistance = 1.8f;
    [SerializeField, Min(0f)] private float dropCooldown = 0.45f;
    private PlayerHealth health;
    private RunnerMotor motor;
    private RunnerAnimation animationController;
    private float nextDrop;
    private readonly RaycastHit[] groundHits = new RaycastHit[32];

    public int CurrentBombs { get; private set; }
    public int MaxBombs => maxBombs;
    public RottenBananaBomb TrapPrefab => trapPrefab;
    public event System.Action<int> CountChanged;
    public bool CanDrop => !RaceStartSequence.IsHolding && isActiveAndEnabled && CurrentBombs > 0 && trapPrefab != null &&
        health != null && !health.IsRunOver && !health.IsProjectileStunned &&
        motor != null && motor.isActiveAndEnabled && Time.timeScale > 0f && Time.time >= nextDrop &&
        (animationController == null || (!animationController.IsReactingToHit && !animationController.IsVineControlled));

    private void Awake()
    {
        health = GetComponent<PlayerHealth>();
        motor = GetComponent<RunnerMotor>();
        animationController = GetComponent<RunnerAnimation>();
        CurrentBombs = Mathf.Clamp(startingBombs, 0, maxBombs);
    }

    public bool TryCollect()
    {
        if (health == null || health.IsRunOver || CurrentBombs >= maxBombs) return false;
        CurrentBombs++;
        CountChanged?.Invoke(CurrentBombs);
        return true;
    }

    // Public void entry point also supports a serialized UI Button event.
    public void Drop() => TryDrop();

    public bool TryDrop()
    {
        if (!CanDrop) return false;
        Vector3 point = transform.position + Vector3.back * dropDistance;
        int count = Physics.RaycastNonAlloc(point + Vector3.up * 1.5f, Vector3.down,
            groundHits, 12f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float distance = float.PositiveInfinity;
        bool grounded = false;
        for (int i = 0; i < count; i++)
        {
            var hit = groundHits[i];
            if (hit.collider.GetComponentInParent<RunnerMotor>() != null ||
                hit.collider.GetComponentInParent<RivalRunner>() != null ||
                hit.collider.GetComponentInParent<RunnerObstacle>() != null || hit.normal.y < 0.65f ||
                hit.distance >= distance) continue;
            point.y = hit.point.y;
            distance = hit.distance;
            grounded = true;
        }
        // Do not spend ammo over a river, missing ground, or during a vine crossing.
        var river = RiverCrocodileGauntlet.Active;
        if (!grounded || (river != null && river.IsInsideRiverGap(point.z))) return false;
        var trap = Instantiate(trapPrefab, point, Quaternion.identity);
        trap.SetOwner(transform);
        RunnerAudio.Instance?.PlayBombDrop();
        CurrentBombs--;
        nextDrop = Time.time + dropCooldown;
        CountChanged?.Invoke(CurrentBombs);
        return true;
    }
}
