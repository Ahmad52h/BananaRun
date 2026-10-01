using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class GameplayFeatureVerification
{
    const string Report = "Temp/gameplay-verification.txt";
    static RunnerMotor motor;
    static PlayerHealth health;
    static LevelProgressHUD progress;
    static Animator animator;
    static double deadline;
    static float phaseStarted;
    static int phase;
    static BananaPickup banana;

    [InitializeOnLoadMethod]
    static void Reconnect()
    {
        EditorApplication.playModeStateChanged -= OnState;
        EditorApplication.playModeStateChanged += OnState;
    }

    [MenuItem("Tools/Banana Run/Verify Extended Gameplay")]
    public static void Begin()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode first.");
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Save scene before testing.");
        File.WriteAllText(Report, "BananaRun extended gameplay integration checks\n");
        SessionState.SetBool("BananaRun.FeatureTest", true);
        EditorApplication.isPlaying = true;
    }

    static void OnState(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("BananaRun.FeatureTest", false)) return;
        SessionState.SetBool("BananaRun.FeatureTest", false);
        Application.runInBackground = true;
        Time.timeScale = 1f;
        EditorApplication.isPaused = false;
        File.AppendAllText(Report, "Play mode entered; beginning runtime checks.\n");
        motor = Object.FindFirstObjectByType<RunnerMotor>();
        motor.enabled = false;
        motor.GetComponent<RunnerInput>().enabled = false;
        health = motor.GetComponent<PlayerHealth>();
        animator = motor.GetComponentInChildren<Animator>();
        progress = Object.FindFirstObjectByType<LevelProgressHUD>();
        phase = 0;
        phaseStarted = Time.time;
        deadline = EditorApplication.timeSinceStartup + 60;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    static void Check(bool passed, string message)
    {
        if (!passed) throw new Exception(message);
        File.AppendAllText(Report, "PASS: " + message + "\n");
    }

    static void Place(Vector3 position)
    {
        var controller = motor.GetComponent<CharacterController>();
        controller.enabled = false;
        motor.transform.position = position;
        controller.enabled = true;
        controller.Move(Vector3.forward * 0.001f);
        Physics.SyncTransforms();
    }

    static void Next() { phase++; phaseStarted = Time.time; }

    static void Tick()
    {
        if (!EditorApplication.isPlaying) { EditorApplication.update -= Tick; return; }
        EditorApplication.QueuePlayerLoopUpdate();
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Integration check timed out.");
            float elapsed = Time.time - phaseStarted;
            if (elapsed < 0.2f) return;
            switch (phase)
            {
                case 0:
                    var course = Object.FindFirstObjectByType<RunnerCourseGenerator>();
                    Check(course.SpawnedObstacleCount >= 50 && course.PatternCount >= 30, $"Extended course generated {course.SpawnedObstacleCount} obstacles across {course.PatternCount} patterns.");
                    var obstacles = course.GetComponentsInChildren<RunnerObstacle>();
                    Check(obstacles.Select(o => o.name).Distinct().Count() >= 6, "All six obstacle types appear.");
                    foreach (var o in obstacles)
                    {
                        var box = o.GetComponent<BoxCollider>();
                        CheckLane(box.bounds, obstacles, motor.LaneDistance);
                    }
                    Check(true, "Every obstacle row leaves at least one lane clear.");
                    var landingGround = GameObject.Find("Landing Ground");
                    Check(landingGround != null && landingGround.GetComponent<Collider>().bounds.max.z > 900,
                        "Landing surface extends beyond the finish.");
                    var audio = RunnerAudio.Instance;
                    Check(audio != null && audio.GetComponents<AudioSource>().Any(s => s.isPlaying && s.loop && s.clip != null), "Assigned background music is looping.");
                    Check(animator.runtimeAnimatorController.animationClips.Any(c => c.name == "MonkeyStumble" && c.humanMotion), "Stumble clip is a valid humanoid motion.");
                    Capture("Temp/gameplay-start-portrait.png", 540, 960);
                    Place(new Vector3(-3.3f, -0.3f, 450));
                    Next();
                    break;
                case 1:
                    if (elapsed < 0.65f) return;
                    Check(Mathf.Abs(progress.Progress - 0.5f) < 0.005f, "HUD reports halfway at 450 / 900 metres.");
                    var marker = progress.transform.Find("Track/Monkey Marker") as RectTransform;
                    Check(marker != null && Mathf.Abs(marker.anchorMin.x - 0.5f) < 0.02f, "Monkey marker smoothly reaches the midpoint.");
                    Capture("Temp/gameplay-midpoint-landscape.png", 1280, 720);
                    var tropical = Object.FindFirstObjectByType<TropicalScenery>();
                    if (tropical != null)
                    {
                        int chunks = tropical.transform.Cast<Transform>().Count(t => t.gameObject.activeSelf);
                        Check(chunks >= 5 && chunks <= 12, "Tropical border streaming remains bounded after a long-distance move.");
                        Check(tropical.GetComponentsInChildren<Collider>().Length == 0, "Decorative borders do not block gameplay lanes.");
                    }
                    var obstacle = Object.FindObjectsByType<RunnerObstacle>(FindObjectsSortMode.None)
                        .Where(o => o.name.StartsWith("LowObstacle")).OrderBy(o => o.transform.position.z).First();
                    Place(obstacle.GetComponent<BoxCollider>().bounds.center - Vector3.up * 0.6f);
                    Next();
                    break;
                case 2:
                    Check(health.Hearts == health.MaxHearts - 1, "Real obstacle overlap damages the player once.");
                    Check(motor.CurrentSpeed < motor.ForwardSpeed * 0.8f, "Impact slows forward movement.");
                    Check(animator.GetCurrentAnimatorStateInfo(0).IsName("Hit") || animator.GetNextAnimatorStateInfo(0).IsName("Hit"), "Obstacle collision enters the Animator Hit state.");
                    health.TakeHit();
                    Check(health.Hearts == health.MaxHearts - 1, "Invulnerability prevents repeated damage.");
                    Capture("Temp/gameplay-hit.png", 540, 960);
                    Next();
                    break;
                case 3:
                    if (elapsed < 1.1f) return;
                    Check(Mathf.Approximately(motor.CurrentSpeed, motor.ForwardSpeed), "Normal running speed recovers after the stumble.");
                    Check(!animator.GetCurrentAnimatorStateInfo(0).IsName("Hit"), "Animator returns to locomotion.");
                    banana = Object.FindObjectsByType<BananaPickup>(FindObjectsSortMode.None).OrderBy(b => b.transform.position.z).First();
                    Place(banana.GetComponent<SphereCollider>().bounds.center - Vector3.up * 0.6f);
                    Next();
                    break;
                case 4:
                    Check(!banana.gameObject.activeSelf, "Real pickup collision collects the banana.");
                    Check(RunnerAudio.Instance.GetComponents<AudioSource>().Any(s => !s.loop && s.isPlaying), "Banana collection plays an independent sound effect.");
                    var audioData = new SerializedObject(RunnerAudio.Instance);
                    audioData.FindProperty("bananaCollect").objectReferenceValue = null;
                    audioData.FindProperty("backgroundMusic").objectReferenceValue = null;
                    audioData.ApplyModifiedPropertiesWithoutUndo();
                    RunnerAudio.Instance.PlayBananaCollect();
                    RunnerAudio.Instance.PlayMusic();
                    Check(true, "Missing audio clips are safely ignored.");
                    motor.GetComponent<BananaInventory>().AddBananas(10);
                    Place(new Vector3(0, -0.3f, 880));
                    Next();
                    break;
                case 5:
                    if (elapsed < 0.5f) return;
                    Capture("Temp/tropical-finish-runtime.png", 1280, 720);
                    Place(new Vector3(0, -0.3f, 900));
                    Next();
                    break;
                case 6:
                    Check(Object.FindFirstObjectByType<FinishGate>().Completed && health.IsRunOver, "Moved finish gate completes the run.");
                    Check(progress.Progress == 1f, "HUD reaches exactly 100% at the finish.");
                    File.AppendAllText(Report, "ALL CHECKS PASSED\n");
                    EditorApplication.update -= Tick;
                    EditorApplication.isPlaying = false;
                    break;
            }
        }
        catch (Exception e)
        {
            File.AppendAllText(Report, "FAIL: " + e.Message + "\n");
            EditorApplication.update -= Tick;
            EditorApplication.isPlaying = false;
            Debug.LogException(e);
        }
    }

    static void CheckLane(Bounds row, RunnerObstacle[] obstacles, float laneWidth)
    {
        for (int lane = -1; lane <= 1; lane++)
        {
            float x = lane * laneWidth;
            if (!obstacles.Any(o => {
                var bounds = o.GetComponent<BoxCollider>().bounds;
                return Mathf.Abs(bounds.center.z - row.center.z) < 3.5f && x > bounds.min.x - 0.4f && x < bounds.max.x + 0.4f;
            })) return;
        }
        throw new Exception("No safe lane near z=" + row.center.z);
    }

    public static void Capture(string path, int width, int height)
    {
        var camera = Camera.main;
        var canvas = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).First(c => c.isRootCanvas);
        var oldMode = canvas.renderMode;
        var oldCamera = canvas.worldCamera;
        float oldDistance = canvas.planeDistance;
        var previous = camera.targetTexture;
        var active = RenderTexture.active;
        var rt = RenderTexture.GetTemporary(width, height, 24);
        var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = rt;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = rt;
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
        }
        finally
        {
            canvas.renderMode = oldMode;
            canvas.worldCamera = oldCamera;
            canvas.planeDistance = oldDistance;
            camera.targetTexture = previous;
            RenderTexture.active = active;
            RenderTexture.ReleaseTemporary(rt);
            Object.DestroyImmediate(texture);
        }
    }
}
