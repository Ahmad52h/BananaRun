using UnityEngine;

[RequireComponent(typeof(BoxCollider), typeof(Rigidbody))]
public class BananaPeelHazard : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float slipDuration = 0.8f;
    [SerializeField] private AudioClip slipSound;
    [SerializeField] private ParticleSystem slipPuff;
    private bool consumed;

    private void Awake()
    {
        GetComponent<BoxCollider>().isTrigger = true;
        var body = GetComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
    }

    private void OnEnable()
    {
        consumed = false;
        GetComponent<BoxCollider>().enabled = true;
    }

    private void OnTriggerEnter(Collider other) => TrySlip(other);
    private void OnTriggerStay(Collider other) => TrySlip(other);

    private void TrySlip(Collider other)
    {
        if (consumed) return;
        var motor = other.GetComponentInParent<RunnerMotor>();
        if (motor == null || !motor.isActiveAndEnabled) return;
        var health = motor.GetComponent<PlayerHealth>();
        var controller = motor.GetComponent<CharacterController>();
        if (health == null || health.IsRunOver || controller == null || !controller.isGrounded) return;
        consumed = true;
        GetComponent<BoxCollider>().enabled = false;
        motor.LockLaneSwitching(slipDuration);
        motor.GetComponent<RunnerAnimation>()?.ReactToSlip(slipDuration);
        // A slip never calls TakeHit or ApplySlowdown.
        if (slipSound != null) AudioSource.PlayClipAtPoint(slipSound, motor.transform.position, 0.8f);
        if (slipPuff != null)
        {
            var puff = Instantiate(slipPuff, transform.position + Vector3.up * 0.12f, Quaternion.identity);
            puff.Play();
            Destroy(puff.gameObject, 2f);
        }
    }
}
