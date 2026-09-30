using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Object = UnityEngine.Object;

public static class GameplayFeatureSetup
{
    const string Root = "Assets/_Game/";
    const string Nature = "Assets/Polytope Studio/Lowpoly_Environments/Prefabs/";

    [MenuItem("Tools/Banana Run/Install Extended Gameplay")]
    public static void Install()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (EditorApplication.isPlaying || scene.path != Root + "Scenes/Level01.unity")
            throw new InvalidOperationException("Open Level01 in edit mode.");
        AssetDatabase.Refresh();
        var player = Object.FindFirstObjectByType<RunnerMotor>();
        var gate = Object.FindFirstObjectByType<FinishGate>();
        Undo.RegisterFullObjectHierarchyUndo(gate.transform.parent.gameObject, "Extend finish line");
        gate.transform.parent.position += Vector3.forward * (900 - gate.transform.position.z);
        var ground = GameObject.Find("Ground").transform;
        Undo.RecordObject(ground, "Extend running surface");
        ground.position = new Vector3(0, -0.8f, 440);
        ground.localScale = new Vector3(10, 1, 1000);

        Directory.CreateDirectory(Root + "Prefabs/Course");
        AssetDatabase.Refresh();
        var palette = new[] {
            MakeObstacle("LogHurdle", Nature + "Trees/PT_Fruit_Tree_01_logs.prefab", new Vector3(2.05f, 0.8f, 1.15f)),
            MakeObstacle("RuneStone", Nature + "Rocks/PT_Menhir_Rock_02.prefab", new Vector3(1.85f, 2.9f, 1.5f)),
            MakeObstacle("Boulder", Nature + "Rocks/PT_Generic_Rock_01.prefab", new Vector3(2.05f, 2.3f, 1.8f)),
            MakeObstacle("TreeStump", Nature + "Trees/PT_Fruit_Tree_01_stump.prefab", new Vector3(1.85f, 1.05f, 1.5f)),
            MakeObstacle("FenceHurdle", "Assets/Polytope Studio/Lowpoly_Village/Prefabs/Modular/Fence/PT_Modular_Fence_Wood_01.prefab", new Vector3(2.15f, 0.95f, 0.7f)),
            MakeCrate()
        };
        var courseObject = GameObject.Find("Extended Course");
        if (courseObject == null) courseObject = new GameObject("Extended Course");
        var course = GetOrAdd<RunnerCourseGenerator>(courseObject);
        var data = new SerializedObject(course);
        data.FindProperty("player").objectReferenceValue = player;
        data.FindProperty("finishLine").objectReferenceValue = gate.transform;
        SetArray(data.FindProperty("obstaclePrefabs"), palette);
        data.FindProperty("bananaPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Prefabs/Banana.prefab");
        data.FindProperty("coconutPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Prefabs/CoconutPickup.prefab");
        SetArray(data.FindProperty("sceneryPrefabs"), new[] {
            AssetDatabase.LoadAssetAtPath<GameObject>(Nature + "Trees/PT_Fruit_Tree_01_green.prefab"),
            AssetDatabase.LoadAssetAtPath<GameObject>(Nature + "Trees/PT_Pine_Tree_03_green.prefab")
        });
        data.FindProperty("roadsideShrubPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(Nature + "Shrubs/PT_Generic_Shrub_01_green.prefab");
        data.FindProperty("scenerySpacing").floatValue = 10;
        data.ApplyModifiedPropertiesWithoutUndo();
        CreateBanks();
        ConfigureAnimation(player);
        ConfigureAudio();
        ConfigureProgress(player, gate.transform);

        // Level01 must be reloadable in a built player as well as in the Editor.
        var scenes = EditorBuildSettings.scenes.ToList();
        var entry = scenes.FirstOrDefault(s => s.path == scene.path);
        if (entry == null) scenes.Insert(0, new EditorBuildSettingsScene(scene.path, true));
        else entry.enabled = true;
        EditorBuildSettings.scenes = scenes.ToArray();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("Extended gameplay installed: 900m course, six obstacle types, progress HUD, Hit state and audio.");
    }

    static T GetOrAdd<T>(GameObject go) where T : Component => go.GetComponent<T>() ?? go.AddComponent<T>();

    static void SetArray(SerializedProperty property, GameObject[] values)
    {
        property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    static Bounds BoundsOf(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>().Where(r => r.enabled).ToArray();
        var bounds = renderers[0].bounds;
        foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
        return bounds;
    }

    static GameObject MakeObstacle(string name, string modelPath, Vector3 size)
    {
        var root = new GameObject(name);
        try
        {
            var model = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath), root.transform);
            model.name = name + " Visual";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = name == "RuneStone" ? Quaternion.Euler(0, 180, 0) : Quaternion.identity;
            foreach (var collider in model.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(collider);
            var bounds = BoundsOf(model);
            model.transform.localScale = Vector3.Scale(model.transform.localScale, new Vector3(size.x / bounds.size.x, size.y / bounds.size.y, size.z / bounds.size.z));
            bounds = BoundsOf(model);
            model.transform.position -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            var box = root.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = new Vector3(0, size.y / 2, 0);
            box.size = size;
            var body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            root.AddComponent<RunnerObstacle>();
            return PrefabUtility.SaveAsPrefabAsset(root, Root + "Prefabs/Course/" + name + ".prefab");
        }
        finally { Object.DestroyImmediate(root); }
    }

    static GameObject MakeCrate()
    {
        var root = new GameObject("RewardCrate");
        try
        {
            var source = Object.FindObjectsByType<RunnerObstacle>(FindObjectsSortMode.None)
                .First(o => new SerializedObject(o).FindProperty("breakable").boolValue);
            var crate = Object.Instantiate(source.gameObject, root.transform);
            crate.name = "Breakable Crate";
            crate.transform.position = Vector3.zero;
            var bounds = BoundsOf(crate);
            crate.transform.position -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            var data = new SerializedObject(crate.GetComponent<RunnerObstacle>());
            ((Transform)data.FindProperty("bananaSpawnPoint").objectReferenceValue).position = new Vector3(0, 2.5f, 0);
            ((Transform)data.FindProperty("bananaLandingPoint").objectReferenceValue).position = new Vector3(0, 2.02f, 4);
            return PrefabUtility.SaveAsPrefabAsset(root, Root + "Prefabs/Course/RewardCrate.prefab");
        }
        finally { Object.DestroyImmediate(root); }
    }

    static void CreateBanks()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(Root + "Materials/M_ExtendedGrass.mat");
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.color = new Color(0.13f, 0.33f, 0.09f);
            material.SetFloat("_Smoothness", 0);
            AssetDatabase.CreateAsset(material, Root + "Materials/M_ExtendedGrass.mat");
        }
        for (int side = -1; side <= 1; side += 2)
        {
            string name = side < 0 ? "Extended Left Bank" : "Extended Right Bank";
            var bank = GameObject.Find(name) ?? GameObject.CreatePrimitive(PrimitiveType.Cube);
            bank.name = name;
            bank.transform.position = new Vector3(side * 22.5f, -0.85f, 585);
            bank.transform.localScale = new Vector3(35, 1, 710);
            bank.GetComponent<Renderer>().sharedMaterial = material;
        }
    }

    static void ConfigureAnimation(RunnerMotor player)
    {
        var animator = player.GetComponentInChildren<Animator>();
        var controller = animator.runtimeAnimatorController as AnimatorController;
        if (controller == null) throw new InvalidOperationException("Player needs an editable AnimatorController.");
        const string clipPath = Root + "Animations/MonkeyStumble.anim";
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        if (clip == null) { clip = new AnimationClip { name = "MonkeyStumble", frameRate = 60 }; AssetDatabase.CreateAsset(clip, clipPath); }
        var runMotion = controller.layers[0].stateMachine.states.Select(s => s.state).First(s => s.name == "Run").motion as AnimationClip;
        if (runMotion != null) EditorUtility.CopySerialized(runMotion, clip);
        clip.name = "MonkeyStumble";
        // A keyed humanoid recoil pose: lean forward, brace the arms, then recover.
        string[] muscles = { "Spine Front-Back", "Chest Front-Back", "Head Nod Down-Up", "Left Arm Down-Up", "Right Arm Down-Up", "Left Forearm Stretch", "Right Forearm Stretch" };
        float[] amplitudes = { 0.6f, 0.45f, -0.35f, -0.45f, -0.45f, -0.65f, -0.65f };
        for (int i = 0; i < muscles.Length; i++)
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), muscles[i]),
                new AnimationCurve(new Keyframe(0, 0), new Keyframe(0.1f, amplitudes[i]), new Keyframe(0.28f, amplitudes[i] * 0.6f), new Keyframe(0.55f, 0)));
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = false;
        settings.startTime = 0;
        settings.stopTime = 0.55f;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        if (!controller.parameters.Any(p => p.name == "Hit")) controller.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
        var machine = controller.layers[0].stateMachine;
        var state = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == "Hit") ?? machine.AddState("Hit", new Vector3(540, 180));
        state.motion = clip;
        state.writeDefaultValues = true;
        if (!machine.anyStateTransitions.Any(t => t.destinationState == state))
        {
            var transition = machine.AddAnyStateTransition(state);
            transition.hasExitTime = false;
            transition.duration = 0.04f;
            transition.canTransitionToSelf = false;
            transition.AddCondition(AnimatorConditionMode.If, 0, "Hit");
        }
        var run = machine.states.Select(s => s.state).First(s => s.name == "Run");
        if (!state.transitions.Any(t => t.destinationState == run))
        {
            var transition = state.AddTransition(run);
            transition.hasExitTime = true;
            transition.exitTime = 1;
            transition.duration = 0.08f;
        }
        EditorUtility.SetDirty(clip);
        EditorUtility.SetDirty(controller);
    }

    static void ConfigureAudio()
    {
        var go = GameObject.Find("Gameplay Audio") ?? new GameObject("Gameplay Audio");
        var manager = GetOrAdd<RunnerAudio>(go);
        var data = new SerializedObject(manager);
        var sources = go.GetComponents<AudioSource>();
        while (sources.Length < 2) { go.AddComponent<AudioSource>(); sources = go.GetComponents<AudioSource>(); }
        foreach (var source in sources) { source.playOnAwake = false; source.spatialBlend = 0; }
        data.FindProperty("musicSource").objectReferenceValue = sources[0];
        data.FindProperty("effectsSource").objectReferenceValue = sources[1];
        data.FindProperty("backgroundMusic").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(Root + "Audio/WoodlandLoop.wav");
        data.FindProperty("bananaCollect").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(Root + "Audio/BananaCollect.wav");
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 position)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        return rect;
    }

    static Image Image(string name, Transform parent, Vector2 size, Vector2 position, Color color, Sprite sprite = null)
    {
        var image = Rect(name, parent, size, position).gameObject.AddComponent<Image>();
        image.color = color;
        image.sprite = sprite;
        image.raycastTarget = false;
        return image;
    }

    static Sprite CircleSprite()
    {
        const string path = Root + "UI/Icons/ProgressCircle.png";
        if (!File.Exists(path))
        {
            var texture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
            {
                float alpha = Mathf.Clamp01(31.5f - Vector2.Distance(new Vector2(x, y), new Vector2(31.5f, 31.5f)));
                texture.SetPixel(x, y, new Color(1, 1, 1, alpha));
            }
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static void ConfigureProgress(RunnerMotor player, Transform finish)
    {
        var safeArea = GameObject.Find("SafeArea").transform;
        var existing = safeArea.Find("Level Progress");
        if (existing != null) Object.DestroyImmediate(existing.gameObject);
        var panel = Rect("Level Progress", safeArea, new Vector2(0, 115), new Vector2(0, -150));
        panel.anchorMin = new Vector2(0.18f, 1);
        panel.anchorMax = new Vector2(0.82f, 1);
        panel.pivot = new Vector2(0.5f, 1);
        var backdrop = panel.gameObject.AddComponent<Image>();
        backdrop.color = new Color(0.055f, 0.12f, 0.07f, 0.87f);
        backdrop.raycastTarget = false;
        var bar = Rect("Track", panel, new Vector2(-90, 16), new Vector2(0, 8));
        bar.anchorMin = new Vector2(0, 0.5f);
        bar.anchorMax = new Vector2(1, 0.5f);
        var track = bar.gameObject.AddComponent<Image>();
        track.color = new Color(0.24f, 0.33f, 0.2f);
        track.raycastTarget = false;
        var fill = Image("Progress Fill", bar, Vector2.zero, Vector2.zero, new Color(1, 0.78f, 0.14f));
        fill.rectTransform.anchorMin = Vector2.zero;
        fill.rectTransform.anchorMax = Vector2.one;
        fill.type = UnityEngine.UI.Image.Type.Simple;
        fill.rectTransform.anchorMax = new Vector2(0, 1);
        var marker = Rect("Monkey Marker", bar, new Vector2(62, 62), Vector2.zero);
        marker.anchorMin = marker.anchorMax = new Vector2(0, 0.5f);
        var circle = CircleSprite();
        Color brown = new Color(0.34f, 0.17f, 0.075f), tan = new Color(0.96f, 0.7f, 0.36f);
        Image("Marker Halo", marker, new Vector2(66, 66), Vector2.zero, new Color(1, 0.84f, 0.3f), circle);
        Image("Left Ear", marker, new Vector2(24, 27), new Vector2(-25, 0), brown, circle);
        Image("Right Ear", marker, new Vector2(24, 27), new Vector2(25, 0), brown, circle);
        Image("Head", marker, new Vector2(53, 57), Vector2.zero, brown, circle);
        Image("Left Face", marker, new Vector2(29, 36), new Vector2(-10, -1), tan, circle);
        Image("Right Face", marker, new Vector2(29, 36), new Vector2(10, -1), tan, circle);
        Image("Muzzle", marker, new Vector2(34, 24), new Vector2(0, -14), tan, circle);
        Image("Left Eye", marker, new Vector2(6, 9), new Vector2(-10, 3), brown, circle);
        Image("Right Eye", marker, new Vector2(6, 9), new Vector2(10, 3), brown, circle);
        Image("Smile", marker, new Vector2(14, 6), new Vector2(0, -15), brown, circle);
        var flag = Rect("Finish Flag", bar, new Vector2(48, 54), Vector2.zero);
        flag.anchorMin = flag.anchorMax = new Vector2(1, 0.5f);
        Image("Pole", flag, new Vector2(4, 52), new Vector2(-17, 1), Color.white);
        for (int y = 0; y < 3; y++) for (int x = 0; x < 4; x++)
            Image("Checker", flag, new Vector2(9, 9), new Vector2(-10 + x * 9, 20 - y * 9), (x + y) % 2 == 0 ? Color.white : new Color(0.07f, 0.1f, 0.08f));
        var label = Rect("Distance", panel, new Vector2(400, 30), new Vector2(0, -34)).gameObject.AddComponent<TextMeshProUGUI>();
        label.text = "0 / 900 m";
        label.fontSize = 23;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(1, 0.93f, 0.72f);
        label.raycastTarget = false;
        var hud = panel.gameObject.AddComponent<LevelProgressHUD>();
        var data = new SerializedObject(hud);
        data.FindProperty("player").objectReferenceValue = player.transform;
        data.FindProperty("finishLine").objectReferenceValue = finish;
        data.FindProperty("progressFill").objectReferenceValue = fill;
        data.FindProperty("monkeyMarker").objectReferenceValue = marker;
        data.FindProperty("distanceLabel").objectReferenceValue = label;
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    [MenuItem("Tools/Banana Run/Inspect Gameplay Setup")]
    public static void Inspect()
    {
        var lines = new System.Collections.Generic.List<string>();
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
        {
            if (t.parent == null || t.name.Contains("Finish") || t is RectTransform || t.GetComponent<Animator>() != null)
                lines.Add($"{t.name} parent={t.parent?.name} pos={t.position} scale={t.lossyScale} children={t.childCount}" +
                    (t is RectTransform r ? $" rect={r.rect} anchors={r.anchorMin}/{r.anchorMax} anchored={r.anchoredPosition}" : ""));
        }
        var animator = Object.FindFirstObjectByType<RunnerAnimation>().GetComponentInChildren<Animator>();
        lines.Add("Animator " + animator.name + " humanoid=" + animator.isHuman + " controller=" + AssetDatabase.GetAssetPath(animator.runtimeAnimatorController));
        File.WriteAllLines("Temp/gameplay-setup.txt", lines);
    }
}
