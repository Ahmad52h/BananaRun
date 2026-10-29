#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Real Play Mode sampling: validates the lock, animation ownership and GO handoff.</summary>
[InitializeOnLoad]
public static class RaceStartVerification
{
    private const string Pending = "BananaRun.RaceStartVerification";
    private const string Report = "Temp/race-start-verification.txt";
    private static readonly HashSet<string> failures = new HashSet<string>();
    private static readonly HashSet<string> beats = new HashSet<string>();
    private static RunnerMotor player;
    private static RivalRunner rival;
    private static GiantGorillaChaser gorilla;
    private static Vector3 playerStart, rivalStart, gorillaStart;
    private static int firstFrame, samples;
    private static bool triedInput, capturedIntro, capturedGo, capturedFollow, capturedReturn;
    private static double deadline;
    private static string previousBeat;
    private static int cueCount;

    static RaceStartVerification()
    {
        EditorApplication.playModeStateChanged += OnPlayMode;
    }

    public static void Begin()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Verification requires a stopped Editor.");
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
            failures.Clear(); beats.Clear();
            samples = 0;
            cueCount = 0;
            previousBeat = "";
            firstFrame = -1;
            triedInput = capturedIntro = capturedGo = capturedFollow = capturedReturn = false;
            player = Object.FindAnyObjectByType<RunnerMotor>();
            rival = Object.FindAnyObjectByType<RivalRunner>();
            gorilla = Object.FindAnyObjectByType<GiantGorillaChaser>();
            playerStart = player.transform.position;
            if (rival != null) rivalStart = rival.transform.position;
            if (gorilla != null) gorillaStart = gorilla.transform.position;
            deadline = EditorApplication.timeSinceStartup + 90;
            Application.runInBackground = true;
            Application.logMessageReceived += OnLog;
            EditorApplication.update += Tick;
        }
        else if (state == PlayModeStateChange.ExitingPlayMode) Cleanup();
    }

    private static void OnLog(string message, string trace, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) failures.Add(message);
    }

    private static void Check(bool condition, string message) { if (!condition) failures.Add(message); }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        var sequence = RaceStartSequence.Instance;
        if (sequence == null) { Finish("FAIL: sequence missing"); return; }
        if (EditorApplication.timeSinceStartup > deadline) { Finish("FAIL: player loop did not finish in 90 seconds"); return; }
        EditorApplication.QueuePlayerLoopUpdate();
        if (firstFrame < 0) firstFrame = Time.frameCount;
        samples++;
        float elapsed = sequence.Elapsed;
        if (sequence.CountdownText != "" && sequence.CountdownText != previousBeat)
        {
            previousBeat = sequence.CountdownText;
            cueCount++;
            var source = sequence.GetComponent<AudioSource>();
            Check(source != null && source.isPlaying, "No audio playing when " + previousBeat + " appeared");
            AudioClip cue = previousBeat == "GO!" ? sequence.goCountdownSound : sequence.startCountdownSound;
            Check(cue != null && cue.loadState == AudioDataLoadState.Loaded && !cue.loadInBackground,
                "Countdown cue was not preloaded for " + previousBeat);
        }
        if (elapsed > 0.1f && !sequence.IsRaceStarted)
        {
            beats.Add(sequence.CountdownText);
            Check(Vector3.Distance(player.transform.position, playerStart) < 0.01f, "Player moved before GO");
            Check(Mathf.Abs(player.CurrentSpeed) < 0.001f, "Player reports movement speed before GO");
            Check(player.GetComponentInChildren<Animator>().GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Warming up"), "Player not warming up during countdown");
            if (rival != null)
            {
                Check(Vector3.Distance(rival.transform.position, rivalStart) < 0.01f, "Rival moved before GO");
                Check(rival.GetComponentInChildren<Animator>().GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Idle"), "Rival not Idle during countdown");
            }
            if (gorilla != null)
            {
                if (beats.Count == 1 && samples < 5) gorillaStart = gorilla.transform.position;
                Check(gorilla.IsStartingIntimidation, "Gorilla not intimidating during countdown");
                Check(Vector3.Distance(gorilla.transform.position, gorillaStart) < 0.01f, "Gorilla moved before GO");
            }
            Check(Object.FindAnyObjectByType<WoodenStartingGate>().OpenProgress == 0f, "Gate opened before GO");
            Check(!player.GetComponent<CoconutThrower>().CanThrow, "Coconuts enabled before GO");
            Check(!player.GetComponent<BombDropper>().CanDrop, "Bombs enabled before GO");
            if (!triedInput && elapsed > 1.3f)
            {
                triedInput = true;
                int lane = player.ActiveLane;
                player.ChangeLane(-1); player.Jump();
                Check(!player.Slide() && player.ActiveLane == lane, "Movement inputs bypassed start lock");
            }
            if (!capturedIntro && elapsed > 0.6f)
            {
                capturedIntro = true;
                ScreenCapture.CaptureScreenshot("Temp/race-start-countdown.png");
            }
        }
        if (sequence.IsRaceStarted && !capturedGo)
        {
            capturedGo = true;
            Check(elapsed >= 3.5f && elapsed < 3.5f + Time.maximumDeltaTime + 0.1f, "GO outside the expected 3.5-second boundary");
            Check(sequence.CountdownText == "GO!", "Missing GO beat");
            Check(player.GetComponentInChildren<ParticleSystem>().particleCount > 0, "Missing launch dust");
            ScreenCapture.CaptureScreenshot("Temp/race-start-go.png");
        }
        if (!capturedReturn && elapsed > 3.85f)
        {
            capturedReturn = true;
            ScreenCapture.CaptureScreenshot("Temp/race-start-return.png");
        }
        if (elapsed > 3.5f && elapsed < 4.25f)
        {
            var screen = Camera.main.WorldToViewportPoint(player.transform.position + Vector3.up);
            Check(screen.z > 0f && screen.x > 0.05f && screen.x < 0.95f && screen.y > 0.05f && screen.y < 0.95f,
                "Camera lost the player during the return to gameplay");
            if (rival != null)
            {
                screen = Camera.main.WorldToViewportPoint(rival.transform.position + Vector3.up);
                Check(screen.z > 0f && screen.x > 0.05f && screen.x < 0.95f && screen.y > 0.05f && screen.y < 0.95f,
                    "Camera lost the rival during the return to gameplay");
            }
        }
        if (elapsed > 4.7f && !capturedFollow)
        {
            capturedFollow = true;
            Check(player.transform.position.z > playerStart.z + 2f, "Player did not run after GO");
            if (rival != null) Check(rival.transform.position.z > rivalStart.z + 2f, "Rival did not run after GO");
            Check(player.GetComponentInChildren<Animator>().GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Run"), "Player did not return to Run");
            if (rival != null) Check(rival.GetComponentInChildren<Animator>().GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Run"), "Rival did not return to Run");
            Check(Object.FindAnyObjectByType<WoodenStartingGate>().OpenProgress >= 1f, "Gate did not clear");
            Check(!RaceStartSequence.IsHolding, "Start lock still held after GO");
            Check(Time.frameCount > firstFrame + 5, "Player loop did not advance");
            Check(beats.Contains("3") && beats.Contains("2") && beats.Contains("1"), "Countdown skipped a number");
            Check(cueCount == 4, "Expected exactly four visual/audio beats");
            ScreenCapture.CaptureScreenshot("Temp/race-start-follow.png");
        }
        if (elapsed > 5.2f) Finish(failures.Count == 0 ? "PASS" : "FAIL");
    }

    private static void Finish(string status)
    {
        File.WriteAllText(Report, status + "\nFrames advanced: " + (Time.frameCount - firstFrame) + "\nSamples: " + samples
            + "\nBeats: " + string.Join(", ", beats) + "\nAudio/visual cues checked: " + cueCount + "\n" + string.Join("\n", failures));
        Cleanup();
        EditorApplication.isPlaying = false;
    }

    private static void Cleanup()
    {
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= OnLog;
        SessionState.SetBool(Pending, false);
    }
}
#endif
