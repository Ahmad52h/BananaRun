using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Runtime-only Level01 extension: lengthens the course to 1500 m, inserts
/// Polytope Studio bridge crossings over flowing water and densifies the pickup
/// layout, all without editing the authored scene YAML.
/// </summary>
[DisallowMultipleComponent]
public sealed class TrackExpansion : MonoBehaviour
{
    private const string GameplayScene = "Level01";
    private const string RootName = "Track Extension (Runtime)";
    private const float TargetFinishZ = 1500f;
    private const float GroundTopY = -0.3f;
    private const float BridgeWaterY = -1.35f;
    private const float BridgeFallLine = -1.9f;
    private const float RespawnDelay = 1.5f;
    private const float SinkDuration = 0.5f;
    private const float RespawnInvulnerability = 2.25f;

    private static readonly List<WaterSpan> waterSpans = new List<WaterSpan>();
    private static TrackExpansion active;

    private readonly List<WaterSpan> spans = new List<WaterSpan>();
    private Transform root;
    private RunnerMotor player;
    private RivalRunner rival;
    private Material waterMaterial;
    private Material splashMaterial;
    private GameObject bridgePrefab;
    private GameObject bananaPrefab;
    private bool playerFalling, rivalFalling;
    private string builtScenePath;

    private struct WaterSpan
    {
        public float Start;
        public float End;
        public WaterSpan(float start, float end) { Start = start; End = end; }
        public float Center => (Start + End) * 0.5f;
        public float Length => End - Start;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindAnyObjectByType<TrackExpansion>() != null) return;
        var host = new GameObject("Track Expansion");
        DontDestroyOnLoad(host);
        var expansion = host.AddComponent<TrackExpansion>();
        expansion.SetupScene(SceneManager.GetActiveScene());
        SceneManager.sceneLoaded += expansion.OnSceneLoaded;
    }

    private void Awake()
    {
        active = this;
    }

    private void OnDestroy()
    {
        if (active == this) active = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SetupScene(scene);
    }

    private void SetupScene(Scene scene)
    {
        if (!scene.IsValid() || scene.name != GameplayScene)
        {
            builtScenePath = null;
            root = null;
            spans.Clear();
            waterSpans.Clear();
            playerFalling = rivalFalling = false;
            return;
        }
        if (builtScenePath == scene.path && root != null) return;

        builtScenePath = scene.path;
        playerFalling = false;
        rivalFalling = false;
        spans.Clear();
        waterSpans.Clear();

        player = FindAnyObjectByType<RunnerMotor>();
        rival = FindAnyObjectByType<RivalRunner>();
        var gate = FindAnyObjectByType<FinishGate>();
        if (player == null || gate == null) return;

        // The finish trigger owns the authoritative race distance. Move its
        // root (basket included) and the decorative arch together.
        float shift = TargetFinishZ - gate.transform.position.z;
        if (Mathf.Abs(shift) > 0.01f)
        {
            Transform finishRoot = gate.transform.parent;
            if (finishRoot != null && finishRoot.name == "Finish")
                finishRoot.position += Vector3.forward * shift;
            else
                gate.transform.position += Vector3.forward * shift;

        }
        var arch = GameObject.Find("Tropical Finish Arch");
        if (arch != null)
            arch.transform.position = new Vector3(player.CenterX, GroundTopY, TargetFinishZ);

        // RaceProgressTracker reads the gate collider bounds in Start; sync the
        // moved trigger immediately so the 1500 m finish is authoritative.
        Physics.SyncTransforms();

        gate.ConfigureBananaQuota(true, 40);

        var rootObject = new GameObject(RootName);
        root = rootObject.transform;

        BuildGround();
        BuildFinishBasket(gate, scene);
        BuildWaterSpans();
        BuildBridges();
        BuildApproachScenery();
        BuildBackdrop();
    }

    private void BuildFinishBasket(FinishGate gate, Scene scene)
    {
        var prefab = Resources.Load<GameObject>("Shared/WovenBananaBasket");
        if (prefab == null)
        {
            Debug.LogError("TrackExpansion: missing shared woven banana basket prefab.", this);
            return;
        }

        // The old banana pile is a separate scene root, so moving Finish never
        // moved it. Retire both old visuals rather than leaving a second prize.
        foreach (var candidate in FindObjectsByType<Transform>())
        {
            if (candidate.gameObject.scene != scene) continue;
            if (candidate.name == "FinishBasket" || candidate.name == "Celebration Banana Basket")
                candidate.gameObject.SetActive(false);

            // Remove the authored glowing rune in the middle of the road,
            // including its obstacle collider. Side-lane obstacles stay in place.
            if (candidate.name.Contains("Rune") && Mathf.Abs(candidate.position.x - player.CenterX) < 0.75f)
            {
                var obstacle = candidate.GetComponentInParent<RunnerObstacle>();
                (obstacle != null ? obstacle.gameObject : candidate.gameObject).SetActive(false);
            }
        }

        var basket = Instantiate(prefab, new Vector3(player.CenterX, GroundTopY, TargetFinishZ + 4f),
            Quaternion.Euler(0f, 20f, 0f), root);
        basket.name = "Final Woven Banana Basket";
        basket.transform.localScale = Vector3.one * 2.4f;
        Transform target = basket.transform.Find("BananaTarget");
        gate.ConfigureBananaTarget(target);
        var cutscene = FindAnyObjectByType<RaceCutsceneDirector>();
        if (cutscene != null) cutscene.ConfigureCelebrationBasket(target);
    }

    private void BuildGround()
    {
        var sourceGround = GameObject.Find("Ground");
        Material groundMaterial = null;
        if (sourceGround != null)
        {
            var renderer = sourceGround.GetComponent<Renderer>();
            if (renderer != null) groundMaterial = renderer.sharedMaterial;
        }

        // Segments cover the extension from the end of the authored ground to
        // 40 m beyond the new finish. Water spans are deliberately left open.
        // The authored Ground ends at z=940; overlapping it any further would
        // z-fight two coplanar top faces, so start a hair inside its edge.
        CreateGroundSegment(groundMaterial, 939.9f, 1000f);
        CreateGroundSegment(groundMaterial, 1034f, 1136f);
        CreateGroundSegment(groundMaterial, 1170f, 1272f);
        CreateGroundSegment(groundMaterial, 1306f, 1540f);
    }

    private void CreateGroundSegment(Material material, float startZ, float endZ)
    {
        var segment = GameObject.CreatePrimitive(PrimitiveType.Cube);
        segment.name = "Track Extension Ground";
        segment.transform.SetParent(root, false);
        segment.transform.position = new Vector3(0f, GroundTopY - 0.5f, (startZ + endZ) * 0.5f);
        segment.transform.localScale = new Vector3(10f, 1f, Mathf.Max(1f, endZ - startZ));
        var renderer = segment.GetComponent<Renderer>();
        if (renderer != null)
        {
            if (material != null) renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = true;
        }
    }

    private void BuildWaterSpans()
    {
        waterMaterial = LoadWaterMaterial();
        Span(1000f, 1034f);
        Span(1136f, 1170f);
        Span(1272f, 1306f);

        void Span(float start, float end)
        {
            var span = new WaterSpan(start, end);
            spans.Add(span);
            waterSpans.Add(span);
            CreateWaterSurface(span);
        }
    }

    private void CreateWaterSurface(WaterSpan span)
    {
        var water = GameObject.CreatePrimitive(PrimitiveType.Cube);
        water.name = "Flowing Water Hazard";
        water.transform.SetParent(root, false);
        water.transform.position = new Vector3(player.CenterX, BridgeWaterY, span.Center);
        water.transform.localScale = new Vector3(70f, 0.08f, span.Length);
        var collider = water.GetComponent<Collider>();
        if (collider != null) { collider.enabled = false; Destroy(collider); }
        var renderer = water.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = waterMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        // A darker underlay keeps the gap from showing the sky when the camera
        // drops low behind the runner.
        var bed = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bed.name = "River Bed";
        bed.transform.SetParent(root, false);
        bed.transform.position = new Vector3(player.CenterX, BridgeWaterY - 0.35f, span.Center);
        bed.transform.localScale = new Vector3(71f, 0.3f, span.Length);
        var bedCollider = bed.GetComponent<Collider>();
        if (bedCollider != null) { bedCollider.enabled = false; Destroy(bedCollider); }
        var bedRenderer = bed.GetComponent<Renderer>();
        if (bedRenderer != null)
            bedRenderer.sharedMaterial = MakeMaterial(new Color(0.08f, 0.22f, 0.25f), "River Bed");
    }

    private void BuildBridges()
    {
        bridgePrefab = Resources.Load<GameObject>("Track/PT_Wooden_Bridge_02");
        var course = FindAnyObjectByType<RunnerCourseGenerator>();
        if (course != null) bananaPrefab = course.BananaPrefab;

        for (int i = 0; i < spans.Count; i++)
        {
            WaterSpan span = spans[i];
            if (course != null) course.RegisterWaterZone(span.Start - 4f, span.End + 4f);

            SpawnBridge(span, player.CenterX);
            SpawnBridgeBananas(span, player.CenterX);
        }
    }

    private void SpawnBridge(WaterSpan span, float centerX)
    {
        float length = span.Length + 8f;
        var controller = player.GetComponent<CharacterController>();
        float clearance = controller != null ? controller.radius + controller.skinWidth + 0.25f : 0.7f;
        float deckWidth = Mathf.Max(6.4f, 2f * (player.LaneDistance + clearance));
        float width = deckWidth + 0.4f;
        GameObject bridge = null;
        Vector3[] deckProfile = null;

        if (bridgePrefab != null)
        {
            bridge = Instantiate(bridgePrefab, root);
            bridge.name = "PT Wooden Bridge";
            // The Polytope mesh's longitudinal axis is LOCAL X. Yaw maps it to
            // world Z; local X stretches the span and local Z sets its width.
            bridge.transform.localScale = Vector3.one;
            bridge.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            bridge.transform.position = new Vector3(centerX, 0f, span.Center);
            Physics.SyncTransforms();
            deckProfile = AlignBridgeDeck(bridge, span, centerX, length, width);

            // Disable ALL imported colliders, including any boxes on posts or
            // rails. Only the continuous walking ribbon below carries runners.
            foreach (var collider in bridge.GetComponentsInChildren<Collider>(true))
                collider.enabled = false;
            if (deckProfile == null)
            {
                bridge.SetActive(false);
                Destroy(bridge);
                bridge = null;
            }
        }

        if (bridge == null)
        {
            bridge = BuildFallbackBridge(span, centerX, length, width);
            deckProfile = new[] {
                new Vector3(centerX, GroundTopY, span.Center - length * 0.5f),
                new Vector3(centerX, GroundTopY, span.Center + length * 0.5f)
            };
        }

        var surface = new GameObject("Bridge Deck Surface");
        surface.transform.SetParent(root, false);
        surface.AddComponent<BridgeDeck>().Configure(deckProfile, deckWidth);
        Physics.SyncTransforms();
    }

    private Vector3[] AlignBridgeDeck(GameObject bridge, WaterSpan span, float centerX, float length, float width)
    {
        Bounds bounds = RendererBounds(bridge);
        if (bounds.size.x < 0.01f || bounds.size.z < 0.01f) return null;
        var colliders = bridge.GetComponentsInChildren<MeshCollider>(true);
        var samples = new List<Vector3>();
        // Sample ONLY the bridge itself, at its centreline and native scale.
        // This cannot hit banks, water, runners or the higher side railings.
        for (int i = 0; i <= 256; i++)
        {
            float z = Mathf.Lerp(bounds.min.z, bounds.max.z, i / 256f);
            var ray = new Ray(new Vector3(bounds.center.x, bounds.max.y + 1f, z), Vector3.down);
            float top = float.MinValue;
            foreach (var collider in colliders)
                if (collider.Raycast(ray, out RaycastHit hit, bounds.size.y + 2f) && hit.normal.y > 0.5f)
                    top = Mathf.Max(top, hit.point.y);
            if (top != float.MinValue)
                samples.Add(new Vector3(bounds.center.x, top, z));
        }
        if (samples.Count < 2) return null;
        Vector3 first = samples[0], last = samples[samples.Count - 1];
        if (last.z - first.z < 0.01f) return null;

        // Measure PLANK width, excluding the higher rails and posts. Scene lane
        // spacing can differ from the prefab defaults; every lane plus the
        // runner's capsule must fit on visible wood, not just an invisible floor.
        Vector3 middle = samples[samples.Count / 2];
        float left = float.MaxValue, right = float.MinValue;
        for (int i = 0; i <= 256; i++)
        {
            float x = Mathf.Lerp(bounds.min.x, bounds.max.x, i / 256f);
            var ray = new Ray(new Vector3(x, bounds.max.y + 1f, middle.z), Vector3.down);
            foreach (var collider in colliders)
                if (collider.Raycast(ray, out RaycastHit hit, bounds.size.y + 2f) &&
                    hit.normal.y > 0.5f && Mathf.Abs(hit.point.y - middle.y) < 0.08f)
                {
                    left = Mathf.Min(left, x);
                    right = Mathf.Max(right, x);
                }
        }
        if (right - left < 0.01f) return null;
        for (int i = 0; i < samples.Count; i++)
            samples[i] = bridge.transform.InverseTransformPoint(samples[i]);

        // Fit the actual plank endpoints rather than the off-centre decorative
        // AABB. Preserve a gentle arch (~30 cm), then level BOTH ends to dirt.
        bridge.transform.localScale = new Vector3(length / (last.z - first.z), 0.6f, width / (right - left));
        first = bridge.transform.TransformPoint(samples[0]);
        last = bridge.transform.TransformPoint(samples[samples.Count - 1]);
        float pitch = Mathf.Atan2(last.y - first.y, last.z - first.z) * Mathf.Rad2Deg;
        bridge.transform.rotation = Quaternion.AngleAxis(pitch, Vector3.right) * bridge.transform.rotation;
        first = bridge.transform.TransformPoint(samples[0]);
        last = bridge.transform.TransformPoint(samples[samples.Count - 1]);
        bridge.transform.position += new Vector3(centerX, GroundTopY, span.Center) - (first + last) * 0.5f;

        var profile = new Vector3[samples.Count + 2];
        for (int i = 0; i < samples.Count; i++)
        {
            Vector3 point = bridge.transform.TransformPoint(samples[i]);
            profile[i + 1] = new Vector3(centerX, point.y, point.z);
        }
        // Small flat bank overlaps prevent a seam at either end of the mesh.
        profile[0] = profile[1] + Vector3.back * 0.5f;
        profile[profile.Length - 1] = profile[profile.Length - 2] + Vector3.forward * 0.5f;
        return profile;
    }

    private GameObject BuildFallbackBridge(WaterSpan span, float centerX, float length, float width)
    {
        var bridge = new GameObject("Wooden Bridge (Fallback)");
        bridge.transform.SetParent(root, false);
        bridge.transform.position = new Vector3(centerX, GroundTopY, span.Center);

        var wood = MakeMaterial(new Color(0.44f, 0.28f, 0.14f), "Bridge Wood");
        CreatePart(bridge.transform, PrimitiveType.Cube, new Vector3(0f, -0.06f, 0f),
            new Vector3(width, 0.12f, length), wood);
        for (int side = -1; side <= 1; side += 2)
        {
            CreatePart(bridge.transform, PrimitiveType.Cube, new Vector3(side * width * 0.5f, 0.5f, 0f),
                new Vector3(0.14f, 1f, length), wood);
            for (float z = -length * 0.5f + 1f; z <= length * 0.5f - 1f; z += 4f)
                CreatePart(bridge.transform, PrimitiveType.Cube, new Vector3(side * width * 0.5f, 0.02f, z),
                    new Vector3(0.2f, 0.95f, 0.2f), wood);
        }
        return bridge;
    }

    private void SpawnBridgeBananas(WaterSpan span, float centerX)
    {
        if (bananaPrefab == null || player == null) return;
        var container = new GameObject("Bridge Bananas");
        // Parent to the extension root, never the bridge: the bridge is scaled
        // along its length and children would inherit that huge stretch.
        container.transform.SetParent(root, false);

        float lane = player.LaneDistance;
        // Extend the line well onto the approach: the course generator reserves
        // roughly 20 m of clear runway around each bridge, and this trail keeps
        // the section from ever feeling empty.
        float start = span.Start - 20f;
        float end = span.End + 20f;
        int index = 0;
        for (float z = start; z <= end; z += 2.6f)
        {
            // Central trail plus staggered side lanes creates a thick reward
            // line without hiding the bridge silhouette.
            SpawnDeckBanana(centerX, z);
            if ((index & 1) == 0)
                SpawnDeckBanana(centerX - lane, z + 0.9f);
            if ((index & 1) == 1)
                SpawnDeckBanana(centerX + lane, z + 0.9f);
            index++;
        }

        void SpawnDeckBanana(float x, float z)
        {
            var position = new Vector3(x, GroundTopY, z);
            float deckY = BridgeDeck.TryGetHeight(position, out float height) ? height : GroundTopY;
            position.y = deckY + 0.8f;
            SpawnBanana(container.transform, position);
        }
    }

    private void SpawnBanana(Transform parent, Vector3 visibleCenter)
    {
        var pickup = Instantiate(bananaPrefab, visibleCenter, bananaPrefab.transform.rotation, parent);
        var sphere = pickup.GetComponent<SphereCollider>();
        if (sphere != null) pickup.transform.position += visibleCenter - pickup.transform.TransformPoint(sphere.center);
    }

    private void BuildApproachScenery()
    {
        if (root == null) return;
        var rockMaterial = MakeMaterial(new Color(0.30f, 0.34f, 0.27f), "Bridge Bank Stone");
        foreach (var span in spans)
        {
            for (int side = -1; side <= 1; side += 2)
                for (int end = 0; end < 2; end++)
                {
                    float edgeZ = end == 0 ? span.Start - 1.2f : span.End + 1.2f;
                    for (int i = 0; i < 3; i++)
                    {
                        float z = edgeZ + Random.Range(-0.8f, 0.8f);
                        // Keep the stones on the 10 m-wide bank, not floating over
                        // the wider water plane.
                        float x = side * Random.Range(4.1f, 4.9f);
                        var rock = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        rock.name = "River Bank Rock";
                        rock.transform.SetParent(root, false);
                        rock.transform.position = new Vector3(x, GroundTopY + Random.Range(0.05f, 0.4f), z);
                        rock.transform.rotation = Quaternion.Euler(Random.Range(-8f, 8f), Random.Range(0f, 360f), Random.Range(-8f, 8f));
                        rock.transform.localScale = new Vector3(Random.Range(0.7f, 1.9f), Random.Range(0.5f, 1.2f), Random.Range(0.8f, 2.2f));
                        var collider = rock.GetComponent<Collider>();
                        if (collider != null) Destroy(collider);
                        var renderer = rock.GetComponent<Renderer>();
                        if (renderer != null) renderer.sharedMaterial = rockMaterial;
                    }
                }
        }
    }

    private void BuildBackdrop()
    {
        if (root == null) return;

        // The authored distance hills stop just past the old 900 m finish. Reuse
        // their material so the horizon behind the new 1500 m gate stays lush.
        Material hillMaterial = null;
        var template = GameObject.Find("Tropical Foreground Hill");
        if (template != null)
        {
            var templateRenderer = template.GetComponentInChildren<Renderer>();
            if (templateRenderer != null) hillMaterial = templateRenderer.sharedMaterial;
        }
        if (hillMaterial == null)
            hillMaterial = MakeMaterial(new Color(0.24f, 0.43f, 0.36f), "Distance Hill");

        for (int i = 0; i < 8; i++)
        {
            bool behindFinish = i < 4;
            float x = (i % 2 == 0 ? 1f : -1f) *
                Random.Range(behindFinish ? 70f : 90f, behindFinish ? 125f : 140f);
            float z = behindFinish
                ? TargetFinishZ + Random.Range(30f, 130f)
                : Mathf.Lerp(1060f, TargetFinishZ - 80f, (i - 4) / 3f) + Random.Range(-18f, 18f);
            var hill = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            hill.name = "Distance Hill (Extension)";
            hill.transform.SetParent(root, false);
            hill.transform.position = new Vector3(x, -7f, z);
            hill.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            hill.transform.localScale = new Vector3(
                Random.Range(50f, 90f), Random.Range(18f, 32f), Random.Range(45f, 80f));
            var collider = hill.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            var renderer = hill.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = hillMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }
    }

    private static Bounds RendererBounds(GameObject target)
    {
        bool found = false;
        Bounds bounds = default;
        foreach (var renderer in target.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer == null) continue;
            if (!found) { bounds = renderer.bounds; found = true; }
            else bounds.Encapsulate(renderer.bounds);
        }
        return bounds;
    }

    private static void CreatePart(Transform parent, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
    {
        var part = GameObject.CreatePrimitive(type);
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.transform.localScale = scale;
        var collider = part.GetComponent<Collider>();
        if (collider != null)
        {
            // Destroy is deferred to the end of the frame; disable it now so
            // same-frame checks (deck collider fallback) cannot see it.
            collider.enabled = false;
            Destroy(collider);
        }
        var renderer = part.GetComponent<Renderer>();
        if (renderer != null && material != null) renderer.sharedMaterial = material;
    }

    private Material LoadWaterMaterial()
    {
        var source = Resources.Load<Material>("Track/PT_Water_mat");
        if (source != null && source.shader != null && source.shader.isSupported)
            return new Material(source) { name = "Bridge Water (Runtime)" };
        return CreateWaterFallback();
    }

    private static Material CreateWaterFallback()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Hidden/InternalErrorShader");
        var material = new Material(shader) { name = "Bridge Water (Fallback)" };
        Color color = new Color(0.16f, 0.65f, 0.75f, 0.78f);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 0f);
        if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        return material;
    }

    private static Material MakeMaterial(Color color, string name)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Hidden/InternalErrorShader");
        var material = new Material(shader) { name = name };
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        return material;
    }

    // ---------------------------------------------------------------------
    // Water hazard
    // ---------------------------------------------------------------------

    /// <summary>True while the given world Z is over one of the runtime rivers.</summary>
    public static bool IsInsideWaterZone(float worldZ)
    {
        return FindSpan(worldZ, out _);
    }

    /// <summary>Scenery chunks that overlap a river are skipped so props do not float.</summary>
    public static bool OverlapsWater(float startZ, float endZ)
    {
        for (int i = 0; i < waterSpans.Count; i++)
        {
            WaterSpan span = waterSpans[i];
            if (endZ < span.Start || startZ > span.End) continue;
            return true;
        }
        return false;
    }

    public static bool TryHandlePlayerWaterFall(PlayerHealth health, CharacterController controller)
    {
        if (active == null || health == null || controller == null || !controller.enabled) return false;
        Vector3 position = health.transform.position;
        if (!FindSpan(position.z, out WaterSpan span)) return false;
        if (BridgeDeck.TryGetHeight(position, out _)) return false;
        if (!controller.isGrounded && position.y > BridgeFallLine) return false;
        if (active.playerFalling || health.IsRunOver) return false;
        active.playerFalling = true;
        health.BeginWaterFall();
        active.StartCoroutine(active.RespawnPlayer(health, span));
        return true;
    }

    public static bool TryHandleRivalWaterFall(RivalRunner runner, CharacterController controller)
    {
        if (active == null || runner == null || controller == null || !controller.enabled) return false;
        Vector3 position = runner.transform.position;
        if (!FindSpan(position.z, out WaterSpan span)) return false;
        if (BridgeDeck.TryGetHeight(position, out _)) return false;
        if (!controller.isGrounded && position.y > BridgeFallLine) return false;
        if (active.rivalFalling || runner.HasFinished) return false;
        active.rivalFalling = true;
        runner.BeginWaterFall();
        active.StartCoroutine(active.RespawnRival(runner, span));
        return true;
    }

    private static bool FindSpan(float worldZ, out WaterSpan span)
    {
        for (int i = 0; i < waterSpans.Count; i++)
        {
            WaterSpan candidate = waterSpans[i];
            if (worldZ > candidate.Start + 0.05f && worldZ < candidate.End - 0.05f)
            {
                span = candidate;
                return true;
            }
        }
        span = default;
        return false;
    }

    private IEnumerator RespawnPlayer(PlayerHealth health, WaterSpan span)
    {
        if (health == null) { playerFalling = false; yield break; }
        Vector3 start = health.transform.position;
        float targetY = BridgeWaterY - 0.7f;
        for (float elapsed = 0f; elapsed < SinkDuration; elapsed += Time.deltaTime)
        {
            if (health == null) break;
            float blend = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / SinkDuration));
            Vector3 position = start;
            position.y = Mathf.Lerp(start.y, targetY, blend);
            health.transform.position = position;
            yield return null;
        }
        Splash(new Vector3(start.x, BridgeWaterY + 0.03f, start.z));
        yield return new WaitForSeconds(RespawnDelay - SinkDuration);
        if (health != null && !health.IsRunOver)
            health.RespawnAfterWater(FindRespawnPoint(health.GetComponent<CharacterController>(), span, 0f, span.End + 6f), RespawnInvulnerability);
        playerFalling = false;
    }

    private IEnumerator RespawnRival(RivalRunner runner, WaterSpan span)
    {
        if (runner == null) { rivalFalling = false; yield break; }
        Vector3 start = runner.transform.position;
        float targetY = BridgeWaterY - 0.7f;
        for (float elapsed = 0f; elapsed < SinkDuration; elapsed += Time.deltaTime)
        {
            if (runner == null) break;
            float blend = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / SinkDuration));
            Vector3 position = start;
            position.y = Mathf.Lerp(start.y, targetY, blend);
            runner.transform.position = position;
            yield return null;
        }
        Splash(new Vector3(start.x, BridgeWaterY + 0.03f, start.z));
        yield return new WaitForSeconds(RespawnDelay - SinkDuration);
        if (runner != null && !runner.HasFinished)
            runner.RespawnFromWater(FindRespawnPoint(runner.GetComponent<CharacterController>(), span, 2.2f, span.End + 8.5f), RespawnInvulnerability);
        rivalFalling = false;
    }

    private static Vector3 FindRespawnPoint(CharacterController controller, WaterSpan span, float laneOffset, float fallbackZ)
    {
        float x = (active != null && active.player != null ? active.player.CenterX : 0f) + laneOffset;
        // Land just past the bridge deck (it extends 4 m beyond each bank) so
        // the restored runner never spawns inside the arch or its rails.
        float z = Mathf.Max(span.End + 4.5f, fallbackZ);
        float bottom = controller != null ? controller.center.y - controller.height * 0.5f : 0f;
        float skin = controller != null ? controller.skinWidth : 0f;
        if (Physics.Raycast(new Vector3(x, 8f, z), Vector3.down, out RaycastHit hit, 20f,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            return new Vector3(x, hit.point.y - bottom + skin, z);
        return new Vector3(x, GroundTopY - bottom + skin, z);
    }

    private void Splash(Vector3 point)
    {
        if (splashMaterial == null) splashMaterial = MakeSplashMaterial();
        var effect = new GameObject("Bridge Water Splash");
        effect.transform.position = point;
        var particles = effect.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = particles.main;
        main.loop = false;
        main.playOnAwake = false;
        main.duration = 0.4f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 4.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.75f, 0.97f, 1f, 0.95f), new Color(0.24f, 0.68f, 0.8f, 0.9f));
        main.gravityModifier = 1.1f;
        main.maxParticles = 90;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)45), new ParticleSystem.Burst(0.07f, (short)20) });
        var shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.25f;
        var renderer = effect.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = splashMaterial;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
        }
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        particles.Play();
        Destroy(effect, 2f);
    }

    private static Material MakeSplashMaterial()
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) return MakeMaterial(Color.white, "Bridge Splash");
        var material = new Material(shader) { name = "Bridge Splash" };
        if (material.HasProperty("_Color")) material.SetColor("_Color", Color.white);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
        return material;
    }
}
