# Maeko Flock
100% FREE starling murmuration for Unity 6 (URP), by Maeko Studio.
A real European starling, modeled and painted by hand in a realistic
painterly style: iridescent greens and purples, pale speckles, visible
brushwork. Reads as a true bird up close and as living smoke in the
hundreds. One component · calm swirl, hover, panic, and dive attack ·
figure eight and racetrack patterns · patrol routes · liquid blob that
stretches with speed · drifting scouts, twitches, wind sway · 3D chirp
audio slot · birds arrive from the horizon and leave the same way.

Full documentation with pictures: **Documentation.pdf** in this folder.

## Quick start - 10 seconds
1. Empty GameObject → add **Maeko Flock**.
2. Drag the **StarlingBird** prefab into Bird Prefab.
3. Press Play. A murmuration gathers around the anchor. Done.

Move the **Anchor** transform at runtime and the whole flock follows it.

## Demo scenes (in MaekoFlock/Demo)
- **MaekoFlockShowcase** - the starling up close in a white studio.
  Auto-toggles between the two flight animations with a note on what
  triggers each in the flock, plus live tri, vert, and bone counts.
- **MaekoFlockSimRoom** - a locked camera on a full flock. Arrow buttons
  browse setups (figure eight, racetrack, hover, liquid stretch,
  drifters, panic, dive attack, patrol, migration). Page 1 is
  **Full Auto**: it plays the whole tour by itself like a highlight reel.

Press **H** in either demo to hide the UI for clean recordings.

## The settings that matter
- **Bird Spacing** - personal space per bird. The ball's size grows with
  bird count automatically, so 500 birds make a visibly bigger ball
  than 100. This is the density dial.
- **Mode** - Calm Swirl (default), Hover (parks tight on the anchor),
  Panic (scatter and recover), Dive Attack (orbits Dive Target, birds
  take turns striking). Switch live, or from code: `SetMode(...)`,
  `TriggerPanic()`.
- **Swirl Pattern / Radius / Speed** - the path the flock traces in Calm
  Swirl: Circle, Figure Eight, or Racetrack. Smaller radius keeps the
  show tighter on the anchor.
- **Patrol Points** - assign 2 or more transforms and the flock flies the
  route in order, looping forever. Leave empty to stay home.
- **Stretch** - how much the ball elongates along its direction of travel,
  like liquid. 0 keeps a rigid sphere.
- **Breathe / Sway / Twitch / Drifter Chance** - the life. Slow ball
  breathing, wandering drift of the whole cloud, sharp little dodges,
  and lone scouts that peel off ahead or beside the flock and return.
- **Spawn & Despawn** - On Start, on a trigger zone, or by script
  (`SpawnFlock()` / `DespawnFlock()`). Birds stream in from
  Arrive From Distance away and leave the same way. Despawn on a timer
  or after N patrol loops.
- **Chirp Clip** - drop ANY looping bird-chatter audio in this slot and
  it plays as 3D sound from the centre of the ball: pans left and right,
  fades with distance, doppler as the flock sweeps past. No clip shipped,
  bring your favourite (freesound.org has great CC0 starling loops).
- **Obstacle Mask** - set to your terrain and building layers and birds
  steer around them. Leave as Nothing to skip the physics cost.

## The animator (already set up in the demo prefab)
The rig is Generic. The controller needs exactly two states and NO
transitions: **Maeko_Fly** (default) and **Maeko_Glide**. The flock
cross-fades them itself: flap speed follows airspeed, glides fire on
dives, descents, and natural bounding-flight pulses. Only Maeko_Fly is
required; without a glide state birds simply always flap.

## Maeko Sky
The murmuration was built to fly through **Maeko Sky**, our painterly
day-night atmosphere for URP. No dependency in either direction, they
just look right together.

## Support
Maeko Flock is completely free, made by a tiny studio. If anything is
broken or confusing, email us FIRST and we will fix it fast:
MaekoStudio@outlook.com

And if the flock earns a place in your project, an honest review on the
store page genuinely helps more than you know.
