# R1 chariot races — notes

Branch `claude/recursing-nightingale-985b9a` (cut from `main`, fast-forwarded to `phase5-imperial-arena` at 25ed4b0).
Code only, no Unity: `Tools/ccheck` errors=0. Rules and numbers: `docs/ARENAS.md` §5 ("the chariot races", "the track
(numbers)"), `docs/tracks/R1-design.md` § "Chariot races"; model parts: `docs/tracks/R1-char-art.md` (`PR_Chariot`),
`docs/tracks/R1-env-art.md` (`PR_LapCounter`).

## What I built
| File | What |
|---|---|
| `Assets/Scripts/Gameplay/Arena/ChariotRace.cs` (new) | The races: schedule, path, telegraphs, trampling the player and the horde, back to back, halt, chariot animation, the lap counter's eggs. `ITickable`, `TickGroup.Enemies` order 32 (after `FireJets`' 31). |
| `Assets/Scripts/Gameplay/Arena/ArenaLayoutDef.cs` | Header "Chariot races (Phase 5.16)": `trackRadius` (default **0** = no races), `trackWestMeta`, `trackEastMeta`, `trackWidth`, `trackStallX`, `chariotFirst`, `chariotEvery`, `chariotTelegraph`, `chariotSpeed`, `chariotCount`, `chariotLaps`, `chariotModel`. Every default except `trackRadius` is the Imperial value. |
| `Assets/Editor/Setup/ArenaBuilder.cs` | One line in `Build` (`if (layout.trackRadius > 0f) AddChariotRace(...)`, after the blade sweepers) and `AddChariotRace`: a `ChariotRace` under `[Arena]` with the layout's fields; `chariotCount` × the model (prefab instances named `Chariot0…`, env palette material, Props layer, not static, inactive); `lapCounter` = the placed piece named `PR_LapCounter`, its `Egg*` children made non-static (static batching would freeze them). |
| `Assets/Tests/EditMode/ChariotTrackTests.cs` (new) | Path math with the Imperial numbers. |
| `Assets/Tests/PlayMode/ChariotRaceTests.cs` (new) | The races in play (Grand Arena scene, see "Tests"). |

## How it works
- **Path** (`BuildPath`, a table of straights and half-circles, no allocation after it): run-in from (stallX, west.z − r)
  east to the west meta's south point; per lap the south straight, the east turn (counter-clockwise from above: south →
  east apex → north), the north straight, the west turn (skipped on the last lap); the run-out west to (stallX,
  west.z + r). The chariot model's root is its footprint centre, placed at `PointAt(s)` on the sampled height.
- **Schedule** (scene game time, like `FireJets`: pauses stop it): Idle → Telegraph (`chariotTelegraph`) → Running
  (chariot n sits at s = −n × lap ÷ count, so it leaves its stall n × `LaunchGap` later) → Idle when the last one is
  back in its stall. A "race start" is the telegraph start: `Races` counts them, the next is due `chariotEvery` after
  it (`chariotFirst + k × chariotEvery` when nothing interrupts). **Back to back** (`BackToBack`, or
  `BossController.Active` alive with `Phase >= backToBackFromBossPhase` and not defeated): the next telegraph starts
  `backToBackGap` after the last chariot is back, at once if none ran for that long. **Halt** (`Halt()`, or by itself on
  `BossController.Active.Defeated`): cancels a telegraph, every chariot on the track vanishes in a dust burst, nothing
  starts again until `StartNow()`.
- **Hits, once per pass** (per chariot: a pass = the same chariot within `footprint + width + 4` m of where it last hit
  that target; every race adds an offset so last race's records never match). The box is square to the chariot:
  `|along| ≤ footprint/2 + r` and `|side| ≤ width/2 + r + 0.5` (the 0.5 m covers the 14 m turns bending inside the
  box), **and** the target's `LaneOffset ≤ width/2 + r` (the whole lane width hurts; the side check rejects the other
  straight 28 m away). `r` is 0.35 for the player, the enemy's radius for enemies.
  - **Player:** feet less than `jumpClear` (1.1) above the lane → `playerDamage` 28 (`DamageFlags.Area`, × the arena's
    `playerDamageTakenMul` inside `PlayerHealth`), impulse `playerKnockback` 13 sideways away from the chariot's centre
    line + `playerKnockUp` 5. Feet above it while in the box, and the box leaves without a hit → `Jumped` +1.
  - **Enemies:** `QueryAlive` round the chariot (spatial hash). Scripted (a boss) → `bossDamage` 600 flat; elite →
    `enemyDamage` 200; ordinary → 200 × `ordinaryEnemyMul` 2. Survivors get `enemyKnockback` 10 sideways.
    **Shields don't help:** a hit the target's shield would block (`EnemyManager.IsBlocking`) is sent from above
    (`Direction = up`, no push), so it lands in full; bosses always get it that way (no shield, no knockback). A hit on
    an invulnerable target (a roaring boss) isn't counted.
- **Telegraph** (`TelegraphRenderer`, the shared orange-red (1, 0.32, 0.12, 0.95)): during the telegraph the south line
  (run-in + straight) fills east, the north line (straight + run-out) fills west, the turns as 16 chords each light one
  after the other: every piece shows the same countdown. During the race: the same pieces at `rimAlpha` 0.35 with fill
  0 (rims + a faint base), plus a fully lit **tongue** per chariot from under it to `tongueSeconds` 1.2 × speed (26.4 m)
  ahead of its nose, following the turns; a chariot still in its stall lights the stall exit 1.2 s before it bursts
  out. Decals: 34 quads for the track + up to ~10 per tongue (≤ ~54 of the 512).
- **Look:** the root leans 8° into the turns (eased); `WheelL` / `WheelR` turn about the chariot's right axis
  (`Rotate(root.right, +deg, Space.World)`, r 0.54), capped at 40°/frame; `Horse0–3` bob (0.08 m) and rock (5°) out of
  phase, `Horse<i>LegsF/B` swing ±30° in opposite phase at 2.6 Hz; a two-mote dust puff behind each chariot every
  0.08 s (`DeathPopFx.Burst`).
- **Feedback:** `SfxId.Horn` twice (0.5 s apart) at the telegraph, `AnnounceKind.Line` "CHARIOTS!", `CrowdExcitement.Spike(0.5)`;
  `SfxId.Gate` at each launch; `SfxId.Slam` on a trample (≤ one per chariot per 0.12 s); `Spike(0.3)` when one chariot
  tramples 8 within 1.5 s.
- **Lap counter:** `Egg0…EggN` found under `lapCounter`; each race tips the next one 95° about its local X over
  0.45 s (pivots at their bottoms). When all seven are down the next race rights them and tips `Egg0`; back to back
  starting rights them all.

## Public API
- State: `Running`, `Telegraphing`, `Halted`, `BackToBack { get; set; }`, `BackToBackNow`, `Count`, `NextStart`, `Clock`,
  `EggsDown`, `EggCount`.
- Counters (bot log, tests): `Races`, `EnemiesHit`, `Kills` (= `Trampled`), `PlayerHits`, `Jumped`, `BossHits`.
- Control: `bool StartNow()` (telegraph now; false if a race is on; lifts a halt), `void Halt()`, `void Configure()`
  (re-read the fields), `void BuildPath()`.
- Chariots: `ChariotActive(c)`, `ChariotPosition(c)`, `ChariotForward(c)`, `ChariotDistance(c)` (m along its path;
  negative = still in its stall; during a telegraph, where it will be).
- Path (pure): `LapLength`, `PathLength`, `Spacing`, `LaunchGap`, `RunSeconds`, `PointAt(s, out forward)`,
  `LaneOffset(p)`, `InLane(p, pad)`.
- **`float Danger(Vector3 p)`**: seconds until a chariot of this race reaches `p` (0 = one is on it), +∞ when no race
  is on or `p` is off the lane; counts the telegraph and the chariots still in their stalls. For the bot: e.g. step
  off the lane while `Danger(player) < 1.5`.
- The boss part needs nothing from here (decision 21 of R1-design.md): the races read `BossController.Active.Phase`,
  `.Alive` and `.Defeated`. `StartNow`, `BackToBack` and `Halt` exist if it wants to drive them.

## Numbers (Imperial values, from the design)
Lap 287.96 m; path 605.94 m (37 + 2 × 287.96 − 43.98 + 37); 27.54 s per chariot at 22 m/s; launch gap 6.55 s; a race
≈ 3 + 6.55 + 27.54 = 37.1 s from the trumpets; 7 races before 8:00 (80, 140, … 440 s). Jump: 1.1 m clears it.
Damage: player 28 (× 1.45 here = 40.6), ordinary 400, elite 200, boss 600.

## Decisions (one-line reasons)
1. **A shield-blocked trample lands from above, in full.** `EnemyManager` blocks by the hit's direction; four horses
   shouldn't be stopped by a Shield Thug (85 %), and the design wants "every ordinary type dies" (a blocked 400 is 60).
2. **A race starts at its telegraph** (`Races`, the crowd roar, the egg, the 60 s cadence): one beat — trumpets,
   "CHARIOTS!", the roar — and it matches `chariotFirst` = "the first race's telegraph".
3. **The telegraph fills every piece at the same rate** (not one front running round the track): the countdown reads
   the same wherever the player looks.
4. **`Jumped` counts a pass that ended with the player in the air over the chariot and not hit** (a jump that lands on
   it is a hit, not a jump).
5. **The tongue starts under the chariot and lights the stall exit 1.2 s early**, so the burst out of the carceres is
   telegraphed too.
6. **Eggs come in sets:** after seven races the eighth rights them; back to back rights them (design Q6).
7. **`Halt` also cancels a telegraph; `StartNow` lifts a halt** (tests; after the boss's fall the next tick halts again).
8. **PlayMode tests run on the Grand Arena** with a smaller track that fits its flat cross (the Imperial scene is the
   layout part's and doesn't exist yet); the EditMode tests check the Imperial numbers themselves.

## Tests (the orchestrator runs them; none run here)
**EditMode `ChariotTrackTests`** (Imperial defaults, no scene):
- `TheLapIs288m_AndAChariotsRunIs606m_At22mps27Point5s_HalfALapApart`: lap = 2 × 100 + 2π·14 (288 ± 0.5), path
  606 ± 1, `RunSeconds` 27.5 ± 0.1, `LaunchGap` 6.5 ± 0.1.
- `ThePath_StartsInTheSouthStallHeadingEast_AndEndsInTheNorthStallHeadingWest`: `PointAt(0)` = (−97, −14) heading +X;
  s = 37 is (−60, −14); the end is (−97, 14) heading −X; clamped past the end.
- `TheChariots_RunCounterClockwise_ThroughTheEastApex_ThenTheWestApex`: (54, 0) heading +Z, then (−10, 14) heading −X,
  then (−74, 0) heading −Z.
- `EachChariot_PassesTheStraightsAndTheEastTurnTwice_TheWestTurnOnce_TheRunInAndOutOnce`: distinct visits within
  0.5 m: (0, ∓14) and (54, 0) twice, (−74, 0) once, the run-in and the run-out once.
- `TheLane_IsTheCentreLinePlusMinus2m_RunInAndOutIncluded_AndTheSpinaIsOffIt`: `LaneOffset` 0 on the straights, the
  apex, the run-in and run-out; 2 at the inner and outer edges; 14 on the spina top and the west meta; `InLane` true
  at 1.9 m, false at 2.5 m, at the player spawn (−10, −26) and in the inner strip; the pad grows it.
- `OneLap_LeavesStraightAfterTheEastTurn_WithNoWestTurn`: one-lap path ≈ 318 m, the west apex is off its lane, it
  still ends in the north stall.
- `NoRace_NoDanger`: `Danger` = +∞ with no race.

**PlayMode `ChariotRaceTests`** (Grand Arena scene, Director and level-ups off, the fire jets and blades disabled, the
player under `ScriptedInput` with no weapons and no i-frames; a `ChariotRace` made on a test object with metae (±30, 0),
radius 14, width 4, stalls at x = −60, two real `PR_Chariot` instances):
- `ARace_TelegraphsTheTrackWithNoChariotOnIt_ThenTwoLeaveHalfALapApart_RunCounterClockwise_AndGoBackIn`: `StartNow`
  telegraphs (a second `StartNow` is refused); halfway through no chariot is active and both models are hidden;
  `Danger` on the lane is finite and above the remaining telegraph, +∞ off it; chariot 0 appears 3 s ± 0.2 after the
  start, chariot 1 `LaunchGap` ± 0.15 after it; 1 s in, chariot 0 is on the south run-in heading +X, its model is on
  the path and its wheel turned between two frames; chariot 0 is back in its stall `RunSeconds` ± 0.25 after leaving;
  then the race is over, nothing is active, and the player (off the track) was never hit.
- `AGroundedPlayerInTheLane_IsHitOncePerPass_ForItsDamage_AndThrownOutOfTheLane`: knockback off, a grounded player
  standing in the lane takes exactly one hit from chariot 0's pass, for 28 × the arena's `playerDamageTakenMul`, and one
  from chariot 1's; with the default knockback the next pass (chariot 0's second lap) hits and 0.8 s later the player is
  out of the lane; `Jumped` stays 0.
- `AJumpOverAChariot_IsNotHit_NorIsAPlayerJustBesideTheLane`: feet held above 1.1 m over the lane while chariot 0
  passes: no hit, `Jumped` = 1; then standing 0.4 m clear of the lane's edge while chariot 1 passes: no hit.
- `AChariot_TramplesOrdinaryEnemies_ShieldOrNot_Takes200OffAnElite_600OffABoss_AndLeavesOneBesideTheLaneAlone`
  (enemies re-rooted every frame: `SE_Root` lasts 2 s): after chariot 0's first pass (chariot 1 not there yet) a
  Convict, a Brute and a Shield Thug in the lane are dead, a Fast elite Brute (486 HP) lost exactly 200, Gorm's body
  (Scripted) lost exactly 600 (`BossHits` 1), a Convict 1.2 m beside the lane is untouched; at the end of the race the
  boss took 4 hits (2 chariots × 2 laps, 2,400) and the Convict beside the lane is still untouched. The player stands
  south of the lane so the Thug, north of the centre line, faces into the chariot's push (a log line says so if the
  staging doesn't make it block).
- `Races_StartChariotEveryApart_AndEachTipsAnEgg` (one lap at 60 m/s, telegraph 1 s, `chariotFirst` 1 s, every 8 s, a
  stand-in counter with 3 eggs): race 1 at 1 s ± 0.1, race 2 8 s ± 0.1 after it; two eggs down, `Egg0` turned 95°,
  `Egg2` upright.
- `BackToBack_RightsTheEggs_StartsAtOnce_ThenWithinTheGap_AndHaltStopsTheRaces`: two races by `StartNow` (2 eggs
  down), nothing starts by itself 2.5 s later; `BackToBack = true` starts race 3 within two frames with the eggs righted
  and one tipped; race 4 starts `backToBackGap` ± 0.25 s after race 3's last chariot is back; `Halt()` mid-race: not
  running, no chariot or model active, `Danger` +∞, and no race 3.5 s later.

The Imperial scene's own tests (the hippodrome, nothing on the track, the boss's phase 3 integration) belong to the
layout and boss parts.

## Verified / unverified
- **Verified:** `Tools/ccheck` errors=0 (all six assemblies, 0 warnings). The path numbers above by arithmetic.
- **Unverified (needs the Editor):** every test; the wheel spin sign and the 40°/frame cap's look (char-art: sign
  unverified in Unity), the lean, the horses' bob/legs, the eggs' tip axis (local X, toward ±z of the counter); whether
  13 m/s really throws the player out of the 4 m lane (the PlayMode test checks it and says to raise
  `playerKnockback` if not); the decal look of 16-chord turns (seams where chords overlap at the inner edge); the
  stall exits at x = −97 behind the carceres' face (the chariot pops out of the bay); FPS with the dust puffs; anything
  about feel.

## Needs from others
- **Layout and setup:** in `ImperialArenaLayout`, set `trackRadius = 14` (the other chariot fields' defaults are the
  Imperial values: metae (−60, 0) / (40, 0), width 4, stall x −97, 80 / 60 / 3 s, 22 m/s, 2 chariots, 2 laps,
  `Props/PR_Chariot`); place `PR_LapCounter` as a piece (the hook finds the instance by that name). In `PlaytestBot.cs`,
  the log line `| races {Races} (trampled {Trampled}, hit you {PlayerHits}, jumped {Jumped}, boss {BossHits})` and,
  optionally, stepping off the lane while `ChariotRace.Instance.Danger(player) < 1.5f`.
- **Boss:** none (see "Public API").
- **Orchestrator:** run both test files; the wheel sign check above when the Imperial scene exists.
