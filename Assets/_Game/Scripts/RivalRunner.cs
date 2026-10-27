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
    [SerializeField, Min(1f)] private float lookAhead = 9f;
    [SerializeField, Min(0.1f)] private float jumpClearance = 0.2f;
    [SerializeField, Min(0.1f)] private float stumbleDuration = 0.85f;
    [SerializeField, Min(0.1f)] private float fallDuration = 1.5f;
    [SerializeField, Min(0.1f)] private float slideDuration = 0.95f;
    [SerializeField, Min(0.1f)] private float projectileStunDuration = 2f;
    [Header("River crossing (takeoff and landing world Z)")]
    [SerializeField] private Vector2 riverCrossing = new Vector2(607f, 646f);
    [SerializeField, Min(0.1f)] private float crossingHeight = 4.5f;

    private readonly Collider[] probes = new Collider[32];
    private readonly Collider[] bananaProbes = new Collider[96];
    private CharacterController controller;
    private float centerX, verticalVelocity, setbackEndsAt, setbackMultiplier = 1f;
    private float protectedUntil, nextDecision, crossingStartY, crossingStartZ;
    private Vector3 lastGroundedPosition;
    private int lane = 1, animationState;
    private bool crossing, recoveringFall, jumping, blocked;
    private bool sliding;
    private float slideEndsAt, standingHeight, standingRadius;
    private Vector3 standingCenter;
    private BananaInventory inventory;
    private float projectileStunEndsAt, projectileProtectionEndsAt;

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
    public bool IsJumping => jumping || crossing;
    public bool IsGrounded => controller != null && controller.enabled && controller.isGrounded && !IsJumping;
    public bool IsProjectileStunned => Time.time < projectileStunEndsAt;
    public bool IsStumbling => Time.time < setbackEndsAt || IsProjectileStunned;
    public bool IsRecoveringFromFall => recoveringFall;
    public bool HasFinished { get; private set; }
    public event System.Action Impacted;
    private float RunningPace => player == null ? 0f :
        player.SpeedAtDifficulty(course != null ? course.DifficultyAt(transform.position.z) : 0f) * paceMultiplier;
    public float CurrentSpeed => HasFinished || recoveringFall || blocked || IsProjectileStunned ? 0f :
        RunningPace * (IsStumbling ? setbackMultiplier : 1f);

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        inventory = GetComponent<BananaInventory>();
        if (inventory == null) inventory = gameObject.AddComponent<BananaInventory>();
        standingHeight = controller.height;
        standingRadius = controller.radius;
        standingCenter = controller.center;
        if (animator == null) animator = GetComponentInChildren<Animator>();
        lastGroundedPosition = transform.position;
        centerX = player != null ? player.transform.position.x : transform.position.x;
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
    }

    private void Update()
    {
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
        if (controller.isGrounded && !crossing && (!jumping || verticalVelocity <= 0f))
        {
            jumping = false;
            lastGroundedPosition = transform.position;
            verticalVelocity = -2f;
            if (!IsStumbling && riverCrossing.y > riverCrossing.x &&
                transform.position.z >= riverCrossing.x && transform.position.z < riverCrossing.y)
            {
                crossing = true;
                crossingStartZ = transform.position.z;
                crossingStartY = transform.position.y;
                float duration = (riverCrossing.y - crossingStartZ) / Mathf.Max(1f, CurrentSpeed);
                if (animator != null) animator.SetFloat("JumpSpeed", 1.666667f / duration);
            }
        }
        if (!IsStumbling && !sliding && IsGrounded && Time.time >= nextDecision)
        {
            nextDecision = Time.time + 0.06f;
            ChooseObstacleAction();
        }

        float x = IsProjectileStunned ? transform.position.x :
            Mathf.MoveTowards(transform.position.x, centerX + lane * player.LaneDistance, 12f * dt);
        float dz = CurrentSpeed * dt;
        float dy;
        if (crossing)
        {
            float t = Mathf.InverseLerp(crossingStartZ, riverCrossing.y, transform.position.z + dz);
            dy = crossingStartY + 4f * crossingHeight * t * (1f - t) - transform.position.y;
            if (t >= 1f) { crossing = false; verticalVelocity = -2f; }
        }
        else
        {
            verticalVelocity += player.BaseGravity * dt;
            dy = verticalVelocity * dt;
        }
        var collision = controller.Move(new Vector3(x - transform.position.x, dy, dz));
        if ((collision & CollisionFlags.Above) != 0) verticalVelocity = Mathf.Min(0f, verticalVelocity);
        if (transform.position.y < lastGroundedPosition.y - 3f) Fall();
        Animate(IsStumbling ? HitState : crossing || !controller.isGrounded ? JumpState : sliding ? SlideState : RunState);
    }

    private void ChooseObstacleAction()
    {
        blocked = false;
        Collider obstacle = ObstacleInLane(lane);
        if (obstacle == null) { ChooseBananaLane(); return; }
        float speed = Mathf.Max(1f, RunningPace);
        var slideObstacle = obstacle.GetComponentInParent<RunnerObstacle>();
        if (slideObstacle != null && slideObstacle.CanSlideUnder &&
            obstacle.bounds.min.y > controller.bounds.min.y + 0.65f)
        {
            float distance = obstacle.bounds.min.z - controller.bounds.max.z;
            if (distance <= speed * 0.35f)
                BeginSlide(Mathf.Max(slideDuration, (obstacle.bounds.max.z - transform.position.z + 1f) / speed));
            return;
        }
        float gravity = Mathf.Max(0.1f, -player.BaseGravity);
        float launchSpeed = Mathf.Sqrt(2f * gravity * player.NormalJumpHeight);
        float requiredHeight = Mathf.Max(0.05f, obstacle.bounds.max.y - controller.bounds.min.y + jumpClearance);
        float discriminant = launchSpeed * launchSpeed - 2f * gravity * requiredHeight;
        if (discriminant > 0f && Mathf.Abs(transform.position.x - (centerX + lane * player.LaneDistance)) < 0.15f)
        {
            float ascent = (launchSpeed - Mathf.Sqrt(discriminant)) / gravity;
            float descent = (launchSpeed + Mathf.Sqrt(discriminant)) / gravity;
            float nearTime = (obstacle.bounds.min.z - controller.radius - transform.position.z) / speed;
            float farTime = (obstacle.bounds.max.z + controller.radius - transform.position.z) / speed;
            if (nearTime > ascent + 0.22f) return;
            if (nearTime >= ascent + 0.04f && farTime <= descent - 0.04f && JumpPathIsClear(launchSpeed, gravity, speed))
            {
                // Wait until takeoff is close enough; do not jump at the first long-range detection.
                if (nearTime <= ascent + 0.22f)
                {
                    jumping = true;
                    verticalVelocity = launchSpeed;
                    if (animator != null) animator.SetFloat("JumpSpeed", 1.666667f / (2f * launchSpeed / gravity));
                }
                return;
            }
        }
        // Tall/wide obstacles and low ceilings require an unobstructed lane transition.
        for (int direction = -1; direction <= 1; direction += 2)
        {
            int candidate = lane + direction;
            if (candidate < -1 || candidate > 1 || ObstacleInLane(candidate) != null || !LaneTransitionIsClear(candidate)) continue;
            lane = candidate;
            return;
        }
        // An impossible row costs distance rather than deliberately running into it.
        blocked = obstacle.bounds.min.z - controller.bounds.max.z < 1.5f;
    }

    private void ChooseBananaLane()
    {
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
            var banana = bananaProbes[i].GetComponentInParent<BananaPickup>();
            if (banana == null || !banana.isActiveAndEnabled) continue;
            Vector3 position = bananaProbes[i].bounds.center;
            float ahead = position.z - transform.position.z;
            if (ahead < 1.5f || Mathf.Abs(position.x - (centerX + candidate * player.LaneDistance)) > 0.8f) continue;
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
        float transitionDistance = Mathf.Abs(targetX - transform.position.x);
        float forwardDistance = RunningPace * transitionDistance / 12f;
        Vector3 center = new Vector3((transform.position.x + targetX) * 0.5f,
            controller.bounds.center.y + 0.08f, transform.position.z + forwardDistance * 0.5f);
        int count = Physics.OverlapBoxNonAlloc(center,
            new Vector3(transitionDistance * 0.5f + controller.radius, controller.height * 0.5f - 0.08f,
                forwardDistance * 0.5f + controller.radius), probes, Quaternion.identity,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
        for (int i = 0; i < count; i++) if (IsObstacle(probes[i])) return false;
        return true;
    }

    private Collider ObstacleInLane(int candidate)
    {
        Vector3 probe = new Vector3(centerX + candidate * player.LaneDistance,
            controller.bounds.center.y + 0.08f, transform.position.z + lookAhead * 0.5f + controller.radius);
        int count = Physics.OverlapBoxNonAlloc(probe,
            new Vector3(controller.radius, controller.height * 0.5f - 0.08f, lookAhead * 0.5f),
            probes, Quaternion.identity, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
        Collider nearest = null;
        for (int i = 0; i < count; i++)
            if (IsObstacle(probes[i]) && (nearest == null || probes[i].bounds.min.z < nearest.bounds.min.z)) nearest = probes[i];
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
        if (sliding && obstacle != null && obstacle.CanSlideUnder && other.bounds.min.y >= controller.bounds.max.y) return;
        if (IsObstacle(other) && other.bounds.max.y > controller.bounds.min.y + 0.08f && Time.time >= protectedUntil) Stumble();
    }

    public bool TakeProjectileHit()
    {
        if (HasFinished || recoveringFall || Time.time < projectileProtectionEndsAt) return false;
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

    public void Stumble(float duration = -1f)
    {
        if (HasFinished || recoveringFall || IsStumbling) return;
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
        blocked = false;
        crossing = false;
        setbackEndsAt = Time.time + (duration > 0f ? duration : fallDuration);
        protectedUntil = setbackEndsAt + 0.75f;
        if (animator != null) animator.SetFloat("StumbleSpeed", 1f / Mathf.Max(0.1f, setbackEndsAt - Time.time));
        Animate(HitState);
        Impacted?.Invoke();
    }

    public void FinishRace()
    {
        HasFinished = true;
        ResetSlide();
        Animate(IdleState);
    }

    private void OnDisable() => ResetSlide();

    private void Animate(int state)
    {
        if (animator == null || animationState == state || !animator.HasState(0, state)) return;
        animationState = state;
        animator.CrossFadeInFixedTime(state, 0.12f, 0, 0f);
    }
}
