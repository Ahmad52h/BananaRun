using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Runtime-only presentation for Shield, Thunder, Magnet, High Jump and
/// Bulldozer abilities. The component reads the owning runner's ability state
/// each frame, so it works for both the Player and the Rival without adding
/// references to prefabs.
/// </summary>
[DisallowMultipleComponent]
public sealed class RunnerAbilityVisual : MonoBehaviour
{
    private PowerUpManager playerAbilities;
    private RivalRunner rivalAbilities;
    private GameObject shieldShell;
    private Material shieldMaterial;
    private Vector3 shieldBaseScale;
    private Light shieldLight;
    private TrailRenderer thunderTrail;
    private Light thunderLight;
    private GameObject magnetAura;
    private Material magnetAuraMaterial;
    private Light magnetLight;
    private GameObject highJumpAura;
    private Material highJumpAuraMaterial;
    private Light highJumpLight;
    private GameObject bulldozerAura;
    private Material bulldozerAuraMaterial;
    private Light bulldozerLight;
    private float shieldFlashUntil;
    private float pulsePhase;
    private bool configured;

    private bool HasShield => playerAbilities != null ? playerAbilities.HasShield :
        rivalAbilities != null && rivalAbilities.HasShield;
    private bool HasThunder => playerAbilities != null ? playerAbilities.HasThunder :
        rivalAbilities != null && rivalAbilities.HasThunder;
    private bool HasMagnet => playerAbilities != null ? playerAbilities.IsActive(PowerUpType.Magnet) :
        rivalAbilities != null && rivalAbilities.HasMagnet;
    private bool HasHighJump => playerAbilities != null ? playerAbilities.IsActive(PowerUpType.Glider) :
        rivalAbilities != null && rivalAbilities.HasHighJump;
    private bool HasBulldozer => playerAbilities != null ? playerAbilities.IsActive(PowerUpType.Bulldozer) :
        rivalAbilities != null && rivalAbilities.HasBulldozer;

    private void Awake()
    {
        playerAbilities = GetComponent<PowerUpManager>();
        rivalAbilities = GetComponent<RivalRunner>();
        if (playerAbilities == null && rivalAbilities == null)
        {
            enabled = false;
            return;
        }

        pulsePhase = Random.value * Mathf.PI * 2f;
        BuildShield();
        BuildThunderTrail();
        BuildAbilityAuras();
        configured = true;
    }

    private void BuildShield()
    {
        shieldShell = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        shieldShell.name = "Shield Energy Shell";
        var shellCollider = shieldShell.GetComponent<Collider>();
        if (shellCollider != null)
        {
            shellCollider.enabled = false;
            Destroy(shellCollider);
        }
        shieldShell.transform.SetParent(transform, false);
        shieldShell.transform.localPosition = new Vector3(0f, 1.3f, 0f);
        shieldBaseScale = new Vector3(2.05f, 2.3f, 2.05f);
        shieldShell.transform.localScale = shieldBaseScale;

        var renderer = shieldShell.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            shieldMaterial = PowerUpVisualUtility.CreateGlowMaterial(
                new Color(0.28f, 0.88f, 1f, 0.2f));
            renderer.material = shieldMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
        shieldShell.SetActive(false);

        var lightObject = new GameObject("Shield Glow Light");
        lightObject.transform.SetParent(transform, false);
        lightObject.transform.localPosition = new Vector3(0f, 1.3f, 0f);
        shieldLight = lightObject.AddComponent<Light>();
        shieldLight.type = LightType.Point;
        shieldLight.color = new Color(0.3f, 0.9f, 1f);
        shieldLight.range = 3.8f;
        shieldLight.intensity = 1.35f;
        shieldLight.shadows = LightShadows.None;
        shieldLight.enabled = false;
    }

    private void BuildThunderTrail()
    {
        var trailObject = new GameObject("Thunder Speed Trail");
        trailObject.transform.SetParent(transform, false);
        trailObject.transform.localPosition = new Vector3(0f, 0.9f, -0.1f);
        thunderTrail = trailObject.AddComponent<TrailRenderer>();
        thunderTrail.time = 0.42f;
        thunderTrail.minVertexDistance = 0.08f;
        thunderTrail.startWidth = 0.55f;
        thunderTrail.endWidth = 0.03f;
        thunderTrail.autodestruct = false;
        thunderTrail.emitting = false;
        thunderTrail.shadowCastingMode = ShadowCastingMode.Off;
        thunderTrail.receiveShadows = false;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.97f, 0.45f), 0f),
                new GradientColorKey(new Color(1f, 0.62f, 0.08f), 0.55f),
                new GradientColorKey(new Color(1f, 0.3f, 0.02f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.9f, 0f),
                new GradientAlphaKey(0.45f, 0.55f),
                new GradientAlphaKey(0f, 1f)
            });
        thunderTrail.colorGradient = gradient;
        var trailShader = Shader.Find("Sprites/Default");
        if (trailShader != null)
            thunderTrail.material = new Material(trailShader) { name = "Runtime Thunder Trail" };
        else
            thunderTrail.material = PowerUpVisualUtility.CreateGlowMaterial(new Color(1f, 0.9f, 0.3f, 1f), true);

        var lightObject = new GameObject("Thunder Glow Light");
        lightObject.transform.SetParent(transform, false);
        lightObject.transform.localPosition = new Vector3(0f, 0.9f, -0.35f);
        thunderLight = lightObject.AddComponent<Light>();
        thunderLight.type = LightType.Point;
        thunderLight.color = new Color(1f, 0.8f, 0.15f);
        thunderLight.range = 4.5f;
        thunderLight.intensity = 1.25f;
        thunderLight.shadows = LightShadows.None;
        thunderLight.enabled = false;
    }

    private void BuildAbilityAuras()
    {
        // Magnet, high-jump and bulldozer pickups need a visible state on the
        // runner too. These rings read as a subtle, colour-coded aura without
        // obscuring the character or requiring prefab edits.
        magnetAura = BuildAuraRing("Magnet Aura", new Color(0.72f, 0.38f, 1f, 0.56f),
            1.02f, 0.62f, 0.92f, 3.4f, out magnetAuraMaterial, out magnetLight);
        highJumpAura = BuildAuraRing("High Jump Aura", new Color(0.35f, 0.95f, 0.45f, 0.5f),
            0.2f, 0.42f, 0.74f, 2.8f, out highJumpAuraMaterial, out highJumpLight);
        bulldozerAura = BuildAuraRing("Bulldozer Aura", new Color(1f, 0.72f, 0.12f, 0.48f),
            1.55f, 0.48f, 0.78f, 3.2f, out bulldozerAuraMaterial, out bulldozerLight);
    }

    private GameObject BuildAuraRing(string objectName, Color color, float y,
        float innerRadius, float outerRadius, float lightRange,
        out Material material, out Light light)
    {
        var auraObject = new GameObject(objectName);
        auraObject.transform.SetParent(transform, false);
        auraObject.transform.localPosition = new Vector3(0f, y, 0f);

        var filter = auraObject.AddComponent<MeshFilter>();
        filter.mesh = PowerUpVisualUtility.CreateRingMesh(innerRadius, outerRadius);
        var renderer = auraObject.AddComponent<MeshRenderer>();
        material = PowerUpVisualUtility.CreateGlowMaterial(color, true);
        renderer.material = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        light = auraObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.range = lightRange;
        light.intensity = 0f;
        light.shadows = LightShadows.None;
        light.enabled = false;

        auraObject.SetActive(false);
        return auraObject;
    }

    private void UpdateAura(GameObject aura, Material material, Light light, bool active,
        Color color, float pulseSpeed, float spinSpeed, float baseAlpha)
    {
        if (aura != null)
        {
            aura.SetActive(active);
            if (active)
            {
                aura.transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.Self);
                float scale = 1f + Mathf.Sin(Time.time * pulseSpeed + pulsePhase) * 0.08f;
                aura.transform.localScale = new Vector3(scale, 1f, scale);
            }
        }
        if (material != null && active)
        {
            color.a = baseAlpha + 0.18f * (0.5f + 0.5f * Mathf.Sin(Time.time * pulseSpeed + pulsePhase));
            material.SetColor("_BaseColor", color);
            material.SetColor("_Color", color);
        }
        if (light != null)
        {
            light.enabled = active;
            if (active) light.intensity = 1.1f + 0.35f * Mathf.Sin(Time.time * pulseSpeed + pulsePhase);
        }
    }

    private void Update()
    {
        if (!configured) return;

        bool shielded = HasShield;
        if (shieldShell != null)
        {
            shieldShell.SetActive(shielded);
            if (shielded)
            {
                float pulse = 1f + Mathf.Sin(Time.time * 5.5f + pulsePhase) * 0.035f;
                shieldShell.transform.localScale = shieldBaseScale * pulse;
            }
        }
        if (shieldMaterial != null && shielded)
        {
            Color color = new Color(0.28f, 0.88f, 1f, 0.16f + 0.06f * (0.5f + 0.5f * Mathf.Sin(Time.time * 4.5f + pulsePhase)));
            shieldMaterial.SetColor("_BaseColor", color);
            shieldMaterial.SetColor("_Color", color);
        }

        if (shieldLight != null)
        {
            bool flashing = Time.time < shieldFlashUntil;
            shieldLight.enabled = shielded || flashing;
            if (shielded)
                shieldLight.intensity = 1.25f + 0.35f * Mathf.Sin(Time.time * 4.5f + pulsePhase);
            else if (flashing)
                shieldLight.intensity = Mathf.Lerp(5f, 0f,
                    Mathf.InverseLerp(shieldFlashUntil - 0.28f, shieldFlashUntil, Time.time));
        }

        bool thundering = HasThunder;
        if (thunderTrail != null) thunderTrail.emitting = thundering;
        if (thunderLight != null)
        {
            thunderLight.enabled = thundering;
            if (thundering)
                thunderLight.intensity = 1.15f + 0.35f * Mathf.Sin(Time.time * 7f + pulsePhase);
        }

        UpdateAura(magnetAura, magnetAuraMaterial, magnetLight, HasMagnet,
            PowerUpVisualUtility.ColorFor(PowerUpType.Magnet), 4.2f, 38f, 0.48f);
        UpdateAura(highJumpAura, highJumpAuraMaterial, highJumpLight, HasHighJump,
            PowerUpVisualUtility.ColorFor(PowerUpType.Glider), 3.4f, 24f, 0.42f);
        UpdateAura(bulldozerAura, bulldozerAuraMaterial, bulldozerLight, HasBulldozer,
            PowerUpVisualUtility.ColorFor(PowerUpType.Bulldozer), 2.6f, 18f, 0.4f);
    }

    /// <summary>Brief bright pulse when a shield consumes an incoming hit.</summary>
    public void PlayShieldBreak()
    {
        if (shieldLight == null) return;
        shieldFlashUntil = Time.time + 0.28f;
        shieldLight.enabled = true;
        shieldLight.intensity = 5f;
    }

    private void OnDisable()
    {
        if (shieldShell != null) shieldShell.SetActive(false);
        if (shieldLight != null) shieldLight.enabled = false;
        if (thunderTrail != null) thunderTrail.emitting = false;
        if (thunderLight != null) thunderLight.enabled = false;
        if (magnetAura != null) magnetAura.SetActive(false);
        if (magnetLight != null) magnetLight.enabled = false;
        if (highJumpAura != null) highJumpAura.SetActive(false);
        if (highJumpLight != null) highJumpLight.enabled = false;
        if (bulldozerAura != null) bulldozerAura.SetActive(false);
        if (bulldozerLight != null) bulldozerLight.enabled = false;
    }
}
