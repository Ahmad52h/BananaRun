using System.Collections;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(TMP_Text))]
public class BananaCounterUI : MonoBehaviour
{
    [SerializeField] private BananaInventory inventory;
    [SerializeField] private RectTransform bananaIcon;

    private TMP_Text label;
    private Coroutine iconPunch;
    private Vector3 iconRestScale;
    private int displayedCount;
    private FinishGate finishGate;
    private bool quotaInitialized;
    private bool quotaEnabled;
    private int quotaTarget;
    private bool originalAutoSize;
    private float originalFontSize;
    private float originalFontSizeMin;
    private float originalFontSizeMax;

    private void Awake()
    {
        label = GetComponent<TMP_Text>();
        if (bananaIcon == null)
        {
            Transform sibling = transform.parent != null ? transform.parent.Find("BananaIcon") : null;
            if (sibling != null) bananaIcon = sibling as RectTransform;
        }
        if (bananaIcon != null) iconRestScale = bananaIcon.localScale;
        if (label != null)
        {
            originalAutoSize = label.enableAutoSizing;
            originalFontSize = label.fontSize;
            originalFontSizeMin = label.fontSizeMin;
            originalFontSizeMax = label.fontSizeMax;
        }
        finishGate = FindAnyObjectByType<FinishGate>();
    }

    private void OnEnable()
    {
        if (inventory == null)
        {
            Debug.LogError(
                "BananaCounterUI: Assign Player to Inventory.",
                this
            );

            return;
        }

        displayedCount = inventory.Count;
        inventory.CountChanged += Refresh;
        SyncQuota(true);
    }

    private void Update()
    {
        SyncQuota(false);
    }

    private void SyncQuota(bool force)
    {
        if (finishGate == null) finishGate = FindAnyObjectByType<FinishGate>();
        bool enabled = finishGate != null && finishGate.BananaQuotaEnabled;
        int target = finishGate != null ? finishGate.BananaBasketTarget : 0;
        if (!force && quotaInitialized && enabled == quotaEnabled && target == quotaTarget)
            return;

        quotaInitialized = true;
        quotaEnabled = enabled;
        quotaTarget = target;
        ApplyQuotaStyle(enabled);
        if (label != null && inventory != null)
            label.text = FormatCount(inventory.Count);
    }

    private void ApplyQuotaStyle(bool enabled)
    {
        if (label == null) return;
        if (enabled)
        {
            // "12 / 20" is wider than the baseline count, so let TMP shrink it
            // to fit the existing badge without changing the scene layout.
            label.enableAutoSizing = true;
            label.fontSizeMin = Mathf.Min(originalFontSize, 20f);
            label.fontSizeMax = Mathf.Max(originalFontSize, 24f);
        }
        else
        {
            label.enableAutoSizing = originalAutoSize;
            label.fontSize = originalFontSize;
            label.fontSizeMin = originalFontSizeMin;
            label.fontSizeMax = originalFontSizeMax;
        }
    }

    private string FormatCount(int count)
    {
        return quotaEnabled && quotaTarget > 0
            ? $"{count} / {quotaTarget}"
            : count.ToString();
    }

    private void OnDisable()
    {
        if (inventory != null)
        {
            inventory.CountChanged -= Refresh;
        }
        if (iconPunch != null) StopCoroutine(iconPunch);
        iconPunch = null;
        if (bananaIcon != null) bananaIcon.localScale = iconRestScale;
    }

    private void Refresh(int count)
    {
        label.text = FormatCount(count);
        if (count > displayedCount && bananaIcon != null)
        {
            if (iconPunch != null) StopCoroutine(iconPunch);
            iconPunch = StartCoroutine(PunchIcon());
        }
        displayedCount = count;
    }

    private IEnumerator PunchIcon()
    {
        const float duration = 0.15f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float phase = Mathf.Clamp01(elapsed / duration);
            bananaIcon.localScale = iconRestScale * (1f + 0.3f * Mathf.Sin(phase * Mathf.PI));
            yield return null;
        }
        bananaIcon.localScale = iconRestScale;
        iconPunch = null;
    }
}
