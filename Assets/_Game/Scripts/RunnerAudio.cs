using UnityEngine;

/// <summary>Scene-owned audio. Separate sources allow overlapping pickups over looping music.</summary>
public class RunnerAudio : MonoBehaviour
{
    public static RunnerAudio Instance { get; private set; }

    [Header("Clips")]
    [SerializeField] private AudioClip backgroundMusic;
    [SerializeField] private AudioClip bananaCollect;
    [SerializeField] private AudioClip monkeyHurt;
    [SerializeField, Range(0f, 0.4f)] private float hurtVolume = 0.38f;
    private AudioSource hurtSource;
    [Header("Mix")]
    [SerializeField, Range(0f, 1f)] private float musicVolume = 0.22f;
    [SerializeField, Range(0f, 1f)] private float bananaVolume = 0.65f;
    [SerializeField, Range(0f, 1f)] private float victoryVolume = 0.65f;
    [SerializeField] private bool playMusicOnStart = true;
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource effectsSource;
    [Header("Banana Combo")]
    [SerializeField, Min(0.1f)] private float comboResetSeconds = 1.2f;

    private int bananaCombo;
    private float lastBananaTime = float.NegativeInfinity;
    private float lastPickupSoundAt = float.NegativeInfinity;
    private float lastHurtSoundAt = float.NegativeInfinity;
    public int BananaCombo => bananaCombo;
    public float LastBananaPitch { get; private set; } = 1f;

    private void Awake()
    {
        if (Instance != null && Instance != this) { enabled = false; return; }
        Instance = this;
        if (musicSource == null) musicSource = gameObject.AddComponent<AudioSource>();
        if (effectsSource == null || effectsSource == musicSource) effectsSource = gameObject.AddComponent<AudioSource>();
        musicSource.playOnAwake = false;
        musicSource.loop = true;
        musicSource.spatialBlend = 0f;
        musicSource.ignoreListenerVolume = true;
        ApplySettings();
        effectsSource.playOnAwake = false;
        effectsSource.loop = false;
        effectsSource.spatialBlend = 0f;
        hurtSource = gameObject.AddComponent<AudioSource>();
        hurtSource.playOnAwake = false;
        hurtSource.spatialBlend = 0f;
    }

    private void Start()
    {
        if (playMusicOnStart) PlayMusic();
    }

    private void OnEnable() { SettingsPanelController.Changed += ApplySettings; }
    private void ApplySettings()
    {
        if (musicSource != null) musicSource.volume = musicVolume * SettingsPanelController.MusicVolume;
    }

    public void PlayMusic()
    {
        if (musicSource == null || backgroundMusic == null) return;
        musicSource.clip = backgroundMusic;
        musicSource.pitch = 1f;
        ApplySettings();
        musicSource.Play();
    }

    public void PlayBananaCollect(AudioClip overrideClip = null)
    {
        var clip = overrideClip != null ? overrideClip : bananaCollect;
        if (!isActiveAndEnabled || effectsSource == null || clip == null) return;
        bananaCombo = Time.time - lastBananaTime <= comboResetSeconds ? bananaCombo + 1 : 1;
        lastBananaTime = Time.time;
        LastBananaPitch = PlayRandomizedEffect(effectsSource, clip, bananaVolume,
            ref lastPickupSoundAt, 0.06f);
    }

    /// <summary>
    /// Ability pickups reuse the light collect cue but do not advance the banana
    /// combo. Callers intentionally skip this for the rival.
    /// </summary>
    public void PlayPowerUpCollect()
    {
        if (!isActiveAndEnabled || effectsSource == null || bananaCollect == null) return;
        PlayRandomizedEffect(effectsSource, bananaCollect, bananaVolume * 0.9f,
            ref lastPickupSoundAt, 0.06f);
    }

    private float PlayRandomizedEffect(AudioSource source, AudioClip clip, float volume,
        ref float lastPlayedAt, float cooldown)
    {
        if (!isActiveAndEnabled || source == null || clip == null) return 1f;
        if (Time.unscaledTime - lastPlayedAt < cooldown) return source.pitch;
        lastPlayedAt = Time.unscaledTime;
        source.pitch = Random.Range(0.9f, 1.1f);
        source.PlayOneShot(clip, volume);
        return source.pitch;
    }

    public void PlayHurt()
    {
        if (!isActiveAndEnabled || hurtSource == null || monkeyHurt == null) return;
        if (Time.unscaledTime - lastHurtSoundAt < 0.15f) return;
        lastHurtSoundAt = Time.unscaledTime;
        // Separate voice avoids changing the pitch of an overlapping banana combo.
        hurtSource.pitch = 1f;
        hurtSource.PlayOneShot(monkeyHurt, Mathf.Min(hurtVolume, 0.38f));
    }

    public void PlayVictory(AudioClip clip)
    {
        if (!isActiveAndEnabled || effectsSource == null || clip == null) return;
        effectsSource.pitch = 1f;
        effectsSource.PlayOneShot(clip, victoryVolume);
    }

    private void OnValidate()
    {
        if (musicSource != null) musicSource.volume = musicVolume;
        comboResetSeconds = Mathf.Max(0.1f, comboResetSeconds);
    }

    private void OnDisable()
    {
        SettingsPanelController.Changed -= ApplySettings;
        if (musicSource != null) musicSource.Stop();
        if (effectsSource != null) effectsSource.Stop();
        if (hurtSource != null) hurtSource.Stop();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
