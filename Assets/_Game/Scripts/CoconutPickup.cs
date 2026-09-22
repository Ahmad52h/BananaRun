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

        CoconutThrower thrower =
            other.GetComponentInParent<CoconutThrower>();

        if (thrower == null || !thrower.TryCollect())
            return;

        collected = true;
        gameObject.SetActive(false);
    }
}