using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LevelProgressHUD : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private Transform finishLine;
    [SerializeField] private Image progressFill;
    [SerializeField] private RectTransform monkeyMarker;
    [SerializeField] private RivalRunner rival;
    [SerializeField] private RectTransform rivalMarker;
    [SerializeField] private RaceProgressTracker race;
    [SerializeField] private TMP_Text distanceLabel;
    [SerializeField, Min(0.01f)] private float smoothingTime = 0.12f;

    private float startZ;
    private float displayedProgress;
    private float velocity;
    private float rivalDisplayedProgress, rivalVelocity;
    private FinishGate finishGate;
    public float Progress { get; private set; }
    public float RivalProgress { get; private set; }
    public Transform Player => player;
    public float RunLength => finishLine != null ? Mathf.Max(0f, finishLine.position.z - startZ) : 0f;
    public float DistanceTravelled => player != null ? DistanceAt(player.position.z) : 0f;
    public float DistanceAt(float worldZ) => Mathf.Max(0f, worldZ - startZ);
    public float ProgressAt(float worldZ) => RunLength > 0f ? Mathf.Clamp01(DistanceAt(worldZ) / RunLength) : 0f;

    private void Awake()
    {
        if (player != null) startZ = player.position.z;
        if (finishLine != null) finishGate = finishLine.GetComponent<FinishGate>();
    }

    private void Start()
    {
        Draw(0f);
        DrawRival(0f);
    }

    private void Update()
    {
        if (player == null || finishLine == null) return;
        float length = RunLength;
        if (length <= 0f) return;
        Progress = race != null ? race.PlayerProgress : ProgressAt(player.position.z);
        if (finishGate != null && finishGate.Completed) Progress = 1f;
        displayedProgress = Mathf.SmoothDamp(displayedProgress, Progress, ref velocity,
            smoothingTime, Mathf.Infinity, Time.deltaTime);
        if (Progress >= 0.999f) displayedProgress = 1f;
        Draw(displayedProgress);
        if (rival != null)
        {
            RivalProgress = race != null ? race.RivalProgress : ProgressAt(rival.transform.position.z);
            rivalDisplayedProgress = Mathf.SmoothDamp(rivalDisplayedProgress, RivalProgress,
                ref rivalVelocity, smoothingTime, Mathf.Infinity, Time.deltaTime);
            if (RivalProgress >= 0.999f) rivalDisplayedProgress = 1f;
            DrawRival(rivalDisplayedProgress);
        }
        if (distanceLabel != null)
            distanceLabel.text = $"{Mathf.RoundToInt(Progress * length)} / {Mathf.RoundToInt(length)} m";
    }

    private void Draw(float amount)
    {
        if (progressFill != null)
        {
            if (progressFill.type == Image.Type.Filled) progressFill.fillAmount = amount;
            else progressFill.rectTransform.anchorMax = new Vector2(amount, 1f);
        }
        if (monkeyMarker == null) return;
        monkeyMarker.anchorMin = monkeyMarker.anchorMax = new Vector2(amount, 0.5f);
        monkeyMarker.anchoredPosition = Vector2.zero;
    }

    private void DrawRival(float amount)
    {
        if (rivalMarker == null) return;
        rivalMarker.anchorMin = rivalMarker.anchorMax = new Vector2(Mathf.Clamp01(amount), 0.5f);
        rivalMarker.anchoredPosition = new Vector2(0f, 52f);
    }
}
