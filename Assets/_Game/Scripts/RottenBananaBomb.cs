using UnityEngine;

/// <summary>A stationary, single-use enemy trap. Its kinematic body never rolls away.</summary>
[RequireComponent(typeof(SphereCollider), typeof(Rigidbody))]
public sealed class RottenBananaBomb : MonoBehaviour
{
    [SerializeField] private GameObject explosionPrefab;
    [SerializeField, Min(0.1f)] private float stunSeconds = 2.2f;
    [SerializeField, Min(1f)] private float launchSpeed = 7f;
    [SerializeField, Min(1f)] private float lifetime = 45f;
    private Transform owner;
    private bool detonated;

    /// <summary>
    /// Records the runner that placed the trap. Only the owner is excluded, so
    /// player traps hit the rival/gorilla and rival traps hit the player.
    /// </summary>
    public void SetOwner(Transform ownerRoot) => owner = ownerRoot;

    private bool IsOwner(Collider other) =>
        owner != null && (other.transform == owner || other.transform.IsChildOf(owner));

    private void Awake()
    {
        GetComponent<SphereCollider>().isTrigger = true;
        var body = GetComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        Destroy(gameObject, lifetime);
    }

    private void OnTriggerEnter(Collider other) => TryDetonate(other);
    // Handles enemies already overlapping at placement and re-enabled controllers.
    private void OnTriggerStay(Collider other) => TryDetonate(other);

    private void TryDetonate(Collider other)
    {
        if (detonated || IsOwner(other)) return;
        var gorilla = other.GetComponentInParent<GiantGorillaChaser>();
        if (gorilla != null)
        {
            if (!gorilla.ApplyBombHit(launchSpeed, stunSeconds)) return;
        }
        else
        {
            var rival = other.GetComponentInParent<RivalRunner>();
            if (rival != null)
            {
                if (!rival.isActiveAndEnabled || rival.HasFinished || rival.IsRecoveringFromFall) return;
                // A shield still consumes the blast even when the rival shrugs
                // off the stun, so the trap must detonate either way.
                rival.TakeBombHit(launchSpeed, stunSeconds);
            }
            else
            {
                var health = other.GetComponentInParent<PlayerHealth>();
                if (health == null || health.IsRunOver) return;
                // Shield/protection can absorb the hit, but the bomb still goes off.
                health.TakeProjectileHit();
            }
        }

        detonated = true;
        GetComponent<SphereCollider>().enabled = false;
        RunnerAudio.Instance?.PlayBombExplosion();
        if (explosionPrefab != null)
            Instantiate(explosionPrefab, transform.position + Vector3.up * 0.3f, Quaternion.identity);
        Destroy(gameObject);
    }
}
