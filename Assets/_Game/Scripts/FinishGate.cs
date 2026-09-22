using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
[RequireComponent(typeof(Rigidbody))]
public class FinishGate : MonoBehaviour
{
    [SerializeField, Min(1)]
    private int requiredBananas = 6;

    [SerializeField] private RunnerHUD hud;

    private bool completed;

    private void Awake()
    {
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

        if (inventory == null || hud == null)
        {
            Debug.LogError(
                "FinishGate: Check HUD and BananaInventory.",
                this
            );

            return;
        }

        completed = true;

        int finalCount = inventory.Count;
        bool won = finalCount >= requiredBananas;

        health.EndRun();
        inventory.enabled = false;

        hud.ShowFinishResult(
            won,
            finalCount,
            requiredBananas
        );
    }
}