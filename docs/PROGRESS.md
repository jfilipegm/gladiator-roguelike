# Progress

**Current phase:** Phase 0 — Foundations
**Current milestone:** M0.3 — PlaytestBot + profiling CSV + test scaffolding
**Last updated:** 2026-10-18

## Resume checklist (read first after a context reset)
1. Read `CLAUDE.md`, then this file, then `docs/TOOLING.md`.
2. `unity status --format json` → Editor ready? If not: `unity pipeline list --format json`.
3. `python3 Tools/blender_bridge.py -c "import bpy; result={'v': bpy.app.version_string}"` → Blender reachable?
4. `Tools/ucompile` → must print `errors=0` before doing anything else.

## Done
- **M0.1** — Project (Unity 6000.6.6f1, URP, Linear, Input System 1.20) + Cinemachine 6.6.0, Test Framework 1.8.0, Pipeline bridge 0.8.0-exp.1. Folder structure, asmdefs (Core / Gameplay / UI / Editor / Tests.EditMode / Tests.PlayMode), `.gitignore` + `.gitattributes` (LFS for .blend/audio), git init, docs scaffolding, `CLAUDE.md`, tooling wrappers in `Tools/`. Model import rules (`ModelImportRules` AssetPostprocessor). Verified: clean compile, assemblies load.

- M0.3 PlaytestBot + ProfilerRecorder CSV + test scaffolding.
- M0.4 look-dev scene.

## Known issues
- (none)
