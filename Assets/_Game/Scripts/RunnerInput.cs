using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(RunnerMotor))]
public class RunnerInput : MonoBehaviour
{
    [Header("Swipe")]
    [SerializeField, Range(0.02f, 0.3f)]
    private float swipeThreshold = 0.08f;

    private RunnerMotor motor;

    private InputActionMap controls;
    private InputAction left;
    private InputAction right;
    private InputAction jump;
    private InputAction pointerPress;
    private InputAction pointerPosition;

    private Vector2 swipeStart;
    private bool trackingSwipe;

    private void Awake()
    {
        motor = GetComponent<RunnerMotor>();

        controls = new InputActionMap("Runner");

        left = controls.AddAction("Left", InputActionType.Button);
        left.AddBinding("<Keyboard>/a");
        left.AddBinding("<Keyboard>/leftArrow");

        right = controls.AddAction("Right", InputActionType.Button);
        right.AddBinding("<Keyboard>/d");
        right.AddBinding("<Keyboard>/rightArrow");

        jump = controls.AddAction("Jump", InputActionType.Button);
        jump.AddBinding("<Keyboard>/space");
        jump.AddBinding("<Keyboard>/upArrow");

        pointerPress = controls.AddAction(
            "PointerPress",
            InputActionType.Button,
            "<Pointer>/press"
        );

        pointerPosition = controls.AddAction(
            "PointerPosition",
            InputActionType.Value,
            "<Pointer>/position"
        );
    }

    private void OnEnable()
    {
        controls.Enable();
    }

    private void OnDisable()
    {
        controls.Disable();
        trackingSwipe = false;
    }

    private void OnDestroy()
    {
        controls.Dispose();
    }

    private void Update()
    {
        if (left.WasPressedThisFrame())
            motor.ChangeLane(-1);

        if (right.WasPressedThisFrame())
            motor.ChangeLane(1);

        if (jump.WasPressedThisFrame())
            motor.Jump();

        ReadSwipe();
    }

    private void ReadSwipe()
    {
        if (pointerPress.WasPressedThisFrame())
        {
            swipeStart = pointerPosition.ReadValue<Vector2>();
            trackingSwipe = true;
        }

        if (!trackingSwipe)
            return;

        Vector2 swipe =
            pointerPosition.ReadValue<Vector2>() - swipeStart;

        float minimumDistance =
            Mathf.Min(Screen.width, Screen.height) * swipeThreshold;

        if (swipe.magnitude >= minimumDistance)
        {
            // Only one action is allowed per gesture.
            trackingSwipe = false;

            if (Mathf.Abs(swipe.x) > Mathf.Abs(swipe.y))
            {
                motor.ChangeLane(swipe.x > 0f ? 1 : -1);
            }
            else if (swipe.y > 0f)
            {
                motor.Jump();
            }
        }

        if (!pointerPress.IsPressed())
        {
            trackingSwipe = false;
        }
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
            trackingSwipe = false;
    }
}