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
- **FBX export settings (verified M0.2):** `apply_unit_scale` + `FBX_SCALE_UNITS`, `-Z` forward / `Y` up, `bake_space_transform`, face smoothing, mesh+empty only, no leaf bones. A 1×2×1 m box arrives as exactly (1, 2, 1) m, origin at the base, front at +Z, Blender +X (asset's left) at Unity −X (no mirroring), identity rotation/scale on every node, child pivots preserved. Unity side: `useFileScale`, scale 1, `bakeAxisConversion` off.
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
