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
    private SphereCollider pickupCollider;
    public int Value => value;
    public bool IsCollected => collected;
    internal void ResetForSpawn() => collected = false;
    public Vector3 WorldCenter => pickupCollider.bounds.center;

    private void Awake()
    {
        visualRenderers = visual != null
            ? visual.GetComponentsInChildren<Renderer>()
            : new Renderer[0];

        pickupCollider = GetComponent<SphereCollider>();
        pickupCollider.isTrigger = true;

        Rigidbody body = GetComponent<Rigidbody>();
        body.useGravity = false;
        body.isKinematic = true;
    }

    private void Start() => TrackDistanceStreamer.Register(gameObject);

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

    private bool TryGetMagnetTarget(out Vector3 target, out float radius) =>
        RunnerMagnetTargets.TryGetNearest(transform.position, out target, out radius);

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

        Vector3 position = pickupCollider.bounds.center;

        // Imported models can have an offset pivot: use visible geometry's center.
        if (visualRenderers.Length > 0)
        {
            Bounds bounds = visualRenderers[0].bounds;
            for (int i = 1; i < visualRenderers.Length; i++)
                bounds.Encapsulate(visualRenderers[i].bounds);
            position = bounds.center;
        }

        // No parent: disabling the banana must not disable its flash.
        PickupEffectPool.Play(pickupEffectPrefab, position);
    }
}
