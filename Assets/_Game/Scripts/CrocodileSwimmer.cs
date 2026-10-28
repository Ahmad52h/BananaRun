using UnityEngine;

/// <summary>Low-poly crocodile that patrols the lake and lunges when a runner splashes down.</summary>
public sealed class CrocodileSwimmer : MonoBehaviour
{
    private Transform tailRoot, tailTip, jawPivot;
    private Vector3 home;
    private float phase, patrolRadius, swimSpeed, baseYaw;
    private float reactionEndsAt, reactionStartedAt;
    private Vector3 reactionPoint;

    public void Configure(Transform tail, Transform tip, Transform jaw, Vector3 homePosition,
        float patrol, float speed, float randomPhase)
    {
        tailRoot = tail;
        tailTip = tip;
        jawPivot = jaw;
        home = homePosition;
        patrolRadius = Mathf.Max(0.2f, patrol);
        swimSpeed = Mathf.Max(0.1f, speed);
        phase = randomPhase;
        baseYaw = transform.eulerAngles.y;
    }

    public void ReactToSplash(Vector3 point)
    {
        // Each crocodile lunges toward the splash but stays near its own patrol
        // water, so several animals converge around the impact without stacking.
        Vector3 offset = point - home;
        offset.y = 0f;
        const float maxLunge = 2.6f;
        if (offset.sqrMagnitude > maxLunge * maxLunge)
            offset = offset.normalized * maxLunge;
        reactionPoint = home + offset;
        reactionStartedAt = Time.time;
        reactionEndsAt = Time.time + 1.55f;
    }

    private void Update()
    {
        float now = Time.time;
        if (now < reactionEndsAt)
        {
            React(now);
            return;
        }
        Idle(now);
    }

    private void Idle(float now)
    {
        float drift = Mathf.Sin(now * swimSpeed + phase);
        float sideDrift = Mathf.Cos(now * swimSpeed * 0.63f + phase);
        Vector3 target = new Vector3(
            home.x + drift * patrolRadius,
            home.y + Mathf.Sin(now * 1.7f + phase) * 0.035f,
            home.z + sideDrift * patrolRadius * 0.35f);

        float blend = 1f - Mathf.Exp(-1.6f * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, target, blend);
        transform.rotation = Quaternion.Euler(0f, baseYaw + drift * 12f, 0f);

        if (tailRoot != null)
            tailRoot.localRotation = Quaternion.Euler(0f, Mathf.Sin(now * 2.1f + phase) * 18f, 0f);
        if (tailTip != null)
            tailTip.localRotation = Quaternion.Euler(0f, Mathf.Sin(now * 2.5f + phase + 0.7f) * 24f, 0f);
        if (jawPivot != null)
            jawPivot.localRotation = Quaternion.Slerp(jawPivot.localRotation, Quaternion.identity,
                1f - Mathf.Exp(-10f * Time.deltaTime));
    }

    private void React(float now)
    {
        float elapsed = Mathf.Max(0f, now - reactionStartedAt);
        float lunge = Mathf.Clamp01(elapsed / 0.45f);
        float settle = Mathf.Clamp01((elapsed - 0.85f) / 0.7f);

        Vector3 target = reactionPoint + new Vector3(0f, 0f, -0.4f);
        Vector3 position = Vector3.Lerp(home, target, lunge);
        position.y = home.y + Mathf.Sin(now * 19f) * 0.06f;
        transform.position = position;

        Vector3 facing = reactionPoint - transform.position;
        facing.y = 0f;
        if (facing.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(facing.normalized, Vector3.up) *
                Quaternion.Euler(0f, 0f, Mathf.Sin(now * 14f) * (1f - settle) * 24f);

        if (jawPivot != null)
            jawPivot.localRotation = Quaternion.Euler(Mathf.Abs(Mathf.Sin(now * 24f)) * 28f * (1f - settle), 0f, 0f);
        if (tailRoot != null)
            tailRoot.localRotation = Quaternion.Euler(0f, Mathf.Sin(now * 22f) * 34f * (1f - settle), 0f);
        if (tailTip != null)
            tailTip.localRotation = Quaternion.Euler(0f, Mathf.Sin(now * 26f + 0.8f) * 38f * (1f - settle), 0f);
    }
}
