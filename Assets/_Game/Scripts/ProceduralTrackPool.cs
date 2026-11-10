using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Cheap full-course spawn plans; only the current visibility window rents live instances.</summary>
[DefaultExecutionOrder(11000)]
public sealed class ProceduralTrackPool : MonoBehaviour
{
    private const int MaxIdlePerPrefab = 12;
    private static ProceduralTrackPool active;
    private sealed class Item
    {
        public GameObject Object;
        public SphereCollider Sphere;
        public BananaPickup[] Bananas;
        public CoconutPickup[] Coconuts;
        public BombPickup[] Bombs;
        public PowerUpPickup[] Powers;
        public RunnerObstacle[] Obstacles;
        public BananaPeelHazard[] Peels;
        public DynamicRunnerHazard[] Hazards;
        public Collider[] Colliders;
        public bool[] ColliderEnabled;
        public Transform[] Nodes;
        public Vector3[] LocalPositions, LocalScales;
        public Quaternion[] LocalRotations;
        public bool[] ChildActive;
        public bool Consumed
        {
            get
            {
                foreach (var value in Bananas) if (value != null && value.IsCollected) return true;
                foreach (var value in Coconuts) if (value != null && value.IsCollected) return true;
                foreach (var value in Bombs) if (value != null && value.IsCollected) return true;
                foreach (var value in Powers) if (value != null && value.IsCollected && !value.IsPendingRespawn) return true;
                foreach (var value in Obstacles) if (value != null && value.IsBroken) return true;
                foreach (var value in Peels) if (value != null && value.IsConsumed) return true;
                return false;
            }
        }
    }

    private struct Plan
    {
        public GameObject Prefab;
        public Vector3 Position;
        public Quaternion Rotation;
        public Transform Parent;
        public bool AlignPickup, Retired;
        public Item Instance;
        public RunnerMotor Runner;
        public RunnerCourseGenerator Course;
        public FallingCoconutHazard FallingPrefab;
        public int CoconutLane;
        public bool Decoration;
        public float Scale;
    }

    private readonly List<Plan> plans = new List<Plan>(1024);
    private readonly Dictionary<GameObject, Queue<Item>> idle = new Dictionary<GameObject, Queue<Item>>();
    private Transform storage;
    private float nextRefresh;
    public int PlannedCount => plans.Count;
    public int LiveCount { get; private set; }
    public int CreatedCount { get; private set; }
    public int ReusedCount { get; private set; }
    public int RetiredCount { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() => active = null;

    public static void Schedule(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent,
        bool alignPickup = false, RunnerCourseGenerator course = null, FallingCoconutHazard fallingPrefab = null,
        int coconutLane = 0, bool decoration = false, float scale = 1f)
    {
        if (prefab == null || parent == null) return;
        if (active == null || active.gameObject.scene != parent.gameObject.scene)
        {
            var host = new GameObject("Procedural Track Pool (Runtime)");
            SceneManager.MoveGameObjectToScene(host, parent.gameObject.scene);
            active = host.AddComponent<ProceduralTrackPool>();
            active.storage = new GameObject("Inactive Track Instances").transform;
            active.storage.SetParent(host.transform, false);
            active.storage.gameObject.SetActive(false);
        }
        active.plans.Add(new Plan { Prefab = prefab, Position = position, Rotation = rotation, Parent = parent,
            AlignPickup = alignPickup, Course = course, Runner = course != null ? course.Player : null,
            FallingPrefab = fallingPrefab, CoconutLane = coconutLane, Decoration = decoration, Scale = scale });
    }

    private void LateUpdate()
    {
        if (Time.unscaledTime < nextRefresh || !TrackDistanceStreamer.TryGetWindow(out float behind, out float ahead)) return;
        nextRefresh = Time.unscaledTime + 0.1f;
        for (int i = 0; i < plans.Count; i++)
        {
            var plan = plans[i];
            if (plan.Retired) continue;
            if (plan.Prefab == null || plan.Parent == null)
            {
                if (plan.Instance != null)
                {
                    if (plan.Instance.Object != null)
                    {
                        TrackDistanceStreamer.Unregister(plan.Instance.Object);
                        Destroy(plan.Instance.Object);
                    }
                    LiveCount--;
                }
                plan.Instance = null;
                plan.Retired = true;
                RetiredCount++;
                plans[i] = plan;
                continue;
            }
            if (plan.Instance != null)
            {
                bool missing = plan.Instance.Object == null;
                float z = missing ? plan.Position.z : plan.Instance.Object.transform.position.z;
                if (missing || plan.Instance.Consumed || z < behind)
                {
                    bool retire = missing || plan.Instance.Consumed || TrackDistanceStreamer.HaveBothRunnersPassed(plan.Position.z);
                    Return(plan.Prefab, plan.Instance);
                    plan.Instance = null;
                    plan.Retired = retire;
                    if (retire) RetiredCount++;
                    LiveCount--;
                }
                else if (z > ahead)
                {
                    Return(plan.Prefab, plan.Instance);
                    plan.Instance = null;
                    LiveCount--;
                }
            }
            // A plan already behind the camera is never materialized just to hide it.
            else if (plan.Position.z < behind)
            {
                if (TrackDistanceStreamer.HaveBothRunnersPassed(plan.Position.z)) { plan.Retired = true; RetiredCount++; }
            }
            else if (plan.Position.z <= ahead)
            {
                plan.Instance = Rent(plan);
                LiveCount++;
            }
            plans[i] = plan;
        }
    }

    private Item Rent(Plan plan)
    {
        if (!idle.TryGetValue(plan.Prefab, out var queue))
        {
            queue = new Queue<Item>();
            idle.Add(plan.Prefab, queue);
        }
        Item item = null;
        while (queue.Count > 0 && item == null)
        {
            var candidate = queue.Dequeue();
            if (candidate.Object != null) item = candidate;
        }
        bool created = item == null;
        if (created)
        {
            item = new Item { Object = Instantiate(plan.Prefab, storage) };
            item.Object.SetActive(false);
            CreatedCount++;
        }
        else ReusedCount++;
        var root = item.Object.transform;
        root.SetParent(plan.Parent, false);
        root.localScale = plan.Prefab.transform.localScale * plan.Scale;
        root.SetPositionAndRotation(plan.Position, plan.Rotation);
        if (!created)
        {
            // Shattering can disable a nested hitbox rather than the wrapper root.
            // Restore the original hierarchy while inactive before OnEnable resets motion.
            for (int i = 1; i < item.Nodes.Length; i++)
            {
                var node = item.Nodes[i];
                if (node == null) continue;
                node.localPosition = item.LocalPositions[i];
                node.localRotation = item.LocalRotations[i];
                node.localScale = item.LocalScales[i];
                node.gameObject.SetActive(item.ChildActive[i]);
            }
            for (int i = 0; i < item.Colliders.Length; i++)
                if (item.Colliders[i] != null) item.Colliders[i].enabled = item.ColliderEnabled[i];
        }
        item.Object.SetActive(true);
        if (created)
        {
            item.Sphere = root.GetComponent<SphereCollider>();
            item.Bananas = root.GetComponentsInChildren<BananaPickup>(true);
            item.Coconuts = root.GetComponentsInChildren<CoconutPickup>(true);
            item.Bombs = root.GetComponentsInChildren<BombPickup>(true);
            item.Powers = root.GetComponentsInChildren<PowerUpPickup>(true);
            item.Obstacles = root.GetComponentsInChildren<RunnerObstacle>(true);
            item.Peels = root.GetComponentsInChildren<BananaPeelHazard>(true);
            item.Hazards = root.GetComponentsInChildren<DynamicRunnerHazard>(true);
            item.Colliders = root.GetComponentsInChildren<Collider>(true);
            item.ColliderEnabled = new bool[item.Colliders.Length];
            for (int i = 0; i < item.Colliders.Length; i++) item.ColliderEnabled[i] = item.Colliders[i].enabled;
            item.Nodes = root.GetComponentsInChildren<Transform>(true);
            item.LocalPositions = new Vector3[item.Nodes.Length];
            item.LocalRotations = new Quaternion[item.Nodes.Length];
            item.LocalScales = new Vector3[item.Nodes.Length];
            item.ChildActive = new bool[item.Nodes.Length];
            for (int i = 0; i < item.Nodes.Length; i++)
            {
                item.LocalPositions[i] = item.Nodes[i].localPosition;
                item.LocalRotations[i] = item.Nodes[i].localRotation;
                item.LocalScales[i] = item.Nodes[i].localScale;
                item.ChildActive[i] = item.Nodes[i].gameObject.activeSelf;
            }
        }
        // Explicit pool resets are separate from visibility activation: collected items
        // cannot be resurrected by the streamer, only reused for a different spawn plan.
        foreach (var value in item.Bananas) value.ResetForSpawn();
        foreach (var value in item.Coconuts) value.ResetForSpawn();
        foreach (var value in item.Bombs) value.ResetForSpawn();
        foreach (var value in item.Powers) value.ResetForSpawn();
        foreach (var hazard in item.Hazards) if (plan.Course != null) hazard.Configure(plan.Runner, plan.Course);
        if (plan.AlignPickup && item.Sphere != null)
            root.position += plan.Position - root.TransformPoint(item.Sphere.center);
        if (plan.FallingPrefab != null)
        {
            var spawner = root.GetComponent<FallingCoconutSpawner>();
            if (spawner == null) spawner = item.Object.AddComponent<FallingCoconutSpawner>();
            spawner.Configure(plan.Runner, plan.FallingPrefab, plan.CoconutLane);
        }
        if (plan.Decoration || plan.FallingPrefab != null)
            foreach (var collider in item.Colliders) if (collider != null) collider.enabled = false;
        MobilePerformanceBootstrap.PrepareTrackObject(item.Object, true);
        TrackDistanceStreamer.Register(item.Object);
        return item;
    }

    private void Return(GameObject prefab, Item item)
    {
        if (item.Object == null) return;
        TrackDistanceStreamer.Unregister(item.Object);
        foreach (var obstacle in item.Obstacles)
            if (obstacle != null && obstacle.WillBeDestroyed) return; // Never rent a Destroy-pending impact victim.
        item.Object.SetActive(false);
        item.Object.transform.SetParent(storage, false);
        var queue = idle[prefab];
        if (queue.Count < MaxIdlePerPrefab) queue.Enqueue(item);
        else Destroy(item.Object);
    }

    public static void RefreshRenderSettings()
    {
        if (active == null) return;
        foreach (var plan in active.plans)
            if (plan.Instance != null && plan.Instance.Object != null)
                MobilePerformanceBootstrap.PrepareTrackObject(plan.Instance.Object, true);
    }

    private void OnDestroy() { if (active == this) active = null; }
}
