using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UI;

namespace Gladiator.UI
{
    /// <summary>Every frame in the atlas (docs/tracks/U1-contract.md "UiFrames"). Append, never reorder: the index file
    /// names its lines after these.</summary>
    public enum FrameKind : byte
    {
        AnsataBronze, AnsataMarble, Marble, Bronze, Wax, Mosaic, Stone, MeanderGold, MeanderDark,
        CardStone, CardBronze, CardSilver, CardGold, Halo, Focus,
        Scutum, ScutumLocked, ScutumSpine, Clipeus, ClipeusLocked,
        VexillumBar, VexillumCrimson, VexillumPurple,
        Trough, TroughFill, Knob, Chip, KeyChip, LaurelBranch, LaurelWreath,
        // P3 (docs/tracks/P3-contract.md): the shape-following focus glow for both ansata plates, the NEW seal, the pilum
        // arrows and the HUD's 16 px icons
        AnsataGlow, SealNew, ArrowLeft, ArrowRight, IconHeart, IconAureus, IconSkullGalea,
    }

    /// <summary>One line of the frame index: the cell (from the PNG's top-left), borders and paddings (left, bottom,
    /// right, top), all in art pixels, and how the Image draws it.</summary>
    public struct FrameEntry
    {
        public string Name;
        public RectInt Rect;
        public Vector4 Border, Padding;
        public Image.Type Type;
    }

    /// <summary>
    /// U1: the Roman frames, cut at runtime from the one point-filtered atlas PixelSrc/ui_frames.py draws (one texture,
    /// so the UI batches). The index (`UiFrames.txt`, the theme's `frameIndex`) is parsed once; each sprite is made on
    /// first use with its 9-slice border at 3 canvas units per art pixel, cached by kind, and flagged
    /// DontUnloadUnusedAsset. A missing atlas or line falls back to PixelIcons.White (logged once per kind), so screens
    /// still build before the setup has run.
    /// </summary>
    public static class UiFrames
    {
        /// <summary>Canvas units per art pixel: whole screen pixels at 1080p (3) and 720p (2). Sprites' pixelsPerUnit =
        /// 100 / ArtScale (UGUI's reference pixels per unit is 100).</summary>
        public const float ArtScale = 3f;

        public static readonly int KindCount = Enum.GetValues(typeof(FrameKind)).Length;

        static readonly Sprite[] _sprites = new Sprite[KindCount];
        static readonly FrameEntry[] _entries = new FrameEntry[KindCount];
        static readonly bool[] _has = new bool[KindCount];
        static readonly bool[] _warned = new bool[KindCount];
        static TextAsset _parsedIndex;
        static Texture2D _spriteAtlas;
        static bool _parsed;

        // Which kind each themed Image shows (for KindOf), kept without a component so it works with or without the
        // atlas (every missing kind shares PixelIcons.White). Weak keys: a destroyed Image's entry goes with it.
        sealed class KindTag { public FrameKind Kind; }
        static readonly ConditionalWeakTable<Image, KindTag> _kinds = new ConditionalWeakTable<Image, KindTag>();

        /// <summary>Forget the parsed index and the sprites (UiTheme.Reload calls this).</summary>
        public static void Reload()
        {
            _parsed = false;
            _parsedIndex = null;
            _spriteAtlas = null;
            for (int i = 0; i < KindCount; i++) { _sprites[i] = null; _has[i] = false; _warned[i] = false; }
        }

        static readonly List<FrameEntry> _parseBuffer = new List<FrameEntry>(32);

        static void EnsureIndex()
        {
            var theme = UiTheme.Current;
            var index = theme.frameIndex;
            if (_parsed && _parsedIndex == index) return;
            for (int i = 0; i < KindCount; i++) { _has[i] = false; _sprites[i] = null; }
            _parsed = true;
            _parsedIndex = index;
            if (index == null) return;
            _parseBuffer.Clear();
            ParseIndex(index.text, _parseBuffer);
            foreach (var e in _parseBuffer)
            {
                if (!Enum.TryParse(e.Name, false, out FrameKind k))
                {
                    Debug.LogWarning("[UiFrames] the index names an unknown frame: " + e.Name);
                    continue;
                }
                _entries[(int)k] = e;
                _has[(int)k] = true;
            }
        }

        static bool TryEntry(FrameKind k, out FrameEntry e)
        {
            EnsureIndex();
            e = _entries[(int)k];
            return _has[(int)k];
        }

        /// <summary>The frame's sprite (sliced/tiled border from the index), or PixelIcons.White without the atlas.</summary>
        public static Sprite Get(FrameKind k)
        {
            EnsureIndex();
            int i = (int)k;
            var atlas = UiTheme.Current.frameAtlas;
            if (atlas != _spriteAtlas)
            {
                for (int j = 0; j < KindCount; j++) _sprites[j] = null;
                _spriteAtlas = atlas;
            }
            var s = _sprites[i];
            if (s != null) return s;
            if (atlas == null || !_has[i])
            {
                if (!_warned[i])
                {
                    _warned[i] = true;
                    Debug.LogWarning(atlas == null
                        ? "[UiFrames] no frame atlas in the UI theme (run Gladiator/Setup/UI/All): " + k + " shows plain white"
                        : "[UiFrames] the frame index has no line for " + k + " (run PixelSrc/ui_frames.py)");
                }
                return PixelIcons.White;
            }
            var e = _entries[i];
            var rect = new Rect(e.Rect.x, atlas.height - e.Rect.y - e.Rect.height, e.Rect.width, e.Rect.height);
            s = Sprite.Create(atlas, rect, new Vector2(0.5f, 0.5f), 100f / ArtScale, 0, SpriteMeshType.FullRect, e.Border);
            s.name = "UiFrame_" + k;
            s.hideFlags = HideFlags.DontUnloadUnusedAsset;
            _sprites[i] = s;
            return s;
        }

        /// <summary>How an Image draws the frame (from the index): Sliced, Tiled or Simple.</summary>
        public static Image.Type TypeOf(FrameKind k) => TryEntry(k, out var e) ? e.Type : Image.Type.Simple;

        /// <summary>The art's size in canvas units (size Simple kinds from this, never hard-coded).</summary>
        public static Vector2 NativeSize(FrameKind k) =>
            TryEntry(k, out var e) ? new Vector2(e.Rect.width, e.Rect.height) * ArtScale : FallbackSize(k) * ArtScale;

        /// <summary>Content insets (left, bottom, right, top) in canvas units.</summary>
        public static Vector4 Padding(FrameKind k) => TryEntry(k, out var e) ? e.Padding * ArtScale : Vector4.zero;

        /// <summary>The border (left, bottom, right, top) in canvas units.</summary>
        public static Vector4 Border(FrameKind k) => TryEntry(k, out var e) ? e.Border * ArtScale : Vector4.zero;

        /// <summary>Card frames, the focus ring and the ansata glow are rims: their centre stays empty.</summary>
        public static bool FillsCenter(FrameKind k) =>
            k != FrameKind.CardStone && k != FrameKind.CardBronze && k != FrameKind.CardSilver && k != FrameKind.CardGold
            && k != FrameKind.Focus && k != FrameKind.AnsataGlow;

        /// <summary>Gives `img` the frame: sprite, type, fillCenter (false for card frames and Focus). Keeps its colour.</summary>
        public static Image Apply(Image img, FrameKind k)
        {
            img.sprite = Get(k);
            img.type = TypeOf(k);
            img.fillCenter = FillsCenter(k);
            img.pixelsPerUnitMultiplier = 1f;
            _kinds.GetOrCreateValue(img).Kind = k;
            return img;
        }

        /// <summary>Tests: the FrameKind an Image shows, or -1.</summary>
        public static int KindOf(Component image)
        {
            var img = image as Image;
            if (img == null) return -1;
            if (_kinds.TryGetValue(img, out var tag) && img.sprite == Get(tag.Kind)) return (int)tag.Kind;
            if (img.sprite == null || img.sprite == PixelIcons.White) return -1;
            for (int i = 0; i < KindCount; i++)
                if (_sprites[i] == img.sprite) return i;
            return -1;
        }

        static readonly char[] Space = { ' ', '\t' };

        /// <summary>Parses index text, appending to `into`, and returns how many lines it read.
        /// Format: <c>name x y w h bl bb br bt type pl pb pr pt</c>; '#' starts a comment; a bad line is skipped with a
        /// warning.</summary>
        public static int ParseIndex(string text, List<FrameEntry> into)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            int n = 0, lineNo = 0;
            foreach (var raw in text.Split('\n'))
            {
                lineNo++;
                var line = raw.Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                var p = line.Split(Space, StringSplitOptions.RemoveEmptyEntries);
                var v = new int[12];
                bool ok = p.Length == 14;
                for (int i = 0; ok && i < 12; i++)
                {
                    int src = i < 8 ? i + 1 : i + 2; // the type sits between the borders and the paddings (token 9)
                    ok = int.TryParse(p[src], out v[i]) && v[i] >= 0;
                }
                Image.Type type = Image.Type.Simple;
                if (ok)
                {
                    switch (p[9])
                    {
                        case "sliced": type = Image.Type.Sliced; break;
                        case "tiled": type = Image.Type.Tiled; break;
                        case "simple": type = Image.Type.Simple; break;
                        default: ok = false; break;
                    }
                }
                if (!ok || v[2] == 0 || v[3] == 0)
                {
                    Debug.LogWarning($"[UiFrames] skipped a bad index line {lineNo}: \"{line}\"");
                    continue;
                }
                into.Add(new FrameEntry
                {
                    Name = p[0],
                    Rect = new RectInt(v[0], v[1], v[2], v[3]),
                    Border = new Vector4(v[4], v[5], v[6], v[7]),
                    Type = type,
                    Padding = new Vector4(v[8], v[9], v[10], v[11]),
                });
                n++;
            }
            return n;
        }

        /// <summary>The art sizes the index ships with, so layouts keep their proportions before the setup has run.</summary>
        static Vector2 FallbackSize(FrameKind k)
        {
            switch (k)
            {
                case FrameKind.AnsataBronze: case FrameKind.AnsataMarble: return new Vector2(30, 22);
                case FrameKind.Marble: case FrameKind.Wax: return new Vector2(24, 24);
                case FrameKind.Bronze: return new Vector2(20, 20);
                case FrameKind.Mosaic: return new Vector2(25, 25);
                case FrameKind.Stone: return new Vector2(12, 12);
                case FrameKind.MeanderGold: case FrameKind.MeanderDark: return new Vector2(6, 5);
                case FrameKind.CardStone: case FrameKind.CardBronze: case FrameKind.CardSilver: case FrameKind.CardGold:
                    return new Vector2(18, 26);
                case FrameKind.Halo: return new Vector2(16, 16);
                case FrameKind.Focus: return new Vector2(6, 6);
                case FrameKind.Scutum: case FrameKind.ScutumLocked: return new Vector2(30, 40);
                case FrameKind.ScutumSpine: return new Vector2(4, 4);
                case FrameKind.Clipeus: case FrameKind.ClipeusLocked: return new Vector2(36, 36);
                case FrameKind.VexillumBar: return new Vector2(30, 8);
                case FrameKind.VexillumCrimson: case FrameKind.VexillumPurple: return new Vector2(254, 40);
                case FrameKind.Trough: return new Vector2(12, 8);
                case FrameKind.TroughFill: return new Vector2(2, 4);
                case FrameKind.Knob: return new Vector2(6, 8);
                case FrameKind.Chip: case FrameKind.KeyChip: return new Vector2(8, 8);
                case FrameKind.LaurelBranch: return new Vector2(20, 24);
                case FrameKind.LaurelWreath: return new Vector2(60, 30);
                case FrameKind.AnsataGlow: return new Vector2(34, 26);
                case FrameKind.SealNew: return new Vector2(12, 12);
                case FrameKind.ArrowLeft: case FrameKind.ArrowRight: return new Vector2(16, 12);
                case FrameKind.IconHeart: case FrameKind.IconAureus: case FrameKind.IconSkullGalea: return new Vector2(16, 16);
                default: return new Vector2(16, 16);
            }
        }
    }
}
