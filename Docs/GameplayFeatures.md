# Extended BananaRun gameplay

Open `Assets/_Game/Scenes/Level01.unity` and press Play. The course ends at 900 metres (about 90 seconds at the default 10 m/s, plus hit recovery time). The original opening section remains authored in the scene; `Extended Course` generates the rest once per run.

## Inspector controls

- **Extended Course / RunnerCourseGenerator:** obstacle palette, seed, optional randomization per run, 12–20 metre row spacing, collectible prefabs, roadside scenery, and start distance. Patterns alternate single blockers, paired blockers, and staggered pairs. Each row preserves a safe lane, moving that lane at most one lane between rows. The finish transform determines the generation endpoint.
- **Player / RunnerAnimation:** Animator reference, `Hit` trigger name, reaction duration (0.55 seconds), initial speed multiplier (0.4), and recovery duration (0.85 seconds). `PlayerHealth.HitTaken` fires only for accepted damage. Invulnerable collisions do not replay the reaction. The Animator's `Hit` state uses the included humanoid `MonkeyStumble` clip; locomotion resumes after the reaction.
- **Gameplay Audio / RunnerAudio:** looping music, banana sound, separate sources, and independent volume controls. Clips may be replaced or left empty. An optional clip on each `BananaPickup` overrides the shared banana sound. The scene-owned manager survives individual pickup deactivation and is recreated on retry.
- **Canvas / SafeArea / Level Progress:** player and finish references, fill, monkey marker, distance text, and smoothing time. All graphics ignore raycasts so they do not intercept swipe input. The bar respects the existing safe-area layout, clamps to 0–100%, and reaches 100% when the finish trigger completes.

The six extension prefabs live in `Assets/_Game/Prefabs/Course`: log hurdle, rune stone, boulder, stump, fence hurdle, and breakable reward crate. Low hurdles can be jumped; tall obstacles require a lane change. Crates retain coconut destruction and banana rewards.

## Audio assets

`WoodlandLoop.wav` is an original 24-second synthesized marimba/pad loop; `BananaCollect.wav` is an original 0.48-second rising chime. They are starter assets generated locally with `python Tools/generate_starter_audio.py`; no downloaded or third-party recordings are used.

## Editor utilities

- `Tools > Banana Run > Verify Extended Gameplay` runs integration checks in Play mode and returns to Edit mode. Save Level01 first. It checks generated lane safety, obstacle variety, music, halfway HUD movement, a real obstacle collision, hit animation, slowdown recovery, a real banana pickup with SFX, missing audio clips, and finish completion. The report and portrait/landscape screenshots are written to `Temp/`.
- `Tools > Banana Run > Install Extended Gameplay` rebuilds the supplied course prefab palette and progress UI and reassigns starter audio. It is an authoring utility, not required at runtime; rerunning it replaces custom edits to those generated assets/UI.

Level01 is included in Build Settings. Retry reloads the active scene by name, including when launched directly in the Editor.
