using TMPro;
using UnityEngine;

[RequireComponent(typeof(TMP_Text))]
public class BananaCounterUI : MonoBehaviour
{
    [SerializeField] private BananaInventory inventory;

    private TMP_Text label;

    private void Awake()
    {
        label = GetComponent<TMP_Text>();
    }

    private void OnEnable()
    {
        if (inventory == null)
        {
            Debug.LogError(
                "BananaCounterUI: Assign Player to Inventory.",
                this
            );

            return;
        }

        inventory.CountChanged += Refresh;
        Refresh(inventory.Count);
    }

    private void OnDisable()
    {
        if (inventory != null)
        {
            inventory.CountChanged -= Refresh;
        }
    }

    private void Refresh(int count)
    {
        label.text = count.ToString();
    }
}