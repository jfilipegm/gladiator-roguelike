# R8-C daily screens — the Daily Games' menu, end screen and HUD (worker notes, 2026-10-22)

Branch `claude/distracted-jackson-56f7fe`, from `origin/phase5-daily` (526843e). Contract: `docs/tracks/R7-contract.md`
"R8-C daily screens"; spec: `docs/tracks/R7-design.md` §3.1, §3.5–3.7. No Unity here: compiled, not run.

## Files
| File | What changed |
|---|---|
| `Assets/Scripts/UI/DailyPanel.cs` (new) | The Daily screen's two blocks: the proclamation (arena, gladiator, the day's edicts, Renown, the attempt, the clock) and the ledger (7 lines); `Show`, `Tick`, `Fit`, `Names`; pure words (`Header`, `DateLong`, `DateShort`, `Countdown`, `Clock`, `ResultLine`); test API |
| `Assets/Scripts/UI/MainMenu.cs` | DAILY as the 2nd menu button (greyed + its unlock's hint until `DailyOpen`, a gold dot while today's attempt is open); `Screen.Daily` with `Chrome`; FIGHT! → `StartDaily(plan, BeginDaily(plan.Date))`; E / North and the edicts block → `Edicts.Open(plan.Edicts)` (read-only); midnight → the new plan; `-daily yyyymmdd`; `CodexButtonText` follows the CODEX button (no longer index 1); test API |
| `Assets/Scripts/UI/RunEndPanel.cs` | Note line "SEED 7F3A-91C2" / "DAILY 22 OCT · SCORED" / "· PRACTICE" (the admin hint after it, dimmer, in debug builds); Renown row "+124 ×1.45 ×1.5 daily = +270"; RETRY · SAME SEED · MAIN MENU, or PRACTICE · MAIN MENU in a daily; S / gamepad West = SAME SEED; test API |
| `Assets/Scripts/UI/Hud.cs` | `Start`: a "DAILY" chip right of the timer in a daily (gold scored, grey practice), built once |
| `Assets/Scripts/UI/StatsPanel.cs` | The Pause face's sub-header ends with "SEED 7F3A-91C2" (`AppendSeed`: written digit by digit into the CharBuffer, no allocation) |
| `Assets/Scripts/UI/CodexPanel.cs` | RECORDS "Dailies played", "Dailies won" (one line, side by side, before "Most edicts in a win") |
| `Assets/Tests/PlayMode/DailyMenuTests.cs` (new) | 1 plain test + 7 PlayMode tests (below) |

No `.meta` files committed for the two new `.cs` files: the orchestrator's Editor makes them on import.

## How it works
- **The DAILY button.** `Awake`: `_dailyOpen = MetaProgress.DailyOpen(meta)`. Buttons PLAY 10 · DAILY −82 · CODEX −174 ·
  OPTIONS −266 · QUIT −358 (same 92 pitch; QUIT's bottom is 144 px above the screen's at 1080). Closed: not interactable
  (the keyboard focus skips it, as before for disabled buttons), its label dim, and beside it the unlock's hint (italic,
  dim gold, autosize 24 → 16, 760 wide; "Defeat Gorm. The heralds only cry…" from DailySetup; a fixed line if the hint is
  empty or "???"). Open: "DAILY", or "DAILY  •" with a gold dot while `!DailyAttempted(Today())`.
- **The Daily screen** (`Screen.Daily`, its own CanvasGroup between Arena and Codex, under the Edicts layer). `Chrome`
  gives "THE DAILY GAMES — 22 OCT 2026" (re-texted on every open), the key line "Enter / A: fight · E / Y: edicts ·
  Esc / B: back", BACK and FIGHT!. `DailyPanel.Build` adds one 1760 × 790 box centred between the header (MenuLayout.Top)
  and the buttons (MenuLayout.Bottom); `Refit` scales it with `GridMath.Fit` into (canvas − 2 margins) × BlockHeight: scale
  1 at every 16:9 (1080p, 720p: the canvas is 1920 × 1080).
  - **Proclamation** (900 wide): "THE EDITOR PROCLAIMS"; two cards side by side, the arena (thumbnail 288 × 162 =
    1.5×, `ArenaLadder.SetThumbnail`; name; tagline) and the gladiator (portrait 162², `RosterGrid.SetPortrait`; name;
    select line); "THE DAY'S EDICTS — click or E / Y to read them" over two edict rows (icon cell at 3× or the initial,
    name, effect line, "+25 % RENOWN", an orange-red edge: it hurts you); "RENOWN ×1.5 · EDICTS ×1.45" (gold; in
    practice the ×1.5 is struck through and greyed); the attempt: "SCORED ATTEMPT" (gold) + "Your first fight today
    counts: ×1.5 Renown and a line in the ledger.", or "PRACTICE · scored: VICTORY 8:00, 1,904 kills, +412" (grey; "…
    today's scored attempt was left unfinished" when the scored run never ended) + "Practice pays as a normal run: no daily
    bonus, no ledger line."; "NEXT DAILY IN 5 H 12 M".
  - **Ledger** (840 wide): "THE LEDGER" + "PLAYED 9 · WON 1"; up to 7 lines of `SaveData.dailies`, newest first: "22
    OCT / 2026", gladiator / arena (names from `Names(AllRuns, gladiators)`; an unknown id reads as words, "old_arena" →
    "Old Arena"), "VICTORY" gold / "DIED" orange-red + "8:00 · 1,904 kills", "+412" gold; today's line framed in gold.
    Empty: "No dailies yet. Your first scored attempt writes the first line."
  - **The plan**: `DailyGames.For(DateKey(UtcNow), meta, _openRuns, _available)` — the arenas and gladiators the select
    screens found earned (For sorts by id itself). Built at Awake (for the dot) and again on every open.
  - **Input**: Enter / Space / South = FIGHT!, Esc / East = back to the menu, E / North = the edicts page (read-only, R7's
    `EdictsPanel.Open(forced)`, which then owns the input until Esc / E closes it). Mouse: DAILY, BACK, FIGHT!, the edicts
    block (hover = a pale ring).
  - **FIGHT!** (`FightDaily`): if midnight passed on the screen, the new day's plan first; closes the edicts page;
    `scored = MetaProgress.BeginDaily(plan.Date)` (marks and saves at once); `GameSession.StartDaily(plan, scored)`, which
    sets the gladiator, run, edicts and seed and loads. Disabled (blocked sound) when the plan is invalid.
  - **The clock**: `MainMenu.Update` calls `Daily.Tick(UtcNow)` while the screen shows; Tick works once a second and
    rewrites the line only when the minute changes; it returns true once the UTC date differs from the plan's, and the
    menu then shows the new day's plan.
- **`-daily yyyymmdd`** (or `-daily today` / a bad date → today, with a warning): `Start` builds that date's plan from
  the save's earned lists and calls `StartDaily(plan, false)` (practice: never spends the attempt), with or without `-bot`.
  `-gladiator`, `-arena`, `-edicts` don't apply to it (the daily fixes them). No plan → the normal `-bot` path.
- **End screen.** `Fill`: `_daily = GameSession.HasDaily`; note = `SeedLine(…)` ("SEED " + `GameRandom.SeedText(RunSeed)`,
  or "DAILY 22 OCT · SCORED / PRACTICE" from `GameSession.Daily.Date` / `DailyScored`); buttons placed per mode (three
  270-wide at −290 / 0 / 290, or two at ∓150); RETRY → `RunEndController.Retry()` (a daily: practice on the same seed,
  R8-B), SAME SEED → `GameSession.RetrySameSeed()`. The Renown value box is 450 wide (x 65, autosize 34 → 18) for the
  longest line "+124 ×1.45 ×1.5 daily = +270 +150  (2410)"; a run with neither edicts nor the daily bonus reads exactly as
  before.
- **HUD chip**: 104 × 32 at (+98, −61) from the top centre, left-pivoted (right of "8:00" and of "BOSS"); dark text on gold
  (scored) or white on grey (practice); placed right after the timer in the hierarchy so the intro's dim, the boss HUD and
  PAUSED draw over it.
- **Pause page**: "every stat, then where it comes from" then, at a 68 % tab stop, "SEED 7F3A-91C2". Same words as
  `GameRandom.SeedText` (a test checks five seeds).

## Decisions (one line each)
1. **Ledger lines are two-line rows (date / who-where / outcome-detail / Renown), not seven columns**: seven columns in 806 px would need 14 pt text.
2. **The ledger shows 7 of the 30 kept** (the spec's 7); the counts line shows the totals.
3. **Arena and gladiator side by side** ("THE NOBODY in THE SLAVE PIT" reads as one proclamation), each picture at its baked size ×1.5 / ×1.27 to fit 790 px of height with the edicts and the lines below.
4. **Practice strikes the ×1.5 through** instead of hiding it: the player sees what the scored attempt paid and that this one won't.
5. **"VICTORY" for a won daily** (`RunOutcome.Survived`), as the spec's example; the record doesn't say whether the boss fell.
6. **An unfinished scored attempt says so** ("today's scored attempt was left unfinished") rather than inventing a result.
7. **SAME SEED's S key waits 1 s after the screen shows (R / Enter wait 0.4 s)**: S also walks backwards, so a last dodge mustn't replay the seed. Gamepad West is unbound in play, so it needs no extra wait.
8. **No SAME SEED in a daily**: its RETRY (PRACTICE) already replays the seed.
9. **`-daily` uses the save's earned lists**, so a bot daily is the daily that save's player would get; it never spends an attempt (R8-B's spec) and runs without `-bot` too (an explicit dev switch).
10. **The menu reads the attempt through `MetaProgress.DailyAttempted`**, the same test `BeginDaily` uses (`>=`: setting the clock back gives nothing), so the screen never promises a scored attempt FIGHT! won't give.
11. **The chip sits under later overlays** (sibling index after the timer). R7's edict row, built the same way at Start, still draws over the intro's dim (an observation, not changed: R7's).
12. **A truncation check lives in the UI assembly** (`DailyPanel.FirstTruncated`): the PlayMode tests' asmdef has no TextMeshPro reference and isn't mine.

## Tests (written, not run: the orchestrator runs them)
`Assets/Tests/PlayMode/DailyMenuTests.cs` (menu scene + `Assets/Scenes/Dev/TestArena.unity`; the sandbox save; SetUp sets
`GameSession.SuppressLoad`; TearDown unpauses, clears it, `RunModifiers`, `GameSession`, dev switches, time, the save).
Needs DailySetup's and EdictSetup's data (`MetaConfig.dailyUnlock`, ≥ 2 daily-eligible edicts, `iron_hides`, `blood_sport`),
`RD_Slice` (id `slice`) and `GL_Nobody`.
1. `Words_Dates_TheClock_TheSeedLine_AndTheRenownBreakdown` (plain): dates, `DateLong == DailyGames.DateText`, countdown (incl. `UntilNext` at 23:59:30), clock, `SeedLine`, `AppendSeed == SeedText` for 5 seeds, `RenownLine` with daily (+ edicts, + bounty) and unchanged without.
2. `BeforeGorm_DailyIsGreyedWithItsHint_AndWontOpen`: not open, no dot, the hint (= UL_Daily's), `OpenDaily` refused, the 5 buttons in order and on screen, the hint not cut off. Ends with `NoUnexpectedReceived`.
3. `Earned_TheScreenProclaimsTodaysPlan`: dot; a real click on DAILY opens it; the plan is today's, valid, earned; header, arena, gladiator, 2 distinct edicts; SCORED ATTEMPT, the Renown line, the clock, empty ledger; `Tick` sees a new day; box / edicts / FIGHT! / BACK on screen, the box between the key line and the buttons, nothing cut off; a click on the edicts opens the read-only page with the day's two, Esc closes it, the save's choice untouched; BACK. Ends with `NoUnexpectedReceived`.
4. `Fight_StartsThePlan_MarksTheAttempt_AndTheNextIsPractice`: a click on FIGHT!: `HasDaily`, `DailyScored`, date, seed, gladiator, run, `RunModifiers.Active`, `LastLoadPath`, the attempt saved at once, the dot gone; reopened: PRACTICE, the struck ×1.5, nothing cut off; FIGHT! again → practice on the same seed, nothing more spent.
5. `TheLedger_ShowsTheSavesRecords_NewestFirst_AndTodaysResult`: 9 records → 7 lines, today's on top and framed, names, an unknown arena id as words, the 7th line; on screen, not cut off; the practice line names today's result exactly; Codex RECORDS 9 / 1. Ends with `NoUnexpectedReceived`.
6. `ANormalRun_ShowsItsSeed_SameSeedKeepsIt_AndRetryDrawsAFreshOne`: no chip; the pause page shows the seed; the end screen's seed, RETRY / SAME SEED on screen and apart, no "daily"; SAME SEED keeps the seed (the scene isn't reloaded under SuppressLoad, `LastLoadPath` = this arena); RETRY draws another.
7. `AScoredDaily_ShowsTheChip_TheDailyLine_TheBonus_AndOnlyPractice`: gold chip on screen, 2 edict icons; "DAILY 22 OCT · SCORED", no SAME SEED, "Practice  [R]", `DailyBonus` > 0 and "×1.5 daily" in the row, nothing cut off on the end screen; PRACTICE → still the daily, not scored, same seed.
8. `APracticeDaily_HasAGreyChip_AndNoBonus`: grey chip; "· PRACTICE"; `DailyBonus` 0, no "daily" in the row.

## Verified / unverified
- **Verified:** `Tools/ccheck` in this worktree: `errors=8`, all `CS0246 DailyPlan` / `DailyRecord` (R8-B's types; the
  compiler stops there). In a scratch copy with **R8-B's pushed files** (`claude/jolly-kalam-811c2a` @ cf24051) and stand-ins
  for R8-A's five `GameRandom` names (`RunSeed`, `BeginRun`, `SeedText`, `TryParseSeed`, `Hash`): all six assemblies `ok`,
  0 warnings, `errors=0`. Also with stand-ins of every contract name (before R8-B pushed): `errors=0`. Every call to an
  R8-A / R8-B name is marked `// R8-STUB(a|b)`.
- **Unverified (no Unity):** everything at runtime: the layout and text fit at 1080p and 720p, colours, the tests, mouse /
  keyboard / gamepad, `-daily` in a bot run, the midnight roll-over.

## Needs from others
- **R8-A:** `GameRandom.RunSeed` (the seed the streams run on), `SeedText(uint)` → "7F3A-91C2" (upper-case hex, a dash after
  four digits: `StatsPanel.AppendSeed` writes the same words without allocating, test 1 compares them), `BeginRun(uint)`
  setting `RunSeed` (the tests seed the test arena with it).
- **R8-B (as pushed):** `GameSession.SuppressLoad` holding RETRY / SAME SEED's reload and recording `LastLoadPath` (tests 4, 6,
  7 rely on it: it does); `StartDaily` setting `Gladiator` / `Run` / `RunModifiers` (it does); `RunRewards.DailyBonus` 0.5 only
  on the scored attempt; `SaveData.dailies` oldest first; `MetaConfig.dailyUnlock.hint`.
- **Orchestrator:** Unity makes `DailyPanel.cs.meta` and `DailyMenuTests.cs.meta`; run `Create Daily Data` before the tests.

## Screenshots for the orchestrator
1. The main menu with DAILY: closed (greyed, the hint beside it) and open (the gold dot), 1080p.
2. The Daily screen at **1080p and 720p**: scored (fresh save with UL_Daily) and practice with a full ledger (stage 9 records
   as test 5 does, today's VICTORY on top); check nothing truncates (arena / gladiator names, effect lines, the practice
   line, ledger names) and the box clears the header and the buttons.
3. The read-only Edicts page over the Daily screen (E).
4. The HUD in a scored daily (gold chip right of the timer) and in practice (grey), plus during the boss ("BOSS" + chip).
5. The pause page's header with the seed (check "SEED 7F3A-91C2" doesn't touch "where it comes from").
6. The end screen: a normal run (seed, RETRY · SAME SEED · MAIN MENU) and a scored daily under two edicts (the longest
   Renown row, "DAILY 22 OCT · SCORED", PRACTICE · MAIN MENU).
7. The Codex RECORDS page (17 rows, 9 lines: the last line close to the page's bottom edge).
