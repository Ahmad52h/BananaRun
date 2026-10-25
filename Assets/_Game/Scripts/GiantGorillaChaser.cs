using UnityEngine;

/// <summary>
/// Brief visual chase. The gorilla reveals itself when the run starts and whenever
/// the player takes a hit, then retreats behind the camera until the next hit.
/// It has no collider, player damage, or physics body.
/// </summary>
public sealed class GiantGorillaChaser : MonoBehaviour
{
    private enum VisibilityState
    {
        Hidden,
        StartReveal,
        HitReveal,
        Retreating,
        Catching
    }

    private static readonly int CatchTrigger = Animator.StringToHash("Catch");
    private static readonly int ResetRunTrigger = Animator.StringToHash("ResetRun");

    [Header("References")]
    [SerializeField] private RunnerMotor player;
    [SerializeField] private GameObject gorillaModel;
    [SerializeField] private Avatar gorillaAvatar;
    [SerializeField] private RuntimeAnimatorController runController;
    [SerializeField] private Material gorillaMaterial;
    [SerializeField] private AudioClip gruntClip;
    [SerializeField] private AudioClip footstepClip;

    [Header("Chase")]
    [SerializeField, Min(0.01f)] private float laneDelay = 0.32f;
    [SerializeField, Min(0.01f)] private float forwardSmoothing = 0.13f;
    [SerializeField, Min(0.1f)] private float stepDistance = 3.2f;
    [SerializeField, Min(0.1f)] private float visualScale = 1f;
    [SerializeField] private float visualYaw;
    [Tooltip("Local offset for the spawned visual. X shifts the gorilla out of the camera's center so the player stays visible; Y fine-tunes the auto-grounding.")]
    [SerializeField] private Vector3 visualOffset = new Vector3(1.2f, 0f, 0f);

    [Header("Visibility Windows")]
    [Tooltip("Distance behind the player when the gorilla first appears at run start.")]
    [SerializeField, Min(2f)] private float startRevealGap = 2.9f;
    [Tooltip("Distance behind the player when the gorilla surges in after a hit.")]
    [SerializeField, Min(2f)] private float hitRevealGap = 2.6f;
    [Tooltip("Distance behind the player where the gorilla is fully out of the camera frustum.")]
    [SerializeField, Min(7.5f)] private float hiddenGap = 10.5f;
    [SerializeField, Min(0.5f)] private float startRevealSeconds = 2.75f;
    [SerializeField, Min(0.5f)] private float hitRevealSeconds = 3.5f;
    [Tooltip("How quickly the gorilla closes from the hidden gap into hit-reveal range.")]
    [SerializeField, Min(1f)] private float revealApproachSpeed = 24f;
    [Tooltip("Time spent smoothly moving from the reveal gap back to the hidden gap.")]
    [SerializeField, Min(0.1f)] private float retreatSeconds = 1.1f;

    [Header("Game Over Catch")]
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
    [SerializeField, Range(0f, 1f)] private float gruntVolume = 0.8f;
    [SerializeField, Range(0f, 1f)] private float footstepVolume = 0.65f;

    private readonly Collider[] obstacleHits = new Collider[24];
    private PlayerHealth health;
    private RunnerCourseGenerator generator;
    private GameObject modelInstance;
    private AudioSource voice;
    private AudioSource footsteps;
    private float laneVelocity;
    private float forwardVelocity;
    private float stepTravel;
    private Animator gorillaAnimator;
    private float nextObstacleCheck;
    private bool initialized;
    private bool subscribedToHits;
    private bool startRevealIssued;

    private bool catchStarted;
    private bool catchLocked;
    private bool catchFrozen;
    private bool catchPlayerMoved;
    private bool catchWarningLogged;
    private bool catchSuppressed;
    private float catchStartedAt;
    private float catchLockedAt;
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

    private bool IsVisibleWindow =>
        visibility == VisibilityState.StartReveal || visibility == VisibilityState.HitReveal;

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

        voice = gameObject.AddComponent<AudioSource>();
        voice.playOnAwake = false;
        voice.loop = false;
        voice.spatialBlend = 0f;
        voice.volume = gruntVolume;
        voice.pitch = 0.8f;
        footsteps = gameObject.AddComponent<AudioSource>();
        footsteps.playOnAwake = false;
        footsteps.loop = false;
        footsteps.spatialBlend = 0f;
        footsteps.volume = footstepVolume;
        footsteps.pitch = 0.9f;
        if (gruntClip == null)
            Debug.LogWarning("GiantGorillaChaser: gruntClip is not assigned. Assign GorillaSound to the Grunt Clip field.", this);
        if (footstepClip == null)
            Debug.LogWarning("GiantGorillaChaser: footstepClip is not assigned. Assign GiantFootstep to the Footstep Clip field.", this);

        visibility = VisibilityState.Hidden;
        currentGap = hiddenGap;
        initialized = true;
        Debug.Log($"GiantGorillaChaser bound to {player.name}; spawned {renderers.Length} renderer(s) with grunt '{(gruntClip != null ? gruntClip.name : "MISSING")}' and footstep '{(footstepClip != null ? footstepClip.name : "MISSING")}'.", this);
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
        BeginStartReveal();
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
        if (!startRevealIssued) BeginStartReveal();
    }

    private void SubscribeToHits()
    {
        if (health == null || subscribedToHits) return;
        health.HitTaken += OnPlayerHit;
        health.Died += OnPlayerDied;
        subscribedToHits = true;
    }

    private void LateUpdate()
    {
        if (player == null || health == null || !player.gameObject.activeInHierarchy)
        {
            StopAudio();
            if (gorillaAnimator != null) gorillaAnimator.speed = 0f;
            return;
        }

        if (!health.IsDead) catchSuppressed = false;

        // EndRun disables RunnerMotor before Died is raised, so the catch has to
        // run before the normal "player is inactive" early return.
        if (health.IsDead && !catchStarted && !catchSuppressed) BeginCatchSequence();
        if (catchStarted)
        {
            UpdateCatch(Time.deltaTime);
            return;
        }

        if (health.IsRunOver || !player.isActiveAndEnabled)
        {
            StopAudio();
            if (gorillaAnimator != null) gorillaAnimator.speed = 0f;
            return;
        }

        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        UpdateVisibilityState(dt);
        FollowPlayer();

        if (gorillaAnimator != null)
        {
            if (visibility == VisibilityState.Hidden)
            {
                gorillaAnimator.speed = 0f;
            }
            else
            {
                float speedRatio = player.CurrentSpeed / Mathf.Max(0.01f, player.ForwardSpeed);
                gorillaAnimator.speed = Mathf.Clamp(speedRatio, 0.7f, 1.4f);
            }
        }

        if (!IsVisibleWindow)
        {
            if (footsteps != null && footsteps.isPlaying) footsteps.Stop();
            return;
        }

        stepTravel += Mathf.Max(0f, Time.deltaTime * player.CurrentSpeed);
        if (stepTravel >= stepDistance)
        {
            stepTravel %= stepDistance;
            if (footstepClip != null && footsteps != null) footsteps.PlayOneShot(footstepClip);
        }

        if (Time.time >= nextObstacleCheck)
        {
            nextObstacleCheck = Time.time + 0.08f;
            SmashBreakablesBehindPlayer(player.transform.position.z);
        }
    }

    private void UpdateVisibilityState(float dt)
    {
        switch (visibility)
        {
            case VisibilityState.StartReveal:
            case VisibilityState.HitReveal:
                float revealGap = visibility == VisibilityState.StartReveal ? startRevealGap : hitRevealGap;
                currentGap = Mathf.MoveTowards(currentGap, revealGap, revealApproachSpeed * dt);
                if (Time.time >= stateEndsAt) BeginRetreat();
                break;

            case VisibilityState.Retreating:
                float retreatTime = Mathf.Clamp01((Time.time - retreatStartedAt) / Mathf.Max(0.01f, retreatSeconds));
                currentGap = Mathf.Lerp(retreatStartGap, hiddenGap, Mathf.SmoothStep(0f, 1f, retreatTime));
                if (retreatTime >= 1f)
                {
                    visibility = VisibilityState.Hidden;
                    currentGap = hiddenGap;
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

    private void FollowPlayer()
    {
        Vector3 target = player.transform.position;
        float x = Mathf.SmoothDamp(transform.position.x, target.x, ref laneVelocity, laneDelay);
        float z = Mathf.SmoothDamp(transform.position.z, target.z - currentGap, ref forwardVelocity, forwardSmoothing);
        transform.position = new Vector3(x, target.y, z);
        transform.rotation = Quaternion.identity;
    }

    private void BeginStartReveal()
    {
        if (player == null) return;
        startRevealIssued = true;

        Vector3 target = player.transform.position;
        visibility = VisibilityState.StartReveal;
        stateEndsAt = Time.time + Mathf.Max(0.1f, startRevealSeconds);
        currentGap = Mathf.Max(2f, startRevealGap);
        laneVelocity = forwardVelocity = 0f;
        stepTravel = 0f;
        nextObstacleCheck = Time.time;
        transform.position = new Vector3(target.x, target.y, target.z - currentGap);
        transform.rotation = Quaternion.identity;
        StopFootsteps();

        if (gorillaAnimator != null)
        {
            gorillaAnimator.speed = 1f;
            if (gorillaAnimator.isActiveAndEnabled) gorillaAnimator.Play("Run", 0, 0f);
        }
        PlayGrunt();
    }

    private void BeginRetreat()
    {
        if (visibility == VisibilityState.Hidden) return;
        visibility = VisibilityState.Retreating;
        retreatStartedAt = Time.time;
        retreatStartGap = currentGap;
        stepTravel = 0f;
        StopFootsteps();
    }

    private void OnPlayerHit()
    {
        if (!initialized || player == null || health == null || health.IsRunOver) return;

        bool wasOffscreen = visibility == VisibilityState.Hidden || visibility == VisibilityState.Retreating;
        visibility = VisibilityState.HitReveal;
        stateEndsAt = Time.time + Mathf.Max(0.1f, hitRevealSeconds);
        stepTravel = 0f;
        nextObstacleCheck = Time.time;
        StopFootsteps();
        if (wasOffscreen && gorillaAnimator != null)
        {
            gorillaAnimator.speed = 1f;
            if (gorillaAnimator.isActiveAndEnabled) gorillaAnimator.Play("Run", 0, 0f);
        }
        PlayGrunt();
    }

    private void OnPlayerDied()
    {
        BeginCatchSequence();
    }

    private void BeginCatchSequence()
    {
        if (catchStarted || catchSuppressed || !initialized || player == null || health == null) return;

        catchStarted = true;
        catchLocked = false;
        catchFrozen = false;
        catchPlayerMoved = false;
        catchWarningLogged = false;
        catchStartedAt = Time.time;
        catchLockedAt = 0f;
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
        stepTravel = 0f;
        nextObstacleCheck = Time.time;
        StopFootsteps();

        if (gorillaAnimator != null)
        {
            gorillaAnimator.speed = 1f;
            if (gorillaAnimator.isActiveAndEnabled)
            {
                if (!gorillaAnimator.GetCurrentAnimatorStateInfo(0).IsName("Run") &&
                    !gorillaAnimator.IsInTransition(0))
                {
                    gorillaAnimator.Play("Run", 0, 0f);
                }
                gorillaAnimator.ResetTrigger(ResetRunTrigger);
                gorillaAnimator.SetTrigger(CatchTrigger);
            }
        }

        // The lethal hit already fired HitTaken and played the grunt in the same
        // frame; do not restart it. If catch is entered any other way, play it.
        if (Time.time - lastGruntAt > 0.15f) PlayGrunt();
        Debug.Log("GiantGorillaChaser: starting game-over catch.", this);
    }

    private void UpdateCatch(float dt)
    {
        if (dt <= 0f) return;
        if (footsteps != null && footsteps.isPlaying) footsteps.Stop();

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
        stepTravel = 0f;
        laneVelocity = forwardVelocity = 0f;
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

    private void StopFootsteps()
    {
        if (footsteps != null) footsteps.Stop();
    }

    private void StopAudio()
    {
        if (voice != null) voice.Stop();
        StopFootsteps();
    }

    private void OnDisable()
    {
        if (health != null && subscribedToHits)
        {
            health.HitTaken -= OnPlayerHit;
            health.Died -= OnPlayerDied;
        }
        subscribedToHits = false;

        bool wasCatching = catchStarted;
        if (wasCatching) RestorePlayerAfterCatch();
        catchStarted = false;
        catchLocked = false;
        catchFrozen = false;
        catchWarningLogged = false;
        if (wasCatching) catchSuppressed = true;
        catchHand = null;
        playerNeck = null;

        StopAudio();
        if (gorillaAnimator != null) gorillaAnimator.speed = 0f;
        visibility = VisibilityState.Hidden;
        currentGap = hiddenGap;
        stepTravel = 0f;
        laneVelocity = forwardVelocity = 0f;
        startRevealIssued = false;
    }
}
