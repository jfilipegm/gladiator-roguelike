# Decisions

One line each: what, and why. Newest at the bottom of each section.

## Tooling / project
- **Drive Unity via Unity CLI + `com.unity.pipeline`, Blender via a direct socket client to the Blender Lab add-on** — the MCP tools aren't exposed to Claude Code sessions here; these bridges offer the same capabilities (C# eval, screenshots, play mode, tests / bpy exec). See `docs/TOOLING.md`.
- **Unity 6000.6.6f1** — the only installed 6000.x editor (spec: "latest installed 6000.x").
- **Cinemachine 6.6.0** — in Unity 6.6 Cinemachine ships as a built-in package versioned with the editor (CM3 API).
- **Tests split into `Gladiator.Tests.EditMode` + `Gladiator.Tests.PlayMode`** instead of one `Gladiator.Tests` — the Test Framework decides EditMode vs PlayMode per assembly platform; one asmdef can't cleanly host both.
- **Git LFS for `.blend` and audio only; FBX/PNG stay in plain git** — procedural low-poly FBX and palette PNGs are a few KB; `.blend` files are binary and grow.
- **`Profiling/` and `Screenshots/` are git-ignored** — they're regenerable outputs; numbers that matter are copied into PROGRESS/gate reports.

## Art pipeline
- **FBX export (revised M0.4, regression-tested by `ModelPipelineTests`):** native Blender axes (`axis_forward=Y`, `axis_up=Z`), **no** `bake_space_transform`, `apply_unit_scale` + `FBX_SCALE_UNITS`, face smoothing, mesh+empty only. Unity's `ModelImporter.bakeAxisConversion = true` converts Z-up → Y-up into meshes *and* node transforms. `export_fbx` adds a temporary 180° yaw on the export root, because Unity maps Blender −Y to −Z. **Why it changed:** the CLAUDE.md §3.3 default (`-Z`/`Y` + `bake_space_transform`) passed the M0.2 box test, but the box root was a mesh. Blender's experimental bake mis-converts EMPTY nodes, and every rigid-part character root is an empty, so characters arrived lying on their backs. Four variants were compared numerically; only this one gives upright, identity-rotation parts with correct facing and handedness. Result: front +Z, left −X, 1×2×1 m exact, parts keep identity rotations, rotated joints come through as pure yaws, and the only extra transform is a 180° yaw on the FBX root node (under an identity prefab root, `preserveHierarchy`).
- **Asset front = Blender −Y** (what Blender's Front view shows) → Unity +Z.
- **Single palette source = `Assets/Art/Textures/Palette/palette.json`** → `Tools/build_palette.py` writes `PAL_Master.png` (128², 8 px swatches, 16 rows = families). Blender scripts read the same JSON for swatch UVs. Retune colours globally by editing the JSON and re-running the script.
- **Palette alpha = 1 − emission** — glowing swatches (XP, fire, VFX, affixes) come from the same texture, so no extra emissive materials are needed.
- **"One material per palette family" interpreted as: one master palette texture, shared materials split by *shader role*** (opaque lit, hit-flash, unlit VFX, transparent, crowd-instanced, blob shadow). Arena families live as palette rows. This keeps every asset on one material and gives ≤ 8 materials per scene.
- **Review renders use Workbench** (studio light for shape, then flat black at 64 px for the silhouette), composed into one contact sheet `Screenshots/blender/<name>_review.png`.

## Architecture
- **`GameLoop` = the only gameplay `Update()`**; systems implement `ITickable` and register with a `TickGroup` (Input → Player → Director → Enemies → Weapons → Projectiles → Pickups → Feedback → Camera → Late). Each tick has its own ProfilerMarker; the whole loop is wrapped in `Gladiator.Gameplay` (the "gameplay logic ≤ 4 ms" number).
- **`TimeController` owns `Time.timeScale`**: pause (level-up/menus) > hit-stop > 1. Hit-stop runs on unscaled time so it always ends.
- **Input is an `IPlayerInputSource` returning a `PlayerInputFrame` struct**; the human source builds Input System actions in code (no generated class, reproducible). PlaytestBot implements the same interface, so the bot exercises the real controller path.
- **PlaytestBot perceives the world only through `IBotSensor`** (pickups, threats, level-up, stress spawn), so gameplay systems plug in as they're built.
- **Zero-GC verification = managed-heap growth, not the "GC Allocated In Frame" counter, when running in the Editor.** In Play mode that counter includes the Editor's own allocations (~10–30 KB/frame with nothing running). `FrameStatsRecorder` also records `Profiler.GetMonoUsedSizeLong()` growth across the GameLoop window and the whole Update→LateUpdate window. It's page-granular, so any steady per-frame allocation shows up over a run; allocation-free code sums to exactly 0. Standalone dev builds use the real GC counter.
- **PlayMode tests run async** (entering play mode reloads the domain, which drops the HTTP request); `Tools/utest PlayMode` polls `test_status`.

## Look (M0.4)
- **Custom `Gladiator/PaletteLit` shader** instead of URP Lit/SimpleLit — it gives style control (wrap lighting, tinted shadows, hemisphere ambient, rim, palette-alpha emission, flash) while staying SRP-Batcher compatible and instanced. It has Forward+ (cluster loop), shadow, depth and depth-normals passes.
- **"Pixel noise" grain in the shader** (point-sampled hash on the dominant projection plane, world space for the environment, object space for characters). Megabonk's look comes largely from chunky pixel textures; this gets that retro textured feel with zero textures and the single palette material.
- **Shared materials by role:** `M_PaletteLit_Env` (grain), `M_PaletteLit_Char` (rim, light grain), `M_PaletteLit_Flash` (hit flash = swap to this shared material for a few frames; no MaterialPropertyBlock), `M_Sky`. All instanced.
- **Hemisphere ambient via `EnvironmentLighting`** (per-scene component → global shader colours + RenderSettings fog/sky). Each arena tunes its own mood.
- **URP:** SSAO renderer feature **off** (not part of the target look, needs a depth-normals prepass, costs GPU at 1080p); shadows 40 m / 2 cascades (§4 "short range"); MSAA 4×; HDR on; static batching on for Linux/Windows standalone.
- **Post (`VP_Gladiator`):** Bloom (threshold 1, intensity 0.55), Neutral tonemapping, mild contrast/saturation, vignette 0.22, slight warm white balance. The first pass was too orange (+18 saturation); pulled back.
- **Player readability mark = cyan headband + scarf tail** (player colour language). The camera sits behind the player, so the back of the character carries the cue. Enemy accent = magenta rag.

## Feel (M1.1)
- **Bake the FBX root yaw on import** (`ModelImportRules.OnPostprocessModel`: subtree conjugation p→q·p, r→q·r·q⁻¹, v→q·v). Every node arrives with identity rotation and part-local +Z = character forward, so animation and weapon code never deal with a flipped frame. Guarded by `ModelPipelineTests.AllModels_RootNodesHaveIdentityRotation`.
- **Always-run, no sprint button** (GDD Q3): run 7.5 m/s; slide and slide-jump are the speed tools (slide +4 m/s, downhill gain, slide-jump +1.5 m/s, momentum bleeds at 9 m/s² above run speed). Megabonk-like; re-check at Gate 1.
- **Slide = press to start, hold to keep sliding** (`slideRequiresHold`). Landing while holding slide → slide with no extra boost (no infinite-speed bunny-sliding).
- **Air control is Quake-style**: it accelerates toward the stick up to `airMaxSpeed` but never brakes existing momentum, so slide-jumps carry.
- **Layers: `Environment` (big geometry, camera avoids) vs `Props` (crates/barrels/fences: solid for the player, ignored by the camera)** — stops the camera popping every time a crate crosses the view.
- **Camera = CM3 OrbitalFollow (world-space binding) + RotationComposer + Deoccluder (pull forward) + Decollider (push out of grazed surfaces, terrain resolution)**, driven directly from the shared input frame. Tuning that made the bot-run clip probe go 124 → 0 frames: min pitch −8°, camera radius 0.4 m, deoccluder min distance 0.05 m. With the player backed against a pillar the camera may come within ~0.45 m of the target, so `CameraProximityFade` renders the player shadows-only below 0.75 m. `PreserveCameraHeight` was tried and was worse (34 frames, hill).
- **Procedural rigid-part animation (`PartRig` + `PartAnimProfile`)** was pulled forward from M3.3 into M1.1 in minimal form (run cycle locked to distance, idle, air, slide, land squash, jump stretch). Judging movement feel on a T-posing statue would be misleading, and the rig was already there.
- **Editor-hosted frame times are not perf data.** An unfocused Editor throttles itself (~18 fps with no bridge traffic, ~180 fps while bridge requests keep it awake). Allocation checks are valid in the Editor (heap growth); FPS/frame-time budgets are measured in standalone builds.

## Combat (M1.2)
- **Enemy core built "for real" in simple form now** instead of throwaway placeholders: struct-of-arrays state, pooled views per `EnemyDef`, one central tick, `SpatialHashGrid` (counting-sort rebuild every tick) for separation + weapon queries, `HeightGrid` for terrain, `StaticObstacles` push-out. M2.1 only extends it (scale, height-grid bake into the arena asset, perf).
- **Walkable vs obstacle is explicit** (`WalkableSurface` marker): walkables feed the height grid (highest walkable hit per cell, so wall tops never become "ground"); everything else solid and taller than 0.5 m becomes an obstacle (roundish → circle, else oriented box). A step-height rule (0.35 m per tick) stops enemies scaling walls/platform edges while stairs/ramps/hills work. Platforms are refuges ground enemies can't reach except via stairs/ramps — fine for now; ranged enemies (Phase 2) counter it.
- **Hit flash = swap the renderers' sharedMaterial to the shared `M_PaletteLit_Flash`** for 0.08 s (no MaterialPropertyBlock, SRP-batcher friendly). Compare materials by reference, never `Object.name` (allocates).
- **Slash VFX uses a MaterialPropertyBlock** for progress/arc — allowed: ≤ 6 instances, not a hot mesh.
- **Death pops = one world-space ParticleSystem with manual Emit** (one draw call for every pop). **Blob shadows = one `Graphics.RenderMeshInstanced`** for all enemies; enemy renderers don't cast real shadows (§4). The blob/gem quads are engine plumbing built in code; art meshes stay in Blender.
- **Auto-aim acquire range ≈ reach** (gladius: acquire 3.2 m, reach 2.6 m): a larger acquire range made it swing at air (6/14 swings landed → 11/5 after).
- **Hit-stop only on crits (0.035 s)**, player hits (0.05 s); screen shake is trauma² roll + tiny orbit jitter (never a position offset, so it can't push the camera into walls), scaled by `ScreenShake.UserScale` (options slider later).
- **Damage numbers: one overlay canvas, 96 pooled TMP labels kept active** (toggling UI GameObjects re-registers them with the canvas and allocates), pre-sized with the longest string so TMP never grows buffers mid-run; numeric `SetText`. Player-damage numbers are orange-red (hurts you), crits gold, normal white.
- **Stable content ids**: `EnemyDef.RuntimeId` / `WeaponDef.RuntimeId` = FNV-1a of the string id (`GetInstanceID` is obsolete in 6.6 and not stable across sessions anyway).
- **Camera: final collision guard** after CinemachineBrain (`CameraCollisionGuard`, ComputePenetration with a 0.16 m sphere) + camera radius 0.25 m (< player capsule 0.32 m, so the target is always a legal fallback). Cinemachine still does the smooth work; the guard removes the rare squeeze that the bot found with enemies around.
- **Perf numbers come from a standalone development build** (`BuildScripts`, Linux, IL2CPP default, windowed, runInBackground) run with `-bot/-stress -profile`.

## Run loop (M1.3)
- **Pickups are data + instanced draws** (`PickupSystem`: one `RenderMeshInstanced` per kind — small/medium/large XP gem, gold coin), never GameObjects. At capacity (700) new value merges into the nearest pickup of the same family, so nothing is lost and nothing allocates.
- **Base magnet radius 2.2 m < gladius reach 2.6 m.** At 3.5 m every melee kill's gems were vacuumed instantly, which makes "collecting" and pickup-range upgrades pointless. Tune at Gate 1.
- **XP curve** `xpToNext(L) = 5 + 4·(L−1)^1.35` (5, 9, 14, 19, 25, … 83 at L10) in `ProgressionConfig`; convicts give 1 XP. Placeholder until the Director (M2.3) sets real kill rates.
- **Level-up is a stub on purpose**: 7 placeholder boons that are plain stat multipliers (damage, attack speed, move speed, max HP, pickup range, reach, crit). This violates the long-term "no flat-stat grinding" rule only *within a run*, and the whole thing is replaced by data-driven cards with tags/rarity/reroll/banish in M2.4. Flow: XP threshold → 0.35 s burst beat → pause (timeScale 0, input off, cursor free) → pick (mouse / 1-4 / gamepad) → resume; queued level-ups open back-to-back.
- **Particles use scaled time**, so the level-up pause freeze-frames the burst (looks intentional; keep).
- **HUD built in code** (`Hud`, `LevelUpPanel`, `UiFactory`, procedural `PixelIcons`): XP bar top, countdown to the boss top-centre (8:00 = `runDuration`), `LV n` top-right, HP/gold/kills top-left, F3 debug line. Text changes only when values change; the timer writes a char buffer (`SetCharArray`) so the HUD is allocation-free.
- **Movement feel tests run without enemies** (a kill → XP → level-up pause would freeze their scaled-time loops); the camera test runs with enemies because fleeing produced the squeeze cases.
- **Every gem pops before the magnet can take it** (`ProgressionConfig.magnetDelay`, 0.25 s). Gems used to spawn with a random age (meant only to stagger bob/spin), which skipped the pop: melee-range gems were vacuumed — or collected — on the frame they dropped, so the drop was invisible. Age now starts at 0 and a separate random phase staggers the bob/spin. Guarded by `RunLoopTests.Kill_DropsGem_MagnetCollects_AddsXp` (the gem must be seen before it's collected).

## Test tools + run end (M1.4, producer request at Gate 1)
- **Admin menu (F1) pulled in before the playtest** so the producer can feel the effect of upgrades without grinding levels. Editor + development builds only (`Debug.isDebugBuild`); opening it pauses the game. Logic in `AdminController` (Gameplay, test-callable), drawing in `AdminPanel` (UI).
- **Admin buffs are a separate layer (`DevSettings`)** multiplied on top of the run's own bonuses (level-up cards), so "Reset buffs" never undoes a real pick. Becomes a "Dev" layer of the stat pipeline in M2.4.
- **`EnemyCatalog` asset kept current by an AssetPostprocessor**: every EnemyDef is listed the moment it exists, so new mobs/bosses appear in the spawn menu with no code change; the Director (M2.3) will read it too.
- **Death and timer end now finish the run** (a minimal early version of M3.5): death → 1.6 s real-time beat (slow motion 0.3 + collapse pose + red burst) → paused end screen with stats, Retry / Quit. Timer 0:00 → every enemy pops → "SURVIVED!" screen; once Gorm exists the timer summons him and this screen moves to after the fight. Retry reloads the scene and keeps the admin switches.
- **God mode floors HP at 1** instead of ignoring damage, so hit numbers, shake and hit-stop still read while testing.
- **The PlaytestBot runs immortal and timeless by default** (`-mortal`, `-timed` to opt out): perf and smoke runs must not end early.
- **Gameplay input blocking is a counter** (`Player.BlockInput/UnblockInput`): level-up, admin menu and end screen can overlap without one re-enabling input under another. Esc only toggles pause when no screen is blocking input.
- **Fence perimeter is the real boundary.** The fences were rotated 90° (pointing at the centre like spokes, with gaps) and purely visual, with an invisible wall 0.5 m behind them. Now each 3 m piece follows the ring so posts meet, with a 4 m solid wall collider exactly on the fence line (Environment layer: the player can't jump out, the camera stays inside). Enemies are clamped by the arena radius + obstacle bake; pickups are clamped to the arena radius.
- **Anything that pushes the static pause releases it in OnDisable** (`RunEndController`, `Player`, `LevelUpController`, `AdminController`): the full PlayMode suite froze because a test ended on the end screen and the next scene inherited the pause.

## Phase 2 (started 2026-10-18)
- **Gate 1 passed by the producer without feel changes** ("you can proceed"): all M1 config values kept; Q3 always-run (no sprint button) kept.

### M2.1 — enemy scale-up
- **Arena nav data is baked in the Editor** (`ArenaNavData` asset per arena, written by `ArenaNavBaker` when the arena is rebuilt; `EnemyManager` falls back to a runtime bake, logged, if it's missing or stale). It only saves ~14 ms on the test arena, but a 140 m Colosseum would need ~300k raycasts at load. Guarded by `ArenaNavTests` (asset == fresh bake).
- **The ~290 ms first-gameplay-frame hitch is mostly first-use rendering** (~205 ms of shader/pipeline-state creation in URP's first camera render), then Input System start-up (~18 ms), first UI layout (~17 ms) and the nav bake (~14 ms, now gone). Fix belongs to M3.5: render the arena behind a loading / "FIGHT!" beat (and try a GraphicsStateCollection / shader-variant warm-up then).
- **Perf runs use `gamescope --backend headless`** (`Tools/perfrun`): real GPU (RX 6600, radeonsi GL 4.6), 1920×1080, no window, works with the desktop locked. Numbers match windowed runs within noise (bot 979 vs 871 fps).
- **Player capture tooling:** `-rawProfile <s> -rawFrames <n>` writes a Unity Profiler capture from a dev build; `Tools/rawreport` turns it into slow-frame + GC-caller reports in the Editor. `-fps N` caps the player (smaller captures).
- **No `OnControllerColliderHit` on the player**: Unity allocates a hit object per callback (40 B per wall-contact frame). Walls are now detected from the blocked part of the move + one short capsule cast for the real normal (normal.y > 0.3 = ledge/slope, ignored).
- **Jump integration is a trapezoid step** (average of start/end vertical velocity): jump height is now exactly `jumpHeight` at any frame rate. Plain semi-implicit Euler lost ~8 cm (5 %) at 60 fps vs ~3 cm at 180 fps, so 60 Hz players jumped lower.
- **Coyote feel test now uses the platform's west edge** (a real 1.2 m drop); it used to run onto the ramp and only passed because the old wall code tripped the player on the ramp lip.
- **Collider wrappers are created at load** (`CameraCollisionGuard.Awake` touches every collider, its Transform and GameObject): Unity allocates a 24 B C# wrapper the first time script sees a native object, so the camera's overlap queries and Cinemachine's raycasts were allocating mid-run each time they met a new fence piece. After this, steady-state gameplay allocates 0 B outside the level-up UI.

### M2.2 — enemy roster
- **Sixth enemy = Fire-Pot Thrower (area denial), not the Assassin**: its burning patches only hurt you while you're on the ground, so jumping and positioning become the counter — it makes the movement kit matter. The Assassin's dash moves to Arena 2–3.
- **Elite affixes for the slice: Armored, Fast, Explosive.** Explosive blasts also damage enemies (chain reactions). Gilded waits for M2.7 so gold has a use first.
- **All "will hurt you" ground warnings go through one instanced `TelegraphRenderer`** (one extra material, `M_FxTelegraph`), keeping the scene within ~8 shared materials. Elite rings use the same renderer in the affix colour.
- **Behaviour = one state machine in `EnemyManager` (Move → Windup → Attack → Recover)**, switched on `EnemyDef.behaviour`; numbers stay in data. No per-enemy components, so 300+ enemies keep one central tick.
- **The Pit Cur's wind-up is a pose only (crouch), no ground decal.** Curs come in packs; dozens of decals would drown out the ones that matter (Brute slam, fire-pot landing). Ranged enemies get a small pulsing ring while winding up; the Brute's slam and pot landings get the full filling circle.
- **Hits are blocked by the Shield Thug's facing, not by a collider**: a hit is blocked if it comes from within `blockArc` of where the thug faces (turn rate 200°/s, so circling works). Blocked hits deal 15 %, knock back 40 %, show a grey number and never crit.
- **Elite hit-stop is rate-limited (one per 0.35 s) plus a bigger one on the elite's death.** Hit-stop on every hit would stutter constantly with a fast weapon.
- **Burning ground ("Status" damage) ignores and doesn't grant the 0.4 s invulnerability, and has no hit-stop.** Otherwise a fire patch would protect you from the crowd, and 0.5 s burn ticks would stutter the game.
- **Brute slam: jumping avoids it; a grounded hit knocks you up and away** (`PlayerMotor.AddImpulse`). Explosions don't care about jumping (they reach 1.6 m up).
- **Explosive elites leave a 1 s fuse circle at their death spot**; the blast chains to enemies. `KillAll` (run end, tests) never triggers blasts.
- **Projectiles stop at walls through the baked obstacle list, not physics.** Same data enemies use for push-out; zero colliders on projectiles.
- **Off-screen threat arrows (concern C2)**: up to 16 chevrons at the screen edge, wind-ups first (pulsing orange-red), then elites (affix colour).
- **Per-type alive counts in `EnemyManager` (`AliveOf`)**; spawners cap kiting types. Pools are prewarmed to those caps, so a pool growing mid-run is a bug and logs a warning.
- **Behaviour tests run without player i-frames** (`invulnerableTime = 0` in `EnemyRosterTests`): a random contact hit just before an attack made the attack tests flaky. The i-frame rule itself is unchanged in the game.

### M2.3 — Director
- **"Spawn budget" = a target head-count curve over run time + a fill-rate curve**, not threat points. The §8 targets are head-counts (300 by 5:00, ~500 at peak) and tougher types are already limited by low weights and caps, so points would add a second knob with no gain.
- **Per-type caps live in the spawn table** (kiting/heavy types low), fixing CONCERNS C6 at the source. The periodic elite ignores caps (it's one special enemy).
- **Swarms come from one direction** (50° arc) on top of the target, so they read as an event you can turn and face, and are announced on the HUD.
- **The Director uses the run clock**: Timeless freezes pacing, the admin timer jumps it; events missed by a jump are rescheduled rather than all fired at once.
- **Pools fill a few views per frame at run start, and spawning waits for them** (~0.8 s): no mid-fight instantiation and a smaller load hitch than one big prewarm.
- **Pool sizes come from simulating the run's head-count** (`Director.ComputePoolTargets`), not each type's weight share: capped types top out and hand their share to the uncapped ones, so by 8:00 convicts are ~330 of 460, not 32 %. Swarm types add a full swarm (swarms ignore caps), +10 % + 8 for enemies still dying. The first 8-minute run with the share formula grew the convict pool 127 times mid-fight.
- **Spawn points are a ring near the arena edge for now**, preferring off-screen. Arena 1's real gates come from its `ArenaLayoutDef` in M3.2.


### M2.4 — stats, upgrades, level-up cards
- **One `StatBlock` per player, 20 stats, value = (base + Σflat) × (1 + Σpercent) × Πmultiply.** Multiplier stats (damage, area, move speed…) have base 1, so "+12 % damage" is a Percent mod and stacks additively with other % bonuses (no runaway compounding); Multiply is reserved for rare "×" effects. Chance-like stats (crit chance/damage, armor, luck) are flat fractions shown as %.
- **Admin buffs stay a separate `DevSettings` layer**, applied on top in `PlayerStats.Get`, so "reset buffs" never undoes a real pick.
- **Rarity scales the size of the step (Megabonk-style), not which upgrade you get:** Common ×1, Uncommon ×1.4, Rare ×1.8, Legendary ×2.5 of the per-level values; whole-number stats (air jumps, rerolls, projectile count) never scale. Base odds 62/26/10/2; Luck multiplies tier r's weight by e^(1.5·luck·r) and gives a 4th card with chance luck/0.5.
- **A Boon's level = times taken (max 5)**; each take adds its step at that card's rarity. The HUD tile shows the best rarity taken.
- **Reroll redraws every card; banish removes one upgrade for the rest of the run and replaces only that card; skip is free.** Rerolls 2 / banishes 1 per run, as base stats so gladiators and Boons can change them.
- **When nothing is eligible the cards fall back to filler consumables** (Bread and Water: heal 30 %; Coin Purse: +15 gold), so a maxed build never sees an empty level-up.
- **Max-HP raises heal by the same amount; armor is % damage reduction capped at 75 %**; regen ticks every frame.
- **XP Gain is applied where XP is awarded (gem pickup), not inside `RunState.AddXp`**, so the admin "level up" button stays exact.
- **Behaviour hooks (on-hit, on-slide…) wait for M2.6**, where the first behavioural Boons need them; M2.4's Boons are pure stat data.
- **Level-up UI avoids every known allocation source:** no `Button` (colour-tint tweens), `UiHit` pointer forwarding + own highlight, shown by toggling the Canvas (no `SetActive`), text from char buffers / non-allocating `SetText`, first show done hidden at load. Skip is X on keyboard (S is movement) and Select on gamepad (East is slide).
- **`GladiatorDef` exists now with base stats only**; starting weapon, signature behaviour and unlock join in M3.5.
