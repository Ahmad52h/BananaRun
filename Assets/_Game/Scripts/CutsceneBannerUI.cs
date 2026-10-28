using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Runtime-built, safe-area aware headline used by the win/loss cutscenes.
/// It never takes raycasts, so the results card underneath stays interactive.
/// </summary>
[DisallowMultipleComponent]
public sealed class CutsceneBannerUI : MonoBehaviour
{
    private const float FadeInSeconds = 0.28f;
    private const float DefaultFadeOutSeconds = 0.45f;
    private const int SortingOrder = 40;

    private Canvas canvas;
    private CanvasGroup group;
    private RectTransform bannerRect;
    private Image dim;
    private Image band;
    private Image topRule;
    private Image bottomRule;
    private TMP_Text title;
    private TMP_Text subtitle;

    private float phaseStartedAt;
    private float holdSeconds;
    private float fadeOutSeconds = DefaultFadeOutSeconds;
    private float dimTarget;
    private bool built;
    private bool showing;
    private bool fadingOut;

    public bool IsShowing => showing;

    public static CutsceneBannerUI Create(Transform parent)
    {
        var host = new GameObject("Cutscene Banner UI");
        host.transform.SetParent(parent, false);
        return host.AddComponent<CutsceneBannerUI>();
    }

    /// <summary>Shows a headline banner and fades it out on its own after the hold.</summary>
    public void Show(string headline, string message, Color color, float hold, float dimAmount)
    {
        EnsureBuilt();
        if (!built) return;

        title.text = headline;
        title.color = color;
        subtitle.text = message;
        subtitle.color = Color.Lerp(color, Color.white, 0.6f);
        subtitle.color = new Color(subtitle.color.r, subtitle.color.g, subtitle.color.b, 0.92f);
        topRule.color = new Color(color.r, color.g, color.b, 0.95f);
        bottomRule.color = new Color(color.r, color.g, color.b, 0.6f);
        band.color = new Color(0.02f, 0.07f, 0.05f, 0.62f);
        dimTarget = Mathf.Clamp01(dimAmount);
        dim.color = new Color(0f, 0f, 0f, 0f);

        fadeOutSeconds = DefaultFadeOutSeconds;
        holdSeconds = Mathf.Max(FadeInSeconds + 0.1f, hold);
        phaseStartedAt = Time.unscaledTime;
        showing = true;
        fadingOut = false;
        group.alpha = 0f;
        bannerRect.localScale = Vector3.one * 0.78f;
        canvas.enabled = true;
    }

    public void Hide(float fadeOut = DefaultFadeOutSeconds)
    {
        if (!showing || fadingOut) return;
        fadingOut = true;
        fadeOutSeconds = Mathf.Max(0.01f, fadeOut);
        phaseStartedAt = Time.unscaledTime;
    }

    public void HideImmediate()
    {
        showing = false;
        fadingOut = false;
        if (group != null) group.alpha = 0f;
        if (canvas != null) canvas.enabled = false;
    }

    private void OnDisable()
    {
        HideImmediate();
    }

    private void Update()
    {
        if (!built || !showing) return;

        float elapsed = Time.unscaledTime - phaseStartedAt;
        if (fadingOut)
        {
            float fade = Mathf.Clamp01(elapsed / fadeOutSeconds);
            group.alpha = 1f - fade;
            if (fade >= 1f)
            {
                showing = false;
                canvas.enabled = false;
            }
            return;
        }

        float intro = Mathf.Clamp01(elapsed / FadeInSeconds);
        float eased = Mathf.SmoothStep(0f, 1f, intro);
        group.alpha = eased;
        // A small overshoot makes the two-word headline land like a stamped sign.
        bannerRect.localScale = Vector3.one *
            (Mathf.Lerp(0.78f, 1f, eased) + 0.07f * Mathf.Sin(intro * Mathf.PI));
        dim.color = new Color(0f, 0f, 0f, dimTarget * eased);

        float holdTime = elapsed - FadeInSeconds;
        if (holdTime > 0f)
        {
            bannerRect.localScale = Vector3.one * (1f + 0.012f * Mathf.Sin(holdTime * 5f));
            title.rectTransform.localScale = Vector3.one * (1f + 0.02f * Mathf.Sin(holdTime * 7f));
        }

        if (elapsed >= holdSeconds) Hide();
    }

    private void EnsureBuilt()
    {
        if (built) return;

        var font = ResolveFont();
        if (font == null)
        {
            Debug.LogWarning("CutsceneBannerUI could not find a TextMeshPro font asset; the cutscene banner is disabled.", this);
            return;
        }

        var canvasObject = new GameObject("Overlay Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasObject.transform.SetParent(transform, false);
        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;

        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(ResponsiveLayout.ReferenceWidth, ResponsiveLayout.ReferenceHeight);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = ResponsiveLayout.MatchWidthOrHeight;

        group = canvasObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;

        var safeAreaObject = new GameObject("Safe Area", typeof(RectTransform));
        var safeArea = safeAreaObject.GetComponent<RectTransform>();
        safeArea.SetParent(canvasObject.transform, false);
        safeArea.anchorMin = Vector2.zero;
        safeArea.anchorMax = Vector2.one;
        safeArea.offsetMin = Vector2.zero;
        safeArea.offsetMax = Vector2.zero;
        // Added after the stretch anchors so the first safe-area pass keeps them.
        safeAreaObject.AddComponent<SafeAreaUI>();

        dim = CreateImage("Cinematic Dim", safeArea, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        dim.color = new Color(0f, 0f, 0f, 0f);

        bannerRect = new GameObject("Banner", typeof(RectTransform)).GetComponent<RectTransform>();
        bannerRect.SetParent(safeArea, false);
        bannerRect.anchorMin = new Vector2(0.04f, 0.60f);
        bannerRect.anchorMax = new Vector2(0.96f, 0.86f);
        bannerRect.offsetMin = Vector2.zero;
        bannerRect.offsetMax = Vector2.zero;

        band = CreateImage("Banner Backing", bannerRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        band.color = new Color(0.02f, 0.07f, 0.05f, 0.62f);

        topRule = CreateImage("Top Rule", bannerRect, new Vector2(0.06f, 0.90f), new Vector2(0.94f, 0.935f),
            Vector2.zero, Vector2.zero);
        bottomRule = CreateImage("Bottom Rule", bannerRect, new Vector2(0.16f, 0.055f), new Vector2(0.84f, 0.075f),
            Vector2.zero, Vector2.zero);

        title = CreateText("Headline", bannerRect, font, new Vector2(0.04f, 0.30f), new Vector2(0.96f, 0.92f),
            40f, 150f, FontStyles.Bold);
        title.characterSpacing = 6f;
        subtitle = CreateText("Message", bannerRect, font, new Vector2(0.05f, 0.06f), new Vector2(0.95f, 0.32f),
            20f, 46f, FontStyles.Normal);
        subtitle.characterSpacing = 1f;

        built = true;
        canvas.enabled = false;
    }

    private static TMP_FontAsset ResolveFont()
    {
        var existing = FindAnyObjectByType<TMP_Text>(FindObjectsInactive.Include);
        if (existing != null && existing.font != null) return existing.font;
        return TMP_Settings.defaultFontAsset;
    }

    private static Image CreateImage(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin, Vector2 offsetMax)
    {
        var image = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image))
            .GetComponent<Image>();
        var rect = image.rectTransform;
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        image.raycastTarget = false;
        return image;
    }

    private static TMP_Text CreateText(string name, Transform parent, TMP_FontAsset font,
        Vector2 anchorMin, Vector2 anchorMax, float minSize, float maxSize, FontStyles style)
    {
        var text = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI))
            .GetComponent<TMP_Text>();
        var rect = text.rectTransform;
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        text.font = font;
        text.alignment = TextAlignmentOptions.Center;
        text.enableAutoSizing = true;
        text.fontSizeMin = minSize;
        text.fontSizeMax = maxSize;
        text.fontStyle = style;
        text.raycastTarget = false;
        var shadow = text.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.65f);
        shadow.effectDistance = new Vector2(0f, -5f);
        return text;
    }
}
