# Tooling — how Claude drives Unity and Blender on this machine

Machine: CachyOS (Linux), KDE Plasma on Wayland. Unity Hub + Unity **6000.6.6f1** (Web module only — Linux standalone is the native target). Blender **5.2.2 LTS**.

## Why not the MCP tools directly
- The **Blender MCP** server (Blender Lab, `ant.dir.gh.blender.blender-mcp`) is installed as a Claude Desktop *extension*, so its tools are only exposed to the Chat tab, not to Claude Code sessions.
- No **Unity MCP** server is configured at all.
- Both apps are reachable through equivalent local bridges, so nothing is blocked. To get native MCP tools in a future Code session instead: `unity mcp configure claude-code` (Unity) — optional.

## Unity — Unity CLI + Pipeline package
- `com.unity.pipeline` (0.8.0-exp.1) is in `Packages/manifest.json`. It runs an HTTP server inside the open Editor that `unity command` talks to (~200–600 ms per call, no domain reload).
- `unity command set_autotick --enable true --persist true` is on, so the Editor keeps ticking while unfocused (Wayland would otherwise throttle it).
- Wrappers in `Tools/`:
  - `Tools/u <command> [--param value]` — any Pipeline command, prints only the result. `Tools/u` with no args lists ~160 commands (`editor_play`, `editor_stop`, `capture_game_view`, `run_tests`, `console`, `package_add`, `simulate_key`, `build`, …).
  - `Tools/ueval '<C# body with return>'` / `Tools/ueval -f file.cs` — Roslyn eval inside the Editor.
  - `Tools/uwait [s]` — block until not compiling / not reloading.
  - `Tools/ucompile [--logs]` — AssetDatabase.Refresh + recompile + wait + dump console; exit 1 if errors.
  - `Tools/snippets/*.cs` — reusable eval snippets (console dump, …).
- If the Editor ever stops answering: `unity pipeline list --format json` (checks Safe Mode → compile errors → read `Logs/Editor.log`).
- First-time package resolution needed the window focused once; done via a KWin script (`qdbus6 org.kde.KWin /Scripting …`). `Tools/u editor_focus` does the same now.

## Blender — direct socket client for the Blender Lab MCP add-on
- The add-on listens on `localhost:9876`. Protocol (same as the MCP server's `connection.py`): send `{"type":"execute","code":…,"strict_json":false}` + NUL, receive JSON + NUL. Code runs on Blender's main thread; assign a dict to `result` to get data back.
- `python3 Tools/blender_bridge.py -c '<python>'` or `python3 Tools/blender_bridge.py BlenderSrc/<script>.py --arg key=value`. Scripts get `ARGS` (dict) and `REPO` injected, and `BlenderSrc/` on `sys.path` (so `import gladiator_kit` works; call `importlib.reload` after editing it).
- Keep each call short (seconds). Screenshots: render via the scripts in `BlenderSrc/` to `Screenshots/blender/…png`, then read the PNG.

## Verification loop for code changes
1. Edit C# → `Tools/ucompile` (must print `errors=0`).
2. `Tools/u editor_play` → let it run → state dump via `Tools/ueval` / `Tools/u console` / `Tools/u capture_game_view --save_path …` → `Tools/u editor_stop`.
3. Tests: `Tools/u run_tests --mode EditMode` / `--mode PlayMode`.

## Gotchas learned
- **Editor throttling:** unfocused, with no bridge traffic, the Editor runs at ~18 fps; while bridge calls arrive it runs at full speed. Never quote Editor FPS. Allocation checks are fine in the Editor (heap growth columns in the frame-stat CSV), but avoid bridge calls during the measured window: the Pipeline server thread's allocations show up in the process-wide heap counter.
- **`capture_game_view` always writes under `Assets/Screenshots`** — use `Tools/ushot`, which moves it out.
- **Dev input from the bridge:** `Tools/ueval 'return Gladiator.Gameplay.DevInput.Hold(0, 1);'` (… `DevInput.Jump()`, `DevInput.Release()`); state dump: `Tools/ueval -f Tools/snippets/player_state.cs`.
- **Finding allocations:** `Tools/ueval -f Tools/snippets/gc_profile_start.cs` while playing, wait, then `Tools/ueval -f Tools/snippets/gc_profile_report.cs 120` → GC.Alloc grouped by profiler sample path. `GameLoop.TrackHeapPerSystem = true` + `GameLoop.HeapReport()` attributes heap growth per ticked system (but background threads pollute it in the Editor). The Editor has a ~368 B/frame IMGUI `GUI.Repaint` allocation that doesn't exist in players.
- **Long main-thread work (builds):** a bridge call must finish its main-thread part within 5 s. `eval` of `BuildScripts.BuildLinuxDev()` still runs the build but the HTTP call errors out; wait on `Logs/Editor.log` for `[Build]`.
- **Standalone perf run:** `Builds/LinuxDev/Gladiator.x86_64 -bot -profile -botSeconds 45 -profileDir "$PWD/Profiling" -screen-fullscreen 0 -screen-width 1920 -screen-height 1080` (add `-stress -stressCount 300`).
- **Locked screen = hung standalone player.** With the KDE session locked (`loginctl show-session <id> -p LockedHint` → `yes`), a windowed player blocks right after "Desktop is … Hz" in its log and ignores SIGTERM (`kill -9` it). The Editor keeps working (auto-tick). Standalone perf runs need an unlocked session.
- **GC triage in the player CSV:** a lone allocation of `32 + 376·N` bytes is a TMP `TMP_CharacterInfo[N]` growth — some label got longer than ever before. Pre-size with the longest string **and** `ForceMeshUpdate(true, true)` right away (`Hud.PreSize`); a SetText alone may never be parsed before the real value replaces it.
- **Editor-only allocations to ignore:** IMGUI `GUI.Repaint` (~368 B/frame) and TMP formatted `SetText` (mirrors the text into `m_text` under `#if UNITY_EDITOR`, ~150 B per call — shows up as `Tick.Hud`).
- **Analyse a run:** `Tools/gcscan.py Profiling/<run>.csv [warmup_s]` lists frames with `gc_bytes > 0` after warm-up and the frame-time/gameplay stats at ≥ 250 enemies (the summary.txt averages include the startup hitch).
