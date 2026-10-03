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
    [SerializeField] private CoconutThrower coconutThrower;
    [SerializeField] private UnityEngine.UI.Button throwButton;
    [SerializeField] private UnityEngine.UI.Image[] heartIcons;
    [SerializeField] private Color fullHeartColor = Color.white;
    [SerializeField] private Color emptyHeartColor = new Color(0.25f, 0.25f, 0.25f, 0.45f);
    [SerializeField] private TMP_Text resultText;
    private BananaInventory inventory;
    private PowerUpManager powers;
    private UnityEngine.UI.Button retryButton;
    private TMP_Text retryLabel, scoreText;
    private CanvasGroup resultGroup;
    private RectTransform card;
    private ResultStar[] stars;
    private bool restarting, resultShown;

    private void Awake()
    {
        if (health == null) health = FindAnyObjectByType<PlayerHealth>();
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
        card.anchorMin = new Vector2(0.08f, 0.22f);
        card.anchorMax = new Vector2(0.92f, 0.78f);
        card.offsetMin = card.offsetMax = Vector2.zero;
        card.GetComponent<UnityEngine.UI.Image>().color = new Color(0.055f, 0.16f, 0.085f, 0.98f);
        if (resultText != null)
        {
            resultText.transform.SetParent(card, false);
            Layout(resultText.rectTransform, 0.72f, 0.96f);
            resultText.alignment = TextAlignmentOptions.Center;
            resultText.enableAutoSizing = true;
            resultText.fontSizeMin = 24; resultText.fontSizeMax = 62;
            resultText.color = new Color(1f, 0.86f, 0.28f);
            resultText.raycastTarget = false;
        }
        scoreText = new GameObject("Result Score", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
        scoreText.transform.SetParent(card, false);
        if (resultText != null) scoreText.font = resultText.font;
        Layout(scoreText.rectTransform, 0.28f, 0.52f);
        scoreText.alignment = TextAlignmentOptions.Center;
        scoreText.enableAutoSizing = true; scoreText.fontSizeMin = 20; scoreText.fontSizeMax = 38;
        scoreText.color = Color.white; scoreText.raycastTarget = false;
        stars = new ResultStar[3];
        for (int i = 0; i < stars.Length; i++)
        {
            var star = new GameObject("Rating Star " + (i + 1), typeof(RectTransform), typeof(ResultStar)).GetComponent<ResultStar>();
            star.transform.SetParent(card, false);
            var rect = star.rectTransform;
            rect.anchorMin = new Vector2(0.28f + i * 0.16f, 0.55f);
            rect.anchorMax = new Vector2(0.40f + i * 0.16f, 0.70f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            star.raycastTarget = false;
            stars[i] = star;
        }
        if (retryButton != null)
        {
            retryButton.transform.SetParent(card, false);
            Layout((RectTransform)retryButton.transform, 0.07f, 0.23f);
            retryLabel = retryButton.GetComponentInChildren<TMP_Text>(true);
            retryButton.onClick = new UnityEngine.UI.Button.ButtonClickedEvent();
            retryButton.onClick.AddListener(Retry);
            if (retryButton.targetGraphic != null) retryButton.targetGraphic.raycastTarget = true;
            if (retryLabel != null) retryLabel.raycastTarget = false;
        }
        gameOverPanel.SetActive(false);
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
        if (health == null) return;
        health.HeartsChanged += UpdateHearts;
        health.Died += ShowGameOver;
    }
    private void Start() { if (health != null) UpdateHearts(health.Hearts); }
    private void Update()
    {
        if (health == null) return;
        if (heartsText != null) heartsText.color = health.IsProtected && !health.IsRunOver ? Color.yellow : Color.white;
        if (throwButton != null) throwButton.interactable = !resultShown && coconutThrower != null && coconutThrower.CanThrow;
        if (health.IsRunOver && !resultShown)
        {
            if (health.IsDead) ShowGameOver();
            else ShowFinishResult(true, inventory != null ? inventory.Count : 0, 0);
        }
    }
    private void OnDisable()
    {
        if (health == null) return;
        health.HeartsChanged -= UpdateHearts;
        health.Died -= ShowGameOver;
    }
    private void UpdateHearts(int hearts)
    {
        if (heartsText != null && health != null) heartsText.text = $"Hearts: {hearts}/{health.MaxHearts}";
        if (heartIcons != null) for (int i = 0; i < heartIcons.Length; i++)
            if (heartIcons[i] != null) heartIcons[i].color = i < hearts ? fullHeartColor : emptyHeartColor;
    }
    private void ShowGameOver() => ShowResult(false, inventory != null ? inventory.Count : 0);
    public void ShowFinishResult(bool won, int collected, int required) => ShowResult(health != null && !health.IsDead, collected);

    private void ShowResult(bool victory, int bananas)
    {
        if (resultShown || gameOverPanel == null) return;
        resultShown = true;
        if (resultText != null) resultText.text = victory ? "VICTORY!" : "GAME OVER";
        int score = bananas + (powers != null ? powers.BonusPoints : 0);
        if (scoreText != null) scoreText.text = $"Bananas Collected: {bananas}" + (victory ? $"\nTotal Score: {score}" : "");
        if (retryLabel != null) retryLabel.text = victory ? "Play Again" : "Retry";
        int rating = health != null ? Mathf.Clamp(health.Hearts, 1, 3) : 1;
        if (stars != null) for (int i = 0; i < stars.Length; i++)
        {
            stars[i].gameObject.SetActive(victory);
            stars[i].color = i < rating ? new Color(1f, 0.8f, 0.12f) : new Color(0.24f, 0.32f, 0.25f);
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
        if (retryButton != null)
        {
            retryButton.interactable = true;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(retryButton.gameObject);
        }
    }
    public void Retry()
    {
        if (restarting) return;
        restarting = true;
        if (retryButton != null) retryButton.interactable = false;
        Time.timeScale = 1f;
        AudioListener.pause = false;
        SceneManager.LoadScene("Level01", LoadSceneMode.Single);
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
