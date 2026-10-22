using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Gladiator.UI
{
    /// <summary>
    /// U1 dev scene (Assets/Scenes/Dev/UiGallery.unity, built by Gladiator/Setup/Dev/Build UI Gallery): the theme on one
    /// screen per page, for tuning the frames, fonts and material looks and for the producer's screenshots.
    /// Pages 1-2 FRAMES: every FrameKind at two sizes with its name (buttons to cards; shields to laurels). Page 3 TYPE:
    /// each role in each look, plates in the four states and with key chips, a trough at 70 %, laurels beside VICTORY!,
    /// meander, rule, chips, scuta, wax and mosaic. Page 4 CARDS: the four card frames at 300×520 with a live icon, the
    /// selected one ringed. A banner drops at the top every few seconds, crimson then purple. Tab / PageDown / 1-4 switch
    /// pages (or ShowPage from a script). Allocates freely: a dev scene.
    /// </summary>
    public sealed class UiGallery : MonoBehaviour
    {
        public const int PageCount = 4;
        static readonly Vector2 Ref = new Vector2(1920f, 1080f);
        static readonly Vector2 Mid = new Vector2(0.5f, 0.5f), TopLeft = new Vector2(0f, 1f), Top = new Vector2(0.5f, 1f);

        [Tooltip("The upgrade icon atlas (PixelSrc/upgrade_icons.py) for the cards' sample icons.")]
        public Texture2D iconAtlas;
        [Tooltip("Atlas rows the four sample cards show.")]
        public int[] iconRows = { 0, 4, 9, 14 };
        [Tooltip("Common, Uncommon, Rare, Legendary (UpgradeConfig's colours; the setup copies them).")]
        public Color[] rarityColors =
        {
            new Color(0.82f, 0.85f, 0.9f), new Color(0.38f, 0.9f, 0.45f), new Color(0.32f, 0.62f, 1f), new Color(1f, 0.74f, 0.2f),
        };
        [Tooltip("Seconds between banners (they alternate crimson and purple).")]
        public float bannerEvery = 4f;

        static readonly string[] PageNames = { "FRAMES: PLATES, PANELS, CARDS", "FRAMES: SHIELDS, BANNER, BARS, LAUREL", "TYPE AND PLATES", "CARDS" };

        public int Page { get; private set; }
        public VexillumBanner Banner { get; private set; }
        /// <summary>Plates on the TYPE page: Primary Normal/Focused/Pressed/Disabled, then Secondary, then the keyed three.</summary>
        public UiPlate[] Plates { get; private set; }

        Canvas _canvas;
        RectTransform _canvasRt;
        RectTransform[] _pages;
        CanvasGroup[] _groups;
        UpgradeIconView[] _icons;
        TextMeshProUGUI _pageLabel;
        Vector2 _fittedTo;
        float _nextBanner;
        int _bannerCount;

        void Start()
        {
            Build();
            ShowPage(0);
            _nextBanner = Time.time + 0.5f;
        }

        public void ShowPage(int i)
        {
            Page = (i % PageCount + PageCount) % PageCount;
            for (int p = 0; p < _pages.Length; p++)
            {
                _groups[p].alpha = p == Page ? 1f : 0f;
                _groups[p].blocksRaycasts = p == Page;
            }
            _pageLabel.text = $"UI GALLERY  {Page + 1} / {PageCount}   {PageNames[Page]}   [Tab] next";
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.tabKey.wasPressedThisFrame || kb.pageDownKey.wasPressedThisFrame) ShowPage(Page + 1);
                if (kb.pageUpKey.wasPressedThisFrame) ShowPage(Page - 1);
                if (kb.digit1Key.wasPressedThisFrame) ShowPage(0);
                if (kb.digit2Key.wasPressedThisFrame) ShowPage(1);
                if (kb.digit3Key.wasPressedThisFrame) ShowPage(2);
                if (kb.digit4Key.wasPressedThisFrame) ShowPage(3);
            }
            if (Time.time >= _nextBanner)
            {
                _nextBanner = Time.time + bannerEvery;
                if (_bannerCount++ % 2 == 0) Banner.Show("SWARM INCOMING!", BannerCloth.Crimson);
                else Banner.Show("GORM, THE PIT MASTER", BannerCloth.Purple, 3f);
            }
            Banner.Tick(Time.deltaTime);
            if (_icons != null)
                foreach (var ic in _icons) ic?.Tick(Time.unscaledTime);
            Fit();
        }

        // every page is laid out at 1920×1080 and scaled down to fit other aspect ratios (4:3, 16:10)
        void Fit()
        {
            var size = _canvasRt.rect.size;
            if (size == _fittedTo || size.x <= 0f) return;
            _fittedTo = size;
            float k = UiKit.FitScale(Ref, size, 0f);
            foreach (var p in _pages) p.localScale = new Vector3(k, k, 1f);
        }

        void Build()
        {
            var theme = UiTheme.Current;
            _canvas = UiFactory.Canvas("GalleryCanvas", transform, UiSortOrder.Menus);
            _canvasRt = (RectTransform)_canvas.transform;
            _pages = new RectTransform[PageCount];
            _groups = new CanvasGroup[PageCount];
            for (int i = 0; i < PageCount; i++)
            {
                _pages[i] = UiFactory.Rect("Page" + (i + 1), _canvas.transform, Mid, Mid, Vector2.zero, Ref);
                _groups[i] = _pages[i].gameObject.AddComponent<CanvasGroup>();
            }
            BuildFrames(_pages[0], theme, 0, (int)FrameKind.Scutum);
            BuildFrames(_pages[1], theme, (int)FrameKind.Scutum, UiFrames.KindCount);
            BuildType(_pages[2], theme);
            BuildCards(_pages[3], theme);
            Banner = new VexillumBanner(_canvas.transform, Top, new Vector2(0f, -16f));
            _pageLabel = UiKit.Text("PageLabel", _canvas.transform, "", TextRole.Title, 20f, theme.dust, TextAlignmentOptions.BottomLeft,
                Vector2.zero, Vector2.zero, new Vector2(16f, 10f), new Vector2(900f, 30f), TextLook.Outline);
        }

        // ---- pages 1-2: every frame at two sizes ---------------------------------------------------------------------------

        static Vector2[] SizesFor(FrameKind k)
        {
            var n = UiFrames.NativeSize(k);
            switch (k)
            {
                case FrameKind.AnsataBronze: return new[] { new Vector2(240, 66), new Vector2(300, 90) };
                case FrameKind.AnsataMarble: return new[] { new Vector2(240, 66), new Vector2(200, 40) };
                case FrameKind.Marble: case FrameKind.Wax: return new[] { new Vector2(150, 120), new Vector2(240, 150) };
                case FrameKind.Bronze: return new[] { new Vector2(120, 90), new Vector2(240, 120) };
                case FrameKind.Mosaic: return new[] { new Vector2(147, 147), new Vector2(243, 171) };
                case FrameKind.Stone: return new[] { new Vector2(90, 60), new Vector2(180, 90) };
                case FrameKind.MeanderGold: case FrameKind.MeanderDark: return new[] { new Vector2(180, 15), new Vector2(288, 15) };
                case FrameKind.CardStone: case FrameKind.CardBronze: case FrameKind.CardSilver: case FrameKind.CardGold:
                    return new[] { new Vector2(90, 132), new Vector2(120, 174) };
                case FrameKind.Halo: return new[] { new Vector2(96, 96), new Vector2(180, 120) };
                case FrameKind.Focus: return new[] { new Vector2(60, 60), new Vector2(150, 90) };
                case FrameKind.Scutum: case FrameKind.ScutumLocked: return new[] { new Vector2(90, 120), new Vector2(144, 186) };
                case FrameKind.ScutumSpine: return new[] { new Vector2(12, 60), new Vector2(18, 36) };
                case FrameKind.VexillumBar: return new[] { new Vector2(300, 24), new Vector2(600, 24) };
                case FrameKind.VexillumCrimson: case FrameKind.VexillumPurple: return new[] { new Vector2(420, 120), new Vector2(760, 120) };
                case FrameKind.Trough: return new[] { new Vector2(200, 24), new Vector2(300, 36) };
                case FrameKind.Chip: return new[] { new Vector2(60, 30), new Vector2(120, 36) };
                case FrameKind.KeyChip: return new[] { new Vector2(36, 36), new Vector2(48, 48) };
                default: return new[] { n, n * (2f / 3f) }; // simple kinds: native and two thirds
            }
        }

        void BuildFrames(RectTransform page, UiTheme theme, int from, int to)
        {
            float x = 24f, y = -170f, rowH = 0f; // below the banner
            for (int i = from; i < to; i++)
            {
                var k = (FrameKind)i;
                var sizes = SizesFor(k);
                for (int s = 0; s < sizes.Length; s++)
                {
                    var size = sizes[s];
                    if (x + size.x > Ref.x - 24f)
                    {
                        x = 24f;
                        y -= rowH + 30f;
                        rowH = 0f;
                    }
                    var tint = Color.white;
                    if (k == FrameKind.Halo) tint = rarityColors[s == 0 ? 2 : 3];
                    if (k == FrameKind.Focus) tint = s == 0 ? Color.white : theme.gold;
                    if (k == FrameKind.TroughFill) tint = theme.hp;
                    var img = UiKit.Frame(k + "_" + s, page, k, TopLeft, TopLeft, new Vector2(x, y), size, tint);
                    if (k == FrameKind.LaurelBranch && s == 1) img.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
                    if (k == FrameKind.Trough) UiKit.Bar(k + "_bar" + s, page, theme.hp, TopLeft, TopLeft, new Vector2(x, y - size.y - 4f), size).fillAmount = 0.7f;
                    if (s == 0)
                        UiKit.Text("Name_" + k, page, k.ToString(), TextRole.Title, 18f, theme.ink, TextAlignmentOptions.TopLeft, TopLeft,
                            TopLeft, new Vector2(x, y - size.y - (k == FrameKind.Trough ? 32f : 4f)), new Vector2(Mathf.Max(size.x, 150f), 24f), TextLook.Outline);
                    x += size.x + 22f;
                    rowH = Mathf.Max(rowH, size.y + (k == FrameKind.Trough ? 30f : 0f));
                }
            }
        }

        // ---- page 3: type in each role and look, plates, bars, laurels ------------------------------------------------

        static readonly string[] Samples = { "125 480! 07:59", "SWARM INCOMING!", "The Grand Arena", "A slow, enormous cleave around you.", "debug 60 fps 300 enemies" };
        static readonly float[] SampleSizes = { 44f, 44f, 36f, 24f, 22f };

        void BuildType(RectTransform page, UiTheme theme)
        {
            // three columns: Flat on dark stone, Outline straight over the scene colour, Engraved on marble
            const float colW = 400f, rowH = 66f, top = -200f;
            UiKit.Frame("FlatPanel", page, FrameKind.Stone, TopLeft, TopLeft, new Vector2(24f, top + 24f), new Vector2(colW + 24f, rowH * 5 + 60f), Color.white);
            UiKit.Frame("EngravedPanel", page, FrameKind.Marble, TopLeft, TopLeft, new Vector2(24f + 2 * (colW + 30f), top + 24f), new Vector2(colW + 24f, rowH * 5 + 60f), Color.white);
            for (int look = 0; look < 3; look++)
            {
                float cx = 36f + look * (colW + 30f);
                UiKit.Text("Look" + look, page, ((TextLook)look).ToString().ToUpperInvariant(), TextRole.Title, 18f, look == 2 ? theme.marbleInk : theme.dust,
                    TextAlignmentOptions.TopLeft, TopLeft, TopLeft, new Vector2(cx, top + 10f), new Vector2(colW, 24f), (TextLook)look);
                for (int r = 0; r < Samples.Length; r++)
                {
                    var role = (TextRole)r;
                    var col = look == 2 ? theme.marbleInk : role == TextRole.Shout ? theme.goldBright : theme.ink;
                    UiKit.Text(role + "_" + (TextLook)look, page, Samples[r], role, SampleSizes[r], col, TextAlignmentOptions.MidlineLeft, TopLeft, TopLeft,
                        new Vector2(cx, top - 20f - r * rowH), new Vector2(colW, rowH), (TextLook)look);
                }
            }

            // plates: Primary and Secondary in the four states, then the level-up's keyed three and a small BUY
            Plates = new UiPlate[11];
            float py = -620f;
            for (int st = 0; st < 2; st++)
                for (int s = 0; s < 4; s++)
                {
                    var p = UiKit.Plate("Plate" + st + s, page, ((UiState)s).ToString().ToUpperInvariant(), (ButtonStyle)st, TopLeft, TopLeft,
                        new Vector2(36f + s * 300f, py - st * 96f), new Vector2(270f, 66f));
                    p.SetState((UiState)s);
                    Plates[st * 4 + s] = p;
                }
            string[] words = { "Reroll (2)", "Banish (1)", "Skip" }, keys = { "R", "B", "X" };
            for (int i = 0; i < 3; i++)
                Plates[8 + i] = UiKit.Plate("Keyed" + i, page, words[i], ButtonStyle.Primary, TopLeft, TopLeft,
                    new Vector2(36f + i * 306f, py - 192f), new Vector2(280f, 66f), 30f, keys[i]);
            UiKit.Plate("Buy", page, "BUY 120", ButtonStyle.Primary, TopLeft, TopLeft, new Vector2(960f, py - 205f), new Vector2(200f, 40f), 24f);

            // the right-hand column (x 1340-1896): header + meander, troughs, chips, laurels, marble, mosaic, wax
            const float rx = 1340f, rw = 556f;
            UiKit.Text("Header", page, "THE EDITOR'S EDICTS", TextRole.Title, 40f, theme.gold, TextAlignmentOptions.TopLeft, TopLeft, TopLeft,
                new Vector2(rx, -186f), new Vector2(rw, 52f), TextLook.Outline);
            UiKit.Meander("HeaderMeander", page, true, TopLeft, TopLeft, new Vector2(rx, -240f), 540f);
            UiKit.Text("HpLabel", page, "HP", TextRole.Title, 22f, theme.bronzeLight, TextAlignmentOptions.MidlineLeft, TopLeft, TopLeft,
                new Vector2(rx, -272f), new Vector2(60f, 36f), TextLook.Outline);
            UiKit.Bar("Hp", page, theme.hp, TopLeft, TopLeft, new Vector2(rx + 60f, -272f), new Vector2(420f, 36f)).fillAmount = 0.7f;
            UiKit.Text("HpText", page, "86 / 120", TextRole.Number, 26f, theme.ink, TextAlignmentOptions.Center, TopLeft, TopLeft,
                new Vector2(rx + 60f, -272f), new Vector2(420f, 36f), TextLook.Outline);
            UiKit.Bar("Xp", page, theme.xp, TopLeft, TopLeft, new Vector2(rx, -322f), new Vector2(480f, 18f)).fillAmount = 0.7f;
            var chip = UiKit.Frame("Chip", page, FrameKind.Chip, TopLeft, TopLeft, new Vector2(rx, -354f), new Vector2(120f, 36f), Color.white);
            UiKit.Text("ChipText", chip.transform, "BLOOD 2/3", TextRole.Title, 18f, theme.ink, TextAlignmentOptions.Center, Mid, Mid, Vector2.zero, new Vector2(120f, 36f));
            var key = UiKit.Frame("KeyChip", page, FrameKind.KeyChip, TopLeft, TopLeft, new Vector2(rx + 134f, -354f), new Vector2(54f, 36f), Color.white);
            UiKit.Text("KeyText", key.transform, "ESC", TextRole.Number, 20f, theme.ink, TextAlignmentOptions.Center, Mid, Mid, new Vector2(0f, 1f), new Vector2(54f, 36f));

            var victory = UiKit.Text("Victory", page, "VICTORY!", TextRole.Shout, 88f, theme.goldBright, TextAlignmentOptions.Center, TopLeft, Mid,
                new Vector2(rx + rw * 0.5f, -470f), new Vector2(380f, 110f), TextLook.Outline);
            UiKit.Laurels("Laurels", victory);
            var unlock = UiKit.Text("Unlock", page, "THE MURDER MACHINE", TextRole.Title, 28f, theme.gold, TextAlignmentOptions.Center, TopLeft, Mid,
                new Vector2(rx + rw * 0.5f, -570f), new Vector2(400f, 40f), TextLook.Outline);
            UiKit.Laurels("Laurels", unlock);

            var marble = UiKit.Frame("Page", page, FrameKind.Marble, TopLeft, TopLeft, new Vector2(rx, -620f), new Vector2(330f, 160f), Color.white);
            UiKit.Text("PageTitle", marble.transform, "THE PIT CUR", TextRole.Title, 30f, theme.marbleInk, TextAlignmentOptions.TopLeft, TopLeft, TopLeft,
                new Vector2(24f, -20f), new Vector2(280f, 40f), TextLook.Engraved);
            UiKit.Rule("Rule", marble.transform, TopLeft, TopLeft, new Vector2(24f, -62f), 280f);
            var bodyText = UiKit.Text("PageBody", marble.transform, "Crouches, then lunges at 13 m/s. Jump or slide aside.", TextRole.Body, 22f,
                theme.marbleInk, TextAlignmentOptions.TopLeft, TopLeft, TopLeft, new Vector2(24f, -72f), new Vector2(280f, 70f));
            bodyText.textWrappingMode = TextWrappingModes.Normal;

            var mosaic = UiKit.Frame("Mosaic", page, FrameKind.Mosaic, TopLeft, TopLeft, new Vector2(rx + 340f, -620f), new Vector2(219f, 171f), Color.white);
            UiKit.Text("MosaicTitle", mosaic.transform, "DAILY", TextRole.Title, 40f, theme.gold, TextAlignmentOptions.Center, Mid, Mid,
                new Vector2(0f, 10f), new Vector2(170f, 60f));
            UiKit.Meander("MosaicMeander", mosaic.transform, true, Mid, Mid, new Vector2(0f, -32f), 132f);

            var wax = UiKit.Frame("Wax", page, FrameKind.Wax, TopLeft, TopLeft, new Vector2(rx, -800f), new Vector2(390f, 150f), Color.white);
            string[] labels = { "TIME", "KILLS", "RENOWN" }, values = { "8:00", "2,417", "+6" };
            for (int i = 0; i < 3; i++)
            {
                UiKit.Text("WaxLabel" + i, wax.transform, labels[i], TextRole.Title, 22f, theme.waxInk, TextAlignmentOptions.MidlineLeft, TopLeft, TopLeft,
                    new Vector2(30f, -22f - i * 38f), new Vector2(200f, 36f));
                UiKit.Text("WaxValue" + i, wax.transform, values[i], TextRole.Number, 30f, theme.ink, TextAlignmentOptions.MidlineRight, TopLeft, TopLeft,
                    new Vector2(170f, -22f - i * 38f), new Vector2(190f, 36f));
            }
        }

        // ---- page 4: the four card frames with live icons ---------------------------------------------------------------

        static readonly string[] Bands = { "COMMON", "UNCOMMON", "RARE", "LEGENDARY" };
        static readonly FrameKind[] CardFrames = { FrameKind.CardStone, FrameKind.CardBronze, FrameKind.CardSilver, FrameKind.CardGold };
        static readonly string[] Names = { "Quick Hands", "Javelins", "Static Charge", "Crimson Greatsword" };
        static readonly string[] Tags = { "NEW!", "NEW WEAPON!", "LV 2", "NEW WEAPON!" };
        static readonly string[] Descriptions =
        {
            "+10% Attack Speed",
            "Throws javelins at the nearest enemies. Pierces one.",
            "Hits have a 15% chance to chain lightning to 2 enemies.",
            "A slow, enormous cleave around you. Every hit makes them bleed.",
        };

        void BuildCards(RectTransform page, UiTheme theme)
        {
            const float w = 300f, h = 520f, gap = 28f;
            float rowW = 4 * w + 3 * gap;
            var titleText = UiKit.Text("Title", page, "LEVEL UP!", TextRole.Shout, 132f, theme.goldBright, TextAlignmentOptions.Center, Top, Top,
                new Vector2(0f, -170f), new Vector2(1200f, 150f), TextLook.Outline);
            titleText.enableAutoSizing = true;
            titleText.fontSizeMin = 72f;
            titleText.fontSizeMax = 132f;
            _icons = new UpgradeIconView[4];
            for (int i = 0; i < 4; i++)
            {
                var rc = rarityColors[i];
                float x = -rowW * 0.5f + w * 0.5f + i * (w + gap);
                bool selected = i == 2;
                var card = UiFactory.Rect("Card" + i, page, Top, Top, new Vector2(x, -330f), new Vector2(w, h));
                if (selected)
                {
                    card.localScale = new Vector3(1.04f, 1.04f, 1f);
                    UiKit.Frame("SelectedHalo", card, FrameKind.Halo, Mid, Mid, Vector2.zero, new Vector2(w + 90f, h + 90f), new Color(rc.r, rc.g, rc.b, 0.55f));
                }
                UiFactory.Image("Inside", card, theme.cardInside, Mid, Mid, Vector2.zero, new Vector2(w - 12f, h - 12f), PixelIcons.White);
                UiFactory.Image("Band", card, rc, Top, Top, new Vector2(0f, -9f), new Vector2(w - 18f, 40f), PixelIcons.White);
                var band = UiKit.Text("BandText", card, Bands[i], TextRole.Title, 24f, new Color32(0x17, 0x12, 0x0F, 0xFF), TextAlignmentOptions.Center,
                    Top, Top, new Vector2(0f, -9f), new Vector2(w - 18f, 40f));
                band.characterSpacing = 12f;
                UiKit.Frame("Halo", card, FrameKind.Halo, Top, Mid, new Vector2(0f, -105f), new Vector2(150f, 150f), new Color(rc.r, rc.g, rc.b, 0.8f));
                if (i == 3) UiKit.Frame("Wreath", card, FrameKind.LaurelWreath, Top, Mid, new Vector2(0f, -122f), UiFrames.NativeSize(FrameKind.LaurelWreath), Color.white);
                if (iconAtlas != null)
                {
                    _icons[i] = new UpgradeIconView(card, "Icon", Top, new Vector2(0f, -105f), 100f, true);
                    _icons[i].Root.pivot = Mid;
                    _icons[i].Set(iconAtlas, iconRows[i % iconRows.Length], rc, i * 0.27f);
                }
                UiKit.Text("Tag", card, Tags[i], TextRole.Title, 21f, theme.gold, TextAlignmentOptions.Center, Top, Top,
                    new Vector2(0f, -161f), new Vector2(w - 24f, 26f));
                var name = UiKit.Text("Name", card, Names[i], TextRole.Title, 32f, theme.ink, TextAlignmentOptions.Center, Top, Top,
                    new Vector2(0f, -187f), new Vector2(w - 24f, 68f));
                name.textWrappingMode = TextWrappingModes.Normal;
                name.enableAutoSizing = true;
                name.fontSizeMin = 26f;
                name.fontSizeMax = 32f;
                for (int p = 0; p < 8; p++)
                    UiFactory.Image("Pip" + p, card, p == 0 ? rc : new Color(0.23f, 0.25f, 0.29f), Top, Top,
                        new Vector2(-70f + p * 20f, -262f), new Vector2(14f, 14f), PixelIcons.White);
                var desc = UiKit.Text("Desc", card, Descriptions[i], TextRole.Body, 24f, theme.ink, TextAlignmentOptions.Top, Top, Top,
                    new Vector2(0f, -285f), new Vector2(w - 30f, 166f));
                desc.textWrappingMode = TextWrappingModes.Normal;
                desc.enableAutoSizing = true;
                desc.fontSizeMin = 20f;
                desc.fontSizeMax = 24f;
                UiKit.Text("Key", card, "[" + (i + 1) + "]", TextRole.Number, 26f, new Color(theme.ink.r, theme.ink.g, theme.ink.b, 0.5f),
                    TextAlignmentOptions.Center, Top, Top, new Vector2(0f, -457f), new Vector2(w - 24f, 30f), TextLook.Outline);
                UiKit.Frame("Frame", card, CardFrames[i], Mid, Mid, Vector2.zero, new Vector2(w, h), Color.white);
                if (selected) UiKit.Frame("Focus", card, FrameKind.Focus, Mid, Mid, Vector2.zero, new Vector2(w + 12f, h + 12f), Color.white);
            }
            string[] words = { "Reroll (2)", "Banish (1)", "Skip" }, keys = { "R", "B", "X" };
            for (int i = 0; i < 3; i++)
                UiKit.Plate("Button" + i, page, words[i], ButtonStyle.Primary, Top, Top, new Vector2(-306f + i * 306f, -880f), new Vector2(280f, 66f), 30f, keys[i]);
        }
    }
}
