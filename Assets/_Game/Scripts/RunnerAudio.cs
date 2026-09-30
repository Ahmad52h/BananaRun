using UnityEngine;

/// <summary>Scene-owned audio. Separate sources allow overlapping pickups over looping music.</summary>
public class RunnerAudio : MonoBehaviour
{
    public static RunnerAudio Instance { get; private set; }

    [Header("Clips")]
    [SerializeField] private AudioClip backgroundMusic;
    [SerializeField] private AudioClip bananaCollect;
    [Header("Mix")]
    [SerializeField, Range(0f, 1f)] private float musicVolume = 0.22f;
    [SerializeField, Range(0f, 1f)] private float bananaVolume = 0.65f;
    [SerializeField] private bool playMusicOnStart = true;
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource effectsSource;

    private void Awake()
    {
        if (Instance != null && Instance != this) { enabled = false; return; }
        Instance = this;
        if (musicSource == null) musicSource = gameObject.AddComponent<AudioSource>();
        if (effectsSource == null || effectsSource == musicSource) effectsSource = gameObject.AddComponent<AudioSource>();
        musicSource.playOnAwake = false;
        musicSource.loop = true;
        musicSource.spatialBlend = 0f;
        musicSource.volume = musicVolume;
        effectsSource.playOnAwake = false;
        effectsSource.loop = false;
        effectsSource.spatialBlend = 0f;
    }

    private void Start()
    {
        if (playMusicOnStart) PlayMusic();
    }

    public void PlayMusic()
    {
        if (musicSource == null || backgroundMusic == null) return;
        musicSource.clip = backgroundMusic;
        musicSource.volume = musicVolume;
        musicSource.Play();
    }

    public void PlayBananaCollect(AudioClip overrideClip = null)
    {
        var clip = overrideClip != null ? overrideClip : bananaCollect;
        if (!isActiveAndEnabled || effectsSource == null || clip == null) return;
        effectsSource.PlayOneShot(clip, bananaVolume);
    }

    private void OnValidate()
    {
        if (musicSource != null) musicSource.volume = musicVolume;
    }

    private void OnDisable()
    {
        if (musicSource != null) musicSource.Stop();
        if (effectsSource != null) effectsSource.Stop();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
