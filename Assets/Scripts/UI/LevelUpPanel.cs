using Gladiator.Core;
using Gladiator.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Gladiator.UI
{
    /// <summary>
    /// Level-up cards (M2.4): rarity-coloured cards with the real numbers, level pips, NEW/LV tag; Reroll, Banish and
    /// Skip. Mouse (hover + click), keyboard (1-4, arrows/A-D + Enter, R/B/X, Esc cancels banish) or gamepad (d-pad /
    /// stick, South pick, West reroll, North banish, East cancel, Select skip). Input is ignored for a short grace
    /// after opening so a held jump/click can't pick blind.
    /// GC: built once; shown/hidden with a CanvasGroup (alpha + raycasts). Not SetActive, and not Canvas.enabled: a
    /// disabled canvas drops its GraphicRegistry set and re-creates it on enable (~8 KB per open, measured). No
    /// Selectable colour tweens; texts are pre-sized and written from char buffers or non-allocating SetText.
    /// </summary>
    public sealed class LevelUpPanel : MonoBehaviour, ITickable, IUiHitTarget
    {
        public TMP_FontAsset font;
        public Material fontMaterial;

        const int MaxCards = 4, MaxPips = 8;
        const int IdReroll = 10, IdBanish = 11, IdSkip = 12;
        const float CardW = 340f, CardH = 480f, Spacing = 380f;

        sealed class CardView
        {
            public RectTransform Root;
            public Image Glow, Border, Inner, Band;
            public TextMeshProUGUI Rarity, Kind, Title, Desc, Key;
            public Image[] Pips;
            public readonly CharBuffer Text = new CharBuffer(160);
        }

        static readonly Color Dim = new Color(0.02f, 0.03f, 0.06f, 0.62f);
        static readonly Color InnerColor = new Color(0.12f, 0.14f, 0.18f, 0.98f);
        static readonly Color BanishRed = new Color(1f, 0.3f, 0.24f);
        static readonly Color AltarRed = new Color(0.9f, 0.16f, 0.36f);
        static readonly Color PipEmpty = new Color(0.26f, 0.29f, 0.34f);
        static readonly Color PipOwned = new Color(0.9f, 0.92f, 0.95f);
        static readonly Color NewGold = new Color(1f, 0.86f, 0.3f);
        static readonly Color ActionColor = new Color(0.16f, 0.2f, 0.25f, 0.98f);
        static readonly Color ActionHover = new Color(0.27f, 0.33f, 0.4f, 1f);

        Canvas _canvas;
        CanvasGroup _group;
        TextMeshProUGUI _title, _sub;
        readonly CardView[] _cards = new CardView[MaxCards];
        Image _reroll, _banish, _skip;
        TextMeshProUGUI _rerollText, _banishText;
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

        void Awake()
        {
            _canvas = UiFactory.Canvas("LevelUpCanvas", transform, UiSortOrder.LevelUp);
            var root = _canvas.transform;
            var dim = UiFactory.Image("Dim", root, Dim, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, PixelIcons.White);
            UiFactory.Stretch(dim.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            dim.raycastTarget = true; // eat clicks meant for the world
            var mid = new Vector2(0.5f, 0.5f);
            _title = UiFactory.Text("Title", root, "LEVEL UP!", 92, NewGold, TextAlignmentOptions.Center, mid, mid, new Vector2(0, 385), new Vector2(1200, 120), font, fontMaterial);
            _sub = UiFactory.Text("Sub", root, "choose one", 34, new Color(1f, 1f, 1f, 0.8f), TextAlignmentOptions.Center, mid, mid, new Vector2(0, 318), new Vector2(1400, 50), font, fontMaterial);
            _sub.fontStyle = FontStyles.Normal;

            for (int i = 0; i < MaxCards; i++) _cards[i] = BuildCard(root, i);

            _reroll = BuildAction(root, IdReroll, new Vector2(-340, -340), out _rerollText);
            _banish = BuildAction(root, IdBanish, new Vector2(0, -340), out _banishText);
            _skip = BuildAction(root, IdSkip, new Vector2(340, -340), out var skipText);
            skipText.text = "[X] Skip";

            Presize();
            _group = _canvas.gameObject.AddComponent<CanvasGroup>();
            SetVisible(false);
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
            var v = new CardView();
            v.Root = UiFactory.Rect("Card" + index, parent, mid, mid, Vector2.zero, new Vector2(CardW, CardH));
            v.Glow = UiFactory.Image("Glow", v.Root, Color.clear, mid, mid, Vector2.zero, new Vector2(CardW + 22, CardH + 22), PixelIcons.White);
            v.Border = UiFactory.Image("Border", v.Root, Color.white, mid, mid, Vector2.zero, new Vector2(CardW, CardH), PixelIcons.White);
            v.Border.raycastTarget = true;
            var hit = v.Border.gameObject.AddComponent<UiHit>();
            hit.Target = this; hit.Id = index;
            v.Inner = UiFactory.Image("Inner", v.Root, InnerColor, mid, mid, Vector2.zero, new Vector2(CardW - 12, CardH - 12), PixelIcons.White);
            v.Band = UiFactory.Image("Band", v.Root, Color.white, mid, mid, new Vector2(0, CardH * 0.5f - 30), new Vector2(CardW - 12, 48), PixelIcons.White);
            v.Rarity = UiFactory.Text("Rarity", v.Root, "COMMON", 26, new Color(0.06f, 0.07f, 0.09f), TextAlignmentOptions.Center, mid, mid, new Vector2(0, CardH * 0.5f - 30), new Vector2(CardW - 20, 44), font, null);
            v.Kind = UiFactory.Text("Kind", v.Root, "NEW!", 26, NewGold, TextAlignmentOptions.Center, mid, mid, new Vector2(0, 168), new Vector2(CardW - 20, 40), font, fontMaterial);
            v.Title = UiFactory.Text("Title", v.Root, "Title", 38, Color.white, TextAlignmentOptions.Center, mid, mid, new Vector2(0, 110), new Vector2(CardW - 30, 84), font, fontMaterial);
            v.Title.textWrappingMode = TextWrappingModes.Normal;
            v.Pips = new Image[MaxPips];
            for (int p = 0; p < MaxPips; p++)
                v.Pips[p] = UiFactory.Image("Pip" + p, v.Root, PipEmpty, mid, mid, new Vector2(0, 50), new Vector2(18, 18), PixelIcons.White);
            v.Desc = UiFactory.Text("Desc", v.Root, "Desc", 30, new Color(0.85f, 0.95f, 1f), TextAlignmentOptions.Top, mid, mid, new Vector2(0, -50), new Vector2(CardW - 36, 150), font, fontMaterial);
            v.Desc.textWrappingMode = TextWrappingModes.Normal;
            v.Desc.fontStyle = FontStyles.Normal;
            v.Key = UiFactory.Text("Key", v.Root, "[" + (index + 1) + "]", 28, new Color(1f, 1f, 1f, 0.55f), TextAlignmentOptions.Center, mid, mid, new Vector2(0, -CardH * 0.5f + 32), new Vector2(CardW - 20, 44), font, fontMaterial);
            return v;
        }

        Image BuildAction(Transform parent, int id, Vector2 pos, out TextMeshProUGUI label)
        {
            var mid = new Vector2(0.5f, 0.5f);
            var img = UiFactory.Image("Action" + id, parent, ActionColor, mid, mid, pos, new Vector2(300, 64), PixelIcons.White);
            img.raycastTarget = true;
            var hit = img.gameObject.AddComponent<UiHit>();
            hit.Target = this; hit.Id = id;
            label = UiFactory.Text("Label", img.transform, "", 28, Color.white, TextAlignmentOptions.Center, mid, mid, Vector2.zero, new Vector2(290, 60), font, fontMaterial);
            label.fontStyle = FontStyles.Normal;
            return img;
        }

        /// <summary>
        /// Grow every TMP buffer once with longer text than will ever show, so opening never allocates. The text holds
        /// every character the cards use, so the dynamic font atlas has all its glyphs before the first level-up.
        /// </summary>
        void Presize()
        {
            const string Long = "WWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWW ABCDEFGHIJKLMNOPQRSTUVWXYZ abcdefghijklmnopqrstuvwxyz 0123456789 " +
                                "+-%×°·—–…!?.,:;'()[]/&#\"<>=_ ’ WWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWW";
            foreach (var c in _cards)
            {
                foreach (var t in new[] { c.Rarity, c.Kind, c.Title, c.Desc })
                {
                    t.SetText(Long);
                    t.ForceMeshUpdate(true, true);
                }
            }
            foreach (var t in new[] { _title, _sub, _rerollText, _banishText })
            {
                t.SetText(Long);
                t.ForceMeshUpdate(true, true);
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
            float x0 = -(_shownCount - 1) * Spacing * 0.5f;
            for (int i = 0; i < MaxCards; i++)
            {
                var v = _cards[i];
                bool on = i < _shownCount;
                // unused cards park off-screen instead of being deactivated
                v.Root.anchoredPosition = on ? new Vector2(x0 + i * Spacing, 10f) : new Vector2(0f, -5000f);
                if (!on) continue;
                var o = c.Offers[i];
                var col = cfg.RarityColor(o.Rarity);
                v.Band.color = col;
                v.Rarity.text = CardText.RarityName(o.Rarity);
                v.Title.text = o.Def.displayName;
                if (o.Def.kind == UpgradeKind.Consumable) { v.Kind.color = PipOwned; v.Kind.text = "SUPPLIES"; }
                else if (o.IsNew) { v.Kind.color = NewGold; v.Kind.text = o.Def.kind == UpgradeKind.Weapon ? "NEW WEAPON!" : "NEW!"; }
                else { v.Kind.color = PipOwned; v.Kind.SetText("LV {0}", o.Level); }
                CardText.Describe(o, cfg, v.Text);
                v.Desc.SetCharArray(v.Text.Chars, 0, v.Text.Length);
                int pips = o.Def.kind == UpgradeKind.Consumable ? 0 : Mathf.Min(MaxPips, o.Def.maxLevel);
                float px0 = -(pips - 1) * 13f;
                for (int p = 0; p < MaxPips; p++)
                {
                    var pip = v.Pips[p];
                    if (p >= pips) { pip.color = Color.clear; continue; }
                    pip.rectTransform.anchoredPosition = new Vector2(px0 + p * 26f, 50f);
                    pip.color = p < o.Level - 1 ? PipOwned : p == o.Level - 1 ? col : PipEmpty;
                }
            }
            _drawnBanishMode = !_banishMode; // force the dynamic part to refresh
        }

        void RefreshDynamic(LevelUpController c)
        {
            var cfg = c.Config;
            for (int i = 0; i < _shownCount; i++)
            {
                var v = _cards[i];
                bool sel = i == _selected;
                var col = _banishMode ? BanishRed : cfg.RarityColor(c.Offers[i].Rarity);
                v.Border.color = sel ? Color.Lerp(col, Color.white, 0.35f) : col * 0.85f;
                var glow = col; glow.a = sel ? 0.45f : 0f;
                v.Glow.color = glow;
                float s = sel ? 1.06f : 1f;
                if (!Mathf.Approximately(v.Root.localScale.x, s)) v.Root.localScale = new Vector3(s, s, 1f);
            }
            if (_banishMode != _drawnBanishMode || c.Source != _drawnSource)
            {
                _drawnBanishMode = _banishMode;
                _drawnSource = c.Source;
                if (_banishMode)
                {
                    _title.text = "BANISH";
                    _title.color = BanishRed;
                    _sub.text = "pick a card to remove it for the rest of the run";
                }
                else if (c.Source == PickSource.Chest)
                {
                    _title.text = "ARMORY CHEST";
                    _title.color = NewGold;
                    _sub.text = "choose one: uncommon or better";
                }
                else if (c.Source == PickSource.Boss)
                {
                    _title.text = "THE PIT MASTER'S HOARD";
                    _title.color = NewGold;
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
                    _title.color = NewGold;
                    _sub.text = "choose one";
                }
            }
            int rr = c.RerollsLeft, bn = c.BanishesLeft;
            if (rr != _drawnRerolls) { _drawnRerolls = rr; _rerollText.SetText("[R] Reroll ({0})", rr); }
            if (bn != _drawnBanishes) { _drawnBanishes = bn; _banishText.SetText("[B] Banish ({0})", bn); }
            _reroll.color = Action(IdReroll, rr > 0);
            _banish.color = _banishMode ? BanishRed * 0.7f : Action(IdBanish, bn > 0);
            _skip.color = Action(IdSkip, true);
        }

        Color Action(int id, bool enabled)
        {
            var col = _hoverAction == id && enabled ? ActionHover : ActionColor;
            if (!enabled) col.a = 0.35f;
            return col;
        }
    }
}
