using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class VineSwingSetup
{
    const string Root = "Assets/_Game/";
    [MenuItem("Tools/Banana Run/Configure Polished Vine Swing")]
    public static void Install()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (EditorApplication.isPlaying || scene.name != "Level01") throw new InvalidOperationException("Open Level01 in edit mode.");
        string path = Root + "Animations/VineSwing.fbx";
        var importer = (ModelImporter)AssetImporter.GetAtPath(path);
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var description = AssetDatabase.LoadAllAssetsAtPath(Root + "Art/Monkey/Monkey.fbx").OfType<Avatar>().First().humanDescription;
        description.skeleton = source.GetComponentsInChildren<Transform>(true).Select(t => new SkeletonBone
            { name = t.name, position = t.localPosition, rotation = t.localRotation, scale = t.localScale }).ToArray();
        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.sourceAvatar = null;
        importer.humanDescription = description;
        var clips = importer.clipAnimations.Length > 0 ? importer.clipAnimations : importer.defaultClipAnimations;
        foreach (var clip in clips)
        {
            clip.name = "MonkeyVineSwing";
            clip.loopTime = true;
            clip.loopPose = true;
            clip.lockRootRotation = true;
            clip.keepOriginalOrientation = true;
            clip.lockRootHeightY = true;
            clip.keepOriginalPositionY = true;
            clip.lockRootPositionXZ = true;
        }
        importer.clipAnimations = clips;
        importer.SaveAndReimport();
        var motion = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c => !c.name.StartsWith("__"));
        if (!motion.humanMotion) throw new Exception("Vine clip must import as humanoid.");
        var ac = AssetDatabase.LoadAssetAtPath<AnimatorController>(Root + "Animations/AC_Monkey.controller");
        if (!ac.parameters.Any(p => p.name == "VineAttached")) ac.AddParameter("VineAttached", AnimatorControllerParameterType.Bool);
        var sm = ac.layers[0].stateMachine;
        var vine = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == "VineSwing") ?? sm.AddState("VineSwing", new Vector3(520, 160));
        vine.motion = motion;
        foreach (string name in new[] { "Run", "Jump", "Idle", "Slide" })
        {
            var state = sm.states.Select(s => s.state).First(s => s.name == name);
            if (!state.transitions.Any(t => t.destinationState == vine))
            {
                var transition = state.AddTransition(vine);
                transition.hasExitTime = false;
                transition.duration = 0.16f;
                transition.hasFixedDuration = true;
                transition.AddCondition(AnimatorConditionMode.If, 0, "VineAttached");
            }
        }
        if (!vine.transitions.Any())
        {
            var exit = vine.AddTransition(sm.states.Select(s => s.state).First(s => s.name == "Jump"));
            exit.hasExitTime = false;
            exit.duration = 0.12f;
            exit.hasFixedDuration = true;
            exit.AddCondition(AnimatorConditionMode.IfNot, 0, "VineAttached");
        }
        EditorUtility.SetDirty(ac);
        var audio = CreateWhoosh();
        var mesh = CreateLeaf();
        foreach (var zone in Object.FindObjectsByType<VineSwingZone>(FindObjectsSortMode.None))
        {
            var data = new SerializedObject(zone);
            var pivot = (Transform)data.FindProperty("vinePivot").objectReferenceValue;
            var old = zone.transform.parent.Find("Visible Hanging Vine");
            pivot.position = new Vector3(0, 36, 615);
            var canopy = zone.transform.parent.Find("Vine Canopy");
            if (canopy != null) canopy.position = pivot.position;
            if (old != null)
            {
                var grab = zone.transform.position + Vector3.up * 2.4f;
                var delta = pivot.position - grab;
                old.position = (pivot.position + grab) * 0.5f;
                old.rotation = Quaternion.FromToRotation(Vector3.up, delta);
                old.localScale = new Vector3(0.11f, delta.magnitude * 0.5f, 0.11f);
            }
            data.FindProperty("restingVine").objectReferenceValue = old;
            var sfx = zone.GetComponent<AudioSource>();
            if (sfx == null) sfx = zone.gameObject.AddComponent<AudioSource>();
            sfx.clip = audio;
            sfx.playOnAwake = false;
            sfx.loop = true;
            sfx.spatialBlend = 0;
            data.FindProperty("whoosh").objectReferenceValue = sfx;
            var leafTransform = pivot.Find("Falling Jungle Leaves");
            var leaves = leafTransform != null ? leafTransform.GetComponent<ParticleSystem>() :
                new GameObject("Falling Jungle Leaves", typeof(ParticleSystem)).GetComponent<ParticleSystem>();
            leaves.transform.SetParent(pivot, false);
            leaves.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = leaves.main;
            main.loop = false;
            main.playOnAwake = false;
            main.duration = 0.3f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 3.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.4f);
            main.startRotation3D = true;
            main.startRotation = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);
            main.gravityModifier = 0.4f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 32;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.3f,0.48f,0.08f), new Color(0.7f,0.8f,0.2f));
            var emission = leaves.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0, 18) });
            var shape = leaves.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.7f;
            var rotation = leaves.rotationOverLifetime;
            rotation.enabled = true;
            rotation.separateAxes = true;
            rotation.x = 2.3f; rotation.y = 1.5f; rotation.z = 3f;
            var noise = leaves.noise; noise.enabled = true; noise.strength = 0.45f; noise.frequency = 0.7f;
            var renderer = leaves.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = mesh;
            renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Root + "Materials/Tropical/LeafLight.mat");
            data.FindProperty("anchorLeaves").objectReferenceValue = leaves;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Vine swing: humanoid animation, palm anchor, pendulum, whoosh and leaf burst configured.");
    }

    static Mesh CreateLeaf()
    {
        string path = Root + "Art/Tropical/VineLeaf.asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh != null) return mesh;
        mesh = new Mesh { name = "Folded jungle leaf" };
        mesh.vertices = new[] { new Vector3(0,0,-0.65f), new Vector3(-0.3f,0,0), new Vector3(0,0.09f,0), new Vector3(0.3f,0,0), new Vector3(0,0,0.65f) };
        mesh.triangles = new[] { 0,1,2, 1,4,2, 0,2,3, 2,4,3, 2,1,0, 2,4,1, 3,2,0, 3,4,2 };
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    static AudioClip CreateWhoosh()
    {
        string path = Root + "Audio/Whoosh.wav";
        if (!File.Exists(path))
        {
            const int rate = 44100, count = rate * 2;
            var random = new System.Random(7351);
            using (var writer = new BinaryWriter(File.Create(path)))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + count * 2);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
                writer.Write((short)1); writer.Write((short)1); writer.Write(rate); writer.Write(rate * 2);
                writer.Write((short)2); writer.Write((short)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(count * 2);
                float filtered = 0;
                for (int i = 0; i < count; i++)
                {
                    filtered = Mathf.Lerp(filtered, (float)random.NextDouble() * 2 - 1, 0.16f);
                    float edge = Mathf.Min(1f, Mathf.Min(i, count - 1 - i) / 1800f);
                    writer.Write((short)(filtered * edge * 26000));
                }
            }
            AssetDatabase.ImportAsset(path);
        }
        return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
    }
}
