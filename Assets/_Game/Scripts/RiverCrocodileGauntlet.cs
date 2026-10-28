using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Runtime-only river upgrade: two lane-specific vines, crocodile swimmers and a lake
/// fall/respawn loop. It deliberately avoids editing the Level01 scene YAML.
/// </summary>
[DisallowMultipleComponent]
public sealed class RiverCrocodileGauntlet : MonoBehaviour
{
    private const string RiverRootName = "River Gap Module";
    private const float RespawnDelay = 1.5f;
    private const float SinkDuration = 0.5f;
    private const float RespawnInvulnerability = 2.25f;

    /// <summary>Y coordinate used by movement scripts to detect a river miss.</summary>
    public const float WaterFallY = -2.5f;

    /// <summary>The live gauntlet, used by runner movement as a water-fall backstop.</summary>
    public static RiverCrocodileGauntlet Active { get; private set; }

    private RunnerMotor player;
    private RivalRunner rival;
    private Transform riverRoot;
    private float centerX, laneDistance, waterY, riverStartZ, riverEndZ, respawnZ;
    private float farBankTopY;
    private bool hasFarBankTop;
    private bool playerRespawning, rivalRespawning;

    private readonly List<CrocodileSwimmer> crocodiles = new List<CrocodileSwimmer>();
    private Material crocDark, crocBelly, crocScutes, crocEye, crocPupil, crocTooth, splashMaterial;
    private bool materialsReady;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindAnyObjectByType<RiverCrocodileGauntlet>() != null) return;

        var host = new GameObject("River Crocodile Gauntlet");
        DontDestroyOnLoad(host);
        var gauntlet = host.AddComponent<RiverCrocodileGauntlet>();
        gauntlet.SetupActiveScene();
        SceneManager.sceneLoaded += gauntlet.OnSceneLoaded;
    }

    private void Awake()
    {
        Active = this;
    }

    private void OnDestroy()
    {
        if (Active == this) Active = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SetupActiveScene();
    }

    private void SetupActiveScene()
    {
        StopAllCoroutines();
        playerRespawning = false;
        rivalRespawning = false;
        crocodiles.Clear();
        player = null;
        rival = null;
        riverRoot = null;
        Setup();
    }

    private void Setup()
    {
        var gap = GameObject.Find(RiverRootName);
        if (gap == null) return;

        riverRoot = gap.transform;
        hasFarBankTop = false;
        if (riverRoot.Find("Crocodile Lake Trigger") != null) return;

        player = FindAnyObjectByType<RunnerMotor>();
        rival = FindAnyObjectByType<RivalRunner>();
        if (player == null) return;

        Transform triggerSource = riverRoot.Find("Vine Swing Trigger");
        Transform landingSource = riverRoot.Find("Safe Landing");
        Transform pivotSource = riverRoot.Find("Overhead Vine Anchor");
        Transform vineSource = riverRoot.Find("Visible Hanging Vine");
        Transform canopySource = riverRoot.Find("Vine Canopy");
        Transform waterSource = riverRoot.Find("Turquoise River");
        if (triggerSource == null || triggerSource.GetComponent<VineSwingZone>() == null ||
            landingSource == null || pivotSource == null) return;

        EnsureMaterials();

        laneDistance = player.LaneDistance;
        float triggerZ = triggerSource.position.z;
        Renderer waterRenderer = waterSource != null ? waterSource.GetComponent<Renderer>() : null;
        if (waterRenderer != null)
        {
            Bounds bounds = waterRenderer.bounds;
            centerX = bounds.center.x;
            waterY = bounds.max.y;
            riverStartZ = bounds.min.z;
            riverEndZ = bounds.max.z;
        }
        else
        {
            centerX = triggerSource.position.x;
            waterY = -3.025f;
            riverStartZ = triggerZ + 3f;
            riverEndZ = Mathf.Max(riverStartZ + 1f, landingSource.position.z - 4f);
        }
        if (riverEndZ <= riverStartZ) riverEndZ = riverStartZ + 32f;
        respawnZ = Mathf.Max(riverEndZ + 4f, landingSource.position.z);

        // Prefer the authored bank top over a probe at the respawn point. A course
        // obstacle can occupy that exact row, and the runner should never respawn
        // standing on top of a hurdle.
        Transform landingGround = riverRoot.Find("Landing Ground");
        if (landingGround != null)
        {
            float top = float.NegativeInfinity;
            foreach (var collider in landingGround.GetComponentsInChildren<Collider>(true))
                top = Mathf.Max(top, collider.bounds.max.y);
            if (top == float.NegativeInfinity)
                foreach (var renderer in landingGround.GetComponentsInChildren<Renderer>(true))
                    top = Mathf.Max(top, renderer.bounds.max.y);
            if (top > float.NegativeInfinity)
            {
                farBankTopY = top;
                hasFarBankTop = true;
            }
        }

        var vineRoot = new GameObject("Dual River Vines").transform;
        vineRoot.SetParent(riverRoot, false);
        var laneXs = new List<float>(2);
        CreateVine(vineRoot, triggerSource, landingSource, pivotSource, vineSource, canopySource,
            centerX - laneDistance, -1, laneXs);
        CreateVine(vineRoot, triggerSource, landingSource, pivotSource, vineSource, canopySource,
            centerX + laneDistance, 1, laneXs);

        triggerSource.gameObject.SetActive(false);
        landingSource.gameObject.SetActive(false);
        pivotSource.gameObject.SetActive(false);
        if (vineSource != null) vineSource.gameObject.SetActive(false);
        if (canopySource != null) canopySource.gameObject.SetActive(false);

        if (rival != null) rival.ConfigureRiverVines(laneXs.ToArray(), triggerZ);

        // Runners must fall through every water surface. Convert any authored water
        // colliders to triggers as well as relying on the movement-script backstop.
        if (waterSource != null) MakeTriggers(waterSource);
        // The far waterfall lip used to be a standable collider just before the
        // landing bank; remove it so a missed vine cannot become a thin ledge.
        Transform waterfall = riverRoot.Find("Waterfall Face");
        if (waterfall != null)
        {
            MakeTriggers(waterfall);
            DisableColliders(waterfall);
        }

        CreateLakeTrigger();
        SpawnCrocodiles();
    }

    /// <summary>True while a runner's Z is over open water between the two banks.</summary>
    public bool IsInsideRiverGap(float z)
    {
        return riverEndZ > riverStartZ &&
            z > riverStartZ + 0.05f && z < riverEndZ - 0.05f;
    }

    private void CreateVine(Transform vineRoot, Transform triggerSource, Transform landingSource,
        Transform pivotSource, Transform vineSource, Transform canopySource, float laneX, int laneId,
        List<float> laneXs)
    {
        string side = laneId < 0 ? "Left" : "Right";
        var trigger = Object.Instantiate(triggerSource.gameObject, vineRoot, true);
        trigger.name = "Vine Swing Trigger " + side;
        trigger.SetActive(false);
        SetWorldX(trigger.transform, laneX);

        Transform landing = CloneVisual(landingSource, vineRoot, "Safe Landing " + side, laneX);
        Transform pivot = CloneVisual(pivotSource, vineRoot, "Overhead Vine Anchor " + side, laneX);
        Transform vine = vineSource != null
            ? CloneVisual(vineSource, vineRoot, "Visible Hanging Vine " + side, laneX)
            : null;
        Transform canopy = canopySource != null
            ? CloneVisual(canopySource, vineRoot, "Vine Canopy " + side, laneX)
            : null;

        StripColliders(landing);
        StripColliders(pivot);
        if (vine != null) StripColliders(vine);
        if (canopy != null) StripColliders(canopy);

        var box = trigger.GetComponent<BoxCollider>();
        if (box != null)
        {
            box.isTrigger = true;
            box.center = new Vector3(0f, 1.5f, 0f);
            box.size = new Vector3(Mathf.Max(1.6f, laneDistance * 0.72f), 4f, 5f);
        }

        var zone = trigger.GetComponent<VineSwingZone>();
        var line = trigger.GetComponent<LineRenderer>();
        var leaves = pivot.GetComponentInChildren<ParticleSystem>(true);
        if (zone != null) zone.ConfigureRuntime(landing, pivot, line, vine, leaves);

        trigger.SetActive(true);
        laneXs.Add(laneX);
    }

    private static Transform CloneVisual(Transform source, Transform parent, string name, float laneX)
    {
        var clone = Object.Instantiate(source.gameObject, parent, true).transform;
        clone.name = name;
        SetWorldX(clone, laneX);
        return clone;
    }

    private static void SetWorldX(Transform target, float x)
    {
        Vector3 position = target.position;
        position.x = x;
        target.position = position;
    }

    private static void MakeTriggers(Transform target)
    {
        if (target == null) return;
        foreach (var collider in target.GetComponentsInChildren<Collider>(true))
            collider.isTrigger = true;
    }

    private static void DisableColliders(Transform target)
    {
        if (target == null) return;
        foreach (var collider in target.GetComponentsInChildren<Collider>(true))
            collider.enabled = false;
    }

    private static void StripColliders(Transform target)
    {
        if (target == null) return;
        foreach (var collider in target.GetComponentsInChildren<Collider>(true))
        {
            collider.enabled = false;
            Object.Destroy(collider);
        }
    }

    private void CreateLakeTrigger()
    {
        var lake = new GameObject("Crocodile Lake Trigger");
        lake.transform.SetParent(riverRoot, false);

        float top = waterY + 1.4f;
        float length = (riverEndZ - riverStartZ) + 2f;
        lake.transform.position = new Vector3(centerX, top - 3.5f, (riverStartZ + riverEndZ) * 0.5f);

        var box = lake.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.center = Vector3.zero;
        box.size = new Vector3(30f, 7f, length);

        var body = lake.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;

        var volume = lake.AddComponent<CrocodileLake>();
        volume.Initialize(this);
    }

    private void SpawnCrocodiles()
    {
        var root = new GameObject("Crocodile Swimmers").transform;
        root.SetParent(riverRoot, false);

        float span = riverEndZ - riverStartZ;
        float[] lateral = { -9f, -5.5f, -1.5f, 2f, 5.5f, 9f };
        float[] depth = { 0.18f, 0.60f, 0.34f, 0.78f, 0.46f, 0.90f };
        for (int i = 0; i < lateral.Length; i++)
        {
            Vector3 home = new Vector3(centerX + lateral[i], waterY - 0.015f,
                riverStartZ + span * depth[i]);
            SpawnCrocodile(root, home, i);
        }
    }

    private void SpawnCrocodile(Transform parent, Vector3 home, int index)
    {
        var root = new GameObject("Crocodile " + (index + 1));
        root.transform.SetParent(parent, false);
        root.transform.position = home;
        root.transform.rotation = Quaternion.Euler(0f, index * 53f + 20f, 0f);
        var swimmer = root.AddComponent<CrocodileSwimmer>();
        Transform bodyRoot = root.transform;

        Part(bodyRoot, PrimitiveType.Cube, Vector3.zero, new Vector3(0.95f, 0.32f, 2.3f), crocDark);
        Part(bodyRoot, PrimitiveType.Cube, new Vector3(0f, -0.12f, 0.08f),
            new Vector3(0.72f, 0.14f, 2.0f), crocBelly);
        Part(bodyRoot, PrimitiveType.Cube, new Vector3(0f, 0.08f, 1.35f),
            new Vector3(0.62f, 0.24f, 0.85f), crocDark);
        Part(bodyRoot, PrimitiveType.Cube, new Vector3(0f, 0.055f, 1.95f),
            new Vector3(0.46f, 0.17f, 0.85f), crocDark);

        var jaw = new GameObject("Jaw Pivot").transform;
        jaw.SetParent(bodyRoot, false);
        jaw.localPosition = new Vector3(0f, -0.07f, 1.45f);
        Part(jaw, PrimitiveType.Cube, new Vector3(0f, -0.035f, 0.5f),
            new Vector3(0.42f, 0.10f, 0.9f), crocBelly);

        for (int i = 0; i < 4; i++)
        {
            float z = 1.72f + i * 0.16f;
            Part(bodyRoot, PrimitiveType.Cube, new Vector3(-0.17f, -0.005f, z),
                new Vector3(0.045f, 0.08f, 0.045f), crocTooth);
            Part(bodyRoot, PrimitiveType.Cube, new Vector3(0.17f, -0.005f, z),
                new Vector3(0.045f, 0.08f, 0.045f), crocTooth);
            Part(jaw, PrimitiveType.Cube, new Vector3(-0.14f, 0.025f, 0.35f + i * 0.14f),
                new Vector3(0.04f, 0.06f, 0.04f), crocTooth);
            Part(jaw, PrimitiveType.Cube, new Vector3(0.14f, 0.025f, 0.35f + i * 0.14f),
                new Vector3(0.04f, 0.06f, 0.04f), crocTooth);
        }

        Part(bodyRoot, PrimitiveType.Sphere, new Vector3(-0.22f, 0.25f, 1.53f),
            new Vector3(0.12f, 0.12f, 0.12f), crocEye);
        Part(bodyRoot, PrimitiveType.Sphere, new Vector3(0.22f, 0.25f, 1.53f),
            new Vector3(0.12f, 0.12f, 0.12f), crocEye);
        Part(bodyRoot, PrimitiveType.Sphere, new Vector3(-0.22f, 0.27f, 1.585f),
            new Vector3(0.055f, 0.055f, 0.055f), crocPupil);
        Part(bodyRoot, PrimitiveType.Sphere, new Vector3(0.22f, 0.27f, 1.585f),
            new Vector3(0.055f, 0.055f, 0.055f), crocPupil);

        for (int i = 0; i < 5; i++)
            Part(bodyRoot, PrimitiveType.Cube, new Vector3(0f, 0.195f, -0.75f + i * 0.42f),
                new Vector3(0.18f, 0.10f, 0.16f), crocScutes);

        Part(bodyRoot, PrimitiveType.Cube, new Vector3(-0.5f, -0.13f, 0.6f),
            new Vector3(0.18f, 0.16f, 0.48f), crocDark);
        Part(bodyRoot, PrimitiveType.Cube, new Vector3(0.5f, -0.13f, 0.6f),
            new Vector3(0.18f, 0.16f, 0.48f), crocDark);
        Part(bodyRoot, PrimitiveType.Cube, new Vector3(-0.5f, -0.13f, -0.65f),
            new Vector3(0.18f, 0.16f, 0.48f), crocDark);
        Part(bodyRoot, PrimitiveType.Cube, new Vector3(0.5f, -0.13f, -0.65f),
            new Vector3(0.18f, 0.16f, 0.48f), crocDark);

        var tailRoot = new GameObject("Tail").transform;
        tailRoot.SetParent(bodyRoot, false);
        tailRoot.localPosition = new Vector3(0f, 0f, -1.0f);
        Part(tailRoot, PrimitiveType.Cube, new Vector3(0f, 0f, -0.45f),
            new Vector3(0.60f, 0.22f, 0.9f), crocDark);

        var tailTip = new GameObject("Tail Tip").transform;
        tailTip.SetParent(tailRoot, false);
        tailTip.localPosition = new Vector3(0f, 0f, -0.95f);
        Part(tailTip, PrimitiveType.Cube, new Vector3(0f, 0f, -0.5f),
            new Vector3(0.36f, 0.15f, 1.0f), crocDark);

        swimmer.Configure(tailRoot, tailTip, jaw, home,
            0.9f + (index % 3) * 0.3f, 0.24f + (index % 2) * 0.07f, index * 1.37f);
        crocodiles.Add(swimmer);
    }

    private static Transform Part(Transform parent, PrimitiveType type, Vector3 localPosition,
        Vector3 localScale, Material material)
    {
        var part = GameObject.CreatePrimitive(type);
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition;
        part.transform.localScale = localScale;
        var collider = part.GetComponent<Collider>();
        if (collider != null)
        {
            collider.enabled = false;
            Object.Destroy(collider);
        }
        var renderer = part.GetComponent<Renderer>();
        if (renderer != null && material != null) renderer.sharedMaterial = material;
        return part.transform;
    }

    private void EnsureMaterials()
    {
        if (materialsReady) return;
        materialsReady = true;
        crocDark = MakeMaterial(new Color(0.14f, 0.25f, 0.11f), "Crocodile Hide");
        crocBelly = MakeMaterial(new Color(0.36f, 0.46f, 0.24f), "Crocodile Belly");
        crocScutes = MakeMaterial(new Color(0.08f, 0.16f, 0.07f), "Crocodile Scutes");
        crocEye = MakeMaterial(new Color(0.95f, 0.72f, 0.10f), "Crocodile Eyes");
        crocPupil = MakeMaterial(new Color(0.025f, 0.025f, 0.02f), "Crocodile Pupils");
        crocTooth = MakeMaterial(new Color(0.93f, 0.89f, 0.72f), "Crocodile Teeth");
        splashMaterial = MakeParticleMaterial();
    }

    private static Material MakeMaterial(Color color, string name)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Hidden/InternalErrorShader");
        var material = new Material(shader) { name = name, color = color };
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        return material;
    }

    private static Material MakeParticleMaterial()
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) return MakeMaterial(Color.white, "Crocodile Splash");
        var material = new Material(shader) { name = "Crocodile Splash" };
        if (material.HasProperty("_Color")) material.SetColor("_Color", Color.white);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
        return material;
    }

    public void HandlePlayerFall(PlayerHealth health)
    {
        if (health == null || playerRespawning || health.IsRunOver) return;
        playerRespawning = true;
        health.BeginWaterFall();
        StartCoroutine(RespawnPlayer(health));
    }

    public void HandleRivalFall(RivalRunner runner)
    {
        if (runner == null || rivalRespawning || runner.HasFinished) return;
        rivalRespawning = true;
        runner.BeginWaterFall();
        StartCoroutine(RespawnRival(runner));
    }

    private IEnumerator RespawnPlayer(PlayerHealth health)
    {
        if (health == null)
        {
            playerRespawning = false;
            yield break;
        }

        Vector3 start = health.transform.position;
        float targetY = Mathf.Min(waterY - 0.7f, WaterFallY);
        for (float elapsed = 0f; elapsed < SinkDuration; elapsed += Time.deltaTime)
        {
            if (health == null) break;
            float blend = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / SinkDuration));
            Vector3 position = start;
            position.y = Mathf.Lerp(start.y, targetY, blend);
            health.transform.position = position;
            yield return null;
        }

        Vector3 splashPoint = new Vector3(start.x, waterY + 0.03f, start.z);
        if (health != null)
        {
            Vector3 position = health.transform.position;
            position.y = targetY;
            health.transform.position = position;
        }
        Splash(splashPoint);
        ReactCrocodiles(splashPoint);

        float remaining = Mathf.Max(0f, RespawnDelay - SinkDuration);
        if (remaining > 0f) yield return new WaitForSeconds(remaining);

        if (health != null && !health.IsRunOver)
        {
            var controller = health.GetComponent<CharacterController>();
            health.RespawnAfterWater(FindFarBankSpawn(controller), RespawnInvulnerability);
        }
        playerRespawning = false;
    }

    private IEnumerator RespawnRival(RivalRunner runner)
    {
        if (runner == null)
        {
            rivalRespawning = false;
            yield break;
        }

        Vector3 start = runner.transform.position;
        float targetY = Mathf.Min(waterY - 0.7f, WaterFallY);
        for (float elapsed = 0f; elapsed < SinkDuration; elapsed += Time.deltaTime)
        {
            if (runner == null) break;
            float blend = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / SinkDuration));
            Vector3 position = start;
            position.y = Mathf.Lerp(start.y, targetY, blend);
            runner.transform.position = position;
            yield return null;
        }

        Vector3 splashPoint = new Vector3(start.x, waterY + 0.03f, start.z);
        if (runner != null)
        {
            Vector3 position = runner.transform.position;
            position.y = targetY;
            runner.transform.position = position;
        }
        Splash(splashPoint);
        ReactCrocodiles(splashPoint);

        float remaining = Mathf.Max(0f, RespawnDelay - SinkDuration);
        if (remaining > 0f) yield return new WaitForSeconds(remaining);

        if (runner != null && !runner.HasFinished)
        {
            var controller = runner.GetComponent<CharacterController>();
            runner.RespawnFromWater(FindFarBankSpawn(controller), RespawnInvulnerability);
        }
        rivalRespawning = false;
    }

    private Vector3 FindFarBankSpawn(CharacterController controller)
    {
        float bottom = controller != null ? controller.center.y - controller.height * 0.5f : 0f;
        float skin = controller != null ? controller.skinWidth : 0f;
        if (hasFarBankTop)
            return new Vector3(centerX, farBankTopY - bottom + skin, respawnZ);

        Vector3 origin = new Vector3(centerX, 10f, respawnZ);
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 30f,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        RaycastHit ground = new RaycastHit();
        float nearest = float.MaxValue;
        for (int i = 0; i < hits.Length; i++)
        {
            Collider hit = hits[i].collider;
            if (hit == null || hit.GetComponentInParent<CharacterController>() != null) continue;
            if (hits[i].distance < nearest)
            {
                nearest = hits[i].distance;
                ground = hits[i];
            }
        }
        if (ground.collider != null)
            return new Vector3(centerX, ground.point.y - bottom + (controller != null ? controller.skinWidth : 0f), respawnZ);
        return new Vector3(centerX, -0.25f - bottom, respawnZ);
    }

    private void Splash(Vector3 point)
    {
        if (splashMaterial == null) EnsureMaterials();
        var effect = new GameObject("Crocodile Lake Splash");
        effect.transform.position = point;
        var particles = effect.AddComponent<ParticleSystem>();

        var main = particles.main;
        main.loop = false;
        main.playOnAwake = false;
        main.duration = 0.4f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.9f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2.2f, 5.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.72f, 0.96f, 1f, 0.95f), new Color(0.25f, 0.66f, 0.78f, 0.9f));
        main.gravityModifier = 1.2f;
        main.maxParticles = 120;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[]
        {
            new ParticleSystem.Burst(0f, (short)60),
            new ParticleSystem.Burst(0.08f, (short)24)
        });

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
        Object.Destroy(effect, 2f);
    }

    private void ReactCrocodiles(Vector3 point)
    {
        for (int i = 0; i < crocodiles.Count; i++)
        {
            var crocodile = crocodiles[i];
            if (crocodile == null) continue;
            if ((crocodile.transform.position - point).sqrMagnitude <= 18f * 18f)
                crocodile.ReactToSplash(point);
        }
    }
}
