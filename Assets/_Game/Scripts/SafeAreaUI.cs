using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class SafeAreaUI : MonoBehaviour
{
    private RectTransform rectTransform;
    private Rect previousSafeArea;
    private Vector2Int previousScreenSize;
    private bool hasApplied;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        ResponsiveLayout.ConfigureAllCanvasScalers();
    }

    private void OnEnable()
    {
        rectTransform = GetComponent<RectTransform>();
        ResponsiveLayout.ConfigureAllCanvasScalers();
        ApplySafeArea();
    }

    private void Update()
    {
        int width = Screen.width;
        int height = Screen.height;
        Rect safeArea = Screen.safeArea;

        if (!hasApplied ||
            safeArea != previousSafeArea ||
            width != previousScreenSize.x ||
            height != previousScreenSize.y)
        {
            ApplySafeArea();
        }
    }

    private void OnRectTransformDimensionsChange()
    {
        if (!isActiveAndEnabled || rectTransform == null)
            return;

        ApplySafeArea();
    }

    private void ApplySafeArea()
    {
        if (rectTransform == null)
            rectTransform = GetComponent<RectTransform>();

        int width = Screen.width;
        int height = Screen.height;
        if (width <= 0 || height <= 0)
            return;

        Rect safeArea = Screen.safeArea;
        if (safeArea.width <= 0f || safeArea.height <= 0f)
            safeArea = new Rect(0f, 0f, width, height);

        Vector2 minimum = new Vector2(
            Mathf.Clamp01(safeArea.xMin / width),
            Mathf.Clamp01(safeArea.yMin / height));
        Vector2 maximum = new Vector2(
            Mathf.Clamp01(safeArea.xMax / width),
            Mathf.Clamp01(safeArea.yMax / height));

        // A malformed or zero-sized safe area must never collapse the UI.
        if (maximum.x <= minimum.x || maximum.y <= minimum.y)
        {
            minimum = Vector2.zero;
            maximum = Vector2.one;
        }

        bool alreadyApplied =
            rectTransform.anchorMin == minimum &&
            rectTransform.anchorMax == maximum &&
            rectTransform.offsetMin == Vector2.zero &&
            rectTransform.offsetMax == Vector2.zero;

        if (!alreadyApplied)
        {
            rectTransform.anchorMin = minimum;
            rectTransform.anchorMax = maximum;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }

        previousSafeArea = safeArea;
        previousScreenSize = new Vector2Int(width, height);
        hasApplied = true;
    }
}

/// <summary>
/// Keeps every canvas on the same mobile scale regardless of scene load order.
/// The scenes are still editable normally; this only applies the authored
/// reference resolution and balanced width/height match at runtime.
/// </summary>
public static class ResponsiveLayout
{
    public const float ReferenceWidth = 1080f;
    public const float ReferenceHeight = 1920f;
    public const float MatchWidthOrHeight = 0.5f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        ConfigureAllCanvasScalers();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ConfigureAllCanvasScalers();
    }

    public static void ConfigureAllCanvasScalers()
    {
        CanvasScaler[] scalers = Object.FindObjectsByType<CanvasScaler>(FindObjectsInactive.Include);
        for (int i = 0; i < scalers.Length; i++)
        {
            CanvasScaler scaler = scalers[i];
            if (scaler == null)
                continue;

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = MatchWidthOrHeight;
        }
    }
}
