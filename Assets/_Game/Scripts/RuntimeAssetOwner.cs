using System.Collections.Generic;
using UnityEngine;

/// <summary>Only newly generated native assets belong here; imported/shared assets are never owned.</summary>
[DisallowMultipleComponent]
public sealed class RuntimeAssetOwner : MonoBehaviour
{
    private readonly List<Object> assets = new List<Object>();

    public static T Own<T>(T asset, Component creator) where T : Object
    {
        if (asset == null || creator == null) return asset;
        var owner = creator.GetComponent<RuntimeAssetOwner>();
        if (owner == null) owner = creator.gameObject.AddComponent<RuntimeAssetOwner>();
        owner.assets.Add(asset);
        return asset;
    }

    private void OnDestroy()
    {
        foreach (var asset in assets) if (asset != null) Destroy(asset);
        assets.Clear();
    }
}
