using System;
using UnityEngine;

/// <summary>
/// Builds the extended course once per run with reproducible, lane-safe patterns.
/// </summary>
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

    public int SpawnedPeelCount { get; private set; }

    [SerializeField] private float gapStart = 610f;
    [SerializeField] private float gapEnd = 642f;
    [SerializeField, Min(8f)] private float minimumSpacing = 12f;
    [SerializeField, Min(8f)] private float maximumSpacing = 20f;

    [Header("Difficulty")]
    [SerializeField, Min(1f)]
    private float maxHazardSpeedMultiplier = 1.30f;

    [Header("Scenery")]
    [SerializeField] private GameObject[] sceneryPrefabs;
    [SerializeField] private GameObject roadsideShrubPrefab;
    [SerializeField, Min(6f)] private float scenerySpacing = 14f;

    public int SpawnedObstacleCount { get; private set; }
    public int PatternCount { get; private set; }

    private float runStartZ;

    // Used by DynamicRunnerHazard
    public RunnerMotor Player => player;

    // Player progress from 0 to 1.
    public float Progress01
    {
        get
        {
            if (player == null || finishLine == null)
                return 0f;

            float totalDistance =
                finishLine.position.z - runStartZ;

            if (totalDistance <= 0.01f)
                return 0f;

            float travelled =
                player.transform.position.z - runStartZ;

            return Mathf.Clamp01(
                travelled / totalDistance
            );
        }
    }

    // Dynamic hazards gradually get faster as the player progresses.
    public float CurrentHazardSpeedMultiplier =>
        Mathf.Lerp(
            1f,
            maxHazardSpeedMultiplier,
            Progress01
        );

    private void Start()
    {
        if (player == null ||
            finishLine == null ||
            obstaclePrefabs == null ||
            obstaclePrefabs.Length == 0)
        {
            return;
        }

        runStartZ = player.transform.position.z;

        var random = new System.Random(
            randomizeEachRun
                ? Environment.TickCount
                : seed
        );

        float centerX =
            player.transform.position.x;

        float laneWidth =
            player.LaneDistance;

        float end =
            finishLine.position.z -
            finishApproach;

        float z =
            Mathf.Max(
                startDistance,
                player.transform.position.z + 20f
            );

        int lastSafeLane = 0;
        int row = 0;

        while (z < end)
        {
            if (z > gapStart - 18f &&
                z < gapEnd + 18f)
            {
                z = gapEnd + 20f;
                continue;
            }

            // A safe lane moves at most one lane per row.
            // Every row has a clear route.
            int safeLane =
                Mathf.Clamp(
                    lastSafeLane +
                    random.Next(-1, 2),
                    -1,
                    1
                );

            int pattern = row % 4;

            int firstLane =
                safeLane == -1
                    ? 0
                    : -1;

            int secondLane =
                safeLane == 1
                    ? 0
                    : 1;

            // Put peels before occasional rows,
            // outside the clear lane.
            if (bananaPeelPrefab != null &&
                row % 4 == 2)
            {
                float lead =
                    Mathf.Max(
                        8f,
                        player.ForwardSpeed *
                        0.8f +
                        2f
                    );

                float peelZ =
                    z - lead;

                if (peelZ > gapEnd + 3f ||
                    peelZ < gapStart - 3f)
                {
                    Instantiate(
                        bananaPeelPrefab,
                        new Vector3(
                            centerX +
                            firstLane *
                            laneWidth,
                            groundHeight,
                            peelZ
                        ),
                        Quaternion.identity,
                        transform
                    );

                    SpawnedPeelCount++;
                }
            }

            SpawnObstacle(
                random,
                centerX +
                firstLane *
                laneWidth,
                z,
                row
            );

            if (pattern == 1 ||
                pattern == 3)
            {
                SpawnObstacle(
                    random,
                    centerX +
                    secondLane *
                    laneWidth,
                    z +
                    (
                        pattern == 3
                            ? 2.5f
                            : 0f
                    ),
                    row + 2
                );
            }

            if (bananaPrefab != null)
            {
                for (int b = 0; b < 3; b++)
                {
                    SpawnPickup(
                        bananaPrefab,
                        new Vector3(
                            centerX +
                            safeLane *
                            laneWidth,
                            groundHeight +
                            1.1f,
                            z - 4f +
                            b * 2.5f
                        )
                    );
                }
            }

            if (coconutPrefab != null &&
                row % 5 == 0)
            {
                SpawnPickup(
                    coconutPrefab,
                    new Vector3(
                        centerX +
                        safeLane *
                        laneWidth,
                        groundHeight +
                        0.8f,
                        z + 5f
                    )
                );
            }

            if (powerUpPrefabs != null &&
                powerUpPrefabs.Length >= 3)
            {
                int kind =
                    row == 4
                        ? 0
                        : row == 9
                            ? 2
                            : row == 15
                                ? 1
                                : -1;

                if (kind >= 0 &&
                    powerUpPrefabs[kind] != null)
                {
                    Instantiate(
                        powerUpPrefabs[kind],
                        new Vector3(
                            centerX +
                            safeLane *
                            laneWidth,
                            groundHeight +
                            1.15f,
                            z + 5f
                        ),
                        Quaternion.identity,
                        transform
                    );
                }
            }

            if (bananaPrefab != null &&
                row >= 15 &&
                row <= 17)
            {
                for (int b = 0; b < 4; b++)
                {
                    SpawnPickup(
                        bananaPrefab,
                        new Vector3(
                            centerX +
                            safeLane *
                            laneWidth,
                            groundHeight +
                            2.2f +
                            Mathf.Sin(
                                b / 3f *
                                Mathf.PI
                            ) *
                            1.8f,
                            z +
                            5f +
                            b *
                            2.2f
                        )
                    );
                }
            }

            lastSafeLane = safeLane;
            row++;

            z += Mathf.Lerp(
                minimumSpacing,
                Mathf.Max(
                    minimumSpacing,
                    maximumSpacing
                ),
                (float)random.NextDouble()
            );
        }

        PatternCount = row;

        if (sceneryPrefabs == null ||
            sceneryPrefabs.Length == 0)
        {
            return;
        }

        for (
            z = startDistance - 10f;
            z < finishLine.position.z + 15f;
            z += Mathf.Max(
                6f,
                scenerySpacing
            ))
        {
            for (
                int side = -1;
                side <= 1;
                side += 2)
            {
                if (z > gapStart - 2f &&
                    z < gapEnd + 2f)
                {
                    continue;
                }

                var prefab =
                    sceneryPrefabs[
                        random.Next(
                            sceneryPrefabs.Length
                        )
                    ];

                if (prefab == null)
                    continue;

                var tree =
                    Instantiate(
                        prefab,
                        new Vector3(
                            centerX +
                            side *
                            (
                                7f +
                                (float)
                                random.NextDouble() *
                                5f
                            ),
                            groundHeight,
                            z
                        ),
                        Quaternion.Euler(
                            0f,
                            random.Next(360),
                            0f
                        ),
                        transform
                    );

                tree.transform.localScale *=
                    Mathf.Lerp(
                        0.8f,
                        1.2f,
                        (float)random.NextDouble()
                    );

                foreach (
                    var collider
                    in tree.GetComponentsInChildren<Collider>())
                {
                    collider.enabled = false;
                }

                if (fallingCoconutPrefab != null &&
                    Mathf.RoundToInt(
                        z / scenerySpacing
                    ) %
                    8 ==
                    0 &&
                    side < 0)
                {
                    // Use tropical palm for falling coconuts.
                    if (sceneryPrefabs[0] != null &&
                        prefab != sceneryPrefabs[0])
                    {
                        Destroy(tree);

                        tree =
                            Instantiate(
                                sceneryPrefabs[0],
                                new Vector3(
                                    centerX +
                                    side * 8f,
                                    groundHeight,
                                    z
                                ),
                                Quaternion.identity,
                                transform
                            );

                        foreach (
                            var collider
                            in tree.GetComponentsInChildren<Collider>())
                        {
                            collider.enabled = false;
                        }
                    }

                    tree.AddComponent<FallingCoconutSpawner>()
                        .Configure(
                            player,
                            fallingCoconutPrefab,
                            random.Next(-1, 2)
                        );
                }

                var outerTree =
                    Instantiate(
                        prefab,
                        new Vector3(
                            centerX +
                            side *
                            (
                                17f +
                                (float)
                                random.NextDouble() *
                                4f
                            ),
                            groundHeight,
                            z + 5f
                        ),
                        Quaternion.Euler(
                            0f,
                            random.Next(360),
                            0f
                        ),
                        transform
                    );

                foreach (
                    var collider
                    in outerTree.GetComponentsInChildren<Collider>())
                {
                    collider.enabled = false;
                }

                if (roadsideShrubPrefab != null)
                {
                    var shrub =
                        Instantiate(
                            roadsideShrubPrefab,
                            new Vector3(
                                centerX +
                                side *
                                6.7f,
                                groundHeight,
                                z + 3f
                            ),
                            Quaternion.Euler(
                                0f,
                                random.Next(360),
                                0f
                            ),
                            transform
                        );

                    shrub.transform.localScale *=
                        0.65f;

                    foreach (
                        var collider
                        in shrub.GetComponentsInChildren<Collider>())
                    {
                        collider.enabled = false;
                    }
                }
            }
        }
    }

    private void SpawnObstacle(
        System.Random random,
        float x,
        float z,
        int variety)
    {
        // Cycle the palette as well as varying it,
        // so all obstacle types appear each run.
        var prefab =
            obstaclePrefabs[
                (
                    variety +
                    random.Next(2)
                ) %
                obstaclePrefabs.Length
            ];

        if (prefab == null)
            return;

        GameObject spawned =
            Instantiate(
                prefab,
                new Vector3(
                    x,
                    groundHeight,
                    z
                ),
                Quaternion.identity,
                transform
            );

        // If this obstacle contains a moving hazard,
        // connect it to this player's difficulty.
        DynamicRunnerHazard[] hazards =
            spawned.GetComponentsInChildren<
                DynamicRunnerHazard
            >(true);

        foreach (
            DynamicRunnerHazard hazard
            in hazards)
        {
            if (hazard != null)
            {
                hazard.Configure(
                    player,
                    this
                );
            }
        }

        SpawnedObstacleCount++;
    }

    private void SpawnPickup(
        GameObject prefab,
        Vector3 visibleCenter)
    {
        var pickup =
            Instantiate(
                prefab,
                visibleCenter,
                prefab.transform.rotation,
                transform
            );

        var sphere =
            pickup.GetComponent<SphereCollider>();

        // Imported collectible meshes have offset pivots;
        // collider stays centered on the visual.
        if (sphere != null)
        {
            pickup.transform.position +=
                visibleCenter -
                pickup.transform.TransformPoint(
                    sphere.center
                );
        }
    }

    private void OnValidate()
    {
        minimumSpacing =
            Mathf.Max(
                8f,
                minimumSpacing
            );

        maximumSpacing =
            Mathf.Max(
                minimumSpacing,
                maximumSpacing
            );

        maxHazardSpeedMultiplier =
            Mathf.Max(
                1f,
                maxHazardSpeedMultiplier
            );
    }
}