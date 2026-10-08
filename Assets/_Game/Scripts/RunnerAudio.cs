using UnityEngine;

/// <summary>Scene-owned audio. Separate sources allow overlapping pickups over looping music.</summary>
public class RunnerAudio : MonoBehaviour
{
    public static RunnerAudio Instance { get; private set; }

    [Header("Clips")]
    [SerializeField] private AudioClip backgroundMusic;
    [SerializeField] private AudioClip bananaCollect;
    [SerializeField] private AudioClip monkeyHurt;
    [SerializeField, Range(0f, 1f)] private float hurtVolume = 0.6f;
    private AudioSource hurtSource;
    [Header("Mix")]
    [SerializeField, Range(0f, 1f)] private float musicVolume = 0.22f;
    [SerializeField, Range(0f, 1f)] private float bananaVolume = 0.65f;
    [SerializeField] private bool playMusicOnStart = true;
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource effectsSource;
    [Header("Musical Banana Combo")]
    [SerializeField, Min(0.1f)] private float comboResetSeconds = 1.2f;
    [SerializeField] private float[] bananaPitchScale = { 1f, 1.125f, 1.25f, 1.333f, 1.5f, 1.667f, 1.875f, 2f };

    private int bananaCombo;
    private float lastBananaTime = float.NegativeInfinity;
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
        ApplySettings();
        musicSource.Play();
    }

    public void PlayBananaCollect(AudioClip overrideClip = null)
    {
        var clip = overrideClip != null ? overrideClip : bananaCollect;
        if (!isActiveAndEnabled || effectsSource == null || clip == null) return;
        bananaCombo = Time.time - lastBananaTime <= comboResetSeconds ? bananaCombo + 1 : 1;
        lastBananaTime = Time.time;
        int index = Mathf.Min(bananaCombo - 1, bananaPitchScale == null ? 0 : bananaPitchScale.Length - 1);
        LastBananaPitch = bananaPitchScale != null && bananaPitchScale.Length > 0
            ? Mathf.Clamp(bananaPitchScale[index], 0.5f, 2f) : 1f;
        effectsSource.pitch = LastBananaPitch;
        effectsSource.PlayOneShot(clip, bananaVolume);
    }

    public void PlayHurt()
    {
        if (!isActiveAndEnabled || hurtSource == null || monkeyHurt == null) return;
        // Separate voice avoids changing the pitch of an overlapping banana combo.
        hurtSource.PlayOneShot(monkeyHurt, hurtVolume);
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
