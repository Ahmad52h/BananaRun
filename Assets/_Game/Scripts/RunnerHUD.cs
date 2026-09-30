using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class RunnerHUD : MonoBehaviour
{
    [SerializeField] private PlayerHealth health;
    [SerializeField] private TMP_Text heartsText;
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private CoconutThrower coconutThrower;
    [SerializeField] private Button throwButton;
    [Header("Heart Icons")]
    [SerializeField] private Image[] heartIcons;

    [SerializeField] private Color fullHeartColor = Color.white;

    [SerializeField]
    private Color emptyHeartColor =
        new Color(0.25f, 0.25f, 0.25f, 0.45f);

    [Header("Result")]
    [SerializeField] private TMP_Text resultText;

    private bool restarting;

    private void Awake()
    {
        gameOverPanel.SetActive(false);
    }

    private void OnEnable()
    {
        health.HeartsChanged += UpdateHearts;
        health.Died += ShowGameOver;
    }

    private void Start()
    {
        UpdateHearts(health.Hearts);
    }

    private void Update()
    {
        heartsText.color =
            health.IsProtected && !health.IsRunOver
            ? Color.yellow
            : Color.white;
        throwButton.interactable = coconutThrower.CanThrow;
    }

    private void OnDisable()
    {
        health.HeartsChanged -= UpdateHearts;
        health.Died -= ShowGameOver;
    }

    private void UpdateHearts(int hearts)
    {
        if (heartsText != null)
        {
            heartsText.text =
                $"Hearts: {hearts}/{health.MaxHearts}";
        }

        if (heartIcons == null)
            return;

        for (int i = 0; i < heartIcons.Length; i++)
        {
            Image icon = heartIcons[i];

            if (icon == null)
                continue;

            icon.color = i < hearts
                ? fullHeartColor
                : emptyHeartColor;
        }
    }

    private void ShowGameOver()
    {
        resultText.text = "GAME OVER";
        gameOverPanel.SetActive(true);
    }

    public void ShowFinishResult(
        bool won,
        int collected,
        int required)
    {
        string title = won
            ? "LEVEL COMPLETE!"
            : "NOT ENOUGH BANANAS";

        resultText.text =
            $"{title}\nBananas: {collected}/{required}";

        gameOverPanel.SetActive(true);
    }

    public void Retry()
    {
        if (restarting)
            return;

        restarting = true;

        SceneManager.LoadScene(
            SceneManager.GetActiveScene().name
        );
    }
}
