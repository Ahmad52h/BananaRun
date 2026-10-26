using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class DamageFlashUI : MonoBehaviour
{
    [SerializeField] private Image overlay;

    private Coroutine fadeRoutine;
    private const float PeakAlpha = 0.35f;
    private const float FadeSeconds = 0.4f;

    public static DamageFlashUI GetOrCreate()
    {
        DamageFlashUI flash = FindAnyObjectByType<DamageFlashUI>(FindObjectsInactive.Include);
        if (flash != null) return flash;

        RunnerHUD hud = FindAnyObjectByType<RunnerHUD>(FindObjectsInactive.Include);
        Canvas canvas = hud != null ? hud.GetComponentInParent<Canvas>() : null;
        if (canvas == null) canvas = FindAnyObjectByType<Canvas>(FindObjectsInactive.Include);
        return canvas != null ? canvas.gameObject.AddComponent<DamageFlashUI>() : null;
    }

    private void Awake()
    {
        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null) canvas = GetComponentInParent<Canvas>();
        if (canvas == null) { enabled = false; return; }

        if (overlay == null)
        {
            foreach (Image image in canvas.GetComponentsInChildren<Image>(true))
            {
                RectTransform rect = image.rectTransform;
                bool fillsCanvas = rect.anchorMin == Vector2.zero && rect.anchorMax == Vector2.one;
                Color color = image.color;
                if (image.name == "DamageFlashOverlay" ||
                    (fillsCanvas && color.r > 0.8f && color.g < 0.2f && color.b < 0.2f))
                {
                    overlay = image;
                    break;
                }
            }
        }

        if (overlay == null)
        {
            GameObject objectWithImage = new GameObject("DamageFlashOverlay", typeof(RectTransform), typeof(Image));
            objectWithImage.transform.SetParent(canvas.transform, false);
            overlay = objectWithImage.GetComponent<Image>();
        }

        RectTransform overlayRect = overlay.rectTransform;
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;
        overlayRect.SetAsLastSibling();
        overlay.raycastTarget = false;
        overlay.color = new Color(1f, 0f, 0f, 0f);
    }

    public void TriggerDamageFlash()
    {
        if (!isActiveAndEnabled || overlay == null) return;
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        SetAlpha(PeakAlpha);
        fadeRoutine = StartCoroutine(Fade());
    }

    private IEnumerator Fade()
    {
        float elapsed = 0f;
        while (elapsed < FadeSeconds)
        {
            yield return null;
            elapsed += Time.unscaledDeltaTime;
            SetAlpha(PeakAlpha * (1f - Mathf.Clamp01(elapsed / FadeSeconds)));
        }
        SetAlpha(0f);
        fadeRoutine = null;
    }

    private void SetAlpha(float alpha)
    {
        Color color = overlay.color;
        color.a = alpha;
        overlay.color = color;
    }

    private void OnDisable()
    {
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = null;
        if (overlay != null) SetAlpha(0f);
    }
}
