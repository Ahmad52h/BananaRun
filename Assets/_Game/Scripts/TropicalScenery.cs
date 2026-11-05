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
    private Queue<GameObject>[] pool;
    public bool HasBorderPrefabs
    {
        get
        {
            if (borderChunks != null) foreach (var prefab in borderChunks) if (prefab != null) return true;
            return false;
        }
    }

    public void DisableAuthoredPreview() { if (editorPreview != null) editorPreview.SetActive(false); }

    private void Start()
    {
        DisableAuthoredPreview();
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
        if (pool == null)
        {
            pool = new Queue<GameObject>[borderChunks.Length];
            for (int i = 0; i < pool.Length; i++) pool[i] = new Queue<GameObject>();
        }
        float length = Mathf.Max(5f, chunkLength);
        int first = Mathf.Max(0, Mathf.FloorToInt((player.position.z - behindDistance) / length));
        int last = Mathf.CeilToInt((player.position.z + aheadDistance) / length);
        if (TrackDistanceStreamer.TryGetWindow(out float cullBehindZ, out float renderAheadZ))
        {
            first = Mathf.Max(0, Mathf.FloorToInt(cullBehindZ / length));
            last = Mathf.CeilToInt(renderAheadZ / length);
        }
        if (finishLine != null) last = Mathf.Min(last, Mathf.CeilToInt((finishLine.position.z + 24) / length));
        // Return expired chunks first so advancing one chunk can reuse the outgoing one.
        expired.Clear();
        foreach (var pair in live) if (pair.Key < first || pair.Key > last) expired.Add(pair.Key);
        foreach (int key in expired)
        {
            var chunk = live[key];
            TrackDistanceStreamer.Unregister(chunk);
            chunk.SetActive(false);
            pool[((key % pool.Length) + pool.Length) % pool.Length].Enqueue(chunk);
            live.Remove(key);
        }
        for (int i = first; i <= last; i++)
        {
            if (live.ContainsKey(i)) continue;
            if (TrackExpansion.OverlapsWater(i * length, (i + 1) * length)) continue;
            if (RiverCrocodileGauntlet.Active != null &&
                RiverCrocodileGauntlet.Active.OverlapsRiverGap(i * length, (i + 1) * length)) continue;
            int kind = ((i % borderChunks.Length) + borderChunks.Length) % borderChunks.Length;
            var prefab = borderChunks[kind];
            if (prefab != null)
            {
                GameObject chunk;
                if (pool[kind].Count > 0)
                {
                    chunk = pool[kind].Dequeue();
                    chunk.transform.position = new Vector3(0, groundHeight, i * length);
                    chunk.SetActive(true);
                }
                else chunk = Instantiate(prefab, new Vector3(0, groundHeight, i * length), Quaternion.identity, transform);
                live[i] = chunk;
                TrackDistanceStreamer.Register(chunk);
            }
        }
    }
}
