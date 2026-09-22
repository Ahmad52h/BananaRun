using UnityEngine;

[DefaultExecutionOrder(100)]
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerHealth))]
public class RunnerAnimation : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField, Min(0f)] private float blendDuration = 0.08f;

    private CharacterController controller;
    private PlayerHealth health;

    private static readonly int RunState =
        Animator.StringToHash("Base Layer.Run");

    private static readonly int JumpState =
        Animator.StringToHash("Base Layer.Jump");

    private static readonly int IdleState =
        Animator.StringToHash("Base Layer.Idle");

    private int currentState;
    private bool frozen;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        health = GetComponent<PlayerHealth>();

        if (animator == null)
        {
            Debug.LogError(
                "Assign MonkeyVisual's Animator to RunnerAnimation.",
                this);

            enabled = false;
            return;
        }

        animator.applyRootMotion = false;
        animator.speed = 1f;
    }

    private void Start()
    {
        if (!animator.HasState(0, RunState) ||
            !animator.HasState(0, JumpState) ||
            !animator.HasState(0, IdleState))
        {
            Debug.LogError(
                "AC_Monkey needs Run, Jump and Idle states in Base Layer.",
                this);

            enabled = false;
            return;
        }

        currentState = RunState;
        animator.Play(RunState, 0, 0f);
    }

    private void Update()
    {
        if (frozen)
            return;

        if (health.IsDead)
        {
            animator.speed = 0f;
            frozen = true;
            return;
        }

        if (health.IsRunOver)
        {
            ChangeState(IdleState);
            return;
        }

        ChangeState(
            controller.isGrounded ? RunState : JumpState);
    }

    private void ChangeState(int nextState)
    {
        if (currentState == nextState)
            return;

        currentState = nextState;

        animator.CrossFadeInFixedTime(
            nextState,
            blendDuration,
            0,
            0f);
    }
}