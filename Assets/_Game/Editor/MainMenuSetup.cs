using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Authors an ordinary, editable scene using the game's existing art.</summary>
public static class MainMenuSetup
{
    const string ScenePath = "Assets/_Game/Scenes/MainMenu.unity";
    static readonly Color Cream = Hex("FFF4C9"), Ink = Hex("173E32"), Gold = Hex("FFD449");
    static Sprite pill;
    static TMP_FontAsset font;
    static Color Hex(string value) { ColorUtility.TryParseHtmlString("#" + value, out var c); return c; }

    [MenuItem("Banana Run/Create Main Menu")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Exit Play mode first.");
        var source = SceneManager.GetActiveScene();
        if (source.name != "Level01") source = EditorSceneManager.OpenScene("Assets/_Game/Scenes/Level01.unity", OpenSceneMode.Additive);
        var originalMonkey = source.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<Animator>(true)).First(x => x.name == "MonkeyVisual");
        var originalAudio = source.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<RunnerAudio>(true)).First();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        pill = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Game/UI/Icons/TropicalPill.png");
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        var environment = new GameObject("Jungle Clearing").transform;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = Hex("B4D4AC");
        RenderSettings.fog = true; RenderSettings.fogColor = Hex("70BCAB");
        RenderSettings.fogMode = FogMode.Linear; RenderSettings.fogStartDistance = 22; RenderSettings.fogEndDistance = 65;
        var camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
        camera.tag = "MainCamera"; camera.transform.position = new Vector3(0, 3.6f, -10);
        camera.transform.LookAt(new Vector3(0, 1.9f, 0)); camera.fieldOfView = 40; camera.farClipPlane = 85;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Hex("85D5CE");
        var sun = new GameObject("Warm Jungle Sun", typeof(Light)).GetComponent<Light>();
        sun.type = LightType.Directional; sun.transform.rotation = Quaternion.Euler(42, -35, 0);
        sun.color = Hex("FFF0C3"); sun.intensity = 1.35f; sun.shadows = LightShadows.Soft;
        Primitive("Grassy Clearing", environment, PrimitiveType.Cube, new Vector3(0,-0.3f,15), new Vector3(80,0.4f,100), "PalmGreen");
        Primitive("Sandy Trail", environment, PrimitiveType.Cube, new Vector3(0,-0.085f,16), new Vector3(3.7f,0.04f,60), "CutWood");
        Primitive("Monkey Stage", environment, PrimitiveType.Cylinder, new Vector3(0,-0.04f,0), new Vector3(3.1f,0.1f,2.6f), "CutWood");
        for (int i = 0; i < 6; i++)
        {
            float z = 2 + i * 5;
            for (int side = -1; side <= 1; side += 2)
            {
                Prop("TropicalPalm", "Palm " + i + (side < 0 ? " Left" : " Right"), environment, new Vector3(side*(3.3f+i*.35f),0,z), 1f+i*.08f, side*35+i*31);
                Prop("Border" + i%3, "Jungle Understory " + i + " " + side, environment, new Vector3(side*3.5f,0,z), 1.2f, i*45);
            }
        }
        Prop("DaisyPatch", "Flowers Left", environment, new Vector3(-1.8f,0,-1), 1.1f, 15);
        Prop("DaisyPatch", "Flowers Right", environment, new Vector3(2,0,1), 1.4f, 95);
        var monkey = Object.Instantiate(originalMonkey.gameObject);
        SceneManager.MoveGameObjectToScene(monkey, scene); monkey.name = "Monkey - Idle Showcase";
        monkey.transform.SetPositionAndRotation(new Vector3(0,0.07f,0), Quaternion.Euler(0,180,0));
        monkey.transform.localScale = Vector3.one * 1.25f;
        foreach (var script in monkey.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(script);
        var animator = monkey.GetComponent<Animator>(); animator.applyRootMotion = false;
        var idle = animator.runtimeAnimatorController.animationClips.First(x=>x.name.ToLowerInvariant().Contains("idle"));
        idle.SampleAnimation(monkey, 0);
        var audio = Object.Instantiate(originalAudio.gameObject); audio.name = "Menu Audio"; SceneManager.MoveGameObjectToScene(audio,scene);
        var canvas = new GameObject("Menu Canvas", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
        canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>(); scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080,1920); scaler.matchWidthOrHeight = 0;
        var safe = Rect("Safe Area", canvas.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        safe.gameObject.AddComponent<SafeAreaUI>();
        var controller = new GameObject("Main Menu Controller").AddComponent<MainMenuController>();
        Text("Adventure Label", safe, "THE JUNGLE IS CALLING", 27, Cream, new Vector2(.5f,.94f), new Vector2(850,65));
        var title = Text("Banana Title", safe, "BANANA", 148, Gold, new Vector2(.5f,.855f), new Vector2(1020,190));
        title.characterSpacing = -7; title.transform.localEulerAngles = new Vector3(0,0,-4);
        Shadow(title.gameObject, Hex("623B16"), new Vector2(0,-9));
        var run = Text("Run Title", safe, "RUN", 126, Cream, new Vector2(.5f,.777f), new Vector2(850,155));
        run.characterSpacing = 13; Shadow(run.gameObject, Ink, new Vector2(0,-8));
        Text("Tagline", safe, "GO BANANAS. GO WILD.", 27, Ink, new Vector2(.5f,.703f), new Vector2(850,55));
        var play = Button("Play Button", safe, "PLAY", new Vector2(.5f,.235f), new Vector2(790,162), Gold, Ink, 76, controller.Play);
        Set(play.GetComponent<MenuButtonFeedback>(), "breathe", true);
        Text("Play Hint", safe, "A little monkey. A big adventure.", 28, Cream, new Vector2(.5f,.158f), new Vector2(950,50));
        Button("Character Button", safe, "CHARACTER", new Vector2(.29f,.085f), new Vector2(400,108), Ink, Cream, 31, controller.OpenCharacter);
        Button("Settings Button", safe, "SETTINGS", new Vector2(.71f,.085f), new Vector2(400,108), Ink, Cream, 31, controller.OpenSettings);
        var settings = Modal("Settings Popup", safe, "SETTINGS", new Vector2(900,900));
        var content = settings.transform.Find("Card");
        Text("Subtitle", content, "Make the jungle your own", 29, Ink, new Vector2(.5f,.79f), new Vector2(800,65));
        var music = Slider("Music", content, .63f);
        var sfx = Slider("SFX", content, .43f);
        Text("Vibration Label", content, "VIBRATION", 33, Ink, new Vector2(.31f,.255f), new Vector2(400,70));
        var toggleRect = Panel("Vibration Toggle", content, new Vector2(.77f,.255f), new Vector2(125,75), Ink);
        var toggle = toggleRect.gameObject.AddComponent<UnityEngine.UI.Toggle>(); toggle.targetGraphic = toggleRect.GetComponent<UnityEngine.UI.Image>();
        var check = Panel("Enabled", toggleRect, new Vector2(.5f,.5f), new Vector2(95,48), Gold);
        toggle.graphic = check.GetComponent<UnityEngine.UI.Image>(); toggle.isOn = true;
        toggle.targetGraphic.raycastTarget = true;
        Button("Close Settings", content, "DONE", new Vector2(.5f,.105f), new Vector2(690,100), Gold, Ink, 36, controller.CloseSettings);
        var settingsController = settings.AddComponent<SettingsPanelController>();
        Set(settingsController,"musicSlider",music); Set(settingsController,"sfxSlider",sfx); Set(settingsController,"vibrationToggle",toggle);
        settings.SetActive(false);
        var character = Modal("Character Popup", safe, "YOUR RUNNER", new Vector2(900,650));
        var characterContent = character.transform.Find("Card");
        Text("Name", characterContent, "ORIGINAL MONKEY", 48, Ink, new Vector2(.5f,.61f), new Vector2(830,95));
        Text("Status", characterContent, "Ready for a jungle adventure!\nYour original runner is selected.", 32, Ink, new Vector2(.5f,.40f), new Vector2(790,145));
        Button("Close Character",characterContent,"LET'S GO",new Vector2(.5f,.16f),new Vector2(690,110),Gold,Ink,38,controller.CloseCharacter);
        character.SetActive(false);
        var fade = Rect("Scene Fade", canvas.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var fadeImage = fade.gameObject.AddComponent<UnityEngine.UI.Image>(); fadeImage.color = Ink;
        var fadeGroup = fade.gameObject.AddComponent<CanvasGroup>(); fadeGroup.alpha = 0; fadeGroup.blocksRaycasts = false;
        Set(controller,"monkey",animator); Set(controller,"settingsPanel",settings); Set(controller,"characterPanel",character);
        Set(controller,"fade",fadeGroup); Set(controller,"playButton",play);
        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
        PlayerSettings.allowedAutorotateToLandscapeLeft = false; PlayerSettings.allowedAutorotateToLandscapeRight = false;
        Polish();
        EditorSceneManager.SaveScene(scene,ScenePath);
        EditorBuildSettings.scenes = new[] {new EditorBuildSettingsScene(ScenePath,true)}.Concat(EditorBuildSettings.scenes.Where(x=>x.path!=ScenePath)).ToArray();
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
        EditorSceneManager.CloseScene(source,true);
        AssetDatabase.SaveAssets();
    }
    public static void Polish()
    {
        const string spritePath = "Assets/_Game/UI/MenuRoundedPanel.asset";
        var rounded = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
        if (rounded == null)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Game/UI/Icons/TropicalPill.png");
            rounded = Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,new Vector4(32,31,32,31));
            rounded.name="Menu Rounded Panel"; AssetDatabase.CreateAsset(rounded,spritePath);
        }
        var canvas = GameObject.Find("Menu Canvas");
        foreach(var image in canvas.GetComponentsInChildren<UnityEngine.UI.Image>(true))
            if(image.sprite != null && image.sprite.texture.name == "TropicalPill")
            { image.sprite=rounded; image.pixelsPerUnitMultiplier = image.name == "Card" ? 1 : .65f; }
        foreach(var slider in canvas.GetComponentsInChildren<UnityEngine.UI.Slider>(true))
            slider.handleRect.sizeDelta = new Vector2(68,-17);
        const string titlePath="Assets/_Game/UI/MenuTitle.mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(titlePath);
        if(material == null)
        {
            material=new Material(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset").material);
            material.name="Banana Run Title";
            material.SetFloat(ShaderUtilities.ID_FaceDilate,.18f);
            material.SetFloat(ShaderUtilities.ID_OutlineWidth,.18f);
            material.SetColor(ShaderUtilities.ID_OutlineColor,Hex("62421C"));
            material.EnableKeyword("OUTLINE_ON");
            material.EnableKeyword("UNDERLAY_ON");
            material.SetColor(ShaderUtilities.ID_UnderlayColor,Hex("3C391A"));
            material.SetFloat(ShaderUtilities.ID_UnderlayOffsetY,-.7f);
            AssetDatabase.CreateAsset(material,titlePath);
        }
        foreach(string name in new[]{"Banana Title","Run Title"})
        {
            var label=GameObject.Find(name).GetComponent<TextMeshProUGUI>(); label.fontSharedMaterial=material;
            var shadow=label.GetComponent<UnityEngine.UI.Shadow>(); if(shadow) Object.DestroyImmediate(shadow);
        }
        var safe=canvas.transform.Find("Safe Area");
        foreach(int side in new[]{-1,1})
        {
            string name=side<0?"Banana Emblem Left":"Banana Emblem Right";
            if(safe.Find(name)) continue;
            var r=Rect(name,safe,new Vector2(.5f+side*.27f,.777f),new Vector2(.5f+side*.27f,.777f),Vector2.zero,new Vector2(100,100));
            var icon=r.gameObject.AddComponent<UnityEngine.UI.Image>(); icon.sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Game/UI/Icons/Icon_Banana.png.png");
            icon.preserveAspect=true; icon.raycastTarget=false; r.localEulerAngles=new Vector3(0,0,side*18);
            r.SetSiblingIndex(3);
        }
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }
    static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max, Vector2 position, Vector2 size)
    {
        var r = new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>(); r.SetParent(parent,false);
        r.anchorMin=min; r.anchorMax=max; r.anchoredPosition=position; r.sizeDelta=size; return r;
    }
    static RectTransform Panel(string name,Transform parent,Vector2 anchor,Vector2 size,Color color)
    {
        var r=Rect(name,parent,anchor,anchor,Vector2.zero,size); var image=r.gameObject.AddComponent<UnityEngine.UI.Image>();
        image.sprite=pill; image.type=UnityEngine.UI.Image.Type.Sliced; image.color=color; image.raycastTarget=false; return r;
    }
    static TextMeshProUGUI Text(string name,Transform parent,string value,float size,Color color,Vector2 anchor,Vector2 dimensions)
    {
        var r=Rect(name,parent,anchor,anchor,Vector2.zero,dimensions); var text=r.gameObject.AddComponent<TextMeshProUGUI>();
        text.font=font; text.text=value; text.fontSize=size; text.fontStyle=FontStyles.Bold; text.color=color;
        text.alignment=TextAlignmentOptions.Center; text.raycastTarget=false; return text;
    }
    static void Shadow(GameObject go,Color color,Vector2 offset)
    { var shadow=go.AddComponent<UnityEngine.UI.Shadow>(); shadow.effectColor=color; shadow.effectDistance=offset; }
    static UnityEngine.UI.Button Button(string name,Transform parent,string label,Vector2 anchor,Vector2 size,Color background,Color foreground,float fontSize,UnityEngine.Events.UnityAction action)
    {
        var r=Panel(name,parent,anchor,size,background); var image=r.GetComponent<UnityEngine.UI.Image>(); image.raycastTarget=true;
        Shadow(r.gameObject,new Color(.04f,.15f,.10f,.65f),new Vector2(0,-10));
        var b=r.gameObject.AddComponent<UnityEngine.UI.Button>(); b.targetGraphic=image;
        var colors=b.colors; colors.pressedColor=Hex("D4D4C0"); colors.highlightedColor=Color.white; b.colors=colors;
        UnityEventTools.AddPersistentListener(b.onClick,action); r.gameObject.AddComponent<MenuButtonFeedback>();
        Text("Label",r,label,fontSize,foreground,new Vector2(.5f,.5f),size-new Vector2(35,12)); return b;
    }
    static GameObject Modal(string name,Transform parent,string title,Vector2 size)
    {
        var root=Rect(name,parent,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
        var blocker=root.gameObject.AddComponent<UnityEngine.UI.Image>(); blocker.color=new Color(.025f,.10f,.08f,.82f);
        var card=Panel("Card",root,new Vector2(.5f,.5f),size,Cream); card.GetComponent<UnityEngine.UI.Image>().raycastTarget=true;
        Text("Title",card,title,64,Ink,new Vector2(.5f,.90f),new Vector2(850,95)); return root.gameObject;
    }
    static UnityEngine.UI.Slider Slider(string name,Transform parent,float y)
    {
        Text(name+" Label",parent,name.ToUpperInvariant(),33,Ink,new Vector2(.5f,y+.07f),new Vector2(720,55));
        var root=Rect(name+" Slider",parent,new Vector2(.5f,y-.015f),new Vector2(.5f,y-.015f),Vector2.zero,new Vector2(710,85));
        var hit=root.gameObject.AddComponent<UnityEngine.UI.Image>(); hit.color=Color.clear;
        Panel("Track",root,new Vector2(.5f,.5f),new Vector2(710,26),Hex("B9CDB1"));
        var fillArea=Rect("Fill Area",root,Vector2.zero,Vector2.one,Vector2.zero,new Vector2(-40,-59));
        var fill=Panel("Fill",fillArea,new Vector2(.5f,.5f),Vector2.zero,Ink);
        var handleArea=Rect("Handle Area",root,Vector2.zero,Vector2.one,Vector2.zero,new Vector2(-40,0));
        var handle=Panel("Handle",handleArea,new Vector2(.5f,.5f),new Vector2(68,68),Gold);
        var slider=root.gameObject.AddComponent<UnityEngine.UI.Slider>(); slider.fillRect=fill; slider.handleRect=handle;
        slider.targetGraphic=handle.GetComponent<UnityEngine.UI.Image>(); slider.value=1; return slider;
    }
    static void Set(Object target,string field,Object value) { var so=new SerializedObject(target); so.FindProperty(field).objectReferenceValue=value; so.ApplyModifiedPropertiesWithoutUndo(); }
    static void Set(Object target,string field,bool value) { var so=new SerializedObject(target); so.FindProperty(field).boolValue=value; so.ApplyModifiedPropertiesWithoutUndo(); }
    static void Primitive(string name,Transform parent,PrimitiveType type,Vector3 position,Vector3 scale,string material)
    {
        var go=GameObject.CreatePrimitive(type); go.name=name; go.transform.SetParent(parent,false); go.transform.localPosition=position; go.transform.localScale=scale;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.GetComponent<Renderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Materials/Tropical/"+material+".mat");
    }
    static void Prop(string asset,string name,Transform parent,Vector3 position,float scale,float rotation)
    {
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Tropical/"+asset+".prefab");
        var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,parent); go.name=name; go.transform.localPosition=position;
        go.transform.localScale=Vector3.one*scale; go.transform.localRotation=Quaternion.Euler(0,rotation,0);
    }
}
