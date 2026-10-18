# Decisions

One line each: what, and why. Newest at the bottom of each section.

## Tooling / project
- **Drive Unity via Unity CLI + `com.unity.pipeline`, Blender via a direct socket client to the Blender Lab add-on** — the MCP tools aren't exposed to Claude Code sessions here; these bridges offer the same capabilities (C# eval, screenshots, play mode, tests / bpy exec). See `docs/TOOLING.md`.
- **Unity 6000.6.6f1** — the only installed 6000.x editor (spec: "latest installed 6000.x").
- **Cinemachine 6.6.0** — in Unity 6.6 Cinemachine ships as a built-in package versioned with the editor (CM3 API).
- **Tests split into `Gladiator.Tests.EditMode` + `Gladiator.Tests.PlayMode`** instead of one `Gladiator.Tests` — the Test Framework decides EditMode vs PlayMode per assembly platform; one asmdef can't cleanly host both.
- **Git LFS for `.blend` and audio only; FBX/PNG stay in plain git** — procedural low-poly FBX and palette PNGs are a few KB; `.blend` files are binary and grow.
- **`Profiling/` and `Screenshots/` are git-ignored** — they're regenerable outputs; numbers that matter are copied into PROGRESS/gate reports.
