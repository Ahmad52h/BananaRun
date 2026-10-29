# Cinematic race start

`Level01` contains **Cinematic Race Start** and its **Wooden Starting Gate** child. The menu loads this scene, and retrying reloads a fresh countdown.

- `RaceStartSequence` holds movement and combat for 3.5 seconds. The three numbers share that time equally; GO releases the runners and stays visible for another 0.65 seconds.
- The original monkey plays the looping humanoid **Warming up** state from `Animations/Warming Up.fbx`. The rival enters **Idle** immediately. Both blend to **Run** on GO.
- The gorilla uses **StartIdle**, an additive chest/head intimidation pose, and its existing `GorillaSound` clip. It then resumes running and retreats to the normal chase position.
- `RunnerCamera` frames the group from the front, adds a small roar shake, and returns over the track to the existing follow camera. The gameplay HUD returns on GO.
- The non-colliding wooden leaves swing open in 0.28 seconds. Hand-placed obstacles and banana peels within the first 15 metres are suppressed; the course generator already reserves at least 20 metres before its first row.

Select **Cinematic Race Start** to change **Start Countdown Sound** (public `startCountdownSound`, assigned to `Audio/CountdownNumber.wav`), **Go Countdown Sound**, volume, countdown duration, or camera return duration. `ShowCountdownBeat` changes the UI and calls `AudioSource.PlayOneShot` together, including GO in the frame that opens the gate and releases movement. Each beat fires once. The sequence uses scaled game time so a paused race does not release the runners. It never sets the global time scale.

The original `Start Sound.mp3` is 6.112625 seconds long and contains two repetitions of a low/low/high cue pattern, with audible onsets near 0.100862, 0.851950, 1.790658, 3.099184, 3.851111 and 4.791202 seconds. It cannot be played unchanged against four equally spaced UI beats. `CountdownNumber.wav` extracts the first low cue (0.100862–0.625 seconds), reused for 3, 2 and 1; `CountdownGo.wav` extracts the first high cue (1.790658–2.25 seconds). Leading silence is removed, tails fade out, and both are preloaded PCM so playback needs no background decoding. The original recording is retained.

To rebuild asset/scene wiring in an open race scene, use **Banana Run > Setup Cinematic Race Start**. This saves that scene and configures the warmup FBX as humanoid with a looping, baked root-motion clip.

For a real Play Mode regression check, invoke `RaceStartVerification.Begin()` from the Unity scripting tool while the Editor is stopped on Level01. The check enters and exits Play Mode, attempts inputs during the countdown, samples animation states and positions, checks GO timing, gate clearance, launch dust, and camera visibility. Results and countdown/GO/return/follow screenshots are written under `Temp/race-start-*`.
