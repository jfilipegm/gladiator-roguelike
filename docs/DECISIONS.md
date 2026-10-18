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
