using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Creates race assets and scene references exclusively through Unity APIs.</summary>
public static class RivalRaceSetup
{
    private const string Root = "Assets/_Game/";
    private const string PrefabPath = Root + "Prefabs/RivalRunner.prefab";

    [MenuItem("Tools/Banana Run/Configure Rival Race")]
    public static void Install()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (EditorApplication.isPlaying || scene.path != Root + "Scenes/Level01.unity")
            throw new InvalidOperationException("Open Level01 in edit mode before configuring the rival race.");
        var player = Object.FindAnyObjectByType<RunnerMotor>();
        var gate = Object.FindAnyObjectByType<FinishGate>();
        var course = Object.FindObjectsByType<RunnerCourseGenerator>()
            .FirstOrDefault(c => c.Player == player);
        if (player == null || gate == null) throw new InvalidOperationException("Player and finish gate are required.");
        var animator = player.GetComponentInChildren<Animator>(true);
        if (animator == null) throw new InvalidOperationException("The player must have an animated monkey visual.");

        // Copy only the rig/visual. Player input, health, inventory and powers never enter the rival prefab.
        Transform visualSource = animator.transform;
        while (visualSource.parent != player.transform) visualSource = visualSource.parent;
        var preview = EditorSceneManager.NewPreviewScene();
        var root = new GameObject("Rival Runner");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, preview);
        try
        {
            var visual = Object.Instantiate(visualSource.gameObject, root.transform, false);
            visual.name = visualSource.name;
            var capsule = root.AddComponent<CharacterController>();
            EditorUtility.CopySerialized(player.GetComponent<CharacterController>(), capsule);
            var rival = root.AddComponent<RivalRunner>();
            var data = new SerializedObject(rival);
            data.FindProperty("animator").objectReferenceValue = visual.GetComponentInChildren<Animator>(true);
            var vine = Object.FindAnyObjectByType<VineSwingZone>();
            if (vine != null)
            {
                var vineData = new SerializedObject(vine);
                var landing = vineData.FindProperty("landingPoint").objectReferenceValue as Transform;
                if (landing != null)
                    data.FindProperty("riverCrossing").vector2Value = new Vector2(vine.transform.position.z, landing.position.z);
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            var materials = new Dictionary<Material, Material>();
            foreach (var renderer in visual.GetComponentsInChildren<Renderer>(true))
            {
                var slots = renderer.sharedMaterials;
                for (int i = 0; i < slots.Length; i++)
                {
                    var source = slots[i];
                    if (source == null) continue;
                    if (!materials.TryGetValue(source, out var dark))
                    {
                        string path = Root + "Materials/RivalBlack" + (materials.Count == 0 ? "" : materials.Count.ToString()) + ".mat";
                        dark = AssetDatabase.LoadAssetAtPath<Material>(path);
                        if (dark == null)
                        {
                            dark = new Material(source);
                            AssetDatabase.CreateAsset(dark, path);
                        }
                        else EditorUtility.CopySerialized(source, dark);
                        ConfigureFurMaterial(dark);
                        materials.Add(source, dark);
                    }
                    slots[i] = dark;
                }
                renderer.sharedMaterials = slots;
            }
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
            EditorSceneManager.ClosePreviewScene(preview);
        }

        var competitor = Object.FindAnyObjectByType<RivalRunner>();
        if (competitor == null)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), scene);
            Undo.RegisterCreatedObjectUndo(instance, "Create rival runner");
            competitor = instance.GetComponent<RivalRunner>();
        }
        Undo.RecordObject(competitor.transform, "Place rival beside player");
        competitor.transform.position = player.transform.position + Vector3.right * player.LaneDistance;
        var rivalData = new SerializedObject(competitor);
        rivalData.FindProperty("player").objectReferenceValue = player;
        rivalData.FindProperty("course").objectReferenceValue = course;
        rivalData.ApplyModifiedProperties();
        var camera = Object.FindAnyObjectByType<RunnerCamera>();
        if (camera != null)
        {
            var cameraData = new SerializedObject(camera);
            cameraData.FindProperty("raceRival").objectReferenceValue = competitor.transform;
            cameraData.ApplyModifiedProperties();
        }

        var tracker = Object.FindAnyObjectByType<RaceProgressTracker>();
        if (tracker == null)
        {
            var race = new GameObject("Direct Race");
            Undo.RegisterCreatedObjectUndo(race, "Create direct race");
            tracker = Undo.AddComponent<RaceProgressTracker>(race);
        }
        var trackerData = new SerializedObject(tracker);
        trackerData.FindProperty("player").objectReferenceValue = player;
        trackerData.FindProperty("rival").objectReferenceValue = competitor;
        trackerData.FindProperty("finishGate").objectReferenceValue = gate;

        var distance = Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include)
            .FirstOrDefault(t => t.name == "Distance" && t.GetComponentInParent<LevelProgressHUD>() != null);
        if (distance != null && distance.transform.parent.parent is RectTransform safeArea)
        {
            var labelTransform = safeArea.Find("Race Standings");
            TMP_Text label;
            if (labelTransform == null)
            {
                label = Object.Instantiate(distance, safeArea, false);
                label.name = "Race Standings";
                Undo.RegisterCreatedObjectUndo(label.gameObject, "Create race standings");
            }
            else label = labelTransform.GetComponent<TMP_Text>();
            var rect = label.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -380f);
            rect.sizeDelta = new Vector2(-64f, 38f);
            label.fontSize = 23f;
            label.enableAutoSizing = true;
            label.fontSizeMin = 16f;
            label.fontSizeMax = 23f;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            label.text = "TIED   YOU 0 m   RIVAL 0 m";
            trackerData.FindProperty("standingsLabel").objectReferenceValue = label;
        }
        trackerData.ApplyModifiedProperties();
        EnableCompetition();
        Debug.Log("Direct race configured: dark rival, independent movement, progress tracking and standings.");
    }

    private static void ConfigureFurMaterial(Material material)
    {
        // Retain the monkey's UV-matched albedo/normal maps and URP lighting.
        material.name = "Rival Charcoal Fur";
        var fur = new Color(0.32f, 0.265f, 0.225f, 1f);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", fur);
        if (material.HasProperty("_Color")) material.SetColor("_Color", fur);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
        if (material.HasProperty("_MetallicGlossMap")) material.SetTexture("_MetallicGlossMap", null);
        material.DisableKeyword("_METALLICSPECGLOSSMAP");
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.18f);
        if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0.18f);
        if (material.HasProperty("_SmoothnessTextureChannel")) material.SetFloat("_SmoothnessTextureChannel", 0f);
        material.DisableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
        var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "Materials/RivalFurNormal.png");
        if (material.HasProperty("_BumpMap") && normal != null) material.SetTexture("_BumpMap", normal);
        if (material.HasProperty("_BumpMap") && material.GetTexture("_BumpMap") != null)
        {
            material.EnableKeyword("_NORMALMAP");
            if (material.HasProperty("_BumpScale")) material.SetFloat("_BumpScale", 0.8f);
        }
        EditorUtility.SetDirty(material);
    }

    [MenuItem("Tools/Banana Run/Refine Rival and Add Ballistic Throws")]
    public static void Refine()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Refine the rival in edit mode.");
        const string normalPath = Root + "Materials/RivalFurNormal.png";
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath) == null)
            AssetDatabase.CopyAsset(Root + "Art/Monkey/Textures/Monkey_Normal.png", normalPath);
        var normalImporter = AssetImporter.GetAtPath(normalPath) as TextureImporter;
        if (normalImporter != null && normalImporter.textureType != TextureImporterType.NormalMap)
        {
            normalImporter.textureType = TextureImporterType.NormalMap;
            normalImporter.sRGBTexture = false;
            normalImporter.SaveAndReimport();
        }
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Prefabs/Tropical/CoconutProjectile.prefab");
        if (source == null) throw new InvalidOperationException("The existing fruit projectile visual is missing.");
        const string projectilePath = Root + "Prefabs/RivalThrownFruit.prefab";
        var preview = EditorSceneManager.NewPreviewScene();
        var fruit = new GameObject("Rival Thrown Fruit");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(fruit, preview);
        try
        {
            var visual = Object.Instantiate(source, fruit.transform, false);
            visual.name = "Spinning Fruit Visual";
            foreach (var behavior in visual.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(behavior);
            foreach (var collider in visual.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
            var renderers = visual.GetComponentsInChildren<Renderer>();
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            float diameter = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            visual.transform.localScale *= 0.5f / Mathf.Max(0.01f, diameter);
            bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            visual.transform.localPosition -= bounds.center - fruit.transform.position;
            var ballistic = fruit.AddComponent<BallisticRivalProjectile>();
            var legacyAudio = new SerializedObject(source.GetComponent<CoconutProjectile>());
            var ballisticData = new SerializedObject(ballistic);
            ballisticData.FindProperty("obstacleHitSound").objectReferenceValue = legacyAudio.FindProperty("smashBoxSound").objectReferenceValue;
            ballisticData.FindProperty("obstacleHitVolume").floatValue = legacyAudio.FindProperty("smashVolume").floatValue;
            ballisticData.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(fruit, projectilePath);
        }
        finally
        {
            Object.DestroyImmediate(fruit);
            EditorSceneManager.ClosePreviewScene(preview);
        }

        var prefab = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            if (prefab.GetComponent<BananaInventory>() == null) prefab.AddComponent<BananaInventory>();
            var paceData = new SerializedObject(prefab.GetComponent<RivalRunner>());
            paceData.FindProperty("paceMultiplier").floatValue = 1.04f;
            paceData.FindProperty("projectileStunDuration").floatValue = 2f;
            paceData.ApplyModifiedPropertiesWithoutUndo();
            foreach (var material in prefab.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Distinct())
                if (material != null) ConfigureFurMaterial(material);
            var thrower = prefab.GetComponent<RivalProjectileThrower>();
            if (thrower == null) thrower = prefab.AddComponent<RivalProjectileThrower>();
            var data = new SerializedObject(thrower);
            data.FindProperty("projectilePrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<BallisticRivalProjectile>(projectilePath);
            data.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(prefab, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }
        AssetDatabase.SaveAssets();
        Debug.Log("Rival refined: textured charcoal fur, obstacle jumping and spinning ballistic fruit throws.");
    }

    [MenuItem("Tools/Banana Run/Enable Full Dual Runner Competition")]
    public static void EnableCompetition()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (EditorApplication.isPlaying || scene.path != Root + "Scenes/Level01.unity")
            throw new InvalidOperationException("Open Level01 in edit mode to enable the dual runner competition.");
        Refine();
        var player = Object.FindAnyObjectByType<RunnerMotor>();
        var rival = Object.FindAnyObjectByType<RivalRunner>();
        if (player == null || rival == null) throw new InvalidOperationException("Configure the rival race first.");
        var thrower = player.GetComponent<CoconutThrower>();
        var data = new SerializedObject(thrower);
        data.FindProperty("competitiveProjectilePrefab").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<BallisticRivalProjectile>(Root + "Prefabs/RivalThrownFruit.prefab");
        data.FindProperty("rival").objectReferenceValue = rival;
        data.FindProperty("startingCoconuts").intValue = 2;
        data.ApplyModifiedProperties();
        var rivalData = new SerializedObject(rival);
        rivalData.FindProperty("paceMultiplier").floatValue = 1.04f;
        rivalData.FindProperty("projectileStunDuration").floatValue = 2f;
        rivalData.ApplyModifiedProperties();
        var healthData = new SerializedObject(player.GetComponent<PlayerHealth>());
        healthData.FindProperty("projectileStunDuration").floatValue = 2f;
        healthData.ApplyModifiedProperties();
        var chaser = Object.FindAnyObjectByType<GiantGorillaChaser>();
        if (chaser != null)
        {
            var chaserData = new SerializedObject(chaser);
            chaserData.FindProperty("rival").objectReferenceValue = rival;
            chaserData.ApplyModifiedProperties();
        }
        ConfigureRivalMarker(player, rival);
        var label = Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include)
            .FirstOrDefault(t => t.name == "Race Standings");
        if (label != null)
        {
            Undo.RecordObject(label.rectTransform, "Fit dual runner banana counts");
            label.rectTransform.sizeDelta = new Vector2(label.rectTransform.sizeDelta.x, 78f);
            label.text = "TIED   YOU 0 m   RIVAL 0 m\nBANANAS   YOU 0/6   RIVAL 0/6";
            EditorUtility.SetDirty(label);
        }
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Dual runner competition enabled: shared banana quota, rival slides, two-way combat and varied AI aim.");
    }

    private static void ConfigureRivalMarker(RunnerMotor player, RivalRunner rival)
    {
        var progress = Object.FindObjectsByType<LevelProgressHUD>().FirstOrDefault(h => h.Player == player.transform);
        if (progress == null) return;
        var data = new SerializedObject(progress);
        var playerMarker = data.FindProperty("monkeyMarker").objectReferenceValue as RectTransform;
        if (playerMarker == null) return;
        var marker = playerMarker.parent.Find("Rival Marker") as RectTransform;
        if (marker == null)
        {
            marker = Object.Instantiate(playerMarker, playerMarker.parent, false);
            marker.name = "Rival Marker";
            Undo.RegisterCreatedObjectUndo(marker.gameObject, "Create rival progress marker");
        }
        marker.localScale = Vector3.one * 0.72f;
        marker.anchorMin = marker.anchorMax = new Vector2(0f, 0.5f);
        marker.anchoredPosition = new Vector2(0f, 52f);
        foreach (var graphic in marker.GetComponentsInChildren<UnityEngine.UI.Image>(true))
        {
            graphic.raycastTarget = false;
            if (graphic.name == "Marker Halo") graphic.color = new Color(0.12f, 0.85f, 0.95f, 1f);
            else if (graphic.name.Contains("Face") || graphic.name == "Muzzle") graphic.color = new Color(0.67f, 0.72f, 0.73f, 1f);
            else graphic.color = new Color(0.12f, 0.10f, 0.09f, 1f);
        }
        var stem = marker.Find("Progress Stem") as RectTransform;
        if (stem == null)
        {
            stem = new GameObject("Progress Stem", typeof(RectTransform), typeof(UnityEngine.UI.Image)).GetComponent<RectTransform>();
            stem.SetParent(marker, false);
        }
        stem.SetAsFirstSibling();
        stem.anchorMin = stem.anchorMax = stem.pivot = new Vector2(0.5f, 0.5f);
        stem.sizeDelta = new Vector2(2f, 72f);
        stem.anchoredPosition = new Vector2(0f, -36f);
        var line = stem.GetComponent<UnityEngine.UI.Image>();
        line.color = new Color(0.12f, 0.85f, 0.95f, 0.8f);
        line.raycastTarget = false;
        data.FindProperty("rival").objectReferenceValue = rival;
        data.FindProperty("rivalMarker").objectReferenceValue = marker;
        data.FindProperty("race").objectReferenceValue = Object.FindAnyObjectByType<RaceProgressTracker>();
        data.ApplyModifiedProperties();
    }
}
