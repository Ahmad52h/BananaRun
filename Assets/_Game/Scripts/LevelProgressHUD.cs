using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LevelProgressHUD : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private Transform finishLine;
    [SerializeField] private Image progressFill;
    [SerializeField] private RectTransform monkeyMarker;
    [SerializeField] private TMP_Text distanceLabel;
    [SerializeField, Min(0.01f)] private float smoothingTime = 0.12f;

    private float startZ;
    private float displayedProgress;
    private float velocity;
    private FinishGate finishGate;
    public float Progress { get; private set; }

    private void Start()
    {
        if (player != null) startZ = player.position.z;
        if (finishLine != null) finishGate = finishLine.GetComponent<FinishGate>();
        Draw(0f);
    }

    private void Update()
    {
        if (player == null || finishLine == null) return;
        float length = finishLine.position.z - startZ;
        if (length <= 0f) return;
        Progress = Mathf.Clamp01((player.position.z - startZ) / length);
        if (finishGate != null && finishGate.Completed) Progress = 1f;
        displayedProgress = Mathf.SmoothDamp(displayedProgress, Progress, ref velocity,
            smoothingTime, Mathf.Infinity, Time.deltaTime);
        if (Progress >= 0.999f) displayedProgress = 1f;
        Draw(displayedProgress);
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
}
