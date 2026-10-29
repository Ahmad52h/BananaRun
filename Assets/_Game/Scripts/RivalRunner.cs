using UnityEngine;

/// <summary>An independent competitor: world movement, obstacle decisions and recoverable setbacks.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(BananaInventory))]
public sealed class RivalRunner : MonoBehaviour
{
    [SerializeField] private RunnerMotor player;
    [SerializeField] private RunnerCourseGenerator course;
    [SerializeField] private Animator animator;
    [SerializeField, Range(0.8f, 1.2f)] private float paceMultiplier = 1.04f;
    [Tooltip("Legacy short-range scan. Detection uses the larger of this and Obstacle Avoidance > Detection Range.")]
    [SerializeField, Min(1f)] private float lookAhead = 9f;
    [SerializeField, Min(0.1f)] private float jumpClearance = 0.2f;
    [SerializeField, Min(0.1f)] private float stumbleDuration = 0.85f;
    [SerializeField, Min(0.1f)] private float fallDuration = 1.5f;
    [SerializeField, Min(0.1f)] private float slideDuration = 0.95f;
    [SerializeField, Min(0.1f)] private float projectileStunDuration = 2f;
    [SerializeField, Min(1f)] private float magnetRadius = 8f;
    [Header("Obstacle Avoidance")]
    [Tooltip("Forward scan length. At top speed the rival needs roughly a full second of warning.")]
    [SerializeField, Min(6f)] private float detectionRange = 16f;
    [Tooltip("Extra half-width on lane scans so wide logs and barriers register before contact.")]
    [SerializeField, Min(0f)] private float detectionMargin = 0.45f;
    [Header("Stuck Recovery")]
    [Tooltip("Seconds of zero forward progress before the runner pops itself free.")]
    [SerializeField, Min(0.05f)] private float stuckTimeout = 0.2f;
    [Tooltip("Forward speed (m/s) below which the runner counts as pinned.")]
    [SerializeField, Min(0.01f)] private float stuckSpeedThreshold = 0.35f;
    [Tooltip("Upward speed for the recovery micro-jump.")]
    [SerializeField, Min(1f)] private float stuckJumpSpeed = 6.5f;
    [Tooltip("Faster lateral speed while escaping a pinned lane.")]
    [SerializeField, Min(1f)] private float stuckLaneShiftSpeed = 17f;
    [Tooltip("How long a recovery side-step is protected from banana-driven lane changes.")]
    [SerializeField, Min(0.1f)] private float stuckLaneHold = 0.75f;
    [Header("Bomb Traps")]
    [SerializeField, Min(1)] private int maxBombs = 3;
    [SerializeField, Min(0)] private int startingBombs;
    [SerializeField, Min(1f)] private float bombDropDistance = 1.8f;
    [SerializeField, Min(0.1f)] private float bombDropCooldown = 1.2f;
    [SerializeField, Min(0.5f)] private float bombDropMinInterval = 2.4f;
    [SerializeField, Min(0.5f)] private float bombDropMaxInterval = 4.5f;
    [SerializeField, Min(2f)] private float bombDropRange = 18f;
    [Header("Giant Banana")]
    [SerializeField] private Transform rivalModel;
    [SerializeField, Min(1f)] private float giantScale = 1.7f;
    [SerializeField, Min(0.1f)] private float giantScaleSmoothing = 9f;
    [Header("River crossing (takeoff and landing world Z)")]
    [SerializeField] private Vector2 riverCrossing = new Vector2(607f, 646f);

    private readonly Collider[] probes = new Collider[32];
    private readonly Collider[] bananaProbes = new Collider[96];
    private readonly RaycastHit[] bombGroundHits = new RaycastHit[16];
    private readonly RaycastHit[] forwardHits = new RaycastHit[24];
    // Feet, torso and head-height casts catch low logs, lane barriers and rocks.
    private static readonly float[] ForwardSampleHeights = { 0.2f, 0.525f, 0.85f };
    private CharacterController controller;
    private float centerX, verticalVelocity, setbackEndsAt, setbackMultiplier = 1f;
    private float protectedUntil, nextDecision;
    private Vector3 lastGroundedPosition;
    private int lane = 1, animationState;
    private bool recoveringFall, jumping;
    private bool sliding;
    private float forcedLaneUntil;
    private float stuckTimer, nextStuckAttempt;
    private int stuckEscapes;
    private float[] riverVineLaneXs;
    private float riverVineTriggerZ;
    private int riverVineLane;
    private bool riverVineConfigured, riverVineLaneChosen;
    private float slideEndsAt, standingHeight, standingRadius;
    private Vector3 standingCenter;
    private BananaInventory inventory;
    private float projectileStunEndsAt, projectileProtectionEndsAt;
    private float shieldEndsAt, thunderEndsAt, magnetEndsAt, highJumpEndsAt, bulldozerEndsAt;
    private bool gliderJump;
    private bool bombLaunched;
    private RottenBananaBomb bombTrapPrefab;
    private float nextBombDrop;
    private Vector3 originalModelScale = Vector3.one;

    public static RivalRunner ActiveRival { get; private set; }

    private static readonly int RunState = Animator.StringToHash("Base Layer.Run");
    private static readonly int HitState = Animator.StringToHash("Base Layer.Hit");
    private static readonly int JumpState = Animator.StringToHash("Base Layer.Jump");
    private static readonly int IdleState = Animator.StringToHash("Base Layer.Idle");
    private static readonly int SlideState = Animator.StringToHash("Base Layer.Slide");

    public RunnerMotor Player => player;
    public int CurrentLane => lane;
    public float ActiveLaneWorldX => centerX + lane * player.LaneDistance;
    public int BananaCount => inventory != null ? inventory.Count : 0;
    public bool IsSliding => sliding;
    public bool IsJumping => jumping;
    public bool IsGrounded => controller != null && controller.enabled && controller.isGrounded && !IsJumping;
    public bool IsProjectileStunned => !HasThunder && Time.time < projectileStunEndsAt;
    public bool HasShield => Time.time < shieldEndsAt;
    public bool HasThunder => Time.time < thunderEndsAt;
    public bool HasMagnet => !HasFinished && Time.time < magnetEndsAt;
    public bool HasHighJump => !HasFinished && Time.time < highJumpEndsAt;
    public bool HasBulldozer => !HasFinished && Time.time < bulldozerEndsAt;
    public float MagnetRadius => magnetRadius;
    public int CurrentBombs { get; private set; }
    public int MaxBombs => maxBombs;
    public bool IsStumbling => !HasThunder && (Time.time < setbackEndsAt || IsProjectileStunned);
    public bool IsRecoveringFromFall => recoveringFall;
    public float BaseGravity => player != null ? player.BaseGravity : -21f;
    public bool HasFinished { get; private set; }
    public event System.Action Impacted;
    private float RunningPace => player == null ? 0f :
        player.SpeedAtDifficulty(course != null ? course.DifficultyAt(transform.position.z) : 0f) * paceMultiplier;
    public float CurrentSpeed => RaceStartSequence.IsHolding || HasFinished || recoveringFall || IsProjectileStunned ? 0f :
        RunningPace * (IsStumbling ? setbackMultiplier : 1f) * (HasThunder ? PowerUpManager.ThunderSpeedMultiplier : 1f);
    private float EffectiveLookAhead => Mathf.Max(lookAhead, detectionRange);

    private void Awake()
    {
        ActiveRival = this;
        controller = GetComponent<CharacterController>();
        inventory = GetComponent<BananaInventory>();
        if (inventory == null) inventory = gameObject.AddComponent<BananaInventory>();
        standingHeight = controller.height;
        standingRadius = controller.radius;
        standingCenter = controller.center;
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (rivalModel == null && animator != null) rivalModel = animator.transform;
        if (rivalModel != null) originalModelScale = rivalModel.localScale;
        var bombSource = player != null ? player.GetComponent<BombDropper>() : null;
        if (bombSource != null) bombTrapPrefab = bombSource.TrapPrefab;
        CurrentBombs = Mathf.Clamp(startingBombs, 0, maxBombs);
        lastGroundedPosition = transform.position;
        centerX = player != null ? player.CenterX : transform.position.x;
        if (player != null)
        {
            lane = Mathf.Clamp(Mathf.RoundToInt((transform.position.x - centerX) / player.LaneDistance), -1, 1);
            // Competitors can pass each other without pushing or blocking a lane.
            foreach (var collider in player.GetComponentsInChildren<Collider>(true))
                Physics.IgnoreCollision(controller, collider);
        }
        if (animator != null)
        {
            animator.applyRootMotion = false;
            for (int i = 1; i < animator.layerCount; i++) animator.SetLayerWeight(i, 0f);
        }
        if (GetComponent<RunnerAbilityVisual>() == null)
            gameObject.AddComponent<RunnerAbilityVisual>();
    }

    private void Start()
    {
        if (RaceStartSequence.IsHolding && animator != null)
        {
            animationState = IdleState;
            animator.Play(IdleState, 0, 0f);
        }
        if (!riverVineConfigured) ConfigureRiverVinesFromScene();
    }

    /// <summary>Called by the runtime river setup once the two lane vines exist.</summary>
    public void ConfigureRiverVines(float[] laneXs, float triggerZ)
    {
        if (laneXs == null || laneXs.Length == 0) return;
        riverVineLaneXs = (float[])laneXs.Clone();
        riverVineTriggerZ = triggerZ;
        riverVineConfigured = true;
        riverVineLaneChosen = false;
    }

    private void ConfigureRiverVinesFromScene()
    {
        var zones = FindObjectsByType<VineSwingZone>();
        if (zones == null || zones.Length == 0) return;

        var laneXs = new float[zones.Length];
        for (int i = 0; i < zones.Length; i++) laneXs[i] = zones[i].transform.position.x;
        float triggerZ = riverCrossing.x > 0f ? riverCrossing.x : zones[0].transform.position.z;
        ConfigureRiverVines(laneXs, triggerZ);
    }

    private void Update()
    {
        if (RaceStartSequence.IsHolding) { Animate(IdleState); return; }
        UpdateGiantScale();
        if (player == null || controller == null || !controller.enabled || Time.deltaTime <= 0f) return;
        if (HasFinished) { Animate(IdleState); return; }
        if (sliding && Time.time >= slideEndsAt) TryEndSlide();
        if (recoveringFall)
        {
            if (Time.time < setbackEndsAt) return;
            controller.enabled = false;
            transform.position = lastGroundedPosition;
            controller.enabled = true;
            verticalVelocity = -2f;
            recoveringFall = false;
        }

        float dt = Time.deltaTime;
        if (controller.isGrounded && (!jumping || verticalVelocity <= 0f))
        {
            jumping = false;
            gliderJump = false;
            bombLaunched = false;
            lastGroundedPosition = transform.position;
            verticalVelocity = -2f;
        }
        else if (!controller.isGrounded && HasHighJump && !bombLaunched)
        {
            // Latch the high-jump arc if the pickup is collected mid-air.
            gliderJump = true;
        }
        if (!IsStumbling && !sliding && IsGrounded && Time.time >= nextDecision)
        {
            nextDecision = Time.time + 0.06f;
            ChooseObstacleAction();
        }

        float laneSpeed = Time.time < forcedLaneUntil ? Mathf.Max(12f, stuckLaneShiftSpeed) : 12f;
        float x = IsProjectileStunned ? transform.position.x :
            Mathf.MoveTowards(transform.position.x, centerX + lane * player.LaneDistance, laneSpeed * dt);
        float dz = CurrentSpeed * dt;
        float gravity = player.BaseGravity * (gliderJump && !bombLaunched ? player.GliderGravityMultiplier : 1f);
        verticalVelocity += gravity * dt;
        float dy = verticalVelocity * dt;
        float previousZ = transform.position.z;
        var collision = controller.Move(new Vector3(x - transform.position.x, dy, dz));
        if ((collision & CollisionFlags.Above) != 0) verticalVelocity = Mathf.Min(0f, verticalVelocity);
        if (EnforceRiverGapFall()) return;
        if (transform.position.y < lastGroundedPosition.y - 3f) Fall();
        ConsiderBombDrop();
        TrackForwardProgress(transform.position.z - previousZ, dt);
        Animate(IsStumbling ? HitState : !controller.isGrounded ? JumpState : sliding ? SlideState : RunState);
    }

    public void PlayRaceStartRun()
    {
        verticalVelocity = -2f;
        nextDecision = Time.time + 0.1f;
        nextBombDrop = Time.time + bombDropMinInterval;
        Animate(RunState);
    }

    /// <summary>
    /// Rejects any leftover walkable surface over the river. Airborne runners are
    /// allowed to finish their jump/glide arc, but a grounded river crossing or a
    /// drop below the fall line becomes a crocodile-lake miss.
    /// </summary>
    private bool EnforceRiverGapFall()
    {
        var gap = RiverCrocodileGauntlet.Active;
        if (gap == null || !gap.IsInsideRiverGap(transform.position.z)) return false;

        bool walkingOnWater = controller.isGrounded;
        bool belowFallLine = transform.position.y <= RiverCrocodileGauntlet.WaterFallY;
        if (!walkingOnWater && !belowFallLine) return false;

        verticalVelocity = -30f;
        gap.HandleRivalFall(this);
        return true;
    }

    private void ChooseObstacleAction()
    {
        // The generated course leaves this approach clear. Commit to one of the two
        // lane vines and do not let banana/obstacle logic pull the rival off it.
        if (riverVineConfigured && transform.position.z >= riverVineTriggerZ - 16f &&
            transform.position.z <= riverVineTriggerZ + 1.5f)
        {
            if (!riverVineLaneChosen)
            {
                ChooseRiverVineLane();
                riverVineLaneChosen = true;
            }
            lane = riverVineLane;
            return;
        }

        Collider obstacle = ObstacleInLane(lane);
        if (obstacle == null) { ChooseBananaLane(); return; }
        float speed = Mathf.Max(1f, CurrentSpeed);
        var slideObstacle = obstacle.GetComponentInParent<RunnerObstacle>();
        if (slideObstacle != null && slideObstacle.CanSlideUnder &&
            obstacle.bounds.min.y > controller.bounds.min.y + 0.65f)
        {
            float distance = obstacle.bounds.min.z - controller.bounds.max.z;
            if (distance <= speed * 0.35f)
                BeginSlide(Mathf.Max(slideDuration, (obstacle.bounds.max.z - transform.position.z + 1f) / speed));
            return;
        }

        // Jumping only makes sense while there is space for takeoff and landing.
        if (TryJumpObstacle(obstacle, speed)) return;

        // Tall/wide obstacles require an unobstructed lane transition. This runs
        // immediately, while the obstacle is still inside the long detection
        // range, so the rival never waits until it is pinned against it.
        if (TryChangeLane()) return;

        // Every lane is blocked within the scan: pick the side with the most
        // open road instead of braking to a halt in front of the obstacle.
        ForceEscapeLane(obstacle);
    }

    private bool TryJumpObstacle(Collider obstacle, float speed)
    {
        bool highJump = HasHighJump;
        float gravity = Mathf.Max(0.1f, -player.BaseGravity) *
            (highJump ? player.GliderGravityMultiplier : 1f);
        float jumpHeight = highJump ? player.HighJumpHeight : player.NormalJumpHeight;
        float launchSpeed = Mathf.Sqrt(2f * gravity * jumpHeight);
        float requiredHeight = Mathf.Max(0.05f, obstacle.bounds.max.y - controller.bounds.min.y + jumpClearance);
        float discriminant = launchSpeed * launchSpeed - 2f * gravity * requiredHeight;
        if (discriminant <= 0f || Mathf.Abs(transform.position.x - (centerX + lane * player.LaneDistance)) >= 0.15f)
            return false;

        float ascent = (launchSpeed - Mathf.Sqrt(discriminant)) / gravity;
        float descent = (launchSpeed + Mathf.Sqrt(discriminant)) / gravity;
        float nearTime = (obstacle.bounds.min.z - controller.radius - transform.position.z) / speed;
        float farTime = (obstacle.bounds.max.z + controller.radius - transform.position.z) / speed;
        if (nearTime > ascent + 0.22f) return true;
        if (nearTime < ascent + 0.04f || farTime > descent - 0.04f || !JumpPathIsClear(launchSpeed, gravity, speed))
            return false;

        jumping = true;
        gliderJump = highJump;
        verticalVelocity = launchSpeed;
        if (animator != null) animator.SetFloat("JumpSpeed", 1.666667f / (2f * launchSpeed / gravity));
        return true;
    }

    private bool TryChangeLane()
    {
        int bestLane = lane;
        for (int direction = -1; direction <= 1; direction += 2)
        {
            int candidate = lane + direction;
            if (candidate < -1 || candidate > 1) continue;
            if (ObstacleInLane(candidate) != null || !LaneTransitionIsClear(candidate)) continue;
            bestLane = candidate;
        }
        if (bestLane == lane) return false;
        CommitLane(bestLane);
        return true;
    }

    /// <summary>Chooses the lateral escape with the most open road when no lane is fully clear.</summary>
    private void ForceEscapeLane(Collider blocker)
    {
        // Stay committed to the emergency lane for a beat; otherwise two
        // equally blocked neighbours make the runner jitter every tick.
        if (Time.time < forcedLaneUntil) return;

        float currentDepth = blocker != null ? blocker.bounds.min.z : NearestObstacleZ(lane);
        int bestLane = lane;
        float bestDepth = currentDepth;
        for (int direction = -1; direction <= 1; direction += 2)
        {
            int candidate = lane + direction;
            if (candidate < -1 || candidate > 1) continue;
            float depth = NearestObstacleZ(candidate);
            if (depth > bestDepth) { bestDepth = depth; bestLane = candidate; }
        }
        if (bestLane == lane)
            bestLane = stuckEscapes % 2 == 0 ? (lane == 1 ? 0 : lane + 1) : (lane == -1 ? 0 : lane - 1);
        CommitLane(bestLane);
    }

    private void CommitLane(int targetLane)
    {
        lane = targetLane;
        forcedLaneUntil = Time.time + stuckLaneHold;
    }

    private float NearestObstacleZ(int candidate)
    {
        Collider nearest = ObstacleInLane(candidate);
        return nearest != null ? nearest.bounds.min.z : float.PositiveInfinity;
    }

    /// <summary>
    /// Safety net: if the capsule stops making forward progress while it should
    /// be running (wedged, pinned by a wide row or a stale collision), jump and
    /// side-step immediately instead of standing still.
    /// </summary>
    private void TrackForwardProgress(float movedZ, float dt)
    {
        bool shouldRun = !HasFinished && !recoveringFall && !IsProjectileStunned &&
            !IsStumbling && !sliding && !jumping && controller.isGrounded;
        float actualSpeed = dt > 0f ? movedZ / dt : 0f;
        if (!shouldRun || actualSpeed > stuckSpeedThreshold)
        {
            stuckTimer = 0f;
            if (actualSpeed > stuckSpeedThreshold) stuckEscapes = 0;
            return;
        }

        stuckTimer += dt;
        if (stuckTimer < stuckTimeout || Time.time < nextStuckAttempt) return;
        stuckTimer = 0f;
        nextStuckAttempt = Time.time + stuckTimeout;
        stuckEscapes++;
        EscapeStuck();
    }

    private void EscapeStuck()
    {
        // The micro-jump lifts the capsule out of shallow overlaps and resets
        // ground contact so the next Move call can slide free.
        if (controller.isGrounded && !jumping)
        {
            jumping = true;
            gliderJump = false;
            bombLaunched = false;
            verticalVelocity = Mathf.Max(verticalVelocity, stuckJumpSpeed);
            if (animator != null)
                animator.SetFloat("JumpSpeed",
                    1.666667f / Mathf.Max(0.1f, 2f * stuckJumpSpeed / Mathf.Max(0.1f, -player.BaseGravity)));
        }

        // Pick the lane with the most open road, or force an alternating
        // side-step when all three are equally blocked, so the runner can
        // never remain motionless in the same lane.
        int bestLane = lane;
        float bestDepth = NearestObstacleZ(lane);
        for (int direction = -1; direction <= 1; direction += 2)
        {
            int candidate = lane + direction;
            if (candidate < -1 || candidate > 1) continue;
            float depth = NearestObstacleZ(candidate);
            if (depth > bestDepth) { bestDepth = depth; bestLane = candidate; }
        }
        if (bestLane == lane)
            bestLane = stuckEscapes % 2 == 0 ? (lane == 1 ? 0 : lane + 1) : (lane == -1 ? 0 : lane - 1);
        CommitLane(bestLane);

        // Repeated failed escapes mean the capsule is genuinely overlapping
        // geometry; lift it clear rather than retrying forever.
        if (stuckEscapes >= 3)
        {
            stuckEscapes = 0;
            bool wasEnabled = controller.enabled;
            controller.enabled = false;
            transform.position += Vector3.up * 0.35f;
            controller.enabled = wasEnabled;
        }
    }

    private void ChooseRiverVineLane()
    {
        if (riverVineLaneXs == null || riverVineLaneXs.Length == 0 || player == null) return;

        int bestLane = lane;
        float bestScore = float.MinValue;
        for (int i = 0; i < riverVineLaneXs.Length; i++)
        {
            int candidate = Mathf.Clamp(
                Mathf.RoundToInt((riverVineLaneXs[i] - centerX) / player.LaneDistance), -1, 1);
            float playerSeparation = Mathf.Abs(riverVineLaneXs[i] - player.transform.position.x);
            float selfDistance = Mathf.Abs(riverVineLaneXs[i] - transform.position.x);
            float score = playerSeparation * 1.25f - selfDistance * 0.25f;
            if (score > bestScore)
            {
                bestScore = score;
                bestLane = candidate;
            }
        }
        riverVineLane = bestLane;
    }

    private void ChooseBananaLane()
    {
        // A recovery side-step owns the lane briefly, so banana greed cannot
        // steer the runner straight back into the blocker it just left.
        if (Time.time < forcedLaneUntil) return;

        int count = Physics.OverlapBoxNonAlloc(new Vector3(centerX, controller.bounds.min.y + 0.8f,
            transform.position.z + 7f), new Vector3(player.LaneDistance * 1.5f, 0.8f, 7f),
            bananaProbes, Quaternion.identity, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
        int bestLane = lane;
        float bestScore = BananaScore(lane, count);
        for (int direction = -1; direction <= 1; direction += 2)
        {
            int candidate = lane + direction;
            if (candidate < -1 || candidate > 1) continue;
            float score = BananaScore(candidate, count);
            if (score <= bestScore + 0.1f || ObstacleInLane(candidate) != null || !LaneTransitionIsClear(candidate)) continue;
            bestLane = candidate;
            bestScore = score;
        }
        lane = bestLane;
    }

    private float BananaScore(int candidate, int count)
    {
        float score = 0f;
        for (int i = 0; i < count; i++)
        {
            Vector3 position = bananaProbes[i].bounds.center;
            float ahead = position.z - transform.position.z;
            if (ahead < 1.5f || Mathf.Abs(position.x - (centerX + candidate * player.LaneDistance)) > 0.8f) continue;

            // A bomb slot is worth crossing lanes for until the rival is full.
            var bomb = bananaProbes[i].GetComponentInParent<BombPickup>();
            if (bomb != null)
            {
                if (CurrentBombs < maxBombs) score += 5f / Mathf.Max(2f, ahead);
                continue;
            }

            var banana = bananaProbes[i].GetComponentInParent<BananaPickup>();
            if (banana == null || !banana.isActiveAndEnabled) continue;
            score += banana.Value / Mathf.Max(2f, ahead);
        }
        return score;
    }

    private void BeginSlide(float duration)
    {
        sliding = true;
        slideEndsAt = Time.time + duration;
        controller.radius = Mathf.Min(0.25f, standingRadius);
        controller.height = 0.55f;
        controller.center = new Vector3(standingCenter.x, standingCenter.y - standingHeight * 0.5f + 0.275f, standingCenter.z);
        if (animator != null)
        {
            animator.SetBool("Sliding", true);
            animator.SetFloat("SlideSpeed", 1f / Mathf.Max(0.1f, duration));
        }
    }

    private void TryEndSlide()
    {
        Vector3 center = transform.TransformPoint(standingCenter);
        int count = Physics.OverlapBoxNonAlloc(center, new Vector3(standingRadius, standingHeight * 0.5f - 0.08f, standingRadius),
            probes, Quaternion.identity, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
        for (int i = 0; i < count; i++)
            if (IsObstacle(probes[i])) { slideEndsAt = Time.time + 0.1f; return; }
        ResetSlide();
    }

    private void ResetSlide()
    {
        if (!sliding) return;
        sliding = false;
        controller.height = standingHeight;
        controller.radius = standingRadius;
        controller.center = standingCenter;
        if (animator != null) animator.SetBool("Sliding", false);
    }

    private bool JumpPathIsClear(float launchSpeed, float gravity, float speed)
    {
        float duration = 2f * launchSpeed / gravity;
        for (int sample = 1; sample <= 12; sample++)
        {
            float t = duration * sample / 12f;
            Vector3 center = controller.bounds.center + Vector3.forward * (speed * t) +
                Vector3.up * (launchSpeed * t - 0.5f * gravity * t * t);
            int count = Physics.OverlapBoxNonAlloc(center,
                new Vector3(controller.radius, controller.height * 0.5f - 0.08f, controller.radius),
                probes, Quaternion.identity, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
                if (IsObstacle(probes[i]) && probes[i].bounds.max.y > center.y - controller.height * 0.5f + 0.08f)
                    return false;
        }
        return true;
    }

    private bool LaneTransitionIsClear(int candidate)
    {
        float targetX = centerX + candidate * player.LaneDistance;
        // Only the destination corridor matters. The old midpoint box always
        // contained the obstacle the rival was escaping, which made every
        // adjacent lane look blocked and froze the runner in place.
        float reach = Mathf.Clamp(Mathf.Abs(targetX - transform.position.x) + 1f, 1.5f, 3.5f);
        Vector3 center = new Vector3(targetX, controller.bounds.center.y + 0.08f,
            transform.position.z + reach * 0.5f + controller.radius);
        int count = Physics.OverlapBoxNonAlloc(center,
            new Vector3(controller.radius + Mathf.Max(0f, detectionMargin) * 0.5f,
                controller.height * 0.5f - 0.08f, reach * 0.5f),
            probes, Quaternion.identity, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
        for (int i = 0; i < count; i++)
        {
            Collider hit = probes[i];
            if (!IsObstacle(hit)) continue;
            // Nothing already level with or behind the runner can pin a step.
            if (hit.bounds.max.z <= transform.position.z + controller.radius * 0.5f) continue;
            return false;
        }
        return true;
    }

    private Collider ObstacleInLane(int candidate)
    {
        float laneX = centerX + candidate * player.LaneDistance;
        float range = EffectiveLookAhead;
        float feetY = controller.bounds.min.y;
        Collider nearest = null;
        float nearestZ = float.PositiveInfinity;

        // Stacked sphere casts detect logs, barriers and rocks across the whole
        // body height, well before the old single-box scan reached them.
        float castRadius = Mathf.Max(0.15f, controller.radius * 0.85f);
        for (int sample = 0; sample < ForwardSampleHeights.Length; sample++)
        {
            Vector3 origin = new Vector3(laneX, feetY + controller.height * ForwardSampleHeights[sample],
                transform.position.z + controller.radius);
            int count = Physics.SphereCastNonAlloc(origin, castRadius, Vector3.forward, forwardHits, range,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                Collider hit = forwardHits[i].collider;
                if (!IsObstacle(hit)) continue;
                float z = hit.bounds.min.z;
                if (z < nearestZ) { nearestZ = z; nearest = hit; }
            }
        }

        // A widened swept box also catches wide/offset volumes that a cast slips
        // past and anything the capsule already overlaps.
        Vector3 center = new Vector3(laneX, controller.bounds.center.y + 0.08f,
            transform.position.z + range * 0.5f + controller.radius);
        int boxCount = Physics.OverlapBoxNonAlloc(center,
            new Vector3(controller.radius + Mathf.Max(0f, detectionMargin),
                controller.height * 0.5f - 0.08f, range * 0.5f),
            probes, Quaternion.identity, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
        for (int i = 0; i < boxCount; i++)
        {
            Collider hit = probes[i];
            if (!IsObstacle(hit)) continue;
            float z = hit.bounds.min.z;
            if (z < nearestZ) { nearestZ = z; nearest = hit; }
        }
        return nearest;
    }

    private bool IsObstacle(Collider collider) => collider != null && collider.enabled &&
        !(collider is TerrainCollider) &&
        !collider.transform.IsChildOf(transform) && !collider.transform.IsChildOf(player.transform) &&
        (IsHazard(collider) || (!collider.isTrigger && collider.bounds.max.y > controller.bounds.min.y + 0.15f));

    private static bool IsHazard(Collider collider) => collider != null && collider.enabled &&
        (collider.GetComponentInParent<RunnerObstacle>() != null ||
         collider.GetComponentInParent<BananaPeelHazard>() != null ||
         collider.GetComponentInParent<FallingCoconutHazard>() != null ||
         collider.GetComponentInParent<DynamicRunnerHazard>() != null);

    private void OnTriggerEnter(Collider other) => TouchHazard(other);
    private void OnTriggerStay(Collider other) => TouchHazard(other);
    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (hit.normal.y < 0.5f) TouchHazard(hit.collider);
    }

    private void TouchHazard(Collider other)
    {
        var obstacle = other.GetComponentInParent<RunnerObstacle>();
        if (HasBulldozer && obstacle != null)
        {
            obstacle.Shatter();
            return;
        }
        if (sliding && obstacle != null && obstacle.CanSlideUnder && other.bounds.min.y >= controller.bounds.max.y) return;
        if (IsObstacle(other) && other.bounds.max.y > controller.bounds.min.y + 0.08f && Time.time >= protectedUntil) Stumble();
    }

    public void CollectPowerUp(PowerUpType type)
    {
        if (HasFinished) return;
        switch (type)
        {
            case PowerUpType.Bulldozer:
                bulldozerEndsAt = Time.time + PowerUpManager.BulldozerDurationSeconds;
                break;
            case PowerUpType.Magnet:
                magnetEndsAt = Time.time + PowerUpManager.MagnetDurationSeconds;
                break;
            case PowerUpType.Glider:
                highJumpEndsAt = Time.time + PowerUpManager.GliderDurationSeconds;
                // Collecting the high-jump pickup while airborne should extend
                // the current arc instead of waiting for the next obstacle.
                if (controller != null && !controller.isGrounded) gliderJump = true;
                break;
            case PowerUpType.Shield:
                shieldEndsAt = Time.time + PowerUpManager.ShieldDurationSeconds;
                break;
            case PowerUpType.Thunder:
                thunderEndsAt = Time.time + PowerUpManager.ThunderDurationSeconds;
                setbackEndsAt = Mathf.Min(setbackEndsAt, Time.time);
                break;
        }
    }

    public bool TryConsumeShield()
    {
        if (!HasShield) return false;
        shieldEndsAt = 0f;
        GetComponent<RunnerAbilityVisual>()?.PlayShieldBreak();
        return true;
    }

    /// <summary>Secondary ammo slot mirroring the player's bomb inventory.</summary>
    public bool TryCollectBomb()
    {
        if (HasFinished || recoveringFall || CurrentBombs >= maxBombs) return false;
        CurrentBombs++;
        return true;
    }

    /// <summary>Places one rival-owned trap directly behind the runner.</summary>
    public bool TryDropBomb()
    {
        if (CurrentBombs <= 0 || HasFinished || recoveringFall || sliding || IsStumbling ||
            !IsGrounded || Time.time < nextBombDrop) return false;

        var prefab = bombTrapPrefab;
        if (prefab == null && player != null)
        {
            var source = player.GetComponent<BombDropper>();
            if (source != null) prefab = source.TrapPrefab;
        }
        if (prefab == null) return false;

        Vector3 point = transform.position + Vector3.back * bombDropDistance;
        int count = Physics.RaycastNonAlloc(point + Vector3.up * 1.5f, Vector3.down,
            bombGroundHits, 12f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float distance = float.PositiveInfinity;
        bool grounded = false;
        for (int i = 0; i < count; i++)
        {
            var hit = bombGroundHits[i];
            if (hit.collider.GetComponentInParent<RunnerMotor>() != null ||
                hit.collider.GetComponentInParent<RivalRunner>() != null ||
                hit.collider.GetComponentInParent<RunnerObstacle>() != null || hit.normal.y < 0.65f ||
                hit.distance >= distance) continue;
            point.y = hit.point.y;
            distance = hit.distance;
            grounded = true;
        }
        var river = RiverCrocodileGauntlet.Active;
        if (!grounded || (river != null && river.IsInsideRiverGap(point.z))) return false;

        var trap = Instantiate(prefab, point, Quaternion.identity);
        trap.SetOwner(transform);
        // Rival trap audio stays muted; only the player's drops make a sound.
        CurrentBombs--;
        nextBombDrop = Time.time + bombDropCooldown;
        return true;
    }

    private void ConsiderBombDrop()
    {
        if (CurrentBombs <= 0 || Time.time < nextBombDrop || player == null ||
            !IsGrounded || sliding || IsStumbling) return;

        // Traps go behind the runner, so they only make sense once the player
        // is trailing nearby; otherwise the rival saves the ammo.
        float gap = transform.position.z - player.transform.position.z;
        if (gap < 2f || gap > bombDropRange) return;
        if (Mathf.Abs(player.transform.position.x - transform.position.x) > player.LaneDistance * 1.25f) return;
        if (!TryDropBomb()) return;
        nextBombDrop = Time.time + Random.Range(bombDropMinInterval, bombDropMaxInterval);
    }

    private void UpdateGiantScale()
    {
        if (rivalModel == null) return;
        Vector3 target = originalModelScale * (HasBulldozer ? giantScale : 1f);
        rivalModel.localScale = Vector3.Lerp(rivalModel.localScale, target,
            1f - Mathf.Exp(-giantScaleSmoothing * Time.deltaTime));
    }

    public bool TakeProjectileHit()
    {
        if (HasFinished || recoveringFall || Time.time < projectileProtectionEndsAt) return false;
        if (HasBulldozer || HasThunder) return false;
        if (TryConsumeShield())
        {
            projectileProtectionEndsAt = Time.time + 0.4f;
            return false;
        }
        projectileStunEndsAt = Time.time + projectileStunDuration;
        projectileProtectionEndsAt = projectileStunEndsAt + 0.4f;
        setbackEndsAt = Mathf.Max(setbackEndsAt, projectileStunEndsAt);
        protectedUntil = Mathf.Max(protectedUntil, projectileStunEndsAt + 0.4f);
        if (animator != null)
        {
            animator.SetBool("Sliding", false);
            animator.SetFloat("StumbleSpeed", 1f / projectileStunDuration);
        }
        Animate(HitState);
        Impacted?.Invoke();
        return true;
    }

    public bool TakeBombHit(float launchSpeed, float stunSeconds)
    {
        if (!TakeProjectileHit()) return false;
        ResetSlide();
        jumping = true;
        gliderJump = false;
        bombLaunched = true;
        verticalVelocity = Mathf.Max(1f, launchSpeed);
        projectileStunEndsAt = Time.time + Mathf.Max(0.1f, stunSeconds);
        projectileProtectionEndsAt = projectileStunEndsAt + 0.4f;
        setbackEndsAt = Mathf.Max(setbackEndsAt, projectileStunEndsAt);
        var playerAnimation = player != null ? player.GetComponent<RunnerAnimation>() : null;
        RunnerHitReaction.Play(animator, playerAnimation != null ? playerAnimation.HitClipLength : 1f, stunSeconds);
        animationState = HitState;
        return true;
    }

    public void Stumble(float duration = -1f)
    {
        if (HasFinished || recoveringFall || IsStumbling || HasThunder) return;
        if (TryConsumeShield())
        {
            protectedUntil = Mathf.Max(protectedUntil, Time.time + 0.5f);
            return;
        }
        setbackEndsAt = Time.time + (duration > 0f ? duration : stumbleDuration);
        protectedUntil = setbackEndsAt + 0.75f;
        setbackMultiplier = 0.25f;
        if (animator != null)
        {
            animator.SetBool("Sliding", false);
            animator.SetFloat("StumbleSpeed", 1f / Mathf.Max(0.1f, setbackEndsAt - Time.time));
        }
        Animate(HitState);
        Impacted?.Invoke();
    }

    public void Fall(float duration = -1f)
    {
        if (HasFinished || recoveringFall) return;
        recoveringFall = true;
        ResetSlide();
        jumping = false;
        setbackEndsAt = Time.time + (duration > 0f ? duration : fallDuration);
        protectedUntil = setbackEndsAt + 0.75f;
        if (animator != null) animator.SetFloat("StumbleSpeed", 1f / Mathf.Max(0.1f, setbackEndsAt - Time.time));
        Animate(HitState);
        Impacted?.Invoke();
    }

    /// <summary>Freezes the rival while the crocodile-lake respawn delay runs.</summary>
    public void BeginWaterFall()
    {
        if (HasFinished) return;
        enabled = false;
        if (controller != null) controller.enabled = false;
        if (animator != null)
        {
            animator.SetBool("Sliding", false);
            animator.SetBool("VineAttached", false);
        }
        jumping = false;
        recoveringFall = false;
        setbackEndsAt = Time.time;
        projectileStunEndsAt = Time.time;
        projectileProtectionEndsAt = Time.time;
    }

    /// <summary>Resumes the rival on the far bank after the water delay.</summary>
    public void RespawnFromWater(Vector3 position, float invulnerability)
    {
        if (HasFinished) return;

        if (controller != null)
        {
            controller.enabled = false;
            transform.position = position;
            controller.enabled = true;
        }
        transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);

        verticalVelocity = -2f;
        jumping = false;
        recoveringFall = false;
        setbackEndsAt = Time.time;
        setbackMultiplier = 1f;
        projectileStunEndsAt = Time.time;
        projectileProtectionEndsAt = Time.time;
        protectedUntil = Time.time + Mathf.Max(0.1f, invulnerability);
        lastGroundedPosition = position;
        if (animator != null)
        {
            animator.SetBool("Sliding", false);
            animator.SetBool("VineAttached", false);
            animator.SetFloat("JumpSpeed", 1f);
        }
        animationState = -1;
        enabled = true;
        Animate(RunState);
    }

    /// <summary>Reclaims the rival after a successful lane-vine swing.</summary>
    public void ResumeFromVine(Vector3 landing)
    {
        if (HasFinished) return;

        if (controller != null)
        {
            controller.enabled = false;
            transform.position = landing;
            controller.enabled = true;
        }
        verticalVelocity = -2f;
        jumping = false;
        recoveringFall = false;
        setbackEndsAt = Time.time;
        setbackMultiplier = 1f;
        protectedUntil = Mathf.Max(protectedUntil, Time.time + 0.5f);
        lastGroundedPosition = landing;
        if (animator != null)
        {
            animator.SetBool("Sliding", false);
            animator.SetBool("VineAttached", false);
            animator.SetFloat("JumpSpeed", 1f);
        }
        animationState = -1;
        enabled = true;
        Animate(RunState);
    }

    public void FinishRace()
    {
        HasFinished = true;
        bulldozerEndsAt = 0f;
        ResetSlide();
        RestoreModelScale();
        Animate(IdleState);
    }

    private void RestoreModelScale()
    {
        if (rivalModel != null) rivalModel.localScale = originalModelScale;
    }

    private void OnDisable()
    {
        ResetSlide();
        RestoreModelScale();
    }

    private void OnDestroy()
    {
        if (ActiveRival == this) ActiveRival = null;
    }

    private void Animate(int state)
    {
        if (animator == null || animationState == state || !animator.HasState(0, state)) return;
        animationState = state;
        animator.CrossFadeInFixedTime(state, 0.12f, 0, 0f);
    }
}
