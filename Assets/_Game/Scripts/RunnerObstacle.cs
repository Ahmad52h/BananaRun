using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
[RequireComponent(typeof(Rigidbody))]
public class RunnerObstacle : MonoBehaviour
{
    [Header("Obstacle Type")]
    [Tooltip("تحديد هل هذا العائق مخصص للتزحلق من تحته")]
    [SerializeField] private bool canSlideUnder;

    [SerializeField] private bool breakable;
    [Header("Break Rewards")]
    [SerializeField] private CrateBreakEffect breakEffectPrefab;
    [SerializeField] private GameObject bananaPrefab;
    [SerializeField] private CoconutPickup coconutPrefab;
    [SerializeField] private Transform bananaSpawnPoint;
    [SerializeField] private Transform bananaLandingPoint;

    private bool touched;
    private bool broken;

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
        touched = true;
        health.TakeHit();
    }

    private void OnTriggerStay(Collider other) => OnTriggerEnter(other);

    public bool TryBreak()
    {
        if (!breakable || broken)
            return false;

        Shatter();
        return true;
    }

    public void Shatter()
    {
        if (broken) return;

        BoxCollider hitbox = GetComponent<BoxCollider>();
        if (hitbox == null) { gameObject.SetActive(false); return; }
        broken = true;

        var powers = PowerUpManager.ActivePlayer;
        if (powers != null && powers.IsActive(PowerUpType.Bulldozer))
            powers.PlayCrushEffect(hitbox.bounds.center);

        Bounds bounds = hitbox.bounds;

        // Stop collisions immediately.
        hitbox.enabled = false;

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
        if (bananaPrefab != null &&
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

        if (coconutPrefab != null)
        {
            Vector3 rewardPosition = new Vector3(bounds.center.x, bounds.min.y + 0.8f, bounds.max.z + 2.4f);
            Instantiate(coconutPrefab, rewardPosition, Quaternion.identity);
        }

        gameObject.SetActive(false);
    }
}
