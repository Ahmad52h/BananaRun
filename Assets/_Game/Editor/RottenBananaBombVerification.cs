using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Integration checks use real pickup/trap triggers and the existing coconut action.</summary>
public static class RottenBananaBombVerification
{
    private const string Report = "Temp/rotten-banana-bomb-verification.txt";
    private static RunnerMotor motor;
    private static PlayerHealth health;
    private static CoconutThrower coconut;
    private static BombDropper bombs;
    private static GiantGorillaChaser gorilla;
    private static RivalRunner rival;
    private static RottenBananaBomb placed;
    private static int phase, startFrame, coconutCount, bombCount;
    private static float since, groundY;
    private static double deadline;
    private static Vector3 trapPosition;
    private static Keyboard keyboard;
    private static bool checkedBombLanding;

    private static AudioSource BombVoice => RunnerAudio.Instance.GetComponents<AudioSource>()
        .FirstOrDefault(s => s.clip != null && (s.clip.name == "BombDrop" || s.clip.name == "BombExplosion"));

    [InitializeOnLoadMethod]
    private static void Register()
    {
        EditorApplication.playModeStateChanged -= State;
        EditorApplication.playModeStateChanged += State;
    }

    [MenuItem("Tools/Banana Run/Verify Secondary Rotten Banana Bomb")]
    public static void Begin()
    {
        if (EditorApplication.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "Level01")
            throw new InvalidOperationException("Open Level01 in edit mode.");
        File.WriteAllText(Report, "Secondary Rotten Banana Bomb integration checks\n");
        SessionState.SetBool("VerifyRottenBomb", true);
        EditorApplication.isPlaying = true;
    }

    private static void State(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingPlayMode) Cleanup();
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("VerifyRottenBomb", false)) return;
        SessionState.SetBool("VerifyRottenBomb", false);
        Application.runInBackground = true;
        Time.timeScale = 1;
        motor = Object.FindFirstObjectByType<RunnerMotor>();
        health = motor.GetComponent<PlayerHealth>();
        bombs = motor.GetComponent<BombDropper>();
        coconut = motor.GetComponent<CoconutThrower>();
        gorilla = Object.FindFirstObjectByType<GiantGorillaChaser>();
        rival = Object.FindFirstObjectByType<RivalRunner>();
        phase = 0;
        checkedBombLanding = false;
        startFrame = Time.frameCount;
        since = Time.time;
        deadline = EditorApplication.timeSinceStartup + 70;
        EditorApplication.update += Tick;
    }

    private static void Check(bool value, string message)
    {
        if (!value) throw new Exception(message);
        File.AppendAllText(Report, "PASS: " + message + "\n");
    }

    private static void Next() { phase++; since = Time.time; }

    private static void PlacePlayer(Vector3 point)
    {
        var controller = motor.GetComponent<CharacterController>();
        controller.enabled = false;
        motor.transform.position = point;
        controller.enabled = true;
        motor.ResetVerticalVelocity();
        Physics.SyncTransforms();
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying) { Cleanup(); return; }
        EditorApplication.QueuePlayerLoopUpdate();
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Play Mode verification timed out.");
            float elapsed = Time.time - since;
            switch (phase)
            {
                case 0:
                    if (elapsed < 0.5f) return;
                    Check(Time.frameCount > startFrame + 3, "Play Mode frames advance.");
                    Check(bombs != null && bombs.CurrentBombs == 0 && coconut.CurrentCoconuts > 0, "Separate bomb slot starts empty; coconut starting ammo remains intact.");
                    Check(Object.FindObjectsByType<BombPickup>(FindObjectsSortMode.None).Length >= 3, "Intro and generated track bomb pickups exist alongside coconut pickups.");
                    Check(Object.FindObjectsByType<CoconutPickup>(FindObjectsSortMode.None).Length > 0, "Coconut pickups remain on the course.");
                    Check(GameObject.Find("Drop Bomb Button").GetComponent<Button>().onClick.GetPersistentEventCount() == 1, "Secondary UI action is wired independently.");
                    Check(GameObject.Find("Bomb Icon").GetComponent<Image>().sprite != null &&
                        GameObject.Find("Bomb Action Icon").GetComponent<Image>().sprite != null, "Both bomb HUD icons reference an imported sprite.");
                    var bombButton = GameObject.Find("Drop Bomb Button").GetComponent<Button>();
                    var coconutButton = GameObject.Find("ThrowButton").GetComponent<Button>();
                    Check(bombButton.image.sprite == coconutButton.image.sprite && bombButton.image.type == coconutButton.image.type &&
                        bombButton.image.color == coconutButton.image.color && bombButton.colors.Equals(coconutButton.colors) &&
                        bombButton.GetComponent<RectTransform>().sizeDelta == coconutButton.GetComponent<RectTransform>().sizeDelta,
                        "Bomb and coconut actions use identical circular backgrounds, dimensions, and button colors.");
                    Check(!bombButton.GetComponentsInChildren<TMP_Text>().Any(t => t.gameObject.activeInHierarchy), "Bomb action is a clean icon with no text box.");
                    Check(AssetDatabase.GetAssetPath(GameObject.Find("Bomb Action Icon").GetComponent<Image>().sprite).EndsWith("bomb_icon_unity.png"), "Bomb action uses the supplied polished sprite.");
                    var audioData = new SerializedObject(RunnerAudio.Instance);
                    var blastClip = (AudioClip)audioData.FindProperty("bombExplosionSound").objectReferenceValue;
                    Check(blastClip != null && blastClip.loadState == AudioDataLoadState.Loaded && blastClip.loadType == AudioClipLoadType.DecompressOnLoad,
                        "Bomb explosion PCM is preloaded for immediate playback.");
                    foreach (var obstacle in Object.FindObjectsByType<RunnerObstacle>(FindObjectsSortMode.None)) obstacle.gameObject.SetActive(false);
                    foreach (var hazard in Object.FindObjectsByType<FallingCoconutSpawner>(FindObjectsSortMode.None)) hazard.enabled = false;
                    foreach (var thrower in Object.FindObjectsByType<RivalProjectileThrower>(FindObjectsSortMode.None)) thrower.enabled = false;
                    if (rival != null) rival.enabled = false;
                    var motorData = new SerializedObject(motor);
                    motorData.FindProperty("forwardSpeed").floatValue = 0;
                    motorData.ApplyModifiedPropertiesWithoutUndo();
                    PlacePlayer(new Vector3(0, -0.22f, 100));
                    coconutCount = coconut.CurrentCoconuts;
                    var pickup = Object.Instantiate(AssetDatabase.LoadAssetAtPath<BombPickup>(RottenBananaBombSetup.PickupPath), motor.transform.position + Vector3.up * 0.6f, Quaternion.identity);
                    pickup.name = "Verification Bomb Pickup";
                    Physics.SyncTransforms();
                    Next();
                    break;
                case 1:
                    if (elapsed < 0.25f) return;
                    Check(bombs.CurrentBombs == 1 && !GameObject.FindObjectsByType<BombPickup>(FindObjectsSortMode.None).Any(p => p.name == "Verification Bomb Pickup"), "A real player trigger collects a bomb exactly once.");
                    Check(coconut.CurrentCoconuts == coconutCount, "Bomb collection leaves coconut ammo unchanged.");
                    while (bombs.CurrentBombs < bombs.MaxBombs) Check(bombs.TryCollect(), "Bomb inventory accepts another pickup.");
                    Check(!bombs.TryCollect() && bombs.CurrentBombs == bombs.MaxBombs, "Bomb capacity rejects overflow.");
                    Check(GameObject.Find("Bomb Count").GetComponent<TMP_Text>().text == "x" + bombs.MaxBombs, "Bomb UI counter updates independently.");
                    bombCount = bombs.CurrentBombs;
                    coconut.Throw();
                    Check(coconut.CurrentCoconuts == coconutCount - 1 && bombs.CurrentBombs == bombCount, "Existing coconut throw consumes only a coconut.");
                    GameObject.Find("Drop Bomb Button").GetComponent<Button>().onClick.Invoke();
                    Check(bombs.CurrentBombs == bombCount - 1 && coconut.CurrentCoconuts == coconutCount - 1, "Drop button spends one bomb and leaves coconut ammo intact.");
                    Check(BombVoice != null && BombVoice.isPlaying && BombVoice.clip.name == "BombDrop", "Placement immediately plays the short drop cue.");
                    placed = Object.FindFirstObjectByType<RottenBananaBomb>();
                    Check(placed != null && placed.transform.position.z < motor.transform.position.z - 1f, "Bomb is placed directly behind the monkey.");
                    Check(placed.GetComponent<Rigidbody>().isKinematic && placed.GetComponent<SphereCollider>().isTrigger, "Placed bomb has a stationary body and trigger collider.");
                    trapPosition = placed.transform.position;
                    PlacePlayer(trapPosition);
                    Next();
                    break;
                case 2:
                    if (elapsed < 0.2f) return;
                    Check(placed != null && Vector3.Distance(placed.transform.position, trapPosition) < 0.001f && !health.IsRunOver, "Bomb stays stationary and ignores its player owner.");
                    PlacePlayer(trapPosition + Vector3.forward * 10.5f);
                    groundY = gorilla.transform.position.y;
                    Next();
                    break;
                case 3:
                    if (!gorilla.IsBombStunned) return;
                    Check(placed == null, "Gorilla trigger detonates and removes the bomb.");
                    Check(Object.FindObjectsByType<GiantImpactEffect>(FindObjectsSortMode.None).Any(e => e.name.StartsWith("FX_RottenBananaBomb")), "Detonation instantiates the dust explosion effect.");
                    Check(BombVoice != null && BombVoice.isPlaying && BombVoice.clip.name == "BombExplosion", "The supplied processed bomb explosion plays on the dedicated channel.");
                    int sample = BombVoice.timeSamples;
                    RunnerAudio.Instance.PlayBombExplosion();
                    RunnerAudio.Instance.PlayBombDrop();
                    Check(BombVoice.timeSamples == sample && BombVoice.clip.name == "BombExplosion" &&
                        Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Count(s => s.isPlaying && s.clip != null &&
                            (s.clip.name == "BombExplosion" || s.clip.name == "BombDrop")) == 1,
                        "Repeated impacts and placement cues cannot overlap or restart the active explosion.");
                    Check(!Object.FindObjectsByType<GiantImpactEffect>(FindObjectsSortMode.None)
                        .Where(e => e.name.StartsWith("FX_RottenBananaBomb")).Any(e => e.GetComponent<AudioSource>() != null),
                        "Explosion particles do not duplicate the bomb sound.");
                    Next();
                    break;
                case 4:
                    if (elapsed < 0.18f) return;
                    Check(gorilla.IsBombStunned && gorilla.transform.position.y > groundY + 0.2f, "Gorilla launches vertically and remains stunned.");
                    Check(gorilla.GetComponentsInChildren<Renderer>().Any(r => r.enabled), "Bomb hit reveals the gorilla reaction.");
                    var gorillaAnimator = gorilla.GetComponentInChildren<Animator>();
                    Check(gorillaAnimator.GetCurrentAnimatorStateInfo(0).IsName("Hit") &&
                        gorillaAnimator.GetCurrentAnimatorClipInfo(0).Any(c => c.clip.name == "Stumble") &&
                        Mathf.Abs(gorillaAnimator.GetFloat("StumbleSpeed") - motor.GetComponent<RunnerAnimation>().HitClipLength / 2.2f) < 0.02f,
                        "Gorilla plays the player's Stumble clip with matching duration-scaled timing.");
                    Next();
                    break;
                case 5:
                    if (gorilla.IsBombStunned) return;
                    // Editor callbacks can observe the timer before the chaser's LateUpdate.
                    // Allow the 0.15-second Run blend to finish before checking its state.
                    if (elapsed < 2.35f) return;
                    Check(Mathf.Abs(gorilla.transform.position.y - groundY) < 0.15f, "Gorilla lands and recovers after its temporary stun.");
                    Check(gorilla.GetComponentInChildren<Animator>().GetCurrentAnimatorStateInfo(0).IsName("Run") ||
                        gorilla.GetComponentInChildren<Animator>().GetNextAnimatorStateInfo(0).IsName("Run"), "Gorilla blends back to Run after its Stumble reaction.");
                    keyboard = InputSystem.AddDevice<Keyboard>("BombVerificationKeyboard");
                    bombCount = bombs.CurrentBombs;
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.B));
                    Next();
                    break;
                case 6:
                    if (elapsed < 0.15f) return;
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    Check(bombs.CurrentBombs == bombCount - 1, "B key invokes the independent secondary action.");
                    foreach (var trap in Object.FindObjectsByType<RottenBananaBomb>(FindObjectsSortMode.None)) Object.Destroy(trap.gameObject);
                    // Use the real rival controller and real trap collision for the enemy path.
                    var cc = rival.GetComponent<CharacterController>();
                    cc.enabled = false;
                    rival.transform.position = new Vector3(0, trapPosition.y, 160);
                    cc.enabled = true;
                    rival.enabled = true;
                    rival.CollectPowerUp(PowerUpType.Glider);
                    Object.Instantiate(AssetDatabase.LoadAssetAtPath<RottenBananaBomb>(RottenBananaBombSetup.TrapPath), rival.transform.position, Quaternion.identity);
                    groundY = rival.transform.position.y;
                    Physics.SyncTransforms();
                    Next();
                    break;
                case 7:
                    if (elapsed < 0.2f) return;
                    Check(rival.IsProjectileStunned && rival.CurrentSpeed == 0 && rival.transform.position.y > groundY + 0.2f, "Enemy trigger launches the rival and temporarily stops forward movement.");
                    Check(Object.FindObjectsByType<RottenBananaBomb>(FindObjectsSortMode.None).Length == 0, "Enemy consumes the trap only once.");
                    var rivalAnimator = rival.GetComponentInChildren<Animator>();
                    Check(rivalAnimator.GetCurrentAnimatorStateInfo(0).IsName("Hit") &&
                        rivalAnimator.GetCurrentAnimatorClipInfo(0).Any(c => c.clip.name == "Stumble") &&
                        Mathf.Abs(rivalAnimator.GetFloat("StumbleSpeed") - motor.GetComponent<RunnerAnimation>().HitClipLength / 2.2f) < 0.02f,
                        "Rival plays the same timed Stumble reaction as the player and gorilla.");
                    Next();
                    break;
                case 8:
                    if (!checkedBombLanding && elapsed >= 0.8f)
                    {
                        Check(rival.IsGrounded && rival.IsProjectileStunned, "Bomb launch uses normal gravity even with a high-jump power-up active.");
                        checkedBombLanding = true;
                    }
                    if (rival.IsProjectileStunned) return;
                    Check(!rival.IsStumbling && rival.IsGrounded, "Rival stun expires and normal grounded movement resumes.");
                    var before = bombs.CurrentBombs;
                    var position = motor.transform.position;
                    PlacePlayer(new Vector3(position.x, position.y, 625));
                    Check(!bombs.TryDrop() && bombs.CurrentBombs == before, "Dropping over the river does not consume ammo.");
                    PlacePlayer(position);
                    Time.timeScale = 0;
                    Check(!bombs.TryDrop() && bombs.CurrentBombs == before, "Pause blocks secondary action without spending ammo.");
                    Time.timeScale = 1;
                    Check(bombs.TryDrop(), "Last bomb can be dropped after recovery.");
                    Check(bombs.CurrentBombs == 0 && !bombs.TryDrop(), "Empty bomb inventory cannot place another trap.");
                    Check(coconut.CurrentCoconuts == coconutCount - 1, "Coconut ammo stays unchanged throughout bomb use.");
                    bombs.TryCollect();
                    before = bombs.CurrentBombs;
                    health.EndRun();
                    Check(!bombs.TryDrop() && !bombs.TryCollect() && bombs.CurrentBombs == before, "A completed run blocks bomb use and collection.");
                    File.AppendAllText(Report, "ALL CHECKS PASSED\n");
                    Cleanup();
                    EditorApplication.isPlaying = false;
                    break;
            }
        }
        catch (Exception e)
        {
            File.AppendAllText(Report, "FAIL: " + e.Message + "\n");
            Cleanup();
            EditorApplication.isPlaying = false;
            Debug.LogException(e);
        }
    }

    private static void Cleanup()
    {
        EditorApplication.update -= Tick;
        if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
        keyboard = null;
    }
}
