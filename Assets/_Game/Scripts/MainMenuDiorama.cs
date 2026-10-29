using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Adds a lush, living jungle diorama behind the authored main-menu UI at runtime.
/// The scene asset is deliberately left untouched: every prop, character and
/// effect below is created while the MainMenu scene loads. Menu buttons, popups
/// and navigation keep using the existing authored objects.
/// </summary>
[DefaultExecutionOrder(-40)]
public sealed class MainMenuDiorama : MonoBehaviour
{
    /// <summary>Length of the PLAY exit beat. MainMenuController uses this to time its fade.</summary>
    public const float ExitSeconds = 0.8f;

    private const string ResourcesRoot = "MainMenu/";
    private const float StageTop = 0.01f;
    private const float JungleGroundY = -0.1f;
    private const float GorillaGroundY = -0.06f;
    private const float GorillaHeight = 5.4f;

    private static readonly int WarmingUpState = Animator.StringToHash("Base Layer.Warming up");
    private static readonly int IdleState = Animator.StringToHash("Base Layer.Idle");
    private static readonly int RunState = Animator.StringToHash("Base Layer.Run");
    private static readonly int GorillaIdleState = Animator.StringToHash("Base Layer.StartIdle");

    public static MainMenuDiorama Instance { get; private set; }
    internal static void SetInstance(MainMenuDiorama value) => Instance = value;

    private Camera menuCamera;
    private Vector3 cameraHome;
    private Quaternion cameraHomeRotation;
    private float cameraBaseFov;
    private float lastAspect;

    private Transform player;
    private Animator playerAnimator;
    private Vector3 playerHome;
    private float playerYaw = 156f;

    private Transform rival;
    private Animator rivalAnimator;
    private Vector3 rivalHome;
    private float rivalYaw = 204f;
    private GameObject rivalCoconut;

    private Transform gorilla;
    private Animator gorillaAnimator;
    private Vector3 gorillaHome;
    private Quaternion gorillaHomeRotation;
    private Vector3 gorillaHomeScale;

    private GameObject trophy;
    private Vector3 trophyHome;
    private Light trophyGlow;

    private ParticleSystem fallingLeaves;
    private readonly List<ParticleSystem> fireflies = new List<ParticleSystem>();

    private Mesh leafMesh;
    private Material leafParticleMaterial;
    private Material leafLitterMaterial;

    private readonly List<UiDrift> uiDrift = new List<UiDrift>();
    private struct UiDrift
    {
        public RectTransform rect;
        public Vector2 home;
        public Quaternion homeRotation;
        public float phase;
        public float amplitude;
        public float tilt;
    }

    private float startedAt;
    private bool exiting;
    private float exitStartedAt;

    private void Awake()
    {
        SetInstance(this);
    }

    private void OnDestroy()
    {
        if (Instance == this) SetInstance(null);
    }

    private void Start()
    {
        startedAt = Time.unscaledTime;
        CacheCamera();
        if (menuCamera == null) return;

        ConfigureEnvironment();
        BuildStage();
        StageCharacters();
        BuildJungle();
        BuildFallingLeaves();
        BuildFireflies();
        CacheMenuAnimations();

        if (playerAnimator != null)
        {
            playerAnimator.applyRootMotion = false;
            playerAnimator.Play(WarmingUpState, 0, 0f);
        }
        if (rivalAnimator != null)
        {
            rivalAnimator.applyRootMotion = false;
            rivalAnimator.Play(IdleState, 0, 0.37f);
        }
        if (gorillaAnimator != null)
        {
            gorillaAnimator.applyRootMotion = false;
            gorillaAnimator.Play(GorillaIdleState, 0, Random.value);
        }

        StartCoroutine(AttachCoconutToRival());
    }

    // ------------------------------------------------------------------
    // Bootstrap
    // ------------------------------------------------------------------

    public static bool BeginExitTransition()
    {
        if (Instance == null) return false;
        Instance.BeginExit();
        return true;
    }

    private void BeginExit()
    {
        if (exiting) return;
        exiting = true;
        exitStartedAt = Time.unscaledTime;

        if (playerAnimator != null)
        {
            playerAnimator.speed = 1.2f;
            playerAnimator.Play(RunState, 0, 0f);
        }
        if (rivalAnimator != null)
        {
            rivalAnimator.speed = 1.25f;
            rivalAnimator.Play(RunState, 0, 0f);
        }
        if (gorillaAnimator != null)
        {
            gorillaAnimator.speed = 1.35f;
            gorillaAnimator.Play(RunState, 0, 0f);
        }
        if (fallingLeaves != null) fallingLeaves.Emit(26);
    }

    // ------------------------------------------------------------------
    // Camera and atmosphere
    // ------------------------------------------------------------------

    private void CacheCamera()
    {
        menuCamera = Camera.main;
        if (menuCamera == null)
            menuCamera = FindMainCamera();

        if (menuCamera == null)
        {
            Debug.LogWarning("MainMenuDiorama: no main camera found; skipping diorama.");
            return;
        }

        cameraHome = menuCamera.transform.position;
        cameraHomeRotation = menuCamera.transform.rotation;
        cameraBaseFov = menuCamera.fieldOfView;
        lastAspect = menuCamera.aspect;
    }

    private static Camera FindMainCamera()
    {
        Camera[] cameras = Camera.allCameras;
        for (int i = 0; i < cameras.Length; i++)
        {
            if (cameras[i] != null && cameras[i].CompareTag("MainCamera"))
                return cameras[i];
        }
        return cameras.Length > 0 ? cameras[0] : null;
    }

    private void ConfigureEnvironment()
    {
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogDensity = 0.017f;
        RenderSettings.fogColor = new Color(0.38f, 0.53f, 0.37f);

        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.44f, 0.6f, 0.52f);
        RenderSettings.ambientEquatorColor = new Color(0.36f, 0.5f, 0.3f);
        RenderSettings.ambientGroundColor = new Color(0.2f, 0.25f, 0.16f);
        RenderSettings.ambientIntensity = 1.12f;

        if (menuCamera.clearFlags == CameraClearFlags.SolidColor)
            menuCamera.backgroundColor = new Color(0.47f, 0.66f, 0.52f);

        Light sun = RenderSettings.sun;
        if (sun == null)
        {
            Light[] lights = FindObjectsByType<Light>(FindObjectsInactive.Exclude);
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i] != null && lights[i].type == LightType.Directional)
                {
                    sun = lights[i];
                    break;
                }
            }
        }
        if (sun != null)
        {
            RenderSettings.sun = sun;
            sun.color = new Color(1f, 0.932f, 0.76f);
            sun.shadowStrength = 0.78f;
        }

        // Warm canopy bounce in front of the characters.
        CreatePointLight("Golden Canopy Fill", new Vector3(-2.6f, 4.4f, -1.6f),
            new Color(1f, 0.8f, 0.44f), 7f, 15f);

        // Cool green rim from behind the jungle to separate the silhouettes.
        GameObject rimObject = new GameObject("Jungle Rim Light");
        rimObject.transform.SetParent(transform, false);
        rimObject.transform.rotation = Quaternion.Euler(16f, 152f, 0f);
        Light rim = rimObject.AddComponent<Light>();
        rim.type = LightType.Directional;
        rim.color = new Color(0.55f, 0.94f, 0.6f);
        rim.intensity = 0.55f;
        rim.shadows = LightShadows.None;
    }

    private Light CreatePointLight(string name, Vector3 position, Color color, float intensity, float range)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.position = position;
        Light light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.intensity = intensity;
        light.range = range;
        light.shadows = LightShadows.None;
        return light;
    }

    // ------------------------------------------------------------------
    // Stage, trophy and props
    // ------------------------------------------------------------------

    private void BuildStage()
    {
        Transform root = new GameObject("Jungle Stage").transform;
        root.SetParent(transform, false);

        Material moss = RuntimeMaterial("Moss Carpet", new Color(0.19f, 0.32f, 0.13f), 0f, 0.08f, Color.black);
        Material mossDark = RuntimeMaterial("Deep Moss", new Color(0.13f, 0.23f, 0.1f), 0f, 0.06f, Color.black);
        Material stone = RuntimeMaterial("Temple Stone", new Color(0.34f, 0.34f, 0.3f), 0.05f, 0.16f, Color.black);
        Material gold = RuntimeMaterial("Trophy Gold", new Color(0.98f, 0.74f, 0.24f), 0.85f, 0.72f,
            new Color(0.55f, 0.34f, 0.05f));
        Material coconut = RuntimeMaterial("Coconut Shell", new Color(0.3f, 0.19f, 0.1f), 0.05f, 0.12f, Color.black);

        // A broad mossy platform replaces the tiny authored showcase disc and
        // gives both monkeys clean footing around the central trophy.
        MakePrimitive(PrimitiveType.Cylinder, "Moss Stage", root,
            new Vector3(0f, StageTop - 0.09f, 1.1f), new Vector3(8.8f, 0.09f, 5.2f), moss);
        MakePrimitive(PrimitiveType.Cylinder, "Stage Border", root,
            new Vector3(0f, StageTop - 0.145f, 1.1f), new Vector3(9.6f, 0.05f, 5.6f), mossDark);

        // Central trophy plinth, standing on the back of the moss stage.
        const float plinthHeight = 0.58f;
        float plinthBottom = StageTop;
        float plinthTop = plinthBottom + plinthHeight;
        MakePrimitive(PrimitiveType.Cylinder, "Trophy Plinth", root,
            new Vector3(0f, plinthBottom + plinthHeight * 0.5f, 3.4f),
            new Vector3(0.95f, plinthHeight * 0.5f, 0.95f), stone);
        MakePrimitive(PrimitiveType.Cylinder, "Plinth Cap", root,
            new Vector3(0f, plinthTop + 0.035f, 3.4f), new Vector3(1.1f, 0.035f, 1.1f), gold);

        GameObject trophyPrefab = LoadPrefab("Trophy");
        if (trophyPrefab != null)
        {
            trophy = Instantiate(trophyPrefab, new Vector3(0f, plinthTop + 0.06f, 3.4f),
                Quaternion.Euler(0f, 25f, 0f), root);
            trophy.name = "Banana Trophy";
            NormalizeHeight(trophy, 1.0f);
            GroundTo(trophy, plinthTop + 0.06f);
            trophyHome = trophy.transform.position;
        }

        // A handful of gold trinkets around the plinth make the pile read at a glance.
        for (int i = 0; i < 6; i++)
        {
            float angle = (i / 6f) * Mathf.PI * 2f + 0.4f;
            float radius = 0.34f + (i % 2) * 0.1f;
            GameObject gem = MakePrimitive(PrimitiveType.Sphere, "Trophy Gem " + i, root,
                new Vector3(Mathf.Cos(angle) * radius, plinthTop + 0.1f, 3.4f + Mathf.Sin(angle) * radius),
                Vector3.one * (0.09f + (i % 3) * 0.028f), gold);
            gem.transform.rotation = Quaternion.Euler(i * 37f, i * 61f, i * 19f);
        }

        trophyGlow = CreatePointLight("Trophy Glow", new Vector3(0f, plinthTop + 0.85f, 3.3f),
            new Color(1f, 0.78f, 0.3f), 4.2f, 6.5f);

        // Storytelling props: coconuts for the rival and the path.
        MakePrimitive(PrimitiveType.Sphere, "Coconut A", root,
            new Vector3(2.55f, 0.16f, 0.35f), new Vector3(0.3f, 0.26f, 0.3f), coconut);
        MakePrimitive(PrimitiveType.Sphere, "Coconut B", root,
            new Vector3(2.95f, 0.13f, 0.9f), new Vector3(0.24f, 0.21f, 0.24f), coconut);
        MakePrimitive(PrimitiveType.Sphere, "Coconut C", root,
            new Vector3(-2.7f, 0.13f, 1.35f), new Vector3(0.24f, 0.21f, 0.24f), coconut);

        // A couple of worn stones at the platform edge.
        MakePrimitive(PrimitiveType.Sphere, "Stage Stone L", root,
            new Vector3(-3.25f, 0.02f, 1.7f), new Vector3(0.7f, 0.4f, 0.55f), stone);
        MakePrimitive(PrimitiveType.Sphere, "Stage Stone R", root,
            new Vector3(3.35f, 0.0f, 1.1f), new Vector3(0.55f, 0.32f, 0.48f), stone);
    }

    private static GameObject MakePrimitive(PrimitiveType type, string name, Transform parent,
        Vector3 position, Vector3 scale, Material material)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        Collider collider = go.GetComponent<Collider>();
        if (collider != null) Destroy(collider);
        go.transform.SetParent(parent, false);
        go.transform.position = position;
        go.transform.localScale = scale;
        Renderer renderer = go.GetComponent<Renderer>();
        if (renderer != null && material != null) renderer.sharedMaterial = material;
        return go;
    }

    private static Material RuntimeMaterial(string name, Color color, float metallic, float smoothness, Color emission)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        Material material = new Material(shader) { name = name };
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
        if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", smoothness);
        if (emission.maxColorComponent > 0.001f)
        {
            material.EnableKeyword("_EMISSION");
            if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", emission);
        }
        return material;
    }

    // ------------------------------------------------------------------
    // Characters
    // ------------------------------------------------------------------

    private void StageCharacters()
    {
        GameObject showcase = GameObject.Find("Monkey - Idle Showcase");
        if (showcase == null)
            Debug.LogWarning("MainMenuDiorama: 'Monkey - Idle Showcase' was not found; player/rival staging skipped.");

        if (showcase != null)
        {
            player = showcase.transform;
            playerAnimator = showcase.GetComponent<Animator>();
            if (playerAnimator == null) playerAnimator = showcase.GetComponentInChildren<Animator>(true);
            player.localScale = Vector3.one * 1.42f;

            GameObject rivalObject = Instantiate(showcase, transform);
            rivalObject.name = "Rival Monkey";
            rivalObject.transform.localScale = Vector3.one * 1.38f;
            rival = rivalObject.transform;
            rivalAnimator = rivalObject.GetComponent<Animator>();
            if (rivalAnimator == null) rivalAnimator = rivalObject.GetComponentInChildren<Animator>(true);

            TintRival(rivalObject);
            rivalCoconut = CreateHeldCoconut();
        }

        LayoutCharacters();

        SpawnGorilla();
    }

    private void LayoutCharacters()
    {
        if (menuCamera == null) return;

        float playerX = Mathf.Min(3.9f, Mathf.Max(0.9f, HalfWidthAt(-0.6f) * 0.7f));
        float rivalX = Mathf.Min(3.9f, Mathf.Max(0.9f, HalfWidthAt(0.15f) * 0.7f));
        float groundY = StageTop + 0.02f;

        if (player != null)
        {
            player.position = new Vector3(-playerX, groundY, -0.6f);
            player.rotation = Quaternion.Euler(0f, playerYaw, 0f);
            GroundTo(player.gameObject, groundY);
            playerHome = player.position;
        }
        if (rival != null)
        {
            rival.position = new Vector3(rivalX, groundY, 0.15f);
            rival.rotation = Quaternion.Euler(0f, rivalYaw, 0f);
            GroundTo(rival.gameObject, groundY);
            rivalHome = rival.position;
        }
    }

    private float HalfWidthAt(float z)
    {
        float distance = Mathf.Max(1f, z - menuCamera.transform.position.z);
        float halfHeight = distance * Mathf.Tan(menuCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        return halfHeight * Mathf.Max(0.35f, menuCamera.aspect);
    }

    private void TintRival(GameObject rivalObject)
    {
        Color tint = new Color(0.9f, 0.58f, 0.45f, 1f);
        Renderer[] renderers = rivalObject.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Material[] materials = renderers[i].materials;
            for (int m = 0; m < materials.Length; m++)
            {
                Material material = materials[m];
                if (material == null) continue;
                if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", tint);
                else if (material.HasProperty("_Color")) material.SetColor("_Color", tint);
            }
        }
    }

    private GameObject CreateHeldCoconut()
    {
        Material coconut = RuntimeMaterial("Held Coconut", new Color(0.3f, 0.19f, 0.1f), 0.05f, 0.12f, Color.black);
        GameObject go = MakePrimitive(PrimitiveType.Sphere, "Rival Coconut", transform,
            new Vector3(3f, 1f, 0f), new Vector3(0.22f, 0.2f, 0.22f), coconut);
        go.SetActive(false);
        return go;
    }

    private IEnumerator AttachCoconutToRival()
    {
        yield return null;
        if (rivalAnimator == null || rivalCoconut == null) yield break;

        Transform hand = null;
        if (rivalAnimator.isHuman)
            hand = rivalAnimator.GetBoneTransform(HumanBodyBones.RightHand);
        if (hand == null) yield break;

        rivalCoconut.transform.SetParent(hand, true);

        // Bones are often wrapped in a 100x armature, so keep the coconut at a
        // readable world size instead of trusting the hand's local scale.
        Vector3 handScale = hand.lossyScale;
        const float worldSize = 0.24f;
        rivalCoconut.transform.localScale = new Vector3(
            worldSize / Mathf.Max(0.0001f, Mathf.Abs(handScale.x)),
            worldSize / Mathf.Max(0.0001f, Mathf.Abs(handScale.y)),
            worldSize / Mathf.Max(0.0001f, Mathf.Abs(handScale.z)));
        rivalCoconut.transform.localPosition = Vector3.zero;
        rivalCoconut.transform.localRotation = Quaternion.Euler(18f, 0f, 0f);
        rivalCoconut.SetActive(true);
    }

    private void SpawnGorilla()
    {
        GameObject prefab = LoadPrefab("Gorilla");
        if (prefab == null)
        {
            Debug.LogWarning("MainMenuDiorama: gorilla prefab missing from Resources/MainMenu.");
            return;
        }

        GameObject instance = Instantiate(prefab, new Vector3(0f, StageTop, 6.8f),
            Quaternion.Euler(0f, 180f, 0f), transform);
        instance.name = "Gorilla Overlook";

        // The prefab ships with its chase brain. In the menu it is pure set dressing.
        GiantGorillaChaser chaser = instance.GetComponentInChildren<GiantGorillaChaser>(true);
        if (chaser != null) chaser.enabled = false;

        Collider[] colliders = instance.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++) colliders[i].enabled = false;
        Rigidbody[] bodies = instance.GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < bodies.Length; i++) bodies[i].isKinematic = true;
        AudioSource[] sources = instance.GetComponentsInChildren<AudioSource>(true);
        for (int i = 0; i < sources.Length; i++)
        {
            sources[i].playOnAwake = false;
            sources[i].Stop();
            sources[i].mute = true;
        }
        Camera[] cameras = instance.GetComponentsInChildren<Camera>(true);
        for (int i = 0; i < cameras.Length; i++) cameras[i].enabled = false;
        Light[] lights = instance.GetComponentsInChildren<Light>(true);
        for (int i = 0; i < lights.Length; i++) lights[i].enabled = false;

        gorilla = instance.transform;
        gorillaAnimator = instance.GetComponentInChildren<Animator>(true);

        // Measure the animated (skinned) body only. The prefab also carries a
        // static oversized mesh from the source FBX; if it were included in the
        // bounds it would shrink the gorilla to a speck.
        NormalizeHeight(instance, GorillaHeight, true);
        CenterXZ(instance, 0f, 6.8f, true);
        GroundTo(instance, GorillaGroundY, true);

        ApplyGorillaMaterial(instance);
        HideOversizedStaticMeshes(instance);

        gorillaHome = gorilla.position;
        gorillaHomeRotation = gorilla.rotation;
        gorillaHomeScale = gorilla.localScale;

        if (TryGetBounds(instance, out Bounds stagedBounds, true))
            Debug.Log($"MainMenuDiorama: gorilla staged {stagedBounds.size.y:F2}m tall at z={gorillaHome.z:F2}.");

        if (gorillaAnimator != null)
        {
            gorillaAnimator.applyRootMotion = false;
            gorillaAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            gorillaAnimator.speed = 0.78f;
        }
    }

    private static void ApplyGorillaMaterial(GameObject instance)
    {
        Material material = Resources.Load<Material>(ResourcesRoot + "M_Gorilla");
        if (material == null) return;

        Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null || !renderers[i].enabled) continue;
            Material[] materials = renderers[i].sharedMaterials;
            if (materials.Length == 0)
                renderers[i].sharedMaterial = material;
            else
            {
                for (int m = 0; m < materials.Length; m++)
                    materials[m] = material;
                renderers[i].sharedMaterials = materials;
            }
        }
    }

    private static void HideOversizedStaticMeshes(GameObject instance)
    {
        SkinnedMeshRenderer body = instance.GetComponentInChildren<SkinnedMeshRenderer>(true);
        if (body == null) return;

        float bodyHeight = body.bounds.size.y;
        if (bodyHeight < 0.001f) return;

        Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
        List<Renderer> hidden = null;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || renderer is SkinnedMeshRenderer || !renderer.enabled)
                continue;

            // A non-skinned mesh that is clearly bigger than the animated body
            // is leftover source-FBX geometry (a background block, not a prop).
            Vector3 size = renderer.bounds.size;
            if (size.y > bodyHeight * 1.6f ||
                size.x > bodyHeight * 2.5f ||
                size.z > bodyHeight * 2.5f)
            {
                renderer.enabled = false;
                hidden ??= new List<Renderer>();
                hidden.Add(renderer);
            }
        }

        // Never hide the only visual in the prefab if the measurement was off.
        if (hidden == null) return;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null && renderers[i].enabled)
                return;
        }
        for (int i = 0; i < hidden.Count; i++)
            if (hidden[i] != null) hidden[i].enabled = true;
    }

    // ------------------------------------------------------------------
    // Jungle
    // ------------------------------------------------------------------

    private void BuildJungle()
    {
        Transform root = new GameObject("Living Jungle").transform;
        root.SetParent(transform, false);

        GameObject fruit = LoadPrefab("PT_Fruit_Tree_01_green");
        GameObject pine = LoadPrefab("PT_Pine_Tree_03_green");
        GameObject shrub = LoadPrefab("PT_Generic_Shrub_01_green");
        GameObject grass = LoadPrefab("PT_Grass_02");
        GameObject flower = LoadPrefab("PT_Poppy_02");

        Random.State previousState = Random.state;
        Random.InitState(48271);

        // Back curtain: dense wall of trees behind the gorilla.
        for (int i = 0; i < 30; i++)
        {
            PlaceTree(root, Random.value < 0.4f ? pine : fruit,
                new Vector3(Random.Range(-6.5f, 6.5f), 0f, Random.Range(10f, 24f)),
                Random.Range(1.0f, 1.6f), Random.Range(0f, 360f));
        }

        // Flanking walls: wider and deeper, framing the clearing in every aspect.
        for (int side = -1; side <= 1; side += 2)
        {
            for (int i = 0; i < 17; i++)
            {
                float x = side * Random.Range(6.5f, 19f);
                float z = Random.Range(7f, 27f);
                PlaceTree(root, Random.value < 0.4f ? pine : fruit, new Vector3(x, 0f, z),
                    Random.Range(1.0f, 1.85f), Random.Range(0f, 360f));
            }
            for (int i = 0; i < 9; i++)
            {
                float x = side * Random.Range(3.1f, 8.5f);
                float z = Random.Range(1.6f, 7.5f);
                PlaceTree(root, Random.value < 0.3f ? pine : fruit, new Vector3(x, 0f, z),
                    Random.Range(0.85f, 1.3f), Random.Range(0f, 360f));
            }
        }

        // Big foreground trees whose canopies overhang the top of the frame.
        PlaceTree(root, fruit, new Vector3(-4.7f, 0f, 1.9f), 1.75f, 24f);
        PlaceTree(root, fruit, new Vector3(4.4f, 0f, 0.9f), 1.62f, 312f);
        PlaceTree(root, pine, new Vector3(-6.2f, 0f, 4.6f), 1.5f, 118f);
        PlaceTree(root, pine, new Vector3(6.4f, 0f, 4.1f), 1.55f, 246f);

        // Shrubs and undergrowth.
        for (int i = 0; i < 42; i++)
        {
            float x = Random.Range(-13f, 13f);
            float z = Random.Range(5.5f, 24f);
            if (Mathf.Abs(x) < 2.6f) x += Mathf.Sign(x <= 0f ? -1f : 1f) * 2.6f;
            PlacePlant(root, shrub, new Vector3(x, 0.02f, z),
                Random.Range(0.8f, 1.65f), Random.Range(0f, 360f), 1.86f);
        }
        for (int side = -1; side <= 1; side += 2)
        {
            for (int i = 0; i < 14; i++)
            {
                float x = side * Random.Range(2.7f, 8f);
                float z = Random.Range(0.4f, 7.5f);
                if (z < 2.6f && Mathf.Abs(x) < 3.9f) x = side * Random.Range(3.9f, 6.5f);
                PlacePlant(root, shrub, new Vector3(x, 0.02f, z),
                    Random.Range(0.75f, 1.4f), Random.Range(0f, 360f), 1.86f);
            }
        }

        // Ferns, grass and flowers for the layered forest floor.
        for (int i = 0; i < 78; i++)
        {
            float x = Random.Range(-9.5f, 9.5f);
            float z = Random.Range(-0.5f, 15f);
            if (z < 2.6f && Mathf.Abs(x) < 3.85f) x = Mathf.Sign(x <= 0f ? -1f : 1f) * Random.Range(3.85f, 6.5f);
            PlacePlant(root, grass, new Vector3(x, 0.02f, z),
                Random.Range(0.7f, 1.5f), Random.Range(0f, 360f), 1.24f);
        }
        for (int side = -1; side <= 1; side += 2)
        {
            for (int i = 0; i < 12; i++)
            {
                float x = side * Random.Range(2.35f, 3.5f);
                float z = Random.Range(-0.8f, 1.4f);
                PlacePlant(root, grass, new Vector3(x, 0.02f, z),
                    Random.Range(1.1f, 2.0f), Random.Range(0f, 360f), 1.24f);
            }
        }
        for (int i = 0; i < 34; i++)
        {
            float x = Random.Range(-8f, 8f);
            float z = Random.Range(0.6f, 13f);
            if (z < 2.6f && Mathf.Abs(x) < 3.9f) x = Mathf.Sign(x <= 0f ? -1f : 1f) * Random.Range(3.9f, 6.5f);
            PlacePlant(root, flower, new Vector3(x, 0.02f, z),
                Random.Range(0.8f, 1.35f), Random.Range(0f, 360f), 0.85f);
        }

        BuildLeafLitter(root);
        Random.state = previousState;
    }

    private void PlaceTree(Transform parent, GameObject prefab, Vector3 position, float scale, float yaw)
    {
        if (prefab != null)
        {
            GameObject instance = Instantiate(prefab, position, Quaternion.Euler(0f, yaw, 0f), parent);
            instance.transform.localScale = Vector3.one * scale;
            GroundTo(instance, JungleGroundY);
            return;
        }

        // Asset-free fallback: low-poly trunk plus stacked canopies.
        Transform root = new GameObject("Runtime Tree").transform;
        root.SetParent(parent, false);
        root.position = position;
        root.rotation = Quaternion.Euler(0f, yaw, 0f);
        Material bark = RuntimeMaterial("Bark", new Color(0.24f, 0.16f, 0.09f), 0f, 0.05f, Color.black);
        Material leaf = RuntimeMaterial("Leaves", new Color(0.16f, 0.4f, 0.14f), 0f, 0.06f, Color.black);
        MakePrimitive(PrimitiveType.Cylinder, "Trunk", root, new Vector3(0f, 2.1f * scale, 0f),
            new Vector3(0.26f, 2.1f, 0.26f) * scale, bark);
        for (int i = 0; i < 3; i++)
        {
            float radius = (1.6f - i * 0.34f) * scale;
            GameObject canopy = MakePrimitive(PrimitiveType.Sphere, "Canopy " + i, root,
                new Vector3(Random.Range(-0.25f, 0.25f) * scale, (3.4f + i * 0.85f) * scale,
                    Random.Range(-0.25f, 0.25f) * scale),
                new Vector3(radius, radius * 0.82f, radius), leaf);
            canopy.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        }
    }

    private void PlacePlant(Transform parent, GameObject prefab, Vector3 position, float scale, float yaw, float nativeHeight)
    {
        if (prefab != null)
        {
            GameObject instance = Instantiate(prefab, position, Quaternion.Euler(0f, yaw, 0f), parent);
            instance.transform.localScale = Vector3.one * scale;
            GroundTo(instance, JungleGroundY);
            return;
        }

        Material leaf = RuntimeMaterial("Understory", new Color(0.17f, 0.42f, 0.16f), 0f, 0.05f, Color.black);
        GameObject bush = MakePrimitive(PrimitiveType.Sphere, "Understory", parent,
            position + Vector3.up * (nativeHeight * scale * 0.4f),
            new Vector3(1.1f, 0.75f, 1.1f) * scale * 0.9f, leaf);
        bush.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
    }

    private void BuildLeafLitter(Transform parent)
    {
        EnsureLeafResources();
        if (leafMesh == null || leafLitterMaterial == null) return;

        GameObject root = new GameObject("Fallen Leaves");
        root.transform.SetParent(parent, false);

        Color[] colors =
        {
            new Color(0.34f, 0.5f, 0.18f), new Color(0.55f, 0.62f, 0.22f),
            new Color(0.78f, 0.52f, 0.14f), new Color(0.46f, 0.36f, 0.12f)
        };
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        for (int i = 0; i < 30; i++)
        {
            float x = Random.Range(-3.3f, 3.3f);
            float z = Random.Range(-0.9f, 3.2f);
            GameObject leaf = new GameObject("Fallen Leaf " + i);
            leaf.transform.SetParent(root.transform, false);
            leaf.transform.position = new Vector3(x, StageTop + 0.006f + i * 0.0002f, z);
            leaf.transform.rotation = Quaternion.Euler(90f, Random.Range(0f, 360f), Random.Range(-18f, 18f));
            leaf.transform.localScale = Vector3.one * Random.Range(0.22f, 0.46f);
            MeshFilter filter = leaf.AddComponent<MeshFilter>();
            filter.sharedMesh = leafMesh;
            MeshRenderer renderer = leaf.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = leafLitterMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            block.SetColor("_BaseColor", colors[i % colors.Length]);
            renderer.SetPropertyBlock(block);
        }
    }

    // ------------------------------------------------------------------
    // Particles
    // ------------------------------------------------------------------

    private void BuildFallingLeaves()
    {
        EnsureLeafResources();
        if (leafMesh == null || leafParticleMaterial == null) return;

        GameObject go = new GameObject("Falling Jungle Leaves");
        go.transform.SetParent(transform, false);
        go.transform.position = new Vector3(0f, 7.2f, 2.6f);

        ParticleSystem system = go.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = system.main;
        main.duration = 8f;
        main.loop = true;
        main.prewarm = true;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(9f, 14f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.04f, 0.3f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.16f, 0.3f);
        main.startRotation3D = true;
        main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.57f, 0.78f, 0.3f), new Color(0.95f, 0.6f, 0.16f));
        main.gravityModifier = 0.03f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = 240;

        ParticleSystem.EmissionModule emission = system.emission;
        emission.enabled = true;
        emission.rateOverTime = 17f;
        emission.rateOverDistance = 0f;

        ParticleSystem.ShapeModule shape = system.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(13f, 0.25f, 11f);
        shape.position = Vector3.zero;
        shape.rotation = Vector3.zero;
        shape.randomDirectionAmount = 0f;

        ParticleSystem.VelocityOverLifetimeModule velocity = system.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(0.16f, 0.4f);
        velocity.y = new ParticleSystem.MinMaxCurve(-0.02f, 0.06f);
        velocity.z = new ParticleSystem.MinMaxCurve(-0.08f, 0.08f);

        ParticleSystem.LimitVelocityOverLifetimeModule limit = system.limitVelocityOverLifetime;
        limit.enabled = true;
        limit.separateAxes = true;
        limit.limitX = new ParticleSystem.MinMaxCurve(1.25f);
        limit.limitY = new ParticleSystem.MinMaxCurve(0.55f);
        limit.limitZ = new ParticleSystem.MinMaxCurve(1.25f);
        limit.dampen = 0.4f;
        limit.space = ParticleSystemSimulationSpace.World;

        ParticleSystem.NoiseModule noise = system.noise;
        noise.enabled = true;
        noise.strength = 0.8f;
        noise.frequency = 0.2f;
        noise.scrollSpeed = 0.18f;
        noise.damping = true;
        noise.octaveCount = 2;
        noise.quality = ParticleSystemNoiseQuality.Medium;

        ParticleSystem.RotationOverLifetimeModule rotation = system.rotationOverLifetime;
        rotation.enabled = true;
        rotation.separateAxes = true;
        rotation.x = new ParticleSystem.MinMaxCurve(-1.3f, 1.3f);
        rotation.y = new ParticleSystem.MinMaxCurve(-0.9f, 0.9f);
        rotation.z = new ParticleSystem.MinMaxCurve(-1.7f, 1.7f);

        ParticleSystem.ColorOverLifetimeModule fade = system.colorOverLifetime;
        fade.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.08f),
                new GradientAlphaKey(1f, 0.82f),
                new GradientAlphaKey(0f, 1f)
            });
        fade.color = new ParticleSystem.MinMaxGradient(gradient);

        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        if (renderer == null) renderer = go.AddComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = leafMesh;
            renderer.alignment = ParticleSystemRenderSpace.World;
            renderer.sharedMaterial = leafParticleMaterial;
            renderer.sortMode = ParticleSystemSortMode.Depth;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        system.Play();
        system.Emit(34);
        fallingLeaves = system;
    }

    private void BuildFireflies()
    {
        CreateFireflySystem("Fireflies Characters", new Vector3(0f, 1.75f, 0.4f),
            new Vector3(10f, 2.6f, 4.2f), 8f, 70);
        CreateFireflySystem("Fireflies Title", new Vector3(0f, 7.0f, 1.4f),
            new Vector3(8f, 2.2f, 3.4f), 6f, 46);
    }

    private void CreateFireflySystem(string name, Vector3 center, Vector3 volume, float rate, int maxParticles)
    {
        Texture2D glow = CreateGlowTexture();
        Material material = CreateParticleMaterial(name + " Material", glow, true);
        if (material == null) return;

        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.position = center;

        ParticleSystem system = go.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = system.main;
        main.duration = 6f;
        main.loop = true;
        main.prewarm = true;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 9.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.12f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.055f, 0.12f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.84f, 0.36f), new Color(1f, 0.97f, 0.6f));
        main.gravityModifier = -0.004f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = maxParticles;

        ParticleSystem.EmissionModule emission = system.emission;
        emission.enabled = true;
        emission.rateOverTime = rate;

        ParticleSystem.ShapeModule shape = system.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = volume;
        shape.randomDirectionAmount = 1f;

        ParticleSystem.NoiseModule noise = system.noise;
        noise.enabled = true;
        noise.strength = 0.32f;
        noise.frequency = 0.24f;
        noise.scrollSpeed = 0.14f;
        noise.damping = true;
        noise.octaveCount = 2;

        ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
        size.enabled = true;
        size.sizeMultiplier = 1f;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.25f),
            new Keyframe(0.22f, 1f),
            new Keyframe(0.44f, 0.35f),
            new Keyframe(0.68f, 0.95f),
            new Keyframe(1f, 0.2f)));

        ParticleSystem.ColorOverLifetimeModule fade = system.colorOverLifetime;
        fade.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.18f),
                new GradientAlphaKey(0.55f, 0.55f),
                new GradientAlphaKey(0f, 1f)
            });
        fade.color = new ParticleSystem.MinMaxGradient(gradient);

        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        if (renderer == null) renderer = go.AddComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.sharedMaterial = material;
            renderer.sortMode = ParticleSystemSortMode.Distance;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        system.Play();
        system.Emit(maxParticles / 3);
        fireflies.Add(system);
    }

    private void EnsureLeafResources()
    {
        if (leafMesh == null) leafMesh = CreateLeafMesh();
        if (leafParticleMaterial == null)
            leafParticleMaterial = CreateParticleMaterial("Falling Leaf Material", CreateLeafTexture(), false);
        if (leafLitterMaterial == null)
            leafLitterMaterial = CreateParticleMaterial("Leaf Litter Material", CreateLeafTexture(), false);
    }

    private static Mesh CreateLeafMesh()
    {
        Mesh mesh = new Mesh { name = "Runtime Leaf Quad" };
        mesh.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, 0f),
            new Vector3(0.5f, -0.5f, 0f),
            new Vector3(-0.5f, 0.5f, 0f),
            new Vector3(0.5f, 0.5f, 0f)
        };
        mesh.uv = new[]
        {
            new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(0f, 1f), new Vector2(1f, 1f)
        };
        mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Texture2D CreateLeafTexture()
    {
        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Runtime Leaf",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            anisoLevel = 1
        };

        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f;
                float v = (y + 0.5f) / size * 2f - 1f;
                float width = 0.62f * Mathf.Pow(Mathf.Max(0f, 1f - v * v), 0.55f);
                float alpha = Mathf.Clamp01((width - Mathf.Abs(u)) * 26f);

                if (v < -0.86f) alpha = 0f;
                if (Mathf.Abs(u) < 0.035f && v < -0.55f)
                    alpha = Mathf.Clamp01((0.035f - Mathf.Abs(u)) * 30f);

                float rib = Mathf.Abs(u) < 0.022f ? 0.74f : 1f;
                float vein = Mathf.Abs(Mathf.Repeat(v * 4.2f + Mathf.Abs(u) * 2.4f, 1f) - 0.5f);
                if (vein < 0.045f && alpha > 0.2f) rib *= 0.9f;

                pixels[y * size + x] = new Color(rib, rib, rib, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return texture;
    }

    private static Texture2D CreateGlowTexture()
    {
        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Runtime Firefly Glow",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            anisoLevel = 1
        };

        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f;
                float v = (y + 0.5f) / size * 2f - 1f;
                float radius = Mathf.Sqrt(u * u + v * v);
                float glow = Mathf.Clamp01(1f - radius);
                float alpha = glow * glow * glow;
                float core = Mathf.Clamp01(1f - radius * 5f);
                alpha = Mathf.Clamp01(alpha * 0.8f + core * 0.6f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return texture;
    }

    private static Material CreateParticleMaterial(string name, Texture2D texture, bool additive)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null || !shader.isSupported) shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null || !shader.isSupported) shader = Shader.Find("Sprites/Default");
        if (shader == null || !shader.isSupported)
        {
            Debug.LogWarning("MainMenuDiorama: no supported particle shader found.");
            return null;
        }

        Material material = new Material(shader) { name = name };
        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
        if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
        if (material.HasProperty("_Color")) material.SetColor("_Color", Color.white);
        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_Blend")) material.SetFloat("_Blend", additive ? 2f : 0f);
        if (material.HasProperty("_SrcBlend"))
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend"))
            material.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
        if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
        if (material.HasProperty("_Cull")) material.SetFloat("_Cull", 0f);
        if (material.HasProperty("_AlphaClip")) material.SetFloat("_AlphaClip", 0f);

        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        if (additive) material.EnableKeyword("_ALPHAPREMULTIPLY_ON");
        else material.EnableKeyword("_ALPHABLEND_ON");
        material.DisableKeyword("_ALPHATEST_ON");
        material.SetOverrideTag("RenderType", "Transparent");
        material.renderQueue = (int)RenderQueue.Transparent;
        return material;
    }

    // ------------------------------------------------------------------
    // UI drift
    // ------------------------------------------------------------------

    private void CacheMenuAnimations()
    {
        Canvas canvas = null;
        GameObject canvasObject = GameObject.Find("Menu Canvas");
        if (canvasObject != null) canvas = canvasObject.GetComponent<Canvas>();
        if (canvas == null)
        {
            Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Exclude);
            if (canvases.Length > 0) canvas = canvases[0];
        }
        if (canvas == null) return;

        RectTransform[] rects = canvas.GetComponentsInChildren<RectTransform>(true);
        for (int i = 0; i < rects.Length; i++)
        {
            RectTransform rect = rects[i];
            float amplitude;
            float tilt;
            switch (rect.name)
            {
                case "Banana Title": amplitude = 5.5f; tilt = 0.8f; break;
                case "Run Title": amplitude = 4.5f; tilt = 0.6f; break;
                case "Adventure Label": amplitude = 3.2f; tilt = 0f; break;
                case "Tagline": amplitude = 2.6f; tilt = 0f; break;
                case "Play Hint": amplitude = 2.2f; tilt = 0f; break;
                case "Banana Emblem Left": amplitude = 3.4f; tilt = 2.2f; break;
                case "Banana Emblem Right": amplitude = 3.4f; tilt = 2.2f; break;
                case "Play Button": amplitude = 3.2f; tilt = 0f; break;
                case "Character Button": amplitude = 2.2f; tilt = 0f; break;
                case "Settings Button": amplitude = 2.2f; tilt = 0f; break;
                default: continue;
            }

            uiDrift.Add(new UiDrift
            {
                rect = rect,
                home = rect.anchoredPosition,
                homeRotation = rect.localRotation,
                phase = (i % 11) * 0.63f,
                amplitude = amplitude,
                tilt = tilt
            });
        }
    }

    private void AnimateUi(float time)
    {
        for (int i = 0; i < uiDrift.Count; i++)
        {
            UiDrift drift = uiDrift[i];
            if (drift.rect == null) continue;

            float bob = Mathf.Sin(time * 1.1f + drift.phase);
            drift.rect.anchoredPosition = drift.home + new Vector2(
                Mathf.Sin(time * 0.63f + drift.phase * 1.4f) * drift.amplitude * 0.35f,
                bob * drift.amplitude);

            if (drift.tilt > 0f)
                drift.rect.localRotation = drift.homeRotation *
                    Quaternion.Euler(0f, 0f, Mathf.Sin(time * 0.82f + drift.phase) * drift.tilt);
        }
    }

    // ------------------------------------------------------------------
    // Frame update
    // ------------------------------------------------------------------

    private void Update()
    {
        if (menuCamera == null) return;
        float time = Time.unscaledTime;

        if (exiting)
        {
            UpdateExit(time - exitStartedAt);
            return;
        }

        if (Mathf.Abs(menuCamera.aspect - lastAspect) > 0.01f)
        {
            lastAspect = menuCamera.aspect;
            LayoutCharacters();
        }

        AnimateIdleCamera(time);
        AnimateIdleCharacters(time);
        AnimateUi(time);
        AnimateTrophy(time);
    }

    private void AnimateIdleCamera(float time)
    {
        Vector3 position = cameraHome + new Vector3(
            Mathf.Sin(time * 0.21f) * 0.16f,
            Mathf.Sin(time * 0.33f + 1.1f) * 0.055f,
            Mathf.Sin(time * 0.17f + 0.4f) * 0.1f);
        Quaternion rotation = cameraHomeRotation * Quaternion.Euler(
            Mathf.Sin(time * 0.27f + 0.6f) * 0.45f,
            Mathf.Sin(time * 0.19f + 2.1f) * 0.55f,
            Mathf.Sin(time * 0.15f) * 0.25f);

        menuCamera.transform.SetPositionAndRotation(position, rotation);
        menuCamera.fieldOfView = cameraBaseFov;
    }

    private void AnimateIdleCharacters(float time)
    {
        if (player != null)
        {
            player.position = playerHome + Vector3.up * (Mathf.Sin(time * 1.7f) * 0.012f);
            player.rotation = Quaternion.Euler(0f, playerYaw + Mathf.Sin(time * 0.55f) * 2.4f, 0f);
        }
        if (rival != null)
        {
            rival.position = rivalHome + Vector3.up * (Mathf.Abs(Mathf.Sin(time * 2.1f)) * 0.03f);
            rival.rotation = Quaternion.Euler(0f, rivalYaw + Mathf.Sin(time * 0.72f + 1.2f) * 3.2f, 0f);
        }
        if (gorilla != null)
        {
            float breath = 1f + Mathf.Sin(time * 1.15f) * 0.012f;
            gorilla.position = gorillaHome + Vector3.up * (Mathf.Sin(time * 1.15f) * 0.035f);
            gorilla.rotation = gorillaHomeRotation * Quaternion.Euler(0f, Mathf.Sin(time * 0.31f) * 1.3f, 0f);
            gorilla.localScale = new Vector3(
                gorillaHomeScale.x / Mathf.Sqrt(breath),
                gorillaHomeScale.y * breath,
                gorillaHomeScale.z / Mathf.Sqrt(breath));
        }

        // The authored controller Start() plays Idle on the showcase monkey; keep
        // the warm-up state authoritative for the first moments of the menu.
        float age = time - startedAt;
        if (age < 2f && playerAnimator != null)
        {
            AnimatorStateInfo state = playerAnimator.GetCurrentAnimatorStateInfo(0);
            if (!state.IsName("Warming up") && !playerAnimator.IsInTransition(0))
                playerAnimator.Play(WarmingUpState, 0, 0f);
        }
    }

    private void AnimateTrophy(float time)
    {
        if (trophyGlow != null)
            trophyGlow.intensity = 3.6f + Mathf.Sin(time * 1.9f) * 1.1f;
        if (trophy != null)
        {
            trophy.transform.position = trophyHome + Vector3.up * (Mathf.Sin(time * 1.25f) * 0.035f);
            trophy.transform.rotation = Quaternion.Euler(0f, 25f + time * 12f, 0f);
        }
    }

    private void UpdateExit(float elapsed)
    {
        float t = Mathf.Clamp01(elapsed / ExitSeconds);
        float turn = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.2f, t));
        float sprint = Mathf.InverseLerp(0.16f, 0.84f, t);
        float sprintEase = sprint * sprint * (3f - 2f * sprint);
        float lunge = Mathf.InverseLerp(0.14f, 0.82f, t);
        float lungeEase = lunge * lunge;

        if (player != null)
        {
            Vector3 home = playerHome;
            player.position = new Vector3(
                Mathf.Lerp(home.x, -2.2f, sprintEase),
                home.y + Mathf.Abs(Mathf.Sin(sprint * 26f)) * 0.055f,
                Mathf.Lerp(home.z, home.z + 6.2f, sprintEase));
            player.rotation = Quaternion.Euler(0f, Mathf.LerpAngle(playerYaw, 0f, turn), 0f);
        }
        if (rival != null)
        {
            Vector3 home = rivalHome;
            float offset = Mathf.Clamp01(sprint - 0.05f);
            rival.position = new Vector3(
                Mathf.Lerp(home.x, 2.2f, offset * offset * (3f - 2f * offset)),
                home.y + Mathf.Abs(Mathf.Sin(sprint * 24f + 1.4f)) * 0.06f,
                Mathf.Lerp(home.z, home.z + 6.6f, sprintEase));
            rival.rotation = Quaternion.Euler(0f, Mathf.LerpAngle(rivalYaw, 0f, turn), 0f);
        }
        if (gorilla != null)
        {
            float windup = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.14f, t));
            gorilla.position = gorillaHome
                + Vector3.forward * (-4.5f * lungeEase)
                + Vector3.up * (windup * 0.18f * (1f - lungeEase));
            gorilla.rotation = gorillaHomeRotation *
                Quaternion.Euler(-9f * windup + 15f * lungeEase, Mathf.Sin(t * 41f) * 2.2f * lungeEase, 0f);

            float breath = 1f + Mathf.Sin(elapsed * 14f) * 0.02f;
            gorilla.localScale = new Vector3(
                gorillaHomeScale.x * breath,
                gorillaHomeScale.y / breath,
                gorillaHomeScale.z * breath);
        }

        // Camera: brace, then push in with a short impact shake.
        float push = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.18f, 0.82f, t));
        float shake = Mathf.Clamp01((t - 0.34f) / 0.46f);
        Vector3 jitter = new Vector3(
            Mathf.Sin(elapsed * 57f) * 0.055f,
            Mathf.Sin(elapsed * 63f + 1.3f) * 0.045f,
            Mathf.Sin(elapsed * 49f) * 0.03f) * shake;

        menuCamera.transform.SetPositionAndRotation(
            cameraHome + jitter + Vector3.forward * (0.55f * push),
            cameraHomeRotation * Quaternion.Euler(
                Mathf.Sin(elapsed * 21f) * 0.7f * shake,
                Mathf.Sin(elapsed * 17f + 0.7f) * 0.9f * shake,
                Mathf.Sin(elapsed * 25f) * 0.5f * shake));
        menuCamera.fieldOfView = Mathf.Lerp(cameraBaseFov, cameraBaseFov - 4.5f, push);
    }

    // ------------------------------------------------------------------
    // Utilities
    // ------------------------------------------------------------------

    private static GameObject LoadPrefab(string name)
    {
        GameObject prefab = Resources.Load<GameObject>(ResourcesRoot + name);
        if (prefab == null)
            Debug.LogWarning("MainMenuDiorama: missing Resources/" + ResourcesRoot + name + ".prefab");
        return prefab;
    }

    private static bool TryGetBounds(GameObject instance, out Bounds bounds, bool skinnedOnly = false)
    {
        bounds = default;

        if (skinnedOnly)
        {
            bool foundSkinned = false;
            SkinnedMeshRenderer[] skins = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int i = 0; i < skins.Length; i++)
            {
                SkinnedMeshRenderer skin = skins[i];
                if (skin == null) continue;
                if (!foundSkinned)
                {
                    bounds = skin.bounds;
                    foundSkinned = true;
                }
                else bounds.Encapsulate(skin.bounds);
            }

            if (foundSkinned && bounds.size.sqrMagnitude > 0.000001f)
                return true;
        }

        bounds = default;
        bool found = false;
        Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null) continue;
            if (!found)
            {
                bounds = renderer.bounds;
                found = true;
            }
            else bounds.Encapsulate(renderer.bounds);
        }
        return found && bounds.size.sqrMagnitude > 0.000001f;
    }

    private static void NormalizeHeight(GameObject instance, float targetHeight, bool skinnedOnly = false)
    {
        if (targetHeight <= 0f) return;
        if (!TryGetBounds(instance, out Bounds bounds, skinnedOnly)) return;
        if (bounds.size.y < 0.0001f) return;
        float factor = targetHeight / bounds.size.y;
        if (factor > 0f && factor < 100000f && !float.IsInfinity(factor) && !float.IsNaN(factor))
            instance.transform.localScale *= factor;
    }

    private static void GroundTo(GameObject instance, float groundY, bool skinnedOnly = false)
    {
        if (!TryGetBounds(instance, out Bounds bounds, skinnedOnly)) return;
        Vector3 position = instance.transform.position;
        position.y += groundY - bounds.min.y;
        instance.transform.position = position;
    }

    private static void CenterXZ(GameObject instance, float x, float z, bool skinnedOnly = false)
    {
        if (!TryGetBounds(instance, out Bounds bounds, skinnedOnly)) return;
        Vector3 position = instance.transform.position;
        position.x += x - bounds.center.x;
        position.z += z - bounds.center.z;
        instance.transform.position = position;
    }
}

/// <summary>Installs the diorama whenever the MainMenu scene finishes loading.</summary>
public static class MainMenuDioramaBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Attach(SceneManager.GetActiveScene());
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Attach(scene);
    }

    private static void Attach(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded || scene.name != "MainMenu")
            return;
        if (MainMenuDiorama.Instance != null)
            return;

        MainMenuDiorama existing = Object.FindAnyObjectByType<MainMenuDiorama>();
        if (existing != null)
        {
            MainMenuDiorama.SetInstance(existing);
            return;
        }

        GameObject root = new GameObject("Main Menu Diorama");
        SceneManager.MoveGameObjectToScene(root, scene);
        root.AddComponent<MainMenuDiorama>();
    }
}
