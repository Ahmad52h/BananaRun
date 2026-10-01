using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class NewAssetIntegrationSetup
{
    const string Root = "Assets/_Game/";

    [MenuItem("Tools/Banana Run/Integrate Slide Throw Smash and Models")]
    public static void Install()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (EditorApplication.isPlaying || scene.path != Root + "Scenes/Level01.unity")
            throw new InvalidOperationException("Open Level01 in edit mode.");

        var slide = ImportHumanoid(Root + "Animations/Monkey@Running Slide.fbx", "Running Slide", true);
        var throwClip = ImportHumanoid(Root + "Animations/Throw.fbx", "mixamo.com", false);
        var magnetImporter = AssetImporter.GetAtPath(Root + "Art/Magnet.obj") as ModelImporter;
        if (magnetImporter != null && magnetImporter.importNormals != ModelImporterNormals.Calculate)
        {
            magnetImporter.importNormals = ModelImporterNormals.Calculate;
            magnetImporter.SaveAndReimport();
        }
        ConfigureAnimator(slide, throwClip);
        PowerSystemsSceneSetup.Install();
        InstallSlideObstacle();

        var player = Object.FindFirstObjectByType<RunnerMotor>();
        var thrower = player.GetComponent<CoconutThrower>();
        var throwerData = new SerializedObject(thrower);
        throwerData.FindProperty("animator").objectReferenceValue = player.GetComponentInChildren<Animator>();
        throwerData.FindProperty("throwTrigger").stringValue = "Throw";
        throwerData.FindProperty("maxCoconuts").intValue = 5;
        throwerData.ApplyModifiedPropertiesWithoutUndo();

        string audioPath = AssetDatabase.FindAssets("t:AudioClip", new[] { Root + "Audio" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .FirstOrDefault(path => Path.GetFileName(path).StartsWith("SMASH BOX", StringComparison.OrdinalIgnoreCase));
        var smash = AssetDatabase.LoadAssetAtPath<AudioClip>(audioPath);
        if (smash == null) throw new InvalidOperationException("SMASH BOX sound clip could not be imported.");
        const string projectilePath = Root + "Prefabs/CoconutProjectile.prefab";
        var projectileRoot = PrefabUtility.LoadPrefabContents(projectilePath);
        try
        {
            var data = new SerializedObject(projectileRoot.GetComponent<CoconutProjectile>());
            data.FindProperty("smashBoxSound").objectReferenceValue = smash;
            data.FindProperty("smashVolume").floatValue = 1f;
            data.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(projectileRoot, projectilePath);
        }
        finally { PrefabUtility.UnloadPrefabContents(projectileRoot); }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("Imported Running Slide and Throw animations, smash audio, and supplied banana/magnet models are integrated.");
    }

    static void InstallSlideObstacle()
    {
        const string path = Root + "Prefabs/Course/HighSlideBeam.prefab";
        var root = new GameObject("HighSlideBeam");
        try
        {
            var wood = AssetDatabase.LoadAssetAtPath<Material>(Root + "Materials/Tropical/WarmBark.mat");
            void AddWood(string name, Vector3 position, Vector3 size)
            {
                var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
                visual.name = name;
                visual.transform.SetParent(root.transform, false);
                visual.transform.localPosition = position;
                visual.transform.localScale = size;
                Object.DestroyImmediate(visual.GetComponent<Collider>());
                if (wood != null) visual.GetComponent<Renderer>().sharedMaterial = wood;
            }
            AddWood("Raised Wooden Beam", new Vector3(0, 1.45f, 0), new Vector3(2.7f, 0.7f, 0.85f));
            AddWood("Left Timber Support", new Vector3(-1.4f, 0.7f, 0), new Vector3(0.25f, 1.4f, 0.4f));
            AddWood("Right Timber Support", new Vector3(1.4f, 0.7f, 0), new Vector3(0.25f, 1.4f, 0.4f));
            var box = root.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = new Vector3(0, 1.45f, 0);
            box.size = new Vector3(2.2f, 1f, 0.85f);
            var body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            var obstacle = root.AddComponent<RunnerObstacle>();
            var data = new SerializedObject(obstacle);
            data.FindProperty("breakEffectPrefab").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<CrateBreakEffect>(Root + "Prefabs/Effects/CrateBreakFX.prefab");
            data.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { Object.DestroyImmediate(root); }

        var course = Object.FindFirstObjectByType<RunnerCourseGenerator>();
        var serialized = new SerializedObject(course);
        var palette = serialized.FindProperty("obstaclePrefabs");
        var beam = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        bool present = false;
        for (int i = 0; i < palette.arraySize; i++)
            if (palette.GetArrayElementAtIndex(i).objectReferenceValue == beam) present = true;
        if (!present)
        {
            int index = palette.arraySize;
            palette.arraySize++;
            palette.GetArrayElementAtIndex(index).objectReferenceValue = beam;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    static AnimationClip ImportHumanoid(string path, string clipName, bool lockRoot)
    {
        var importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer == null) throw new InvalidOperationException("Missing imported animation: " + path);
        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.importNormals = ModelImporterNormals.Calculate;
        var clips = importer.clipAnimations;
        if (clips.Length == 0) clips = importer.defaultClipAnimations;
        foreach (var clip in clips)
        {
            clip.lockRootPositionXZ = lockRoot;
            clip.keepOriginalPositionXZ = !lockRoot;
            clip.loopTime = false;
        }
        importer.clipAnimations = clips;
        importer.SaveAndReimport();
        var animation = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
            .FirstOrDefault(clip => clip.name == clipName && !clip.name.StartsWith("__"));
        if (animation == null || !animation.humanMotion)
            throw new InvalidOperationException("Animation is not a valid Humanoid clip: " + path);
        return animation;
    }

    static void ConfigureAnimator(AnimationClip slide, AnimationClip throwClip)
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Root + "Animations/AC_Monkey.controller");
        if (controller == null) throw new InvalidOperationException("AC_Monkey.controller is missing.");
        var baseMachine = controller.layers[0].stateMachine;
        var slideState = baseMachine.states.Select(s => s.state).FirstOrDefault(s => s.name == "Slide")
            ?? baseMachine.AddState("Slide", new Vector3(565, 300));
        slideState.motion = slide;
        slideState.speed = Mathf.Max(1f, slide.length / 0.75f);
        slideState.writeDefaultValues = true;
        if (!controller.parameters.Any(p => p.name == "Throw"))
            controller.AddParameter("Throw", AnimatorControllerParameterType.Trigger);

        const string maskPath = Root + "Animations/ThrowUpperBody.mask";
        var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(maskPath);
        if (mask == null)
        {
            mask = new AvatarMask();
            AssetDatabase.CreateAsset(mask, maskPath);
        }
        for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
            mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);
        foreach (var part in new[] { AvatarMaskBodyPart.Body, AvatarMaskBodyPart.Head,
                     AvatarMaskBodyPart.LeftArm, AvatarMaskBodyPart.RightArm,
                     AvatarMaskBodyPart.LeftFingers, AvatarMaskBodyPart.RightFingers })
            mask.SetHumanoidBodyPartActive(part, true);
        EditorUtility.SetDirty(mask);

        if (!controller.layers.Any(l => l.name == "Throw Overlay")) controller.AddLayer("Throw Overlay");
        var layers = controller.layers;
        int index = Array.FindIndex(layers, l => l.name == "Throw Overlay");
        var layer = layers[index];
        layer.defaultWeight = 1f;
        layer.blendingMode = AnimatorLayerBlendingMode.Override;
        layer.avatarMask = mask;
        layers[index] = layer;
        controller.layers = layers;
        var machine = controller.layers[index].stateMachine;
        var empty = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == "Empty")
            ?? machine.AddState("Empty", new Vector3(80, 70));
        empty.motion = null;
        empty.writeDefaultValues = false;
        machine.defaultState = empty;
        var throwing = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == "Throw")
            ?? machine.AddState("Throw", new Vector3(330, 70));
        throwing.motion = throwClip;
        throwing.speed = Mathf.Max(1f, throwClip.length / 0.75f);
        throwing.writeDefaultValues = false;
        if (!machine.anyStateTransitions.Any(t => t.destinationState == throwing))
        {
            var enter = machine.AddAnyStateTransition(throwing);
            enter.hasExitTime = false;
            enter.duration = 0.04f;
            enter.canTransitionToSelf = false;
            enter.AddCondition(AnimatorConditionMode.If, 0, "Throw");
        }
        if (!throwing.transitions.Any(t => t.destinationState == empty))
        {
            var exit = throwing.AddTransition(empty);
            exit.hasExitTime = true;
            exit.exitTime = 0.93f;
            exit.duration = 0.08f;
        }
        EditorUtility.SetDirty(slideState);
        EditorUtility.SetDirty(throwing);
        EditorUtility.SetDirty(controller);
    }
}
