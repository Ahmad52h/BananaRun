using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Real player-loop checks, plus swept-volume route audits of multiple course seeds.</summary>
public static class ProgressiveDifficultyVerification
{
    const string Report = "Temp/progressive-difficulty-verification.txt";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    const string ActiveKey = "BananaRun.ProgressiveDifficulty.Active";
    const string RunningKey = "BananaRun.ProgressiveDifficulty.Running";
    const string FinishedKey = "BananaRun.ProgressiveDifficulty.Finished";
    const string OverridesKey = "BananaRun.ProgressiveDifficulty.Overrides";
    const string BackgroundKey = "BananaRun.ProgressiveDifficulty.Background";
    const string TimeScaleKey = "BananaRun.ProgressiveDifficulty.TimeScale";
    const string PassedKey = "BananaRun.ProgressiveDifficulty.Passed";
    static RunnerMotor motor;
    static RunnerCourseGenerator course;
    static VineSwingZone vine;
    static BananaPickup[] arc;
    static float started, jumpStart, apex, speedBefore;
    static int phase, initialFrame, normalCollected, initialBananas;
    static double deadline;
    static bool sawSwing;

    [InitializeOnLoadMethod] static void Register()
    {
        EditorApplication.playModeStateChanged -= State;
        EditorApplication.playModeStateChanged += State;
        EditorApplication.update -= Tick;
        AssemblyReloadEvents.beforeAssemblyReload -= BeforeAssemblyReload;
        AssemblyReloadEvents.beforeAssemblyReload += BeforeAssemblyReload;
        EditorApplication.quitting -= OnQuitting;
        EditorApplication.quitting += OnQuitting;
        if (SessionState.GetBool(ActiveKey, false) && SessionState.GetBool(RunningKey, false))
        {
            if (!SessionState.GetBool(FinishedKey, false))
                Finish(false, "Script reload interrupted the verification.");
            EditorApplication.delayCall += StopAfterReload;
        }
    }

    /// <summary>Batch entry point: loads Level01 first, then runs the normal Play Mode verification.</summary>
    public static void BeginFromBatch()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        EditorSceneManager.OpenScene("Assets/_Game/Scenes/Level01.unity", OpenSceneMode.Single);
        Begin();
    }

    [MenuItem("Tools/Banana Run/Verify Progressive Difficulty")]
    public static void Begin()
    {
        if (SessionState.GetBool(ActiveKey, false))
        {
            Debug.LogError("Progressive difficulty verification is already running; its report was left untouched.");
            return;
        }
        try
        {
            Directory.CreateDirectory("Temp");
            File.WriteAllText(Report, "Progressive difficulty runtime verification\n");
        }
        catch (Exception e)
        {
            Debug.LogError("Progressive difficulty verification cannot create its temporary report: " + e.Message);
            return;
        }
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        { AbortBeforePlay("Stop Play Mode before starting verification."); return; }
        if (PrefabStageUtility.GetCurrentPrefabStage() != null)
        { AbortBeforePlay("Close Prefab Mode before verification so test changes cannot affect a prefab asset."); return; }
        if (EditorSettings.enterPlayModeOptionsEnabled &&
            (EditorSettings.enterPlayModeOptions & EnterPlayModeOptions.DisableSceneReload) != 0)
        { AbortBeforePlay("Enable Scene Reload in Enter Play Mode Options before verification."); return; }
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (scene.isDirty || string.IsNullOrEmpty(scene.path))
            { AbortBeforePlay("Save or close the unsaved scene '" + scene.name + "' before verification."); return; }
        }
        try
        {
            if (SceneManager.GetActiveScene().path != "Assets/_Game/Scenes/Level01.unity" || SceneManager.sceneCount != 1)
                EditorSceneManager.OpenScene("Assets/_Game/Scenes/Level01.unity", OpenSceneMode.Single);
            SessionState.SetInt(PassedKey, 0);
            SessionState.SetBool(FinishedKey, false);
            SessionState.SetBool(RunningKey, false);
            SessionState.SetBool(OverridesKey, false);
            SessionState.SetBool(ActiveKey, true);
            EditorApplication.isPlaying = true;
        }
        catch (Exception e)
        {
            SessionState.SetBool(ActiveKey, false);
            AbortBeforePlay("Could not start verification: " + e.GetBaseException().Message);
        }
    }

    static void State(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(ActiveKey, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode && !SessionState.GetBool(FinishedKey, false))
        {
            try
            {
                SessionState.SetBool(BackgroundKey, Application.runInBackground);
                SessionState.SetFloat(TimeScaleKey, Time.timeScale);
                SessionState.SetBool(OverridesKey, true);
                Application.runInBackground = true;
                Time.timeScale = 1f;
                SessionState.SetBool(RunningKey, true);
                phase = 0; initialFrame = Time.frameCount; started = Time.time;
                deadline = EditorApplication.timeSinceStartup + 150;
                EditorApplication.update -= Tick;
                EditorApplication.update += Tick;
            }
            catch (Exception e)
            {
                Finish(false, "Could not initialize Play Mode verification: " + e.GetBaseException().Message);
                EditorApplication.isPlaying = false;
            }
        }
        else if (state == PlayModeStateChange.ExitingPlayMode)
        {
            if (!SessionState.GetBool(FinishedKey, false)) Finish(false, "Play Mode stopped before verification completed.");
            RestoreOverrides();
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            if (!SessionState.GetBool(FinishedKey, false)) Finish(false, "Verification was interrupted before Play Mode started.");
            Cleanup();
        }
    }

    static void BeforeAssemblyReload()
    {
        if (!SessionState.GetBool(ActiveKey, false) || !SessionState.GetBool(RunningKey, false)) return;
        if (!SessionState.GetBool(FinishedKey, false)) Finish(false, "Script/domain reload interrupted the verification.");
        RestoreOverrides();
    }

    static void OnQuitting()
    {
        if (!SessionState.GetBool(ActiveKey, false)) return;
        if (!SessionState.GetBool(FinishedKey, false)) Finish(false, "Unity Editor closed before verification completed.");
        Cleanup();
    }

    static void StopAfterReload()
    {
        if (!SessionState.GetBool(ActiveKey, false)) return;
        if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
        else if (!EditorApplication.isPlayingOrWillChangePlaymode) Cleanup();
    }

    static void AbortBeforePlay(string reason)
    {
        WriteReport("FAIL: " + reason + "\nFINAL: FAIL (0 checks passed; 1 failed)\n");
        Debug.LogError("Progressive difficulty verification: " + reason);
    }

    static void WriteReport(string text)
    {
        try { File.AppendAllText(Report, text); }
        catch (Exception e) { Debug.LogError("Progressive difficulty report could not be written: " + e.Message); }
    }

    static void Finish(bool passed, string reason)
    {
        if (SessionState.GetBool(FinishedKey, false)) return;
        SessionState.SetBool(FinishedKey, true);
        EditorApplication.update -= Tick;
        int checks = SessionState.GetInt(PassedKey, 0);
        if (!passed) WriteReport("FAIL: " + reason + "\n");
        WriteReport("FINAL: " + (passed ? "PASS" : "FAIL") + " (" + checks +
            " checks passed; " + (passed ? "0" : "1") + " failed)\n");
        if (passed) Debug.Log("Progressive difficulty verification PASS. Report: " + Report);
        else Debug.LogError("Progressive difficulty verification FAIL: " + reason + " Report: " + Report);
        RestoreOverrides();
    }

    static void RestoreOverrides()
    {
        if (!SessionState.GetBool(OverridesKey, false)) return;
        Application.runInBackground = SessionState.GetBool(BackgroundKey, false);
        Time.timeScale = SessionState.GetFloat(TimeScaleKey, 1f);
        SessionState.SetBool(OverridesKey, false);
    }

    static void Cleanup()
    {
        EditorApplication.update -= Tick;
        RestoreOverrides();
        SessionState.SetBool(RunningKey, false);
        SessionState.SetBool(ActiveKey, false);
        SessionState.SetBool(FinishedKey, false);
    }

    static void Check(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException(message);
        SessionState.SetInt(PassedKey, SessionState.GetInt(PassedKey, 0) + 1);
        WriteReport("PASS: " + message + "\n");
    }
    static void Set(object target, string field, object value)
    {
        var info = target.GetType().GetField(field, Private);
        if (info == null) throw new InvalidOperationException("Verification field missing: " + target.GetType().Name + "." + field);
        info.SetValue(target, value);
    }
    static void Place(float x, float z)
    {
        if (motor == null) throw new InvalidOperationException("Player RunnerMotor disappeared before placement.");
        var cc = motor.GetComponent<CharacterController>();
        if (cc == null) throw new InvalidOperationException("Player CharacterController disappeared before placement.");
        cc.enabled = false;
        motor.transform.position = new Vector3(x, -0.22f, z);
        cc.enabled = true;
        motor.ResetVerticalVelocity();
        Set(motor, "currentLane", Mathf.RoundToInt(x / motor.LaneDistance));
        cc.Move(Vector3.down * 0.1f);
        Physics.SyncTransforms();
    }

    static void Audit(RunnerCourseGenerator target)
    {
        if (target == null) throw new InvalidOperationException("Course generator is missing during route audit.");
        if (motor == null || motor.GetComponent<CharacterController>() == null)
            throw new InvalidOperationException("Player or CharacterController is missing during route audit.");
        float previousEnd = float.NegativeInfinity;
        int lane = 0;
        foreach (var row in target.Sections)
        {
            if (previousEnd > float.NegativeInfinity && row.SafeLane != lane)
                if (row.Start - previousEnd + 0.01f < target.ReactionDistance)
                    throw new Exception("Insufficient maximum-speed lane transition at " + row.Z);
            if (row.Start < 660f && row.End > 592f) throw new Exception("Pattern overlaps protected river approach.");
            foreach (var collider in target.GetComponentsInChildren<BoxCollider>())
            {
                if (!collider.enabled || collider.GetComponentInParent<RunnerObstacle>() == null) continue;
                Bounds b = collider.bounds;
                var hazard = collider.GetComponentInParent<DynamicRunnerHazard>();
                float back = hazard != null ? hazard.TravelLimit : 0f;
                float radius = motor.GetComponent<CharacterController>().radius + 0.19f;
                float safeX = row.SafeLane * motor.LaneDistance;
                if (b.max.z + radius <= row.Start || b.min.z - back - radius >= row.End) continue;
                if (safeX > b.min.x - radius && safeX < b.max.x + radius)
                    throw new Exception("Swept obstacle intersects safe corridor at " + row.Z + ": " + collider.name);
            }
            previousEnd = Mathf.Max(previousEnd, row.End);
            lane = row.SafeLane;
        }
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying)
        {
            Finish(false, "Play Mode ended while verification was running.");
            return;
        }
        try
        {
            EditorApplication.QueuePlayerLoopUpdate();
            if (EditorApplication.timeSinceStartup > deadline)
                throw new InvalidOperationException("Timed out during verification phase " + phase + ".");
            if (phase > 0 && phase < 8)
            {
                if (motor == null) throw new InvalidOperationException("Player RunnerMotor disappeared during verification.");
                if (course == null) throw new InvalidOperationException("RunnerCourseGenerator disappeared during verification.");
                if (vine == null) throw new InvalidOperationException("VineSwingZone disappeared during verification.");
                if (motor.GetComponent<CharacterController>() == null)
                    throw new InvalidOperationException("Player CharacterController disappeared during verification.");
            }
            float elapsed = Time.time - started;
            if (phase == 0)
            {
                if (elapsed < 0.3f) return;
                Check(Time.frameCount > initialFrame + 2, "Player frames advance.");
                motor = Object.FindFirstObjectByType<RunnerMotor>();
                course = Object.FindFirstObjectByType<RunnerCourseGenerator>();
                vine = Object.FindFirstObjectByType<VineSwingZone>();
                var progress = Object.FindFirstObjectByType<LevelProgressHUD>();
                Check(motor != null, "RunnerMotor exists in Level01.");
                Check(course != null, "RunnerCourseGenerator exists in Level01.");
                Check(vine != null, "VineSwingZone exists in Level01.");
                Check(progress != null, "LevelProgressHUD exists in Level01.");
                Check(motor.GetComponent<CharacterController>() != null, "Player CharacterController exists.");
                Check(motor.GetComponent<RunnerInput>() != null, "Player RunnerInput exists.");
                Check(motor.GetComponent<PowerUpManager>() != null, "Player PowerUpManager exists.");
                Check(motor.GetComponent<BananaInventory>() != null, "Player BananaInventory exists.");
                Check(course.Player == motor, "Generator references the test player.");
                Check(progress.Player == motor.transform, "Progress HUD references the test player.");
                float origin = motor.transform.position.z - progress.DistanceTravelled;
                float last = 0;
                for (int i = 0; i <= 100; i++)
                {
                    float speed = motor.SpeedAtDifficulty(course.DifficultyAt(origin + progress.RunLength * i / 100f));
                    if (speed < last || (i > 0 && speed - last > motor.ForwardSpeed * 0.01f)) throw new Exception("Non-smooth speed progression.");
                    last = speed;
                    if (i % 25 == 0) WriteReport("Speed at " + i + "%: " + speed.ToString("F3") + "\n");
                }
                Check(Mathf.Abs(last - motor.MaximumForwardSpeed) < 0.001f, "Smooth speed reaches its cap at the finish.");
                Check(course.ReactionDistance / motor.MaximumForwardSpeed >= 0.8f + motor.LaneChangeDuration - 0.001f,
                    "Maximum-speed transitions reserve at least 0.8 seconds of reaction time plus a complete lane change.");
                Audit(course);
                foreach (var range in new[] { new Vector2(0f, 0.35f), new Vector2(0.35f, 0.7f), new Vector2(0.7f, 1.01f) })
                {
                    var gaps = course.Sections.Skip(1).Select((r, i) => new { Row = r, Gap = r.Z - course.Sections[i].Z })
                        .Where(r => r.Row.Difficulty >= range.x && r.Row.Difficulty < range.y && r.Gap < 65f).ToArray();
                    WriteReport("Row spacing at difficulty " + range + ": " + (gaps.Length > 0 ? gaps.Average(r => r.Gap).ToString("F2") : "n/a") + "m\n");
                }
                Check(course.PatternCount > 20, "Authored course has a swept-volume safe route through all " + course.PatternCount + " rows.");
                Check(course.HighBananaPathCount > 0, "Occasional elevated banana paths are generated after green pickups: " + course.HighBananaPathCount);
                var moving = course.GetComponentsInChildren<DynamicRunnerHazard>().FirstOrDefault();
                Check(moving != null, "Generated course contains a DynamicRunnerHazard.");
                float earlyHazardSpeed = moving.EffectiveSpeed;
                Vector3 originalPosition = motor.transform.position;
                motor.transform.position = new Vector3(0, originalPosition.y, origin + progress.RunLength);
                Check(moving.EffectiveSpeed > earlyHazardSpeed && moving.EffectiveSpeed <= earlyHazardSpeed * 1.45f,
                    "Moving hazard speed scales gradually with the same difficulty.");
                motor.transform.position = originalPosition;
                // Generate fresh courses with varied seeds, no scenery/pickups, and audit actual collider bounds.
                motor.enabled = false;
                try
                {
                    for (int seed = 0; seed < 12; seed++)
                    {
                        var root = new GameObject("Verification Course");
                        try
                        {
                            var sample = root.AddComponent<RunnerCourseGenerator>();
                            EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(course), sample);
                            Set(sample, "seed", seed); Set(sample, "randomizeEachRun", false);
                            Set(sample, "sceneryPrefabs", new GameObject[0]); Set(sample, "bananaPrefab", null);
                            Set(sample, "coconutPrefab", null); Set(sample, "powerUpPrefabs", new PowerUpPickup[0]);
                            var start = typeof(RunnerCourseGenerator).GetMethod("Start", Private);
                            Check(start != null, "Generator Start method is available for seed audit.");
                            start.Invoke(sample, null);
                            Audit(sample);
                        }
                        finally { Object.DestroyImmediate(root); }
                    }
                }
                finally { motor.ConfigureDifficulty(course); motor.enabled = true; }
                Check(true, "Twelve additional seeds preserve safe corridors and reaction gaps.");
                // Isolate jump and input tests from unrelated encounters.
                foreach (var o in Object.FindObjectsByType<RunnerObstacle>(FindObjectsSortMode.None)) o.gameObject.SetActive(false);
                foreach (var o in Object.FindObjectsByType<BananaPeelHazard>(FindObjectsSortMode.None)) o.gameObject.SetActive(false);
                foreach (var o in Object.FindObjectsByType<FallingCoconutSpawner>(FindObjectsSortMode.None)) o.enabled = false;
                foreach (var o in Object.FindObjectsByType<PowerUpPickup>(FindObjectsSortMode.None)) o.gameObject.SetActive(false);
                var arcRoot = course.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == "High Jump Banana Arc");
                Check(arcRoot != null, "Generated course contains a High Jump Banana Arc.");
                arc = arcRoot.GetComponentsInChildren<BananaPickup>();
                Check(arc.Length >= 2, "High Jump Banana Arc has at least two bananas.");
                Check(arc.All(b => b != null && b.GetComponent<SphereCollider>() != null), "High arc bananas have SphereColliders.");
                float first = arc[0].GetComponent<SphereCollider>().bounds.center.z;
                float step = arc[1].GetComponent<SphereCollider>().bounds.center.z - first;
                jumpStart = first - step;
                Place(arc[0].GetComponent<SphereCollider>().bounds.center.x, jumpStart);
                started = Time.time; phase = 1;
            }
            else if (phase == 1)
            {
                if (!motor.GetComponent<CharacterController>().isGrounded) return;
                motor.Jump(); apex = motor.transform.position.y;
                started = Time.time; phase = 2;
            }
            else if (phase == 2)
            {
                apex = Mathf.Max(apex, motor.transform.position.y);
                if (elapsed < motor.HighJumpDuration + 0.4f) return;
                normalCollected = arc.Count(b => !b.gameObject.activeSelf);
                Check(normalCollected < arc.Length, "Normal jump cannot collect the whole high arc (" + normalCollected + "/" + arc.Length + ").");
                Check(apex < 2.5f, "Normal jump height remains unchanged.");
                foreach (var b in arc) b.gameObject.SetActive(true);
                Place(arc[0].GetComponent<SphereCollider>().bounds.center.x, jumpStart);
                var powers = motor.GetComponent<PowerUpManager>();
                Check(powers != null, "Player PowerUpManager remains available for the high jump.");
                powers.Activate(PowerUpType.Glider);
                started = Time.time; phase = 3;
            }
            else if (phase == 3)
            {
                if (!motor.GetComponent<CharacterController>().isGrounded) return;
                motor.Jump(); apex = motor.transform.position.y;
                started = Time.time; phase = 4;
            }
            else if (phase == 4)
            {
                apex = Mathf.Max(apex, motor.transform.position.y);
                if (elapsed < motor.HighJumpDuration + 0.4f) return;
                int high = arc.Count(b => !b.gameObject.activeSelf);
                Check(high > normalCollected && high >= arc.Length - 1, "Green jump collects the high arc (" + high + "/" + arc.Length + ", apex " + apex.ToString("F2") + "m).");
                Place(0, 603.5f);
                motor.LockLaneSwitching(0.4f);
                var inventory = motor.GetComponent<BananaInventory>();
                Check(inventory != null, "Player BananaInventory remains available for the vine swing.");
                initialBananas = inventory.Count;
                sawSwing = false; started = Time.time; phase = 5;
            }
            else if (phase == 5)
            {
                sawSwing |= vine.IsSwinging;
                if (!sawSwing || vine.IsSwinging) return;
                Check(vine.GetComponentsInChildren<BananaPickup>(true).Length == course.RopeBananaCount, "Exactly one bounded rope trail spawned.");
                var swingInventory = motor.GetComponent<BananaInventory>();
                var runnerInput = motor.GetComponent<RunnerInput>();
                Check(swingInventory != null, "Player BananaInventory remains available after the swing.");
                Check(runnerInput != null, "Player RunnerInput remains available after the swing.");
                Check(swingInventory.Count - initialBananas >= course.RopeBananaCount - 1, "Swing collects rope bananas into existing inventory.");
                Check(motor.enabled && motor.GetComponent<CharacterController>().enabled && runnerInput.enabled,
                    "Swing landing restores motor, input and controller.");
                // End-course speed and unchanged lateral/slide responsiveness.
                var progress = Object.FindFirstObjectByType<LevelProgressHUD>();
                Check(progress != null, "LevelProgressHUD remains available after the swing.");
                float origin = motor.transform.position.z - progress.DistanceTravelled;
                Place(0, origin + progress.RunLength * 0.92f);
                speedBefore = motor.CurrentSpeed;
                motor.ChangeLane(1);
                Check(motor.Slide(), "Slide starts at late-course speed.");
                started = Time.time; phase = 6;
            }
            else if (phase == 6)
            {
                if (elapsed < motor.LaneChangeDuration + 0.12f) return;
                Check(Mathf.Abs(motor.transform.position.x - motor.LaneDistance) < 0.02f, "Lane switching remains responsive at high speed.");
                Check(speedBefore > motor.ForwardSpeed * 1.23f && motor.IsSliding, "Late-course speed exceeds 1.23x while slide stays active.");
                started = Time.time; phase = 7;
            }
            else if (phase == 7)
            {
                if (motor.IsSliding) return;
                Check(true, "Slide restores standing state normally.");
                SceneManager.LoadScene("Level01", LoadSceneMode.Single);
                started = Time.time; phase = 8;
            }
            else if (phase == 8)
            {
                motor = Object.FindFirstObjectByType<RunnerMotor>();
                course = Object.FindFirstObjectByType<RunnerCourseGenerator>();
                if (elapsed < 0.5f && (motor == null || course == null || course.PatternCount == 0)) return;
                Check(motor != null, "Restarted Level01 contains RunnerMotor.");
                Check(course != null, "Restarted Level01 contains RunnerCourseGenerator.");
                Check(course.PatternCount > 0, "Restarted generator produced course rows.");
                Check(course.Difficulty < 0.001f && Mathf.Abs(motor.CurrentSpeed - motor.ForwardSpeed) < 0.001f,
                    "Actual scene restart resets difficulty and speed to baseline.");
                var restartedVine = Object.FindFirstObjectByType<VineSwingZone>();
                Check(restartedVine != null, "Restarted Level01 contains VineSwingZone.");
                Check(restartedVine.GetComponentsInChildren<BananaPickup>(true).Length == 0,
                    "Restart clears rope bananas for the next run.");
                Finish(true, "All progressive difficulty checks passed.");
                EditorApplication.isPlaying = false;
            }
        }
        catch (Exception e)
        {
            WriteReport("DETAIL: " + e + "\n");
            Finish(false, e.GetBaseException().Message);
            EditorApplication.isPlaying = false;
        }
    }
}
