using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class RottenBananaBombSetup
{
    private const string Root = "Assets/_Game/";
    public const string TrapPath = Root + "Prefabs/PowerUps/RottenBananaBomb.prefab";
    public const string PickupPath = Root + "Prefabs/PowerUps/BombPickup.prefab";
    public const string EffectPath = Root + "Prefabs/Effects/FX_RottenBananaBomb.prefab";

    [MenuItem("Tools/Banana Run/Install Secondary Rotten Banana Bomb")]
    public static void Install()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (EditorApplication.isPlaying || scene.name != "Level01")
            throw new InvalidOperationException("Open Level01 in edit mode first.");
        var player = Object.FindFirstObjectByType<RunnerMotor>();
        var course = Object.FindFirstObjectByType<RunnerCourseGenerator>();
        var hud = Object.FindFirstObjectByType<RunnerHUD>();
        if (player == null || course == null || hud == null) throw new InvalidOperationException("Missing runner, course, or HUD.");
        var model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/BTM_Assets/BTM_Items_Gems/Prefabs/Bomb.prefab");
        if (model == null) model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/BTM_Assets/BTM_Items_Gems/Models/Bomb.fbx");
        if (model == null) throw new InvalidOperationException("The supplied BTM Bomb model is missing.");

        var effect = MakeEffect();
        var trap = MakeItem(model, false, effect).GetComponent<RottenBananaBomb>();
        var pickup = MakeItem(model, true, null);
        var dropper = player.GetComponent<BombDropper>() ?? Undo.AddComponent<BombDropper>(player.gameObject);
        var slot = new SerializedObject(dropper);
        slot.FindProperty("trapPrefab").objectReferenceValue = trap;
        slot.ApplyModifiedProperties();
        var generator = new SerializedObject(course);
        generator.FindProperty("bombPickupPrefab").objectReferenceValue = pickup;
        generator.ApplyModifiedProperties();

        MakeHud(hud, dropper, MakeIcon());
        ConfigureAudio();
        ConfigureChaserReaction(player);
        // An early pickup makes the new slot discoverable before the generated course begins.
        if (GameObject.Find("Intro Bomb Pickup") == null)
        {
            var intro = (GameObject)PrefabUtility.InstantiatePrefab(pickup);
            Undo.RegisterCreatedObjectUndo(intro, "Add introductory bomb pickup");
            intro.name = "Intro Bomb Pickup";
            intro.transform.position = new Vector3(player.transform.position.x, -0.3f, player.transform.position.z + 24f);
            intro.transform.position += Vector3.up * (0.8f - intro.GetComponent<SphereCollider>().center.y);
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("Rotten Banana Bomb installed as a secondary slot. Drop with B or DROP BOMB; coconut controls unchanged.");
    }

    private static GameObject MakeItem(GameObject source, bool pickup, GameObject effect)
    {
        var root = new GameObject(pickup ? "Bomb Pickup" : "Rotten Banana Bomb");
        try
        {
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(source);
            visual.name = "BTM Bomb Visual";
            visual.transform.SetParent(root.transform, false);
            foreach (var animation in visual.GetComponentsInChildren<MonoBehaviour>(true)) animation.enabled = false;
            foreach (var collider in visual.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
            foreach (var body in visual.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(body);
            Bounds bounds = BoundsOf(visual);
            visual.transform.localScale *= 0.8f / Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            bounds = BoundsOf(visual);
            visual.transform.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            var sphere = root.AddComponent<SphereCollider>();
            sphere.isTrigger = true;
            sphere.center = Vector3.up * 0.38f;
            sphere.radius = pickup ? 0.65f : 0.6f;
            var rigidbody = root.AddComponent<Rigidbody>();
            rigidbody.isKinematic = true;
            rigidbody.useGravity = false;
            if (pickup) root.AddComponent<BombPickup>();
            else
            {
                var trap = root.AddComponent<RottenBananaBomb>();
                var data = new SerializedObject(trap);
                data.FindProperty("explosionPrefab").objectReferenceValue = effect;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            return PrefabUtility.SaveAsPrefabAsset(root, pickup ? PickupPath : TrapPath);
        }
        finally { Object.DestroyImmediate(root); }
    }

    private static Bounds BoundsOf(GameObject visual)
    {
        var renderers = visual.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) throw new InvalidOperationException("Bomb model has no renderer.");
        var bounds = renderers[0].bounds;
        foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
        return bounds;
    }

    private static GameObject MakeEffect()
    {
        var root = new GameObject("Rotten Banana Dust Explosion");
        try
        {
            var particles = root.AddComponent<ParticleSystem>();
            var main = particles.main;
            main.duration = 1.2f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.3f, 4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.85f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.32f, 0.31f, 0.19f, 0.85f), new Color(0.7f, 0.65f, 0.42f, 0.7f));
            main.gravityModifier = -0.15f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = particles.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 36) });
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 0.35f;
            var color = particles.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0.85f, 0), new GradientAlphaKey(0, 1) });
            color.color = gradient;
            particles.GetComponent<ParticleSystemRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Root + "Materials/ImpactDust.mat");
            var effect = root.AddComponent<GiantImpactEffect>();
            var data = new SerializedObject(effect);
            // Bomb audio belongs to RunnerAudio's single voice, so particles never add a second sound.
            data.FindProperty("smash").objectReferenceValue = null;
            data.ApplyModifiedPropertiesWithoutUndo();
            return PrefabUtility.SaveAsPrefabAsset(root, EffectPath);
        }
        finally { Object.DestroyImmediate(root); }
    }

    private static Sprite MakeIcon()
    {
        const string path = Root + "UI/Icons/bomb_icon_unity.png";
        if (!File.Exists(path)) throw new InvalidOperationException("The supplied bomb HUD sprite is missing.");
        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null) throw new InvalidOperationException("Bomb icon did not import as a sprite.");
        return sprite;
    }

    private static void ConfigureAudio()
    {
        var audio = Object.FindFirstObjectByType<RunnerAudio>();
        if (audio == null) throw new InvalidOperationException("Scene audio is missing.");
        var data = new SerializedObject(audio);
        foreach (var cue in new[] { ("BombDrop.wav", "bombDropSound"), ("BombExplosion.wav", "bombExplosionSound") })
        {
            string path = Root + "Audio/" + cue.Item1;
            AssetDatabase.ImportAsset(path);
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer == null) throw new InvalidOperationException("Run Tools/prepare_bomb_audio.py to prepare bomb cues.");
            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.PCM;
            settings.preloadAudioData = true;
            importer.defaultSampleSettings = settings;
            importer.loadInBackground = false;
            importer.forceToMono = true;
            importer.SaveAndReimport();
            data.FindProperty(cue.Item2).objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }
        data.ApplyModifiedProperties();
    }

    private static void ConfigureChaserReaction(RunnerMotor player)
    {
        var chaser = Object.FindFirstObjectByType<GiantGorillaChaser>();
        if (chaser == null) throw new InvalidOperationException("Gorilla chaser is missing.");
        var playerController = (AnimatorController)player.GetComponentInChildren<Animator>().runtimeAnimatorController;
        var playerHit = playerController.layers[0].stateMachine.states.First(s => s.state.name == "Hit").state;
        var controller = (AnimatorController)new SerializedObject(chaser).FindProperty("runController").objectReferenceValue;
        var machine = controller.layers[0].stateMachine;
        var hit = machine.states.FirstOrDefault(s => s.state.name == "Hit").state;
        if (hit == null) hit = machine.AddState("Hit");
        hit.motion = playerHit.motion;
        hit.speed = 1;
        hit.speedParameter = "StumbleSpeed";
        hit.speedParameterActive = true;
        if (!controller.parameters.Any(p => p.name == "StumbleSpeed"))
            controller.AddParameter("StumbleSpeed", AnimatorControllerParameterType.Float);
        var parameters = controller.parameters;
        foreach (var parameter in parameters) if (parameter.name == "StumbleSpeed") parameter.defaultFloat = 1;
        controller.parameters = parameters;
        EditorUtility.SetDirty(controller);
    }

    private static void MakeHud(RunnerHUD hud, BombDropper dropper, Sprite icon)
    {
        var data = new SerializedObject(hud);
        var coconut = (RectTransform)data.FindProperty("coconutBadge").objectReferenceValue;
        var throwButton = (Button)data.FindProperty("throwButton").objectReferenceValue;
        var font = ((TMP_Text)data.FindProperty("coconutCountText").objectReferenceValue).font;
        var parent = coconut.parent;
        var badge = parent.Find("Bomb Inventory") as RectTransform;
        if (badge == null)
        {
            badge = new GameObject("Bomb Inventory", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            Undo.RegisterCreatedObjectUndo(badge.gameObject, "Add bomb inventory badge");
            badge.SetParent(parent, false);
        }
        badge.anchorMin = badge.anchorMax = badge.pivot = new Vector2(0, 1);
        badge.anchoredPosition = coconut.anchoredPosition + Vector2.right * (coconut.sizeDelta.x + 10);
        badge.sizeDelta = new Vector2(180, 100);
        var background = badge.GetComponent<Image>();
        background.sprite = coconut.GetComponent<Image>().sprite;
        background.type = Image.Type.Sliced;
        background.color = coconut.GetComponent<Image>().color;
        background.raycastTarget = false;
        ImageAt(badge, "Bomb Icon", icon, new Vector2(0.28f, 0.5f), new Vector2(78, 78));
        var counter = Label(badge, "Bomb Count", "x0", font, new Vector2(0.72f, 0.5f), new Vector2(78, 80), 40);

        var buttonRect = parent.Find("Drop Bomb Button") as RectTransform;
        if (buttonRect == null)
        {
            buttonRect = new GameObject("Drop Bomb Button", typeof(RectTransform), typeof(Image), typeof(Button)).GetComponent<RectTransform>();
            Undo.RegisterCreatedObjectUndo(buttonRect.gameObject, "Add secondary bomb button");
            buttonRect.SetParent(parent, false);
        }
        var original = (RectTransform)throwButton.transform;
        buttonRect.anchorMin = buttonRect.anchorMax = buttonRect.pivot = new Vector2(1, 0);
        buttonRect.anchoredPosition = original.anchoredPosition + Vector2.left * (original.sizeDelta.x + 20);
        buttonRect.sizeDelta = original.sizeDelta;
        buttonRect.localRotation = original.localRotation;
        var image = buttonRect.GetComponent<Image>();
        var coconutBackground = throwButton.GetComponent<Image>();
        image.sprite = coconutBackground.sprite;
        image.type = coconutBackground.type;
        image.preserveAspect = coconutBackground.preserveAspect;
        image.color = coconutBackground.color;
        image.raycastTarget = true;
        var button = buttonRect.GetComponent<Button>();
        button.targetGraphic = image;
        button.transition = throwButton.transition;
        button.colors = throwButton.colors;
        button.spriteState = throwButton.spriteState;
        button.onClick = new Button.ButtonClickedEvent();
        UnityEventTools.AddPersistentListener(button.onClick, dropper.Drop);
        var coconutIcon = throwButton.transform.Find("Coconut Action Icon").GetComponent<Image>();
        ImageAt(buttonRect, "Bomb Action Icon", icon, new Vector2(0.5f, 0.5f), coconutIcon.rectTransform.sizeDelta);
        var oldLabel = buttonRect.Find("Drop Label");
        if (oldLabel != null) oldLabel.gameObject.SetActive(false);
        if (button.GetComponent<MenuButtonFeedback>() == null) button.gameObject.AddComponent<MenuButtonFeedback>();
        data.FindProperty("bombDropper").objectReferenceValue = dropper;
        data.FindProperty("bombCountText").objectReferenceValue = counter;
        data.FindProperty("dropBombButton").objectReferenceValue = button;
        data.ApplyModifiedProperties();
    }

    private static Image ImageAt(Transform parent, string name, Sprite sprite, Vector2 anchor, Vector2 size)
    {
        var image = parent.Find(name)?.GetComponent<Image>();
        if (image == null) image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        image.transform.SetParent(parent, false);
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        Place(image.rectTransform, anchor, size);
        return image;
    }

    private static TMP_Text Label(Transform parent, string name, string text, TMP_FontAsset font, Vector2 anchor, Vector2 size, float fontSize)
    {
        var label = parent.Find(name)?.GetComponent<TMP_Text>();
        if (label == null) label = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
        label.transform.SetParent(parent, false);
        label.text = text;
        label.font = font;
        label.fontSize = fontSize;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.color = parent.name == "Bomb Inventory" ? new Color(1, 0.95f, 0.78f) : new Color(0.12f, 0.23f, 0.12f);
        label.raycastTarget = false;
        Place(label.rectTransform, anchor, size);
        return label;
    }

    private static void Place(RectTransform rect, Vector2 anchor, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = size;
    }
}
