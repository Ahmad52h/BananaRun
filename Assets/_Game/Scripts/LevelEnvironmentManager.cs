using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>Distance-driven weather; owns global environment settings independently of the camera.</summary>
[DefaultExecutionOrder(400)]
[DisallowMultipleComponent]
public sealed class LevelEnvironmentManager : MonoBehaviour
{
    [Serializable]
    public sealed class Phase
    {
        public Color sunlight = Color.white;
        [Min(0f)] public float sunIntensity = 1f;
        [Range(5f, 85f)] public float sunElevation = 50f;
        [Range(0f, 1f)] public float shadowStrength = 0.8f;
        public Color ambientSky, ambientEquator, ambientGround;
        public Color fogColor;
        [Min(0f)] public float fogDensity;
        public Color skyTint, skyGround;
        [Min(0f)] public float skyExposure = 1f;
        [Range(0f, 5f)] public float atmosphereThickness = 1f;
    }

    [Header("Track references")]
    [SerializeField] private RunnerMotor player;
    [SerializeField] private RaceProgressTracker race;
    [SerializeField] private Transform finishLine;
    [SerializeField] private Light sunlight;
    [Tooltip("A Skybox/Procedural material. A private runtime copy is animated; the asset stays unchanged.")]
    [SerializeField] private Material skyboxTemplate;
    [SerializeField] private ParticleSystem jungleLeaves;
    [Tooltip("Used only when neither the race tracker nor a finish line is available.")]
    [SerializeField, Min(1f)] private float fallbackTrackLength = 900f;

    [Header("Distance transitions")]
    [SerializeField, Range(0.05f, 0.9f)] private float jungleStart = 1f / 3f;
    [SerializeField, Range(0.1f, 0.95f)] private float sunsetStart = 2f / 3f;
    [Tooltip("Fraction of the complete track occupied by each blend, centered on its boundary.")]
    [SerializeField, Range(0.01f, 0.25f)] private float transitionWidth = 0.08f;

    [Header("Sunny morning")]
    [SerializeField] private Phase morning = new Phase
    {
        sunlight = new Color(1f, 0.94f, 0.78f), sunIntensity = 1.15f, sunElevation = 55f,
        shadowStrength = 0.8f,
        ambientSky = new Color(0.58f, 0.75f, 0.78f),
        ambientEquator = new Color(0.48f, 0.62f, 0.36f),
        ambientGround = new Color(0.31f, 0.35f, 0.22f),
        fogColor = new Color(0.68f, 0.83f, 0.88f), fogDensity = 0f,
        skyTint = new Color(0.48f, 0.65f, 0.85f), skyGround = new Color(0.42f, 0.52f, 0.30f),
        skyExposure = 1.15f, atmosphereThickness = 1f
    };

    [Header("Misty jungle")]
    [SerializeField] private Phase jungle = new Phase
    {
        sunlight = new Color(0.80f, 0.90f, 0.72f), sunIntensity = 0.62f, sunElevation = 60f,
        shadowStrength = 0.45f,
        ambientSky = new Color(0.35f, 0.48f, 0.43f),
        ambientEquator = new Color(0.28f, 0.39f, 0.25f),
        ambientGround = new Color(0.18f, 0.25f, 0.17f),
        fogColor = new Color(0.48f, 0.59f, 0.52f), fogDensity = 0.018f,
        skyTint = new Color(0.40f, 0.49f, 0.45f), skyGround = new Color(0.25f, 0.34f, 0.25f),
        skyExposure = 0.65f, atmosphereThickness = 1.4f
    };

    [Header("Golden sunset")]
    [SerializeField] private Phase sunset = new Phase
    {
        sunlight = new Color(1f, 0.75f, 0.4f), sunIntensity = 1.05f, sunElevation = 12f,
        shadowStrength = 0.9f,
        ambientSky = new Color(0.70f, 0.55f, 0.35f),
        ambientEquator = new Color(0.58f, 0.43f, 0.22f),
        ambientGround = new Color(0.33f, 0.26f, 0.13f),
        fogColor = new Color(0.90f, 0.64f, 0.35f), fogDensity = 0.006f,
        skyTint = new Color(0.72f, 0.45f, 0.26f), skyGround = new Color(0.48f, 0.31f, 0.17f),
        skyExposure = 1.05f, atmosphereThickness = 1.65f
    };

    [Header("Falling leaves")]
    [SerializeField] private Vector3 leavesOffset = new Vector3(0f, 8f, 12f);
    [SerializeField, Min(0f)] private float jungleLeavesPerSecond = 22f;

    public float Progress { get; private set; }
    public float JungleWeight { get; private set; }
    public float SunsetWeight { get; private set; }
    public ParticleSystem JungleLeaves => jungleLeaves;
    public Light Sunlight => sunlight;

    private bool started, captured;
    private float startZ, groundY;
    private Material originalSkybox, runtimeSkybox;
    private Light originalSun;
    private AmbientMode originalAmbientMode;
    private Color originalSky, originalEquator, originalGround, originalAmbient, originalFogColor;
    private float originalAmbientIntensity, originalFogDensity, originalFogStart, originalFogEnd;
    private bool originalFog;
    private FogMode originalFogMode;
    private Color originalSunColor;
    private float originalSunIntensity, originalShadowStrength;
    private Quaternion originalSunRotation;
    private LightShadows originalShadows;
    private bool originalUseColorTemperature;

    private void Start()
    {
        if (player == null) player = FindAnyObjectByType<RunnerMotor>();
        if (race == null) race = FindAnyObjectByType<RaceProgressTracker>();
        if (finishLine == null)
        {
            var gate = FindAnyObjectByType<FinishGate>();
            if (gate != null) finishLine = gate.transform;
        }
        if (sunlight == null) sunlight = RenderSettings.sun;
        if (player == null || sunlight == null || sunlight.type != LightType.Directional)
        {
            Debug.LogError("LevelEnvironmentManager needs a player and a directional sunlight reference.", this);
            enabled = false;
            return;
        }
        startZ = player.transform.position.z;
        groundY = player.transform.position.y;
        started = true;
        CaptureEnvironment();
        UpdateEnvironment();
    }

    private void OnEnable()
    {
        if (!started) return;
        CaptureEnvironment();
        UpdateEnvironment();
    }

    private void LateUpdate() => UpdateEnvironment();

    private void UpdateEnvironment()
    {
        if (!captured || player == null) return;
        float length = finishLine != null ? Mathf.Max(1f, finishLine.position.z - startZ) : fallbackTrackLength;
        float progress = race != null && race.isActiveAndEnabled && race.Player == player
            ? race.PlayerProgress : (player.transform.position.z - startZ) / Mathf.Max(1f, length);
        ApplyProgress(progress);
    }

    /// <summary>Applies a normalized distance, also usable by distance triggers or preview tools.</summary>
    public void ApplyProgress(float progress)
    {
        if (!captured) return;
        Progress = Mathf.Clamp01(progress);
        EvaluateWeights(Progress, out float jungleBlend, out float sunsetBlend);
        JungleWeight = jungleBlend * (1f - sunsetBlend);
        SunsetWeight = sunsetBlend;

        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientIntensity = 1f;
        RenderSettings.ambientSkyColor = Blend(morning.ambientSky, jungle.ambientSky, sunset.ambientSky, jungleBlend, sunsetBlend);
        RenderSettings.ambientEquatorColor = Blend(morning.ambientEquator, jungle.ambientEquator, sunset.ambientEquator, jungleBlend, sunsetBlend);
        RenderSettings.ambientGroundColor = Blend(morning.ambientGround, jungle.ambientGround, sunset.ambientGround, jungleBlend, sunsetBlend);
        // Keep one fog variant throughout the run. Density zero gives a clear morning
        // without a shader keyword switch when the mist starts.
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogDensity = Blend(morning.fogDensity, jungle.fogDensity, sunset.fogDensity, jungleBlend, sunsetBlend);
        RenderSettings.fogColor = Blend(morning.fogColor, jungle.fogColor, sunset.fogColor, jungleBlend, sunsetBlend);
        if (sunlight != null)
        {
            sunlight.color = Blend(morning.sunlight, jungle.sunlight, sunset.sunlight, jungleBlend, sunsetBlend);
            sunlight.intensity = Blend(morning.sunIntensity, jungle.sunIntensity, sunset.sunIntensity, jungleBlend, sunsetBlend);
            sunlight.shadowStrength = Blend(morning.shadowStrength, jungle.shadowStrength, sunset.shadowStrength, jungleBlend, sunsetBlend);
            sunlight.shadows = LightShadows.Soft;
            sunlight.useColorTemperature = false;
            float elevation = Blend(morning.sunElevation, jungle.sunElevation, sunset.sunElevation, jungleBlend, sunsetBlend);
            Vector3 angles = originalSunRotation.eulerAngles;
            sunlight.transform.rotation = Quaternion.Euler(elevation, angles.y, angles.z);
        }
        if (runtimeSkybox != null)
        {
            runtimeSkybox.SetColor("_SkyTint", Blend(morning.skyTint, jungle.skyTint, sunset.skyTint, jungleBlend, sunsetBlend));
            runtimeSkybox.SetColor("_GroundColor", Blend(morning.skyGround, jungle.skyGround, sunset.skyGround, jungleBlend, sunsetBlend));
            runtimeSkybox.SetFloat("_Exposure", Blend(morning.skyExposure, jungle.skyExposure, sunset.skyExposure, jungleBlend, sunsetBlend));
            runtimeSkybox.SetFloat("_AtmosphereThickness", Blend(morning.atmosphereThickness, jungle.atmosphereThickness, sunset.atmosphereThickness, jungleBlend, sunsetBlend));
        }
        if (jungleLeaves != null)
        {
            jungleLeaves.transform.position = new Vector3(player.transform.position.x + leavesOffset.x,
                groundY + leavesOffset.y, player.transform.position.z + leavesOffset.z);
            var emission = jungleLeaves.emission;
            emission.rateOverTime = jungleLeavesPerSecond * JungleWeight;
            if (JungleWeight > 0f && !jungleLeaves.isPlaying) jungleLeaves.Play(false);
            // Existing leaves finish their lifetime as emission fades out of the jungle.
            if (JungleWeight <= 0f && jungleLeaves.isPlaying)
                jungleLeaves.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    public void EvaluateWeights(float progress, out float jungleBlend, out float sunsetBlend)
    {
        float first = Mathf.Clamp(jungleStart, 0.05f, 0.9f);
        float second = Mathf.Clamp(sunsetStart, first + 0.01f, 0.95f);
        float halfWidth = Mathf.Min(Mathf.Max(0.001f, transitionWidth) * 0.5f,
            Mathf.Min(first, Mathf.Min(1f - second, (second - first) * 0.5f)));
        jungleBlend = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(first - halfWidth, first + halfWidth, progress));
        sunsetBlend = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(second - halfWidth, second + halfWidth, progress));
    }

    private static Color Blend(Color a, Color b, Color c, float j, float s) => Color.Lerp(Color.Lerp(a, b, j), c, s);
    private static float Blend(float a, float b, float c, float j, float s) => Mathf.Lerp(Mathf.Lerp(a, b, j), c, s);

    private void CaptureEnvironment()
    {
        originalSkybox = RenderSettings.skybox;
        originalSun = RenderSettings.sun;
        originalAmbientMode = RenderSettings.ambientMode;
        originalAmbient = RenderSettings.ambientLight;
        originalSky = RenderSettings.ambientSkyColor;
        originalEquator = RenderSettings.ambientEquatorColor;
        originalGround = RenderSettings.ambientGroundColor;
        originalAmbientIntensity = RenderSettings.ambientIntensity;
        originalFog = RenderSettings.fog;
        originalFogMode = RenderSettings.fogMode;
        originalFogColor = RenderSettings.fogColor;
        originalFogDensity = RenderSettings.fogDensity;
        originalFogStart = RenderSettings.fogStartDistance;
        originalFogEnd = RenderSettings.fogEndDistance;
        originalSunColor = sunlight.color;
        originalSunIntensity = sunlight.intensity;
        originalSunRotation = sunlight.transform.rotation;
        originalShadowStrength = sunlight.shadowStrength;
        originalShadows = sunlight.shadows;
        originalUseColorTemperature = sunlight.useColorTemperature;
        RenderSettings.sun = sunlight;
        Material template = skyboxTemplate != null ? skyboxTemplate : originalSkybox;
        if (template != null && template.HasProperty("_SkyTint") && template.HasProperty("_AtmosphereThickness"))
        {
            runtimeSkybox = new Material(template) { name = "Level sky (runtime)", hideFlags = HideFlags.DontSave };
            RenderSettings.skybox = runtimeSkybox;
        }
        else Debug.LogWarning("Assign a Skybox/Procedural material to LevelEnvironmentManager to animate the sky.", this);
        if (jungleLeaves != null) jungleLeaves.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        captured = true;
    }

    private void OnDisable()
    {
        if (!captured) return;
        // A newly loaded scene already owns RenderSettings during old-scene teardown.
        if (gameObject.scene == SceneManager.GetActiveScene()) RestoreGlobals();
        if (sunlight != null)
        {
            sunlight.color = originalSunColor;
            sunlight.intensity = originalSunIntensity;
            sunlight.transform.rotation = originalSunRotation;
            sunlight.shadowStrength = originalShadowStrength;
            sunlight.shadows = originalShadows;
            sunlight.useColorTemperature = originalUseColorTemperature;
        }
        if (jungleLeaves != null) jungleLeaves.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (runtimeSkybox != null) Destroy(runtimeSkybox);
        runtimeSkybox = null;
        captured = false;
    }

    private void RestoreGlobals()
    {
        RenderSettings.skybox = originalSkybox;
        RenderSettings.sun = originalSun;
        RenderSettings.ambientMode = originalAmbientMode;
        RenderSettings.ambientLight = originalAmbient;
        RenderSettings.ambientSkyColor = originalSky;
        RenderSettings.ambientEquatorColor = originalEquator;
        RenderSettings.ambientGroundColor = originalGround;
        RenderSettings.ambientIntensity = originalAmbientIntensity;
        RenderSettings.fog = originalFog;
        RenderSettings.fogMode = originalFogMode;
        RenderSettings.fogColor = originalFogColor;
        RenderSettings.fogDensity = originalFogDensity;
        RenderSettings.fogStartDistance = originalFogStart;
        RenderSettings.fogEndDistance = originalFogEnd;
    }
}
