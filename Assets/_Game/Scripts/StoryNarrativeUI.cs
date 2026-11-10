using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

/// <summary>Scene-owned comic framing. Built once; animation only changes cached value types.</summary>
public sealed class StoryNarrativeUI : MonoBehaviour
{
    public static readonly Color Gold = new Color(1f, 0.79f, 0.23f);
    public static readonly Color Ink = new Color(0.025f, 0.085f, 0.065f, 0.96f);
    private Canvas canvas;
    private CanvasGroup group;
    private RectTransform card;
    private TMP_Text title, caption, chapter;
    private readonly RectTransform[] progress = new RectTransform[3];
    private int beat;

    public static StoryNarrativeUI Create(Transform parent, UnityAction next, UnityAction skip)
    {
        var root = new GameObject("Royal Banana Story", typeof(RectTransform));
        root.transform.SetParent(parent, false);
        var ui = root.AddComponent<StoryNarrativeUI>();
        RectTransform safe = CreateCanvas(root.transform, 70, true, out ui.canvas);
        ui.group = ui.canvas.gameObject.AddComponent<CanvasGroup>();
        // Transparent scene-wide blocker protects the underlying gameplay and pause UI.
        Image("Input Shield", ui.canvas.transform, Vector2.zero, Vector2.one, new Color(0f, 0f, 0f, 0.04f)).raycastTarget = true;
        safe.SetAsLastSibling();
        Image("Top Ink", safe, new Vector2(0f, 0.83f), Vector2.one, Ink);
        Image("Top Gold Rule", safe, new Vector2(0.04f, 0.83f), new Vector2(0.96f, 0.833f), Gold);
        ui.chapter = Text("Chapter", safe, new Vector2(0.06f, 0.94f), new Vector2(0.94f, 0.98f), 30f, Gold);
        ui.title = Text("Story Title", safe, new Vector2(0.06f, 0.845f), new Vector2(0.94f, 0.94f), 68f, Color.white);
        ui.title.fontStyle = FontStyles.Bold;
        ui.card = Image("Caption Card", safe, new Vector2(0.04f, 0.035f), new Vector2(0.96f, 0.33f), Ink).rectTransform;
        Image("Caption Gold Rule", ui.card, new Vector2(0f, 0.98f), Vector2.one, Gold);
        ui.caption = Text("Story Caption", ui.card, new Vector2(0.055f, 0.42f), new Vector2(0.945f, 0.91f), 42f, new Color(1f, 0.96f, 0.81f));
        Text("Controls", ui.card, new Vector2(0.04f, 0.25f), new Vector2(0.96f, 0.39f), 25f, Gold).text =
            "SWIPE: Move / Jump / Slide   |   TAP: Coconuts & Power-ups";
        Button("Skip Story", ui.card, new Vector2(0.055f, 0.045f), new Vector2(0.43f, 0.215f), "SKIP", false, skip);
        Button("Next Story Beat", ui.card, new Vector2(0.48f, 0.045f), new Vector2(0.945f, 0.215f), "NEXT >>", true, next);
        for (int i = 0; i < 3; i++)
        {
            float left = 0.06f + i * 0.30f;
            var track = Image("Beat Progress", safe, new Vector2(left, 0.345f), new Vector2(left + 0.28f, 0.35f), Ink);
            ui.progress[i] = Image("Gold Fill", track.transform, Vector2.zero, Vector2.one, Gold).rectTransform;
        }
        EnsureEventSystem();
        return ui;
    }

    public void ShowBeat(int index, string headline, string story)
    {
        beat = index;
        title.text = headline;
        caption.text = story;
        chapter.text = index == 0 ? "BANANA RUN  /  THE LEGEND  /  01" :
            index == 1 ? "BANANA RUN  /  THE GUARDIAN  /  02" : "BANANA RUN  /  THE ESCAPE  /  03";
        canvas.enabled = true;
        for (int i = 0; i < progress.Length; i++)
            progress[i].anchorMax = new Vector2(i < index ? 1f : 0f, 1f);
        Animate(0f, 0f);
    }

    public void Animate(float age, float fraction)
    {
        float entrance = Mathf.SmoothStep(0f, 1f, age / 0.28f);
        group.alpha = entrance;
        card.anchoredPosition = new Vector2(0f, Mathf.Lerp(-45f, 0f, entrance));
        title.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.94f, 1f, entrance);
        progress[beat].anchorMax = new Vector2(Mathf.Clamp01(fraction), 1f);
    }

    public void Hide()
    {
        if (canvas == null || group == null) return;
        canvas.enabled = false;
        group.blocksRaycasts = false;
        group.interactable = false;
    }

    public static UnityEngine.UI.Button AddMenuButton(UnityEngine.UI.Button play, UnityAction action)
    {
        var button = Button("Story How To Play", play.transform.parent, new Vector2(0.15f, 0.006f),
            new Vector2(0.85f, 0.048f), "STORY / HOW TO PLAY", false, action);
        button.transform.SetSiblingIndex(play.transform.GetSiblingIndex() + 1);
        return button;
    }

    public static RectTransform CreateCanvas(Transform parent, int order, bool interactive, out Canvas canvas)
    {
        var host = new GameObject("Narrative Canvas", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
        host.transform.SetParent(parent, false);
        canvas = host.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = order;
        var scaler = host.GetComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0.5f;
        if (interactive) host.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        var safe = Rect("Safe Area", host.transform, Vector2.zero, Vector2.one);
        safe.gameObject.AddComponent<SafeAreaUI>();
        return safe;
    }

    public static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }

    public static UnityEngine.UI.Image Image(string name, Transform parent, Vector2 min, Vector2 max, Color color)
    {
        var image = Rect(name, parent, min, max).gameObject.AddComponent<UnityEngine.UI.Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    public static TMP_Text Text(string name, Transform parent, Vector2 min, Vector2 max, float size, Color color)
    {
        var text = Rect(name, parent, min, max).gameObject.AddComponent<TextMeshProUGUI>();
        text.font = TMP_Settings.defaultFontAsset;
        if (text.font == null) text.font = RuntimeResourceCache.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        text.fontSize = size;
        text.enableAutoSizing = true;
        text.fontSizeMin = size * 0.5f;
        text.fontSizeMax = size;
        text.color = color;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        return text;
    }

    private static UnityEngine.UI.Button Button(string name, Transform parent, Vector2 min, Vector2 max,
        string label, bool gold, UnityAction action)
    {
        var image = Image(name, parent, min, max, gold ? Gold : new Color(0.12f, 0.25f, 0.18f));
        image.raycastTarget = true;
        var button = image.gameObject.AddComponent<UnityEngine.UI.Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(action);
        Text("Label", image.transform, new Vector2(0.03f, 0.04f), new Vector2(0.97f, 0.96f), 32f,
            gold ? Ink : Gold).text = label;
        var colors = button.colors;
        colors.pressedColor = new Color(0.7f, 0.8f, 0.65f);
        button.colors = colors;
        return button;
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current == null && FindAnyObjectByType<EventSystem>() == null)
            new GameObject("Narrative Event System", typeof(EventSystem), typeof(InputSystemUIInputModule));
    }
}
