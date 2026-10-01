using UnityEngine;

[RequireComponent(typeof(SphereCollider), typeof(Rigidbody))]
public class PowerUpPickup : MonoBehaviour
{
    [SerializeField] private PowerUpType type;
    [SerializeField, Min(0f)] private float spinSpeed = 75f;
    [SerializeField] private ParticleSystem collectionEffect;
    private bool collected;

    private void Awake()
    {
        GetComponent<SphereCollider>().isTrigger = true;
        var body = GetComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
    }

    private void Update() => transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);

    private void OnTriggerEnter(Collider other)
    {
        if (collected) return;
        var manager = other.GetComponentInParent<PowerUpManager>();
        if (manager == null || !manager.CompareTag("Player")) return;
        collected = true;
        manager.Activate(type);
        if (collectionEffect != null)
        {
            var effect = Instantiate(collectionEffect, transform.position, Quaternion.identity);
            var main = effect.main;
            main.loop = false;
            main.stopAction = ParticleSystemStopAction.Destroy;
            effect.Play();
        }
        gameObject.SetActive(false);
    }

    private void OnEnable() => collected = false;
}
