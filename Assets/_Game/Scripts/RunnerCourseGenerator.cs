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
    [SerializeField, Min(0f)] private float startDistance = 0f;
    [SerializeField, Min(10f)] private float finishApproach = 25f;
    [SerializeField] private float groundHeight = -0.3f;
    [SerializeField] private int seed = 731;
    [SerializeField] private bool randomizeEachRun;
    [Header("Patterns")]
    [SerializeField] private GameObject[] obstaclePrefabs;
    [SerializeField] private GameObject bananaPrefab;
    [SerializeField] private GameObject coconutPrefab;
    [SerializeField] private GameObject bombPickupPrefab;
    [SerializeField] private PowerUpPickup[] powerUpPrefabs;
    [SerializeField] private FallingCoconutHazard fallingCoconutPrefab;
    [SerializeField] private BananaPeelHazard bananaPeelPrefab;
    [SerializeField] private float gapStart = 610f;
    [SerializeField] private float gapEnd = 642f;
    [Tooltip("Lower bound of relative row-spacing jitter, normalized around the average of this pair. Not an absolute minimum gap.")]
    [SerializeField, Min(8f)] private float minimumSpacing = 10f;
    [Tooltip("Upper bound of relative row-spacing jitter, normalized around the average of this pair. Not an absolute maximum gap.")]
    [SerializeField, Min(8f)] private float maximumSpacing = 16f;
    [Header("Progressive patterns")]
    [SerializeField, Min(8f)] private float easyObstacleSpacing = 15f;
    [SerializeField, Min(8f)] private float hardObstacleSpacing = 9.5f;
    [Tooltip("Forward offset between paired obstacles in a staggered row.")]
    [SerializeField, Min(0f)] private float staggerDistance = 3.2f;
    [SerializeField, Range(0f, 1f)] private float easyDoubleObstacleChance = 0.62f;
    [SerializeField, Range(0f, 1f)] private float hardDoubleObstacleChance = 0.98f;
    [SerializeField, Range(0f, 1f)] private float easyLaneChangeChance = 0.42f;
    [SerializeField, Range(0f, 1f)] private float hardLaneChangeChance = 0.75f;
    [SerializeField, Range(0f, 1f)] private float easyStaggerChance = 0.3f;
    [SerializeField, Range(0f, 1f)] private float hardStaggerChance = 0.85f;
    [Header("Ability banana paths")]
    [Tooltip("Chance of an elevated reward arc after a green glider pickup.")]
    [SerializeField, Range(0f, 1f)] private float highBananaFrequency = 0.75f;
    [Tooltip("Apex above the ground, clamped to the existing high jump's reach.")]
    [SerializeField, Min(1f)] private float highBananaHeight = 4.7f;
    [SerializeField, Range(5, 13)] private int highBananaCount = 9;
    [SerializeField, Range(0, 24)] private int ropeBananaCount = 12;
    [Header("Difficulty (per runner)")]
    [SerializeField] private LevelProgressHUD progressSource;
    [SerializeField, Min(0f)] private float difficultyStartDistance = 90f;
    [Tooltip("Distance from this runner's start. Zero uses its full distance to the finish.")]
    [SerializeField, Min(0f)] private float maximumDifficultyDistance;
    [SerializeField, Range(1f, 1.6f)] private float obstacleFrequencyMultiplier = 1.45f;
    [SerializeField, Range(1f, 1.5f)] private float maxHazardSpeedMultiplier = 1.35f;
    [SerializeField, Range(0.65f, 1f)] private float powerUpFrequencyMultiplier = 0.85f;
    [SerializeField, Range(0f, 1f)] private float earlyDynamicChance = 0.3f;
    [SerializeField, Range(0f, 1f)] private float lateDynamicChance = 0.78f;
    [SerializeField, Min(0.8f)] private float minimumReactionSeconds = 0.8f;
    [SerializeField, Min(30f)] private float powerUpSpacing = 120f;
    [Header("Scenery")]
    [SerializeField] private GameObject[] sceneryPrefabs;
    [SerializeField] private GameObject roadsideShrubPrefab;
    [SerializeField, Min(6f)] private float scenerySpacing = 14f;

    public int SpawnedPeelCount { get; private set; }
    public int SpawnedObstacleCount { get; private set; }
    public int PatternCount { get; private set; }
    public int HighBananaPathCount { get; private set; }
    public GameObject BananaPrefab => bananaPrefab;
    public GameObject[] SceneryPrefabs => sceneryPrefabs;
    public GameObject RoadsideShrubPrefab => roadsideShrubPrefab;
    public float GroundHeight => groundHeight;
    public int RopeBananaCount => ropeBananaCount;
    public RunnerMotor Player => player;
    public float FinishZ => finishLine != null ? finishLine.position.z : transform.position.z;
    public float Progress01 => progressSource != null && player != null ? progressSource.ProgressAt(player.transform.position.z) : 0f;
    public float Difficulty => player != null ? DifficultyAt(player.transform.position.z) : 0f;
    public float CurrentHazardSpeedMultiplier => Mathf.Lerp(1f, maxHazardSpeedMultiplier, Difficulty);
    // Reserve against the cap, including future speed increases through the whole transition.
    public float ReactionDistance => player != null ? player.MaximumForwardSpeed * (minimumReactionSeconds + player.LaneChangeDuration) : 0f;

    /// <summary>
    /// Runtime water spans (bridges) are registered before Start so the course
    /// never places an obstacle or scenery tree over open water.
    /// </summary>
    public void RegisterWaterZone(float startZ, float endZ)
    {
        float start = Mathf.Min(startZ, endZ);
        float end = Mathf.Max(startZ, endZ);
        if (end <= start) return;
        waterZones.Add(new Vector2(start, end));
    }

    public bool IsInsideWaterZone(float worldZ, float margin = 0f)
    {
        for (int i = 0; i < waterZones.Count; i++)
        {
            Vector2 zone = waterZones[i];
            if (worldZ >= zone.x - margin && worldZ <= zone.y + margin) return true;
        }
        return false;
    }

    private bool TryGetWaterZone(float startZ, float endZ, out float zoneEnd)
    {
        zoneEnd = 0f;
        bool found = false;
        for (int i = 0; i < waterZones.Count; i++)
        {
            Vector2 zone = waterZones[i];
            if (endZ < zone.x || startZ > zone.y) continue;
            zoneEnd = found ? Mathf.Max(zoneEnd, zone.y) : zone.y;
            found = true;
        }
        return found;
    }

    public readonly struct Section
    {
        public readonly float Z, Start, End, Difficulty;
        public readonly int SafeLane;
        public Section(float z, float start, float end, float difficulty, int lane)
        { Z = z; Start = start; End = end; Difficulty = difficulty; SafeLane = lane; }
    }
    private readonly List<Section> sections = new List<Section>();
    private readonly List<Vector2> waterZones = new List<Vector2>();
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
    private bool uniformCourse;
    private bool progressLookupComplete;
    private bool warnedMissingProgress;

    public float DifficultyAt(float worldZ)
    {
        if (progressSource == null || player == null || progressSource.Player != player.transform)
        {
            if (progressLookupComplete && !warnedMissingProgress)
            {
                Debug.LogWarning("RunnerCourseGenerator has no matching LevelProgressHUD; difficulty remains at baseline while course generation continues.", this);
                warnedMissingProgress = true;
            }
            return 0f;
        }
        float maximum = maximumDifficultyDistance > 0 ? maximumDifficultyDistance : progressSource.RunLength;
        return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(difficultyStartDistance,
            Mathf.Max(difficultyStartDistance + 1f, maximum), progressSource.DistanceAt(worldZ)));
    }

    private void Start()
    {
        uniformCourse = gameObject.scene.name == "Level01";
        if (uniformCourse)
        {
            ProceduralTrackStartup.Prepare(gameObject.scene);
            startDistance = 0f; // Override the scene's old 245m boundary without editing YAML.
        }
        if (player == null)
        {
            Debug.LogWarning("RunnerCourseGenerator cannot build a course: player reference is missing.", this);
            return;
        }
        if (finishLine == null)
        {
            Debug.LogWarning("RunnerCourseGenerator cannot build a course: finish line reference is missing.", this);
            return;
        }
        if (obstaclePrefabs == null || obstaclePrefabs.Length == 0)
        {
            Debug.LogWarning("RunnerCourseGenerator cannot build a course: obstacle prefab list is empty.", this);
            return;
        }
        if (progressSource == null || progressSource.Player != player.transform)
            foreach (var progress in FindObjectsByType<LevelProgressHUD>(FindObjectsSortMode.None))
                if (progress.Player == player.transform) { progressSource = progress; break; }
        progressLookupComplete = true;
        if (progressSource == null || progressSource.Player != player.transform) DifficultyAt(player.transform.position.z);
        player.ConfigureDifficulty(this);
        var controller = player.GetComponent<CharacterController>();
        if (controller == null)
        {
            Debug.LogWarning("RunnerCourseGenerator cannot build a course: player has no CharacterController.", this);
            return;
        }
        playerRadius = controller.radius + 0.2f;
        BuildPalette();
        EnsureRuntimePowerUpPalette();
        if (palette.Count == 0)
        {
            Debug.LogWarning("RunnerCourseGenerator cannot build a course: obstacle prefabs have no enabled BoxCollider profiles.", this);
            return;
        }
        var random = new System.Random(randomizeEachRun ? Environment.TickCount : seed);
        float centerX = player.CenterX;
        float laneWidth = player.LaneDistance;
        float end = finishLine.position.z - finishApproach;
        float z = uniformCourse ? Mathf.Max(30f, player.transform.position.z + 25f) :
            Mathf.Max(startDistance, player.transform.position.z + 20f);
        if (z >= end)
        {
            Debug.LogWarning("RunnerCourseGenerator cannot build a course: the finish approach starts before the first obstacle row.", this);
            return;
        }
        int lastSafeLane = 0, row = 0, powerIndex = 0;
        float previousEnd = z - ReactionDistance - 12f;
        float safeRunStart = previousEnd;
        float nextPowerUp = uniformCourse ? ProceduralTrackStartup.AuthoredOpeningEnd : z + 55f;
        float lastPowerUpZ = float.NegativeInfinity;
        bool thunderSpawned = false;
        while (z < end)
        {
            float difficulty = DifficultyAt(z);
            int safeLane = row > 0 && random.NextDouble() < Mathf.Lerp(easyLaneChangeChance, hardLaneChangeChance, difficulty)
                ? (lastSafeLane == 0 ? (random.Next(2) == 0 ? -1 : 1) : 0) : lastSafeLane;
            int firstLane = safeLane == -1 ? 0 : -1;
            int secondLane = safeLane == 1 ? 0 : 1;
            var first = ChooseObstacle(random, firstLane, safeLane, difficulty);
            var second = random.NextDouble() < Mathf.Lerp(easyDoubleObstacleChance, hardDoubleObstacleChance, difficulty)
                ? ChooseObstacle(random, secondLane, safeLane, difficulty, first) : null;
            float stagger = second != null && random.NextDouble() < Mathf.Lerp(easyStaggerChance, hardStaggerChance, difficulty) ? staggerDistance : 0f;
            bool peel = bananaPeelPrefab != null && random.NextDouble() < Mathf.Lerp(0.04f, 0.14f, difficulty);
            bool droppingCoconut = fallingCoconutPrefab != null && sceneryPrefabs != null && sceneryPrefabs.Length > 0
                && sceneryPrefabs[0] != null && random.NextDouble() < Mathf.Lerp(0.04f, 0.13f, difficulty);
            float lead = Mathf.Max(8f, player.MaximumForwardSpeed * 0.8f + 2f);
            float back = 4f, front = 5f; // Include pickup paths, not only damaging colliders.
            IncludeEnvelope(first, 0, ref back, ref front);
            IncludeEnvelope(second, stagger, ref back, ref front);
            if (peel) back = Mathf.Max(back, lead + 1.5f);
            back += playerRadius; front += playerRadius;
            if (uniformCourse) z = Mathf.Max(z, ProceduralTrackStartup.ClearRunwayEnd + back);
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
            if (TryGetWaterZone(z - back, z + front, out float waterEnd))
            {
                z = waterEnd + 18f + back;
                lastSafeLane = 0; previousEnd = safeRunStart = waterEnd + 3f;
                continue;
            }
            difficulty = DifficultyAt(z);
            if (uniformCourse && row == 0 && bananaPrefab != null)
                for (float pickupZ = ProceduralTrackStartup.ClearRunwayEnd; pickupZ < z - 5.1f; pickupZ += 2.6f)
                    SpawnPickup(bananaPrefab, new Vector3(centerX, groundHeight + 1.1f, pickupZ));
            SpawnObstacle(first, centerX + firstLane * laneWidth, z);
            SpawnObstacle(second, centerX + secondLane * laneWidth, z + stagger);
            if (peel)
            {
                Vector3 point = new Vector3(centerX + firstLane * laneWidth, groundHeight, z - lead);
                if (uniformCourse) ProceduralTrackPool.Schedule(bananaPeelPrefab.gameObject, point, Quaternion.identity, transform);
                else Instantiate(bananaPeelPrefab, point, Quaternion.identity, transform);
                SpawnedPeelCount++;
            }
            if (droppingCoconut)
            {
                Vector3 point = new Vector3(centerX - 8f, groundHeight, z - 2f);
                if (uniformCourse)
                    ProceduralTrackPool.Schedule(sceneryPrefabs[0], point, Quaternion.identity, transform,
                        course: this, fallingPrefab: fallingCoconutPrefab, coconutLane: firstLane, decoration: true);
                else
                {
                    var tree = Instantiate(sceneryPrefabs[0], point, Quaternion.identity, transform);
                    foreach (var collider in tree.GetComponentsInChildren<Collider>()) collider.enabled = false;
                    tree.AddComponent<FallingCoconutSpawner>().Configure(player, fallingCoconutPrefab, firstLane);
                }
            }
            if (bananaPrefab != null)
            {
                for (int b = 0; b < 4; b++) SpawnPickup(bananaPrefab, new Vector3(centerX + safeLane * laneWidth, groundHeight + 1.1f, z - 5 + b * 2.6f));
                // More frequent adjacent-lane transitions create demanding, still-safe banana trails.
                if (safeLane != lastSafeLane)
                    for (int b = 1; b <= 4; b++)
                    {
                        float t = b / 5f;
                        SpawnPickup(bananaPrefab, new Vector3(centerX + Mathf.Lerp(lastSafeLane, safeLane, t) * laneWidth,
                            groundHeight + 1.1f, Mathf.Lerp(previousEnd + 0.2f, z - back - 0.2f, t)));
                    }
            }
            // Paired pickups sit just past the obstacle row, where both chosen
            // lanes are clear, so neither runner can monopolise the rewards.
            float pairZ = z + front + 1f;
            if (row % 3 == 0)
            {
                bool coconutLeft = row % 2 == 0;
                if (coconutPrefab != null)
                    SpawnPickup(coconutPrefab, new Vector3(centerX + (coconutLeft ? -1 : 1) * laneWidth,
                        groundHeight + 0.8f, pairZ));
                if (bombPickupPrefab != null)
                    SpawnPickup(bombPickupPrefab, new Vector3(centerX + (coconutLeft ? 1 : -1) * laneWidth,
                        groundHeight + 0.8f, pairZ));
            }
            if (powerUpPrefabs != null && powerUpPrefabs.Length > 0 && z >= nextPowerUp)
            {
                int kind = powerIndex++ % powerUpPrefabs.Length;
                var primary = powerUpPrefabs[kind];
                if (primary != null && TrySpawnPowerUpPair(primary.Type, centerX, laneWidth, safeLane, pairZ, row))
                {
                    lastPowerUpZ = z;
                    PowerUpType partnerType = PartnerFor(primary.Type);
                    if (primary.Type == PowerUpType.Thunder || partnerType == PowerUpType.Thunder)
                        thunderSpawned = true;
                    if (primary.Type == PowerUpType.Glider && bananaPrefab != null && random.NextDouble() < highBananaFrequency)
                    {
                        float arcStart = pairZ + player.MaximumForwardSpeed * 0.8f;
                        float arcLength = player.SpeedAtDifficulty(difficulty) * player.HighJumpDuration;
                        float arcEnd = arcStart + arcLength;
                        if (arcEnd + ReactionDistance < end && (arcEnd < gapStart - 18f || arcStart > gapEnd + 18f) &&
                            !IsInsideWaterZone(arcStart - 2f) && !IsInsideWaterZone(arcEnd + 2f))
                        {
                            SpawnHighBananaArc(centerX + safeLane * laneWidth, arcStart, arcLength);
                            // Keep this safe lane open through the complete jump and landing.
                            front = Mathf.Max(front, arcEnd - z + playerRadius);
                        }
                    }
                }
                nextPowerUp = z + (uniformCourse && z < ProceduralTrackStartup.AuthoredOpeningEnd ? 40f :
                    powerUpSpacing / Mathf.Lerp(1f, powerUpFrequencyMultiplier, difficulty));
            }
            sections.Add(new Section(z, z - back, z + front, difficulty, safeLane));
            if (safeLane != lastSafeLane) safeRunStart = z - back;
            previousEnd = Mathf.Max(previousEnd, z + front);
            lastSafeLane = safeLane;
            row++;
            float jitter = Mathf.Lerp(minimumSpacing, maximumSpacing, (float)random.NextDouble())
                / Mathf.Max(1f, (minimumSpacing + maximumSpacing) * 0.5f);
            z += Mathf.Max(player.MaximumForwardSpeed * minimumReactionSeconds,
                Mathf.Lerp(easyObstacleSpacing, hardObstacleSpacing, difficulty) * jitter
                / Mathf.Lerp(1f, obstacleFrequencyMultiplier, difficulty));
        }
        if (uniformCourse) thunderSpawned |= ScheduleOpeningPowerUps();
        // Guarantee that the Speed Boost pickup exists even if the cadence or a
        // river skip consumed its slot. A safe generated row is always preferred;
        // reusing the final safe row is a last-resort fallback.
        if (!thunderSpawned && powerUpPrefabs != null)
        {
            bool hasThunder = false;
            for (int i = 0; i < powerUpPrefabs.Length; i++)
                if (powerUpPrefabs[i] != null && powerUpPrefabs[i].Type == PowerUpType.Thunder)
                {
                    hasThunder = true;
                    break;
                }

            if (hasThunder)
            {
                bool found = false;
                Section target = default;
                for (int i = sections.Count - 1; i >= 0; i--)
                {
                    Section candidate = sections[i];
                    if (candidate.Z >= end || candidate.Z <= lastPowerUpZ + 18f) continue;
                    target = candidate;
                    found = true;
                    break;
                }
                if (!found)
                    for (int i = sections.Count - 1; i >= 0; i--)
                    {
                        Section candidate = sections[i];
                        if (candidate.Z >= end) continue;
                        target = candidate;
                        found = true;
                        break;
                    }

                if (found)
                {
                    float pairZ = Mathf.Min(target.End + 1f, end - 1f);
                    TrySpawnPowerUpPair(PowerUpType.Thunder, centerX, laneWidth,
                        target.SafeLane, pairZ, sections.Count);
                }
            }
        }
        PatternCount = row;
        if (row == 0)
            Debug.LogWarning("RunnerCourseGenerator produced no rows: the available space before the finish is too short for the configured safety bounds.", this);
        BuildScenery(random, centerX);
    }

    private void BuildScenery(System.Random random, float centerX)
    {
        if (uniformCourse)
            foreach (var scenery in FindObjectsByType<TropicalScenery>())
                if (scenery.gameObject.scene == gameObject.scene && scenery.HasBorderPrefabs) return;
        if (sceneryPrefabs == null || sceneryPrefabs.Length == 0) return;
        for (float z = uniformCourse ? 0f : startDistance - 10; z < finishLine.position.z + 15; z += Mathf.Max(6, scenerySpacing))
            for (int side = -1; side <= 1; side += 2)
            {
                if (z > gapStart - 2f && z < gapEnd + 2f) continue;
                if (IsInsideWaterZone(z, 5f)) continue;
                var prefab = sceneryPrefabs[random.Next(sceneryPrefabs.Length)];
                if (prefab == null) continue;
                SpawnDecoration(prefab, new Vector3(centerX + side * (7f + (float)random.NextDouble() * 5f), groundHeight, z),
                    Quaternion.Euler(0, random.Next(360), 0), Mathf.Lerp(0.8f, 1.2f, (float)random.NextDouble()));
                SpawnDecoration(prefab, new Vector3(centerX + side * (17f + (float)random.NextDouble() * 4f), groundHeight, z + 5),
                    Quaternion.Euler(0, random.Next(360), 0), 1f);
                if (roadsideShrubPrefab != null)
                    SpawnDecoration(roadsideShrubPrefab, new Vector3(centerX + side * 6.7f, groundHeight, z + 3),
                        Quaternion.Euler(0, random.Next(360), 0), 0.65f);
            }
    }

    private void SpawnHighBananaArc(float x, float start, float length)
    {
        var path = new GameObject("High Jump Banana Arc").transform;
        path.SetParent(transform, false);
        float apex = Mathf.Clamp(highBananaHeight, player.NormalJumpHeight + 1.4f, player.HighJumpHeight + 0.9f);
        for (int i = 0; i < highBananaCount; i++)
        {
            float t = (i + 1f) / (highBananaCount + 1f);
            SpawnPickup(bananaPrefab, new Vector3(x, groundHeight + 1f + (apex - 1f) * 4f * t * (1f - t), start + length * t), path);
        }
        HighBananaPathCount++;
    }

    private void SpawnObstacle(ObstacleProfile profile, float x, float z)
    {
        if (profile == null) return;
        if (uniformCourse)
        {
            ProceduralTrackPool.Schedule(profile.prefab, new Vector3(x, groundHeight, z), Quaternion.identity, transform, course: this);
            SpawnedObstacleCount++;
            return;
        }
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

    private ObstacleProfile ChooseObstacle(System.Random random, int lane, int safeLane, float difficulty, ObstacleProfile paired = null)
    {
        bool dynamic = random.NextDouble() < Mathf.Lerp(earlyDynamicChance, lateDynamicChance, difficulty);
        float centerX = player.CenterX;
        float safeX = centerX + safeLane * player.LaneDistance;
        for (int pass = 0; pass < 2; pass++)
        {
            ObstacleProfile selected = null; int count = 0;
            foreach (var candidate in palette)
            {
                if (candidate.dynamic != dynamic || candidate == paired) continue;
                float left = centerX + lane * player.LaneDistance + candidate.bounds.min.x - playerRadius;
                float right = centerX + lane * player.LaneDistance + candidate.bounds.max.x + playerRadius;
                if (safeX >= left && safeX <= right) continue;
                if (random.Next(++count) == 0) selected = candidate;
            }
            if (selected != null) return selected;
            dynamic = !dynamic;
        }
        // A one-prefab palette must still support paired rows.
        return paired != null ? ChooseObstacle(random, lane, safeLane, difficulty) : null;
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

    private void EnsureRuntimePowerUpPalette()
    {
        var runtime = new List<PowerUpPickup>();
        bool hasShield = false, hasThunder = false, hasMagnet = false;
        ParticleSystem effect = null;
        if (powerUpPrefabs != null)
        {
            foreach (var pickup in powerUpPrefabs)
            {
                if (pickup == null) continue;
                runtime.Add(pickup);
                if (pickup.Type == PowerUpType.Shield) hasShield = true;
                if (pickup.Type == PowerUpType.Thunder) hasThunder = true;
                if (pickup.Type == PowerUpType.Magnet) hasMagnet = true;
                if (effect == null && pickup.CollectionEffect != null) effect = pickup.CollectionEffect;
            }
        }

        // Level01's serialized array predates these two abilities. Expand it at
        // runtime instead of touching the scene asset.
        if (!hasShield) runtime.Add(PowerUpPickup.CreateTemplate(PowerUpType.Shield, effect));
        if (!hasThunder) runtime.Add(PowerUpPickup.CreateTemplate(PowerUpType.Thunder, effect));
        if (uniformCourse && !hasMagnet) runtime.Add(PowerUpPickup.CreateTemplate(PowerUpType.Magnet, effect));
        if (uniformCourse) runtime.Sort((a, b) => OpeningPowerUpOrder(a.Type).CompareTo(OpeningPowerUpOrder(b.Type)));
        powerUpPrefabs = runtime.ToArray();

        // Fit the expanded palette inside the existing course. The original
        // 120 m cadence leaves the newly appended Thunder pickup beyond the
        // finish line, so tighten it with a small safety margin for the river
        // skip and all five abilities to appear.
        if (player != null && finishLine != null && runtime.Count > 1)
        {
            float first = Mathf.Max(startDistance, player.transform.position.z + 20f) + 55f;
            float last = finishLine.position.z - finishApproach - 8f;
            float fit = (last - first) / (runtime.Count - 1) * 0.75f;
            // Keep the pickups lively on the lengthened course instead of leaving
            // several hundred metres between abilities.
            if (fit > 0f) powerUpSpacing = Mathf.Min(powerUpSpacing, Mathf.Max(60f, fit), 85f);
        }
    }

    private static int OpeningPowerUpOrder(PowerUpType type) => type == PowerUpType.Shield ? 0 :
        type == PowerUpType.Thunder ? 1 : type == PowerUpType.Magnet ? 2 : type == PowerUpType.Bulldozer ? 3 : 4;

    private bool ScheduleOpeningPowerUps()
    {
        bool thunder = false;
        float previous = float.NegativeInfinity;
        var types = new[] { PowerUpType.Shield, PowerUpType.Thunder, PowerUpType.Magnet };
        var targets = new[] { 52f, 105f, 170f };
        for (int i = 0; i < types.Length; i++)
        {
            int selected = -1;
            float best = float.PositiveInfinity;
            for (int j = 0; j < sections.Count; j++)
            {
                var section = sections[j];
                if (section.Z <= previous + 20f || section.Z + 1f >= ProceduralTrackStartup.AuthoredOpeningEnd) continue;
                float distance = Mathf.Abs(section.Z - targets[i]);
                if (distance < best) { selected = j; best = distance; }
            }
            if (selected < 0) continue;
            var target = sections[selected];
            float pairZ = Mathf.Min(target.End + 1f, ProceduralTrackStartup.AuthoredOpeningEnd - 2f);
            if (TrySpawnPowerUpPair(types[i], player.CenterX, player.LaneDistance,
                target.SafeLane, pairZ, selected))
            {
                previous = target.Z;
                if (types[i] == PowerUpType.Thunder || PartnerFor(types[i]) == PowerUpType.Thunder)
                    thunder = true;
            }
        }
        return thunder;
    }

    /// <summary>
    /// Fixed complement pairs so a single pickup line never hands every ability
    /// to whichever runner happens to be leading.
    /// </summary>
    private static PowerUpType PartnerFor(PowerUpType type)
    {
        switch (type)
        {
            case PowerUpType.Shield: return PowerUpType.Thunder;
            case PowerUpType.Thunder: return PowerUpType.Magnet;
            case PowerUpType.Magnet: return PowerUpType.Shield;
            case PowerUpType.Bulldozer: return PowerUpType.Thunder;
            case PowerUpType.Glider: return PowerUpType.Shield;
            default: return PowerUpType.Shield;
        }
    }

    private PowerUpPickup FindPowerUpPrefab(PowerUpType type)
    {
        if (powerUpPrefabs == null) return null;
        foreach (var prefab in powerUpPrefabs)
            if (prefab != null && prefab.Type == type) return prefab;
        return null;
    }

    /// <summary>Spawns the primary ability and its partner in two different lanes at one Z.</summary>
    private bool TrySpawnPowerUpPair(PowerUpType primaryType, float centerX, float laneWidth,
        int safeLane, float z, int parity)
    {
        var primary = FindPowerUpPrefab(primaryType);
        if (primary == null) return false;
        SpawnPowerUp(primary, new Vector3(centerX + safeLane * laneWidth, groundHeight + 1.15f, z));

        int partnerLane = safeLane == 0 ? (parity % 2 == 0 ? -1 : 1) : 0;
        if (partnerLane != safeLane)
        {
            var partner = FindPowerUpPrefab(PartnerFor(primaryType));
            if (partner != null)
                SpawnPowerUp(partner, new Vector3(centerX + partnerLane * laneWidth,
                    groundHeight + 1.15f, z));
        }
        return true;
    }

    private void SpawnPowerUp(PowerUpPickup prefab, Vector3 position)
    {
        if (uniformCourse) ProceduralTrackPool.Schedule(prefab.gameObject, position, Quaternion.identity, transform);
        else
        {
            var pickup = Instantiate(prefab, position, Quaternion.identity, transform);
            if (!pickup.gameObject.activeSelf) pickup.gameObject.SetActive(true);
        }
    }

    private void SpawnDecoration(GameObject prefab, Vector3 position, Quaternion rotation, float scale)
    {
        if (uniformCourse)
            ProceduralTrackPool.Schedule(prefab, position, rotation, transform, decoration: true, scale: scale);
        else
        {
            var instance = Instantiate(prefab, position, rotation, transform);
            instance.transform.localScale *= scale;
            foreach (var collider in instance.GetComponentsInChildren<Collider>()) collider.enabled = false;
        }
    }

    private void SpawnPickup(GameObject prefab, Vector3 visibleCenter, Transform parent = null)
    {
        if (parent == null) parent = transform;
        if (uniformCourse)
            ProceduralTrackPool.Schedule(prefab, visibleCenter, prefab.transform.rotation, parent, true);
        else
        {
            var pickup = Instantiate(prefab, visibleCenter, prefab.transform.rotation, parent);
            var sphere = pickup.GetComponent<SphereCollider>();
            if (sphere != null) pickup.transform.position += visibleCenter - pickup.transform.TransformPoint(sphere.center);
        }
    }

    private void OnValidate()
    {
        minimumSpacing = Mathf.Max(8f, minimumSpacing);
        maximumSpacing = Mathf.Max(minimumSpacing, maximumSpacing);
        obstacleFrequencyMultiplier = Mathf.Clamp(obstacleFrequencyMultiplier, 1f, 1.6f);
        maxHazardSpeedMultiplier = Mathf.Clamp(maxHazardSpeedMultiplier, 1f, 1.5f);
        powerUpFrequencyMultiplier = Mathf.Clamp(powerUpFrequencyMultiplier, 0.65f, 1f);
        minimumReactionSeconds = Mathf.Max(0.8f, minimumReactionSeconds);
        lateDynamicChance = Mathf.Max(earlyDynamicChance, lateDynamicChance);
        easyObstacleSpacing = Mathf.Max(8f, easyObstacleSpacing);
        hardObstacleSpacing = Mathf.Clamp(hardObstacleSpacing, 8f, easyObstacleSpacing);
        staggerDistance = Mathf.Max(0f, staggerDistance);
        hardDoubleObstacleChance = Mathf.Max(easyDoubleObstacleChance, hardDoubleObstacleChance);
        hardLaneChangeChance = Mathf.Max(easyLaneChangeChance, hardLaneChangeChance);
        hardStaggerChance = Mathf.Max(easyStaggerChance, hardStaggerChance);
    }
}
