# R7-C menu — the Edicts' screens (worker notes, 2026-10-22)

Branch `claude/pensive-jepsen-c8fd77`, from `origin/phase5-run-modifiers` (9836f50). Contract: `docs/tracks/R7-contract.md`
"R7-C menu"; spec: `docs/tracks/R7-design.md` §2.5. No Unity here: compiled, not run.

## Files
| File | What changed |
|---|---|
| `Assets/Scripts/UI/EdictsPanel.cs` (new) | The page: 2 × 6 cards, toggle / buy / clear / close, read-only mode, footer, keys, `Fit`, test API |
| `Assets/Scripts/UI/MainMenu.cs` | EDICTS button (arena screen), the panel's layer, E / North, input routing, `AsksForArena`, `EdictsForLaunch`, `Launch` → `RunModifiers.Set`, `-edicts`, Codex / Renown refresh after a purchase |
| `Assets/Scripts/UI/ArenaLadder.cs` (`ArenaTablet`) | `Show(…, RunModifierDef[] edicts)` (the old overload passes null); the EDICTS row |
| `Assets/Scripts/UI/Hud.cs` | `Start`: one icon per `RunModifiers.Active` under the timer; hidden while the boss HUD shows |
| `Assets/Scripts/UI/RunEndPanel.cs` | Renown row "+124 ×1.65 = +205 +50  (2410)" under edicts; `RenownLine`, `RenownText` |
| `Assets/Scripts/UI/CodexPanel.cs` | `Reward(u)` names edicts, edict icons on their unlock cards, RECORDS "Most edicts in a win", `Refresh()`, `RewardText`, `RecordValue` |
| `Assets/Tests/PlayMode/EdictMenuTests.cs` (new) | 10 PlayMode tests (below) |

No `.meta` files committed for the two new `.cs` files: the orchestrator's Editor makes them on import.

## How it works
- **Visibility.** `MainMenu.Awake`: `_edictsShown = meta.edicts.Length > 0 && MetaProgress.EdictsOpen(meta)`. The
  EDICTS button (bottom centre of the arena screen, 520 × 76, between BACK and FIGHT!) is built always and active only
  then; `AsksForArena` is also true then, so the arena screen always shows once the Edicts are earned. The key line adds
  "E / Y: edicts" then.
- **The page** (`EdictsPanel`) lives in its own `CanvasGroup` layer ("Edicts") over every screen, so R8-C can open it
  read-only over the daily screen. Closed = its root inactive (no clicks taken). A full-screen dim blocks the screen
  under it. The box (1640 × 980) is scaled down by `MainMenu.Refit` when the canvas is smaller (`GridMath.Fit`); at any
  16:9 (1080p, 720p) the canvas is 1920 × 1080, so scale 1.
- **Cards** (780 × 100, two columns, six rows, `MetaConfig.edicts` order, row-major): icon (20 px cell at 3×; the
  initial in orange-red before the atlas row exists), name, effect line (`effectLine`, else `description`), "+25 %
  RENOWN" top right, the state bottom right. States: **on** = orange-red frame (#FF5433, the end screen's YOU DIED), a
  warm card, an ON chip; **off** = grey frame, "OFF"; **Market, locked** = lit card, a BUY button "BUY · 150 RENOWN"
  (affordable) or "150 RENOWN" in grey (disabled); **challenge, locked** = dark card, the name dimmed, the unlock's hint
  (italic, dim gold), a padlock and "1 / 2" (`MetaProgress.Progress` of a single count condition, else "LOCKED").
  Focus = a gold ring; mouse hover = a pale ring.
- **Input.** While the page is open `MainMenu.Update` hands it everything: arrows / WASD / d-pad move
  (`GridMath.Step`), Enter / Space / South toggles (a locked Market card: buys), Backspace / Delete / West clears all,
  Esc / East / E / North closes. Mouse: a click on a free card focuses and toggles it; a Market card's body only focuses
  (its BUY button buys); CLEAR and DONE buttons at the bottom corners.
- **Saving.** `Close()` calls `MetaProgress.SetChosenEdicts(Chosen)` (not read-only). Buying goes through
  `MetaProgress.Buy(u)` (saves at once). The menu follows live through `EdictsPanel.OnChanged` (button text, tablet row,
  corner Renown); on close it re-reads `ChosenEdicts(meta)` and, if something was bought, rebuilds the Codex
  (`CodexPanel.Refresh`) and its NEW count.
- **Launch.** `MainMenu.Launch` calls `RunModifiers.Set(EdictsForLaunch())` right before `GameSession.StartRun`.
  `EdictsForLaunch()`: `-edicts a,b` (ids in `meta.edicts`, earned or not; an unknown id logs a warning) → else a
  `-bot` / `-stress` run gets none → else `MetaProgress.ChosenEdicts(meta)` (only while the Edicts are shown).
- **Tablet.** With edicts chosen and an open arena picked, a fifth row: label EDICTS, value "Iron Hides · Blood Sport
  RENOWN ×1.45" (one line, autosize 17 → 11 pt, Truncate). The names stop at ~38 characters, then "· +N more"; the
  multiplier always shows. To fit the wax, the rows close up (heights 24/40/24/24/24 instead of 26/44/26/26, the group
  6 px higher, ending exactly at the wax's bottom); with no edicts the rows sit exactly as before.
- **HUD.** `Hud.Start` (after `RunModifiers.Set`, which precedes the load): a row centred under the timer at y −88, one
  30 px dark plate per edict with its 28 px icon (the atlas's first column, no shine: nothing ticks). The boss HUD's name
  sits at −112, so the row is hidden while the boss HUD shows (toggled only when its alpha changes in `DrawBoss`).
- **End screen.** With `rewards.EdictBonus > 0`: "+{BaseRenown} ×{1 + EdictBonus} = +{Renown}", then " +{Bounty}" and
  "  ({RenownTotal})" as today. Without edicts the text is exactly today's. The Renown value box is 400 wide (was 220)
  and autosizes 34 → 22.
- **Codex.** `Reward(u)`: after a gladiator and a pool upgrade, the edicts `u` opens: those whose `unlock == u`, and for
  `meta.edictsUnlock` the ones with `unlock == null` ("Reward: Edicts — Iron Hides, The Hungry Horde, Blood Sport",
  "Reward: Edict — Glass Jaw"; a bounty is appended as today). The unlock's card shows the first such edict's icon.
  RECORDS: "Most edicts in a win" (`bestEdictWin`); the two-column grid closes up when it passes eight lines.

## Decisions (one line each)
1. **Two columns × six rows** (the spec's "2 × 6"): the effect lines are sentences, so wide cards read better than tall ones.
2. **Buying doesn't turn the edict on**: one action, one effect; a purchase never adds a handicap by itself.
3. **Mouse buys only through the BUY button** (as the Codex's Market does); Enter on a focused Market card buys at once.
4. **E / North also closes the page**: the key that opens it closes it.
5. **The button appears only when `meta.edicts` has data**: before EdictSetup runs, `edictsUnlock == null` reads as
   "open" and every player would get an empty page and an arena screen.
6. **A bot / perf run without `-edicts` fights under none**, not under the save's choice (perf numbers stay comparable).
   The contract says "else ChosenEdicts"; that still holds for every non-bot launch.
7. **Read-only page:** the forced edicts show lit even if not owned (the daily proclaims any), the rest at 40 % alpha,
   CLEAR hidden, DONE reads BACK, Buy returns `NotForSale`, nothing is saved, the menu's choice is untouched.
8. **A locked challenge card shows the bare count "1 / 2"** (the hint above it says what it counts); the spec's "1 / 2 wins
   under edicts" needs a unit per condition kind that the data doesn't have.
9. **HUD icons stay still** (no `UpgradeIconView` shine): "nothing per frame".
10. **HUD row hides during the boss fight**: the boss's name and bar own that space; the player chose the edicts.
11. **Tablet rows close up only when the EDICTS row shows**: nobody without edicts sees a change.
12. **Keys line is the spec's text** ("Arrows: move · Enter / A: toggle or buy · X / Backspace: clear · Esc / B: done").
13. **`Toggle` also refuses while the page is closed** (the choice lives in the open page; Open re-reads the save).

## Tests (written, not run: the orchestrator runs them)
`Assets/Tests/PlayMode/EdictMenuTests.cs` (menu scene + `Assets/Scenes/Dev/TestArena.unity`; the sandbox save; TearDown
clears `RunModifiers`, `GameSession`, dev switches, time, the save). They need EdictSetup's data, and assume the spec's
ids and order (`iron_hides` first, `hungry_horde` second, `glass_jaw` / `swift_foes` Market, `meagre_choices` a challenge,
Iron Hides +25 % and Blood Sport +20 %). `LogAssert.NoUnexpectedReceived` fails on any log (even `[Meta] bought …`), so
only tests 1, 2 and 7, which buy, save and unlock nothing, end with it (there it catches TMP warnings).
1. `BeforeTheEditorTakesNotice_ThereIsNoButton_AndNoPage`: hidden, `AsksForArena` false, `OpenEdicts` does nothing, launch empty.
2. `Earned_TheButtonShowsOnTheArenaScreen_AndOpensACardPerEdict`: shown, `AsksForArena`, "EDICTS", Count = `meta.edicts`, the key line, a real EventSystem click on the button opens the page, every card on screen, a click on a card toggles it (orange-red frame), no TMP warnings.
3. `FreeOnesToggle_AMarketOneIsBoughtFirst_AChallengeOneNeverToggles`: the three free toggle; Glass Jaw locked → `Buy` spends its price, saved, stays off, then toggles; Meagre Choices never toggles, `NotForSale`, shows its hint.
4. `AMarketEdict_WithoutTheRenown_IsTooExpensive`: `TooExpensive`, nothing spent, still locked, the grey price.
5. `TwoOn_TheButtonAndTabletShowTheMultiplier_CloseSavesThem_AndFightTakesThem`: "×1.45" on the button, footer and tablet; Esc closes and the file holds both ids; `EdictsForLaunch` returns them; a reloaded menu remembers them.
6. `Keys_MoveToggleBuyClearAndClose`: Enter toggles, right / down / up move, Enter on Glass Jaw buys, Backspace clears, Esc closes and saves the empty choice.
7. `TheDailysPage_IsReadOnly_AndSavesNothing`: `Open(forced)` → "PROCLAIMED BY THE EDITOR", no toggle / clear / buy, `Chosen` = forced, nothing saved.
8. `TheCodex_NamesTheEdictsAnUnlockOpens_AndRecordsTheMostInAWin`: the two reward texts; RECORDS shows `bestEdictWin` 3.
9. `UnderTwoEdicts_TheHudShowsTwoIcons_AndTheEndScreenTheMultiplier`: 2 icons on screen; after `RunState.End` the Renown row contains "×", the multiplier and "= +Renown".
10. `WithoutEdicts_TheHudHasNoRow_AndTheRenownRowReadsAsBefore`: no row; today's Renown text exactly.

## Verified / unverified
- **Verified:** `Tools/ccheck` in this worktree: `errors=13`, all `CS0246 RunModifierDef` (R7-A's type; the compiler stops
  there). In a scratch copy with stand-ins of exactly the contract's R7-A / R7-B names (`RunModifierDef`, `RunModifiers`,
  `MetaConfig.edicts` / `edictsUnlock`, `SaveData.edictsChosen` / `edictWins` / `bestEdictWin`,
  `RunRewards.BaseRenown` / `EdictBonus`, `MetaProgress.EdictsOpen` / `IsOpen` / `ChosenEdicts` / `SetChosenEdicts`):
  all six assemblies `ok`, 0 warnings, `errors=0`. Every such call is marked `// R7-STUB(a|b)`.
- **Unverified (no Unity):** everything at runtime: the layout, text fit at 1080p and 720p, the colours, the tests, mouse /
  keyboard / gamepad, `-edicts` in a bot run.

## Needs from others
- **R7-A:** `RunModifierDef` (`id`, `displayName`, `description`, `effectLine`, `icon`, `iconRow`, `renownBonus`,
  `unlock`); `RunModifiers.Active` never null; `RunModifiers.Bonus(defs)` pure and null-safe (I pass `Chosen`, maybe
  empty); `Set`, `Clear`.
- **R7-B:** the names above with the contract's signatures (I call `EdictsOpen(meta)`, `IsOpen(e, meta, save)`,
  `ChosenEdicts(meta)` and `(meta, save)`, `SetChosenEdicts(defs)`); `cfg.edicts` in the spec's order; Market edicts'
  unlocks `UnlockCategory.Market` with `renownCost`, challenge ones with a `hint`; `RunRewards.BaseRenown` /
  `EdictBonus` filled by `Apply`. Effect lines short enough for two lines at 19 pt in 470 px (about 95 characters; the
  spec's Meagre Choices text is 118 and will shrink toward 13 pt).
- **R8-C (later):** open the daily's edicts with `menu.Edicts.Open(plan.Edicts)` (the layer sits over every screen; its
  close never changes the player's choice). RECORDS has 15 rows; two more (Dailies played / won) make 9 lines, which the
  grid now closes up to fit (pitch 68).
- **Orchestrator:** Unity makes `EdictsPanel.cs.meta` and `EdictMenuTests.cs.meta`.

## Screenshots for the orchestrator
1. The arena screen with the EDICTS button: none on ("EDICTS") and two on ("EDICTS · RENOWN ×1.45"), 1080p.
2. The page at **1080p and 720p** with mixed states: two on, a Market card affordable and one not (set Renown ~200), a
   locked challenge card, the focus ring on a card (check nothing truncates: names, effect lines, "BUY · 150 RENOWN").
3. The read-only page (`menu.Edicts.Open(new[]{ swift_foes, champions_day })`).
4. The arena tablet with the EDICTS row: two edicts, and five (the "· +N more" path).
5. The HUD in a run with four edicts (`-edicts hungry_horde,champions_day,impatient_emperor,swift_foes`), and in the boss
   fight (the row hidden).
6. The end screen under edicts (ideally with a bounty: the longest Renown line).
7. The Codex: CHALLENGES (The Editor Takes Notice: reward line and icon), MARKET (Glass Jaw), RECORDS.
