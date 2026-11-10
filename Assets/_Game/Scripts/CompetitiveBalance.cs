using UnityEngine;

/// <summary>
/// Shared competitive-fairness tuning for the player-versus-rival race.
/// Keeps both runners inside a fair, visible window through paired pickups,
/// position-scaled Thunder, a soft elastic distance leash and post-hit help.
/// </summary>
public static class CompetitiveBalance
{
    /// <summary>A runner this far ahead counts as leading for Thunder scaling.</summary>
    public const float LeadingGapMeters = 4f;

    /// <summary>Leading Thunder is a reward, not an escape: +18% for 2.5 s.</summary>
    public const float LeaderThunderSpeedBonus = 0.18f;
    public const float LeaderThunderSeconds = 2.5f;
    /// <summary>Trailing/neck-and-neck Thunder is a catch-up tool: +38% for 4 s.</summary>
    public const float TrailerThunderSpeedBonus = 0.38f;
    public const float TrailerThunderSeconds = 4f;

    /// <summary>Rival starts easing once it is 18 m ahead of the player.</summary>
    public const float LeashAheadStartMeters = 18f;
    /// <summary>Rival starts pushing once it is 20 m behind the player.</summary>
    public const float LeashBehindStartMeters = 20f;
    public const float LeashAheadSlowMin = 0.10f;
    public const float LeashAheadSlowMax = 0.18f;
    public const float LeashBehindBoostMin = 0.12f;
    public const float LeashBehindBoostMax = 0.20f;
    /// <summary>Full leash strength is reached at these gaps.</summary>
    public const float LeashAheadFullMeters = 34f;
    public const float LeashBehindFullMeters = 38f;
    /// <summary>The leash releases inside the final stretch so the finish is earned.</summary>
    public const float LeashRelaxDistance = 80f;

    /// <summary>Chain-hit protection granted once a runner recovers.</summary>
    public const float RecoveryImmunitySeconds = 2.5f;
    /// <summary>Short acceleration burst that follows the protection window start.</summary>
    public const float RecoveryBurstSeconds = 1.2f;
    public const float RecoveryBurstSpeedBonus = 0.35f;

    /// <summary>Leader-only pickup respawn delay and its scale-in presentation.</summary>
    public const float PickupRespawnDelaySeconds = 1.2f;
    public const float PickupRespawnScaleInSeconds = 0.25f;
    /// <summary>How close the trailing runner may be before respawn is pointless.</summary>
    public const float PickupRespawnPassedMargin = 0.25f;

    public static bool IsLeading(float collectorZ, float otherZ) =>
        collectorZ - otherZ > LeadingGapMeters;

    public static float ThunderSpeedMultiplier(bool leading) =>
        1f + (leading ? LeaderThunderSpeedBonus : TrailerThunderSpeedBonus);

    public static float ThunderDuration(bool leading) =>
        leading ? LeaderThunderSeconds : TrailerThunderSeconds;

    /// <summary>
    /// Taper/boost curve for the rival's base pace. A positive lead means the
    /// rival is ahead of the player. The leash is fully relaxed within the
    /// final <see cref="LeashRelaxDistance"/> metres.
    /// </summary>
    public static float RivalLeashMultiplier(float rivalLead, float distanceToFinish)
    {
        if (distanceToFinish <= LeashRelaxDistance) return 1f;
        if (rivalLead > LeashAheadStartMeters)
        {
            float t = Mathf.InverseLerp(LeashAheadStartMeters, LeashAheadFullMeters, rivalLead);
            return 1f - Mathf.Lerp(LeashAheadSlowMin, LeashAheadSlowMax, t);
        }
        if (rivalLead < -LeashBehindStartMeters)
        {
            float t = Mathf.InverseLerp(LeashBehindStartMeters, LeashBehindFullMeters, -rivalLead);
            return 1f + Mathf.Lerp(LeashBehindBoostMin, LeashBehindBoostMax, t);
        }
        return 1f;
    }

    /// <summary>
    /// Decaying recovery burst. Returns 1 outside the burst window and eases
    /// from <paramref name="bonus"/> back to zero over its duration.
    /// </summary>
    public static float RecoveryBurstMultiplier(float now, float start, float end, float bonus)
    {
        if (bonus <= 0f || end <= now || now < start) return 1f;
        float t = Mathf.Clamp01((now - start) / Mathf.Max(0.01f, end - start));
        return 1f + bonus * (1f - t);
    }
}
