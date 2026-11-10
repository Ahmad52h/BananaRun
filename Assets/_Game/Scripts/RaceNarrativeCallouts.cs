using System;
using TMPro;
using UnityEngine;

/// <summary>Distance/state-driven narrative with cached landmarks and a fixed pending set.</summary>
public sealed class RaceNarrativeCallouts : MonoBehaviour
{
    private RunnerMotor player;
    private GiantGorillaChaser gorilla;
    private PlayerHealth health;
    private FinishGate finish;
    private Canvas canvas;
    private CanvasGroup group;
    private RectTransform card;
    private TMP_Text headline, subtitle;
    private float riverZ = float.PositiveInfinity;
    private float[] bridgeZ;
    private int nextBridge;
    private bool riverSeen, riverPending, bridgePending, gorillaPending, wasClosing;
    private float toastAge, toastDuration, nextGorillaWarning;
    public string CurrentMessage => canvas != null && canvas.enabled ? headline.text : string.Empty;

    public void Configure(RunnerMotor runner, GiantGorillaChaser chaser)
    {
        player = runner;
        gorilla = chaser;
        health = player.GetComponent<PlayerHealth>();
        finish = FindAnyObjectByType<FinishGate>();
        var safe = StoryNarrativeUI.CreateCanvas(transform, 45, false, out canvas);
        group = canvas.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;
        card = StoryNarrativeUI.Image("Mission / Story Toast", safe,
            new Vector2(0.055f, 0.72f), new Vector2(0.945f, 0.86f), StoryNarrativeUI.Ink).rectTransform;
        StoryNarrativeUI.Image("Gold Accent", card, Vector2.zero, new Vector2(0.008f, 1f), StoryNarrativeUI.Gold);
        headline = StoryNarrativeUI.Text("Narrative Headline", card, new Vector2(0.04f, 0.43f), new Vector2(0.96f, 0.94f), 43f, StoryNarrativeUI.Gold);
        headline.fontStyle = FontStyles.Bold;
        subtitle = StoryNarrativeUI.Text("Objective", card, new Vector2(0.04f, 0.08f), new Vector2(0.96f, 0.41f), 32f, Color.white);
        canvas.enabled = false;
    }

    private void Start()
    {
        // Runtime track builders finish before Start. Read real landmark geometry once.
        var river = GameObject.Find("River Gap Module");
        if (river != null)
        {
            var vine = river.transform.Find("Vine Swing Trigger");
            if (vine != null) riverZ = vine.position.z;
        }
        var bridges = FindObjectsByType<BridgeDeck>(FindObjectsSortMode.None);
        bridgeZ = new float[bridges.Length];
        for (int i = 0; i < bridges.Length; i++) bridgeZ[i] = bridges[i].StartZ;
        Array.Sort(bridgeZ);
    }

    public void ShowMission() => Show("CHAPTER 1: WRATH OF THE JUNGLE GUARDIAN",
        "Objective: Beat the Rival & Collect 40 Bananas!", 4.5f, true);

    private void Update()
    {
        if (player == null || canvas == null || RaceStartSequence.IsHolding) return;
        if ((health != null && health.IsRunOver) || (finish != null && finish.Completed))
        {
            canvas.enabled = false;
            enabled = false;
            return;
        }
        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        float z = player.transform.position.z;
        float lead = Mathf.Max(22f, player.CurrentSpeed * 2.8f);
        if (!riverSeen && z >= riverZ - lead)
        {
            riverSeen = true;
            riverPending = z < riverZ + 5f;
        }
        if (bridgeZ != null && nextBridge < bridgeZ.Length && z >= bridgeZ[nextBridge] - lead)
        {
            if (z < bridgeZ[nextBridge] + 5f) bridgePending = true;
            nextBridge++;
        }
        bool closing = gorilla != null && gorilla.IsClosingOnPlayer;
        if (closing && !wasClosing && Time.time >= nextGorillaWarning)
        {
            gorillaPending = true;
            nextGorillaWarning = Time.time + 12f;
        }
        wasClosing = closing;
        if (!closing) gorillaPending = false;

        if (canvas.enabled)
        {
            toastAge += dt;
            float alpha = Mathf.Min(Mathf.Clamp01(toastAge / 0.22f), Mathf.Clamp01((toastDuration - toastAge) / 0.35f));
            group.alpha = alpha;
            card.anchoredPosition = new Vector2(0f, (1f - Mathf.SmoothStep(0f, 1f, alpha)) * 18f);
            if (toastAge < toastDuration) return;
            canvas.enabled = false;
        }
        if (riverPending)
        {
            riverPending = false;
            if (z < riverZ + 5f) Show("Crocodile River Ahead — Grab the Vines!", "Kongo won't wait. Take an outer lane!", 2.8f, false);
        }
        else if (bridgePending)
        {
            bridgePending = false;
            Show("Cross the Ancient Jungle Bridge!", "Keep your lead. The temple is waiting.", 2.8f, false);
        }
        else if (gorillaPending)
        {
            gorillaPending = false;
            Show("Kongo is closing in!", "Dodge the hazards and regain your speed!", 2.4f, false);
        }
    }

    private void Show(string title, string message, float seconds, bool mission)
    {
        headline.text = title;
        subtitle.text = message;
        card.anchorMin = new Vector2(0.055f, mission ? 0.72f : 0.73f);
        card.anchorMax = new Vector2(0.945f, mission ? 0.86f : 0.83f);
        toastAge = 0f;
        toastDuration = seconds;
        group.alpha = 0f;
        canvas.enabled = true;
    }

    private void OnDisable()
    {
        if (canvas != null) canvas.enabled = false;
    }
}
