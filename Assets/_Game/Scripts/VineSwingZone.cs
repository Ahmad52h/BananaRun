using UnityEngine;

/// <summary>Owns movement until landing; aligns the animated palm after pose evaluation.</summary>
[DefaultExecutionOrder(200)]
[RequireComponent(typeof(BoxCollider), typeof(Rigidbody))]
public class VineSwingZone : MonoBehaviour
{
    [SerializeField] private Transform landingPoint;
    [SerializeField] private Transform vinePivot;
    [SerializeField] private LineRenderer vineLine;
    [SerializeField, Min(0.5f)] private float swingDuration = 2.1f;
    [SerializeField, Min(0.5f)] private float flightDuration = 1.5f;
    [SerializeField] private Transform restingVine;
    [SerializeField] private ParticleSystem anchorLeaves;
    [SerializeField] private AudioSource whoosh;
    [SerializeField] private Vector3 palmOffset = new Vector3(0, 0.06f, 0);

    private RunnerMotor motor;
    private RunnerInput input;
    private RunnerAnimation animationDriver;
    private CharacterController controller;
    private PlayerHealth health;
    private Transform hand, visual;
    private Vector3 visualPosition, end, grip, pivot, forward, flightStart, launchVelocity;
    private Quaternion visualRotation;
    private bool motorEnabled, inputEnabled, controllerEnabled, released;
    private float elapsed, length, angle, cooldown;
    public bool IsSwinging => motor != null;
    public bool IsReleased => released;
    public Vector3 GripPosition => grip;
    public float GripError => hand == null ? 0f : Vector3.Distance(hand.TransformPoint(palmOffset), grip);

    private void Awake()
    {
        GetComponent<BoxCollider>().isTrigger = true;
        var body = GetComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        if (vineLine != null) { vineLine.useWorldSpace = true; vineLine.positionCount = 2; vineLine.enabled = false; }
    }

    private void OnTriggerEnter(Collider other) => TryGrab(other.GetComponentInParent<RunnerMotor>());
    private void OnTriggerStay(Collider other) => TryGrab(other.GetComponentInParent<RunnerMotor>());

    private void TryGrab(RunnerMotor candidate)
    {
        if (IsSwinging || Time.time < cooldown || candidate == null || !candidate.enabled ||
            landingPoint == null || vinePivot == null || !candidate.CompareTag("Player")) return;
        var driver = candidate.GetComponent<RunnerAnimation>();
        var playerHealth = candidate.GetComponent<PlayerHealth>();
        var animator = candidate.GetComponentInChildren<Animator>();
        if (driver == null || driver.IsVineControlled || playerHealth == null || playerHealth.IsRunOver || animator == null ||
            !animator.isHuman || !animator.HasState(0, Animator.StringToHash("Base Layer.VineSwing"))) return;
        hand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
        if (hand == null) return;
        motor = candidate;
        health = playerHealth;
        controller = motor.GetComponent<CharacterController>();
        input = motor.GetComponent<RunnerInput>();
        animationDriver = driver;
        visual = animator.transform;
        visualPosition = visual.localPosition;
        visualRotation = visual.localRotation;
        motorEnabled = motor.enabled;
        inputEnabled = input != null && input.enabled;
        controllerEnabled = controller.enabled;
        Vector3 start = motor.transform.position;
        end = landingPoint.position;
        end.x = start.x;
        float bottom = controller.center.y - controller.height * 0.5f;
        if (Physics.Raycast(end + Vector3.up * 5f, Vector3.down, out var ground, 10f,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            end.y = ground.point.y - bottom + controller.skinWidth;
        pivot = vinePivot.position;
        forward = Vector3.ProjectOnPlane(pivot - start, Vector3.up).normalized;
        Vector3 catchPoint = start + Vector3.up * 2.4f;
        float horizontal = Vector3.ProjectOnPlane(catchPoint - pivot, Vector3.up).magnitude;
        float vertical = Mathf.Max(1f, pivot.y - catchPoint.y);
        length = Mathf.Sqrt(horizontal * horizontal + vertical * vertical);
        angle = Mathf.Atan2(horizontal, vertical);
        elapsed = 0f;
        released = false;
        motor.enabled = false;
        if (input != null) input.enabled = false;
        controller.enabled = false;
        animationDriver.BeginVineSwing();
        if (restingVine != null) restingVine.gameObject.SetActive(false);
        if (vineLine != null) vineLine.enabled = true;
        if (anchorLeaves != null) anchorLeaves.Play(true);
        if (whoosh != null) { whoosh.volume = 0f; whoosh.Play(); }
        UpdateGrip(0f);
    }

    private void UpdateGrip(float t)
    {
        // Harmonic pendulum: zero speed at both apexes, peak speed at the bottom.
        float theta = -angle * Mathf.Cos(Mathf.PI * t);
        grip = pivot + forward * (Mathf.Sin(theta) * length) - Vector3.up * (Mathf.Cos(theta) * length);
    }

    private void Update()
    {
        if (!IsSwinging) return;
        if (health == null || health.IsRunOver) { Restore(false); return; }
        elapsed += Time.deltaTime;
        if (!released)
        {
            float t = Mathf.Clamp01(elapsed / swingDuration);
            UpdateGrip(t);
            if (whoosh != null)
            {
                whoosh.volume = Mathf.Sin(t * Mathf.PI) * 0.7f;
                whoosh.pitch = 0.85f + Mathf.Sin(t * Mathf.PI) * 0.4f;
            }
        }
        else
        {
            float t = Mathf.Min(elapsed, flightDuration);
            motor.transform.position = flightStart + launchVelocity * t + Vector3.up * (0.5f * motor.BaseGravity * t * t);
            visual.localRotation = Quaternion.Slerp(visual.localRotation, visualRotation, 1f - Mathf.Exp(-12f * Time.deltaTime));
            visual.localPosition = Vector3.Lerp(visual.localPosition, visualPosition, 1f - Mathf.Exp(-12f * Time.deltaTime));
            if (elapsed >= flightDuration) { motor.transform.position = end; Restore(true); }
        }
    }

    private void LateUpdate()
    {
        if (!IsSwinging || released) return;
        float catchBlend = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / 0.22f));
        visual.localRotation = visualRotation;
        visual.rotation = Quaternion.Slerp(Quaternion.identity,
            Quaternion.FromToRotation(Vector3.up, (pivot - grip).normalized), catchBlend) * visual.rotation;
        motor.transform.position += (grip - hand.TransformPoint(palmOffset)) * catchBlend;
        if (vineLine != null)
        {
            vineLine.SetPosition(0, pivot);
            vineLine.SetPosition(1, hand.TransformPoint(palmOffset));
        }
        if (elapsed < swingDuration) return;
        released = true;
        elapsed = 0f;
        flightStart = motor.transform.position;
        // Deliberate push-off at the forward apex; gravity solves the safe landing.
        launchVelocity = (end - flightStart) / flightDuration - Vector3.up * (0.5f * motor.BaseGravity * flightDuration);
        animationDriver.ReleaseVine(flightDuration);
        if (whoosh != null) whoosh.Stop();
    }

    private void Restore(bool landed)
    {
        if (motor != null)
        {
            if (!landed && health != null && !health.IsRunOver) motor.transform.position = end;
            if (visual != null) { visual.localPosition = visualPosition; visual.localRotation = visualRotation; }
            if (controller != null) controller.enabled = controllerEnabled;
            motor.ResetVerticalVelocity();
            if (animationDriver != null) animationDriver.EndVineSwing();
            bool alive = health != null && !health.IsRunOver;
            motor.enabled = motorEnabled && alive;
            if (input != null) input.enabled = inputEnabled && alive;
        }
        motor = null;
        released = false;
        cooldown = Time.time + 1f;
        if (vineLine != null) vineLine.enabled = false;
        if (restingVine != null) restingVine.gameObject.SetActive(true);
        if (whoosh != null) whoosh.Stop();
    }

    private void OnDisable() => Restore(false);
}
