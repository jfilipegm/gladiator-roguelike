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
  - `Tools/ucompile [--logs]` — AssetDatabase.Refresh + recompile + wait + dump console; exit 1 if errors. The bridge's own "Failed to handle /api/exec request" errors (timeouts during builds, aborts during domain reloads) are counted apart as "bridge noise", not as errors.
  - Clear the console: `Tools/ueval 'System.Type.GetType("UnityEditor.LogEntries,UnityEditor").GetMethod("Clear").Invoke(null,null); return "ok";'`
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
- **Headless perf runs (preferred): `Tools/perfrun bot [s]` / `Tools/perfrun stress <n> [s] [extra args]`** — gamescope headless backend on the real GPU at 1080p, no window, works while the desktop is locked; prints the summary + GC scan. perfrun passes `-profileFrames` sized for ~800 fps so long uncapped runs don't fill the frame recorder early (the default 108k frames lasted only 150 s at 720 fps). Full run with the Director: `Tools/perfrun bot 495 -timed` (bot immortal, clock runs, ends "Survived" at 8:00).
- **Player profiler captures:** `Tools/perfrun stress 300 45 -fps 60 -rawProfile 15 -rawFrames 1500` writes `Profiling/stress_TestArena.raw`; `Tools/rawreport Profiling/stress_TestArena.raw [slowFrames] [depth]` → slowest frames' marker trees + GC.Alloc grouped by caller (also saved as `.raw.report.txt`). Startup: add `-profiler-enable -profiler-log-file <path>.raw` to the player command line.
- **Harmless build warning:** "Pre-baked collision is disabled on N mesh(es)…" — Unity 6.6 doesn't expose that mesh-import option to scripts yet and still pre-bakes today; `PlayerSettings.bakeCollisionMeshes` is on.
- **Bot runs are immortal and timeless** (DevSettings.GodMode/Timeless while the PlaytestBot is active). Add `-mortal` / `-timed` to let it die or run out the clock.
- **Admin tools from the bridge:** `Tools/ueval 'Gladiator.Gameplay.AdminController.Instance.SpawnNearPlayer(25); return "ok";'` (also `SetBuff`, `KillPlayer`, `SetTimer`, `AdjustCrowd`, `SetOpen(true)`); state: `RunEndController.Instance.CurrentPhase`, `DevSettings.*`.
- **Locked screen = hung standalone player.** With the KDE session locked (`loginctl show-session <id> -p LockedHint` → `yes`), a windowed player blocks right after "Desktop is … Hz" in its log and ignores SIGTERM (`kill -9` it). The Editor keeps working (auto-tick). Standalone perf runs need an unlocked session.
- **GC triage in the player CSV:** a lone allocation of `32 + 376·N` bytes is a TMP `TMP_CharacterInfo[N]` growth — some label got longer than ever before. Pre-size with the longest string **and** `ForceMeshUpdate(true, true)` right away (`Hud.PreSize`); a SetText alone may never be parsed before the real value replaces it.
- **A lone 24 B allocation** = Unity creating a C# wrapper for a native object script code touches for the first time (a collider from a raycast/overlap, `.transform`, `.gameObject`). Pre-touch them at load (see `CameraCollisionGuard.Awake`).
- **Editor-only allocations to ignore:** IMGUI `GUI.Repaint` (~368 B/frame) and TMP formatted `SetText` (mirrors the text into `m_text` under `#if UNITY_EDITOR`, ~150 B per call — shows up as `Tick.Hud`).
- **Analyse a run:** `Tools/gcscan.py Profiling/<run>.csv [warmup_s]` lists frames with `gc_bytes > 0` after warm-up and the frame-time/gameplay stats at ≥ 250 enemies (the summary.txt averages include the startup hitch).
- **Mixed-roster perf runs:** add `-mix` (every EnemyDef in the catalog, random per spawn) and optionally `-eliteChance 0.03` to any bot/stress run, e.g. `Tools/perfrun stress 300 45 -mix`.
- **Review screenshots of staged fights:** in Play mode, `Tools/ueval 'return Gladiator.EditorTools.DevShowcase.Lineup();'` (all types + elites in front of the camera) or `.Fight()`; `DevShowcase.Pause()` → `Tools/ushot <name> 1600 900` → `DevShowcase.Resume()`. Decals/projectiles stay drawn while paused.
- **Don't recompile while in Play mode** (domain reload mid-play throws a stray SlashFx NullReference): `Tools/u editor_stop` first.

- **Key presses in Play mode:** the bridge's `simulate_key` doesn't reach `Keyboard.current` with the Editor unfocused. Queue state events instead: `Tools/ueval 'UnityEngine.InputSystem.InputSystem.QueueStateEvent(UnityEngine.InputSystem.Keyboard.current, new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.Digit2)); return "down";'`, then the same with `new KeyboardState()` a few hundred ms later for the release.
- **UI GC rule:** never hide a UGUI panel with `SetActive` or `Canvas.enabled` (a disabled canvas drops and re-creates its GraphicRegistry set: ~8 KB per show) and never use `Button`/`Selectable` colour transitions on panels that open mid-run. Use a `CanvasGroup` (alpha + blocksRaycasts) and `UiHit` for pointer input.
- **Installing Unity build modules on this distro:** `unity install-modules` downloads the module `.pkg` (a macOS xar archive) into `~/.config/unityhub/downloads/` and then fails ("N item(s) failed to install"; `~/.config/unityhub/logs/cli-log.json`: `/usr/bin/tar exited with code 2`) because GNU tar can't read xar. It also **closes the running Editor of that version first**. Manual install: `bsdtar -xf <pkg> TargetSupport.pkg.tmp/Payload`, then `gzip -dc Payload | cpio -idm` into `~/Unity/Hub/Editor/<ver>/Editor/Data/PlaybackEngines/<Windows|Mac>StandaloneSupport/`, and set `"selected": true` for the module in that Editor's `modules.json`. Restart the Editor afterwards. Windows/Mac (Mono) support was installed this way on 2026-10-19.
- **Releases:** `Tools/release <version>` (on an up-to-date `main`): compiles, builds Linux release + dev, Windows and macOS through `BuildScripts.BuildReleaseSet`, packs them into `Builds/Releases/v<version>/`, tags and publishes the GitHub release with `docs/releases/v<version>.md` as notes. `--pack-only` stops before tagging.
- **If the Editor is killed** (e.g. by the module installer), the relaunched Editor stops on a modal "Scene Backup Detected" dialog that nobody can click while the desktop is locked. With the producer's OK: force-stop it, move `Temp/__Backupscenes/` out of the way (Unity checks the folder, not just the file), then `unity open "<project>"`. Relaunching through the Hub also flips `ProjectSettings/UnityConnectSettings.asset` `m_Enabled` to 1 — never commit that.
- **The PlaytestBot freezes the run clock (Timeless) and enables God mode by default**; pass `-timed` (and `-mortal`) for runs whose clock must advance. A `-bot` run without `-timed` staying at 0:00 is not a bug.
- **Staging in Play mode:** `RunBotSensor.ChooseCardHook(0)` closes a pending level-up; `UnityEngine.Object.FindAnyObjectByType<…>` (plain `Object` is ambiguous in `ueval`). Foreground `sleep` is blocked for the agent: wait inside a background command instead.
- **Smoke-running the release archives (after `Tools/release`):** extract each archive into its own empty scratch folder.
  - Linux: `timeout -s KILL 120 gamescope --backend headless -W 1920 -H 1080 -w 1920 -h 1080 -- ./Gladiator-v<ver>-linux/Gladiator.x86_64 -bot -timed -botSeconds 40 -screen-fullscreen 0 -screen-width 1920 -screen-height 1080 -logFile "$S/linux.log"`.
  - Windows: unzip, then `export WINEPREFIX="$S/wineprefix" WINEDEBUG=-all WINEDLLOVERRIDES="mscoree,mshtml="` and run the same gamescope line with `wine ./Gladiator-v<ver>-windows/Gladiator.exe …`. Give `-logFile` as a `Z:\…` path with backslashes. Afterwards run `wineserver -k`.
  - **Check:** the log has `[Gladiator] v<ver>`, `[PlaytestBot] finished after 40.0s`, a `build:` line whose `outcome … at` is close to 38 (the clock ran), and 0 `Exception` lines. Wine runs end with exit 137 because the timeout kills Wine's leftover processes after the game has quit (the log ends with "CodeReloadManager destroyed"), so that exit code is expected.
  - **Clean up:** the bot writes `~/.config/unity3d/SoloDev/Gladiator/Profiling/*_bot_SlavePit.*`; delete the files your smoke run created. Bot runs use `save_bot.json`, never the player's save.
- **Bot log `hitstops N (S s)`:** hit-stop count and real seconds spent in it. A run whose clock lags the wall clock by much more than the level-up pauses usually has too much hit-stop (v0.2.0 fix: crits and elite hits share `HitFeedbackConfig.hitStopInterval`).
