# Progress

**Current phase:** Phase 0 — Foundations
**Current milestone:** M0.4 — Look-dev scene
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

## Next
- M0.4 look-dev scene.

## Known issues
- `DevKinematicMover` is a stand-in; delete when `PlayerMotor` lands (M1.1).
- Smoke test points at `Dev/BotSandbox`; move to the test arena in M1.1 and to Arena 1 in Phase 3 (`SmokeTestSettings.ScenePath`).
