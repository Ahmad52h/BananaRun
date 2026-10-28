using UnityEngine;

[RequireComponent(typeof(SphereCollider), typeof(Rigidbody))]
public sealed class BombPickup : MonoBehaviour
{
    private bool collected;
    private void Awake()
    {
        GetComponent<SphereCollider>().isTrigger = true;
        var body = GetComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
    }
    private void OnEnable() => collected = false;
    private void OnTriggerEnter(Collider other)
    {
        if (collected) return;

        var dropper = other.GetComponentInParent<BombDropper>();
        if (dropper != null && dropper.CompareTag("Player"))
        {
            if (!dropper.TryCollect()) return;
            RunnerAudio.Instance?.PlayPowerUpCollect();
        }
        else
        {
            // The AI rival keeps its own secondary ammo so it can place traps
            // while racing. Its pickups stay silent, matching power-up pickups.
            var rival = other.GetComponentInParent<RivalRunner>();
            if (rival == null || !rival.TryCollectBomb()) return;
        }

        collected = true;
        gameObject.SetActive(false);
    }
}
