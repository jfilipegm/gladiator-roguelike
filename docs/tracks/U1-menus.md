# U1-C — the menus in the Roman look (worker notes)

Branch `claude/wonderful-elgamal-d4aa78` (from `origin/phase5-ui` @ 0a61775). Contract: `docs/tracks/U1-contract.md`
("U1-C menus"); spec: `docs/tracks/U1-design.md` §5, §8. No Unity: compiled with `Tools/ccheck` (errors=0) against the
contract's stand-in `Assets/Scripts/UI/Theme/U1Stub.cs` (byte for byte, its own commit). Tests written, not run.

## Files (all owned by C)
| File | What changed |
|---|---|
| `Assets/Scripts/UI/Theme/U1Stub.cs` | the stand-in, byte for byte (commit `U1-STUB(a): …`); the orchestrator deletes it at merge |
| `MainMenu.cs` | mosaic title plaque + gold meander; the ansata column (PLAY bronze, the rest marble) with the focus on the plates; the bronze Renown plate (Title label + Number); `Chrome` (header Title 56 gold Outline + meander, key line Body 22 dust, BACK marble 220×66, FIGHT! bronze 300×78); EDICTS a marble plate; Codex / Edicts / Daily scaled with `UiKit.FitScale`; hooks |
| `RosterGrid.cs` | scutum tiles: `Scutum` / `ScutumLocked` body, `ScutumSpine`, the portrait behind a `Clipeus` / `ClipeusLocked` ring, the name Title 20 on the lower band; picked = gold focus ring + wreath + lift, hovered = pale ring; tessera sprites no longer used; `TileFrame(i)` |
| `LanistaTablet.cs` | the `Wax` frame; names Title 40 `waxInk`, labels Title gold, lines Body 22 → 18; the gladiator page re-flowed (below) |
| `ArenaLadder.cs` | steps keep their palette art; names Title (30 max, all steps at one size), lines Body 20, numerals Title (engraved); focus ring frames; the arena tablet on wax (re-flowed); the EDICTS row's names give way until it fits |
| `EdictsPanel.cs` | `Marble` page; header Title 48 `marbleInk` engraved + crimson `Rule`; cards on `Stone` (name Title 26, effect Body 20, share Number 22); ON = a `Focus` ring in the danger colour hugging the card + the ON chip; the keyboard focus a gold ring outside it; locked darker; BUY bronze 250×40; footer Title 26; CLEAR marble, DONE bronze |
| `DailyPanel.cs` | proclamation in a `Mosaic`, the arena / gladiator cards on `Bronze`, the edicts block on `Stone`; SCORED ATTEMPT Title gold / PRACTICE … Body dust; the clock Number 28; the ledger on `Wax` (date, details, Renown in Number), today's line in a gold ring, a won day's outcome between `UiKit.Laurels` |
| `CodexPanel.cs` | tabs = ansata plates 218×62 (marble; the active one re-skinned bronze, a gold meander under it); entries on `Marble` (still-locked ones on `Stone`); progress = `UiKit.Bar`; BUY bronze 240×48; RECORDS on a `Wax` tablet (labels Title 26, values Number 30); &lt; &gt; BACK marble; the whole page a 1640×980 box with `Fit` |
| `SkinStrip.cs` | a `Bronze` plate; ◀ ▶ small marble plates (60×88, today's chevron in `marbleInk`); name Title 26; status Body 20; BUY bronze 420×40 (dimmed with `SetState(Disabled)` when short) |
| `OptionsPanel.cs` | a `Bronze` panel (1240×960, `FitScale`); header Title 48; rows: label Title 24, a `UiKit.Bar` trough + `Knob` (the Slider drives the knob, the trough's fill follows the value), value Number 26; the focused row in a gold `Focus` ring; BACK bronze (the panel's "Done", word kept) |
| `Assets/Tests/PlayMode/UiMenuLookTests.cs` | new (below) |

No other file touched. No existing test edited.

## How it works
- Everything themed goes through `UiKit.Text / Frame / Button / Bar / Meander / Rule / Laurels`, `UiFrames.KindOf / Apply /
  NativeSize / Padding` and `UiTheme.Current` colours, read when a screen is built (menus may allocate). The old
  `font` / `fontMaterial` fields and the font constructor arguments stay (ignored); the version label stays Plain.
- **Fit.** Text sizes were checked offline from the four TTFs' metrics (fontTools; advance widths, TMP spacing per role,
  line height 1.254 em for Marcellus / Marcellus SC) against every box and the real data strings (gladiators, traits,
  weapons, arenas, edicts, skins). Every box is ≥ its line height; every label that can grow autosizes to the 18 floor
  and Truncates, never Ellipsis.
- **The gladiator tablet** (wax 518×670): portrait 192 at the top-left; name (Title 40 → 24, wrapped), select line, runs,
  stats beside it; the tradeoff full width under it; a rule; then the description, the weapon row and up to three
  traits **stacked by their measured heights** (`TMP.GetPreferredValues`) at the largest size from 22 down to 18 that
  fits; at 18 the last rows get what's left. Simulated against the data with the code's rules (ceil of the measured
  height, 6 between rows, names Title 20 ≥ 26 tall): the Nobody and the Pit Goblin fit at 22, the Oracle and the
  Beastmaster at 20, the Murder Machine at 19, the rest at 18; the tightest (Blood Knight, Golden Idol: three traits)
  end at 656 of the 664 units the rows may use.
- **The arena tablet**: name (Title 40 → 24), tagline, the thumbnail at 288×162, description (Body 22 → 18, 4 lines),
  rule, the boss block, then FLOOR / HAZARDS / DIFFICULTY / RENOWN (+ EDICTS) rows (labels Title 18 gold, values Body 20 →
  18). The EDICTS row tries `EdictLine(edicts, 38)` and lowers the names budget by 4 until the line fits at 18
  (`ArenaTablet.EdictLine(edicts, budget)` is a new overload; the old one is unchanged).
- **Menu focus**: `RefreshMenuFocus` sets `Focused` on the focused plate, `Disabled` on a shut DAILY, `Normal` on the
  rest, only when the focus or DAILY's state changes (no per-frame recolour, no allocation: a test checks).
- **Codex tabs** switch bronze ↔ marble in place: `UiFrames.Apply(plate.Frame, …)`, `plate.Style`, the label colour.

## Decisions (one line each)
1. **Tablet portrait 192 (the 128 px bake at 1.5×), arena thumbnail 288×162 (1.5×).** At the 18 floor the words need the
   room: at 256 / 384×216 the heaviest pages ran ~30–60 units past the wax. Tunables `GladiatorTablet.Portrait`,
   `ArenaTablet.ThumbW/H`.
2. **Tablet rows flow by measured height (22 → 18)** rather than fixed slots: the gladiators differ by 3× in text.
3. **The ladder's names share one size** (the smallest the longest needs, ≥ 18): autosize per step mixed 30 and 23.
4. **Picked / hovered tile = a gold / pale `Focus` ring** round the scutum (spec §4.1's focus ring for tiles); tinting
   the shield would tint its face too.
5. **Still-locked Codex entries on `Stone`** (the frame table's "locked entries"), not darkened marble: `dust` on
   darkened marble is ~1.3:1; on stone it reads.
6. **Gold written on marble is `#A8741F`** (the DAILY dot, CODEX "• n NEW", EDICTS' multiplier, the Edicts footer's
   multiplier): `#FFD84A` on marble is unreadable. Orange-red EDICTS stays (it hurts you).
7. **A death in the ledger is `loss`**, not orange-red (spec §1.2: nothing to dodge).
8. **Skin strip BUY 420 wide, Edicts BUY 250, Codex BUY 240**: "250 RENOWN · NEED 250 MORE" / "BUY · 350 RENOWN" /
   "NEED 1200 MORE" need it at 18 inside the ansata's handles.
9. **Options keeps the word BACK** on its bronze "Done" plate (no text changes outside the contract's list).
10. **The Codex is a 1640×980 box scaled by `FitScale`** like the other modals (it overflowed at 21:9 before).
11. **Ladder steps keep `PixelIcons.Tessera`** tinted per arena ("steps keep their palette art"); only words and rings
    changed.
12. **Codex 3-column cards' picture 100** (icons show at 80 either way): +20 units of body width.
13. **`EdictsPanel.CardFrame(i)` now reads the ON ring's colour while on** (still `Hurt`), else the plate's tint — the
    hook keeps its meaning ("orange-red while on"); `EdictMenuTests` passes unchanged.
14. **Tablet / ladder / tablet text is Engraved on the light steps, Flat on wax and stone, Outline over the 3D scene**
    (spec §2.3).

## Needs from U1-A (assumptions the code makes; all match the stand-in)
- `UiKit.Button` / `Plate`: the plate's **Root is named `name`, carries the `Button` and is the `Frame` image's object**;
  the label is its first TMP child. Tests find `MenuCanvas/Menu/Play`, `Select/Back`, `Daily/Fight`, `Arena/Edicts`
  and click them through the EventSystem (the top hit's click handler must be that object); `SetActive` is done on
  `plate.Root`.
- `UiFrames.Padding(Clipeus / ClipeusLocked)` = the ring's width, so the portrait window = `NativeSize − padding`.
- `UiKit.Laurels` returns **one container** holding both branches (the ledger destroys it when a row is redrawn).
- `UiFrames.Apply` on a plate's `Frame` re-skins an ansata in place, and `SetState` respects a changed `plate.Style`
  (the Codex's active tab).
- `UiKit.FitScale(size, area, 0f)` accepts margin 0 (the Daily blocks fit their area exactly, as `GridMath.Fit` did).
- `UiKit.HasRole` follows `UiKit.Style` (the Daily status switches Title ↔ Body).

## Tests
**New: `Assets/Tests/PlayMode/UiMenuLookTests.cs`** (menu scene, the sandbox save):
- `TheMainMenu_PlayIsBronze_TheRestMarble_AndTheArrowsMoveOneFocusedPlate` — PLAY `AnsataBronze`, four `AnsataMarble`;
  title Title, Renown Number; plaque `Mosaic`, Renown plate `Bronze`; a shut DAILY `Disabled` and skipped; one
  `Focused` plate as the focus moves (and wraps); nothing cut.
- `TheMainMenusIdleFrames_AllocateNothing` — 2000 `Update`s on the main menu: 0 B.
- `TheSelectScreen_ScutumTiles_TheWaxTablet_BronzeFight_MarbleBack` — open tiles `Scutum`, locked `ScutumLocked`, names
  Title, the pick's ring gold + wreath, FIGHT! bronze, BACK marble, tablet `Wax`, strip `Bronze`; nothing cut, and every
  locked tablet page fits.
- `EveryGladiatorsTablet_FitsTheWax_AtTheFloor` — everyone earned, every tablet page: nothing cut.
- `TheArenaScreen_WaxTablet_MarbleEdicts_AndTheMarbleEdictsPage` — all arenas + edicts earned: EDICTS marble, FIGHT!
  bronze, BACK marble, tablet `Wax`, step names Title, every arena's tablet fits; the page `Marble`, cards `Stone`, two
  edicts on → a `Focus` ring ≈ `danger` (±0.01) and `CardFrame` = `Hurt`; the page and the tablet's EDICTS row fit.
- `TheDailyScreen_MosaicProclamation_WaxLedger_LaurelsOnAWonDay` — `Mosaic`, `Wax`, SCORED ATTEMPT Title, clock Number,
  laurels on yesterday's win and not on a death, FIGHT! bronze, BACK marble; nothing cut.
- `TheCodex_BronzeActiveTab_WaxRecords_MarbleEntries_AndEveryPageFits` — every tab: the active one bronze, the others
  marble; RECORDS `Wax`; every page of every tab: entries on `Marble` or `Stone` and nothing cut; Market entries all
  `Marble`; GLADIATORS tiles scuta, tablet `Wax`.
- `Options_ABronzePanel_NumbersBesideTroughs` — `Bronze`, values Number, the trough's fill = the master volume, nothing cut.

**Intentional truncations: none** (the daily header and DAILY's hint fit at 1080p; `DailyMenuTests` already asserts both).

**Existing tests: none edited.** They should pass unchanged: `RosterTests` (tiles still named Card / Locked, the shield
is the click target, nothing raycastable on top, idle + hover allocate nothing), `SkinMenuTests` (strip box, arrows and
BUY are plate roots), `EdictMenuTests` (`CardFrame` = `Hurt` while on, card centre is the stone plate, BUY plate's root
toggled), `DailyMenuTests` (Header / Keys / Fight / Back names, the box between the key line and the buttons, no cut
text, `LedgerIsToday`), `CodexPlayTests`, `RunFlowTests`, `GoblinPlayTests` (the only `Card` buttons are tiles).

## Verified / unverified
- Verified: `Tools/ccheck` errors=0 (all six assemblies, with the stand-in). Text widths and heights computed from the
  font files for every fixed label and the data's longest strings.
- **Unverified (no Unity):** everything on screen; the tests; frames and fonts from A. Biggest risk: the Codex's
  3-column entries (Arsenal, Bestiary) hold 4 lines of Body at 18 (~180 characters) — a long Boon or Vow description
  could be cut; the Codex test names the tab, page and label ("Body"). Fallback: `PerPage` 9 → 6 for those tabs (wide
  cards hold ~300).

## Screenshots for the orchestrator (1920×1080 and 1280×720; ★ also 1920×1200 and 1600×1200)
1. Main menu, fresh save (DAILY dimmed, hint beside it) ★, and with DAILY open + the gold dot.
2. Gladiator select with everyone earned, a mid-roster gladiator picked (wreath, ring), a skin shown ★; a locked pick.
3. The Blood Knight's tablet (three traits: the tightest page).
4. Arena select with all arenas earned and two edicts on (the EDICTS row) ★.
5. The Edicts page with two on (danger rings + ON chips), a Market card with BUY, a locked challenge card ★.
6. Daily: scored, and practice with a ledger of 7 (a won day's laurels, today's gold ring) ★.
7. Codex: RECORDS, GLADIATORS, ARSENAL page 1, MARKET (one ★).
8. Options over the main menu and over the pause screen.

## Tunables
`GladiatorTablet.Portrait / Gap / FlowTop / BottomMargin`, `LanistaTablet.Floor / Rim`, `ArenaTablet.ThumbW / ThumbH / LabelW / NamesBudget`,
`ArenaLadder.NameMax / NameMin`, `RosterGrid.NameH / NameW / TopInset / BottomInset`, `SkinStrip.BuyW`,
`OptionsPanel.Size / RowPitch`, `MainMenu.MarbleGoldHex`, `EdictsPanel.RightW`.
