using System.Collections.Generic;
using UnityEngine;

/// <summary>Scene-owned, bounded reuse of frequent pickup bursts; rival pickups remain silent.</summary>
public sealed class PickupEffectPool : MonoBehaviour
{
    private const int MaxIdlePerPrefab = 12;
    private static PickupEffectPool active;
    private sealed class Effect
    {
        public ParticleSystem Prefab, Particles;
        public AudioSource[] Audio;
        public bool[] AudioEnabled;
        public bool[] AudioMuted;
    }
    private readonly Dictionary<ParticleSystem, Queue<Effect>> idle = new Dictionary<ParticleSystem, Queue<Effect>>();
    private readonly List<Effect> playing = new List<Effect>(24);
    private Transform storage;
    private float nextRefresh;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => active = null;

    public static void Play(ParticleSystem prefab, Vector3 position, bool withAudio = true)
    {
        if (prefab == null) return;
        if (active == null)
        {
            var host = new GameObject("Pickup Effects (Runtime Pool)");
            active = host.AddComponent<PickupEffectPool>();
            active.storage = new GameObject("Inactive Effects").transform;
            active.storage.SetParent(host.transform, false);
            active.storage.gameObject.SetActive(false);
        }
        active.Rent(prefab, position, withAudio);
    }

    private void Rent(ParticleSystem prefab, Vector3 position, bool withAudio)
    {
        if (!idle.TryGetValue(prefab, out var pool))
        {
            pool = new Queue<Effect>();
            idle.Add(prefab, pool);
        }
        Effect effect;
        if (pool.Count > 0) effect = pool.Dequeue();
        else
        {
            var particles = Instantiate(prefab, storage);
            particles.gameObject.SetActive(false);
            var audio = particles.GetComponentsInChildren<AudioSource>(true);
            var enabled = new bool[audio.Length];
            var muted = new bool[audio.Length];
            for (int i = 0; i < audio.Length; i++) { enabled[i] = audio[i].enabled; muted[i] = audio[i].mute; }
            effect = new Effect { Prefab = prefab, Particles = particles, Audio = audio, AudioEnabled = enabled, AudioMuted = muted };
            foreach (var system in particles.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = system.main;
                main.loop = false;
                main.stopAction = ParticleSystemStopAction.None;
            }
            MobilePerformanceBootstrap.PrepareTrackObject(particles.gameObject, true);
        }
        effect.Particles.transform.SetParent(transform, false);
        effect.Particles.transform.SetPositionAndRotation(position, Quaternion.identity);
        for (int i = 0; i < effect.Audio.Length; i++)
        {
            effect.Audio[i].mute = !withAudio || effect.AudioMuted[i];
            effect.Audio[i].enabled = withAudio && effect.AudioEnabled[i];
        }
        effect.Particles.gameObject.SetActive(true);
        effect.Particles.Play(true);
        playing.Add(effect);
    }

    private void Update()
    {
        if (Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + 0.1f;
        for (int i = playing.Count - 1; i >= 0; i--)
        {
            var effect = playing[i];
            if (effect.Particles != null && effect.Particles.IsAlive(true)) continue;
            playing[i] = playing[playing.Count - 1];
            playing.RemoveAt(playing.Count - 1);
            if (effect.Particles == null) continue;
            foreach (var source in effect.Audio) source.Stop();
            effect.Particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            effect.Particles.gameObject.SetActive(false);
            effect.Particles.transform.SetParent(storage, false);
            var pool = idle[effect.Prefab];
            if (pool.Count < MaxIdlePerPrefab) pool.Enqueue(effect);
            else Destroy(effect.Particles.gameObject);
        }
    }

    private void OnDestroy()
    {
        if (active == this) active = null;
    }
}
