using UnityEngine;

[RequireComponent(typeof(SphereCollider))]
[RequireComponent(typeof(Rigidbody))]
public class BananaPickup : MonoBehaviour
{
    [SerializeField, Min(1)] private int value = 1;
    [SerializeField] private Transform visual;
    [SerializeField] private float rotationSpeed = 90f;

    [Header("Pickup Effect")]
    [SerializeField] private ParticleSystem pickupEffectPrefab;
    [Header("Audio")]
    [Tooltip("Optional per-pickup override; otherwise uses RunnerAudio's banana clip.")]
    [SerializeField] private AudioClip collectSound;
    [SerializeField, Min(1f)] private float magnetPullSpeed = 19f;

    private Renderer[] visualRenderers;
    private bool collected;
    public int Value => value;

    private void Awake()
    {
        visualRenderers = visual != null
            ? visual.GetComponentsInChildren<Renderer>()
            : new Renderer[0];

        GetComponent<SphereCollider>().isTrigger = true;

        Rigidbody body = GetComponent<Rigidbody>();
        body.useGravity = false;
        body.isKinematic = true;
    }

    private void OnEnable()
    {
        collected = false;
    }

    private void Update()
    {
        if (!collected && TryGetMagnetTarget(out Vector3 target, out float radius) &&
            (target - transform.position).sqrMagnitude <= radius * radius)
        {
            transform.position = Vector3.MoveTowards(transform.position, target, magnetPullSpeed * Time.deltaTime);
        }
        if (visual != null)
        {
            visual.Rotate(
                Vector3.up,
                rotationSpeed * Time.deltaTime,
                Space.World
            );
        }
    }

    private bool TryGetMagnetTarget(out Vector3 target, out float radius)
    {
        Vector3 bestTarget = Vector3.zero;
        float bestRadius = 0f;
        float nearest = float.MaxValue;

        var player = PowerUpManager.ActivePlayer;
        if (player != null && player.isActiveAndEnabled && player.IsActive(PowerUpType.Magnet))
            ConsiderMagnet(player.transform.position + Vector3.up * 0.9f, player.MagnetRadius);

        var rival = RivalRunner.ActiveRival;
        if (rival != null && rival.isActiveAndEnabled && rival.HasMagnet)
            ConsiderMagnet(rival.transform.position + Vector3.up * 0.9f, rival.MagnetRadius);

        target = bestTarget;
        radius = bestRadius;
        return radius > 0f;

        void ConsiderMagnet(Vector3 candidate, float candidateRadius)
        {
            float distance = (candidate - transform.position).sqrMagnitude;
            if (distance <= candidateRadius * candidateRadius && distance < nearest)
            {
                nearest = distance;
                bestTarget = candidate;
                bestRadius = candidateRadius;
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        TryCollect(other.GetComponentInParent<BananaInventory>());
    }

    // Also used by the vine while its character collider is disabled.
    public void TryCollect(BananaInventory inventory)
    {
        if (collected)
            return;

        if (inventory == null || !inventory.isActiveAndEnabled)
            return;

        PlayerHealth health = inventory.GetComponent<PlayerHealth>();
        if (health != null && health.IsRunOver)
            return;
        var rival = inventory.GetComponent<RivalRunner>();
        if (rival != null && rival.HasFinished) return;

        bool collectedByRival = rival != null;
        collected = true;
        inventory.AddBananas(value);

        PlayPickupEffect();
        // Pickup audio and the banana combo belong to the player only.
        if (!collectedByRival && RunnerAudio.Instance != null)
            RunnerAudio.Instance.PlayBananaCollect(collectSound);
        gameObject.SetActive(false);
    }
    private void PlayPickupEffect()
    {
        if (pickupEffectPrefab == null)
            return;

        Vector3 position = GetComponent<SphereCollider>().bounds.center;

        // Imported models can have an offset pivot: use visible geometry's center.
        if (visualRenderers.Length > 0)
        {
            Bounds bounds = visualRenderers[0].bounds;
            for (int i = 1; i < visualRenderers.Length; i++)
                bounds.Encapsulate(visualRenderers[i].bounds);
            position = bounds.center;
        }

        // No parent: disabling the banana must not disable its flash.
        ParticleSystem effect = Instantiate(
            pickupEffectPrefab, position, Quaternion.identity);

        var main = effect.main;
        main.loop = false;
        main.stopAction = ParticleSystemStopAction.Destroy;
        effect.Play(true);
    }
}
