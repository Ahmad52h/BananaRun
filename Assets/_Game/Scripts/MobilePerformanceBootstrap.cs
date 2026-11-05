using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>Mobile GPU budget applied to runtime state, never to authored pipeline/scene assets.</summary>
[DefaultExecutionOrder(9000)]
public sealed class MobilePerformanceBootstrap : MonoBehaviour
{
    private static MobilePerformanceBootstrap active;
    private static readonly List<Material> sharedMaterials = new List<Material>(8);
    private UniversalRenderPipelineAsset runtimePipeline;
    private RenderPipelineAsset previousQualityPipeline;
    private int previousFrameRate, previousVSync;
    private float previousShadowDistance;
    private bool mobileProfile, pendingScene = true;

    public static bool IsMobileProfileActive => Application.isMobilePlatform || active != null && active.mobileProfile;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        active = null;
        sharedMaterials.Clear();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (active != null) return;
        var host = new GameObject("Mobile Performance (Runtime)");
        DontDestroyOnLoad(host);
        host.AddComponent<MobilePerformanceBootstrap>();
    }

    private void Awake()
    {
        active = this;
        SceneManager.sceneLoaded += OnSceneLoaded;
        if (Application.isMobilePlatform) ApplyMobileProfile();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => pendingScene = true;

    /// <summary>Also callable in Play Mode to validate the mobile budget in the Editor.</summary>
    public void ApplyMobileProfile()
    {
        if (mobileProfile) return;
        mobileProfile = true;
        previousFrameRate = Application.targetFrameRate;
        previousVSync = QualitySettings.vSyncCount;
        previousShadowDistance = QualitySettings.shadowDistance;
        previousQualityPipeline = QualitySettings.renderPipeline;
        Application.targetFrameRate = 60;
        QualitySettings.vSyncCount = 0;
        QualitySettings.shadowDistance = Mathf.Min(QualitySettings.shadowDistance, 30f);

        var source = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (source != null)
        {
            runtimePipeline = Instantiate(source);
            runtimePipeline.name = source.name + " (Mobile Runtime)";
            runtimePipeline.hideFlags = HideFlags.DontSave;
            runtimePipeline.renderScale = Mathf.Min(source.renderScale, 0.8f);
            runtimePipeline.supportsHDR = false;
            runtimePipeline.shadowDistance = Mathf.Min(source.shadowDistance, 30f);
            runtimePipeline.shadowCascadeCount = 1;
            runtimePipeline.mainLightShadowmapResolution = Mathf.Min(source.mainLightShadowmapResolution, 1024);
            runtimePipeline.maxAdditionalLightsCount = Mathf.Min(source.maxAdditionalLightsCount, 2);
            runtimePipeline.useSRPBatcher = true;
            QualitySettings.renderPipeline = runtimePipeline;
        }
        pendingScene = true;
    }

    private void LateUpdate()
    {
        if (!pendingScene) return;
        pendingScene = false;
        var scene = SceneManager.GetActiveScene();
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true)) PrepareRenderer(renderer, false);
            if (!mobileProfile) continue;
            foreach (var camera in root.GetComponentsInChildren<Camera>(true))
            {
                camera.allowHDR = false;
                // Environment colour/fog is driven by LevelEnvironmentManager, not a grading volume.
                if (camera.TryGetComponent<UniversalAdditionalCameraData>(out var data)) data.renderPostProcessing = false;
            }
            foreach (var light in root.GetComponentsInChildren<Light>(true))
                if (light.shadows != LightShadows.None)
                    light.shadows = light.type == LightType.Directional ? LightShadows.Hard : LightShadows.None;
        }
        if (scene.name == "Level01")
        {
            if (mobileProfile)
            {
                TrackDistanceStreamer.RefreshRenderSettings();
                ProceduralTrackPool.RefreshRenderSettings();
            }
            // Single-scene loads already destroy these. Cover an additive menu transition too.
            foreach (var menu in FindObjectsByType<MainMenuDiorama>(FindObjectsInactive.Include))
                menu.gameObject.SetActive(false);
        }
    }

    public static void PrepareTrackObject(GameObject target, bool smallProp)
    {
        foreach (var renderer in target.GetComponentsInChildren<Renderer>(true)) PrepareRenderer(renderer, smallProp);
    }

    private static void PrepareRenderer(Renderer renderer, bool smallProp)
    {
        renderer.GetSharedMaterials(sharedMaterials);
        foreach (var material in sharedMaterials)
            if (material != null && !material.enableInstancing) material.enableInstancing = true;
        sharedMaterials.Clear();
        if (IsMobileProfileActive && (smallProp || renderer is ParticleSystemRenderer || renderer is TrailRenderer))
        {
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (active == this) active = null;
        if (!mobileProfile) return;
        if (QualitySettings.renderPipeline == runtimePipeline) QualitySettings.renderPipeline = previousQualityPipeline;
        Application.targetFrameRate = previousFrameRate;
        QualitySettings.vSyncCount = previousVSync;
        QualitySettings.shadowDistance = previousShadowDistance;
        if (runtimePipeline != null) Destroy(runtimePipeline);
    }
}
