using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
public class PlayerDustEffects : MonoBehaviour
{
    [SerializeField] private ParticleSystem runDustParticles;
    [SerializeField] private ParticleSystem jumpDustParticles;

    private CharacterController controller;
    private bool generatedRun;
    private bool generatedJump;
    private bool running;
    private Material generatedMaterial;
    private Texture2D softDustTexture;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (runDustParticles == null)
        {
            runDustParticles = CreateDust("RunDustParticles", true);
            generatedRun = true;
        }
        if (jumpDustParticles == null)
        {
            jumpDustParticles = CreateDust("JumpDustParticles", false);
            generatedJump = true;
        }
    }

    public void SetRunning(bool shouldRun)
    {
        if (runDustParticles == null || running == shouldRun) return;
        running = shouldRun;
        if (running)
        {
            PositionAtFeet(runDustParticles, generatedRun);
            runDustParticles.Play();
        }
        else
            runDustParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    public void EmitJump()
    {
        if (jumpDustParticles == null) return;
        PositionAtFeet(jumpDustParticles, generatedJump);
        jumpDustParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        jumpDustParticles.Emit(12);
    }

    private void LateUpdate()
    {
        if (running) PositionAtFeet(runDustParticles, generatedRun);
    }

    private void PositionAtFeet(ParticleSystem particles, bool generated)
    {
        if (!generated || particles == null) return;
        float groundY = controller != null && controller.enabled
            ? controller.bounds.min.y + 0.03f : transform.position.y + 0.03f;
        particles.transform.position = new Vector3(transform.position.x, groundY, transform.position.z - 0.15f);
    }

    private ParticleSystem CreateDust(string objectName, bool continuous)
    {
        GameObject dustObject = new GameObject(objectName);
        dustObject.transform.SetParent(transform, false);
        ParticleSystem particles = dustObject.AddComponent<ParticleSystem>();

        var main = particles.main;
        main.playOnAwake = false;
        main.loop = continuous;
        main.duration = 0.6f;
        main.startLifetime = continuous ? 0.3f : 0.45f;
        main.startSpeed = continuous ? 0.35f : 0.8f;
        main.startSize = continuous ? 0.13f : 0.2f;
        main.startColor = new Color(0.85f, 0.72f, 0.5f, 0.5f);
        main.gravityModifier = 0.35f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = continuous ? 32 : 24;

        var emission = particles.emission;
        emission.rateOverTime = continuous ? 14f : 0f;

        var shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 25f;
        shape.radius = continuous ? 0.08f : 0.16f;

        var size = particles.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f,
            new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f)));

        ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial = GetDustMaterial();
        if (renderer.sharedMaterial == null)
        {
            // A missing shader should hide the effect, never draw magenta quads.
            renderer.enabled = false;
            emission.enabled = false;
        }

        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        return particles;
    }

    private Material GetDustMaterial()
    {
        if (generatedMaterial != null) return generatedMaterial;

        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null || !shader.isSupported)
            shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null || !shader.isSupported)
            shader = Shader.Find("Sprites/Default");
        if (shader == null || !shader.isSupported) return null;

        const int resolution = 32;
        softDustTexture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
        softDustTexture.name = "Procedural Soft Dust";
        softDustTexture.wrapMode = TextureWrapMode.Clamp;
        softDustTexture.filterMode = FilterMode.Bilinear;
        Color[] pixels = new Color[resolution * resolution];
        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float dx = ((x + 0.5f) / resolution - 0.5f) * 2f;
                float dy = ((y + 0.5f) / resolution - 0.5f) * 2f;
                float opacity = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                opacity = opacity * opacity * (3f - 2f * opacity);
                pixels[y * resolution + x] = new Color(1f, 1f, 1f, opacity);
            }
        }
        softDustTexture.SetPixels(pixels);
        softDustTexture.Apply(false, true);

        generatedMaterial = new Material(shader) { name = "Procedural Soft Dust" };
        if (generatedMaterial.HasProperty("_BaseMap"))
            generatedMaterial.SetTexture("_BaseMap", softDustTexture);
        if (generatedMaterial.HasProperty("_MainTex"))
            generatedMaterial.SetTexture("_MainTex", softDustTexture);
        if (generatedMaterial.HasProperty("_BaseColor"))
            generatedMaterial.SetColor("_BaseColor", Color.white);
        if (generatedMaterial.HasProperty("_Color"))
            generatedMaterial.SetColor("_Color", Color.white);
        if (generatedMaterial.HasProperty("_Surface"))
            generatedMaterial.SetFloat("_Surface", 1f);
        if (generatedMaterial.HasProperty("_SrcBlend"))
            generatedMaterial.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (generatedMaterial.HasProperty("_DstBlend"))
            generatedMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (generatedMaterial.HasProperty("_ZWrite"))
            generatedMaterial.SetFloat("_ZWrite", 0f);
        generatedMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        generatedMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        return generatedMaterial;
    }

    private void OnDisable()
    {
        SetRunning(false);
        if (jumpDustParticles != null)
            jumpDustParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void OnDestroy()
    {
        if (generatedMaterial != null) Destroy(generatedMaterial);
        if (softDustTexture != null) Destroy(softDustTexture);
    }
}
