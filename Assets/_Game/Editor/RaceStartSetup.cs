#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class RaceStartSetup
{
    [MenuItem("Banana Run/Setup Cinematic Race Start")]
    public static void Apply()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before configuring the race start.");
        const string path = "Assets/_Game/Animations/Warming Up.fbx";
        var importer = (ModelImporter)AssetImporter.GetAtPath(path);
        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.sourceAvatar = null;
        importer.importAnimation = true;
        var clips = importer.defaultClipAnimations;
        foreach (var clip in clips)
        {
            clip.name = "Warming up";
            clip.loopTime = true;
            clip.loopPose = true;
            clip.lockRootRotation = true;
            clip.lockRootHeightY = true;
            clip.lockRootPositionXZ = true;
            clip.keepOriginalOrientation = true;
            clip.keepOriginalPositionY = true;
            clip.keepOriginalPositionXZ = true;
        }
        importer.clipAnimations = clips;
        importer.SaveAndReimport();
        var warmup = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
            .First(c => !c.name.StartsWith("__preview__"));
        if (!warmup.isHumanMotion) throw new InvalidOperationException("Warming up must import as humanoid motion.");
        var monkey = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/_Game/Animations/AC_Monkey.controller");
        AddState(monkey, "Warming up", warmup);
        var idle = monkey.layers[0].stateMachine.states.First(s => s.state.name == "Idle").state.motion;
        var gorilla = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/_Game/Art/Gorilla/AC_Gorilla.controller");
        AddState(gorilla, "StartIdle", idle);
        AssetDatabase.SaveAssets();

        var player = UnityEngine.Object.FindAnyObjectByType<RunnerMotor>();
        if (player == null) throw new InvalidOperationException("Open the race scene before setup.");
        var sequence = UnityEngine.Object.FindAnyObjectByType<RaceStartSequence>();
        if (sequence == null) sequence = new GameObject("Cinematic Race Start").AddComponent<RaceStartSequence>();
        var gate = UnityEngine.Object.FindAnyObjectByType<WoodenStartingGate>();
        if (gate == null)
        {
            gate = new GameObject("Wooden Starting Gate").AddComponent<WoodenStartingGate>();
            gate.transform.SetParent(sequence.transform, false);
        }
        gate.transform.position = player.transform.position + Vector3.forward * 2.8f;
        var wood = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Materials/M_CrateWood.mat");
        var trim = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Materials/M_FinishBasket.mat");
        gate.Build(wood, trim, player.LaneDistance + 1f);
        gate.transform.Find("Starting line").localPosition = new Vector3(0f, 0.23f, -0.25f);
        gate.ResetClosed();
        EditorUtility.SetDirty(gate);
        var settings = new SerializedObject(sequence);
        settings.FindProperty("player").objectReferenceValue = player;
        settings.FindProperty("rival").objectReferenceValue = UnityEngine.Object.FindAnyObjectByType<RivalRunner>();
        settings.FindProperty("gorilla").objectReferenceValue = UnityEngine.Object.FindAnyObjectByType<GiantGorillaChaser>();
        settings.FindProperty("raceCamera").objectReferenceValue = UnityEngine.Object.FindAnyObjectByType<RunnerCamera>();
        settings.FindProperty("gate").objectReferenceValue = gate;
        settings.FindProperty("countdownSeconds").floatValue = 3.5f;
        settings.FindProperty("isRaceStarted").boolValue = false;
        settings.ApplyModifiedPropertiesWithoutUndo();
        ConfigureCountdownAudio(sequence);
        EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
        EditorSceneManager.SaveScene(player.gameObject.scene);
        Debug.Log("Cinematic race start configured: humanoid Warming up / Idle, Start Sound, wooden gate and 3.5-second countdown.");
    }

    public static void ConfigureCountdownAudio(RaceStartSequence sequence)
    {
        foreach (string path in new[] { "Assets/_Game/Audio/CountdownNumber.wav", "Assets/_Game/Audio/CountdownGo.wav" })
        {
            var importer = (AudioImporter)AssetImporter.GetAtPath(path);
            importer.loadInBackground = false;
            var audio = importer.defaultSampleSettings;
            audio.preloadAudioData = true;
            audio.loadType = AudioClipLoadType.DecompressOnLoad;
            audio.compressionFormat = AudioCompressionFormat.PCM;
            importer.defaultSampleSettings = audio;
            importer.SaveAndReimport();
        }
        sequence.startCountdownSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Game/Audio/CountdownNumber.wav");
        sequence.goCountdownSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Game/Audio/CountdownGo.wav");
        EditorUtility.SetDirty(sequence);
    }

    private static void AddState(AnimatorController controller, string name, Motion clip)
    {
        var machine = controller.layers[0].stateMachine;
        var state = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == name) ?? machine.AddState(name);
        state.motion = clip;
        state.speed = 1f;
        state.writeDefaultValues = true;
        EditorUtility.SetDirty(controller);
    }
}
#endif
