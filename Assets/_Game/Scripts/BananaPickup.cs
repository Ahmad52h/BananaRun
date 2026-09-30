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

    private Renderer[] visualRenderers;
    private bool collected;

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
        if (visual != null)
        {
            visual.Rotate(
                Vector3.up,
                rotationSpeed * Time.deltaTime,
                Space.World
            );
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (collected)
            return;

        BananaInventory inventory =
            other.GetComponentInParent<BananaInventory>();

        if (inventory == null || !inventory.isActiveAndEnabled)
            return;

        PlayerHealth health = inventory.GetComponent<PlayerHealth>();
        if (health != null && health.IsRunOver)
            return;

        collected = true;
        inventory.AddBananas(value);

        PlayPickupEffect();
        if (RunnerAudio.Instance != null) RunnerAudio.Instance.PlayBananaCollect(collectSound);
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
