using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gladiator.UI
{
    /// <summary>Builds UGUI hierarchies in code (reproducible, no prefab YAML). Reference resolution 1920×1080.</summary>
    public static class UiFactory
    {
        public static Canvas Canvas(string name, Transform parent, int sortOrder)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(parent, false);
            var c = go.GetComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = sortOrder;
            var s = go.GetComponent<CanvasScaler>();
            s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            s.referenceResolution = new Vector2(1920, 1080);
            s.matchWidthOrHeight = 0.5f;
            return c;
        }

        public static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        public static void Stretch(RectTransform rt, Vector2 min, Vector2 max, Vector2 offMin, Vector2 offMax)
        {
            rt.anchorMin = min; rt.anchorMax = max; rt.offsetMin = offMin; rt.offsetMax = offMax;
        }

        public static Image Image(string name, Transform parent, Color color, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, Sprite sprite = null)
        {
            var rt = Rect(name, parent, anchor, pivot, pos, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.sprite = sprite;
            img.raycastTarget = false;
            return img;
        }

        public static TextMeshProUGUI Text(string name, Transform parent, string text, float size, Color color, TextAlignmentOptions align,
            Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 box, TMP_FontAsset font, Material mat)
        {
            var rt = Rect(name, parent, anchor, pivot, pos, box);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            if (mat != null) t.fontSharedMaterial = mat;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.fontStyle = FontStyles.Bold;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.raycastTarget = false;
            return t;
        }

        static readonly Color ButtonColor = new Color(0.16f, 0.2f, 0.25f, 0.98f);

        /// <summary>Flat button with a centred label. Returns the button; the label is its child "Label".</summary>
        public static Button Button(string name, Transform parent, string label, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size,
            TMP_FontAsset font, Material mat, UnityEngine.Events.UnityAction onClick, float fontSize = 24f)
        {
            var img = Image(name, parent, ButtonColor, anchor, pivot, pos, size, PixelIcons.White);
            img.raycastTarget = true;
            var btn = img.gameObject.AddComponent<Button>();
            var colors = btn.colors;
            colors.highlightedColor = new Color(1.35f, 1.35f, 1.35f);
            colors.pressedColor = new Color(0.7f, 0.95f, 1f);
            colors.selectedColor = Color.white;
            colors.fadeDuration = 0.05f;
            btn.colors = colors;
            btn.navigation = new Navigation { mode = Navigation.Mode.None };
            if (onClick != null) btn.onClick.AddListener(onClick);
            var t = Text("Label", img.transform, label, fontSize, Color.white, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size, font, mat);
            t.fontStyle = FontStyles.Normal;
            return btn;
        }

        public static TextMeshProUGUI Label(Button b) => b.GetComponentInChildren<TextMeshProUGUI>();

        /// <summary>Background + horizontally filled foreground. Returns the fill image.</summary>
        public static Image Bar(string name, Transform parent, Color bg, Color fill, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var back = Image(name, parent, bg, anchor, pivot, pos, size, PixelIcons.White);
            var f = Image(name + "Fill", back.transform, fill, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(3, 0), size - new Vector2(6, 6), PixelIcons.White);
            f.type = UnityEngine.UI.Image.Type.Filled;
            f.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
            f.fillOrigin = 0;
            f.fillAmount = 1f;
            return f;
        }
    }

    /// <summary>
    /// Tiny point-filtered pixel icons generated in code (no third-party art). Cached with Unity's null check (not ??=)
    /// and flagged DontUnloadUnusedAsset: a scene load (Retry) unloads unreferenced runtime assets, and with domain
    /// reload off the statics outlive a play session — either way the HUD showed white squares.
    /// </summary>
    public static class PixelIcons
    {
        static Sprite _white, _coin, _skull, _heart, _chevron, _laurel;
        static Sprite _tessera, _plate, _hole, _crack, _bust, _wreath, _sigil, _lock;

        public static Sprite White => _white != null ? _white : _white = Make(new[] { "#" }, Color.white, Color.white);

        public static Sprite Coin => _coin != null ? _coin : _coin = Make(new[]
        {
            "..####..",
            ".#OOOO#.",
            "#OOYYOO#",
            "#OYOOOO#",
            "#OYOOOO#",
            "#OOYYOO#",
            ".#OOOO#.",
            "..####..",
        }, new Color(1f, 0.82f, 0.25f), new Color(0.72f, 0.53f, 0.16f), new Color(1f, 0.95f, 0.6f));

        public static Sprite Skull => _skull != null ? _skull : _skull = Make(new[]
        {
            ".######.",
            "#OOOOOO#",
            "#O##O##O",
            "#O##O##O",
            "#OOO#OOO",
            ".#OOOOO#",
            "..#O#O#.",
            "..#####.",
        }, new Color(0.95f, 0.93f, 0.88f), new Color(0.12f, 0.1f, 0.1f));

        public static Sprite Heart => _heart != null ? _heart : _heart = Make(new[]
        {
            ".##..##.",
            "#RR##RR#",
            "#RRRRRR#",
            "#RRRRRR#",
            ".#RRRR#.",
            "..#RR#..",
            "...##...",
            "........",
        }, new Color(1f, 0.33f, 0.3f), new Color(0.35f, 0.05f, 0.08f));

        /// <summary>A laurel wreath with a gold ribbon (the crowd's Favor).</summary>
        public static Sprite Laurel => _laurel != null ? _laurel : _laurel = Make(new[]
        {
            "..#O..O#..",
            ".#O#..#O#.",
            "#O#....#O#",
            "#O#....#O#",
            ".#O#..#O#.",
            "..#OYYO#..",
            "...#YY#...",
            "..#Y##Y#..",
        }, new Color(0.45f, 0.78f, 0.3f), new Color(0.12f, 0.24f, 0.08f), new Color(1f, 0.82f, 0.3f));

        /// <summary>Arrow pointing up (+Y), white so Image.color tints it; rotate to aim.</summary>
        public static Sprite Chevron => _chevron != null ? _chevron : _chevron = Make(new[]
        {
            "....##....",
            "...#OO#...",
            "..#OOOO#..",
            ".#OOOOOO#.",
            "#OOO##OOO#",
            "#OO#..#OO#",
            "#O#....#O#",
            "##......##",
        }, Color.white, new Color(0.1f, 0.06f, 0.05f));

        // ---- R9: the select screens' tesserae and the lanista's tablet --------------------------------------------

        /// <summary>A bone tessera: chamfered top corners, a shaded foot, a dark rim. White-based: tint it (bone open,
        /// charcoal locked, gold for the picked frame). 9-sliced, 5 canvas units per art pixel.</summary>
        public static Sprite Tessera => _tessera != null ? _tessera : _tessera = Make(new[]
        {
            "..######..",
            ".#OOOOOO#.",
            "#OOOOOOOO#",
            "#OOOOOOOO#",
            "#OOOOOOOO#",
            "#OOOOOOOO#",
            "#OOOOOOOO#",
            "#YYYYYYYY#",
            "#YYYYYYYY#",
            "##########",
        }, Color.white, new Color(0.56f, 0.5f, 0.44f), new Color(0.82f, 0.79f, 0.74f), new Vector4(3, 3, 3, 3), 20f);

        /// <summary>The tessera's shape filled flat (no rim, no shade): the gold or pale frame drawn behind a tile.</summary>
        public static Sprite Plate => _plate != null ? _plate : _plate = Make(new[]
        {
            "..OOOOOO..",
            ".OOOOOOOO.",
            "OOOOOOOOOO",
            "OOOOOOOOOO",
            "OOOOOOOOOO",
            "OOOOOOOOOO",
            "OOOOOOOOOO",
            "OOOOOOOOOO",
            "OOOOOOOOOO",
            "OOOOOOOOOO",
        }, Color.white, Color.white, null, new Vector4(3, 3, 3, 3), 20f);

        /// <summary>The cord hole in a tessera's head.</summary>
        public static Sprite Hole => _hole != null ? _hole : _hole = Make(new[]
        {
            ".###.",
            "#OOO#",
            "#OOO#",
            "#OOO#",
            ".###.",
        }, new Color(0.12f, 0.1f, 0.09f), new Color(0.42f, 0.36f, 0.3f));

        /// <summary>Cracks across a locked tessera (transparent, ink lines).</summary>
        public static Sprite Crack => _crack != null ? _crack : _crack = Make(new[]
        {
            "........#...",
            ".......#....",
            ".......#....",
            "......##....",
            ".....#..#...",
            "....#....#..",
            "....#.....#.",
            "...##.......",
            "..#.........",
            "..#.........",
            ".#..........",
            ".##.........",
            "#..#........",
            "....#.......",
            "............",
            "............",
        }, Color.white, new Color(0.08f, 0.07f, 0.06f));

        /// <summary>A head-and-shoulders silhouette: the tile's picture when no portrait is baked. White: tint it.</summary>
        public static Sprite Bust => _bust != null ? _bust : _bust = Make(new[]
        {
            "....OOOO....",
            "...OOOOOO...",
            "..OOOOOOOO..",
            "..OOOOOOOO..",
            "..OOOOOOOO..",
            "...OOOOOO...",
            "....OOOO....",
            "..OOOOOOOO..",
            ".OOOOOOOOOO.",
            "OOOOOOOOOOOO",
            "OOOOOOOOOOOO",
            "OOOOOOOOOOOO",
        }, Color.white, Color.white);

        /// <summary>A gold laurel wreath on a crimson ribbon: hung on the picked tessera's cord, and a beaten arena's mark.</summary>
        public static Sprite Wreath => _wreath != null ? _wreath : _wreath = Make(new[]
        {
            "..#O....O#..",
            ".#O#....#O#.",
            "#O#......#O#",
            "#O#......#O#",
            ".#O#....#O#.",
            "..#OO..OO#..",
            "...##YY##...",
            "....#YY#....",
            "...#Y##Y#...",
        }, new Color(0.97f, 0.85f, 0.42f), new Color(0.6f, 0.42f, 0.12f), new Color(0.65f, 0.11f, 0.19f));

        /// <summary>A small bronze shield: a trait's mark on the tablet.</summary>
        public static Sprite Sigil => _sigil != null ? _sigil : _sigil = Make(new[]
        {
            ".######.",
            "#OOOOOO#",
            "#OYYYYO#",
            "#OYOOYO#",
            "#OYOOYO#",
            "#OYYYYO#",
            "#OOOOOO#",
            ".######.",
        }, new Color(0.69f, 0.48f, 0.23f), new Color(0.3f, 0.19f, 0.08f), new Color(0.97f, 0.85f, 0.42f));

        /// <summary>An iron padlock: a locked arena.</summary>
        public static Sprite Lock => _lock != null ? _lock : _lock = Make(new[]
        {
            "..###..",
            ".#...#.",
            ".#...#.",
            "#######",
            "#OOOOO#",
            "#OO#OO#",
            "#OO#OO#",
            "#OOOOO#",
            "#######",
        }, new Color(0.55f, 0.58f, 0.62f), new Color(0.15f, 0.16f, 0.18f));

        /// <summary>'#' = outline colour, letters = fill (O/R) or highlight (Y), '.' = transparent. A non-zero `border`
        /// (left, bottom, right, top, in art pixels) makes a 9-sliceable sprite.</summary>
        static Sprite Make(string[] rows, Color fill, Color outline, Color? highlight = null, Vector4 border = default, float pixelsPerUnit = 8f)
        {
            int h = rows.Length, w = rows[0].Length;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    char c = rows[h - 1 - y][x];
                    Color col = c == '.' ? Color.clear : c == '#' ? (w == 1 ? fill : outline) : c == 'Y' ? (highlight ?? fill) : fill;
                    tex.SetPixel(x, y, col);
                }
            tex.Apply();
            tex.hideFlags = HideFlags.DontUnloadUnusedAsset;
            var sprite = border == default
                ? Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), pixelsPerUnit)
                : Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), pixelsPerUnit, 0, SpriteMeshType.FullRect, border);
            sprite.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return sprite;
        }
    }
}
