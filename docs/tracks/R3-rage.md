# R3 rage — notes (Asterion's Gore and Rage)

Branch `claude/adoring-ellis-b1686a`, fast-forwarded to `origin/phase5-asterion` at 2823019. Code only: no Unity, no
Blender. Built to `docs/tracks/R3-design.md` § "For each code part" → "Rage" and decisions 14–23, with the
orchestrator's answers Q1–Q11 (`docs/tracks/R3-asterion.md`).

## What I built
| File | What |
|---|---|
| `Assets/Scripts/Gameplay/Gladiators/GoreTrait.cs` (new) | `GoreTrait` (menu `Gladiator/Traits/Gore`, `TR_Gore`) + `Gore : MonoBehaviour, ITickable` (`Instance`, `TickGroup.Player` 60, `SourceId = StableId("gore")`). Every field of the design's table with its default. Public `Gored`, `GoreKills`, `Goring`. |
| `Assets/Scripts/Gameplay/Gladiators/RageTrait.cs` (new) | `RageTrait` (menu `Gladiator/Traits/Rage`, `TR_Rage`) + `Rage : MonoBehaviour, ITickable` (`Instance`, `TickGroup.Player` 59, `BellowSourceId = StableId("bellow")`, `enum Mode : byte { Calm, Unchained, Spent }`). Every field of the design's table with its default. State: `Value`, `Fill01`, `Full`, `Current`, `IsUnchained`, `IsSpent`, `WaitingForSlide`, `UnchainedLeft`, `Unchained01`, `SpentLeft`, `Pressing`, `PressRate`, `Cornered`; counters `Unchains`, `BellowHits`, `PeakPress`, `UnchainedTime`; `Fill()`, `SetRage(float)`, `bool Unchain()`, `Halt()`. |
| `Assets/Scripts/UI/Hud.cs` | `DrawRage(Rage, float)` on the Blood bar, chosen in `DrawBlood` after the Golden Idol's (one gladiator at a time). Colours `RageColor` / `RageHot` / `RageSpent` as designed; fill calm `Fill01` (×3), Unchained `Unchained01` (×6), Spent 0 (×6); calm bronze pulsing to hot at 8 Hz when Cornered, above 85 % or waiting for a slide; Unchained white-gold pulsing at 14 Hz; Spent grey. Labels (constant strings, own `_bloodKey` values −20…−24): "CORNERED", "UNCHAINED", "SPENT", "SLIDE!", else empty. `public string BloodLabel` (tests). |
| `Assets/Scripts/Gameplay/Dev/PlaytestBot.cs` | After the wolves: ` sweeps S (whirls W, hits H, stone T)` when `Sweeps > 0`; ` unchained U (Xs, bellow B) peakPress P` plus ` damageTaken D` (Q10) when a `Rage` exists; ` gored G (kills K)` when a `Gore` exists. |
| `Assets/Tests/PlayMode/RageTests.cs` (new) | 15 PlayMode tests (below). |

## How it works (the short version)
- **Gore** (tick): nothing while dead, `WeaponSystem.Halted` or paused. Calm or Unchained numbers (from `Rage.Instance`).
  Below `minSpeed` (from `PlayerMotor.HorizontalVelocity`): nothing. `QueryAlive(pos + heading × ahead, capsule + reach +
  ahead)`, then within capsule + reach + the enemy's radius of his centre (XZ), within `halfAngle` of the heading (180° = all round, no float
  edge), within `maxHeight` vertically, not gored within `interval` (per-slot `float[] _next` + `int[] _uid`, allocated
  once at `em.capacity`). Push = the sideways part of `to` (dead ahead: the uid's low bit picks the side) +
  heading × `forward`, normalised; a Scripted enemy gets `to / dist` and no knockback. `ApplyDamage(Melee, damage ×
  Damage, push, knockback × Knockback, SourceId)`, a 3-speck `puff` at the contact point, `SfxId.Hit` at most every 0.1 s.
- **Rage** (tick): while dead or `RunState.CombatOver` → `Halt()` (before the paused check, so it also runs at the end
  screen). **Calm:** the press (`QueryAlive(pos, pressRadius)`, elites and Scripted count `pressHeavy`), `PressRate =
  min(Pressing × pressPerEnemy, pressMaxPerSecond)`, `Cornered` when the raw rate reaches the cap; `+perHit` on a
  non-status `DamagedEvent` (calm only); `_quiet` resets on press or hit, after `calmDelay` it cools; snorts from
  `breathFrom` at the `Head` part (found once under the animator's `visualRoot`) + 0.3 m forward; full → `Unchain()`
  (with `triggerOnSlide`, on the next `SlideEvent`). **`Unchain()`:** the two StatMods (Multiply), saves and zeroes
  `KnockbackTakenMul`, `WhirlSweeps = true`, `ScaleBoost` / `ExtraLean`, the bellow (`DamageFlags.Area`,
  `BellowSourceId`, radius × √Area, two `Shockwave`s, 30 specks), "UNCHAINED!" (`AnnounceKind.Line`, `hot`, 1.4 s),
  `BossRoar`, shake 0.3, crowd spike 0.4. **Unchained:** the bar drains (`Value = meterMax × Unchained01`), specks every
  `speckEvery`, `UnchainedTime`. **Spent:** mods/knockback/whirl/scale/lean undone, MoveSpeed × `spentMoveSpeed`,
  `HeadDrop`, no fill; then calm with the clocks reset. **`Halt()`:** everything undone, calm, meter empty; idempotent.
- Zero GC per frame: fixed arrays, struct mods/sources/events, constant strings; the only allocations are the Gore
  arrays (once per run) and the `Head` lookup (once, recursive `GetChild`, no enumerator).

## Decisions (one-line reasons)
1. **`Unchain()` works from calm whatever the meter** (the tick calls it when full): admin and tests can force a rampage;
   it refuses while Unchained/Spent or once the player is down.
2. **The StatMods carry `StatSource(Trait, TR_Rage)`**: the P1 stats page names them "Rage" in the trait colour instead
   of the anonymous "Traits & effects" the older traits get.
3. **Hits only fill while calm**; Unchained the bar is the timer, Spent fills nothing (the design's "no fill").
4. **`SetRage` restarts the cooling clock** (like `Stormcaller.SetCharge`): tests and admin get a deterministic 2.5 s.
5. **`Fill()` / `SetRage()` do nothing unless calm**: they can't corrupt the Unchained timer or the Spent beat.
6. **Gore: `maxHeight` 1.2 m (new field, not in the design)**: the horns shouldn't hit the crowd below a ledge or under a
   jump; one field, the default leaves flat ground exactly as designed.
7. **Gore's query is centred `ahead` (0.35 m) along his velocity, as designed, and the hit test is from his centre**
   (within capsule + reach + the enemy's radius): the query circle (reach + ahead) holds that whole disc, so the result
   is exact either way.
8. **The bellow pushes bosses like everyone else** (as the Verdict does): their `knockbackTaken` already decides it.
9. **The bot's `damageTaken`** is printed with the Rage line (unless the Machine's plate already printed it): Q10 wants
   it to tune his HP and the rates.
10. **`pressHeavy` is a float (4) rounded per enemy**, `Pressing` stays an int: the setup part can assign `4` or `4f`.
11. **The bar's label keys are −20…−24**: the Blood bar's other gladiators use 0+, −1, −2, −3, and the key is shared.

## Tests (`RageTests`, PlayMode; written, NOT run: no Unity here)
A stand-in Asterion made in the test (`GladiatorDef` with MaxHp 130 / MoveSpeed 0.95, the Sweep `WeaponDef` with the
chain's numbers, `GoreTrait`, `RageTrait` with defaults) through `GladiatorLoadout.NextOverride`, on the Nobody's
model (no `model` set), in the test arena with no Director, no `LevelUpController`, no obstacles, no weapon (except the
whirl test). Enemies: copies of the Convict held in place (`moveSpeed 0`, no contact damage, 1000 HP; `knockbackTaken`
1 or 0 per test). The ground round the spawn is checked flat (else Inconclusive).
1. `ThePress_FillsTheMeter_…` — 4 enemies at 2.6 m: Pressing 4, rate 4, not Cornered, ≈ 8 in 2 s; one at 6.5 m doesn't count; HUD label empty.
2. `ThePress_IsCappedAt5ASecond_Cornered` — 8 at 2.4 m: rate 5, Cornered, ≈ 10 in 2 s; HUD "CORNERED".
3. `AnElite_OrABoss_CountsFour` — an `AF_Armored` elite: Pressing 4; a Scripted one: 4.
4. `AHitTaken_Adds3_AStatusTick_Nothing` — two hits: 3, 6; a Burn tick: still 6.
5. `WithNobodyNearAndNoHit_ItCools_…` — 50 stays for 2.2 s, starts cooling at 2.5 ± 0.2 s, at 8 ± 0.6 /s.
6. `AFullMeter_UnchainsHimAtOnce_…` — Fill → Unchained next frame; "UNCHAINED!"; the bellow takes 8 × Damage off the
   enemy at 3 m (and throws it > 0.5 m further out), not the one at 6 m; 2 shockwaves; AttackSpeed ×1.3, MoveSpeed ×1.2;
   `KnockbackTakenMul` 0; `WhirlSweeps`; `ScaleBoost` 1.1, `ExtraLean` 14; HUD "UNCHAINED"; the bar drains; 8 ± 0.25 s
   later Spent: MoveSpeed ×0.75, attack speed, knockback, whirl and poses back, `HeadDrop` 30, meter 0 and still 0 with
   6 enemies pressing; HUD "SPENT"; then calm 2 s after Spent began with every stat at base, and the press fills again.
7. `WithTriggerOnSlide_…` — full waits (`WaitingForSlide`, HUD "SLIDE!"); a `SlideEvent` Unchains.
8. `DyingWhileUnchained_PutsEveryStatBack` — `Kill()` → calm, meter 0, stats/knockback/whirl/poses back; `Unchain()`
   refused and `Fill()` undone while down.
9. `TheRunEndingWhileSpent_PutsEveryStatBack` — `RunState.End` while Spent → calm, MoveSpeed and `HeadDrop` back.
10. `Unchained_TheChainWhirlsAllRound_…` (needs the chain part) — calm, the sweep hits the enemy it aims at and not one
    4.6 m straight behind; Unchained (the bellow can't reach 4.6 m), `Whirls` rises and the one behind is hit.
11. `Running_IntoAnEnemyAhead_GoresIt_…` — at full speed into one dead ahead (0.25 m right): gored once, 8 × Damage,
    0.4 s later > 0.6 m to the right and more sideways than along his path.
12. `NotOneBesideHim_NorAtAWalk` — one appearing 1 m beside him at full speed isn't gored; walking (2 m/s, top speed
    checked < 4) straight through one doesn't gore it.
13. `TheSameEnemy_IsntGoredTwice_WithinASecond` — an unmovable one: gored on the way through, not on the way straight
    back (< 1 s), gored again on a run 1.2 s later.
14. `Unchained_HeGoresFromEverySide_AtAJog` — Unchained at 3 m/s (under calm's 4): one right behind him is gored for
    14 × Damage.
15. `ABoss_IsGored_ButNotMoved` — a Scripted one: 8 × Damage, moved < 0.05 m.

**Deviation from the design's test list:** "moved more than 1 m sideways within 0.3 s" became "> 0.6 m within 0.4 s":
knockback 10 with the feedback's decay 9 travels 1.11 m in total, ~1.0 m sideways by 0.3 s, too tight for a test. No
GC test for the traits (the Editor's "GC Allocated In Frame" includes Editor noise; the chain part's GC test runs with
the traits off): zero GC is by construction, the orchestrator's bot run measures it.

## Verified / unverified
- **Verified:** `Tools/ccheck` fails only on the chain part's 13 names in Gameplay (below). With those names stubbed
  locally in `WeaponSystem.cs`, `PlayerPartAnimator.cs` and `WeaponDef.cs` (not committed, reverted), **all six
  assemblies compile: errors=0, 0 warnings** (Core, Gameplay, UI, Editor, EditMode, PlayMode tests).
- **Unverified:** every test (not run); every feel number (rates, toss strength, bellow, Spent's cost, uptime); the HUD's
  look; the poses (the chain part's hooks); behaviour with 300 enemies (perf: one 512-slot press query + one Gore query
  per frame, like the Verdict's).

## `// R3-STUB(chain)` — calls to the chain part's names (ccheck fails only on these until it merges)
- `RageTrait.cs`: `WeaponSystem.WhirlSweeps` (set in `Unchain`, cleared in `ReleaseUnchained`);
  `PlayerPartAnimator.ScaleBoost`, `ExtraLean` (`Unchain`, `ReleaseUnchained`), `HeadDrop` (`EnterSpent`,
  `ReleaseSpent`). 9 lines.
- `PlaytestBot.cs`: `WeaponSystem.Sweeps`, `Whirls`, `SweepHits`, `WeightHits` (one line, marked).
- `RageTests.cs`: `WeaponPattern.Sweep`, `WeaponSystem.WhirlSweeps` / `Sweeps` / `Whirls`,
  `PlayerPartAnimator.ScaleBoost` / `ExtraLean` / `HeadDrop` (21 marked lines).
Expected signatures (the contract's): `public bool WhirlSweeps { get; set; }`; `public int Sweeps, Whirls, SweepHits,
WeightHits { get; }`; `public float ScaleBoost { get; set; } = 1`, `ExtraLean`, `HeadDrop` on `PlayerPartAnimator`.

## Needs from others
- **chain:** the names above, as the contract gives them. `PlayerPartAnimator.ScaleBoost` must default to 1 (Rage sets
  it back to 1, never reads it).
- **orchestrator:** after the chain merges, merge this branch and run `Tools/ccheck` (it should be clean); run
  `RageTests` (PlayMode).
- **setup:** nothing new: `TR_Gore` / `TR_Rage` come from `CreateInstance` defaults (the new `GoreTrait.maxHeight`
  defaults to 1.2).
