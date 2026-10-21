# R1 layout and setup — notes (the Imperial Arena's layout, data, scene build, bot log)

Branch `claude/funny-bun-03b4a6` (cut from `main`, fast-forwarded to `origin/phase5-imperial-arena` at a061887). Code
only, no Unity: `Tools/ccheck` errors=0, 0 warnings (all six assemblies). **Nothing here has run in Unity**: the
orchestrator runs the menu items and the tests below.

## What I built
| File | What |
|---|---|
| `Assets/Editor/Setup/ImperialArenaLayout.cs` (new) | `AL_ImperialArena`: the hippodrome's wall line and play outline, stands (straight and curved), invisible stone and player walls on every wall-line edge, the carceres (22 bays, the two at z = ±14 open), corner towers, the five gates with their models, the pulvinar and its court, the spina (10 segments, 2 metae, the obelisk, the lap counter, 8 stairs), four terraces with stairs, braziers, 14 chests, 3 altars, the chariot fields, difficulty and pacing. Menu `Gladiator/Arenas/Regenerate Imperial Arena Layout (overwrites)`. Also `TrackOffset` / `OnTrack` and a write-time check that logs an error for any anchor, brazier, gate mouth or the player spawn within the lane + 1.5 m. |
| `Assets/Editor/Setup/ImperialArenaSetup.cs` (new) | Menu `Gladiator/Arenas/Build Imperial Arena`: the layout, `EN_Praetorian` and `EN_Centurion`, the boss (`BossSetup.EnsureEmperorsChampion()`, always called so his Guard link is filled once the Centurion exists), `ST_ImperialArena`, `UL_ImperialArena` (+ `MetaSetup.CreateOrUpdate`), `RD_ImperialArena`, then `ArenaBuilder.Build` → `Assets/Scenes/Arenas/ImperialArena.unity`. |
| `Assets/Editor/Setup/ProvincialPitSetup.cs` | `AllRuns()` appends `ImperialArenaSetup.EnsureRun()` (the menu's arena list, and what `-arena <run id>` searches). |
| `Assets/Editor/Build/BuildScripts.cs` | `ImperialArenaSetup.Scene` in the release scenes and the dev build's list. Nothing else lists arenas (grep for `GrandArenaSetup.Scene`, `RD_GrandArena`, `grand_arena`). |
| `Assets/Scripts/Gameplay/Dev/PlaytestBot.cs` | Log: `| races N (trampled X, hit you Y, jumped J, boss B) dodged D formations F linesBroken L`, and `javelins J charges C` on the boss line when he throws or charges. Behaviour: steps off the lane when `ChariotRace.Danger(pos) < chariotDodge` (1.5 s) and won't step onto it within `chariotLookAhead` (2 m) while a chariot is due (both tunable on the component; `chariotDodge 0` turns it off). No allocation. |
| `Assets/Scripts/UI/Hud.cs` | A phase-3 notch on the boss bar next to the phase-2 one when `phase3At > 0` (R1-boss.md's request). |
| `Assets/Tests/PlayMode/ImperialArenaTests.cs` (new) | Seven tests, below. |

## The numbers
- **Wall line:** 34 corners, clockwise: (−95, 44) → (51, 44), 15 chords round the sphendone (r 44 round (51, 0)) to
  (93.73, 10.5), the triumphal gate's 21 m chord to (93.73, −10.5), 15 chords to (51, −44), → (−95, −44). Convex, so
  the radial edge table works from the centre (0, 0).
- **Play area (inset 1.1 m): 15,302 m², 1.31× the Grand Arena's 11,674** and above the GDD's 13,500 (the design's
  15,315 used 24 segments and a round apex). Wall line 15,868 m². Outline from x = −93.9 to 92.63, z ±42.9.
- **Stands:** the long sides' runs (m sections × width): north 39.5 (10 × 3.95), 27.5 (7 × 3.93), 27.5 (7 × 3.93), 15.5
  (4 × 3.875); south 15.5 (4), 69 (17 × 4.06), 39.5 (10). The sphendone: 30 `SM_ImpStand_Curve` sections, one per
  wall-line chord (3.90 m, scale 0.975), at the chord's middle facing (51, 0).
- **Crowd: 2,183** = 2,166 on the stands (0.75 per metre per tier, curved tiers longer by (44 + offset) / 44, a running
  remainder instead of per-zone rounding, 298 zones) + 16 senators in the pulvinar + 1 Emperor at his throne.
- **Pieces:** 145 (floor, 22 carceres, 2 towers, 59 straight + 30 curved stands, pulvinar, 10 spina, 2 metae, obelisk,
  lap counter, 8 spina stairs, 4 terraces, 4 terrace stairs) + 5 gates + 6 braziers (each with a point light, as the
  Grand Arena's 8) + 2 chariots. Env-art measured their 150-piece preview at 43,434 tris.
- **Track:** metae (−60, 0) / (40, 0), radius 14, width 4, stalls at x = −98: lap 287.96 m, a chariot's path
  38 + 2 × 287.96 − 43.98 + 38 = **607.9 m** (27.6 s at 22 m/s), the rest as the design (80 / 60 / 3 s, 2 chariots,
  2 laps). Closest things to the lane + 1.5 m: the inner-strip chests (6.5 m from the centre line), the west altar (8 m),
  the spina braziers (11.8 m), the player spawn (12 m).
- **Values:** id `imperial_arena`, tier 4, radius 42.9, fog (80, 330), spawn (−10, 0.1, −26) yaw 0, maxAlive 400,
  densityMul 1.25, spawnMaxDistance 55, eliteEvery 60, sun (45, −35, 0), enemyHpMul 2.15, playerDamageTakenMul 1.45,
  gateModel `Environment/SM_ImpGate`, bossGate 0, no secrets, no guard spots, no zones / barricades / spikes / cages /
  jets / blades.
- **Gates:** [0] `SM_TriumphalGate` at (93.73, 0) yaw −90; [1] (−50, 44) and [2] (30, 44) yaw 180; [3] (−50, −44) and
  [4] (30, −44) yaw 0 (`SM_ImpGate`, the layout's `gateModel`).
- **Enemies:** `EN_Praetorian` (ShieldBearer, 70 HP, 3.0 m/s, r 0.5, accel 14, contact 10, block 75 % / 120°,
  knockback ×0.25, turn 180, xp 4, gold 0.06, prewarm 40, `PartAnim_Murmillo`); `EN_Centurion` (Lunger, 160 HP,
  2.6 m/s, jitter 0, r 0.42, accel 10, contact 10, windup 0.7, range 4.2, lunge 8 × 0.25 s, 16 damage, cooldown 3.5,
  recover 0.7, knockback ×0.3, turn 80, xp 12, gold 0.35, prewarm 8, the Convict's anim; escort `EN_Praetorian` × 5 on
  slots (±2.6, 1.8), (±1.3, 1.8), (0, 1.8), hold 2.6, pad 0.8, depth 2.6, knockback 9, `freeEscortsOnDeath` false,
  `SE_Stagger`, shields down 3 s). Both pick up their FBX (`Enemies/EN_<name>.fbx`) through `CombatSetup.Enemy`.
- **Table:** ARENAS.md §5 exactly (the Centurion from 1:00, 0.15 → 0.25, cap 3, not swarm, not elite; no Beast Handler,
  no loose Praetorian); the Slave Pit's affixes.

## Decisions (one line each)
1. **The triumphal gate stands on a flattened apex at x = 93.73, not at (95, 0)** (env-art's recommendation): the gate
   is 21 m wide, so at (95, 0) its ends would sit 1.27 m behind the curve with the stands' end caps showing.
2. **`trackStallX` −98, not −97** (env-art's recommendation): the 4.44 m chariot is wholly inside the open bay's tunnel
   only between −98.78 and −97.22; at −97 its nose pokes 0.22 m out of the facade when it appears. Costs 2 m of path
   (0.09 s per chariot). The EditMode `ChariotTrackTests` use the component's defaults (−97) and are unaffected.
3. **One wall-line edge per curved stand section** (15 a side): the stands, the invisible stone and the player walls
   line up exactly on the same chords; 24 segments over the full half circle (the design's) wouldn't match the stands.
4. **The Emperor is one spectator** at the throne's front (no NPC model): "the Emperor watching from his box" for one
   instance.
5. **`Build()` always calls `BossSetup.EnsureEmperorsChampion()`**, not only when the layout has no boss: the boss data
   is created once, and a later call fills his empty Guard list once `EN_Centurion` exists.
6. **The Centurion's escort and break status are repaired if empty** (as `BeastmasterSetup` does for the handler), so a
   rerun after `SE_Stagger` or the Praetorian appear fixes them.
7. **Missing table enemies are skipped with a warning**, not written as null entries.
8. **The bot dodges chariots** (the optional part): without it, the 10-minute bot would stand in the lane and its log's
   "hit you" would measure the bot, not the race. `dodged` counts how often it had to.

## Menu items for the orchestrator (in this order)
1. `Gladiator/Arenas/Build Imperial Arena`. Console: `[ImperialArenaLayout] play area 15302 m² (1.31× the Grand Arena's
   11,674), 145 pieces, 5 gates, crowd 2183`, and **no** `[ImperialArenaLayout] … is on the chariot track` error. It
   creates `AL_ImperialArena`, `EN_Praetorian`, `EN_Centurion`, `EN_/BD_EmperorsChampion`, `ST_ImperialArena`,
   `UL_ImperialArena` (into `MetaConfig.unlocks`), `RD_ImperialArena`, the scene and its nav bake.
2. `Gladiator/Setup/Build Main Menu`: the menu scene stores its run list, so the arena select and `-arena
   imperial_arena` only see the new run after this.
3. Tests (below), then `Gladiator/Build/Linux Development Build` and `Tools/perfrun bot 600 -timed -arena
   imperial_arena` (CSV `*_bot_ImperialArena.csv`).

## Tests: PlayMode `ImperialArenaTests` (written, not run)
| Test | Asserts |
|---|---|
| `TheHippodrome_IsBiggerThanTheGrandArena_AndKeepsTheHordeIn` | 34 outline corners; `PlayArea()` 15,302 ± 30, > 1.25 × the Grand Arena's, ≥ 13,500; tier 4; inside / outside the edge at the carceres, both long sides, the sphendone at 45° and the triumphal gate; convicts spawned behind the carceres, both sides, the curve and the gate are pulled back in. |
| `LockedUntilThePatronFalls_ThenARunThereIsTier4_WithItsOwnDifficulty_AndTheBiggestHorde` | locked on a fresh save; beating `patron` earns it; the menu picks it and launches `ImperialArena`; tier 4, HP ×2.15, damage ×1.45, `ST_ImperialArena`, cap 400, density 1.25, spawn distance 55, elites every 60 s, 5 gates, a `ChariotRace`, boss `emperors_champion`; the save's deepest arena is 4. |
| `NothingStandsOnTheTrack_NorWithinOnePointFiveMetresOfIt` | every chest, altar, brazier (6), the player spawn and every gate's spawn area are off the lane + 1.5 m (`ChariotRace.InLane`); no collider (`Layers.SolidMask`) in a 0.45 m sphere at 1 m height every metre along the whole path and every 0.5 m across the lane + 1.5 m, inside the play area (> 8,000 checks). |
| `TheTrack_IsTheLayouts_AndARaceComesOutOfTheCarceresOpenBay` | the race's fields are the layout's (radius 14, width 4, metae, stall −98, 80 / 60 / 3 s, 22 m/s, 2 × 2); lap 288, path 608; 2 hidden chariots; the lap counter with 7 eggs; next start 80 s; two `SM_CarceresOpen` at z = ±14 on x = −95 and 20 `SM_Carceres`; `StartNow`: telegraph, then chariot 0 alone on the south run-in (x −98 … −80, z −14) heading east, on the floor, one egg down; chariot 1 out `LaunchGap` later; the player at the spawn is never hit. |
| `TheSpina_IsWalkableAtOnePointTwoMetres_AndTheHordeClimbsItsStairs` | height grid: 1.2 on the spina and both terraces, 0 on the inner strip and the track, the stairs at x = −20 rising from < 0.35 to > 0.9; a convict below the south stairs reaches a player standing on the spina (y > 1, \|z\| < 3) within 10 s. |
| `ACenturion_FromTheTable_MarchesBehindHisLineOfFive_AndKillingHimBreaksIt` | the table's Centurion entry (from 60, 0.15 → 0.25, cap 3, not elite or swarm), no Praetorian or Beast Handler entry; both defs' key numbers and formation fields; spawned: +1 formation, 5 formed, each on its slot (< 0.6 m) and between him and the player; killed: +1 line broken, all 5 alive, out of formation, shields down. |
| `TheBossGate_IsTheTriumphalGate_AndTheEmperorWatchesFromThePulvinar` | `BossFight.gate` 0, boss `emperors_champion` with `EN_Centurion` as his Guard; gate 0 is `SM_TriumphalGate` at (93.73, 0) facing −X, gates 1–4 `SM_ImpGate` on the long sides facing in; `SM_Pulvinar` at (−10, 0, 44); crowd 2,000–2,400; `StartNow`: he appears at x > 80, \|z\| < 5. |

Also run: `EmperorsChampionTests` (needs this scene), `BossPhaseDataTests` (needs `BD_EmperorsChampion`),
`FormationTests`, `ChariotRaceTests`, `ChariotTrackTests`, and `GrandArenaTests` / `BeastmasterTests` as regressions.

## Verified / unverified
- **Verified:** `Tools/ccheck` errors=0, 0 warnings. By arithmetic (a Python copy of the layout code): the areas, 34
  convex corners, the stand runs, the crowd count, the pieces count, every anchor's / brazier's / gate mouth's distance
  from the lane, and that every anchor is inside the outline.
- **Unverified (needs the Editor):** the menu item and the scene build; every test above; the look: the pieces'
  orientations (taken from R1-env-art.md), the straight stands meeting the curved ones at x = 51, the curved sections
  tiling, the triumphal gate on its chord, the chariot leaving its tunnel at x = −98; the bot's dodge in play; the Hud
  notch; performance (2,183 instanced spectators, 145 pieces, 6 brazier lights; the Grand Arena measured 400 enemies
  inside budget).
- **To watch in play:** `HeightGrid.Sample` is bilinear over 0.5 m cells, so the spina's 1.2 m side reads as a 0.5 m
  ramp; at 60 fps an enemy rises ~0.12 m a frame there, under `maxStepUp` 0.35, so the horde may climb the spina's sides
  anywhere, not only its stairs (the formations part expected them to jam). The spina test passes either way.

## Needs from others
- **Orchestrator:** the menu items above (the build, then the main menu); the tests. In `docs/ARENAS.md` §5 (not my
  section): the triumphal gate is at (93.73, 0) on a flattened apex, the stalls at x = −98 (run-in and run-out 38 m, a
  chariot's path 608 m, 27.6 s), the play area 15,302 m².
- **Spotted, not R1:** the Codex's bestiary defaults (`CodexSetup.Defaults`) stop at tier 2: no Grand Arena or Imperial
  enemies or bosses, and `CodexPanel.ArenaName` names tiers 2+ "ARENA n".
