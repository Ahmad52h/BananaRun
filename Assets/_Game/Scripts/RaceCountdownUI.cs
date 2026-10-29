using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Non-interactive, responsive countdown with one punch per beat.</summary>
public sealed class RaceCountdownUI : MonoBehaviour
{
    private TMP_Text label;
    private CanvasGroup group;
    private float age;
    private bool go;
    public string CurrentText => label != null ? label.text : "";

    public static RaceCountdownUI Create(Transform parent)
    {
        var host = new GameObject("Race Countdown", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
        host.transform.SetParent(parent, false);
        host.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        host.GetComponent<Canvas>().sortingOrder = 60;
        var scaler = host.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0.5f;
        var ui = host.AddComponent<RaceCountdownUI>();
        ui.group = host.GetComponent<CanvasGroup>();
        ui.group.blocksRaycasts = false;
        ui.group.interactable = false;
        ui.label = ui.MakeLabel("Countdown", 260f, new Vector2(850f, 360f), Vector2.zero);
        return ui;
    }

    private TMP_Text MakeLabel(string objectName, float size, Vector2 dimensions, Vector2 position)
    {
        var host = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        host.transform.SetParent(transform, false);
        var text = host.GetComponent<TextMeshProUGUI>();
        text.font = TMP_Settings.defaultFontAsset;
        if (text.font == null) text.font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        text.fontSize = size;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        text.outlineColor = new Color(0.13f, 0.065f, 0.02f, 1f);
        text.outlineWidth = 0.2f;
        var rect = text.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = dimensions;
        rect.anchoredPosition = position;
        return text;
    }

    public void Show(string value, bool isGo)
    {
        if (label.text == value) return;
        label.text = value;
        go = isGo;
        age = 0f;
        group.alpha = 1f;
        label.color = isGo ? new Color(0.3f, 1f, 0.18f) : new Color(1f, 0.74f, 0.06f);
        label.enableVertexGradient = true;
        label.colorGradient = new VertexGradient(Color.white, Color.white,
            isGo ? new Color(0.4f, 0.9f, 0.2f) : new Color(1f, 0.48f, 0.02f),
            isGo ? new Color(0.4f, 0.9f, 0.2f) : new Color(1f, 0.48f, 0.02f));
        label.rectTransform.localScale = Vector3.one * 0.65f;
    }

    private void Update()
    {
        if (label == null || group.alpha <= 0f) return;
        age += Time.deltaTime;
        float t = Mathf.Clamp01(age / 0.42f);
        float punch = 1f + 0.3f * Mathf.Sin(t * Mathf.PI) * (1f - t);
        label.rectTransform.localScale = Vector3.one * (Mathf.Lerp(0.65f, 1f, Mathf.Clamp01(t * 5f)) * punch);
        if (go) group.alpha = 1f - Mathf.InverseLerp(0.4f, 0.65f, age);
    }

    public void Hide() { if (group != null) group.alpha = 0f; }
}
