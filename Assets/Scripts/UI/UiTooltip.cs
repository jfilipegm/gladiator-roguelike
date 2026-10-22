using Gladiator.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gladiator.UI
{
    /// <summary>
    /// P3 (the producer's notes 15, 17 and 19): a hover tooltip on a wax tablet: a name (Title), one short line under it
    /// ("LV 3 · RARE", a stat's total), and a body (Body, rich text, wrapped). A plain class built and placed by its owner
    /// (InventoryPanel, StatsPanel), like VexillumBanner: no component on any scene. Every label sits inside the wax's
    /// ContentRect (UiKit.ContentPad), the height follows the body, and Place keeps it on the canvas. The words are
    /// written into the three CharBuffers and handed to TMP (pre-sized labels, no allocation); shown and hidden with a
    /// CanvasGroup's alpha.
    /// </summary>
    public sealed class UiTooltip
    {
        public const float DefaultWidth = 410f;
        /// <summary>The body's tallest (canvas units at scale 1): longer text is cut, never the frame off the screen.</summary>
        public const float MaxBodyHeight = 420f;
        const float TitleSize = 26f, SubSize = 18f, BodySize = 20f, Gap = 6f, ScreenGap = 8f;

        readonly CanvasGroup _group;
        readonly TextMeshProUGUI _title, _sub, _body;
        readonly Vector4 _inset; // the wax's padding + ContentPad (left, bottom, right, top)
        readonly float _width, _titleH, _subH;
        float _height;

        /// <summary>The words: fill these, then call Apply.</summary>
        public readonly CharBuffer Title = new CharBuffer(96), Sub = new CharBuffer(96), Body = new CharBuffer(1024);

        public RectTransform Root { get; }
        public bool Shown { get; private set; }
        /// <summary>The tooltip's size at scale 1 (its height follows the body).</summary>
        public Vector2 Size => new Vector2(_width, _height);
        /// <summary>Tests: what it says (the title, the line under it and the body, one per line).</summary>
        public string Text => Title.ToString() + "\n" + Sub.ToString() + "\n" + Body.ToString();
        public string TitleText => Title.ToString();
        public Component TitleLabel => _title;
        public Component BodyLabel => _body;

        /// <param name="parent">A canvas's root (the tooltip is placed in that canvas's centred units).</param>
        public UiTooltip(Transform parent, string name, float width = DefaultWidth)
        {
            var th = UiTheme.Current;
            _width = width;
            var mid = new Vector2(0.5f, 0.5f);
            var tl = new Vector2(0f, 1f);
            Root = UiFactory.Rect(name, parent, mid, tl, Vector2.zero, new Vector2(width, 160f));
            _group = Root.gameObject.AddComponent<CanvasGroup>();
            _group.blocksRaycasts = false;
            _group.interactable = false;
            var frame = UiKit.Frame("Frame", Root, FrameKind.Wax, mid, mid, Vector2.zero, Vector2.zero, Color.white);
            UiFactory.Stretch(frame.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var pad = UiFrames.Padding(FrameKind.Wax);
            float p = UiKit.ContentPad;
            _inset = new Vector4(pad.x + p, pad.y + p, pad.z + p, pad.w + p);
            float inner = width - _inset.x - _inset.z;
            // each line as tall as its textBounds (TextBox): the padding rule holds for the real fonts
            _titleH = Mathf.Ceil(TextBox.Height(TextRole.Title, TitleSize));
            _subH = Mathf.Ceil(TextBox.Height(TextRole.Body, SubSize));
            // the wax is dark: light inks (the surface rule, P3-contract "Surfaces and contrast")
            _title = UiKit.Text("Title", Root, "", TextRole.Title, TitleSize, th.goldBright, TextAlignmentOptions.Left, tl, tl,
                new Vector2(_inset.x, -_inset.w), new Vector2(inner, _titleH));
            _title.enableAutoSizing = true; // "Gilded Mercenary's Purse" shrinks before it is cut
            _title.fontSizeMin = 18f;
            _title.fontSizeMax = TitleSize;
            _sub = UiKit.Text("Sub", Root, "", TextRole.Body, SubSize, th.dust, TextAlignmentOptions.Left, tl, tl,
                new Vector2(_inset.x, -_inset.w - _titleH), new Vector2(inner, _subH));
            _sub.richText = true;
            _body = UiKit.Text("Body", Root, "", TextRole.Body, BodySize, th.ink, TextAlignmentOptions.TopLeft, tl, tl,
                new Vector2(_inset.x, -_inset.w - _titleH - _subH - Gap), new Vector2(inner, 60f));
            _body.richText = true;
            _body.textWrappingMode = TextWrappingModes.Normal;
            Presize();
            Hide();
        }

        /// <summary>Grow TMP's buffers once past the longest words (no allocation when shown in a run).</summary>
        void Presize()
        {
            const string Glyphs = "ABCDEFGHIJKLMNOPQRSTUVWXYZ abcdefghijklmnopqrstuvwxyz 0123456789 +-−→×·%/()[]:'’!?.,&—=";
            var sb = new System.Text.StringBuilder(Body.Chars.Length + 96);
            while (sb.Length < Body.Chars.Length) sb.Append(Glyphs).Append('\n');
            string body = sb.ToString();
            sb.Length = 0;
            while (sb.Length < Title.Chars.Length) sb.Append(Glyphs);
            string line = sb.ToString(0, Title.Chars.Length);
            _body.SetText(body);
            _body.ForceMeshUpdate(true, true);
            _body.GetPreferredValues(_width, 32767f); // TMP sizes the arrays it measures with separately: grow them now too
            _title.SetText(line);
            _title.ForceMeshUpdate(true, true);
            _sub.SetText(line);
            _sub.ForceMeshUpdate(true, true);
            _body.SetText("");
            _title.SetText("");
            _sub.SetText("");
        }

        /// <summary>Hands the three buffers to the labels and fits the height to the body (call when the words change,
        /// not every frame).</summary>
        public void Apply()
        {
            _title.SetCharArray(Title.Chars, 0, Title.Length);
            _sub.SetCharArray(Sub.Chars, 0, Sub.Length);
            _body.SetCharArray(Body.Chars, 0, Body.Length);
            float inner = _width - _inset.x - _inset.z;
            float bodyH = Body.Length > 0 ? Mathf.Min(MaxBodyHeight, Mathf.Ceil(_body.GetPreferredValues(inner, 32767f).y) + 2f) : 0f;
            _body.rectTransform.sizeDelta = new Vector2(inner, Mathf.Max(1f, bodyH));
            _height = _inset.w + _titleH + _subH + (bodyH > 0f ? Gap + bodyH : 0f) + _inset.y;
            Root.sizeDelta = new Vector2(_width, _height);
        }

        /// <summary>
        /// Shows it with its `pivot` at `pos` (the canvas's centred units), at `scale`, moved as little as needed to stay
        /// 8 units inside a canvas of `canvas` units.
        /// </summary>
        public void Place(Vector2 pos, Vector2 pivot, float scale, Vector2 canvas)
        {
            if (canvas.x < 1f || canvas.y < 1f) canvas = MenuLayout.Reference;
            var size = new Vector2(_width, _height) * scale;
            float left = pos.x - pivot.x * size.x, top = pos.y + (1f - pivot.y) * size.y;
            float minX = -canvas.x * 0.5f + ScreenGap, maxX = canvas.x * 0.5f - ScreenGap - size.x;
            float maxTop = canvas.y * 0.5f - ScreenGap, minTop = -canvas.y * 0.5f + ScreenGap + size.y;
            left = Mathf.Clamp(left, minX, Mathf.Max(minX, maxX));
            top = Mathf.Clamp(top, Mathf.Min(minTop, maxTop), maxTop);
            Root.localScale = new Vector3(scale, scale, 1f);
            Root.anchoredPosition = new Vector2(left, top); // the root's pivot is its top-left corner
            Show();
        }

        public void Show()
        {
            Shown = true;
            if (_group.alpha != 1f) _group.alpha = 1f;
        }

        public void Hide()
        {
            Shown = false;
            if (_group.alpha != 0f) _group.alpha = 0f;
        }

        /// <summary>Its rect in the canvas's centred units (tests: it stays on the screen).</summary>
        public Rect CanvasRect
        {
            get
            {
                float s = Root.localScale.x;
                var p = Root.anchoredPosition;
                return new Rect(p.x, p.y - _height * s, _width * s, _height * s);
            }
        }
    }
}

namespace Gladiator.UI
{
    /// <summary>
    /// P3: how tall a one-line label's TMP textBounds is (the padding rule measures it): TMP spans every visible
    /// character from its font's ascent line to its descent line (TMP_Text.GetTextBounds), scaled by size / pointSize ×
    /// faceInfo.scale, whatever the glyphs. In-run layouts size their rows from this at build time, so they follow the
    /// theme's real fonts. With no font yet it guesses 1.3 em (the theme's fonts sit between 1.2 and 1.4).
    /// </summary>
    public static class TextBox
    {
        const float Guess = 1.3f, CapGuess = 0.7f;

        static bool Face(TextRole role, out UnityEngine.TextCore.FaceInfo face)
        {
            face = default;
            var font = UiTheme.Current.FontFor(role);
            if (font == null) return false;
            face = font.faceInfo;
            return face.pointSize > 0f && face.ascentLine > face.descentLine;
        }

        static float Scale(in UnityEngine.TextCore.FaceInfo face, float size) => size / face.pointSize * (face.scale > 0f ? face.scale : 1f);

        /// <summary>One line's textBounds height for `role` at `size`.</summary>
        public static float Height(TextRole role, float size) =>
            Face(role, out var f) ? (f.ascentLine - f.descentLine) * Scale(f, size) : Guess * size;

        /// <summary>
        /// How far above the capitals' middle the textBounds box's middle sits. A Midline (Geometry) label centres its
        /// glyphs (numbers, capitals) on its rect, so its box is shifted by this much.
        /// </summary>
        public static float MidlineShift(TextRole role, float size)
        {
            if (!Face(role, out var f)) return (Guess - 1f - CapGuess) * 0.5f * size;
            float cap = f.capLine > 0f ? f.capLine : CapGuess * (f.ascentLine - f.descentLine);
            return (f.ascentLine + f.descentLine - cap) * 0.5f * Scale(f, size);
        }

        /// <summary>The row a Midline label needs so its whole box stays inside the row (its glyphs centred on it).</summary>
        public static float MidlineRow(TextRole role, float size) => Height(role, size) + 2f * Mathf.Abs(MidlineShift(role, size));
    }
}
