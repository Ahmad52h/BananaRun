using System.Collections;
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

    private void Start()
    {
        Time.timeScale = 1f;
        if (monkey != null)
        {
            monkey.applyRootMotion = false;
            monkey.Play("Base Layer.Idle", 0, 0f);
            for (int i = 1; i < monkey.layerCount; i++) monkey.SetLayerWeight(i, 0f);
        }
        StartCoroutine(FadeIn());
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
        playButton.interactable = false;
        fade.blocksRaycasts = true;
        SettingsPanelController.Save();
        for (float t = 0; t < 0.25f; t += Time.unscaledDeltaTime)
        { fade.alpha = t / 0.25f; yield return null; }
        fade.alpha = 1f;
        yield return SceneManager.LoadSceneAsync("Level01");
    }
}
