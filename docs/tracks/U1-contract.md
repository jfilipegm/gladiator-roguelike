# U1 contract — the Roman UI look (DRAFT, 2026-10-22)

**Spec:** `docs/tracks/U1-design.md` (the look per screen, type, colours, frames, the card, the banner, fit rules,
decisions, Q1–Q8). This file splits the work into three parts by file ownership and fixes the names they share.

**Draft until the producer answers Q1–Q8** (spec §12). Every recommendation is the default here, so the wave can start
on them; what each answer would change: Q1 → U1-C (roster tiles), Q2 → B and C (`ButtonStyle` per button), Q3 → B
(pause face), Q4 → B (routing table), Q5 → B (end screen), Q6 → B (one title), Q7 → nothing now, Q8 → the release.

| Item | Item branch (proposed) | Release (proposed) | Parts |
|---|---|---|---|
| U1 the Roman UI look | `phase5-ui` from `main` after `phase5-skins` (R6, v0.23.0) merges | v0.24.0 | **U1-A** theme + frames + fonts API · **U1-B** in-run screens · **U1-C** menus |

**Within the wave no file is owned by two parts**, except the stand-in file `U1Stub.cs`, which B and C add with
identical bytes (identical adds merge cleanly) and the orchestrator deletes at each merge.

## Rules for every part
- **Start:** `git fetch origin && git merge --ff-only origin/phase5-ui` (a plain merge if the fast-forward fails).
- Read `docs/tracks/README.md`, CLAUDE.md §2, §4, §5 and §7.1, the spec, then this file's sections for your part.
- **B and C compile alone:** their first commit adds `Assets/Scripts/UI/Theme/U1Stub.cs` with exactly the block in
  "The stand-in file" below (copy it byte for byte, LF line ends, final newline), message
  `U1-STUB(a): stand-ins for U1-A's theme API so U1-<B|C> compiles alone (delete at merge)`. Code only against the
  names in this file; if you need more from A, message the orchestrator. `Tools/ccheck` must print `errors=0`.
- **Gameplay code allocates nothing per frame** (CLAUDE.md §4): the HUD, banner, damage numbers and level-up stay
  allocation-free while a run is live (pre-size every TMP label you add, constant strings, colours / alpha / positions
  only, `CanvasGroup` to hide). Menus and the end screen may allocate.
- **Same words, keys and test hooks** (spec §1.7): no text, input or public member is removed or renamed; the only text
  changes are listed in this file. Public constructors that take a `TMP_FontAsset` keep their signature (the font
  argument is ignored: the theme decides). Add read-only test hooks freely.
- **Test code uses only `Gladiator.*` APIs and UnityEngine core types** (the PlayMode test assembly has no TMP or UGUI
  reference): inspect themed elements through `UiKit.HasRole`, `UiFrames.KindOf` and `UiKit.TruncatedLabels`.
- **Append, never reorder:** `TextRole`, `TextLook`, `FrameKind`, `BannerCloth`, `BannerPhase`, `UiState`,
  `ButtonStyle`; the frame index lines.
- Truncate, never Ellipsis. No `FontStyles.Bold` / `Italic` on themed text. No Unity, no hand-written YAML: assets
  come from `Assets/Editor/Setup/` scripts the orchestrator runs.

---

## Names every part uses

### Files

| What | Path |
|---|---|
| Theme, frames, builders, banner (A) | `Assets/Scripts/UI/Theme/UiTheme.cs`, `UiFrames.cs`, `UiKit.cs`, `VexillumBanner.cs` (namespace `Gladiator.UI`) |
| The theme asset | `Assets/Resources/UiTheme.asset` (`Resources.Load<UiTheme>("UiTheme")`) |
| Frame art | `PixelSrc/ui_frames.py` → `Assets/Art/Textures/UI/UiFrames.png` + `UiFrames.txt`; preview `Screenshots/ui/ui_frames_preview.png` |
| Fonts (the orchestrator adds them) | `Assets/Art/Fonts/LuckiestGuy/LuckiestGuy-Regular.ttf` + `LICENSE.txt` (Apache 2.0); `CaesarDressing/CaesarDressing-Regular.ttf` + `OFL.txt`; `MarcellusSC/MarcellusSC-Regular.ttf` + `OFL.txt`; `Marcellus/Marcellus-Regular.ttf` + `OFL.txt` |
| TMP assets (made by A's setup) | `Assets/Art/Fonts/TMP/FN_LuckiestGuy SDF.asset`, `FN_CaesarDressing SDF.asset`, `FN_MarcellusSC SDF.asset`, `FN_Marcellus SDF.asset`, `FN_Symbols SDF.asset`; materials `FN_<Family> SDF - Flat.mat` / `- Outline.mat` / `- Engraved.mat` |
| Licences in builds | `Assets/StreamingAssets/Licenses/Fonts/<Family>-<licence file name>` (copied by A's setup) |
| Gallery | `Assets/Scripts/UI/Dev/UiGallery.cs`; scene `Assets/Scenes/Dev/UiGallery.unity` |
| Level-up fit math (B) | `Assets/Scripts/Core/Layout/LevelUpLayout.cs` (namespace `Gladiator.Core`, beside `MenuLayout`) |

### Enums (`UiTheme.cs` / `UiFrames.cs` / `UiKit.cs` / `VexillumBanner.cs`)
```csharp
public enum TextRole : byte { Number, Shout, Title, Body, Plain }      // spec §2.1
public enum TextLook : byte { Flat, Outline, Engraved }                // spec §2.3
public enum ButtonStyle : byte { Primary, Secondary }                  // bronze ansata, marble ansata
public enum UiState : byte { Normal, Focused, Pressed, Disabled }
public enum BannerCloth : byte { Crimson, Purple }
public enum BannerPhase : byte { Hidden, Dropping, Settling, Holding, Rolling }
public enum FrameKind : byte
{
    AnsataBronze, AnsataMarble, Marble, Bronze, Wax, Mosaic, Stone, MeanderGold, MeanderDark,
    CardStone, CardBronze, CardSilver, CardGold, Halo, Focus,
    Scutum, ScutumLocked, ScutumSpine, Clipeus, ClipeusLocked,
    VexillumBar, VexillumCrimson, VexillumPurple,
    Trough, TroughFill, Knob, Chip, KeyChip, LaurelBranch, LaurelWreath,
}
```

### `UiTheme` (ScriptableObject, `[CreateAssetMenu(menuName = "Gladiator/UI Theme", fileName = "UiTheme")]`)
```csharp
[Serializable] public struct RoleFont
{
    public TMP_FontAsset font;
    public Material flat, outline, engraved;
    [Tooltip("TMP character spacing (em/100): Title +4, rarity band +12 (set by the caller), Number +2, Shout +1.")]
    public float spacing;
}
public const string ResourcePath = "UiTheme";
[Header("Fonts")] public RoleFont number, shout, title, body, plain;
[Header("Frames")] public Texture2D frameAtlas; public TextAsset frameIndex;
[Header("Colours")] public Color night, stone, stoneLight, line, cardInside, ink, dust, outlineInk,
    bronze, bronzeLight, bronzeDark, bronzeInk, gold, goldBright, crimson, crimsonLight, purple, purpleDark, purpleInk,
    marble, marbleShade, marbleInk, waxWood, wax, waxInk, danger, gain, loss, hp, hpDark, xp, favor, coin;
    // defaults = spec §3's hex (new Color32(…)); cardInside #1C1714
[Header("Banner (seconds, degrees, share of the cloth's height)")] public float bannerDrop = 0.32f,
    bannerSettle = 0.12f, bannerSway = 0.7f, bannerHold = 2f, bannerRoll = 0.28f, bannerQuickRoll = 0.15f,
    bannerSwayDegrees = 1.4f, bannerOvershoot = 0.05f;

public static UiTheme Current { get; }          // Resources.Load once; no asset → a CreateInstance with the defaults.
                                                // Cached with Unity's null check (not ??=), like PixelIcons.
public static void Reload();                    // drop the cache (setup scripts, tests)
public RoleFont Role(TextRole r);
public TMP_FontAsset FontFor(TextRole r);       // role font ?? plain font ?? TMP_Settings.defaultFontAsset
public Material MaterialFor(TextRole r, TextLook look); // the look's preset ?? the role's flat ?? FontFor(r).material
public static string Glyphs(TextRole r);        // the role's required glyphs (spec §2.3; letters, digits, its punctuation)
public const string Symbols = "×·—–…→←↑↓’“”•−°²÷±≈√Σ∓◀▶■"; // baked into every theme font that has them and into FN_Symbols
```

### `UiFrames` (static)
```csharp
public const float ArtScale = 3f;                // canvas units per art pixel → sprites' pixelsPerUnit = 100 / ArtScale
public static Sprite Get(FrameKind k);           // cut from frameAtlas by the index (Sprite.Create, FullRect, border),
                                                 // cached, DontUnloadUnusedAsset; missing atlas or line → PixelIcons.White
                                                 // (logged once per kind)
public static Image.Type TypeOf(FrameKind k);    // from the index: Sliced / Tiled / Simple
public static Vector2 NativeSize(FrameKind k);   // art size × ArtScale (size Simple kinds from this, never hard-coded)
public static Vector4 Padding(FrameKind k);      // content insets (left, bottom, right, top) in canvas units
public static Image Apply(Image img, FrameKind k); // sprite, type, fillCenter (false for card frames and Focus); keeps color
public static int KindOf(Component image);       // tests: the FrameKind an Image shows, or -1
public static int ParseIndex(string text, List<FrameEntry> into); // tests; FrameEntry = name, rect, border, type, padding
```
**Index format** (`UiFrames.txt`, one line per kind, `#` comments): `name x y w h bl bb br bt type pl pb pr pt` — name =
the `FrameKind` name; `x y` from the PNG's **top-left**, sizes, borders (left bottom right top) and paddings in **art
pixels**; type `sliced | tiled | simple`. 2 px of transparent gutter between cells; atlas ≤ 512×512.

**The frames** (targets: A may change sizes and borders in the index; names, types and what each is for are fixed)

| FrameKind | Art px | Border l,b,r,t | Type | Notes |
|---|---|---|---|---|
| `AnsataBronze`, `AnsataMarble` | 30×22 | 10,0,10,0 | sliced | dovetail handles in the side borders, so any height 40–90 scales them evenly; native height 66 |
| `Marble` | 24×24 | 6,6,6,6 | sliced | bevel and veins in the border (the centre stretches) |
| `Bronze` | 20×20 | 6,6,6,6 | sliced | a rivet in each corner |
| `Wax` | 24×24 | 7,7,7,7 | sliced | wooden frame, dark wax centre |
| `Mosaic` | 24×24 | 8,8,8,8 | tiled | tesserae checker border, dark field |
| `Stone` | 12×12 | 4,4,4,4 | sliced | dark stone plate (edict cards, locked entries, dark sub-panels) |
| `MeanderGold`, `MeanderDark` | 6×5 | 0 | tiled | one period of the Greek key; height 15 |
| `CardStone`, `CardBronze`, `CardSilver`, `CardGold` | 18×26 | 3,8,3,3 | tiled, no centre | same insets for all four (9 / 24-unit foot); Silver's foot holds the meander |
| `Halo` | 16×16 | 7,7,7,7 | sliced | white stepped alpha rings: tint with the rarity colour |
| `Focus` | 6×6 | 2,2,2,2 | sliced, no centre | white ring: tint white / gold / crimson / danger |
| `Scutum`, `ScutumLocked` | 30×40 | 9,12,9,12 | sliced | curved shield, gold rim / dark iron |
| `ScutumSpine` | 4×4 | 1,1,1,1 | sliced | the gold spine above and below the clipeus |
| `Clipeus`, `ClipeusLocked` | 36×36 | — | simple | bronze / iron ring, transparent round window, corners in the shield's colour (no mask needed) |
| `VexillumBar` | 30×8 | 6,0,6,0 | sliced | the crossbar, gold finials in the side borders |
| `VexillumCrimson`, `VexillumPurple` | 254×40 | 4,12,4,4 | sliced | the cloth at 762×120, widths 700–920; swallowtail hem in the bottom border |
| `Trough` | 12×8 | 3,3,3,3 | sliced | bronze rim, dark channel |
| `TroughFill` | 2×4 | — | simple | white top row, 85 % grey below: tint, then `Image.Type.Filled` (caller) |
| `Knob` | 6×8 | — | simple | slider knob |
| `Chip`, `KeyChip` | 8×8 | 3,3,3,3 | sliced | dark-bronze chip / `bronzeInk` key plate |
| `LaurelBranch` | 16×22 | — | simple | one branch; the right one is mirrored (scale.x −1) |
| `LaurelWreath` | 60×30 | — | simple | behind the Legendary icon |

### `UiKit` (static builders) and `UiPlate`
```csharp
public static TextMeshProUGUI Text(string name, Transform parent, string text, TextRole role, float size, Color color,
    TextAlignmentOptions align, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 box, TextLook look = TextLook.Flat);
    // font + MaterialFor(role, look), spacing, FontStyles.Normal, NoWrap, Truncate, raycast off
public static void Style(TMP_Text t, TextRole role, TextLook look);       // restyle an existing label the same way
public static Image Frame(string name, Transform parent, FrameKind kind, Vector2 anchor, Vector2 pivot, Vector2 pos,
    Vector2 size, Color tint);                                              // raycast off
public static UiPlate Plate(string name, Transform parent, string label, ButtonStyle style, Vector2 anchor, Vector2 pivot,
    Vector2 pos, Vector2 size, float fontSize = 30f, string key = null);   // ansata + label (Title, Engraved, bronzeInk on
    // bronze / marbleInk on marble) + optional key chip (Number 22 on KeyChip, left of the label) + Focus ring (hidden).
    // The label is the plate's FIRST TMP child, so UiFactory.Label(plate.Button) still finds it. No Button component.
public static UiPlate Button(string name, Transform parent, string label, ButtonStyle style, Vector2 anchor, Vector2 pivot,
    Vector2 pos, Vector2 size, UnityAction onClick, float fontSize = 30f, string key = null);
    // Plate + a Button (target graphic = the frame; ColorTint 1 / 1.15 / 0.85; Navigation None). Menus only.
public static Image Bar(string name, Transform parent, Color fill, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size);
    // Trough + a Filled TroughFill inset by the trough's padding; returns the fill (as UiFactory.Bar does)
public static Image Meander(string name, Transform parent, bool gold, Vector2 anchor, Vector2 pivot, Vector2 pos, float width);
public static RectTransform Laurels(string name, TMP_Text word, float gap = 12f);
    // two LaurelBranch images beside the word's rendered text (outside its textBounds); call after the text is set
public static Image Rule(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, float width); // crimson, 3 tall
public static float FitScale(Vector2 size, Vector2 canvas, float margin = 24f); // min(1, (W-2m)/w, (H-2m)/h), spec §8.2
public static bool HasRole(Component text, TextRole role);                      // tests
public static int TruncatedLabels(Transform root, List<string> into);            // tests: visible labels whose text is cut

public sealed class UiPlate
{
    public RectTransform Root; public Image Frame, Focus; public TextMeshProUGUI Label, Key; public Button Button;
    public ButtonStyle Style;
    public UiState State { get; }
    public void SetState(UiState s); // Normal; Focused = ring on (gold) + face ×1.1; Pressed ×0.85; Disabled grey α .45.
                                     // Colours and alpha only: no allocation, no SetActive
}
```

### `VexillumBanner` (a plain class, ticked by its owner, like `UpgradeIconView`)
```csharp
public VexillumBanner(Transform parent, Vector2 anchor, Vector2 pos, float width = 760f);
    // its own nested Canvas; crossbar (width + 60) at pos, the cloth under it inside a RectMask2D, pivot top-centre;
    // words Shout 56 (autosize 36-56), Outline, goldBright on crimson / purpleInk on purple; pre-sized; hidden
public RectTransform Root { get; }
public BannerPhase Phase { get; }
public bool Visible { get; }          // Phase != Hidden
public BannerCloth Cloth { get; }
public string Text { get; }
public float Down01 { get; }          // 0 = fully behind the bar (−104 %), 1 = resting (1 + overshoot at the drop's end)
public float SwayDegrees { get; }
public bool Held { get; }
public void Show(string text, BannerCloth cloth, float seconds = 0f);
    // hold = max(theme.bannerHold, seconds). While visible: a quick roll-up (bannerQuickRoll), then the new one drops.
    // Constant strings only (no allocation in a run).
public void Hold(string text, BannerCloth cloth);   // drops, sways and stays (the end screen)
public void RollUp(bool quick = false);
public void Clear();                                 // hidden at once
public void Tick(float dt);                          // the owner passes scaled dt in a run (spec §7.1)
public static float TotalSeconds(float seconds = 0f); // drop + settle + hold + roll, from UiTheme.Current
```
Timeline (spec §7.1): Dropping `bannerDrop` (−104 % → +overshoot, ease-out) → Settling `bannerSettle` (→ −2 % → 0)
→ Holding (the sway `bannerSway` plays at its start: +deg, −0.64 deg, +0.29 deg, 0) → Rolling `bannerRoll` (ease-in)
→ Hidden.

### `LevelUpLayout` (B, `Gladiator.Core`, pure)
```csharp
public static class LevelUpLayout
{
    public const float CardW = 300f, CardH = 520f, CardGap = 28f, ContentH = 900f, StatsW = 410f, Edge = 24f,
        MinStats = 0.7f, MinCards = 0.6f;
    public struct Fit { public float ContentScale, StatsScale, ContentX; public bool Centred; } // ContentX: from the centre, < 0 = left
    public static float RowWidth(int cards);       // n·300 + (n−1)·28
    public static Fit For(Vector2 canvas, int cards); // spec §8.3
}
```

---

## The stand-in file (`Assets/Scripts/UI/Theme/U1Stub.cs`, B and C, byte for byte)
```csharp
// U1-STUB(a): stand-ins for U1-A's theme API (docs/tracks/U1-contract.md) so U1-B and U1-C compile alone.
// Both parts add this exact file; the orchestrator deletes it when it merges them (U1-A's files win).
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Gladiator.UI
{
    public enum TextRole : byte { Number, Shout, Title, Body, Plain }
    public enum TextLook : byte { Flat, Outline, Engraved }
    public enum ButtonStyle : byte { Primary, Secondary }
    public enum UiState : byte { Normal, Focused, Pressed, Disabled }
    public enum BannerCloth : byte { Crimson, Purple }
    public enum BannerPhase : byte { Hidden, Dropping, Settling, Holding, Rolling }
    public enum FrameKind : byte
    {
        AnsataBronze, AnsataMarble, Marble, Bronze, Wax, Mosaic, Stone, MeanderGold, MeanderDark,
        CardStone, CardBronze, CardSilver, CardGold, Halo, Focus,
        Scutum, ScutumLocked, ScutumSpine, Clipeus, ClipeusLocked,
        VexillumBar, VexillumCrimson, VexillumPurple,
        Trough, TroughFill, Knob, Chip, KeyChip, LaurelBranch, LaurelWreath,
    }

    [Serializable]
    public struct RoleFont
    {
        public TMP_FontAsset font;
        public Material flat, outline, engraved;
        public float spacing;
    }

    public sealed class UiTheme : ScriptableObject // U1-STUB(a)
    {
        public const string ResourcePath = "UiTheme";
        public const string Symbols = "";
        public RoleFont number, shout, title, body, plain;
        public Texture2D frameAtlas;
        public TextAsset frameIndex;
        public Color night, stone, stoneLight, line, cardInside, ink, dust, outlineInk,
            bronze, bronzeLight, bronzeDark, bronzeInk, gold, goldBright, crimson, crimsonLight, purple, purpleDark, purpleInk,
            marble, marbleShade, marbleInk, waxWood, wax, waxInk, danger, gain, loss, hp, hpDark, xp, favor, coin;
        public float bannerDrop = 0.32f, bannerSettle = 0.12f, bannerSway = 0.7f, bannerHold = 2f, bannerRoll = 0.28f,
            bannerQuickRoll = 0.15f, bannerSwayDegrees = 1.4f, bannerOvershoot = 0.05f;
        static UiTheme _current;
        public static UiTheme Current => _current != null ? _current : _current = CreateInstance<UiTheme>();
        public static void Reload() => _current = null;
        public RoleFont Role(TextRole r) => default;
        public TMP_FontAsset FontFor(TextRole r) => TMP_Settings.defaultFontAsset;
        public Material MaterialFor(TextRole r, TextLook look) => null;
        public static string Glyphs(TextRole r) => "";
    }

    public struct FrameEntry // U1-STUB(a)
    {
        public string Name;
        public RectInt Rect;
        public Vector4 Border, Padding;
        public Image.Type Type;
    }

    public static class UiFrames // U1-STUB(a)
    {
        public const float ArtScale = 3f;
        public static Sprite Get(FrameKind k) => PixelIcons.White;
        public static Image.Type TypeOf(FrameKind k) => Image.Type.Simple;
        public static Vector2 NativeSize(FrameKind k) => new Vector2(48f, 48f);
        public static Vector4 Padding(FrameKind k) => Vector4.zero;
        public static Image Apply(Image img, FrameKind k) { img.sprite = PixelIcons.White; return img; }
        public static int KindOf(Component image) => -1;
        public static int ParseIndex(string text, List<FrameEntry> into) => 0;
    }

    public sealed class UiPlate // U1-STUB(a)
    {
        public RectTransform Root;
        public Image Frame, Focus;
        public TextMeshProUGUI Label, Key;
        public Button Button;
        public ButtonStyle Style;
        public UiState State { get; private set; }
        public void SetState(UiState s) => State = s;
    }

    public static class UiKit // U1-STUB(a)
    {
        static readonly Vector2 Mid = new Vector2(0.5f, 0.5f);

        public static TextMeshProUGUI Text(string name, Transform parent, string text, TextRole role, float size, Color color,
            TextAlignmentOptions align, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 box, TextLook look = TextLook.Flat)
            => UiFactory.Text(name, parent, text, size, color, align, anchor, pivot, pos, box, null, null);

        public static void Style(TMP_Text t, TextRole role, TextLook look) { }

        public static Image Frame(string name, Transform parent, FrameKind kind, Vector2 anchor, Vector2 pivot, Vector2 pos,
            Vector2 size, Color tint)
            => UiFactory.Image(name, parent, tint, anchor, pivot, pos, size, PixelIcons.White);

        public static UiPlate Plate(string name, Transform parent, string label, ButtonStyle style, Vector2 anchor, Vector2 pivot,
            Vector2 pos, Vector2 size, float fontSize = 30f, string key = null)
        {
            var frame = UiFactory.Image(name, parent, Color.white, anchor, pivot, pos, size, PixelIcons.White);
            var text = UiFactory.Text("Label", frame.transform, label, fontSize, Color.black, TextAlignmentOptions.Center, Mid, Mid,
                Vector2.zero, size, null, null);
            return new UiPlate { Root = frame.rectTransform, Frame = frame, Label = text, Style = style };
        }

        public static UiPlate Button(string name, Transform parent, string label, ButtonStyle style, Vector2 anchor, Vector2 pivot,
            Vector2 pos, Vector2 size, UnityAction onClick, float fontSize = 30f, string key = null)
        {
            var plate = Plate(name, parent, label, style, anchor, pivot, pos, size, fontSize, key);
            plate.Frame.raycastTarget = true;
            plate.Button = plate.Frame.gameObject.AddComponent<Button>();
            if (onClick != null) plate.Button.onClick.AddListener(onClick);
            return plate;
        }

        public static Image Bar(string name, Transform parent, Color fill, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
            => UiFactory.Bar(name, parent, Color.black, fill, anchor, pivot, pos, size);

        public static Image Meander(string name, Transform parent, bool gold, Vector2 anchor, Vector2 pivot, Vector2 pos, float width)
            => UiFactory.Image(name, parent, Color.white, anchor, pivot, pos, new Vector2(width, 15f), PixelIcons.White);

        public static RectTransform Laurels(string name, TMP_Text word, float gap = 12f)
            => UiFactory.Rect(name, word.transform, Mid, Mid, Vector2.zero, Vector2.zero);

        public static Image Rule(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, float width)
            => UiFactory.Image(name, parent, Color.white, anchor, pivot, pos, new Vector2(width, 3f), PixelIcons.White);

        public static float FitScale(Vector2 size, Vector2 canvas, float margin = 24f) => 1f;
        public static bool HasRole(Component text, TextRole role) => false;
        public static int TruncatedLabels(Transform root, List<string> into) => 0;
    }

    public sealed class VexillumBanner // U1-STUB(a)
    {
        public VexillumBanner(Transform parent, Vector2 anchor, Vector2 pos, float width = 760f)
            => Root = UiFactory.Rect("Banner", parent, anchor, new Vector2(0.5f, 1f), pos, new Vector2(width, 150f));

        public RectTransform Root { get; }
        public BannerPhase Phase { get; private set; }
        public bool Visible => Phase != BannerPhase.Hidden;
        public BannerCloth Cloth { get; private set; }
        public string Text { get; private set; }
        public float Down01 => 0f;
        public float SwayDegrees => 0f;
        public bool Held { get; private set; }
        public void Show(string text, BannerCloth cloth, float seconds = 0f) { Text = text; Cloth = cloth; Phase = BannerPhase.Dropping; }
        public void Hold(string text, BannerCloth cloth) { Show(text, cloth); Held = true; }
        public void RollUp(bool quick = false) => Phase = BannerPhase.Hidden;
        public void Clear() { Phase = BannerPhase.Hidden; Held = false; }
        public void Tick(float dt) { }
        public static float TotalSeconds(float seconds = 0f) => 2.72f;
    }
}
```
A's real files must keep every signature above (A may add members, never change these). `FrameEntry` lives in
`UiFrames.cs`.

---

## Parts

| Part | Owns | Done when |
|---|---|---|
| **U1-A theme + frames + fonts API** | new `Assets/Scripts/UI/Theme/UiTheme.cs`, `UiFrames.cs`, `UiKit.cs`, `VexillumBanner.cs`; new `Assets/Scripts/UI/Dev/UiGallery.cs`; new `PixelSrc/ui_frames.py` + its outputs `Assets/Art/Textures/UI/UiFrames.png`, `UiFrames.txt`, `Screenshots/ui/ui_frames_preview.png`; new `Assets/Editor/Setup/UiThemeSetup.cs`, `Assets/Editor/Setup/UiGallerySetup.cs`; `Assets/Tests/EditMode/Gladiator.Tests.EditMode.asmdef` (references only); new tests `Assets/Tests/EditMode/UiThemeTests.cs`, `UiKitTests.cs`, `BannerTests.cs`; notes `docs/tracks/U1-theme.md` | Every name above implemented, the 30 frames drawn and reviewed on the preview, the setup menus written (fonts, frames, theme, gallery), the banner's timeline, zero GC in `Tick` / `SetState`; ccheck errors=0; tests written. |
| **U1-B in-run screens** | `Assets/Scripts/UI/Hud.cs`, `LevelUpPanel.cs`, `StatsPanel.cs`, `DamageNumbers.cs`, `RunEndPanel.cs`; new `Assets/Scripts/Core/Layout/LevelUpLayout.cs`; the stand-in `U1Stub.cs`; new tests `Assets/Tests/EditMode/LevelUpLayoutTests.cs`, `Assets/Tests/PlayMode/UiInRunLookTests.cs`; `Assets/Tests/PlayMode/StatsPanelTests.cs` (the one layout assertion); notes `docs/tracks/U1-inrun.md` | The HUD, announcer routing, damage numbers, level-up / chest / altar / boss-reward cards (layout A), stats faces, end screen as spec §5–§8; inputs unchanged; zero GC in a run; ccheck errors=0 (with the stand-in); tests written. |
| **U1-C menus** | `Assets/Scripts/UI/MainMenu.cs`, `RosterGrid.cs`, `LanistaTablet.cs`, `ArenaLadder.cs`, `CodexPanel.cs`, `EdictsPanel.cs`, `DailyPanel.cs`, `SkinStrip.cs`, `OptionsPanel.cs`; the stand-in `U1Stub.cs`; new tests `Assets/Tests/PlayMode/UiMenuLookTests.cs`; existing menu tests only where a layout (not text) assertion must follow the look (list them in the notes); notes `docs/tracks/U1-menus.md` | Main, select (scutum roster, wax tablet, skin strip), arena, edicts, daily, Codex, options as spec §5; inputs and `MenuLayout` fitting unchanged; ccheck errors=0 (with the stand-in); tests written. |

Not touched by anyone: `UiFactory.cs` (and `PixelIcons`), `UpgradeIconView.cs`, `AdminPanel.cs`, `ThreatIndicators.cs`,
`HurtOverlay.cs`, `UiSortOrder.cs`, the scene builders (no scene is rebuilt), every Gameplay file (announcer senders
keep their kinds, texts and colours).

---

## U1-A theme + frames + fonts API
- **`UiTheme`:** as named. `Current` = `Resources.Load<UiTheme>(ResourcePath)`; if none, `CreateInstance` (defaults =
  spec §3) with `hideFlags = DontUnloadUnusedAsset`. `Glyphs`: Number = `0-9`, `A-Z`, `a-z`, space and `+-%:/.,!?[]()'`;
  Shout = `A-Z`, `a-z`, `0-9`, space and `!?.,'-:`; Title and Body = printable ASCII 32–126; Plain = "".
- **`UiFrames`:** parse `frameIndex` once; `Get` makes each sprite on first use: `Sprite.Create(frameAtlas, rect (y
  flipped to Unity's bottom-left), pivot (0.5, 0.5), 100f / ArtScale, 0, SpriteMeshType.FullRect, border)`, flagged
  `DontUnloadUnusedAsset`, cached in an array by kind (Unity null check). All frames come from the one atlas, so they
  batch. `Apply` sets `fillCenter = false` for the card kinds and `Focus`.
- **`UiKit`:** as named; plates and texts per spec §2 and §4. `Laurels` sizes each branch to `NativeSize(LaurelBranch)`
  scaled to the word's font size / 48 (floor 0.75), placed `gap` outside the text's bounds, vertically centred on them.
  `TruncatedLabels` counts active, visible (alpha > 0.01, in an active `CanvasGroup` chain with alpha > 0) TMP labels
  whose `isTextTruncated` is true after `ForceMeshUpdate`, adding their names.
- **`VexillumBanner`:** as named, on a nested `Canvas` (inherits sorting), raycasts off. The cloth moves by
  `anchoredPosition.y` and `localEulerAngles.z` only; the text and cloth sprite change only on `Show` / `Hold`.
  Reads its timings from `UiTheme.Current` at `Show`.
- **`PixelSrc/ui_frames.py`** (`python3 PixelSrc/ui_frames.py [--preview]`, PIL, like `upgrade_icons.py`): draws the
  30 frames from the spec's colour tokens (one palette dict at the top, hex from spec §3, extra shades derived and
  named), packs them into `UiFrames.png` with the 2 px gutter, writes `UiFrames.txt`; `--preview` renders every frame
  at 3× on the dark `night` and on a mid-grey arena tone, each sliced/tiled frame at two sizes, a sample ansata at 66 and
  78 tall, the cloths with the crossbar, the four card frames side by side at 300×520 with a 100 px dummy icon square.
  **Review it like a Blender asset**: at most 3 iterations per frame; compare with the mockup pages; laurels must read
  as laurel at 66 units tall; note what you'd still improve in your notes.
- **`UiThemeSetup`** (Editor, menus under `Gladiator/Setup/UI/`, idempotent, each logging what it did):
  - `Import UI Frames`: the atlas importer as `IconSetup` does (Default type, point, no mips, uncompressed, clamp,
    alphaIsTransparency, sRGB, not readable, NPOT none).
  - `Create Font Assets`: for each family folder, `TMP_FontAsset.CreateFontAsset(font, 72, 9, GlyphRenderMode.SDFAA,
    1024, 1024, AtlasPopulationMode.Dynamic)`, add `Glyphs(role) + Symbols` (the ones the font has), then
    `atlasPopulationMode = Static`; save as `FN_<Family> SDF.asset` with its atlas texture and material as sub-assets;
    `FN_Symbols SDF` the same way from `Assets/TextMesh Pro/Fonts/LiberationSans.ttf` with `Symbols`. Fallback table of
    each theme font: `[FN_Symbols SDF, LiberationSans SDF]`. Three material presets per font: Flat (copy), Outline
    (`OUTLINE_ON` width ≈ 0.2, colour `outlineInk`; `UNDERLAY_ON` `outlineInk` α .9, offset y ≈ −0.6, softness 0),
    Engraved (`UNDERLAY_ON` white α .5, offset y ≈ −0.35). Tune the numbers in the gallery, keep them as named constants.
    Copies each family's licence file to `Assets/StreamingAssets/Licenses/Fonts/<Family>-<file>`. A missing family
    logs an error and is skipped.
  - `Create UI Theme`: create `Assets/Resources/UiTheme.asset` if missing (defaults); **every run re-links** the role
    fonts, materials (Plain = `CombatSetup.FontPath` + `FontOutlinePath`), atlas and index by path; never overwrites
    colours or banner timings once created (they belong to the designer, as `CombatSetup`'s data); `UiTheme.Reload()`.
  - `All`: the three in order.
- **`UiGallerySetup`** (`Gladiator/Setup/Dev/Build UI Gallery` → `Assets/Scenes/Dev/UiGallery.unity`: a camera with a
  dim arena-coloured background, an EventSystem, a `UiGallery`). **`UiGallery`** builds on Start: every `FrameKind` at
  two sizes with its name, each role in each look ("125 480! 07:59", "SWARM INCOMING!", "The Grand Arena", a card
  description), plates in the four states and with a key chip, a bar at 70 %, laurels beside "VICTORY!", the four card
  frames 300×520 with a sample icon (`UpgradeIconView` on a real atlas row) and texts, and a banner looping crimson
  "SWARM INCOMING!" then purple "GORM, THE PIT MASTER" every 4 s. Allocation is fine here (dev scene).
- **EditMode asmdef:** references become exactly `"UnityEngine.TestRunner", "UnityEditor.TestRunner", "Gladiator.Core",
  "Gladiator.Gameplay", "Gladiator.UI", "Unity.TextMeshPro", "UnityEngine.UI"`.
- **Tests (EditMode).** **`UiThemeTests`:** `Current` never null, and without an asset its colours equal spec §3 (eight
  tokens checked) and `FontFor` is TMP's default; `ParseIndex` reads a sample (comments and blank lines skipped, a bad
  line skipped with a warning); the real `UiFrames.txt` has one line per `FrameKind`, every rect inside the PNG, borders
  inside their rects, no two rects overlapping, valid types; once the theme asset exists (else `Assert.Ignore`): `Get`
  gives each kind a sprite with the index's border and `pixelsPerUnit` 100 / 3; each of Number / Shout / Title / Body
  has a font with a Static atlas whose fallbacks start with `FN_Symbols SDF`, `HasCharacters(Glyphs(r) + Symbols, out
  missing, searchFallbacks: true)` with `missing` listed in the failure; the three materials share the font's atlas;
  the four licence files are in StreamingAssets. **`UiKitTests`:** `Text` uses the role's font, material and spacing,
  `FontStyles.Normal`, raycast off; `Plate` Primary → `KindOf` = AnsataBronze, Secondary → AnsataMarble, the label is
  the first TMP child, a key chip only with `key`; `SetState` shows the ring only when Focused, dims when Disabled,
  and allocates 0 B over 100 calls (`GC.GetAllocatedBytesForCurrentThread`); `Laurels` keeps both branches outside the
  word's `textBounds`, the right one mirrored; `FitScale` ((1640, 980) in (1663, 1247) ≈ 0.985; in (1920, 1080) = 1;
  in (2217, 935) ≈ 0.905); `TruncatedLabels` finds a long text in a short box and ignores a hidden one.
  **`BannerTests`** (dt 1/60): `Show` → Dropping → Settling → Holding → Rolling → Hidden, Hidden at `TotalSeconds()` ±
  2 frames; `Down01` peaks at 1 + overshoot ± 0.01; |`SwayDegrees`| ≤ `bannerSwayDegrees` + 0.01 and 0 after the sway; a
  longer `seconds` lengthens the hold, a shorter one doesn't shorten it; `Show` while Holding rolls up in
  `bannerQuickRoll` with the old words, then drops with the new words and cloth; `Hold` stays Holding for 10 s, then
  `RollUp` hides it after `bannerRoll`; `Clear` hides at once; 600 `Tick`s over a whole cycle allocate 0 B.

## U1-B in-run screens
- **`Hud`:** the HUD row of spec §5 with `UiKit` (the bronze top-left plate holding HP, the special meter, gold and
  kills; bronze build tiles with a 2-unit rarity line; chips; troughs; the timer, level and counts in Number; the
  intro's mosaic plaque; PAUSED in Shout). Every label pre-sized as today. New:
  - **The banner:** `_banner = new VexillumBanner(canvas, top-centre, (0, −204))`, ticked with scaled dt.
    **Routing (spec §7.2):** `OnDirector` Swarm / Elite and `AnnounceKind.Whim` → Crimson; BossEnters / BossPhase /
    BossGuards / BossFalls → Purple; every other kind → the **callout** (today's `_announce` label restyled: Shout 48,
    the sender's colour, Outline, at y −380, today's pop-and-fade and chatter rule). Priority Purple > Crimson; a banner
    of the same or higher priority while one is visible → `Show` (it rolls up quickly and drops); a lower one → one
    pending slot (newest wins), shown when the banner is Hidden. Run ended → `Clear()` and no callout (today's rule).
    The event's `Seconds` goes to `Show`.
  - Test hooks: `public VexillumBanner Banner`, `public string PendingBanner` (null when none), `public string
    CalloutText`, `public bool CalloutShown`; every existing hook kept.
  - Colours: `BossRed` → `crimsonLight` for the "BOSS" timer and `crimson` for the boss fill; the intro's FIGHT! →
    `goldBright` (Shout 160, Outline); debug line and dev tags stay Plain (`UiFactory`).
- **`DamageNumbers`:** each pooled label: `UiTheme.Current.FontFor(Number)` + `MaterialFor(Number, Outline)` (the
  serialized `font` / `fontMaterial` stay as unused fallbacks); pre-size as today; test hook `public Component
  PoolLabel(int i)`.
- **`LevelUpPanel`:** layout A (spec §6) through `LevelUpLayout.For(canvas size, ChoiceCount)` on every `Redraw`:
  content scale and x, `StatsPanel.SetPickScale(fit.StatsScale)`. Cards per spec §6.2: frame by rarity (Common →
  `CardStone`, Uncommon → `CardBronze`, Rare → `CardSilver`, Legendary → `CardGold` + `LaurelWreath` behind the icon),
  `Halo` behind the icon (rarity colour) and behind the selected card, `Focus` ring (white; crimson in banish mode),
  scale 1.04 when selected, the rarity band (split crimson | gold for a Vow), icon 100, Number 26 key. Buttons: three
  `UiKit.Plate` (Primary, 280×66, keys "R", "B", "X", words "Reroll ({0})", "Banish ({0})", "Skip" written with
  `SetText`), each with a `UiHit` (no `Button`: no Selectable tweens in a run), `SetState` for hover / disabled.
  Titles: Shout 132 (autosize ≥ 72), colours `goldBright` (LEVEL UP!, ARMORY CHEST, SPOILS OF VICTORY), today's altar
  colour (BLOOD ALTAR), `crimsonLight` (BANISH). **Text change:** the boss source's title "THE PIT MASTER'S HOARD" →
  "SPOILS OF VICTORY" (subtitle unchanged). Inputs, grace, `Presize` (now with the theme fonts), CanvasGroup show / hide
  unchanged. New hooks: `public int CardFrame(int i)` (the card's `FrameKind`), `public bool CardHasWreath(int i)`,
  `public float StatsScale`; `CardIsVowFramed(i)` = the split band shows.
- **`LevelUpLayout`** as named, spec §8.3 exactly.
- **`StatsPanel`:** Pick and Prompt faces on `Bronze`, Pause on `Marble` (spec §5); `PickWidth` 410;
  `public void SetPickScale(float k)` (scales the Pick face; `PickReserve` = 410·k + 24); hook `public int FrameShown`.
  Text building, preview logic and no-allocation rules unchanged.
- **`RunEndPanel`:** death → a `VexillumBanner` (width 900) `Hold("YOU DIED", Crimson)` where the title was, ticked with
  unscaled dt (the screen is paused), Shout 96 `ink` words; survived / victory → Shout 132 `goldBright` title with
  `UiKit.Laurels`; stats rows on `Wax` (labels Title 22 `waxInk`, values Number 30); the unlock line Title 30 gold
  between laurels; buttons: RETRY / PRACTICE Primary, SAME SEED and MAIN MENU Secondary (`UiKit.Button`, the end screen
  may allocate), today's keys. Hooks: `public VexillumBanner TitleBanner`, `public bool TitleHasLaurels`,
  `public int StatsFrame`.
- **Tests.** EditMode **`LevelUpLayoutTests`:** spec §8.3's five cases (StatsScale ± 0.01, ContentX ± 1, Centred);
  21:9 (2217×935) ContentScale ≈ 0.994; for canvases 1600–2600 wide × 900–1300 tall and 1–4 cards: the cards' right
  edge + 24 ≤ the column's left edge, and nothing leaves the canvas. PlayMode **`UiInRunLookTests`** (the test arena, as
  `LevelUpTests` boots it): timer, level, gold and a damage number's label have the Number role, the callout Shout, the
  boss name Title; a Swarm `DirectorEvent` → `Banner` Crimson "SWARM INCOMING!"; then BossEnters → Purple replaces it
  (Rolling, then its text); an Elite while Purple holds → `PendingBanner` "AN ELITE ENTERS!", shown after; a Streak →
  `CalloutShown` and the banner untouched; three offers forced Common / Rare / Legendary (the way `LevelUpTests` forces
  offers): `CardRect` 300×520, `CardFrame` per rarity, `CardHasWreath` only on Legendary, the cards' row and the buttons
  share the centre x when `LevelUpLayout.For(…).Centred`; banish → the title "BANISH"; a boss-source pick → "SPOILS OF
  VICTORY"; `UiKit.TruncatedLabels` = 0 on the HUD (debug line excluded) and on the level-up with the catalogue's
  longest description; death → `TitleBanner.Held`, Crimson; victory → `TitleHasLaurels`; `StatsFrame` = Wax; 5 s of
  fighting with a banner every 2 s: log `GameLoop.HeapReport()`, don't assert (R7 lesson: the player run checks GC).
  **`StatsPanelTests`:** replace "the cards moved left for it" with "the content sits where `LevelUpLayout.For(canvas,
  n)` puts it (x ± 1)"; keep "the last card ends before the stats column starts".

## U1-C menus
- **`MainMenu`:** spec §5: the mosaic title plaque (`Mosaic` frame round the title and subtitle) and a gold meander
  under it; the button column as `UiKit.Button` 360×66 (PLAY Primary; DAILY, CODEX, OPTIONS, QUIT Secondary); focus
  via `UiPlate.SetState(Focused)` instead of the gold side bars; the Renown plate (`Bronze`, label Title 22, number
  Number 34); `Chrome`: header Title 56 gold Outline + `Meander` under it, key line Body 22 `dust`, BACK Secondary
  220×66, FIGHT! Primary 300×78 (EDICTS Secondary; its orange-red word when on stays). `UiFactory.Label(button)` keeps
  working on `plate.Button`. Fields typed `Button` stay `Button` (`plate.Button`). Hooks: `public UiPlate MenuPlate(int
  i)` (PLAY = 0 …).
- **`RosterGrid`:** scutum tiles in today's `MenuLayout.Tile` slots: `Scutum` / `ScutumLocked` body, `ScutumSpine`
  above and below a `Clipeus` / `ClipeusLocked` ring that frames the portrait (portrait behind the ring, sized to its
  window), the name Title 20 on the lower band; picked = gold rim tint + the wreath + the lift; hovered = pale rim;
  locked = silhouette and "???"; the NEW dot kept. Drop the tessera sprites' use (PixelIcons stays). Hook `public int
  TileFrame(int i)`.
- **`LanistaTablet`** (`GladiatorTablet`, the shared tablet): the `Wax` frame; names Title 40 `waxInk`, text Body 22.
- **`ArenaLadder`:** steps keep their palette art; names Title 30, lines Body 20, numerals Title; `ArenaTablet` on Wax.
- **`EdictsPanel`:** `Marble` page (scaled by `UiKit.FitScale`), header Title 48 `marbleInk` + `Rule`; cards on `Stone`;
  name Title 26, effect Body 20, share Number 22; ON = `Focus` tinted `danger` + the ON chip (today's rule); locked
  darker; BUY Primary; footer Title 26.
- **`DailyPanel`:** `Mosaic` round the proclamation; the arena and gladiator cards on `Bronze`; the edicts block on
  `Stone`; "SCORED ATTEMPT" Title gold / "PRACTICE …" Body `dust`; the clock Number 28; the ledger on `Wax` (values
  Number), today's row gold, a won day's outcome between small laurels. Scaled by `FitScale`.
- **`CodexPanel`:** tabs as plates 218×62 (Secondary; the active one Primary with a gold `Meander` under it); entries
  on `Marble` (name Title 28, text Body 22 `marbleInk`; locked: tinted darker, hint `dust`); progress via `UiKit.Bar`;
  BUY Primary; RECORDS on `Wax` (values Number); GLADIATORS = the scutum roster + the wax tablet; < > and BACK
  Secondary.
- **`SkinStrip`:** a `Bronze` plate; ◀ ▶ buttons as small Secondary plates (60×88 keeps the hit area; the arrow glyph
  or today's icon); name Title 26; status Body 20; BUY Primary (340×40 allowed: ansata 40–90 tall).
- **`OptionsPanel`:** `Bronze` panel, header Title 48; rows: label Title 24, `UiKit.Bar` trough with a `Knob`, value
  Number 26; Done Primary.
- Each screen keeps its key lines, inputs and fitting (`MenuLayout`); modals scale with `UiKit.FitScale`.
- **Tests.** PlayMode **`UiMenuLookTests`** (the menu scene, a temp save as `SaveSandbox` does): the main menu's PLAY
  plate is `AnsataBronze` and the other four `AnsataMarble`, the title has the Title role and the Renown number the
  Number role, arrows move one Focused plate; the select screen's open tiles are `Scutum` and locked ones
  `ScutumLocked` (a save with only the Nobody), names Title, FIGHT! Bronze, BACK Marble, the tablet `Wax`; the arena
  tablet `Wax`, EDICTS Marble, the Edicts page `Marble` with an ON card's ring in `danger`; the Daily screen's `Mosaic`
  and `Wax` ledger; the Codex's active tab Bronze and the others Marble, RECORDS `Wax`, entries `Marble`; options
  `Bronze`; on every screen `UiKit.TruncatedLabels` = 0 except the intentional truncations you list (the daily header,
  hints). The existing `RunFlowTests`, `RosterTests`, `CodexPlayTests`, `EdictMenuTests`, `DailyMenuTests`,
  `SkinMenuTests` must still pass unchanged (or with layout-only edits listed in your notes).

---

## Merge order and the orchestrator's steps
**Before the wave:** the producer's answers to Q1–Q8 (or the defaults); `phase5-skins` merged; `phase5-ui` cut from
`main`. Add the four font families to `phase5-ui` from github.com/google/fonts (`apache/luckiestguy`,
`ofl/caesardressing`, `ofl/marcellussc`, `ofl/marcellus`: the Regular TTF and its licence file) at the paths above,
then this contract, then start A, B and C together.

**A → B → C.**
1. **Merge A.** In Unity: `Gladiator/Setup/UI/All` (frames import, font assets, theme), then
   `Gladiator/Setup/Dev/Build UI Gallery`. Read the `UiThemeTests` missing-glyph list. Run `UiThemeTests`,
   `UiKitTests`, `BannerTests`. Screenshot the gallery at 1920×1080, 1280×720, 1920×1200, 1600×1200 and, if the
   producer wants, show them before B and C merge (frames and fonts are cheapest to retune now: `ui_frames.py` and the
   theme asset).
2. **Merge B,** then `git rm Assets/Scripts/UI/Theme/U1Stub.cs`; compile. Run `LevelUpLayoutTests`,
   `UiInRunLookTests`, `LevelUpTests`, `StatsPanelTests`, `RunEndAndAdminTests`, `RunEndP1Tests`, `FeedbackTests`,
   `BossTests`.
3. **Merge C,** then `git rm` the stub again (C's add brings it back); compile. Run `UiMenuLookTests`, `RunFlowTests`,
   `RosterTests`, `CodexPlayTests`, `EdictMenuTests`, `DailyMenuTests`, `SkinMenuTests`.
4. Full EditMode and PlayMode suites. **Bot runs:** the Slave Pit perf run at 300 enemies with a fixed `-runSeed`
   (`Tools/perfrun`): frame time and gameplay ms against v0.23.0, **0 B/frame GC in steady state** with banners
   dropping; a full Slave Pit run to Gorm (the purple banners, the boss reward, the end screen).
5. **Screenshots** (1920×1080 and 1280×720; ★ also 1920×1200 and 1600×1200): the gallery ★; a fight in the Slave Pit
   (~150 enemies, damage numbers with a crit, a crimson banner mid-sway) ★; Gorm's entrance (purple banner, boss bar) ★;
   level-up Common / Rare / Legendary ★, four cards ★, banish mode, a Vow card, ARMORY CHEST, SPOILS OF VICTORY; pause ★
   and options over it; YOU DIED ★; VICTORY! with an unlock ★; the arena intro and FIGHT!; main menu ★; gladiator select
   with a skin ★; arena ladder with EDICTS; the Edicts page ★; Daily ★; Codex RECORDS, GLADIATORS, ARSENAL, MARKET (one
   ★). Put the new level-up beside `levelup_a.png` for the producer.
6. **Standalone build** smoke at 1280×720 and 1920×1080 windowed: fonts, frames and the theme load from Resources;
   `Licenses/Fonts` is in the build's StreamingAssets.
7. **Shared docs:** new `docs/CREDITS.md` (the four fonts: names, designers, licences, where the licence files ship),
   DECISIONS (spec §10), BACKLOG (spec §11), `docs/TOOLING.md` (`python3 PixelSrc/ui_frames.py`, then
   `Gladiator/Setup/UI/All`), PROGRESS, release notes v0.24.0.
