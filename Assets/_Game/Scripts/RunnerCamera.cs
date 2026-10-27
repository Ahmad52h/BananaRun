using UnityEngine;

public class RunnerCamera : MonoBehaviour
{
    [SerializeField] private Transform target;
    [Tooltip("Optional nearby competitor to frame alongside the player during a direct race.")]
    [SerializeField] private Transform raceRival;
    [SerializeField] private Vector3 offset = new Vector3(0f, 5f, -8f);
    [Header("Optional Level Feel")]
    [Tooltip("Subtle Dutch roll on lane changes plus a speed-based FOV lift. Disable for the original camera feel.")]
    [SerializeField] private bool enableDynamicCameraTilt = true;
    [Tooltip("In the final 15% of the course, lift and frame the camera toward the finish arch and banana basket.")]
    [SerializeField] private bool enableApproachZoom = true;
    [Tooltip("Blend ambient and directional lighting through morning, mid-jungle, and golden finish phases.")]
    [SerializeField] private bool enableZoneLighting = true;
    [Header("Dynamic Camera Tilt")]
    [SerializeField, Range(0f, 5f)] private float maxCameraTilt = 1.8f;
    [SerializeField, Min(0.01f)] private float cameraTiltSmoothTime = 0.18f;
    [SerializeField, Range(0f, 8f)] private float speedFovBoost = 2.5f;
    [SerializeField, Min(0.01f)] private float speedFovSmoothTime = 0.35f;
    [Header("Finish Approach Framing")]
    [SerializeField, Range(0.5f, 0.95f)] private float approachStartProgress = 0.85f;
    [SerializeField, Min(0f)] private float approachCameraLift = 0.8f;
    [SerializeField, Range(-8f, 8f)] private float approachFovOffset = -1.5f;
    [SerializeField, Range(0f, 1f)] private float approachLookWeight = 0.35f;
    [SerializeField, Min(0.05f)] private float approachSmoothTime = 0.8f;
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
    private float cameraTilt, cameraTiltVelocity;
    private float speedFov, speedFovVelocity;
    private float approachBlend, approachVelocity;
    private float levelStartZ;
    private Quaternion baselineRotation;
    private bool orientationApplied;
    private LevelProgressHUD progressSource;
    private FinishGate finishGate;
    private Transform finishArch;
    private Transform celebrationBasket;
    private Vector3 approachLookPoint;
    private bool approachLookPointReady;
    private bool lightingCaptured;
    private bool lightingApplied;
    private float zoneBlend;
    private Color originalAmbientLight;
    private Color originalAmbientSky;
    private Color originalAmbientEquator;
    private Color originalAmbientGround;
    private float originalAmbientIntensity;
    private UnityEngine.Rendering.AmbientMode originalAmbientMode;
    private Light sun;
    private Color originalSunColor;
    private float originalSunIntensity;
    private Camera view;
    private PowerUpManager powers;
    private RunnerMotor motor;
    private CharacterController controller;
    private AudioSource footsteps;
    public float GiantBlend => giantBlend;
    public int HeavyStepsPlayed { get; private set; }

    private static readonly Color MorningSky = new Color(0.58f, 0.75f, 0.78f);
    private static readonly Color MorningEquator = new Color(0.48f, 0.62f, 0.36f);
    private static readonly Color MorningGround = new Color(0.31f, 0.35f, 0.22f);
    private static readonly Color MorningSun = new Color(1f, 0.94f, 0.78f);
    private static readonly Color MidSky = new Color(0.40f, 0.57f, 0.50f);
    private static readonly Color MidEquator = new Color(0.30f, 0.46f, 0.22f);
    private static readonly Color MidGround = new Color(0.20f, 0.28f, 0.13f);
    private static readonly Color MidSun = new Color(0.86f, 0.94f, 0.68f);
    private static readonly Color GoldenSky = new Color(0.70f, 0.55f, 0.35f);
    private static readonly Color GoldenEquator = new Color(0.58f, 0.43f, 0.22f);
    private static readonly Color GoldenGround = new Color(0.33f, 0.26f, 0.13f);
    private static readonly Color GoldenSun = new Color(1f, 0.70f, 0.36f);
    private const float MorningAmbientIntensity = 1f;
    private const float MidAmbientIntensity = 0.92f;
    private const float GoldenAmbientIntensity = 1.08f;
    private const float MorningSunIntensity = 1.15f;
    private const float MidSunIntensity = 1f;
    private const float GoldenSunIntensity = 1.12f;

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
        baselineRotation = transform.rotation;
        levelStartZ = target.position.z;
        progressSource = FindProgressSource(target);
        finishGate = FindAnyObjectByType<FinishGate>();
        finishArch = FindSceneTransform("Tropical Finish Arch");
        celebrationBasket = FindSceneTransform("Celebration Banana Basket");
        approachLookPoint = ComputeApproachLookPoint();
        approachLookPointReady = true;
        CaptureLighting();
    }

    private static LevelProgressHUD FindProgressSource(Transform player)
    {
        LevelProgressHUD fallback = null;
        foreach (LevelProgressHUD candidate in FindObjectsByType<LevelProgressHUD>(FindObjectsInactive.Exclude))
        {
            if (candidate == null) continue;
            if (fallback == null) fallback = candidate;
            if (candidate.Player == player) return candidate;
        }
        return fallback;
    }

    private static Transform FindSceneTransform(string name)
    {
        GameObject found = GameObject.Find(name);
        return found != null ? found.transform : null;
    }

    private void LateUpdate()
    {
        if (target == null) return;
        float dt = Time.deltaTime;
        bool giant = powers != null && powers.isActiveAndEnabled && powers.IsActive(PowerUpType.Bulldozer);
        giantBlend = Mathf.Lerp(giantBlend, giant ? 1f : 0f, 1f - Mathf.Exp(-5f * dt));
        bool stepping = giant && motor != null && motor.enabled && controller != null &&
            controller.enabled && controller.isGrounded && !motor.IsSliding;
        if (stepping)
        {
            // Distance-based cadence follows running speed and stops in the air.
            stepPhase += motor.CurrentSpeed * dt / 3.2f;
            if (stepPhase >= 1f)
            {
                stepPhase %= 1f;
                shakeAge = 0f;
                HeavyStepsPlayed++;
                if (heavyStep != null) footsteps.PlayOneShot(heavyStep);
            }
        }
        else stepPhase = 0f;
        shakeAge += dt;
        float impulse = Mathf.Exp(-20f * shakeAge) * Mathf.Sin(shakeAge * 65f) * shakeStrength * giantBlend;
        float targetX = trackCenterX + offset.x + (target.position.x - trackCenterX) * horizontalFollow;
        float raceWeight = raceRival != null ?
            (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(4f, 12f,
                Mathf.Abs(raceRival.position.z - target.position.z)))) * (1f - giantBlend) * (1f - approachBlend) : 0f;
        if (raceRival != null)
            targetX = Mathf.Lerp(targetX, (target.position.x + raceRival.position.x) * 0.5f + offset.x, raceWeight);
        smoothX = Mathf.SmoothDamp(smoothX, targetX, ref horizontalVelocity, horizontalSmoothTime);
        Vector3 extra = giantOffset * giantBlend;
        float desiredLift = motor != null && motor.IsGliderJump
            ? Mathf.Max(0f, target.position.y - groundReferenceY - jumpFramingDeadZone) : 0f;
        jumpLift = Mathf.SmoothDamp(jumpLift, desiredLift, ref verticalFollowVelocity, verticalSmoothTime);

        float progress = CurrentProgress01();
        UpdateApproach(progress, dt);
        UpdateDynamicTilt(targetX, dt);
        UpdateZoneLighting(progress);

        if (view != null)
        {
            float aspect = view.aspect > 0.001f
                ? view.aspect
                : (float)Screen.width / Mathf.Max(1, Screen.height);
            float responsiveFov = CalculateResponsiveFov(aspect);
            float giantTarget = Mathf.Max(giantFov, responsiveFov + giantFovBoost);
            float dynamicFov = 0f;
            if (enableDynamicCameraTilt) dynamicFov += speedFov;
            if (enableApproachZoom) dynamicFov += approachFovOffset * approachBlend;
            if (raceRival != null)
                dynamicFov += raceWeight * Mathf.Min(10f, Mathf.Abs(raceRival.position.x - target.position.x) * 2f);
            responsiveFov += dynamicFov;
            giantTarget += dynamicFov;
            view.fieldOfView = Mathf.Lerp(responsiveFov, giantTarget, giantBlend);
        }

        float approachLift = enableApproachZoom ? approachCameraLift * approachBlend : 0f;
        transform.position = new Vector3(smoothX + extra.x,
            groundReferenceY + offset.y + extra.y + impulse + jumpLift + approachLift,
            target.position.z + offset.z + extra.z);
        ApplyCameraOrientation();
    }

    private float CurrentProgress01()
    {
        if (finishGate != null && finishGate.Completed) return 1f;
        if (progressSource != null && progressSource.RunLength > 0f)
            return progressSource.ProgressAt(target.position.z);
        if (finishGate != null)
        {
            float runLength = finishGate.transform.position.z - levelStartZ;
            if (runLength > 0.01f)
                return Mathf.Clamp01((target.position.z - levelStartZ) / runLength);
        }
        return 0f;
    }

    private void UpdateApproach(float progress, float dt)
    {
        if (!enableApproachZoom)
        {
            approachBlend = 0f;
            approachVelocity = 0f;
            return;
        }

        float target = progress >= approachStartProgress ? 1f : 0f;
        approachBlend = Mathf.SmoothDamp(
            approachBlend, target, ref approachVelocity, approachSmoothTime, Mathf.Infinity, dt);
    }

    private void UpdateDynamicTilt(float targetX, float dt)
    {
        if (!enableDynamicCameraTilt)
        {
            cameraTilt = 0f;
            cameraTiltVelocity = 0f;
            speedFov = 0f;
            speedFovVelocity = 0f;
            return;
        }

        // The camera lags behind the lane target while changing lanes. That lag
        // gives us a short, self-clearing bank instead of a permanent offset.
        float lateralLag = Mathf.Clamp((targetX - smoothX) / 0.9f, -1f, 1f);
        float tiltTarget = -lateralLag * maxCameraTilt;
        cameraTilt = Mathf.SmoothDamp(
            cameraTilt, tiltTarget, ref cameraTiltVelocity, cameraTiltSmoothTime, Mathf.Infinity, dt);

        float speed01 = 0f;
        if (motor != null && motor.ForwardSpeed > 0.01f)
            speed01 = Mathf.Clamp01(Mathf.InverseLerp(
                motor.ForwardSpeed, motor.MaximumForwardSpeed, motor.CurrentSpeed));
        float fovTarget = speed01 * speedFovBoost;
        speedFov = Mathf.SmoothDamp(
            speedFov, fovTarget, ref speedFovVelocity, speedFovSmoothTime, Mathf.Infinity, dt);
    }

    private void ApplyCameraOrientation()
    {
        bool approachActive = enableApproachZoom && approachBlend > 0.001f;
        if (!enableDynamicCameraTilt && !approachActive)
        {
            if (orientationApplied)
            {
                transform.rotation = baselineRotation;
                orientationApplied = false;
            }
            return;
        }

        Quaternion rotation = baselineRotation;
        if (approachActive)
        {
            Vector3 toLookPoint = GetApproachLookPoint() - transform.position;
            if (toLookPoint.sqrMagnitude > 0.001f)
            {
                Quaternion lookRotation = Quaternion.LookRotation(toLookPoint, Vector3.up);
                rotation = Quaternion.Slerp(
                    baselineRotation, lookRotation, approachBlend * approachLookWeight);
            }
        }

        if (enableDynamicCameraTilt && Mathf.Abs(cameraTilt) > 0.0001f)
            rotation *= Quaternion.AngleAxis(cameraTilt, Vector3.forward);

        transform.rotation = rotation;
        orientationApplied = true;
    }

    private Vector3 GetApproachLookPoint()
    {
        if (!approachLookPointReady)
        {
            approachLookPoint = ComputeApproachLookPoint();
            approachLookPointReady = true;
        }
        return approachLookPoint;
    }

    private Vector3 ComputeApproachLookPoint()
    {
        Vector3 archPoint;
        if (finishArch != null)
            archPoint = GetVisualCenter(finishArch, finishArch.position + Vector3.up * 3f);
        else if (finishGate != null)
            archPoint = finishGate.transform.position + Vector3.up * 3f;
        else
            archPoint = target.position + Vector3.forward * 10f + Vector3.up * 1.5f;

        Vector3 basketPoint;
        if (finishGate != null && finishGate.BananaTarget != null)
            basketPoint = finishGate.BananaTarget.position + Vector3.up * 0.5f;
        else if (celebrationBasket != null)
            basketPoint = GetVisualCenter(celebrationBasket, archPoint + Vector3.forward * 4f);
        else
            basketPoint = archPoint + Vector3.forward * 4f;

        return Vector3.Lerp(archPoint, basketPoint, 0.5f);
    }

    private static Vector3 GetVisualCenter(Transform root, Vector3 fallback)
    {
        if (root == null) return fallback;
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return fallback;
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);
        return bounds.center;
    }

    private void CaptureLighting()
    {
        originalAmbientMode = RenderSettings.ambientMode;
        originalAmbientLight = RenderSettings.ambientLight;
        originalAmbientSky = RenderSettings.ambientSkyColor;
        originalAmbientEquator = RenderSettings.ambientEquatorColor;
        originalAmbientGround = RenderSettings.ambientGroundColor;
        originalAmbientIntensity = RenderSettings.ambientIntensity;
        sun = RenderSettings.sun;
        if (sun == null)
        {
            foreach (Light candidate in FindObjectsByType<Light>(FindObjectsInactive.Exclude))
            {
                if (candidate.type == LightType.Directional)
                {
                    sun = candidate;
                    break;
                }
            }
        }
        if (sun != null)
        {
            originalSunColor = sun.color;
            originalSunIntensity = sun.intensity;
        }
        lightingCaptured = true;
    }

    private void UpdateZoneLighting(float progress)
    {
        if (!lightingCaptured) return;
        zoneBlend = Mathf.MoveTowards(
            zoneBlend, enableZoneLighting ? 1f : 0f, Time.unscaledDeltaTime / 0.8f);
        if (zoneBlend <= 0f)
        {
            if (lightingApplied) RestoreLighting();
            return;
        }
        ApplyZoneLighting(progress, zoneBlend);
    }

    private void ApplyZoneLighting(float progress, float blend)
    {
        float midBlend = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.30f, 0.42f, progress));
        float goldenBlend = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.74f, 0.86f, progress));

        Color sky = Color.Lerp(Color.Lerp(MorningSky, MidSky, midBlend), GoldenSky, goldenBlend);
        Color equator = Color.Lerp(Color.Lerp(MorningEquator, MidEquator, midBlend), GoldenEquator, goldenBlend);
        Color ground = Color.Lerp(Color.Lerp(MorningGround, MidGround, midBlend), GoldenGround, goldenBlend);
        Color sunColor = Color.Lerp(Color.Lerp(MorningSun, MidSun, midBlend), GoldenSun, goldenBlend);
        float ambientIntensity = Mathf.Lerp(
            Mathf.Lerp(MorningAmbientIntensity, MidAmbientIntensity, midBlend),
            GoldenAmbientIntensity, goldenBlend);
        float sunIntensity = Mathf.Lerp(
            Mathf.Lerp(MorningSunIntensity, MidSunIntensity, midBlend),
            GoldenSunIntensity, goldenBlend);

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientLight = Color.Lerp(originalAmbientLight, sky, blend);
        RenderSettings.ambientSkyColor = Color.Lerp(originalAmbientSky, sky, blend);
        RenderSettings.ambientEquatorColor = Color.Lerp(originalAmbientEquator, equator, blend);
        RenderSettings.ambientGroundColor = Color.Lerp(originalAmbientGround, ground, blend);
        RenderSettings.ambientIntensity = Mathf.Lerp(originalAmbientIntensity, ambientIntensity, blend);

        if (sun != null)
        {
            sun.color = Color.Lerp(originalSunColor, sunColor, blend);
            sun.intensity = Mathf.Lerp(originalSunIntensity, sunIntensity, blend);
        }
        lightingApplied = true;
    }

    private void RestoreLighting()
    {
        if (!lightingCaptured)
        {
            lightingApplied = false;
            zoneBlend = 0f;
            return;
        }

        RenderSettings.ambientMode = originalAmbientMode;
        RenderSettings.ambientLight = originalAmbientLight;
        RenderSettings.ambientSkyColor = originalAmbientSky;
        RenderSettings.ambientEquatorColor = originalAmbientEquator;
        RenderSettings.ambientGroundColor = originalAmbientGround;
        RenderSettings.ambientIntensity = originalAmbientIntensity;
        if (sun != null)
        {
            sun.color = originalSunColor;
            sun.intensity = originalSunIntensity;
        }
        lightingApplied = false;
        zoneBlend = 0f;
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
        if (orientationApplied)
        {
            transform.rotation = baselineRotation;
            orientationApplied = false;
        }
        if (gameObject.scene == UnityEngine.SceneManagement.SceneManager.GetActiveScene())
            RestoreLighting();
        else
            zoneBlend = 0f;
        giantBlend = 0f;
        cameraTilt = 0f;
        cameraTiltVelocity = 0f;
        speedFov = 0f;
        speedFovVelocity = 0f;
        approachBlend = 0f;
        approachVelocity = 0f;
    }
}
