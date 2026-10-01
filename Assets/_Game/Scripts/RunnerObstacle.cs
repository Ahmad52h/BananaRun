using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
[RequireComponent(typeof(Rigidbody))]
public class RunnerObstacle : MonoBehaviour
{
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
        GetComponent<BoxCollider>().isTrigger = true;

        Rigidbody body = GetComponent<Rigidbody>();
        body.useGravity = false;
        body.isKinematic = true;
    }

    private void OnEnable()
    {
        touched = false;
        broken = false;
        GetComponent<BoxCollider>().enabled = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (touched || broken)
            return;

        PlayerHealth health =
            other.GetComponentInParent<PlayerHealth>();

        if (health == null || health.IsRunOver)
            return;

        var powers = health.GetComponent<PowerUpManager>();
        if (powers != null && powers.IsActive(PowerUpType.Bulldozer))
        {
            powers.AwardBulldozerPoints();
            Shatter();
            return;
        }

        touched = true;
        health.TakeHit();
    }

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

        broken = true;

        BoxCollider hitbox = GetComponent<BoxCollider>();
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
            // Drop it beyond the crate, in the same lane and at pickup height.
            Vector3 rewardPosition = new Vector3(bounds.center.x, bounds.min.y + 0.8f, bounds.max.z + 2.4f);
            Instantiate(coconutPrefab, rewardPosition, Quaternion.identity);
        }

        gameObject.SetActive(false);
    }
}
