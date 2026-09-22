using System;
using UnityEngine;

public class BananaInventory : MonoBehaviour
{
    public int Count { get; private set; }

    public event Action<int> CountChanged;

    public void AddBananas(int amount)
    {
        if (amount <= 0)
            return;

        Count += amount;
        CountChanged?.Invoke(Count);
    }
}