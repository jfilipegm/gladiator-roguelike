# Gate 1 — "Feel check" report

*2026-10-18 · commit `9e1dc9b` · Phase 0 + Phase 1 (M1.1–M1.3) complete*

## 1. What works

**Verified** (by automated tests, logged numbers or screenshots):
- **Movement** (`MovementFeelTests`, standalone measurements):
  - Run speed 7.50 m/s, reaching 90 % of it in 0.10 s; stops within 0.49 m.
  - Jump: 1.67 m held (config 1.7 m), 0.47 m tapped.
  - Slide: +4.0 m/s boost; 11.5 m/s top speed on flat ground, 15.5 m/s down the 30° hill.
  - Stairs and ramp both reach the platform. A coyote jump works 72 ms after leaving an edge.
- **Camera:**
  - In a 25 s bot run with enemies, it was never inside geometry for a single frame and never lost sight of the player; the closest it got was 0.45 m.
  - Below 0.75 m the player turns into a shadow only, so they never fill the screen.
- **Combat:**
  - The gladius auto-aims and swings a 150° arc. It hits enemies in front and misses those behind.
  - Each hit shows a white flash, a damage number, knockback and a small recoil.
  - Crits show a gold number and a 35 ms hit-stop. Damage you take shows as an orange-red number with a short shake.
  - Each kill plays a small death pop.
  - Enemies never walk through walls.
- **Run loop:**
  - A kill drops a gem that pops out, then gets pulled in by the magnet and adds XP.
  - On level-up the game pauses and shows 3 cards; your pick applies and the game resumes. Level-ups that stack open one after another.
  - The HUD shows the XP bar, an 8:00 countdown, level, HP, gold and kills.

**Built but not verified by me:**
- **Feel:** I can't feel it. That's what this gate is for.
- **Gamepad:** bindings exist (left stick, right stick, South, East or LT, Start), but they've never been driven by a real controller.
- **Clicking cards with the mouse:** cards are picked through the same code path the tests use, not with a real mouse click.

**Not built yet** (later phases, by plan):
- Sound: none at all.
- Real art: the arena is a test layout and the characters are look-dev versions.
- Real cards: the 7 level-up cards are placeholder stat boosts.
- Enemy variety, the Director (enemy spawning over time), the boss, and the menus and run flow.

## 2. What to test

**Start it** either way:
- In the Editor: open `Assets/Scenes/Dev/TestArena.unity` and press Play.
- As a standalone build: run `Builds/LinuxDev/Gladiator.x86_64`. It opens windowed in the same arena.

**Controls:**

| Action | Keyboard / mouse | Gamepad |
|---|---|---|
| Move | WASD or arrow keys | Left stick |
| Camera | Mouse | Right stick |
| Jump | Space | South button |
| Slide (hold) | Shift, Ctrl or C | East button or LT |
| Pause | Esc | Start |
| Pick a card | Mouse, or keys 1–3 | Stick + South button |
| Debug line (fps / enemies / pickups) | F3 | — |

Your weapon swings on its own; 25 convicts keep spawning around the arena.

**Try, roughly in this order:**
1. **Run and stop** on the long runway. Does starting and stopping feel snappy or skatey?
2. **Jump:** tap it, then hold it. Jump off the platform edges, including a little late. Is the height right? Does it feel floaty or heavy?
3. **Slide:** slide on flat ground, then down the 30° hill. Slide-jump in a chain and steer in the air. Does momentum feel rewarding? Is holding to keep sliding OK?
4. **Camera:** orbit freely, back into the wall corner and the pillars, and stand on the platform. Look for clipping, the camera fighting you, the speed FOV kick, and the player fading out when the camera gets close.
5. **Combat:** let the convicts reach you. Does the auto-swing hit what you expect? Are hits readable (flash, numbers, pop)? Is the crit hit-stop noticeable but not annoying? Is the shake when you get hit OK?
6. **Gems and level-ups:** each gem should pop out briefly before the magnet pulls it in. The first level-up comes at 5 kills, then at 9, 14, 19 and so on. How does that pacing feel? Pick cards with the mouse, the number keys and the gamepad.

Notes from even a sentence per item are enough. "Too floaty", "camera too close" and "slide feels great but stops too soon" are all useful.

## 3. Tunables

All of these are assets under `Assets/Data/`. Edit them in the Inspector while in Play mode; changes to assets stay after you stop, so a tune you like is kept.
- **`Config/MovementConfig`:**
  - `maxSpeed` 7.5; `groundAccel` 70 / `groundDecel` 55
  - `jumpHeight` 1.7; `jumpCutMultiplier` 0.5 (how much a tapped jump is cut short)
  - `gravity` 30 / `fallGravityMultiplier` 1.35 (floaty ↔ heavy)
  - `coyoteTime` 0.12; `jumpBuffer` 0.15
  - `airAccel` 30 / `airMaxSpeed` 7.5
  - `slideStartBoost` 4; `slideFriction` 5; `slopeGain` 1.0; `slideJumpBoost` 1.5; `slideRequiresHold`
- **`Config/CameraConfig`:**
  - `distance` 6.2; `targetHeight` 1.25
  - `startPitch` 20; `minPitch` / `maxPitch` −8 / 70
  - `mouseSensitivity` 0.11; `stickYawSpeed` / `stickPitchSpeed` 220 / 130; `invertY`
  - `baseFov` 62 + `speedFovKick` 12; `followDamping`
- **`Config/HitFeedbackConfig`:**
  - `flashDuration` 0.08
  - `hitStopOnCrit` 0.035; `hitStopOnPlayerHit` 0.05
  - `shakeOnCrit` 0.12; `shakeOnPlayerHit` 0.35
  - `numberLifetime` 0.7; `critNumberScale` 1.55; `deathPop*`
- **`Weapons/WP_Gladius`:** `damage` 12; `cooldown` 0.8; `range` 2.6; `arcDegrees` 150; `acquireRange` 3.2; `critChance` 0.12
- **`Enemies/EN_Convict`:** `maxHp` 20; `moveSpeed` 3.6; `contactDamage` 8
- **`Config/ProgressionConfig`:**
  - `magnetRadius` 2.2; `magnetDelay` 0.25
  - XP curve: `baseXp` 5, `perLevelXp` 4, `exponent` 1.35
- **In the scene:** the `DebugEnemySpawner` object's `targetAlive` is 25. Raise it to feel a crowd.

## 4. Measurements

Standalone dev build, 1080p windowed, on this PC (i5-12400F, RX 6600). Every number below was measured with the PlaytestBot, not estimated.

| Run | Enemies | FPS (avg) | Frame ms avg / p95 / p99 | Gameplay ms avg / p95 |
|---|---|---|---|---|
| Bot, 57 s | 25 | 871 | 1.15 / 1.47 / 1.79 | 0.15 / 0.22 |
| Stress, 69 s | 250–300 | 251 | 3.98 / 5.89 / 6.78 | 1.18 / 1.68 |

- **Budgets** (CLAUDE.md §8): 60 FPS with 300 enemies is met with a wide margin. Gameplay logic at 1.18 ms is well under 4 ms. Caveats: this is a single enemy type in the test arena, and the 500-enemy and 1,000-enemy stress tests are M2.1 work.
- **GC:** the target is 0 B/frame and it's met almost everywhere, but not perfectly. This is from a headless rerun with every fix in: the screen got locked, which blocks windowed runs, and headless runs are valid for GC only.
  - **Gameplay frames:** 0 B, apart from about 6 rare allocations in 72 s (24 B to 3.6 KB, about 4 KB in total, not yet traced to their source).
  - **Level-ups:** each one allocates about 1.2–1.5 KB inside Unity's UI system while the game is paused. The first one also allocates 26.6 KB once.
  - **Startup:** a hitch of about 300 ms and 1–2 MB while pools fill and the arena data is baked.
  - **Fixed this milestone:** TMP text buffers that kept growing (about 27 KB per level-up, plus about 12 KB each time the debug line got longer).
- **Tests:** 10/10 EditMode and 8/8 PlayMode pass, including a 20 s bot smoke test.

## 5. Top concerns

| # | Concern | Recommendation |
|---|---|---|
| C1 | A ~35 m Slave Pit with 300 enemies risks a wall of bodies where positioning stops mattering. | Make the playable floor about 42 m wide and give each arena its own `maxAlive` (Slave Pit about 200–250). Keep 300+ for stress tests and the bigger arenas. Decide with real numbers after M2.3. |
| C2 | With a free camera, threats closing in from behind go unseen. | Add orange-red screen-edge markers, but only for ranged wind-ups and elites, alongside the Slinger in M2.2. Tell me if you got blindsided in this playtest. |
| C3 | The camera gets cramped when you back onto a pillar. | It's handled: the player fades out and the camera never clips. If it still bothers you, fade small occluders instead (dithered transparency). |
| Q3 | No sprint button: you always run, and slide and slide-jump are the speed tools. | Keep it, since that's how Megabonk works. Confirm or overrule at this gate. |
| — | The magnet radius (2.2 m) is smaller than the gladius reach (2.6 m), so a few gems need walking over. | That's on purpose, so collecting means something and pickup-range upgrades are worth taking. Tune it if it's annoying. |
| — | Leftover GC from the level-up UI. | Remove it when M2.4 rebuilds the level-up UI: no toggling UI objects on and off, and highlighting cards ourselves instead of with Unity's built-in colour transitions. |

## 6. Proposed next phase

Phase 2 as written, after your feel feedback:
1. **Feel tuning:** apply your notes to the configs above. These are small changes; I'll commit them separately.
2. **M2.1:** much of `EnemyManager` already exists (pooling, central tick, spatial hash, separation, obstacle push-out, height grid). M2.1 becomes:
   - store the baked height grid and obstacles in the arena data, which removes the 300 ms startup hitch
   - add 500-enemy and 1,000-enemy stress runs
   - trace the leftover GC with the Profiler attached to the dev build
3. **M2.2–M2.7** as specified, with the C2 threat markers added to M2.2.

**No scope changes proposed.** Decisions I need from you: your feel notes; the sprint question (Q3); and anything about pacing, such as how fast the first level-ups come.
