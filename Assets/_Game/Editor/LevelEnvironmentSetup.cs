#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Repeatable setup for the currently open race scene.</summary>
public static class LevelEnvironmentSetup
{
    [MenuItem("Banana Run/Setup Distance Weather and Lighting")]
    public static void Apply()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before setting up weather.");
        var player = UnityEngine.Object.FindAnyObjectByType<RunnerMotor>();
        var race = UnityEngine.Object.FindAnyObjectByType<RaceProgressTracker>();
        var gate = UnityEngine.Object.FindAnyObjectByType<FinishGate>();
        var sun = RenderSettings.sun;
        if (player == null || gate == null || sun == null)
            throw new InvalidOperationException("Open a race scene with a player, finish gate and directional sun.");

        var manager = UnityEngine.Object.FindAnyObjectByType<LevelEnvironmentManager>();
        if (manager == null)
        {
            var root = new GameObject("Level Environment");
            Undo.RegisterCreatedObjectUndo(root, "Add distance weather");
            manager = Undo.AddComponent<LevelEnvironmentManager>(root);
        }
        Undo.RecordObject(manager, "Configure distance weather");
        var sky = LoadOrCreateMaterial("Assets/_Game/Materials/Tropical/LevelEnvironmentSky.mat", "Skybox/Procedural");
        sky.SetColor("_SkyTint", new Color(0.48f, 0.65f, 0.85f));
        sky.SetColor("_GroundColor", new Color(0.42f, 0.52f, 0.30f));
        sky.SetFloat("_Exposure", 1.15f);
        sky.SetFloat("_AtmosphereThickness", 1f);
        sky.SetFloat("_SunDisk", 2f);
        sky.EnableKeyword("_SUNDISK_HIGH_QUALITY");
        sky.DisableKeyword("_SUNDISK_SIMPLE");
        sky.DisableKeyword("_SUNDISK_NONE");
        EditorUtility.SetDirty(sky);

        Transform existing = manager.transform.Find("Canopy Falling Leaves");
        var leaves = existing != null ? existing.GetComponent<ParticleSystem>() : null;
        if (leaves == null)
        {
            var root = new GameObject("Canopy Falling Leaves");
            Undo.RegisterCreatedObjectUndo(root, "Add jungle leaves");
            root.transform.SetParent(manager.transform, false);
            leaves = root.AddComponent<ParticleSystem>();
        }
        leaves.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        leaves.transform.SetPositionAndRotation(player.transform.position + new Vector3(0, 8, 12), Quaternion.identity);
        var main = leaves.main;
        main.loop = true;
        main.playOnAwake = false;
        main.duration = 6f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 6f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.22f, 0.48f);
        main.startRotation3D = true;
        main.startRotationX = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);
        main.startRotationY = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);
        main.startRotationZ = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.36f, 0.55f, 0.13f), new Color(0.74f, 0.70f, 0.25f));
        main.gravityModifier = 0.025f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 160;
        main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
        var emission = leaves.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.rateOverDistance = 0f;
        emission.SetBursts(Array.Empty<ParticleSystem.Burst>());
        var shape = leaves.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(14f, 1f, 28f);
        var velocity = leaves.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(-0.35f, 0.35f);
        velocity.y = new ParticleSystem.MinMaxCurve(-1.4f, -0.9f);
        velocity.z = new ParticleSystem.MinMaxCurve(-0.5f, 0.1f);
        var rotation = leaves.rotationOverLifetime;
        rotation.enabled = true;
        rotation.separateAxes = true;
        rotation.x = 1.3f; rotation.y = 0.8f; rotation.z = 1.6f;
        var noise = leaves.noise;
        noise.enabled = true; noise.strength = 0.35f; noise.frequency = 0.4f;
        var color = leaves.colorOverLifetime;
        color.enabled = true;
        var fade = new Gradient();
        fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f),
                new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f) });
        color.color = fade;
        var leafMesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/_Game/Art/Tropical/VineLeaf.asset");
        if (leafMesh == null) throw new InvalidOperationException("The existing VineLeaf mesh is missing.");
        var leafMaterial = LoadOrCreateMaterial("Assets/_Game/Materials/Tropical/EnvironmentLeaves.mat", "Universal Render Pipeline/Particles/Simple Lit");
        leafMaterial.SetColor("_BaseColor", Color.white);
        leafMaterial.SetFloat("_Cull", 0f);
        // Alpha blending lets individual leaves fade in and out of their lifetime.
        leafMaterial.SetFloat("_Surface", 1f);
        leafMaterial.SetFloat("_Blend", 0f);
        leafMaterial.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        leafMaterial.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        leafMaterial.SetFloat("_ZWrite", 0f);
        leafMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        leafMaterial.SetOverrideTag("RenderType", "Transparent");
        leafMaterial.renderQueue = (int)RenderQueue.Transparent;
        EditorUtility.SetDirty(leafMaterial);
        var renderer = leaves.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Mesh;
        renderer.mesh = leafMesh;
        renderer.sharedMaterial = leafMaterial;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.alignment = ParticleSystemRenderSpace.World;

        var data = new SerializedObject(manager);
        data.FindProperty("player").objectReferenceValue = player;
        data.FindProperty("race").objectReferenceValue = race;
        data.FindProperty("finishLine").objectReferenceValue = gate.transform;
        data.FindProperty("sunlight").objectReferenceValue = sun;
        data.FindProperty("skyboxTemplate").objectReferenceValue = sky;
        data.FindProperty("jungleLeaves").objectReferenceValue = leaves;
        data.ApplyModifiedProperties();
        EditorUtility.SetDirty(manager);
        EditorUtility.SetDirty(leaves);
        EditorUtility.SetDirty(renderer);
        // Store the same exponential variant in the scene so player builds retain it.
        RenderSettings.fogMode = FogMode.Exponential;
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
        EditorSceneManager.SaveScene(manager.gameObject.scene);
        Debug.Log("Distance weather configured: sunny morning, misty jungle with falling leaves, golden sunset.");
    }

    private static Material LoadOrCreateMaterial(string path, string shaderName)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;
        var shader = Shader.Find(shaderName);
        if (shader == null) throw new InvalidOperationException("Missing shader: " + shaderName);
        material = new Material(shader);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }
}
#endif
