using System;
using TMPro;
using UnityEngine;

/// <summary>Ranks the two runners by actual track distance and locks their finish order.</summary>
[DefaultExecutionOrder(300)]
[DisallowMultipleComponent]
public sealed class RaceProgressTracker : MonoBehaviour
{
    public enum Competitor { Tied, Player, Rival }
    public enum RaceOutcome { Racing, PlayerWon, PlayerLost }

    [SerializeField] private RunnerMotor player;
    [SerializeField] private RivalRunner rival;
    [SerializeField] private FinishGate finishGate;
    [SerializeField] private TMP_Text standingsLabel;
    [SerializeField] private float playerDistance, rivalDistance;
    [SerializeField] private int playerPosition = 1, rivalPosition = 1;

    private float startZ, finishZ, previousPlayerZ, previousRivalZ, previousTime;
    private PlayerHealth playerHealth;
    private RunnerAnimation playerAnimation;
    private CharacterController playerController;
    private float lastGroundedY;
    private bool playerWasStumbling;
    private BananaInventory playerInventory;

    public float PlayerDistance => playerDistance;
    public float RivalDistance => rivalDistance;
    public float RaceLength => Mathf.Max(0.01f, finishZ - startZ);
    public float PlayerProgress => Mathf.Clamp01(playerDistance / RaceLength);
    public float RivalProgress => Mathf.Clamp01(rivalDistance / RaceLength);
    public int PlayerPosition => playerPosition;
    public int RivalPosition => rivalPosition;
    public RunnerMotor Player => player;
    public RivalRunner Rival => rival;
    public RaceOutcome Outcome { get; private set; }
    public int PlayerBananas => playerInventory != null ? playerInventory.Count : 0;
    public int RivalBananas => rival != null ? rival.BananaCount : 0;
    public bool PlayerQualified => finishGate != null && PlayerBananas >= finishGate.RequiredBananas;
    public bool RivalQualified => finishGate != null && RivalBananas >= finishGate.RequiredBananas;
    public bool PlayerWonBasketRace => Outcome == RaceOutcome.PlayerWon;
    public bool PlayerFinished => PlayerFinishTime >= 0f;
    public bool RivalFinished => RivalFinishTime >= 0f;
    public float PlayerFinishTime { get; private set; } = -1f;
    public float RivalFinishTime { get; private set; } = -1f;
    public Competitor Leader { get; private set; }
    public event Action<Competitor> LeaderChanged;

    private void Start()
    {
        // The debug standings overlay duplicated the top HUD progress bar.
        // Keep it in the scene for compatibility but never show it.
        if (standingsLabel != null)
        {
            standingsLabel.text = string.Empty;
            standingsLabel.gameObject.SetActive(false);
        }

        if (player == null || rival == null || finishGate == null)
        {
            Debug.LogError("RaceProgressTracker needs a player, rival and finish gate.", this);
            enabled = false;
            return;
        }
        startZ = player.transform.position.z;
        // Both runners cross the front of the same finish trigger with the same capsule radius.
        var gateCollider = finishGate.GetComponent<BoxCollider>();
        var playerCollider = player.GetComponent<CharacterController>();
        finishZ = gateCollider.bounds.min.z - playerCollider.radius - playerCollider.skinWidth;
        previousPlayerZ = player.transform.position.z;
        previousRivalZ = rival.transform.position.z;
        previousTime = Time.time;
        playerHealth = player.GetComponent<PlayerHealth>();
        playerInventory = player.GetComponent<BananaInventory>();
        playerAnimation = player.GetComponent<RunnerAnimation>();
        playerController = player.GetComponent<CharacterController>();
        lastGroundedY = player.transform.position.y;
        RefreshStandings();
    }

    private void LateUpdate()
    {
        // Existing hits already slow the motor. Slips also need a distance penalty in a race.
        bool stumbling = playerAnimation != null && playerAnimation.IsReactingToHit;
        if (stumbling && !playerWasStumbling && playerHealth != null && !playerHealth.IsRunOver)
            player.ApplySlowdown(0.4f, 0.85f);
        playerWasStumbling = stumbling;
        if (playerController != null && playerController.enabled && player.isActiveAndEnabled &&
            playerHealth != null && !playerHealth.IsRunOver)
        {
            if (playerController.isGrounded) lastGroundedY = player.transform.position.y;
            else if ((playerAnimation == null || !playerAnimation.IsVineControlled) &&
                player.transform.position.y < lastGroundedY - 2f)
                // Falling below the track must not retain full racing speed.
                player.ApplySlowdown(0f, 0.3f);
        }
        RefreshStandings();
    }

    private void RefreshStandings()
    {
        if (player == null || rival == null || finishGate == null) return;
        float playerZ = player.transform.position.z, rivalZ = rival.transform.position.z;
        if (Outcome == RaceOutcome.Racing && !PlayerFinished && (playerHealth == null || !playerHealth.HasFailed) &&
            (playerZ >= finishZ || finishGate.Completed))
            PlayerFinishTime = CrossingTime(previousPlayerZ, playerZ);
        bool rivalJustFinished = Outcome == RaceOutcome.Racing && !RivalFinished &&
            (rivalZ >= finishZ || finishGate.RivalDelivered);
        if (rivalJustFinished)
        {
            RivalFinishTime = CrossingTime(previousRivalZ, rivalZ);
            rival.FinishRace();
        }
        if (PlayerFinished) playerDistance = RaceLength;
        else if (Outcome == RaceOutcome.Racing) playerDistance = Mathf.Clamp(playerZ - startZ, 0f, RaceLength);
        if (RivalFinished) rivalDistance = RaceLength;
        else if (Outcome == RaceOutcome.Racing) rivalDistance = Mathf.Clamp(rivalZ - startZ, 0f, RaceLength);

        if (Outcome == RaceOutcome.Racing)
        {
            if (playerHealth != null && playerHealth.HasFailed) Outcome = RaceOutcome.PlayerLost;
            else if (RivalFinished && (!PlayerFinished || RivalFinishTime < PlayerFinishTime))
            {
                Outcome = RaceOutcome.PlayerLost;
                playerHealth?.FailRun(PlayerHealth.FailureCause.RivalFinishedFirst);
            }
            else if (PlayerFinished)
            {
                Outcome = PlayerQualified ? RaceOutcome.PlayerWon : RaceOutcome.PlayerLost;
                if (!PlayerQualified) playerHealth?.FailRun(PlayerHealth.FailureCause.NotEnoughBananas);
            }
            if (Outcome != RaceOutcome.Racing) rival.FinishRace();
        }
        if (rivalJustFinished) finishGate.ReceiveRival(rival);

        Competitor leader;
        if (PlayerFinished && RivalFinished)
            leader = Mathf.Abs(PlayerFinishTime - RivalFinishTime) < 0.001f ? Competitor.Tied :
                PlayerFinishTime < RivalFinishTime ? Competitor.Player : Competitor.Rival;
        else if (PlayerFinished) leader = Competitor.Player;
        else if (RivalFinished) leader = Competitor.Rival;
        else leader = Mathf.Abs(playerDistance - rivalDistance) < 0.05f ? Competitor.Tied :
                playerDistance > rivalDistance ? Competitor.Player : Competitor.Rival;
        playerPosition = leader == Competitor.Rival ? 2 : 1;
        rivalPosition = leader == Competitor.Player ? 2 : 1;
        if (leader != Leader) { Leader = leader; LeaderChanged?.Invoke(leader); }
        previousPlayerZ = playerZ;
        previousRivalZ = rivalZ;
        previousTime = Time.time;
    }

    public void RecordPlayerFinish() => RefreshStandings();
    public void RecordRivalFinish(RivalRunner competitor)
    {
        if (competitor == rival && !RivalFinished && Outcome == RaceOutcome.Racing) RefreshStandings();
    }

    private float CrossingTime(float previousZ, float currentZ) =>
        Mathf.Lerp(previousTime, Time.time, Mathf.InverseLerp(previousZ, currentZ, finishZ));
}
