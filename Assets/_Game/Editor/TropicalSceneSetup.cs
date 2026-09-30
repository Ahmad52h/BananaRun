using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static partial class TropicalArtSetup
{
    [MenuItem("Tools/Banana Run/Apply Tropical Art Direction")]
    public static void Apply()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (EditorApplication.isPlaying || scene.path != "Assets/_Game/Scenes/Level01.unity")
            throw new InvalidOperationException("Open Level01 in edit mode.");
        if (UniversalRenderPipeline.asset == null) throw new InvalidOperationException("URP must be active.");
        Directory.CreateDirectory(Art); Directory.CreateDirectory(Prefabs); Directory.CreateDirectory(Materials);
        AssetDatabase.Refresh();
        bark = Mat("WarmBark", "#795032"); cutWood = Mat("CutWood", "#DCAA64");
        leaves = Mat("PalmGreen", "#569C39"); leafLight = Mat("LeafLight", "#8EBE48");
        white = Mat("PetalCream", "#FFF7DE"); gold = Mat("BananaGold", "#FFD34F");
        stone = Mat("RiverStone", "#B6BBA8"); rope = Mat("Rope", "#C3A373");
        Lighting();
        Trail();
        var palm = BuildPalm(); var flowers = BuildFlowers(); var log = BuildLog();
        var borders = new[] { BuildBorder(0, palm, flowers), BuildBorder(1, palm, flowers), BuildBorder(2, palm, flowers) };
        Scenery(palm, borders);
        UpdateLogs(log);
        FinishArch();
        BananaHighlights();
        Hud();
        foreach (var root in scene.GetRootGameObjects())
            foreach (var component in root.GetComponentsInChildren<Component>(true))
                if (component != null && PrefabUtility.IsPartOfPrefabInstance(component))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(component);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("Tropical art direction applied and saved to Level01.");
    }

    static void Lighting()
    {
        var pipeline = UniversalRenderPipeline.asset;
        pipeline.supportsHDR = true;
        var pipelineData = new SerializedObject(pipeline);
        pipelineData.FindProperty("m_SoftShadowsSupported").boolValue = true;
        pipelineData.ApplyModifiedPropertiesWithoutUndo();
        pipeline.shadowDistance = 80;
        EditorUtility.SetDirty(pipeline);
        var camera = Camera.main;
        camera.allowHDR = true;
        camera.farClipPlane = 450;
        var cameraData = camera.GetUniversalAdditionalCameraData();
        cameraData.renderPostProcessing = true;
        cameraData.volumeLayerMask |= 1;
        cameraData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
        var light = Object.FindObjectsByType<Light>(FindObjectsSortMode.None).First(l => l.type == LightType.Directional);
        light.transform.rotation = Quaternion.Euler(55, -35, 0);
        ColorUtility.TryParseHtmlString("#FFF3D6", out var sunlight);
        light.color = sunlight;
        light.intensity = 1.35f;
        light.shadows = LightShadows.Soft;
        light.shadowStrength = 0.8f;
        light.shadowBias = 0.035f;
        light.shadowNormalBias = 0.25f;
        RenderSettings.sun = light;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.52f, 0.67f, 0.73f);
        RenderSettings.ambientEquatorColor = new Color(0.43f, 0.53f, 0.34f);
        RenderSettings.ambientGroundColor = new Color(0.3f, 0.32f, 0.21f);
        RenderSettings.ambientIntensity = 1;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.57f, 0.76f, 0.69f);
        RenderSettings.fogStartDistance = 95;
        RenderSettings.fogEndDistance = 320;
        var volume = Object.FindObjectsByType<Volume>(FindObjectsSortMode.None).FirstOrDefault(v => v.isGlobal);
        if (volume == null) volume = new GameObject("Global Volume").AddComponent<Volume>();
        volume.gameObject.layer = 0;
        volume.isGlobal = true; volume.enabled = true; volume.weight = 1; volume.priority = 10;
        const string path = "Assets/_Game/Settings/TropicalSunlight.asset";
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
        if (profile == null) { profile = ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(profile, path); }
        volume.sharedProfile = profile;
        var color = Override<ColorAdjustments>(profile);
        color.saturation.Override(18); color.contrast.Override(9); color.postExposure.Override(0.1f);
        var tone = Override<Tonemapping>(profile); tone.mode.Override(TonemappingMode.Neutral);
        var bloom = Override<Bloom>(profile);
        bloom.threshold.Override(1.05f); bloom.intensity.Override(0.23f); bloom.scatter.Override(0.55f);
        bloom.tint.Override(new Color(1, 0.94f, 0.77f));
        var vignette = Override<Vignette>(profile); vignette.intensity.Override(0.08f); vignette.smoothness.Override(0.5f);
        foreach (var component in profile.components)
        {
            if (!AssetDatabase.Contains(component)) AssetDatabase.AddObjectToAsset(component, profile);
            EditorUtility.SetDirty(component);
        }
        EditorUtility.SetDirty(profile);
    }

    static T Override<T>(VolumeProfile profile) where T : VolumeComponent
        => profile.TryGet<T>(out var component) ? component : profile.Add<T>(true);

    static GameObject Fresh(string name)
    {
        var old = GameObject.Find(name);
        if (old != null) Object.DestroyImmediate(old);
        return new GameObject(name);
    }

    static void Trail()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(Materials + "OrganicTrail.mat");
        if (material == null) { material = new Material(Shader.Find("BananaRun/TropicalTrail")); AssetDatabase.CreateAsset(material, Materials + "OrganicTrail.mat"); }
        material.SetColor("_DirtColor", new Color(0.64f, 0.51f, 0.37f));
        material.SetColor("_GrassColor", new Color(0.31f, 0.45f, 0.18f));
        material.SetFloat("_HalfWidth", 4.6f); material.SetFloat("_EdgeBlend", 1.45f);
        var root = Fresh("Organic Dirt Trail");
        var v = new List<Vector3> { new Vector3(-70, -0.295f, -60), new Vector3(-70, -0.295f, 945), new Vector3(70, -0.295f, 945), new Vector3(70, -0.295f, -60) };
        MeshObject("Blended Dirt and Grass", root.transform, SaveMesh("OrganicTrailSurface", v, new List<int> { 0, 1, 2, 0, 2, 3 }), material);
        root.GetComponentInChildren<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        GameObject.Find("Ground").GetComponent<Renderer>().enabled = false;
        foreach (string bank in new[] { "Extended Left Bank", "Extended Right Bank" })
            if (GameObject.Find(bank) != null) GameObject.Find(bank).GetComponent<Renderer>().enabled = false;
        EditorUtility.SetDirty(material);
    }

    static void Scenery(GameObject palm, GameObject[] borders)
    {
        var gate = Object.FindFirstObjectByType<FinishGate>();
        var course = Object.FindFirstObjectByType<RunnerCourseGenerator>();
        var data = new SerializedObject(course);
        var palette = data.FindProperty("sceneryPrefabs");
        palette.arraySize = 2;
        palette.GetArrayElementAtIndex(0).objectReferenceValue = palm;
        palette.GetArrayElementAtIndex(1).objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Polytope Studio/Lowpoly_Environments/Prefabs/Trees/PT_Fruit_Tree_01_green.prefab");
        data.FindProperty("scenerySpacing").floatValue = 16;
        data.ApplyModifiedPropertiesWithoutUndo();
        var root = Fresh("Tropical Border Streamer");
        var preview = new GameObject("Editor Border Preview"); preview.transform.SetParent(root.transform);
        for (int i = -1; i < 4; i++)
        {
            var chunk = (GameObject)PrefabUtility.InstantiatePrefab(borders[(i + 3) % 3], preview.transform);
            chunk.transform.position = new Vector3(0, -0.3f, i * 24);
        }
        var streamer = root.AddComponent<TropicalScenery>();
        data = new SerializedObject(streamer);
        data.FindProperty("player").objectReferenceValue = Object.FindFirstObjectByType<RunnerMotor>().transform;
        data.FindProperty("finishLine").objectReferenceValue = gate.transform;
        data.FindProperty("editorPreview").objectReferenceValue = preview;
        palette = data.FindProperty("borderChunks"); palette.arraySize = borders.Length;
        for (int i = 0; i < borders.Length; i++) palette.GetArrayElementAtIndex(i).objectReferenceValue = borders[i];
        data.ApplyModifiedPropertiesWithoutUndo();
        Mountains();
    }

    static void Mountains()
    {
        var root = Fresh("Distant Jungle Silhouettes");
        var material = Mat("DistantJungle", "#527F6C");
        var v = new List<Vector3>(); var t = new List<int>();
        // Flat-shaded tapered peaks, kept entirely outside the playable corridor.
        for (int side = 0; side < 8; side++)
        {
            float a = side * Mathf.PI / 4, b = (side + 1) * Mathf.PI / 4;
            int n = v.Count;
            v.Add(new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)));
            v.Add(new Vector3(0.15f, 1, 0.1f));
            v.Add(new Vector3(Mathf.Cos(b), 0, Mathf.Sin(b)));
            t.AddRange(new[] { n, n + 1, n + 2 });
        }
        var mesh = SaveMesh("JunglePeak", v, t);
        for (int i = 0; i < 10; i++) for (int side = -1; side <= 1; side += 2)
        {
            var mountain = new GameObject("Jungle Ridge"); mountain.transform.SetParent(root.transform);
            MeshObject("Peak", mountain.transform, mesh, material);
            mountain.transform.position = new Vector3(side * (62 + i % 3 * 8), -3, 75 + i * 95);
            mountain.transform.localScale = new Vector3(32 + i % 3 * 6, 30 + i % 4 * 9, 52);
            mountain.transform.localEulerAngles = new Vector3(0, i * 37, 0);
            mountain.GetComponentInChildren<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        }
    }

    static void UpdateLogs(GameObject log)
    {
        foreach (var obstacle in Object.FindObjectsByType<RunnerObstacle>(FindObjectsSortMode.None).Where(o => o.name.StartsWith("LowObstacle")))
        {
            foreach (Transform child in obstacle.transform) child.gameObject.SetActive(false);
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(log, obstacle.transform);
            visual.transform.localPosition = Vector3.zero;
            var box = obstacle.GetComponent<BoxCollider>(); box.center = new Vector3(0, 0.43f, 0); box.size = new Vector3(2.24f, 0.86f, 0.86f);
        }
        const string path = "Assets/_Game/Prefabs/Course/LogHurdle.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            foreach (Transform child in root.transform.Cast<Transform>().ToArray()) Object.DestroyImmediate(child.gameObject);
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(log, root.transform);
            visual.transform.localPosition = Vector3.zero;
            var box = root.GetComponent<BoxCollider>(); box.center = new Vector3(0, 0.43f, 0); box.size = new Vector3(2.24f, 0.86f, 0.86f);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static void FinishArch()
    {
        var gate = Object.FindFirstObjectByType<FinishGate>();
        float z = gate.transform.position.z;
        var root = Fresh("Tropical Finish Arch");
        root.transform.position = new Vector3(0, -0.3f, z);
        for (int side = -1; side <= 1; side += 2)
        {
            var palm = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "TropicalPalm.prefab"), root.transform);
            palm.transform.localPosition = new Vector3(side * 8.5f, 0, 6);
            palm.transform.localEulerAngles = new Vector3(0, side * 35, 0);
            for (int i = 0; i < 4; i++)
            {
                var flowers = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "DaisyPatch.prefab"), root.transform);
                flowers.transform.localPosition = new Vector3(side * (3.4f + i * 0.35f), 0, 4 + i * 1.5f);
            }
        }
        for (int side = -1; side <= 1; side += 2)
        {
            Primitive("Timber Post", root.transform, PrimitiveType.Cylinder, new Vector3(side * 4.9f, 2.8f, 0), new Vector3(0.48f, 2.8f, 0.48f), bark, new Vector3(0, 0, side * -3));
            Primitive("Rope Binding", root.transform, PrimitiveType.Cylinder, new Vector3(side * 4.75f, 4.8f, 0), new Vector3(0.55f, 0.14f, 0.55f), rope);
            Primitive("Banana Flag", root.transform, PrimitiveType.Cube, new Vector3(side * 5.9f, 4.4f, 0), new Vector3(1.15f, 1.7f, 0.08f), leaves);
            var flag = new GameObject("Banana Emblem", typeof(SpriteRenderer)); flag.transform.SetParent(root.transform, false);
            flag.transform.localPosition = new Vector3(side * 5.9f, 4.4f, -0.06f);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Game/UI/Icons/Icon_Banana.png.png");
            flag.GetComponent<SpriteRenderer>().sprite = sprite;
            if (sprite != null) flag.transform.localScale = Vector3.one * (0.9f / sprite.bounds.size.x);
        }
        Primitive("Top Crossbeam", root.transform, PrimitiveType.Cylinder, new Vector3(0, 5.3f, 0), new Vector3(0.42f, 5.15f, 0.42f), bark, new Vector3(0, 0, 90));
        Primitive("Finish Banner", root.transform, PrimitiveType.Cube, new Vector3(0, 4.6f, -0.13f), new Vector3(7.8f, 1.25f, 0.18f), cutWood);
        var text = new GameObject("FINISH", typeof(TextMeshPro)).GetComponent<TextMeshPro>();
        text.transform.SetParent(root.transform, false);
        text.transform.localPosition = new Vector3(0, 4.6f, -0.24f);
        text.text = "FINISH"; text.fontSize = 10; text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center; text.color = new Color(1, 0.96f, 0.73f);
        text.rectTransform.sizeDelta = new Vector2(7.2f, 1.1f);
        var basket = GameObject.Find("FinishBasket");
        Fit(basket, new Vector3(3.6f, 2.4f, 2.8f), new Vector3(0, -0.3f, z + 6));
        var pile = Fresh("Celebration Banana Basket");
        var bananaAsset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Effects/BananaDeliveryVisual.prefab");
        for (int i = 0; i < 18; i++)
        {
            var banana = (GameObject)PrefabUtility.InstantiatePrefab(bananaAsset, pile.transform);
            float a = i * 2.4f, radius = i < 12 ? 0.8f : 0.3f;
            Fit(banana, new Vector3(0.85f, 0.48f, 0.4f), new Vector3(Mathf.Cos(a) * radius, 1.6f + (i % 3) * 0.16f, z + 6 + Mathf.Sin(a) * radius * 0.6f));
            foreach (var collider in banana.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(collider);
        }
        var data = new SerializedObject(gate);
        var target = data.FindProperty("bananaTarget").objectReferenceValue as Transform;
        if (target != null) target.position = new Vector3(0, 2, z + 6);
    }

    static void BananaHighlights()
    {
        const string path = "Assets/_Game/Prefabs/Banana.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>())
            {
                var mats = renderer.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null || !mats[i].HasProperty("_EmissionColor")) continue;
                    string materialPath = Materials + "GoldenBanana" + i + ".mat";
                    var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                    if (material == null) { material = new Material(mats[i]); AssetDatabase.CreateAsset(material, materialPath); }
                    material.EnableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor", new Color(0.35f, 0.21f, 0.015f));
                    material.SetFloat("_Smoothness", 0.28f);
                    mats[i] = material;
                    EditorUtility.SetDirty(material);
                }
                renderer.sharedMaterials = mats;
            }
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static Sprite BadgeSprite()
    {
        const string path = "Assets/_Game/UI/Icons/TropicalPill.png";
        var texture = new Texture2D(128, 64, TextureFormat.RGBA32, false);
        for (int y = 0; y < 64; y++) for (int x = 0; x < 128; x++)
        {
            float distance = Vector2.Distance(new Vector2(x, y), new Vector2(Mathf.Clamp(x, 31.5f, 95.5f), 31.5f));
            texture.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(31.5f - distance)));
        }
        texture.Apply(); File.WriteAllBytes(path, texture.EncodeToPNG()); Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite; importer.spriteBorder = new Vector4(32, 32, 32, 32);
        importer.alphaIsTransparency = true; importer.mipmapEnabled = false; importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static void Hud()
    {
        var safe = GameObject.Find("SafeArea").transform;
        var badgeTransform = safe.Find("Banana Badge") as RectTransform;
        if (badgeTransform == null) badgeTransform = new GameObject("Banana Badge", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        badgeTransform.SetParent(safe, false);
        badgeTransform.anchorMin = badgeTransform.anchorMax = new Vector2(0, 1);
        badgeTransform.pivot = new Vector2(0, 1); badgeTransform.anchoredPosition = new Vector2(32, -30); badgeTransform.sizeDelta = new Vector2(240, 100);
        var badge = badgeTransform.GetComponent<Image>(); badge.sprite = BadgeSprite(); badge.type = Image.Type.Sliced;
        badge.color = new Color(0.12f, 0.23f, 0.12f, 0.93f); badge.raycastTarget = false;
        var icon = GameObject.Find("BananaIcon").GetComponent<RectTransform>();
        icon.SetParent(badgeTransform, false); icon.anchorMin = icon.anchorMax = new Vector2(0, 0.5f); icon.pivot = new Vector2(0.5f, 0.5f);
        icon.anchoredPosition = new Vector2(58, 0); icon.sizeDelta = new Vector2(76, 76);
        var label = GameObject.Find("BananaCounterText").GetComponent<TMP_Text>();
        label.transform.SetParent(badgeTransform, false);
        label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        label.rectTransform.pivot = new Vector2(0.5f, 0.5f); label.rectTransform.anchoredPosition = new Vector2(37, 0); label.rectTransform.sizeDelta = new Vector2(115, 80);
        label.fontSize = 48; label.color = new Color(1, 0.96f, 0.79f); label.alignment = TextAlignmentOptions.Center; label.fontStyle = FontStyles.Bold; label.raycastTarget = false;
        for (int i = 1; i <= 3; i++)
        {
            var heart = GameObject.Find("Heart_" + i).GetComponent<RectTransform>();
            heart.anchorMin = heart.anchorMax = new Vector2(1, 1); heart.pivot = new Vector2(1, 1);
            heart.anchoredPosition = new Vector2(-35 - (i - 1) * 88, -42); heart.sizeDelta = new Vector2(74, 74);
        }
        var button = GameObject.Find("ThrowButton").GetComponent<Button>();
        var circle = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Game/UI/Icons/ProgressCircle.png");
        var coconutSprite = button.GetComponent<Image>().sprite;
        button.GetComponent<Image>().sprite = circle; button.GetComponent<Image>().color = new Color(1, 0.78f, 0.27f);
        var rect = button.GetComponent<RectTransform>(); rect.anchoredPosition = new Vector2(-45, 65); rect.sizeDelta = new Vector2(175, 175);
        var colors = button.colors; colors.disabledColor = new Color(0.75f, 0.78f, 0.66f, 0.65f); colors.pressedColor = new Color(0.82f, 0.65f, 0.27f); button.colors = colors;
        var coconut = button.transform.Find("Coconut Action Icon");
        if (coconut == null)
        {
            var image = new GameObject("Coconut Action Icon", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(button.transform, false); image.sprite = coconutSprite;
            image.rectTransform.sizeDelta = new Vector2(122, 122); image.raycastTarget = false; image.preserveAspect = true;
        }
    }
}
