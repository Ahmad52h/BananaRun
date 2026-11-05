using UnityEngine;

/// <summary>All active bananas share one runner/magnet snapshot per frame.</summary>
public static class RunnerMagnetTargets
{
    private static int frame = -1;
    private static Vector3 playerTarget, rivalTarget;
    private static float playerRadius, rivalRadius;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void Invalidate() => frame = -1;

    public static bool TryGetNearest(Vector3 position, out Vector3 target, out float radius)
    {
        if (frame != Time.frameCount)
        {
            frame = Time.frameCount;
            var player = PowerUpManager.ActivePlayer;
            var rival = RivalRunner.ActiveRival;
            playerRadius = player != null && player.isActiveAndEnabled && player.IsActive(PowerUpType.Magnet)
                ? player.MagnetRadius : 0f;
            rivalRadius = rival != null && rival.isActiveAndEnabled && rival.HasMagnet ? rival.MagnetRadius : 0f;
            if (playerRadius > 0f) playerTarget = player.transform.position + Vector3.up * 0.9f;
            if (rivalRadius > 0f) rivalTarget = rival.transform.position + Vector3.up * 0.9f;
        }
        target = default;
        radius = 0f;
        float nearest = float.PositiveInfinity;
        float distance = (playerTarget - position).sqrMagnitude;
        if (playerRadius > 0f && distance <= playerRadius * playerRadius)
        {
            target = playerTarget;
            radius = playerRadius;
            nearest = distance;
        }
        distance = (rivalTarget - position).sqrMagnitude;
        if (rivalRadius > 0f && distance <= rivalRadius * rivalRadius && distance < nearest)
        {
            target = rivalTarget;
            radius = rivalRadius;
        }
        return radius > 0f;
    }
}
