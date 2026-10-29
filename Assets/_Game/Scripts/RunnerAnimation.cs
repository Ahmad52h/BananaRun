using System.Collections;
using UnityEngine;

[DefaultExecutionOrder(100)]
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerHealth))]
public class RunnerAnimation : MonoBehaviour
{
    [SerializeField] private Animator animator;
    
    [Header("Transition Blending")]
    [SerializeField, Min(0f)] private float blendDuration = 0.08f;
    [SerializeField, Min(0f)] private float landBlendDuration = 0.2f;

    [Header("Jump Tuning")]
    [SerializeField, Min(0.1f)] private float jumpClipLength = 1.666667f;

    [Header("Hit Reaction")]
    [SerializeField] private string hitTrigger = "Hit";
    [SerializeField, Min(0.05f)] private float hitDuration = 0.8f;
    [SerializeField, Range(0.1f, 1f)] private float hitSpeedMultiplier = 0.4f;
    [SerializeField, Min(0f)] private float slowdownDuration = 0.85f;

    [SerializeField] private float slideClipLength = 1f;
    [SerializeField] private float stumbleClipLength = 1f;

    private RunnerMotor motor;
    private float hitEndsAt;
    private int hitTriggerHash;
    private bool hasHitTrigger;

    public bool IsReactingToHit => Time.time < hitEndsAt;
    public float HitClipLength => stumbleClipLength;

    private CharacterController controller;
    private PlayerHealth health;

    private static readonly int RunState = Animator.StringToHash("Base Layer.Run");
    private static readonly int WarmupState = Animator.StringToHash("Base Layer.Warming up");

    public void PlayRaceStartRun()
    {
        if (animator == null) return;
        animator.speed = 1f;
        animator.applyRootMotion = false;
        // Rebind tears down and rebuilds the Animator's playable graph, so the
        // "Warming up" pose cannot keep driving bones once the race begins.
        animator.Rebind();
        animator.Play(RunState, 0, 0f);
        currentState = RunState;
        wasGrounded = true;
        EnforceVisualRoot();
        EnforceCenteredHips();
    }
    private static readonly int JumpState = Animator.StringToHash("Base Layer.Jump");
    private static readonly int IdleState = Animator.StringToHash("Base Layer.Idle");
    private static readonly int SlideState = Animator.StringToHash("Base Layer.Slide");
    private static readonly int HitState = Animator.StringToHash("Base Layer.Hit");

    private int currentState;
    private bool frozen;
    private bool wasGrounded;
    public bool IsVineControlled { get; private set; }
    private bool vineReleased;
    private static readonly int VineState = Animator.StringToHash("Base Layer.VineSwing");

    public void BeginVineSwing()
    {
        IsVineControlled = true;
        vineReleased = false;
        hitEndsAt = 0f;
        StopThrowOverlay();
        animator.ResetTrigger(hitTriggerHash);
        animator.SetBool("Sliding", false);
        animator.SetBool("VineAttached", true);
        animator.speed = 1f;
        ChangeState(VineState, 0.16f);
    }

    public void ReleaseVine(float flightDuration)
    {
        vineReleased = true;
        animator.SetBool("VineAttached", false);
        animator.SetFloat("JumpSpeed", jumpClipLength / Mathf.Max(0.1f, flightDuration));
        ChangeState(JumpState, 0.12f);
    }

    public void EndVineSwing()
    {
        IsVineControlled = false;
        vineReleased = false;
        animator.SetBool("VineAttached", false);
        wasGrounded = false;
        if (!health.IsRunOver) ChangeState(RunState, landBlendDuration);
    }
    private Transform hips;
    private Transform leftFoot;
    private Transform rightFoot;
    private Transform visualRoot;
    private float visualRootLocalY;
    private float restHipsX;
    private float restHipsZ;
    private readonly RaycastHit[] groundHits = new RaycastHit[8];
    [System.Serializable]
    private class SoleContact
    {
        public Transform bone;
        public Vector3[] points;
    }
    [SerializeField] private SoleContact[] soleContacts;

    // طبقة الرمي لتجنب تشوه اليد
    private int throwLayerIndex = -1;
    private Coroutine throwCoroutine;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        health = GetComponent<PlayerHealth>();
        motor = GetComponent<RunnerMotor>();
        if (animator == null) animator = GetComponentInChildren<Animator>();

        if (animator == null)
        {
            Debug.LogError("Assign MonkeyVisual's Animator to RunnerAnimation.", this);
            enabled = false;
            return;
        }

        animator.applyRootMotion = false;
        animator.speed = 1f;
        visualRoot = animator.transform;
        if (visualRoot != null) visualRootLocalY = visualRoot.localPosition.y;
        if (animator.isHuman)
        {
            hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            if (hips != null)
            {
                restHipsX = hips.localPosition.x;
                restHipsZ = hips.localPosition.z;
            }
        }
        RefreshHitTrigger();

        // تصفير وزن طبقة الرمي فوراً لكي لا تتشوه اليد
        InitThrowLayer();
    }

    private void InitThrowLayer()
    {
        throwLayerIndex = animator.GetLayerIndex("Throw Overlay");
        if (throwLayerIndex != -1)
        {
            animator.SetLayerWeight(throwLayerIndex, 0f);
        }
    }

    public void TriggerThrow(float duration = 0.55f)
    {
        if (IsVineControlled || IsReactingToHit || (motor != null && motor.IsSliding)) return;
        if (throwLayerIndex == -1)
            throwLayerIndex = animator.GetLayerIndex("Throw Overlay");

        if (throwLayerIndex != -1)
        {
            if (throwCoroutine != null) StopCoroutine(throwCoroutine);
            throwCoroutine = StartCoroutine(ThrowWeightRoutine(duration));
        }
    }

    private IEnumerator ThrowWeightRoutine(float duration)
    {
        // رفع الوزن إلى 1 وتشغيل الرمي
        animator.SetLayerWeight(throwLayerIndex, 1f);
        animator.Play("Throw", throwLayerIndex, 0f);

        yield return new WaitForSeconds(duration);

        // إعادة الوزن تدريجياً وبنعومة إلى 0
        float elapsed = 0f;
        float fadeTime = 0.12f;
        while (elapsed < fadeTime)
        {
            elapsed += Time.deltaTime;
            animator.SetLayerWeight(throwLayerIndex, Mathf.Lerp(1f, 0f, elapsed / fadeTime));
            yield return null;
        }

        animator.SetLayerWeight(throwLayerIndex, 0f);
        throwCoroutine = null;
    }

    private void OnEnable()
    {
        if (health != null)
        {
            health.HitTaken += ReactToHit;
            health.ProjectileHit += ReactToProjectile;
        }
    }

    private void OnDisable()
    {
        if (health != null)
        {
            health.HitTaken -= ReactToHit;
            health.ProjectileHit -= ReactToProjectile;
        }
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
        BeginStumble(Mathf.Max(hitDuration, health.ProjectileStunRemaining));
    }

    private void ReactToProjectile() => BeginStumble(health.ProjectileStunRemaining);

    private void Start()
    {
        if (animator == null || health == null || motor == null || controller == null) { enabled = false; return; }
        if (!animator.HasState(0, RunState) ||
            !animator.HasState(0, JumpState) ||
            !animator.HasState(0, IdleState))
        {
            Debug.LogError("AC_Monkey needs Run, Jump and Idle states in Base Layer.", this);
            enabled = false;
            return;
        }

        currentState = RaceStartSequence.IsHolding ? WarmupState : RunState;
        wasGrounded = controller.isGrounded;
        animator.Play(currentState, 0, 0f);
    }

    private void Update()
    {
        if (animator == null || health == null || motor == null || controller == null) return;
        if (RaceStartSequence.IsHolding)
        {
            ChangeState(WarmupState, 0.1f);
            return;
        }
        if (frozen) return;
        if (health.HasFailed)
        {
            animator.speed = 0f;
            frozen = true;
            return;
        }
        if (health.IsRunOver)
        {
            animator.speed = 1f;
            ChangeState(IdleState, blendDuration);
            return;
        }
        if (IsVineControlled) return;
        if (IsReactingToHit) return;

        bool grounded = controller.isGrounded;
        bool sliding = motor != null && motor.IsSliding;
        animator.speed = 1f;
        animator.SetBool("Sliding", sliding);
        if (sliding)
        {
            StopThrowOverlay();
            animator.SetFloat("SlideSpeed", slideClipLength / Mathf.Max(0.1f, motor.SlideDuration));
            ChangeState(SlideState, 0.08f);
        }
        else if (!grounded)
        {
            // Use a state multiplier, so jump timing cannot speed up a hit or throw.
            animator.SetFloat("JumpSpeed", jumpClipLength /
                Mathf.Max(0.1f, motor.JumpDuration - Time.deltaTime));
            ChangeState(JumpState, blendDuration);
        }
        else
        {
            ChangeState(RunState, currentState == SlideState || currentState == HitState ? 0.15f :
                (!wasGrounded ? landBlendDuration : blendDuration));
        }
        wasGrounded = grounded;
    }

    private void LateUpdate()
    {
        // Runs after the Animator: the visual model can never be translated or
        // rotated off the runner root by a clip, override graph or blend.
        EnforceVisualRoot();
        if (hips == null || frozen || !animator.isActiveAndEnabled || (IsVineControlled && !vineReleased)) return;
        EnforceCenteredHips();

        var state = animator.GetCurrentAnimatorStateInfo(0);
        bool stumbling = IsReactingToHit || state.fullPathHash == HitState ||
            (animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).fullPathHash == HitState);
        if (!stumbling || !controller.isGrounded || leftFoot == null || rightFoot == null) return;

        // Anchor the lowest sole to the actual path surface. Only the pose changes;
        // the visual GameObject and CharacterController retain their original Y/center.
        float surfaceY = float.NegativeInfinity;
        int count = Physics.RaycastNonAlloc(controller.bounds.center, Vector3.down, groundHits,
            controller.bounds.extents.y + 0.5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
            if (!groundHits[i].collider.transform.IsChildOf(transform) && groundHits[i].normal.y > 0.5f)
                surfaceY = Mathf.Max(surfaceY, groundHits[i].point.y);
        if (float.IsNegativeInfinity(surfaceY)) return;
        float soleY = Mathf.Min(leftFoot.position.y - animator.leftFeetBottomHeight,
            rightFoot.position.y - animator.rightFeetBottomHeight);
        // Humanoid ankle offsets do not include the monkey's long toes as they
        // rotate. These contact samples come from the mesh in each foot's bind pose.
        if (soleContacts != null && soleContacts.Length > 0)
        {
            soleY = float.PositiveInfinity;
            foreach (var contact in soleContacts)
            {
                if (contact.bone == null || contact.points == null) continue;
                var matrix = contact.bone.localToWorldMatrix;
                foreach (var point in contact.points)
                    soleY = Mathf.Min(soleY, matrix.MultiplyPoint3x4(point).y);
            }
            if (float.IsPositiveInfinity(soleY)) return;
        }
        hips.position += Vector3.up * (surfaceY - soleY);
    }

    /// <summary>Keeps the animated model rooted at the runner's transform origin.</summary>
    private void EnforceVisualRoot()
    {
        if (visualRoot == null) return;
        visualRoot.localPosition = new Vector3(0f, visualRootLocalY, 0f);
        visualRoot.localRotation = Quaternion.identity;
    }

    /// <summary>
    /// Locks the pelvis's lateral and forward offsets to the bind pose. The
    /// warm-up clip can otherwise drag the whole body sideways while still
    /// keeping the CharacterController and camera on the correct lane.
    /// </summary>
    private void EnforceCenteredHips()
    {
        if (hips == null) return;
        Vector3 position = hips.localPosition;
        position.x = restHipsX;
        position.z = restHipsZ;
        hips.localPosition = position;
    }

    private void StopThrowOverlay()
    {
        if (throwCoroutine != null) StopCoroutine(throwCoroutine);
        throwCoroutine = null;
        if (throwLayerIndex >= 0) animator.SetLayerWeight(throwLayerIndex, 0f);
    }

    public void ReactToSlip(float duration = 0.8f)
    {
        if (health.IsRunOver) return;
        BeginStumble(Mathf.Max(duration, health.ProjectileStunRemaining));
    }

    private void BeginStumble(float duration)
    {
        if (IsVineControlled) return;
        RefreshHitTrigger();
        if (animator == null || !animator.isActiveAndEnabled || !hasHitTrigger) return;
        StopThrowOverlay();
        RunnerHitReaction.Play(animator, stumbleClipLength, duration);
        hitEndsAt = Time.time + duration;
        currentState = HitState;
    }
    private void ChangeState(int nextState, float transitionTime)
    {
        if (currentState == nextState)
            return;

        currentState = nextState;

        animator.CrossFadeInFixedTime(
            nextState,
            transitionTime,
            0,
            0f);
    }
}
