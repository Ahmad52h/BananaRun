using System;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Repairs the menu resource through Unity's prefab API, without touching scene files.</summary>
public static class MainMenuJungleSetup
{
    [MenuItem("Tools/Banana Run/Repair Menu Gorilla Resource")]
    public static void Install()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before repairing menu assets.");
        // This FBX contains both the current gorilla mesh and its valid humanoid
        // avatar. The older menu resource referenced removed FBX sub-assets.
        const string modelPath = "Assets/_Game/Art/Gorilla/source/Mutant Run.fbx";
        const string outputPath = "Assets/Resources/MainMenu/Gorilla.prefab";
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/_Game/Art/Gorilla/AC_Gorilla.controller");
        var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/MainMenu/M_Gorilla.mat");
        if (model == null || controller == null || material == null)
            throw new InvalidOperationException("The menu gorilla model, controller and material must exist.");

        var instance = Object.Instantiate(model);
        try
        {
            instance.name = "Menu Gorilla";
            instance.transform.position = Vector3.zero;
            instance.transform.rotation = Quaternion.identity;
            var animator = instance.GetComponentInChildren<Animator>(true);
            var skin = instance.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (animator == null || animator.avatar == null || !animator.avatar.isValid || skin == null || skin.sharedMesh == null)
                throw new InvalidOperationException("The source gorilla must have a valid rig and skinned mesh.");
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            skin.updateWhenOffscreen = true;
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++) materials[i] = material;
                renderer.sharedMaterials = materials;
                renderer.enabled = true;
            }
            foreach (var collider in instance.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
            foreach (var behaviour in instance.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(behaviour);
            PrefabUtility.SaveAsPrefabAsset(instance, outputPath);
            AssetDatabase.SaveAssets();
        }
        finally { Object.DestroyImmediate(instance); }
    }
}
