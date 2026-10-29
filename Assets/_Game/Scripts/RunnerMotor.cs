using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class RunnerMotor : MonoBehaviour
{
    [Header("Running")]
    [SerializeField, Min(0f)] private float forwardSpeed = 8f;
    [Tooltip("Maximum forward speed relative to the existing base run speed.")]
    [SerializeField, Range(1f, 1.5f)] private float maxRunSpeedMultiplier = 1.25f;
    [SerializeField] private AnimationCurve speedDifficultyCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    private RunnerCourseGenerator difficultySource;
    private bool difficultyStartupComplete;
    private bool warnedMissingDifficultySource;
    private bool warnedRejectedDifficultySource;

    [Header("Lanes")]
    [Tooltip("World X of the true road/track center. Never use the side-by-side countdown position.")]
    [SerializeField] private float trackCenterX = 0f;
    [SerializeField, Min(0.1f)] private float laneDistance = 2.2f;
    [SerializeField, Min(0.1f)] private float laneChangeSpeed = 12f;

    [Header("Jump")]
    [SerializeField, Min(0.1f)] private float jumpHeight = 2.3f;
    [SerializeField] private float gravity = -21f;
    [Header("Glider Jump")]
    [SerializeField, Range(1f, 2.5f)] private float gliderHeightMultiplier = 1.7f;
    private const float GliderGravityScale = 0.35f;
    private bool gliderJump;

    [Header("Slide")]
    [Tooltip("مدة الزحلقة بالثواني - زدناها ليعبر العائق كاملاً دون أن يقف تحته")]
    [SerializeField, Min(0.1f)] private float slideDuration = 0.95f;
    [Tooltip("ارتفاع الكوليدر أثناء الزحلقة")]
    [SerializeField, Min(0.2f)] private float slideHeight = 0.55f;
    [Tooltip("تصغير نصف القطر أثناء الزحلقة ليسمح يونيتي بانخفاض الكوليدر للأسفل تماماً")]
    [SerializeField, Min(0.1f)] private float slideRadius = 0.25f;
    [SerializeField] private LaneChangeVisual laneChangeVisual;

    private CharacterController controller;
    private int currentLane;
    private float centerX;
    private float verticalVelocity;
    private float slowdownStarted;
    private float slowdownDuration;
    private float slowdownMultiplier = 1f;
    private PowerUpManager powerUps;
    private PlayerHealth health;
    private MonkeyFootstepDust dustEffects;
    
    // حفظ الأبعاد الأصلية للكوليدر
    private float standingHeight;
    private Vector3 standingCenter;
    private float standingRadius;

    private float slideEndsAt;
    private bool sliding;
    private float laneLockEndsAt;

    public float SlideDuration => slideDuration;
    public bool IsLaneLocked => Time.time < laneLockEndsAt;

    public void LockLaneSwitching(float duration)
    {
        laneLockEndsAt = Mathf.Max(laneLockEndsAt, Time.time + Mathf.Max(0f, duration));
        // Discard an in-progress lane request; hold the actual horizontal position.
        currentLane = Mathf.Clamp(Mathf.RoundToInt((transform.position.x - centerX) / laneDistance), -1, 1);
    }

    /// <summary>True road center. The player may stand off-center for the countdown, but lanes never move with it.</summary>
    public float CenterX => trackCenterX;
    public float LaneDistance => laneDistance;
    public int ActiveLane => currentLane;
    public float ActiveLaneWorldX => centerX + currentLane * laneDistance;
    public float LaneChangeDuration => laneDistance / Mathf.Max(0.01f, laneChangeSpeed);
    public float ForwardSpeed => forwardSpeed;
    public float MaximumForwardSpeed => forwardSpeed * maxRunSpeedMultiplier;
    public float NormalJumpHeight => jumpHeight;
    public float HighJumpHeight => jumpHeight * gliderHeightMultiplier;
    public float GliderGravityMultiplier => GliderGravityScale;
    public float HighJumpDuration => 2f * Mathf.Sqrt(HighJumpHeight * -2f * gravity * GliderGravityScale) / (-gravity * GliderGravityScale);
    public float SpeedAtDifficulty(float difficulty) => forwardSpeed * Mathf.Lerp(1f, maxRunSpeedMultiplier,
        Mathf.Clamp01(speedDifficultyCurve == null ? difficulty : speedDifficultyCurve.Evaluate(Mathf.Clamp01(difficulty))));
    public void ConfigureDifficulty(RunnerCourseGenerator source)
    {
        if (source == null || source.Player != this)
        {
            if (!warnedRejectedDifficultySource)
            {
                Debug.LogWarning(source == null
                    ? "RunnerMotor rejected difficulty configuration: generator reference is missing. Keeping the current speed source."
                    : "RunnerMotor rejected difficulty configuration: generator references another player. Keeping the current speed source.", this);
                warnedRejectedDifficultySource = true;
            }
            return;
        }
        difficultySource = source;
    }
    public float BaseGravity => gravity;
    public float JumpDuration => gliderJump
        ? 2f * Mathf.Sqrt(jumpHeight * gliderHeightMultiplier * -2f * gravity * GliderGravityScale) / (-gravity * GliderGravityScale)
        : 2f * Mathf.Sqrt(jumpHeight * -2f * gravity) / -gravity;
    public bool IsGliderJump => gliderJump;
    public bool IsSliding => sliding;
    public bool IsStunned => health != null && health.IsProjectileStunned;
    // Latch the arc through landing so an expiring pickup cannot change gravity mid-jump.
    public float EffectiveGravity => gravity * (gliderJump ? GliderGravityScale : 1f);
    public float CurrentSpeed
    {
        get
        {
            if (RaceStartSequence.IsHolding || IsStunned) return 0f;
            if (difficultyStartupComplete && difficultySource == null && !warnedMissingDifficultySource)
            {
                Debug.LogWarning("RunnerMotor has no difficulty generator; CurrentSpeed is using baseline difficulty.", this);
                warnedMissingDifficultySource = true;
            }
            float speed = SpeedAtDifficulty(difficultySource != null ? difficultySource.Difficulty : 0f) * (slowdownDuration > 0f
                ? Mathf.Lerp(slowdownMultiplier, 1f, Mathf.Clamp01((Time.time - slowdownStarted) / slowdownDuration))
                : 1f);
            if (powerUps != null && powerUps.HasThunder) speed *= PowerUpManager.ThunderSpeedMultiplier;
            return speed;
        }
    }

    public void ApplySlowdown(float multiplier, float duration)
    {
        slowdownMultiplier = Mathf.Clamp01(multiplier);
        slowdownDuration = Mathf.Max(0f, duration);
        slowdownStarted = Time.time;
    }

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        powerUps = GetComponent<PowerUpManager>();
        health = GetComponent<PlayerHealth>();
        dustEffects = GetComponent<MonkeyFootstepDust>();
        if (dustEffects == null) dustEffects = gameObject.AddComponent<MonkeyFootstepDust>();
        
        standingHeight = controller.height;
        standingCenter = controller.center;
        standingRadius = controller.radius;

        // Lanes are fixed to the road, never to the countdown's side-by-side
        // starting offset. The runner slides back onto lane 0 after GO.
        centerX = trackCenterX;
        currentLane = 0;
    }

    private void Start() => difficultyStartupComplete = true;

    /// <summary>
    /// Called on GO. Re-asserts the true road grid and lets Update smoothly
    /// slide the runner from the off-center countdown spot into the center lane.
    /// </summary>
    public void BeginRaceRun()
    {
        centerX = trackCenterX;
        currentLane = 0;
    }

    private void Update()
    {
        if (RaceStartSequence.IsHolding) return;
        if (controller == null || !controller.enabled) return;
        float deltaTime = Time.deltaTime;

        if (deltaTime <= 0f)
            return;

        if (sliding && Time.time >= slideEndsAt) EndSlide();

        if (controller.isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = -2f;
            gliderJump = false;
        }
        else if (!controller.isGrounded && powerUps != null && powerUps.IsActive(PowerUpType.Glider))
        {
            // Preserve gliding off ledges and collecting the ability while airborne.
            gliderJump = true;
        }

        verticalVelocity += EffectiveGravity * deltaTime;

        float targetX = centerX + currentLane * laneDistance;

        float nextX = Mathf.MoveTowards(
            transform.position.x,
            targetX,
            laneChangeSpeed * deltaTime
        );
        if (IsLaneLocked || IsStunned) nextX = transform.position.x;

        Vector3 movement = new Vector3(
            nextX - transform.position.x,
            verticalVelocity * deltaTime,
            CurrentSpeed * deltaTime
        );

        CollisionFlags collisions = controller.Move(movement);

        if ((collisions & CollisionFlags.Above) != 0 &&
            verticalVelocity > 0f)
        {
            verticalVelocity = 0f;
        }

        if (EnforceRiverGapFall()) return;
    }

    /// <summary>
    /// The river has no walkable floor. If a solid leftover collider keeps the
    /// controller grounded over the gap, or the runner has already dropped past
    /// the fall line, hand them to the crocodile lake immediately. Airborne
    /// runners above the fall line keep their jump arc.
    /// </summary>
    private bool EnforceRiverGapFall()
    {
        var gap = RiverCrocodileGauntlet.Active;
        if (gap == null || health == null || !gap.IsInsideRiverGap(transform.position.z))
            return false;

        bool walkingOnWater = controller.isGrounded;
        bool belowFallLine = transform.position.y <= RiverCrocodileGauntlet.WaterFallY;
        if (!walkingOnWater && !belowFallLine) return false;

        // Bypass the normal ground clamp; the lake controller owns the visible sink.
        verticalVelocity = -30f;
        gap.HandlePlayerFall(health);
        return true;
    }

    public void ChangeLane(int direction)
{
    if (RaceStartSequence.IsHolding || !isActiveAndEnabled || IsLaneLocked || IsStunned)
        return;

    int oldLane = currentLane;

    currentLane = Mathf.Clamp(
        currentLane + direction,
        -1,
        1
    );

    // فقط شغّل الحركة إذا اللاعب فعلاً انتقل إلى Lane جديد
    if (currentLane != oldLane && laneChangeVisual != null)
    {
        int actualDirection = currentLane > oldLane ? 1 : -1;
        laneChangeVisual.PlayLaneChange(actualDirection);
    }
}

    public void Jump()
    {
        if (RaceStartSequence.IsHolding || !isActiveAndEnabled || IsStunned || controller == null || !controller.enabled || sliding || !controller.isGrounded)
            return;

        gliderJump = powerUps != null && powerUps.IsActive(PowerUpType.Glider);
        verticalVelocity = Mathf.Sqrt(jumpHeight * (gliderJump ? gliderHeightMultiplier : 1f)
            * -2f * gravity * (gliderJump ? GliderGravityScale : 1f));
    }

    public bool Slide()
    {
        if (RaceStartSequence.IsHolding || !isActiveAndEnabled || IsStunned || sliding || IsLaneLocked || !controller.enabled)
            return false;

        // إذا كان القرد في الهواء وضغطت زحلقة، ينزل بسرعة للأرض ويتزحلق
        if (!controller.isGrounded)
        {
            verticalVelocity = -20f;
        }

        sliding = true;
        slideEndsAt = Time.time + slideDuration;

        // تصغير القطر والارتفاع معاً للنزول تحت العائق بنعومة
        controller.radius = slideRadius;
        controller.height = slideHeight;
        
        float bottom = standingCenter.y - standingHeight * 0.5f;
        controller.center = new Vector3(standingCenter.x, bottom + slideHeight * 0.5f, standingCenter.z);
        
        return true;
    }

    private void EndSlide()
    {
        sliding = false;
        controller.height = standingHeight;
        controller.center = standingCenter;
        controller.radius = standingRadius;
    }

    private void OnDisable()
    {
        if (dustEffects != null) dustEffects.StopAllDust();
        if (sliding && controller != null) EndSlide();
    }

    public void ResetVerticalVelocity() { verticalVelocity = -2f; gliderJump = false; }

    private void OnValidate()
    {
        gravity = Mathf.Min(gravity, -0.1f);
    }
}
