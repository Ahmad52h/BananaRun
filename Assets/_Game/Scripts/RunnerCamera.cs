using UnityEngine;

public class RunnerCamera : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 offset = new Vector3(0f, 5f, -8f);
    [Header("Lane Follow")]
    [SerializeField, Range(0f, 1f)] private float horizontalFollow = 0.5f;
    [SerializeField, Min(0.01f)] private float horizontalSmoothTime = 0.15f;
    [Header("Glider Framing")]
    [SerializeField, Min(0.01f)] private float verticalSmoothTime = 0.12f;
    [SerializeField, Min(0f)] private float jumpFramingDeadZone = 0.65f;
    private float jumpLift, verticalFollowVelocity;
    [Header("Responsive Framing")]
    [Tooltip("The 16:9 portrait aspect the scene FOV was authored for.")]
    [SerializeField, Range(0.1f, 2f)] private float referenceAspect = 9f / 16f;
    [Tooltip("Minimum vertical FOV used on wide tablets/landscape displays.")]
    [SerializeField, Range(30f, 90f)] private float minimumResponsiveFov = 50f;
    [Tooltip("Maximum vertical FOV used on ultra-tall phones.")]
    [SerializeField, Range(50f, 110f)] private float maximumResponsiveFov = 82f;
    [Tooltip("Extra FOV added to the responsive baseline while the giant power-up is active.")]
    [SerializeField, Range(0f, 20f)] private float giantFovBoost = 6f;
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
        if (view == null) view = Camera.main;
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
        if (view != null)
        {
            float aspect = view.aspect > 0.001f
                ? view.aspect
                : (float)Screen.width / Mathf.Max(1, Screen.height);
            float responsiveFov = CalculateResponsiveFov(aspect);
            float giantTarget = Mathf.Max(giantFov, responsiveFov + giantFovBoost);
            view.fieldOfView = Mathf.Lerp(responsiveFov, giantTarget, giantBlend);
        }
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
        float desiredLift = motor != null && motor.IsGliderJump
            ? Mathf.Max(0f, target.position.y - groundReferenceY - jumpFramingDeadZone) : 0f;
        jumpLift = Mathf.SmoothDamp(jumpLift, desiredLift, ref verticalFollowVelocity, verticalSmoothTime);
        transform.position = new Vector3(smoothX + extra.x, groundReferenceY + offset.y + extra.y + impulse + jumpLift,
            target.position.z + offset.z + extra.z);
    }

    /// <summary>
    /// Keeps the same horizontal world width as the authored 16:9 portrait view.
    /// hFov = 2 atan(tan(vFov / 2) * aspect), so the equivalent vertical FOV is
    /// 2 atan(tan(defaultFov / 2) * referenceAspect / aspect).
    /// </summary>
    private float CalculateResponsiveFov(float aspect)
    {
        if (defaultFov <= 0f || aspect <= 0.001f)
            return defaultFov;

        float baseline = Mathf.Tan(defaultFov * 0.5f * Mathf.Deg2Rad);
        float fov = 2f * Mathf.Atan(baseline * referenceAspect / aspect) * Mathf.Rad2Deg;
        return Mathf.Clamp(fov, minimumResponsiveFov, maximumResponsiveFov);
    }

    private void OnDisable()
    {
        if (view != null && defaultFov > 0f) view.fieldOfView = defaultFov;
        if (footsteps != null) footsteps.Stop();
        giantBlend = 0;
    }
}
