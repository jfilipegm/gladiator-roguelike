# Progress

**Current phase:** Phase 1 — Feel
**Current milestone:** M1.2 — Placeholder enemies + gladius arc + hit feedback
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

## Next
- M1.2 placeholder enemies + gladius arc + hit feedback.
- M1.3 HUD, XP gems + magnet, stub level-up.
- Gate 1 report.

## Known issues
- Smoke test points at `Dev/TestArena`; move to Arena 1 in Phase 3 (`SmokeTestSettings.ScenePath`).
- No standalone build yet → no trustworthy FPS numbers (Editor throttles when unfocused). First standalone stress run in M2.1.
