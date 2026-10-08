using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Builds a runner-owned course with distance-based difficulty and swept-volume safe lanes.</summary>
[DefaultExecutionOrder(-50)]
public class RunnerCourseGenerator : MonoBehaviour
{
    [Header("Course")]
    [SerializeField] private RunnerMotor player;
    [SerializeField] private Transform finishLine;
    [SerializeField, Min(0f)] private float startDistance = 245f;
    [SerializeField, Min(10f)] private float finishApproach = 25f;
    [SerializeField] private float groundHeight = -0.3f;
    [SerializeField] private int seed = 731;
    [SerializeField] private bool randomizeEachRun;
    [Header("Patterns")]
    [SerializeField] private GameObject[] obstaclePrefabs;
    [SerializeField] private GameObject bananaPrefab;
    [SerializeField] private GameObject coconutPrefab;
    [SerializeField] private PowerUpPickup[] powerUpPrefabs;
    [SerializeField] private FallingCoconutHazard fallingCoconutPrefab;
    [SerializeField] private BananaPeelHazard bananaPeelPrefab;
    [SerializeField] private float gapStart = 610f;
    [SerializeField] private float gapEnd = 642f;
    [SerializeField, Min(8f)] private float minimumSpacing = 12f;
    [SerializeField, Min(8f)] private float maximumSpacing = 20f;
    [Header("Difficulty (per runner)")]
    [SerializeField] private LevelProgressHUD progressSource;
    [SerializeField, Min(0f)] private float difficultyStartDistance = 90f;
    [Tooltip("Distance from this runner's start. Zero uses its full distance to the finish.")]
    [SerializeField, Min(0f)] private float maximumDifficultyDistance;
    [SerializeField, Range(1f, 1.3f)] private float obstacleFrequencyMultiplier = 1.18f;
    [SerializeField, Range(1f, 1.3f)] private float maxHazardSpeedMultiplier = 1.15f;
    [SerializeField, Range(0.65f, 1f)] private float powerUpFrequencyMultiplier = 0.85f;
    [SerializeField, Range(0f, 1f)] private float earlyDynamicChance = 0.10f;
    [SerializeField, Range(0f, 1f)] private float lateDynamicChance = 0.38f;
    [SerializeField, Min(0.8f)] private float minimumReactionSeconds = 1.1f;
    [SerializeField, Min(30f)] private float powerUpSpacing = 120f;
    [Header("Scenery")]
    [SerializeField] private GameObject[] sceneryPrefabs;
    [SerializeField] private GameObject roadsideShrubPrefab;
    [SerializeField, Min(6f)] private float scenerySpacing = 14f;

    public int SpawnedPeelCount { get; private set; }
    public int SpawnedObstacleCount { get; private set; }
    public int PatternCount { get; private set; }
    public RunnerMotor Player => player;
    public float Progress01 => progressSource != null && player != null ? progressSource.ProgressAt(player.transform.position.z) : 0f;
    public float Difficulty => player != null ? DifficultyAt(player.transform.position.z) : 0f;
    public float CurrentHazardSpeedMultiplier => Mathf.Lerp(1f, maxHazardSpeedMultiplier, Difficulty);
    public float ReactionDistance => player != null ? player.ForwardSpeed * (minimumReactionSeconds + player.LaneChangeDuration) : 0f;

    public readonly struct Section
    {
        public readonly float Z, Start, End, Difficulty;
        public readonly int SafeLane;
        public Section(float z, float start, float end, float difficulty, int lane)
        { Z = z; Start = start; End = end; Difficulty = difficulty; SafeLane = lane; }
    }
    private readonly List<Section> sections = new List<Section>();
    public IReadOnlyList<Section> Sections => sections;
    private sealed class ObstacleProfile
    {
        public GameObject prefab;
        public Bounds bounds;
        public float travel;
        public bool dynamic;
    }
    private readonly List<ObstacleProfile> palette = new List<ObstacleProfile>();
    private float playerRadius;

    public float DifficultyAt(float worldZ)
    {
        if (progressSource == null || player == null || progressSource.Player != player.transform) return 0f;
        float maximum = maximumDifficultyDistance > 0 ? maximumDifficultyDistance : progressSource.RunLength;
        return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(difficultyStartDistance,
            Mathf.Max(difficultyStartDistance + 1f, maximum), progressSource.DistanceAt(worldZ)));
    }

    private void Start()
    {
        if (player == null || finishLine == null || obstaclePrefabs == null || obstaclePrefabs.Length == 0) return;
        if (progressSource == null || progressSource.Player != player.transform)
            foreach (var progress in FindObjectsByType<LevelProgressHUD>(FindObjectsSortMode.None))
                if (progress.Player == player.transform) { progressSource = progress; break; }
        playerRadius = player.GetComponent<CharacterController>().radius + 0.2f;
        BuildPalette();
        if (palette.Count == 0) return;
        var random = new System.Random(randomizeEachRun ? Environment.TickCount : seed);
        float centerX = player.transform.position.x;
        float laneWidth = player.LaneDistance;
        float end = finishLine.position.z - finishApproach;
        float z = Mathf.Max(startDistance, player.transform.position.z + 20f);
        int lastSafeLane = 0, row = 0, powerIndex = 0;
        float previousEnd = z - ReactionDistance - 12f;
        float safeRunStart = previousEnd;
        float nextPowerUp = z + 55f;
        while (z < end)
        {
            float difficulty = DifficultyAt(z);
            int safeLane = row > 0 && random.NextDouble() < Mathf.Lerp(0.15f, 0.38f, difficulty)
                ? Mathf.Clamp(lastSafeLane + (random.Next(2) == 0 ? -1 : 1), -1, 1) : lastSafeLane;
            int firstLane = safeLane == -1 ? 0 : -1;
            int secondLane = safeLane == 1 ? 0 : 1;
            var first = ChooseObstacle(random, firstLane, safeLane, difficulty);
            var second = random.NextDouble() < Mathf.Lerp(0.22f, 0.68f, difficulty)
                ? ChooseObstacle(random, secondLane, safeLane, difficulty) : null;
            float stagger = second != null && random.NextDouble() < difficulty * 0.45f ? 2.5f : 0f;
            bool peel = bananaPeelPrefab != null && random.NextDouble() < Mathf.Lerp(0.04f, 0.14f, difficulty);
            bool droppingCoconut = fallingCoconutPrefab != null && sceneryPrefabs != null && sceneryPrefabs.Length > 0
                && sceneryPrefabs[0] != null && random.NextDouble() < Mathf.Lerp(0.04f, 0.13f, difficulty);
            float lead = Mathf.Max(8f, player.ForwardSpeed * 0.8f + 2f);
            float back = 4f, front = 5f; // Include pickup paths, not only damaging colliders.
            IncludeEnvelope(first, 0, ref back, ref front);
            IncludeEnvelope(second, stagger, ref back, ref front);
            if (peel) back = Mathf.Max(back, lead + 1.5f);
            back += playerRadius; front += playerRadius;
            // Different safe lanes may not overlap even at a hazard's maximum backwards travel.
            if (safeLane != lastSafeLane) z = Mathf.Max(z, previousEnd + ReactionDistance + back);
            else z = Mathf.Max(z, safeRunStart + back);
            if (z + front >= end) break;
            if (z + front > gapStart - 18f && z - back < gapEnd + 18f)
            {
                z = gapEnd + 20f + back;
                lastSafeLane = 0; previousEnd = safeRunStart = gapEnd + 3f;
                continue;
            }
            difficulty = DifficultyAt(z);
            SpawnObstacle(first, centerX + firstLane * laneWidth, z);
            SpawnObstacle(second, centerX + secondLane * laneWidth, z + stagger);
            if (peel)
            {
                Instantiate(bananaPeelPrefab, new Vector3(centerX + firstLane * laneWidth, groundHeight, z - lead), Quaternion.identity, transform);
                SpawnedPeelCount++;
            }
            if (droppingCoconut)
            {
                var tree = Instantiate(sceneryPrefabs[0], new Vector3(centerX - 8f, groundHeight, z - 2f), Quaternion.identity, transform);
                foreach (var collider in tree.GetComponentsInChildren<Collider>()) collider.enabled = false;
                tree.AddComponent<FallingCoconutSpawner>().Configure(player, fallingCoconutPrefab, firstLane);
            }
            if (bananaPrefab != null)
            {
                for (int b = 0; b < 3; b++) SpawnPickup(bananaPrefab, new Vector3(centerX + safeLane * laneWidth, groundHeight + 1.1f, z - 4 + b * 2.5f));
                // More frequent adjacent-lane transitions create demanding, still-safe banana trails.
                if (safeLane != lastSafeLane)
                    for (int b = 1; b <= 3; b++)
                    {
                        float t = b / 4f;
                        SpawnPickup(bananaPrefab, new Vector3(centerX + Mathf.Lerp(lastSafeLane, safeLane, t) * laneWidth,
                            groundHeight + 1.1f, Mathf.Lerp(previousEnd + 0.2f, z - back - 0.2f, t)));
                    }
            }
            if (coconutPrefab != null && row % 5 == 0)
                SpawnPickup(coconutPrefab, new Vector3(centerX + safeLane * laneWidth, groundHeight + 0.8f, z + 5));
            if (powerUpPrefabs != null && powerUpPrefabs.Length > 0 && z >= nextPowerUp)
            {
                int kind = powerIndex++ % powerUpPrefabs.Length;
                if (powerUpPrefabs[kind] != null)
                    Instantiate(powerUpPrefabs[kind], new Vector3(centerX + safeLane * laneWidth, groundHeight + 1.15f, z + 1), Quaternion.identity, transform);
                nextPowerUp = z + powerUpSpacing / Mathf.Lerp(1f, powerUpFrequencyMultiplier, difficulty);
            }
            sections.Add(new Section(z, z - back, z + front, difficulty, safeLane));
            if (safeLane != lastSafeLane) safeRunStart = z - back;
            previousEnd = Mathf.Max(previousEnd, z + front);
            lastSafeLane = safeLane;
            row++;
            z += Mathf.Max(player.ForwardSpeed * minimumReactionSeconds,
                Mathf.Lerp(minimumSpacing, maximumSpacing, (float)random.NextDouble())
                * Mathf.Lerp(1.25f, 1f / obstacleFrequencyMultiplier, difficulty));
        }
        PatternCount = row;
        BuildScenery(random, centerX);
    }

    private void BuildScenery(System.Random random, float centerX)
    {
        if (sceneryPrefabs == null || sceneryPrefabs.Length == 0) return;
        for (float z = startDistance - 10; z < finishLine.position.z + 15; z += Mathf.Max(6, scenerySpacing))
            for (int side = -1; side <= 1; side += 2)
            {
                if (z > gapStart - 2f && z < gapEnd + 2f) continue;
                var prefab = sceneryPrefabs[random.Next(sceneryPrefabs.Length)];
                if (prefab == null) continue;
                var tree = Instantiate(prefab, new Vector3(centerX + side * (7f + (float)random.NextDouble() * 5f), groundHeight, z),
                    Quaternion.Euler(0, random.Next(360), 0), transform);
                tree.transform.localScale *= Mathf.Lerp(0.8f, 1.2f, (float)random.NextDouble());
                foreach (var collider in tree.GetComponentsInChildren<Collider>()) collider.enabled = false;
                // Dropping palms are authored with obstacle rows so they cannot target the safe lane.
                var outerTree = Instantiate(prefab, new Vector3(centerX + side * (17f + (float)random.NextDouble() * 4f), groundHeight, z + 5),
                    Quaternion.Euler(0, random.Next(360), 0), transform);
                foreach (var collider in outerTree.GetComponentsInChildren<Collider>()) collider.enabled = false;
                if (roadsideShrubPrefab != null)
                {
                    var shrub = Instantiate(roadsideShrubPrefab, new Vector3(centerX + side * 6.7f, groundHeight, z + 3),
                        Quaternion.Euler(0, random.Next(360), 0), transform);
                    shrub.transform.localScale *= 0.65f;
                    foreach (var collider in shrub.GetComponentsInChildren<Collider>()) collider.enabled = false;
                }
            }
    }

    private void SpawnObstacle(ObstacleProfile profile, float x, float z)
    {
        if (profile == null) return;
        var obstacle = Instantiate(profile.prefab, new Vector3(x, groundHeight, z), Quaternion.identity, transform);
        foreach (var hazard in obstacle.GetComponentsInChildren<DynamicRunnerHazard>(true)) hazard.Configure(player, this);
        SpawnedObstacleCount++;
    }

    private static void IncludeEnvelope(ObstacleProfile profile, float offset, ref float back, ref float front)
    {
        if (profile == null) return;
        back = Mathf.Max(back, profile.travel - profile.bounds.min.z - offset);
        front = Mathf.Max(front, profile.bounds.max.z + offset);
    }

    private ObstacleProfile ChooseObstacle(System.Random random, int lane, int safeLane, float difficulty)
    {
        bool dynamic = random.NextDouble() < Mathf.Lerp(earlyDynamicChance, lateDynamicChance, difficulty);
        float safeX = safeLane * player.LaneDistance;
        for (int pass = 0; pass < 2; pass++)
        {
            ObstacleProfile selected = null; int count = 0;
            foreach (var candidate in palette)
            {
                if (candidate.dynamic != dynamic) continue;
                float left = lane * player.LaneDistance + candidate.bounds.min.x - playerRadius;
                float right = lane * player.LaneDistance + candidate.bounds.max.x + playerRadius;
                if (safeX >= left && safeX <= right) continue;
                if (random.Next(++count) == 0) selected = candidate;
            }
            if (selected != null) return selected;
            dynamic = !dynamic;
        }
        return null;
    }

    private void BuildPalette()
    {
        palette.Clear();
        foreach (var prefab in obstaclePrefabs)
        {
            if (prefab == null) continue;
            bool found = false; Bounds bounds = default;
            foreach (var collider in prefab.GetComponentsInChildren<BoxCollider>())
            {
                if (!collider.enabled) continue;
                var matrix = Matrix4x4.Scale(prefab.transform.localScale) * prefab.transform.worldToLocalMatrix * collider.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 sign = new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1);
                    Vector3 point = matrix.MultiplyPoint3x4(collider.center + Vector3.Scale(collider.size * 0.5f, sign));
                    if (!found) { bounds = new Bounds(point, Vector3.zero); found = true; } else bounds.Encapsulate(point);
                }
            }
            if (!found) continue;
            var profile = new ObstacleProfile { prefab = prefab, bounds = bounds };
            foreach (var hazard in prefab.GetComponentsInChildren<DynamicRunnerHazard>())
            { profile.dynamic = true; profile.travel = Mathf.Max(profile.travel, hazard.TravelLimit); }
            palette.Add(profile);
        }
    }

    private void SpawnPickup(GameObject prefab, Vector3 visibleCenter)
    {
        var pickup = Instantiate(prefab, visibleCenter, prefab.transform.rotation, transform);
        var sphere = pickup.GetComponent<SphereCollider>();
        if (sphere != null) pickup.transform.position += visibleCenter - pickup.transform.TransformPoint(sphere.center);
    }

    private void OnValidate()
    {
        minimumSpacing = Mathf.Max(8f, minimumSpacing);
        maximumSpacing = Mathf.Max(minimumSpacing, maximumSpacing);
        obstacleFrequencyMultiplier = Mathf.Clamp(obstacleFrequencyMultiplier, 1f, 1.3f);
        maxHazardSpeedMultiplier = Mathf.Clamp(maxHazardSpeedMultiplier, 1f, 1.3f);
        powerUpFrequencyMultiplier = Mathf.Clamp(powerUpFrequencyMultiplier, 0.65f, 1f);
        minimumReactionSeconds = Mathf.Max(0.8f, minimumReactionSeconds);
        lateDynamicChance = Mathf.Max(earlyDynamicChance, lateDynamicChance);
    }
}
