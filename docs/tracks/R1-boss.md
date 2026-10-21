# R1 boss — notes (the Emperor's Champion)

Branch `claude/affectionate-kalam-f0f393`, cut from `phase5-imperial-arena` (25ed4b0). Code only: no Unity, no Blender.
Owns `BossDef.cs`, `BossController.cs`, `BossSetup.cs`, plus the new `EmperorsChampionTests.cs` (PlayMode),
`BossPhaseDataTests.cs` (EditMode) and the throwaway `R1Stubs.cs`. Source: `docs/BOSSES.md` "The Emperor's Champion",
`docs/tracks/R1-design.md` § "The boss", `docs/tracks/R1-char-art.md` (his parts).

## What was built

### `BossDef`: new fields (the defaults keep every older boss as it is)
| Field | Header | Default | Emperor's Champion | Meaning |
|---|---|---|---|---|
| `closeInRange`, `closeInSpeed` | Movement | 0, 0 | 28, 6.5 | farther than this he strides at this speed and doesn't attack |
| `volleyJavelins` | Golden Volley | false | true | `FireStraight(arrow: true)` aimed at the player's height (`at.y = player.y + 0.9`) instead of coins |
| `guardsFromPhase` | Call the Guards | 1 | 2 | the guard clock runs from this phase; reaching it calls them as that roar ends |
| `guardsNearestGates` | Call the Guards | false | true | the `guardSpotsUsed` gates nearest the player (guard k at gate k % that) instead of gates 0, 1, 2… |
| `phase3At` | Phase 3 | 0 (none) | 0.3 | phase 3 below this HP fraction, after phase 2 |
| `phase3Attacks` | Phase 3 | empty (= phase 2's) | Whip, Bash, Volley, Charge | |
| `phase3Text` | Phase 3 | "" | `"FOR THE EMPEROR!"` | no announcement when empty |
| `phase3Speed` | Phase 3 | 1.4 | 1.4 | wind-ups and recovery ÷ this |
| `phase3MoveSpeed` | Phase 3 | 0 (= enraged) | 4.6 | |
| `phase3ChargeRepeats` | Phase 3 | 0 (= `chargeRepeats.y`) | 2 | |

Pair values (`volleyCount`, `volleyArc`, `bombCount`, `flurryThrusts`, `pounceRepeats`) use their phase-2 value in
phase 3 (design decision 20).

### `BossController`
- **Three phases.** `_phase` runs 1 → 2 → 3; `BeginEnrage(to)` starts a roar and the Enraging state remembers where it
  leads (`_enrageTo`). Phase 3 starts when `_phase == 2 && phase3At > 0 && Hp01 <= phase3At` and he isn't entering or
  roaring; a hit from above 60 % to below 30 % plays the phase-2 roar, then the phase-3 roar. Phase 2's arena extras
  (spike volley, cages, jets, blades on the roar; Gorm's fence as it ends) fire only on the way to phase 2.
- Every `_phase == 2` check is now `>= 2` (`Choose`, the repeat counts, the enraged move speed, the slam's fire,
  `BombCount`, `VolleyCount`, `VolleyArc`). `Speed` = `phase3Speed` in phase 3, `MoveSpeed` = `phase3MoveSpeed` (if
  set), `Choose` uses `phase3Attacks` (if set), the charge repeats `phase3ChargeRepeats` (if set). `Phase` returns 3
  (ChariotRace reads it).
- **Stride:** in Chase, beyond `closeInRange` he walks at `max(closeInSpeed, MoveSpeed)` and starts no attack.
- **Guards:** `GuardsClock` only while `_phase >= guardsFromPhase`; the Enraging into that phase calls `CallGuards()`
  as it ends. With `guardsNearestGates`, `NearestGates()` ranks the gates by distance to the player (a selection over
  ≤ 16, into a fixed `int[8]`: no allocation) and guard k goes through gate `k % use`, opened 3 s. The guards are just
  `EN_Centurion`: `EnemyManager.Spawn` brings his line (the formations part).
- **Javelins:** `volleyJavelins` fires arrows at the player's height and counts `JavelinsFired` (coins still count
  `CoinsFired`); no coin SFX.
- **Poses** (signs unverified in Unity): the Spear Thrust draws ArmR back and levels the `Spear` (+90° minus the arm's
  swing about its local X), then lunges with ArmR forward; the volley raises ArmR over the shoulder and flings it
  forward with the spear kept upright; his roars raise the spear to the imperial box; `WingL` / `WingR` beat (±22°
  about local Z) while he roars. Only a model with parts named `Spear`, `WingL`, `WingR` gets these (only
  `EN_EmperorsChampion` has them).
- New read-only properties: `JavelinsFired`, `Charges` (every charge started, chained ones too), `StateDuration` (the
  current state's length: a wind-up after the phase's speed; tests).

### `BossSetup.EnsureEmperorsChampion()`
Menu `Gladiator/Setup/Bosses/Create Emperor's Champion Data`; `EmperorsChampionEnemyPath =
"Assets/Data/Enemies/EN_EmperorsChampion.asset"`, `EmperorsChampionPath = "Assets/Data/Bosses/BD_EmperorsChampion.asset"`.
Created once with every number in R1-design.md § "The boss" → `BossSetup` (body 18,000 HP, radius 1.25, block 100° / 50 %,
model `EN_EmperorsChampion.fbx`; the words; attacks; movement; Spear Thrust, Shield Bash, Javelins, Shield Charge;
guards; phases; no phase-2 extras; `scatterOnDefeat`; Rare floor; no gold). No deviation from that list. The guard is
`EN_Centurion` loaded by its literal path; like the Patron's mercenary, it's filled in on any later run if the list is
empty, so running the setup again after the layout part made the Centurion repairs it.

## Decisions (one-line reasons)
1. **The first formations come as the phase-2 roar ends** (R1-design.md's rule; BOSSES.md line 7 says "on the roar"):
   at its start "THE PRAETORIAN GUARD!" would replace "PRAETORIANS, TO ME!" on the announcer the same frame.
2. **Beyond `closeInRange` he starts no attack** (the design only said he strides): his phase-1 list has no any-range
   attack, so `Choose` fell back to the first listed one and he'd have thrust his spear at a player 60 m away every
   0.45 s instead of walking.
3. **`Choose`'s last fallback is `None` (keep closing in), not "the first listed".** No older boss reaches it (each
   lists a charge, pounce, bombs or slam, which come first), so their picks and random draws are unchanged.
4. **Fixed: a phase roar could end on the frame it started.** `Tick` computed the state's progress `u` before the
   phase check and the Enraging case reused it, so a hit landing late in a state (most of a chase) skipped the 1.5 s
   invulnerable roar. This affects the four older bosses too (their roar now always lasts `enrageRoar`); their tests
   hit at the start of a chase, so they don't see it. Bot fight times may grow by up to 1.5 s.
5. **The thrust doesn't call `PlayerMotor.Slow` when `whipSlow >= 1` or `whipSlowSeconds <= 0`** (it was a no-op at
   best); Gorm's and the Beast Lord's whips are unchanged.
6. **The boss doesn't call `ChariotRace`** (design decision 21): the races read `BossController.Active.Phase` and
   `.Defeated`, so `BossController` compiles and works without that part.

## Tests (the orchestrator runs them; none has run yet)
**PlayMode `EmperorsChampionTests`** (the Imperial Arena scene; positions are the contract's shared facts: track at
z = ±14 ± 2, spina along z = 0 from x = −60 to 40, obelisk at x = −10):
| Test | Asserts |
|---|---|
| `HeEntersThroughTheTriumphalGate_StridesWhenYoureFar_AndHisRoarLastsEvenMidStride` | only him alive; BossFight gate 0 at x > 85, he's at x > 80; phase 1, shield up; the model has `Spear`, `Shield`, `Javelins`, `Eagle`, `WingL`, `WingR`; with the player ~65 m away he moves > 6 m/s for 1 s, stays in Chase, no attack; a hit past 60 % mid-stride → Enraging next frame, `phase2Text` announced, invulnerable, still roaring 0.9 s later (decision 4) |
| `SpearThrust_FillsItsLane_ThenLungesDownIt_NotBesideIt` | wind-up = `whipWindup`; no hit halfway; one hit after; no slow; a player 2.5 m beside the lane isn't hit |
| `ShieldBash_FillsItsCone_ThenPunchesThePlayerAway` | wind-up = `bashWindup`; no hit halfway; one hit; the player ends > 3.6 m away (knockback) |
| `JavelinVolley_GlowsFirst_ThenFliesAtYourHeight_ReachingYouOnTheSpina` | player on the 1.2 m spina, he's on the floor 8 m south; wind-up = `volleyWindup`; no javelin halfway; then 5 javelins, 0 coins, and the player is hit |
| `PhaseTwo_TwoFormationsMarchInThroughTheTwoGatesNearestYou_AndAgainOnTheClock` | his guard is `EN_Centurion`; 28 s of phase 1 at ×4 speed: no wave; at 60 %: Enraging with `phase2Text`, no wave during the roar, one as it ends with `guardsText`; 2 Centurions and 10 Praetorians alive; each Centurion within 5 m of one of the two gates nearest the player, one at each; `FormedCount` = 5 for each (formations API); both gates open; a second wave within `guardsEvery` |
| `PhaseTwo_TheShieldCharge_StunsHimOnTheObelisk_GuardDown_TakesHalfAsMuchAgain` | wind-up = `chargeWindup / 1.2`; charging west on the spina into the obelisk: Stunned, 1 stun, stopped at x ∈ [−9.5, −3], shield down, a frontal hit takes ×1.5; after the stun, shield up and the frontal hit takes 50 % |
| `PhaseThree_ForTheEmperor_WindupsOver1Point4_AndTheChargeComesTwice` | at 30 %: Enraging with `phase3Text`, invulnerable, then phase 3; no guard wave on that roar; thrust, bash and charge wind-ups ÷ 1.4; one forced charge → Strike, then a re-aimed Windup (Charge), then exactly 2 charges, no third |
| `PhaseThree_TheChariotRacesRunBackToBack` | `ChariotRace.Instance` exists; within 2.5 s of phase 3 a race telegraphs or runs; at ×4 it runs out and the next starts within 3 s; he's alive, phase 3 |
| `AtHisFall_TheRacesHalt_AndTheGuardStandsDown` | formations on the sand (> 11 alive); in phase 3 the chariots run; killing him → Defeated, BossFight Falling, 0 enemies alive; 0.8 s (real) later no race runs or telegraphs |
| `Gorm_HasNoPhaseThree_RoarsOnce_AndStillLightsTheFence` | (Slave Pit) `phase3At` 0, `guardsFromPhase` 1; 100 % → 10 % in one hit: one roar with "BURN IT!", phase 2, the fence ring (> 30 zones); 1.5 s later still phase 2, no second roar or announcement |

**EditMode `BossPhaseDataTests`:** a new `BossDef` has no phase 3, guards from phase 1, gates in order, coins, no
stride; `BD_Gorm`, `BD_Champion`, `BD_BeastLord`, `BD_GildedPatron` load with `phase3At` 0, `guardsFromPhase` 1 and
the new flags off; `BD_EmperorsChampion` (after the setup has run) matches the spec: body, attacks per phase,
thresholds, speeds, charge repeats, javelins, stride, guards (`centurion`), no phase-2 extras, scatter, Rare.

## Stubs to delete at merge
**`Assets/Scripts/Gameplay/Boss/R1Stubs.cs`** (marked at the top). It defines:
- `ChariotRace : MonoBehaviour` with `Instance`, `Running`, `Telegraphing`, `BackToBack`, `Races`, `BossHits`,
  `StartNow()`, `Halt()` (the chariot part's names from R1-design.md);
- `EnemyManager.FormedCount(int leader)` (the formations part's name).

Only the tests use them: without the file, ccheck fails on exactly three lines, `EmperorsChampionTests.cs` 323
(`FormedCount`), 415 and 441 (`ChariotRace`). The boss code compiles without either part. Once the real ones merge,
the stub file is a duplicate definition: delete it (no `.meta` was written for it, the Editor makes one).

## Needs from others
- **Orchestrator:** delete `R1Stubs.cs` when the chariot and formations parts are in; run
  `Gladiator/Setup/Bosses/Create Emperor's Champion Data` after `EN_Centurion` exists (or `ImperialArenaSetup.Build`);
  run the tests above and the four older bosses' (`BossTests`, `ChampionTests`, `BeastLordTests`, `GildedPatronTests`).
  Optional, `Hud.cs` (no part owns it): a phase-3 notch on the boss bar next to the phase-2 one at line 360
  (`760f * b.def.phase3At` when `phase3At > 0`).
- **Layout and setup:** `ImperialArenaSetup.Build()` calls `BossSetup.EnsureEmperorsChampion()` after `EnsureEnemies()`
  (so his guard link is filled), with `bossGate 0` = the triumphal gate; the bot's boss line could add
  `JavelinsFired` and `Charges` (it owns `PlaytestBot.cs`).
- **Chariot races:** `ChariotRace.Instance`, `Running`, `Telegraphing` readable (property or field), back to back while
  `BossController.Active.Phase >= 3`, halted on `.Defeated` (as designed).
- **Formations:** a public `EnemyManager.FormedCount(int leader)` (as designed).

## Verified / unverified
- **Verified:** `Tools/ccheck` errors=0 on all six assemblies (with the stubs); without the stubs only the three test
  lines above fail. Read-through of every path the four older bosses take: unchanged except decision 4 (and 3, which
  they never reach).
- **Unverified (no Unity):** every test (none has run: the scene, `EN_Centurion`, `ChariotRace` and the formations
  don't exist on this branch); the older bosses' tests still passing; the spear and wing poses and their signs; the
  fight's feel and length (18,000 HP over three phases, the stride, javelins at 22 m/s): needs bot runs and the
  producer's playtest.
