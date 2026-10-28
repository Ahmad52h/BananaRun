using UnityEngine;

[RequireComponent(typeof(SphereCollider))]
[RequireComponent(typeof(Rigidbody))]
public class CoconutPickup : MonoBehaviour
{
    private bool collected;

    private void Awake()
    {
        GetComponent<SphereCollider>().isTrigger = true;

        Rigidbody body = GetComponent<Rigidbody>();
        body.useGravity = false;
        body.isKinematic = true;
    }

    private void OnEnable()
    {
        collected = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (collected)
            return;

        // The rival consumes fruit it runs through. It has no ammo UI or
        // pickup sound, so the pickup simply disappears.
        RivalRunner rival = other.GetComponentInParent<RivalRunner>();
        if (rival != null)
        {
            if (rival.HasFinished)
                return;

            collected = true;
            gameObject.SetActive(false);
            return;
        }

        CoconutThrower thrower =
            other.GetComponentInParent<CoconutThrower>();

        if (thrower == null || !thrower.CompareTag("Player") || !thrower.TryCollect())
            return;

        collected = true;
        gameObject.SetActive(false);
    }
}
