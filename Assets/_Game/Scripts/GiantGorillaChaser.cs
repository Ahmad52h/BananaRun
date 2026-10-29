using UnityEngine;

/// <summary>
/// Stays offscreen until a runner is hit, surges in briefly, then retreats.
/// Also catches the player when the run fails. A trigger receives secondary bomb traps.
/// </summary>
public sealed class GiantGorillaChaser : MonoBehaviour
{
    private enum VisibilityState
    {
        Hidden,
        HitReveal,
        Retreating,
        Catching
    }

    private static readonly int CatchTrigger = Animator.StringToHash("Catch");
    private static readonly int ResetRunTrigger = Animator.StringToHash("ResetRun");

    [Header("References")]
    [SerializeField] private RunnerMotor player;
    [SerializeField] private RivalRunner rival;
    [SerializeField] private GameObject gorillaModel;
    [SerializeField] private Avatar gorillaAvatar;
    [SerializeField] private RuntimeAnimatorController runController;
    [SerializeField] private Material gorillaMaterial;
    [SerializeField] private AudioClip gruntClip;

    [Header("Chase")]
    [SerializeField, Min(0.01f)] private float laneDelay = 0.32f;
    [SerializeField, Min(0.01f)] private float forwardSmoothing = 0.13f;
    [SerializeField, Min(0.1f)] private float visualScale = 1f;
    [SerializeField] private float visualYaw;
    [Tooltip("Local offset for the spawned visual. X shifts the gorilla out of the camera's center so the player stays visible; Y fine-tunes the auto-grounding.")]
    [SerializeField] private Vector3 visualOffset = new Vector3(1.2f, 0f, 0f);

    [Header("Visibility Windows")]
    [Tooltip("Distance behind the hit runner where the gorilla surges into view.")]
    [SerializeField, Min(2f)] private float hitRevealGap = 2.6f;
    [Tooltip("Distance behind the runner where the gorilla is fully out of the camera frustum.")]
    [SerializeField, Min(7.5f)] private float hiddenGap = 10.5f;
    [Tooltip("Minimum time the gorilla stays visible after a hit.")]
    [SerializeField, Min(0.5f)] private float hitRevealSeconds = 3.5f;
    [Tooltip("Extra time the gorilla stays visible after the hit runner is back at full speed.")]
    [SerializeField, Min(0f)] private float recoveryLingerSeconds = 0.35f;
    [Tooltip("How quickly the gorilla closes from the hidden gap into hit-reveal range.")]
    [SerializeField, Min(1f)] private float revealApproachSpeed = 24f;
    [Tooltip("Time spent smoothly moving from the reveal gap back to the hidden gap.")]
    [SerializeField, Min(0.1f)] private float retreatSeconds = 1.1f;

    [Header("Game Over Catch")]
    [Tooltip("Beat where the monkey slumps before the gorilla surges in and grabs.")]
    [SerializeField, Range(0f, 2f)] private float catchWindupSeconds = 0.65f;
    [Tooltip("Distance behind the player while the gorilla catches and lifts him.")]
    [SerializeField, Range(1.1f, 1.4f)] private float catchGap = 1.2f;
    [Tooltip("How quickly the gorilla surges into catch range on game over.")]
    [SerializeField, Min(1f)] private float catchApproachSpeed = 30f;
    [Tooltip("Seconds into Mutant Swiping when the active hand reaches the monkey.")]
    [SerializeField, Min(0f)] private float catchGrabTime = 0.45f;
    [Tooltip("Seconds into Mutant Swiping to freeze the pose at the top of the lift.")]
    [SerializeField, Min(0f)] private float catchFreezeTime = 1.1f;
    [Tooltip("Faster lane alignment while the gorilla is catching up to the monkey.")]
    [SerializeField, Min(0.01f)] private float catchLaneSmoothing = 0.08f;
    [Tooltip("Short blend so the monkey does not snap into the hand in a single frame.")]
    [SerializeField, Min(0f)] private float catchAttachSeconds = 0.12f;
    [Tooltip("Offset in the grabbing hand's local space. The monkey's neck is aligned to this point.")]
    [SerializeField] private Vector3 catchHandOffset = new Vector3(0f, 0.06f, 0f);

    [Header("Audio")]
    [Tooltip("Impact grunt only; footsteps are intentionally muted.")]
    [SerializeField, Range(0f, 1f)] private float gruntVolume = 0.8f;

    private readonly Collider[] obstacleHits = new Collider[24];
    private PlayerHealth health;
    private RunnerAnimation playerAnimation;
    private Transform chaseTarget;
    private float chaseGroundY;
    private bool subscribedToRival;
    private RunnerCourseGenerator generator;
    private GameObject modelInstance;
    private AudioSource voice;
    private float laneVelocity;
    private float forwardVelocity;
    private Animator gorillaAnimator;
    private float nextObstacleCheck;
    private bool initialized;
    private bool subscribedToHits;
    private bool playerWasReacting;
    private bool rivalWasStumbling;
    private Renderer[] gorillaRenderers;
    private Transform revealTarget;
    private bool gorillaVisible;

    private bool catchStarted;
    private bool catchLocked;
    private bool catchFrozen;
    private bool catchPlayerMoved;
    private bool catchWarningLogged;
    private bool catchSuppressed;
    private float catchStartedAt;
    private float catchLockedAt;
    private float catchWindupEndsAt;
    private bool catchWindupComplete;
    private float lastGruntAt = -10f;
    private Vector3 catchOriginalPlayerPosition;
    private Quaternion catchOriginalPlayerRotation;
    private CharacterController playerController;
    private bool playerControllerWasEnabled;
    private Transform playerNeck;
    private Transform catchHand;

    private VisibilityState visibility;
    private float currentGap;
    private float stateEndsAt;
    private float retreatStartedAt;
    private float retreatStartGap;
    private float bombStunEndsAt, bombVerticalVelocity, bombGroundY;
    public bool IsBombStunned => Time.time < bombStunEndsAt;

    public Transform CurrentTarget => catchStarted && player != null ? player.transform : chaseTarget;
    public bool IsCatchingPlayer => catchStarted;
    public bool HasCaughtPlayer => catchLocked;
    private float TargetSpeed => rival != null && chaseTarget == rival.transform ? rival.CurrentSpeed : player.CurrentSpeed;

    private bool IsVisibleWindow => visibility == VisibilityState.HitReveal;

    private bool startingIntimidation;
    private float startingRoarAt = -100f;
    public bool IsStartingIntimidation => startingIntimidation;

    public void BeginStartingIntimidation()
    {
        if (!TryInitialize()) return;
        startingIntimidation = true;
        visibility = VisibilityState.HitReveal;
        currentGap = 4.5f;
        transform.position = player.transform.position - Vector3.forward * currentGap - Vector3.right * 2.6f;
        SetGorillaVisible(true);
        gorillaAnimator.speed = 1f;
        gorillaAnimator.Play("Base Layer.StartIdle", 0, 0f);
    }

    public void PlayStartingRoar()
    {
        if (!startingIntimidation) return;
        startingRoarAt = Time.time;
        if (voice != null && gruntClip != null) voice.PlayOneShot(gruntClip, 0.75f);
    }

    public void EndStartingIntimidation()
    {
        if (!startingIntimidation) return;
        startingIntimidation = false;
        gorillaAnimator.CrossFadeInFixedTime("Base Layer.Run", 0.15f, 0, 0f);
        gorillaAnimator.speed = 1f;
        BeginRetreat();
    }

    private void PoseStartingRoar()
    {
        if (gorillaAnimator == null || !gorillaAnimator.isHuman) return;
        float age = Time.time - startingRoarAt;
        float weight = Mathf.Sin(Mathf.Clamp01(age / 1.5f) * Mathf.PI);
        var chest = gorillaAnimator.GetBoneTransform(HumanBodyBones.Chest);
        var head = gorillaAnimator.GetBoneTransform(HumanBodyBones.Head);
        if (chest != null) chest.localRotation *= Quaternion.Euler(-12f * weight, 0f, 0f);
        if (head != null) head.localRotation *= Quaternion.Euler(-18f * weight, 0f, 0f);
    }

    private void Awake()
    {
        TryInitialize();
    }

    private bool TryInitialize()
    {
        if (initialized) return true;
        if (player == null || !player.isActiveAndEnabled || !player.gameObject.activeInHierarchy)
        {
            player = null;
            foreach (RunnerMotor candidate in FindObjectsByType<RunnerMotor>(FindObjectsInactive.Exclude))
                if (candidate.isActiveAndEnabled) { player = candidate; break; }
        }
        if (player == null || gorillaModel == null || runController == null) return false;

        health = player.GetComponent<PlayerHealth>();
        if (health == null) return false;
        if (rival == null) rival = FindAnyObjectByType<RivalRunner>();
        playerAnimation = player.GetComponent<RunnerAnimation>();
        chaseTarget = player.transform;
        chaseGroundY = player.transform.position.y;
        generator = FindAnyObjectByType<RunnerCourseGenerator>();
        modelInstance = Instantiate(gorillaModel, transform);
        modelInstance.name = "Gorilla Visual";
        modelInstance.SetActive(true);
        modelInstance.transform.localPosition = Vector3.zero;
        modelInstance.transform.localRotation = Quaternion.Euler(0f, visualYaw, 0f);
        modelInstance.transform.localScale = Vector3.one * visualScale;

        // The visual has no gameplay collision or root-driven chase movement.
        foreach (Collider collider in modelInstance.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        foreach (Rigidbody body in modelInstance.GetComponentsInChildren<Rigidbody>(true)) body.isKinematic = true;
        foreach (Light light in modelInstance.GetComponentsInChildren<Light>(true)) light.enabled = false;
        foreach (Camera camera in modelInstance.GetComponentsInChildren<Camera>(true)) camera.enabled = false;
        // The gorilla is a silent visual threat: no imported footsteps or stomps.
        foreach (AudioSource source in modelInstance.GetComponentsInChildren<AudioSource>(true))
        {
            source.playOnAwake = false;
            source.Stop();
            source.mute = true;
        }
        gorillaAnimator = modelInstance.GetComponentInChildren<Animator>(true);
        if (gorillaAnimator == null) gorillaAnimator = modelInstance.AddComponent<Animator>();
        if (gorillaAvatar != null && gorillaAvatar.isValid &&
            (gorillaAnimator.avatar == null || !gorillaAnimator.avatar.isValid || gorillaAnimator.avatar != gorillaAvatar))
        {
            gorillaAnimator.avatar = gorillaAvatar;
            gorillaAnimator.Rebind();
        }
        ActivateVisualHierarchy(gorillaAnimator.transform);
        gorillaAnimator.runtimeAnimatorController = runController;
        gorillaAnimator.applyRootMotion = false;
        gorillaAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        gorillaAnimator.enabled = true;
        Renderer[] renderers = modelInstance.GetComponentsInChildren<Renderer>(true);
        gorillaRenderers = renderers;
        gorillaVisible = true;
        foreach (Renderer renderer in renderers)
        {
            ActivateVisualHierarchy(renderer.transform);
            renderer.enabled = true;
            if (gorillaMaterial != null)
            {
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++) materials[i] = gorillaMaterial;
                renderer.sharedMaterials = materials;
            }
        }
        if (renderers.Length > 0)
        {
            // FBX pivots are usually at the hips, so zeroing the local position buries the
            // legs. Ground the lowest rendered point at the chaser root (the player's feet).
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            float groundOffset = transform.position.y - bounds.min.y;
            modelInstance.transform.localPosition = new Vector3(
                visualOffset.x, groundOffset + visualOffset.y, visualOffset.z);
            Debug.Log($"GiantGorillaChaser grounded the gorilla visual by {groundOffset:F2}m and offset it by {visualOffset.x:F2}m on X.", this);
        }
        else
        {
            modelInstance.transform.localPosition = visualOffset;
            Debug.LogWarning("GiantGorillaChaser spawned the model, but it has no Renderer components.", this);
        }
        if (gorillaAnimator.avatar == null)
            Debug.LogWarning("GiantGorillaChaser: the gorilla Animator has no Avatar; the Run clip will not retarget.", this);

        // The prefab can carry legacy audio sources. Mute them before adding the
        // single impact-grunt source; footsteps are deliberately removed.
        foreach (AudioSource source in GetComponents<AudioSource>())
        {
            source.playOnAwake = false;
            source.Stop();
            source.mute = true;
        }
        voice = gameObject.AddComponent<AudioSource>();
        voice.playOnAwake = false;
        voice.loop = false;
        voice.spatialBlend = 0f;
        voice.volume = Mathf.Min(gruntVolume, 0.4f);
        voice.pitch = 0.8f;
        if (gruntClip == null)
            Debug.LogWarning("GiantGorillaChaser: gruntClip is not assigned. Assign GorillaSound to the Grunt Clip field.", this);

        visibility = VisibilityState.Hidden;
        currentGap = hiddenGap;
        transform.position = new Vector3(
            player.transform.position.x,
            player.transform.position.y,
            player.transform.position.z - Mathf.Max(2f, hiddenGap));
        SetGorillaVisible(false);
        initialized = true;
        // Imported model colliders stay disabled. This receiver follows the logical running lane.
        var trapReceiver = GetComponent<CapsuleCollider>();
        if (trapReceiver == null) trapReceiver = gameObject.AddComponent<CapsuleCollider>();
        trapReceiver.isTrigger = true;
        trapReceiver.center = Vector3.up * 0.9f;
        trapReceiver.height = 1.8f;
        trapReceiver.radius = 0.55f;
        var trapBody = GetComponent<Rigidbody>();
        if (trapBody == null) trapBody = gameObject.AddComponent<Rigidbody>();
        trapBody.isKinematic = true;
        trapBody.useGravity = false;
        Debug.Log($"GiantGorillaChaser bound to {player.name}; spawned {renderers.Length} renderer(s) with grunt '{(gruntClip != null ? gruntClip.name : "MISSING")}' and footsteps muted.", this);
        return true;
    }

    private void ActivateVisualHierarchy(Transform target)
    {
        for (Transform current = target; current != null && current != modelInstance.transform; current = current.parent)
            current.gameObject.SetActive(true);
    }

    private void OnEnable()
    {
        if (!initialized) return;
        SubscribeToHits();
        if (!startingIntimidation) HideImmediately();
    }

    private void Start()
    {
        if (!initialized && !TryInitialize())
        {
            Debug.LogError("GiantGorillaChaser could not start: assign an active RunnerMotor with PlayerHealth, the Gorilla FBX, and AC_Gorilla.", this);
            enabled = false;
            return;
        }
        SubscribeToHits();
        if (!startingIntimidation) HideImmediately();
    }

    private void SubscribeToHits()
    {
        if (health != null && !subscribedToHits)
        {
            health.HitTaken += OnPlayerHit;
            health.ProjectileHit += OnPlayerHit;
            health.RunFailed += OnPlayerDied;
            subscribedToHits = true;
        }
        if (rival != null && !subscribedToRival)
        {
            rival.Impacted += OnRivalHit;
            subscribedToRival = true;
        }
    }

    private void LateUpdate()
    {
        if (startingIntimidation) { PoseStartingRoar(); return; }
        if (RaceStartSequence.IsHolding) return;
        if (player == null || health == null || !player.gameObject.activeInHierarchy)
        {
            StopAudio();
            SetGorillaVisible(false);
            if (gorillaAnimator != null) gorillaAnimator.speed = 0f;
            return;
        }

        if (!health.HasFailed) catchSuppressed = false;

        // EndRun disables RunnerMotor before Died is raised, so the catch has to
        // run before the normal "player is inactive" early return.
        if (health.HasFailed && !catchStarted && !catchSuppressed) BeginCatchSequence();
        if (catchStarted)
        {
            UpdateCatch(Time.deltaTime);
            return;
        }

        if (health.IsRunOver || (!player.isActiveAndEnabled && (rival == null || !rival.isActiveAndEnabled || rival.HasFinished)))
        {
            StopAudio();
            SetGorillaVisible(false);
            if (gorillaAnimator != null) gorillaAnimator.speed = 0f;
            return;
        }

        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        if (IsBombStunned)
        {
            bombVerticalVelocity += player.BaseGravity * dt;
            Vector3 position = transform.position;
            position.y = Mathf.Max(bombGroundY, position.y + bombVerticalVelocity * dt);
            transform.position = position;
            return;
        }
        if (bombStunEndsAt > 0f)
        {
            bombStunEndsAt = 0f;
            var position = transform.position;
            position.y = bombGroundY;
            transform.position = position;
            if (gorillaAnimator != null)
            {
                gorillaAnimator.speed = 1f;
                gorillaAnimator.CrossFadeInFixedTime("Base Layer.Run", 0.15f, 0, 0f);
            }
            currentGap = Mathf.Max(hitRevealGap, chaseTarget.position.z - position.z);
            BeginRetreat();
        }

        DetectRunnerImpacts();
        UpdateChaseTarget();
        UpdateVisibilityState(dt);
        FollowRunner();

        if (gorillaAnimator != null)
        {
            if (visibility == VisibilityState.Hidden)
            {
                gorillaAnimator.speed = 0f;
            }
            else
            {
                float speedRatio = TargetSpeed / Mathf.Max(0.01f, player.ForwardSpeed);
                gorillaAnimator.speed = Mathf.Clamp(speedRatio, 0.7f, 1.4f);
            }
        }

        if (!IsVisibleWindow) return;

        if (Time.time >= nextObstacleCheck)
        {
            nextObstacleCheck = Time.time + 0.08f;
            SmashBreakablesBehindPlayer(chaseTarget.position.z);
        }
    }

    public bool ApplyBombHit(float launchSpeed, float stunSeconds)
    {
        if (!initialized || !isActiveAndEnabled || health == null || health.IsRunOver || catchStarted || IsBombStunned) return false;
        bombGroundY = transform.position.y;
        bombVerticalVelocity = Mathf.Max(1f, launchSpeed);
        float duration = Mathf.Max(stunSeconds, 2f * bombVerticalVelocity / Mathf.Max(0.1f, -player.BaseGravity) + 0.1f);
        bombStunEndsAt = Time.time + duration;
        laneVelocity = forwardVelocity = 0f;
        visibility = VisibilityState.HitReveal;
        SetGorillaVisible(true);
        RunnerHitReaction.Play(gorillaAnimator, playerAnimation != null ? playerAnimation.HitClipLength : 1f, duration);
        return true;
    }

    private void UpdateVisibilityState(float dt)
    {
        switch (visibility)
        {
            case VisibilityState.HitReveal:
                currentGap = Mathf.MoveTowards(currentGap, hitRevealGap, revealApproachSpeed * dt);
                if (IsRevealTargetRecovering())
                {
                    // Stay in frame while the hit runner is still slowed, then
                    // add a short grace beat once they are back at full speed.
                    stateEndsAt = Mathf.Max(stateEndsAt, Time.time + recoveryLingerSeconds);
                }
                else if (Time.time >= stateEndsAt)
                {
                    BeginRetreat();
                }
                break;

            case VisibilityState.Retreating:
                float retreatTime = Mathf.Clamp01((Time.time - retreatStartedAt) / Mathf.Max(0.01f, retreatSeconds));
                currentGap = Mathf.Lerp(retreatStartGap, hiddenGap, Mathf.SmoothStep(0f, 1f, retreatTime));
                if (retreatTime >= 1f)
                {
                    visibility = VisibilityState.Hidden;
                    currentGap = hiddenGap;
                    SetGorillaVisible(false);
                }
                break;

            case VisibilityState.Hidden:
                currentGap = hiddenGap;
                break;

            case VisibilityState.Catching:
                currentGap = Mathf.MoveTowards(currentGap, catchGap, catchApproachSpeed * dt);
                break;
        }
    }

    private void DetectRunnerImpacts()
    {
        // HitTaken/ProjectileHit events cover obstacles and fruit, while this
        // poll also catches banana-peel slips (which never call TakeHit).
        bool playerReacting = playerAnimation != null && playerAnimation.IsReactingToHit;
        if (playerReacting && !playerWasReacting) OnPlayerHit();
        playerWasReacting = playerReacting;

        bool rivalStumbling = rival != null && !rival.HasFinished &&
            (rival.IsStumbling || rival.IsRecoveringFromFall);
        if (rivalStumbling && !rivalWasStumbling) OnRivalHit();
        rivalWasStumbling = rivalStumbling;
    }

    private void UpdateChaseTarget()
    {
        if (revealTarget != null && IsRunnerAvailable(revealTarget))
        {
            chaseTarget = revealTarget;
            return;
        }

        if (rival != null && !rival.HasFinished &&
            (player == null || !player.isActiveAndEnabled))
        {
            chaseTarget = rival.transform;
            return;
        }

        if (player != null) chaseTarget = player.transform;
    }

    private bool IsRunnerAvailable(Transform candidate)
    {
        if (candidate == null || player == null) return false;
        if (candidate == player.transform)
            return player.isActiveAndEnabled && (health == null || !health.IsRunOver);
        if (rival != null && candidate == rival.transform)
            return rival.isActiveAndEnabled && !rival.HasFinished;
        return candidate.gameObject.activeInHierarchy;
    }

    private bool IsRevealTargetRecovering()
    {
        Transform target = revealTarget != null ? revealTarget : chaseTarget;
        if (target == null) return false;

        if (target == player.transform)
        {
            if (health != null && health.IsProjectileStunned) return true;
            if (playerAnimation != null && playerAnimation.IsReactingToHit) return true;
            return player.CurrentSpeed < player.ForwardSpeed * 0.85f;
        }

        if (rival != null && target == rival.transform)
            return rival.IsStumbling || rival.IsRecoveringFromFall;

        return false;
    }

    private void FollowRunner()
    {
        Vector3 target = chaseTarget != null ? chaseTarget.position : player.transform.position;
        float x = Mathf.SmoothDamp(transform.position.x, target.x, ref laneVelocity, laneDelay);
        float z = Mathf.SmoothDamp(transform.position.z, target.z - currentGap, ref forwardVelocity, forwardSmoothing);
        transform.position = new Vector3(x, Mathf.Max(chaseGroundY, target.y), z);
        transform.rotation = Quaternion.identity;
    }

    private void SetGorillaVisible(bool visible)
    {
        if (gorillaVisible == visible) return;
        gorillaVisible = visible;

        if (gorillaRenderers != null)
        {
            foreach (Renderer renderer in gorillaRenderers)
                if (renderer != null) renderer.enabled = visible;
        }

        if (!visible && gorillaAnimator != null)
            gorillaAnimator.speed = 0f;
    }

    private void HideImmediately()
    {
        visibility = VisibilityState.Hidden;
        currentGap = hiddenGap;
        laneVelocity = forwardVelocity = 0f;
        revealTarget = null;
        stateEndsAt = 0f;
        SetGorillaVisible(false);
        StopAudio();

        if (gorillaAnimator != null) gorillaAnimator.speed = 0f;
        if (player != null)
        {
            transform.position = new Vector3(
                player.transform.position.x,
                player.transform.position.y,
                player.transform.position.z - Mathf.Max(2f, hiddenGap));
        }
    }

    private void BeginRetreat()
    {
        if (visibility == VisibilityState.Hidden) return;
        visibility = VisibilityState.Retreating;
        retreatStartedAt = Time.time;
        retreatStartGap = currentGap;
    }

    private void OnPlayerHit()
    {
        if (!initialized || player == null || health == null || health.IsRunOver || catchStarted) return;
        revealTarget = player.transform;
        chaseTarget = player.transform;
        BeginImpactReveal();
    }

    private void OnRivalHit()
    {
        if (!initialized || rival == null || health == null || health.IsRunOver || catchStarted) return;
        revealTarget = rival.transform;
        chaseTarget = rival.transform;
        BeginImpactReveal();
    }

    private void BeginImpactReveal()
    {
        bool wasOffscreen = visibility == VisibilityState.Hidden || visibility == VisibilityState.Retreating;
        visibility = VisibilityState.HitReveal;
        stateEndsAt = Mathf.Max(stateEndsAt, Time.time + Mathf.Max(0.1f, hitRevealSeconds));
        nextObstacleCheck = Time.time;
        if (wasOffscreen) SetGorillaVisible(true);
        if (gorillaAnimator != null)
        {
            gorillaAnimator.speed = 1f;
            if (gorillaAnimator.isActiveAndEnabled && wasOffscreen)
                gorillaAnimator.Play("Run", 0, 0f);
        }
        if (Time.time - lastGruntAt > 0.15f) PlayGrunt();
    }

    private void OnPlayerDied()
    {
        BeginCatchSequence();
    }

    private void BeginCatchSequence()
    {
        if (catchStarted || catchSuppressed || !initialized || player == null || health == null) return;

        bombStunEndsAt = 0f;

        catchStarted = true;
        chaseTarget = player.transform;
        catchLocked = false;
        catchFrozen = false;
        catchPlayerMoved = false;
        catchWarningLogged = false;
        catchStartedAt = Time.time;
        catchLockedAt = 0f;
        catchWindupEndsAt = Time.time + Mathf.Max(0f, catchWindupSeconds);
        catchWindupComplete = catchWindupSeconds <= 0f;
        catchHand = null;
        playerNeck = null;

        catchOriginalPlayerPosition = player.transform.position;
        catchOriginalPlayerRotation = player.transform.rotation;
        playerController = player.GetComponent<CharacterController>();
        playerControllerWasEnabled = playerController != null && playerController.enabled;
        if (playerController != null) playerController.enabled = false;

        visibility = VisibilityState.Catching;
        laneVelocity = 0f;
        forwardVelocity = 0f;
        nextObstacleCheck = Time.time;
        revealTarget = player.transform;
        SetGorillaVisible(true);

        if (catchWindupComplete) PlayCatchAnimation();
        else if (gorillaAnimator != null) gorillaAnimator.speed = 0.85f;

        // The lethal hit already fired HitTaken and played the grunt in the same
        // frame; do not restart it. If catch is entered any other way, play it.
        if (Time.time - lastGruntAt > 0.15f) PlayGrunt();
        Debug.Log("GiantGorillaChaser: starting game-over catch.", this);
    }

    private void PlayCatchAnimation()
    {
        if (gorillaAnimator == null) return;
        gorillaAnimator.speed = 1f;
        if (!gorillaAnimator.isActiveAndEnabled) return;
        if (!gorillaAnimator.GetCurrentAnimatorStateInfo(0).IsName("Run") &&
            !gorillaAnimator.IsInTransition(0))
        {
            gorillaAnimator.Play("Run", 0, 0f);
        }
        gorillaAnimator.ResetTrigger(ResetRunTrigger);
        gorillaAnimator.SetTrigger(CatchTrigger);
    }

    private void UpdateCatch(float dt)
    {
        if (dt <= 0f) return;

        if (!catchWindupComplete)
        {
            // The monkey slumps for a beat while the gorilla closes the last
            // few metres, then the Catch clip starts on top of the monkey.
            currentGap = Mathf.MoveTowards(currentGap, catchGap + 1.4f, catchApproachSpeed * 0.45f * dt);
            FollowCatchTarget();
            if (gorillaAnimator != null) gorillaAnimator.speed = 0.85f;
            if (Time.time >= catchWindupEndsAt)
            {
                catchWindupComplete = true;
                PlayCatchAnimation();
            }
            return;
        }

        if (!catchLocked)
        {
            currentGap = Mathf.MoveTowards(currentGap, catchGap, catchApproachSpeed * dt);
            FollowCatchTarget();
            if (gorillaAnimator != null) gorillaAnimator.speed = 1f;

            if (GetCatchStateTime() >= catchGrabTime) TryBeginGrab();
            return;
        }

        if (gorillaAnimator != null && !catchFrozen) gorillaAnimator.speed = 1f;
        AlignPlayerNeckToHand();

        if (!catchFrozen && GetCatchStateTime() >= catchFreezeTime)
        {
            catchFrozen = true;
            if (gorillaAnimator != null) gorillaAnimator.speed = 0f;
            Debug.Log("GiantGorillaChaser: holding the monkey at the top of the lift.", this);
        }
    }

    private void FollowCatchTarget()
    {
        Vector3 target = player.transform.position;
        float x = Mathf.SmoothDamp(transform.position.x, target.x, ref laneVelocity, catchLaneSmoothing);
        transform.position = new Vector3(x, target.y, target.z - currentGap);
        transform.rotation = Quaternion.identity;
    }

    private float GetCatchStateTime()
    {
        if (gorillaAnimator == null) return Time.time - catchStartedAt;

        AnimatorStateInfo current = gorillaAnimator.GetCurrentAnimatorStateInfo(0);
        if (current.IsName("Catch") && current.length > 0f)
            return Mathf.Max(0f, current.normalizedTime) * current.length;

        if (gorillaAnimator.IsInTransition(0))
        {
            AnimatorStateInfo next = gorillaAnimator.GetNextAnimatorStateInfo(0);
            if (next.IsName("Catch") && next.length > 0f)
                return Mathf.Max(0f, next.normalizedTime) * next.length;
        }

        return Time.time - catchStartedAt;
    }

    private bool IsCatchStateActive()
    {
        if (gorillaAnimator == null) return false;
        if (gorillaAnimator.GetCurrentAnimatorStateInfo(0).IsName("Catch")) return true;
        return gorillaAnimator.IsInTransition(0) &&
            gorillaAnimator.GetNextAnimatorStateInfo(0).IsName("Catch");
    }

    private void TryBeginGrab()
    {
        if (catchLocked || gorillaAnimator == null || !IsCatchStateActive()) return;

        Transform leftHand = gorillaAnimator.GetBoneTransform(HumanBodyBones.LeftHand);
        Transform rightHand = gorillaAnimator.GetBoneTransform(HumanBodyBones.RightHand);
        Transform hand = SelectCatchHand(leftHand, rightHand);
        Transform neck = ResolvePlayerNeck();
        if (hand == null || neck == null)
        {
            if (!catchWarningLogged)
            {
                catchWarningLogged = true;
                Debug.LogWarning("GiantGorillaChaser: could not resolve the grabbing hand or the monkey's neck.", this);
            }
            return;
        }

        catchHand = hand;
        catchLocked = true;
        catchLockedAt = Time.time;

        // Make sure the gorilla is exactly in the monkey's lane when the hand
        // closes. The visual offset is deliberately left intact so the monkey
        // and gorilla remain readable from the chase camera.
        Vector3 position = transform.position;
        position.x = player.transform.position.x;
        transform.position = position;

        Debug.Log($"GiantGorillaChaser: catch locked with {hand.name} at " +
            $"{GetCatchStateTime():F2}s.", this);
    }

    private Transform SelectCatchHand(Transform leftHand, Transform rightHand)
    {
        if (leftHand == null) return rightHand;
        if (rightHand == null) return leftHand;

        Transform modelRoot = modelInstance != null ? modelInstance.transform : transform;
        Vector3 left = modelRoot.InverseTransformPoint(leftHand.position);
        Vector3 right = modelRoot.InverseTransformPoint(rightHand.position);
        // The swipe briefly puts both hands near the neck. The lifting hand keeps
        // rising and sweeps out to the side, so weight height and lateral reach on
        // top of forward reach instead of locking onto the hand that merely
        // happens to be a centimetre ahead at this instant.
        float leftScore = left.z + left.y * 0.5f + Mathf.Abs(left.x) * 0.5f;
        float rightScore = right.z + right.y * 0.5f + Mathf.Abs(right.x) * 0.5f;
        return rightScore >= leftScore ? rightHand : leftHand;
    }

    private Transform ResolvePlayerNeck()
    {
        if (playerNeck != null) return playerNeck;

        Animator playerAnimator = player.GetComponentInChildren<Animator>(true);
        if (playerAnimator != null && playerAnimator.isHuman)
        {
            playerNeck = playerAnimator.GetBoneTransform(HumanBodyBones.Neck)
                ?? playerAnimator.GetBoneTransform(HumanBodyBones.Head);
        }

        if (playerNeck == null)
        {
            playerNeck = player.transform;
            Debug.LogWarning("GiantGorillaChaser: monkey neck/head bone not found; using the player root as the grab point.", this);
        }
        return playerNeck;
    }

    private void AlignPlayerNeckToHand()
    {
        if (catchHand == null || playerNeck == null || player == null) return;

        Vector3 target = catchHand.TransformPoint(catchHandOffset);
        Vector3 delta = target - playerNeck.position;
        float attachTime = Mathf.Clamp01((Time.time - catchLockedAt) /
            Mathf.Max(0.01f, catchAttachSeconds));
        float weight = Mathf.SmoothStep(0f, 1f, attachTime);
        if (weight <= 0f) return;

        player.transform.position += delta * weight;
        catchPlayerMoved = true;
    }

    private void RestorePlayerAfterCatch()
    {
        if (player == null) return;

        if (catchPlayerMoved)
            player.transform.SetPositionAndRotation(catchOriginalPlayerPosition, catchOriginalPlayerRotation);

        if (playerController != null) playerController.enabled = playerControllerWasEnabled;
        catchPlayerMoved = false;
        playerController = null;
    }

    /// <summary>Releases the monkey and returns the gorilla to Run before a restart.</summary>
    public void ReleaseCatch()
    {
        if (!catchStarted && !catchLocked) return;

        RestorePlayerAfterCatch();
        catchStarted = false;
        catchLocked = false;
        catchFrozen = false;
        catchWindupComplete = false;
        catchWarningLogged = false;
        catchSuppressed = true;
        catchHand = null;
        playerNeck = null;

        if (gorillaAnimator != null)
        {
            gorillaAnimator.speed = 1f;
            if (gorillaAnimator.isActiveAndEnabled)
            {
                gorillaAnimator.ResetTrigger(CatchTrigger);
                gorillaAnimator.SetTrigger(ResetRunTrigger);
            }
        }

        visibility = VisibilityState.Hidden;
        currentGap = hiddenGap;
        laneVelocity = forwardVelocity = 0f;
        revealTarget = null;
        SetGorillaVisible(false);
        StopAudio();
        Debug.Log("GiantGorillaChaser: released the monkey and returned to Run.", this);
    }

    private void SmashBreakablesBehindPlayer(float playerZ)
    {
        int count = Physics.OverlapBoxNonAlloc(transform.position + Vector3.up, new Vector3(1.25f, 1.5f, 0.9f),
            obstacleHits, Quaternion.identity, ~0, QueryTriggerInteraction.Collide);
        for (int i = 0; i < count; i++)
        {
            Collider hit = obstacleHits[i];
            obstacleHits[i] = null;
            if (hit == null || hit.transform.position.z >= playerZ - 1f) continue;
            RunnerObstacle obstacle = hit.GetComponent<RunnerObstacle>();
            if (obstacle != null) obstacle.TryBreakFromChaser();
        }
    }

    private void PlayGrunt()
    {
        if (voice == null || gruntClip == null) return;
        lastGruntAt = Time.time;
        voice.Stop();
        voice.PlayOneShot(gruntClip);
    }

    private void StopAudio()
    {
        if (voice != null) voice.Stop();
    }

    private void OnDisable()
    {
        bombStunEndsAt = 0f;
        if (health != null && subscribedToHits)
        {
            health.HitTaken -= OnPlayerHit;
            health.ProjectileHit -= OnPlayerHit;
            health.RunFailed -= OnPlayerDied;
        }
        if (rival != null && subscribedToRival) rival.Impacted -= OnRivalHit;
        subscribedToRival = false;
        subscribedToHits = false;

        bool wasCatching = catchStarted;
        if (wasCatching) RestorePlayerAfterCatch();
        catchStarted = false;
        catchLocked = false;
        catchFrozen = false;
        catchWindupComplete = false;
        catchWarningLogged = false;
        if (wasCatching) catchSuppressed = true;
        catchHand = null;
        playerNeck = null;

        StopAudio();
        if (gorillaAnimator != null) gorillaAnimator.speed = 0f;
        visibility = VisibilityState.Hidden;
        currentGap = hiddenGap;
        laneVelocity = forwardVelocity = 0f;
        revealTarget = null;
        playerWasReacting = false;
        rivalWasStumbling = false;
        SetGorillaVisible(false);
    }
}
