using System;
using UnityEngine;

[RequireComponent(typeof(RunnerMotor))]
[RequireComponent(typeof(RunnerInput))]
public class PlayerHealth : MonoBehaviour
{
    [SerializeField, Min(1)] private int maxHearts = 3;

    [SerializeField, Min(0f)]
    private float protectionDuration = 2f;

    public int Hearts { get; private set; }
    public int MaxHearts => maxHearts;
    public bool IsRunOver { get; private set; }
    public bool IsDead => Hearts <= 0;
    public bool IsProtected => Time.time < protectionEndsAt ||
        (powerUps != null && powerUps.IsActive(PowerUpType.Bulldozer));

    public event Action<int> HeartsChanged;
    public event Action Died;
    public event Action HitTaken;

    private float protectionEndsAt;
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

    public void TakeHit()
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
            EndRun();
        }

        HeartsChanged?.Invoke(Hearts);

        if (IsDead)
        {
            Died?.Invoke();
        }
    }
    public void EndRun()
    {
        if (IsRunOver)
            return;

        IsRunOver = true;

        if (motor != null) motor.enabled = false;
        if (runnerInput != null) runnerInput.enabled = false;
    }
}
