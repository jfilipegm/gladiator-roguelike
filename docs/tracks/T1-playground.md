# T1 — Admin mode and the Playground (the producer's request, 2026-10-23)

> "there should be way i activate an admin version and have access to the 'play ground' arena to test everything and
> every character, Boss etc"

Branch `phase5-playground` (cut from `phase5-artdir`, so it has the A1 cast). Ships with A1 as v0.27.0 if the producer
OKs both merges. Orchestrator owns the spec, integration, Unity runs, tests and the release.

## Design (orchestrator's call; logged in DECISIONS)
1. **Admin mode, any build.** Editor and development builds: always on (as F1 is today). Release builds (Windows,
   macOS, Linux): off until the player **types `ADMIN` on the main menu** (letters in order, within 3 s between
   keys; no on-screen hint) or launches with **`-admin`**. Typing it again turns it off. Remembered between launches
   (PlayerPrefs `gladiator.admin`, not the save). A small gold **ADMIN** tag in the menu's corner while on. For the
   Steam build, a scripting define `GLADIATOR_SHIPPING` compiles the activation out (S1 checklist; BACKLOG).
2. **While admin mode is on:**
   - every gladiator, skin and arena can be picked in the menu. This is a view, not the save: nothing gets unlocked
     or bought, and it all goes back when admin mode is off;
   - F1 opens the admin panel in any arena;
   - the main menu shows a **PLAYGROUND** button.
3. **The Playground.** PLAYGROUND → the select screen (any gladiator, any skin) → FIGHT loads the Playground arena:
   - a big flat field with the test arena's slope, stairs, ramp, platform and walls;
   - **the Showroom:** every character on show in labelled rows: the gladiators with their skins, the enemies by
     tier, then the bosses at full size. They play idle → run → attack → hit in a loop, so you walk among them and
     look from any side;
   - no Director (nothing spawns on its own), the run is timeless, god mode starts on (F1 to change), and Back to
     menu works as usual.
4. **Admin panel additions (F1, in any arena):**
   - **Fight a boss:** six buttons, one per boss. Each loads that boss's arena with the current gladiator and starts
     the boss fight about 3 s in: the real fight, with its entrance, phases and arena hazards;
   - **Freeze enemies:** enemies stand still and don't attack or touch-damage. They still take hits, flash and die.
     For looking at models up close and testing weapons;
   - the existing tools stay: spawn any enemy or elite, upgrades, buffs, god mode, timer, speed.

## Contracts between the parts (names are fixed; code against them)
- `Gladiator.Gameplay.AdminMode` (new, `Assets/Scripts/Gameplay/Dev/AdminMode.cs`, **W-menu**):
  `static bool On { get; }` (Editor / `Debug.isDebugBuild` / `-admin` / the PlayerPrefs flag), `static void Toggle()`,
  `static event System.Action Changed`. No allocation per frame.
- `GameSession` (RunDef.cs, **W-menu**): `public static RunDef[] KnownRuns` — the menu's run list (set by the menu
  on Start, so the admin panel can offer boss fights); `public static bool Playground` — true while the current run
  is the Playground (set by the menu's PLAYGROUND launch, cleared by StartRun/StartDaily/Clear).
- `MainMenu.playgroundRun` (**W-menu** declares `public RunDef playgroundRun;`; **W-play**'s setup assigns
  `Assets/Data/Runs/RD_Playground.asset` on the MainMenu scene's MainMenu).
- `AdminController.Available` → `AdminMode.On` (**W-admin**). `AdminController.PendingBossFight` (static,
  **W-admin**): set before loading a boss's arena, consumed by the arena's AdminController on Start.
- `DevSettings.FreezeEnemies` (**W-admin**), reset with the others.

## Parts (one worker each, disjoint files)
| Worker | Files it owns | Done when |
|---|---|---|
| **W-menu** | new `Gameplay/Dev/AdminMode.cs`; `UI/MainMenu.cs`; `Gameplay/Run/RunDef.cs` (GameSession only) | the typed code and `-admin` toggle admin mode; the ADMIN tag; all gladiators, skins and arenas pickable in admin mode without touching the save; PLAYGROUND button → select → FIGHT starts `playgroundRun` with `GameSession.Playground = true`; EditMode tests for AdminMode (toggle, persistence, flag) and the unlock bypass |
| **W-play** | new `Editor/Setup/PlaygroundSetup.cs` (menu `Gladiator/Setup/Dev/Build Playground`: scene `Assets/Scenes/Dev/Playground.unity`, `AL_`/`RD_Playground` data, assigns `MainMenu.playgroundRun`), new `Gameplay/Dev/Showroom.cs`; `Editor/Build/BuildScripts.cs` (the Playground in every build's scene list) | the setup script builds the scene from the test-arena pieces plus the Showroom; Showroom instantiates every GladiatorDef model (+ skins) and EnemyDef model (bosses included) in labelled rows with the shared character material, drives each with `PartRig` through an idle/run/attack/hit loop (zero GC per frame); a PlayMode test boots the Playground and checks every model is there and animating |
| **W-admin** | `Gameplay/Dev/AdminController.cs`, `UI/AdminPanel.cs`, `Gameplay/Dev/DevSettings.cs`, `Gameplay/Enemies/EnemyManager.cs` (the freeze guard only) | F1 works when `AdminMode.On`; FIGHT A BOSS row (from `GameSession.KnownRuns`; disabled when empty) loads the run and the arena starts the boss ~3 s in; FREEZE ENEMIES toggle; PlayMode tests: freeze stops movement and attacks but not damage; a boss jump reaches the boss fight |

All workers: **never open Unity** (the orchestrator compiles and tests); write code that compiles against the
contracts; keep zero GC per frame in gameplay; match the surrounding code's style; tests where listed.
