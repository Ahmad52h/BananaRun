using UnityEngine;

[RequireComponent(typeof(SphereCollider), typeof(Rigidbody))]
public class FallingCoconutHazard : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float warningSeconds = 1f;
    [SerializeField, Min(1f)] private float dropHeight = 9f;
    [SerializeField, Min(0.1f)] private float impactRadius = 0.95f;
    [SerializeField] private Transform warningShadow;
    [SerializeField] private ParticleSystem impactEffect;
    private Vector3 target;
    private float launchedAt;
    private bool initialized;
    private bool impacted;

    private void Awake()
    {
        var collider = GetComponent<SphereCollider>();
        collider.isTrigger = true;
        var body = GetComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
    }

    public void Initialize(Vector3 landingPoint)
    {
        target = landingPoint;
        transform.position = target + Vector3.up * dropHeight;
        launchedAt = Time.time;
        initialized = true;
        if (warningShadow != null)
        {
            warningShadow.position = target + Vector3.up * 0.04f;
            warningShadow.localScale = new Vector3(0.3f, 0.012f, 0.3f);
        }
    }

    private void Update()
    {
        if (!initialized || impacted) return;
        float t = Mathf.Clamp01((Time.time - launchedAt) / warningSeconds);
        transform.position = Vector3.Lerp(target + Vector3.up * dropHeight, target + Vector3.up * 0.55f, t * t);
        if (warningShadow != null)
        {
            warningShadow.position = target + Vector3.up * 0.04f;
            float size = Mathf.Lerp(0.3f, 1.8f, t);
            warningShadow.localScale = new Vector3(size, 0.012f, size);
        }
        if (t >= 1f) Impact();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!initialized || impacted || Time.time - launchedAt < warningSeconds) return;
        var health = other.GetComponentInParent<PlayerHealth>();
        var rival = other.GetComponentInParent<RivalRunner>();
        if (health != null || rival != null) Impact();
    }

    private void Impact()
    {
        if (impacted) return;
        impacted = true;
        PlayerHealth stunnedPlayer = null;
        RivalRunner stunnedRival = null;
        foreach (var collider in Physics.OverlapSphere(target + Vector3.up * 0.7f, impactRadius))
        {
            var health = collider.GetComponentInParent<PlayerHealth>();
            if (health != null && health != stunnedPlayer)
            {
                var powers = health.GetComponent<PowerUpManager>();
                if (powers != null && powers.IsActive(PowerUpType.Bulldozer)) powers.AwardBulldozerPoints();
                else health.TakeProjectileHit();
                stunnedPlayer = health;
            }
            var rival = collider.GetComponentInParent<RivalRunner>();
            if (rival != null && rival != stunnedRival) { rival.TakeProjectileHit(); stunnedRival = rival; }
        }
        if (impactEffect != null)
        {
            var effect = Instantiate(impactEffect, target + Vector3.up * 0.2f, Quaternion.identity);
            var main = effect.main;
            main.loop = false;
            main.stopAction = ParticleSystemStopAction.Destroy;
            effect.Play();
        }
        Destroy(gameObject);
    }

}
