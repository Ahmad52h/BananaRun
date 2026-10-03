using UnityEngine;

public class RunnerCamera : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 offset = new Vector3(0f, 5f, -8f);
    [Header("Lane Follow")]
    [SerializeField, Range(0f, 1f)] private float horizontalFollow = 0.5f;
    [SerializeField, Min(0.01f)] private float horizontalSmoothTime = 0.15f;
    [Header("Giant")]
    [SerializeField] private float giantFov = 75f;
    [SerializeField] private Vector3 giantOffset = new Vector3(0, 1.2f, -3.5f);
    [SerializeField] private AudioClip heavyStep;
    [SerializeField, Range(0f, 0.3f)] private float shakeStrength = 0.09f;
    private float trackCenterX, groundReferenceY, horizontalVelocity, smoothX;
    private float defaultFov, giantBlend, stepPhase, shakeAge = 1f;
    private Camera view;
    private PowerUpManager powers;
    private RunnerMotor motor;
    private CharacterController controller;
    private AudioSource footsteps;
    public float GiantBlend => giantBlend;
    public int HeavyStepsPlayed { get; private set; }

    private void Start()
    {
        if (target == null) { Debug.LogError("RunnerCamera: Assign Player to Target.", this); enabled = false; return; }
        view = GetComponent<Camera>();
        defaultFov = view != null ? view.fieldOfView : 60f;
        powers = target.GetComponent<PowerUpManager>();
        motor = target.GetComponent<RunnerMotor>();
        controller = target.GetComponent<CharacterController>();
        footsteps = gameObject.AddComponent<AudioSource>();
        footsteps.playOnAwake = false;
        footsteps.spatialBlend = 0f;
        footsteps.volume = 0.75f;
        trackCenterX = target.position.x;
        groundReferenceY = target.position.y;
        smoothX = trackCenterX + offset.x;
    }

    private void LateUpdate()
    {
        if (target == null) return;
        bool giant = powers != null && powers.isActiveAndEnabled && powers.IsActive(PowerUpType.Bulldozer);
        giantBlend = Mathf.Lerp(giantBlend, giant ? 1f : 0f, 1f - Mathf.Exp(-5f * Time.deltaTime));
        if (view != null) view.fieldOfView = Mathf.Lerp(defaultFov, giantFov, giantBlend);
        bool stepping = giant && motor != null && motor.enabled && controller != null &&
            controller.enabled && controller.isGrounded && !motor.IsSliding;
        if (stepping)
        {
            // Distance-based cadence follows running speed and stops in the air.
            stepPhase += motor.CurrentSpeed * Time.deltaTime / 3.2f;
            if (stepPhase >= 1f)
            {
                stepPhase %= 1f;
                shakeAge = 0f;
                HeavyStepsPlayed++;
                if (heavyStep != null) footsteps.PlayOneShot(heavyStep);
            }
        }
        else stepPhase = 0f;
        shakeAge += Time.deltaTime;
        float impulse = Mathf.Exp(-20f * shakeAge) * Mathf.Sin(shakeAge * 65f) * shakeStrength * giantBlend;
        float targetX = trackCenterX + offset.x + (target.position.x - trackCenterX) * horizontalFollow;
        smoothX = Mathf.SmoothDamp(smoothX, targetX, ref horizontalVelocity, horizontalSmoothTime);
        Vector3 extra = giantOffset * giantBlend;
        transform.position = new Vector3(smoothX + extra.x, groundReferenceY + offset.y + extra.y + impulse,
            target.position.z + offset.z + extra.z);
    }

    private void OnDisable()
    {
        if (view != null && defaultFov > 0f) view.fieldOfView = defaultFov;
        if (footsteps != null) footsteps.Stop();
        giantBlend = 0;
    }
}
