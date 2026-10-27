using System.Collections;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
[RequireComponent(typeof(Rigidbody))]
public class FinishGate : MonoBehaviour
{
    [SerializeField, Min(1)]
    private int requiredBananas = 6;

    [Header("Banana Basket Quota")]
    [Tooltip("Show a basket goal in the HUD and award 1-3 stars based on bananas delivered.")]
    [SerializeField] private bool enableBananaQuota = true;
    [Tooltip("Bananas needed to fill the celebration basket; never lower than Required Bananas.")]
    [SerializeField, Min(1)] private int targetBananasForBasket = 20;

    [SerializeField] private RunnerHUD hud;

    [Header("Banana Delivery")]
    [SerializeField] private GameObject bananaDeliveryVisual;
    [SerializeField] private Transform bananaTarget;

    [SerializeField]
    private Vector3 releaseOffset = new Vector3(0f, 1.2f, 0.5f);

    [SerializeField, Min(0.1f)]
    private float flightDuration = 0.65f;

    [SerializeField, Min(0f)]
    private float arcHeight = 1.5f;

    [SerializeField, Min(0.01f)]
    private float launchInterval = 0.1f;

    // Matches the longest dimension of Banana.prefab's visible model.
    private const float DeliveryBananaScale = 1.33f;

    private bool completed;
    public bool Completed => completed;
    public bool BananaQuotaEnabled => enableBananaQuota;
    public int BananaBasketTarget => Mathf.Max(requiredBananas, targetBananasForBasket);
    public Transform BananaTarget => bananaTarget;

    private void OnValidate()
    {
        targetBananasForBasket = Mathf.Max(requiredBananas, targetBananasForBasket);
    }

    private void Awake()
    {
        if (hud == null) hud = FindAnyObjectByType<RunnerHUD>();
        GetComponent<BoxCollider>().isTrigger = true;

        Rigidbody body = GetComponent<Rigidbody>();
        body.useGravity = false;
        body.isKinematic = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (completed)
            return;

        PlayerHealth health =
            other.GetComponentInParent<PlayerHealth>();

        if (health == null || health.IsRunOver)
            return;

        BananaInventory inventory =
            health.GetComponent<BananaInventory>();

        completed = true;

        int finalCount = inventory != null ? inventory.Count : 0;
        bool won = finalCount >= requiredBananas;

        health.EndRun();
        if (inventory != null) inventory.enabled = false;

        StartCoroutine(FinishSequence(
            health.transform, finalCount, won,
            enableBananaQuota, BananaBasketTarget));
    }

    private IEnumerator FinishSequence(
        Transform player, int finalCount, bool won,
        bool quotaEnabled, int quotaTarget)
    {
        if (finalCount > 0 &&
            bananaDeliveryVisual != null &&
            bananaTarget != null)
        {
            yield return DeliverBananas(player, finalCount);
        }

        yield return new WaitForSecondsRealtime(0.3f);

        if (hud != null) hud.ShowFinishResult(
            won, finalCount, requiredBananas, quotaEnabled, quotaTarget);
    }

    private IEnumerator DeliverBananas(
        Transform player, int finalCount)
    {
        // Limit decorative objects; the actual score stays unchanged.
        int visualCount = Mathf.Min(finalCount, 24);

        Transform[] visuals = new Transform[visualCount];
        Vector3[] scales = new Vector3[visualCount];

        Vector3 start = player.position + releaseOffset;
        Vector3 destination = bananaTarget.position;

        float duration = Mathf.Max(0.1f, flightDuration);

        // Keep the sequence short even with many collected bananas.
        float interval = Mathf.Min(
            Mathf.Max(0.01f, launchInterval),
            1.5f / Mathf.Max(1, visualCount - 1));

        float totalDuration =
            duration + (visualCount - 1) * interval;

        float elapsed = 0f;

        try
        {
            while (elapsed < totalDuration)
            {
                for (int i = 0; i < visualCount; i++)
                {
                    float age = elapsed - i * interval;

                    if (age < 0f)
                        continue;

                    float t = Mathf.Clamp01(age / duration);

                    if (t >= 1f)
                    {
                        if (visuals[i] != null)
                        {
                            Destroy(visuals[i].gameObject);
                            visuals[i] = null;
                        }

                        continue;
                    }

                    if (visuals[i] == null)
                    {
                        GameObject banana = Instantiate(
                            bananaDeliveryVisual,
                            start,
                            bananaDeliveryVisual.transform.rotation);

                        visuals[i] = banana.transform;
                        scales[i] =
                            visuals[i].localScale * DeliveryBananaScale;
                    }

                    Vector3 position =
                        Vector3.Lerp(start, destination, t);

                    position.y +=
                        4f * arcHeight * t * (1f - t);

                    visuals[i].position = position;

                    visuals[i].Rotate(
                        Vector3.up,
                        360f * Time.unscaledDeltaTime,
                        Space.World);

                    // Shrink as the banana enters the basket.
                    float shrink =
                        Mathf.InverseLerp(0.8f, 1f, t);

                    visuals[i].localScale =
                        scales[i] * (1f - shrink);
                }

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }
        finally
        {
            foreach (Transform visual in visuals)
            {
                if (visual != null)
                    Destroy(visual.gameObject);
            }
        }
    }
}
