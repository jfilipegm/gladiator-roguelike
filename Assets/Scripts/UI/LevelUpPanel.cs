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
    /// U1 layout A (docs/tracks/U1-design.md §6, §8.3): 300×520 cards centred with the title and the buttons, the stats
    /// column (StatsPanel, created here) in the right margin; LevelUpLayout says where on every screen shape. Each card
    /// wears its rarity's frame (stone, bronze, silver + meander, gold + a laurel wreath behind the icon) round a dark
    /// inside: the rarity band, the icon on a halo, tag, name, pips, description, key. A Vow's band is split crimson |
    /// gold. Buttons are bronze tabulae ansatae with key chips (UiKit.Plate + UiHit: no Button in a run).
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
        const float CardW = LevelUpLayout.CardW, CardH = LevelUpLayout.CardH;
        // the card (spec §6.2), y from its centre: 9 frame · 40 band · 112 icon · 26 tag · 68 name · 30 pips ·
        // 174 description · 30 key · 24 foot (the frame's)
        const float Inset = 9f, BandY = 231f, BandH = 40f, IconY = 151f, IconSize = 100f, IconHaloW = 140f, IconHaloH = 112f,
            WreathY = 143f, TagY = 82f, TagH = 26f, NameY = 35f, NameH = 68f, PipY = -14f, PipSize = 14f, PipPitch = 20f,
            DescY = -117f, DescH = 174f, KeyY = -221f, KeyH = 30f;
        const float SelectedScale = 1.04f, RingOut = 6f, HaloOut = 22f;
        // layout A at 1920×1080, in the content's units from the canvas centre: the title's glyphs from y 46, the
        // subtitle at 188, the cards' tops at 262, the buttons at 812 (from the top)
        const float TitleY = 425f, TitleH = 150f, TitleMaxW = 1400f, SubY = 326f, SubH = 50f, CardsY = 18f,
            ButtonsY = -305f, ButtonPitch = 306f;
        static readonly Vector2 ButtonSize = new Vector2(280f, 66f);

        sealed class CardView
        {
            public RectTransform Root;
            public Image Halo, Inside, Frame, Focus, Band, VowHalf, IconHalo, Wreath;
            public TextMeshProUGUI Rarity, Kind, Title, Desc, Key;
            public Image[] Pips;
            public UpgradeIconView Icon;
            public FrameKind FrameKind = FrameKind.CardStone;
            public Color RarityColor = Color.white;
            public bool VowBand;
            public readonly CharBuffer Text = new CharBuffer(384); // description + Phase 5.5 hints with colour tags
        }

        static readonly Color BandInk = new Color32(0x17, 0x12, 0x0F, 0xFF);
        static readonly Color AltarRed = new Color(0.9f, 0.16f, 0.36f); // the altar's blood (today's)
        static readonly Color PipEmpty = new Color(0.26f, 0.29f, 0.34f);
        static readonly Color PipOwned = new Color(0.9f, 0.92f, 0.95f);
        static readonly Color NewGold = new Color(1f, 0.86f, 0.3f);
        static readonly Color VowGold = new Color(1f, 0.76f, 0.22f);
        static readonly Color BanishTint = new Color(0.6f, 0.6f, 0.6f, 1f); // banish mode: every frame at 60 %
        const float SelectedGlow = 0.45f, IconGlow = 0.55f;

        Canvas _canvas;
        CanvasGroup _group;
        RectTransform _content;
        StatsPanel _statsPanel;
        TextMeshProUGUI _title, _sub;
        readonly CardView[] _cards = new CardView[MaxCards];
        UiPlate _reroll, _banish, _skip;
        Color _goldBright, _gold, _crimson, _crimsonLight, _ink;
        LevelUpLayout.Fit _fit = new LevelUpLayout.Fit { ContentScale = 1f, StatsScale = 1f, Centred = true };
        bool _shown, _banishMode;
        int _selected, _hoverAction = -1, _drawnVersion = -1, _shownCount;
        int _drawnRerolls = -1, _drawnBanishes = -1;
        float _openedAt, _prevStickX;
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
        /// <summary>The cards, title and buttons (centred, or slid left for the stats column: LevelUpLayout).</summary>
        public RectTransform Content => _content;
        public StatsPanel Stats => _statsPanel;
        /// <summary>The layout of the last redraw, and the stats column's scale in it.</summary>
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
            root = _content;
            _title = UiKit.Text("Title", root, "LEVEL UP!", TextRole.Shout, 132f, _goldBright, TextAlignmentOptions.Center, mid, mid,
                new Vector2(0f, TitleY), new Vector2(TitleMaxW, TitleH), TextLook.Outline);
            AutoSize(_title, 72f, 132f); // SPOILS OF VICTORY at 4:3
            _sub = UiKit.Text("Sub", root, "choose one", TextRole.Body, 34f, _ink, TextAlignmentOptions.Center, mid, mid,
                new Vector2(0f, SubY), new Vector2(TitleMaxW, SubH), TextLook.Outline);
            AutoSize(_sub, 22f, 34f);

            for (int i = 0; i < MaxCards; i++) _cards[i] = BuildCard(root, i);

            _reroll = BuildAction(root, IdReroll, "R", -ButtonPitch);
            _banish = BuildAction(root, IdBanish, "B", 0f);
            _skip = BuildAction(root, IdSkip, "X", ButtonPitch);

            Presize();
            _skip.Label.SetText("Skip");
            _group = _canvas.gameObject.AddComponent<CanvasGroup>();
            SetVisible(false);
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

        /// <summary>
        /// Layout A on this canvas (LevelUpLayout): the content's scale and x, the title's room (it may not run off either
        /// edge), and the stats column's scale. Only on a redraw (open, reroll, banish), never per frame.
        /// </summary>
        void LayoutContent()
        {
            var size = ((RectTransform)_canvas.transform).rect.size;
            if (size.x < 1f || size.y < 1f) size = MenuLayout.Reference;
            _fit = LevelUpLayout.For(size, _shownCount);
            float s = _fit.ContentScale;
            _content.localScale = new Vector3(s, s, 1f);
            _content.anchoredPosition = new Vector2(_fit.ContentX, 0f);
            float cx = size.x * 0.5f + _fit.ContentX;
            float titleW = Mathf.Min(TitleMaxW, 2f * Mathf.Min(cx - LevelUpLayout.Edge, size.x - LevelUpLayout.Edge - cx) / s);
            _title.rectTransform.sizeDelta = new Vector2(titleW, TitleH);
            _sub.rectTransform.sizeDelta = new Vector2(titleW, SubH);
            if (_statsPanel != null) _statsPanel.SetPickScale(_fit.StatsScale);
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
            v.Rarity = UiKit.Text("Rarity", v.Root, "COMMON", TextRole.Title, 24f, BandInk, TextAlignmentOptions.Center, mid, mid,
                new Vector2(0f, BandY), new Vector2(innerW - 8f, BandH));
            v.Rarity.characterSpacing = 12f; // the band's letter-spacing (spec §2.2)
            // the icon on a halo in its rarity colour; a Legendary's laurel wreath behind it (playtest: an animated 8-bit icon)
            v.IconHalo = UiKit.Frame("IconHalo", v.Root, FrameKind.Halo, mid, mid, new Vector2(0f, IconY), new Vector2(IconHaloW, IconHaloH), Color.clear);
            v.Wreath = UiKit.Frame("Wreath", v.Root, FrameKind.LaurelWreath, mid, mid, new Vector2(0f, WreathY), UiFrames.NativeSize(FrameKind.LaurelWreath), Color.clear);
            v.Icon = new UpgradeIconView(v.Root, "Icon", mid, new Vector2(0f, IconY), IconSize, lively: true);
            v.Kind = UiKit.Text("Kind", v.Root, "NEW!", TextRole.Title, 21f, NewGold, TextAlignmentOptions.Center, mid, mid,
                new Vector2(0f, TagY), new Vector2(innerW - 8f, TagH));
            v.Title = UiKit.Text("Title", v.Root, "Title", TextRole.Title, 32f, _ink, TextAlignmentOptions.Center, mid, mid,
                new Vector2(0f, NameY), new Vector2(innerW - 12f, NameH));
            v.Title.textWrappingMode = TextWrappingModes.Normal; // two lines, then smaller
            AutoSize(v.Title, 26f, 32f);
            v.Pips = new Image[MaxPips];
            for (int p = 0; p < MaxPips; p++)
                v.Pips[p] = UiFactory.Image("Pip" + p, v.Root, PipEmpty, mid, mid, new Vector2(0f, PipY), new Vector2(PipSize, PipSize), PixelIcons.White);
            v.Desc = UiKit.Text("Desc", v.Root, "Desc", TextRole.Body, 24f, _ink, TextAlignmentOptions.Top, mid, mid,
                new Vector2(0f, DescY), new Vector2(innerW - 12f, DescH));
            v.Desc.textWrappingMode = TextWrappingModes.Normal;
            v.Desc.richText = true;
            AutoSize(v.Desc, 18f, 24f); // evolutions and tag hints run longer: shrink to fit, never below the 18 floor
            var keyColor = _ink;
            keyColor.a = 0.5f;
            v.Key = UiKit.Text("Key", v.Root, "[" + (index + 1) + "]", TextRole.Number, 26f, keyColor, TextAlignmentOptions.Center, mid, mid,
                new Vector2(0f, KeyY), new Vector2(innerW, KeyH));
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
            if (id < MaxCards) { if (id < _shownCount) _selected = id; _hoverAction = -1; }
            else _hoverAction = id;
        }

        public void Click(int id)
        {
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

        public void Tick(float dt, float unscaledDt)
        {
            var c = Ctl;
            bool open = c != null && c.IsOpen;
            if (open != _shown)
            {
                _shown = open;
                SetVisible(open);
                _banishMode = false;
                _hoverAction = -1;
                if (open) { _openedAt = Time.unscaledTime; _selected = 0; _drawnVersion = -1; }
            }
            if (!open) return;
            float now = Time.unscaledTime;
            for (int i = 0; i < _shownCount; i++) _cards[i].Icon.Tick(now);
            if (!InGrace) ReadInput(c);
            if (!c.IsOpen) return;
            if (c.OffersVersion != _drawnVersion) Redraw(c);
            if (_selected >= _shownCount) _selected = Mathf.Max(0, _shownCount - 1);
            RefreshDynamic(c);
        }

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
            LayoutContent();
            for (int i = 0; i < MaxCards; i++)
            {
                var v = _cards[i];
                bool on = i < _shownCount;
                // unused cards park off-screen instead of being deactivated
                v.Root.anchoredPosition = on ? new Vector2(LevelUpLayout.CardX(i, _shownCount), CardsY) : new Vector2(0f, -5000f);
                if (!on) continue;
                var o = c.Offers[i];
                var col = cfg.RarityColor(o.Rarity);
                v.RarityColor = col;
                var kind = FrameFor(o.Rarity);
                if (kind != v.FrameKind) { v.FrameKind = kind; UiFrames.Apply(v.Frame, kind); }
                v.Wreath.color = o.Rarity == Rarity.Legendary ? Color.white : Color.clear;
                var halo = col;
                halo.a = IconGlow;
                v.IconHalo.color = halo;
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
