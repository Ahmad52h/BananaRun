using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class NewAssetVerification
{
    const string Report = "Temp/new-assets-verification.txt";
    static int phase;
    static float phaseStarted;
    static double deadline;
    static RunnerMotor motor;
    static CharacterController controller;
    static CoconutThrower thrower;
    static Animator animator;
    static float throwStartZ;
    static bool slideObserved;
    static int heartsBeforeSlide;

    [InitializeOnLoadMethod]
    static void Connect()
    {
        EditorApplication.playModeStateChanged -= OnState;
        EditorApplication.playModeStateChanged += OnState;
    }

    [MenuItem("Tools/Banana Run/Verify New Slide Throw Assets")]
    public static void Begin()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode first.");
        File.WriteAllText(Report, "BananaRun imported asset verification\n");
        SessionState.SetBool("BananaRun.NewAssetTest", true);
        EditorApplication.isPlaying = true;
    }

    static void OnState(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("BananaRun.NewAssetTest", false)) return;
        SessionState.SetBool("BananaRun.NewAssetTest", false);
        Application.runInBackground = true;
        Time.timeScale = 1f;
        motor = Object.FindFirstObjectByType<RunnerMotor>();
        controller = motor.GetComponent<CharacterController>();
        thrower = motor.GetComponent<CoconutThrower>();
        animator = motor.GetComponentInChildren<Animator>();
        controller.enabled = false;
        motor.transform.position = new Vector3(0, -0.5f, 120);
        controller.enabled = true;
        controller.Move(Vector3.down * 0.03f);
        phase = 0;
        slideObserved = false;
        phaseStarted = Time.time;
        deadline = EditorApplication.timeSinceStartup + 30;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    static void Check(bool result, string message)
    {
        if (!result) throw new Exception(message);
        File.AppendAllText(Report, "PASS: " + message + "\n");
    }

    static void Next() { phase++; phaseStarted = Time.time; }

    static void Tick()
    {
        if (!EditorApplication.isPlaying) { EditorApplication.update -= Tick; return; }
        EditorApplication.QueuePlayerLoopUpdate();
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("New asset verification timed out.");
            float elapsed = Time.time - phaseStarted;
            if (elapsed < 0.2f) return;
            switch (phase)
            {
                case 0:
                    var slideClip = AssetDatabase.LoadAllAssetsAtPath("Assets/_Game/Animations/Monkey@Running Slide.fbx")
                        .OfType<AnimationClip>().First(c => c.name == "Running Slide");
                    var throwClip = AssetDatabase.LoadAllAssetsAtPath("Assets/_Game/Animations/Throw.fbx")
                        .OfType<AnimationClip>().First(c => c.name == "mixamo.com");
                    Check(slideClip.humanMotion && throwClip.humanMotion, "Imported slide and throw clips are valid Humanoid motions.");
                    Check(animator.HasState(0, Animator.StringToHash("Base Layer.Slide")), "Animator has a Slide state.");
                    int throwLayer = animator.GetLayerIndex("Throw Overlay");
                    Check(throwLayer > 0 && animator.HasState(throwLayer, Animator.StringToHash("Throw Overlay.Throw")),
                        "Animator has a masked throw overlay state.");
                    foreach (var type in new[] { PowerUpType.Bulldozer, PowerUpType.Magnet })
                    {
                        string path = "Assets/_Game/Prefabs/PowerUps/" + type + "Pickup.prefab";
                        var pickup = AssetDatabase.LoadAssetAtPath<PowerUpPickup>(path);
                        string modelName = type == PowerUpType.Bulldozer ? "Giant Golden Banana Visual" : "Magnet Visual";
                        Check(pickup.transform.Find(modelName) != null && pickup.GetComponentsInChildren<Renderer>().Length > 0 &&
                            pickup.GetComponent<SphereCollider>().isTrigger &&
                            pickup.GetComponent<Rigidbody>().isKinematic,
                            type + " uses the supplied model and a working pickup trigger.");
                    }
                    var beamPrefab = AssetDatabase.LoadAssetAtPath<RunnerObstacle>("Assets/_Game/Prefabs/Course/HighSlideBeam.prefab");
                    var beamCollider = beamPrefab != null ? beamPrefab.GetComponent<BoxCollider>() : null;
                    Check(beamCollider != null && beamCollider.center.y - beamCollider.size.y * 0.5f > 0.8f,
                        "Raised beam leaves enough space for the low slide collider.");
                    Object.Instantiate(beamPrefab, new Vector3(0, -0.3f, 125f), Quaternion.identity);
                    heartsBeforeSlide = motor.GetComponent<PlayerHealth>().Hearts;
                    Check(controller.isGrounded && motor.Slide(), "Grounded slide starts from keyboard or swipe action.");
                    Check(Mathf.Abs(controller.height - 0.8f) < 0.02f && Mathf.Abs(controller.center.y - 0.4f) < 0.02f,
                        "Slide lowers the CharacterController while keeping its feet on the ground.");
                    var velocityField = typeof(RunnerMotor).GetField("verticalVelocity",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                    float beforeJump = (float)velocityField.GetValue(motor);
                    motor.Jump();
                    Check(motor.IsSliding && Mathf.Approximately(beforeJump, (float)velocityField.GetValue(motor)),
                        "Jump input is ignored during a slide.");
                    Next();
                    break;
                case 1:
                    if (!slideObserved)
                    {
                        Check(animator.GetCurrentAnimatorStateInfo(0).IsName("Slide") ||
                            animator.GetNextAnimatorStateInfo(0).IsName("Slide"), "Slide animation plays during the low collider window.");
                        slideObserved = true;
                    }
                    if (elapsed < 0.85f) return;
                    Check(!motor.IsSliding && Mathf.Abs(controller.height - 2f) < 0.02f &&
                        Mathf.Abs(controller.center.y - 1f) < 0.02f,
                        "Slide collider restores after its duration.");
                    Check(motor.GetComponent<PlayerHealth>().Hearts == heartsBeforeSlide,
                        "Sliding passes safely beneath a raised wooden beam.");
                    for (int i = 0; i < 5; i++) Check(thrower.TryCollect(), "Coconut inventory accepts a pickup.");
                    Check(!thrower.TryCollect() && thrower.CurrentCoconuts == 5, "Coconut capacity caps at five.");
                    throwStartZ = motor.transform.position.z;
                    thrower.Throw();
                    Check(thrower.CurrentCoconuts == 4, "Throw consumes one coconut and leaves the remaining ammo.");
                    Next();
                    break;
                case 2:
                    int layer = animator.GetLayerIndex("Throw Overlay");
                    Check(animator.GetCurrentAnimatorStateInfo(layer).IsName("Throw") ||
                        animator.GetNextAnimatorStateInfo(layer).IsName("Throw"), "Throw animation plays in the upper-body layer.");
                    Check(motor.transform.position.z > throwStartZ + 0.5f && motor.enabled,
                        "Runner continues advancing during the throw animation.");
                    var projectilePrefab = AssetDatabase.LoadAssetAtPath<CoconutProjectile>("Assets/_Game/Prefabs/CoconutProjectile.prefab");
                    Check(new SerializedObject(projectilePrefab).FindProperty("smashBoxSound").objectReferenceValue != null,
                        "Projectile prefab has the imported SMASH BOX clip assigned.");
                    var crate = Object.FindObjectsByType<RunnerObstacle>(FindObjectsSortMode.None)
                        .First(o => new SerializedObject(o).FindProperty("breakable").boolValue && o.gameObject.activeInHierarchy);
                    var projectile = Object.Instantiate(projectilePrefab, crate.GetComponent<BoxCollider>().bounds.center, Quaternion.identity);
                    projectile.Initialize(motor.GetComponent<PlayerHealth>());
                    Physics.SyncTransforms();
                    projectile.SendMessage("Update");
                    Check(!crate.gameObject.activeSelf && Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None)
                        .Any(source => source.clip != null && source.clip.name.StartsWith("SMASH BOX") && source.isPlaying),
                        "Coconut smash destroys a crate and plays its impact sound.");
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
}
