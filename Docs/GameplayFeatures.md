# Extended BananaRun gameplay

Open `Assets/_Game/Scenes/Level01.unity` and press Play. The course ends at 900 metres (about 90 seconds at the default 10 m/s, plus hit recovery time). The original opening section remains authored in the scene; `Extended Course` generates the rest once per run.

## Inspector controls

- **Extended Course / RunnerCourseGenerator:** obstacle palette, seed, optional randomization per run, 10–16 metre jitter band, collectible prefabs, roadside scenery, and start distance. Patterns alternate single blockers, paired blockers, and staggered pairs (62–98% doubles, 30–85% staggered by 3.2 m). Each row preserves a safe lane, moving that lane at most one lane between rows. The finish transform determines the generation endpoint.
- **Player / RunnerAnimation:** Animator reference, `Hit` trigger name, reaction duration (0.55 seconds), initial speed multiplier (0.4), and recovery duration (0.85 seconds). `PlayerHealth.HitTaken` fires only for accepted damage. Invulnerable collisions do not replay the reaction. The Animator's `Hit` state uses the included humanoid `MonkeyStumble` clip; locomotion resumes after the reaction.
- **Gameplay Audio / RunnerAudio:** looping music, banana sound, separate sources, and independent volume controls. Clips may be replaced or left empty. An optional clip on each `BananaPickup` overrides the shared banana sound. The scene-owned manager survives individual pickup deactivation and is recreated on retry.
- **Canvas / SafeArea / Level Progress:** player and finish references, fill, monkey marker, distance text, and smoothing time. All graphics ignore raycasts so they do not intercept swipe input. The bar respects the existing safe-area layout, clamps to 0–100%, and reaches 100% when the finish trigger completes.

The six extension prefabs live in `Assets/_Game/Prefabs/Course`: log hurdle, rune stone, boulder, stump, fence hurdle, and breakable reward crate. Low hurdles can be jumped; tall obstacles require a lane change. Crates retain coconut destruction and banana rewards.

## Win and loss cutscenes

`Race Cutscenes / RaceCutsceneDirector` watches `RaceProgressTracker.Outcome` and `PlayerHealth`. It is serialized in Level01 and auto-created at runtime if a scene with a finish gate has no director, so the sequences also work in test scenes.

- **Victory:** a gold `VICTORY` banner appears immediately, then three hops on the existing `Jump` clip carry the monkey over `FinishGate.bananaTarget`. Procedural crescent-shaped bananas burst from each landing and from the basket. `FinishGate` holds the results card until `RaceCutsceneDirector.VictoryShowcasePlaying` clears (6 second guard), so the hops are not hidden behind the card.
- **Defeat:** a red `DEFEAT` banner appears, the monkey is forced onto the Idle pose and slumped into a ground sit (hips drop 0.55 m, thighs 84°, head down, slow sad bob), then `GiantGorillaChaser.catchWindupSeconds` (0.65 s) elapses before the existing Catch clip steps in and lifts him. The slump relaxes to a limp pose once the hand closes. `RunnerHUD.gameOverRevealDelay` is 2.1 s so the grab and lift read before the results card.

The banner is a runtime uGUI canvas (sorting order 40) built from the existing TextMeshPro font. It has no `GraphicRaycaster` and every graphic ignores raycasts, so it never blocks the retry button or swipe input. The director only poses the player and spawns presentation meshes: `RunnerMotor`, `RunnerInput`, `RivalRunner`, and the gorilla's grab/loom logic keep their existing owners and timing.

## Audio assets

`WoodlandLoop.wav` is an original 24-second synthesized marimba/pad loop; `BananaCollect.wav` is an original 0.48-second rising chime. They are starter assets generated locally with `python Tools/generate_starter_audio.py`; no downloaded or third-party recordings are used.

## Editor utilities

- `Tools > Banana Run > Verify Extended Gameplay` runs integration checks in Play mode and returns to Edit mode. Save Level01 first. It checks generated lane safety, obstacle variety, music, halfway HUD movement, a real obstacle collision, hit animation, slowdown recovery, a real banana pickup with SFX, missing audio clips, and finish completion. The report and portrait/landscape screenshots are written to `Temp/`.
- `Tools > Banana Run > Install Extended Gameplay` rebuilds the supplied course prefab palette and progress UI and reassigns starter audio. It is an authoring utility, not required at runtime; rerunning it replaces custom edits to those generated assets/UI.
- `Tools > Banana Run > Install Win-Loss Cutscenes` wires the cutscene director into Level01, applies the 0.65 s gorilla windup and the 2.1 s results delay, and reapplies the competitive course profile. `WinLossCutsceneSetup.InstallFromBatch` is the batch-mode entry point used for headless scene setup.
- `Tools > Banana Run > Increase Competitive Course Difficulty` rewrites the serialized course profile (denser non-lane-change rows, more doubles and 3.2 m staggers, more dynamic hazards) and retunes the rolling log and moving slide barrier prefabs in place.

Level01 is included in Build Settings. Retry reloads the active scene by name, including when launched directly in the Editor.
