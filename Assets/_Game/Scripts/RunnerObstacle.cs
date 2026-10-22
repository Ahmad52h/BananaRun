using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
[RequireComponent(typeof(Rigidbody))]
public class RunnerObstacle : MonoBehaviour
{
    [Header("Obstacle Type")]
    [Tooltip("تحديد هل هذا العائق مخصص للتزحلق من تحته")]
    [SerializeField] private bool canSlideUnder;

    [SerializeField] private bool breakable;
    [Header("Normal Player Impact")]
    [Tooltip("Independent of coconut-projectile breakability. Special hazard components are always excluded.")]
    [SerializeField] private bool breakOnPlayerHit = true;
    [SerializeField] private GiantImpactEffect playerHitImpactPrefab;
    [Tooltip("Whole obstacle instance to remove, including wrapper visuals and colliders.")]
    [SerializeField] private GameObject obstacleRoot;
    [Header("Break Rewards")]
    [SerializeField] private CrateBreakEffect breakEffectPrefab;
    [SerializeField] private GameObject bananaPrefab;
    [SerializeField] private CoconutPickup coconutPrefab;
    [SerializeField] private Transform bananaSpawnPoint;
    [SerializeField] private Transform bananaLandingPoint;

    private bool touched;
    private bool broken;
    public bool BreaksOnPlayerHit => breakOnPlayerHit &&
        GetComponentInParent<DynamicRunnerHazard>(true) == null &&
        GetComponentInChildren<DynamicRunnerHazard>(true) == null &&
        GetComponentInParent<FallingCoconutHazard>(true) == null &&
        GetComponentInParent<BananaPeelHazard>(true) == null;

    private void Awake()
    {
        var hitbox = GetComponent<BoxCollider>();
        if (hitbox == null) { enabled = false; return; }
        hitbox.isTrigger = true;

        Rigidbody body = GetComponent<Rigidbody>();
        if (body != null) { body.useGravity = false; body.isKinematic = true; }

        // يتعرف تلقائياً إذا كان هذا العائق لوحاً مرتفعاً للتزحلق (مثل HighSlideBeam)
        string objName = gameObject.name.ToLower();
        if (objName.Contains("slide") || objName.Contains("beam") || objName.Contains("high"))
        {
            canSlideUnder = true;
        }
    }

    private void OnEnable()
    {
        touched = false;
        broken = false;
        var hitbox = GetComponent<BoxCollider>();
        if (hitbox != null) hitbox.enabled = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (broken)
            return;

        PlayerHealth health = other.GetComponentInParent<PlayerHealth>();
        if (health == null || health.IsRunOver)
            return;

        // 1. فحص القدرة الخارقة (Bulldozer)
        var powers = health.GetComponent<PowerUpManager>();
        if (powers != null && powers.IsActive(PowerUpType.Bulldozer))
        {
            powers.AwardBulldozerPoints();
            Shatter();
            return;
        }

        // 2. الحماية أثناء الزحلقة: إذا كان القرد يتزحلق والعائق مرتفع، يتجاهل الضرر ويمر بسلام!
        if (touched) return;
        RunnerMotor motor = other.GetComponentInParent<RunnerMotor>();
        if (canSlideUnder && motor != null && motor.IsSliding)
        {
            return; // عبور آمن 100% بدون أي ضرر!
        }

        // إذا كان القرد واقفاً ولم يتزحلق، يتأذى
        bool normalImpact = BreaksOnPlayerHit;
        if (normalImpact && health.IsProtected) return;
        touched = true;
        int previousHearts = health.Hearts;
        health.TakeHit();
        if (normalImpact && health.Hearts < previousHearts)
            BreakObstacle(true);
    }

    private void OnTriggerStay(Collider other) => OnTriggerEnter(other);

    public bool TryBreak()
    {
        if (!breakable || broken)
            return false;

        Shatter();
        return true;
    }

    // The chaser only breaks obstacles intended to be breakable. It does not
    // simulate a player collision or grant the crate's pickup rewards.
    public bool TryBreakFromChaser()
    {
        if (!breakable || broken)
            return false;

        BreakObstacle(true, false);
        return true;
    }

    public void Shatter()
    {
        BreakObstacle(false);
    }

    private void BreakObstacle(bool normalPlayerHit, bool spawnRewards = true)
    {
        if (broken) return;

        BoxCollider hitbox = GetComponent<BoxCollider>();
        if (hitbox == null) { gameObject.SetActive(false); return; }
        broken = true;

        Bounds bounds = hitbox.bounds;

        // Stop collisions immediately.
        hitbox.enabled = false;
        GameObject removalRoot = obstacleRoot != null && transform.IsChildOf(obstacleRoot.transform)
            ? obstacleRoot : gameObject;
        if (normalPlayerHit)
        {
            foreach (var collider in removalRoot.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            // Reuse Giant's existing particle burst and its single short smash sound.
            if (playerHitImpactPrefab != null) Instantiate(playerHitImpactPrefab, bounds.center, Quaternion.identity);
        }
        else
        {
            // Preserve the original Giant/projectile path and its existing audio ownership.
            var powers = PowerUpManager.ActivePlayer;
            if (powers != null && powers.IsActive(PowerUpType.Bulldozer))
                powers.PlayCrushEffect(bounds.center);
        }

        // Spawn the effect independently from this obstacle.
        if (breakEffectPrefab != null)
        {
            CrateBreakEffect effect = Instantiate(
                breakEffectPrefab,
                bounds.center,
                Quaternion.identity);

            effect.transform.localScale = bounds.size;
        }

        // Spawn one collectible banana.
        if (spawnRewards && bananaPrefab != null &&
            bananaSpawnPoint != null &&
            bananaLandingPoint != null)
        {
            Vector3 start = bananaSpawnPoint.position;
            Vector3 target = bananaLandingPoint.position;

            // Keep the reward in the same lane.
            target.x = start.x;

            GameObject banana = Instantiate(
                bananaPrefab,
                start,
                bananaPrefab.transform.rotation);

            BananaPop pop = banana.GetComponent<BananaPop>();

            if (pop == null)
                pop = banana.AddComponent<BananaPop>();

            pop.Initialize(target);
        }

        if (spawnRewards && coconutPrefab != null)
        {
            Vector3 rewardPosition = new Vector3(bounds.center.x, bounds.min.y + 0.8f, bounds.max.z + 2.4f);
            Instantiate(coconutPrefab, rewardPosition, Quaternion.identity);
        }

        if (normalPlayerHit)
        {
            // Independent fragments/particles remain visible after the intact mesh is removed.
            removalRoot.SetActive(false);
            Destroy(removalRoot);
        }
        else gameObject.SetActive(false);
    }
}
