using Gladiator.Core;
using Gladiator.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Gladiator.UI
{
    /// <summary>
    /// W1 (the producer's note 23, docs/tracks/W1-wager.md): the Bookmaker's book, open while Bookmaker.BookOpen (E in his
    /// ring; the run is paused). THE BOOKMAKER'S BOOK in the level-up title's place, "place your wager" under it, 1–4 cards
    /// in the level-up's row (LevelUpLayout, centred; at a three-card level-up's fit or wider, like the level-up), and a
    /// WALK AWAY plate. Each card: the odds in its band, the tier's numeral, the challenge, the time, the stake beside the
    /// aureus, WINS n, and a bronze BET plate (Disabled with "Need n gold" when you can't pay it). The frame's material
    /// climbs with the stake (stone, bronze, silver, gold) and tiers III and IV wear the orange-red of "this hurts".
    /// Fewer than four cards: "MORE KILLS, BIGGER WAGERS" under the row.
    /// Mouse (hover + click a card or its plate), keyboard (1–4, arrows/A-D, Enter/Space, Down/Up to WALK AWAY and back,
    /// Esc walks away) or gamepad (d-pad / stick, South picks, East walks away). Input waits a short grace after opening.
    /// GC: built once, shown with a CanvasGroup (never SetActive or Canvas.enabled), texts pre-sized and written with
    /// constant strings or non-allocating SetText when the book opens; nothing is written per frame but colours.
    /// </summary>
    public sealed class WagerPanel : MonoBehaviour, ITickable, IUiHitTarget
    {
        const int MaxCards = Bookmaker.MaxTiers;
        /// <summary>Hover / Click ids (UiHit; tests click them the way the mouse does): card i is i, its plate 20 + i.</summary>
        public const int WalkAwayId = 10, PlateIdBase = 20;
        public const string TitleWord = "THE BOOKMAKER'S BOOK", SubWord = "place your wager", MoreLine = "MORE KILLS, BIGGER WAGERS";
        const string WalkWord = "Walk away", WalkKey = "ESC";
        static readonly string[] BandWords = { "SMALL BET", "FAIR BET", "LONG SHOT", "HIGH STAKES" };
        static readonly FrameKind[] TierFrames = { FrameKind.CardStone, FrameKind.CardBronze, FrameKind.CardSilver, FrameKind.CardGold };
        static readonly string[] Keys = { "[1]", "[2]", "[3]", "[4]" };
        const float CardW = LevelUpLayout.CardW, CardH = LevelUpLayout.CardH;
        // the card, y from its centre (every label inside the frame's ContentRect: y −228…243, x ±133): 9 frame · 40 band ·
        // the numeral · the challenge · the time · the description · the stake · WINS · the BET plate · the key
        const float Inset = 9f, BandY = 231f, BandH = 40f, NumeralY = 158f, NumeralH = 96f, ChallengeY = 80f, ChallengeH = 60f,
            TimeY = 34f, TimeH = 28f, DescY = -14f, DescH = 62f, StakeY = -66f, StakeH = 36f, WinsY = -104f, WinsH = 36f,
            PlateY = -160f, KeySize = 24f, KeyMinH = 30f, CoinPx = 30f, CoinGap = 8f;
        static readonly Vector2 PlateSize = new Vector2(250f, 56f);
        // the level-up's places (LevelUpPanel), in the content's units from the canvas centre
        const float TitleY = 425f, TitleH = 150f, TitleMaxW = 1400f, SubY = 326f, SubH = 50f, CardsY = 18f,
            MoreY = -280f, MoreH = 34f, WalkY = -345f, Parked = -5000f, Grace = 0.2f;
        static readonly Vector2 WalkSize = new Vector2(320f, 66f);
        const float SelectedScale = 1.04f, RingOut = 6f, HaloOut = 8f, SelectedGlow = 0.45f, WalkKeyWidth = 66f;

        static readonly Color BandInk = new Color32(0x17, 0x12, 0x0F, 0xFF);

        sealed class CardView
        {
            public RectTransform Root;
            public Image Halo, Inside, Frame, Focus, Band, Coin;
            public TextMeshProUGUI BandText, Numeral, Challenge, Duration, Desc, Stake, Wins, Key;
            public UiPlate Plate;
            public FrameKind FrameKind = FrameKind.CardStone;
            public Color Glow = Color.white;
            public bool Afford;
        }

        Canvas _canvas;
        CanvasGroup _group;
        RectTransform _content;
        TextMeshProUGUI _title, _sub, _more;
        readonly CardView[] _cards = new CardView[MaxCards];
        UiPlate _walk;
        Color _goldBright, _ink, _dust, _danger, _coin, _loss, _bronzeLight;
        LevelUpLayout.Fit _fit;
        Vector2 _laidOutFor;
        bool _shown, _onWalk;
        int _selected, _count, _drawnVersion = -1;
        float _openedAt, _prevStickX, _prevStickY;

        public static WagerPanel Instance { get; private set; }

        /// <summary>Tests: the book is on screen, its cards, what card `i` shows, its BET plate, WALK AWAY, the hint line.</summary>
        public bool Shown => _shown;
        public int CardCount => _count;
        public int Selected => _selected;
        public bool WalkAwayFocused => _onWalk;
        public RectTransform Content => _content;
        public RectTransform CanvasRect => (RectTransform)_canvas.transform;
        public RectTransform CardRect(int i) => _cards[i].Root;
        public string CardBand(int i) => _cards[i].BandText.text;
        public string CardNumeral(int i) => _cards[i].Numeral.text;
        public string CardChallenge(int i) => _cards[i].Challenge.text;
        public string CardTime(int i) => _cards[i].Duration.text;
        public string CardDescription(int i) => _cards[i].Desc.text;
        public string CardStake(int i) => _cards[i].Stake.text;
        public string CardWins(int i) => _cards[i].Wins.text;
        public int CardFrame(int i) => UiFrames.KindOf(_cards[i].Frame);
        public UiPlate CardPlate(int i) => _cards[i].Plate;
        public string CardPlateLabel(int i) => _cards[i].Plate.Label.text;
        public UiPlate WalkAwayPlate => _walk;
        public string TitleText => _title.text;
        public string SubtitleText => _sub.text;
        public string MoreText => _more.text;

        void Awake()
        {
            Instance = this;
            var th = UiTheme.Current;
            _goldBright = th.goldBright;
            _ink = th.ink;
            _dust = th.dust;
            _danger = th.danger;
            _coin = th.coin;
            _loss = th.loss;
            _bronzeLight = th.bronzeLight;
            _canvas = UiFactory.Canvas("WagerCanvas", transform, UiSortOrder.LevelUp + 1);
            var root = _canvas.transform;
            var mid = new Vector2(0.5f, 0.5f);
            var dimColor = th.night;
            dimColor.a = 0.64f;
            var dim = UiFactory.Image("Dim", root, dimColor, mid, mid, Vector2.zero, Vector2.zero, PixelIcons.White);
            UiFactory.Stretch(dim.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            dim.raycastTarget = true; // eat clicks meant for the world
            _content = UiFactory.Rect("Content", root, mid, mid, Vector2.zero, Vector2.zero);
            _title = UiKit.Text("Title", _content, TitleWord, TextRole.Shout, 132f, _goldBright, TextAlignmentOptions.Center, mid, mid,
                new Vector2(0f, TitleY), new Vector2(TitleMaxW, TitleH), TextLook.Outline);
            AutoSize(_title, 72f, 132f);
            _sub = UiKit.Text("Sub", _content, SubWord, TextRole.Body, 34f, _ink, TextAlignmentOptions.Center, mid, mid,
                new Vector2(0f, SubY), new Vector2(TitleMaxW, SubH), TextLook.Outline);
            AutoSize(_sub, 22f, 34f);
            for (int i = 0; i < MaxCards; i++) _cards[i] = BuildCard(_content, i);
            _more = UiKit.Text("More", _content, "", TextRole.Title, 26f, _dust, TextAlignmentOptions.Center, mid, mid,
                new Vector2(0f, MoreY), new Vector2(TitleMaxW, MoreH), TextLook.Outline);
            AutoSize(_more, 20f, 26f);
            _walk = UiKit.Plate("WalkAway", _content, WalkWord, ButtonStyle.Secondary, mid, mid, new Vector2(0f, WalkY), WalkSize, 30f, WalkKey);
            _walk.Frame.raycastTarget = true;
            var hit = _walk.Frame.gameObject.AddComponent<UiHit>();
            hit.Target = this; hit.Id = WalkAwayId;
            WidenKey(_walk, WalkKeyWidth);
            Presize();
            _group = _canvas.gameObject.AddComponent<CanvasGroup>();
            SetVisible(false);
            Park();
        }

        static void AutoSize(TMP_Text t, float min, float max)
        {
            t.enableAutoSizing = true;
            t.fontSizeMin = min;
            t.fontSizeMax = max;
        }

        CardView BuildCard(Transform parent, int index)
        {
            var mid = new Vector2(0.5f, 0.5f);
            var th = UiTheme.Current;
            float innerW = CardW - 2f * Inset;
            // every label inside the stone frame's ContentRect (the four card frames share their insets)
            var cr = UiKit.ContentRect(FrameKind.CardStone, new Vector2(CardW, CardH));
            float keyH = Mathf.Max(KeyMinH, Mathf.Ceil(TextBox.MidlineRow(TextRole.Number, KeySize)));
            float textW = cr.width, keyY = cr.yMin + keyH * 0.5f;
            var v = new CardView();
            v.Root = UiFactory.Rect("Wager" + index, parent, mid, mid, Vector2.zero, new Vector2(CardW, CardH));
            // back to front: the selected card's halo, the dark inside, the tier's frame, the focus ring
            v.Halo = UiKit.Frame("Halo", v.Root, FrameKind.Halo, mid, mid, Vector2.zero, new Vector2(CardW + 2f * HaloOut, CardH + 2f * HaloOut), Color.clear);
            v.Inside = UiFactory.Image("Inside", v.Root, th.cardInside, mid, mid, Vector2.zero, new Vector2(CardW - 6f, CardH - 6f), PixelIcons.White);
            v.Inside.raycastTarget = true;
            var hit = v.Inside.gameObject.AddComponent<UiHit>();
            hit.Target = this; hit.Id = index;
            v.Frame = UiKit.Frame("Frame", v.Root, FrameKind.CardStone, mid, mid, Vector2.zero, new Vector2(CardW, CardH), Color.white);
            v.Focus = UiKit.Frame("Focus", v.Root, FrameKind.Focus, mid, mid, Vector2.zero, new Vector2(CardW + 2f * RingOut, CardH + 2f * RingOut), Color.clear);
            v.Band = UiFactory.Image("Band", v.Root, Color.white, mid, mid, new Vector2(0f, BandY), new Vector2(innerW, BandH), PixelIcons.White);
            v.BandText = UiKit.Text("Odds", v.Root, "", TextRole.Title, 24f, BandInk, TextAlignmentOptions.Midline, mid, mid,
                new Vector2(0f, BandY), new Vector2(textW, BandH));
            v.BandText.characterSpacing = 12f; // the band's letter-spacing, as the level-up's rarity band
            v.Numeral = UiKit.Text("Numeral", v.Root, "", TextRole.Shout, 92f, _goldBright, TextAlignmentOptions.Center, mid, mid,
                new Vector2(0f, NumeralY), new Vector2(textW, NumeralH));
            AutoSize(v.Numeral, 60f, 92f);
            v.Challenge = UiKit.Text("Challenge", v.Root, "", TextRole.Title, 30f, _ink, TextAlignmentOptions.Center, mid, mid,
                new Vector2(0f, ChallengeY), new Vector2(textW, ChallengeH));
            v.Challenge.textWrappingMode = TextWrappingModes.Normal; // two lines, then smaller
            AutoSize(v.Challenge, 22f, 30f);
            v.Duration = UiKit.Text("Time", v.Root, "", TextRole.Number, 24f, _dust, TextAlignmentOptions.Midline, mid, mid,
                new Vector2(0f, TimeY), new Vector2(textW, TimeH));
            AutoSize(v.Duration, 18f, 24f);
            v.Desc = UiKit.Text("Desc", v.Root, "", TextRole.Body, 22f, _ink, TextAlignmentOptions.Top, mid, mid,
                new Vector2(0f, DescY), new Vector2(textW, DescH));
            v.Desc.textWrappingMode = TextWrappingModes.Normal;
            AutoSize(v.Desc, 18f, 22f);
            // the stake beside the aureus: the pair is centred when the book opens (Redraw measures the words)
            v.Coin = UiKit.Frame("Coin", v.Root, FrameKind.IconAureus, mid, mid, new Vector2(-40f, StakeY), new Vector2(CoinPx, CoinPx), Color.white);
            v.Stake = UiKit.Text("Stake", v.Root, "", TextRole.Number, 28f, _coin, TextAlignmentOptions.Midline, mid, mid,
                new Vector2(0f, StakeY), new Vector2(textW - CoinPx - CoinGap, StakeH));
            AutoSize(v.Stake, 20f, 28f);
            v.Wins = UiKit.Text("Wins", v.Root, "", TextRole.Title, 30f, _goldBright, TextAlignmentOptions.Midline, mid, mid,
                new Vector2(0f, WinsY), new Vector2(textW, WinsH));
            AutoSize(v.Wins, 22f, 30f);
            v.Plate = UiKit.Plate("Bet", v.Root, "", ButtonStyle.Primary, mid, mid, new Vector2(0f, PlateY), PlateSize, 26f);
            v.Plate.Frame.raycastTarget = true;
            var ph = v.Plate.Frame.gameObject.AddComponent<UiHit>();
            ph.Target = this; ph.Id = PlateIdBase + index;
            var keyColor = _ink;
            keyColor.a = 0.5f;
            v.Key = UiKit.Text("Key", v.Root, Keys[index], TextRole.Number, KeySize, keyColor, TextAlignmentOptions.Midline, mid, mid,
                new Vector2(0f, keyY), new Vector2(textW, keyH));
            return v;
        }

        /// <summary>Widens a plate's key chip to `width` (growing right) and moves the label's left edge with it (as the pause's).</summary>
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

        /// <summary>Grow every TMP buffer once with longer text than will ever show, so opening the book never allocates.</summary>
        void Presize()
        {
            const string Long = "WWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWW ABCDEFGHIJKLMNOPQRSTUVWXYZ abcdefghijklmnopqrstuvwxyz 0123456789 " +
                                "+-%×°·—–…!?.,:;'()[]/&#\"<>=_ ’ WWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWW";
            foreach (var c in _cards)
            {
                foreach (var t in new TMP_Text[] { c.BandText, c.Numeral, c.Challenge, c.Duration, c.Desc, c.Stake, c.Wins, c.Plate.Label })
                {
                    t.SetText(Long);
                    t.ForceMeshUpdate(true, true);
                    t.SetText("");
                }
            }
            foreach (var t in new TMP_Text[] { _title, _sub, _more })
            {
                t.SetText(Long);
                t.ForceMeshUpdate(true, true);
                t.SetText("");
            }
            _title.SetText(TitleWord);
            _sub.SetText(SubWord);
        }

        // The canvas stays enabled while hidden, so its meshes and batches are built during load, not on the first opening.
        void Start() => Canvas.ForceUpdateCanvases();

        void OnEnable()
        {
            Instance = this;
            GameLoop.Register(this, TickGroup.Late, 61);
        }

        void OnDisable()
        {
            GameLoop.Unregister(this);
            if (Instance == this) Instance = null;
        }

        void SetVisible(bool on)
        {
            _group.alpha = on ? 1f : 0f;
            _group.blocksRaycasts = on;
            _group.interactable = on;
        }

        void Park()
        {
            for (int i = 0; i < MaxCards; i++) _cards[i].Root.anchoredPosition = new Vector2(0f, Parked);
        }

        bool InGrace => Time.unscaledTime - _openedAt < Grace;

        public void Hover(int id)
        {
            if (!_shown) return;
            if (id == WalkAwayId) { _onWalk = true; return; }
            int i = id >= PlateIdBase ? id - PlateIdBase : id;
            if (i >= 0 && i < _count) { _selected = i; _onWalk = false; }
        }

        public void Click(int id)
        {
            if (!_shown || InGrace) return;
            var bm = Bookmaker.Instance;
            if (bm == null || !bm.BookOpen) return;
            if (id == WalkAwayId) { bm.CloseBook(); return; }
            Choose(bm, id >= PlateIdBase ? id - PlateIdBase : id);
        }

        /// <summary>Card `i`: takes the wager (the book closes) or, when you can't pay it, the Bookmaker refuses (the book stays).</summary>
        void Choose(Bookmaker bm, int i)
        {
            if (i < 0 || i >= _count) return;
            _selected = i;
            _onWalk = false;
            if (bm.Pick(i)) SfxPlayer.Play2D(SfxId.Pick);
        }

        public void Tick(float dt, float unscaledDt)
        {
            var bm = Bookmaker.Instance;
            bool open = bm != null && bm.BookOpen;
            if (open != _shown) Show(open);
            if (!open) return;
            if (bm.BookVersion != _drawnVersion) Redraw(bm);
            else if (((RectTransform)_canvas.transform).rect.size != _laidOutFor) Layout(_count); // the window changed size
            if (!InGrace) ReadInput(bm);
            if (!bm.BookOpen) return; // picked, or walked away
            RefreshFocus();
        }

        void Show(bool on)
        {
            _shown = on;
            SetVisible(on);
            _openedAt = Time.unscaledTime;
            _selected = 0;
            _onWalk = false;
            if (on) _drawnVersion = -1;
            else Park();
        }

        /// <summary>Layout A on this canvas for a row of `cards` (at least three, as the level-up: the same fit, the same card size).</summary>
        void Layout(int cards)
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
            _more.rectTransform.sizeDelta = new Vector2(titleW, MoreH);
        }

        static bool Dangerous(int tier) => tier >= 2; // III and IV: the orange-red of "this hurts"

        Color BandColor(int tier) => Dangerous(tier) ? _danger : tier == 1 ? _bronzeLight : _dust;

        void Redraw(Bookmaker bm)
        {
            _drawnVersion = bm.BookVersion;
            _count = Mathf.Clamp(bm.OfferCount, 0, MaxCards);
            Layout(_count);
            if (_selected >= _count) _selected = 0;
            for (int i = 0; i < MaxCards; i++)
            {
                var v = _cards[i];
                bool on = i < _count;
                // unused cards park off-screen instead of being deactivated
                v.Root.anchoredPosition = on ? new Vector2(LevelUpLayout.CardX(i, _count), CardsY) : new Vector2(0f, Parked);
                if (!on) continue;
                var o = bm.Offer(i);
                int tier = Mathf.Clamp(o.Tier, 0, MaxCards - 1);
                var kind = TierFrames[tier];
                if (kind != v.FrameKind) { v.FrameKind = kind; UiFrames.Apply(v.Frame, kind); }
                var band = BandColor(tier);
                v.Band.color = band;
                v.Glow = band;
                v.BandText.text = BandWords[tier];
                v.Numeral.text = Bookmaker.Numerals[tier];
                v.Numeral.color = Dangerous(tier) ? _danger : _goldBright;
                switch (o.Kind)
                {
                    case WagerKind.Champion:
                        v.Challenge.text = "SLAY HIS CHAMPION";
                        v.Desc.text = "an elite comes in at a gate: kill it in time";
                        break;
                    case WagerKind.NoHit:
                        v.Challenge.SetText("KILL {0}, UNHIT", o.Target);
                        v.Desc.text = "one hit and the house wins";
                        break;
                    default:
                        v.Challenge.SetText("KILL {0}", o.Target);
                        v.Desc.text = bm.tiers != null && tier < bm.tiers.Length && bm.tiers[tier].paceFactor < 1f ? "keep up your pace" : "beat your own pace";
                        break;
                }
                v.Duration.SetText("IN {0} SECONDS", Mathf.Round(o.Seconds));
                v.Afford = bm.CanAfford(i);
                v.Stake.SetText("STAKE {0}", o.Stake);
                v.Stake.color = v.Afford ? _coin : _loss;
                PlaceStake(v);
                v.Wins.SetText("WINS {0}", o.Payout);
                if (v.Afford) v.Plate.Label.SetText("Bet {0} gold", o.Stake);
                else v.Plate.Label.SetText("Need {0} gold", o.Stake);
                v.Plate.SetState(v.Afford ? UiState.Normal : UiState.Disabled);
            }
            _more.text = _count < MaxCards ? MoreLine : "";
            RefreshFocus();
        }

        /// <summary>Centres the aureus and the stake's words together (measured once, when the book opens).</summary>
        static void PlaceStake(CardView v)
        {
            v.Stake.ForceMeshUpdate();
            float w = Mathf.Max(0f, v.Stake.textBounds.size.x);
            float total = CoinPx + CoinGap + w;
            v.Coin.rectTransform.anchoredPosition = new Vector2(-total * 0.5f + CoinPx * 0.5f, StakeY);
            v.Stake.rectTransform.anchoredPosition = new Vector2(-total * 0.5f + CoinPx + CoinGap + w * 0.5f, StakeY);
        }

        void ReadInput(Bookmaker bm)
        {
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.escapeKey.wasPressedThisFrame) { bm.CloseBook(); return; }
                if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) { Choose(bm, 0); return; }
                if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) { Choose(bm, 1); return; }
                if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame) { Choose(bm, 2); return; }
                if (kb.digit4Key.wasPressedThisFrame || kb.numpad4Key.wasPressedThisFrame) { Choose(bm, 3); return; }
                if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) Move(-1);
                if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) Move(1);
                if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) _onWalk = true;
                if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) _onWalk = false;
                if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)
                {
                    Activate(bm);
                    return;
                }
            }
            var pad = Gamepad.current;
            if (pad != null)
            {
                if (pad.buttonEast.wasPressedThisFrame) { bm.CloseBook(); return; }
                if (pad.dpad.left.wasPressedThisFrame) Move(-1);
                if (pad.dpad.right.wasPressedThisFrame) Move(1);
                if (pad.dpad.down.wasPressedThisFrame) _onWalk = true;
                if (pad.dpad.up.wasPressedThisFrame) _onWalk = false;
                float x = pad.leftStick.x.ReadValue(), y = pad.leftStick.y.ReadValue();
                if (x < -0.5f && _prevStickX >= -0.5f) Move(-1);
                if (x > 0.5f && _prevStickX <= 0.5f) Move(1);
                if (y < -0.5f && _prevStickY >= -0.5f) _onWalk = true;
                if (y > 0.5f && _prevStickY <= 0.5f) _onWalk = false;
                _prevStickX = x;
                _prevStickY = y;
                if (pad.buttonSouth.wasPressedThisFrame) Activate(bm);
            }
        }

        void Activate(Bookmaker bm)
        {
            if (_onWalk) bm.CloseBook();
            else Choose(bm, _selected);
        }

        void Move(int d)
        {
            if (_count <= 0) return;
            _onWalk = false;
            _selected = (_selected + d + _count) % _count;
        }

        /// <summary>Colours only, and only when they change: the selected card's ring, halo and scale, its BET plate's glow.</summary>
        void RefreshFocus()
        {
            for (int i = 0; i < _count; i++)
            {
                var v = _cards[i];
                bool sel = !_onWalk && i == _selected;
                v.Focus.color = sel ? Color.white : Color.clear;
                var glow = v.Glow;
                glow.a = sel ? SelectedGlow : 0f;
                v.Halo.color = glow;
                float s = sel ? SelectedScale : 1f;
                if (!Mathf.Approximately(v.Root.localScale.x, s)) v.Root.localScale = new Vector3(s, s, 1f);
                SetState(v.Plate, !v.Afford ? UiState.Disabled : sel ? UiState.Focused : UiState.Normal);
            }
            SetState(_walk, _onWalk ? UiState.Focused : UiState.Normal);
        }

        /// <summary>Colours only, and only when the state changes (UiPlate.SetState allocates nothing).</summary>
        static void SetState(UiPlate plate, UiState state)
        {
            if (plate.State != state) plate.SetState(state);
        }
    }
}
