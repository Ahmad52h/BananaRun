using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class PowerSystemsVerification
{
    const string Report = "Temp/power-systems-verification.txt";
    static int phase;
    static float started;
    static double deadline;
    static RunnerMotor motor;
    static PowerUpManager powers;
    static CharacterController controller;
    static PlayerHealth health;
    static BananaPickup magnetBanana;
    static float bananaDistance;
    static RunnerObstacle targetObstacle;
    static int previousPoints;
    static int previousHearts;
    static FallingCoconutHazard falling;
    static Transform warningShadow;
    static float initialShadowScale;
    static bool swingStartedLogged;
    static bool warningLogged;

    [InitializeOnLoadMethod]
    static void Connect()
    {
        EditorApplication.playModeStateChanged -= OnState;
        EditorApplication.playModeStateChanged += OnState;
    }

    [MenuItem("Tools/Banana Run/Verify Power Systems")]
    public static void Begin()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode first.");
        File.WriteAllText(Report, "BananaRun power systems verification\n");
        SessionState.SetBool("BananaRun.PowerTest", true);
        EditorApplication.isPlaying = true;
    }

    static void OnState(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("BananaRun.PowerTest", false)) return;
        SessionState.SetBool("BananaRun.PowerTest", false);
        Application.runInBackground = true;
        Time.timeScale = 1f;
        motor = Object.FindFirstObjectByType<RunnerMotor>();
        powers = motor.GetComponent<PowerUpManager>();
        controller = motor.GetComponent<CharacterController>();
        health = motor.GetComponent<PlayerHealth>();
        motor.enabled = false;
        motor.GetComponent<RunnerInput>().enabled = false;
        phase = 0;
        swingStartedLogged = false;
        warningLogged = false;
        started = Time.time;
        deadline = EditorApplication.timeSinceStartup + 45;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        File.AppendAllText(Report, "PASS: " + message + "\n");
    }

    static void Next() { phase++; started = Time.time; }

    static void Place(Vector3 position)
    {
        controller.enabled = false;
        motor.transform.position = position;
        controller.enabled = true;
        controller.Move(Vector3.forward * 0.001f);
        Physics.SyncTransforms();
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying) { EditorApplication.update -= Tick; return; }
        EditorApplication.QueuePlayerLoopUpdate();
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Power test timed out.");
            float elapsed = Time.time - started;
            if (elapsed < 0.25f) return;
            switch (phase)
            {
                case 0:
                    Check(powers != null, "Player has PowerUpManager.");
                    Check(Object.FindObjectsByType<PowerUpPickup>(FindObjectsSortMode.None).Length >= 3, "All three power-ups spawned on the course.");
                    Check(Object.FindObjectsByType<FallingCoconutSpawner>(FindObjectsSortMode.None).Length > 0, "Palm trees carry timed coconut hazards.");
                    var gap = Object.FindFirstObjectByType<VineSwingZone>();
                    Check(gap != null && gap.GetComponent<BoxCollider>().isTrigger && gap.GetComponent<Rigidbody>().isKinematic,
                        "River gap has a CharacterController-compatible vine trigger.");
                    Check(!GameObject.Find("Ground").GetComponent<BoxCollider>().enabled &&
                        GameObject.Find("Ground Before River").GetComponent<Collider>().bounds.max.z <= 610.01f &&
                        GameObject.Find("Landing Ground").GetComponent<Collider>().bounds.min.z >= 641.99f,
                        "The path and collision surface have a real river gap.");
                    var audio = RunnerAudio.Instance;
                    audio.PlayBananaCollect();
                    Check(audio.LastBananaPitch >= 0.9f && audio.LastBananaPitch <= 1.1f,
                        "First banana pickup pitch stays within the natural variation range.");
                    audio.PlayBananaCollect();
                    Check(audio.LastBananaPitch >= 0.9f && audio.LastBananaPitch <= 1.1f && audio.BananaCombo == 2,
                        "Consecutive banana pickup keeps combo tracking and pitch variation.");
                    for (int i = 0; i < 8; i++) audio.PlayBananaCollect();
                    Check(audio.LastBananaPitch >= 0.9f && audio.LastBananaPitch <= 1.1f,
                        "Repeated pickups never reach harsh high pitches.");
                    var bananaPrefab = AssetDatabase.LoadAssetAtPath<BananaPickup>("Assets/_Game/Prefabs/Banana.prefab");
                    Check(new SerializedObject(bananaPrefab).FindProperty("pickupEffectPrefab").objectReferenceValue != null,
                        "Banana pickup has its golden sparkle particle prefab assigned.");
                    Next();
                    break;
                case 1:
                    if (elapsed < 1.3f) return;
                    RunnerAudio.Instance.PlayBananaCollect();
                    Check(RunnerAudio.Instance.BananaCombo == 1 && RunnerAudio.Instance.LastBananaPitch >= 0.9f &&
                        RunnerAudio.Instance.LastBananaPitch <= 1.1f,
                        "Pickup combo resets after the timer lapses while pitch stays balanced.");
                    powers.Activate(PowerUpType.Bulldozer);
                    previousHearts = health.Hearts;
                    previousPoints = powers.BonusPoints;
                    targetObstacle = Object.FindObjectsByType<RunnerObstacle>(FindObjectsSortMode.None)
                        .First(o => o.transform.position.z > 300 && o.gameObject.activeInHierarchy);
                    Place(targetObstacle.GetComponent<BoxCollider>().bounds.center - Vector3.up * 0.6f);
                    Next();
                    break;
                case 2:
                    Check(!targetObstacle.gameObject.activeSelf && powers.BonusPoints > previousPoints && health.Hearts == previousHearts,
                        "Bulldozer shatters an obstacle for points without damage.");
                    Check(motor.GetComponentInChildren<Animator>().transform.localScale.magnitude > 1.1f,
                        "Giant banana smoothly enlarges the monkey model.");
                    powers.Activate(PowerUpType.Glider);
                    powers.Activate(PowerUpType.Magnet);
                    controller.Move(Vector3.up * 0.4f);
                    Check(!controller.isGrounded && Mathf.Abs(motor.EffectiveGravity - motor.BaseGravity * 0.35f) < 0.01f,
                        "Palm leaf glider cuts airborne gravity by 65 percent.");
                    magnetBanana = Object.FindObjectsByType<BananaPickup>(FindObjectsSortMode.None)
                        .First(b => b.gameObject.activeInHierarchy);
                    magnetBanana.transform.position = motor.transform.position + new Vector3(4f, 0.8f, 0);
                    bananaDistance = Vector3.Distance(magnetBanana.transform.position, motor.transform.position);
                    Next();
                    break;
                case 3:
                    Check(powers.IsActive(PowerUpType.Glider) && powers.IsActive(PowerUpType.Magnet),
                        "Glider and magnet can run together.");
                    Check(!magnetBanana.gameObject.activeSelf ||
                        Vector3.Distance(magnetBanana.transform.position, motor.transform.position) < bananaDistance - 0.5f,
                        "Magnet pulls a nearby banana toward the monkey.");
                    Place(new Vector3(motor.transform.position.x, -0.5f, motor.transform.position.z));
                    var hazardPrefab = AssetDatabase.LoadAssetAtPath<FallingCoconutHazard>("Assets/_Game/Prefabs/PowerUps/FallingCoconut.prefab");
                    var target = new Vector3(motor.transform.position.x, -0.3f, motor.transform.position.z);
                    falling = Object.Instantiate(hazardPrefab);
                    falling.Initialize(target);
                    warningShadow = falling.transform.Find("Growing Landing Shadow");
                    initialShadowScale = warningShadow.localScale.x;
                    previousPoints = powers.BonusPoints;
                    previousHearts = health.Hearts;
                    Next();
                    break;
                case 4:
                    if (elapsed < 1.2f)
                    {
                        if (!warningLogged)
                        {
                            Check(falling != null && warningShadow.localScale.x > initialShadowScale &&
                                powers.BonusPoints == previousPoints && health.Hearts == previousHearts,
                                "Falling coconut displays a growing warning before impact.");
                            warningLogged = true;
                        }
                        return;
                    }
                    Check(falling == null && powers.BonusPoints > previousPoints && health.Hearts == previousHearts,
                        "Falling coconut impacts after warning and bulldozer blocks damage.");
                    var zone = Object.FindFirstObjectByType<VineSwingZone>();
                    motor.enabled = true;
                    motor.GetComponent<RunnerInput>().enabled = true;
                    Place(new Vector3(zone.transform.position.x, -0.5f, zone.transform.position.z));
                    motor.LockLaneSwitching(0.6f);
                    Next();
                    break;
                case 5:
                    if (elapsed < 0.6f && !swingStartedLogged)
                    {
                        Check(!controller.enabled && !motor.enabled, "Entering the gap starts the scripted vine swing.");
                        swingStartedLogged = true;
                    }
                    if (elapsed < 2.4f) return;
                    Check(controller.enabled && motor.enabled && motor.transform.position.z > 644f,
                        "Vine swing releases onto the landing track and restores running.");
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
