# R8-B daily core — notes (worker, 2026-10-22)

Branch `claude/jolly-kalam-811c2a` (from `origin/phase5-daily` at 526843e). Contract: `docs/tracks/R7-contract.md`
"R8-B daily core"; spec `docs/tracks/R7-design.md` §3.5, §3.6, §5. Code and tests only: no Unity, no Blender.
`Tools/ccheck`: **errors=14, every one an R8-A name** (`GameRandom.Hash / BeginRun / RunSeed / SeedText / TryParseSeed`);
with a temporary stub of those five members in `DamageInfo.cs` (never committed) **all six assemblies compile: errors=0, 0
warnings**. Tests **written, not run**.

## Files (only the ones the contract gives R8-B)
| File | What changed |
|---|---|
| `Assets/Scripts/Gameplay/Run/RunDef.cs` (`GameSession`) | `Seed`, `Daily`, `HasDaily`, `DailyScored`, `SuppressLoad`, `LastLoadPath`, `FreshSeed()`, `StartRun(g, run)` / `StartRun(g, run, seed)`, `StartDaily(in plan, scored)`, `Retry()`, `RetrySameSeed()`; extras `RunSeedOverride` (the test hook) and `TryCommandLineSeed(out seed)`; a `BeforeSceneLoad` hook that honours `-runSeed` in a build that boots straight into an arena. R7-A's `SeedRun` / `NextRunSeed` (and their `-bot` / `-stress` skip) are gone. |
| `Assets/Scripts/Gameplay/Meta/DailyPlan.cs` (new) | `struct DailyPlan`; `static DailyGames`: `Today`, `DateKey`, `DateText`, `UntilNext`, `SeedFor`, `For`; extras `DefaultSalt`, `TryDate(int, out DateTime)`. |
| `Assets/Scripts/Gameplay/Meta/SaveData.cs` | `CurrentVersion = 3`; `dailyAttempt`, `dailiesPlayed`, `dailiesWon`, `List<DailyRecord> dailies`; `struct DailyRecord` (the spec's nine fields); `Migrate`'s v2 → v3 step (fill the list, clamp the counters). |
| `Assets/Scripts/Gameplay/Meta/UnlockDef.cs` | `DailiesPlayed` (16), `DailiesWon` (17) appended. |
| `Assets/Scripts/Gameplay/Meta/MetaConfig.cs` | `[Header("Daily Games (R8)")] dailyUnlock, dailyRenownBonus 0.5, dailyEdicts 2, dailySalt "gladiator-daily-1", dailyLedger 30`; `RenownFor` × `DailyMultiplier(run)` (extra, public: 1 + bonus on the scored attempt, else 1). |
| `Assets/Scripts/Gameplay/Meta/MetaProgress.cs` | `RunSummary.Seed / DailyDate / DailyScored` (+ extra `RunId`); `RunRewards.DailyBonus`; `Apply` records the scored daily; `Met` / `Progress` for the two kinds; `DailyOpen`, `DailyAttempted`, `BeginDaily` (each with a pure `SaveData` overload). |
| `Assets/Scripts/Gameplay/Run/RunEndController.cs` | `Summary`: `Seed = GameRandom.RunSeed`, `RunId = GameSession.Run.id`, `DailyDate`, `DailyScored`. |
| `Assets/Scripts/Gameplay/Dev/PlaytestBot.cs` | The build summary ends with ` seed 7F3A-91C2` and, in a daily, ` daily 20261022 scored` (or ` practice`). |
| `Assets/Editor/Setup/DailySetup.cs` (new) | Menu `Gladiator/Setup/Meta/Create Daily Data` (below). |
| `Assets/Tests/EditMode/DailyTests.cs` (new) | 26 tests (below). |

## How it works
- **Every run gets a seed, and `GameRandom.BeginRun(Seed)` runs right before its scene loads** (`LoadRun` for a run's
  first arena, `Reload` for Retry / SAME SEED). The menu's scene (`ToMenu`) isn't seeded. Under `SuppressLoad` the
  seeding and `LastLoadPath` still happen; only the `LoadScene` (and its `TimeController` reset) is skipped, so a test can
  check `GameRandom.RunSeed == GameSession.Seed`.
- **Which seed:** `StartRun(g, run)` → `-runSeed` if the command line has one (`RunSeedOverride` in tests), else
  `FreshSeed()` (Stopwatch ticks ^ UTC ticks, and a counter, through `GameRandom.Hash`; never 0). `StartRun(g, run, seed)`
  uses `seed` and leaves any daily. `StartDaily(plan, scored)` uses `plan.Seed`, sets the plan's gladiator, run and
  `RunModifiers.Set(plan.Edicts)`. `Retry()`: a daily keeps its seed and turns `DailyScored` off (practice); a normal run
  gets `FreshSeed()` (not `-runSeed`: RETRY is a new run). `RetrySameSeed()`: keeps `Seed` (a daily: practice too); if the
  session never started a run (an arena loaded straight from the Editor or a test, `Seed == 0`) it replays
  `GameRandom.RunSeed`, the seed the end screen shows.
- **`Clear()` resets everything, `SuppressLoad` and `RunSeedOverride` included**: set them after calling it.
- **The plan** (`DailyGames.For`): the seed is `Hash(FNV-1a(salt), date, 0)` (FNV-1a = `GameInfo.StableId`, the same as
  R8-A's `KeyOf`); runs and gladiators are copied without nulls or repeats and sorted by id (ordinal; ties by asset name);
  arena = `runs[Hash(seed, 1, 0) % n]`, gladiator = `gladiators[Hash(seed, 2, 0) % n]`; edicts: the `dailyEligible` ones in
  `meta.edicts` (owned or not), sorted by id, one per id, then a partial Fisher–Yates where step i swaps in
  `i + Hash(seed, 3, i) % (n − i)`; `min(dailyEdicts, n)` of them, in draw order. No date, no arena or no gladiator → an
  invalid plan (`Edicts` is still an empty array, never null). No `MetaConfig` → the default salt and no edicts.
- **One scored attempt:** `BeginDaily(date)` returns true and saves `dailyAttempt = date` at once when the day's attempt is
  open, else false (practice). `DailyAttempted(date)` is `dailyAttempt >= date`, so setting the clock back to an earlier
  day never gives a second scored attempt.
- **The run's end:** `RunEndController.Summary` copies the seed and the daily's date and flag. `MetaConfig.RenownFor` =
  round(base × (1 + edict bonus) × (1 + dailyRenownBonus if scored)); bounties are added after, never multiplied.
  `Apply` with `DailyScored`: `dailiesPlayed++`, a win `dailiesWon++`, a `DailyRecord` appended (renown = what the run
  paid, edicts "a|b", seed as long), the ledger trimmed from the front to `meta.dailyLedger` (30); then `GrantEarned`, so
  Daily Bread and Champion of the Day unlock (and pay) at the run's end. Practice and normal runs touch none of it.
  `RunRewards.DailyBonus` is 0.5 on the scored attempt, 0 otherwise (the end screen's "×1.5").
- **Save v3:** new fields default to 0 / empty; `Migrate` fills the list and clamps the counters (idempotent, so a v1 file
  runs every step); `SaveSystem.Save` writes 3.

## DailySetup (`Gladiator/Setup/Meta/Create Daily Data`)
Creates or updates (every run rewrites the three unlocks' fields, so twice = once), all in `Assets/Data/Meta/`:
| File | Id | Name | Kind | Bounty | Hint |
|---|---|---|---|---|---|
| `UL_Daily` | `daily` | The Daily Games | Challenge, `DefeatBoss gorm 1` | — | Defeat Gorm. The heralds only cry the names of those who have. |
| `UL_DailyBread` | `daily_bread` | Daily Bread | Challenge, `DailiesPlayed 3` | 100 | Play 3 scored dailies. |
| `UL_ChampionOfTheDay` | `champion_of_the_day` | Champion of the Day | Challenge, `DailiesWon 1` | 150 | Win a scored daily. |
Then `MetaConfig.dailyUnlock = UL_Daily`, an empty `dailySalt` is filled with the default (the other daily numbers keep
their code defaults and any inspector tuning), `MetaSetup.CreateOrUpdate()` collects the unlocks. Log:
`[DailySetup] 3 daily unlocks; daily: Renown ×1.5, 2 of 10 eligible edicts, salt "gladiator-daily-1", ledger 30; N unlocks
in MetaConfig` (a warning if fewer eligible edicts than `dailyEdicts`).

## Decisions (one-line reasons)
1. **`DailyAttempted` is `>=`, not `==`:** the spec's "no time-zone exploits" — setting the clock back a day would otherwise
   give yesterday's scored attempt again.
2. **`RunSummary.RunId` added** (my file, filled in my `RunEndController.Summary`): the ledger's `DailyRecord.run` needs the
   RunDef id and the summary had none (only the arena tier).
3. **`RetrySameSeed` with no session seed replays `GameRandom.RunSeed`:** an arena opened straight in the Editor would
   otherwise "replay" seed 0.
4. **The bot / stress skip is gone** (contract: `StartRun` = `-runSeed` or fresh): bot runs started from the menu now roll
   differently per run unless `-runSeed` is passed; `Tools/perfrun` should pass one (orchestrator's step).
5. **`-runSeed` also seeds a first scene that is an arena** (`BeforeSceneLoad`, after R8-A's `SubsystemRegistration`
   reset): a bare arena build never goes through `StartRun`. Harmless when the first scene is the menu.
6. **`For` drops nulls and repeats, one edict per id, ties sorted by asset name:** "two players with the same unlocks get
   the same daily" must hold however the menu builds its lists.
7. **`DateText` uses a fixed month table ("22 OCT 2026", "5 JAN 2027"):** invariant whatever the machine's culture; no
   leading zero on the day.
8. **`StartDaily` with an invalid plan logs a warning and does nothing** (no load): the menu can't start an empty daily.
9. **DailySetup leaves the daily's numbers alone** (unlike EdictSetup, which rewrites its numbers): they're MetaConfig
   tunables with code defaults; the existing MetaConfig.asset gets the defaults on load because the fields are new.
10. **PlaytestBot's line adds "scored" / "practice"** after ` daily yyyymmdd` (the contract's text is a prefix of it).

## Tests (`Assets/Tests/EditMode/DailyTests.cs`, written, not run)
`For_IsTheSame_ForTheSameDateAndLists` · `For_ChangesWithTheDate` (30 days: 30 seeds, >1 arena / gladiator / edict pair) ·
`For_IgnoresTheListsOrder_NullsAndRepeats` (shuffled lists with nulls and repeats, `meta.edicts` reversed, 20 days) ·
`For_PicksOnlyFromTheEarnedLists` · `For_GivesDistinctEligibleEdicts` (60 days; a duplicate id; 3, 9 and 0 a day; no edicts
→ still valid) · `For_IsInvalid_WithNoArenaOrGladiator` · `DateKey_DateText_UntilNext_AtTheEndOfTheUtcDay` (23:59:30 UTC →
30 s, midnight → the next day, tr-TR culture, leap day, bad keys) · `SeedFor_ChangesWithTheSaltAndTheDate` ·
`BeginDaily_ScoredOnce_ThenPractice_ThenScoredTheNextDay` · `BeginDaily_SavesTheAttemptAtOnce` (temp save, reload) ·
`DailyOpen_NeedsItsUnlock` · `Apply_ScoredDaily_CountsAndRecords` · `Apply_TrimsTheLedger_ToMetaConfigsLength` (35 → 30, then
7) · `Apply_PracticeOrNormalRun_TouchesNoDailyRecord` · `RenownFor_TimesOneAndAHalf_OnlyWhenScored_AndStacksWithEdicts` (112
→ 168; ×1.45 × 1.5 = 244) · `DailiesPlayedAndWon_MetAndProgress` (+ indices 16, 17) ·
`DailyChallenges_UnlockAtTheRunsEnd_WithTheirBounties` · `Save_V2File_MigratesToV3` (a v2 file, a full-uint seed through
the file, a bare object, idempotent) · `Save_V1File_RunsEveryStep` · `StartRun_SetsAFreshSeed_AndRecordsTheLoad` ·
`FreshSeed_DiffersEveryTime_AndIsNeverZero` · `Retry_ANormalRun_GetsAFreshSeed_SameSeedKeepsIt` ·
`StartDaily_ThePlan_AndItsRetryIsPracticeOnTheSameSeed` · `StartDaily_AnInvalidPlan_DoesNothing` ·
`RunSeedOverride_StandsInForTheCommandLine` · `StartRun_WithoutAnArena_LoadsNothing`.
The GameSession tests assume R8-A's `BeginRun(s)` sets `RunSeed = s` (the contract's wording).

## Verified / unverified
- **Verified:** `Tools/ccheck` errors=0, 0 warnings, all six assemblies, against a five-member stub of R8-A's API (the
  contract's signatures) put into `DamageInfo.cs` for the check and reverted; without it, the 14 R8-STUB(a) errors only.
- **Unverified (needs Unity / R8-A):** every test; the setup script's run; R8-A's real `Hash` / `TryParseSeed` (the
  tests use them only through relative checks: equal / different / round trips); the `BeforeSceneLoad` hook's order after
  R8-A's reset (Unity documents SubsystemRegistration → BeforeSceneLoad).

## Needs from others / for the orchestrator
- **`EdictMetaTests.Save_V1File_MigratesToV2` will fail** (not my file): it asserts `2` for `CurrentVersion` and the loaded,
  written and migrated versions (lines 269, 272, 281, 288, 290). Make them `SaveData.CurrentVersion` (or `>= 2`).
- **`GameInfo.SaveVersion`** (yours) mirrors the save: 2 → 3.
- **`Tools/perfrun`**: pass a fixed `-runSeed` (bot runs are no longer seeded per launch only).
- **R8-A:** nothing beyond the contract's names: `GameRandom.Hash(uint, uint, uint)`, `BeginRun(uint)`, `RunSeed`,
  `SeedText(uint)`, `TryParseSeed(string, out uint)`.
- **R8-C:** nothing new is required; extras it may use: `DailyGames.TryDate`, `MetaConfig.DailyMultiplier`,
  `GameSession.TryCommandLineSeed`, `SaveData.dailies` (oldest first; today's scored result = the last record whose `date`
  is today). `GameSession.Clear()` turns `SuppressLoad` off.
- **Optional (CONCERNS candidate):** Daily Bread and Champion of the Day show in the Codex's Challenges before the DAILY
  button opens (as the edict challenges do; R7-B's `WaitsForEdicts` was never wired into `CodexPanel`).

## Orchestrator: menu items, in order (after merging A → B → C)
1. `Gladiator/Setup/Meta/Create Daily Data` (after `Create Edict Data` has run once; it ends with `Create Meta Data`).
   Expect `[DailySetup] 3 daily unlocks; daily: Renown ×1.5, 2 of 10 eligible edicts, …`.
2. Remove the `R8-STUB(a)` markers (`grep -rn "R8-STUB(a)" Assets`).
3. Fix `EdictMetaTests.Save_V1File_MigratesToV2` (above), then run `DailyTests` and the EditMode suite.
