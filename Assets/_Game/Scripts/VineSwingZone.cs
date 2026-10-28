using UnityEngine;

/// <summary>Owns movement until landing; supports the player and the rival.</summary>
[DefaultExecutionOrder(200)]
[RequireComponent(typeof(BoxCollider), typeof(Rigidbody))]
public class VineSwingZone : MonoBehaviour
{
    [SerializeField] private Transform landingPoint;
    [SerializeField] private Transform vinePivot;
    [SerializeField] private LineRenderer vineLine;
    [SerializeField, Min(0.5f)] private float swingDuration = 2.1f;
    [SerializeField, Min(0.5f)] private float flightDuration = 1.5f;
    [SerializeField] private Transform restingVine;
    [SerializeField] private ParticleSystem anchorLeaves;
    [SerializeField] private Vector3 palmOffset = new Vector3(0, 0.06f, 0);
    [SerializeField] private bool allowRival = true;

    private RunnerMotor motor;
    private RivalRunner rivalRunner;
    private RunnerInput input;
    private RunnerAnimation animationDriver;
    private CharacterController controller;
    private PlayerHealth health;
    private Animator animator;
    private Transform runner, hand, visual;
    private Vector3 visualPosition, end, grip, pivot, forward, flightStart, launchVelocity;
    private Quaternion visualRotation;
    private bool motorEnabled, inputEnabled, controllerEnabled, released, rivalSwing;
    private float elapsed, length, angle, cooldown, gravity = -21f;
    private GameObject bananaPrefab;
    private int bananaCount;
    private BananaPickup[] swingBananas;
    private BananaInventory bananaInventory;
    private bool bananasSpawned;
    private Vector3 previousBananaProbe;

    private static readonly int VineState = Animator.StringToHash("Base Layer.VineSwing");
    private static readonly int JumpState = Animator.StringToHash("Base Layer.Jump");

    public bool IsSwinging => runner != null;
    public bool IsReleased => released;
    public Vector3 GripPosition => grip;
    public float GripError => hand == null ? 0f : Vector3.Distance(hand.TransformPoint(palmOffset), grip);
    public ParticleSystem AnchorLeaves => anchorLeaves;

    /// <summary>Assigns the runtime-cloned vine parts without touching scene YAML.</summary>
    public void ConfigureRuntime(Transform landing, Transform pivotTransform, LineRenderer line,
        Transform resting, ParticleSystem leaves)
    {
        landingPoint = landing;
        vinePivot = pivotTransform;
        vineLine = line;
        restingVine = resting;
        anchorLeaves = leaves;
        if (vineLine != null)
        {
            vineLine.useWorldSpace = true;
            vineLine.positionCount = 2;
            vineLine.enabled = false;
        }
    }

    private void Awake()
    {
        GetComponent<BoxCollider>().isTrigger = true;
        var body = GetComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        if (vineLine != null) { vineLine.useWorldSpace = true; vineLine.positionCount = 2; vineLine.enabled = false; }
    }

    private void OnTriggerEnter(Collider other) => TryGrab(other);
    private void OnTriggerStay(Collider other) => TryGrab(other);

    private void TryGrab(Collider other)
    {
        if (IsSwinging || Time.time < cooldown || other == null) return;

        var candidate = other.GetComponentInParent<RunnerMotor>();
        if (candidate != null)
        {
            TryGrabPlayer(candidate);
            return;
        }

        if (!allowRival) return;
        var rival = other.GetComponentInParent<RivalRunner>();
        if (rival != null) TryGrabRival(rival);
    }

    private void TryGrabPlayer(RunnerMotor candidate)
    {
        if (candidate == null || !candidate.enabled || candidate.IsSliding ||
            !candidate.CompareTag("Player")) return;
        var driver = candidate.GetComponent<RunnerAnimation>();
        var playerHealth = candidate.GetComponent<PlayerHealth>();
        var driverAnimator = candidate.GetComponentInChildren<Animator>();
        if (driver == null || driver.IsVineControlled || playerHealth == null || playerHealth.IsRunOver ||
            driverAnimator == null) return;

        CharacterController candidateController = candidate.GetComponent<CharacterController>();
        if (!PrepareSwing(candidate.transform, candidateController, driverAnimator, candidate.BaseGravity)) return;

        motor = candidate;
        health = playerHealth;
        input = candidate.GetComponent<RunnerInput>();
        animationDriver = driver;
        motorEnabled = candidate.enabled;
        inputEnabled = input != null && input.enabled;
        rivalSwing = false;
        BeginSwing();
    }

    private void TryGrabRival(RivalRunner candidate)
    {
        if (candidate == null || !candidate.enabled || candidate.HasFinished || candidate.IsStumbling ||
            candidate.IsRecoveringFromFall || candidate.IsSliding) return;

        CharacterController candidateController = candidate.GetComponent<CharacterController>();
        var candidateAnimator = candidate.GetComponentInChildren<Animator>();
        if (!PrepareSwing(candidate.transform, candidateController, candidateAnimator, candidate.BaseGravity)) return;

        rivalRunner = candidate;
        rivalSwing = true;
        motor = null;
        health = null;
        input = null;
        animationDriver = null;
        BeginSwing();
    }

    /// <summary>Validates and snapshots a runner before either the player or rival takes the vine.</summary>
    private bool PrepareSwing(Transform candidateRunner, CharacterController candidateController,
        Animator candidateAnimator, float baseGravity)
    {
        if (candidateRunner == null || candidateController == null || !candidateController.enabled ||
            candidateAnimator == null || !candidateAnimator.isHuman ||
            !candidateAnimator.HasState(0, VineState) || landingPoint == null || vinePivot == null) return false;

        Transform candidateHand = candidateAnimator.GetBoneTransform(HumanBodyBones.LeftHand);
        if (candidateHand == null) return false;

        Vector3 start = candidateRunner.position;
        Vector3 candidateEnd = landingPoint.position;
        float bottom = candidateController.center.y - candidateController.height * 0.5f;
        if (Physics.Raycast(candidateEnd + Vector3.up * 5f, Vector3.down, out var ground, 10f,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            candidateEnd.y = ground.point.y - bottom + candidateController.skinWidth;

        Vector3 candidatePivot = vinePivot.position;
        Vector3 candidateForward = Vector3.ProjectOnPlane(candidatePivot - start, Vector3.up).normalized;
        if (candidateForward.sqrMagnitude < 0.01f) return false;
        Vector3 catchPoint = start + Vector3.up * 2.4f;
        float horizontal = Vector3.ProjectOnPlane(catchPoint - candidatePivot, Vector3.up).magnitude;
        float vertical = Mathf.Max(1f, candidatePivot.y - catchPoint.y);

        runner = candidateRunner;
        controller = candidateController;
        animator = candidateAnimator;
        hand = candidateHand;
        visual = candidateAnimator.transform;
        visualPosition = visual.localPosition;
        visualRotation = visual.localRotation;
        controllerEnabled = controller.enabled;
        gravity = baseGravity;
        end = candidateEnd;
        pivot = candidatePivot;
        forward = candidateForward;
        length = Mathf.Sqrt(horizontal * horizontal + vertical * vertical);
        angle = Mathf.Atan2(horizontal, vertical);
        elapsed = 0f;
        released = false;
        controller.enabled = false;
        return true;
    }

    private void BeginSwing()
    {
        if (!rivalSwing)
        {
            if (motor != null) motor.enabled = false;
            if (input != null) input.enabled = false;
            if (animationDriver != null) animationDriver.BeginVineSwing();
        }
        else if (rivalRunner != null)
        {
            rivalRunner.enabled = false;
            if (animator != null)
            {
                animator.SetBool("Sliding", false);
                animator.SetBool("VineAttached", true);
                animator.SetFloat("JumpSpeed", 1f);
                animator.CrossFadeInFixedTime(VineState, 0.16f, 0, 0f);
            }
        }

        if (restingVine != null) restingVine.gameObject.SetActive(false);
        if (vineLine != null) vineLine.enabled = true;
        if (anchorLeaves != null) anchorLeaves.Play(true);
        UpdateGrip(0f);
        if (!rivalSwing) SpawnBananaTrail();
        previousBananaProbe = grip;
    }

    private void UpdateGrip(float t)
    {
        grip = SwingPoint(t);
    }

    private Vector3 SwingPoint(float t)
    {
        // Harmonic pendulum: zero speed at both apexes, peak speed at the bottom.
        float theta = -angle * Mathf.Cos(Mathf.PI * t);
        return pivot + forward * (Mathf.Sin(theta) * length) - Vector3.up * (Mathf.Cos(theta) * length);
    }

    private void SpawnBananaTrail()
    {
        if (bananasSpawned) return;
        foreach (var course in FindObjectsByType<RunnerCourseGenerator>())
            if (course.Player == motor) { bananaPrefab = course.BananaPrefab; bananaCount = course.RopeBananaCount; break; }
        bananaCount = Mathf.Clamp(bananaCount, 0, 24);
        if (bananaPrefab == null || bananaCount <= 0) return;
        bananasSpawned = true;
        bananaInventory = motor.GetComponent<BananaInventory>();
        swingBananas = new BananaPickup[bananaCount];
        for (int i = 0; i < bananaCount; i++)
        {
            Vector3 point = SwingPoint((i + 1f) / (bananaCount + 1f));
            var pickup = Instantiate(bananaPrefab, point, bananaPrefab.transform.rotation, transform);
            var sphere = pickup.GetComponent<SphereCollider>();
            if (sphere != null) pickup.transform.position += point - pickup.transform.TransformPoint(sphere.center);
            swingBananas[i] = pickup.GetComponent<BananaPickup>();
        }
    }

    private void CollectSwingBananas(Vector3 probe)
    {
        if (swingBananas == null || bananaInventory == null) return;
        Vector3 step = probe - previousBananaProbe;
        for (int i = 0; i < swingBananas.Length; i++)
        {
            var banana = swingBananas[i];
            if (banana == null || !banana.gameObject.activeSelf) continue;
            Vector3 center = banana.GetComponent<SphereCollider>().bounds.center;
            float t = step.sqrMagnitude > 0.0001f ? Mathf.Clamp01(Vector3.Dot(center - previousBananaProbe, step) / step.sqrMagnitude) : 0f;
            if ((center - (previousBananaProbe + step * t)).sqrMagnitude <= 0.85f * 0.85f)
                banana.TryCollect(bananaInventory);
        }
        previousBananaProbe = probe;
    }

    private void Update()
    {
        if (!IsSwinging) return;
        if (rivalSwing)
        {
            if (rivalRunner == null || rivalRunner.HasFinished) { Restore(false); return; }
        }
        else if (health == null || health.IsRunOver) { Restore(false); return; }

        elapsed += Time.deltaTime;
        if (!released)
        {
            float t = Mathf.Clamp01(elapsed / swingDuration);
            UpdateGrip(t);
        }
        else
        {
            float t = Mathf.Min(elapsed, flightDuration);
            runner.position = flightStart + launchVelocity * t + Vector3.up * (0.5f * gravity * t * t);
            visual.localRotation = Quaternion.Slerp(visual.localRotation, visualRotation, 1f - Mathf.Exp(-12f * Time.deltaTime));
            visual.localPosition = Vector3.Lerp(visual.localPosition, visualPosition, 1f - Mathf.Exp(-12f * Time.deltaTime));
            if (elapsed >= flightDuration) { runner.position = end; Restore(true); }
        }
    }

    private void LateUpdate()
    {
        if (!IsSwinging || released) return;
        float catchBlend = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / 0.22f));
        visual.localRotation = visualRotation;
        visual.rotation = Quaternion.Slerp(Quaternion.identity,
            Quaternion.FromToRotation(Vector3.up, (pivot - grip).normalized), catchBlend) * visual.rotation;
        runner.position += (grip - hand.TransformPoint(palmOffset)) * catchBlend;
        if (!rivalSwing) CollectSwingBananas(hand.TransformPoint(palmOffset));
        if (vineLine != null)
        {
            vineLine.SetPosition(0, pivot);
            vineLine.SetPosition(1, hand.TransformPoint(palmOffset));
        }
        if (elapsed < swingDuration) return;
        released = true;
        elapsed = 0f;
        flightStart = runner.position;
        // Deliberate push-off at the forward apex; gravity solves the safe landing.
        launchVelocity = (end - flightStart) / flightDuration - Vector3.up * (0.5f * gravity * flightDuration);
        if (rivalSwing)
        {
            if (animator != null)
            {
                animator.SetBool("VineAttached", false);
                animator.SetFloat("JumpSpeed", 1f);
                animator.CrossFadeInFixedTime(JumpState, 0.12f, 0, 0f);
            }
        }
        else if (animationDriver != null)
        {
            animationDriver.ReleaseVine(flightDuration);
        }
    }

    private void Restore(bool landed)
    {
        if (runner != null)
        {
            if (!landed) runner.position = end;
            if (visual != null) { visual.localPosition = visualPosition; visual.localRotation = visualRotation; }
            if (controller != null) controller.enabled = controllerEnabled;

            if (rivalRunner != null)
            {
                rivalRunner.ResumeFromVine(end);
            }
            else if (motor != null)
            {
                motor.ResetVerticalVelocity();
                if (animationDriver != null) animationDriver.EndVineSwing();
                bool alive = health != null && !health.IsRunOver;
                motor.enabled = motorEnabled && alive;
                if (input != null) input.enabled = inputEnabled && alive;
            }
        }

        runner = null;
        motor = null;
        rivalRunner = null;
        input = null;
        animationDriver = null;
        controller = null;
        health = null;
        animator = null;
        hand = null;
        visual = null;
        rivalSwing = false;
        released = false;
        cooldown = Time.time + 1f;
        if (vineLine != null) vineLine.enabled = false;
        if (restingVine != null) restingVine.gameObject.SetActive(true);
    }

    private void OnDisable() => Restore(false);
}
