using Gladiator.Core;
using Gladiator.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Gladiator.UI
{
    /// <summary>
    /// Level-up cards (M2.4): rarity-framed cards with the real numbers, level pips, NEW/LV tag; Reroll, Banish and
    /// Skip. Mouse (hover + click), keyboard (1-4, arrows/A-D + Enter, R/B/X, Esc cancels banish) or gamepad (d-pad /
    /// stick, South pick, West reroll, North banish, East cancel, Select skip). Input is ignored for a short grace
    /// after opening so a held jump/click can't pick blind.
    /// GC: built once; shown/hidden with a CanvasGroup (alpha + raycasts). Not SetActive, and not Canvas.enabled: a
    /// disabled canvas drops its GraphicRegistry set and re-creates it on enable (~8 KB per open, measured). No
    /// Selectable colour tweens; texts are pre-sized and written from char buffers or non-allocating SetText.
    /// U1 layout A (docs/tracks/U1-design.md §6, §8.3): 300×520 cards centred with the title and the buttons. Each card
    /// wears its rarity's frame (stone, bronze, silver + meander, gold + a laurel wreath behind the icon) round a dark
    /// inside: the rarity band, the icon on a halo, tag, name, pips, description, key. A Vow's band is split crimson |
    /// gold. Buttons are bronze tabulae ansatae with key chips (UiKit.Plate + UiHit: no Button in a run).
    /// P3 (docs/tracks/P3-contract.md, the producer's notes 15, 17, 19): layout A is symmetric: "your items"
    /// (InventoryPanel, built here) on the left mirrors "your stats" (StatsPanel) on the right; the selected card lights
    /// its upgrade's slot there, or shows a new one as a ghost in the next free slot. Every card label sits inside the
    /// card frame's ContentRect. The pause is this screen too: PAUSED in the title's place, three ansata plates where
    /// the cards stand (RESUME · ESC, OPTIONS · O, MAIN MENU · Q; clickable, or up/down + Enter / gamepad South), the
    /// inventory left and the stats column right, at a three-card level-up's fit so the two screens line up. Clicking
    /// empty space no longer resumes: the plates are the commands.
    /// </summary>
    public sealed class LevelUpPanel : MonoBehaviour, ITickable, IUiHitTarget
    {
        [Tooltip("Unused since U1 (UiTheme decides the fonts); kept so the scene builders' links stay valid.")]
        public TMP_FontAsset font;
        public Material fontMaterial;

        const int MaxCards = 4, MaxPips = 8;
        const int IdReroll = 10, IdBanish = 11, IdSkip = 12;
        /// <summary>The buttons' ids for Hover / Click (UiHit; tests click them the way the mouse does).</summary>
        public const int RerollId = IdReroll, BanishId = IdBanish, SkipId = IdSkip;
        /// <summary>P3: the pause's plates (Hover / Click ids).</summary>
        public const int ResumeId = 20, OptionsId = 21, MenuId = 22;
        public const int PausePlates = 3;
        public const string PausedWord = "PAUSED";
        static readonly string[] PauseWords = { "Resume", "Options", "Main Menu" };
        static readonly string[] PauseKeys = { "ESC", "O", "Q" };
        const float CardW = LevelUpLayout.CardW, CardH = LevelUpLayout.CardH;
        // the card (spec §6.2), y from its centre: 9 frame · 40 band · 112 icon · 26 tag · 68 name · 30 pips ·
        // the description · the key · 24 foot (the frame's); P3: the key's row sits on the frame's ContentRect's bottom,
        // as tall as its textBounds (TextBox), and the description ends 2 above it (the padding rule)
        const float Inset = 9f, BandY = 231f, BandH = 40f, IconY = 151f, IconSize = 100f,
            WreathY = 143f, TagY = 82f, TagH = 28f, NameY = 35f, NameH = 68f, PipY = -14f, PipSize = 14f, PipPitch = 20f,
            DescTop = -24f, KeySize = 24f, KeyMinH = 30f;
        const float SelectedScale = 1.04f, RingOut = 6f, HaloOut = 8f; // producer (2026-10-22): the glow stays clear of the subtitle and the buttons
        // layout A at 1920×1080, in the content's units from the canvas centre: the title's glyphs from y 46, the
        // subtitle at 188, the cards' tops at 262, the buttons at 812 (from the top)
        const float TitleY = 425f, TitleH = 150f, TitleMaxW = 1400f, SubY = 326f, SubH = 50f, CardsY = 18f,
            ButtonsY = -305f, ButtonPitch = 306f, Parked = -5000f;
        static readonly Vector2 ButtonSize = new Vector2(280f, 66f);
        // P3 the pause's plates: stacked where the cards stand, a key chip wide enough for ESC
        static readonly Vector2 PausePlateSize = new Vector2(440f, 80f);
        const float PausePitch = 108f, PauseFont = 32f, PauseKeyWidth = 66f, PauseGrace = 0.15f;

        sealed class CardView
        {
            public RectTransform Root;
            public Image Halo, Inside, Frame, Focus, Band, VowHalf, Wreath;
            public TextMeshProUGUI Rarity, Kind, Title, Desc, Key;
            public Image[] Pips;
            public UpgradeIconView Icon;
            public FrameKind FrameKind = FrameKind.CardStone;
            public Color RarityColor = Color.white;
            public bool VowBand;
            public readonly CharBuffer Text = new CharBuffer(384); // description + Phase 5.5 hints with colour tags
        }

        enum Face : byte { None, Pick, Pause }

        static readonly Color BandInk = new Color32(0x17, 0x12, 0x0F, 0xFF);
        static readonly Color AltarRed = new Color(0.9f, 0.16f, 0.36f); // the altar's blood (today's)
        static readonly Color PipEmpty = new Color(0.26f, 0.29f, 0.34f);
        static readonly Color PipOwned = new Color(0.9f, 0.92f, 0.95f);
        static readonly Color NewGold = new Color(1f, 0.86f, 0.3f);
        static readonly Color VowGold = new Color(1f, 0.76f, 0.22f);
        static readonly Color BanishTint = new Color(0.6f, 0.6f, 0.6f, 1f); // banish mode: every frame at 60 %
        const float SelectedGlow = 0.45f;

        Canvas _canvas;
        CanvasGroup _group;
        RectTransform _content;
        StatsPanel _statsPanel;
        InventoryPanel _inventory;
        TextMeshProUGUI _title, _sub;
        readonly CardView[] _cards = new CardView[MaxCards];
        UiPlate _reroll, _banish, _skip;
        readonly UiPlate[] _pause = new UiPlate[PausePlates];
        Color _goldBright, _gold, _crimson, _crimsonLight, _ink;
        LevelUpLayout.Fit _fit = new LevelUpLayout.Fit { ContentScale = 1f, StatsScale = 1f, Centred = true, Cards = LevelUpLayout.PauseCards };
        Face _face;
        bool _banishMode;
        int _selected, _hoverAction = -1, _drawnVersion = -1, _shownCount;
        int _drawnRerolls = -1, _drawnBanishes = -1;
        int _pauseFocus;
        float _openedAt, _prevStickX, _prevStickY;
        bool _drawnBanishMode;
        PickSource _drawnSource = (PickSource)255;

        public bool BanishMode => _banishMode;
        public int Selected => _selected;
        /// <summary>Tests: what card `i` shows.</summary>
        public string CardTitle(int i) => _cards[i].Title.text;
        public string CardRarity(int i) => _cards[i].Rarity.text;
        public string CardDescription(int i) => _cards[i].Text.ToString();
        public UpgradeIconView CardIcon(int i) => _cards[i].Icon;
        public RectTransform CardRect(int i) => _cards[i].Root;
        /// <summary>Tests: card `i` wears the Vow's split band (crimson | gold).</summary>
        public bool CardIsVowFramed(int i) => _cards[i].VowHalf.color.a > 0f;
        /// <summary>Tests: the FrameKind card `i`'s frame shows (UiFrames.KindOf), and whether the laurel wreath is behind its icon.</summary>
        public int CardFrame(int i) => UiFrames.KindOf(_cards[i].Frame);
        public bool CardHasWreath(int i) => i < _shownCount && _cards[i].Wreath.color.a > 0f;
        /// <summary>Tests: card `i`'s name, description and key labels (role checks).</summary>
        public Component CardNameLabel(int i) => _cards[i].Title;
        public Component CardDescriptionLabel(int i) => _cards[i].Desc;
        public Component CardKeyLabel(int i) => _cards[i].Key;
        /// <summary>The cards, title and buttons (always centred since P3: LevelUpLayout).</summary>
        public RectTransform Content => _content;
        public StatsPanel Stats => _statsPanel;
        /// <summary>P3: "your items", left of the cards (and of the pause's plates).</summary>
        public InventoryPanel Inventory => _inventory;
        /// <summary>The layout of the last redraw, and the columns' scale in it.</summary>
        public LevelUpLayout.Fit Fit => _fit;
        public float StatsScale => _fit.StatsScale;
        /// <summary>Tests: the title's and subtitle's words, the title label, the panel's canvas.</summary>
        public string TitleText => _title.text;
        public string SubtitleText => _sub.text;
        public Component TitleLabel => _title;
        public RectTransform CanvasRect => (RectTransform)_canvas.transform;
        /// <summary>Tests: button `i` (0 Reroll, 1 Banish, 2 Skip).</summary>
        public UiPlate ActionPlate(int i) => i == 0 ? _reroll : i == 1 ? _banish : _skip;
        public RectTransform ActionRect(int i) => ActionPlate(i).Root;
        /// <summary>Tests: redraw the offers next tick (after a test edits LevelUpController.Offers, e.g. their rarity).</summary>
        public void ForceRedraw() => _drawnVersion = -1;
        /// <summary>P3 tests: the pause shows (the plates, the inventory, the stats column), its plates (0 Resume,
        /// 1 Options, 2 Main Menu), their words and keys, the focused one, and the PAUSED title.</summary>
        public bool PauseShown => _face == Face.Pause;
        public UiPlate PausePlate(int i) => _pause[i];
        public RectTransform PausePlateRect(int i) => _pause[i].Root;
        public string PauseLabel(int i) => _pause[i].Label.text;
        public string PauseKey(int i) => _pause[i].Key != null ? _pause[i].Key.text : "";
        public int PauseFocus => _pauseFocus;
        public RectTransform PauseTitleRect => _title.rectTransform;
        public float PauseTitleWidth => _title.GetPreferredValues(PausedWord).x;
        public int PauseTitleLines
        {
            get
            {
                _title.ForceMeshUpdate();
                return _title.textInfo.lineCount;
            }
        }

        void Awake()
        {
            var th = UiTheme.Current;
            _goldBright = th.goldBright;
            _gold = th.gold;
            _crimson = th.crimson;
            _crimsonLight = th.crimsonLight;
            _ink = th.ink;
            _canvas = UiFactory.Canvas("LevelUpCanvas", transform, UiSortOrder.LevelUp);
            var root = _canvas.transform;
            var mid = new Vector2(0.5f, 0.5f);
            var dimColor = th.night;
            dimColor.a = 0.64f;
            var dim = UiFactory.Image("Dim", root, dimColor, mid, mid, Vector2.zero, Vector2.zero, PixelIcons.White);
            UiFactory.Stretch(dim.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            dim.raycastTarget = true; // eat clicks meant for the world
            _content = UiFactory.Rect("Content", root, mid, mid, Vector2.zero, Vector2.zero);
            var content = _content;
            _title = UiKit.Text("Title", content, "LEVEL UP!", TextRole.Shout, 132f, _goldBright, TextAlignmentOptions.Center, mid, mid,
                new Vector2(0f, TitleY), new Vector2(TitleMaxW, TitleH), TextLook.Outline);
            AutoSize(_title, 72f, 132f); // SPOILS OF VICTORY at 4:3
            _sub = UiKit.Text("Sub", content, "choose one", TextRole.Body, 34f, _ink, TextAlignmentOptions.Center, mid, mid,
                new Vector2(0f, SubY), new Vector2(TitleMaxW, SubH), TextLook.Outline);
            AutoSize(_sub, 22f, 34f);

            for (int i = 0; i < MaxCards; i++) _cards[i] = BuildCard(content, i);

            _reroll = BuildAction(content, IdReroll, "R", -ButtonPitch);
            _banish = BuildAction(content, IdBanish, "B", 0f);
            _skip = BuildAction(content, IdSkip, "X", ButtonPitch);
            for (int i = 0; i < PausePlates; i++) _pause[i] = BuildPausePlate(content, i);

            Presize();
            _skip.Label.SetText("Skip");
            // P3: "your items" (and its tooltip) over the dim, beside the content; the stats column is StatsPanel's
            _inventory = new InventoryPanel(root);
            _group = _canvas.gameObject.AddComponent<CanvasGroup>();
            SetVisible(false);
            ParkPause();
            EnsureStatsPanel();
        }

        static void AutoSize(TMP_Text t, float min, float max)
        {
            t.enableAutoSizing = true;
            t.fontSizeMin = min;
            t.fontSizeMax = max;
        }

        /// <summary>The stats page lives beside the cards: use the scene's, or make one (no scene rebuild needed).</summary>
        void EnsureStatsPanel()
        {
            _statsPanel = StatsPanel.Instance != null ? StatsPanel.Instance : FindAnyObjectByType<StatsPanel>();
            if (_statsPanel == null)
            {
                var go = new GameObject("StatsPanel");
                go.SetActive(false); // its Awake builds with the font: set it first
                go.transform.SetParent(transform.parent != null ? transform.parent : transform, false);
                _statsPanel = go.AddComponent<StatsPanel>();
                _statsPanel.font = font;
                _statsPanel.fontMaterial = fontMaterial;
                go.SetActive(true);
            }
            _statsPanel.cards = this;
        }

        Vector2 _laidOutFor; // the canvas size LayoutContent last fitted

        /// <summary>
        /// Layout A on this canvas (LevelUpLayout) for a row of `cards` (at least three, so the columns stay put: the
        /// pause uses three): the content's scale (always centred), the title's room (the row's span, so it never runs
        /// over a column), and the inventory column (the stats column places itself the same way). On a redraw (open,
        /// reroll, banish), on entering the pause and when the canvas changes size.
        /// </summary>
        void LayoutContent(int cards)
        {
            var size = ((RectTransform)_canvas.transform).rect.size;
            _laidOutFor = size;
            if (size.x < 1f || size.y < 1f) size = MenuLayout.Reference;
            int fitCards = Mathf.Max(LevelUpLayout.PauseCards, cards);
            _fit = LevelUpLayout.For(size, fitCards);
            float s = _fit.ContentScale;
            _content.localScale = new Vector3(s, s, 1f);
            _content.anchoredPosition = new Vector2(_fit.ContentX, 0f);
            float titleW = Mathf.Min(TitleMaxW, LevelUpLayout.RowWidth(fitCards));
            _title.rectTransform.sizeDelta = new Vector2(titleW, TitleH);
            _sub.rectTransform.sizeDelta = new Vector2(titleW, SubH);
            _inventory.Place(size, _fit);
        }

        void SetVisible(bool on)
        {
            _group.alpha = on ? 1f : 0f;
            _group.blocksRaycasts = on;
            _group.interactable = on;
        }

        CardView BuildCard(Transform parent, int index)
        {
            var mid = new Vector2(0.5f, 0.5f);
            var th = UiTheme.Current;
            float innerW = CardW - 2f * Inset;
            // P3: every label inside the stone frame's ContentRect (the four card frames share their insets)
            var cr = UiKit.ContentRect(FrameKind.CardStone, new Vector2(CardW, CardH));
            float keyH = Mathf.Max(KeyMinH, Mathf.Ceil(TextBox.MidlineRow(TextRole.Number, KeySize)));
            float textW = cr.width, keyY = cr.yMin + keyH * 0.5f;
            float descBottom = cr.yMin + keyH + 2f, descH = DescTop - descBottom;
            var v = new CardView();
            v.Root = UiFactory.Rect("Card" + index, parent, mid, mid, Vector2.zero, new Vector2(CardW, CardH));
            // back to front: the selected card's halo, the dark inside, the rarity frame, the focus ring
            v.Halo = UiKit.Frame("Halo", v.Root, FrameKind.Halo, mid, mid, Vector2.zero, new Vector2(CardW + 2f * HaloOut, CardH + 2f * HaloOut), Color.clear);
            v.Inside = UiFactory.Image("Inside", v.Root, th.cardInside, mid, mid, Vector2.zero, new Vector2(CardW - 6f, CardH - 6f), PixelIcons.White);
            v.Inside.raycastTarget = true;
            var hit = v.Inside.gameObject.AddComponent<UiHit>();
            hit.Target = this; hit.Id = index;
            v.Frame = UiKit.Frame("Frame", v.Root, FrameKind.CardStone, mid, mid, Vector2.zero, new Vector2(CardW, CardH), Color.white);
            v.Focus = UiKit.Frame("Focus", v.Root, FrameKind.Focus, mid, mid, Vector2.zero, new Vector2(CardW + 2f * RingOut, CardH + 2f * RingOut), Color.clear);
            v.Band = UiFactory.Image("Band", v.Root, Color.white, mid, mid, new Vector2(0f, BandY), new Vector2(innerW, BandH), PixelIcons.White);
            v.VowHalf = UiFactory.Image("VowHalf", v.Root, Color.clear, mid, mid, new Vector2(-innerW * 0.25f, BandY), new Vector2(innerW * 0.5f, BandH), PixelIcons.White);
            v.Rarity = UiKit.Text("Rarity", v.Root, "COMMON", TextRole.Title, 24f, BandInk, TextAlignmentOptions.Midline, mid, mid,
                new Vector2(0f, BandY), new Vector2(textW, BandH));
            v.Rarity.characterSpacing = 12f; // the band's letter-spacing (spec §2.2)
            // the animated 8-bit icon with its own rarity aura (no square behind it: producer, 2026-10-22); a Legendary's laurel wreath behind it
            v.Wreath = UiKit.Frame("Wreath", v.Root, FrameKind.LaurelWreath, mid, mid, new Vector2(0f, WreathY), UiFrames.NativeSize(FrameKind.LaurelWreath), Color.clear);
            v.Icon = new UpgradeIconView(v.Root, "Icon", mid, new Vector2(0f, IconY), IconSize, lively: true);
            v.Kind = UiKit.Text("Kind", v.Root, "NEW!", TextRole.Title, 21f, NewGold, TextAlignmentOptions.Center, mid, mid,
                new Vector2(0f, TagY), new Vector2(textW, TagH));
            v.Title = UiKit.Text("Title", v.Root, "Title", TextRole.Title, 32f, _ink, TextAlignmentOptions.Center, mid, mid,
                new Vector2(0f, NameY), new Vector2(textW, NameH));
            v.Title.textWrappingMode = TextWrappingModes.Normal; // two lines, then smaller
            AutoSize(v.Title, 26f, 32f);
            v.Pips = new Image[MaxPips];
            for (int p = 0; p < MaxPips; p++)
                v.Pips[p] = UiFactory.Image("Pip" + p, v.Root, PipEmpty, mid, mid, new Vector2(0f, PipY), new Vector2(PipSize, PipSize), PixelIcons.White);
            v.Desc = UiKit.Text("Desc", v.Root, "Desc", TextRole.Body, 24f, _ink, TextAlignmentOptions.Top, mid, mid,
                new Vector2(0f, (DescTop + descBottom) * 0.5f), new Vector2(textW, descH));
            v.Desc.textWrappingMode = TextWrappingModes.Normal;
            v.Desc.richText = true;
            AutoSize(v.Desc, 18f, 24f); // evolutions and tag hints run longer: shrink to fit, never below the 18 floor
            var keyColor = _ink;
            keyColor.a = 0.5f;
            v.Key = UiKit.Text("Key", v.Root, "[" + (index + 1) + "]", TextRole.Number, KeySize, keyColor, TextAlignmentOptions.Midline, mid, mid,
                new Vector2(0f, keyY), new Vector2(textW, keyH));
            return v;
        }

        /// <summary>A bronze tabula ansata with its key chip; a UiHit takes the pointer (no Button: no Selectable tweens in a run).</summary>
        UiPlate BuildAction(Transform parent, int id, string key, float x)
        {
            var mid = new Vector2(0.5f, 0.5f);
            var plate = UiKit.Plate("Action" + id, parent, "", ButtonStyle.Primary, mid, mid, new Vector2(x, ButtonsY), ButtonSize, 30f, key);
            plate.Frame.raycastTarget = true;
            var hit = plate.Frame.gameObject.AddComponent<UiHit>();
            hit.Target = this; hit.Id = id;
            AutoSize(plate.Label, 20f, 30f); // "Banish (1)" never truncates, whatever room the key chip leaves
            return plate;
        }

        /// <summary>P3: RESUME (bronze: the screen's main action), OPTIONS and MAIN MENU (marble), with their keys.</summary>
        UiPlate BuildPausePlate(Transform parent, int i)
        {
            var mid = new Vector2(0.5f, 0.5f);
            var plate = UiKit.Plate("Pause" + i, parent, PauseWords[i], i == 0 ? ButtonStyle.Primary : ButtonStyle.Secondary, mid, mid,
                new Vector2(0f, CardsY + (1 - i) * PausePitch), PausePlateSize, PauseFont, PauseKeys[i]);
            plate.Frame.raycastTarget = true;
            var hit = plate.Frame.gameObject.AddComponent<UiHit>();
            hit.Target = this; hit.Id = ResumeId + i;
            WidenKey(plate, PauseKeyWidth); // every chip as wide as ESC's
            CentreLabel(plate);
            return plate;
        }

        /// <summary>Note 26 (v0.27.0): the word centred on the whole plate, not on the room right of the key chip; its box
        /// as wide as stays clear of the chip on both sides (AutoSize-free: the three pause words fit).</summary>
        static void CentreLabel(UiPlate plate)
        {
            if (plate.KeyChip == null) return;
            var chip = plate.KeyChip.rectTransform;
            var label = plate.Label.rectTransform;
            float chipRight = chip.anchoredPosition.x + chip.sizeDelta.x * 0.5f;
            float half = Mathf.Max(10f, -chipRight - 6f);
            label.sizeDelta = new Vector2(half * 2f, label.sizeDelta.y);
            label.anchoredPosition = new Vector2(0f, label.anchoredPosition.y);
        }

        /// <summary>Widens a plate's key chip to `width` (growing right) and moves the label's left edge with it.</summary>
        static void WidenKey(UiPlate plate, float width)
        {
            if (plate.KeyChip == null || plate.Key == null) return;
            var chip = plate.KeyChip.rectTransform;
            float grow = width - chip.sizeDelta.x;
            if (grow <= 0f) return;
            chip.sizeDelta = new Vector2(width, chip.sizeDelta.y);
            chip.anchoredPosition += new Vector2(grow * 0.5f, 0f);
            var key = plate.Key.rectTransform;
            key.sizeDelta = new Vector2(width, key.sizeDelta.y);
            var label = plate.Label.rectTransform;
            float right = label.anchoredPosition.x + label.sizeDelta.x * 0.5f;
            float left = label.anchoredPosition.x - label.sizeDelta.x * 0.5f + grow;
            label.sizeDelta = new Vector2(Mathf.Max(10f, right - left), label.sizeDelta.y);
            label.anchoredPosition = new Vector2((left + right) * 0.5f, label.anchoredPosition.y);
        }

        /// <summary>
        /// Grow every TMP buffer once with longer text than will ever show, so opening never allocates. The theme's fonts
        /// are static atlases (U1), so this no longer adds glyphs; it still sizes TMP's character arrays.
        /// </summary>
        void Presize()
        {
            const string Long = "WWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWW ABCDEFGHIJKLMNOPQRSTUVWXYZ abcdefghijklmnopqrstuvwxyz 0123456789 " +
                                "+-%×°·—–…!?.,:;'()[]/&#\"<>=_ ’ WWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWW";
            // descriptions take up to their 384-char buffer (Phase 5.5 hints + colour tags): grow theirs past that, or the
            // first long card grows TMP's character arrays mid-run (one ~77 KB allocation per card, v0.6.0/0.7.0 bot runs)
            var longDesc = new System.Text.StringBuilder(400);
            while (longDesc.Length < 400) longDesc.Append(Long).Append(' ');
            longDesc.Length = 400;
            string descText = longDesc.ToString();
            foreach (var c in _cards)
            {
                foreach (var t in new[] { c.Rarity, c.Kind, c.Title, c.Desc })
                {
                    t.SetText(t == c.Desc ? descText : Long);
                    t.ForceMeshUpdate(true, true);
                    t.SetText(""); // a parked card shows nothing (and nothing cut off)
                }
            }
            foreach (var t in new[] { _title, _sub, _reroll.Label, _banish.Label, _skip.Label })
            {
                t.SetText(Long);
                t.ForceMeshUpdate(true, true);
                t.SetText("");
            }
            _title.SetText("LEVEL UP!");
            _sub.SetText("choose one");
        }

        // The canvas stays enabled while hidden, so its meshes and batches are built during load, not on the first level-up.
        void Start() => Canvas.ForceUpdateCanvases();

        void OnEnable() => GameLoop.Register(this, TickGroup.Late, 60);
        void OnDisable() => GameLoop.Unregister(this);

        LevelUpController Ctl => LevelUpController.Instance;
        bool InGrace => Ctl == null || Ctl.Config == null || Time.unscaledTime - _openedAt < Ctl.Config.inputGrace;

        public void Hover(int id)
        {
            if (id >= ResumeId && id < ResumeId + PausePlates) { if (_face == Face.Pause) _pauseFocus = id - ResumeId; return; }
            if (id < MaxCards) { if (id < _shownCount) _selected = id; _hoverAction = -1; }
            else _hoverAction = id;
        }

        public void Click(int id)
        {
            if (id >= ResumeId && id < ResumeId + PausePlates)
            {
                if (_face == Face.Pause && Time.unscaledTime - _openedAt >= PauseGrace) ActivatePause(id - ResumeId);
                return;
            }
            if (InGrace) return;
            if (id < MaxCards) Activate(id);
            else if (id == IdReroll) Reroll();
            else if (id == IdBanish) ToggleBanish();
            else if (id == IdSkip) Skip();
        }

        void Activate(int i)
        {
            var c = Ctl;
            if (c == null || i < 0 || i >= c.ChoiceCount) return;
            if (_banishMode)
            {
                _banishMode = false;
                c.Banish(i);
            }
            else c.Choose(i);
        }

        void Reroll() { _banishMode = false; Ctl?.Reroll(); }
        void Skip() { _banishMode = false; Ctl?.Skip(); }

        void ToggleBanish()
        {
            if (!_banishMode && (Ctl == null || Ctl.BanishesLeft <= 0)) return;
            _banishMode = !_banishMode;
        }

        /// <summary>The pause shows while the player has paused a live run (not under the options or the admin menu).</summary>
        static bool PauseWanted()
        {
            var p = Player.Main;
            var run = RunState.Current;
            return p != null && p.IsPaused && run != null && !run.Ended && !RunState.CombatOver && !OptionsPanel.IsOpen &&
                   (AdminController.Instance == null || !AdminController.Instance.IsOpen);
        }

        public void Tick(float dt, float unscaledDt)
        {
            var c = Ctl;
            bool open = c != null && c.IsOpen;
            var face = open ? Face.Pick : PauseWanted() ? Face.Pause : Face.None;
            if (face != _face) SwitchFace(face);
            if (face == Face.None) return;
            float now = Time.unscaledTime;
            if (face == Face.Pause)
            {
                if (((RectTransform)_canvas.transform).rect.size != _laidOutFor) LayoutContent(LevelUpLayout.PauseCards);
                _inventory.Tick(now, false, default);
                if (now - _openedAt >= PauseGrace) ReadPauseInput();
                RefreshPause();
                return;
            }
            for (int i = 0; i < _shownCount; i++) _cards[i].Icon.Tick(now);
            if (!InGrace) ReadInput(c);
            if (!c.IsOpen) return;
            if (c.OffersVersion != _drawnVersion) Redraw(c);
            else if (((RectTransform)_canvas.transform).rect.size != _laidOutFor) LayoutContent(_shownCount); // the window changed size mid-pick
            if (_selected >= _shownCount) _selected = Mathf.Max(0, _shownCount - 1);
            RefreshDynamic(c);
            bool has = _selected < c.ChoiceCount;
            _inventory.Tick(now, has, has ? c.Offers[_selected] : default);
        }

        void SwitchFace(Face face)
        {
            _face = face;
            SetVisible(face != Face.None);
            _inventory.SetVisible(face != Face.None);
            _banishMode = false;
            _hoverAction = -1;
            _openedAt = Time.unscaledTime;
            if (face == Face.Pick)
            {
                _selected = 0;
                _drawnVersion = -1;
                ParkPause();
                PlaceActions(true);
            }
            else if (face == Face.Pause) EnterPause();
            else ParkPause();
        }

        // ---------- the pause (P3) ----------

        void EnterPause()
        {
            LayoutContent(LevelUpLayout.PauseCards);
            for (int i = 0; i < MaxCards; i++) _cards[i].Root.anchoredPosition = new Vector2(0f, Parked);
            _shownCount = 0;
            PlaceActions(false);
            for (int i = 0; i < PausePlates; i++)
            {
                _pause[i].Root.anchoredPosition = new Vector2(0f, CardsY + (1 - i) * PausePitch);
                if (_pause[i].State != UiState.Normal) _pause[i].SetState(UiState.Normal);
            }
            _pauseFocus = 0;
            _title.text = PausedWord;
            _title.color = Color.white;
            _sub.text = "";
            _drawnBanishMode = !_banishMode; // the next pick redraws its title
            _drawnSource = (PickSource)255;
        }

        void ParkPause()
        {
            for (int i = 0; i < PausePlates; i++) _pause[i].Root.anchoredPosition = new Vector2(0f, Parked);
        }

        void PlaceActions(bool on)
        {
            _reroll.Root.anchoredPosition = new Vector2(-ButtonPitch, on ? ButtonsY : Parked);
            _banish.Root.anchoredPosition = new Vector2(0f, on ? ButtonsY : Parked);
            _skip.Root.anchoredPosition = new Vector2(ButtonPitch, on ? ButtonsY : Parked);
        }

        void ReadPauseInput()
        {
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.qKey.wasPressedThisFrame) { ActivatePause(2); return; }
                if (kb.oKey.wasPressedThisFrame) { ActivatePause(1); return; }
                if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) MovePause(-1);
                if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) MovePause(1);
                if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)
                {
                    ActivatePause(_pauseFocus);
                    return;
                }
            }
            var pad = Gamepad.current;
            if (pad != null)
            {
                if (pad.dpad.up.wasPressedThisFrame) MovePause(-1);
                if (pad.dpad.down.wasPressedThisFrame) MovePause(1);
                float y = pad.leftStick.y.ReadValue();
                if (y > 0.5f && _prevStickY <= 0.5f) MovePause(-1);
                if (y < -0.5f && _prevStickY >= -0.5f) MovePause(1);
                _prevStickY = y;
                if (pad.buttonSouth.wasPressedThisFrame) { ActivatePause(_pauseFocus); return; }
                if (pad.buttonEast.wasPressedThisFrame) ActivatePause(0);
            }
        }

        void MovePause(int d) => _pauseFocus = (_pauseFocus + d + PausePlates) % PausePlates;

        /// <summary>0 RESUME, 1 OPTIONS (over the pause), 2 MAIN MENU.</summary>
        void ActivatePause(int i)
        {
            var p = Player.Main;
            switch (i)
            {
                case 0:
                    if (p != null) p.SetPaused(false);
                    break;
                case 1:
                    if (OptionsPanel.Instance == null) break;
                    if (p != null && !p.IsPaused) p.SetPaused(true); // a click may have resumed first (Player's click-to-resume)
                    OptionsPanel.Instance.Open();
                    break;
                case 2:
                    GameSession.ToMenu();
                    break;
            }
        }

        /// <summary>Colours only, and only when they change: the focused plate glows.</summary>
        void RefreshPause()
        {
            for (int i = 0; i < PausePlates; i++) SetState(_pause[i], i == _pauseFocus ? UiState.Focused : UiState.Normal);
        }

        // ---------- the cards ----------

        void ReadInput(LevelUpController c)
        {
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.digit1Key.wasPressedThisFrame) { Activate(0); return; }
                if (kb.digit2Key.wasPressedThisFrame) { Activate(1); return; }
                if (kb.digit3Key.wasPressedThisFrame) { Activate(2); return; }
                if (kb.digit4Key.wasPressedThisFrame) { Activate(3); return; }
                if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) Move(-1);
                if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) Move(1);
                if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) { Activate(_selected); return; }
                if (kb.rKey.wasPressedThisFrame) { Reroll(); return; }
                if (kb.bKey.wasPressedThisFrame) ToggleBanish();
                if (kb.xKey.wasPressedThisFrame) { Skip(); return; }
                if (kb.escapeKey.wasPressedThisFrame) _banishMode = false;
            }
            var pad = Gamepad.current;
            if (pad != null)
            {
                if (pad.dpad.left.wasPressedThisFrame) Move(-1);
                if (pad.dpad.right.wasPressedThisFrame) Move(1);
                float x = pad.leftStick.x.ReadValue();
                if (x < -0.5f && _prevStickX >= -0.5f) Move(-1);
                if (x > 0.5f && _prevStickX <= 0.5f) Move(1);
                _prevStickX = x;
                if (pad.buttonSouth.wasPressedThisFrame) { Activate(_selected); return; }
                if (pad.buttonWest.wasPressedThisFrame) { Reroll(); return; }
                if (pad.buttonNorth.wasPressedThisFrame) ToggleBanish();
                if (pad.buttonEast.wasPressedThisFrame) _banishMode = false;
                if (pad.selectButton.wasPressedThisFrame) { Skip(); return; }
            }
        }

        void Move(int d)
        {
            if (_shownCount <= 0) return;
            _selected = (_selected + d + _shownCount) % _shownCount;
        }

        void Redraw(LevelUpController c)
        {
            _drawnVersion = c.OffersVersion;
            _shownCount = c.ChoiceCount;
            var cfg = c.Config;
            LayoutContent(_shownCount);
            for (int i = 0; i < MaxCards; i++)
            {
                var v = _cards[i];
                bool on = i < _shownCount;
                // unused cards park off-screen instead of being deactivated
                v.Root.anchoredPosition = on ? new Vector2(LevelUpLayout.CardX(i, _shownCount), CardsY) : new Vector2(0f, Parked);
                if (!on) continue;
                var o = c.Offers[i];
                var col = cfg.RarityColor(o.Rarity);
                v.RarityColor = col;
                var kind = FrameFor(o.Rarity);
                if (kind != v.FrameKind) { v.FrameKind = kind; UiFrames.Apply(v.Frame, kind); }
                v.Wreath.color = o.Rarity == Rarity.Legendary ? Color.white : Color.clear;
                bool vow = o.Def.IsVow;
                v.Band.color = vow ? _gold : col;
                v.VowHalf.color = vow ? _crimson : Color.clear; // a Vow: the oath's crimson | its gold
                if (vow != v.VowBand)
                {
                    v.VowBand = vow; // light words, outlined, read on both halves
                    UiKit.Style(v.Rarity, TextRole.Title, vow ? TextLook.Outline : TextLook.Flat);
                    v.Rarity.characterSpacing = 12f;
                    v.Rarity.color = vow ? _ink : BandInk;
                }
                v.Rarity.text = CardText.RarityName(o.Rarity);
                v.Title.text = o.Def.displayName;
                v.Icon.Set(o.Def, col, i * 0.31f);
                if (o.Def.kind == UpgradeKind.Consumable) { v.Kind.color = PipOwned; v.Kind.text = "SUPPLIES"; }
                else if (vow) { v.Kind.color = VowGold; if (o.IsNew) v.Kind.text = "A VOW!"; else v.Kind.SetText("VOW LV {0}", o.Level); }
                else if (o.Def.evolvesFrom != null) { v.Kind.color = NewGold; v.Kind.text = "EVOLUTION!"; }
                else if (o.IsNew) { v.Kind.color = NewGold; v.Kind.text = o.Def.kind == UpgradeKind.Weapon ? "NEW WEAPON!" : "NEW!"; }
                else { v.Kind.color = PipOwned; v.Kind.SetText("LV {0}", o.Level); }
                CardText.Describe(o, cfg, v.Text);
                if (c.build != null) CardText.Hints(o, c.build.State, c.build.catalog, v.Text);
                v.Desc.SetCharArray(v.Text.Chars, 0, v.Text.Length);
                int pips = o.Def.kind == UpgradeKind.Consumable ? 0 : Mathf.Min(MaxPips, o.Def.maxLevel);
                float px0 = -(pips - 1) * PipPitch * 0.5f;
                for (int p = 0; p < MaxPips; p++)
                {
                    var pip = v.Pips[p];
                    if (p >= pips) { pip.color = Color.clear; continue; }
                    pip.rectTransform.anchoredPosition = new Vector2(px0 + p * PipPitch, PipY);
                    pip.color = p < o.Level - 1 ? PipOwned : p == o.Level - 1 ? col : PipEmpty;
                }
            }
            _drawnBanishMode = !_banishMode; // force the dynamic part to refresh
        }

        /// <summary>Rarity = material: stone, bronze, silver (a meander in its foot), gold (and the wreath).</summary>
        static FrameKind FrameFor(Rarity r) => r switch
        {
            Rarity.Uncommon => FrameKind.CardBronze,
            Rarity.Rare => FrameKind.CardSilver,
            Rarity.Legendary => FrameKind.CardGold,
            _ => FrameKind.CardStone,
        };

        void RefreshDynamic(LevelUpController c)
        {
            for (int i = 0; i < _shownCount; i++)
            {
                var v = _cards[i];
                bool sel = i == _selected;
                v.Frame.color = _banishMode ? BanishTint : Color.white;
                v.Focus.color = !sel ? Color.clear : _banishMode ? _crimson : Color.white; // white ring; crimson to banish
                var glow = v.RarityColor;
                glow.a = sel ? SelectedGlow : 0f;
                v.Halo.color = glow;
                float s = sel ? SelectedScale : 1f;
                if (!Mathf.Approximately(v.Root.localScale.x, s)) v.Root.localScale = new Vector3(s, s, 1f);
            }
            if (_banishMode != _drawnBanishMode || c.Source != _drawnSource)
            {
                _drawnBanishMode = _banishMode;
                _drawnSource = c.Source;
                if (_banishMode)
                {
                    _title.text = "BANISH";
                    _title.color = _crimsonLight; // Roman red, not orange-red: banishing doesn't hurt you
                    _sub.text = "pick a card to remove it for the rest of the run";
                }
                else if (c.Source == PickSource.Chest)
                {
                    _title.text = "ARMORY CHEST";
                    _title.color = _goldBright;
                    _sub.text = "choose one: uncommon or better";
                }
                else if (c.Source == PickSource.Boss)
                {
                    _title.text = "SPOILS OF VICTORY"; // every boss's reward (U1; it read "THE PIT MASTER'S HOARD")
                    _title.color = _goldBright;
                    _sub.text = "take one: rare or better";
                }
                else if (c.Source == PickSource.Altar)
                {
                    _title.text = "BLOOD ALTAR";
                    _title.color = AltarRed;
                    _sub.text = "your blood buys one: rare or better";
                }
                else
                {
                    _title.text = "LEVEL UP!";
                    _title.color = _goldBright;
                    _sub.text = "choose one";
                }
            }
            int rr = c.RerollsLeft, bn = c.BanishesLeft;
            if (rr != _drawnRerolls) { _drawnRerolls = rr; _reroll.Label.SetText("Reroll ({0})", rr); }
            if (bn != _drawnBanishes) { _drawnBanishes = bn; _banish.Label.SetText("Banish ({0})", bn); }
            SetState(_reroll, rr <= 0 ? UiState.Disabled : _hoverAction == IdReroll ? UiState.Focused : UiState.Normal);
            SetState(_banish, _banishMode ? UiState.Pressed : bn <= 0 ? UiState.Disabled : _hoverAction == IdBanish ? UiState.Focused : UiState.Normal);
            SetState(_skip, _hoverAction == IdSkip ? UiState.Focused : UiState.Normal);
        }

        /// <summary>Colours only, and only when the state changes (UiPlate.SetState allocates nothing).</summary>
        static void SetState(UiPlate plate, UiState state)
        {
            if (plate.State != state) plate.SetState(state);
        }
    }
}
