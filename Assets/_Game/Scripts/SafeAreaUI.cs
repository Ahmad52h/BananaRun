using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public class SafeAreaUI : MonoBehaviour
{
    private RectTransform rectTransform;
    private Rect previousSafeArea;
    private Vector2Int previousScreenSize;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
    }

    private void OnEnable()
    {
        ApplySafeArea();
    }

    private void Update()
    {
        if (Screen.safeArea != previousSafeArea ||
            Screen.width != previousScreenSize.x ||
            Screen.height != previousScreenSize.y)
        {
            ApplySafeArea();
        }
    }

    private void ApplySafeArea()
    {
        if (Screen.width <= 0 || Screen.height <= 0)
            return;

        Rect safeArea = Screen.safeArea;

        Vector2 minimum = safeArea.min;
        Vector2 maximum = safeArea.max;

        minimum.x /= Screen.width;
        minimum.y /= Screen.height;

        maximum.x /= Screen.width;
        maximum.y /= Screen.height;

        rectTransform.anchorMin = minimum;
        rectTransform.anchorMax = maximum;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;

        previousSafeArea = safeArea;
        previousScreenSize =
            new Vector2Int(Screen.width, Screen.height);
    }
}