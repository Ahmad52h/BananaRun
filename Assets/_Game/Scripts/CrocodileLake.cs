using UnityEngine;

/// <summary>Trigger volume below the river surface; routes falls to the respawn controller.</summary>
[RequireComponent(typeof(BoxCollider), typeof(Rigidbody))]
public sealed class CrocodileLake : MonoBehaviour
{
    private RiverCrocodileGauntlet owner;

    public void Initialize(RiverCrocodileGauntlet gauntlet)
    {
        owner = gauntlet;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (owner == null || other == null) return;

        var health = other.GetComponentInParent<PlayerHealth>();
        if (health != null)
        {
            owner.HandlePlayerFall(health);
            return;
        }

        var rival = other.GetComponentInParent<RivalRunner>();
        if (rival != null) owner.HandleRivalFall(rival);
    }
}
