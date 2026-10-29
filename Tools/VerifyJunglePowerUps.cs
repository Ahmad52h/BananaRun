using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// Run in the playing MainMenu with Pipeline's run_script command. Kept outside
// Assets so this verification harness never ships or modifies the scene asset.
public static class VerifyJunglePowerUps
{
    private const string Report = ".utmp/jungle-powerups-verification.txt";
    private static GameObject root;
    private static RunnerMotor player;
    private static RivalRunner rival;
    private static PowerUpPickup pickup;
    private static BananaPickup banana;
    private static int step, count, menuFrame;
    private static float began, stageTime, startZ, startY, peakY, normalSpeed, animationTime;
    private static Animator menuAnimator;

    public static string Run()
    {
        if (!EditorApplication.isPlaying || MainMenuDiorama.Instance == null)
            throw new InvalidOperationException("Run in the playing MainMenu.");
        Directory.CreateDirectory(".utmp");
        File.WriteAllText(Report, "Jungle menu and rival integration verification\n");
        step = count = 0;
        began = stageTime = Time.realtimeSinceStartup;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        return "Running; results: " + Report;
    }

    private static void Check(bool condition, string description)
    {
        if (!condition) throw new InvalidOperationException(description);
        File.AppendAllText(Report, "PASS: " + description + "\n");
        count++;
    }

    private static void Next() { step++; stageTime = Time.realtimeSinceStartup; }
    private static float Age => Time.realtimeSinceStartup - stageTime;
    private static void Set(object target, string field, object value) =>
        target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

    private static void Spawn(PowerUpType type)
    {
        var template = PowerUpPickup.CreateTemplate(type);
        template.transform.SetParent(root.transform);
        pickup = Object.Instantiate(template, rival.transform.position + Vector3.forward, Quaternion.identity, root.transform);
        pickup.name = "Verification " + type;
        pickup.gameObject.SetActive(true);
        Physics.SyncTransforms();
    }

    private static bool Collected()
    {
        if (Age > 8f) throw new TimeoutException("Track pickup was not collected: " + pickup.Type);
        return pickup != null && !pickup.gameObject.activeSelf && Age > 0.15f;
    }

    private static void BuildTrack()
    {
        root = new GameObject("Temporary Power-Up Verification Track");
        var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.transform.SetParent(root.transform);
        ground.transform.position = new Vector3(1000f, -0.5f, 100f);
        ground.transform.localScale = new Vector3(24f, 1f, 260f);
        ground.GetComponent<Renderer>().enabled = false;

        var playerObject = new GameObject("Verification Player");
        playerObject.SetActive(false);
        playerObject.tag = "Player";
        playerObject.transform.SetParent(root.transform);
        playerObject.transform.position = new Vector3(1000f, 0f, -15f);
        player = playerObject.AddComponent<RunnerMotor>();
        Set(player, "trackCenterX", 1000f);
        player.enabled = false;
        playerObject.AddComponent<PowerUpManager>();
        playerObject.SetActive(true);

        var rivalObject = new GameObject("Verification Rival");
        rivalObject.SetActive(false);
        rivalObject.transform.SetParent(root.transform);
        rivalObject.transform.position = new Vector3(1000f, 0.02f, 0f);
        var controller = rivalObject.AddComponent<CharacterController>();
        controller.height = 1.8f; controller.radius = 0.35f; controller.center = Vector3.up * 0.9f;
        rival = rivalObject.AddComponent<RivalRunner>();
        Set(rival, "player", player);
        rivalObject.SetActive(true);
        Physics.SyncTransforms();
    }

    private static void Tick()
    {
        try
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Play Mode ended before verification completed.");
            if (Time.realtimeSinceStartup - began > 90f) throw new TimeoutException("Verification timed out.");
            switch (step)
            {
                case 0:
                    var diorama = MainMenuDiorama.Instance;
                    Check(diorama.transform.Find("Living Jungle").childCount > 200, "Dense jungle layers are present.");
                    var leaves = diorama.GetComponentsInChildren<ParticleSystem>().First(p => p.name == "Falling Jungle Leaves");
                    Check(leaves.isPlaying && leaves.particleCount > 0 && leaves.main.useUnscaledTime,
                        "Falling leaves emit and use the menu's unscaled clock.");
                    foreach (string name in new[] { "Monkey - Idle Showcase", "Rival Monkey", "Gorilla Overlook" })
                    {
                        var character = GameObject.Find(name);
                        var animator = character.GetComponentInChildren<Animator>();
                        Check(animator != null && animator.runtimeAnimatorController != null && animator.isHuman &&
                            animator.GetCurrentAnimatorClipInfo(0).Length > 0, name + " has a playing humanoid animation.");
                        Check(character.GetComponentsInChildren<SkinnedMeshRenderer>().Any(s => s.sharedMesh != null && s.enabled),
                            name + " has a visible mesh.");
                    }
                    menuAnimator = GameObject.Find("Gorilla Overlook").GetComponentInChildren<Animator>();
                    animationTime = menuAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime;
                    menuFrame = Time.frameCount;
                    Next();
                    break;
                case 1:
                    if (Age < 0.25f) return;
                    Check(Time.frameCount > menuFrame && menuAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime > animationTime,
                        "Menu frames and gorilla animation advance.");
                    BuildTrack(); Next(); break;
                case 2:
                    if (!rival.IsGrounded) return;
                    normalSpeed = rival.CurrentSpeed;
                    Spawn(PowerUpType.Shield); Next(); break;
                case 3:
                    if (!Collected()) return;
                    Check(rival.HasShield && rival.transform.Find("Shield Energy Shell").gameObject.activeSelf,
                        "Track Shield pickup activates the rival shield and visual.");
                    rival.Stumble();
                    Check(!rival.HasShield && !rival.IsStumbling, "Shield absorbs one obstacle hit and is consumed.");
                    rival.Stumble(10f);
                    rival.TakeProjectileHit();
                    Check(rival.IsStumbling && rival.IsProjectileStunned, "Rival has setbacks before collecting Thunder.");
                    Spawn(PowerUpType.Thunder); Next(); break;
                case 4:
                    if (!Collected()) return;
                    Check(rival.HasThunder && !rival.IsStumbling && !rival.IsProjectileStunned &&
                        Mathf.Abs(rival.CurrentSpeed - normalSpeed * PowerUpManager.ThunderSpeedMultiplier) < 0.01f,
                        "Track Thunder clears setbacks and increases pace by 35 percent.");
                    Check(rival.transform.Find("Thunder Speed Trail").GetComponent<TrailRenderer>().emitting,
                        "Thunder enables the rival speed trail.");
                    startZ = rival.transform.position.z;
                    stageTime = Time.realtimeSinceStartup;
                    Next(); break;
                case 5:
                    if (Age < 0.3f) return;
                    Check(rival.transform.position.z > startZ + 0.5f, "Thunder drives actual forward movement.");
                    Spawn(PowerUpType.Magnet); Next(); break;
                case 6:
                    if (!Collected()) return;
                    Check(rival.HasMagnet, "Track Magnet pickup activates the rival magnet.");
                    Check(rival.HasThunder, "Thunder and Magnet remain active together.");
                    banana = Object.Instantiate(AssetDatabase.LoadAssetAtPath<BananaPickup>("Assets/_Game/Prefabs/Tropical/Banana.prefab"),
                        rival.transform.position + new Vector3(3.5f, 0.9f, 3f), Quaternion.identity, root.transform);
                    startZ = Vector3.Distance(banana.transform.position, rival.transform.position + Vector3.up * 0.9f);
                    Next(); break;
                case 7:
                    if (Age < 0.35f) return;
                    Check(!banana.gameObject.activeSelf || Vector3.Distance(banana.transform.position,
                        rival.transform.position + Vector3.up * 0.9f) < startZ - 0.5f, "Magnet pulls a nearby banana toward the rival.");
                    Next(); break;
                case 8:
                    if (banana.gameObject.activeSelf) return;
                    Check(rival.BananaCount > 0, "Magnet delivers the banana to the rival inventory.");
                    startY = rival.transform.position.y; peakY = startY;
                    Spawn(PowerUpType.Glider); Next(); break;
                case 9:
                    if (!Collected()) return;
                    Check(rival.HasHighJump, "Track High Jump pickup activates the rival ability.");
                    Next(); break;
                case 10:
                    peakY = Mathf.Max(peakY, rival.transform.position.y);
                    if (!rival.IsJumping || rival.transform.position.y < startY + 0.4f) return;
                    Check(rival.transform.Find("High Jump Aura").gameObject.activeSelf, "High Jump enables the rival aura.");
                    Set(rival, "highJumpEndsAt", Time.time - 1f);
                    Next(); break;
                case 11:
                    peakY = Mathf.Max(peakY, rival.transform.position.y);
                    if (!rival.IsGrounded) return;
                    Check(peakY - startY > player.NormalJumpHeight + 0.5f,
                        "High Jump immediately launches above normal jump height and preserves its arc through expiry.");
                    Check(!rival.HasHighJump && !rival.IsJumping, "High Jump ends cleanly after landing.");
                    Spawn(PowerUpType.Bulldozer); Next(); break;
                case 12:
                    if (!Collected()) return;
                    Check(rival.HasBulldozer, "Existing Bulldozer pickup still activates for the rival.");
                    Set(rival, "thunderEndsAt", Time.time - 1f);
                    Set(rival, "magnetEndsAt", Time.time - 1f);
                    Check(!rival.HasThunder && !rival.IsStumbling && !rival.IsProjectileStunned &&
                        Mathf.Abs(rival.CurrentSpeed - normalSpeed) < 0.01f, "Thunder expiry restores normal pace without resurrecting a cleared stun.");
                    Spawn(PowerUpType.Shield);
                    pickup.transform.position = new Vector3(1000f + player.LaneDistance, 0f, rival.transform.position.z + 6f);
                    Physics.SyncTransforms();
                    Next(); break;
                case 13:
                    if (!Collected()) return;
                    Check(rival.HasShield && rival.CurrentLane == 1,
                        "Rival chooses an adjacent clear lane and collects its power-up.");
                    rival.FinishRace();
                    Check(!rival.HasShield && !rival.HasThunder && !rival.HasMagnet && !rival.HasHighJump && !rival.HasBulldozer,
                        "Finishing clears every power-up.");
                    Spawn(PowerUpType.Shield); Next(); break;
                case 14:
                    if (Age < 0.2f) return;
                    Check(pickup.gameObject.activeSelf && !rival.HasShield, "Finished rival leaves track pickups available.");
                    File.AppendAllText(Report, "ALL " + count + " CHECKS PASSED\n");
                    Cleanup(); break;
            }
        }
        catch (Exception exception)
        {
            File.AppendAllText(Report, "FAIL at step " + step + ": " + exception + "\n");
            Cleanup();
        }
    }

    private static void Cleanup()
    {
        EditorApplication.update -= Tick;
        if (root != null) Object.Destroy(root);
    }
}
