# BananaRun

Rotten Banana Bomb is a secondary item alongside coconuts. Collect the BTM bomb pickups on the track to fill its separate slot (up to three bombs). Tap the circular **bomb icon** or press **B** to place a stationary trap behind the monkey. The main coconut throw button and swipe-down slide keep their existing behavior.

Gorilla and rival collisions detonate the trap with dust, impact audio, a vertical launch, and a temporary stun. Pausing, crossing the river without ground, or ending the run blocks dropping without spending ammo.

Both chasers use the player's Stumble clip and duration-scaled playback. Bomb placement uses a short whoosh; explosions use the supplied `bomb sound.mp3`, trimmed and filtered into preloaded PCM. A dedicated audio voice prevents bomb cues from stacking. Regenerate the click-free WAV files with `python Tools/prepare_bomb_audio.py` (requires FFmpeg).

In Unity, use **Tools > Banana Run > Verify Secondary Rotten Banana Bomb** with Level01 open to run the integration checks. The report is written to `Temp/rotten-banana-bomb-verification.txt`. **Install Secondary Rotten Banana Bomb** recreates the prefabs and scene wiring when needed.

