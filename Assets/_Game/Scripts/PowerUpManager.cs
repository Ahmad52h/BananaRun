using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum PowerUpType { Bulldozer, Glider, Magnet, Shield, Thunder }

[RequireComponent(typeof(RunnerMotor))]
public class PowerUpManager : MonoBehaviour
{
    public const float BulldozerDurationSeconds = 5f;
    public const float GliderDurationSeconds = 7f;
    public const float MagnetDurationSeconds = 7f;
    public const float ShieldDurationSeconds = 8f;
    public const float ThunderDurationSeconds = 4f;
    public const float ThunderSpeedMultiplier = 1.35f;
    public static PowerUpManager ActivePlayer { get; private set; }

    [Header("Durations")]
    [SerializeField, Min(0.1f)] private float bulldozerDuration = BulldozerDurationSeconds;
    [SerializeField, Min(0.1f)] private float gliderDuration = GliderDurationSeconds;
    [SerializeField, Min(0.1f)] private float magnetDuration = MagnetDurationSeconds;
    [SerializeField, Min(0.1f)] private float shieldDuration = ShieldDurationSeconds;
    [SerializeField, Min(0.1f)] private float thunderDuration = ThunderDurationSeconds;
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
    [Header("Giant Impact")]
    [SerializeField] private GiantImpactEffect giantImpactPrefab;

    public void PlayCrushEffect(Vector3 position)
    {
        if (giantImpactPrefab != null) Instantiate(giantImpactPrefab, position, Quaternion.identity);
    }

    private readonly float[] endsAt = new float[(int)PowerUpType.Thunder + 1];
    private Vector3 originalModelScale;
    private PowerUpType displayedType;
    private readonly StringBuilder timerText = new StringBuilder(96);
    public float MagnetRadius => magnetRadius;
    public bool HasShield => IsActive(PowerUpType.Shield);
    public bool HasThunder => IsActive(PowerUpType.Thunder);
    public float SpeedMultiplier => HasThunder ? ThunderSpeedMultiplier : 1f;
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
        if (GetComponent<RunnerAbilityVisual>() == null)
            gameObject.AddComponent<RunnerAbilityVisual>();
        ConfigureTimerLabel();
    }

    private void ConfigureTimerLabel()
    {
        if (countdownLabel == null) return;
        countdownLabel.alignment = TextAlignmentOptions.Left;
        countdownLabel.textWrappingMode = TextWrappingModes.NoWrap;
        countdownLabel.fontSize = 20f;
        countdownLabel.lineSpacing = 1f;
        var rect = countdownLabel.rectTransform;
        rect.sizeDelta = new Vector2(210f, 150f);
        rect.anchoredPosition = new Vector2(28f, -30f);
    }

    public bool IsActive(PowerUpType type) => Time.time < endsAt[(int)type];
    public float Remaining(PowerUpType type) => Mathf.Max(0f, endsAt[(int)type] - Time.time);

    private float DurationFor(PowerUpType type)
    {
        switch (type)
        {
            case PowerUpType.Bulldozer: return bulldozerDuration;
            case PowerUpType.Glider: return gliderDuration;
            case PowerUpType.Magnet: return magnetDuration;
            case PowerUpType.Shield: return shieldDuration;
            case PowerUpType.Thunder: return thunderDuration;
            default: return 1f;
        }
    }

    public void Activate(PowerUpType type)
    {
        var health = GetComponent<PlayerHealth>();
        if (health != null && health.IsRunOver) return;
        endsAt[(int)type] = Time.time + DurationFor(type);
        displayedType = type;
        RefreshIndicator();
    }

    /// <summary>Consumes the one hit absorbed by an active Shield.</summary>
    public bool ConsumeShield()
    {
        if (!HasShield) return false;
        endsAt[(int)PowerUpType.Shield] = 0f;
        GetComponent<RunnerAbilityVisual>()?.PlayShieldBreak();
        if (!IsActive(displayedType)) displayedType = PowerUpType.Shield;
        RefreshIndicator();
        return true;
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
        Sprite displayedIcon = IconFor(displayedType);
        if (icon != null)
        {
            icon.enabled = visible && displayedIcon != null;
            icon.gameObject.SetActive(visible);
            if (visible) icon.sprite = displayedIcon;
        }
        if (countdownFill != null)
        {
            countdownFill.gameObject.SetActive(visible);
            if (visible)
            {
                countdownFill.color = PowerUpVisualUtility.ColorFor(displayedType);
                countdownFill.fillAmount = Mathf.Clamp01(Remaining(displayedType) / Mathf.Max(0.01f, DurationFor(displayedType)));
            }
        }
        if (countdownLabel != null)
        {
            countdownLabel.gameObject.SetActive(visible);
            if (visible)
            {
                timerText.Length = 0;
                for (int i = 0; i < endsAt.Length; i++)
                {
                    var type = (PowerUpType)i;
                    if (!IsActive(type)) continue;
                    if (timerText.Length > 0) timerText.Append('\n');
                    timerText.Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(PowerUpVisualUtility.ColorFor(type)))
                        .Append('>').Append(type.ToString().ToUpperInvariant()).Append("</color> ")
                        .Append(Remaining(type).ToString("0.0")).Append('s');
                }
                countdownLabel.text = timerText.ToString();
            }
        }
    }

    private Sprite IconFor(PowerUpType type)
    {
        switch (type)
        {
            case PowerUpType.Bulldozer: return bulldozerIcon;
            case PowerUpType.Glider: return gliderIcon;
            case PowerUpType.Magnet: return magnetIcon;
            default: return null;
        }
    }

    private void OnDisable()
    {
        if (playerModel != null) playerModel.localScale = originalModelScale;
        if (ActivePlayer == this) ActivePlayer = null;
    }
}
