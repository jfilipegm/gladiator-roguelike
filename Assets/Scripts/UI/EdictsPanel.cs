using System;
using System.Collections.Generic;
using System.Globalization;
using Gladiator.Core;
using Gladiator.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gladiator.UI
{
    /// <summary>
    /// The Editor's Edicts (R7, docs/tracks/R7-design.md §2.5): a modal over the arena screen where the player accepts
    /// decrees that make the games harder, each for a share of extra Renown. A 2 × 6 grid of cards in MetaConfig.edicts
    /// order (all eleven on one page): icon, name, effect line, "+25 % RENOWN". On: an orange-red frame, a warm card and
    /// an ON chip (the colour language: it hurts you); off: grey. A locked challenge edict is a dark card with its
    /// unlock's hint and progress; a locked Market one stays lit (you see what you buy) with a BUY button. The footer sums
    /// them ("3 EDICTS · RENOWN ×1.65"). Arrows / d-pad move, Enter / South toggles or buys, Backspace / West clears all,
    /// Esc / East closes and saves the choice (SaveData.edictsChosen). `Open(forced)` is the daily's read-only page
    /// ("PROCLAIMED BY THE EDITOR"). Built once; a toggle only recolours and retexts the cards. Menu-only UI: it
    /// allocates freely. `save` is the save the menu shows (MetaProgress.Data): buying and closing write through
    /// MetaProgress.
    /// U1: a carved `Marble` page (the menu scales it with UiKit.FitScale), the header engraved in Title 48 over a
    /// crimson rule; the cards dark `Stone` plates (name Title 26, effect Body 20, share Number 22); ON = a focus ring in
    /// the danger colour hugging the card plus the ON chip; the keyboard focus a gold ring outside it; locked cards
    /// darker; BUY a bronze plate; the footer in Title 26.
    /// P3 (the producer's notes 1, 4 and 5): no focus ring. The keyboard focus is a brighter face (a soft band over the
    /// stone) and a small lift, the hover a brighter face; the orange-red ON ring stays (it means something). The daily's
    /// read-only page has nothing to press: no ON chips, no OFF / LOCKED / progress words, no padlocks; the day's edicts
    /// keep their ring and say "TODAY'S EDICT". Every label sits inside its frame's content rect; the orange-red written
    /// on marble is the darker one (MainMenu.MarbleHurtHex) so it reads there.
    /// </summary>
    public sealed class EdictsPanel : IUiHoverTarget
    {
        public const int Columns = 2;
        /// <summary>The panel's box at 1:1 (the menu scales it down to fit smaller canvases).</summary>
        public static readonly Vector2 Size = new Vector2(1640f, 980f);
        static readonly Vector2 Card = new Vector2(780f, 100f);
        const float GapX = 20f, GapY = 12f, Ring = 6f, GridTop = 340f, RightW = 270f, Lift = 4f; // RightW: "BUY · 250 RENOWN" inside the plate's content rect
        /// <summary>The read-only page's word for the day's edicts.</summary>
        public const string TodaysEdict = "TODAY'S EDICT";

        /// <summary>Anything that hurts the player (CLAUDE.md §5): the edicts that are on.</summary>
        public static readonly Color Hurt = new Color(1f, 0.33f, 0.2f);
        /// <summary>A locked card's stone: the plate darkened.</summary>
        static readonly Color StoneDark = new Color(0.55f, 0.55f, 0.55f, 1f);
        static readonly Color PlateColor = new Color(0.02f, 0.02f, 0.03f, 1f);
        static readonly Color Silhouette = new Color(0f, 0f, 0f, 0.9f);
        /// <summary>Gold written on the marble page (the bright gold doesn't read on marble).</summary>
        const string HurtHex = MainMenu.MarbleHurtHex, GoldHex = "#7E5514"; // P3: 4.6:1 on marble (#A8741F was 2.7:1)

        const string KeysLine = "Arrows: move  ·  Enter / A: toggle or buy  ·  X / Backspace: clear  ·  Esc / B: done";
        const string KeysReadOnly = "Esc / B: back";

        sealed class CardView
        {
            public RunModifierDef Def;
            public CanvasGroup Group;
            public RectTransform Holder;
            public Image Glow, OnRing, Border, Chip, Padlock;
            public RawImage Icon;
            public TextMeshProUGUI Glyph, Name, Line, Renown, State;
            public Button Buy;
            public UiPlate BuyPlate;
            public TextMeshProUGUI BuyLabel;
        }

        MetaConfig _meta;
        SaveData _save;
        Action _onClose;
        GameObject _root;
        RectTransform _box;
        Image _page;
        TextMeshProUGUI _header, _sub, _renown, _footer, _keys;
        Button _clear, _done;
        RunModifierDef[] _defs = Array.Empty<RunModifierDef>();
        CardView[] _cards = Array.Empty<CardView>();
        bool[] _on = Array.Empty<bool>();
        int _focus, _hover = -1;

        /// <summary>The font arguments are ignored (U1: the theme decides).</summary>
        public EdictsPanel(TMP_FontAsset font, Material fontMat) { }

        /// <summary>Called after every toggle, clear or purchase (the menu's button and tablet follow the panel live).</summary>
        public Action OnChanged;

        public bool IsOpen { get; private set; }
        /// <summary>The daily's page: the forced edicts on, nothing toggles, nothing is saved.</summary>
        public bool ReadOnly { get; private set; }
        /// <summary>Something was bought since the panel last opened (the menu refreshes the Codex and its Renown).</summary>
        public bool BoughtAny { get; private set; }
        public int Count => _defs.Length;
        public int Focus => _focus;
        public RectTransform Box => _box;
        public RunModifierDef Def(int i) => i >= 0 && i < _defs.Length ? _defs[i] : null;
        public bool IsOn(int i) => i >= 0 && i < _on.Length && _on[i];
        /// <summary>Not earned yet (the Edicts themselves, its Market purchase or its challenge).</summary>
        public bool IsLocked(int i) => i < 0 || i >= _defs.Length || !MetaProgress.IsOpen(_defs[i], _meta, _save);
        /// <summary>The index of the edict with `id`, or -1.</summary>
        public int IndexOf(string id)
        {
            for (int i = 0; i < _defs.Length; i++) if (_defs[i].id == id) return i;
            return -1;
        }

        /// <summary>The edicts that are on, in MetaConfig.edicts order.</summary>
        public RunModifierDef[] Chosen
        {
            get
            {
                var list = new List<RunModifierDef>();
                for (int i = 0; i < _defs.Length; i++) if (_on[i]) list.Add(_defs[i]);
                return list.ToArray();
            }
        }

        /// <summary>What the chosen edicts multiply a run's Renown by: 1 + Σ bonus.</summary>
        public float RenownMul => 1f + RunModifiers.Bonus(Chosen);

        // ---- tests: what the panel shows
        public string HeaderText => _header.text;
        public string FooterText => _footer.text;
        public string KeysText => _keys.text;
        public string RenownText => _renown.text;
        public string CardLine(int i) => i >= 0 && i < _cards.Length ? _cards[i].Line.text : "";
        public string CardState(int i) => i >= 0 && i < _cards.Length ? (_cards[i].Buy.gameObject.activeSelf ? _cards[i].BuyLabel.text : _cards[i].State.text) : "";
        public RectTransform CardRect(int i) => _cards[i].Border.rectTransform;
        /// <summary>The colour of the frame that marks the card's state: the ON ring (orange-red: it hurts you) while it's
        /// on, else the stone plate's tint.</summary>
        public Color CardFrame(int i) => _on[i] ? _cards[i].OnRing.color : _cards[i].Border.color;
        public TextMeshProUGUI CardName(int i) => _cards[i].Name;
        // ---- tests (U1): the look
        /// <summary>The page's frame (`FrameKind.Marble`) and card `i`'s plate (`FrameKind.Stone`).</summary>
        public int PageFrame => _page != null ? UiFrames.KindOf(_page) : -1;
        public int CardPlate(int i) => UiFrames.KindOf(_cards[i].Border);
        /// <summary>Tests (P3): card `i` shows its ON chip; it's brighter (focused or hovered); it stands lifted (focused).</summary>
        public bool ChipShown(int i) => _cards[i].Chip.gameObject.activeSelf;
        public bool CardBright(int i) => _cards[i].Glow.enabled;
        public bool CardLifted(int i) => _cards[i].Holder.anchoredPosition != CardHome(i);
        /// <summary>Tests (P3): card `i` shows its padlock.</summary>
        public bool PadlockShown(int i) => _cards[i].Padlock.enabled;
        /// <summary>Card `i`'s ON ring: shown (enabled with alpha) only while it's on; its frame kind and colour.</summary>
        public bool OnRingShown(int i) => _cards[i].OnRing.enabled && _cards[i].OnRing.color.a > 0f;
        public int OnRingKind(int i) => UiFrames.KindOf(_cards[i].OnRing);
        public Color OnRingColor(int i) => _cards[i].OnRing.color;
        public Component HeaderLabel => _header;
        public UiPlate BuyPlate(int i) => _cards[i].BuyPlate;

        /// <summary>"×1.65" (two decimals at most, invariant culture).</summary>
        public static string MulText(float mul) => "×" + mul.ToString("0.##", CultureInfo.InvariantCulture);

        /// <summary>The arena screen's button: "EDICTS" with none on, else "EDICTS · RENOWN ×1.65".</summary>
        public static string ButtonText(int count, float mul) => count <= 0 ? "EDICTS" : "EDICTS · RENOWN " + MulText(mul);

        /// <summary>"+25 % RENOWN": an edict's share.</summary>
        public static string Share(RunModifierDef d) => "+" + Mathf.RoundToInt(Mathf.Max(0f, d.renownBonus) * 100f) + " % RENOWN";

        /// <summary>Builds the panel (hidden) under `root`, which should cover the screen.</summary>
        public void Build(Transform root, MetaConfig meta, SaveData save, Action onClose)
        {
            _meta = meta;
            _save = save ?? MetaProgress.Data;
            _onClose = onClose;
            var defs = new List<RunModifierDef>();
            if (meta != null && meta.edicts != null)
                foreach (var e in meta.edicts)
                    if (e != null && !defs.Contains(e)) defs.Add(e);
            _defs = defs.ToArray();
            _on = new bool[_defs.Length];

            var go = new GameObject("Edicts", typeof(RectTransform));
            go.transform.SetParent(root, false);
            _root = go;
            UiFactory.Stretch((RectTransform)go.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var mid = new Vector2(0.5f, 0.5f);
            var night = UiTheme.Current.night;
            night.a = 0.86f;
            var dim = UiFactory.Image("Dim", go.transform, night, mid, mid, Vector2.zero, Vector2.zero, PixelIcons.White);
            UiFactory.Stretch(dim.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            dim.raycastTarget = true; // the arena screen underneath takes no clicks

            _box = UiFactory.Rect("Box", go.transform, mid, mid, Vector2.zero, Size);
            _page = UiKit.Frame("Page", _box, FrameKind.Marble, mid, mid, Vector2.zero, Size, Color.white);
            _page.raycastTarget = true;

            var th = UiTheme.Current;
            float left = -Size.x * 0.5f + 40f, right = Size.x * 0.5f - 40f;
            var leftMid = new Vector2(0f, 0.5f);
            _header = Text(_box, "", TextRole.Title, 48, th.marbleInk, new Vector2(left + 500f, 436f), new Vector2(1000f, 62f), TextAlignmentOptions.MidlineLeft, TextLook.Engraved);
            UiKit.Rule("Rule", _box, mid, leftMid, new Vector2(left, 404f), 720f);
            _sub = Text(_box, "", TextRole.Body, 22, th.marbleInk, new Vector2(left + 500f, 380f), new Vector2(1000f, 30f), TextAlignmentOptions.MidlineLeft, TextLook.Engraved);
            _renown = Text(_box, "", TextRole.Number, 34, th.goldBright, new Vector2(right - 260f, 436f), new Vector2(520f, 50f), TextAlignmentOptions.MidlineRight, TextLook.Outline);

            _cards = new CardView[_defs.Length];
            for (int i = 0; i < _defs.Length; i++) _cards[i] = BuildCard(i);
            if (_defs.Length == 0)
                Text(_box, "No edicts yet.", TextRole.Body, 28, th.marbleInk, new Vector2(0f, 0f), new Vector2(800f, 60f), TextAlignmentOptions.Center, TextLook.Engraved);

            _footer = Text(_box, "", TextRole.Title, 26, th.marbleInk, new Vector2(0f, -364f), new Vector2(1000f, 40f), TextAlignmentOptions.Center, TextLook.Engraved);
            var keys = th.marbleInk;
            keys.a = 0.75f;
            _keys = Text(_box, "", TextRole.Body, 22, keys, new Vector2(0f, -440f), new Vector2(1060f, 30f), TextAlignmentOptions.Center, TextLook.Flat);
            _clear = UiKit.Button("Clear", _box, "CLEAR", ButtonStyle.Secondary, mid, mid, new Vector2(left + 110f, -432f), new Vector2(220f, 66f), () => ClearAll(), 28f).Button;
            _done = UiKit.Button("Done", _box, "DONE", ButtonStyle.Primary, mid, mid, new Vector2(right - 110f, -432f), new Vector2(220f, 66f), () => Close(), 28f).Button;
            go.SetActive(false);
        }

        CardView BuildCard(int i)
        {
            var d = _defs[i];
            var th = UiTheme.Current;
            var v = new CardView { Def = d };
            var mid = new Vector2(0.5f, 0.5f);
            var holder = UiFactory.Rect("Edict_" + d.id, _box, mid, mid, CardHome(i), Card + new Vector2(Ring * 2f, Ring * 2f));
            v.Holder = holder;
            v.Group = holder.gameObject.AddComponent<CanvasGroup>();
            v.Border = UiKit.Frame("Border", holder, FrameKind.Stone, mid, mid, Vector2.zero, Card, Color.white);
            v.Border.raycastTarget = true;
            // P3: focused or hovered, a brighter face (a soft band over the stone, under the words); no ring
            v.Glow = UiFactory.Image("Glow", v.Border.transform, UiKit.BandLight, mid, mid, Vector2.zero, Card, PixelIcons.White);
            v.Glow.raycastTarget = false;
            v.Glow.enabled = false;
            var button = v.Border.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            int index = i;
            button.onClick.AddListener(() => Click(index));
            var hover = v.Border.gameObject.AddComponent<UiHover>();
            hover.Target = this;
            hover.Id = i;
            // ON: the danger ring hugging the plate (it hurts you)
            v.OnRing = UiKit.Frame("OnRing", holder, FrameKind.Focus, mid, mid, Vector2.zero, Card + new Vector2(Ring * 2f, Ring * 2f), Hurt);
            v.OnRing.enabled = false;
            var c = v.Border.transform;
            float half = Card.x * 0.5f;

            // the icon: a 20 px atlas cell at 3× (whole art pixels); before the atlas has its row, the initial
            var content = UiKit.ContentRect(FrameKind.Stone, Card);
            var iconAt = new Vector2(-half + 48f, 0f);
            UiFactory.Image("IconPlate", c, PlateColor, mid, mid, iconAt, new Vector2(68f, 68f), PixelIcons.White);
            v.Icon = UiFactory.Rect("Icon", c, mid, mid, iconAt, new Vector2(60f, 60f)).gameObject.AddComponent<RawImage>();
            v.Icon.raycastTarget = false;
            bool hasIcon = d.icon != null && d.iconRow >= 0;
            v.Icon.enabled = hasIcon;
            if (hasIcon)
            {
                float cw = (float)UpgradeIconView.Cell / d.icon.width, ch = (float)UpgradeIconView.Cell / d.icon.height;
                v.Icon.texture = d.icon;
                v.Icon.uvRect = new Rect(0f, 1f - (d.iconRow + 1) * ch, cw, ch);
            }
            v.Glyph = Text(c, Initial(d), TextRole.Title, 36, Hurt, iconAt, new Vector2(60f, 60f), TextAlignmentOptions.Center, TextLook.Flat);
            v.Glyph.enabled = !hasIcon;

            // the words, each on one line inside the plate's content rect (P3): name over effect line in the middle, the
            // Renown share over the state on the right; placed by their ink (Midline)
            float rw = RightW, rx = content.xMax - rw * 0.5f;
            float x0 = -half + 96f, x1 = rx - rw * 0.5f - 8f, cx = (x0 + x1) * 0.5f, w = x1 - x0;
            float top = content.yMax - 10f, bottom = content.yMin + 10f;
            v.Name = Text(c, d.displayName, TextRole.Title, 26, th.ink, new Vector2(cx, top), new Vector2(w, 30f), TextAlignmentOptions.MidlineLeft, TextLook.Flat);
            v.Name.textWrappingMode = TextWrappingModes.NoWrap;
            AutoSize(v.Name, 18f, 26f);
            v.Line = Text(c, "", TextRole.Body, 20, th.ink, new Vector2(cx, bottom), new Vector2(w, 26f), TextAlignmentOptions.MidlineLeft, TextLook.Flat);
            v.Line.textWrappingMode = TextWrappingModes.NoWrap;
            AutoSize(v.Line, 18f, 20f);
            v.Renown = Text(c, Share(d), TextRole.Number, 22, th.gold, new Vector2(rx, top), new Vector2(rw, 26f), TextAlignmentOptions.MidlineRight, TextLook.Outline);
            v.Renown.textWrappingMode = TextWrappingModes.NoWrap;
            AutoSize(v.Renown, 18f, 22f);
            v.State = Text(c, "", TextRole.Title, 20, th.dust, new Vector2(rx, bottom), new Vector2(rw, 26f), TextAlignmentOptions.MidlineRight, TextLook.Flat);
            v.State.textWrappingMode = TextWrappingModes.NoWrap;
            AutoSize(v.State, 18f, 20f);
            v.Padlock = UiFactory.Image("Padlock", c, Color.white, mid, mid, new Vector2(x1 - 6f, bottom), new Vector2(18f, 24f), PixelIcons.Lock);
            v.Chip = UiFactory.Image("On", c, Hurt, mid, mid, new Vector2(content.xMax - 42f, bottom), new Vector2(84f, 34f), PixelIcons.White);
            Text(v.Chip.transform, "ON", TextRole.Title, 24, Color.white, Vector2.zero, new Vector2(84f, 34f), TextAlignmentOptions.Midline, TextLook.Flat);
            v.BuyPlate = UiKit.Button("Buy", c, "BUY", ButtonStyle.Primary, mid, mid, new Vector2(rx, bottom), new Vector2(rw, 44f), () => Buy(index), 20f);
            v.Buy = v.BuyPlate.Button;
            v.BuyLabel = UiFactory.Label(v.Buy);
            v.BuyLabel.overflowMode = TextOverflowModes.Truncate;
            AutoSize(v.BuyLabel, 18f, 20f);
            return v;
        }

        /// <summary>Card `i`'s resting place in the 2-column grid (the focused one stands Lift above it).</summary>
        static Vector2 CardHome(int i)
        {
            int col = i % Columns, row = i / Columns;
            return new Vector2((col - (Columns - 1) * 0.5f) * (Card.x + GapX), GridTop - Card.y * 0.5f - row * (Card.y + GapY));
        }

        /// <summary>"The Hungry Horde" → "H": the stand-in glyph while the icon atlas has no row for it.</summary>
        public static string Initial(RunModifierDef d)
        {
            string n = d.displayName ?? "";
            if (n.StartsWith("The ", StringComparison.OrdinalIgnoreCase)) n = n.Substring(4);
            if (n.StartsWith("A ", StringComparison.OrdinalIgnoreCase)) n = n.Substring(2);
            return n.Length > 0 ? n.Substring(0, 1).ToUpperInvariant() : "?";
        }

        /// <summary>Opens the page: the saved choice, or (read-only, the daily) `forced` on and nothing else.</summary>
        public void Open(RunModifierDef[] forced = null)
        {
            ReadOnly = forced != null;
            BoughtAny = false;
            var on = forced ?? (_meta != null ? MetaProgress.ChosenEdicts(_meta, _save) : Array.Empty<RunModifierDef>());
            for (int i = 0; i < _defs.Length; i++) _on[i] = Array.IndexOf(on, _defs[i]) >= 0;
            _focus = 0;
            _hover = -1;
            _header.text = ReadOnly ? "PROCLAIMED BY THE EDITOR" : "THE EDITOR'S EDICTS";
            _sub.text = ReadOnly
                ? "The day's edicts are fixed: every attempt fights under them."
                : "Decrees that make the games harder. Each one you accept pays a share of extra Renown.";
            _keys.text = ReadOnly ? KeysReadOnly : KeysLine;
            _clear.gameObject.SetActive(!ReadOnly);
            UiFactory.Label(_done).text = ReadOnly ? "BACK" : "DONE";
            if (_root != null) _root.SetActive(true);
            IsOpen = true;
            Refresh();
        }

        /// <summary>Closes the page; the choice is saved (SaveData.edictsChosen) unless it's read-only.</summary>
        public void Close()
        {
            if (!IsOpen) return;
            if (!ReadOnly) MetaProgress.SetChosenEdicts(Chosen);
            IsOpen = false;
            if (_root != null) _root.SetActive(false);
            _onClose?.Invoke();
        }

        /// <summary>Turns an earned edict on or off. False (and a refusal sound) when it's locked or the page is read-only.</summary>
        public bool Toggle(int i)
        {
            if (!IsOpen || ReadOnly || i < 0 || i >= _defs.Length || IsLocked(i))
            {
                SfxPlayer.Play2D(SfxId.Blocked);
                return false;
            }
            _on[i] = !_on[i];
            SfxPlayer.Play2D(_on[i] ? SfxId.Pick : SfxId.Card);
            Changed();
            return true;
        }

        /// <summary>Buys a Market edict's unlock with Renown (it stays off: buying never adds a handicap by itself).</summary>
        public BuyResult Buy(int i)
        {
            var u = i >= 0 && i < _defs.Length ? _defs[i].unlock : null;
            if (ReadOnly || u == null || u.category != UnlockCategory.Market)
            {
                SfxPlayer.Play2D(SfxId.Blocked);
                return BuyResult.NotForSale;
            }
            var r = ReferenceEquals(_save, MetaProgress.Data) ? MetaProgress.Buy(u) : MetaProgress.Buy(u, _save);
            SfxPlayer.Play2D(r == BuyResult.Bought ? SfxId.Coin : SfxId.Blocked);
            if (r == BuyResult.Bought)
            {
                BoughtAny = true;
                Changed();
            }
            return r;
        }

        /// <summary>Every edict off (not on the read-only page).</summary>
        public void ClearAll()
        {
            if (!IsOpen || ReadOnly) { SfxPlayer.Play2D(SfxId.Blocked); return; }
            bool any = false;
            for (int i = 0; i < _on.Length; i++) { any |= _on[i]; _on[i] = false; }
            SfxPlayer.Play2D(SfxId.Card);
            if (any) Changed();
        }

        /// <summary>Scales the box (MenuLayout-style: down, never up) about the screen's centre.</summary>
        public void Fit(float scale)
        {
            if (_box != null) _box.localScale = new Vector3(scale, scale, 1f);
        }

        /// <summary>Keyboard / gamepad: returns true when the page closes.</summary>
        public bool HandleInput(bool left, bool right, bool up, bool down, bool confirm, bool back, bool clear)
        {
            if (!IsOpen) return false;
            if (back)
            {
                SfxPlayer.Play2D(SfxId.Card);
                Close();
                return true;
            }
            int dx = (right ? 1 : 0) - (left ? 1 : 0), dy = (down ? 1 : 0) - (up ? 1 : 0);
            if ((dx != 0 || dy != 0) && _defs.Length > 0)
            {
                int s = GridMath.Step(_focus, _defs.Length, Columns, dx, dx != 0 ? 0 : dy);
                if (s != _focus)
                {
                    _focus = s;
                    SfxPlayer.Play2D(SfxId.Card);
                    Refresh();
                }
            }
            if (confirm) Activate(_focus);
            else if (clear) ClearAll();
            return false;
        }

        /// <summary>Enter / South on a card: a locked Market edict is bought, anything else toggles.</summary>
        void Activate(int i)
        {
            if (i < 0 || i >= _defs.Length) return;
            if (IsMarketLocked(i)) Buy(i);
            else Toggle(i);
        }

        /// <summary>A click focuses the card and toggles it; a locked Market card only takes the focus (its BUY button buys).</summary>
        void Click(int i)
        {
            if (!IsOpen) return;
            bool moved = i != _focus;
            _focus = i;
            if (ReadOnly || IsMarketLocked(i))
            {
                SfxPlayer.Play2D(moved ? SfxId.Card : SfxId.Blocked);
                Refresh();
                return;
            }
            if (!Toggle(i)) Refresh();
        }

        public void Hover(int id, bool on)
        {
            if (on) _hover = id;
            else if (_hover == id) _hover = -1;
            if (IsOpen) Refresh();
        }

        bool IsMarketLocked(int i)
        {
            var u = _defs[i].unlock;
            return IsLocked(i) && u != null && u.category == UnlockCategory.Market;
        }

        void Changed()
        {
            Refresh();
            OnChanged?.Invoke();
        }

        void Refresh()
        {
            int n = 0;
            for (int i = 0; i < _defs.Length; i++)
            {
                if (_on[i]) n++;
                Draw(i);
            }
            float mul = RenownMul;
            _footer.text = n <= 0
                ? "NO EDICTS · RENOWN " + MulText(1f)
                : "<color=" + HurtHex + ">" + n + (n == 1 ? " EDICT" : " EDICTS") + "</color> · <color=" + GoldHex + ">RENOWN " + MulText(mul) + "</color>";
            _renown.text = "RENOWN " + _save.renown;
        }

        void Draw(int i)
        {
            var th = UiTheme.Current;
            var v = _cards[i];
            var d = v.Def;
            bool on = _on[i], locked = IsLocked(i);
            var u = d.unlock != null ? d.unlock : _meta != null ? _meta.edictsUnlock : null;
            bool market = locked && d.unlock != null && d.unlock.category == UnlockCategory.Market;
            bool dark = locked && !market && !(ReadOnly && on); // the daily may proclaim an edict you haven't earned: it shows lit
            // P3: focused = brighter + lifted, hovered = brighter (the read-only page has nothing to focus)
            bool focused = i == _focus && !ReadOnly;
            v.Glow.enabled = focused || (i == _hover && !ReadOnly);
            var home = CardHome(i);
            var at = focused ? home + new Vector2(0f, Lift) : home;
            if (v.Holder.anchoredPosition != at) v.Holder.anchoredPosition = at;
            v.OnRing.enabled = on;
            v.Border.color = dark ? StoneDark : Color.white;
            v.Icon.color = dark ? Silhouette : Color.white;
            v.Glyph.color = dark ? th.dust : Hurt;
            v.Name.color = dark ? th.dust : th.ink;
            v.Line.text = dark ? (u != null && !string.IsNullOrEmpty(u.hint) ? u.hint : "???") : !string.IsNullOrEmpty(d.effectLine) ? d.effectLine : d.description ?? "";
            v.Line.color = dark ? th.dust : th.ink;
            var share = th.gold;
            if (dark) share.a = 0.45f;
            v.Renown.color = share;
            v.Chip.gameObject.SetActive(on && !ReadOnly); // read-only (note 5): nothing to switch, no chip
            bool buy = market && !ReadOnly && !on;
            v.BuyPlate.Root.gameObject.SetActive(buy);
            if (buy)
            {
                int cost = Mathf.Max(0, d.unlock.renownCost);
                bool afford = _save.renown >= cost;
                v.BuyLabel.text = afford ? "BUY · " + cost + " RENOWN" : cost + " RENOWN";
                v.Buy.interactable = afford;
                v.BuyPlate.SetState(afford ? UiState.Normal : UiState.Disabled);
            }
            v.Padlock.enabled = dark && !ReadOnly;
            if (ReadOnly) // the day's edicts say so; the rest say nothing (no OFF, LOCKED or progress to act on)
            {
                v.State.text = on ? TodaysEdict : "";
                v.State.color = Hurt;
            }
            else
            {
                v.State.text = on || buy ? "" : dark ? Progress(u) : market ? "LOCKED" : "OFF";
                v.State.color = th.dust;
            }
            v.Group.alpha = ReadOnly && !on ? 0.4f : 1f;
        }

        /// <summary>"1 / 2" for an unlock that waits on one count, else "LOCKED".</summary>
        string Progress(UnlockDef u)
        {
            if (u == null || u.conditions == null || u.conditions.Length != 1) return "LOCKED";
            MetaProgress.Progress(u.conditions[0], _save, out float cur, out float tgt);
            return tgt > 1f ? LanistaTablet.Num(Mathf.Min(cur, tgt)) + " / " + LanistaTablet.Num(tgt) : "LOCKED";
        }

        static void AutoSize(TextMeshProUGUI t, float min, float max)
        {
            t.enableAutoSizing = true;
            t.fontSizeMin = min;
            t.fontSizeMax = max;
        }

        static TextMeshProUGUI Text(Transform parent, string text, TextRole role, float size, Color color, Vector2 pos, Vector2 box, TextAlignmentOptions align, TextLook look)
        {
            var mid = new Vector2(0.5f, 0.5f);
            var t = UiKit.Text("Line", parent, text, role, size, color, align, mid, mid, pos, box, look);
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Truncate; // never Ellipsis (docs/tracks/README.md)
            return t;
        }
    }
}
