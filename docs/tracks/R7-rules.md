# R7-A rules: notes (Edicts: run modifiers in the game; Q14: runs no longer repeat)

Worker for part **R7-A rules** of `docs/tracks/R7-contract.md`, branch `claude/gracious-archimedes-5b1319` (from
`origin/phase5-run-modifiers` at 9836f50). Code and tests only: no Unity, no Blender. `Tools/ccheck`: **errors=0** (all
six assemblies, 0 warnings). Tests **written, not run** (the orchestrator runs them in Unity).

## Files
| File | What changed |
|---|---|
| `Assets/Scripts/Gameplay/Run/RunModifierDef.cs` (new) | The `Gladiator/Edict` ScriptableObject (`ED_`), fields exactly as the contract names them, each with a tooltip. Every effect defaults to "no change". |
| `Assets/Scripts/Gameplay/Run/RunModifiers.cs` (new) | Static: `Totals` (+ `None`), pure `Combine` / `Bonus`, `Active` (never null), `RenownBonus`, `Current`, `AppliedTo`, `Set`, `Clear`, `ActiveIds`, `ApplyTo(RunState)`; `SubsystemRegistration` resets and subscribes once to `sceneLoaded`. |
| `Assets/Scripts/Gameplay/Enemies/EnemyManager.cs` | `scriptedHpMul` (replaces the `1f` for Scripted enemies in `Spawn`'s HP line), `speedMul` (× `_speed[i]` at spawn), `GetSpeed(i)` (a query for tests). |
| `Assets/Scripts/Gameplay/Director/Director.cs` | `clockScale`, `PaceClock` (uncapped), `Clock = clockScale > 1 ? min(PaceClock, run length) : PaceClock`; the swarm/elite schedules, the "clock jumped" check and `TriggerSwarmNow/EliteNow` run on `PaceClock`; the curves, `TargetAlive`, spawns per second, swarm size and `Pick` read `Clock`. `NextSwarmAt` / `NextEliteAt` documented as PaceClock seconds. |
| `Assets/Scripts/Gameplay/Interactables/Interactables.cs` | `priceMul`; `ChestPrice = RoundToInt(PriceFor(opened) × priceMul)`. **Q14:** `_rng` seeded in `Awake` from `GameRandom.NextUInt() ^ 0x51ED27` (never 0). |
| `Assets/Scripts/Gameplay/Crowd/Favor.cs` | `gainMul` multiplies `Add`'s positive amount (so every source: kills, streaks, close calls, no-hit, whims' bounty). |
| `Assets/Scripts/Gameplay/Crowd/ArenaMaster.cs` | `boredomMul` × `config.boredomRise` (the fall is unchanged). **Q14:** `_rng` from `GameRandom.NextUInt() ^ 0xB0BA5` in `Awake`. |
| `Assets/Scripts/Gameplay/Crowd/CrowdGifts.cs` | **Q14:** `_rng` from `GameRandom.NextUInt() ^ 0x6A11F7` in `Awake`. |
| `Assets/Scripts/Gameplay/Run/LevelUpController.cs` | `levelUpCardDelta`; in `Open`, for `PickSource.LevelUp` only: `n = Clamp(n + delta, 1, 4)` (rerolls and banishes keep that count). |
| `Assets/Scripts/Gameplay/Combat/PlayerHealth.cs` | `HealingScale` (default 1) × regen, `Heal` and `HealUnscaled` (through a private `Restore`); never `OnMaxHpChanged`'s growth. |
| `Assets/Scripts/Core/Stats/StatBlock.cs` | `StatSourceKind.Edict` appended after `Admin`. |
| `Assets/Scripts/Gameplay/Stats/StatText.cs` | `SourceName`: `"Edict: " + displayName`; `SourceColor`: `EdictCol` `#FF5A3C` (orange-red: it hurts you). |
| `Assets/Scripts/Gameplay/Dev/PlaytestBot.cs` | Build summary line ends with ` edicts N (+X %)` when any are active. |
| `Assets/Scripts/Gameplay/Run/RunDef.cs` (`GameSession` seeding only) | `StartRun` and `Retry` call `SeedRun()` right before the load: `GameRandom.Seed(NextRunSeed())` (Stopwatch ticks + a counter through a 32-bit lowbias hash, never 0), skipped under `-bot` / `-stress`. |
| Tests | new `Assets/Tests/EditMode/EdictMathTests.cs`, `Assets/Tests/PlayMode/EdictRunTests.cs`, `Assets/Tests/PlayMode/RunVarietyTests.cs`; one line in `Assets/Tests/PlayMode/InteractableTests.cs` (below). |

## How it works
- The menu (R7-C) calls `RunModifiers.Set(defs)` before `GameSession.StartRun`; Retry keeps them (static, like `GameSession`).
- On every `sceneLoaded` (after every `Awake`, before any `Start` or tick), `ApplyTo(RunState.Current)` runs. It returns
  false with no run (the menu), no edicts, or the run it already applied to (an additive load). Otherwise it multiplies
  `Current` into that scene's hooks, adds each def's `playerStats` with `StatSource(Edict, def)`, then
  `PlayerHealth.RescaleFrom(oldMax)` so HP starts full at the new max, and logs `[Edicts] applied N (+X %)`.
- Each hook is a field of a scene object that starts at 1 (or 0) in every fresh scene, and the arena's own values
  (`hpMul`, `densityMultiplier`, `eliteEveryOverride`…) are multiplied, not overwritten. A reload (Retry) builds new
  objects, so nothing compounds.
- The Director's pools are sized on its first tick (`ComputePoolTargets`), after `ApplyTo`, so they include the Hungry
  Horde's density and swarm size.
- **Q14:** each run started from the menu (or retried) seeds `GameRandom`; the three systems that used constant seeds
  now draw theirs from it in `Awake`, so chests and the altar, gifts and whims change from run to run. A test that seeds
  `GameRandom` before a load gets the same placement every time.

## Decisions (one-line reasons)
1. **`Set` also drops a second def with the same non-empty id** (as well as the same asset twice): ids are the save's
   keys (`edictsChosen`, `RunSummary.Edicts`), so two defs with one id would be ambiguous.
2. **`ApplyTo` falls back to `FindAnyObjectByType(Include inactive)` when an `Instance` is null:** a component whose object
   starts inactive hasn't set `Instance` yet; this only runs once per load (it may allocate there).
3. **`HealFull` (the admin's Heal, one test) ignores `HealingScale`:** it's a dev tool; F1 → Heal should still work
   under No Mercy. Every gameplay heal (`Heal`, `HealUnscaled`, regen) is scaled. Not in the contract: flagging it.
4. **`Clock` is `PaceClock` for `clockScale ≤ 1`, capped only above 1** (the contract's wording); at exactly 1 it is the
   run clock, bit for bit.
5. **Bot and stress runs keep the fixed per-launch sequence** (`SeedRun` returns under `-bot` / `-stress`). The contract
   assumed bot runs load an arena directly, but `PlaytestBotBootstrap` and `MainMenu.Launch` start them through
   `GameSession.StartRun`; without this they would roll differently each launch and perf runs would stop being
   comparable. R8 can drop it when `-runSeed` exists.
6. **Favor's `gainMul` is applied inside `Add`** (after the "positive amount" check), so every Favor source, the whims'
   bounty included, is halved by A Fickle Crowd.
7. **`EnemyManager.GetSpeed(i)`** added (a read-only query, like `GetMaxHp`) so the tests can see Swift Foes' number.
8. **`ApplyTo` reads `EliteEvery` from `config.eliteEvery` when the arena has no override**, and leaves the override alone
   when neither exists (no config): nothing to halve.

## Tests (written, not run)
- **EditMode `EdictMathTests`** (9): `Totals.None` and no edicts change nothing; `Combine` {1.5, 1.2} = 1.8, deltas −1 + −1
  = −2, nulls skipped, every field reaches its total; `Bonus` {0.25, 0.2} = 0.45, negative shares count 0, all eleven
  = 2.35 (×3.35); `Set` drops nulls, the same asset twice and a duplicate id, keeps order, recomputes `Current` / bonus /
  `ActiveIds`; `Set` copies; `Clear`; `ApplyTo(null)` and no-edict `ApplyTo` are false; `StatSourceKind.Edict` is
  `Admin + 1` and `SourceName` reads "Edict: Glass Jaw".
- **PlayMode `EdictRunTests`** (8, the test arena, the run clock pinned at 60 s with Timeless; defs made with the spec's
  numbers; each run measured against a cached no-edict measurement): no edicts → every hook 1 and today's formulas (and a
  second load equals the first); Iron Hides + Swift Foes + Hungry Horde → a Convict ×1.5 HP, a Scripted dummy ×1.25,
  speed ×1.15, density and swarm ×1.3, `TargetAlive` ×1.3 below the cap; Blood Sport + Glass Jaw + No Mercy → damage ×1.4,
  max HP ×0.6 and full, `Heal` / `HealUnscaled` / regen add 0, the stats page's "Edict: Glass Jaw"; Empty Purses + Fickle
  Crowd + Meagre Choices → chest price ×1.5, a coin's gold ×0.5, `Add(10)` = 5 Favor, boredom rise ×2, a level-up one card
  fewer, a chest's reward unchanged, `RerollsLeft` 0; Impatient Emperor → `PaceClock` 7.98 at 6 s, `Clock` capped at the run
  length at 90 % of the run (and `TargetAlive` reads the curve there), a swarm still starts after the cap and the next is
  scheduled on `PaceClock`; Champions' Day → the next elite in half the interval; Retry → a fresh run with the edicts
  applied once (no ×2.25); all eleven → every hook at its combined number, 200 Convicts for 5 s, `GameLoop.HeapReport()`
  logged (not asserted).
- **PlayMode `RunVarietyTests`** (2, the test arena: no chest anchors, random spots): two runs through
  `GameSession.StartRun` → at most one of the two starting chests and the altar in the same spot, and the first three
  gifts (kind + landing spot) or the first three whims differ; two loads after `GameRandom.Seed(1234)` → the same chests,
  altar, gifts and whims. The player is put at the arena's centre at full HP before the gifts (their spot and odds depend
  on it); whims run at a pinned 200 s with a quiet Director.
- **`InteractablePlayTests.Boot`** now seeds `GameRandom` (5150) before its load: `Chests_AppearOnClearFlatGround…`
  counts the placed chests, which relied on the old constant placement (the contract's Q14 bullet asks for this). The
  other users (`SlavePitTests` anchors, `FavorPlayTests`) don't depend on the order: unchanged.

## Verified / unverified
- **Verified:** `Tools/ccheck` errors=0, 0 warnings, all six assemblies.
- **Unverified (needs Unity):** every test above; that `sceneLoaded` fires after the arena's `Awake`s and before the
  Director's first tick in a real load (Unity documents it; the tests check it); the Editor heap log; the bot summary
  line; that `GameRandom` draws nothing between `Seed` and the new scene's `Awake`s when a sync `LoadScene` is called
  after the frame's gameplay tick (the same-seed test relies on it; I found no `LateUpdate` user of `GameRandom`).

## Needs from others / for the orchestrator
- **None blocking.** R7-B and R7-C can call `RunModifiers.Set / Active / RenownBonus / ActiveIds` as the contract names
  them; nothing in this part is a stub.
- **Known limits (CONCERNS candidates):** the Eternal Victor's remains HP (`BossController` sets it absolute, `SetHp(…,
  def.remainsHp)`) isn't scaled by Iron Hides' boss multiplier; the rise HP is (a fraction of max). Bot runs (`-bot`)
  still repeat the chests/gifts/whims per launch by design (decision 5).
- `.meta` files for the two new scripts and three new tests come from the Editor on import (not hand-written).
