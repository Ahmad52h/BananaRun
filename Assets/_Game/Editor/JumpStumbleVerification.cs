using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class JumpStumbleVerification
{
    const string Report = "Temp/jump-stumble-verification.txt";
    static RunnerMotor motor;
    static Animator animator;
    static CharacterController capsule;
    static PlayerHealth health;
    static Transform hips;
    static int phase, frame, hearts, captures;
    static float started, baselineY, minZ, maxZ, maxHeight, lastAngle, rotation, minFoot, maxFoot;
    static float maxForwardError, lastZ, lastTime, lastJumpTime;
    static Vector3 center, visualPosition;
    static double deadline;
    static Mesh sampledMesh;
    static float minMeshY;
    static RunnerObstacle hitObstacle;
    static string lowestVertex;

    [InitializeOnLoadMethod]
    static void Connect() => EditorApplication.playModeStateChanged += OnState;

    [MenuItem("Tools/Banana Run/Verify Jump and Stumble")]
    public static void Begin()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode first.");
        File.WriteAllText(Report, "Jump and stumble verification\n");
        SessionState.SetBool("VerifyJumpStumble", true);
        EditorApplication.isPlaying = true;
    }

    static void OnState(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("VerifyJumpStumble", false)) return;
        SessionState.SetBool("VerifyJumpStumble", false);
        Application.runInBackground = true;
        Time.timeScale = 1;
        motor = Object.FindAnyObjectByType<RunnerMotor>();
        animator = motor.GetComponentInChildren<Animator>();
        capsule = motor.GetComponent<CharacterController>();
        health = motor.GetComponent<PlayerHealth>();
        hips = animator.GetBoneTransform(HumanBodyBones.Hips);
        motor.GetComponent<RunnerInput>().enabled = false;
        foreach (var obstacle in Object.FindObjectsByType<RunnerObstacle>(FindObjectsSortMode.None)) obstacle.gameObject.SetActive(false);
        capsule.enabled = false;
        motor.transform.position = new Vector3(0, -0.3f, 120);
        capsule.enabled = true;
        phase = 0; started = Time.time; frame = -1;
        deadline = EditorApplication.timeSinceStartup + 45;
        EditorApplication.update += Tick;
    }

    static void Check(bool pass, string message)
    {
        if (!pass) throw new Exception(message);
        File.AppendAllText(Report, "PASS: " + message + "\n");
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying) { EditorApplication.update -= Tick; return; }
        EditorApplication.QueuePlayerLoopUpdate();
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Timed out.");
            if (frame == Time.frameCount) return;
            frame = Time.frameCount;
            float elapsed = Time.time - started;
            switch (phase)
            {
                case 0:
                    if (elapsed < 0.4f) return;
                    Check(capsule.isGrounded, "Runner starts on path.");
                    var hurdle = AssetDatabase.LoadAssetAtPath<RunnerObstacle>("Assets/_Game/Prefabs/Course/LogHurdle.prefab");
                    Object.Instantiate(hurdle, new Vector3(0, -0.3f, motor.transform.position.z + motor.ForwardSpeed * motor.JumpDuration * 0.5f), Quaternion.identity);
                    baselineY = motor.transform.position.y; hearts = health.Hearts;
                    minZ = float.PositiveInfinity; maxZ = float.NegativeInfinity; maxHeight = 0;
                    maxForwardError = 0; lastZ = motor.transform.position.z; lastTime = Time.time;
                    lastAngle = Angle(); rotation = 0; captures = 0; lastJumpTime = 0;
                    motor.Jump(); phase = 1; started = Time.time;
                    break;
                case 1:
                    maxForwardError = Mathf.Max(maxForwardError, Mathf.Abs(motor.transform.position.z - lastZ - motor.CurrentSpeed * (Time.time - lastTime)));
                    lastZ = motor.transform.position.z; lastTime = Time.time;
                    maxHeight = Mathf.Max(maxHeight, motor.transform.position.y - baselineY);
                    float angle = Angle(); rotation += Mathf.DeltaAngle(lastAngle, angle); lastAngle = angle;
                    if (!capsule.isGrounded)
                    {
                        minZ = Mathf.Min(minZ, hips.localPosition.z); maxZ = Mathf.Max(maxZ, hips.localPosition.z);
                        var s = animator.GetCurrentAnimatorStateInfo(0);
                        if (s.IsName("Jump")) lastJumpTime = s.normalizedTime;
                        if (elapsed > (captures + 1) * 0.23f && captures < 3) Capture("Temp/flip-" + captures++ + ".png");
                        return;
                    }
                    if (elapsed < 0.15f) return;
                    File.AppendAllText(Report, $"Flight={elapsed:F3}s, apex={maxHeight:F3}m, hip Z range={maxZ-minZ:F5}m, rotation={rotation:F1}deg, final clip phase={lastJumpTime:F3}, forward error={maxForwardError:F5}m\n");
                    Check(maxHeight > 2.1f && maxHeight < 2.5f, "Jump follows the intended 2.3m arc.");
                    Check(maxZ - minZ < 0.005f && maxForwardError < 0.005f, "Flip stays centered; motor supplies all forward travel.");
                    Check(Mathf.Abs(rotation) > 320 && lastJumpTime > 0.90f, "Full front flip completes at landing.");
                    Check(health.Hearts == hearts, "Jump clears the low hurdle without damage.");
                    phase = 2; started = Time.time;
                    break;
                case 2:
                    if (elapsed < 0.3f) return;
                    Check(animator.GetCurrentAnimatorStateInfo(0).IsName("Run"), "Landing blends back to Run.");
                    center = capsule.center; visualPosition = animator.transform.localPosition;
                    hitObstacle = Object.Instantiate(AssetDatabase.LoadAssetAtPath<RunnerObstacle>("Assets/_Game/Prefabs/TallObstacle.prefab"), motor.transform.position + Vector3.forward * 1.5f, Quaternion.identity);
                    minFoot = float.PositiveInfinity; maxFoot = float.NegativeInfinity; captures = 0;
                    minMeshY = float.PositiveInfinity; sampledMesh = new Mesh();
                    phase = 3; started = Time.time;
                    break;
                case 3:
                    if (health.Hearts == hearts) return;
                    Check(health.Hearts == hearts - 1, "Actual obstacle collision triggers damage.");
                    foreach (var renderer in hitObstacle.GetComponentsInChildren<Renderer>()) renderer.enabled = false;
                    phase = 4; started = Time.time;
                    break;
                case 4:
                    float sole = Mathf.Min(animator.GetBoneTransform(HumanBodyBones.LeftFoot).position.y - animator.leftFeetBottomHeight,
                        animator.GetBoneTransform(HumanBodyBones.RightFoot).position.y - animator.rightFeetBottomHeight);
                    minFoot = Mathf.Min(minFoot, sole); maxFoot = Mathf.Max(maxFoot, sole);
                    foreach (var skin in animator.GetComponentsInChildren<SkinnedMeshRenderer>())
                    {
                        skin.BakeMesh(sampledMesh);
                        var vertices = sampledMesh.vertices;
                        var weights = skin.sharedMesh.boneWeights;
                        for (int i = 0; i < vertices.Length; i++)
                        {
                            var world = skin.transform.TransformPoint(vertices[i]);
                            if (world.y >= minMeshY) continue;
                            minMeshY = world.y;
                            lowestVertex = $"{skin.name}, {skin.bones[weights[i].boneIndex0].name}, {world}, t={elapsed:F3}";
                        }
                    }
                    CheckUnchanged();
                    if (elapsed > (captures + 1) * 0.2f && captures < 3) Capture("Temp/stumble-ground-" + captures++ + ".png");
                    if (elapsed < 0.7f) return;
                    File.AppendAllText(Report, $"Stumble sole Y range={minFoot:F4}..{maxFoot:F4}, path surface=-0.3000\n");
                    File.AppendAllText(Report, $"Lowest skinned vertex Y={minMeshY:F4}\n");
                    File.AppendAllText(Report, lowestVertex + "\n");
                    Check(minMeshY > -0.34f, "Stumble mesh remains above the floor.");
                    Check(minMeshY >= -0.34f && minMeshY <= -0.28f, "Mesh sole contact stays on the path throughout stumble.");
                    Check(animator.GetCurrentAnimatorStateInfo(0).IsName("Hit"), "Stumble clip is connected and plays.");
                    phase = 5; started = Time.time;
                    break;
                case 5:
                    if (elapsed < 0.4f) return;
                    Check(animator.GetCurrentAnimatorStateInfo(0).IsName("Run"), "Stumble recovers smoothly to Run.");
                    CheckUnchanged();
                    File.AppendAllText(Report, "ALL CHECKS PASSED\n");
                    Object.DestroyImmediate(sampledMesh);
                    EditorApplication.update -= Tick; EditorApplication.isPlaying = false;
                    break;
            }
        }
        catch (Exception e)
        {
            File.AppendAllText(Report, "FAIL: " + e + "\n");
            EditorApplication.update -= Tick; EditorApplication.isPlaying = false;
            Debug.LogException(e);
        }
    }

    static void CheckUnchanged()
    {
        if (capsule.center != center || animator.transform.localPosition != visualPosition)
            throw new Exception("Stumble changed collider center or visual transform position.");
    }
    static float Angle() => Mathf.Atan2(hips.up.z, hips.up.y) * Mathf.Rad2Deg;

    static void Capture(string path)
    {
        var go = new GameObject("Pose Verification Camera");
        var camera = go.AddComponent<Camera>();
        var focus = motor.transform.position + Vector3.up * 0.8f;
        camera.transform.position = focus + new Vector3(4, 1.3f, 3);
        camera.transform.LookAt(focus); camera.fieldOfView = 42;
        var rt = RenderTexture.GetTemporary(640, 640, 24); var previous = RenderTexture.active;
        camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
        var image = new Texture2D(640, 640, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, 640, 640), 0, 0); image.Apply(); File.WriteAllBytes(path, image.EncodeToPNG());
        RenderTexture.active = previous; camera.targetTexture = null; RenderTexture.ReleaseTemporary(rt);
        Object.DestroyImmediate(image); Object.DestroyImmediate(go);
    }
}
