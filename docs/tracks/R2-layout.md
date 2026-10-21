# R2 layout and setup — notes (the Colosseum's layout, data, scene build, bot log)

Branch `claude/r2-layout-8bb080` (cut from `phase5-colosseum` at 2c22c20; `origin/phase5-colosseum` merged in at 775be98). Code only, no Unity. **Nothing here has run in
Unity**: the orchestrator runs the menu items and the tests below.

**Compile check:** after merging `origin/phase5-colosseum` (the hypogeum, enemies, boss and char-art parts, merged by
the orchestrator) into this branch, `Tools/ccheck -w` prints **errors=0, 0 warnings in all six assemblies**, with every
`R2-STUB` marker removed from my files (the names were already the real ones; the merge had no conflicts). Before that
merge, this branch alone stopped at `Gladiator.Gameplay` (the missing `Hypogeum` type), and a throwaway merge with the three
code branches compiled clean.

## What I built
| File | What |
|---|---|
| `Assets/Editor/Setup/ColosseumLayout.cs` (new) | `AL_Colosseum`: the oval's wall line and play outline, invisible stone and player walls on every wall-line edge, the six gates with their models, the six cavea runs, the two boxes and their guests, the crowd along the bowl's profile, the stage, its four stairs and four statues, the four hills, 18 chests, 3 altars, the hypogeum's data (30 trapdoors, 8 lifts, the Victor's lift, the clocks, `swarmMul`), difficulty and pacing, `guardSpots` = the lifts. Menu `Gladiator/Arenas/Regenerate Colosseum Layout (overwrites)`. Public `WallLine()`, `Outline()`, `Ellipse(t)`, `Outward(p)`, `Trapdoors`, `Lifts`, `CenterLift`, `DoorDistance`, `TrapdoorClearance`, `LiftClearance`, `OnTrapdoor(p, pad)`, `OnLift(p, pad)`, and a write-time check that logs an error for any chest, altar, gate mouth, stair / hill / statue footprint or the player spawn within 1 m of a trapdoor or lift. |
| `Assets/Editor/Setup/ColosseumSetup.cs` (new) | Menu `Gladiator/Arenas/Build Colosseum`: the layout, `EN_SkeletalLegionnaire` and `EN_Chimera`, the boss (`BossSetup.EnsureEternalVictor()`, after the Legionnaire exists: his guards), `ST_Colosseum`, the lift packs, `UL_Colosseum` (+ `MetaSetup.CreateOrUpdate`), `RD_Colosseum`, then `ArenaBuilder.Build` → `Assets/Scenes/Arenas/Colosseum.unity`. Public `Scene`, `RunPath`, `UnlockPath`, `TablePath`, `LegionnairePath`, `ChimeraPath`, `BonePileModel`, `EnsureRun()`, `EnsureEnemies()`, `EnsureTable()`, `EnsureLiftPacks(layout)`. |
| `Assets/Editor/Setup/ProvincialPitSetup.cs` | `AllRuns()` appends `ColosseumSetup.EnsureRun()` (the menu's arena list, and what `-arena colosseum` searches). |
| `Assets/Editor/Build/BuildScripts.cs` | `ColosseumSetup.Scene` in the release scenes and the dev build's list. |
| `Assets/Scripts/Gameplay/Dev/PlaytestBot.cs` | Log: `\| hypogeum drops N (fallen F, hit you H, jumped J, boss B) lifts W (raised L, released R) dodged D`, `piles P (risen, stomped, burned, denied)`, `breaths B (hit you H)`, and on the boss line `falx N (hit you H, stopped S) grasps G falseDeaths F (rose R, burst hit B)`. Behaviour: steps off a door or lift whose `Hypogeum.Danger(pos)` < `hypogeumDodge` (2.5 s), won't step onto one then, and walks round held doors (`hypogeumLookAhead` 2 m; `hypogeumDodge 0` turns it off). No allocation. |
| `Assets/Tests/PlayMode/ColosseumTests.cs` (new) | Six tests, below. |
| `Assets/Tests/PlayMode/ArenaNavTests.cs` | The test body became `Check(scene)`; a second test runs it on the Colosseum (timeout 180 s: a 208 × 146 m bake). |

## The numbers
- **Wall line:** 90 corners, **clockwise** from above (as `GrandArenaLayout.Inset` needs): (96.67, 11) → (96.67, −11)
  (edge 0, the Gate of Death's chord), the ellipse's samples t = −11.25° … −168.75° (every 3.75°), (−96.67, −11) →
  (−96.67, 11) (edge 45, the Gate of Life's chord), the samples t = 168.75° … 11.25°. Five samples fall inside each great
  gate's span (|z| < 11) and are replaced by the chord: 96 − 10 + 4 = 90. `ChordX` = 98 √(1 − 11²/67²) = 96.670.
- **Play area (inset 1.1 m): 20,005 m², 1.31× the Imperial Arena's 15,302**, ≥ the GDD's 17,500 (a Python copy of
  `WallLine` + `Inset`; the design's geometry script gets the same). Wall line 20,576 m².
- **Crowd: 3,524** = 3,494 in the bowl (1,364 zones) + 30 in the boxes. Rows: the senators' terrace (u 1.35, y 4.0, 0.5 /
  m), maenianum I (tier k at u 2.4 + 0.8(k − ½), y 4.0 + 0.5k, 0.5 / m), II (u 7.4 + …, y 7.7 + 0.5k, 0.45), III
  (u 12.4 + …, y 11.4 + 0.5k, 0.35): one zone per wall-line edge per row, its ends moved out along the ellipse's normal at
  each corner, a running remainder instead of per-zone rounding. Per row: terrace 232, maenianum I 232–246, II 219–229,
  III 183–198; the top tier stands at 13.9 m.
- **Pieces: 22**: `SM_ColFloor` (Walkable), `SM_ColCavea_0`–`_5` (Decor, origin), `SM_ColPulvinar` ×2 (Decor, (0, ±67),
  yaw 180 / 0), `SM_ColStage` (Walkable), `SM_ColStairs` ×4 (Walkable, (0, 12) 0, (12, 0) 90, (0, −12) 180, (−12, 0) −90),
  `PR_VictorStatue` ×4 (Solid, (±9.9, ±9.9), yaw atan2(x, z)), `SM_ColMons_A` (28, ±50.5) and `SM_ColMons_B` (−28, ±50.5)
  (Walkable, yaw 180 north / 0 south). Trapdoors and lifts aren't pieces: the hypogeum hook places them.
- **Gates (counter-clockwise from the east):** G0 `SM_ColGreatGate` (96.67, 0) yaw −90; G1 (49.00, 58.02) −158.46; G2
  (−49.00, 58.02) 158.46; G3 `SM_ColGreatGate` (−96.67, 0) 90; G4 (−49.00, −58.02) 21.54; G5 (49.00, −58.02) −21.54 (the
  side gates `SM_ColGate`, the layout's `gateModel`, computed on the ellipse at t = 60, 120, 240, 300°, facing along the
  inward normal). `bossGate` 0.
- **Values:** id `colosseum`, "The Colosseum", tier 5, centre (0, 0, 0), radius 96.9, aspect 0.68, fog (90, 380), spawn
  (0, 0.1, −26) yaw 0, maxAlive 450, densityMul 1.3, swarmMul 1.5, spawnMaxDistance 55, eliteEvery 60, sun (32, −40, 0),
  enemyHpMul 2.55, playerDamageTakenMul 1.55, gateModel `Environment/SM_ColGate`, no secrets, `guardSpots` = the 8 lifts,
  solidWallHeight 8.4, no zones / barricades / braziers / spikes / cages / jets / blades / chariots.
- **Hypogeum:** the contract's 30 doors (x, z, yaw, group) copied verbatim, size (8, 4), `Environment/SM_ColTrapdoor`; the
  8 lifts, size 4, `Environment/SM_ColLift`; the centre lift (0, 1.2, 0) ×1.25; drops 40 / 14 / 2 / 4 s, lift waves
  90 / 45 s, 3 per wave, ≥ 12 m away.
- **Clearances** (the write-time check's Python copy): the closest thing to a door or lift is a hill footprint's corner,
  3.82 m from a ring door; then chests (≥ 4.9 m), the stairs, statues, gate mouths and the spawn far further.
- **Enemies:** `EN_SkeletalLegionnaire` (Chaser, 34 HP, 3.7 m/s, jitter 0.15, r 0.36, accel 14, contact 9 / 0.9 s,
  knockback ×1, turn 540, xp 2.5, gold 0.05, prewarm 140, the Convict's anim; revive 1 × after 2.5 s at ×0.5 HP, stomp
  0.9, burn stops it, `pileMesh` = the first mesh in `Props/PR_BonePile.fbx`, filled in on a later build if the FBX
  arrives late); `EN_Chimera` (Breather, 420 HP, 3.0 m/s, jitter 0.05, r 0.95, accel 8, contact 14 / 1.0 s, knockback
  ×0.3, turn 160, windup 1.0, range 7, cone 8 m × 60°, 24 damage, ×3 to the horde, breath 1.0 s, 3 patches r 1.4 for 4 s at
  10 dps, knockback 4, cooldown 4.5, recover 1.4, xp 30, gold 0.6, prewarm 6, the War Boar's anim `PartAnim_Boar`). Both
  pick up `Enemies/EN_<name>.fbx` through `CombatSetup.Enemy`.
- **Table:** ARENAS.md §6 exactly (12 entries; the Legionnaire from 0:45, 0.8 → 6.0, cap 160, swarm, elite; the
  Centurion 1:15, 0.12 → 0.2, cap 2, not elite; the Chimera 4:00, 0.08 → 0.15, cap 2, not elite); the Slave Pit's
  affixes. **Lift packs:** Pit Cur 1:30 → 4:00 w 3, 4–6, cap 100, "THE BEASTS RISE!"; War Boar 2:30 → end w 2, 2–3, cap
  12, same call; Chimera 4:00 → end w 1, 1, cap 2, "A CHIMERA!"; Legionnaire 4:30 → end w 3, 5–7, cap 160, "THE DEAD
  RISE!".
- **Run and unlock:** `UL_Colosseum` (id `colosseum`, `DefeatBoss emperors_champion` × 1, the design's hint and
  description); `RD_Colosseum` (id `colosseum`, "THE COLOSSEUM", the design's description).

## Decisions (one line each)
1. **The wall line runs clockwise**, not counter-clockwise as R2-design's sketch says: `GrandArenaLayout.Inset` moves a
   clockwise polygon inward (a counter-clockwise one would grow outward). The gates are still listed counter-clockwise.
2. **The 30 doors are the contract's table verbatim**, not recomputed by arc length: env-art cuts the floor's holes from
   the same table, so they match to the centimetre.
3. **No seats over the great gates' chords** (the contract), seats over the side gates kept: 3,524, not the design's
   ~3,786 estimate, which counted the great gates' 22 m spans on every tier.
4. **Behind each box, the rows below 11 m are cut** (terrace, maenianum I and II, for |x| < 7.8): its canopy reaches 9 m,
   so spectators there would stand inside it; maenianum III (from 11.9 m) runs on behind it. `BoxHalf` 7.8 is R2-design's
   15.6 m estimate of the box's width: **env-art's model decides it.**
5. **The boxes' guests:** 7 senators either side of the throne (local (0, 4.0, −0.8)) and one on it, in both boxes (the
   Emperor; the editor of the games): 15 a box.
6. **No braziers:** the contract keeps fire out of the static art (the telegraphs are the floor's only orange-red).
7. **The lift packs are set by `ColosseumSetup`**, not the layout writer: they point at enemy assets created after the
   layout. A pack whose enemy is missing is skipped, and appended on a later build once it exists (like the table).
8. **The bot probes `Danger` instead of using `OutOfRect`:** 16 bearings at 1.5, 3 and 4.5 m, the nearest safe ring, the
   bearing nearest its goal. Same result for one door (2 m across), but it also steps over a same-group neighbour (the
   axis doors are 4 m apart) and handles lifts, with no door geometry. Held doors: it turns its heading up to ±150° in 25°
   steps until the point 2 m ahead is safe (else it waits). Its log counts `dodged`.
9. **The bot's boss line has the boss part's counters** (`DiscsStopped`, `Grasps`, `RemainsRisen`, `RiseBurstHits`,
   R2-boss.md's request). It doesn't dodge the flying falx (god mode; noted below).
10. **`-arena colosseum` needs no bot code:** `MainMenu.Launch` finds the run by id in its run list; the main menu must be
    rebuilt after the arena's setup (as R1).
11. **The player walls stay ArenaBuilder's 30 m boxes (y −3 to 27)**: above the 22 m bowl, so the contract's "≥ 30 m"
    is met without a new field.
12. **`ArenaNavTests` became one body run on two scenes** rather than a copy (the Colosseum's case checks that the
    invisible floors over the holes bake the same at runtime).

## R2-STUB markers
All removed from this part's files (the calls were written with the real names). Still in the boss part's
`BossController.cs` (4 lines, its `SetHpFloor` / `SetHp` calls, which now resolve against the enemies part): the
orchestrator's to strip. The cross-part names this part uses: `ArenaLayoutDef`'s hypogeum fields and `LiftPack`,
`Hypogeum` (`Instance`, `Danger`, `Halt`, `Count`, `LiftCount`, `GroupOf`, `doors`, `lifts`, `centerLift`, the counters),
`Director.swarmMultiplier`; `EnemyBehaviour.Breather`, the `EnemyDef` revive and breath fields, `EnemyManager`'s pile and
breath counters; `BossSetup.EnsureEternalVictor`, `BossDef.entranceRise`, `BossController`'s falx and false-death counters.

## Menu items for the orchestrator (after merging all four code parts and the art)
1. `Gladiator/Arenas/Build Colosseum`. Console: `[ColosseumLayout] play area 20005 m² (1.31× the Imperial Arena's
   15,302), 90 corners, 22 pieces, 6 gates, 30 trapdoors, 8 lifts, crowd 3524 in 1370 zones`, and **no**
   `[ColosseumLayout] … within 1 m of a trapdoor / lift` error. It creates `AL_Colosseum`, `EN_SkeletalLegionnaire`,
   `EN_Chimera`, `EN_/BD_EternalVictor`, `ST_Colosseum`, `UL_Colosseum` (into `MetaConfig.unlocks`), `RD_Colosseum`, the
   scene and its nav bake. If `PR_BonePile.fbx` wasn't there yet, a warning; run it again once it is (the pile mesh is
   filled in then).
2. `Gladiator/Setup/Build Main Menu` (the menu stores its run list: the arena select and `-arena colosseum`).
3. Tests (below), then `Gladiator/Build/Linux Development Build` and `Tools/perfrun bot 600 -timed -arena colosseum`.

## Tests: PlayMode `ColosseumTests` (written, not run)
| Test | Asserts |
|---|---|
| `TheGreatOval_IsBiggerThanTheImperialArena_AndKeepsTheHordeIn` | 90 outline corners; `PlayArea()` 20,005 ± 30, > 1.25 × the Imperial's, ≥ 17,500; tier 5; inside 1.8 m / outside 0.5 m from the wall line on 22 bearings (every 15°) and at both great gates' chords; convicts spawned 3 m behind the wall on 24 bearings and in all six gate tunnels are pulled back in. |
| `LockedUntilTheEmperorsChampionFalls_ThenARunThereIsTier5_WithItsOwnDifficulty_AndTheHugestWaves` | locked on a fresh save and after the Patron; beating `emperors_champion` earns it; Renown ×3.5 for tier 5; the menu launches `Colosseum`; tier 5, HP ×2.55, damage ×1.55, `ST_Colosseum`, cap 450, density 1.3, swarms ×1.5, spawn distance 55, elites 60 s, 6 gates, a `Hypogeum`, boss `eternal_victor`; the save's deepest arena is 5. |
| `NothingStandsOnATrapdoorOrALift_NorWithinAMetreOfOne` | every chest (18), altar (3), the spawn, every gate's spawn mouth, the 4 stairs' and 4 hills' footprints and the 4 statues' plinths are > 1 m from every door and lift (the layout's numbers); no door hides under a hill; no collider (`Layers.SolidMask`) in a 0.45 m sphere at 1 m height every 0.5 m over every door and lift + 1 m (> 9,000 checks). |
| `TheStage_IsWalkableAtOnePointTwo_TheHillsAtTwoPointTwo_AndTheHordeClimbsThem` | height grid: 1.2 on the stage and over the Victor's lift, 0 on the floor, a closed door and a lift, 2.2 on all four hill tops, the north stairs rising (< 0.35, 0.2–1.1, > 0.9); a convict north of the stage reaches a player on it (y > 1, r < 9.5) within 10 s; one south of hill A reaches its top (y > 2) within 10 s. |
| `TheHypogeum_IsTheLayouts_AndTheBossGuardSpotsAreTheLifts` | the layout's hypogeum fields (30 doors, groups 6/6/6/6/3/3, sizes, models, the centre lift, the clocks, `swarmMul`), `guardSpots` = the lifts, the closest two doors 4 m apart; in the scene `Hypogeum` has 30 doors and 8 lifts with the layout's groups, the boss's `guardSpots` are the lifts, every door model at its hole with its yaw (and `LeafF` / `LeafB`), every lift at its hole (with `Platform`), the Victor's at (0, 1.2, 0) ×1.25. |
| `ALegionnaireAndAChimera_ComeFromTheTable_AndTheLiftsRaiseTheDesignsPacks` | the 12 table entries exactly, no Slinger / Fire-Pot / Mercenary / Beast Handler; both defs' numbers, `pileMesh`, models; the 4 lift packs in order; spawned in the scene with ×2.55 max HP. |
| `TheGatesBoxesStatuesAndCrowd_AreTheContracts_AndTheLightIsLateAfternoon` | boss gate 0, `eternal_victor` with `entranceRise` and the Legionnaire as his guard; gate 0 / 3 `SM_ColGreatGate` on their chords facing in, 1 / 2 / 4 / 5 `SM_ColGate` on the ellipse facing along the inward normal; the boxes at (0, ±67) facing the stage; one of each cavea run, the stage and the floor; the statues at r 14 facing out; crowd 3,300–4,000 with the top tier at 13.9 m; fog (90, 380), the sun at (32, −40). |
Plus `ArenaNavTests.Colosseum_BakedNavData_IsUsed_AndMatchesRuntimeBake`. Also run the other parts' Colosseum tests
(`HypogeumTests`, `UndeadTests`, `ChimeraTests`, `EternalVictorTests`) and `ImperialArenaTests` as a regression (the run
list grew).

## Verified / unverified
- **Verified:** `Tools/ccheck -w` errors=0, 0 warnings in all six assemblies on this branch (with `phase5-colosseum`
  merged in). By arithmetic (a Python copy of `WallLine`, `Inset`, the
  crowd rows and the clearance check): 90 clockwise corners, the play area 20,005 m², the crowd 3,524, the gates'
  positions and yaws (match the contract), every anchor, gate mouth and footprint ≥ 3.8 m from every hole.
- **Unverified (needs the Editor):** the menu item and the scene build; every test above; the look: the cavea runs
  meeting the gates, the boxes' width against the crowd cut, the crowd standing on the tiers env-art actually modelled,
  the hills' and stage's heights in the bake; the bot's dodging in play; performance (3,524 instanced spectators, 450
  enemies, 39 animated doors and lifts).

## Needs from others
- **env-art:** `SM_ColPulvinar`'s real width (my crowd cut assumes 15.6 m: `ColosseumLayout.BoxHalf`); each hill's flat
  top must contain its origin (the chests and altars sit at the hills' origins at 2.2 m); the stand profile as the
  contract (the crowd stands on those numbers).
- **char-art (done, merged):** `Props/PR_BonePile.fbx`, `EN_SkeletalLegionnaire.fbx`, `EN_Chimera.fbx`, `EN_EternalVictor.fbx`.
- **Enemies part (answered):** the Legionnaire's pool headroom: the Director sizes table pools from the table (its cap
  160, plus a full swarm × density × `swarmMul`, × 1.1 + 8: ~300 with the code's default curve), well above cap + 48; check
  `PoolGrowths` in the first bot run.
- **Hypogeum part (done on its branch):** prewarming the lift packs' enemies (the War Boar isn't in the table).
- **Orchestrator:** the menu items above; in `docs/ARENAS.md` §6 (not my section): the crowd
  is 3,524 (no seats over the great gates), the side gates' yaws ±158.46 / ±21.54.

## Concerns (for CONCERNS.md)
- **The hills' rock face** is a 2.2 m step in a bilinear 0.5 m height grid: like the spina's sides in R1, the horde may
  climb it anywhere, not only up the slopes. Harmless (the test passes either way); watch it in play.
- **`SM_ColMons_B`'s 0.55 m terraces** may be taller than the player's `CharacterController` step: the player might have
  to jump each one. Check in play; if so, env-art lowers them to ≤ 0.35 or adds a ramp.
- **The bot doesn't dodge the falx** (god mode: its `hit you` counters measure the bot, not the throw's fairness), nor
  any boss attack; R2-boss.md suggests treating `DiscFlying` as a lane to stay out of.
