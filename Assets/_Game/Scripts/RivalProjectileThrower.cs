using UnityEngine;

/// <summary>Telegraphs a held, spinning fruit and snapshots the player's lane at release.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RivalRunner))]
public sealed class RivalProjectileThrower : MonoBehaviour
{
    [SerializeField] private BallisticRivalProjectile projectilePrefab;
    [SerializeField, Min(0.1f)] private float firstThrowDelay = 4f;
    [SerializeField, Min(1f)] private float throwInterval = 6.5f;
    [SerializeField, Min(0.1f)] private float windupDuration = 0.7f;
    [SerializeField, Min(0.5f)] private float flightTime = 1.25f;
    [SerializeField, Min(1f)] private float maximumRange = 24f;
    [Header("Natural difficulty")]
    [SerializeField, Range(0f, 0.4f)] private float intervalVariation = 0.2f;
    [SerializeField, Range(0f, 1f)] private float accuracy = 0.72f;
    [SerializeField, Min(0f)] private float accurateAimSpread = 0.18f;
    [SerializeField] private Vector3 handOffset = new Vector3(0f, 0.08f, 0.12f);

    private RivalRunner rival;
    private RunnerMotor player;
    private PlayerHealth health;
    private CharacterController playerController;
    private RunnerAnimation playerAnimation;
    private Animator animator;
    private Transform hand;
    private BallisticRivalProjectile held;
    private float nextThrow, releaseAt = -1f;
    private int throwLayer = -1;
    private readonly System.Random random = new System.Random();

    public BallisticRivalProjectile HeldProjectile => held;
    public bool IsWindingUp => releaseAt >= 0f;
    public int ShotsThrown { get; private set; }
    public Vector3 LastTargetPoint { get; private set; }
    public bool LastShotWasAccurate { get; private set; }
    public float NextThrowTime => nextThrow;

    private void Start()
    {
        rival = GetComponent<RivalRunner>();
        player = rival.Player;
        if (player == null || projectilePrefab == null) { enabled = false; return; }
        health = player.GetComponent<PlayerHealth>();
        playerController = player.GetComponent<CharacterController>();
        playerAnimation = player.GetComponent<RunnerAnimation>();
        animator = GetComponentInChildren<Animator>();
        if (animator != null)
        {
            hand = animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.RightHand) : null;
            throwLayer = animator.GetLayerIndex("Throw Overlay");
        }
        if (hand == null) hand = transform;
        nextThrow = Time.time + RandomizedDelay(firstThrowDelay);
    }

    private bool CanPrepare => player != null && player.isActiveAndEnabled && health != null && !health.IsRunOver &&
        rival != null && !rival.HasFinished && !rival.IsStumbling && !rival.IsSliding && rival.IsGrounded &&
        playerController != null && playerController.enabled && playerController.isGrounded &&
        (playerAnimation == null || !playerAnimation.IsVineControlled) &&
        Mathf.Abs(player.transform.position.z - transform.position.z) <= maximumRange;

    private void Update()
    {
        if (RaceStartSequence.IsHolding)
        {
            nextThrow = Time.time + firstThrowDelay;
            CancelHeld();
            return;
        }
        if (!CanPrepare) { CancelHeld(); return; }
        if (held == null)
        {
            held = Instantiate(projectilePrefab, hand, false);
            held.transform.localPosition = hand == transform ? new Vector3(0.5f, 1.3f, 0.4f) : handOffset;
            held.transform.localRotation = Quaternion.identity;
        }
        if (releaseAt < 0f && Time.time >= nextThrow)
        {
            releaseAt = Time.time + windupDuration;
            if (animator != null && throwLayer >= 0)
            {
                animator.SetLayerWeight(throwLayer, 1f);
                animator.Play("Throw", throwLayer, 0f);
            }
        }
        if (releaseAt < 0f || Time.time < releaseAt) return;

        // Predict forward travel once, using the active lane even during a lane transition.
        // The fixed X target and fixed launch velocity leave switching lanes as a real dodge.
        LastTargetPoint = new Vector3(player.ActiveLaneWorldX,
            player.transform.position.y + playerController.center.y,
            player.transform.position.z + player.CurrentSpeed * flightTime);
        LastShotWasAccurate = random.NextDouble() < accuracy;
        float lateralError = LastShotWasAccurate ? RandomRange(-accurateAimSpread, accurateAimSpread) :
            RandomRange(1.05f, 1.6f) * (random.Next(2) == 0 ? -1f : 1f);
        LastTargetPoint += new Vector3(lateralError, 0f, RandomRange(-0.25f, 0.25f));
        held.Launch(rival, health, LastTargetPoint, flightTime);
        held = null;
        ShotsThrown++;
        releaseAt = -1f;
        nextThrow = Time.time + RandomizedDelay(throwInterval);
    }

    private float RandomRange(float min, float max) => Mathf.Lerp(min, max, (float)random.NextDouble());
    private float RandomizedDelay(float baseline) => Mathf.Max(0.5f, baseline * RandomRange(1f - intervalVariation, 1f + intervalVariation));

    private void LateUpdate()
    {
        if (animator != null && throwLayer >= 0 && !IsWindingUp)
            animator.SetLayerWeight(throwLayer, Mathf.MoveTowards(animator.GetLayerWeight(throwLayer), 0f, Time.deltaTime * 6f));
    }

    private void CancelHeld()
    {
        bool interrupted = IsWindingUp;
        if (held != null) { Destroy(held.gameObject); held = null; }
        releaseAt = -1f;
        if (interrupted) nextThrow = Time.time + 1f;
    }

    private void OnDisable()
    {
        CancelHeld();
        if (animator != null && throwLayer >= 0) animator.SetLayerWeight(throwLayer, 0f);
    }
}
