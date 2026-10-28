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
    public bool IsProjectileStunned => !(powerUps != null && powerUps.HasThunder) && Time.time < projectileStunEndsAt;
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
        if (powerUps != null && powerUps.ConsumeShield()) return;

        Hearts = Mathf.Max(0, Hearts - 1);
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

    /// <summary>
    /// A crocodile-lake fall costs a heart but never ends the run immediately. Movement is
    /// frozen here and restored by <see cref="RespawnAfterWater"/> once the lake delay passes.
    /// </summary>
    public bool BeginWaterFall()
    {
        if (IsRunOver) return false;

        bool damaged = false;
        if (!IsProtected)
        {
            Hearts = Mathf.Max(0, Hearts - 1);
            damaged = true;
            protectionEndsAt = Time.time + 1.5f;
            if (damageFlash == null) damageFlash = DamageFlashUI.GetOrCreate();
            damageFlash?.TriggerDamageFlash();
            RunnerAudio.Instance?.PlayHurt();
            HeartsChanged?.Invoke(Hearts);
        }

        if (motor != null) motor.enabled = false;
        if (runnerInput != null) runnerInput.enabled = false;
        var controller = GetComponent<CharacterController>();
        if (controller != null) controller.enabled = false;
        projectileStunEndsAt = Mathf.Min(projectileStunEndsAt, Time.time);
        projectileProtectionEndsAt = Mathf.Min(projectileProtectionEndsAt, Time.time);
        return damaged;
    }

    /// <summary>Places the runner on the far bank after the lake delay with brief protection.</summary>
    public void RespawnAfterWater(Vector3 position, float invulnerability)
    {
        if (IsRunOver) return;

        var controller = GetComponent<CharacterController>();
        if (controller != null)
        {
            controller.enabled = false;
            transform.position = position;
            controller.enabled = true;
        }
        transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);

        if (motor != null)
        {
            motor.ResetVerticalVelocity();
            motor.enabled = true;
        }
        if (runnerInput != null) runnerInput.enabled = true;
        projectileStunEndsAt = Mathf.Min(projectileStunEndsAt, Time.time);
        projectileProtectionEndsAt = Mathf.Min(projectileProtectionEndsAt, Time.time);
        protectionEndsAt = Mathf.Max(protectionEndsAt, Time.time + Mathf.Max(0.1f, invulnerability));
    }

    public bool TakeProjectileHit()
    {
        if (IsRunOver || Time.time < projectileProtectionEndsAt ||
            (powerUps != null && powerUps.IsActive(PowerUpType.Bulldozer))) return false;
        // Thunder grants temporary stumble immunity while it is active.
        if (powerUps != null && powerUps.HasThunder) return false;
        if (powerUps != null && powerUps.ConsumeShield())
        {
            projectileProtectionEndsAt = Time.time + 0.4f;
            return false;
        }
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
