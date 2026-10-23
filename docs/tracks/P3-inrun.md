# P3-B — in-run screens (worker notes, 2026-10-22)

Branch `claude/sleepy-liskov-999cc9` (from `origin/phase5-playtest3` @ cfc734f). Contract: `docs/tracks/P3-contract.md`
"P3-B", items 1-7. **Nothing was run in Unity** (worker rules): `Tools/ccheck` prints `errors=0`; every look, layout and
test below is unverified until the orchestrator runs it.

## What I built

| # | Item | Files |
|---|---|---|
| 1 | **Symmetric layout A.** `LevelUpLayout.For` keeps the cards centred (`ContentX = 0`, `Centred = true`); both columns are 410 × k, hang from `ColumnTop`, sit 24 outside the row (and ≥ 24 inside the screen). The contract's maths: `margin = (w − row·s)/2 − 2·Edge`, `k = min(1, margin/410)`; under 0.7 the row shrinks between 0.7 columns, under 0.6 cards the columns shrink (floor `MinColumns` 0.5). New: `Fit.Cards`, `InventoryLeft`, `PauseCards = 3`, `MinColumns`. `ColumnLeft` is now `RowRight + Edge`. | `Core/Layout/LevelUpLayout.cs`, `LevelUpLayoutTests` |
| 2 | **`InventoryPanel`** (plain class, built and ticked by `LevelUpPanel`): bronze column "YOUR ITEMS" / "hover for details", WEAPONS n/4 (one row of 4 slots) and BOONS n/6 (two rows of 3, centred), 76-unit slots: empty = a dim `Stone` well; filled = `Chip` + 2-unit rarity line + dark inside + `UpgradeIconView` + level badge (Number, Outline, in the chip's ContentRect; "EVO" for an evolution). **`UiTooltip`** (plain class): `Wax` frame, name (Title), "LV n · RARITY" (Body, dust), body (Body, wrapped, rich text), all inside the wax's ContentRect, height fitted to the body, `Place` clamps it 8 units inside the canvas, shown with alpha. | `UI/InventoryPanel.cs`, `UI/UiTooltip.cs` |
| 2 | Tooltips: hovering a slot (`UiHover`); and while a card is selected (keys, gamepad or the mouse) whose upgrade you own, its slot lights (a gold `Halo`) and shows its tooltip. The item tooltip sits **under the inventory column**, as wide as it (never covers a slot or a card). | |
| 3 | **The ghost:** a new weapon/Boon card shows its icon translucent (grey-blue tint, α 0.45, aura off) in the next free slot of its row, with the card's tooltip (Describe + Hints). An evolution card lights the weapon it replaces, with the evolution's tooltip. **StatsPanel's pick preview has fixed rows**: one label per row (title, the line under it, 3 heads, 21 name/value pairs), each at its own place; only the words change. Weapon lines are gone; a supplies card's HP/gold line goes into the line under the title ("if you take Supplies: HP 54 → 100"). | `UI/StatsPanel.cs` |
| 4 | **The pause = the level-up's frame** (`LevelUpPanel` has a third face): "PAUSED" (Shout, white) in the title's place, no subtitle, three ansata plates stacked where the cards stand: RESUME (bronze, `ESC`), OPTIONS (marble, `O`), MAIN MENU (marble, `Q`); clickable (UiHit), the keys as before (Esc toggles in `Player`, O and Q here), plus up/down/W/S + Enter/Space and gamepad d-pad/stick + South, East = resume. The inventory left, the stats column right at a 3-card level-up's fit (no preview): the gladiator's name, "SEED 7F3A-91C2 · hover for sources", every stat's total. Hovering a stat row shows its P1 breakdown (base, cards ×n, synergies, traits, altar, edicts, admin) in a `UiTooltip` left of the column at the row's height; hovering the name shows the gladiator's traits. The old hint text and the HUD's PAUSED label are gone. | `UI/LevelUpPanel.cs`, `UI/StatsPanel.cs`, `UI/Hud.cs` |
| 5 | **HUD.** XP bar 18 → 21 (everything under it 3 lower). The status plate's rows are laid out from its ContentRect: HP (heart + trough + number), the special meter (Blood/Wrath/Charge/Midas/Rage) + its label, gold + kills; the same inset above the first row as under the last. Icons `IconHeart`, `IconAureus`, `IconSkullGalea` via `UiKit.Frame` at 32×32, centred on their trough / number (numbers use `Midline`, so their glyphs are centred). Price tags: `IconAureus` 32×32, the price glyph-centred on it. **Level plate:** `public static string LevelWord = "LEVEL";` (read at Awake), the plate as wide as `LevelWord + " 88"` needs at 50 pt (+ its insets, at least 220), the word's row then the laurel + Favor trough, both inside the ContentRect. The build strip and synergy chips hang 10 under the (taller) plate. The timer hides while paused (PAUSED sits over it, as LEVEL UP! does). | `UI/Hud.cs` |
| 6 | **The banner:** no rotation, no sway (`SwayDegrees` is always 0). A wave travels across the cloth while it drops and settles: `ClothWave : BaseMeshEffect` on the cloth's Image cuts each quad of its sliced mesh into 16-unit columns on a grid from the cloth's left edge and moves each column by `A·env·sin(2π(x/λ − speed·t))` (`bannerWaveAmplitude` × cloth height, `bannerWaveLength` × width, `bannerWaveSpeed`). The words ride the same function at each character's centre x: their TMP vertices are moved from a copy taken when the words changed, then `UpdateVertexData` (`ForceMeshUpdate` only when TMP says its properties changed: a new banner, YOU DIED's restyle). | `UI/Theme/VexillumBanner.cs`, new `UI/Theme/ClothWave.cs` |
| 7 | **Run end:** the wax tablet's seven rows spread evenly over its ContentRect, names from its left edge, values to its right edge (the Renown value from just right of its label). | `UI/RunEndPanel.cs` |
| — | **`TextBox`** (in `UiTooltip.cs`): a one-line label's textBounds height for a role and size, from the theme font's face metrics. TMP's `textBounds` spans each character's **ascent line to descent line** (`TMP_Text.GetTextBounds`), not its glyphs, so the padding rule needs rows as tall as that; every in-run row I built is sized from it at build time (the stats rows shrink 20 → 16 pt until the 21 rows fit the 730 column). | |

## Decisions (one line each)
- **The columns hug the row** ("each column sits Edge outside the row"), not the screen edges: at 1920 the inventory is x 48-458, cards 482-1438, stats 1462-1872; on 21:9 they stay beside the cards.
- **Every fit uses at least 3 cards** (`LevelUpPanel` and `StatsPanel` pass `max(3, n)`): a 1-2 card pick keeps the columns (and the title's room) where they always are; the pause uses the same 3-card fit.
- The title and subtitle are as wide as the row (956 at 3 cards) so a long title never runs over a column (it autosizes, 72-132).
- Inventory: Boons in two centred rows of three (the 6 slots read as one block; a 4-wide grid would leave a ragged 4 + 2).
- Owned Boon tooltips show **totals** (per-level stats and effects × the Boon's potency, the sum of its takes' rarities); a Vow shows its per-level text ("each level"), its price's per-take rarity isn't kept. Weapons: description + "hit 12 · 1.25/s" (the numbers the old pause page listed).
- **The ghost is tinted, not truly desaturated:** UI vertex colours only multiply; real desaturation needs a UI shader asset (not hand-writable here). Grey-blue at α 0.45 with the rarity aura off reads as "not yours yet". Follow-up if the producer wants grey: a `UI/Desaturate` shader + material.
- **Clicking empty space no longer resumes** (contract): the plates are the commands. `Player.cs` still resumes on any left click (needs below); until that goes, OPTIONS re-pauses before opening the options, so every plate already works.
- Gamepad in the pause: d-pad/stick + South, East = resume (gamepad players had no way to OPTIONS or MAIN MENU before).
- Key chips on the pause plates widened to 66 so "ESC" isn't cut; the three labels share that left edge.
- Pause column title = the gladiator's name (as the P1 page); the traits moved into the name's tooltip; the admin buffs' share shows in the stat tooltips (pink), not as a footer.
- **Banner wave envelope:** 1 while dropping, then a third every `bannerWaveDecay` s, cut smoothly to 0 between 2 and 3 × decay after the drop (gone 1.35 s after it with the defaults, 0.77 s before the roll-up). The contract's "fades out before the hold" can't hold literally with a 0.45 s decay and a 0.12 s settle, so I read it as "the cloth hangs still well before it rolls up". For the strict reading set `bannerWaveDecay` ≤ 0.04.
- The wave moves whole columns up/down (the contract's formula), not growing with depth: the crossbar hides the top edge (±8.4 units at the defaults; the bar covers 12 below its centre line).
- Card key label 26 → 24 pt and the description starts at −24 (was −30): the key's row sits on the card's ContentRect bottom and the description keeps ~166 units (174 before).
- StatsPanel's prompt plate 60 → 88 tall: its words fit inside the bronze's ContentRect.
- `Hud.PauseRect / PauseTextWidth / PauseLineCount` (test hooks) now read LevelUpPanel's PAUSED title; `StatsPanel.PageWidth` = 410 (both faces), `FrameShown` = Bronze in the pause, `SetPickScale` only asks the column to re-place (it places itself).

## Zero GC in a live run
Everything is built once; words are written into CharBuffers and handed to pre-sized labels (`SetCharArray` slices); the
inventory redraws on `BuildState.Version`, the tooltip when what it shows changes, the stats column on its key. The banner's
per-frame work is a few float writes, `SetVerticesDirty`, the mesh effect into kept lists and TMP `UpdateVertexData`.
Tests: `InventoryTests.HoveringCardsAndSlots_AllocatesNothing` (a third cycle of hovers over every card and slot, ticked
directly), `UiInRunLookTests.Banner_ASecondDrop_AllocatesNothingPerFrame_AndNeverRotates` (own canvas, Tick +
`Canvas.ForceUpdateCanvases`, logs the bytes of the second Show separately), `BannerTests.TickingAWholeCycle_AllocatesNothing`.

## Tests (written, not run)
- EditMode `LevelUpLayoutTests` (rewritten): the P3 cases 16:9 3/4 cards, 4:3, 5:4, 21:9, 1280×720 raw (3 and 4 cards) with
  exact scales, 1080p's x positions, the 21:9 columns beside the row, and a sweep 1280-2600 × 720-1300 × 1-4 cards:
  centred, mirrored, no overlap, on screen, floors.
- EditMode `BannerTests`: the sway test replaced by never-rotates + the envelope through a banner, `WaveEnvelope` values
  (1, 1/3, 1/9, cut, 0), `ClothWave.Offset` = the formula and travels right, the words ride the wave and lie flat after,
  the cloth's mesh is cut into columns (Inconclusive if the EditMode canvas doesn't rebuild).
- PlayMode new `InventoryTests` (7): every slot shown + the starting weapon + mirror of the stats column; an owned card
  lights its slot with its tooltip; a new Boon's ghost in the first free Boon slot (then taken: fills it); a new weapon's
  ghost in slot 1 and no weapon lines in the stats; hovering a slot shows its tooltip on screen under the column; the
  pause (plates, keys, focus, columns, a stat tooltip, RESUME resumes); hovering allocates nothing.
- PlayMode `StatsPanelTests`: the pause page = the bronze column with the seed (no "WEAPONS"), sources via the row
  tooltip (+ admin share, + traits on the name), new `PickPreview_HasFixedRows_NothingMovesBetweenCards`.
- PlayMode `RunEndP1Tests`: `Paused_SitsLeftOfTheStatsPage_WithoutWrapping` → `Paused_IsTheLevelUpsFrame_PlatesBetweenTheColumns`;
  the synergy-chip test reads `Hud.BoonRowBottom` (the strip moved down with the plate).
- PlayMode `UiInRunLookTests` new: HUD labels/bars inside their plates' ContentRects + icons centred + LEVEL + XP 21 + the
  new icon kinds; card labels inside the card frame's ContentRect; run-end rows inside the tablet's; the banner's PlayMode
  GC check. `LevelUpTests`, `RunEndAndAdminTests`: unchanged (nothing they read changed).
- Not changed but reads my code: `DailyMenuTests` (the pause page's text still contains "SEED …").

## Needs from others
- **Orchestrator (Gameplay):** remove `Player.cs` lines 67-69 (a left click while paused resumes) — contract item 4.
- **P3-C (`UiAudit`):** labels that sit on small frames by design can't meet the padding rule; please skip or `Exempt`:
  the HP number on its 32-tall trough (`Hud.HpLabel`), the HUD strip's level badges and letters on 52-unit `Chip` tiles,
  the synergy chips' names (`Chip`, 30 tall), the DAILY chip, every `KeyChip` letter (UiKit's), and the cards' rarity
  band label (dark ink on a light band that is a plain Image, not a frame: the audit sees the card's dark surface).
- **Orchestrator:** new scripts have no `.meta` yet (`InventoryPanel.cs`, `UiTooltip.cs`, `Theme/ClothWave.cs`,
  `Tests/PlayMode/InventoryTests.cs`): Unity makes them on import. No scene rebuild: nothing new is serialized.
- Screenshots for the producer's LV/LEVEL choice: set `Hud.LevelWord = "LV"` before the arena loads.

## Tunables
`LevelUpLayout` (Edge, MinStats, MinCards, MinColumns), `InventoryPanel` (SlotSize 76, SlotGap 16, IconSize 60, the
ghost's tint), `StatsPanel` (RowSize 20 → MinRowSize 16, SectionGap 8), `UiTooltip` (sizes, MaxBodyHeight 420),
`Hud` (IconPx 32, row gaps, LevelMinW 220, `LevelWord`), `UiTheme.bannerWave*`, `ClothWave.ColumnWidth` (16).
