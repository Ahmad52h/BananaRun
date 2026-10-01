using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class SlideHazardVerification
{
    const string Report = "Temp/slide-hazard-verification.txt";
    static RunnerMotor motor;
    static RunnerAnimation animation;
    static PlayerHealth health;
    static Animator animator;
    static CharacterController capsule;
    static int phase, hearts;
    static float started, x, z, speed, height;
    static double deadline;
    static BananaPeelHazard peel;
    static bool slipObserved;

    [InitializeOnLoadMethod]
    static void Connect() => EditorApplication.playModeStateChanged += OnState;

    [MenuItem("Tools/Banana Run/Verify Slide and Peel")]
    public static void Begin()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode first.");
        File.WriteAllText(Report, "Slide and peel play mode verification\n");
        SessionState.SetBool("VerifySlidePeel", true);
        EditorApplication.isPlaying = true;
    }

    static void OnState(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("VerifySlidePeel", false)) return;
        SessionState.SetBool("VerifySlidePeel", false);
        Application.runInBackground = true;
        Time.timeScale = 1;
        motor = Object.FindFirstObjectByType<RunnerMotor>();
        animation = motor.GetComponent<RunnerAnimation>();
        health = motor.GetComponent<PlayerHealth>();
        animator = motor.GetComponentInChildren<Animator>();
        capsule = motor.GetComponent<CharacterController>();
        motor.GetComponent<RunnerInput>().enabled = false;
        // Use an empty stretch so unrelated course obstacles cannot affect assertions.
        foreach (var obstacle in Object.FindObjectsByType<RunnerObstacle>(FindObjectsSortMode.None)) obstacle.gameObject.SetActive(false);
        capsule.enabled = false;
        motor.transform.position = new Vector3(0, -0.3f, 120);
        capsule.enabled = true;
        height = capsule.height;
        phase = 0; started = Time.time; deadline = EditorApplication.timeSinceStartup + 40;
        slipObserved = false;
        EditorApplication.update += Tick;
    }

    static void Check(bool pass, string message)
    {
        if (!pass) throw new Exception(message);
        File.AppendAllText(Report, "PASS: " + message + "\n");
    }
    static bool State(string name) => animator.GetCurrentAnimatorStateInfo(0).IsName(name) ||
        (animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsName(name));
    static void Next() { phase++; started = Time.time; }

    static void Tick()
    {
        if (!EditorApplication.isPlaying) { EditorApplication.update -= Tick; return; }
        EditorApplication.QueuePlayerLoopUpdate();
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Verification timeout.");
            float elapsed = Time.time - started;
            if (elapsed < 0.2f) return;
            switch (phase)
            {
                case 0:
                    Check(animator.avatar.isValid && animator.avatar.isHuman, "Valid humanoid MonkeyAvatar.");
                    Check(Object.FindFirstObjectByType<RunnerCourseGenerator>().SpawnedPeelCount > 0, "Course generator places intermittent peels.");
                    animation.TriggerThrow();
                    Check(capsule.isGrounded && motor.Slide(), "Grounded slide starts.");
                    Check(capsule.height < height, "Slide lowers collision capsule.");
                    Next(); break;
                case 1:
                    Check(State("Slide") && motor.IsSliding, "Slide remains active without Run overriding it.");
                    Check(animator.GetLayerWeight(animator.GetLayerIndex("Throw Overlay")) == 0, "Throw overlay is suppressed during slide.");
                    Capture("Temp/slide-pose.png");
                    Next(); break;
                case 2:
                    if (elapsed < 0.9f) return;
                    Check(!motor.IsSliding && Mathf.Approximately(capsule.height, height) && State("Run"), "Slide restores capsule and blends back to Run.");
                    var prefab = AssetDatabase.LoadAssetAtPath<BananaPeelHazard>("Assets/_Game/Prefabs/Course/BananaPeelHazard.prefab");
                    var data = new SerializedObject(prefab);
                    Check(prefab.GetComponent<BoxCollider>().isTrigger && prefab.GetComponent<Rigidbody>().isKinematic &&
                        !prefab.GetComponent<Rigidbody>().useGravity && data.FindProperty("slipSound").objectReferenceValue != null &&
                        data.FindProperty("slipPuff").objectReferenceValue != null, "Peel collider, body, sound and puff are wired.");
                    peel = Object.Instantiate(prefab, new Vector3(motor.transform.position.x, -0.3f, motor.transform.position.z + 2), Quaternion.identity);
                    hearts = health.Hearts; speed = motor.CurrentSpeed;
                    Next(); break;
                case 3:
                    if (!motor.IsLaneLocked) return;
                    x = motor.transform.position.x; z = motor.transform.position.z;
                    Check(health.Hearts == hearts && Mathf.Approximately(motor.CurrentSpeed, speed), "Real peel trigger preserves hearts and forward speed.");
                    Check(!peel.GetComponent<BoxCollider>().enabled, "Peel consumes itself once.");
                    motor.ChangeLane(1);
                    Check(Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Any(a => a.clip != null && a.clip.name == "CartoonSlip" && a.isPlaying), "Cartoon slip sound plays.");
                    Check(Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None).Any(p => p.name.Contains("SlipPuff") && p.isPlaying), "Slip puff plays.");
                    Next(); break;
                case 4:
                    if (!slipObserved)
                    {
                        Check(State("Hit") && animation.IsReactingToHit, "Slip plays the stumble state.");
                        Check(Mathf.Abs(motor.transform.position.x - x) < 0.001f && motor.transform.position.z > z + 0.5f,
                            "Lane input is ignored while forward movement continues.");
                        Capture("Temp/stumble-pose.png");
                        slipObserved = true;
                    }
                    if (elapsed < 0.9f) return;
                    Check(!motor.IsLaneLocked && !animation.IsReactingToHit && State("Run"), "Slip lock and stumble recover after 0.8 seconds.");
                    Check(health.Hearts == hearts, "Slip costs no hearts after recovery.");
                    motor.ChangeLane(1);
                    Next(); break;
                case 5:
                    Check(motor.transform.position.x > x + 0.1f, "Lane switching resumes after slip.");
                    var obstaclePrefab = AssetDatabase.LoadAssetAtPath<RunnerObstacle>("Assets/_Game/Prefabs/TallObstacle.prefab");
                    var obstacle = Object.Instantiate(obstaclePrefab, motor.transform.position, Quaternion.identity);
                    obstacle.SendMessage("OnTriggerEnter", capsule);
                    Check(health.Hearts == hearts - 1 && animation.IsReactingToHit, "Obstacle damage triggers stumble and costs one heart.");
                    Next(); break;
                case 6:
                    Check(State("Hit"), "Damage stumble is not interrupted by locomotion.");
                    Next(); break;
                case 7:
                    if (elapsed < 0.9f) return;
                    Check(State("Run") && !animation.IsReactingToHit, "Damage stumble recovers to Run.");
                    File.AppendAllText(Report, "ALL CHECKS PASSED\n");
                    EditorApplication.update -= Tick;
                    EditorApplication.isPlaying = false;
                    break;
            }
        }
        catch (Exception e)
        {
            File.AppendAllText(Report, "FAIL: " + e + "\n");
            EditorApplication.update -= Tick;
            EditorApplication.isPlaying = false;
            Debug.LogException(e);
        }
    }

    static void Capture(string path)
    {
        var go = new GameObject("Verification Camera");
        var camera = go.AddComponent<Camera>();
        var focus = animator.GetBoneTransform(HumanBodyBones.Hips).position + Vector3.up * 0.25f;
        File.AppendAllText(Report, path + " hips relative to motor: " + (focus - Vector3.up * 0.25f - motor.transform.position) + "\n");
        camera.transform.position = focus + new Vector3(3.5f, 1.8f, 3.5f);
        camera.transform.LookAt(focus);
        camera.fieldOfView = 38;
        var rt = RenderTexture.GetTemporary(700, 700, 24);
        var previous = RenderTexture.active;
        camera.targetTexture = rt;
        camera.Render();
        RenderTexture.active = rt;
        var image = new Texture2D(700, 700, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, 700, 700), 0, 0); image.Apply();
        File.WriteAllBytes(path, image.EncodeToPNG());
        RenderTexture.active = previous; camera.targetTexture = null;
        RenderTexture.ReleaseTemporary(rt); Object.DestroyImmediate(image); Object.DestroyImmediate(go);
    }
}
