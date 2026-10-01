using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class SlideHazardSetup
{
    const string Root = "Assets/_Game/";

    [MenuItem("Tools/Banana Run/Configure Slide and Peel")]
    public static void Install()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (EditorApplication.isPlaying || scene.path != Root + "Scenes/Level01.unity")
            throw new InvalidOperationException("Open Level01 in edit mode.");
        var avatar = AssetDatabase.LoadAllAssetsAtPath(Root + "Art/Monkey/Monkey.fbx").OfType<Avatar>().First();
        if (!avatar.isValid || !avatar.isHuman) throw new InvalidOperationException("Invalid MonkeyAvatar.");
        var slide = ImportSlide(avatar);
        var stumble = AssetDatabase.LoadAssetAtPath<AnimationClip>(Root + "Animations/MonkeyStumble.anim");
        if (stumble == null || !stumble.humanMotion) throw new InvalidOperationException("Missing humanoid stumble.");
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Root + "Animations/AC_Monkey.controller");
        var machine = controller.layers[0].stateMachine;
        AnimatorState State(string name) => machine.states.Select(s => s.state).FirstOrDefault(s => s.name == name) ?? machine.AddState(name);
        void Parameter(string name, AnimatorControllerParameterType type)
        {
            if (!controller.parameters.Any(p => p.name == name)) controller.AddParameter(name, type);
        }
        Parameter("Sliding", AnimatorControllerParameterType.Bool);
        Parameter("SlideSpeed", AnimatorControllerParameterType.Float);
        Parameter("StumbleSpeed", AnimatorControllerParameterType.Float);
        Parameter("Hit", AnimatorControllerParameterType.Trigger);
        var parameters = controller.parameters;
        foreach (var p in parameters)
            if (p.name == "SlideSpeed" || p.name == "StumbleSpeed") p.defaultFloat = 1f;
        controller.parameters = parameters;
        var run = State("Run");
        var duck = State("Slide");
        var hit = State("Hit");
        duck.motion = slide;
        duck.speed = 1f;
        duck.speedParameter = "SlideSpeed";
        duck.speedParameterActive = true;
        duck.iKOnFeet = false;
        hit.motion = stumble;
        hit.speed = 1f;
        hit.speedParameter = "StumbleSpeed";
        hit.speedParameterActive = true;
        foreach (var t in run.transitions.Where(t => t.destinationState == duck).ToArray()) run.RemoveTransition(t);
        foreach (var t in duck.transitions.ToArray()) duck.RemoveTransition(t);
        foreach (var t in hit.transitions.ToArray()) hit.RemoveTransition(t);
        foreach (var t in machine.anyStateTransitions.Where(t => t.destinationState == hit).ToArray()) machine.RemoveAnyStateTransition(t);
        var enter = run.AddTransition(duck);
        Transition(enter, 0.08f);
        enter.AddCondition(AnimatorConditionMode.If, 0, "Sliding");
        var leave = duck.AddTransition(run);
        Transition(leave, 0.15f);
        leave.AddCondition(AnimatorConditionMode.IfNot, 0, "Sliding");
        var react = machine.AddAnyStateTransition(hit);
        Transition(react, 0.05f);
        react.canTransitionToSelf = true;
        react.AddCondition(AnimatorConditionMode.If, 0, "Hit");
        var recover = hit.AddTransition(run);
        Transition(recover, 0.15f);
        recover.hasExitTime = true;
        recover.exitTime = 1f;
        EditorUtility.SetDirty(controller);

        var prefab = CreatePeel();
        var motor = Object.FindFirstObjectByType<RunnerMotor>();
        Set(motor, "slideDuration", 0.95f);
        var animation = motor.GetComponent<RunnerAnimation>();
        var animator = motor.GetComponentInChildren<Animator>();
        animator.avatar = avatar;
        animator.applyRootMotion = false;
        EditorUtility.SetDirty(animator);
        Set(animation, "animator", animator);
        Set(animation, "slideClipLength", slide.length);
        Set(animation, "stumbleClipLength", stumble.length);
        Set(animation, "hitDuration", 0.8f);
        Set(Object.FindFirstObjectByType<RunnerCourseGenerator>(), "bananaPeelPrefab", prefab.GetComponent<BananaPeelHazard>());
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log($"Slide/peel configured: avatar={avatar.name}, slide={slide.length:F3}s / 0.95s, stumble={stumble.length:F3}s / 0.8s.");
    }

    static AnimationClip ImportSlide(Avatar avatar)
    {
        string path = Root + "Animations/Monkey@Running Slide.fbx";
        var importer = (ModelImporter)AssetImporter.GetAtPath(path);
        importer.animationType = ModelImporterAnimationType.Human;
        // The animation FBX has different limb lengths. Copying MonkeyAvatar's
        // skeleton produces a rig mismatch; retain source proportions and retarget.
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var description = avatar.humanDescription;
        description.skeleton = source.GetComponentsInChildren<Transform>(true).Select(t => new SkeletonBone
        {
            name = t.name, position = t.localPosition, rotation = t.localRotation, scale = t.localScale
        }).ToArray();
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.sourceAvatar = null;
        importer.humanDescription = description;
        importer.importNormals = ModelImporterNormals.Calculate;
        var clips = importer.clipAnimations;
        if (clips.Length == 0) clips = importer.defaultClipAnimations;
        foreach (var clip in clips)
        {
            clip.loopTime = false;
            clip.lockRootRotation = true;
            // Extract the source's forward travel; RunnerMotor owns movement.
            // Baking it into the pose sends the mesh outside its collision capsule.
            clip.lockRootPositionXZ = false;
            clip.keepOriginalPositionXZ = false;
            clip.lockRootHeightY = true;
            clip.keepOriginalPositionY = true;
        }
        importer.clipAnimations = clips;
        importer.SaveAndReimport();
        var result = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c => !c.name.StartsWith("__"));
        if (!result.humanMotion) throw new InvalidOperationException("Slide is not humanoid.");
        return result;
    }

    static void Transition(AnimatorStateTransition t, float seconds)
    {
        t.hasExitTime = false;
        t.hasFixedDuration = true;
        t.duration = seconds;
        t.canTransitionToSelf = false;
    }

    static GameObject CreatePeel()
    {
        var sound = CreateSound();
        var puff = new GameObject("SlipPuff");
        var ps = puff.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = false; main.duration = 0.6f; main.startLifetime = 0.45f;
        main.startSpeed = 0.9f; main.startSize = 0.16f;
        main.startColor = new Color(1f, 0.91f, 0.65f, 0.65f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0, 12) });
        var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Hemisphere; shape.radius = 0.2f;
        var size = ps.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1, 1, 0));
        string matPath = Root + "Materials/SlipPuff.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            AssetDatabase.CreateAsset(mat, matPath);
        }
        string texturePath = Root + "Materials/SlipPuffTexture.asset";
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        if (texture == null)
        {
            texture = new Texture2D(32, 32, TextureFormat.RGBA32, false) { name = "Soft Slip Puff", wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 32; y++)
                for (int x = 0; x < 32; x++)
                {
                    float radius = new Vector2((x - 15.5f) / 15.5f, (y - 15.5f) / 15.5f).magnitude;
                    texture.SetPixel(x, y, new Color(1, 1, 1, Mathf.SmoothStep(1, 0, radius)));
                }
            texture.Apply();
            AssetDatabase.CreateAsset(texture, texturePath);
        }
        mat.SetTexture("_BaseMap", texture);
        mat.SetFloat("_Surface", 1);
        mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_ZWrite", 0);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = 3000;
        EditorUtility.SetDirty(mat);
        ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = mat;
        var puffPrefab = PrefabUtility.SaveAsPrefabAsset(puff, Root + "Prefabs/Effects/SlipPuff.prefab");
        Object.DestroyImmediate(puff);
        var root = new GameObject("BananaPeelHazard");
        try
        {
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Art/Banana Peel Slip Hazard.fbx"));
            visual.transform.SetParent(root.transform, false);
            foreach (var c in visual.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
            var renderers = visual.GetComponentsInChildren<Renderer>();
            var bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            visual.transform.localScale *= 1.1f / Mathf.Max(bounds.size.x, bounds.size.z);
            bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            visual.transform.localPosition -= new Vector3(bounds.center.x, bounds.min.y - 0.02f, bounds.center.z);
            var box = root.AddComponent<BoxCollider>(); box.isTrigger = true;
            box.center = new Vector3(0, 0.14f, 0); box.size = new Vector3(1.1f, 0.28f, 1.1f);
            var body = root.AddComponent<Rigidbody>(); body.isKinematic = true; body.useGravity = false;
            var hazard = root.AddComponent<BananaPeelHazard>();
            Set(hazard, "slipSound", sound);
            Set(hazard, "slipPuff", puffPrefab.GetComponent<ParticleSystem>());
            return PrefabUtility.SaveAsPrefabAsset(root, Root + "Prefabs/Course/BananaPeelHazard.prefab");
        }
        finally { Object.DestroyImmediate(root); }
    }

    static AudioClip CreateSound()
    {
        string path = Root + "Audio/CartoonSlip.wav";
        const int rate = 22050, count = 11025;
        using (var w = new BinaryWriter(File.Create(path)))
        {
            w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + count * 2);
            w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); w.Write(16);
            w.Write((short)1); w.Write((short)1); w.Write(rate); w.Write(rate * 2);
            w.Write((short)2); w.Write((short)16);
            w.Write(System.Text.Encoding.ASCII.GetBytes("data")); w.Write(count * 2);
            double phase = 0;
            for (int i = 0; i < count; i++)
            {
                double t = (double)i / count;
                phase += 2 * Math.PI * (950 - 730 * t + 70 * Math.Sin(t * 35)) / rate;
                double envelope = Math.Min(1, t * 35) * Math.Pow(1 - t, 1.5);
                w.Write((short)(14000 * envelope * (Math.Sin(phase) + 0.2 * Math.Sin(phase * 2))));
            }
        }
        AssetDatabase.ImportAsset(path);
        return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
    }

    static void Set(Object target, string property, Object value)
    {
        var data = new SerializedObject(target); data.FindProperty(property).objectReferenceValue = value; data.ApplyModifiedPropertiesWithoutUndo();
    }
    static void Set(Object target, string property, float value)
    {
        var data = new SerializedObject(target); data.FindProperty(property).floatValue = value; data.ApplyModifiedPropertiesWithoutUndo();
    }
}
