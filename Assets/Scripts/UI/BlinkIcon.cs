using Gladiator.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gladiator.UI
{
    /// <summary>
    /// Note 35: the Lightning Dash's bolt, 16 px art written as text like PixelIcons (no third-party art), in the upgrade
    /// icons' palette (PixelSrc/upgrade_icons.py: white, cyan, deep blue, a dark navy outline). Lit when the blink is
    /// ready, a grey twin while it recharges. Cached with Unity's null check and flagged DontUnloadUnusedAsset (see
    /// PixelIcons). Drawn at 32 × 32 on the HUD: 2 canvas units per art pixel, as the plate's 16 px icons.
    /// </summary>
    public static class BlinkIconArt
    {
        // '#' outline, 'W' lit edge (up-left), 'C' body, 'B' shaded edge (down-right), '.' transparent
        static readonly string[] Bolt =
        {
            "......######....",
            ".....#WWWWWW#...",
            "....#WCCCCCB#...",
            "....#WCCCCB#....",
            "...#WCCCCB#.....",
            "...#WCCCCB####..",
            "..#WCCCCCWWWWC#.",
            "..#WCCCCCCCCB#..",
            "..#CBBBBCCCB#...",
            "...####WCCB#....",
            "......#WCB#.....",
            ".....#WCB#......",
            ".....#WB#.......",
            "....#WB#........",
            "....#B#.........",
            "....##..........",
        };

        static Sprite _lit, _dim;

        /// <summary>The bolt lit: white, cyan and deep blue on a navy outline.</summary>
        public static Sprite Lit => _lit != null ? _lit : _lit = Make(Bolt, new Color32(6, 10, 22, 255), new Color32(251, 251, 244, 255),
            new Color32(102, 224, 255, 255), new Color32(47, 116, 216, 255));

        /// <summary>The same bolt in greys (recharging).</summary>
        public static Sprite Dim => _dim != null ? _dim : _dim = Make(Bolt, new Color32(20, 20, 24, 255), new Color32(150, 150, 156, 255),
            new Color32(100, 102, 110, 255), new Color32(66, 68, 76, 255));

        /// <summary>Rows top to bottom; '#' outline, 'W' light, 'C' body, 'B' shade, '.' transparent.</summary>
        static Sprite Make(string[] rows, Color outline, Color light, Color body, Color shade)
        {
            int h = rows.Length, w = rows[0].Length;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    char c = rows[h - 1 - y][x];
                    tex.SetPixel(x, y, c == '#' ? outline : c == 'W' ? light : c == 'C' ? body : c == 'B' ? shade : Color.clear);
                }
            tex.Apply();
            tex.hideFlags = HideFlags.DontUnloadUnusedAsset;
            var sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 8f);
            sprite.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return sprite;
        }
    }

    /// <summary>
    /// Note 35 (the producer: "there should be an icon or such informing the user the dash is available"): the Thunder
    /// Champion's blink as a tile at the right end of the weapon row, under the status plate's right corner, the way the
    /// loaned weapon has its tile. Built in the build strip's style (a dark-bronze chip, a 2-unit line, the dark inside)
    /// with a key chip under it naming what blinks: SLIDE (the slide key starts the blink; Shift, Ctrl, C or the pad's
    /// East / left trigger, so the action, not one key).
    /// - Ready: the bolt lit on a navy inside, the line cyan, a cyan halo, all gently pulsing; the key's word bright.
    /// - Recharging: the bolt grey with its lit twin filling it from the bottom as the dash charges; the chip and line
    ///   greyed, no halo; the key's word dust.
    /// - The moment it recharges: a pop (the tile swells 30 % and settles, the inside flashes, the halo bursts).
    /// Only while a LightningDash exists (hidden for every other gladiator). Colours only change when they change;
    /// no allocation after the build.
    /// </summary>
    public sealed class BlinkReadyIcon
    {
        public const float TileSize = 52f, LineSize = 44f, InnerSize = 40f, BoltSize = 32f, HaloOut = 12f;
        public const float KeyGap = 4f, KeyH = 26f, KeySize = 16f, PopSeconds = 0.3f, PopScale = 0.3f, PulseSpeed = 4.5f;
        public const string KeyWord = "SLIDE";

        static readonly Color Cyan = new Color(0.45f, 0.88f, 1f);
        static readonly Color InnerReady = new Color(0.06f, 0.12f, 0.22f, 0.95f);
        static readonly Color InnerFlash = new Color(0.75f, 0.95f, 1f, 1f);
        static readonly Color InnerIdle = new Color(0.1f, 0.11f, 0.13f, 0.92f);
        static readonly Color LineIdle = new Color(0.3f, 0.34f, 0.4f);
        static readonly Color ChipIdle = new Color(0.7f, 0.7f, 0.74f);
        static readonly Color LitCharging = new Color(0.7f, 0.85f, 1f, 1f);

        readonly RectTransform _root, _tile, _haloRt;
        readonly Image _halo, _chip, _line, _inner, _dim, _lit, _keyChip;
        readonly TextMeshProUGUI _key;
        readonly Color _ink, _dust;
        bool _shown, _wasReady;
        float _popT = PopSeconds;

        /// <summary>Tests: whether it shows, whether it shows the dash ready, how much of the bolt is lit (0-1), how many
        /// recharge pops it has played, its key's word, and its root and tile.</summary>
        public bool Shown => _shown;
        public bool ShowsReady { get; private set; }
        public float LitFill => _lit.fillAmount;
        public int Pops { get; private set; }
        public bool Popping => _popT < PopSeconds;
        public string KeyText => _key.text;
        public RectTransform Root => _root;
        public RectTransform Tile => _tile;

        /// <param name="parent">The HUD's build strip (it fades with it under the level-up, the pause and the book).</param>
        /// <param name="rightEdge">Canvas units from the left where the widget ends (the status plate's right edge).</param>
        /// <param name="top">Canvas units from the top where the tile starts (the weapon row's).</param>
        public BlinkReadyIcon(Transform parent, float rightEdge, float top)
        {
            var th = UiTheme.Current;
            _ink = th.ink;
            _dust = th.dust;
            var tl = new Vector2(0f, 1f);
            var tr = new Vector2(1f, 1f);
            var topMid = new Vector2(0.5f, 1f);
            var mid = new Vector2(0.5f, 0.5f);

            _root = UiFactory.Rect("BlinkIcon", parent, tl, tr, new Vector2(rightEdge, top), new Vector2(TileSize, TileSize + KeyGap + KeyH));
            // the halo first (behind the tile); the tile's pivot at its centre, so the pop swells it in place
            _halo = UiKit.Frame("Halo", _root, FrameKind.Halo, topMid, mid, new Vector2(0f, -TileSize * 0.5f),
                new Vector2(TileSize + 2f * HaloOut, TileSize + 2f * HaloOut), Color.clear);
            _haloRt = _halo.rectTransform;
            _chip = UiKit.Frame("Tile", _root, FrameKind.Chip, topMid, mid, new Vector2(0f, -TileSize * 0.5f), new Vector2(TileSize, TileSize), ChipIdle);
            _tile = _chip.rectTransform;
            _line = UiFactory.Image("Line", _tile, LineIdle, mid, mid, Vector2.zero, new Vector2(LineSize, LineSize), PixelIcons.White);
            _inner = UiFactory.Image("Inner", _tile, InnerIdle, mid, mid, Vector2.zero, new Vector2(InnerSize, InnerSize), PixelIcons.White);
            _dim = UiFactory.Image("BoltDim", _inner.transform, Color.white, mid, mid, Vector2.zero, new Vector2(BoltSize, BoltSize), BlinkIconArt.Dim);
            _lit = UiFactory.Image("Bolt", _inner.transform, LitCharging, mid, mid, Vector2.zero, new Vector2(BoltSize, BoltSize), BlinkIconArt.Lit);
            _lit.type = Image.Type.Filled;
            _lit.fillMethod = Image.FillMethod.Vertical;
            _lit.fillOrigin = (int)Image.OriginVertical.Bottom;
            _lit.fillAmount = 0f;

            // the key chip: as wide as its word needs (never narrower than the tile), centred under the tile
            _keyChip = UiKit.Frame("KeyChip", _root, FrameKind.KeyChip, topMid, topMid, new Vector2(0f, -TileSize - KeyGap), new Vector2(TileSize, KeyH), Color.white);
            _key = UiKit.Text("Key", _keyChip.transform, KeyWord, TextRole.Number, KeySize, _dust, TextAlignmentOptions.Midline, mid, mid,
                Vector2.zero, new Vector2(TileSize, KeyH));
            UiAudit.ExemptPadding(_key); // a keycap: its word fills the chip by design (its contrast is still checked)
            float w = Mathf.Max(TileSize, Mathf.Ceil(_key.GetPreferredValues(KeyWord).x) + 14f);
            _keyChip.rectTransform.sizeDelta = new Vector2(w, KeyH);
            _key.rectTransform.sizeDelta = new Vector2(w, KeyH);
            _root.sizeDelta = new Vector2(w, TileSize + KeyGap + KeyH);
            _root.gameObject.SetActive(false);
        }

        /// <summary>Called every frame by the HUD with the player's dash (null for every other gladiator).</summary>
        public void Draw(LightningDash dash, float unscaledDt)
        {
            bool show = dash != null && dash.trait != null;
            if (show != _shown)
            {
                _shown = show;
                _root.gameObject.SetActive(show);
                if (show) { _wasReady = dash.Ready; _popT = PopSeconds; } // no pop for the dash he spawns with
            }
            if (!show) return;

            bool ready = dash.Ready;
            if (ready && !_wasReady) { _popT = 0f; Pops++; }
            _wasReady = ready;
            if (_popT < PopSeconds) _popT += unscaledDt;
            float pop = _popT < PopSeconds ? 1f - _popT / PopSeconds : 0f; // 1 at the recharge, 0 when settled
            pop *= pop;

            if (ready)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * PulseSpeed);
                _lit.fillAmount = 1f;
                _lit.color = Color.white;
                _chip.color = Color.white;
                _inner.color = Color.Lerp(InnerReady, InnerFlash, pop);
                _line.color = Color.Lerp(Cyan, Color.white, Mathf.Max(pop, 0.45f * pulse));
                _halo.color = new Color(Cyan.r, Cyan.g, Cyan.b, Mathf.Min(1f, 0.22f + 0.28f * pulse + 0.8f * pop));
                float s = 1f + PopScale * pop + 0.03f * pulse;
                _tile.localScale = new Vector3(s, s, 1f);
                float hs = 1f + 0.5f * pop;
                _haloRt.localScale = new Vector3(hs, hs, 1f);
                _key.color = _ink;
            }
            else
            {
                _lit.fillAmount = dash.Charge01;
                _lit.color = LitCharging;
                _chip.color = ChipIdle;
                _inner.color = InnerIdle;
                _line.color = LineIdle;
                _halo.color = Color.clear;
                _tile.localScale = Vector3.one;
                _haloRt.localScale = Vector3.one;
                _key.color = _dust;
            }
            ShowsReady = ready;
        }
    }
}
