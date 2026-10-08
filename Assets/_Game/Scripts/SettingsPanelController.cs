using System;
using UnityEngine;

public sealed class SettingsPanelController : MonoBehaviour
{
    [SerializeField] private UnityEngine.UI.Slider musicSlider;
    [SerializeField] private UnityEngine.UI.Slider sfxSlider;
    [SerializeField] private UnityEngine.UI.Toggle vibrationToggle;
    public static float MusicVolume => PlayerPrefs.GetFloat("BananaRun.Music", 1f);
    public static float SfxVolume => PlayerPrefs.GetFloat("BananaRun.SFX", 1f);
    public static bool VibrationEnabled => PlayerPrefs.GetInt("BananaRun.Vibration", 1) != 0;
    public static event Action Changed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Initialize() { Changed = null; AudioListener.volume = SfxVolume; }
    private void OnEnable()
    {
        musicSlider.SetValueWithoutNotify(MusicVolume);
        sfxSlider.SetValueWithoutNotify(SfxVolume);
        vibrationToggle.SetIsOnWithoutNotify(VibrationEnabled);
        musicSlider.onValueChanged.AddListener(SetMusic);
        sfxSlider.onValueChanged.AddListener(SetSfx);
        vibrationToggle.onValueChanged.AddListener(SetVibration);
    }
    private void OnDisable()
    {
        musicSlider.onValueChanged.RemoveListener(SetMusic);
        sfxSlider.onValueChanged.RemoveListener(SetSfx);
        vibrationToggle.onValueChanged.RemoveListener(SetVibration);
        Save();
    }
    public void SetMusic(float value) { PlayerPrefs.SetFloat("BananaRun.Music", Mathf.Clamp01(value)); Changed?.Invoke(); }
    public void SetSfx(float value) { PlayerPrefs.SetFloat("BananaRun.SFX", Mathf.Clamp01(value)); AudioListener.volume = SfxVolume; }
    public void SetVibration(bool value) { PlayerPrefs.SetInt("BananaRun.Vibration", value ? 1 : 0); }
    public static void Save() { PlayerPrefs.Save(); }
    private void OnApplicationPause(bool paused) { if (paused) Save(); }
}
