using UnityEngine;

/// <summary>Scene-owned start clock. Gameplay stays locked until the single GO transition.</summary>
[DefaultExecutionOrder(-200)]
[DisallowMultipleComponent]
public sealed class RaceStartSequence : MonoBehaviour
{
    public static RaceStartSequence Instance { get; private set; }
    public static bool IsHolding => Instance != null && Instance.isActiveAndEnabled && !Instance.isRaceStarted;
    [SerializeField] private RunnerMotor player;
    [SerializeField] private RivalRunner rival;
    [SerializeField] private GiantGorillaChaser gorilla;
    [SerializeField] private RunnerCamera raceCamera;
    [SerializeField] private WoodenStartingGate gate;
    [Header("Start audio")]
    [Tooltip("One short cue for each of 3, 2 and 1. Use a clip without leading silence.")]
    public AudioClip startCountdownSound;
    [Tooltip("Launch cue played in the same frame as GO and the gate opening.")]
    public AudioClip goCountdownSound;
    [SerializeField, Range(0f, 1f)] private float countdownVolume = 0.75f;
    [Header("Timing")]
    [SerializeField, Min(3f)] private float countdownSeconds = 3.5f;
    [SerializeField, Min(0.1f)] private float cameraReturnSeconds = 0.75f;
    [SerializeField] private bool isRaceStarted;
    public bool IsRaceStarted => isRaceStarted;
    public float Elapsed { get; private set; }
    public float CountdownSeconds => countdownSeconds;
    public string CountdownText => countdownUI != null ? countdownUI.CurrentText : "";
    public float ReleasedAt { get; private set; } = -1f;

    private RaceCountdownUI countdownUI;
    private AudioSource audioSource;
    private bool began;
    private bool roared;
    private int currentBeat = -1;
    private Canvas gameplayCanvas;
    private bool gameplayCanvasWasEnabled;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Instance = null;

    private void Awake()
    {
        if (Instance != null && Instance != this) { enabled = false; return; }
        Instance = this;
        isRaceStarted = false;
        if (player == null) player = FindAnyObjectByType<RunnerMotor>();
        if (rival == null) rival = FindAnyObjectByType<RivalRunner>();
        if (gorilla == null) gorilla = FindAnyObjectByType<GiantGorillaChaser>();
        if (raceCamera == null) raceCamera = FindAnyObjectByType<RunnerCamera>();
    }

    private void Start()
    {
        if (player == null) { enabled = false; return; }
        // Start runs after every Awake: the gorilla visual and both animators now exist.
        Elapsed = 0f;
        began = true;
        var hud = FindAnyObjectByType<RunnerHUD>();
        if (hud != null) gameplayCanvas = hud.GetComponentInParent<Canvas>();
        if (gameplayCanvas != null)
        {
            gameplayCanvasWasEnabled = gameplayCanvas.enabled;
            gameplayCanvas.enabled = false;
        }
        gate?.ResetClosed();
        gorilla?.BeginStartingIntimidation();
        if (raceCamera != null) raceCamera.BeginRaceIntro(player.transform, rival != null ? rival.transform : null, gorilla);
        countdownUI = RaceCountdownUI.Create(transform);
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        audioSource.volume = countdownVolume;
        // Reserve a clear runway even for hand-placed obstacles near the start.
        ClearStartingRunway();
        ShowCountdownBeat(0);
    }

    private void Update()
    {
        if (!began || Time.deltaTime <= 0f) return;
        // Do not count the Start frame's delta: the first cue only began this frame.
        if (Time.frameCount == firstBeatFrame) return;
        Elapsed += Time.deltaTime;
        if (!isRaceStarted)
        {
            if (!roared && Elapsed >= 0.22f)
            {
                roared = true;
                gorilla?.PlayStartingRoar();
                raceCamera?.ShakeRaceIntro();
            }
            if (Elapsed >= countdownSeconds) ReleaseRunners();
            else
            {
                int beat = Mathf.Min(2, Mathf.FloorToInt(Elapsed / (countdownSeconds / 3f)));
                ShowCountdownBeat(beat);
            }
        }
        else if (Elapsed >= countdownSeconds + 0.65f) countdownUI.Hide();
    }

    private void ReleaseRunners()
    {
        // The GO cue, UI, barrier and movement release share this frame.
        ShowCountdownBeat(3);
        isRaceStarted = true;
        ReleasedAt = Time.time;
        gate?.Open();
        player.ResetVerticalVelocity();
        player.GetComponent<RunnerAnimation>()?.PlayRaceStartRun();
        rival?.PlayRaceStartRun();
        gorilla?.EndStartingIntimidation();
        raceCamera?.EndRaceIntro(cameraReturnSeconds);
        RestoreGameplayHUD();
        player.GetComponent<MonkeyFootstepDust>()?.EmitStartPuff();
        if (rival != null)
        {
            var dust = rival.GetComponent<MonkeyFootstepDust>();
            if (dust == null) dust = rival.gameObject.AddComponent<MonkeyFootstepDust>();
            dust.EmitStartPuff();
        }
    }

    private int firstBeatFrame;

    private void ShowCountdownBeat(int beat)
    {
        if (currentBeat == beat) return;
        currentBeat = beat;
        bool isGo = beat == 3;
        AudioClip clip = isGo ? goCountdownSound : startCountdownSound;
        // One owner for both effects prevents UI calls and audio calls from drifting apart.
        countdownUI.Show(isGo ? "GO!" : (3 - beat).ToString(), isGo);
        if (clip != null) audioSource.PlayOneShot(clip);
        else Debug.LogWarning($"Race start has no audio cue assigned for {(isGo ? "GO" : "numbers")}.", this);
        if (beat == 0) firstBeatFrame = Time.frameCount;
    }

    private void ClearStartingRunway()
    {
        float start = player.transform.position.z;
        foreach (var obstacle in FindObjectsByType<RunnerObstacle>(FindObjectsSortMode.None))
        {
            var collider = obstacle.GetComponent<Collider>();
            float near = collider != null ? collider.bounds.min.z : obstacle.transform.position.z;
            float far = collider != null ? collider.bounds.max.z : obstacle.transform.position.z;
            if (far >= start - 5f && near < start + 15f) obstacle.gameObject.SetActive(false);
        }
        foreach (var peel in FindObjectsByType<BananaPeelHazard>(FindObjectsSortMode.None))
            if (peel.transform.position.z >= start - 5f && peel.transform.position.z < start + 15f)
                peel.gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        if (Instance != this) return;
        // Disabling the director must never strand gameplay behind a closed gate.
        if (began && !isRaceStarted)
        {
            isRaceStarted = true;
            gate?.Open();
            gorilla?.EndStartingIntimidation();
            raceCamera?.EndRaceIntro(cameraReturnSeconds);
        }
        countdownUI?.Hide();
        RestoreGameplayHUD();
        if (audioSource != null) audioSource.Stop();
        Instance = null;
    }

    private void RestoreGameplayHUD()
    {
        if (gameplayCanvas != null) gameplayCanvas.enabled = gameplayCanvasWasEnabled;
    }
}
