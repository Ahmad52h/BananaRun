using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Applies the competitive course profile through Unity's scene and prefab APIs.</summary>
public static class CompetitiveCourseDifficultySetup
{
    private const string Folder = "Assets/_Game/Prefabs/Course/";

    [MenuItem("Tools/Banana Run/Increase Competitive Course Difficulty")]
    public static void Apply()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.name != "Level01")
            throw new InvalidOperationException("Open Level01 in Edit Mode before applying course difficulty.");
        var course = Object.FindFirstObjectByType<RunnerCourseGenerator>();
        if (course == null) throw new InvalidOperationException("Level01 requires a course generator.");

        var log = MovingVariant("LogHurdle", "RollingLog", DynamicRunnerHazard.Motion.RollingLog, 3.1f, 5f, 0.4f);
        var barrier = MovingVariant("HighSlideBeam", "MovingSlideBarrier", DynamicRunnerHazard.Motion.Barrier, 2.2f, 4f, 0f);
        var data = new SerializedObject(course);
        Set(data, "easyObstacleSpacing", 18f); Set(data, "hardObstacleSpacing", 12f);
        Set(data, "easyDoubleObstacleChance", 0.5f); Set(data, "hardDoubleObstacleChance", 0.92f);
        Set(data, "easyLaneChangeChance", 0.3f); Set(data, "hardLaneChangeChance", 0.6f);
        Set(data, "easyStaggerChance", 0.2f); Set(data, "hardStaggerChance", 0.7f);
        Set(data, "obstacleFrequencyMultiplier", 1.28f); Set(data, "maxHazardSpeedMultiplier", 1.22f);
        Set(data, "earlyDynamicChance", 0.22f); Set(data, "lateDynamicChance", 0.6f);
        Set(data, "minimumReactionSeconds", 0.95f);
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
        Debug.Log("Competitive course: denser progressive pairs, staggered rows, rolling logs and moving slide barriers.");
    }

    private static void Set(SerializedObject data, string name, float value) => data.FindProperty(name).floatValue = value;

    private static GameObject MovingVariant(string sourceName, string name, DynamicRunnerHazard.Motion kind,
        float speed, float travel, float pivotHeight)
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + sourceName + ".prefab");
        if (source == null) throw new InvalidOperationException("Missing course obstacle " + sourceName);
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
            return PrefabUtility.SaveAsPrefabAsset(root, Folder + name + ".prefab");
        }
        finally { Object.DestroyImmediate(root); }
    }
}
