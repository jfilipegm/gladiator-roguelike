# P3-art: frames and icons (the producer's v0.24.0 UI notes 1, 6, 10, 11, 13)

Contract: `docs/tracks/P3-contract.md` "P3-art". Everything is drawn in code by `PixelSrc/ui_frames.py` (no third-party art;
the producer's images 14 and 15 were looked at for the idea only). Run `python3 PixelSrc/ui_frames.py --preview`.

## What I drew (index lines appended in `FrameKind` order, 37 lines, atlas still 512×128)

| Kind | Art px / border / type | What it is | Iterations |
|---|---|---|---|
| `AnsataGlow` | 34×26 / 12 2 12 2 / sliced, no centre / padding 0 | `ansata()`'s silhouette (now `ansata_mask()`, shared with both plates) placed 2 px in and dilated by 2 px; only the ring: inner row (distance < 1.5 px) white opaque, outer row (< 2.3 px, so the outer corners round off) white at 45 % (alpha 115). Fills the V between the plaque and each handle, so it follows the plate's outline exactly. | 1 |
| `SealNew` | 12×12 / simple | Crimson wax seal: an irregular round (bulge up-right, flat spot low down), a dark edge, a raised rim lit top-left (`sealHi`) and dark bottom-right, one shine pixel, a 4×6 gold N (cols 4-7, centred) whose strokes cast a 1 px dark shadow down-right into the wax. | 2 (v1: the N was 5 wide, lopsided and off-centre) |
| `ArrowLeft` / `ArrowRight` | 16×12 / simple | Bronze pilum: a barbed head (8 px tall at its base), the pilum's long thin iron shank, a short socket with a dark ring at its mouth. Ridge along the middle: lit upper facets, dark lower ones. Right = mirror of left. Vertically centred (rows 2-9). | 3 (v1 read as a cross, v2 as a fish/diamond at 48×36; v3 reads as ← / → at 48×36) |
| `IconHeart` | 16×16 / simple | Chunky heart in the `hp` reds, dark outline, a shine and light-red highlight top-left, `hpDark` on the lower right. Box rows 2-14: half a pixel low on purpose (the optical centre; a heart's mass is up top). | 1 |
| `IconAureus` | 16×16 / simple | A Roman aureus: round flan, dark edge, a **beaded rim** (alternating light/dark round the ring), a laureate head facing right in relief (laurel leaves round the crown, eye, jaw line, wreath ties at the nape; the head's top-left edges catch the light, a shadow falls down-right on the field). | 3 (v1 keyhole, v2 horse-like mane; v3 accepted: reads as a gold coin with a head in relief; the profile is suggestive at 32×32, not a portrait) |
| `IconSkullGalea` | 16×16 / simple | Straight, symmetric bone skull (2×2 sockets, nose hole, teeth row) in a steel galea: bowl with a gold crest holder, gold brow band, steel cheek guards, a **red crest** fanning out (wider at the top, bristle lines). Rows 1-14 (centred). | 3 (v1 crest a flat block; v2 re-centred; v3 fan crest) |
| `LaurelWreath` (redraw, same 60×30, same U envelope, same line) | 60×30 / simple | The U1 branch pair, now gilded laurel: `laurel()` takes a `tones` table; the wreath's leaves are gold where lit (`wreathHi`, `wreathGold`) and olive green in the mids and shadows (`wreathOlive`, `wreathOliveLo`), a dark olive outline; a small red bow (two loops, the knot, two tails) tied where the stems meet, centred on x = 30. | 2 (v1's bow read as a heart) |

Every other frame is pixel-identical to v0.24.0 (checked crop by crop against the old atlas; the packer moved some cells,
the index has the new positions). New colours are in `SHADES` (wreath, ribbon, seal, heart, coin, bone, steel, crest).

## Review
- Preview (`Screenshots/ui/ui_frames_preview.png`, git-ignored; a copy in the session notes folder as
  `p3_art_ui_frames_preview.png`): the flow grids show every new kind at two sizes on `night` and on the arena mid-tone;
  a new **P3 band** on each tone (`preview_p3`) shows the glow (gold) round AnsataBronze and AnsataMarble at 360×64,
  300×80 and 200×48 (an Image of the plate's size + 12), the seal on an ARSENAL tab's top-right corner (36) and beside
  a Codex entry (24), the pager arrows at 48×36 resting (0.82 grey) and hovered (white), the HUD row on a bronze plate
  with each icon at 32×32 beside its trough / number, the icons alone at 32 and 48, the wreath on a scutum's top
  (120×60) and behind a legendary card's icon (180×90). Crop: `p3_art_p3_band.png`; every new asset at ×8 on both
  tones: `p3_art_assets_x8.png`.
- The glow lines up exactly at all six plate sizes (zoomed crops: no gap, no overlap with the plate's outline; the
  stretched rows thicken the diagonal ring the same way they stretch the ansata's own handles).

## Gallery (`UiGallery.cs`)
- `SizesFor`: AnsataGlow 252×78 and 312×92 (round 240×66 and 300×80 plates; the second tinted gold like Focus), SealNew
  36 and 24, arrows 48×36 and 32×24, the three icons 48 and 32.
- Frames page 2 ends with the **P3 row** (`BuildP3`, `BuildFrames` now returns the y under its last row): focused
  bronze (PLAY) and marble (CODEX) plates whose `Focus` Image is switched to `AnsataGlow` at plate + 12 (a no-op once C's
  `UiKit.Plate` does it itself), a Codex-style ARSENAL tab with the seal on its top-right corner, both arrows round
  "1 / 3", and the HUD row on a bronze plate: heart + trough (70 %) + "86 / 120", aureus + "1,250", skull + "2,417",
  icons at 32×32, every piece centred on one line. New public `P3Plates` (the two focused plates and the tab); the
  page name gains ", P3". Still 4 pages (UiGallerySetup's log says 1-4). Simulated layout of page 2: rows end at
  y −886; the P3 row spans −886 to −1012 (the page label sits below −1040).

## Decisions
- **One glow for both plates** (as the contract and `FrameKind` have it; P3-notes' table still says
  AnsataGlowBronze/Marble): the bronze and marble ansatas share one silhouette, so one white ring tinted by
  `FocusColor` serves both.
- **Pilum proportions:** a broad barbed head + long thin shank + short socket instead of a long narrow head: the long
  head read as a diamond at 48×36 and the pager needs the direction first; the shank and socket keep it a pilum.
- **Wreath tones:** gold on the lit half of each leaf, olive on the shaded half, so it reads gold first and laurel
  second (the producer's "gold laurel (green tones?)").
- **Icon centring:** the heart's box sits half an art px low (optical centre); coin and skull boxes are centred; arrows
  centred on rows 2-9. B can centre each 32×32 cell on its trough / number line.

## Verified / unverified
- **Verified:** `Tools/ccheck` errors=0; the index has 37 lines in the enum's order, inside the 512×128 PNG, no overlaps,
  simple kinds borderless, borders inside their rects (a Python replica of `UiThemeTests.ShippedIndex_…`); unchanged
  frames pixel-identical; every asset reviewed at 1:1 (×8–×16 zoom) and at in-game size on `night` and the mid-tone.
- **Unverified (no Unity in a worker):** the sprites in the Editor, `UiThemeTests` actually run, the gallery's P3 row
  on screen, the glow on real `UiKit` plates (C wires it).

## Needs from others
- **C (UiKit):** `plate.Focus` → `UiFrames.Apply(focus, FrameKind.AnsataGlow)` at the plate's size + (12, 12), as the
  contract says; the art is drawn for exactly that.
- **C (roster / arena grid):** the picked roster tile and the beaten arena still use `PixelIcons.Wreath` (a 12×9 code
  sprite); the redrawn wreath is `FrameKind.LaurelWreath`. Show it at whole-pixel scales (120×60 = 2 units per art px,
  or 180×90) so the point filtering stays crisp.
- **C (arrows):** the art is full-bright; "brighter when hovered" = tint about 0.82 grey at rest, white on hover (the
  preview shows both). SealNew: 36×36 on tabs and plates, 24×24 beside entries (whole pixels).
- **B (HUD):** the icons at 32×32 via `UiKit.Frame` (Simple); no change needed in the art for price tags (48 or 32).
