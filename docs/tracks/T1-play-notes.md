# T1 W-play notes: the Playground and its Showroom

Branch `t1-play` (from `phase5-playground`). Nothing here was compiled or run: the orchestrator does that.

## What I built
- `Assets/Editor/Setup/PlaygroundSetup.cs`, menu **Gladiator/Setup/Dev/Build Playground**:
  - writes `AL_Playground` and `RD_Playground` (no unlock, not in `ProvincialPitSetup.AllRuns()`, scene path
    `Assets/Scenes/Dev/Playground.unity`, `layout` = AL_Playground);
  - builds the scene with `ArenaBuilder.Build` (run objects, boundary loop, nav/height bake → `Playground_Nav.asset`);
  - then turns the Director off, the Arena Master off, sets `RunEndController.meta = null`, adds the Showroom, saves;
  - opens the menu scene, sets `MainMenu.playgroundRun` by name (SerializedObject, warns if the field is missing),
    saves it, and reopens the Playground.
- `Assets/Scripts/Gameplay/Dev/Showroom.cs`: rows built on Start, `PartRig` loop idle → run → attack → hit (1.5 s
  each, golden-ratio phase offset per model, hit flash), GameLoop tick (Feedback +40), no allocation per tick, labels
  turned to the camera about Y, no colliders. Test API: `Count`, `IndexOf`, `ModelAt`, `LabelAt`, `LabelText`,
  `RowOf`, `MoveOf`, `NameOf`.
- `Assets/Editor/Build/BuildScripts.cs`: the Playground is in the dev build and in `ReleaseScenes`.
- `Assets/Tests/PlayMode/PlaygroundTests.cs`: 3 tests (every model and label there; every ArmR moves in 1.2 s and 400
  ticks allocate 0 B; timeless + god mode + Director off + nothing spawns + not recorded + switches put back).

## Decisions (for DECISIONS.md)
- **Built from data through ArenaBuilder** (CLAUDE.md §2.5) instead of copying TestArenaScene. AL_Playground is
  code-owned: every build rewrites it, so edit `PlaygroundSetup`, not the asset.
- **Field:** `SM_PitFloor` scaled 3.4 (156 m, flat to 61 m), fence of 126 `SM_Fence_3m` at r 60.3, the boundary loop
  at r 60, enemies inside r 59. Spawn (0, 0.1, 0) facing east. Test pieces = TestArenaScene's, moved (-26, 0, 0).
  Showroom at (8, 0, 0), yaw 90 (rows run north-south, each further east, models face west). Chests/altar to the south.
- **Timeless:** there is no per-arena duration (`ProgressionConfig.runDuration` is shared), so the Playground uses
  `DevSettings.Timeless`, the switch F1 already shows. **God mode starts on** the same way (spec §3). The Showroom sets
  both on enable and puts back what it found on disable (a static owner, so a reload in either order is safe). They
  show as the HUD's dev tags and F1 can turn them off.
- **Not recorded:** `RunEndController.meta = null` ("None = this scene's runs aren't recorded"): no Renown, records
  or unlocks from the Playground. No secrets there either.
- **Arena Master off:** no boredom whims (their swarms need the Director anyway).
- **Rows:** an enemy's tier = the first ladder arena that brings it (spawn table, cage beast, lift packs, boss guards,
  and escorts), mapped to the GDD ladder: Arena 1 → I, 2–3 → II, 4–5 → III, 6 → IV. Row 2 = tiers I–II, row 3 =
  tiers III–IV (the Colosseum's two included, else they had no row). Bosses = every `BossDef.enemy`, in arena order.
  Expected now: 10 gladiators + 20 skins, 12 tier I–II, 6 tier III–IV, 6 bosses (54 models).
- **Labels:** the theme's Title font (Marcellus SC) with its Outline preset; LiberationSans if no theme. Models on the
  Props layer, shadows on.

## What to run, in order
1. Merge W-menu first (it declares `MainMenu.playgroundRun`), then compile; console clean.
2. **Gladiator/Setup/Dev/Build Playground.** Expect in the log: `[PlaygroundSetup] Showroom: 10 gladiators, 20
   skins, 12 enemies of tiers I–II, 6 of tiers III–IV, 6 bosses`, `[ArenaNavBaker] Assets/Data/Arenas/Playground_Nav.asset`,
   `[PlaygroundSetup] MainMenu.playgroundRun → Assets/Data/Runs/RD_Playground.asset`. A warning that the field is
   missing means W-menu isn't in: run it again after merging.
3. **Gladiator/Setup/Build Main Menu rebuilds the menu without the link.** Either run Build Playground after it, or
   add one line to `MenuSceneBuilder.Build` after `menu.run = run;`:
   `PlaygroundSetup.Link(menu, AssetDatabase.LoadAssetAtPath<RunDef>(PlaygroundSetup.RunPath));`
4. PlayMode: `PlaygroundTests` (3), then the full suite (the tests' teardown unloads the Playground so its switch
   restore can't land on the next test).
5. Look: play the Playground from the Editor; screenshot from the spawn (the front row 8 m east) and from the side.
   Watch for: labels overlapping (raise `minWidth` / lower `labelSize`), models sunk or floating (ground raycast on the
   Environment layer), bosses too close (`gap`), and the in-place run cadence (`Run` uses the profile's full speed).

## Notes
- `PortraitBaker.AllRuns()` collects every RunDef, so a portrait bake also bakes RD_Playground's thumbnail. Harmless.
- The Nobody has no `GladiatorDef.model`: the Showroom uses `CH_Nobody` (through `ModelStyle`, so the A1 twin).
- Tunables on the Showroom component: `stageSeconds`, `poseSharpness`, `attackTimes`, `recoilAngle`, `gap`,
  `minWidth`, `rowGap`, `labelSize`, `bossLabelSize`, `godMode`, `timeless`.
