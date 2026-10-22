# Progressive difficulty

`RunnerCourseGenerator.DifficultyAt` remains the single per-runner difficulty source, based on `LevelProgressHUD` distance. `RunnerMotor` reads it directly; restarting Level01 recreates both objects and resets speed. Disabling the motor for a vine swing does not reset progression. Slowdown effects multiply the difficulty-adjusted speed.

Inspector controls:

- **RunnerMotor / Running:** Forward Speed remains the existing base speed (10 in Level01). Max Run Speed Multiplier defaults to 1.25. Speed Difficulty Curve maps existing smoothed difficulty into that multiplier.
- **RunnerCourseGenerator / Progressive patterns:** Easy/Hard Obstacle Spacing control row density. The existing serialized Minimum/Maximum Spacing values (12/20 in Level01) bound a random *relative jitter*, not absolute row gaps: the sampled value is divided by the pair's average, giving 0.75–1.25 with those values. The proposed next-row step is the larger of `MaximumForwardSpeed × MinimumReactionSeconds` and `Easy/Hard Obstacle Spacing × jitter ÷ difficulty-scaled Obstacle Frequency Multiplier`. Safe-lane transitions, swept bounds, and the protected river approach can push actual rows farther apart. Thus the effective minimum proposed step is at least `MaximumForwardSpeed × MinimumReactionSeconds`; the two Spacing fields do not set that floor. Easy/Hard Double Obstacle Chance default to 18%/72%; lane-change chance defaults to 15%/55% and only requests adjacent safe lanes.
- **Difficulty:** existing obstacle frequency, dynamic-hazard chance, hazard speed cap (1.15), and minimum reaction seconds remain active. Lane transitions reserve maximum player speed times reaction seconds plus lane-change duration, outside obstacle swept bounds. River approaches remain protected.
- **Ability banana paths:** High Banana Frequency is the probability of a reward after a generated green glider pickup (75%). High Banana Height is the apex above ground (4.7m), limited by existing jump reach. Nine bananas sample a ballistic arc. The safe lane remains open through landing. Rope Banana Count defaults to 12 and is capped at 24.

The vine samples the same pendulum function used for movement. A trail is created once per zone per run. Swept hand proximity calls `BananaPickup.TryCollect`, sharing the normal inventory, health checks, sound, effect, and duplicate-collection guard while the character collider is disabled. No per-frame spawning or new manager is used.

Lane distance, lane-change speed, jump height, gravity, and slide settings are unchanged.

To verify in Unity, save or close every dirty or untitled scene, close Prefab Mode, enable Scene Reload in **Project Settings > Editor > Enter Play Mode Options**, and stop Play Mode. Then choose **Tools > Banana Run > Verify Progressive Difficulty**. The tool opens Level01 if needed, runs frame-based jump, swing, lane-change, slide, and scene-restart checks, audits safe corridors across the authored course and twelve other seeds, and writes `Temp/progressive-difficulty-verification.txt`. It reports a final PASS or FAIL with the failed check and stops Play Mode. Test-only hazards and pickups are disabled only in the Play Mode scene, which Scene Reload discards on exit. The route audit runs before those changes.

Scene serialization, runtime behavior, and the verifier's Play Mode lifecycle still need validation in the Unity Editor.
