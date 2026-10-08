using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(UnityEngine.UI.Button))]
public sealed class MenuButtonFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [SerializeField] private bool breathe;
    private bool pressed;
    private UnityEngine.UI.Button button;
    private void Awake() { button = GetComponent<UnityEngine.UI.Button>(); }
    private void Update()
    {
        float target = pressed ? 0.95f : breathe && button.interactable ? 1f + 0.018f * Mathf.Sin(Time.unscaledTime * 2.2f) : 1f;
        transform.localScale = Vector3.Lerp(transform.localScale, Vector3.one * target, 18f * Time.unscaledDeltaTime);
    }
    public void OnPointerDown(PointerEventData data)
    {
        if (!button.interactable) return;
        pressed = true;
        if (SettingsPanelController.VibrationEnabled && Application.isMobilePlatform) Handheld.Vibrate();
    }
    public void OnPointerUp(PointerEventData data) { pressed = false; }
    public void OnPointerExit(PointerEventData data) { pressed = false; }
    private void OnDisable() { pressed = false; transform.localScale = Vector3.one; }
}
