# Tropical art direction

The saved `Level01` scene now uses golden sunlight at a 55-degree pitch, `#FFF3D6` light color, soft shadows, and a three-color ambient fill. `Assets/_Game/Settings/TropicalSunlight.asset` supplies Neutral tonemapping, +18 saturation, +9 contrast, subtle warm bloom, and a light vignette. The main camera has HDR and post-processing enabled.

`Organic Dirt Trail` renders a world-space dirt/grass shader over the full running surface. The shader blends irregular edges beyond the outer lanes and adds subtle noise without stretching a texture along the course. The original ground collider is retained. Terrain, trees, and gameplay obstacles remain editable separately.

Tropical models, meshes, and materials are in:

- `Assets/_Game/Prefabs/Tropical`: palms, white daisy patches, fallen-log visuals, and three 24-metre border variants.
- `Assets/_Game/Art/Tropical`: generated low-poly meshes.
- `Assets/_Game/Materials/Tropical`: bark, cut wood, foliage, stone, flowers, and trail materials.

`Tropical Border Streamer / TropicalScenery` spawns nearby decorative chunks and removes chunks behind the player. Its first section is also previewed in Edit mode. Decorations have no colliders and stay outside the lane corridor. The course generator uses the new palms mixed with existing broadleaf trees. Distant ridges supply a backdrop, with distance fog separating them from the path.

Existing low obstacles and the generated log prefab now use a horizontal fallen trunk with visible cut ends. Breakable reward crates retain their existing gameplay. `Tropical Finish Arch` holds the timber structure, FINISH sign, banana flags, palms, and flowers. The resized wicker basket and its decorative banana pile sit behind the finish trigger; banana delivery still targets the basket.

The top-left banana counter is inside a rounded green badge. Health hearts remain top-right, the progress bar remains centered, and the bottom-right throw button uses a circular gold background with the coconut icon. The existing button action and swipe handling are preserved.

Authoring utilities:

- `Tools > Banana Run > Apply Tropical Art Direction` regenerates these authored assets and applies them to Level01. This replaces custom edits to the generated art; it is not required during normal play.
- `Tools > Banana Run > Review Tropical Finish` validates the lighting setup and saves a finish preview to `Temp/tropical-finish.png`.
- `Tools > Banana Run > Verify Extended Gameplay` checks gameplay, streamed border limits, and decoration collider safety; it also saves portrait, landscape, and runtime finish previews in `Temp/`.

If rebuilding gameplay assets with `Install Extended Gameplay`, apply the tropical art pass afterward to restore its log, scenery, and HUD styling.
