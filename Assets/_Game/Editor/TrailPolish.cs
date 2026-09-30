using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class TrailPolish
{
    private const string EnvironmentPath = "Assets/Polytope Studio/Lowpoly_Environments/Prefabs/";

    [MenuItem("Tools/Banana Run/Apply Woodland Polish")]
    public static void Apply()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (EditorApplication.isPlaying || scene.path != "Assets/_Game/Scenes/Level01.unity")
            throw new System.InvalidOperationException("Open Level01 in edit mode first.");
        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Polish woodland trail");
        var motor = Object.FindFirstObjectByType<RunnerMotor>();
        float lane = new SerializedObject(motor).FindProperty("laneDistance").floatValue;
        float center = motor.transform.position.x;
        float floor = GameObject.Find("Ground").GetComponent<Collider>().bounds.max.y;
        var obstacles = Object.FindObjectsByType<RunnerObstacle>(FindObjectsSortMode.None)
            .OrderBy(o => o.transform.position.z).ThenBy(o => o.transform.position.x).ToArray();
        int[] lanes = { 0, -1, 1, 0, 1, 0, -1, 1, -1, 0, 1, 0, -1, 1, 0, -1, 1 };
        for (int i = 0; i < obstacles.Length; i++)
        {
            var obstacle = obstacles[i];
            var t = obstacle.transform;
            bool crate = new SerializedObject(obstacle).FindProperty("breakable").boolValue;
            bool low = obstacle.name.StartsWith("LowObstacle");
            Undo.RegisterFullObjectHierarchyUndo(obstacle.gameObject, "Fit obstacle to lane");
            t.rotation = Quaternion.identity;
            t.localScale = Vector3.one;
            t.position = new Vector3(center + lanes[i % lanes.Length] * lane, floor, 28 + i * 12);
            Bounds bounds;
            if (!crate)
            {
                // Keep original prefab geometry as an inactive, recoverable override.
                foreach (Transform child in t.Cast<Transform>().ToArray())
                {
                    if (child.name == "Woodland Log Hurdle" || child.name == "Woodland Rune Stone")
                        Undo.DestroyObjectImmediate(child.gameObject);
                    else child.gameObject.SetActive(false);
                }
                var rootRenderer = t.GetComponent<Renderer>();
                if (rootRenderer != null) rootRenderer.enabled = false;
                var model = AddModel(low ? "Trees/PT_Fruit_Tree_01_logs.prefab" : "Rocks/PT_Menhir_Rock_02.prefab", t);
                model.name = low ? "Woodland Log Hurdle" : "Woodland Rune Stone";
                if (!low) model.transform.localRotation = Quaternion.Euler(0, 180, 0);
                Fit(model, low ? new Vector3(2.05f, 0.8f, 1.15f) : new Vector3(1.85f, 2.9f, 1.5f), t.position);
                bounds = VisibleBounds(model);
            }
            else
            {
                bounds = VisibleBounds(obstacle.gameObject);
                var reward = new SerializedObject(obstacle);
                var spawn = reward.FindProperty("bananaSpawnPoint").objectReferenceValue as Transform;
                var landing = reward.FindProperty("bananaLandingPoint").objectReferenceValue as Transform;
                if (spawn != null) spawn.position = new Vector3(t.position.x, floor + 2.5f, t.position.z);
                if (landing != null) landing.position = new Vector3(t.position.x, floor + 2.02f, t.position.z + 4);
                t.localScale *= 1.5f / bounds.size.y;
                bounds = VisibleBounds(obstacle.gameObject);
                t.position += new Vector3(t.position.x - bounds.center.x, floor - bounds.min.y, t.position.z - bounds.center.z);
                bounds = VisibleBounds(obstacle.gameObject);
            }
            var box = obstacle.GetComponent<BoxCollider>();
            box.center = t.InverseTransformPoint(bounds.center);
            box.size = new Vector3(bounds.size.x / t.lossyScale.x, bounds.size.y / t.lossyScale.y, bounds.size.z / t.lossyScale.z);
            box.isTrigger = true;
            foreach (var collider in obstacle.GetComponentsInChildren<Collider>())
                if (collider != box) collider.enabled = false;
            PrefabUtility.RecordPrefabInstancePropertyModifications(t);
            PrefabUtility.RecordPrefabInstancePropertyModifications(box);
        }
        // Align collectibles to real lanes and lift the imported banana's offset mesh off the dirt.
        foreach (var banana in Object.FindObjectsByType<BananaPickup>(FindObjectsSortMode.None))
        {
            Undo.RegisterFullObjectHierarchyUndo(banana.gameObject, "Align banana trail");
            var t = banana.transform;
            var bounds = VisibleBounds(banana.gameObject);
            int chosenLane = Mathf.Clamp(Mathf.RoundToInt((t.position.x - center) / lane), -1, 1);
            float z = t.position.z;
            for (int attempt = 0; attempt < 3; attempt++)
            {
                float x = center + chosenLane * lane;
                if (!obstacles.Any(o => Mathf.Abs(o.transform.position.z - z) < 3.5f && Mathf.Abs(o.transform.position.x - x) < 1.2f)) break;
                chosenLane = chosenLane == 1 ? -1 : chosenLane + 1;
            }
            t.position += new Vector3(center + chosenLane * lane - bounds.center.x, floor + 1.1f - bounds.center.y, 0);
            var sphere = banana.GetComponent<SphereCollider>();
            sphere.center = t.InverseTransformPoint(VisibleBounds(banana.gameObject).center);
            PrefabUtility.RecordPrefabInstancePropertyModifications(t);
            PrefabUtility.RecordPrefabInstancePropertyModifications(sphere);
        }
        var edging = GameObject.Find("Trail Edge Details");
        if (edging == null)
        {
            edging = new GameObject("Trail Edge Details");
            Undo.RegisterCreatedObjectUndo(edging, "Add trail edge details");
            for (int i = 0; i < 23; i++)
                for (int side = -1; side <= 1; side += 2)
                {
                    var rock = AddModel("Rocks/PT_Generic_Rock_01.prefab", edging.transform);
                    Fit(rock, new Vector3(0.38f, 0.22f + (i % 3) * 0.04f, 0.65f), new Vector3(side * 4.85f, floor - 0.04f, 8 + i * 10));
                }
        }
        ConfigureFlash();
        foreach (var root in scene.GetRootGameObjects())
            foreach (var component in root.GetComponentsInChildren<Component>(true))
                if (component != null && PrefabUtility.IsPartOfPrefabInstance(component))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(component);
        Physics.SyncTransforms();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Inspect();
        Debug.Log($"Woodland polish complete: {obstacles.Length} obstacles aligned; banana flash configured.");
    }

    private static GameObject AddModel(string path, Transform parent)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(EnvironmentPath + path);
        if (asset == null) throw new System.IO.FileNotFoundException(path);
        var model = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
        Undo.RegisterCreatedObjectUndo(model, "Add woodland detail");
        foreach (var collider in model.GetComponentsInChildren<Collider>()) collider.enabled = false;
        return model;
    }

    private static Bounds VisibleBounds(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>().Where(r => r.enabled).ToArray();
        var bounds = renderers[0].bounds;
        foreach (var r in renderers) bounds.Encapsulate(r.bounds);
        return bounds;
    }

    private static void Fit(GameObject model, Vector3 size, Vector3 bottom)
    {
        // Fit world-aligned bounds before translating; meshes have imported, off-center pivots.
        var bounds = VisibleBounds(model);
        model.transform.localScale = Vector3.Scale(model.transform.localScale,
            new Vector3(size.x / bounds.size.x, size.y / bounds.size.y, size.z / bounds.size.z));
        bounds = VisibleBounds(model);
        model.transform.position += new Vector3(bottom.x - bounds.center.x, bottom.y - bounds.min.y, bottom.z - bounds.center.z);
    }

    [MenuItem("Tools/Banana Run/Configure Pickup Flash")]
    public static void ConfigureFlash()
    {
        var shader = Shader.Find("BananaRun/PickupFlash");
        if (shader == null) throw new System.InvalidOperationException("Pickup flash shader has not imported.");
        const string materialPath = "Assets/_Game/Materials/M_PickupGlow.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, materialPath);
        }
        material.SetFloat("_StarStrength", 0.7f);
        const string effectPath = "Assets/_Game/Prefabs/Effects/FX_BananaPickup.prefab";
        var root = PrefabUtility.LoadPrefabContents(effectPath);
        try
        {
            var flash = root.GetComponent<ParticleSystem>();
            SetupParticles(flash, material, 1, 0.24f, 0.24f, 1.8f, 1.8f, 0, 0);
            var size = flash.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(new Keyframe(0, 0.35f), new Keyframe(0.2f, 1), new Keyframe(1, 1.4f)));
            var sparksTransform = root.transform.Find("Gold Sparkles");
            var sparks = sparksTransform != null ? sparksTransform.GetComponent<ParticleSystem>() : new GameObject("Gold Sparkles").AddComponent<ParticleSystem>();
            sparks.transform.SetParent(root.transform, false);
            SetupParticles(sparks, material, 14, 0.35f, 0.6f, 0.12f, 0.24f, 1.1f, 2.6f);
            var sparksMain = sparks.main;
            sparksMain.gravityModifier = 0.18f;
            sparksMain.stopAction = ParticleSystemStopAction.None;
            var shape = sparks.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.15f;
            var sparkSize = sparks.sizeOverLifetime;
            sparkSize.enabled = true;
            sparkSize.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, 1, 1, 0));
            PrefabUtility.SaveAsPrefabAsset(root, effectPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
    }

    private static void SetupParticles(ParticleSystem ps, Material material, short count, float lifeMin, float lifeMax, float sizeMin, float sizeMax, float speedMin, float speedMax)
    {
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.duration = 0.75f;
        main.loop = false;
        main.prewarm = false;
        main.playOnAwake = false;
        main.startDelay = 0;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, lifeMax);
        main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speedMin, speedMax);
        main.startColor = new Color(1, 0.8f, 0.25f, 1);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Shape;
        main.maxParticles = 32;
        main.stopAction = ParticleSystemStopAction.Destroy;
        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0;
        emission.rateOverDistance = 0;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0, count) });
        var shape = ps.shape;
        shape.enabled = false;
        var color = ps.colorOverLifetime;
        color.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(new Color(1, 1, 0.8f), 0), new GradientColorKey(new Color(1, 0.65f, 0.08f), 1) },
            new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0.9f, 0.15f), new GradientAlphaKey(0, 1) });
        color.color = gradient;
        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.maxParticleSize = 0.5f;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    [MenuItem("Tools/Banana Run/Inspect Trail")]
    public static void Inspect()
    {
        Physics.SyncTransforms();
        var lines = new System.Collections.Generic.List<string>();
        lines.Add("Scene: " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().path);
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
        {
            if (t.GetComponent<RunnerObstacle>() == null && t.GetComponent<BananaPickup>() == null &&
                t.GetComponent<RunnerMotor>() == null && t.name != "Ground" && t.name != "FinishGate") continue;
            var box = t.GetComponent<BoxCollider>();
            var renderers = t.GetComponentsInChildren<Renderer>().Where(r => r.enabled).ToArray();
            var bounds = renderers.Length > 0 ? renderers[0].bounds : new Bounds();
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            lines.Add($"{t.name}: pos={t.position} scale={t.lossyScale} visible={bounds} collider={(box != null ? box.bounds.ToString() : "none")} prefab={PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject)}");
        }
        File.WriteAllLines("Temp/trail-inspection.txt", lines);
        Debug.Log("Trail inspection saved.");
    }
}
