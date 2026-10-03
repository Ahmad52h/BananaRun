using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using MaekoStudio.Flock;
using Object = UnityEngine.Object;

public static class DynamicHazardSetup
{
    const string Root = "Assets/_Game/";
    const string Folder = Root + "Prefabs/Course/";
    [MenuItem("Tools/Banana Run/Install Dynamic Jungle Hazards")]
    public static void Install()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (EditorApplication.isPlaying || scene.name != "Level01") throw new Exception("Open Level01 in edit mode.");
        var bird = Bird();
        var hog = Warthog();
        var boulder = Boulder();
        var course = Object.FindFirstObjectByType<RunnerCourseGenerator>();
        var data = new SerializedObject(course);
        var palette = data.FindProperty("obstaclePrefabs");
        var originals = Enumerable.Range(0, palette.arraySize).Select(i => palette.GetArrayElementAtIndex(i).objectReferenceValue)
            .Where(o => o != null && o.name != "LowFlyingStarling" && o.name != "ChargingWarthog" && o.name != "RollingBoulder").ToList();
        originals.AddRange(new Object[] { bird, hog, boulder });
        palette.arraySize = originals.Count;
        for (int i = 0; i < originals.Count; i++) palette.GetArrayElementAtIndex(i).objectReferenceValue = originals[i];
        data.ApplyModifiedPropertiesWithoutUndo();
        var power = Object.FindFirstObjectByType<PowerUpManager>();
        data = new SerializedObject(power);
        data.FindProperty("giantImpactPrefab").objectReferenceValue = Impact();
        data.ApplyModifiedPropertiesWithoutUndo();
        var camera = Object.FindFirstObjectByType<RunnerCamera>();
        camera.GetComponent<Camera>().fieldOfView = 60;
        data = new SerializedObject(camera);
        data.FindProperty("heavyStep").objectReferenceValue = Sound("GiantFootstep", false);
        data.ApplyModifiedPropertiesWithoutUndo();
        Flocks();
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Dynamic jungle hazards, Maeko flocks and giant camera/audio installed.");
    }
    static Material Material(string name, Color color)
    {
        string path = Root + "Materials/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) return mat;
        mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = color };
        mat.SetFloat("_Smoothness", 0.12f);
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }
    static GameObject Shape(string name, Transform parent, PrimitiveType type, Vector3 pos, Vector3 scale, Material mat)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name; go.transform.SetParent(parent, false);
        go.transform.localPosition = pos; go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }
    static GameObject Base(string name, Vector3 center, Vector3 size, DynamicRunnerHazard.Motion kind, float speed)
    {
        var go = new GameObject(name);
        var box = go.AddComponent<BoxCollider>(); box.isTrigger = true; box.center = center; box.size = size;
        var rb = go.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false;
        go.AddComponent<RunnerObstacle>();
        var motion = go.AddComponent<DynamicRunnerHazard>();
        var data = new SerializedObject(motion);
        data.FindProperty("motion").enumValueIndex = (int)kind;
        data.FindProperty("speed").floatValue = speed;
        data.ApplyModifiedPropertiesWithoutUndo();
        return go;
    }
    static void SetVisual(GameObject go, Transform visual, Transform[] legs = null)
    {
        var data = new SerializedObject(go.GetComponent<DynamicRunnerHazard>());
        data.FindProperty("visual").objectReferenceValue = visual;
        if (legs != null)
        {
            var array = data.FindProperty("legs"); array.arraySize = legs.Length;
            for (int i = 0; i < legs.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = legs[i];
        }
        data.ApplyModifiedPropertiesWithoutUndo();
    }
    static GameObject Save(GameObject go)
    {
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, Folder + go.name + ".prefab");
        Object.DestroyImmediate(go);
        return prefab;
    }
    static GameObject Bird()
    {
        var go = Base("LowFlyingStarling", new Vector3(0,1.48f,0), new Vector3(1.65f,0.8f,0.85f), DynamicRunnerHazard.Motion.Bird, 3.2f);
        var data = new SerializedObject(go.GetComponent<RunnerObstacle>());
        data.FindProperty("canSlideUnder").boolValue = true;
        data.ApplyModifiedPropertiesWithoutUndo();
        var pivot = new GameObject("Swooping Starling").transform; pivot.SetParent(go.transform, false);
        pivot.localPosition = new Vector3(0,1.48f,0);
        var model = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/MaekoFlock/Starling/Starling.prefab"), pivot);
        model.name = "Maeko Starling";
        model.transform.localRotation = Quaternion.Euler(0,180,0);
        var renderers = model.GetComponentsInChildren<Renderer>();
        var bounds = renderers[0].bounds;
        foreach (var r in renderers) bounds.Encapsulate(r.bounds);
        model.transform.localScale *= 1.7f / Mathf.Max(bounds.size.x, bounds.size.z);
        bounds = renderers[0].bounds;
        foreach (var r in renderers) bounds.Encapsulate(r.bounds);
        model.transform.position += pivot.position - bounds.center;
        foreach (var c in model.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
        model.GetComponentInChildren<Animator>().applyRootMotion = false;
        SetVisual(go, pivot);
        return Save(go);
    }
    static GameObject Warthog()
    {
        var go = Base("ChargingWarthog", new Vector3(0,0.52f,0), new Vector3(1.15f,1.04f,1.65f), DynamicRunnerHazard.Motion.Warthog, 1.7f);
        var visual = new GameObject("Warthog Visual").transform; visual.SetParent(go.transform, false);
        var hide = Material("WarthogHide", new Color(0.3f,0.21f,0.13f));
        var dark = Material("WarthogMane", new Color(0.09f,0.055f,0.025f));
        var ivory = Material("WarthogTusks", new Color(0.94f,0.86f,0.65f));
        Shape("Body",visual,PrimitiveType.Sphere,new Vector3(0,0.63f,0.13f),new Vector3(1.05f,0.72f,1.3f),hide);
        Shape("Head",visual,PrimitiveType.Sphere,new Vector3(0,0.64f,-0.57f),new Vector3(0.8f,0.62f,0.65f),hide);
        Shape("Snout",visual,PrimitiveType.Sphere,new Vector3(0,0.49f,-0.91f),new Vector3(0.48f,0.29f,0.2f),dark);
        var legs = new Transform[4];
        for (int i = 0; i < 4; i++)
            legs[i] = Shape("Leg " + i,visual,PrimitiveType.Capsule,new Vector3(i < 2 ? -0.34f : 0.34f,0.25f,i % 2 == 0 ? -0.33f : 0.48f),new Vector3(0.18f,0.22f,0.2f),dark).transform;
        for (int side = -1; side <= 1; side += 2)
        {
            Shape("Eye",visual,PrimitiveType.Sphere,new Vector3(side*0.32f,0.78f,-0.77f),Vector3.one*0.1f,dark);
            var ear = Shape("Ear",visual,PrimitiveType.Sphere,new Vector3(side*0.36f,0.99f,-0.38f),new Vector3(0.25f,0.29f,0.09f),hide);
            ear.transform.localRotation = Quaternion.Euler(-20,0,side*-25);
            var tusk = Shape("Tusk",visual,PrimitiveType.Capsule,new Vector3(side*0.37f,0.5f,-0.86f),new Vector3(0.095f,0.2f,0.095f),ivory);
            tusk.transform.localRotation = Quaternion.Euler(-25,0,side*-27);
        }
        for (int i=0;i<6;i++) Shape("Mane",visual,PrimitiveType.Cube,new Vector3(0,0.99f,-0.24f+i*0.16f),new Vector3(0.1f,0.16f,0.1f),dark);
        var tail = Shape("Tail",visual,PrimitiveType.Capsule,new Vector3(0,0.69f,0.92f),new Vector3(0.05f,0.2f,0.05f),dark);
        tail.transform.localRotation = Quaternion.Euler(50,0,0);
        SetVisual(go, visual, legs);
        return Save(go);
    }
    static GameObject Boulder()
    {
        var go = Base("RollingBoulder",new Vector3(0,0.65f,0),Vector3.one*1.3f,DynamicRunnerHazard.Motion.Boulder,2.5f);
        var visual = Shape("Rolling stone",go.transform,PrimitiveType.Sphere,new Vector3(0,0.65f,0),Vector3.one*1.3f,
            Material("RollingStone",new Color(0.38f,0.39f,0.33f)));
        var seams = Material("StoneSeams",new Color(0.2f,0.23f,0.18f));
        // Surface patches make no-slip rotation legible.
        for(int i=0;i<5;i++)
        {
            float a=i*1.2566f;
            Shape("Stone facet",visual.transform,PrimitiveType.Sphere,new Vector3(Mathf.Cos(a)*0.44f,Mathf.Sin(a)*0.44f,0.13f),
                new Vector3(0.2f,0.18f,0.16f),seams);
        }
        SetVisual(go,visual.transform);
        var rumble=go.AddComponent<AudioSource>(); rumble.clip=Sound("BoulderRumble",true);
        rumble.loop=true; rumble.playOnAwake=false; rumble.spatialBlend=1; rumble.volume=0.3f;
        rumble.minDistance=3; rumble.maxDistance=23; rumble.rolloffMode=AudioRolloffMode.Linear; rumble.dopplerLevel=0.2f;
        var data=new SerializedObject(go.GetComponent<DynamicRunnerHazard>()); data.FindProperty("rumble").objectReferenceValue=rumble; data.ApplyModifiedPropertiesWithoutUndo();
        return Save(go);
    }
    static GiantImpactEffect Impact()
    {
        string path=Root+"Prefabs/Effects/GiantImpact.prefab";
        var go=new GameObject("GiantImpact");
        var effect=go.AddComponent<GiantImpactEffect>();
        var data=new SerializedObject(effect);
        data.FindProperty("smash").objectReferenceValue=AssetDatabase.LoadAssetAtPath<AudioClip>(Root+"Audio/Smash rock sound.wav");
        data.ApplyModifiedPropertiesWithoutUndo();
        for(int i=0;i<2;i++)
        {
            var child=new GameObject(i==0 ? "Dust burst" : "Stone debris",typeof(ParticleSystem));
            child.transform.SetParent(go.transform,false);
            var ps=child.GetComponent<ParticleSystem>(); ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=ps.main; main.loop=false; main.playOnAwake=false; main.duration=0.2f;
            main.startLifetime=new ParticleSystem.MinMaxCurve(0.5f,1.3f);
            main.startSpeed=new ParticleSystem.MinMaxCurve(2,5); main.startSize=new ParticleSystem.MinMaxCurve(i==0?0.2f:0.07f,i==0?0.55f:0.18f);
            main.gravityModifier=i==0?0.15f:1f; main.maxParticles=40;
            main.startColor=i==0?new Color(0.68f,0.57f,0.39f):new Color(0.4f,0.4f,0.34f);
            var emission=ps.emission; emission.rateOverTime=0; emission.SetBursts(new[]{new ParticleSystem.Burst(0,(short)(i==0?22:14))});
            var shape=ps.shape; shape.shapeType=ParticleSystemShapeType.Hemisphere; shape.radius=0.35f;
            child.transform.localRotation=Quaternion.Euler(-90,0,0);
            var size=ps.sizeOverLifetime; size.enabled=true; size.size=new ParticleSystem.MinMaxCurve(1,new AnimationCurve(new Keyframe(0,1),new Keyframe(1,0)));
            var renderer=ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode=ParticleSystemRenderMode.Mesh;
            var temp=GameObject.CreatePrimitive(i==0?PrimitiveType.Sphere:PrimitiveType.Cube);
            renderer.mesh=temp.GetComponent<MeshFilter>().sharedMesh; Object.DestroyImmediate(temp);
            renderer.sharedMaterial=Material(i==0?"ImpactDust":"ImpactStone",i==0?new Color(0.68f,0.57f,0.39f):new Color(0.4f,0.4f,0.34f));
        }
        var prefab=PrefabUtility.SaveAsPrefabAsset(go,path); Object.DestroyImmediate(go); return prefab.GetComponent<GiantImpactEffect>();
    }
    static void Flocks()
    {
        var root=GameObject.Find("Jungle Bird Flocks");
        if(root!=null) Object.DestroyImmediate(root);
        root=new GameObject("Jungle Bird Flocks");
        var player=Object.FindFirstObjectByType<RunnerMotor>();
        for(int i=0;i<2;i++)
        {
            var go=new GameObject(i==0?"Canopy Murmuration":"River Crossing Murmuration");
            go.transform.SetParent(root.transform);
            go.transform.position=new Vector3(0,i==0?19:17,i==0?40:626);
            var flock=go.AddComponent<MaekoFlock>();
            flock.birdPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/MaekoFlock/Starling/Starling.prefab");
            flock.anchor=go.transform; flock.count=i==0?24:32; flock.boundsRadius=18; flock.blobRadius=4;
            flock.birdSpacing=1.2f; flock.floorBelowAnchor=4; flock.arriveFromFar=false;
            flock.swirlRadius=12; flock.obstacleMask=0; flock.minSpeed=5; flock.maxSpeed=10;
            if(i==0)
            {
                var follow=go.AddComponent<CanopyFlockFollow>(); var data=new SerializedObject(follow);
                data.FindProperty("player").objectReferenceValue=player; data.ApplyModifiedPropertiesWithoutUndo();
            }
            else
            {
                foreach(var position in new[]{new Vector3(-18,17,619),new Vector3(18,20,635),new Vector3(10,18,643),new Vector3(-15,18,626)})
                {
                    var point=new GameObject("River flight waypoint").transform; point.SetParent(root.transform); point.position=position;
                    flock.patrolPoints.Add(point);
                }
                flock.patrolSpeed=7;
            }
        }
    }
    static AudioClip Sound(string name,bool loop)
    {
        string path=Root+"Audio/"+name+".wav";
        if(!File.Exists(path))
        {
            const int rate=22050; int count=loop?rate*2:rate/3;
            var random=new System.Random(138);
            using(var w=new BinaryWriter(File.Create(path)))
            {
                w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));w.Write(36+count*2);w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
                w.Write(16);w.Write((short)1);w.Write((short)1);w.Write(rate);w.Write(rate*2);w.Write((short)2);w.Write((short)16);
                w.Write(System.Text.Encoding.ASCII.GetBytes("data"));w.Write(count*2);
                float noise=0;
                for(int i=0;i<count;i++)
                {
                    float t=i/(float)rate;
                    noise=Mathf.Lerp(noise,(float)random.NextDouble()*2-1,0.035f);
                    float envelope=loop?Mathf.Min(1,Mathf.Min(i,count-1-i)/300f):Mathf.Exp(-t*17)*Mathf.Min(1,t*250);
                    float sample=(Mathf.Sin(2*Mathf.PI*(loop?48:58)*t)*0.4f+noise*0.6f)*envelope;
                    w.Write((short)(sample*28000));
                }
            }
            AssetDatabase.ImportAsset(path);
        }
        return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
    }
}
