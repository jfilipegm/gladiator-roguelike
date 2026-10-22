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
    /// </summary>
    public sealed class EdictsPanel : IUiHoverTarget
    {
        public const int Columns = 2;
        /// <summary>The panel's box at 1:1 (the menu scales it down to fit smaller canvases).</summary>
        public static readonly Vector2 Size = new Vector2(1640f, 980f);
        static readonly Vector2 Card = new Vector2(780f, 100f);
        const float GapX = 20f, GapY = 12f, Ring = 6f, GridTop = 340f;

        static readonly Color Gold = new Color(1f, 0.84f, 0.3f);
        static readonly Color DimGold = new Color(1f, 0.84f, 0.3f, 0.45f);
        static readonly Color HintGold = new Color(1f, 0.84f, 0.3f, 0.7f);
        /// <summary>Anything that hurts the player (CLAUDE.md §5): the edicts that are on.</summary>
        public static readonly Color Hurt = new Color(1f, 0.33f, 0.2f);
        static readonly Color Soft = new Color(1f, 1f, 1f, 0.78f);
        static readonly Color Dim = new Color(1f, 1f, 1f, 0.42f);
        static readonly Color Pale = new Color(1f, 1f, 1f, 0.55f);
        static readonly Color BoxColor = new Color(0.05f, 0.06f, 0.09f, 0.98f);
        static readonly Color Border = new Color(0.3f, 0.3f, 0.34f, 0.9f);
        static readonly Color BorderDark = new Color(0.16f, 0.16f, 0.19f, 0.9f);
        static readonly Color CardColor = new Color(0.08f, 0.1f, 0.14f, 1f);
        static readonly Color CardOn = new Color(0.2f, 0.08f, 0.06f, 1f);
        static readonly Color CardDark = new Color(0.035f, 0.04f, 0.055f, 1f);
        static readonly Color PlateColor = new Color(0.02f, 0.02f, 0.03f, 1f);
        static readonly Color Silhouette = new Color(0f, 0f, 0f, 0.9f);
        const string HurtHex = "#FF5433", GoldHex = "#FFD84A";

        const string KeysLine = "Arrows: move  ·  Enter / A: toggle or buy  ·  X / Backspace: clear  ·  Esc / B: done";
        const string KeysReadOnly = "Esc / B: back";

        sealed class CardView
        {
            public RunModifierDef Def;
            public CanvasGroup Group;
            public Image Ring, Border, Inner, Chip, Padlock;
            public RawImage Icon;
            public TextMeshProUGUI Glyph, Name, Line, Renown, State;
            public Button Buy;
            public TextMeshProUGUI BuyLabel;
        }

        readonly TMP_FontAsset _font;
        readonly Material _fontMat;
        MetaConfig _meta;
        SaveData _save;
        Action _onClose;
        GameObject _root;
        RectTransform _box;
        TextMeshProUGUI _header, _sub, _renown, _footer, _keys;
        Button _clear, _done;
        RunModifierDef[] _defs = Array.Empty<RunModifierDef>();
        CardView[] _cards = Array.Empty<CardView>();
        bool[] _on = Array.Empty<bool>();
        int _focus, _hover = -1;

        public EdictsPanel(TMP_FontAsset font, Material fontMat)
        {
            _font = font;
            _fontMat = fontMat;
        }

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
        public bool IsLocked(int i) => i < 0 || i >= _defs.Length || !MetaProgress.IsOpen(_defs[i], _meta, _save); // R7-STUB(b)
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
        public float RenownMul => 1f + RunModifiers.Bonus(Chosen); // R7-STUB(a)

        // ---- tests: what the panel shows
        public string HeaderText => _header.text;
        public string FooterText => _footer.text;
        public string KeysText => _keys.text;
        public string RenownText => _renown.text;
        public string CardLine(int i) => i >= 0 && i < _cards.Length ? _cards[i].Line.text : "";
        public string CardState(int i) => i >= 0 && i < _cards.Length ? (_cards[i].Buy.gameObject.activeSelf ? _cards[i].BuyLabel.text : _cards[i].State.text) : "";
        public RectTransform CardRect(int i) => _cards[i].Border.rectTransform;
        public Color CardFrame(int i) => _cards[i].Border.color;
        public TextMeshProUGUI CardName(int i) => _cards[i].Name;

        /// <summary>"×1.65" (two decimals at most, invariant culture).</summary>
        public static string MulText(float mul) => "×" + mul.ToString("0.##", CultureInfo.InvariantCulture);

        /// <summary>The arena screen's button: "EDICTS" with none on, else "EDICTS · RENOWN ×1.65".</summary>
        public static string ButtonText(int count, float mul) => count <= 0 ? "EDICTS" : "EDICTS · RENOWN " + MulText(mul);

        /// <summary>"3 EDICTS · RENOWN ×1.65", "1 EDICT · …", or "NO EDICTS · RENOWN ×1" (plain text).</summary>
        public static string Total(int count, float mul) =>
            (count <= 0 ? "NO EDICTS" : count + (count == 1 ? " EDICT" : " EDICTS")) + " · RENOWN " + MulText(count <= 0 ? 1f : mul);

        /// <summary>"+25 % RENOWN": an edict's share.</summary>
        public static string Share(RunModifierDef d) => "+" + Mathf.RoundToInt(Mathf.Max(0f, d.renownBonus) * 100f) + " % RENOWN";

        /// <summary>Builds the panel (hidden) under `root`, which should cover the screen.</summary>
        public void Build(Transform root, MetaConfig meta, SaveData save, Action onClose)
        {
            _meta = meta;
            _save = save ?? MetaProgress.Data;
            _onClose = onClose;
            var defs = new List<RunModifierDef>();
            if (meta != null && meta.edicts != null) // R7-STUB(b)
                foreach (var e in meta.edicts)
                    if (e != null && !defs.Contains(e)) defs.Add(e);
            _defs = defs.ToArray();
            _on = new bool[_defs.Length];

            var go = new GameObject("Edicts", typeof(RectTransform));
            go.transform.SetParent(root, false);
            _root = go;
            UiFactory.Stretch((RectTransform)go.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var mid = new Vector2(0.5f, 0.5f);
            var dim = UiFactory.Image("Dim", go.transform, new Color(0.02f, 0.02f, 0.04f, 0.86f), mid, mid, Vector2.zero, Vector2.zero, PixelIcons.White);
            UiFactory.Stretch(dim.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            dim.raycastTarget = true; // the arena screen underneath takes no clicks

            _box = UiFactory.Rect("Box", go.transform, mid, mid, Vector2.zero, Size);
            UiFactory.Image("Frame", _box, Border, mid, mid, Vector2.zero, Size, PixelIcons.White);
            UiFactory.Image("Back", _box, BoxColor, mid, mid, Vector2.zero, Size - new Vector2(6f, 6f), PixelIcons.White).raycastTarget = true;

            float left = -Size.x * 0.5f + 30f, right = Size.x * 0.5f - 30f;
            _header = Text(_box, "", 54, Gold, new Vector2(left + 500f, 436f), new Vector2(1000f, 70f), TextAlignmentOptions.Left, FontStyles.Bold);
            _sub = Text(_box, "", 22, Soft, new Vector2(left + 500f, 388f), new Vector2(1000f, 32f), TextAlignmentOptions.Left, FontStyles.Italic);
            _renown = Text(_box, "", 34, Gold, new Vector2(right - 280f, 436f), new Vector2(560f, 60f), TextAlignmentOptions.Right, FontStyles.Bold);

            _cards = new CardView[_defs.Length];
            for (int i = 0; i < _defs.Length; i++) _cards[i] = BuildCard(i);
            if (_defs.Length == 0)
                Text(_box, "No edicts yet.", 28, Dim, new Vector2(0f, 0f), new Vector2(800f, 60f), TextAlignmentOptions.Center, FontStyles.Normal);

            _footer = Text(_box, "", 36, Color.white, new Vector2(0f, -366f), new Vector2(1000f, 50f), TextAlignmentOptions.Center, FontStyles.Bold);
            _keys = Text(_box, "", 22, Dim, new Vector2(0f, -440f), new Vector2(1060f, 30f), TextAlignmentOptions.Center, FontStyles.Normal);
            _clear = UiFactory.Button("Clear", _box, "CLEAR", mid, mid, new Vector2(left + 110f, -432f), new Vector2(220f, 64f), _font, _fontMat, () => ClearAll(), 30f);
            _done = UiFactory.Button("Done", _box, "DONE", mid, mid, new Vector2(right - 110f, -432f), new Vector2(220f, 64f), _font, _fontMat, () => Close(), 30f);
            go.SetActive(false);
        }

        CardView BuildCard(int i)
        {
            var d = _defs[i];
            var v = new CardView { Def = d };
            var mid = new Vector2(0.5f, 0.5f);
            int col = i % Columns, row = i / Columns;
            var pos = new Vector2((col - (Columns - 1) * 0.5f) * (Card.x + GapX), GridTop - Card.y * 0.5f - row * (Card.y + GapY));
            var holder = UiFactory.Rect("Edict_" + d.id, _box, mid, mid, pos, Card + new Vector2(Ring * 2f, Ring * 2f));
            v.Group = holder.gameObject.AddComponent<CanvasGroup>();
            v.Ring = UiFactory.Image("Ring", holder, Color.clear, mid, mid, Vector2.zero, Card + new Vector2(Ring * 2f, Ring * 2f), PixelIcons.White);
            v.Border = UiFactory.Image("Border", holder, Border, mid, mid, Vector2.zero, Card, PixelIcons.White);
            v.Border.raycastTarget = true;
            var button = v.Border.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            int index = i;
            button.onClick.AddListener(() => Click(index));
            var hover = v.Border.gameObject.AddComponent<UiHover>();
            hover.Target = this;
            hover.Id = i;
            v.Inner = UiFactory.Image("Inner", v.Border.transform, CardColor, mid, mid, Vector2.zero, Card - new Vector2(6f, 6f), PixelIcons.White);
            var c = v.Inner.transform;
            float half = Card.x * 0.5f;

            // the icon: a 20 px atlas cell at 3× (whole art pixels); before the atlas has its row, the initial
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
            v.Glyph = Text(c, Initial(d), 36, Hurt, iconAt, new Vector2(60f, 60f), TextAlignmentOptions.Center, FontStyles.Bold);
            v.Glyph.enabled = !hasIcon;

            // the words: name and effect line in the middle, the Renown share and the state on the right
            float x0 = -half + 96f, x1 = half - 206f, cx = (x0 + x1) * 0.5f, w = x1 - x0;
            v.Name = Text(c, d.displayName, 26, Color.white, new Vector2(cx, 25f), new Vector2(w, 34f), TextAlignmentOptions.Left, FontStyles.Bold);
            v.Name.textWrappingMode = TextWrappingModes.NoWrap;
            AutoSize(v.Name, 18f, 26f);
            v.Line = Text(c, "", 19, Soft, new Vector2(cx, -15f), new Vector2(w, 50f), TextAlignmentOptions.TopLeft, FontStyles.Normal);
            AutoSize(v.Line, 13f, 19f);
            float rx = half - 104f, rw = 184f;
            v.Renown = Text(c, Share(d), 20, Gold, new Vector2(rx, 25f), new Vector2(rw, 28f), TextAlignmentOptions.Right, FontStyles.Bold);
            v.Renown.textWrappingMode = TextWrappingModes.NoWrap;
            AutoSize(v.Renown, 14f, 20f);
            v.State = Text(c, "", 20, Dim, new Vector2(rx, -18f), new Vector2(rw, 34f), TextAlignmentOptions.Right, FontStyles.Bold);
            v.State.textWrappingMode = TextWrappingModes.NoWrap;
            AutoSize(v.State, 13f, 20f);
            v.Padlock = UiFactory.Image("Padlock", c, Color.white, mid, mid, new Vector2(half - 184f, -18f), new Vector2(18f, 24f), PixelIcons.Lock);
            v.Chip = UiFactory.Image("On", c, Hurt, mid, mid, new Vector2(half - 54f, -18f), new Vector2(84f, 34f), PixelIcons.White);
            Text(v.Chip.transform, "ON", 24, Color.white, Vector2.zero, new Vector2(84f, 34f), TextAlignmentOptions.Center, FontStyles.Bold);
            v.Buy = UiFactory.Button("Buy", c, "BUY", mid, mid, new Vector2(rx, -18f), new Vector2(rw, 40f), _font, _fontMat, () => Buy(index), 18f);
            v.BuyLabel = UiFactory.Label(v.Buy);
            v.BuyLabel.overflowMode = TextOverflowModes.Truncate;
            AutoSize(v.BuyLabel, 13f, 18f);
            return v;
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
            var on = forced ?? (_meta != null ? MetaProgress.ChosenEdicts(_meta, _save) : Array.Empty<RunModifierDef>()); // R7-STUB(b)
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
            if (!ReadOnly) MetaProgress.SetChosenEdicts(Chosen); // R7-STUB(b)
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
            var v = _cards[i];
            var d = v.Def;
            bool on = _on[i], locked = IsLocked(i);
            var u = d.unlock != null ? d.unlock : _meta != null ? _meta.edictsUnlock : null; // R7-STUB(b)
            bool market = locked && d.unlock != null && d.unlock.category == UnlockCategory.Market;
            bool dark = locked && !market && !(ReadOnly && on); // the daily may proclaim an edict you haven't earned: it shows lit
            v.Ring.color = i == _focus ? Gold : i == _hover ? Pale : Color.clear;
            v.Border.color = on ? Hurt : dark ? BorderDark : Border;
            v.Inner.rectTransform.sizeDelta = Card - (on ? new Vector2(10f, 10f) : new Vector2(6f, 6f));
            v.Inner.color = on ? CardOn : dark ? CardDark : CardColor;
            v.Icon.color = dark ? Silhouette : Color.white;
            v.Glyph.color = dark ? Dim : Hurt;
            v.Name.color = dark ? Dim : Color.white;
            v.Line.text = dark ? (u != null && !string.IsNullOrEmpty(u.hint) ? u.hint : "???") : !string.IsNullOrEmpty(d.effectLine) ? d.effectLine : d.description ?? "";
            v.Line.fontStyle = dark ? FontStyles.Italic : FontStyles.Normal;
            v.Line.color = dark ? HintGold : Soft;
            v.Renown.color = dark ? DimGold : Gold;
            v.Chip.gameObject.SetActive(on);
            bool buy = market && !ReadOnly && !on;
            v.Buy.gameObject.SetActive(buy);
            if (buy)
            {
                int cost = Mathf.Max(0, d.unlock.renownCost);
                bool afford = _save.renown >= cost;
                v.BuyLabel.text = afford ? "BUY · " + cost + " RENOWN" : cost + " RENOWN";
                v.BuyLabel.color = afford ? Color.white : Dim;
                v.Buy.interactable = afford;
            }
            v.Padlock.enabled = dark;
            v.State.text = on || buy ? "" : dark ? Progress(u) : market ? "LOCKED" : "OFF";
            v.State.color = dark ? HintGold : Dim;
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

        TextMeshProUGUI Text(Transform parent, string text, float size, Color color, Vector2 pos, Vector2 box, TextAlignmentOptions align, FontStyles style)
        {
            var mid = new Vector2(0.5f, 0.5f);
            var t = UiFactory.Text("Line", parent, text, size, color, align, mid, mid, pos, box, _font, _fontMat);
            t.fontStyle = style;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Truncate; // the font has no U+2026: Ellipsis warns (docs/tracks/README.md)
            return t;
        }
    }
}
