# Progress

**Current phase:** Phase 1 — Feel
**Current milestone:** M1.3 — HUD, XP gems + magnet, stub level-up
**Last updated:** 2026-10-18

## Resume checklist (read first after a context reset)
1. Read `CLAUDE.md`, then this file, then `docs/TOOLING.md`.
2. `unity status --format json` → Editor ready? If not: `unity pipeline list --format json`.
3. `python3 Tools/blender_bridge.py -c "import bpy; result={'v': bpy.app.version_string}"` → Blender reachable?
4. `Tools/ucompile` → must print `errors=0` before doing anything else.

## Done
- **M0.1** — Project (Unity 6000.6.6f1, URP, Linear, Input System 1.20) + Cinemachine 6.6.0, Test Framework 1.8.0, Pipeline bridge 0.8.0-exp.1. Folder structure, asmdefs (Core / Gameplay / UI / Editor / Tests.EditMode / Tests.PlayMode), `.gitignore` + `.gitattributes` (LFS for .blend/audio), git init, docs scaffolding, `CLAUDE.md`, tooling wrappers in `Tools/`. Model import rules (`ModelImportRules`). Verified: clean compile, assemblies load.
- **M0.2** — Smoke tests. Unity via Pipeline: console read, scene create, cube create, play/stop (698 frames in 4 s), Game-view capture (`Tools/ushot`) + camera→PNG helper (`EditorCli.RenderCamera`). Blender via socket: scene info, clear, 1×2×1 m box with front (−Y) and left (+X) markers, Workbench review sheet (front/side/¾ + 64 px silhouette), FBX export, `.blend` saved. **Round trip verified:** box arrives 1.000×2.000×1.000 m, origin at base, front marker at +Z, left marker at −X, identity transforms. Palette pipeline (`palette.json` → `PAL_Master.png`, point-filtered, `TextureImportRules`).

- **M0.3** — `GameLoop` (central tick + profiler markers), `TimeController`, input abstraction (`HumanInputSource` in code / `PlaytestBot`), `PlaytestBot` (seeded; seek pickups, flee threats, stay in arena, jump/slide rhythm, camera orbit, random cards; `-bot -stress -stressCount -botSeconds -profile` via `PlaytestBotBootstrap`), `FrameStatsRecorder` (ProfilerRecorder main-thread/gameplay/GC + heap-growth → `Profiling/*.csv` + `.summary.txt`). Tests: 6 EditMode (frame-stat maths, palette guards) + PlayMode smoke test (boots scene, runs bot, asserts no errors + budget). `Tools/utest`. **Verified:** bot drives the stand-in player in `Dev/BotSandbox` (rebuilt by `Gladiator/Setup/Dev/Build Bot Sandbox`); CSV written; 6/6 EditMode pass; smoke test pass (15 s); steady-state gameplay heap growth 0 B (only bumps coincide with external bridge requests).

- **M0.4** — Look-dev. `Gladiator/PaletteLit` + `Gladiator/SkyGradient` shaders, shared role materials, `VP_Gladiator` post profile, URP tuned (SSAO off, 40 m/2-cascade shadows, MSAA 4×, static batching), `EnvironmentLighting`. Blender assets: `CH_Nobody` (828 tris, iteration 2), `EN_Convict` (192 tris), `SM_PitFloor`, mound, rock, crate, barrel, 3 m fence, brazier (emissive fire), ramp, stairs, platform. `Dev/LookDev` scene rebuilt by `Gladiator/Setup/Dev/Build Look-Dev Scene`. **Verified:** Play-mode capture shows bloom/vignette/grade, fog, sky, soft shadows, pixel grain; 3 materials in scene; 266 renderers static-batched. **Pipeline bug found and fixed:** FBX empties broke with `bake_space_transform` → switched to native axes + Unity `bakeAxisConversion`, guarded by `ModelPipelineTests` (8/8 EditMode green).

- **M1.1** — `PlayerMotor` (CharacterController state machine) + `MovementConfig`, `Player` (single shared input frame, pause/cursor), `PlayerCameraRig` + `CameraConfig` (CM3 orbit + deoccluder + decollider, speed FOV kick), `CameraProximityFade`, `PartRig`/`PlayerPartAnimator`/`PartAnimProfile`, typed `GameEvents` bus, layers + physics matrix (`ProjectSetup`), `P_Player` prefab (`PlayerSetup`), `Dev/TestArena` (`TestArenaScene`: runway, 30°/20° hill, platform via stairs or ramp, wall corner, pillars, props, boundary). FBX root yaw baked on import. **Measured (PlayMode `MovementFeelTests`):** run 7.50 m/s (config 7.5), 0→90 % in 0.10 s, stop in 0.49 m; jump held 1.67 m (config 1.7) / tapped 0.47 m; slide +4.00 boost, flat top 11.5 m/s, 30° downhill 15.5 m/s; stairs to deck y=1.27; coyote jump 72 ms after the edge (window 120 ms); camera: 0 frames touching geometry, 0 blocked line of sight in a 25 s bot run (closest 0.45 m). Gameplay+scripts heap growth 0 B over 19 s. 9/9 EditMode, 3/3 PlayMode green.

- **M1.2** — `EnemyManager` (SoA, pools, central tick, spatial hash separation, obstacle push-out, height grid + step rule, contact damage, flash/knockback/recoil, rigid-part run cycle, death pop), `EnemyDef` (EN_Convict), `WeaponDef` + `WeaponSystem` (MeleeArc gladius: auto-aim, arc hit via spatial hash, crits), `DamageInfo` pipeline, `PlayerHealth` (i-frames), `HitFeedbackConfig`, `ScreenShake`, `DeathPopFx`, `BlobShadowRenderer`, `SlashFx` + `FX_SlashArc` (Blender) + `Gladiator/FxSlash` & `Gladiator/BlobShadow` shaders, `DamageNumbers` (UI), `DebugEnemySpawner`, `RunBotSensor`, `CameraCollisionGuard`, TMP essentials imported, `BuildScripts` (Linux dev build). **Verified:** swing sequence screenshots (slash arc, white flash, white/gold/orange-red numbers, death-pop particles, body twist); `CombatTests` (front hit / behind safe, forced crit → hit-stop that ends + OnKill, enemies never inside walls); camera test with enemies 3/3 green. **Standalone dev build, 1080p windowed, this PC:** 25 enemies 884 fps avg (1.13 ms, p95 1.51 ms), gameplay 0.14 ms; early stress (188 alive) 573 fps (1.74 ms, p95 3.15 ms), gameplay 0.38 ms avg. GC: startup hitch 322 ms / 1.1 MB (pool prewarm + bakes at load); sporadic TMP buffer growth found by profiler and fixed (pre-sizing) — re-verify in the next standalone run. 9/9 EditMode, 6/6 PlayMode.

## In progress — M1.3 (state at the WIP commit "WIP M1.3")
**Built and working in Play mode (verified by screenshots + state dumps):**
- `ProgressionConfig` (XP curve `5 + 4·(L−1)^1.35`, magnet 2.2 m, collect 0.7 m, gem tiers 1/5/25, run 480 s, 3 cards) — asset `Assets/Data/Config/ProgressionConfig.asset`.
- `RunState` (timer, level/XP, gold, kills, pending level-ups), `PickupSystem` (instanced gems S/M/L + coin, pop/bounce/bob/spin, magnet vacuum, merge at cap, `ClearAll`, bot hook), `LevelUpController` (stub: 7 placeholder boons, 0.35 s burst beat → pause → choose → resume, queued level-ups chain), `PlayerMotor.SpeedMultiplier`, `DeathPopFx.Burst`.
- UI: `Hud` (XP bar, countdown, LV, HP/gold/kills, LEVEL UP toast, pause overlay, F3 debug line), `LevelUpPanel` (mouse / 1-4 / gamepad), `UiFactory`, `PixelIcons`. EventSystem added by `ArenaSceneCommon.AddRunSystems`.
- Blender: `BlenderSrc/props/pickups.py` → `Assets/Art/Models/Props/PR_GemXP_S/M/L.fbx`, `PR_Coin.fbx` (coin joined into one mesh).
- Test arena rebuilt with run systems (`Gladiator/Setup/Dev/Build Test Arena`).

**Tests:** EditMode 10/10 green (incl. new `ProgressionTests`). PlayMode: `RunLoopTests.LevelUp_Pauses_AppliesChoice_Resumes` passes; everything else from M1.1/M1.2 passed in the last full run.
**OPEN BUG (first thing to do on resume):** `RunLoopTests.Kill_DropsGem_MagnetCollects_AddsXp` fails with "a gem should have dropped" (kill registers, but `PickupSystem.ActiveCount` never reads > 0 inside the test), while the *same* sequence done live through the bridge works (gem appears, is vacuumed ~0.3 s later, XP +1). The test currently carries temporary per-frame instrumentation (`[GemTest]` log line) — run it once, read the log, fix, then remove the instrumentation. Suspects: `PickupSystem` spawns with a random `_age` (0–3 s) so gems can be vacuumed immediately; test-boot ordering (KillAll → ClearAll).
**Last test run was aborted** (test runner error "SaveCurrentModifiedScenesIfUserWantsTo during play mode" after a cancel) — if PlayMode tests misbehave: `Tools/u cancel_tests`, `Tools/u editor_stop`, `Tools/ucompile`, retry. Never call `GetInstanceID()` in code (obsolete = compile error in 6.6; a compile error makes `Tools/utest PlayMode` wait forever).

## Next
1. Fix the gem test (above), run `Tools/utest EditMode` + `GLADIATOR_SMOKE_SECONDS=20 Tools/utest PlayMode "" 900` (run it with `run_in_background`; the full PlayMode suite takes ~3 min).
2. Rebuild the standalone dev build (`Tools/ueval 'return Gladiator.EditorTools.BuildScripts.BuildLinuxDev();'` — the HTTP call times out but the build runs; wait for `[Build]` in `Logs/Editor.log`), then run normal + stress (`-stress -stressCount 300`) and confirm the TMP pre-size fix removed the sporadic GC (target: 0 B/frame after warm-up).
3. Commit M1.3, then write the **Gate 1 report** (CLAUDE.md §11) — and STOP for the producer's playtest.

## Known issues
- Smoke test points at `Dev/TestArena`; move to Arena 1 in Phase 3 (`SmokeTestSettings.ScenePath`).
- First-frame hitch in builds (~320 ms, 1.1 MB GC): pool prewarm + height-grid/obstacle bake run on the first gameplay frame. Move behind a loading/"FIGHT!" beat in M3.5 (or bake the grid into arena data in M2.1/M3.2).
- Editor-only: ~368 B/frame IMGUI `GUI.Repaint` allocation (not ours; absent in players).
