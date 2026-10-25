using UnityEngine;

/// <summary>Visual chase only: no collider, player damage, or physics body.</summary>
public sealed class GiantGorillaChaser : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RunnerMotor player;
    [SerializeField] private GameObject gorillaModel;
    [SerializeField] private Avatar gorillaAvatar;
    [SerializeField] private RuntimeAnimatorController runController;
    [SerializeField] private Material gorillaMaterial;
    [SerializeField] private AudioClip gruntClip;
    [SerializeField] private AudioClip footstepClip;

    [Header("Chase")]
    [SerializeField, Min(2f)] private float normalGap = 3.5f;
    [SerializeField, Min(1f)] private float closestGap = 2.5f;
    [SerializeField, Min(0f)] private float hitGain = 1.25f;
    [SerializeField, Min(0f)] private float recoveryPerSecond = 0.5f;
    [SerializeField, Min(0.01f)] private float laneDelay = 0.32f;
    [SerializeField, Min(0.01f)] private float forwardSmoothing = 0.13f;
    [SerializeField, Min(0.1f)] private float stepDistance = 3.2f;
    [SerializeField, Min(0.1f)] private float visualScale = 1f;
    [SerializeField] private float visualYaw;
    [Tooltip("Local offset for the spawned visual. X shifts the gorilla out of the camera's center so the player stays visible; Y fine-tunes the auto-grounding.")]
    [SerializeField] private Vector3 visualOffset = new Vector3(1.2f, 0f, 0f);

    private readonly Collider[] obstacleHits = new Collider[24];
    private PlayerHealth health;
    private RunnerCourseGenerator generator;
    private GameObject modelInstance;
    private AudioSource voice;
    private AudioSource footsteps;
    private float lostGap;
    private float laneVelocity;
    private float forwardVelocity;
    private float stepTravel;
    private Animator gorillaAnimator;
    private float nextGruntTime;
    private float nextObstacleCheck;
    private bool initialized;
    private bool subscribedToHits;

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
        voice.spatialBlend = 0f;
        voice.volume = 0.55f;
        voice.pitch = 0.7f;
        footsteps = gameObject.AddComponent<AudioSource>();
        footsteps.playOnAwake = false;
        footsteps.spatialBlend = 0f;
        footsteps.volume = 0.5f;
        footsteps.pitch = 0.88f;
        initialized = true;
        ResetChase();
        Debug.Log($"GiantGorillaChaser bound to {player.name}; spawned {renderers.Length} renderer(s) at {transform.position}.", this);
        return true;
    }

    private void ActivateVisualHierarchy(Transform target)
    {
        for (Transform current = target; current != null && current != modelInstance.transform; current = current.parent)
            current.gameObject.SetActive(true);
    }

    private void OnEnable()
    {
        if (initialized) { SubscribeToHits(); ResetChase(); }
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
        gorillaAnimator.Play("Run", 0, 0f);
        PlayGrunt();
    }

    private void SubscribeToHits()
    {
        if (health == null || subscribedToHits) return;
        health.HitTaken += OnPlayerHit;
        subscribedToHits = true;
    }

    private void LateUpdate()
    {
        if (player == null || health == null || health.IsRunOver || !player.isActiveAndEnabled)
        {
            if (voice != null) voice.Stop();
            if (footsteps != null) footsteps.Stop();
            if (gorillaAnimator != null) gorillaAnimator.speed = 0f;
            return;
        }

        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        float normalSpeed = player.SpeedAtDifficulty(0f);
        // Compare against current difficulty speed, so progression itself is not a slowdown.
        if (generator != null) normalSpeed = player.SpeedAtDifficulty(generator.Difficulty);
        bool slowed = player.CurrentSpeed < normalSpeed * 0.95f;
        lostGap = Mathf.MoveTowards(lostGap, 0f, recoveryPerSecond * dt);
        float desiredGap = Mathf.Clamp(normalGap - lostGap - (slowed ? 1.1f : 0f), closestGap, normalGap);
        Vector3 target = player.transform.position;
        float previousZ = transform.position.z;
        float x = Mathf.SmoothDamp(transform.position.x, target.x, ref laneVelocity, laneDelay);
        float z = Mathf.SmoothDamp(transform.position.z, target.z - desiredGap, ref forwardVelocity, forwardSmoothing);
        transform.position = new Vector3(x, target.y, Mathf.Min(z, target.z - closestGap));
        transform.rotation = Quaternion.identity;
        if (gorillaAnimator != null)
            gorillaAnimator.speed = Mathf.Clamp(player.CurrentSpeed / Mathf.Max(0.01f, player.ForwardSpeed), 0.7f, 1.4f);

        stepTravel += Mathf.Max(0f, transform.position.z - previousZ);
        if (stepTravel >= stepDistance)
        {
            stepTravel %= stepDistance;
            if (footstepClip != null) footsteps.PlayOneShot(footstepClip);
        }

        if (desiredGap < normalGap - 1.5f && Time.time >= nextGruntTime) PlayGrunt();
        if (Time.time >= nextObstacleCheck)
        {
            nextObstacleCheck = Time.time + 0.08f;
            SmashBreakablesBehindPlayer(target.z);
        }
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

    private void OnPlayerHit()
    {
        lostGap = Mathf.Min(normalGap - closestGap, lostGap + hitGain);
        PlayGrunt();
    }

    private void PlayGrunt()
    {
        if (voice != null && gruntClip != null) voice.PlayOneShot(gruntClip);
        nextGruntTime = Time.time + 4f;
    }

    private void ResetChase()
    {
        if (player == null) return;
        lostGap = laneVelocity = forwardVelocity = stepTravel = 0f;
        nextObstacleCheck = Time.time;
        nextGruntTime = Time.time;
        transform.position = player.transform.position + Vector3.back * normalGap;
        transform.rotation = Quaternion.identity;
        if (voice != null) voice.Stop();
        if (footsteps != null) footsteps.Stop();
        if (gorillaAnimator != null)
        {
            gorillaAnimator.speed = 1f;
            // OnDisable runs while the spawned visual is already inactive. Playing a
            // state on an inactive Animator logs an error, so only replay it when live.
            if (gorillaAnimator.isActiveAndEnabled)
                gorillaAnimator.Play("Run", 0, 0f);
        }
    }

    private void OnDisable()
    {
        if (health != null && subscribedToHits) health.HitTaken -= OnPlayerHit;
        subscribedToHits = false;
        if (voice != null) voice.Stop();
        if (footsteps != null) footsteps.Stop();
        if (initialized) ResetChase();
    }

}
