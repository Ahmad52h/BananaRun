using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Orchestrates the win and loss vignettes for the direct race.
/// Victory hops the monkey over the celebration basket while bananas burst out;
/// defeat drops the monkey into a sad sit before the gorilla grabs him.
/// Movement, input and combat keep their existing owners.
/// </summary>
[DefaultExecutionOrder(400)]
[DisallowMultipleComponent]
public sealed class RaceCutsceneDirector : MonoBehaviour
{
    private enum Cutscene { None, Victory, Defeat }

    private static readonly int IdleState = Animator.StringToHash("Base Layer.Idle");
    private static readonly int JumpState = Animator.StringToHash("Base Layer.Jump");
    private static readonly Color VictoryColor = new Color(1f, 0.84f, 0.22f);
    private static readonly Color DefeatColor = new Color(0.94f, 0.28f, 0.24f);

    public static RaceCutsceneDirector Instance { get; private set; }

    [Header("Scene References")]
    [SerializeField] private RaceProgressTracker race;
    [SerializeField] private RunnerMotor player;
    [SerializeField] private RivalRunner rival;
    [SerializeField] private FinishGate finishGate;
    [SerializeField] private GiantGorillaChaser chaser;
    [Tooltip("Basket the winner jumps over. Falls back to the finish gate's banana target.")]
    [SerializeField] private Transform celebrationBasket;

    [Header("Victory Showcase")]
    [SerializeField, Min(0f)] private float victoryStartDelay = 0.35f;
    [SerializeField, Range(1, 5)] private int victoryHops = 3;
    [SerializeField, Min(0.25f)] private float victoryHopDuration = 0.85f;
    [SerializeField, Min(0.2f)] private float victoryHopHeight = 1.55f;
    [Tooltip("Maximum forward travel of the whole celebration hop sequence.")]
    [SerializeField, Min(1f)] private float victoryTravel = 6.5f;
    [SerializeField, Range(2, 40)] private int victoryBananasPerBurst = 10;
    [SerializeField, Min(0.5f)] private float victoryBannerHold = 2.7f;

    [Header("Defeat Showcase")]
    [SerializeField, Min(0.1f)] private float defeatSitBlendSeconds = 0.55f;
    [Tooltip("How far the hips drop for the ground sit.")]
    [SerializeField, Min(0f)] private float defeatSitDrop = 0.55f;
    [SerializeField, Min(0.4f)] private float defeatMaxSitSeconds = 2.4f;
    [SerializeField, Min(0.5f)] private float defeatBannerHold = 2.2f;

    private PlayerHealth health;
    private Animator playerAnimator;
    private CutsceneBannerUI banner;
    private CelebrationBananaBurst burst;
    private Cutscene cutscene;
    private bool victoryRunning;

    private bool posing;
    private float poseWeight;
    private Transform hips;
    private Vector3 hipsRest;
    private BoneOffset[] sitBones;

    private struct BoneOffset
    {
        public Transform Bone;
        public Quaternion Rest;
        public float Angle;
        public bool Sob;
    }

    /// <summary>True while the finish gate should hold the results card back.</summary>
    public bool VictoryShowcasePlaying => victoryRunning;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInstall()
    {
        if (FindAnyObjectByType<RaceCutsceneDirector>() != null) return;
        if (FindAnyObjectByType<RunnerMotor>() == null) return;
        if (FindAnyObjectByType<FinishGate>() == null) return;
        var host = new GameObject("Race Cutscenes");
        host.AddComponent<RaceCutsceneDirector>();
    }

    private void Awake()
    {
        Instance = this;
        ResolveReferences();

        banner = CutsceneBannerUI.Create(transform);
        var burstHost = new GameObject("Celebration Bananas");
        burstHost.transform.SetParent(transform, false);
        burst = burstHost.AddComponent<CelebrationBananaBurst>();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void ResolveReferences()
    {
        if (race == null) race = FindAnyObjectByType<RaceProgressTracker>();
        if (player == null) player = race != null ? race.Player : FindAnyObjectByType<RunnerMotor>();
        if (rival == null) rival = race != null ? race.Rival : FindAnyObjectByType<RivalRunner>();
        if (finishGate == null) finishGate = FindAnyObjectByType<FinishGate>();
        if (chaser == null) chaser = FindAnyObjectByType<GiantGorillaChaser>();
        if (celebrationBasket == null && finishGate != null) celebrationBasket = finishGate.BananaTarget;
        if (celebrationBasket == null)
        {
            var found = GameObject.Find("Celebration Banana Basket");
            if (found != null) celebrationBasket = found.transform;
        }

        if (player == null) return;
        health = player.GetComponent<PlayerHealth>();
        playerAnimator = player.GetComponentInChildren<Animator>(true);
    }

    private void Update()
    {
        if (cutscene != Cutscene.None || health == null) return;

        // Defeat wins ties: a failed run never plays the celebration.
        if (health.HasFailed)
        {
            BeginDefeat();
            return;
        }

        bool won = race != null
            ? race.Outcome == RaceProgressTracker.RaceOutcome.PlayerWon
            : finishGate != null && finishGate.Completed;
        if (won) BeginVictory();
    }

    private void LateUpdate()
    {
        if (!posing) return;
        ApplySitPose(poseWeight);
    }

    private void BeginVictory()
    {
        cutscene = Cutscene.Victory;
        victoryRunning = true;
        string message = rival != null
            ? "You beat the rival to the basket!"
            : "The celebration basket is yours!";
        banner.Show("VICTORY", message, VictoryColor, victoryBannerHold, 0.12f);
        StartCoroutine(VictoryRoutine());
    }

    private void BeginDefeat()
    {
        cutscene = Cutscene.Defeat;
        banner.Show("DEFEAT", DefeatMessage(), DefeatColor, defeatBannerHold, 0.28f);
        StartCoroutine(DefeatRoutine());
    }

    private string DefeatMessage()
    {
        if (health == null) return "The jungle wins this round.";
        switch (health.FailureReason)
        {
            case PlayerHealth.FailureCause.RivalFinishedFirst:
                return "The rival filled the basket first.";
            case PlayerHealth.FailureCause.NotEnoughBananas:
                return "Not enough bananas for the basket.";
            case PlayerHealth.FailureCause.OutOfHearts:
                return "Caught in the jungle.";
            default:
                return "The jungle wins this round.";
        }
    }

    private IEnumerator VictoryRoutine()
    {
        yield return WaitUnscaled(victoryStartDelay);
        if (player == null)
        {
            victoryRunning = false;
            yield break;
        }

        Vector3 origin = player.transform.position;
        float travel = VictoryTravelDistance(origin);
        int hops = Mathf.Max(1, victoryHops);
        float hopDistance = travel / hops;

        for (int hop = 0; hop < hops; hop++)
        {
            Vector3 from = player.transform.position;
            Vector3 to = from + Vector3.forward * hopDistance;
            PlayJumpPose();

            bool burstFired = false;
            float elapsed = 0f;
            float duration = Mathf.Max(0.25f, victoryHopDuration);
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                Vector3 position = Vector3.Lerp(from, to, t);
                position.y = from.y + 4f * victoryHopHeight * t * (1f - t);
                player.transform.position = position;

                if (!burstFired && t >= 0.4f)
                {
                    burstFired = true;
                    burst.Emit(position + Vector3.up * 0.3f,
                        Mathf.Max(3, victoryBananasPerBurst / 2), 0.95f, 0.3f);
                }
                yield return null;
            }

            player.transform.position = new Vector3(to.x, from.y, to.z);
            burst.Emit(player.transform.position + Vector3.up * 1.15f,
                victoryBananasPerBurst, 1.05f, 0.4f);
        }

        PlayIdlePose();
        burst.Emit(BasketPoint() + Vector3.up * 0.35f,
            victoryBananasPerBurst * 2, 1.4f, 0.65f);
        yield return WaitUnscaled(0.35f);
        victoryRunning = false;
    }

    private float VictoryTravelDistance(Vector3 from)
    {
        float travel = victoryTravel;
        if (celebrationBasket != null)
        {
            float toBasket = celebrationBasket.position.z - from.z + 1.2f;
            travel = Mathf.Clamp(toBasket, 2.8f, victoryTravel);
        }
        return travel;
    }

    private Vector3 BasketPoint()
    {
        if (celebrationBasket != null) return celebrationBasket.position;
        if (player != null) return player.transform.position + Vector3.up * 1.2f;
        return transform.position;
    }

    private IEnumerator DefeatRoutine()
    {
        CaptureSitPose();
        poseWeight = 0f;
        posing = hips != null || sitBones != null;

        float elapsed = 0f;
        while (elapsed < defeatSitBlendSeconds)
        {
            elapsed += Time.deltaTime;
            poseWeight = Mathf.Clamp01(elapsed / defeatSitBlendSeconds);
            yield return null;
        }
        poseWeight = 1f;

        // The gorilla winds up and steps in while the monkey slumps.
        float waited = 0f;
        while (waited < defeatMaxSitSeconds)
        {
            if (chaser != null && chaser.HasCaughtPlayer) break;
            if (chaser == null && waited > 1.1f) break;
            waited += Time.deltaTime;
            yield return null;
        }

        // Once the hand closes, relax the slump so the lift reads as a limp monkey.
        yield return new WaitForSeconds(0.12f);
        float blend = 0f;
        while (blend < 0.4f)
        {
            blend += Time.deltaTime;
            poseWeight = 1f - Mathf.Clamp01(blend / 0.4f);
            yield return null;
        }

        poseWeight = 0f;
        ApplySitPose(0f);
        posing = false;
    }

    private void PlayJumpPose()
    {
        if (playerAnimator == null || !playerAnimator.isActiveAndEnabled) return;
        if (!playerAnimator.HasState(0, JumpState)) return;
        playerAnimator.speed = 1f;
        playerAnimator.Play(JumpState, 0, 0f);
    }

    private void PlayIdlePose()
    {
        if (playerAnimator == null || !playerAnimator.isActiveAndEnabled) return;
        if (!playerAnimator.HasState(0, IdleState)) return;
        playerAnimator.speed = 1f;
        playerAnimator.Play(IdleState, 0, 0f);
    }

    /// <summary>
    /// Freezes the player on the Idle pose, then records the bone offsets used by
    /// the ground sit. RunnerAnimation is already frozen after a failure, so the
    /// director owns the pose until the gorilla lifts the monkey.
    /// </summary>
    private void CaptureSitPose()
    {
        hips = null;
        sitBones = null;
        if (playerAnimator == null || !playerAnimator.isHuman) return;

        bool hasIdle;
        try { hasIdle = playerAnimator.HasState(0, IdleState); }
        catch (System.Exception) { hasIdle = false; }
        if (!hasIdle) return;

        playerAnimator.speed = 1f;
        playerAnimator.Play(IdleState, 0, 0f);
        playerAnimator.Update(0.001f);
        playerAnimator.speed = 0f;

        hips = playerAnimator.GetBoneTransform(HumanBodyBones.Hips);
        if (hips == null) return;
        hipsRest = hips.localPosition;

        var bones = new List<BoneOffset>(10);
        AddBone(bones, HumanBodyBones.Spine, 8f, false);
        AddBone(bones, HumanBodyBones.Chest, 7f, true);
        AddBone(bones, HumanBodyBones.Head, 24f, true);
        AddBone(bones, HumanBodyBones.LeftUpperArm, -20f, false);
        AddBone(bones, HumanBodyBones.RightUpperArm, -20f, false);
        AddBone(bones, HumanBodyBones.LeftLowerArm, -42f, true);
        AddBone(bones, HumanBodyBones.RightLowerArm, -42f, true);
        AddBone(bones, HumanBodyBones.LeftUpperLeg, -84f, false);
        AddBone(bones, HumanBodyBones.RightUpperLeg, -84f, false);
        AddBone(bones, HumanBodyBones.LeftLowerLeg, 6f, false);
        AddBone(bones, HumanBodyBones.RightLowerLeg, 6f, false);
        sitBones = bones.Count > 0 ? bones.ToArray() : null;
    }

    private void AddBone(List<BoneOffset> bones, HumanBodyBones bone, float angle, bool sobbing)
    {
        Transform boneTransform = playerAnimator.GetBoneTransform(bone);
        if (boneTransform == null) return;
        bones.Add(new BoneOffset { Bone = boneTransform, Rest = boneTransform.rotation, Angle = angle, Sob = sobbing });
    }

    private void ApplySitPose(float weight)
    {
        if (weight <= 0f)
        {
            if (hips != null) hips.localPosition = hipsRest;
            if (sitBones != null)
            {
                for (int i = 0; i < sitBones.Length; i++)
                    if (sitBones[i].Bone != null) sitBones[i].Bone.rotation = sitBones[i].Rest;
            }
            return;
        }

        if (hips != null)
            hips.localPosition = hipsRest + Vector3.down * (defeatSitDrop * weight);

        if (sitBones == null) return;
        Vector3 axis = player != null ? player.transform.right : Vector3.right;
        // A slow head/shoulder bob sells the disappointment without an extra clip.
        float sob = weight >= 0.85f ? Mathf.Sin(Time.unscaledTime * 5.4f) * 1.7f : 0f;
        for (int i = 0; i < sitBones.Length; i++)
        {
            BoneOffset offset = sitBones[i];
            if (offset.Bone == null) continue;
            float angle = (offset.Angle + (offset.Sob ? sob : 0f)) * weight;
            offset.Bone.rotation = Quaternion.AngleAxis(angle, axis) * offset.Rest;
        }
    }

    private static IEnumerator WaitUnscaled(float seconds)
    {
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }
}
