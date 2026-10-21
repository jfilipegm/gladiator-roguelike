# R9 contract — the select screens and the Codex roster (orchestrator, 2026-10-21 19:35)

**Item branch:** `phase5-select-screens`, cut from `main` (v0.17.0). The orchestrator does every merge. **Release:** with
the next version after it's integrated. Touches no file that R2 (the Colosseum) owns, so its worker may run while R2's
do; it's integrated in Unity after R2.

## Why (the producer's playtest, 2026-10-21)
- **Bug:** on CHOOSE YOUR GLADIATOR, picking the last earned gladiator slides the card row to its right end (so the
  locked cards can be read). The picked card and every earlier card leave the screen, and the mouse can't bring them
  back (the arrow keys still can). `MainMenu.RefreshPick`.
- **Request (verbatim):** "This part and the map ones need to be re done, i was thinking of having a slot under codex
  where you could see all the gladiators, but each gladiator should display just a square with their face similar to
  what megabonk has but maybe give it a personal touch not a str8 rip off". The reference shown: Megabonk's character
  selection, a grid of square face portraits on the left (locked ones as black silhouettes), the picked character in
  the middle, and a detail panel on the right (portrait, name, rank and runs, description, weapon, passive, skins,
  CONFIRM).

## The design (the orchestrator's proposal; the producer may redirect)
Megabonk's layout (grid, model, detail panel) with our own Roman touch:
- **Tiles are tesserae.** A *tessera gladiatoria* was the bone token a Roman gladiator was given after his first
  fights. Each tile is an ivory/bone tessera (a square with a chamfered top and a small hole for the cord) with the
  portrait set into it and the name carved below. Locked: a dark, cracked tessera with the portrait as a pure-black
  silhouette and the name "???". Picked: a gold laurel frame and a small lift. Hovered: a lighter frame.
- **Portraits** are head-and-shoulders renders of each gladiator's own model (3/4 front, the game's flat palette look),
  rendered small (128 px) and shown at 2× with point filtering: chunky and retro, matching the art.
- **The detail panel is the lanista's wax tablet:** a dark wood frame round a warm wax panel, the text "scratched" in.
  It shows the big portrait, name, select line, "runs N · victories M" with this gladiator, description, the starting
  weapon (icon, name, one line), each trait (icon, name, one line), the tradeoff and the stats line. Locked: "???", the
  silhouette and the unlock hint.

### Gladiator select (replaces the sliding card row)
Left: the roster grid, 4 columns, all gladiators in roster order (the open ones first, then the locked), fitting at
1920×1080 with no scrolling for up to 16. Centre: the 3D diorama (`ShowHero`) as today. Picking a locked tile shows
its tablet, and the diorama keeps the last open gladiator. Right: the tablet. Bottom: BACK and FIGHT!; FIGHT! is
disabled while a locked tile is picked.

### Arena select (the "map" screen): the ladder
Left: the arenas as wide tiles stacked **bottom to top like steps**, the Slave Pit at the bottom and the newest at the
top: the climb itself. Each tile has a thumbnail of its arena and the name; locked ones are dark with "???". Right: the
tablet with the name, the arena's line from the design ("I need to survive." … "I have become a legend."), floor size,
hazards, the boss's portrait and name, the difficulty (enemy HP +x %, damage taken +y %), the Renown multiplier, and
the best result there (the boss beaten or not). Locked: the hint.

### The Codex's GLADIATORS tab
The same roster grid and tablet, read-only (no FIGHT!), replacing the tab's text list. New-entry dots work as today.

### Input
Mouse: click picks, a second click on the picked tile fights (as today). Keyboard/gamepad: arrows/WASD/d-pad move in
2D through the grid (up/down on the ladder), Enter/Space/A fights, Esc/B goes back. Nothing slides: every screen fits
1920×1080 and scales with the canvas scaler (check 1280×720 and 16:10).

## Shared names
- **Fields:**
  - `GladiatorDef.portrait` (Sprite) and `GladiatorDef.portraitSilhouette` (Sprite).
  - `RunDef.thumbnail` (Sprite), `RunDef.tagline` (string), `RunDef.bossPortrait` (Sprite) and `RunDef.bossName`
    (string). Don't touch `BossDef.cs`: R2's boss part owns it.
- **Save:** `SaveData.gladiatorRuns` (`List<IdCount>`, like the existing `gladiatorWins`), counted where a run ends.
  Bump the save version only if the loader needs it.
- **Baker:** `CodexSetup.cs` already bakes the Codex's portrait atlas from the models in a preview scene
  (`BakePortraits`, 128 px cells, `Assets/Art/Textures/UI/CodexPortraits.png`). Reuse it: extend it, or factor its
  camera setup into `Assets/Editor/Setup/PortraitBaker.cs` with an Editor menu item `Gladiator/Setup/UI/Bake Portraits`.
  It renders, with the game's URP look and a fixed camera:
  - each `GladiatorDef.model` → `Assets/Art/UI/Portraits/PT_<gladiatorId>.png` and `PT_<gladiatorId>_sil.png`;
  - each boss model → `PT_boss_<bossId>.png`;
  - each `RunDef`'s first arena scene, from a fixed high 3/4 camera → `Assets/Art/UI/Arenas/AT_<runId>.png`.

  It imports them as point-filtered sprites and assigns them to the fields above. It must work for any number of
  gladiators and runs, so R2's Colosseum gets its tiles once it merges.

## Part (one worker: code and Editor scripts, no Unity)
**Owns:**
- `Assets/Scripts/UI/MainMenu.cs` and `CodexPanel.cs`;
- new UI files (e.g. `RosterGrid.cs`, `LanistaTablet.cs`, `ArenaLadder.cs`);
- the additions to `UiFactory.cs` / `PixelIcons.cs`;
- `GladiatorDef.cs` (the portrait fields), `RunDef.cs`, `SaveData.cs` and the run-end recorder (`gladiatorRuns`);
- `Assets/Editor/Setup/PortraitBaker.cs` (new), `CodexSetup.cs`, `MenuSceneBuilder.cs`, and the taglines / boss names in the existing
  run setups. **Not** `ProvincialPitSetup.cs`'s run list or anything R2 owns: put taglines on the runs from the baker,
  or a small new `RunTaglines` table in its own Editor file.
- tests: keep `MainMenu.Pick` / `PickArena` / `AsksForArena` and every existing menu and Codex test passing; add
  `RosterTests` (PlayMode: grid fits the screen, a locked pick disables FIGHT!, 2D keyboard moves, the last open
  gladiator stays on screen, i.e. the bug) and EditMode tests for the grid maths;
- notes: `docs/tracks/R9-select-screens.md` § "Worker notes".

**Must not touch (R2 owns them):** `Hypogeum.cs`, `ArenaLayoutDef.cs`, `ArenaBuilder.cs`, `StaticObstacles.cs`,
`Director.cs`, `EnemyDef.cs`, `EnemyManager*.cs`, `BossDef.cs`, `BossController.cs`, `BossFight.cs`, `BossSetup.cs`,
`Colosseum*.cs`, `PlaytestBot.cs`, `ProvincialPitSetup.cs`, `BuildScripts.cs`, `ArenaNavTests.cs`.

**Done when:** `Tools/ccheck` errors=0; the baker, the three screens and the tests are written; the notes list what the
orchestrator must run (the baker, the menu scene build) and what to look for in the screenshots.
