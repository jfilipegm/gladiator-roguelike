# P3-C — menus, kit and the audit (worker notes)

Branch `claude/mystifying-ritchie-d63be1` (from `origin/phase5-playtest3` @ cfc734f). Contract: `docs/tracks/P3-contract.md`
"P3-C". No Unity was run: `Tools/ccheck` prints `errors=0` (0 warnings); every test below is written, none run.

## What I built

### The kit (`Theme/UiKit.cs`)
- **The focus glow (note 1):** `UiKit.Plate`'s `Focus` is now an `AnsataGlow` Image of the plate's size + `UiKit.GlowGrow`
  (12, 12), centred, tinted by `FocusColor` (alpha 0 until Focused). `SetState` unchanged. Every plate in the game gets it
  (menus and B's in-run plates).
- **Plate labels inside the padding:** the label's box now spans the plate's `ContentRect` width (it was the frame less the
  padding; 16 units narrower now), so autosize keeps the words inside. The key chip's letter is `Midline`-centred and
  padding-exempt (a keycap).
- **New members** (all additive): `GlowGrow`, `HasLook(label, look)` (Style now records the look beside the role),
  `SealSize` 36, `Seal(name, parent, size)` (a hidden `SealNew` Image), `PlaceSeal(seal, plate, label)` (top-right corner,
  over the last letter's ink), `Arrow(name, parent, right, anchor, pivot, pos, hit, onClick)` (a pilum tip on a clear hit
  area; rest 0.78, hovered 1), `RowFocus` + `Row(...)` + `BandLight` (a list row's focus: a soft band and an `ArrowRight`
  marker at 2 units/px pointing at the row's left edge; `Show(bool)` only enables).
- `ContentPad` / `ContentRect` untouched.

### The audit (`Theme/UiAudit.cs`, new) — the contract's API plus a few helpers
`MinContrast`, `SurfaceTone(kind)`, `Contrast(a, b)`, `PaddingViolations(root, into)`, `ContrastViolations(root, into)`,
`Exempt(label)`; plus `ExemptPadding(label)`, `IsExempt`, `Luminance`, `ToneOf`, `SurfaceOf(label, out kind)`,
`TryInkBounds(label, out rect)`, `Outlined(label, out colour)`, `Labels(root)`, `Slack` 0.5, `PlainSurfaceAlpha` 0.6.
- Tones from `ui_frames.py`'s face middles: AnsataBronze `#B07A3B`, AnsataMarble / Marble `#ECE7DE`, Bronze `#3A2819`, Wax
  `#251E19`, Mosaic `#211915`, Stone `#26201C`, the four card frames `#1C1714` (cardInside: they have no centre), Scutum
  `#8A2A2C`, ScutumLocked `#45484E`, VexillumCrimson `#822329`, VexillumPurple `#371E45`, Trough `#1A120C`, TroughFill
  `#D9D9D9` (× its tint, the bar's colour), Chip `#5A3A1B`, KeyChip `#2A180A`; everything else alpha 0 (rims, glows, meanders,
  laurels, the clipeus' window, seals, arrows, icons).
- Failure lines read `parent/label: why`, e.g. `Tablet/Low: the text leaves Wax 'Tablet''s content rect by 34 at the bottom
  ("Starts with Rusty Gladius")`, `Wax/Dark: 3D342B on Wax 'Wax' (251E19) is 1.2:1 ("Left / Right: tab")`.

### The menus
- **Main menu:** the title without the mosaic box (note 3): "GLADIATOR" Title 92 gold, Outline, `UiKit.Laurels` either side,
  a gold meander rule under it as wide as the word, the line (Body 36, Outline) under that. RENOWN in `marble` on a taller
  bronze plate (340 × 86) with both words inside its content rect, ink-centred (note 2). The CODEX plate wears the seal
  (note 6; the label stays "CODEX"). The version line is outlined (it's over the scene). EDICTS' "EDICTS" uses a darker
  orange-red on marble (`MainMenu.MarbleHurtHex` `#B8361C`, 4.3:1; `#FF5433` is 2.6:1 there).
- **Gladiator select:** tiles have no rings (note 10). Picked = the `LaurelWreath` (2 units per art px, 120 × 60, crowning
  the shield) + a 4-unit lift + a brighter face (1/1.1 → 1); hovered = a brighter face. NEW = the seal on the shield's
  top-right corner. The grid starts `RosterGrid.WreathRoom` (36) lower so the wreath clears the key line; it scales with
  `MainMenu.RosterScale` (MenuLayout.GridScale with that room).
- **Skin strip:** pilum arrows, no plates (note 11); the strip 120 tall so the name and the status sit inside its content
  rect; BUY 44 tall.
- **Arena select = the arena grid (note 12):** `ArenaLadder` keeps its name, `Step`, `Build`, `Tile(i)`, `Button(i)`,
  `HasWreath`, `NameLabel`, `Climb` (reading order). Tiles 216 × 250 in a `Bronze` frame, `Columns` 3 (6 arenas = 2 rows):
  the 192 × 108 thumbnail at 1:1 in a wood rim, the numeral (Title 26, in a 60 × 30 laurel once its boss has fallen), the
  name (Title 22 → 18, two lines, one size for all tiles). Open = `Marble` tinted with the arena's palette family lightened
  35 % (marbleInk ≥ 3.85:1 on every family); locked = `Stone`, a dark thumbnail, the padlock, "???". Picked = brighter +
  lifted, hovered = brighter. Arrows / d-pad move in 2D (`MainMenu.MoveArena(dx, dy)`, `GridMath.Step`); the key line says
  "Arrows / d-pad: move". `ArenaLadder.FrameSize / GridScale / GridRight / Cell / Inset / DioramaRoom` hold the maths (it
  shrinks to keep 300 units of diorama clear: 0.95 at 1024 × 768, 1 at 1080p). `RingColor` is gone (allowed).
- **Edicts page:** no focus ring: the focused card brightens (a soft band over the stone) and lifts 4, a hovered card
  brightens; the orange-red ON ring stays. The daily's read-only page (note 5): no ON chips, no OFF / LOCKED / progress, no
  padlocks; the day's edicts keep their ring and say `EdictsPanel.TodaysEdict` ("TODAY'S EDICT", orange-red). Cards' words
  one line each inside the stone's content rect; the right column 270 wide so "BUY · 250 RENOWN" fits its plate.
- **Daily screen:** re-laid by content rects (note 4's "Starts with Rusty Gladius" was the gladiator card's line): cards
  400 × 290, the edicts block 190 tall with one-line rows (icons 40 = 20 px at 2×), the Renown / attempt / clock stack
  below; the ledger's today line is a `RowFocus` (band + marker), not a gold ring; the edicts block brightens and lifts when
  hovered.
- **Codex:** tabs carry the seal over their last letter, no meander under the active tab (notes 6, 9); the pager's arrows
  are pilum tips (note 11); the bottom row's page number and key line are Flat on the dim (note 8: outlined thin Marcellus
  read dark); entries are laid from their content rect (kind and title at the top, the footer at the bottom, the bar / BUY
  above it, the body between; note 7); a new entry wears a 24 seal on its corner; the Market's keyboard focus is a warm band
  + the marker. The roster tab leaves room for the wreath.
- **Options:** the focused row is a band + the marker (no ring).
- **Tablets:** `LanistaTablet.Inset` (the wax frame's padding + ContentPad − the rim = 11); the gladiator page's flowing rows
  stop there; the arena tablet's boss laurel is the redrawn `LaurelWreath`.

### Tests
- **New PlayMode `UiAuditTests`:** main menu (fresh, CODEX focused, seasoned), gladiator select (every slot fresh and earned,
  every skin on the strip), arena select (every arena earned; every tile on a fresh save), the Edicts page (none on; two on
  with the focus moved; closed), the Daily screen (scored, practice) and its read-only Edicts page, every page of every
  Codex tab + the Market's focus, Options; in a run: the HUD, the level-up with 3 and with 4 cards
  (`levelUpCardDelta`), the HUD with an upgrade, the pause, the boss reward, both end screens. Each asserts 0 padding + 0
  contrast violations over every visible root canvas and lists them. The HUD's debug line is exempted in the test (a dev
  readout).
- **EditMode `UiKitTests`:** the glow (kind, size, colour), the plate label inside the content rect; UiAudit's contrast
  maths, the light/dark tone split, surfaces (last drawn under the words, siblings, plain dims, see-through dims), padding
  (the note-4 case), contrast (dark on wax, bare over the scene, outlined, a rich-text colour).
- **Updated:** `RosterLayoutTests` (the arena grid fits 1-12 arenas on every screen size and keeps the diorama clear; the
  roster + wreath fits; the grid's cells), `RosterTests` (the ladder test is now `TheArenaGrid_…`: climb order, 2D moves,
  frames, the laurel), `UiMenuLookTests` (meander under the title, glow, seals, no rings / wreath / lift / brighter face,
  pilum arrows, the arena grid, no tab meander, Options' row focus), `CodexPlayTests` (`CodexSealShown`, tab seals; the
  text stays "CODEX"), `EdictMenuTests` (the read-only page's words, chips, padlocks; the lifted focus).

## Decisions (one line each)
- **"Rendered text" = the glyphs' ink, not `textBounds`.** TMP's textBounds spans the font's ascent-to-descent line
  (Marcellus: 0.97 em above the baseline, its capitals reach 0.71), so it flagged plates whose words visibly clear the
  frame; ink is what the producer saw touching the edge.
- **A label's surface = the last-drawn surface under its text centre** (the contract said the smallest): the same in every
  nested case, but right when pages overlap (the Edicts page over the arena grid, whose tiles are smaller than its cards).
- **Plain colour Images ≥ 60 % opaque are surfaces** (the Codex / Edicts / Options dims, icon plates, the ON chip): words on a
  dim aren't "over the scene"; their tone is their colour. They have no padding rule.
- **Every visible character's colour counts** (rich-text colour tags), and an outlined label also passes as a sticker
  (outline vs surface ≥ 3 and face vs outline ≥ 3): goldBright Number stickers on marble read.
- **Alpha is ignored** in contrast (disabled plates and the read-only page's dimmed cards are meant to recede).
- **The roster's names are padding-exempt** (`ExemptPadding`): the Scutum's padding (9 × 12 art px) is its curved corners;
  the name band sits on the shield's straight lower part inside the 3-px rim. Contrast still checked. See Needs.
- **Key chips are padding-exempt** (keycaps: the letter fills the chip).
- **The Codex holds 6 narrow / 4 wide entries a page** (was 9 / 6): with the padding rule the old cards had room for two
  body lines; fewer, roomier cards beat smaller text.
- **Tiles focus by brightness + lift, rows by a band + pilum marker** (contract); on dark stone "brighter" is a white 8 %
  band (a tint can only darken).
- **The wreath at 2 units per art px on the shield, 1 unit on the arena numeral and the tablet** (whole screen pixels at
  1080p; the numeral row and the tablet line have room for 60 × 30 only).
- **Darker orange-red on marble** (`#B8361C`) for the EDICTS plate's word and the Edicts page's footer count: the colour
  language holds, the contrast reaches 4.3:1.
- **The arena key line** changed to "Arrows / d-pad: move" (the grid moves in 2D; "Up / Down: climb" would be wrong).

## Verified / unverified
- **Verified:** `Tools/ccheck` errors=0, 0 warnings, after every commit. Label fits computed offline from the TTFs'
  metrics (a TMP-layout emulation without kerning, so a little conservative): e.g. "AMPHITHEATER" 141 at 18 in a 152 name
  box, "NEED 4700 MORE" 165 at 18 in a 184 label box, every edict effect line ≤ 351 at 20 in 383.
- **Unverified (no Unity):** every test; how the screens look (the new art draws plain white until P3-art lands, as the
  contract says); exact TMP autosize results; whether the Codex's longest bodies fit 5-6 lines; the 4-card test depends on
  `levelUpCardDelta` adding a card.

## Needs from others
- **Orchestrator:** no scene rebuild needed (every menu builds at runtime; MenuSceneBuilder untouched). Run
  `UiAuditTests` after both merges: the in-run cases are written against today's screens and will list B's labels until
  P3-B lands. `UiKitTests` reads `GlowGrow`; `UiThemeTests`' frame-count check is art's.
- **P3-art (optional):** `Scutum` / `ScutumLocked` padding `4 4 4 4` (rim + 1 px) instead of `9 12 9 12` would describe
  where the shield's content can go; then RosterGrid's `ExemptPadding(name)` can go.
- **P3-B (FYI):** plate labels are now as wide as the plate's ContentRect (16 units narrower); a plate needs ≥ 44 units of
  height for an 18-unit label to sit inside its padding (AnsataBronze/Marble pad 6 + 8 top and bottom). Plates get the glow
  from UiKit. `UiAudit.Exempt(_hud.DebugLabel)` could move into Hud.cs (the test exempts it today).
