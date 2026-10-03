using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class VineSwingVerification
{
    const string Report = "Temp/vine-verification.txt";
    static RunnerMotor motor;
    static VineSwingZone zone;
    static Animator animator;
    static int scenario, phase, initialFrame;
    static float started, resumeZ, maxError, minY;
    static bool sawHang, sawRelease, heardWhoosh, sawLeaves;
    static double deadline;
    [InitializeOnLoadMethod]
    static void Register()
    {
        EditorApplication.playModeStateChanged -= State;
        EditorApplication.playModeStateChanged += State;
    }
    [MenuItem("Tools/Banana Run/Verify Polished Vine Swing")]
    public static void Begin()
    {
        if (EditorApplication.isPlaying) throw new Exception("Stop play mode first.");
        File.WriteAllText(Report, "Vine swing runtime verification\n");
        SessionState.SetBool("VineVerification", true);
        EditorApplication.isPlaying = true;
    }
    static void State(PlayModeStateChange change)
    {
        if (change != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("VineVerification", false)) return;
        SessionState.SetBool("VineVerification", false);
        Application.runInBackground = true;
        Time.timeScale = 1;
        motor = Object.FindFirstObjectByType<RunnerMotor>();
        zone = Object.FindFirstObjectByType<VineSwingZone>();
        animator = motor.GetComponentInChildren<Animator>();
        scenario = 0; phase = 0;
        started = Time.time; initialFrame = Time.frameCount;
        deadline = EditorApplication.timeSinceStartup + 90;
        EditorApplication.update += Tick;
    }
    static void Check(bool ok, string message)
    {
        if (!ok) throw new Exception(message);
        File.AppendAllText(Report, "PASS: " + message + "\n");
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying) { EditorApplication.update -= Tick; return; }
        EditorApplication.QueuePlayerLoopUpdate();
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Timed out.");
            if (phase == 0)
            {
                if (Time.time - started < 1.2f) return;
                Check(Time.frameCount > initialFrame + 2, "Player loop advances.");
                var cc = motor.GetComponent<CharacterController>();
                cc.enabled = false;
                float lane = scenario == 1 ? -motor.LaneDistance : scenario == 2 ? motor.LaneDistance : 0;
                motor.transform.position = new Vector3(lane, scenario == 1 ? 1.1f : -0.22f, 603.5f);
                cc.enabled = true;
                motor.ResetVerticalVelocity();
                motor.LockLaneSwitching(0.4f);
                zone.enabled = true;
                Physics.SyncTransforms();
                sawHang = sawRelease = heardWhoosh = sawLeaves = false;
                maxError = 0; minY = 100;
                started = Time.time; phase = 1;
            }
            else if (phase == 1)
            {
                if (!zone.IsSwinging)
                {
                    if (Time.time - started > 2) throw new Exception("Real trigger did not catch player.");
                    return;
                }
                started = Time.time; phase = 2;
            }
            else if (phase == 2)
            {
                float elapsed = Time.time - started;
                if (zone.IsSwinging && !zone.IsReleased)
                {
                    sawHang |= animator.GetCurrentAnimatorStateInfo(0).IsName("VineSwing");
                    heardWhoosh |= zone.GetComponent<AudioSource>().isPlaying && zone.GetComponent<AudioSource>().volume > 0.2f;
                    var leaves = GameObject.Find("Falling Jungle Leaves").GetComponent<ParticleSystem>();
                    sawLeaves |= leaves.particleCount > 0;
                    if (elapsed > 0.3f) maxError = Mathf.Max(maxError, zone.GripError);
                    minY = Mathf.Min(minY, motor.transform.position.y);
                    if (scenario == 3 && elapsed > 0.6f)
                    {
                        zone.enabled = false;
                        Check(motor.enabled && ccEnabled() && motor.GetComponent<RunnerInput>().enabled, "Disabling vine restores all movement components.");
                        started = Time.time; resumeZ = motor.transform.position.z; phase = 3;
                        return;
                    }
                }
                sawRelease |= zone.IsReleased && (animator.GetCurrentAnimatorStateInfo(0).IsName("Jump") || animator.GetNextAnimatorStateInfo(0).IsName("Jump"));
                if (zone.IsSwinging) return;
                Check(sawHang && sawRelease, "Scenario " + scenario + ": hanging animation transitions to release flip.");
                Check(maxError < 0.015f, "Palm remains on rope; maximum error " + maxError.ToString("F4") + "m.");
                Check(minY > -3f, "Swing stays above river; lowest root " + minY.ToString("F2") + "m.");
                Check(heardWhoosh && sawLeaves, "Whoosh plays and anchor emits leaves.");
                Check(motor.transform.position.z >= 645 && ccEnabled() && motor.enabled && motor.GetComponent<RunnerInput>().enabled, "Safe bank landing restores controller, motor and input.");
                started = Time.time; resumeZ = motor.transform.position.z; phase = 3;
            }
            else if (phase == 3)
            {
                if (Time.time - started < 0.35f) return;
                Check(motor.transform.position.z > resumeZ + 1f, "Running continues after landing/interruption.");
                Check(motor.GetComponent<CharacterController>().isGrounded, "Runner grounded on continuation track.");
                if (++scenario < 4) { phase = 0; started = Time.time; }
                else
                {
                    File.AppendAllText(Report, "ALL CHECKS PASSED\n");
                    EditorApplication.update -= Tick;
                    EditorApplication.isPlaying = false;
                }
            }
        }
        catch (Exception e)
        {
            File.AppendAllText(Report, "FAIL: " + e.Message + "\n");
            EditorApplication.update -= Tick;
            EditorApplication.isPlaying = false;
        }
    }
    static bool ccEnabled() => motor.GetComponent<CharacterController>().enabled;
}
