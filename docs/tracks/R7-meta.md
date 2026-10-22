# R7-B meta + setup — notes (worker, 2026-10-22)

Branch `claude/vigilant-varahamihira-3247fd` (from `origin/phase5-run-modifiers` at 9836f50). Contract:
`docs/tracks/R7-contract.md` "R7-B meta + setup"; spec `docs/tracks/R7-design.md` §2 and §5. No Unity, no Blender.

## Files (only the ones the contract gives R7-B)
| File | What changed |
|---|---|
| `Assets/Scripts/Gameplay/Meta/SaveData.cs` | `CurrentVersion = 2`; `List<string> edictsChosen`, `List<IdCount> edictWins`, `int bestEdictWin`; `Migrate` fills the lists and clamps the record (the v1 → v2 step) |
| `Assets/Scripts/Gameplay/Meta/UnlockDef.cs` | `UnlockConditionKind.WinWithEdicts` appended (index 15) |
| `Assets/Scripts/Gameplay/Meta/MetaConfig.cs` | `[Header("Edicts (R7)")] edicts`, `edictsUnlock`; `BaseRenownFor` (today's formula); `RenownFor` = round(base float × (1 + max(0, EdictBonus))) |
| `Assets/Scripts/Gameplay/Meta/MetaProgress.cs` | `RunSummary.Edicts` / `EdictBonus`; `RunRewards.BaseRenown` / `EdictBonus`; `Apply` fills both and records edict wins; `Met` / `Progress` for `WinWithEdicts`; `EdictsOpen`, `IsOpen`, `ChosenEdicts`, `SetChosenEdicts` (+ `Data` overloads and pure `SaveData` overloads); **extra:** `WaitsForEdicts` (see "Needs") |
| `Assets/Scripts/Gameplay/Run/RunEndController.cs` | `Summary`: `s.Edicts = RunModifiers.ActiveIds(); s.EdictBonus = RunModifiers.RenownBonus;` |
| `Assets/Editor/Setup/EdictSetup.cs` (new) | menu `Gladiator/Setup/Meta/Create Edict Data` |
| `PixelSrc/upgrade_icons.py` + `Assets/Art/Textures/UI/UpgradeIcons.png` / `.txt` | eleven `ed_<id>` glyphs appended (rows 65–75); rows 0–64 pixel-identical (checked) |
| `Assets/Tests/EditMode/EdictMetaTests.cs` (new) | 13 tests (below) |

## How it works
- **Renown.** `BaseRenownFor(run)` is the old `RenownFor` (time, kills, boss, victory, × arena depth). `RenownFor(run)` multiplies
  the *unrounded* base by `1 + max(0, run.EdictBonus)` and rounds once. With no edicts both are equal, so every existing
  caller and test is unchanged. `Apply` pays `RenownFor`; bounties are added after, never multiplied. `RunRewards` carries
  `BaseRenown` and `EdictBonus` for the end screen ("+base ×1.45 = +renown").
- **Records.** On a **won** run, each distinct non-empty id in `run.Edicts` gets +1 in `edictWins`, and `bestEdictWin` =
  max(itself, that count). A death records nothing (the Renown bonus still pays). `WinWithEdicts`: `bestEdictWin >= max(1,
  amount)`; `Progress` = bestEdictWin / amount, capped.
- **Open / chosen.** `EdictsOpen(meta, d)` = meta set and (`edictsUnlock` null or unlocked). `IsOpen(e)` = feature open and
  (`e.unlock` null or unlocked). `ChosenEdicts` walks `meta.edicts` (so the result is in the panel's order) and keeps the open
  ones whose id is in `edictsChosen`: locked, unknown and repeated ids drop out silently (the save keeps them; a later Buy
  brings a Market one back). `SetChosenEdicts(chosen)` replaces the ids (nulls/repeats dropped) and saves.
- **Run summary.** `RunEndController.Summary` copies `RunModifiers.ActiveIds()` / `RenownBonus` exactly as the contract says
  (not gated on `RunModifiers.AppliedTo == run`: a PlayMode test that sets edicts after the load would lose its bonus).
- **Save v2.** New fields default to empty/0; `Migrate` fills null lists (an idempotent step, so a v0/v1 file runs every step)
  and bumps the version; `SaveSystem.Save` writes 2.

## EdictSetup (`Gladiator/Setup/Meta/Create Edict Data`)
Creates or updates (every run rewrites the spec's numbers and texts, so twice = once):
- **Eleven `Assets/Data/Edicts/ED_*.asset`** in the spec's order: IronHides, HungryHorde, BloodSport, GlassJaw, SwiftFoes,
  EmptyPurses, FickleCrowd, MeagreChoices, ImpatientEmperor, ChampionsDay, NoMercy. Ids, numbers and Renown bonuses are
  spec §2.3 (Σ 2.35 → ×3.35 all on); every effect field is reset to "no change" before the edict's own; No Mercy is the only
  one with `dailyEligible = false`. Effect lines are kept short for the cards (≤ 40 chars); the flavour (`description`)
  carries the details the line leaves out (the cap still holds; 2 cards, 3 with Luck; the timer and boss unchanged).
- **Seven unlocks in `Assets/Data/Meta/`** (ids in brackets):
  | File | Id | Name | Kind | Opens |
  |---|---|---|---|---|
  | `UL_Edicts` | `edicts` | The Editor Takes Notice | Challenge, `DefeatBoss gorm 1` | the feature + Iron Hides, The Hungry Horde, Blood Sport (`unlock == null`) |
  | `UL_Edict_GlassJaw` | `edict_glass_jaw` | Edict: Glass Jaw | Market, 150 | Glass Jaw |
  | `UL_Edict_SwiftFoes` | `edict_swift_foes` | Edict: Swift Foes | Market, 150 | Swift Foes |
  | `UL_Edict_EmptyPurses` | `edict_empty_purses` | Edict: Empty Purses | Market, 150 | Empty Purses |
  | `UL_Edict_FickleCrowd` | `edict_fickle_crowd` | Edict: A Fickle Crowd | Market, 150 | A Fickle Crowd |
  | `UL_EditorsEye` | `editors_eye` | Under the Editor's Eye | Challenge, `WinWithEdicts 2` | Meagre Choices, The Impatient Emperor |
  | `UL_DayOfChampions` | `day_of_champions` | A Day of Champions | Challenge, `ReachArena 3` (the Grand Arena) | Champions' Day |
  | `UL_EmperorsPleasure` | `emperors_pleasure` | The Emperor's Pleasure | Challenge, `WinWithEdicts 4` | No Mercy |
  No bounties, `addsToPool` empty (the reward is the edicts that point at the unlock: `CodexPanel.Reward`, R7-C).
  Challenge hints are the goals ("Win a run under 2 edicts."; UL_Edicts: "Defeat Gorm. Someone in the stands is taking
  notes."); Market hints are "For sale in the Market." like the other Market items.
- **Icons:** `icon` = the atlas (`IconSetup.AtlasPath`), `iconRow` = the row of `ed_<id>` in `UpgradeIcons.txt` (warns per
  missing row; errors if the atlas isn't there).
- `MetaConfig.edicts` (spec order), `MetaConfig.edictsUnlock = UL_Edicts`, `SetDirty` + `SaveAssets`, then
  `MetaSetup.CreateOrUpdate()` (collects the seven into `MetaConfig.unlocks`, saves). Logs
  `[EdictSetup] 11 edicts (all on: Renown ×3.35), N unlocks in MetaConfig`.

## The icons (`PixelSrc/upgrade_icons.py`, `edict()` helper)
A 12 px glyph inside a red ring with cut corners and a dark inner line (the colour language: it hurts you). Review sheet
(8× and 28 px): the shared notes folder, `r7_meta_edict_icons.png`.
Iron Hides: a blade snapping, a spark · The Hungry Horde: three skulls · Blood Sport: a heart shot through · Glass Jaw: a
cracked glass heart · Swift Foes: a skull with speed lines · Empty Purses: a coin split in half · A Fickle Crowd: the thumb
down · Meagre Choices: two cards · The Impatient Emperor: an hourglass in a laurel · Champions' Day: a horned champion's helm
· No Mercy: the healer's cross struck out.
Iterations: 1 → Iron Hides (read as a beetle), Empty Purses (a bulb), A Fickle Crowd (an ice cream), No Mercy (a clover) and
the frame (orange top bar looked detached) redone; 2 → Iron Hides still too thin; 3 → thicker blade. **Art debt candidate:**
Iron Hides' break reads, but only just, at 28 px (3 iterations used; shipped).

## Decisions (one-line reasons)
1. **`RenownFor` rounds once from the unrounded base** (contract): `BaseRenownFor` × mul rounded twice could differ by 1.
2. **A negative `EdictBonus` counts as 0** (contract's `max(0, …)`): an edict never takes Renown away.
3. **Repeated or blank ids in `run.Edicts` count once / not at all** in `edictWins` and `bestEdictWin`: `RunModifiers.Set`
   already drops repeats; this keeps the record honest if a summary is built by hand.
4. **`EdictsOpen` is false with no `MetaConfig`** (the contract's formula, plus a null guard): no config, no edicts to show.
5. **EdictSetup overwrites its fields each run** ("creates or updates", idempotent): the numbers live in `EdictSetup.cs`;
   inspector tweaks to `ED_*` / the seven `UL_*` are lost on a re-run (tune in the script).
6. **Market unlock titles are "Edict: Glass Jaw"**: in the Codex Market beside Winged Sandals, a bare "Glass Jaw" for sale
   reads like an item that helps you.
7. **Effect lines short, details in the flavour**: the panel's cards are 2 × 6 at 720p too (R7-C truncates).
8. **`WaitsForEdicts` added** (not in the contract, my file only): see "Needs" — the feature is meant to be unknown until
   Gorm falls, but the Codex lists every Market item and challenge unconditionally.

## R7-STUB(a) (the only errors without R7-A)
`RunModifierDef` (type and fields) and `RunModifiers.ActiveIds` / `RenownBonus`:
- per line: `MetaConfig.cs` (the `edicts` field), `MetaProgress.cs` (9 lines: the `IsOpen` / `ChosenEdicts` /
  `SetChosenEdicts` signatures, `WaitsForEdicts`' loop), `RunEndController.cs` (2 lines);
- per file (a header comment, `RunModifierDef` throughout): `EdictSetup.cs`, `EdictMetaTests.cs`.
Without R7-A, `Tools/ccheck` stops at Gladiator.Gameplay with 7 errors, all CS0246 `RunModifierDef`.

## Tests (`Assets/Tests/EditMode/EdictMetaTests.cs`, written, not run)
`BaseRenownFor_IsTodaysFormula` (three summaries; equals `RenownFor` with no edicts) · `RenownFor_MultipliesByTheBonus_WinOrLose`
(×1.45 on a win and a death, ×3.35, a negative bonus, rounding once) · `Apply_PaysTheBonus_ButNeverOnBounties` ·
`Apply_RecordsEdictWins_OnlyOnWonRuns` (death, wins, repeats, blanks, none) · `WinWithEdicts_MetAndProgress` (+ index 15) ·
`WinWithEdicts_UnlocksAChallengeAtTheRunsEnd` · `IsOpen_NeedsTheFeature_ThenEachEdictsUnlock` (feature locked; Market before
and after `Buy`; challenge; null feature = open) · `ChosenEdicts_DropsLockedAndUnknown_KeepsMetaOrder` ·
`SetChosenEdicts_WritesIds_RoundTrips` (through a temp save file) · `WaitsForEdicts_HidesTheirUnlocksUntilTheFeatureOpens` ·
`Save_V1File_MigratesToV2` (a v1 file on disk, then an object with null lists; idempotent) · `Buy_AMarketEdict_SpendsRenown`
(Bought / AlreadyOwned / TooExpensive / NotForSale; conditions never grant it) · **`EdictData_MatchesTheSpec`** (reads the real
`MetaConfig.asset`: eleven in order, texts, icons on rows ≥ 65, Σ 2.35, each number, daily flags, the seven unlocks' kinds,
prices and conditions, all in `MetaConfig.unlocks`) — it fails until `Create Edict Data` has run, on purpose.
Existing tests: `MetaTests` (RenownFor numbers, `CurrentVersion` comparisons), `UnlockVarietyTests` and `RosterLayoutTests`
(v1 JSON + Migrate) only compare against `CurrentVersion` or lists, so the bump to 2 doesn't touch them.

## Verified / unverified
- **Verified:** `Tools/ccheck` errors=0 across all six assemblies, compiled against R7-A's real `RunModifierDef.cs` (read from
  its worktree, copied in temporarily, not committed) plus a two-member `RunModifiers` stub; without them only the 7
  `RunModifierDef` errors above. The atlas regenerates (76 rows, 160 × 1520) with rows 0–64 pixel-identical to before; the
  TXT diff is the eleven appended lines.
- **Unverified (needs Unity):** every test; the setup script's run (assets, folder creation, the icon wiring, the
  `MetaSetup` collect); the atlas import; the icons at 28 px in the HUD/panel.

## Needs from others
- **R7-C (`CodexPanel`)**: hide the edict unlocks until the feature opens — in the unlock loop,
  `if (MetaProgress.WaitsForEdicts(u, _meta, _save)) continue;`. Without it, before Gorm falls the Market lists the four
  "Edict: …" items (buyable for 150 Renown, useless until then) and the Challenges tab shows Under the Editor's Eye, A Day of
  Champions and The Emperor's Pleasure, spoiling the reveal (spec §2.3 "the feature is unknown until The Editor Takes
  Notice"). If R7-C has finished, it's a one-line integration fix.
- **R7-C (`RunEndPanel`)**: the Renown row uses `rewards.BaseRenown`, `rewards.EdictBonus`, `rewards.Renown` (as contracted).
- **R7-A:** nothing beyond the contract's names, as spelled (checked against its file).
- **Orchestrator (shared docs):** UNLOCKS (the seven above), DECISIONS (1–8 if wanted), ART_DEBT (Iron Hides icon).

## Orchestrator: menu items, in order (after merging A → B → C)
1. `Gladiator/Setup/Upgrade Icons` (re-imports the grown atlas; edicts aren't UpgradeDefs, so it doesn't touch them).
2. `Gladiator/Setup/Meta/Create Edict Data` (ends with `Create Meta Data`). Expect the log line above with `11 edicts`.
3. Remove the `R7-STUB(a)` markers (`grep -rn "R7-STUB(a)" Assets`).
4. Run `EdictMetaTests` (all 13, including `EdictData_MatchesTheSpec`), then the EditMode suite.
