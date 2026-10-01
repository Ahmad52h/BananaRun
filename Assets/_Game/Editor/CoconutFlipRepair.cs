using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class CoconutFlipRepair
{
    const string Root = "Assets/_Game/";

    [MenuItem("Tools/Banana Run/Repair Coconut Pickups and Front Flip")]
    public static void Apply()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (EditorApplication.isPlaying || scene.path != Root + "Scenes/Level01.unity")
            throw new InvalidOperationException("Open Level01 in edit mode.");

        const string coconutPath = Root + "Prefabs/CoconutPickup.prefab";
        var coconutRoot = PrefabUtility.LoadPrefabContents(coconutPath);
        try
        {
            if (coconutRoot.GetComponent<CoconutPickup>() == null)
                coconutRoot.AddComponent<CoconutPickup>();
            coconutRoot.GetComponent<SphereCollider>().isTrigger = true;
            var body = coconutRoot.GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            PrefabUtility.SaveAsPrefabAsset(coconutRoot, coconutPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(coconutRoot); }

        var coconut = AssetDatabase.LoadAssetAtPath<CoconutPickup>(coconutPath);
        if (coconut == null) throw new InvalidOperationException("CoconutPickup must be on the prefab root.");
        var player = Object.FindFirstObjectByType<RunnerMotor>();
        player.gameObject.tag = "Player";
        foreach (var pickup in Object.FindObjectsByType<CoconutPickup>(FindObjectsSortMode.None))
        {
            pickup.GetComponent<SphereCollider>().isTrigger = true;
            var body = pickup.GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            PrefabUtility.RecordPrefabInstancePropertyModifications(pickup.gameObject);
        }
        foreach (var crate in Object.FindObjectsByType<RunnerObstacle>(FindObjectsSortMode.None))
        {
            var so = new SerializedObject(crate);
            if (!so.FindProperty("breakable").boolValue) continue;
            so.FindProperty("coconutPrefab").objectReferenceValue = coconut;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.RecordPrefabInstancePropertyModifications(crate);
        }
        const string cratePath = Root + "Prefabs/Course/RewardCrate.prefab";
        var crateRoot = PrefabUtility.LoadPrefabContents(cratePath);
        try
        {
            foreach (var obstacle in crateRoot.GetComponentsInChildren<RunnerObstacle>())
            {
                var so = new SerializedObject(obstacle);
                so.FindProperty("coconutPrefab").objectReferenceValue = coconut;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            PrefabUtility.SaveAsPrefabAsset(crateRoot, cratePath);
        }
        finally { PrefabUtility.UnloadPrefabContents(crateRoot); }

        const string flipPath = Root + "Animations/Front-Flip.fbx";
        var importer = AssetImporter.GetAtPath(flipPath) as ModelImporter;
        if (importer == null) throw new InvalidOperationException("Front-Flip.fbx is missing.");
        var clips = importer.clipAnimations;
        if (clips.Length == 0) clips = importer.defaultClipAnimations;
        foreach (var clip in clips)
        {
            clip.lockRootPositionXZ = true;
            clip.keepOriginalPositionXZ = false;
            clip.loopTime = false;
        }
        importer.clipAnimations = clips;
        importer.SaveAndReimport();
        var frontFlip = AssetDatabase.LoadAllAssetsAtPath(flipPath)
            .OfType<AnimationClip>().First(c => c.name == "mixamo.com");
        const string inplacePath = Root + "Animations/FrontFlip_InPlace.anim";
        var inplace = AssetDatabase.LoadAssetAtPath<AnimationClip>(inplacePath);
        if (inplace == null)
        {
            inplace = new AnimationClip();
            AssetDatabase.CreateAsset(inplace, inplacePath);
        }
        EditorUtility.CopySerialized(frontFlip, inplace);
        inplace.name = "FrontFlip_InPlace";
        // Humanoid root translation is separate from the rotating body pose.
        foreach (var binding in AnimationUtility.GetCurveBindings(inplace))
            if (binding.propertyName == "RootT.x" || binding.propertyName == "RootT.z")
                AnimationUtility.SetEditorCurve(inplace, binding, AnimationCurve.Constant(0, inplace.length, 0));
        EditorUtility.SetDirty(inplace);

        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Root + "Animations/AC_Monkey.controller");
        var jump = controller.layers[0].stateMachine.states.Select(s => s.state).First(s => s.name == "Jump");
        jump.motion = inplace;
        // 1.667 second source at 1.7x ~= 0.98 second, matching the 2.4m/-20m/s2 jump arc.
        jump.speed = 1.7f;
        EditorUtility.SetDirty(jump);
        EditorUtility.SetDirty(controller);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("Coconut pickups and crate rewards repaired; Front-Flip is in place and timed to the jump arc.");
    }
}
