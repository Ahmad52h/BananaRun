using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Runtime-only Level01 visibility window. Disabling roots also stops physics and scripts.</summary>
[DefaultExecutionOrder(10000)]
[DisallowMultipleComponent]
public sealed class TrackDistanceStreamer : MonoBehaviour
{
    public const float BehindCameraDistance = 15f;
    public const float AheadRunnerDistance = 110f;
    private const float RefreshInterval = 0.1f;
    private static TrackDistanceStreamer active;

    private struct Entry
    {
        public GameObject Object;
        public Transform Transform;
        public float Z;
        public bool Hidden, Consumed;
        public BananaPickup Banana;
        public CoconutPickup Coconut;
        public BombPickup Bomb;
        public PowerUpPickup PowerUp;
    }

    private readonly List<Entry> entries = new List<Entry>(2048);
    private readonly HashSet<GameObject> registered = new HashSet<GameObject>();
    private Transform player, rival;
    private Camera trackCamera;
    private bool initialized;
    private float nextRefresh;

    public float MinRunnerZ { get; private set; }
    public float MaxRunnerZ { get; private set; }
    public float CullBehindZ { get; private set; }
    public float RenderAheadZ { get; private set; }
    public int RegisteredCount => entries.Count;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        active = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "Level01") return;
        foreach (var root in scene.GetRootGameObjects())
            if (root.GetComponent<TrackDistanceStreamer>() != null) return;
        var host = new GameObject("Track Distance Streamer (Runtime)");
        SceneManager.MoveGameObjectToScene(host, scene);
        host.AddComponent<TrackDistanceStreamer>();
    }

    private void Awake() => active = this;

    private void OnDestroy()
    {
        if (active == this) active = null;
    }

    private void LateUpdate()
    {
        if (Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + RefreshInterval;
        if (!UpdateWindow()) return;
        if (!initialized)
        {
            // All course/scenery Start methods have finished before this first LateUpdate.
            initialized = true;
            foreach (var root in gameObject.scene.GetRootGameObjects())
                RegisterHierarchy(root.transform);
        }

        for (int i = entries.Count - 1; i >= 0; i--)
        {
            Entry entry = entries[i];
            if (entry.Object == null)
            {
                registered.Remove(entry.Object);
                entries[i] = entries[entries.Count - 1];
                entries.RemoveAt(entries.Count - 1);
                continue;
            }
            ApplyVisibility(ref entry);
            entries[i] = entry;
        }
    }

    private bool UpdateWindow()
    {
        if (player == null)
        {
            var runner = FindAnyObjectByType<RunnerMotor>();
            if (runner != null) player = runner.transform;
        }
        if (rival == null && RivalRunner.ActiveRival != null)
            rival = RivalRunner.ActiveRival.transform;
        if (trackCamera == null || !trackCamera.isActiveAndEnabled || !trackCamera.CompareTag("MainCamera"))
            trackCamera = Camera.main;
        if ((player == null && rival == null) || trackCamera == null) return false;

        float playerZ = player != null ? player.position.z : rival.position.z;
        float rivalZ = rival != null ? rival.position.z : playerZ;
        MinRunnerZ = Mathf.Min(playerZ, rivalZ);
        MaxRunnerZ = Mathf.Max(playerZ, rivalZ);
        CullBehindZ = trackCamera.transform.position.z - BehindCameraDistance;
        RenderAheadZ = MaxRunnerZ + AheadRunnerDistance;
        return true;
    }

    /// <summary>Also lets the foliage producer use both runners and the same camera cutoff.</summary>
    public static bool TryGetWindow(out float cullBehindZ, out float renderAheadZ)
    {
        cullBehindZ = renderAheadZ = 0f;
        if (active == null || !active.UpdateWindow()) return false;
        cullBehindZ = active.CullBehindZ;
        renderAheadZ = active.RenderAheadZ;
        return true;
    }

    public static bool HaveBothRunnersPassed(float z) => active != null &&
        z < active.MinRunnerZ - BehindCameraDistance;

    /// <summary>Register a newly spawned pickup or a complete decorative chunk, once configured.</summary>
    public static void Register(GameObject target)
    {
        if (active == null || !active.initialized || target == null ||
            target.scene != active.gameObject.scene || !target.activeInHierarchy) return;
        active.Add(target);
    }

    /// <summary>Only reusable decorative chunks unregister; consumed pickups keep their tombstones.</summary>
    public static void Unregister(GameObject target)
    {
        if (active == null || target == null || !active.registered.Remove(target)) return;
        for (int i = active.entries.Count - 1; i >= 0; i--)
        {
            if (active.entries[i].Object != target) continue;
            active.entries[i] = active.entries[active.entries.Count - 1];
            active.entries.RemoveAt(active.entries.Count - 1);
            return;
        }
    }

    public static void RefreshRenderSettings()
    {
        if (active == null) return;
        foreach (var entry in active.entries)
            if (entry.Object != null) MobilePerformanceBootstrap.PrepareTrackObject(entry.Object, IsSmallProp(entry));
    }

    private static bool IsSmallProp(Entry entry) =>
        entry.Banana != null || entry.Coconut != null || entry.Bomb != null || entry.PowerUp != null ||
        IsDecoration(entry.Object.name) || entry.Transform.parent != null &&
        entry.Transform.parent.GetComponent<TropicalScenery>() != null;

    private void RegisterHierarchy(Transform node)
    {
        if (!node.gameObject.activeInHierarchy || IsPersistentRoot(node)) return;
        bool pickup = node.GetComponent<BananaPickup>() != null || node.GetComponent<CoconutPickup>() != null ||
            node.GetComponent<BombPickup>() != null || node.GetComponent<PowerUpPickup>() != null;
        bool obstacle = node.GetComponent<RunnerObstacle>() != null || node.GetComponent<DynamicRunnerHazard>() != null ||
            node.GetComponent<BananaPeelHazard>() != null || node.GetComponent<CrocodileSwimmer>() != null;
        // Generated obstacle wrappers own all their visuals, not just a nested hitbox.
        bool generatedObject = node.parent != null &&
            (node.parent.GetComponent<RunnerCourseGenerator>() != null || node.parent.GetComponent<TropicalScenery>() != null) &&
            node.GetComponentInChildren<Renderer>() != null &&
            (pickup || (node.GetComponentInChildren<BananaPickup>() == null &&
                node.GetComponentInChildren<PowerUpPickup>() == null &&
                node.GetComponentInChildren<CoconutPickup>() == null && node.GetComponentInChildren<BombPickup>() == null));
        bool decoration = IsDecoration(node.name) && node.GetComponentInChildren<Renderer>() != null;
        if (pickup || obstacle || generatedObject || decoration)
        {
            Add(node.gameObject);
            return;
        }
        for (int i = 0; i < node.childCount; i++) RegisterHierarchy(node.GetChild(i));
    }

    private static bool IsDecoration(string name)
    {
        return name.IndexOf("tree", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("palm", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("bush", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("shrub", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("foliage", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("grassstrip", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("river bank rock", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("riverbank rock", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsPersistentRoot(Transform node)
    {
        // Positive registration plus explicit safeguards: never disable gameplay infrastructure.
        return node.GetComponent<RunnerMotor>() != null || node.GetComponent<RivalRunner>() != null ||
            node.GetComponent<GiantGorillaChaser>() != null || node.GetComponent<Canvas>() != null ||
            node.GetComponent<Camera>() != null || node.GetComponent<Light>() != null ||
            node.GetComponent<Terrain>() != null || node.GetComponent<FinishGate>() != null ||
            node.GetComponent<BridgeDeck>() != null || node.GetComponent<TrackDistanceStreamer>() != null ||
            node.GetComponent<TrackExpansion>() != null || node.GetComponent<LevelEnvironmentManager>() != null ||
            node.GetComponent<RaceProgressTracker>() != null || node.GetComponent<RaceCutsceneDirector>() != null ||
            node.GetComponent<RaceStartSequence>() != null || node.GetComponent<PowerUpManager>() != null ||
            node.name == "Finish" || node.name == "Tropical Finish Arch" || node.name == "Ground" ||
            node.name == "Terrain" || node.name == "Track Extension Ground" || node.name == "Bridge Deck Surface" ||
            node.name == "PT Wooden Bridge" || node.name == "Wooden Bridge (Fallback)" ||
            node.name == "Flowing Water Hazard" || node.name == "Turquoise River" || node.name == "River Bed";
    }

    private void Add(GameObject target)
    {
        if (registered.Contains(target)) return;
        // Protect persistent descendants even if a decorative wrapper was misnamed.
        foreach (var node in target.GetComponentsInChildren<Transform>(true))
            if (IsPersistentRoot(node) || node.GetComponent<RunnerCourseGenerator>() != null ||
                node.GetComponent<TropicalScenery>() != null) return;
        for (Transform parent = target.transform.parent; parent != null; parent = parent.parent)
            if (registered.Contains(parent.gameObject) || IsPersistentRoot(parent)) return;

        var entry = new Entry
        {
            Object = target, Transform = target.transform, Z = target.transform.position.z,
            Banana = target.GetComponent<BananaPickup>(), Coconut = target.GetComponent<CoconutPickup>(),
            Bomb = target.GetComponent<BombPickup>(), PowerUp = target.GetComponent<PowerUpPickup>()
        };
        registered.Add(target);
        MobilePerformanceBootstrap.PrepareTrackObject(target, IsSmallProp(entry));
        ApplyVisibility(ref entry);
        entries.Add(entry);
    }

    private void ApplyVisibility(ref Entry entry)
    {
        if (entry.Consumed) return;
        entry.Consumed = (entry.Banana != null && entry.Banana.IsCollected) ||
            (entry.Coconut != null && entry.Coconut.IsCollected) ||
            (entry.Bomb != null && entry.Bomb.IsCollected) ||
            (entry.PowerUp != null && entry.PowerUp.IsCollected) ||
            (!entry.Hidden && !entry.Object.activeSelf);
        if (entry.Consumed) return;

        // Magnets and moving hazards change Z; dormant objects retain their cached position.
        if (!entry.Hidden) entry.Z = entry.Transform.position.z;
        bool visible = entry.Z >= CullBehindZ && entry.Z <= RenderAheadZ;
        entry.Hidden = !visible;
        if (entry.Object.activeSelf != visible) entry.Object.SetActive(visible);
    }
}
