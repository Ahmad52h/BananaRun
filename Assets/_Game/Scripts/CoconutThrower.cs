using UnityEngine;

[RequireComponent(typeof(PlayerHealth))]
public class CoconutThrower : MonoBehaviour
{
    [SerializeField] private CoconutProjectile projectilePrefab;

    [SerializeField]
    private Vector3 spawnOffset = new Vector3(0f, 1f, 0.7f);

    [Header("Ammo Settings")]
    [SerializeField] private int maxCoconuts = 5;

    private int currentCoconuts = 0;
    private PlayerHealth health;
    private RunnerAnimation runnerAnimation;

    public bool HasCoconut => currentCoconuts > 0;
    public int CurrentCoconuts => currentCoconuts;
    public int MaxCoconuts => maxCoconuts;

    public bool CanThrow =>
        HasCoconut &&
        health != null &&
        !health.IsRunOver &&
        projectilePrefab != null;

    private void Awake()
    {
        health = GetComponent<PlayerHealth>();
        runnerAnimation = GetComponent<RunnerAnimation>();
    }

    public bool TryCollect()
    {
        if (health == null || health.IsRunOver || currentCoconuts >= maxCoconuts)
            return false;

        currentCoconuts++;
        return true;
    }

    public void Throw()
    {
        if (!CanThrow)
            return;

        currentCoconuts--;

        // تشغيل أنيميشن الرمي باليدين تلقائياً دون تشويه
        if (runnerAnimation != null)
        {
            runnerAnimation.TriggerThrow(0.5f);
        }

        CoconutProjectile projectile = Instantiate(
            projectilePrefab,
            transform.position + spawnOffset,
            Quaternion.identity
        );

        projectile.Initialize(health);
    }
}
