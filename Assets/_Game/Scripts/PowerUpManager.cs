using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum PowerUpType { Bulldozer, Glider, Magnet }

[RequireComponent(typeof(RunnerMotor))]
public class PowerUpManager : MonoBehaviour
{
    public static PowerUpManager ActivePlayer { get; private set; }

    [Header("Durations")]
    [SerializeField, Min(0.1f)] private float bulldozerDuration = 5f;
    [SerializeField, Min(0.1f)] private float gliderDuration = 7f;
    [SerializeField, Min(0.1f)] private float magnetDuration = 7f;
    [SerializeField, Min(1f)] private float magnetRadius = 8f;
    [SerializeField, Min(1)] private int bulldozerPointsPerObstacle = 5;
    [Header("Visuals")]
    [SerializeField] private Transform playerModel;
    [SerializeField, Min(1f)] private float giantScale = 1.7f;
    [SerializeField, Min(0.1f)] private float scaleSmoothing = 9f;
    [SerializeField] private Image icon;
    [SerializeField] private Image countdownFill;
    [SerializeField] private TMP_Text countdownLabel;
    [SerializeField] private Sprite bulldozerIcon;
    [SerializeField] private Sprite gliderIcon;
    [SerializeField] private Sprite magnetIcon;

    private readonly float[] endsAt = new float[3];
    private Vector3 originalModelScale;
    private PowerUpType displayedType;
    public float MagnetRadius => magnetRadius;
    public int BonusPoints { get; private set; }
    public event Action<int> BonusPointsChanged;

    private void Awake()
    {
        ActivePlayer = this;
        if (playerModel == null)
        {
            var animator = GetComponentInChildren<Animator>();
            if (animator != null) playerModel = animator.transform;
        }
        if (playerModel != null) originalModelScale = playerModel.localScale;
    }

    public bool IsActive(PowerUpType type) => Time.time < endsAt[(int)type];
    public float Remaining(PowerUpType type) => Mathf.Max(0f, endsAt[(int)type] - Time.time);

    public void Activate(PowerUpType type)
    {
        var health = GetComponent<PlayerHealth>();
        if (health != null && health.IsRunOver) return;
        float duration = type == PowerUpType.Bulldozer ? bulldozerDuration
            : type == PowerUpType.Glider ? gliderDuration : magnetDuration;
        endsAt[(int)type] = Time.time + duration;
        displayedType = type;
        RefreshIndicator();
    }

    public void AwardBulldozerPoints()
    {
        if (!IsActive(PowerUpType.Bulldozer)) return;
        BonusPoints += bulldozerPointsPerObstacle;
        BonusPointsChanged?.Invoke(BonusPoints);
    }

    private void Update()
    {
        if (playerModel != null)
        {
            Vector3 target = originalModelScale * (IsActive(PowerUpType.Bulldozer) ? giantScale : 1f);
            playerModel.localScale = Vector3.Lerp(playerModel.localScale, target, 1f - Mathf.Exp(-scaleSmoothing * Time.deltaTime));
        }
        RefreshIndicator();
    }

    private void RefreshIndicator()
    {
        if (!IsActive(displayedType))
        {
            for (int i = 0; i < endsAt.Length; i++)
                if (IsActive((PowerUpType)i)) { displayedType = (PowerUpType)i; break; }
        }
        bool visible = IsActive(displayedType);
        if (icon != null)
        {
            icon.gameObject.SetActive(visible);
            if (visible) icon.sprite = displayedType == PowerUpType.Bulldozer ? bulldozerIcon
                : displayedType == PowerUpType.Glider ? gliderIcon : magnetIcon;
        }
        if (countdownFill != null)
        {
            countdownFill.gameObject.SetActive(visible);
            if (visible)
            {
                float duration = displayedType == PowerUpType.Bulldozer ? bulldozerDuration
                    : displayedType == PowerUpType.Glider ? gliderDuration : magnetDuration;
                countdownFill.fillAmount = Remaining(displayedType) / duration;
            }
        }
        if (countdownLabel != null)
        {
            countdownLabel.gameObject.SetActive(visible);
            if (visible) countdownLabel.text = $"{displayedType} {Remaining(displayedType):0.0}s";
        }
    }

    private void OnDisable()
    {
        if (playerModel != null) playerModel.localScale = originalModelScale;
        if (ActivePlayer == this) ActivePlayer = null;
    }
}
