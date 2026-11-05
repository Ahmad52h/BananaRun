using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

/// <summary>Retires the authored opening before any runtime builder creates replacements.</summary>
public static class ProceduralTrackStartup
{
    public const float ClearRunwayEnd = 25f;
    public const float AuthoredOpeningEnd = 220f;
    private static readonly HashSet<Scene> preparedScenes = new HashSet<Scene>();
    public static int DisabledRootCount { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        preparedScenes.Clear();
        DisabledRootCount = 0;
        SceneManager.sceneLoaded -= Loaded;
        SceneManager.sceneUnloaded -= Unloaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        SceneManager.sceneLoaded += Loaded;
        SceneManager.sceneUnloaded += Unloaded;
    }

    private static void Loaded(Scene scene, LoadSceneMode mode) => Prepare(scene);
    private static void Unloaded(Scene scene) => preparedScenes.Remove(scene);

    public static void Prepare(Scene scene)
    {
        if (scene.name != "Level01" || !preparedScenes.Add(scene)) return;
        DisabledRootCount = 0;
        // Snapshot once. Never run this over later pooled/runtime instances.
        foreach (var root in scene.GetRootGameObjects()) Visit(root.transform, false);
    }

    private static void Visit(Transform node, bool manualGameplay)
    {
        var scenery = node.GetComponent<TropicalScenery>();
        if (scenery != null) scenery.DisableAuthoredPreview();
        if (!node.gameObject.activeInHierarchy || IsCore(node)) return;
        manualGameplay |= node.name == "Gameplay";
        bool pickupOrObstacle = node.GetComponent<BananaPickup>() != null || node.GetComponent<CoconutPickup>() != null ||
            node.GetComponent<BombPickup>() != null || node.GetComponent<PowerUpPickup>() != null ||
            node.GetComponent<RunnerObstacle>() != null || node.GetComponent<DynamicRunnerHazard>() != null ||
            node.GetComponent<BananaPeelHazard>() != null;
        bool inOpening = node.position.z >= 0f && node.position.z <= AuthoredOpeningEnd;
        if (Has(node.name, "water") && node.TryGetComponent<Renderer>(out var water))
            inOpening |= water.bounds.max.z >= 0f && water.bounds.min.z <= AuthoredOpeningEnd;
        bool decorative = Has(node.name, "ForestChunk") || Has(node.name, "tree") || Has(node.name, "palm") ||
            Has(node.name, "bush") || Has(node.name, "shrub") || Has(node.name, "foliage") ||
            Has(node.name, "grassstrip") || Has(node.name, "water") || Has(node.name, "rock") ||
            Has(node.name, "prop") || Has(node.name, "hurdle") || Has(node.name, "obstacle") || Has(node.name, "log");
        // The Gameplay group has a few hand-placed pickups past 220m too. Retire that
        // complete manual layout so it cannot overlap the new full-course generator.
        bool retire = pickupOrObstacle && (inOpening || manualGameplay) ||
            inOpening && (decorative || node.name == "Trail Edge Details" || node.name == "Intro Bomb Pickup" ||
                node.parent != null && node.parent.name == "Trail Edge Details");
        if (retire && !ContainsCore(node))
        {
            node.gameObject.SetActive(false);
            DisabledRootCount++;
            return;
        }
        for (int i = 0; i < node.childCount; i++) Visit(node.GetChild(i), manualGameplay);
    }

    private static bool Has(string name, string part) => name.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0;

    private static bool ContainsCore(Transform root)
    {
        foreach (var node in root.GetComponentsInChildren<Transform>(true)) if (IsCore(node)) return true;
        return false;
    }

    private static bool IsCore(Transform node) =>
        node.GetComponent<Camera>() != null || node.GetComponent<Light>() != null &&
        node.GetComponent<Light>().type == LightType.Directional || node.GetComponent<Canvas>() != null ||
        node.GetComponent<EventSystem>() != null || node.GetComponent<RunnerMotor>() != null ||
        node.GetComponent<RivalRunner>() != null || node.GetComponent<GiantGorillaChaser>() != null ||
        node.GetComponent<RunnerCourseGenerator>() != null || node.GetComponent<TropicalScenery>() != null ||
        node.GetComponent<RaceStartSequence>() != null || node.GetComponent<WoodenStartingGate>() != null ||
        node.GetComponent<RaceProgressTracker>() != null || node.GetComponent<RaceCutsceneDirector>() != null ||
        node.GetComponent<RunnerHUD>() != null || node.GetComponent<RunnerAudio>() != null ||
        node.GetComponent<LevelEnvironmentManager>() != null || node.GetComponent<FinishGate>() != null ||
        node.GetComponent<BridgeDeck>() != null || node.GetComponent<Terrain>() != null ||
        node.name == "Ground" || node.name == "Ground Before River" || node.name == "Landing Ground" ||
        node.name == "Organic Dirt Trail" || node.name == "River Gap Module" || node.name == "Finish" ||
        node.name == "Tropical Finish Arch" || node.name == "Celebration Banana Basket";
}
