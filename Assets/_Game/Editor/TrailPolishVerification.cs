using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// A short integration check using the real player collider and pickup trigger.
public static class TrailPolishVerification
{
    static BananaPickup banana;
    static BananaInventory inventory;
    static float started;
    static bool captured;
    static bool placed;
    static int before;

    static void Begin()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop play mode first.");
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path != "Assets/_Game/Scenes/Level01.unity" || scene.isDirty)
            throw new InvalidOperationException("Save Level01 before verification.");
        EditorSceneManager.OpenScene(scene.path);
        Physics.SyncTransforms();
        foreach (var obstacle in Object.FindObjectsByType<RunnerObstacle>(FindObjectsSortMode.None))
        {
            var renderers = obstacle.GetComponentsInChildren<Renderer>().Where(r => r.enabled).ToArray();
            var bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            var hitbox = obstacle.GetComponent<BoxCollider>().bounds;
            if (Vector3.Distance(bounds.center, hitbox.center) > 0.05f || Vector3.Distance(bounds.size, hitbox.size) > 0.05f)
                throw new InvalidOperationException("Hitbox mismatch: " + obstacle.name);
        }
        File.WriteAllText("Temp/polish-verification.txt", "PASS: saved scene reload and all obstacle hitboxes match visible bounds.\n");
        EditorApplication.playModeStateChanged += OnPlayMode;
        EditorApplication.isPlaying = true;
    }

    static void OnPlayMode(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode) return;
        EditorApplication.playModeStateChanged -= OnPlayMode;
        StartCheck();
    }

    // InitializeOnLoad reconnects the callback after entering play mode reloads the domain.
    [InitializeOnLoadMethod]
    static void Reconnect()
    {
        if (SessionState.GetBool("BananaRun.Verify", false))
        {
            SessionState.SetBool("BananaRun.Verify", false);
            EditorApplication.playModeStateChanged += OnPlayMode;
        }
    }

    [MenuItem("Tools/Banana Run/Run Integration Check")]
    public static void Run()
    {
        SessionState.SetBool("BananaRun.Verify", true);
        try { Begin(); }
        catch { SessionState.SetBool("BananaRun.Verify", false); throw; }
    }

    static void StartCheck()
    {
        EditorApplication.update -= Tick;
        var motor = Object.FindFirstObjectByType<RunnerMotor>();
        motor.enabled = false;
        motor.GetComponent<RunnerInput>().enabled = false;
        inventory = motor.GetComponent<BananaInventory>();
        banana = Object.FindObjectsByType<BananaPickup>(FindObjectsSortMode.None).OrderBy(b => b.transform.position.z).First();
        before = inventory.Count;
        captured = false;
        placed = false;
        started = Time.time;
        EditorApplication.update += Tick;
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying) { EditorApplication.update -= Tick; return; }
        try
        {
            if (!placed && Time.time - started > 0.1f)
            {
                var controller = inventory.GetComponent<CharacterController>();
                controller.enabled = false;
                inventory.transform.position = banana.GetComponent<SphereCollider>().bounds.center - Vector3.up * 0.6f;
                controller.enabled = true;
                controller.Move(Vector3.forward * 0.01f);
                Physics.SyncTransforms();
                placed = true;
                started = Time.time;
            }
            if (!placed) return;
            if (!captured && !banana.gameObject.activeSelf)
            {
                var effects = Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None)
                    .Where(p => p.name.StartsWith("FX_BananaPickup")).ToArray();
                if (effects.Length != 1) throw new Exception("Expected one pickup effect.");
                var effect = effects[0];
                effect.Simulate(0.065f, true, true);
                Capture("Temp/banana-flash-preview.png");
                effect.Play(true);
                if (inventory.Count != before + 1) throw new Exception("Pickup count did not increase exactly once.");
                typeof(BananaPickup).GetMethod("OnTriggerEnter", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(banana, new object[] { inventory.GetComponent<CharacterController>() });
                if (inventory.Count != before + 1) throw new Exception("Pickup awarded twice.");
                File.AppendAllText("Temp/polish-verification.txt", "PASS: actual player trigger awards one banana, spawns independent flash and sparkles, and ignores repeat trigger.\n");
                captured = true;
            }
            if (Time.time - started > 1.5f)
            {
                if (!captured) throw new Exception("Player trigger did not collect banana.");
                if (Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None).Any(p => p.name.StartsWith("FX_BananaPickup")))
                    throw new Exception("Pickup effect did not clean itself up.");
                File.AppendAllText("Temp/polish-verification.txt", "PASS: pickup particles cleaned up after completion.\n");
                EditorApplication.update -= Tick;
                EditorApplication.isPlaying = false;
            }
        }
        catch (Exception e)
        {
            File.AppendAllText("Temp/polish-verification.txt", "FAIL: " + e.Message + "\n");
            EditorApplication.update -= Tick;
            EditorApplication.isPlaying = false;
            Debug.LogException(e);
        }
    }

    static void Capture(string path)
    {
        var camera = Camera.main;
        var previous = camera.targetTexture;
        var active = RenderTexture.active;
        var rt = RenderTexture.GetTemporary(960, 720, 24);
        var texture = new Texture2D(960, 720, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = rt;
            camera.Render();
            RenderTexture.active = rt;
            texture.ReadPixels(new Rect(0, 0, 960, 720), 0, 0);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = previous;
            RenderTexture.active = active;
            RenderTexture.ReleaseTemporary(rt);
            Object.DestroyImmediate(texture);
        }
    }
}
