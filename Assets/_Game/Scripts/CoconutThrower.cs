using UnityEngine;

[RequireComponent(typeof(PlayerHealth))]
public class CoconutThrower : MonoBehaviour
{
    [SerializeField] private CoconutProjectile projectilePrefab;

    [SerializeField]
    private Vector3 spawnOffset = new Vector3(0f, 1f, 0.7f);

    public bool HasCoconut { get; private set; }

    public bool CanThrow =>
        HasCoconut &&
        health != null &&
        !health.IsRunOver &&
        projectilePrefab != null;

    private PlayerHealth health;

    private void Awake()
    {
        health = GetComponent<PlayerHealth>();
    }

    public bool TryCollect()
    {
        if (health.IsRunOver || HasCoconut)
            return false;

        HasCoconut = true;
        return true;
    }

    public void Throw()
    {
        if (!CanThrow)
            return;

        HasCoconut = false;

        CoconutProjectile projectile = Instantiate(
            projectilePrefab,
            transform.position + spawnOffset,
            Quaternion.identity
        );

        projectile.Initialize(health);
    }
}