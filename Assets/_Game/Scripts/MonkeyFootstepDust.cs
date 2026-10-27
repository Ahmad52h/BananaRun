using System;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// The single world-space dust emitter for the runner's feet.
/// RunnerMotor owns movement; this component only toggles emission and emits
/// one extra puff when the runner lands.
/// </summary>
[DefaultExecutionOrder(50)]
[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
public sealed class MonkeyFootstepDust : MonoBehaviour
{
    private const float RunEmissionRate = 22f;
    private const int LandingParticleCount = 10;

    // Local fallback used when the collider/renderer cannot provide a foot point.
    private static readonly Vector3 BehindHeels = new Vector3(0f, 0.03f, -0.15f);
    private static readonly Color DustTint = new Color(0.82f, 0.68f, 0.45f, 1f);

    [SerializeField] private ParticleSystem dustSystem;
    [SerializeField] private Material dustMaterial;

    private CharacterController controller;
    private PlayerHealth health;
    private SkinnedMeshRenderer feetRenderer;
    private Material generatedMaterial;
    private Texture2D generatedTexture;
    private bool groundStateKnown;
    private bool wasGrounded;
    private bool materialReady;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        health = GetComponent<PlayerHealth>();
        feetRenderer = GetComponentInChildren<SkinnedMeshRenderer>(true);

        if (dustSystem == null)
            dustSystem = FindExistingDustSystem();

        // Remove any other dust emitter left by old scene/prefab setups.
        RemoveDuplicateDustSystems();

        if (dustSystem == null)
            dustSystem = CreateDustSystem();

        ConfigureDustSystem();
    }

    private void OnEnable()
    {
        if (dustSystem != null && !dustSystem.isPlaying)
            dustSystem.Play();

        groundStateKnown = false;
    }

    private void Update()
    {
        if (dustSystem == null)
            return;

        bool isGrounded = controller != null && controller.isGrounded;
        bool isGameOver = health != null && health.IsRunOver;
        bool shouldEmit = isGrounded && !isGameOver;

        if (isGrounded)
            PositionAtFeet();

        // Toggle emission only. Never Play/Stop every frame: already-emitted
        // particles keep finishing their lifetime in world space while jumping.
        var emission = dustSystem.emission;
        emission.rateOverTime = isGrounded ? RunEmissionRate : 0f;
        emission.enabled = shouldEmit;

        if (!groundStateKnown)
        {
            wasGrounded = isGrounded;
            groundStateKnown = true;
        }
        else if (isGrounded && !wasGrounded && !isGameOver)
        {
            PositionAtFeet();
            dustSystem.Emit(LandingParticleCount);
        }

        wasGrounded = isGrounded;
    }

    /// <summary>Stops and clears the emitter when the runner is disabled.</summary>
    public void StopAllDust()
    {
        if (dustSystem == null)
            return;

        var emission = dustSystem.emission;
        emission.enabled = false;
        dustSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        groundStateKnown = false;
        wasGrounded = false;
    }

    private ParticleSystem FindExistingDustSystem()
    {
        ParticleSystem fallback = null;
        foreach (var candidate in GetComponentsInChildren<ParticleSystem>(true))
        {
            string objectName = candidate.gameObject.name;
            if (objectName.Equals("RunDust", StringComparison.OrdinalIgnoreCase) ||
                objectName.Equals("Run Dust", StringComparison.OrdinalIgnoreCase) ||
                objectName.Equals("Footstep Dust", StringComparison.OrdinalIgnoreCase))
                return candidate;

            if (fallback == null && LooksLikeDust(candidate))
                fallback = candidate;
        }

        return fallback;
    }

    private void RemoveDuplicateDustSystems()
    {
        foreach (var candidate in GetComponentsInChildren<ParticleSystem>(true))
        {
            if (candidate == dustSystem || !LooksLikeDust(candidate))
                continue;

            candidate.gameObject.SetActive(false);
            Destroy(candidate.gameObject);
        }
    }

    private static bool LooksLikeDust(ParticleSystem system)
    {
        if (system == null)
            return false;

        string objectName = system.gameObject.name;
        return objectName.IndexOf("dust", StringComparison.OrdinalIgnoreCase) >= 0 ||
               objectName.Equals("RunDust", StringComparison.OrdinalIgnoreCase);
    }

    private ParticleSystem CreateDustSystem()
    {
        var dustObject = new GameObject("Footstep Dust", typeof(ParticleSystem));
        dustObject.transform.SetParent(transform, false);
        dustObject.transform.localPosition = BehindHeels;
        return dustObject.GetComponent<ParticleSystem>();
    }

    private void ConfigureDustSystem()
    {
        if (dustSystem == null)
            return;

        dustSystem.gameObject.name = "Footstep Dust";
        dustSystem.gameObject.SetActive(true);
        dustSystem.transform.SetParent(transform, false);
        dustSystem.transform.localScale = Vector3.one;
        dustSystem.transform.localPosition = BehindHeels;

        // Unity's cone points along local +Z. This aims it up and slightly
        // backward, so puffs kick up behind the heels instead of sideways.
        dustSystem.transform.localRotation = Quaternion.Euler(-105f, 0f, 0f);

        dustSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = dustSystem.main;
        main.playOnAwake = false;
        main.prewarm = false;
        main.loop = true;
        main.duration = 1f;
        main.simulationSpeed = 1f;
        main.startDelay = 0f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.55f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.32f);
        main.startSize3D = false;
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
        main.startRotation3D = false;
        main.startColor = new ParticleSystem.MinMaxGradient(Color.white);
        main.gravityModifier = -0.05f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = 64;

        // The scene has a legacy RunDust object; strip any extra modules that
        // could make it spread sideways or otherwise fight the narrow cone.
        var velocity = dustSystem.velocityOverLifetime;
        velocity.enabled = false;
        var limitVelocity = dustSystem.limitVelocityOverLifetime;
        limitVelocity.enabled = false;
        var inheritVelocity = dustSystem.inheritVelocity;
        inheritVelocity.enabled = false;
        var force = dustSystem.forceOverLifetime;
        force.enabled = false;
        var colorBySpeed = dustSystem.colorBySpeed;
        colorBySpeed.enabled = false;
        var sizeBySpeed = dustSystem.sizeBySpeed;
        sizeBySpeed.enabled = false;
        var rotationOverLifetime = dustSystem.rotationOverLifetime;
        rotationOverLifetime.enabled = false;
        var rotationBySpeed = dustSystem.rotationBySpeed;
        rotationBySpeed.enabled = false;
        var externalForces = dustSystem.externalForces;
        externalForces.enabled = false;
        var noise = dustSystem.noise;
        noise.enabled = false;
        var collision = dustSystem.collision;
        collision.enabled = false;
        var trigger = dustSystem.trigger;
        trigger.enabled = false;
        var subEmitters = dustSystem.subEmitters;
        subEmitters.enabled = false;
        var lights = dustSystem.lights;
        lights.enabled = false;
        var trails = dustSystem.trails;
        trails.enabled = false;
        var customData = dustSystem.customData;
        customData.enabled = false;
        var textureSheetAnimation = dustSystem.textureSheetAnimation;
        textureSheetAnimation.enabled = false;

        var emission = dustSystem.emission;
        emission.enabled = false;
        emission.rateOverTime = RunEmissionRate;
        emission.rateOverDistance = 0f;
        emission.SetBursts(Array.Empty<ParticleSystem.Burst>());

        var shape = dustSystem.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 22f;
        shape.arc = 360f;
        shape.radius = 0.08f;
        shape.radiusThickness = 1f;
        shape.randomDirectionAmount = 0f;
        shape.sphericalDirectionAmount = 0f;
        shape.randomPositionAmount = 0f;
        shape.position = Vector3.zero;
        shape.rotation = Vector3.zero;
        shape.scale = Vector3.one;
        shape.alignToDirection = false;

        // Small tight puff -> quick expansion -> gentle settle.
        var size = dustSystem.sizeOverLifetime;
        size.enabled = true;
        size.separateAxes = false;
        size.sizeMultiplier = 1f;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.4f),
            new Keyframe(0.18f, 1.2f),
            new Keyframe(1f, 0.65f)));

        var fade = dustSystem.colorOverLifetime;
        fade.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(DustTint, 0f),
                new GradientColorKey(DustTint, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.55f, 0.15f),
                new GradientAlphaKey(0f, 1f)
            });
        fade.color = new ParticleSystem.MinMaxGradient(gradient);

        var renderer = dustSystem.GetComponent<ParticleSystemRenderer>();
        if (renderer == null)
            renderer = dustSystem.gameObject.AddComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.alignment = ParticleSystemRenderSpace.View;
        renderer.sortMode = ParticleSystemSortMode.Distance;
        renderer.minParticleSize = 0.01f;
        renderer.maxParticleSize = 0.5f;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        Material material = ResolveMaterial();
        materialReady = material != null;
        renderer.sharedMaterial = material;
        renderer.enabled = materialReady;

        dustSystem.Play();
    }

    private void PositionAtFeet()
    {
        if (dustSystem == null)
            return;

        Vector3 position = transform.TransformPoint(new Vector3(0f, 0f, BehindHeels.z));
        position.y = GetFeetBottomY() + 0.03f;
        dustSystem.transform.position = position;
    }

    private float GetFeetBottomY()
    {
        if (controller != null && controller.enabled)
        {
            Bounds controllerBounds = controller.bounds;
            if (controllerBounds.size.y > 0.0001f)
                return controllerBounds.min.y;
        }

        if (feetRenderer != null)
        {
            Bounds rendererBounds = feetRenderer.bounds;
            if (rendererBounds.size.y > 0.0001f)
                return rendererBounds.min.y;
        }

        return transform.position.y;
    }

    private Material ResolveMaterial()
    {
        // Keep an explicitly assigned, supported material.
        if (dustMaterial != null && dustMaterial.shader != null && dustMaterial.shader.isSupported)
            return dustMaterial;

        if (generatedMaterial != null)
            return generatedMaterial;

        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null || !shader.isSupported)
            shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null || !shader.isSupported)
            shader = Shader.Find("Sprites/Default");
        if (shader == null || !shader.isSupported)
        {
            Debug.LogWarning("MonkeyFootstepDust: no supported transparent particle shader found.", this);
            return null;
        }

        generatedTexture = CreateSoftCloudTexture();
        generatedMaterial = new Material(shader) { name = "Footstep Dust (Generated)" };

        if (generatedMaterial.HasProperty("_BaseMap"))
            generatedMaterial.SetTexture("_BaseMap", generatedTexture);
        if (generatedMaterial.HasProperty("_MainTex"))
            generatedMaterial.SetTexture("_MainTex", generatedTexture);
        if (generatedMaterial.HasProperty("_BaseColor"))
            generatedMaterial.SetColor("_BaseColor", Color.white);
        if (generatedMaterial.HasProperty("_Color"))
            generatedMaterial.SetColor("_Color", Color.white);
        if (generatedMaterial.HasProperty("_Surface"))
            generatedMaterial.SetFloat("_Surface", 1f);
        if (generatedMaterial.HasProperty("_Blend"))
            generatedMaterial.SetFloat("_Blend", 0f);
        if (generatedMaterial.HasProperty("_SrcBlend"))
            generatedMaterial.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (generatedMaterial.HasProperty("_DstBlend"))
            generatedMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (generatedMaterial.HasProperty("_ZWrite"))
            generatedMaterial.SetFloat("_ZWrite", 0f);
        if (generatedMaterial.HasProperty("_Cull"))
            generatedMaterial.SetFloat("_Cull", 0f);
        if (generatedMaterial.HasProperty("_AlphaClip"))
            generatedMaterial.SetFloat("_AlphaClip", 0f);
        if (generatedMaterial.HasProperty("_Mode"))
            generatedMaterial.SetFloat("_Mode", 2f);

        generatedMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        generatedMaterial.EnableKeyword("_ALPHABLEND_ON");
        generatedMaterial.DisableKeyword("_ALPHATEST_ON");
        generatedMaterial.SetOverrideTag("RenderType", "Transparent");
        generatedMaterial.renderQueue = (int)RenderQueue.Transparent;
        return generatedMaterial;
    }

    private Texture2D CreateSoftCloudTexture()
    {
        const int resolution = 64;
        var texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false)
        {
            name = "Procedural Soft Dust Cloud",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            anisoLevel = 1
        };

        var pixels = new Color[resolution * resolution];
        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float dx = (x + 0.5f) / resolution * 2f - 1f;
                float dy = (y + 0.5f) / resolution * 2f - 1f;
                float radius = Mathf.Sqrt(dx * dx + dy * dy);
                float radial = Mathf.Clamp01(1f - radius);
                radial = radial * radial * (3f - 2f * radial);

                float noise1 = Mathf.PerlinNoise(x * 0.13f + 12.3f, y * 0.13f + 31.7f);
                float noise2 = Mathf.PerlinNoise(x * 0.31f + 73.1f, y * 0.31f + 5.9f);
                float noise = Mathf.Lerp(0.65f, 1.15f, noise1) * Mathf.Lerp(0.85f, 1.05f, noise2);

                float lobe1 = Mathf.Clamp01(1f - Mathf.Sqrt(
                    (dx - 0.18f) * (dx - 0.18f) + (dy + 0.12f) * (dy + 0.12f)) * 1.35f);
                float lobe2 = Mathf.Clamp01(1f - Mathf.Sqrt(
                    (dx + 0.16f) * (dx + 0.16f) + (dy - 0.1f) * (dy - 0.1f)) * 1.55f);

                float alpha = Mathf.Clamp01(radial * noise + lobe1 * 0.2f + lobe2 * 0.14f);
                alpha *= Mathf.Clamp01((1f - radius) / 0.12f);
                pixels[y * resolution + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return texture;
    }

    private void OnDisable()
    {
        StopAllDust();
    }

    private void OnDestroy()
    {
        if (generatedMaterial != null)
            Destroy(generatedMaterial);
        if (generatedTexture != null)
            Destroy(generatedTexture);
    }
}
