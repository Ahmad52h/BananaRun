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
    private bool _isApplying;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
    }

    private void OnEnable()
    {
        if (rectTransform == null)
            rectTransform = GetComponent<RectTransform>();
        ApplySafeArea();
    }

    private void Update()
    {
        if (_isApplying)
            return;

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
        // Setting anchors/offsets below can trigger another layout callback.
        // The guard makes that nested callback a no-op, preventing a feedback loop.
        if (_isApplying || !isActiveAndEnabled || rectTransform == null)
            return;

        ApplySafeArea();
    }

    private void ApplySafeArea()
    {
        if (_isApplying)
            return;

        if (rectTransform == null)
            rectTransform = GetComponent<RectTransform>();
        if (rectTransform == null)
            return;

        int width = Screen.width;
        int height = Screen.height;
        if (width <= 0 || height <= 0)
            return;

        _isApplying = true;
        try
        {
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
                NearlyEqual(rectTransform.anchorMin, minimum) &&
                NearlyEqual(rectTransform.anchorMax, maximum) &&
                NearlyEqual(rectTransform.offsetMin, Vector2.zero) &&
                NearlyEqual(rectTransform.offsetMax, Vector2.zero);

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
        finally
        {
            _isApplying = false;
        }
    }

    private static bool NearlyEqual(Vector2 a, Vector2 b)
    {
        return Mathf.Abs(a.x - b.x) <= 0.0001f &&
               Mathf.Abs(a.y - b.y) <= 0.0001f;
    }
}

/// <summary>
/// Keeps every canvas on the same mobile scale regardless of scene load order.
/// This is the single place that writes CanvasScaler settings; callers only need
/// the scene-load hook and should not repeatedly dirty the Canvas during layout.
/// </summary>
public static class ResponsiveLayout
{
    public const float ReferenceWidth = 1080f;
    public const float ReferenceHeight = 1920f;
    public const float MatchWidthOrHeight = 0.5f;

    private static bool isConfiguring;
    private static bool sceneLoadedSubscribed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        isConfiguring = false;
        sceneLoadedSubscribed = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        EnsureSceneLoadedSubscription();
        ConfigureAllCanvasScalers();
    }

    private static void EnsureSceneLoadedSubscription()
    {
        if (sceneLoadedSubscribed)
            return;

        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        sceneLoadedSubscribed = true;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ConfigureAllCanvasScalers();
    }

    public static void ConfigureAllCanvasScalers()
    {
        if (isConfiguring)
            return;

        isConfiguring = true;
        try
        {
            Vector2 referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
            CanvasScaler[] scalers = Object.FindObjectsByType<CanvasScaler>(FindObjectsInactive.Include);

            for (int i = 0; i < scalers.Length; i++)
            {
                CanvasScaler scaler = scalers[i];
                if (scaler == null)
                    continue;

                // Do not touch a property that already has the target value.
                // Reassigning these dirtied the Canvas and could retrigger layout
                // while a new scene was still activating.
                if (scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize)
                    scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;

                if (scaler.referenceResolution != referenceResolution)
                    scaler.referenceResolution = referenceResolution;

                if (scaler.screenMatchMode != CanvasScaler.ScreenMatchMode.MatchWidthOrHeight)
                    scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;

                if (!Mathf.Approximately(scaler.matchWidthOrHeight, MatchWidthOrHeight))
                    scaler.matchWidthOrHeight = MatchWidthOrHeight;
            }
        }
        finally
        {
            isConfiguring = false;
        }
    }
}
