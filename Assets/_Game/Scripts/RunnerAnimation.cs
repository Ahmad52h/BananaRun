using UnityEngine;

[DefaultExecutionOrder(100)]
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerHealth))]
public class RunnerAnimation : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField, Min(0f)] private float blendDuration = 0.08f;
    [Header("Hit Reaction")]
    [SerializeField] private string hitTrigger = "Hit";
    [SerializeField, Min(0.05f)] private float hitDuration = 0.55f;
    [SerializeField, Range(0.1f, 1f)] private float hitSpeedMultiplier = 0.4f;
    [SerializeField, Min(0f)] private float slowdownDuration = 0.85f;

    private RunnerMotor motor;
    private float hitEndsAt;
    private int hitTriggerHash;
    private bool hasHitTrigger;

    public bool IsReactingToHit => Time.time < hitEndsAt;

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
        motor = GetComponent<RunnerMotor>();
        if (animator == null) animator = GetComponentInChildren<Animator>();

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
        RefreshHitTrigger();
    }

    private void OnEnable()
    {
        if (health != null) health.HitTaken += ReactToHit;
    }

    private void OnDisable()
    {
        if (health != null) health.HitTaken -= ReactToHit;
    }

    private void RefreshHitTrigger()
    {
        hasHitTrigger = false;
        if (animator == null || animator.runtimeAnimatorController == null || string.IsNullOrEmpty(hitTrigger)) return;
        hitTriggerHash = Animator.StringToHash(hitTrigger);
        foreach (var parameter in animator.parameters)
            if (parameter.nameHash == hitTriggerHash && parameter.type == AnimatorControllerParameterType.Trigger)
                hasHitTrigger = true;
    }

    private void ReactToHit()
    {
        if (motor != null) motor.ApplySlowdown(hitSpeedMultiplier, slowdownDuration);
        RefreshHitTrigger();
        if (animator == null || !animator.isActiveAndEnabled || !hasHitTrigger) return;
        animator.ResetTrigger(hitTriggerHash);
        animator.SetTrigger(hitTriggerHash);
        hitEndsAt = Time.time + hitDuration;
        // Allow the Animator's Hit transition to finish before resuming locomotion.
        currentState = 0;
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

        if (IsReactingToHit) return;

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
