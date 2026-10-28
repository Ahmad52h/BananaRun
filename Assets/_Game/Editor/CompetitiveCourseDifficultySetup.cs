using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Applies the competitive course profile through Unity's scene and prefab APIs.</summary>
public static class CompetitiveCourseDifficultySetup
{
    private const string CourseFolder = "Assets/_Game/Prefabs/Course/";

    [MenuItem("Tools/Banana Run/Increase Competitive Course Difficulty")]
    public static void Apply()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.name != "Level01")
            throw new InvalidOperationException("Open Level01 in Edit Mode before applying course difficulty.");
        var course = Object.FindAnyObjectByType<RunnerCourseGenerator>();
        if (course == null) throw new InvalidOperationException("Level01 requires a course generator.");

        // Faster, longer-travelling hazards turn each row into a real decision.
        var log = TuneHazard(CourseFolder + "RollingLog.prefab", CourseFolder + "LogHurdle.prefab",
            "RollingLog", DynamicRunnerHazard.Motion.RollingLog, 3.9f, 6.5f, 0.4f);
        var barrier = TuneHazard(CourseFolder + "MovingSlideBarrier.prefab", CourseFolder + "HighSlideBeam.prefab",
            "MovingSlideBarrier", DynamicRunnerHazard.Motion.Barrier, 2.9f, 5f, 0f);

        var data = new SerializedObject(course);
        // Tighter jitter band, denser rows and a shorter maximum-speed reaction floor.
        Set(data, "minimumSpacing", 10f); Set(data, "maximumSpacing", 16f);
        Set(data, "easyObstacleSpacing", 15f); Set(data, "hardObstacleSpacing", 9.5f);
        Set(data, "staggerDistance", 3.2f);
        Set(data, "easyDoubleObstacleChance", 0.62f); Set(data, "hardDoubleObstacleChance", 0.98f);
        Set(data, "easyLaneChangeChance", 0.42f); Set(data, "hardLaneChangeChance", 0.75f);
        Set(data, "easyStaggerChance", 0.3f); Set(data, "hardStaggerChance", 0.85f);
        Set(data, "obstacleFrequencyMultiplier", 1.45f); Set(data, "maxHazardSpeedMultiplier", 1.35f);
        Set(data, "earlyDynamicChance", 0.3f); Set(data, "lateDynamicChance", 0.78f);
        Set(data, "minimumReactionSeconds", 0.8f);
        data.FindProperty("randomizeEachRun").boolValue = true;
        var palette = data.FindProperty("obstaclePrefabs");
        var prefabs = new List<GameObject>();
        for (int i = 0; i < palette.arraySize; i++)
        {
            var prefab = palette.GetArrayElementAtIndex(i).objectReferenceValue as GameObject;
            if (prefab != null && prefab.name != log.name && prefab.name != barrier.name) prefabs.Add(prefab);
        }
        prefabs.Add(log); prefabs.Add(barrier);
        palette.arraySize = prefabs.Count;
        for (int i = 0; i < prefabs.Count; i++) palette.GetArrayElementAtIndex(i).objectReferenceValue = prefabs[i];
        data.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Competitive course: denser pairs and staggers, faster rolling logs and moving slide barriers.");
    }

    private static void Set(SerializedObject data, string name, float value) => data.FindProperty(name).floatValue = value;

    /// <summary>Tunes the authored hazard in place, or builds it from its source on a fresh checkout.</summary>
    private static GameObject TuneHazard(string prefabPath, string sourcePath, string name,
        DynamicRunnerHazard.Motion kind, float speed, float travel, float pivotHeight)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        var hazard = prefab != null ? prefab.GetComponent<DynamicRunnerHazard>() : null;
        if (hazard == null)
            return MovingVariant(sourcePath, name, kind, speed, travel, pivotHeight);

        var data = new SerializedObject(hazard);
        Set(data, "speed", speed);
        Set(data, "travelLimit", travel);
        Set(data, "activationDistance", 30f);
        data.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssetIfDirty(prefab);
        return prefab;
    }

    private static GameObject MovingVariant(string sourcePath, string name, DynamicRunnerHazard.Motion kind,
        float speed, float travel, float pivotHeight)
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
        if (source == null) throw new InvalidOperationException("Missing course obstacle " + sourcePath);
        var root = Object.Instantiate(source);
        try
        {
            root.name = name;
            root.transform.position = Vector3.zero;
            var children = new List<Transform>();
            foreach (Transform child in root.transform) children.Add(child);
            var pivot = new GameObject("Moving Visual").transform;
            pivot.SetParent(root.transform, false);
            pivot.localPosition = Vector3.up * pivotHeight;
            foreach (var child in children) child.SetParent(pivot, true);
            var obstacle = new SerializedObject(root.GetComponent<RunnerObstacle>());
            // Moving obstacles remain until passed or broken by a projectile/power-up.
            obstacle.FindProperty("breakOnPlayerHit").boolValue = false;
            obstacle.ApplyModifiedPropertiesWithoutUndo();
            var motion = root.AddComponent<DynamicRunnerHazard>();
            var data = new SerializedObject(motion);
            data.FindProperty("motion").enumValueIndex = (int)kind;
            data.FindProperty("visual").objectReferenceValue = pivot;
            Set(data, "speed", speed); Set(data, "travelLimit", travel);
            Set(data, "activationDistance", 30f); Set(data, "rollingRadius", Mathf.Max(0.1f, pivotHeight));
            data.ApplyModifiedPropertiesWithoutUndo();
            return PrefabUtility.SaveAsPrefabAsset(root, CourseFolder + name + ".prefab");
        }
        finally { Object.DestroyImmediate(root); }
    }
}
