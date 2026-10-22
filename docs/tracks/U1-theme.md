# U1-A — theme, frames, fonts API (worker notes)

Worker U1-A, branch `claude/musing-hertz-4ba9af` (from `origin/phase5-ui` at 4298b61). Contract:
`docs/tracks/U1-contract.md` ("U1-A"); spec: `docs/tracks/U1-design.md`. No Unity was run: C# checked with the compiler,
art rendered and reviewed in Python, tests written but not run.

## Files

| File | What |
|---|---|
| `PixelSrc/ui_frames.py` | Draws the 30 frames (PIL), packs `UiFrames.png` (512×128, 2 px gutter), writes `UiFrames.txt`; `--preview` renders `Screenshots/ui/ui_frames_preview.png` |
| `Assets/Art/Textures/UI/UiFrames.png`, `UiFrames.txt` | The atlas and its index (generated; committed) |
| `Assets/Scripts/UI/Theme/UiTheme.cs` | `TextRole`, `TextLook`, `RoleFont`, `UiTheme` (ScriptableObject, `Current`, `Reload`, `CreateDefault`, `Role`, `FontFor`, `MaterialFor`, `SpacingFor`, `Glyphs`, `Symbols`) |
| `Assets/Scripts/UI/Theme/UiFrames.cs` | `FrameKind`, `FrameEntry`, `UiFrames` (`Get`, `TypeOf`, `NativeSize`, `Padding`, `Border`, `FillsCenter`, `Apply`, `KindOf`, `ParseIndex`, `Reload`, `KindCount`) |
| `Assets/Scripts/UI/Theme/UiKit.cs` | `ButtonStyle`, `UiState`, `UiPlate`, `UiKit` (`Text`, `Style`, `Frame`, `Plate`, `Button`, `Bar`, `Meander`, `Laurels`, `Rule`, `FitScale`, `HasRole`, `TruncatedLabels`) |
| `Assets/Scripts/UI/Theme/VexillumBanner.cs` | `BannerCloth`, `BannerPhase`, `VexillumBanner` |
| `Assets/Scripts/UI/Dev/UiGallery.cs` | The gallery (4 pages) |
| `Assets/Editor/Setup/UiThemeSetup.cs` | `Gladiator/Setup/UI/All`, `Import UI Frames`, `Create Font Assets`, `Rebuild Font Assets`, `Create UI Theme` |
| `Assets/Editor/Setup/UiGallerySetup.cs` | `Gladiator/Setup/Dev/Build UI Gallery` → `Assets/Scenes/Dev/UiGallery.unity` (not added to the build) |
| `Assets/Tests/EditMode/Gladiator.Tests.EditMode.asmdef` | references + `Gladiator.UI`, `Unity.TextMeshPro`, `UnityEngine.UI` (exactly the contract's list) |
| `Assets/Tests/EditMode/UiThemeTests.cs`, `UiKitTests.cs`, `BannerTests.cs` | The tests below |

No `.meta` files were written: the orchestrator's Editor makes them on import.

## How it works

- **Theme.** `UiTheme.Current` = `Resources.Load<UiTheme>("UiTheme")`, cached with Unity's null check; with no asset, a
  `CreateDefault()` instance (spec §3 colours, banner timings, spacing Title +4 / Number +2 / Shout +1, no fonts → TMP's
  default font). `FontFor` = role font ?? plain font ?? TMP default. `MaterialFor` = the look's preset ?? the role's flat
  ?? the font's material; a role without a font uses the **plain role's** materials (a material must match the font it
  draws). `SpacingFor` is 0 for a borrowed font. `Reload()` also drops the frame cache.
- **Frames.** `UiFrames.txt` is parsed once per index asset (`name x y w h bl bb br bt type pl pb pr pt`, art px, x y
  from the top-left). `Get(k)` cuts the sprite on first use (`Sprite.Create`, y flipped, pivot centre, ppu 100/3,
  FullRect, the index border), caches it by kind, `DontUnloadUnusedAsset`; it rebuilds if the theme's atlas changes.
  No atlas or no line → `PixelIcons.White`, warned once per kind. `Apply` sets sprite, type, `fillCenter` (false for
  the four card frames and Focus) and `pixelsPerUnitMultiplier` 1, and remembers the kind (below).
- **Kit.** `Text` = `UiFactory.Text` + NoWrap + `Style` (font, material, spacing, `FontStyles.Normal`, Truncate,
  raycast off). `Plate` = ansata frame, then **Label first** (Title, Engraved, autosize down to 75 % / floor 18), then the
  optional `KeyChip` (+ its `Key`, Number) left of the label, then the `Focus` ring 6 units outside, hidden.
  `SetState` = colours and alpha only. `Bar` = Trough + a Filled TroughFill **stretched** inside the trough's padding.
  `Laurels` = two branches `gap` outside `textBounds`, vertically centred, `NativeSize × max(0.75, fontSize / 48)`,
  the right one at scale.x −1; calling it again re-places the same pair.
- **Banner.** Root (pivot top-centre at `pos`) carries a nested `Canvas` (inherits sorting, no raycaster) and a
  `CanvasGroup`; a `RectMask2D` slot whose top is the bar's centre line holds the cloth (pivot top-centre); the bar is
  drawn last. Cloth y = (1 − Down01) × 1.04 × height; rotation about its top-centre. Timeline as the contract
  (drop ease-out to 1 + overshoot, settle via 1 − 0.4 × overshoot, sway keys 1, −0.64, 0.29, 0 inside the hold, roll
  ease-in). `Show` while visible: quick roll from wherever the cloth is (old words), then the new one drops with the
  carried-over time. The group's alpha = clamp01(Down01 × 4), so the crossbar fades in with the drop and out with the
  roll and nothing shows when Hidden. Tick only writes anchoredPosition / localRotation / alpha when they change.
- **Art.** One palette dict (spec §3 tokens + named derived shades) at the top of the script. The preview rebuilds
  UGUI's sliced/tiled drawing in PIL at 3 units per pixel and uses the producer's TTFs for the sample words.

## Decisions (one-line reasons)

1. **`Symbols` drops ∓ ◀ ▶** (now `×·—–…→←↑↓’“”•−°²÷±≈√Σ■`): no shipped font has them (LiberationSans neither,
   checked with fontTools) and no UI string writes them (only comments/tooltips); keeping them would fail the glyph
   test forever. Every symbol a UI string writes today (° ² · × – — ’ “ ” • … → −) is in every theme font or FN_Symbols.
2. **A resting plate face is 1/1.1 grey** (`UiPlate.FaceNormal`): UI tints only darken (vertex colours clamp at 1), so
   "Focused ×1.1" and the menu Button's 1.15 highlight can only show from below white.
3. **`Style` keeps the label's wrapping but forces Truncate** ("the same way" as `Text`, minus NoWrap, which would break
   restyled descriptions).
4. **`KindOf` / `HasRole` remember what `Apply` / `Style` set in weak tables** (`ConditionalWeakTable`): they work before
   the atlas exists (every missing frame is the same white sprite) and need no new component file.
5. **Index changes from the contract's targets** (names, types and uses unchanged): Mosaic 25×25, borders l9 b8 r8 t9
   (a 4 px tessera lattice whose 8 px edge pieces tile seamlessly); Trough border 2 (a 3 px rim left too thin a fill);
   the cloths' bottom border 14 (deeper swallowtail points + tassels); LaurelBranch 20×24 (drawn from its own curve: the
   mockup's branch clumped into a smear at 16 px). Paddings: ansata 10/2, marble 8, bronze 7, wax 8, mosaic 10, stone 5,
   card = border, scutum = border, clipeus 6 (its ring), cloth 6/14/6/5, trough 2, chips 2.
6. **Laurel is gold, not green** (spec §3: gold = laurel). The HUD's Favor icon (`PixelIcons.Laurel`,
   green) is B's to change or keep.
7. **Existing font assets are kept** by `Create Font Assets` (no GUID churn on re-runs); `Rebuild Font Assets`
   regenerates them. Presets are updated in place (GUIDs stay).
8. **FN_Symbols has no presets**: TMP draws a fallback glyph with the primary font's material look (it builds that
   fallback material once per pair and caches it).
9. **The licence of LiberationSans** (FN_Symbols' source) is copied too, as `LiberationSans-OFL.txt`.
10. **The gallery has four pages** (frames ×2, type and plates, cards): one 1080-high screen can't hold every frame at
    two sizes under the banner.
11. **Extra members** (allowed by the contract): `UiTheme.CreateDefault`, `SpacingFor`; `UiFrames.KindCount`, `Border`,
    `FillsCenter`, `Reload`; `UiPlate.KeyChip`, `FocusColor`, `LabelColor`, `KeyColor`, `FaceNormal`…; banner
    `(parent, anchor, pos, width, height)` overload, `Words`, `ClothImage`, `Bar`, `Replacing`, `CrimsonInk`, `PurpleInk`;
    `UiGallery.ShowPage`, `Page`, `Banner`, `Plates`.

## Art review (preview: `Screenshots/ui/ui_frames_preview.png`, a copy in the session notes folder as `u1a_ui_frames_preview.png`)

Three iterations. 1: all 30 drawn; the ansata read as a candy wrapper, the cloth's hem was a shallow zig-zag with
heavy fold stripes, the crossbar's collars sat in the stretched centre, the laurel branch clumped. 2: the plaque's
sides drawn through the handles (handles one band darker) → reads as plaque + dovetails; 14 px hem with real points
and tassels, faint 1 px creases; collars moved into the side borders; laurel redrawn from its own curve. 3: laurel
leaves separated (later leaves edged darker where they touch). Shipped.

What I'd still improve: marble veins only live in the corners (the centre is flat by design); the laurel at 2.75×
(beside VICTORY! at Shout 132) is chunky — a 2× resolution branch appended as a new kind would fix it; an ansata at 40
tall squashes its handles to 1.8× vertically; the purple cloth's creases barely show.

## Tests (written, not run)

- **UiThemeTests:** `Current` never null; `CreateDefault` colours = spec §3 (9 tokens), every role's `FontFor` = TMP's
  default, banner/spacing defaults; `MaterialFor` falls back to the plain role, then its flat preset; `ParseIndex`
  (comments, blank lines, CRLF, padding spaces; three bad lines warned and skipped); the shipped index: one line per
  kind in enum order, inside the PNG (read from its header), borders inside rects, simple = no border, no overlaps.
  With the theme asset (else Ignore): every sprite's border, ppu 100/3, flipped rect, type and native size; each of
  Number/Shout/Title/Body: font static, first fallback `FN_Symbols SDF`, `HasCharacters(Glyphs + Symbols, searchFallbacks)`
  (the failure lists missing glyphs with code points), three presets on the font's atlas; the four licences in
  StreamingAssets.
- **UiKitTests:** `Text` per role/look (font, material, spacing, Normal, NoWrap, Truncate, raycast off, `HasRole`);
  `Style` keeps wrapping; plates: bronze/marble, label first TMP child, key chip only with a key and left of the label,
  ring hidden; `Button`: target graphic, `UiFactory.Label`, navigation None, click; `SetState`: ring only when Focused,
  brighter/darker/faded, 0 B over 100 calls (after a warm-up); `Laurels` outside the text bounds, centred, sized, right
  mirrored, re-call doesn't duplicate; `FitScale` (0.985 / 1 / 0.905); `TruncatedLabels` finds the cut label and skips
  the hidden (group α 0), clear (α 0) and inactive ones.
- **BannerTests** (dt 1/60): phases in order and Hidden at `TotalSeconds()` ± 2 frames; peak `Down01` = 1 + overshoot
  ± 0.01; |sway| ≤ degrees + 0.01, 0 after the sway, and it does sway; longer seconds lengthen, shorter don't shorten;
  Show while Holding → quick roll with the old words in `bannerQuickRoll`, then the new words, cloth and ink; Hold stays
  10 s, `RollUp` hides after `bannerRoll`; Clear; 600 ticks over a whole cycle allocate 0 B.
  The timeline was checked numerically with a Python port of `Tick`: every expectation holds within one frame.

## What the orchestrator runs in Unity

1. (Already done, outputs committed) `python3 PixelSrc/ui_frames.py --preview`.
2. `Gladiator/Setup/UI/All` — imports the atlas, makes `Assets/Art/Fonts/TMP/FN_*.asset` + `- Flat/Outline/Engraved.mat`,
   copies 5 licences to `Assets/StreamingAssets/Licenses/Fonts/`, creates `Assets/Resources/UiTheme.asset`. Read the
   log: any "not in the font (left to the fallbacks)" line is expected for symbols a family lacks; an "atlases" warning
   means a font overflowed 1024².
3. `Gladiator/Setup/Dev/Build UI Gallery`, then Play; `Tab` or `1`–`4` switch pages (or
   `Object.FindFirstObjectByType<Gladiator.UI.UiGallery>().ShowPage(n)` from `Tools/ueval`).
4. EditMode: `UiThemeTests`, `UiKitTests`, `BannerTests` (read the missing-glyph list if a font test fails).
5. Tune the text looks in the gallery if needed: the constants at the top of `UiThemeSetup` (outline width 0.2, face
   dilate 0.1, drop α .9 / y −0.6 / dilate 0.2, engraved α .5 / y −0.35), then `Rebuild Font Assets` is not needed —
   re-running `Create Font Assets` updates the presets in place.

**`Tools/ccheck` and this branch:** ccheck compiles `Gladiator.Tests.EditMode` from the main checkout's
`Library/Bee/*.rsp`, which predates this asmdef change (no Gladiator.UI / TMP / UGUI references), so stock ccheck reports
errors in the three new test files until the main Editor has compiled the merged asmdef once. With those three
references appended (`u1a_ccheck_plus.py` in the session notes folder: ccheck plus that one change) every assembly
compiles: **errors=0, 0 warnings**. Core, Gameplay, UI and Editor pass stock ccheck. Suggested follow-up for the
orchestrator: let ccheck add asmdef references missing from the rsp.

## For U1-B and U1-C

- **Text:** `UiKit.Text` sets NoWrap; set wrapping/autosize after it. `UiKit.Style` keeps wrapping and sets Truncate.
  Bands with +12 spacing: set `characterSpacing = 12` after `Text`.
- **Plates:** `Plate` has no Button; `SetState(Normal)` is applied at build (face 0.91 grey). Change `plate.FocusColor`
  (e.g. crimson) before `SetState(Focused)`. `plate.Label` is the first TMP child; `plate.Key`/`KeyChip` only with a key.
- **TruncatedLabels / textBounds / Laurels** need the labels under a Canvas (TMP lays out only there).
- **Banner:** `new VexillumBanner(parent, top-centre, pos)`; for YOU DIED use the overload with `height` (≈ 180 for
  Shout 96 words in a 900 cloth: the text box is height − 57), set `CrimsonInk = theme.ink` before `Hold`, and
  `Words.fontSizeMax = 96` (`Words.fontSizeMin` 36). `Replacing` = a quick roll-up is in progress before a new drop.
- **Card frames** are rims (tiled, no centre): put a `cardInside` image inset 6 under the frame; the band sits 9 in from
  the top, 40 tall; the frame's inset is 9 / 9 / 9 and the foot 24. The selected ring: `Frame(Focus)` at card size +12.
  `Halo` is white with soft stepped alpha: tint it with the rarity colour (and alpha).
- **Clipeus:** native 108×108; its window = native − 2 × padding (72 units); the corners are the scutum's flat centre
  colour, so place it on a `Scutum` (same for `ClipeusLocked` on `ScutumLocked`) with the portrait as an earlier sibling.
  `ScutumSpine` is a sliced 4×4 gold bar: 12 wide, any height.
- **Clean tiling:** meander bands repeat every 18 units (widths in multiples of 18 end on a whole key); the mosaic's edges
  repeat every 24 (sizes 75 + 24k join cleanly at both corners); card frames every 36 across / 45 down.
- **◀ ▶ are in no font**: draw arrows with an icon (e.g. `PixelIcons.Chevron` rotated), not glyphs.
- Size `Simple` kinds from `UiFrames.NativeSize`; content insets from `UiFrames.Padding` (canvas units, l b r t).

## Verified / unverified

- Verified: the compiler (all six assemblies with the asmdef's references: errors=0, 0 warnings; stock ccheck as
  above); the art (preview reviewed in three iterations); the index (parsed by the same rules as the C#); the banner
  timeline (Python port); glyph coverage of the four TTFs and LiberationSans (fontTools).
- Unverified (needs Unity): every test; the TMP font creation and material looks; frames sliced/tiled in UGUI (the
  preview mimics it); the gallery's layout at 1280×720 / 16:10 / 4:3; zero GC measured in Unity.

## Needs from others

- Orchestrator: run the Unity steps above; optionally teach `Tools/ccheck` the asmdef's new references.
- Nothing from B or C.
