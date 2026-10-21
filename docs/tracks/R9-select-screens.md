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

## Worker notes (code part, branch `claude/r9-select-eaa956`, cut from `phase5-select-screens`)

**Status:** `Tools/ccheck` errors=0, 0 warnings. **Nothing here has run in Unity:** no bake, no scene build, no test run,
no screenshot. Everything visual and every test below is unverified until the orchestrator runs it.

### What's built
| File | What |
|---|---|
| `Scripts/Core/Layout/GridMath.cs` (new) | `GridMath`: 2D `Step` (row-bounded, stops at edges; down into a short last row lands on its last tile), `StepWrap`, `Climb`, `Size`, `Cell`, `Fit`, `LadderCell/Size`, `CanvasSize` (the CanvasScaler formula). `MenuLayout`: the constants (margin 56, header block 140, buttons block 136, tile 148×190, gap 12, 4 columns, step 500×124, gap 10, shift 22, tablet 560×712) and the per-block scales. In Core because the EditMode tests can't reference `Gladiator.UI` (ccheck reads the main checkout's references). |
| `Scripts/UI/RosterGrid.cs` (new) | The tessera grid: bone tile (9-sliced pixel sprite, chamfered head, rim, shaded foot), cord hole, portrait window (sand backdrop, 128 px face at 1×), carved name without the article ("PIT GOBLIN"). Locked: charcoal, cracks, ink silhouette, "???". Picked: gold frame, gold wreath on the cord, 4-unit lift. Hovered: pale frame (`UiHover`). Open tiles' buttons are named `Card`, locked ones `Locked` (GoblinPlayTests counts `Card`s). `RosterGrid.Slots()` gives both screens the same order: earned (roster order), locked, not-yet-data. Picture fallback: baked portrait → the Codex atlas cell → a pixel bust. |
| `Scripts/UI/LanistaTablet.cs` (new) | The frame (wood, dark lip, bronze pegs, two hinge cords on the left, ochre wax; text in the font's plain material, no outline) and `GladiatorTablet`: portrait 2× (256), name, select line, "N runs · M victories", stats, tradeoff; rule; description; weapon (its card's icon at 2×, name, line); up to 3 traits (bronze-shield mark, name, line). Locked: silhouette, "???", LOCKED, the hint, and "cur / target" when the unlock is one count. |
| `Scripts/UI/ArenaLadder.cs` (new) | `ArenaLadder`: steps bottom to top, each 22 units further right; each step cut from its arena's palette family (A1 dirt, A2 straw, A3 sand, A4 marble, A5 gold, A6 travertine) with its Roman numeral in the family's accent; a beaten boss puts a laurel round the numeral. Locked: charcoal, dark thumbnail, padlock, "???". `ArenaTablet`: name, the ladder's line in quotes, thumbnail 2× (384×216), description; boss portrait, name, "Beaten N×" with a laurel (faint while unbeaten); FLOOR "W × D m · area m²", HAZARDS, DIFFICULTY "Enemy HP +x % · damage taken +y %", RENOWN "×1.5". |
| `Scripts/UI/UiHover.cs` (new) | Pointer enter/exit with an id (the click stays on the Button, so it's never handled twice). |
| `Scripts/UI/UiFactory.cs` | `PixelIcons.Tessera`, `Plate`, `Hole`, `Crack`, `Bust`, `Wreath`, `Sigil`, `Lock`; `Make` takes an optional 9-slice border and pixels-per-unit. |
| `Scripts/UI/MainMenu.cs` | Gladiator select = header + key line, the grid (top-left), the tablet (top-right), BACK (bottom-left) and FIGHT! (bottom-right); the diorama in the middle, unchanged. Arena select = the ladder (bottom-left) + its tablet. Blocks are anchored to their edges and scaled to fit (`Refit`, re-run when `Screen` size changes: two int compares a frame). Kept: `Pick`, `PickArena`, `AsksForArena`, `Selected`, `SelectedRun`, `_available`, `_screen` (Menu/Select/Arena/Codex), `OpenSelect`, `StartRun`, `Launch`. New for tests: `Roster`, `Tablet`, `Ladder`, `ArenaTablet`, `Slot`, `Step`, `PickSlot`, `MoveCursor`, `PickStep`, `Climb`, `FightEnabled`, `ArenaFightEnabled`. |
| `Scripts/UI/CodexPanel.cs` | GLADIATORS tab = the same grid and tablet, read-only (no FIGHT!), in a 700-unit-tall area between the tab row and the bottom row. Entries follow the tiles' order; NEW shows as a gold dot for the visit, then it's seen (as before). One page. |
| `Gameplay/Stats/GladiatorDef.cs` | `portrait`, `portraitSilhouette` (Sprite). |
| `Gameplay/Run/RunDef.cs` | `thumbnail`, `tagline`, `bossPortrait`, `bossName` (the contract's), plus `hazards` (string) and `layout` (ArenaLayoutDef), and `Boss` (= `layout.boss`). |
| `Gameplay/Meta/SaveData.cs`, `MetaProgress.cs` | `gladiatorRuns` counted in `MetaProgress.Apply` (won or lost). `Migrate` seeds an older file's list from `gladiatorWins` (an empty runs list beside non-empty wins can only be a pre-R9 file). No version bump. |
| `Editor/Setup/PortraitBaker.cs` (new) | Menu `Gladiator/Setup/UI/Bake Portraits` (`PortraitBaker.BakeAll()` returns a summary line). `Rig`: preview scene, ortho camera, the key light (shared with CodexSetup). Portraits: head and shoulders from the "Head" part's bounds (top = head top + 10 % of the head's height, bottom = head bottom − 60 %), yaw 150 / pitch 8, 128 px, plus an ink `_sil`. Bosses: `PT_boss_<id>.png`. Arenas: each RunDef's first scene opened in turn, camera Euler(38, 25, 0), FOV 34, pulled back until the layout's floor box (+6 %, 7 m tall) fits, rendered 384×216 with post-processing and box-filtered to 192×108 → `AT_<run id>.png`. Point-filtered sprites, assigned to the defs. Every GladiatorDef / BossDef / RunDef in the project (R2's Colosseum included once merged). |
| `Editor/Setup/RunTaglines.cs` (new) | Fills empty `tagline`, `hazards`, `bossName` and a missing `layout` (`AL_<scene name>`, else the layout whose displayName matches) for each run; the table has the six arenas (`colosseum`: "I have become a legend." / "Trapdoors that drop into the hypogeum, lifts that raise packs from below."); a run not in the table gets its tier's line. |
| `Editor/Setup/CodexSetup.cs` | The atlas bake uses `PortraitBaker.Rig` (same full-body framing as before). |
| `Editor/Setup/MenuSceneBuilder.cs` | After `menu.runs = …AllRuns()`: `RunTaglines.Apply` + `PortraitBaker.Link` per run, then `SaveAssets` (no rendering). |
| `Tests/EditMode/RosterLayoutTests.cs` (new, 9) | 2D moves and edges, wrap/climb, sizes and cells, fit, the scaler's canvas at 16:9/16:10/21:9, the layout at 8 screen sizes × 1-16 gladiators × 1-8 arenas (fits, ≥ 300 units clear for the diorama), full size at 1080p and 16:10, gladiatorRuns counting and the old-save seed. |
| `Tests/PlayMode/RosterTests.cs` (new, 8) | The menu scene only, no run: every tile on screen beside the tablet; **the bug** (all earned, pick the last, every tile still on screen, a real EventSystem click on the first tile picks it); a locked pick (hint on the tablet, FIGHT! off, StartRun and a second click refused, the diorama keeps the last earned); 2D cursor; the ladder (bottom to top, a locked step can't fight, Renown ×1); a beaten Gorm (laurel on step I, Provincial ×1.5 and +25 %); the Codex roster (same count, NEW dot, up/down walks and wraps, left still switches tabs, no Fight button); idle Update + hovers allocate nothing (`Profiler.GetMonoUsedSizeLong`, as GameLoop measures). |

### Decisions (one line each)
- **Codex keys:** left/right stay the tabs and up/down walk the roster (wrapping): `CodexPlayTests.Keys_SwitchTabs_AndPages` expects Right twice from RECORDS to reach ARSENAL. The select screen has full 2D.
- **Locked tiles and steps are pickable** (to read the hint); `Pick(g)` / `PickArena(r)` still refuse locked ones (existing tests); `Selected` / `SelectedRun` stay the last earned pick, so FIGHT!, the diorama and `-bot` are unchanged.
- **Grid edges stop the cursor** (no wrap, no row-hopping): predictable on a d-pad; Enter on a locked tile plays Blocked.
- **Pictures at whole multiples:** 128 px faces at 1× on tiles and 2× on the tablet; 192×108 thumbnails at 1× on steps and 2× on the tablet (exact pixels at 1080p).
- **`RunDef.layout` instead of copied numbers:** floor, difficulty, tier and the boss's records always match the arena's data. **`RunDef.hazards`** is words, so it's a table (RunTaglines), not derived from the layout's arrays.
- **The rig pins the palette shader's ambient globals** (`_GLAmbientSky/Equator/Ground`, EnvironmentLighting's defaults) while it renders: they were whatever the open scene had set, so a bake depended on the open scene. Restored after.
- **Arena thumbnails turn fog off** for the render (the camera stands well beyond the player's fog range). They need every open scene saved: if one is dirty they're skipped with an error (no dialog, so the bridge never blocks); the scene setup is restored after (an untitled scene comes back as a new empty one).
- **Trait marks** are one generic bronze shield (traits have no icons in data); weapon icons come from the weapon's card in `codex.catalog`.
- **The tablet keeps the select line** ("Starts with Rusty Gladius") as the contract lists it, though the weapon row repeats it: one line to drop if it reads as clutter.

### What the orchestrator runs (in order)
1. Merge, let the Editor compile: console clean.
2. With every open scene saved: `Gladiator/Setup/UI/Bake Portraits`, or `Tools/ueval 'return Gladiator.EditorTools.PortraitBaker.BakeAll();'`. Expect `[PortraitBaker] 9 gladiator portraits, <bosses> boss portraits, 5 of 5 arena thumbnails` (6 of 6 once the Colosseum is built). It opens each arena scene and puts the open scenes back. Commit `Assets/Art/UI/Portraits/*`, `Assets/Art/UI/Arenas/*` (and their folder metas), the `GL_*.asset` and `RD_*.asset` changes, and the new scripts' `.meta` files.
3. Optional: `Gladiator/Setup/Meta/Bake Codex Portraits` (the atlas through the shared rig; it should look the same).
4. `Gladiator/Setup/Build Main Menu`: it links each run's layout, words and baked pictures.
5. Tests: EditMode `RosterLayoutTests`; PlayMode `RosterTests`, then the menu tests that must still pass: `CodexPlayTests` (4), `GoblinPlayTests.ClickingTheGoblinsCard_PicksIt_AndTheDioramaShowsIt` and `TheFlag_UnlocksTheGoblin…`, `ProvincialPitTests.LockedUntilGormFalls…`, the Amphitheater / Grand / Imperial menu tests, `RunFlowTests`.

### Screenshots, and what to look for
Take 1920×1080, 1280×720 and 1680×1050 (16:10) of: the gladiator select on a fresh save; all earned with the last picked; a
locked tile picked; the arena select with two or more arenas earned (one beaten boss); the Codex's GLADIATORS tab.
- **Tiles:** ivory tesserae with a chamfered head, a cord hole, the face in a sand window and the name carved below. Locked
  ones dark, cracked, the face pure black, "???". The picked one has a gold band, a wreath on the cord and sits a little higher.
- **Portraits:** head and shoulders, the face filling the window: not cut at the forehead, not a tiny head in empty sand, not
  full body. If off, the framing is in `PortraitBaker.Rig.Render` (the head branch). The Pit Goblin's ears and the Murder
  Machine's bulk are the cases to check.
- **Nothing covers the diorama's gladiator** (the hero stands just right of the grid; check 16:10 especially). Nothing slides.
- **Tablet text fits:** no ellipsis on names; the long descriptions (the Net-Weaver's, the Thunder Champion's) shrink rather
  than spill; three trait rows fit (the Blood Knight has three).
- **Arena thumbnails:** the whole floor, not fogged, readable at 192×108. The ladder climbs bottom-left to top-right with the
  Slave Pit at the bottom, each step in its arena's stone colour, the numeral wreathed where the boss fell.
- **Hover** shows the pale frame; a click picks; a second click on the picked tile fights.

### Risks (unverified)
- `Camera.Render()` of an opened (non-preview) scene in edit mode under URP: expected to work, untested here.
- The head framing per model, and how the 2× tablet portrait reads.
- `GetMonoUsedSizeLong` only moves in heap-block steps: the idle test runs 2,000 iterations so a real per-call allocation crosses one.

### Needs from others
None. R2's Colosseum gets its tile, words and thumbnail on the next bake + menu build (run id `colosseum`, layout `AL_Colosseum`).
