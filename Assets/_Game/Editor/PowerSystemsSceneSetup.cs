using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class PowerSystemsSceneSetup
{
    const string Root = "Assets/_Game/";
    const string Prefabs = Root + "Prefabs/PowerUps/";
    const string Materials = Root + "Materials/Tropical/";

    [MenuItem("Tools/Banana Run/Install Powerups and Jungle Gap")]
    public static void Install()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (EditorApplication.isPlaying || scene.path != Root + "Scenes/Level01.unity")
            throw new InvalidOperationException("Open Level01 in edit mode.");
        Directory.CreateDirectory(Prefabs);
        AssetDatabase.Refresh();
        var sparkle = AssetDatabase.LoadAssetAtPath<ParticleSystem>(Root + "Prefabs/Effects/FX_BananaPickup.prefab");
        var types = new[] { PowerUpType.Bulldozer, PowerUpType.Glider, PowerUpType.Magnet };
        var colors = new[] { "#FFD750", "#70CE72", "#A27DEB" };
        var pickups = types.Select((type, i) => MakePickup(type, colors[i], sparkle)).ToArray();
        var hazard = MakeHazard(sparkle);

        var player = Object.FindFirstObjectByType<RunnerMotor>();
        var manager = player.GetComponent<PowerUpManager>() ?? player.gameObject.AddComponent<PowerUpManager>();
        var managerData = new SerializedObject(manager);
        managerData.FindProperty("playerModel").objectReferenceValue = player.GetComponentInChildren<Animator>().transform;
        Indicator(managerData);
        managerData.ApplyModifiedPropertiesWithoutUndo();

        var course = Object.FindFirstObjectByType<RunnerCourseGenerator>();
        var courseData = new SerializedObject(course);
        var palette = courseData.FindProperty("powerUpPrefabs");
        palette.arraySize = pickups.Length;
        for (int i = 0; i < pickups.Length; i++) palette.GetArrayElementAtIndex(i).objectReferenceValue = pickups[i];
        courseData.FindProperty("fallingCoconutPrefab").objectReferenceValue = hazard;
        courseData.FindProperty("gapStart").floatValue = 610;
        courseData.FindProperty("gapEnd").floatValue = 642;
        courseData.ApplyModifiedPropertiesWithoutUndo();

        var breakEffect = AssetDatabase.LoadAssetAtPath<CrateBreakEffect>(Root + "Prefabs/Effects/CrateBreakFX.prefab");
        foreach (var obstacle in Object.FindObjectsByType<RunnerObstacle>(FindObjectsSortMode.None))
        {
            var data = new SerializedObject(obstacle);
            if (data.FindProperty("breakEffectPrefab").objectReferenceValue == null)
            {
                data.FindProperty("breakEffectPrefab").objectReferenceValue = breakEffect;
                data.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.RecordPrefabInstancePropertyModifications(obstacle);
            }
        }
        foreach (string path in AssetDatabase.FindAssets("t:Prefab", new[] { Root + "Prefabs/Course" })
                     .Select(AssetDatabase.GUIDToAssetPath))
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach (var obstacle in root.GetComponentsInChildren<RunnerObstacle>())
                {
                    var data = new SerializedObject(obstacle);
                    if (data.FindProperty("breakEffectPrefab").objectReferenceValue != null) continue;
                    data.FindProperty("breakEffectPrefab").objectReferenceValue = breakEffect;
                    data.ApplyModifiedPropertiesWithoutUndo();
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        BuildGap();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("Musical pickups, three power-ups, warning coconuts, and river vine swing installed.");
    }

    static Material Mat(string name, string hex)
    {
        string path = Materials + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, path);
        }
        ColorUtility.TryParseHtmlString(hex, out var color);
        material.color = color;
        material.SetFloat("_Smoothness", 0.23f);
        EditorUtility.SetDirty(material);
        return material;
    }

    static GameObject Primitive(string name, Transform parent, PrimitiveType shape, Vector3 position, Vector3 scale, Material material)
    {
        var go = GameObject.CreatePrimitive(shape);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localScale = scale;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.GetComponent<Renderer>().sharedMaterial = material;
        return go;
    }

    static PowerUpPickup MakePickup(PowerUpType type, string hex, ParticleSystem sparkle)
    {
        string path = Prefabs + type + "Pickup.prefab";
        var root = new GameObject(type + " Pickup");
        try
        {
            GameObject source = type == PowerUpType.Bulldozer
                ? AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Art/Giant Golden Banana.fbx")
                : type == PowerUpType.Magnet
                    ? AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Art/Magnet.obj") : null;
            if (source != null)
            {
                var model = Object.Instantiate(source, root.transform);
                model.name = source.name + " Visual";
                foreach (var childCollider in model.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(childCollider);
                var renderers = model.GetComponentsInChildren<Renderer>();
                if (renderers.Length > 0)
                {
                    Bounds bounds = renderers[0].bounds;
                    foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
                    model.transform.localScale *= (type == PowerUpType.Magnet ? 1.7f : 2f) / Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
                    bounds = renderers[0].bounds;
                    foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
                    model.transform.position -= bounds.center - root.transform.position;
                    if (type == PowerUpType.Magnet)
                    {
                        var magnetMaterial = AssetDatabase.LoadAssetAtPath<Material>(Root + "Materials/M_Magnetic.mat");
                        if (magnetMaterial != null)
                            foreach (var renderer in renderers) renderer.sharedMaterial = magnetMaterial;
                    }
                }
            }
            else
            {
                var visual = Primitive("Glowing Emblem", root.transform, PrimitiveType.Sphere, Vector3.zero, Vector3.one * 1.25f, Mat(type + "Power", hex));
                Primitive("Bright Core", visual.transform, PrimitiveType.Sphere, new Vector3(0, 0.22f, 0), Vector3.one * 0.38f, Mat("PowerCore", "#FFF3C1"));
            }
            if (type == PowerUpType.Glider)
                Primitive("Palm Leaf", root.transform, PrimitiveType.Capsule, new Vector3(0, 0.7f, 0), new Vector3(0.22f, 0.06f, 0.9f), Mat("PalmGreen", "#569C39"));
            var collider = root.AddComponent<SphereCollider>();
            collider.isTrigger = true;
            collider.radius = 1f;
            var body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            var pickup = root.AddComponent<PowerUpPickup>();
            var data = new SerializedObject(pickup);
            data.FindProperty("type").enumValueIndex = (int)type;
            data.FindProperty("collectionEffect").objectReferenceValue = sparkle;
            data.ApplyModifiedPropertiesWithoutUndo();
            return PrefabUtility.SaveAsPrefabAsset(root, path).GetComponent<PowerUpPickup>();
        }
        finally { Object.DestroyImmediate(root); }
    }

    static FallingCoconutHazard MakeHazard(ParticleSystem sparkle)
    {
        var root = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        root.name = "Falling Coconut Warning";
        try
        {
            root.transform.localScale = Vector3.one * 0.9f;
            root.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Root + "Materials/M_Coconut.mat");
            root.GetComponent<SphereCollider>().isTrigger = true;
            var body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            var shadow = Primitive("Growing Landing Shadow", root.transform, PrimitiveType.Cylinder,
                Vector3.down * 10f, new Vector3(1f, 0.012f, 1f), Mat("CoconutWarningShadow", "#394832"));
            var hazard = root.AddComponent<FallingCoconutHazard>();
            var data = new SerializedObject(hazard);
            data.FindProperty("warningShadow").objectReferenceValue = shadow.transform;
            data.FindProperty("impactEffect").objectReferenceValue = sparkle;
            data.ApplyModifiedPropertiesWithoutUndo();
            return PrefabUtility.SaveAsPrefabAsset(root, Prefabs + "FallingCoconut.prefab").GetComponent<FallingCoconutHazard>();
        }
        finally { Object.DestroyImmediate(root); }
    }

    static void Indicator(SerializedObject manager)
    {
        var safe = GameObject.Find("SafeArea").transform;
        var old = safe.Find("Power-Up Indicator");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var root = new GameObject("Power-Up Indicator", typeof(RectTransform)).GetComponent<RectTransform>();
        root.SetParent(safe, false);
        root.anchorMin = root.anchorMax = new Vector2(0.5f, 1f);
        root.pivot = new Vector2(0.5f, 1f);
        root.anchoredPosition = new Vector2(0, -270);
        root.sizeDelta = new Vector2(260, 72);
        var circle = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "UI/Icons/ProgressCircle.png");
        var ring = new GameObject("Countdown Ring", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        ring.transform.SetParent(root, false);
        ring.rectTransform.sizeDelta = new Vector2(68, 68);
        ring.rectTransform.anchoredPosition = new Vector2(-90, 0);
        ring.sprite = circle;
        ring.color = new Color(1f, 0.79f, 0.25f);
        ring.type = Image.Type.Filled;
        ring.fillMethod = Image.FillMethod.Radial360;
        ring.raycastTarget = false;
        var icon = new GameObject("Power Icon", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        icon.transform.SetParent(root, false);
        icon.rectTransform.sizeDelta = new Vector2(48, 48);
        icon.rectTransform.anchoredPosition = new Vector2(-90, 0);
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        var label = new GameObject("Power Countdown", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        label.transform.SetParent(root, false);
        label.rectTransform.sizeDelta = new Vector2(180, 55);
        label.rectTransform.anchoredPosition = new Vector2(31, 0);
        label.fontSize = 23;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(1, 0.95f, 0.78f);
        label.raycastTarget = false;
        manager.FindProperty("icon").objectReferenceValue = icon;
        manager.FindProperty("countdownFill").objectReferenceValue = ring;
        manager.FindProperty("countdownLabel").objectReferenceValue = label;
        manager.FindProperty("bulldozerIcon").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "UI/Icons/Icon_Banana.png.png");
        manager.FindProperty("gliderIcon").objectReferenceValue = MakePowerIcon(PowerUpType.Glider);
        manager.FindProperty("magnetIcon").objectReferenceValue = MakePowerIcon(PowerUpType.Magnet);
        root.gameObject.SetActive(true);
        ring.gameObject.SetActive(false);
        icon.gameObject.SetActive(false);
        label.gameObject.SetActive(false);
    }

    static Sprite MakePowerIcon(PowerUpType type)
    {
        string path = Root + "UI/Icons/Power_" + type + ".png";
        var texture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
        for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
        {
            float px = x - 31.5f, py = y - 31.5f;
            Color color = Color.clear;
            if (type == PowerUpType.Glider)
            {
                float along = (px + py) * 0.7071f;
                float across = (py - px) * 0.7071f;
                if (Mathf.Abs(along) < 27f && Mathf.Abs(across) < 11f * (1f - Mathf.Abs(along) / 30f))
                    color = new Color(0.47f, 0.92f, 0.38f);
                if (Mathf.Abs(across) < 1.5f && Mathf.Abs(along) < 30f)
                    color = new Color(0.91f, 1f, 0.65f);
            }
            else
            {
                bool prong = (Mathf.Abs(px - 17f) < 6f || Mathf.Abs(px + 17f) < 6f) && py > -9f && py < 24f;
                float radius = new Vector2(px, py + 8f).magnitude;
                bool bend = py < -4f && radius > 11f && radius < 23f;
                if (prong || bend) color = new Color(0.75f, 0.55f, 1f);
                if (prong && py > 17f) color = new Color(1f, 0.93f, 0.7f);
            }
            texture.SetPixel(x, y, color);
        }
        texture.Apply();
        File.WriteAllBytes(path, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static void BuildGap()
    {
        var old = GameObject.Find("River Gap Module");
        if (old != null) Object.DestroyImmediate(old);
        var gap = new GameObject("River Gap Module");
        const float start = 610f, end = 642f;
        var trailMesh = AssetDatabase.LoadAssetAtPath<Mesh>(Root + "Art/Tropical/OrganicTrailSurface.asset");
        if (trailMesh != null)
        {
            trailMesh.Clear();
            trailMesh.vertices = new[] {
                new Vector3(-70,-0.295f,-60),new Vector3(-70,-0.295f,start),new Vector3(70,-0.295f,start),new Vector3(70,-0.295f,-60),
                new Vector3(-70,-0.295f,end),new Vector3(-70,-0.295f,945),new Vector3(70,-0.295f,945),new Vector3(70,-0.295f,end)
            };
            trailMesh.triangles = new[] { 0,1,2,0,2,3,4,5,6,4,6,7 };
            trailMesh.RecalculateNormals();
            trailMesh.RecalculateBounds();
            EditorUtility.SetDirty(trailMesh);
        }
        var ground = GameObject.Find("Ground");
        if (ground != null) ground.GetComponent<BoxCollider>().enabled = false;
        for (int i = 0; i < 2; i++)
        {
            float z0 = i == 0 ? -60 : end;
            float z1 = i == 0 ? start : 945;
            var section = new GameObject(i == 0 ? "Ground Before River" : "Landing Ground", typeof(BoxCollider));
            section.transform.SetParent(gap.transform);
            section.transform.position = new Vector3(0, -0.8f, (z0 + z1) * 0.5f);
            section.GetComponent<BoxCollider>().size = new Vector3(12, 1, z1 - z0);
        }
        Primitive("Turquoise River", gap.transform, PrimitiveType.Cube, new Vector3(0, -3.1f, (start + end) * 0.5f),
            new Vector3(52, 0.15f, end - start), Mat("RiverWater", "#3DADB9"));
        Primitive("Waterfall Face", gap.transform, PrimitiveType.Cube, new Vector3(0, -1.6f, end - 0.3f),
            new Vector3(26, 3f, 0.2f), Mat("WaterfallFoam", "#A8E6E1"));
        for (int side = -1; side <= 1; side += 2)
            Primitive("Riverbank Rock", gap.transform, PrimitiveType.Sphere, new Vector3(side * 8f, -0.2f, start + 5),
                new Vector3(7, 2.5f, 8), Mat("RiverStone", "#B6BBA8"));

        var zone = new GameObject("Vine Swing Trigger", typeof(BoxCollider), typeof(Rigidbody), typeof(VineSwingZone));
        zone.transform.SetParent(gap.transform);
        zone.transform.position = new Vector3(0, -0.3f, start - 3f);
        var box = zone.GetComponent<BoxCollider>();
        box.isTrigger = true;
        box.center = new Vector3(0, 1.5f, 0);
        box.size = new Vector3(11, 4, 5);
        zone.GetComponent<Rigidbody>().isKinematic = true;
        zone.GetComponent<Rigidbody>().useGravity = false;
        var landing = new GameObject("Safe Landing").transform;
        landing.SetParent(gap.transform);
        landing.position = new Vector3(0, -0.5f, end + 4f);
        var pivot = new GameObject("Overhead Vine Anchor").transform;
        pivot.SetParent(gap.transform);
        pivot.position = new Vector3(0, 8.5f, (start + end) * 0.5f);
        Vector3 grab = new Vector3(0, 2.5f, start - 3f);
        Vector3 vineDirection = pivot.position - grab;
        var hangingVine = Primitive("Visible Hanging Vine", gap.transform, PrimitiveType.Cylinder,
            (pivot.position + grab) * 0.5f,
            new Vector3(0.11f, vineDirection.magnitude * 0.5f, 0.11f), Mat("VineRope", "#8B672E"));
        hangingVine.transform.rotation = Quaternion.FromToRotation(Vector3.up, vineDirection);
        Primitive("Vine Canopy", gap.transform, PrimitiveType.Sphere, pivot.position,
            new Vector3(5, 0.8f, 3), Mat("PalmGreen", "#569C39"));
        var line = zone.AddComponent<LineRenderer>();
        line.positionCount = 2;
        line.startWidth = 0.15f;
        line.endWidth = 0.1f;
        line.sharedMaterial = Mat("VineRope", "#8B672E");
        line.enabled = false;
        var data = new SerializedObject(zone.GetComponent<VineSwingZone>());
        data.FindProperty("landingPoint").objectReferenceValue = landing;
        data.FindProperty("vinePivot").objectReferenceValue = pivot;
        data.FindProperty("vineLine").objectReferenceValue = line;
        data.ApplyModifiedPropertiesWithoutUndo();
    }
}
