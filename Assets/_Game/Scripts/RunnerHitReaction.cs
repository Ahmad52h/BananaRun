using UnityEngine;

/// <summary>Shared stumble pose and clip timing for player, rival, and gorilla hit reactions.</summary>
public static class RunnerHitReaction
{
    private static readonly int HitState = Animator.StringToHash("Base Layer.Hit");
    private static readonly int StumbleSpeed = Animator.StringToHash("StumbleSpeed");
    private static readonly int Sliding = Animator.StringToHash("Sliding");
    private static readonly int HitTrigger = Animator.StringToHash("Hit");

    public static bool Play(Animator animator, float clipLength, float duration)
    {
        if (animator == null || !animator.isActiveAndEnabled || !animator.HasState(0, HitState)) return false;
        animator.speed = 1f;
        foreach (var parameter in animator.parameters)
        {
            if (parameter.nameHash == StumbleSpeed && parameter.type == AnimatorControllerParameterType.Float)
                animator.SetFloat(StumbleSpeed, clipLength / Mathf.Max(0.1f, duration));
            else if (parameter.nameHash == Sliding && parameter.type == AnimatorControllerParameterType.Bool)
                animator.SetBool(Sliding, false);
            else if (parameter.nameHash == HitTrigger && parameter.type == AnimatorControllerParameterType.Trigger)
                animator.ResetTrigger(HitTrigger);
        }
        animator.CrossFadeInFixedTime(HitState, 0.08f, 0, 0f);
        return true;
    }
}
