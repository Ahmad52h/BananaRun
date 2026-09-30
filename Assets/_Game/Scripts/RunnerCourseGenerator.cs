using System;
using UnityEngine;

/// <summary>Builds the extended course once per run with reproducible, lane-safe patterns.</summary>
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
    [SerializeField, Min(8f)] private float minimumSpacing = 12f;
    [SerializeField, Min(8f)] private float maximumSpacing = 20f;
    [Header("Scenery")]
    [SerializeField] private GameObject[] sceneryPrefabs;
    [SerializeField] private GameObject roadsideShrubPrefab;
    [SerializeField, Min(6f)] private float scenerySpacing = 14f;

    public int SpawnedObstacleCount { get; private set; }
    public int PatternCount { get; private set; }

    private void Start()
    {
        if (player == null || finishLine == null || obstaclePrefabs == null || obstaclePrefabs.Length == 0) return;
        var random = new System.Random(randomizeEachRun ? Environment.TickCount : seed);
        float centerX = player.transform.position.x;
        float laneWidth = player.LaneDistance;
        float end = finishLine.position.z - finishApproach;
        float z = Mathf.Max(startDistance, player.transform.position.z + 20f);
        int lastSafeLane = 0;
        int row = 0;
        while (z < end)
        {
            // A safe lane moves at most one lane per row. Every row has a clear route.
            int safeLane = Mathf.Clamp(lastSafeLane + random.Next(-1, 2), -1, 1);
            int pattern = row % 4;
            int firstLane = safeLane == -1 ? 0 : -1;
            int secondLane = safeLane == 1 ? 0 : 1;
            SpawnObstacle(random, centerX + firstLane * laneWidth, z, row);
            if (pattern == 1 || pattern == 3)
                SpawnObstacle(random, centerX + secondLane * laneWidth, z + (pattern == 3 ? 2.5f : 0), row + 2);
            if (bananaPrefab != null)
                for (int b = 0; b < 3; b++) SpawnPickup(bananaPrefab, new Vector3(centerX + safeLane * laneWidth, groundHeight + 1.1f, z - 4 + b * 2.5f));
            if (coconutPrefab != null && row % 5 == 0)
                SpawnPickup(coconutPrefab, new Vector3(centerX + safeLane * laneWidth, groundHeight + 0.8f, z + 5));
            lastSafeLane = safeLane;
            row++;
            z += Mathf.Lerp(minimumSpacing, Mathf.Max(minimumSpacing, maximumSpacing), (float)random.NextDouble());
        }
        PatternCount = row;
        if (sceneryPrefabs == null || sceneryPrefabs.Length == 0) return;
        for (z = startDistance - 10; z < finishLine.position.z + 15; z += Mathf.Max(6, scenerySpacing))
            for (int side = -1; side <= 1; side += 2)
            {
                var prefab = sceneryPrefabs[random.Next(sceneryPrefabs.Length)];
                if (prefab == null) continue;
                var tree = Instantiate(prefab, new Vector3(centerX + side * (7f + (float)random.NextDouble() * 5f), groundHeight, z),
                    Quaternion.Euler(0, random.Next(360), 0), transform);
                tree.transform.localScale *= Mathf.Lerp(0.8f, 1.2f, (float)random.NextDouble());
                foreach (var collider in tree.GetComponentsInChildren<Collider>()) collider.enabled = false;
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

    private void SpawnObstacle(System.Random random, float x, float z, int variety)
    {
        // Cycle the palette as well as varying it, so all obstacle types appear each run.
        var prefab = obstaclePrefabs[(variety + random.Next(2)) % obstaclePrefabs.Length];
        if (prefab == null) return;
        Instantiate(prefab, new Vector3(x, groundHeight, z), Quaternion.identity, transform);
        SpawnedObstacleCount++;
    }

    private void SpawnPickup(GameObject prefab, Vector3 visibleCenter)
    {
        var pickup = Instantiate(prefab, visibleCenter, prefab.transform.rotation, transform);
        var sphere = pickup.GetComponent<SphereCollider>();
        // Imported collectible meshes have offset pivots; the pickup collider is centered on the visual.
        if (sphere != null) pickup.transform.position += visibleCenter - pickup.transform.TransformPoint(sphere.center);
    }

    private void OnValidate()
    {
        minimumSpacing = Mathf.Max(8f, minimumSpacing);
        maximumSpacing = Mathf.Max(minimumSpacing, maximumSpacing);
    }
}
