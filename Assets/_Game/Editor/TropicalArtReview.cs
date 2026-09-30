using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

public static class TropicalArtReview
{
    [MenuItem("Tools/Banana Run/Review Tropical Finish")]
    public static void Review()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop play mode before the finish review.");
        var camera = Camera.main;
        var position = camera.transform.position; var rotation = camera.transform.rotation;
        var canvas = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).First(c => c.isRootCanvas);
        bool wasEnabled = canvas.enabled;
        try
        {
            canvas.enabled = false;
            float z = Object.FindFirstObjectByType<FinishGate>().transform.position.z;
            camera.transform.position = new Vector3(0, 3.6f, z - 19);
            camera.transform.LookAt(new Vector3(0, 2.8f, z + 2));
            GameplayFeatureVerification.Capture("Temp/tropical-finish.png", 1280, 720);
        }
        finally { camera.transform.SetPositionAndRotation(position, rotation); canvas.enabled = wasEnabled; }
        var pipeline = UniversalRenderPipeline.asset;
        var data = camera.GetUniversalAdditionalCameraData();
        var volume = Object.FindObjectsByType<Volume>(FindObjectsSortMode.None).First(v => v.isGlobal && v.priority == 10);
        if (!pipeline.supportsHDR || !pipeline.supportsSoftShadows || !data.renderPostProcessing || (data.volumeLayerMask.value & (1 << volume.gameObject.layer)) == 0)
            throw new Exception("Lighting preflight failed.");
        var profile = volume.sharedProfile;
        if (!profile.TryGet<ColorAdjustments>(out var color) || !color.saturation.overrideState || color.saturation.value != 18)
            throw new Exception("Color grading not active.");
        if (!profile.TryGet<Bloom>(out var bloom) || !bloom.intensity.overrideState || bloom.intensity.value <= 0)
            throw new Exception("Bloom not active.");
        if (ShaderUtil.ShaderHasError(Shader.Find("BananaRun/TropicalTrail"))) throw new Exception("Trail shader has errors.");
        var rendererList = new SerializedObject(pipeline).FindProperty("m_RendererDataList");
        for (int i = 0; i < rendererList.arraySize; i++)
        {
            var renderer = rendererList.GetArrayElementAtIndex(i).objectReferenceValue;
            if (renderer != null && new SerializedObject(renderer).FindProperty("postProcessData").objectReferenceValue == null)
                throw new Exception("Renderer has no post-processing data.");
        }
        File.WriteAllText("Temp/tropical-art-review.txt", "PASS: URP HDR, soft shadows, camera post-processing, volume layer mask, saturation +18, bloom and trail shader.\nPASS: renderer post-processing resources present.\nFinish preview: Temp/tropical-finish.png\n");
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
    }
}
