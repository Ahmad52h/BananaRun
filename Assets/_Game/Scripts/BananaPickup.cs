using UnityEngine;

[RequireComponent(typeof(SphereCollider))]
[RequireComponent(typeof(Rigidbody))]
public class BananaPickup : MonoBehaviour
{
    [SerializeField, Min(1)] private int value = 1;
    [SerializeField] private Transform visual;
    [SerializeField] private float rotationSpeed = 90f;

    private bool collected;

    private void Awake()
    {
        GetComponent<SphereCollider>().isTrigger = true;

        Rigidbody body = GetComponent<Rigidbody>();
        body.useGravity = false;
        body.isKinematic = true;
    }

    private void OnEnable()
    {
        collected = false;
    }

    private void Update()
    {
        if (visual != null)
        {
            visual.Rotate(
                Vector3.up,
                rotationSpeed * Time.deltaTime,
                Space.Self
            );
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (collected)
            return;

        BananaInventory inventory =
            other.GetComponentInParent<BananaInventory>();

        if (inventory == null)
            return;

        collected = true;
        inventory.AddBananas(value);

        gameObject.SetActive(false);
    }
}