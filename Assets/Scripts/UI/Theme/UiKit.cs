using System.Collections.Generic;
using System.Runtime.CompilerServices;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Gladiator.UI
{
    /// <summary>Plate looks: bronze ansata for a screen's main action, marble ansata for the rest (spec §4.1).</summary>
    public enum ButtonStyle : byte { Primary, Secondary }

    public enum UiState : byte { Normal, Focused, Pressed, Disabled }

    /// <summary>A themed plate (tabula ansata + engraved label, an optional key chip and a hidden focus glow), from
    /// UiKit.Plate / UiKit.Button. SetState changes colours and alpha only: no allocation, no SetActive.
    /// P3 (the producer's note 1): the focus is a gold glow that follows the ansata's silhouette (`FrameKind.AnsataGlow`,
    /// the plate's size + 12), not a rectangle.</summary>
    public sealed class UiPlate
    {
        // UI tints can only darken (vertex colours clamp at 1), so a resting face sits at 1/1.1 and Focused can be ×1.1.
        public const float FaceNormal = 1f / 1.1f, FaceFocused = 1f, FacePressed = FaceNormal * 0.85f;
        // P3: 0.8, not 0.6: a disabled plate fades (alpha) but its words must still read (a BUY price you can't afford
        // yet was 2.1:1 bronzeInk on the darkened bronze; 3.1:1 now)
        public const float DisabledGrey = 0.8f, DisabledAlpha = 0.45f;

        public RectTransform Root;
        public Image Frame, Focus;
        public TextMeshProUGUI Label, Key;
        public Button Button;
        public ButtonStyle Style;
        /// <summary>The key chip behind `Key` (null without a key).</summary>
        public Image KeyChip;
        /// <summary>The focus glow's colour when Focused (gold by default; set it before SetState).</summary>
        public Color FocusColor;
        /// <summary>The label's and key's resting colours (Disabled fades them).</summary>
        public Color LabelColor, KeyColor;

        public UiState State { get; private set; }

        public void SetState(UiState s)
        {
            State = s;
            if (s == UiState.Disabled)
                Frame.color = new Color(DisabledGrey, DisabledGrey, DisabledGrey, DisabledAlpha);
            else
            {
                float f = s == UiState.Focused ? FaceFocused : s == UiState.Pressed ? FacePressed : FaceNormal;
                Frame.color = new Color(f, f, f, 1f);
            }
            if (Focus != null)
                Focus.color = new Color(FocusColor.r, FocusColor.g, FocusColor.b, s == UiState.Focused ? FocusColor.a : 0f);
            float a = s == UiState.Disabled ? DisabledAlpha : 1f;
            if (Label != null) Label.color = new Color(LabelColor.r, LabelColor.g, LabelColor.b, LabelColor.a * a);
            if (Key != null) Key.color = new Color(KeyColor.r, KeyColor.g, KeyColor.b, KeyColor.a * a);
            if (KeyChip != null) KeyChip.color = new Color(1f, 1f, 1f, a);
        }
    }

    /// <summary>
    /// U1: themed builders (docs/tracks/U1-contract.md "UiKit"). Text in the theme's role fonts and looks, frames from
    /// the atlas, ansata plates and buttons, troughs, meander bands, laurels and the crimson rule. UiFactory stays as it
    /// is (the admin panel and anything not restyled keep today's look); screens move to UiKit one by one.
    /// </summary>
    public static class UiKit
    {
        static readonly Vector2 Mid = new Vector2(0.5f, 0.5f);

        sealed class RoleTag { public TextRole Role; public TextLook Look; }
        static readonly ConditionalWeakTable<TMP_Text, RoleTag> _roles = new ConditionalWeakTable<TMP_Text, RoleTag>();

        /// <summary>A label in the role's font, the look's material and the role's spacing; FontStyles.Normal (no faux
        /// bold), NoWrap, Truncate (never Ellipsis), raycast off.</summary>
        public static TextMeshProUGUI Text(string name, Transform parent, string text, TextRole role, float size, Color color,
            TextAlignmentOptions align, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 box, TextLook look = TextLook.Flat)
        {
            var t = UiFactory.Text(name, parent, text, size, color, align, anchor, pivot, pos, box, null, null);
            t.textWrappingMode = TextWrappingModes.NoWrap;
            Style(t, role, look);
            return t;
        }

        /// <summary>Restyles an existing label the same way (font, material, spacing, Normal style, Truncate, raycast
        /// off). Its wrapping, size, colour and alignment stay as they are.</summary>
        public static void Style(TMP_Text t, TextRole role, TextLook look)
        {
            var theme = UiTheme.Current;
            t.font = theme.FontFor(role);
            var mat = theme.MaterialFor(role, look);
            if (mat != null) t.fontSharedMaterial = mat;
            t.characterSpacing = theme.SpacingFor(role);
            t.fontStyle = FontStyles.Normal;
            t.overflowMode = TextOverflowModes.Truncate;
            t.raycastTarget = false;
            var tag = _roles.GetOrCreateValue(t);
            tag.Role = role;
            tag.Look = look;
        }

        /// <summary>An Image showing the frame, tinted; raycast off.</summary>
        public static Image Frame(string name, Transform parent, FrameKind kind, Vector2 anchor, Vector2 pivot, Vector2 pos,
            Vector2 size, Color tint)
        {
            var img = UiFactory.Image(name, parent, tint, anchor, pivot, pos, size);
            return UiFrames.Apply(img, kind);
        }

        /// <summary>A tabula ansata plate: the frame (bronze for Primary, marble for Secondary), the label (Title,
        /// Engraved; bronzeInk on bronze, marbleInk on marble; autosized down to 75 %, floor 18) as wide as the plate's
        /// ContentRect (P3: the padding rule), an optional key chip left of the label (Number on KeyChip) and a hidden
        /// focus glow round the plate's silhouette (`AnsataGlow`, the plate's size + 12). The label is the plate's FIRST
        /// TMP child, so UiFactory.Label(plate.Button) still finds it. No Button component (see Button).</summary>
        public static UiPlate Plate(string name, Transform parent, string label, ButtonStyle style, Vector2 anchor, Vector2 pivot,
            Vector2 pos, Vector2 size, float fontSize = 30f, string key = null)
        {
            var theme = UiTheme.Current;
            var kind = style == ButtonStyle.Primary ? FrameKind.AnsataBronze : FrameKind.AnsataMarble;
            var frame = Frame(name, parent, kind, anchor, pivot, pos, size, Color.white);
            var pad = UiFrames.Padding(kind);
            var content = ContentRect(kind, size);
            bool hasKey = !string.IsNullOrEmpty(key);
            float chip = hasKey ? Mathf.Clamp(Mathf.Round(size.y * 0.55f), 24f, 42f) : 0f;
            float gap = hasKey ? 8f : 0f;
            float left = -size.x * 0.5f + pad.x, right = content.xMax;

            // the label first: it must be the first TMP child. Its box spans the content rect's width (the chip, when
            // there is one, takes the left of it), so autosize keeps the words inside the padding.
            float labelLeft = hasKey ? Mathf.Max(content.xMin, left + 6f + chip + gap) : content.xMin;
            var ink = style == ButtonStyle.Primary ? theme.bronzeInk : theme.marbleInk;
            var text = Text("Label", frame.transform, label, TextRole.Title, fontSize, ink, TextAlignmentOptions.Midline, Mid, Mid, // glyph-centred (P3)
                new Vector2((labelLeft + right) * 0.5f, 0f), new Vector2(Mathf.Max(10f, right - labelLeft), size.y), TextLook.Engraved);
            text.enableAutoSizing = true;
            text.fontSizeMax = fontSize;
            text.fontSizeMin = Mathf.Min(fontSize, Mathf.Max(18f, fontSize * 0.75f));

            var plate = new UiPlate
            {
                Root = frame.rectTransform, Frame = frame, Label = text, Style = style,
                FocusColor = theme.gold, LabelColor = ink, KeyColor = theme.ink,
            };
            if (hasKey)
            {
                plate.KeyChip = Frame("KeyChip", frame.transform, FrameKind.KeyChip, Mid, Mid,
                    new Vector2(left + 6f + chip * 0.5f, 0f), new Vector2(chip, chip), Color.white);
                plate.Key = Text("Key", plate.KeyChip.transform, key, TextRole.Number, Mathf.Min(22f, Mathf.Round(chip * 0.62f)),
                    theme.ink, TextAlignmentOptions.Midline, Mid, Mid, Vector2.zero, new Vector2(chip, chip));
                UiAudit.ExemptPadding(plate.Key); // a keycap: its letter fills the chip by design (its contrast is still checked)
            }
            // the glow: AnsataGlow's 2-art-px ring round the plate's silhouette (both ansatas share it), FocusColor-tinted
            plate.Focus = Frame("Focus", frame.transform, FrameKind.AnsataGlow, Mid, Mid, Vector2.zero, size + GlowGrow,
                new Color(theme.gold.r, theme.gold.g, theme.gold.b, 0f));
            plate.SetState(UiState.Normal);
            return plate;
        }

        /// <summary>Plate + a Button on the frame (target graphic = the frame; ColorTint 1 / 1.15 / 0.85; Navigation
        /// None). Menus only: in a run use Plate + UiHit (no Selectable tweens).</summary>
        public static UiPlate Button(string name, Transform parent, string label, ButtonStyle style, Vector2 anchor, Vector2 pivot,
            Vector2 pos, Vector2 size, UnityAction onClick, float fontSize = 30f, string key = null)
        {
            var plate = Plate(name, parent, label, style, anchor, pivot, pos, size, fontSize, key);
            plate.Frame.raycastTarget = true;
            var btn = plate.Frame.gameObject.AddComponent<Button>();
            btn.targetGraphic = plate.Frame;
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.75f, 0.75f, 0.75f, 0.6f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.05f;
            btn.colors = colors;
            btn.navigation = new Navigation { mode = Navigation.Mode.None };
            if (onClick != null) btn.onClick.AddListener(onClick);
            plate.Button = btn;
            return plate;
        }

        /// <summary>A trough with a horizontally Filled TroughFill inset by the trough's padding (it stretches with the
        /// trough). Returns the fill, as UiFactory.Bar does.</summary>
        public static Image Bar(string name, Transform parent, Color fill, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var trough = Frame(name, parent, FrameKind.Trough, anchor, pivot, pos, size, Color.white);
            var pad = UiFrames.Padding(FrameKind.Trough);
            var f = UiFactory.Image(name + "Fill", trough.transform, fill, Mid, Mid, Vector2.zero, Vector2.zero);
            UiFactory.Stretch(f.rectTransform, Vector2.zero, Vector2.one, new Vector2(pad.x, pad.y), new Vector2(-pad.z, -pad.w));
            UiFrames.Apply(f, FrameKind.TroughFill);
            f.type = UnityEngine.UI.Image.Type.Filled;
            f.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
            f.fillOrigin = 0;
            f.fillAmount = 1f;
            return f;
        }

        /// <summary>A meander band (gold on dark, or dark on silver), `width` long, its native height (15) tall.</summary>
        public static Image Meander(string name, Transform parent, bool gold, Vector2 anchor, Vector2 pivot, Vector2 pos, float width)
        {
            var kind = gold ? FrameKind.MeanderGold : FrameKind.MeanderDark;
            return Frame(name, parent, kind, anchor, pivot, pos, new Vector2(width, UiFrames.NativeSize(kind).y), Color.white);
        }

        /// <summary>Two LaurelBranch images beside the word's rendered text, `gap` outside its textBounds and vertically
        /// centred on them, sized NativeSize × fontSize / 48 (floor 0.75); the right one mirrored. Call after the text
        /// is set; calling again re-places the same pair (after the text changes).</summary>
        public static RectTransform Laurels(string name, TMP_Text word, float gap = 12f)
        {
            word.ForceMeshUpdate();
            var b = word.textBounds;
            float minX = b.min.x, maxX = b.max.x, cy = b.center.y;
            if (!(maxX >= minX) || float.IsInfinity(minX) || float.IsInfinity(maxX) || float.IsNaN(cy))
            {
                minX = maxX = 0f;
                cy = 0f;
            }
            float k = Mathf.Max(0.75f, word.fontSize / 48f);
            var size = UiFrames.NativeSize(FrameKind.LaurelBranch) * k;

            var rt = word.transform.Find(name) as RectTransform;
            if (rt == null)
            {
                var pivot = word.rectTransform.pivot;
                rt = UiFactory.Rect(name, word.transform, pivot, Mid, Vector2.zero, Vector2.zero);
                Frame("Left", rt, FrameKind.LaurelBranch, Mid, Mid, Vector2.zero, size, Color.white);
                Frame("Right", rt, FrameKind.LaurelBranch, Mid, Mid, Vector2.zero, size, Color.white).rectTransform.localScale =
                    new Vector3(-1f, 1f, 1f);
            }
            var l = (RectTransform)rt.Find("Left");
            var r = (RectTransform)rt.Find("Right");
            l.sizeDelta = r.sizeDelta = size;
            l.anchoredPosition = new Vector2(minX - gap - size.x * 0.5f, cy);
            r.anchoredPosition = new Vector2(maxX + gap + size.x * 0.5f, cy);
            return rt;
        }

        /// <summary>The crimson rule under a title on marble (3 units tall).</summary>
        public static Image Rule(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, float width) =>
            UiFactory.Image(name, parent, UiTheme.Current.crimson, anchor, pivot, pos, new Vector2(width, 3f), PixelIcons.White);

        // ------------------------------------------------------------------ P3 (docs/tracks/P3-contract.md)

        /// <summary>How much a plate's focus glow grows past the plate: 2 art px × 3 on every side.</summary>
        public static readonly Vector2 GlowGrow = new Vector2(12f, 12f);

        /// <summary>Tests: was this label themed (Text / Style) with `look`?</summary>
        public static bool HasLook(Component text, TextLook look)
        {
            var t = text as TMP_Text;
            return t != null && _roles.TryGetValue(t, out var tag) && tag.Look == look;
        }

        /// <summary>The NEW seal's size on plates, tabs and tiles (12 art px at 3 units each); Codex entries use 24 (2 each).</summary>
        public const float SealSize = 36f;
        /// <summary>The seal beside a plate's word (PlaceSeal): about the capitals' height (note 27).</summary>
        public const float SealSizeInline = 24f;

        /// <summary>The NEW mark (note 6): a crimson wax seal with a gold N (`FrameKind.SealNew`), hidden until enabled;
        /// PlaceSeal puts it on a plate's corner.</summary>
        public static Image Seal(string name, Transform parent, float size = SealSize)
        {
            var img = Frame(name, parent, FrameKind.SealNew, Mid, Mid, Vector2.zero, new Vector2(size, size), Color.white);
            img.enabled = false;
            return img;
        }

        /// <summary>Puts `seal` (a child of `plate`) on the text line right after the last letter of `label`, like the dot
        /// after DAILY (note 27, v0.27.0: it sat on the plate's top-right corner, too big and off the word): its centre a
        /// third of its size plus half of it right of the ink, at the ink's vertical middle, never past the plate's right
        /// edge. Call after the text is set (menu-only: it measures).</summary>
        public static void PlaceSeal(Image seal, RectTransform plate, TMP_Text label)
        {
            if (seal == null || plate == null) return;
            var r = plate.rect;
            float size = seal.rectTransform.sizeDelta.x;
            float x = r.xMax - size * 0.5f, y = r.center.y;
            if (label != null && UiAudit.TryInkBounds(label, out var ink))
            {
                var at = plate.InverseTransformPoint(label.transform.TransformPoint(new Vector3(ink.xMax, (ink.yMin + ink.yMax) * 0.5f, 0f)));
                x = at.x + size * (0.5f + 0.35f);
                y = at.y;
            }
            x = Mathf.Min(x, r.xMax - size * 0.5f - 4f);
            var rt = seal.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x, y) - r.center; // from the plate's centre (its anchor)
        }

        /// <summary>A pager arrow (note 11): a bronze pilum tip (`ArrowLeft` / `ArrowRight` at their art's size), no plate
        /// behind. The Button sits on a clear hit area of `hit` units; the arrow is its target graphic, a little dim at rest
        /// and full bright when hovered.</summary>
        public static Button Arrow(string name, Transform parent, bool right, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 hit,
            UnityAction onClick)
        {
            var area = UiFactory.Image(name, parent, Color.clear, anchor, pivot, pos, hit, PixelIcons.White);
            area.raycastTarget = true;
            var kind = right ? FrameKind.ArrowRight : FrameKind.ArrowLeft;
            var arrow = Frame("Arrow", area.transform, kind, Mid, Mid, Vector2.zero, UiFrames.NativeSize(kind), Color.white);
            var btn = area.gameObject.AddComponent<Button>();
            btn.targetGraphic = arrow;
            var colors = btn.colors;
            colors.normalColor = new Color(ArrowRest, ArrowRest, ArrowRest, 1f);
            colors.highlightedColor = Color.white;
            colors.pressedColor = new Color(0.62f, 0.62f, 0.62f, 1f);
            colors.selectedColor = colors.normalColor;
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.45f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.05f;
            btn.colors = colors;
            btn.navigation = new Navigation { mode = Navigation.Mode.None };
            if (onClick != null) btn.onClick.AddListener(onClick);
            return btn;
        }

        /// <summary>A pager arrow's brightness at rest (hovered = 1).</summary>
        public const float ArrowRest = 0.78f;

        /// <summary>A list row's focus (no rectangles on menus): a soft band behind the row's words and a pilum marker
        /// (`ArrowRight`, 2 units per art px) pointing at the row's left edge. Build it before the row's words (it must
        /// draw under them); Show only enables and disables (no allocation).</summary>
        public sealed class RowFocus
        {
            public RectTransform Root;
            public Image Band, Marker;
            public bool Shown { get; private set; }

            public void Show(bool on)
            {
                Shown = on;
                if (Band != null) Band.enabled = on;
                if (Marker != null) Marker.enabled = on;
            }
        }

        /// <summary>A soft white band: lighter on dark faces (wax, bronze plates, stone).</summary>
        public static readonly Color BandLight = new Color(1f, 1f, 1f, 0.08f);

        /// <summary>Builds a hidden RowFocus over a row at `pos` (`size`, from `anchor` / `pivot`): the band fills it (in
        /// `band`'s colour: BandLight on dark faces, a warm shade on marble), the marker's tip 4 units left of it.</summary>
        public static RowFocus Row(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, Color band)
        {
            var root = UiFactory.Rect(name, parent, anchor, pivot, pos, size);
            var b = UiFactory.Image("Band", root, band, Mid, Mid, Vector2.zero, Vector2.zero, PixelIcons.White);
            UiFactory.Stretch(b.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            b.raycastTarget = false;
            var marker = UiFrames.NativeSize(FrameKind.ArrowRight) * (2f / UiFrames.ArtScale);
            var left = new Vector2(0f, 0.5f);
            var m = Frame("Marker", root, FrameKind.ArrowRight, left, new Vector2(1f, 0.5f), new Vector2(-4f, 0f), marker, Color.white);
            var f = new RowFocus { Root = root, Band = b, Marker = m };
            f.Show(false);
            return f;
        }

        /// <summary>P3 (the producer's note 4): the gap every label keeps inside its frame's padding, canvas units.</summary>
        public const float ContentPad = 8f;

        /// <summary>P3: where labels may sit inside a frame of `kind` and `size`: the frame less its padding
        /// (UiFrames.Padding) and ContentPad on every side, in the frame's local units with (0, 0) at its centre. Never
        /// negative: a frame too small for its padding gives a zero-size rect at its centre.</summary>
        public static Rect ContentRect(FrameKind kind, Vector2 size)
        {
            var p = UiFrames.Padding(kind);
            float xMin = -size.x * 0.5f + p.x + ContentPad, xMax = size.x * 0.5f - p.z - ContentPad;
            float yMin = -size.y * 0.5f + p.y + ContentPad, yMax = size.y * 0.5f - p.w - ContentPad;
            if (xMax < xMin) xMin = xMax = (xMin + xMax) * 0.5f;
            if (yMax < yMin) yMin = yMax = (yMin + yMax) * 0.5f;
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        /// <summary>The scale that fits a `size` block in `canvas` with `margin` on every side, never above 1 (spec §8.2).</summary>
        public static float FitScale(Vector2 size, Vector2 canvas, float margin = 24f)
        {
            float s = 1f;
            if (size.x > 0f) s = Mathf.Min(s, (canvas.x - 2f * margin) / size.x);
            if (size.y > 0f) s = Mathf.Min(s, (canvas.y - 2f * margin) / size.y);
            return Mathf.Max(0f, s);
        }

        /// <summary>Tests: was this label themed (Text / Style) with `role`, and does it still use that role's font?
        /// An unthemed label counts when it uses the role's own font.</summary>
        public static bool HasRole(Component text, TextRole role)
        {
            var t = text as TMP_Text;
            if (t == null) return false;
            var theme = UiTheme.Current;
            if (_roles.TryGetValue(t, out var tag)) return tag.Role == role && t.font == theme.FontFor(role);
            var own = theme.Role(role).font;
            return own != null && t.font == own;
        }

        /// <summary>Tests: visible labels under `root` whose text is cut (TMP isTextTruncated after ForceMeshUpdate).
        /// Counts active labels with alpha > 0.01 under CanvasGroups whose alpha product is above 0; adds their names
        /// to `into` (may be null). Labels must sit under a Canvas to lay out.</summary>
        public static int TruncatedLabels(Transform root, List<string> into)
        {
            int n = 0;
            foreach (var t in root.GetComponentsInChildren<TMP_Text>(false))
            {
                if (!t.isActiveAndEnabled || t.color.a <= 0.01f || string.IsNullOrEmpty(t.text)) continue;
                float a = 1f;
                for (var tr = t.transform; tr != null; tr = tr.parent)
                {
                    var g = tr.GetComponent<CanvasGroup>();
                    if (g == null || !g.enabled) continue;
                    a *= g.alpha;
                    if (g.ignoreParentGroups) break;
                }
                if (a <= 0f) continue;
                t.ForceMeshUpdate();
                if (!t.isTextTruncated) continue;
                n++;
                into?.Add(t.name);
            }
            return n;
        }
    }
}
