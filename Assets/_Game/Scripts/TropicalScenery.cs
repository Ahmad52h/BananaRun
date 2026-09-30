using System.Collections.Generic;
using UnityEngine;

/// <summary>Keeps decorative border chunks near the runner instead of populating all 900m.</summary>
public class TropicalScenery : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private Transform finishLine;
    [SerializeField] private GameObject[] borderChunks;
    [SerializeField] private GameObject editorPreview;
    [SerializeField, Min(5f)] private float chunkLength = 24f;
    [SerializeField, Min(24f)] private float aheadDistance = 120f;
    [SerializeField, Min(12f)] private float behindDistance = 40f;
    [SerializeField] private float groundHeight = -0.3f;
    private readonly Dictionary<int, GameObject> live = new Dictionary<int, GameObject>();
    private readonly List<int> expired = new List<int>();
    private float nextRefresh;

    private void Start()
    {
        if (editorPreview != null) editorPreview.SetActive(false);
        Refresh();
    }

    private void Update()
    {
        if (Time.time < nextRefresh) return;
        nextRefresh = Time.time + 0.2f;
        Refresh();
    }

    private void Refresh()
    {
        if (player == null || borderChunks == null || borderChunks.Length == 0) return;
        float length = Mathf.Max(5f, chunkLength);
        int first = Mathf.Max(-1, Mathf.FloorToInt((player.position.z - behindDistance) / length));
        int last = Mathf.CeilToInt((player.position.z + aheadDistance) / length);
        if (finishLine != null) last = Mathf.Min(last, Mathf.CeilToInt((finishLine.position.z + 24) / length));
        for (int i = first; i <= last; i++)
        {
            if (live.ContainsKey(i)) continue;
            var prefab = borderChunks[((i % borderChunks.Length) + borderChunks.Length) % borderChunks.Length];
            if (prefab != null) live[i] = Instantiate(prefab, new Vector3(0, groundHeight, i * length), Quaternion.identity, transform);
        }
        expired.Clear();
        foreach (var pair in live) if (pair.Key < first || pair.Key > last) expired.Add(pair.Key);
        foreach (int key in expired) { Destroy(live[key]); live.Remove(key); }
    }
}
