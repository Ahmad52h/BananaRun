using UnityEngine;

[RequireComponent(typeof(PlayerHealth))]
public class CoconutThrower : MonoBehaviour
{
    [SerializeField] private CoconutProjectile projectilePrefab;
    [SerializeField] private Vector3 spawnOffset = new Vector3(0f, 1f, 0.7f);
    [Header("Ammo")]
    [SerializeField, Range(1, 5)] private int maxCoconuts = 5;
    [Header("Throw Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private string throwTrigger = "Throw";

    private PlayerHealth health;
    private int currentCoconuts;
    private int throwTriggerHash;
    private bool hasThrowTrigger;

    public bool HasCoconut => currentCoconuts > 0;
    public int CurrentCoconuts => currentCoconuts;
    public int MaxCoconuts => maxCoconuts;
    public bool CanThrow => HasCoconut && health != null && !health.IsRunOver && projectilePrefab != null;

    private void Awake()
    {
        health = GetComponent<PlayerHealth>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (animator != null && !string.IsNullOrEmpty(throwTrigger))
        {
            throwTriggerHash = Animator.StringToHash(throwTrigger);
            foreach (var parameter in animator.parameters)
                if (parameter.nameHash == throwTriggerHash && parameter.type == AnimatorControllerParameterType.Trigger)
                    hasThrowTrigger = true;
        }
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
        if (!CanThrow) return;
        currentCoconuts--;
        if (hasThrowTrigger && animator != null && animator.isActiveAndEnabled)
        {
            animator.ResetTrigger(throwTriggerHash);
            animator.SetTrigger(throwTriggerHash);
        }
        var projectile = Instantiate(projectilePrefab, transform.position + spawnOffset, Quaternion.identity);
        projectile.Initialize(health);
    }

    private void OnValidate() => maxCoconuts = Mathf.Clamp(maxCoconuts, 1, 5);
}
