using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// Installs the win/loss cutscene director into Level01 through Unity's scene
/// APIs. Scene YAML is never touched by hand.
/// </summary>
public static class WinLossCutsceneSetup
{
    private const string ScenePath = "Assets/_Game/Scenes/Level01.unity";
    private const string RootObjectName = "Race Cutscenes";

    [MenuItem("Tools/Banana Run/Install Win-Loss Cutscenes")]
    public static void Install()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Open Level01 in Edit Mode before installing the win/loss cutscenes.");

        var player = Object.FindAnyObjectByType<RunnerMotor>();
        var gate = Object.FindAnyObjectByType<FinishGate>();
        if (player == null || gate == null)
            throw new InvalidOperationException("Level01 needs a player and a finish gate.");

        var race = Object.FindAnyObjectByType<RaceProgressTracker>();
        var rival = Object.FindAnyObjectByType<RivalRunner>();
        var chaser = Object.FindAnyObjectByType<GiantGorillaChaser>();

        // Ship the denser obstacle profile in the same deterministic pass.
        CompetitiveCourseDifficultySetup.Apply();

        var host = GameObject.Find(RootObjectName);
        if (host == null)
        {
            host = new GameObject(RootObjectName);
            Undo.RegisterCreatedObjectUndo(host, "Create race cutscene director");
        }
        var director = host.GetComponent<RaceCutsceneDirector>();
        if (director == null) director = Undo.AddComponent<RaceCutsceneDirector>(host);
        var data = new SerializedObject(director);
        data.FindProperty("race").objectReferenceValue = race;
        data.FindProperty("player").objectReferenceValue = player;
        data.FindProperty("rival").objectReferenceValue = rival;
        data.FindProperty("finishGate").objectReferenceValue = gate;
        data.FindProperty("chaser").objectReferenceValue = chaser;
        Transform basket = gate.BananaTarget;
        if (basket == null)
        {
            var found = GameObject.Find("Celebration Banana Basket");
            if (found != null) basket = found.transform;
        }
        data.FindProperty("celebrationBasket").objectReferenceValue = basket;
        data.ApplyModifiedPropertiesWithoutUndo();

        // The gorilla needs a short windup so the sad-sit beat reads before the grab.
        if (chaser != null)
        {
            var chaserData = new SerializedObject(chaser);
            var windup = chaserData.FindProperty("catchWindupSeconds");
            if (windup != null) windup.floatValue = 0.65f;
            chaserData.ApplyModifiedPropertiesWithoutUndo();
        }

        // Keep the results card behind the catch so the lift is visible.
        var hud = Object.FindAnyObjectByType<RunnerHUD>();
        if (hud != null)
        {
            var hudData = new SerializedObject(hud);
            var delay = hudData.FindProperty("gameOverRevealDelay");
            if (delay != null) delay.floatValue = 2.1f;
            hudData.ApplyModifiedPropertiesWithoutUndo();
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("Win/loss cutscenes installed: victory hops over the basket and defeat sits for the gorilla.");
    }

    /// <summary>Batch-mode entry point used by the build/verification pipeline.</summary>
    public static void InstallFromBatch()
    {
        try
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Install();
            Debug.Log("Win/loss cutscene batch setup completed for " + scene.path);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }
}
