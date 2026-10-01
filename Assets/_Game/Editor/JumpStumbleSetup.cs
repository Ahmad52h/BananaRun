using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class JumpStumbleSetup
{
    const string Root = "Assets/_Game/";

    [MenuItem("Tools/Banana Run/Configure Jump and Stumble")]
    public static void Install()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (EditorApplication.isPlaying || scene.path != Root + "Scenes/Level01.unity")
            throw new InvalidOperationException("Open Level01 in edit mode.");
        var flip = ConfigureClip("Front-Flip.fbx", false);
        var stumble = ConfigureClip("Stumble.fbx", true);
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Root + "Animations/AC_Monkey.controller");
        var states = controller.layers[0].stateMachine.states.Select(s => s.state).ToArray();
        var jump = states.First(s => s.name == "Jump");
        jump.motion = flip;
        jump.speed = 1;
        jump.speedParameter = "JumpSpeed";
        jump.speedParameterActive = true;
        if (!controller.parameters.Any(p => p.name == "JumpSpeed")) controller.AddParameter("JumpSpeed", AnimatorControllerParameterType.Float);
        var parameters = controller.parameters;
        foreach (var p in parameters) if (p.name == "JumpSpeed") p.defaultFloat = 1;
        controller.parameters = parameters;
        var hit = states.First(s => s.name == "Hit");
        hit.motion = stumble;
        hit.speed = 1;
        hit.speedParameter = "StumbleSpeed";
        hit.speedParameterActive = true;
        EditorUtility.SetDirty(controller);
        var motor = UnityEngine.Object.FindAnyObjectByType<RunnerMotor>();
        var data = new SerializedObject(motor);
        data.FindProperty("jumpHeight").floatValue = 2.3f;
        data.FindProperty("gravity").floatValue = -21;
        data.ApplyModifiedPropertiesWithoutUndo();
        data = new SerializedObject(motor.GetComponent<RunnerAnimation>());
        data.FindProperty("jumpClipLength").floatValue = flip.length;
        data.FindProperty("stumbleClipLength").floatValue = stumble.length;
        data.FindProperty("landBlendDuration").floatValue = 0.15f;
        ConfigureSoles(motor.GetComponentInChildren<Animator>(), data.FindProperty("soleContacts"));
        data.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log($"Jump and stumble configured: flip {flip.length:F3}s, flight {motor.JumpDuration:F3}s, stumble {stumble.length:F3}s.");
    }

    static void ConfigureSoles(Animator animator, SerializedProperty contacts)
    {
        var bones = new[] { HumanBodyBones.LeftFoot, HumanBodyBones.LeftToes, HumanBodyBones.RightFoot, HumanBodyBones.RightToes };
        contacts.arraySize = bones.Length;
        for (int b = 0; b < bones.Length; b++)
        {
            var bone = animator.GetBoneTransform(bones[b]);
            var points = new System.Collections.Generic.List<Vector3>();
            foreach (var skin in animator.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                int index = Array.IndexOf(skin.bones, bone);
                if (index < 0) continue;
                var mesh = skin.sharedMesh;
                var vertices = mesh.vertices;
                var weights = mesh.boneWeights;
                var bind = mesh.bindposes[index];
                for (int i = 0; i < vertices.Length; i++)
                    if (weights[i].boneIndex0 == index && weights[i].weight0 >= 0.5f)
                        points.Add(bind.MultiplyPoint3x4(vertices[i]));
            }
            var contact = contacts.GetArrayElementAtIndex(b);
            contact.FindPropertyRelative("bone").objectReferenceValue = bone;
            var samples = contact.FindPropertyRelative("points");
            samples.arraySize = points.Count;
            for (int i = 0; i < points.Count; i++) samples.GetArrayElementAtIndex(i).vector3Value = points[i];
        }
    }

    static AnimationClip ConfigureClip(string file, bool feet)
    {
        string path = Root + "Animations/" + file;
        var importer = (ModelImporter)AssetImporter.GetAtPath(path);
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var avatar = AssetDatabase.LoadAllAssetsAtPath(Root + "Art/Monkey/Monkey.fbx").OfType<Avatar>().First();
        var description = avatar.humanDescription;
        description.skeleton = source.GetComponentsInChildren<Transform>(true).Select(t => new SkeletonBone
        {
            name = t.name, position = t.localPosition, rotation = t.localRotation, scale = t.localScale
        }).ToArray();
        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.sourceAvatar = null;
        importer.humanDescription = description;
        importer.importNormals = ModelImporterNormals.Calculate;
        var clips = importer.clipAnimations.Length > 0 ? importer.clipAnimations : importer.defaultClipAnimations;
        foreach (var clip in clips)
        {
            // The source continues into a seated backward fall. Keep the standing
            // recoil for a runner hit, then let the controller blend back to Run.
            if (feet) clip.lastFrame = Mathf.Min(clip.lastFrame, clip.firstFrame + 48);
            clip.loopTime = false;
            clip.lockRootRotation = true;
            clip.keepOriginalOrientation = true;
            clip.lockRootPositionXZ = false;
            clip.keepOriginalPositionXZ = false;
            // The motor supplies the jump arc; only stumble bakes vertical pose.
            clip.lockRootHeightY = feet;
            clip.heightFromFeet = feet;
            clip.keepOriginalPositionY = !feet;
        }
        importer.clipAnimations = clips;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c => !c.name.StartsWith("__"));
    }
}
