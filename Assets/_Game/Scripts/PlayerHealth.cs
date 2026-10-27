using System;
using UnityEngine;

[RequireComponent(typeof(RunnerMotor))]
[RequireComponent(typeof(RunnerInput))]
public class PlayerHealth : MonoBehaviour
{
    public enum FailureCause { None, OutOfHearts, RivalFinishedFirst, NotEnoughBananas }
    [SerializeField, Min(1)] private int maxHearts = 3;

    [SerializeField, Min(0f)]
    private float protectionDuration = 2f;
    [SerializeField, Min(0.1f)] private float projectileStunDuration = 2f;

    public int Hearts { get; private set; }
    public int MaxHearts => maxHearts;
    public bool IsRunOver { get; private set; }
    public bool IsDead => Hearts <= 0;
    public bool HasFailed { get; private set; }
    public FailureCause FailureReason { get; private set; }
    public bool IsProjectileStunned => Time.time < projectileStunEndsAt;
    public float ProjectileStunRemaining => Mathf.Max(0f, projectileStunEndsAt - Time.time);
    public bool IsProtected => Time.time < protectionEndsAt ||
        (powerUps != null && powerUps.IsActive(PowerUpType.Bulldozer));

    public event Action<int> HeartsChanged;
    public event Action Died;
    public event Action HitTaken;
    public event Action RunFailed;
    public event Action ProjectileHit;

    private float protectionEndsAt;
    private float projectileStunEndsAt, projectileProtectionEndsAt;
    private RunnerMotor motor;
    private RunnerInput runnerInput;
    private PowerUpManager powerUps;
    private DamageFlashUI damageFlash;

    private void Awake()
    {
        Hearts = maxHearts;

        motor = GetComponent<RunnerMotor>();
        runnerInput = GetComponent<RunnerInput>();
        powerUps = GetComponent<PowerUpManager>();
        damageFlash = DamageFlashUI.GetOrCreate();
    }

    // Existing editor tools and obstacle setups may still call this entry point.
    public void TakeHit() => TakeObstacleHit();

    public void TakeObstacleHit()
    {
        if (IsRunOver || IsProtected)
            return;

        Hearts--;
        protectionEndsAt = Time.time + protectionDuration;
        if (damageFlash == null) damageFlash = DamageFlashUI.GetOrCreate();
        damageFlash?.TriggerDamageFlash();
        RunnerAudio.Instance?.PlayHurt();
        HitTaken?.Invoke();

        if (IsDead)
        {
            FailRun(FailureCause.OutOfHearts);
        }

        HeartsChanged?.Invoke(Hearts);

        if (IsDead)
        {
            Died?.Invoke();
        }
    }

    public bool TakeProjectileHit()
    {
        if (IsRunOver || Time.time < projectileProtectionEndsAt ||
            (powerUps != null && powerUps.IsActive(PowerUpType.Bulldozer))) return false;
        projectileStunEndsAt = Time.time + projectileStunDuration;
        projectileProtectionEndsAt = projectileStunEndsAt + 0.4f;
        RunnerAudio.Instance?.PlayHurt();
        ProjectileHit?.Invoke();
        return true;
    }
    public void EndRun()
    {
        if (IsRunOver)
            return;

        IsRunOver = true;

        if (motor != null) motor.enabled = false;
        if (runnerInput != null) runnerInput.enabled = false;
    }

    public void FailRun(FailureCause cause)
    {
        if (HasFailed || IsRunOver) return;
        HasFailed = true;
        FailureReason = cause;
        EndRun();
        RunFailed?.Invoke();
    }
}
