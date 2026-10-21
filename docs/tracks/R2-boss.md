# R2 boss — notes (the Eternal Victor)

Branch `claude/r2-boss-c2a944`, cut from `phase5-colosseum` (2c22c20). Code only: no Unity, no Blender. Owns
`BossDef.cs`, `BossController.cs`, `BossFight.cs`, `BossSetup.cs` (`EnsureEternalVictor()`), the new PlayMode
`EternalVictorTests.cs` and the additions to the EditMode `BossPhaseDataTests.cs`. Source: `docs/BOSSES.md` "The
Eternal Victor", `docs/tracks/R2-design.md` § "The boss", `docs/tracks/R2-colosseum.md` (ids, enum values, the arena's
numbers).

## What was built

### `BossDef`: new fields (every default keeps the older bosses as they are)
| Field | Header | Default | Eternal Victor |
|---|---|---|---|
| `whipGrasp` | Whip Lash | false | true (bone hands along the lane, the falx into the deck; `whipSlow 0.1` = held) |
| `discLength`, `discWidth`, `discWindup`, `discSpeed` | Falx Throw | 18, 1.6, 0.8, 24 | 20, 1.6, 0.8, 26 |
| `discDamage`, `discKnockback`, `discSpin` | Falx Throw | 18, 6, 1080 | 20, 6, 1080 |
| `discRepeats`, `discPart` | Falx Throw | (1, 1), `"Falx"` | (1, 1), `"Falx"` |
| `phase3DiscRepeats`, `phase3PounceRepeats` | Phase 3 | 0, 0 (= the phase-2 values) | 2, 2 |
| `entranceRise`, `riseDepth`, `riseTime` | Entrance | false, 3, 2.6 | true, 3, 2.6 |
| `falseDeath`, `remainsHp`, `remainsTime`, `riseHp01` | Eternal | false, 1500, 8, 0.2 | true, same |
| `remainsText`, `riseText` | Eternal | "", "" | "HE WILL NOT STAY DOWN!", "\"NOT YET.\"" |
| `riseBurstRadius`, `riseBurstDamage`, `riseBurstKnockback`, `riseBurstWarn` | Eternal | 4, 20, 10, 1 | same |
| `remainsColor` | Eternal | bone (0.89, 0.84, 0.74, 0.85) | same |

### `BossController`
- **Enums appended:** `BossAttack.Disc` = 10 (after `Volley` = 9), `State.Remains` = 10 (after `Dead` = 9).
- **`BeginRise(Vector3 at)`** (shares `SpawnBody` with `Begin`, which is unchanged in effect): he spawns at
  `ground − riseDepth` under `at` (ground from the height grid), facing the player, invulnerable; `Entering` lasts
  `riseTime`: `Position.y` eases (smoothstep) up to the ground, a kneeling pose (one knee up, the falx across his knees,
  root −0.7) straightens over the last 40 %; no walking, no contact. A player within 2.9 m of the lift (on its level)
  is shoved off (9 m/s + 4 up, no damage, every 0.35 s). Then the usual `Roaring` (the falx raised: the salute).
  `Rising` is true meanwhile.
- **The Falx Throw (`Disc`):** `Weight` 2.4 when 4 < d < `discLength × 0.9`; wind-up `discWindup` (÷ the phase's speed):
  the lane fills, the right arm drawn back high, the body twisted. On the strike the falx leaves his hand: it flies in
  world space from `Hand` along the aim at `discSpeed`, spinning `discSpin` about the vertical through its mesh's middle,
  until it has gone `discLength`, or `Obstacles.Overlaps(xz, 0.4)` (stone: a statue; walk-only pits don't count), or
  it leaves the arena's outline; then back toward his current `Hand`, caught within 0.6 m (its bind pose back). The
  player within `discWidth / 2 + playerRadius` of its path this frame (a segment, so no tunnelling) and between 1.8 m
  below and 2 m above it is hit (`discDamage`, along the flight, `discKnockback`) once out and once back. The lane stays
  lit at full while it's out. He stands in `Strike`, facing the falx (no other attack), until it's caught; then again
  (re-aimed) per `discRepeats` / `phase3DiscRepeats`, else `Recover`. The falx keeps flying if a phase roar interrupts;
  his remains, a forced attack or his death snap it back to his hand (the pooled view is never left mid-throw).
- **The grasp (`whipGrasp`):** the Whip's lane and hit are unchanged (the slow `whipSlow 0.1` for 1 s = held); the
  crack's burst and SFX become six bone-coloured bursts along the lane and `Slam`; poses: both arms raised in the
  wind-up, driven down with the body leaning 35° on the strike.
- **Phase-3 repeats:** `PounceRepeats` and `DiscRepeats` use the `phase3…Repeats` fields when > 0 (like R1's
  `ChargeRepeats`).
- **The false death:** with `falseDeath`, `SetHpFloor(slot, 1)` at spawn. In his last phase (3 when `phase3At > 0`),
  not entering or roaring, HP at the floor → `EnterRemains`: the falx back, `SetHpFloor(0)`, `SetHp(remainsHp)`,
  invulnerable 0.4 s, grounded (a leap ends where he is), every repeat cleared, `State.Remains` for `remainsTime`,
  `remainsText` (BossPhase), a bone burst, `Splinter`, shake, crowd spike, `FalseDeaths++`. In Remains: a heap (body
  folded 80°, head down, arms out, root −1.1), no movement, no contact, the guard clock paused, a bone ring (r 2.2) fills,
  and in its last `riseBurstWarn` an orange-red circle of `riseBurstRadius` fills while the heap stirs. Killed → `Die()`
  as before (`Defeated`). Time out → `RiseFromRemains`: `SetHp(maxHp × riseHp01)`, the burst (a grounded player within
  `riseBurstRadius + playerRadius`: `riseBurstDamage`, thrown away), `riseText`, and a roar (`Enraging` back into the
  same phase, invulnerable `enrageRoar`) with no phase text and no extras (no guards, no fence), then `Chase`.
- **Poses for a model with a `Falx` part** (signs unverified in Unity, as R1's): Chase, the falx low across the body
  (ArmR (−25, 0, −15), ArmL (−45, 0, 30)); Reaper's Circle, wound up twisted back (Body yaw −45), then a real full turn
  of his yaw during the 0.3 s strike with both arms out; the lunges, both arms drawn back then thrust; the roar, the
  falx raised high.
- **Public, for the hypogeum, the bot and tests:** existing `Phase`, `CurrentState`, `Position`, `GuardWaves`,
  `Defeated`, `Pounces`, `AttacksDone`; new `Rising`, `InRemains`, `DiscFlying`, `DiscReturning`, `DiscPosition`,
  counters `DiscsThrown`, `DiscHits`, `DiscsStopped`, `Grasps`, `FalseDeaths`, `RemainsRisen`, `RiseBurstHits`.
  The Hypogeum's reads need nothing new: `Active`, `CurrentState == Entering`, `Position.y`, `Phase`, `GuardWaves`,
  `Defeated`, `def.guardSpotsUsed` all exist. He never calls the Hypogeum.
- `HitPlayer` now returns whether the hit landed (same effect as before).

### `BossFight`
`Begin`: with `boss.entranceRise`, `controller.BeginRise(EnemyManager.arenaCenter)` and no gate opens; if that fails
(or without the flag) the gate entrance exactly as before. The camera beat turns to `controller.Position` either way.

### `BossSetup.EnsureEternalVictor()`
Menu `Gladiator/Setup/Bosses/Create Eternal Victor Data`; `EternalVictorEnemyPath =
"Assets/Data/Enemies/EN_EternalVictor.asset"`, `EternalVictorPath = "Assets/Data/Bosses/BD_EternalVictor.asset"`.
Created once with every number in R2-design.md § "The boss" → `BossSetup` (body 22,000 HP, radius 1.0, no block, no
knockback, model `EN_EternalVictor.fbx`; the words; attacks per phase; movement and stride 30 m / 7 m/s; circle,
lunges, grasp, falx, leap; guards; phases 0.65 / 0.3 at ×1.2 / ×1.4; no phase-2 extras; scatter; Rare; no gold; rise;
false death). Deviations: `shortName` and the two summary lines (decision 2). The guard is `EN_SkeletalLegionnaire`
loaded by its literal path; like the Emperor's Centurion, it's filled in on any later run if the list is empty.

## Decisions (one-line reasons)
1. **The HP floor is set at his spawn, not when phase 3 starts** (the design's moment): a lethal hit in phase 1 or 2
   would otherwise kill him outright and skip both his phase 3 and his remains; with the floor he roars through the
   phases at 1 HP and then lies as remains. The remains still wait for his last phase.
2. **`shortName` "Eternal Victor", the death and victory lines trimmed to 74 and 78 characters.** The orchestrator's
   rule (the summary row overflowed with "The Emperor's Champion"; "The Eternal Victor" is as long as the row that
   barely fits); the subtitle is 1,400 px at 36 px and the longest shipped line is 78 (the spec's were 82 and 103).
3. **The falx flies level at his hand's height (1.6 m above his ground) and hits a player 1.8 m below to 2 m above
   it.** "Can't be jumped" (a 1.7 m jump is still in reach) and it flies over a player on the floor while he stands on
   the 1.2 m stage; a player on the stage is hit by a throw from the floor.
4. **The falx keeps flying through a phase roar; his remains, a forced attack and his death snap it back.** It's a
   thrown object, not a pose; and the pooled view must never go back to the pool with the falx in world space.
5. **Its lane stays lit (full) while it's out.** The rule is "stay out until it's back": the player needs to see the
   lane after the wind-up's fill is gone.
6. **It spins about its mesh's middle and eases from his hand onto its path over 0.15 s.** About the grip it would orbit
   the flight point; snapping to the path would jump ~1 m.
7. **The Reaper's Circle with a falx turns his whole yaw 360° in the 0.3 s strike.** A torso twist of a full turn would
   unwind backwards afterwards (the pose lerps Euler angles); the yaw is a real angle and `Face` takes the short way.
8. **The roar as he rises from his remains has no extras** (`_quietRoar`): no guard wave on the spot, no second fence;
   the guard clock carries on as before.
9. **0.4 s of grace as the remains form** (the design's number): the same blow that felled him mustn't end the remains.
10. **The shove off the lift runs through the whole rise**, not only at its start: a player can walk back onto it.
11. **The false death waits for `LastPhase`** (3 with `phase3At > 0`, else 2): a two-phase boss with `falseDeath` can't
    sit at its floor forever.
12. **He faces the player as he rises** (the camera turns to him meanwhile); the salute is the existing roar pose with
    the falx raised.

## Tests (the orchestrator runs them; none has run yet)
**PlayMode `EternalVictorTests`** (the Colosseum scene; positions are the contract's shared facts):
| Test | Asserts |
|---|---|
| `HeRisesFromTheStageLift_InvulnerableUntilHeStands_ThenStridesWhenYoureFar` | Entering and `Rising` on the first frame; only him alive; the height at (0, 0) is the stage top 1.2 (±0.15); he starts at the centre ≥ 2.5 m below it, the model has its `Falx`; invulnerable; always rising, straight up; `Roaring` after ~`riseTime` standing on 1.2; invulnerable in the roar; with the player at (0, −60) he moves > 6 m/s and doesn't attack |
| `ReapersCircle_FillsItsRing_ThenCutsInsideIt_NotAJumpOrBeyondIt` | wind-up = `sweepWindup`; no hit halfway; one hit at 3 m; none with the feet 1.5 m up at the strike; none at 7 m |
| `VictorsLunges_TwoThrustsInPhaseOne_EachTelegraphed` | wind-up = `flurryWindup`; no hit halfway; 2 hits |
| `LeapOfTheVictor_TrailsYou_LocksThenLandsOnYou_OnceInPhaseOne` | wind-up = `pounceWindup`; no hit halfway; one leap, one hit, he's near the player |
| `FalxThrow_FliesOutAndBack_HitsYouInItsLaneOnceEachWay_MissesYouBesideIt` | lane along z = −28 from x = −12: wind-up = `discWindup`, nothing thrown halfway; the falx flies (he stays in `Strike`), goes the lane's whole 20 m, comes back; `DiscHits` 2, 2 damage events, no stop, no second throw in phase 1; a player 2.5 m beside the lane: no hit either way |
| `FalxThrow_TurnsBackEarlyOnAStatue_SoTheStatueIsCover` | the statue at (9.9, −9.9) is an obstacle; thrown north from (9.9, −26) at a player behind it at (9.9, −7): `DiscsStopped` 1, it never passes z −9.9, short of 17 m, no hit |
| `PhaseTwo_RiseBrothers_HisDeadComeUpTheTwoLiftsNearestYou_AndTheGraspHoldsYou` | his guard is `EN_SkeletalLegionnaire`; no wave in phase 1; at 65 % `phase2Text`, a wave as the roar ends with `guardsText`; 6–8 Legionnaires, each within 4 m of (34, −20) or (−34, −20), both used; the grasp's wind-up ÷ 1.2, no hit halfway, one hit, `Grasps` 1, `SlowMultiplier` 0.1, back to 1 after 1.3 s |
| `PhaseThree_TheArenaIsMine_WindupsOver1Point4_TwoLeapsAndTwoThrowsInARow` | at 30 % `phase3Text`, phase 3; sweep, falx and leap wind-ups ÷ 1.4; one forced throw → exactly 2 thrown; one forced leap → exactly 2 pounces |
| `TheFalseDeath_HisRemainsHold1500_DestroyThemAndHesGone_HisDeadDraggedOff` | a lethal hit in phase 3: `InRemains`, not `Defeated`, alive, `FalseDeaths` 1, HP 1,500, `remainsText`, the fight goes on; a hit in the first 0.4 s does nothing; he doesn't move; 1,510 more → `Defeated`, `Falling`, 0 enemies alive, never rose |
| `TheFalseDeath_LeftAlone_HeRisesWith20Percent_TheBurstThrowsYouBack_TheNextZeroIsFinal` | the player 2.8 m away, ×4 speed: still remains at 7.5 s and no damage; then `RemainsRisen` 1, HP 20 %, `riseText`, roaring, the guard clock didn't run, `RiseBurstHits` 1, the player thrown back; after the roar, still phase 3; a lethal hit → `Defeated`; `FalseDeaths` still 1 |
| `TheOlderBosses_StillWalkInThroughTheirGates_AndDieAtZero` | Gorm, the Champion, the Beast Lord, the Patron and the Emperor's Champion in their scenes: no rise or false death, start within 3 m of their gate and are within 10 m of it after the entrance, die (`Defeated`) to one lethal hit |

**EditMode `BossPhaseDataTests` additions:** `Volley` 9 / `Disc` 10, `State.Dead` 9 / `Remains` 10; a new `BossDef` has
no rise, false death or grasp, no phase-3 repeats, `discRepeats` (1, 1), `discPart` "Falx", no `Disc` listed; the five
older bosses' assets the same (none lists `Disc`); `BD_EternalVictor` (after the setup has run) matches the spec, the
summary words' lengths included (≤ 14 and ≤ 80).

Test helpers clear his dead with `Execute(slot, GameInfo.StableId("hypogeum"))` (a fall leaves no bone pile), so nothing
reassembles behind a test, without depending on the enemies part's `FallSourceId` name.

## Stubs (no stub file)
The boss code calls the enemies part's two names, each marked `// R2-STUB(enemies)` in `BossController.cs`:
`EnemyManager.SetHpFloor` (in `SpawnBody` and `EnterRemains`) and `EnemyManager.SetHp` (in `EnterRemains` and
`RiseFromRemains`). On this branch alone `Tools/ccheck` fails on exactly those 4 lines (Gladiator.Gameplay, errors=4).
**Checked against the enemies branch:** a throwaway detached merge of this branch with `origin/claude/r2-enemies-74db63`
(8707873) compiles with **errors=0, 0 warnings** on all six assemblies; its `SetHpFloor(int, float)` / `SetHp(int,
float)` match (floor 0 = off, reset at `Spawn`; `ApplyDamage`, ticks and blasts stop at the floor). No stub file to
delete. The markers can go once both are merged.

## Needs from others
- **Orchestrator:** merge the enemies part before (or with) this one; run `Gladiator/Setup/Bosses/Create Eternal Victor
  Data` after `EN_SkeletalLegionnaire` exists (or `ColosseumSetup.Build`); run the tests above plus the older bosses'
  (`BossTests`, `ChampionTests`, `BeastLordTests`, `GildedPatronTests`, `EmperorsChampionTests`) and
  `BossPhaseDataTests`. Look at the boss bar once: "THE ETERNAL VICTOR, FIRST CHAMPION OF THE COLOSSEUM" is 51
  characters in a 900 px box at 30 px (the Emperor's Champion's is 49); if it wraps, shorten the title (e.g. "FIRST
  CHAMPION") in the asset. Optional, `Hud.cs`: a phase-3 notch at `phase3At` if R1's suggestion wasn't taken.
- **Layout and setup:** `ColosseumSetup.Build()` calls `BossSetup.EnsureEternalVictor()` after `EnsureEnemies()` (so his
  guard link is filled); `guardSpots` = the 8 lifts, `bossGate` 0 (the fallback). The bot's boss line could add
  ` falx N (hit you H, stopped S) grasps G false deaths F (rose R, burst hit B)` from `DiscsThrown`, `DiscHits`,
  `DiscsStopped`, `Grasps`, `FalseDeaths`, `RemainsRisen`, `RiseBurstHits`; the bot should treat `DiscFlying` as "stay
  out of the lane" (the lane is `Position` along the aim; `DiscPosition` is where the falx is).
- **Hypogeum:** the centre lift's walkable box must be in the height grid at 1.2 (his ground at the centre is
  `Height.Sample(0, 0)`: if the bake misses the box and samples the shaft, he'd stand in the hole). His `Position.y`
  starts at 1.2 − 3 = −1.8 on his first frame, so the platform jumps to its bottom then (no visible sink); if a sink beat
  is wanted, the Hypogeum can play it before following him (the design says the lift "sinks and rises").

## Concerns (for the orchestrator's CONCERNS)
- **A phase roar mid-leap leaves any pouncing boss hanging in the air for the roar** (1.5 s), then he snaps to the
  ground (`Step` in Chase). Old behaviour (the Beast Lord, now the Victor); not changed here because the older bosses
  must stay as they are. Fix: in `BeginEnrage`, land a leaping boss at `_target` first. More likely with the Victor
  (two leaps in phase 3 and three thresholds).
- The falx's look (the spin plane, the release orientation) and every new pose's signs are unverified; the falx uses
  its rotation at release, spun about world up: if the blade is upright at release it reads as a spinning wheel, not a
  flat disc. Tune in Unity (or give it a fixed flat flight rotation) after a screenshot.

## Verified / unverified
- **Verified:** `Tools/ccheck` on this branch: errors=4, exactly the four `R2-STUB(enemies)` lines; with a temporary
  local stub of the two names (deleted, never committed): errors=0, 0 warnings on all six assemblies; merged with the
  enemies branch: errors=0, 0 warnings. Read-through of every path the five older bosses take: unchanged (the new code is
  behind `entranceRise`, `falseDeath`, `whipGrasp`, a `Falx` part, `BossAttack.Disc` or the `phase3…Repeats` fields,
  all off or absent for them; `Begin` and `HitPlayer` were refactored with the same effect).
- **Unverified (no Unity):** every test (none has run: the Colosseum scene, the Hypogeum, the Legionnaire and the
  Victor's model don't exist on this branch); the older bosses' tests still passing; every pose and its signs, the falx's
  flight look and its centre offset; the fight's feel and length (22,000 HP + 1,500 over three phases): needs bot runs
  and the producer's playtest.
