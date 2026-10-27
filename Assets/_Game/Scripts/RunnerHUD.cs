using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public class RunnerHUD : MonoBehaviour
{
    [SerializeField] private PlayerHealth health;
    [SerializeField] private TMP_Text heartsText;
    [SerializeField] private GameObject gameOverPanel;
    [Tooltip("Pause before the GAME OVER overlay so the gorilla catch can play.")]
    [SerializeField, Min(0f)] private float gameOverRevealDelay = 1.6f;
    [SerializeField] private CoconutThrower coconutThrower;
    [SerializeField] private UnityEngine.UI.Button throwButton;
    [SerializeField] private UnityEngine.UI.Image[] heartIcons;
    [SerializeField] private Color fullHeartColor = Color.white;
    [SerializeField] private Color emptyHeartColor = new Color(0.25f, 0.25f, 0.25f, 0.45f);
    [SerializeField] private TMP_Text resultText;
    [Header("Tropical Results")]
    [SerializeField] private Sprite roundedPanelSprite;
    [SerializeField] private Sprite bananaResultIcon;
    [SerializeField] private AudioClip victorySound;
    [SerializeField] private AudioClip gameOverSound;
    [SerializeField] private AudioSource gameOverSource;
    [SerializeField, Range(0f, 1f)] private float gameOverVolume = 0.6f;
    [Header("Coconut Inventory")]
    [SerializeField] private TMP_Text coconutCountText;
    [SerializeField] private RectTransform coconutBadge;
    private int displayedCoconuts;
    private Coroutine coconutPop;
    private Coroutine gameOverReveal;
    private BananaInventory inventory;
    private PowerUpManager powers;
    private UnityEngine.UI.Button retryButton;
    private UnityEngine.UI.Button homeButton;
    private TMP_Text subtitle;
    private TMP_Text retryLabel, scoreText;
    private CanvasGroup resultGroup;
    private RectTransform card;
    private ResultStar[] stars;
    private bool restarting, resultShown;

    private void Awake()
    {
        // CanvasScaler configuration is owned by ResponsiveLayout's single
        // scene-load hook. Reapplying it here dirtied the Canvas during the
        // scene transition and could retrigger layout callbacks.
        if (health == null) health = FindAnyObjectByType<PlayerHealth>();
        if (gameOverSource == null) gameOverSource = gameObject.AddComponent<AudioSource>();
        gameOverSource.playOnAwake = false;
        gameOverSource.loop = false;
        gameOverSource.spatialBlend = 0f;
        if (health != null)
        {
            inventory = health.GetComponent<BananaInventory>();
            powers = health.GetComponent<PowerUpManager>();
            if (coconutThrower == null) coconutThrower = health.GetComponent<CoconutThrower>();
        }
        if (gameOverPanel == null)
        {
            var existing = transform.Find("GameOverPanel");
            if (existing != null) gameOverPanel = existing.gameObject;
        }
        if (gameOverPanel == null) return;
        if (resultText == null) resultText = gameOverPanel.GetComponentInChildren<TMP_Text>(true);
        retryButton = gameOverPanel.GetComponentInChildren<UnityEngine.UI.Button>(true);
        resultGroup = gameOverPanel.GetComponent<CanvasGroup>();
        if (resultGroup == null) resultGroup = gameOverPanel.AddComponent<CanvasGroup>();
        var canvas = gameOverPanel.GetComponentInParent<Canvas>();
        if (canvas != null && canvas.GetComponent<UnityEngine.UI.GraphicRaycaster>() == null)
            canvas.gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        card = new GameObject("Result Card", typeof(RectTransform), typeof(UnityEngine.UI.Image)).GetComponent<RectTransform>();
        card.SetParent(gameOverPanel.transform, false);
        card.anchorMin = new Vector2(0.06f, 0.20f);
        card.anchorMax = new Vector2(0.94f, 0.80f);
        card.offsetMin = card.offsetMax = Vector2.zero;
        StyleImage(card.GetComponent<UnityEngine.UI.Image>(), new Color(1f, 0.957f, 0.788f));
        var backdrop = gameOverPanel.GetComponent<UnityEngine.UI.Image>();
        if (backdrop != null) { backdrop.color = new Color(0.025f, 0.10f, 0.08f, 0.84f); backdrop.raycastTarget = true; }
        if (resultText != null)
        {
            resultText.transform.SetParent(card, false);
            Layout(resultText.rectTransform, 0.78f, 0.94f);
            resultText.alignment = TextAlignmentOptions.Center;
            resultText.enableAutoSizing = true;
            resultText.fontSizeMin = 24; resultText.fontSizeMax = 62;
            resultText.color = new Color(0.09f, 0.24f, 0.20f);
            resultText.fontStyle = FontStyles.Bold;
            resultText.raycastTarget = false;
        }
        scoreText = new GameObject("Result Score", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
        scoreText.transform.SetParent(card, false);
        if (resultText != null) scoreText.font = resultText.font;
        Layout(scoreText.rectTransform, 0.40f, 0.54f);
        scoreText.alignment = TextAlignmentOptions.Center;
        scoreText.enableAutoSizing = true; scoreText.fontSizeMin = 20; scoreText.fontSizeMax = 38;
        scoreText.color = new Color(0.09f, 0.24f, 0.20f); scoreText.raycastTarget = false;
        subtitle = new GameObject("Result Encouragement", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
        subtitle.transform.SetParent(card, false);
        if (resultText != null) subtitle.font = resultText.font;
        Layout(subtitle.rectTransform, 0.70f, 0.78f);
        subtitle.alignment = TextAlignmentOptions.Center; subtitle.enableAutoSizing = true;
        subtitle.fontSizeMin = 20; subtitle.fontSizeMax = 30;
        subtitle.color = new Color(0.09f, 0.24f, 0.20f); subtitle.raycastTarget = false;
        var emblem = new GameObject("Banana Emblem", typeof(RectTransform), typeof(UnityEngine.UI.Image)).GetComponent<UnityEngine.UI.Image>();
        emblem.transform.SetParent(card, false); emblem.sprite = bananaResultIcon; emblem.preserveAspect = true; emblem.raycastTarget = false;
        Layout(emblem.rectTransform, 0.55f, 0.69f);
        emblem.gameObject.SetActive(bananaResultIcon != null);
        stars = new ResultStar[3];
        for (int i = 0; i < stars.Length; i++)
        {
            var star = new GameObject("Rating Star " + (i + 1), typeof(RectTransform), typeof(CanvasRenderer), typeof(ResultStar)).GetComponent<ResultStar>();
            star.transform.SetParent(card, false);
            var rect = star.rectTransform;
            rect.anchorMin = new Vector2(0.28f + i * 0.16f, 0.55f);
            rect.anchorMax = new Vector2(0.40f + i * 0.16f, 0.69f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            star.raycastTarget = false;
            stars[i] = star;
        }
        if (retryButton != null)
        {
            retryButton.transform.SetParent(card, false);
            Layout((RectTransform)retryButton.transform, 0.23f, 0.37f);
            retryLabel = retryButton.GetComponentInChildren<TMP_Text>(true);
            retryButton.onClick = new UnityEngine.UI.Button.ButtonClickedEvent();
            retryButton.onClick.AddListener(Retry);
            if (retryButton.targetGraphic != null) retryButton.targetGraphic.raycastTarget = true;
            if (retryLabel != null) retryLabel.raycastTarget = false;
            StyleButton(retryButton, new Color(1f, 0.83f, 0.286f), new Color(0.09f, 0.24f, 0.20f));
            if (Application.CanStreamedLevelBeLoaded("MainMenu"))
            {
                homeButton = Instantiate(retryButton, card); homeButton.name = "Home Button";
                Layout((RectTransform)homeButton.transform, 0.06f, 0.18f);
                homeButton.onClick = new UnityEngine.UI.Button.ButtonClickedEvent();
                homeButton.onClick.AddListener(Home);
                var label = homeButton.GetComponentInChildren<TMP_Text>(); if (label != null) label.text = "HOME";
                StyleButton(homeButton, new Color(0.09f, 0.24f, 0.20f), new Color(1f, 0.957f, 0.788f));
            }
        }
        gameOverPanel.SetActive(false);
    }

    private void StyleImage(UnityEngine.UI.Image image, Color color)
    {
        image.color = color;
        if (roundedPanelSprite != null) { image.sprite = roundedPanelSprite; image.type = UnityEngine.UI.Image.Type.Sliced; }
    }
    private void StyleButton(UnityEngine.UI.Button button, Color background, Color foreground)
    {
        var image = button.GetComponent<UnityEngine.UI.Image>();
        if (image != null) { StyleImage(image, background); image.raycastTarget = true; button.targetGraphic = image; }
        var label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            Layout(label.rectTransform, 0.08f, 0.92f); label.color = foreground; label.fontStyle = FontStyles.Bold;
            label.enableAutoSizing = true; label.fontSizeMin = 26; label.fontSizeMax = 44;
            label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false;
        }
        if (button.GetComponent<MenuButtonFeedback>() == null) button.gameObject.AddComponent<MenuButtonFeedback>();
        var colors = button.colors; colors.normalColor = colors.highlightedColor = Color.white;
        colors.pressedColor = new Color(.82f,.86f,.78f); button.colors = colors;
    }

    private static void Layout(RectTransform rect, float bottom, float top)
    {
        rect.anchorMin = new Vector2(0.08f, bottom);
        rect.anchorMax = new Vector2(0.92f, top);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    private void OnEnable()
    {
        if (coconutThrower != null)
        {
            displayedCoconuts = coconutThrower.CurrentCoconuts;
            UpdateCoconuts(displayedCoconuts);
            coconutThrower.CountChanged += UpdateCoconuts;
        }
        if (health == null) return;
        health.HeartsChanged += UpdateHearts;
        health.RunFailed += ShowGameOver;
    }
    private void Start() { if (health != null) UpdateHearts(health.Hearts); }
    private void Update()
    {
        if (health == null) return;
        if (heartsText != null) heartsText.color = health.IsProtected && !health.IsRunOver ? Color.yellow : Color.white;
        if (throwButton != null) throwButton.interactable = !resultShown && coconutThrower != null && coconutThrower.CanThrow;
        if (health.HasFailed && !resultShown && gameOverReveal == null) ShowGameOver();
    }
    private void OnDisable()
    {
        if (coconutThrower != null) coconutThrower.CountChanged -= UpdateCoconuts;
        if (coconutPop != null) StopCoroutine(coconutPop);
        coconutPop = null;
        if (gameOverReveal != null) StopCoroutine(gameOverReveal);
        gameOverReveal = null;
        if (coconutBadge != null) coconutBadge.localScale = Vector3.one;
        if (health == null) return;
        health.HeartsChanged -= UpdateHearts;
        health.RunFailed -= ShowGameOver;
    }
    private void UpdateHearts(int hearts)
    {
        if (heartsText != null && health != null) heartsText.text = $"Hearts: {hearts}/{health.MaxHearts}";
        if (heartIcons != null) for (int i = 0; i < heartIcons.Length; i++)
            if (heartIcons[i] != null) heartIcons[i].color = i < hearts ? fullHeartColor : emptyHeartColor;
    }
    private void UpdateCoconuts(int count)
    {
        if (coconutCountText != null) coconutCountText.text = "x" + count;
        if (count > displayedCoconuts && coconutBadge != null)
        {
            if (coconutPop != null) StopCoroutine(coconutPop);
            coconutPop = StartCoroutine(PopCoconutCount());
        }
        displayedCoconuts = count;
    }
    private IEnumerator PopCoconutCount()
    {
        for (float elapsed = 0; elapsed < 0.32f; elapsed += Time.unscaledDeltaTime)
        {
            float t = elapsed / 0.32f;
            coconutBadge.localScale = Vector3.one * (1f + 0.16f * Mathf.Sin(t * Mathf.PI) * (1f - 0.35f * t));
            yield return null;
        }
        coconutBadge.localScale = Vector3.one;
        coconutPop = null;
    }
    private void ShowGameOver()
    {
        if (resultShown || gameOverPanel == null || gameOverReveal != null) return;
        gameOverReveal = StartCoroutine(ShowGameOverAfterCatch());
    }

    private IEnumerator ShowGameOverAfterCatch()
    {
        float elapsed = 0f;
        while (elapsed < gameOverRevealDelay)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        gameOverReveal = null;
        if (!resultShown)
        {
            ShowResult(false, inventory != null ? inventory.Count : 0);
            if (health != null && health.FailureReason == PlayerHealth.FailureCause.RivalFinishedFirst)
            {
                if (resultText != null) resultText.text = "RIVAL WINS!";
                if (subtitle != null) subtitle.text = "The rival reached the finish first. Race again!";
            }
            else if (health != null && health.FailureReason == PlayerHealth.FailureCause.NotEnoughBananas)
            {
                if (resultText != null) resultText.text = "NOT ENOUGH BANANAS";
                if (subtitle != null) subtitle.text = "Collect enough bananas before reaching the basket.";
            }
        }
    }

    public void ShowFinishResult(
        bool won, int collected, int required,
        bool quotaEnabled = false, int quotaTarget = 0) =>
        ShowResult(
            won && collected >= required && health != null && !health.HasFailed,
            collected, required, quotaEnabled, quotaTarget);

    public void ShowCompetitiveFinishResult(bool won, int collected, int rivalCollected, int required,
        bool quotaEnabled, int quotaTarget)
    {
        ShowFinishResult(won, collected, required, quotaEnabled, quotaTarget);
        if (health == null || health.IsDead) return;
        if (resultText != null)
            resultText.text = won ? "YOU WIN!" : collected < required ? "NOT ENOUGH BANANAS" : "RIVAL WINS!";
        if (subtitle != null)
            subtitle.text = won ? "You reached the basket first with enough bananas!" : collected < required
                ? $"Collect {required} bananas and race to the basket." : "The rival filled its basket first. Race again!";
        if (scoreText != null)
        {
            scoreText.text = $"BASKET   YOU {collected}/{required}   RIVAL {rivalCollected}/{required}";
            if (won) scoreText.text += $"\nTotal Score: {collected + (powers != null ? powers.BonusPoints : 0)}";
        }
    }

    private void ShowResult(
        bool victory, int bananas, int required = 0,
        bool quotaEnabled = false, int quotaTarget = 0)
    {
        if (resultShown || gameOverPanel == null) return;
        if (gameOverReveal != null)
        {
            StopCoroutine(gameOverReveal);
            gameOverReveal = null;
        }
        resultShown = true;
        if (!victory && gameOverSound != null)
        {
            gameOverSource.Stop();
            gameOverSource.clip = gameOverSound;
            gameOverSource.pitch = 1f;
            gameOverSource.volume = gameOverVolume;
            gameOverSource.Play();
        }
        bool shortOnBananas = required > 0 && bananas < required && health != null && !health.IsDead;
        bool quotaActive = quotaEnabled && quotaTarget > 0;
        bool goalComplete = quotaActive && bananas >= quotaTarget;
        if (resultText != null)
        {
            if (victory && goalComplete) resultText.text = "GOAL COMPLETE!";
            else if (victory) resultText.text = quotaActive ? "NICE RUN!" : "VICTORY!";
            else resultText.text = shortOnBananas ? "NOT ENOUGH BANANAS" : "GAME OVER";
        }
        int score = bananas + (powers != null ? powers.BonusPoints : 0);
        if (scoreText != null)
        {
            if (quotaActive)
            {
                scoreText.text = $"Bananas in Basket: {bananas} / {quotaTarget}";
                if (victory) scoreText.text += $"\nTotal Score: {score}";
            }
            else
            {
                scoreText.text = $"Bananas Collected: {bananas}" +
                    (shortOnBananas ? $" / {required}" : victory ? $"\nTotal Score: {score}" : "");
            }
        }
        if (retryLabel != null) retryLabel.text = victory ? "PLAY AGAIN" : "RESTART";
        if (subtitle != null)
        {
            if (victory && goalComplete) subtitle.text = "The celebration basket is full!";
            else if (shortOnBananas) subtitle.text = $"Collect {required} bananas to win.";
            else if (victory && quotaActive) subtitle.text =
                $"{Mathf.Max(0, quotaTarget - bananas)} more bananas for a full basket.";
            else subtitle.text = victory ? "You went bananas!" : "The jungle awaits another try.";
        }
        var emblem = card.Find("Banana Emblem"); if (emblem != null) emblem.gameObject.SetActive(!victory && bananaResultIcon != null);
        int rating = 1;
        if (quotaActive)
        {
            if (goalComplete) rating = 3;
            else if (bananas * 5 >= quotaTarget * 3) rating = 2;
        }
        else if (health != null) rating = Mathf.Clamp(health.Hearts, 1, 3);
        if (stars != null) for (int i = 0; i < stars.Length; i++)
        {
            stars[i].gameObject.SetActive(victory);
            stars[i].color = i < rating ? new Color(1f, 0.8f, 0.12f) : new Color(0.24f, 0.32f, 0.25f);
            stars[i].rectTransform.localScale = victory ? Vector3.zero : Vector3.one;
        }
        gameOverPanel.transform.SetAsLastSibling();
        gameOverPanel.SetActive(true);
        StartCoroutine(Reveal());
    }
    private IEnumerator Reveal()
    {
        resultGroup.alpha = 0f; resultGroup.blocksRaycasts = true; resultGroup.interactable = false;
        if (retryButton != null) retryButton.interactable = false;
        float elapsed = 0;
        while (elapsed < 0.35f)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0, 1, Mathf.Clamp01(elapsed / 0.35f));
            resultGroup.alpha = t;
            card.localScale = Vector3.one * Mathf.Lerp(0.88f, 1f, t);
            yield return null;
        }
        resultGroup.alpha = 1; card.localScale = Vector3.one; resultGroup.interactable = true;
        if (stars != null && stars[0].gameObject.activeSelf)
        {
            RunnerAudio.Instance?.PlayVictory(victorySound);
            for (int i = 0; i < stars.Length; i++) StartCoroutine(PopVictoryStar(stars[i].rectTransform, i * 0.14f));
        }
        if (retryButton != null)
        {
            retryButton.interactable = true;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(retryButton.gameObject);
        }
    }
    private IEnumerator PopVictoryStar(RectTransform star, float delay)
    {
        float elapsed = 0f;
        while (elapsed < delay) { elapsed += Time.unscaledDeltaTime; yield return null; }
        elapsed = 0f;
        const float duration = 0.26f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float scale = t < 0.65f
                ? Mathf.SmoothStep(0f, 1.18f, t / 0.65f)
                : Mathf.SmoothStep(1.18f, 1f, (t - 0.65f) / 0.35f);
            star.localScale = Vector3.one * scale;
            yield return null;
        }
        star.localScale = Vector3.one;
    }
    public void Retry()
    {
        if (restarting) return;
        restarting = true;
        if (retryButton != null) retryButton.interactable = false;
        ReleaseCatchForRestart();
        Time.timeScale = 1f;
        AudioListener.pause = false;
        SceneManager.LoadScene("Level01", LoadSceneMode.Single);
    }
    public void Home()
    {
        if (restarting || !Application.CanStreamedLevelBeLoaded("MainMenu")) return;
        restarting = true;
        if (homeButton != null) homeButton.interactable = false;
        ReleaseCatchForRestart();
        Time.timeScale = 1f;
        AudioListener.pause = false;
        SceneManager.LoadScene("MainMenu", LoadSceneMode.Single);
    }

    private static void ReleaseCatchForRestart()
    {
        GiantGorillaChaser chaser = FindAnyObjectByType<GiantGorillaChaser>();
        if (chaser != null) chaser.ReleaseCatch();
    }
}

/// <summary>Vector stars avoid depending on a font's star glyph.</summary>
public class ResultStar : UnityEngine.UI.MaskableGraphic
{
    protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper vh)
    {
        vh.Clear();
        var rect = GetPixelAdjustedRect();
        Vector2 center = rect.center;
        float radius = Mathf.Min(rect.width, rect.height) * 0.5f;
        vh.AddVert(center, color, Vector2.zero);
        for (int i = 0; i < 10; i++)
        {
            float angle = (90 + i * 36) * Mathf.Deg2Rad;
            float r = radius * (i % 2 == 0 ? 1f : 0.44f);
            vh.AddVert(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r, color, Vector2.zero);
        }
        for (int i = 0; i < 10; i++) vh.AddTriangle(0, i + 1, (i + 1) % 10 + 1);
    }
}
