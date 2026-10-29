#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Exercises the real race distance, global settings and particle lifecycle in Play Mode.</summary>
[InitializeOnLoad]
public static class LevelEnvironmentVerification
{
    private const string Pending = "BananaRun.VerifyEnvironment";
    private const string Report = "Temp/environment-verification.txt";
    private static readonly List<string> checks = new List<string>();
    private static readonly float[] Samples = { 0.15f, 0.5f, 0.85f, 0.15f };
    private static LevelEnvironmentManager manager;
    private static RaceProgressTracker race;
    private static RunnerMotor player;
    private static float initialZ, initialY, enteredAt;
    private static int stage, firstFrame, stageFrame;
    private static bool placed;
    private static double deadline;
    private static float morningIntensity, morningElevation, jungleDensity, morningExposure;
    private static Color morningAmbient;

    static LevelEnvironmentVerification() => EditorApplication.playModeStateChanged += OnPlayMode;

    [MenuItem("Banana Run/Verify Distance Weather and Lighting")]
    public static void Begin()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Verification requires a stopped Editor.");
        if (Object.FindAnyObjectByType<LevelEnvironmentManager>() == null)
            throw new InvalidOperationException("Set up the level environment first.");
        Directory.CreateDirectory("Temp");
        File.WriteAllText(Report, "Waiting for Play Mode\n");
        SessionState.SetBool(Pending, true);
        EditorApplication.isPlaying = true;
    }

    private static void OnPlayMode(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Pending, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            checks.Clear();
            manager = Object.FindAnyObjectByType<LevelEnvironmentManager>();
            race = Object.FindAnyObjectByType<RaceProgressTracker>();
            player = Object.FindAnyObjectByType<RunnerMotor>();
            initialZ = player.transform.position.z;
            initialY = player.transform.position.y;
            firstFrame = Time.frameCount;
            stage = 0; placed = false;
            deadline = EditorApplication.timeSinceStartup + 60;
            Application.runInBackground = true;
            Application.logMessageReceived += OnLog;
            EditorApplication.update += Tick;
        }
        else if (state == PlayModeStateChange.ExitingPlayMode) Cleanup();
    }

    private static void OnLog(string message, string trace, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            checks.Add("FAIL runtime: " + message);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        checks.Add("PASS: " + message);
    }

    private static void Tick()
    {
        EditorApplication.QueuePlayerLoopUpdate();
        if (EditorApplication.timeSinceStartup > deadline) { Finish("FAIL: player loop did not advance within 60 seconds."); return; }
        if (Time.frameCount < firstFrame + 3) return;
        try
        {
            if (!placed)
            {
                // Isolate environment sampling from race collisions and scripted start/cutscenes.
                player.enabled = false;
                player.GetComponent<RunnerInput>().enabled = false;
                player.GetComponent<CharacterController>().enabled = false;
                if (race.Rival != null) race.Rival.enabled = false;
                var gorilla = Object.FindAnyObjectByType<GiantGorillaChaser>();
                if (gorilla != null) gorilla.enabled = false;
                var intro = Object.FindAnyObjectByType<RaceStartSequence>();
                if (intro != null) intro.enabled = false;
                Object.FindAnyObjectByType<RunnerCamera>()?.EndRaceIntro(0.01f);
                player.transform.position = new Vector3(player.transform.position.x, initialY, initialZ + Samples[stage] * race.RaceLength);
                placed = true;
                enteredAt = Time.time;
                stageFrame = Time.frameCount;
                return;
            }
            if (Time.frameCount < stageFrame + 20 || Time.time - enteredAt < 1.5f) return;
            Require(Time.frameCount > firstFrame + 20, "Play Mode frames advance");
            Require(Mathf.Abs(manager.Progress - Samples[stage]) < 0.002f, "Environment follows the race tracker's player distance at " + Samples[stage]);
            Require(RenderSettings.fog && RenderSettings.fogMode == FogMode.Exponential, "Exponential fog mode remains stable");
            Require(manager.Sunlight.shadows == LightShadows.Soft, "Directional sun uses soft shadows");
            var data = new SerializedObject(manager);
            var template = (Material)data.FindProperty("skyboxTemplate").objectReferenceValue;
            Require(RenderSettings.skybox != template && RenderSettings.skybox.shader.isSupported, "Supported sky uses a private runtime material");
            if (stage == 0)
            {
                morningIntensity = manager.Sunlight.intensity;
                morningElevation = manager.Sunlight.transform.eulerAngles.x;
                morningAmbient = RenderSettings.ambientSkyColor;
                morningExposure = RenderSettings.skybox.GetFloat("_Exposure");
                Require(RenderSettings.fogDensity == 0f && manager.JungleLeaves.particleCount == 0, "Morning is clear with no falling leaves");
                ScreenCapture.CaptureScreenshot("Temp/environment-morning.png");
            }
            else if (stage == 1)
            {
                jungleDensity = RenderSettings.fogDensity;
                Require(jungleDensity > 0.01f && manager.Sunlight.intensity < morningIntensity, "Jungle has dense mist and dimmer sunlight");
                Require(RenderSettings.ambientSkyColor.grayscale < morningAmbient.grayscale, "Canopy ambient light is softer and darker");
                Require(manager.JungleWeight > 0.99f && manager.JungleLeaves.particleCount > 0, "Jungle emits visible leaf particles");
                Require(manager.JungleLeaves.main.simulationSpace == ParticleSystemSimulationSpace.World, "Leaves remain in world space as the runner moves");
                Require(RenderSettings.skybox.GetFloat("_Exposure") < morningExposure, "Jungle darkens the sky");
                ScreenCapture.CaptureScreenshot("Temp/environment-jungle.png");
            }
            else if (stage == 2)
            {
                Require(Vector4.Distance(manager.Sunlight.color, new Color(1f, 0.75f, 0.4f)) < 0.001f, "Sunset reaches the requested golden sunlight color");
                Require(manager.Sunlight.transform.eulerAngles.x < morningElevation * 0.5f, "Sunset lowers the sun for longer shadows");
                Require(RenderSettings.fogDensity < jungleDensity && RenderSettings.fogColor.r > RenderSettings.fogColor.b, "Sunset mist thins and turns golden");
                Require(manager.JungleLeaves.emission.rateOverTime.constant == 0f, "Leaf emission fades to zero outside the jungle");
                ScreenCapture.CaptureScreenshot("Temp/environment-sunset.png");
            }
            else
            {
                Require(RenderSettings.fogDensity == 0f && manager.JungleWeight == 0f, "Returning to the start resets distance-driven weather");
                CheckContinuity();
                CheckRestore();
                Finish("Completed environment integration verification");
                return;
            }
            stage++;
            placed = false;
        }
        catch (Exception exception) { Finish("FAIL: " + exception); }
    }

    private static void CheckContinuity()
    {
        float previousDensity = 0f;
        Color previousColor = Color.black;
        Quaternion previousRotation = Quaternion.identity;
        for (int i = 0; i <= 2000; i++)
        {
            manager.ApplyProgress(i / 2000f);
            if (i > 0 && (Mathf.Abs(RenderSettings.fogDensity - previousDensity) > 0.001f ||
                Vector4.Distance(manager.Sunlight.color, previousColor) > 0.02f ||
                Quaternion.Angle(manager.Sunlight.transform.rotation, previousRotation) > 1f))
                throw new InvalidOperationException("Environment pops at normalized distance " + i / 2000f);
            previousDensity = RenderSettings.fogDensity;
            previousColor = manager.Sunlight.color;
            previousRotation = manager.Sunlight.transform.rotation;
        }
        Require(true, "2001 distance samples blend fog, sun color and rotation continuously across both boundaries");
        manager.ApplyProgress(-1f);
        Require(manager.Progress == 0f && RenderSettings.fogDensity == 0f, "Negative distances clamp to morning");
        manager.ApplyProgress(2f);
        Require(manager.Progress == 1f && manager.SunsetWeight == 1f, "Distances beyond the finish clamp to sunset");
    }

    private static void CheckRestore()
    {
        // Disabling/re-enabling must not accumulate material copies or alter the template.
        var fieldFlags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var originalSky = (Material)typeof(LevelEnvironmentManager).GetField("originalSkybox", fieldFlags).GetValue(manager);
        var originalColor = (Color)typeof(LevelEnvironmentManager).GetField("originalSunColor", fieldFlags).GetValue(manager);
        var originalRotation = (Quaternion)typeof(LevelEnvironmentManager).GetField("originalSunRotation", fieldFlags).GetValue(manager);
        float originalDensity = (float)typeof(LevelEnvironmentManager).GetField("originalFogDensity", fieldFlags).GetValue(manager);
        manager.enabled = false;
        Require(RenderSettings.skybox == originalSky && Mathf.Approximately(RenderSettings.fogDensity, originalDensity), "Disabling restores original skybox and fog");
        Require(Vector4.Distance(manager.Sunlight.color, originalColor) < 0.001f && Quaternion.Angle(manager.Sunlight.transform.rotation, originalRotation) < 0.01f,
            "Disabling restores original sun color and angle");
        Require(manager.JungleLeaves.particleCount == 0, "Disabling clears all weather leaves");
        manager.enabled = true;
        manager.ApplyProgress(0.5f);
        Require(manager.JungleWeight > 0.99f && RenderSettings.skybox != originalSky, "Re-enabling recreates the runtime sky and resumes weather");
        var data = new SerializedObject(manager);
        var template = (Material)data.FindProperty("skyboxTemplate").objectReferenceValue;
        Require(Mathf.Abs(template.GetFloat("_Exposure") - 1.15f) < 0.001f, "Skybox asset remains unchanged by weather animation");
    }

    private static void Finish(string result)
    {
        checks.Add(result);
        File.WriteAllLines(Report, checks);
        Cleanup();
        EditorApplication.isPlaying = false;
    }

    private static void Cleanup()
    {
        SessionState.SetBool(Pending, false);
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= OnLog;
    }
}
#endif
