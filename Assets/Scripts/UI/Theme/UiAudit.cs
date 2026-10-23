using System.Collections.Generic;
using System.Runtime.CompilerServices;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gladiator.UI
{
    /// <summary>
    /// P3 (the producer's notes 4, 7 and 8; docs/tracks/P3-contract.md "Surfaces and contrast"): the padding and contrast
    /// checks every screen's tests run.
    /// - A label's **surface** is the frame its text sits on: of the visible surfaces drawn before the label on its root
    ///   canvas whose rect holds the centre of the label's text, the last drawn (the one you see behind the words).
    ///   Siblings count (the HUD's labels sit beside their plates, not under them). A surface is a frame with a tone
    ///   (SurfaceTone(kind).a &gt; 0), or a plain colour Image (no frame: PixelIcons.White) at least 60 % opaque, such as a
    ///   screen's dim layer or an icon's plate.
    /// - **Padding:** the label's rendered text (its glyphs' ink, see InkBounds) lies inside UiKit.ContentRect of its
    ///   frame surface. Plain colour surfaces have no padding.
    /// - **Contrast:** every visible character's colour (rich-text colours included) against the surface's tone (the
    ///   frame's face times the Image's tint) has a WCAG 2 ratio of at least MinContrast; an outlined label also passes
    ///   when its outline stands out from the surface and its face from its outline (a sticker). A label with no surface
    ///   (over the 3D scene or the sky) must be outlined (TextLook.Outline) or a Shout.
    /// - Visible = active and enabled, alpha &gt; 0.01 through its CanvasGroups, with text. Menu and test code: it allocates.
    /// </summary>
    public static class UiAudit
    {
        public const float MinContrast = 3f;
        /// <summary>How far (canvas units) ink may cross a content rect's edge before it counts (rounding).</summary>
        // P3 integration: ink may reach 2.5 units into the 8-unit ContentPad (a Q's tail, autosize rounding) and still keep
        // 5.5 units of air inside the frame's own padding; the producer's note 4 was words touching the frame (0 gap)
        public const float Slack = 2.5f;
        /// <summary>A plain colour Image is a surface from this opacity up (a dim layer, an icon's plate).</summary>
        public const float PlainSurfaceAlpha = 0.6f;

        sealed class Mark { public bool All, Padding; }
        static readonly ConditionalWeakTable<Component, Mark> _exempt = new ConditionalWeakTable<Component, Mark>();

        /// <summary>Leaves `label` out of both checks. Rare: give the reason in a comment at the call.</summary>
        public static void Exempt(Component label)
        {
            if (label != null) _exempt.GetOrCreateValue(label).All = true;
        }

        /// <summary>Leaves `label` out of the padding check only (its contrast is still checked): a key chip's letter fills
        /// its keycap by design.</summary>
        public static void ExemptPadding(Component label)
        {
            if (label != null) _exempt.GetOrCreateValue(label).Padding = true;
        }

        public static bool IsExempt(Component label) => label != null && _exempt.TryGetValue(label, out var m) && m.All;
        static bool PaddingExempt(Component label) => label != null && _exempt.TryGetValue(label, out var m) && (m.All || m.Padding);

        // ------------------------------------------------------------------ tones

        static Color Hex(int rgb) => new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 0xFF);
        static readonly Color None = new Color(0f, 0f, 0f, 0f);

        /// <summary>The colour of the frame's face where words sit (the middle of its face in PixelSrc/ui_frames.py, before
        /// the Image's tint); alpha 0 = not a surface (rims, glows, bands, icons, the clipeus' window).</summary>
        public static Color SurfaceTone(FrameKind k)
        {
            switch (k)
            {
                case FrameKind.AnsataBronze: return Hex(0xB07A3B);  // bronzeFace3, the plate's middle band
                case FrameKind.AnsataMarble: return Hex(0xECE7DE);  // marbleFace2
                case FrameKind.Marble: return Hex(0xECE7DE);        // marble
                case FrameKind.Bronze: return Hex(0x3A2819);        // bronzePlate, the dark plate inside the rim
                case FrameKind.Wax: return Hex(0x251E19);           // wax
                case FrameKind.Mosaic: return Hex(0x211915);        // field
                case FrameKind.Stone: return Hex(0x26201C);         // stone
                case FrameKind.CardStone:
                case FrameKind.CardBronze:
                case FrameKind.CardSilver:
                case FrameKind.CardGold: return Hex(0x1C1714);      // the card's inside (cardInside: the frames have no centre)
                case FrameKind.Scutum: return Hex(0x8A2A2C);        // shield4, the shield's middle
                case FrameKind.ScutumLocked: return Hex(0x45484E);  // iron4
                case FrameKind.VexillumCrimson: return Hex(0x822329); // clothC3
                case FrameKind.VexillumPurple: return Hex(0x371E45);  // clothP3
                case FrameKind.Trough: return Hex(0x1A120C);        // channel
                case FrameKind.TroughFill: return Hex(0xD9D9D9);    // 85 % grey under its tint (the bar's colour)
                case FrameKind.Chip: return Hex(0x5A3A1B);          // bronzeDark
                case FrameKind.KeyChip: return Hex(0x2A180A);       // bronzeInk
                default: return None;
            }
        }

        /// <summary>WCAG 2 relative luminance of an sRGB colour (alpha ignored).</summary>
        public static float Luminance(Color c) => 0.2126f * Lin(c.r) + 0.7152f * Lin(c.g) + 0.0722f * Lin(c.b);

        static float Lin(float v)
        {
            v = Mathf.Clamp01(v);
            return v <= 0.04045f ? v / 12.92f : Mathf.Pow((v + 0.055f) / 1.055f, 2.4f);
        }

        /// <summary>WCAG 2 contrast ratio between two colours, 1 (same) to 21 (black on white). Alpha is ignored.</summary>
        public static float Contrast(Color a, Color b)
        {
            float la = Luminance(a), lb = Luminance(b);
            return (Mathf.Max(la, lb) + 0.05f) / (Mathf.Min(la, lb) + 0.05f);
        }

        // ------------------------------------------------------------------ the checks

        /// <summary>Visible labels under `root` whose rendered text leaves the ContentRect of the frame they sit on; adds
        /// "name: why" lines to `into` (may be null).</summary>
        public static int PaddingViolations(Transform root, List<string> into)
        {
            _indexed = null;
            int n = 0;
            foreach (var t in Labels(root))
            {
                if (PaddingExempt(t)) continue;
                var surface = Surface(t, out int kind);
                if (surface == null || kind < 0) continue; // over the scene, or a plain colour: no padding
                if (!TryInkWorld(t, out var ink)) continue;
                var rt = surface.rectTransform;
                var r = rt.rect;
                var content = UiKit.ContentRect((FrameKind)kind, r.size);
                content.center += r.center; // ContentRect is about the frame's centre; the rect may have another pivot
                var a = rt.InverseTransformPoint(ink[0]);
                var b = rt.InverseTransformPoint(ink[1]);
                float xMin = Mathf.Min(a.x, b.x), xMax = Mathf.Max(a.x, b.x), yMin = Mathf.Min(a.y, b.y), yMax = Mathf.Max(a.y, b.y);
                float l = content.xMin - xMin, rr = xMax - content.xMax, bo = content.yMin - yMin, to = yMax - content.yMax;
                float worst = Mathf.Max(Mathf.Max(l, rr), Mathf.Max(bo, to));
                if (worst <= Slack) continue;
                n++;
                into?.Add(Path(t) + ": the text leaves " + (FrameKind)kind + " '" + surface.name + "''s content rect by " + worst.ToString("0.#")
                          + (to >= worst ? " at the top" : bo >= worst ? " at the bottom" : l >= worst ? " on the left" : " on the right")
                          + " (\"" + Short(t.text) + "\")");
            }
            return n;
        }

        /// <summary>Visible labels under `root` whose colours fall under MinContrast against the surface they sit on, or
        /// with no surface and no outline; adds "name: why" lines to `into` (may be null).</summary>
        public static int ContrastViolations(Transform root, List<string> into)
        {
            _indexed = null;
            int n = 0;
            foreach (var t in Labels(root))
            {
                if (IsExempt(t)) continue;
                var surface = Surface(t, out int kind);
                bool outlined = Outlined(t, out var outline);
                if (surface == null)
                {
                    if (outlined || UiKit.HasLook(t, TextLook.Outline) || UiKit.HasRole(t, TextRole.Shout)) continue;
                    n++;
                    into?.Add(Path(t) + ": over the scene with no surface and no outline (\"" + Short(t.text) + "\")");
                    continue;
                }
                var tone = ToneOf(surface, kind);
                float worst = WorstContrast(t, tone, outlined, outline, out var face);
                if (worst >= MinContrast) continue;
                n++;
                into?.Add(Path(t) + ": " + ColorUtility.ToHtmlStringRGB(face) + " on " + (kind >= 0 ? ((FrameKind)kind).ToString() : "a plain colour") + " '"
                          + surface.name + "' (" + ColorUtility.ToHtmlStringRGB(tone) + ") is " + worst.ToString("0.0") + ":1 (\"" + Short(t.text) + "\")");
            }
            return n;
        }

        /// <summary>The tone of a surface Image: its frame's tone (or white for a plain image) times its tint.</summary>
        public static Color ToneOf(Image surface, int kind)
        {
            var c = surface.color;
            var face = kind >= 0 ? SurfaceTone((FrameKind)kind) : Color.white;
            return new Color(face.r * c.r, face.g * c.g, face.b * c.b, 1f);
        }

        /// <summary>The lowest contrast of any visible character's colour against `tone` (an outlined label scores the
        /// better of its face and its outline-as-sticker); `face` is that character's colour.</summary>
        static float WorstContrast(TMP_Text t, Color tone, bool outlined, Color outline, out Color face)
        {
            t.ForceMeshUpdate();
            var info = t.textInfo;
            float worst = float.MaxValue;
            face = t.color;
            int count = info != null ? Mathf.Min(info.characterCount, info.characterInfo.Length) : 0;
            bool any = false;
            for (int i = 0; i < count; i++)
            {
                var ch = info.characterInfo[i];
                if (!ch.isVisible) continue;
                any = true;
                Color c = ch.color;
                float k = Score(c, tone, outlined, outline);
                if (k < worst) { worst = k; face = c; }
            }
            if (!any) worst = Score(t.color, tone, outlined, outline);
            return worst;
        }

        static float Score(Color c, Color tone, bool outlined, Color outline)
        {
            float k = Contrast(c, tone);
            if (outlined) k = Mathf.Max(k, Mathf.Min(Contrast(outline, tone), Contrast(c, outline)));
            return k;
        }

        /// <summary>Does the label's material draw an outline (TMP's outline width above zero)? `color` is its colour.</summary>
        public static bool Outlined(TMP_Text t, out Color color)
        {
            color = Color.black;
            var m = t.fontSharedMaterial;
            if (m == null || !m.HasProperty(ShaderUtilities.ID_OutlineWidth)) return false;
            if (m.GetFloat(ShaderUtilities.ID_OutlineWidth) <= 0.05f) return false;
            if (m.HasProperty(ShaderUtilities.ID_OutlineColor)) color = m.GetColor(ShaderUtilities.ID_OutlineColor);
            return true;
        }

        // ------------------------------------------------------------------ labels and surfaces

        static readonly List<TMP_Text> _labels = new List<TMP_Text>(256);

        /// <summary>The visible labels under `root` (active, alpha above 0.01 through their CanvasGroups, with text).</summary>
        public static List<TMP_Text> Labels(Transform root)
        {
            _labels.Clear();
            if (root == null) return _labels;
            foreach (var t in root.GetComponentsInChildren<TMP_Text>(false))
            {
                if (!t.isActiveAndEnabled || t.color.a <= 0.01f || string.IsNullOrEmpty(t.text)) continue;
                if (GroupAlpha(t.transform) <= 0.01f) continue;
                _labels.Add(t);
            }
            return _labels;
        }

        static float GroupAlpha(Transform from)
        {
            float a = 1f;
            for (var tr = from; tr != null; tr = tr.parent)
            {
                var g = tr.GetComponent<CanvasGroup>();
                if (g == null || !g.enabled) continue;
                a *= g.alpha;
                if (g.ignoreParentGroups) break;
            }
            return a;
        }

        /// <summary>Does `img` count as a surface, and of which frame kind (-1 = a plain colour)?</summary>
        static bool IsSurface(Image img, out int kind)
        {
            kind = UiFrames.KindOf(img);
            if (!img.isActiveAndEnabled || img.color.a <= 0.01f) return false;
            if (kind >= 0) return SurfaceTone((FrameKind)kind).a > 0f;
            var s = img.sprite;
            if (s != null && s != PixelIcons.White) return false; // an icon or a picture, not a surface
            return img.color.a * GroupAlpha(img.transform) >= PlainSurfaceAlpha;
        }

        /// <summary>The surface `label` sits on (null: over the scene) and its frame kind (-1: a plain colour).</summary>
        public static Image SurfaceOf(TMP_Text label, out int kind)
        {
            _indexed = null; // the hierarchy may have changed since the last call
            return Surface(label, out kind);
        }

        static Image Surface(TMP_Text label, out int kind)
        {
            kind = -1;
            var canvas = label.canvas;
            if (canvas == null) return null;
            var root = canvas.rootCanvas.transform;
            if (!TryInkWorld(label, out var ink)) return null;
            var centre = (ink[0] + ink[1]) * 0.5f;

            // the Images drawn before the label (a depth-first walk; nested canvases that sort themselves by their order)
            Image best = null;
            int bestKind = -1;
            long bestKey = long.MinValue;
            long labelKey = DrawKey(label.transform, root);
            foreach (var img in root.GetComponentsInChildren<Image>(false))
            {
                if (!IsSurface(img, out int k)) continue;
                if (GroupAlpha(img.transform) <= 0.01f) continue;
                if (img.transform.IsChildOf(label.transform)) continue; // drawn over the label
                long key = DrawKey(img.transform, root);
                if (key >= labelKey || key <= bestKey) continue;
                if (!Holds(img, centre)) continue;
                best = img;
                bestKind = k;
                bestKey = key;
            }
            kind = bestKind;
            return best;
        }

        static bool Holds(Image img, Vector3 world)
        {
            var rt = img.rectTransform;
            Vector2 p = rt.InverseTransformPoint(world);
            var r = rt.rect;
            if (img.type == Image.Type.Filled && img.fillMethod == Image.FillMethod.Horizontal)
            {
                float w = r.width * Mathf.Clamp01(img.fillAmount);
                r = img.fillOrigin == 0 ? new Rect(r.xMin, r.yMin, w, r.height) : new Rect(r.xMax - w, r.yMin, w, r.height);
            }
            return r.Contains(p);
        }

        /// <summary>A sort key that follows UGUI's draw order under one root canvas: a nested canvas with its own sorting
        /// draws by its order, the rest in hierarchy order (depth first).</summary>
        static long DrawKey(Transform t, Transform root)
        {
            int order = 0;
            for (var tr = t; tr != null && tr != root.parent; tr = tr.parent)
            {
                var c = tr.GetComponent<Canvas>();
                if (c != null && c.overrideSorting && tr != root) { order = c.sortingOrder; break; }
            }
            return ((long)(order + 32768) << 32) | (uint)HierarchyIndex(t, root);
        }

        static readonly Dictionary<Transform, int> _index = new Dictionary<Transform, int>(1024);
        static Transform _indexed;

        /// <summary>`t`'s place in a depth-first walk of `root` (cached per root for one check).</summary>
        static int HierarchyIndex(Transform t, Transform root)
        {
            if (_indexed != root)
            {
                _index.Clear();
                _indexed = root;
                int i = 0;
                Walk(root, ref i);
            }
            return _index.TryGetValue(t, out int k) ? k : int.MaxValue;
        }

        static void Walk(Transform t, ref int i)
        {
            _index[t] = i++;
            for (int c = 0; c < t.childCount; c++) Walk(t.GetChild(c), ref i);
        }

        /// <summary>The label's rendered text, in its own local units: the visible glyphs' ink (each glyph's box from its
        /// metrics: TMP's textBounds spans the font's ascent and descent lines, far above Marcellus' capitals, and would
        /// flag plates whose letters sit well clear of their frame). False when nothing is drawn.</summary>
        public static bool TryInkBounds(TMP_Text t, out Rect ink)
        {
            ink = default;
            t.ForceMeshUpdate();
            var info = t.textInfo;
            if (info == null) return false;
            int count = Mathf.Min(info.characterCount, info.characterInfo.Length);
            float xMin = float.MaxValue, yMin = float.MaxValue, xMax = float.MinValue, yMax = float.MinValue;
            for (int i = 0; i < count; i++)
            {
                var ch = info.characterInfo[i];
                if (!ch.isVisible || ch.textElement == null) continue;
                var g = ch.textElement.glyph;
                if (g == null) continue;
                var m = g.metrics;
                float s = ch.scale;
                float x0 = ch.origin + m.horizontalBearingX * s, y1 = ch.baseLine + m.horizontalBearingY * s;
                float x1 = x0 + m.width * s, y0 = y1 - m.height * s;
                if (ch.elementType == TMP_TextElementType.Sprite)
                {
                    x0 = ch.bottomLeft.x; x1 = ch.topRight.x; y0 = ch.bottomLeft.y; y1 = ch.topRight.y;
                }
                if (x1 <= x0 || y1 <= y0) continue;
                xMin = Mathf.Min(xMin, x0); xMax = Mathf.Max(xMax, x1);
                yMin = Mathf.Min(yMin, y0); yMax = Mathf.Max(yMax, y1);
            }
            if (xMax < xMin) return false;
            ink = Rect.MinMaxRect(xMin, yMin, xMax, yMax);
            return true;
        }

        static readonly Vector3[] _ink = new Vector3[2];

        static bool TryInkWorld(TMP_Text t, out Vector3[] ink)
        {
            ink = _ink;
            if (!TryInkBounds(t, out var r)) return false;
            ink[0] = t.transform.TransformPoint(new Vector3(r.xMin, r.yMin, 0f));
            ink[1] = t.transform.TransformPoint(new Vector3(r.xMax, r.yMax, 0f));
            return true;
        }

        static string Short(string s) => s.Length <= 40 ? s : s.Substring(0, 40);

        static string Path(Component c)
        {
            var t = c.transform;
            var p = t.parent;
            return p != null ? p.name + "/" + t.name : t.name;
        }
    }
}
