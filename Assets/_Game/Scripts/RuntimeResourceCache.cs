using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Cache successful and missing resource lookups; release menu references on scene unload.</summary>
public static class RuntimeResourceCache
{
    private static readonly Dictionary<(Type, string), UnityEngine.Object> assets =
        new Dictionary<(Type, string), UnityEngine.Object>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        assets.Clear();
        SceneManager.sceneUnloaded -= ClearScene;
        SceneManager.sceneUnloaded += ClearScene;
    }

    private static void ClearScene(Scene scene) => assets.Clear();

    public static T Load<T>(string path) where T : UnityEngine.Object
    {
        var key = (typeof(T), path);
        if (!assets.TryGetValue(key, out var asset))
        {
            asset = Resources.Load<T>(path);
            assets.Add(key, asset);
        }
        return asset as T;
    }
}
