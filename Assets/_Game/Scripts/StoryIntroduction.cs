using UnityEngine;

/// <summary>Three 3.5-second story shots, before the race clock starts. No scene asset dependencies.</summary>
[DefaultExecutionOrder(200)]
public sealed class StoryIntroduction : MonoBehaviour
{
    public const float BeatSeconds = 3.5f;
    private static readonly string[] Titles =
    {
        "THE ROYAL BANANA HEIST", "AWAKENING THE GUARDIAN", "THE GREAT JUNGLE ESCAPE"
    };
    private static readonly string[] Captions =
    {
        "Deep in the Emerald Jungle, two rival monkeys discovered the legendary Royal Banana Basket...",
        "Their fight over the treasure awoke Kongo, the giant guardian gorilla of the jungle!",
        "Outrun the Gorilla, defeat your Rival with coconuts and power-ups, and reach the temple basket first!"
    };
    private RunnerMotor player;
    private RivalRunner rival;
    private GiantGorillaChaser gorilla;
    private RunnerCamera cameraRig;
    private StoryNarrativeUI ui;
    private GameObject basket;
    private Bounds basketBounds, gorillaBounds, runnersBounds;
    private Transform rivalHead;
    private float age;
    private int beganFrame;
    public bool IsPlaying { get; private set; }
    public int CurrentBeat { get; private set; }

    public static StoryIntroduction Create(Transform parent, RunnerMotor player, RivalRunner rival,
        GiantGorillaChaser gorilla, RunnerCamera cameraRig)
    {
        var host = new GameObject("Jungle Story Introduction");
        host.transform.SetParent(parent, false);
        var story = host.AddComponent<StoryIntroduction>();
        story.player = player;
        story.rival = rival;
        story.gorilla = gorilla;
        story.cameraRig = cameraRig;
        return story;
    }

    public void Begin()
    {
        ui = StoryNarrativeUI.Create(transform, Next, Skip);
        BuildTreasure();
        gorillaBounds = VisualBounds(gorilla != null ? gorilla.transform : player.transform);
        runnersBounds = VisualBounds(player.transform);
        if (rival != null)
        {
            runnersBounds.Encapsulate(VisualBounds(rival.transform));
            var animator = rival.GetComponentInChildren<Animator>();
            if (animator != null && animator.isHuman) rivalHead = animator.GetBoneTransform(HumanBodyBones.Head);
        }
        IsPlaying = true;
        ShowBeat(0);
    }

    private void BuildTreasure()
    {
        var prefab = RuntimeResourceCache.Load<GameObject>("Shared/WovenBananaBasket");
        if (prefab == null)
        {
            // Keep the introduction playable if an art resource is unavailable.
            var gate = FindAnyObjectByType<FinishGate>();
            basketBounds = VisualBounds(gate != null && gate.BananaTarget != null ? gate.BananaTarget : player.transform);
            return;
        }
        basket = Instantiate(prefab, player.transform.position + Vector3.forward * 7f,
            Quaternion.Euler(0f, 20f, 0f), transform);
        basket.name = "Royal Golden Woven Banana Basket (Story Only)";
        basket.transform.localScale = Vector3.one * 1.8f;
        foreach (var collider in basket.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        foreach (var body in basket.GetComponentsInChildren<Rigidbody>(true)) body.isKinematic = true;
        var gold = new MaterialPropertyBlock();
        gold.SetColor("_BaseColor", new Color(1f, 0.78f, 0.32f));
        foreach (var renderer in basket.GetComponentsInChildren<Renderer>()) renderer.SetPropertyBlock(gold);
        basketBounds = VisualBounds(basket.transform);
        // Ground the shared model by its actual geometry, including its authored pivot offset.
        basket.transform.position += Vector3.up * (player.transform.position.y + 0.05f - basketBounds.min.y);
        basketBounds = VisualBounds(basket.transform);
    }

    private void ShowBeat(int index)
    {
        CurrentBeat = index;
        age = 0f;
        beganFrame = Time.frameCount;
        if (basket != null) basket.SetActive(index == 0);
        ui.ShowBeat(index, Titles[index], Captions[index]);
        if (index == 0) cameraRig?.BeginStoryShot(basketBounds, new Vector3(0.5f, 0.35f, 1f));
        else if (index == 1)
        {
            cameraRig?.BeginStoryShot(gorillaBounds, new Vector3(0.08f, 0.18f, 1f));
            gorilla?.PlayStartingRoar();
            cameraRig?.ShakeRaceIntro();
        }
        else cameraRig?.BeginStoryShot(runnersBounds, new Vector3(-0.5f, 0.3f, 1f));
    }

    private void Update()
    {
        if (!IsPlaying || Time.deltaTime <= 0f || Time.frameCount == beganFrame) return;
        age += Time.deltaTime;
        float progress = Mathf.Clamp01(age / BeatSeconds);
        ui.Animate(age, progress);
        cameraRig?.SetStoryProgress(progress);
        if (age >= BeatSeconds) Next();
    }

    public void Next()
    {
        if (!IsPlaying || age < 0.12f) return; // Ignore double taps on a beat transition.
        if (CurrentBeat < 2) ShowBeat(CurrentBeat + 1);
        else Finish();
    }

    public void Skip()
    {
        if (IsPlaying) Finish();
    }

    private void Finish()
    {
        IsPlaying = false;
        ui?.Hide();
        if (basket != null) basket.SetActive(false);
        cameraRig?.EndStoryShot();
    }

    public void Cancel() => Finish();

    private void OnDisable() => Finish();

    private void LateUpdate()
    {
        // Pose after animation evaluation. The rival watches the player before GO.
        if (rivalHead == null || player == null || !RaceStartSequence.IsHolding ||
            (IsPlaying && CurrentBeat != 2)) return;
        Vector3 towardPlayer = player.transform.position + Vector3.up * 1.2f - rivalHead.position;
        if (towardPlayer.sqrMagnitude < 0.01f) return;
        Quaternion glare = Quaternion.FromToRotation(rivalHead.forward, towardPlayer.normalized) * rivalHead.rotation;
        rivalHead.rotation = Quaternion.RotateTowards(rivalHead.rotation, glare, 32f);
    }

    public static Bounds VisualBounds(Transform root)
    {
        if (root == null) return new Bounds(Vector3.zero, Vector3.one * 2f);
        Bounds bounds = new Bounds(root.position + Vector3.up, Vector3.one * 2f);
        var renderers = root.GetComponentsInChildren<Renderer>();
        bool found = false;
        foreach (var renderer in renderers)
        {
            if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer)) continue;
            if (!found) { bounds = renderer.bounds; found = true; }
            else bounds.Encapsulate(renderer.bounds);
        }
        return bounds;
    }
}
