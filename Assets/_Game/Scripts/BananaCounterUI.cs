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

    private void Awake()
    {
        label = GetComponent<TMP_Text>();
        if (bananaIcon == null)
        {
            Transform sibling = transform.parent != null ? transform.parent.Find("BananaIcon") : null;
            if (sibling != null) bananaIcon = sibling as RectTransform;
        }
        if (bananaIcon != null) iconRestScale = bananaIcon.localScale;
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
        label.text = displayedCount.ToString();
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
        label.text = count.ToString();
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
