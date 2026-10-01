using System.Collections;
using UnityEngine;

[RequireComponent(typeof(BoxCollider), typeof(Rigidbody))]
public class VineSwingZone : MonoBehaviour
{
    [SerializeField] private Transform landingPoint;
    [SerializeField] private Transform vinePivot;
    [SerializeField] private LineRenderer vineLine;
    [SerializeField, Min(0.5f)] private float swingDuration = 2.1f;
    [SerializeField, Min(0f)] private float arcHeight = 4.5f;
    private bool swinging;

    private void Awake()
    {
        GetComponent<BoxCollider>().isTrigger = true;
        var body = GetComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        if (vineLine != null) vineLine.enabled = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (swinging || landingPoint == null) return;
        var motor = other.GetComponentInParent<RunnerMotor>();
        if (motor == null || !motor.CompareTag("Player")) return;
        var health = motor.GetComponent<PlayerHealth>();
        if (health != null && health.IsRunOver) return;
        StartCoroutine(Swing(motor));
    }

    private IEnumerator Swing(RunnerMotor motor)
    {
        swinging = true;
        var controller = motor.GetComponent<CharacterController>();
        var input = motor.GetComponent<RunnerInput>();
        bool motorWasEnabled = motor.enabled;
        bool inputWasEnabled = input != null && input.enabled;
        Vector3 start = motor.transform.position;
        Vector3 end = landingPoint.position;
        motor.enabled = false;
        if (input != null) input.enabled = false;
        controller.enabled = false;
        if (vineLine != null) vineLine.enabled = true;
        float elapsed = 0f;
        while (elapsed < swingDuration && motor != null)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / swingDuration);
            float smooth = t * t * (3f - 2f * t);
            Vector3 position = Vector3.Lerp(start, end, smooth);
            position.y += Mathf.Sin(t * Mathf.PI) * arcHeight;
            motor.transform.position = position;
            if (vineLine != null && vinePivot != null)
            {
                vineLine.SetPosition(0, vinePivot.position);
                vineLine.SetPosition(1, position + Vector3.up * 1.4f);
            }
            yield return null;
        }
        if (motor != null)
        {
            motor.transform.position = end;
            controller.enabled = true;
            motor.ResetVerticalVelocity();
            motor.enabled = motorWasEnabled;
            if (input != null) input.enabled = inputWasEnabled;
        }
        if (vineLine != null) vineLine.enabled = false;
        swinging = false;
    }
}
