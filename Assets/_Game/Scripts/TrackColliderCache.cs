using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Physics queries reuse collider classifications, including misses, for the current scene.</summary>
public static class TrackColliderCache
{
    public struct Info
    {
        public RunnerObstacle Obstacle;
        public BridgeDeck Bridge;
        public BananaPickup Banana;
        public BombPickup Bomb;
        public PowerUpPickup PowerUp;
        public BananaPeelHazard Peel;
        public FallingCoconutHazard CoconutHazard;
        public DynamicRunnerHazard DynamicHazard;
        public RunnerMotor Player;
        public RivalRunner Rival;
        public bool IsHazard => Obstacle != null || Peel != null || CoconutHazard != null || DynamicHazard != null;
    }

    private static readonly Dictionary<Collider, Info> cache = new Dictionary<Collider, Info>(512);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        cache.Clear();
        SceneManager.sceneUnloaded -= ClearScene;
        SceneManager.sceneUnloaded += ClearScene;
    }

    private static void ClearScene(Scene scene) => cache.Clear();

    public static Info Get(Collider collider)
    {
        if (collider == null) return default;
        if (cache.TryGetValue(collider, out var info)) return info;
        info = new Info
        {
            Obstacle = collider.GetComponentInParent<RunnerObstacle>(),
            Bridge = collider.GetComponentInParent<BridgeDeck>(),
            Banana = collider.GetComponentInParent<BananaPickup>(),
            Bomb = collider.GetComponentInParent<BombPickup>(),
            PowerUp = collider.GetComponentInParent<PowerUpPickup>(),
            Peel = collider.GetComponentInParent<BananaPeelHazard>(),
            CoconutHazard = collider.GetComponentInParent<FallingCoconutHazard>(),
            DynamicHazard = collider.GetComponentInParent<DynamicRunnerHazard>(),
            Player = collider.GetComponentInParent<RunnerMotor>(),
            Rival = collider.GetComponentInParent<RivalRunner>()
        };
        cache.Add(collider, info);
        return info;
    }
}
