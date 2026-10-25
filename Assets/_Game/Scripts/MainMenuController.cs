using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class MainMenuController : MonoBehaviour
{
    [SerializeField] private Animator monkey;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject characterPanel;
    [SerializeField] private CanvasGroup fade;
    [SerializeField] private UnityEngine.UI.Button playButton;
    private bool loading;
    private ResponsiveMenuContent responsiveMenu;

    private void Awake()
    {
        SafeAreaUI safeArea = FindAnyObjectByType<SafeAreaUI>();
        if (safeArea != null)
            responsiveMenu = new ResponsiveMenuContent((RectTransform)safeArea.transform);
    }

    private void Start()
    {
        Time.timeScale = 1f;
        responsiveMenu?.Apply();
        if (monkey != null)
        {
            monkey.applyRootMotion = false;
            monkey.Play("Base Layer.Idle", 0, 0f);
            for (int i = 1; i < monkey.layerCount; i++) monkey.SetLayerWeight(i, 0f);
        }
        StartCoroutine(FadeIn());
    }

    private void Update()
    {
        // Do not touch UI layout while the scene transition is in flight.
        if (!loading)
            responsiveMenu?.ApplyIfNeeded();
    }

    private IEnumerator FadeIn()
    {
        fade.blocksRaycasts = true;
        for (float t = 0; t < 0.3f; t += Time.unscaledDeltaTime)
        { fade.alpha = 1f - t / 0.3f; yield return null; }
        fade.alpha = 0f;
        fade.blocksRaycasts = false;
    }

    public void OpenSettings() { if (!loading) settingsPanel.SetActive(true); }
    public void CloseSettings() { settingsPanel.SetActive(false); SettingsPanelController.Save(); }
    public void OpenCharacter() { if (!loading) characterPanel.SetActive(true); }
    public void CloseCharacter() { characterPanel.SetActive(false); }
    public void Play() { if (!loading) StartCoroutine(LoadLevel()); }

    private IEnumerator LoadLevel()
    {
        loading = true;
        Time.timeScale = 1f;
        AudioListener.pause = false;
        playButton.interactable = false;
        fade.blocksRaycasts = true;
        SettingsPanelController.Save();
        for (float t = 0; t < 0.25f; t += Time.unscaledDeltaTime)
        { fade.alpha = t / 0.25f; yield return null; }
        fade.alpha = 1f;

        // LoadSceneAsync is cooperative; yield its operation instead of
        // spinning or doing synchronous scene setup here.
        yield return null;
        yield return SceneManager.LoadSceneAsync("Level01", LoadSceneMode.Single);
    }
}

/// <summary>
/// Keeps the authored main-menu content inside the safe area on tall phones.
/// Full-screen modal backdrops are left untouched so they still cover the
/// entire display; only the actual menu items shrink.
/// </summary>
public sealed class ResponsiveMenuContent
{
    private const float ReferenceWidth = 1080f;
    private const float MinimumScale = 0.72f;

    private readonly RectTransform root;
    private readonly List<Entry> entries = new List<Entry>();
    private float lastWidth = -1f;
    private float lastScale = -1f;
    private bool _isApplying;

    private struct Entry
    {
        public RectTransform rect;
        public MenuButtonFeedback feedback;
        public Vector3 baseScale;
    }

    public ResponsiveMenuContent(RectTransform root)
    {
        this.root = root;
        CaptureChildren();
    }

    public void ApplyIfNeeded()
    {
        if (_isApplying || root == null)
            return;

        float width = root.rect.width;
        if (width <= 1f || Mathf.Abs(width - lastWidth) < 0.5f)
            return;

        Apply(width);
    }

    public void Apply()
    {
        if (_isApplying || root == null)
            return;

        Apply(root.rect.width);
    }

    private void Apply(float width)
    {
        if (_isApplying || root == null || width <= 1f)
            return;

        _isApplying = true;
        try
        {
            if (entries.Count == 0)
                CaptureChildren();
            if (entries.Count == 0)
                return;

            float scale = Mathf.Clamp(width / ReferenceWidth, MinimumScale, 1f);
            bool scaleChanged = !Mathf.Approximately(lastScale, scale);
            if (scaleChanged)
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    Entry entry = entries[i];
                    if (entry.rect == null)
                        continue;

                    if (entry.feedback != null)
                    {
                        // MenuButtonFeedback owns the button scale so its press/breathe
                        // animation keeps working; give it the responsive multiplier.
                        entry.feedback.ContentScale = scale;
                    }
                    else
                    {
                        entry.rect.localScale = entry.baseScale * scale;
                    }
                }

                lastScale = scale;
            }

            // Remember the width even when the clamped scale is unchanged so a
            // resize within the same clamp bucket does not dirty layout again.
            lastWidth = width;
        }
        finally
        {
            _isApplying = false;
        }
    }

    private void CaptureChildren()
    {
        entries.Clear();
        if (root == null)
            return;

        for (int i = 0; i < root.childCount; i++)
        {
            RectTransform child = root.GetChild(i) as RectTransform;
            if (child == null || IsFullStretch(child))
                continue;

            entries.Add(new Entry
            {
                rect = child,
                feedback = child.GetComponent<MenuButtonFeedback>(),
                baseScale = child.localScale
            });
        }
    }

    private static bool IsFullStretch(RectTransform rect)
    {
        return rect.anchorMin.x <= 0.001f && rect.anchorMax.x >= 0.999f &&
               rect.anchorMin.y <= 0.001f && rect.anchorMax.y >= 0.999f;
    }
}
