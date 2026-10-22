using Gladiator.Core;
using Gladiator.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gladiator.UI
{
    /// <summary>
    /// P3 (the producer's notes 15, 17 and 19): "your items", the bronze column left of the level-up's cards and the
    /// pause's commands, the stats column's mirror image (LevelUpLayout.InventoryLeft, the same width, scale and top).
    /// Four weapon slots in a row and six Boon slots in two rows of three, always all shown: an empty slot is a dim stone
    /// well, so it reads as an inventory with room; a filled one is the HUD strip's tile, bigger: a dark-bronze chip, a
    /// 2-unit rarity line, the upgrade's animated icon (UpgradeIconView) and its level badge (Number). Hovering a slot
    /// shows a UiTooltip under the column (name, "LV n · RARITY", what it does: a weapon's description and its hit and
    /// rate, a Boon's totals). While a card is selected (keys, gamepad or the mouse): the card's upgrade, if you own it,
    /// lights its slot and shows its tooltip; a new weapon or Boon shows as a ghost (translucent, its aura off) in the
    /// next free slot of its row, with the card's tooltip; an evolution lights the weapon it replaces.
    /// A plain class built and ticked by LevelUpPanel (no component on any scene). Built once; slots are redrawn when the
    /// build's version changes, the tooltip when what it shows changes: no allocation in a run.
    /// </summary>
    public sealed class InventoryPanel : IUiHoverTarget
    {
        public const int WeaponSlots = 4, BoonSlots = 6, Slots = WeaponSlots + BoonSlots;
        public const float Width = LevelUpLayout.StatsW;
        /// <summary>The two columns' shared header (StatsPanel uses them too, so the titles line up): heights from the
        /// top of the frame's ContentRect, and font sizes.</summary>
        public const float TitleH = 34f, SubH = 24f, TitleSize = 28f, SubSize = 17f, HeadH = 22f, HeadSize = 17f;
        public const float SlotSize = 76f, SlotGap = 16f, IconSize = 60f;
        const float GapAfterSub = 6f, GapAfterHead = 6f, GapBetweenSections = 14f, TipGap = 12f, HaloOut = 10f;
        const float LineInset = 4f, InnerInset = 6f, BadgeSize = 20f;
        public const string TitleWord = "YOUR ITEMS", HintWord = "hover for details", WeaponsWord = "WEAPONS", BoonsWord = "BOONS";

        static readonly Color WeaponInner = new Color(0.2f, 0.13f, 0.07f, 0.92f); // the HUD strip's warm bronze: weapons vs Boons
        static readonly Color BoonInner = new Color(0.1f, 0.12f, 0.15f, 0.92f);
        static readonly Color WellTint = new Color(1f, 1f, 1f, 0.55f);
        static readonly Color GhostTint = new Color(0.72f, 0.74f, 0.8f, 0.45f);

        sealed class SlotView
        {
            public RectTransform Root;
            public Image Well, Chip, Line, Inner;
            public UpgradeIconView Icon;
            public TextMeshProUGUI Badge;
            public UpgradeDef Def;
            public int Level;
            public Rarity Rarity;
            public float Potency;
        }

        readonly RectTransform _root;
        readonly CanvasGroup _group;
        readonly TextMeshProUGUI _title, _sub, _weaponsHead, _boonsHead, _weaponsCount, _boonsCount;
        readonly SlotView[] _slots = new SlotView[Slots];
        readonly Image _halo;
        readonly UpgradeIconView _ghost;
        readonly RawImage _ghostIcon, _ghostAura;
        readonly UiTooltip _tip;
        readonly float _height;
        readonly Color _head, _dim, _gold;

        PlayerBuild _build;
        WeaponSystem _weapons;
        Player _player;
        int _drawnVersion = int.MinValue;
        int _hover = -1, _lit = -1, _ghostAt = -1, _tipFor = int.MinValue, _tipVersion = -1;
        bool _hasOffer, _offerIsTip;
        CardOffer _offer;
        Vector2 _canvas = MenuLayout.Reference;
        float _scale = 1f;
        Vector2 _topLeft; // the column's top-left corner in the canvas's centred units

        public RectTransform Root => _root;
        /// <summary>The column's height at scale 1 (it hangs from LevelUpLayout.ColumnTop).</summary>
        public float Height => _height;
        public bool Visible => _group.alpha > 0f;
        public float Scale => _scale;

        /// <param name="parent">The owner's canvas root (the column is placed in its centred units).</param>
        public InventoryPanel(Transform parent)
        {
            var th = UiTheme.Current;
            ColorUtility.TryParseHtmlString(StatText.Head, out _head);
            ColorUtility.TryParseHtmlString(StatText.Dim, out _dim);
            _gold = th.goldBright;
            var tl = new Vector2(0f, 1f);
            var mid = new Vector2(0.5f, 0.5f);

            // the content's rows from its top (the frame's ContentRect: padding + UiKit.ContentPad)
            float yWeaponsHead = TitleH + SubH + GapAfterSub;
            float yWeapons = yWeaponsHead + HeadH + GapAfterHead;
            float yBoonsHead = yWeapons + SlotSize + GapBetweenSections;
            float yBoons = yBoonsHead + HeadH + GapAfterHead;
            float contentH = yBoons + 2f * SlotSize + SlotGap;
            var pad = UiFrames.Padding(FrameKind.Bronze);
            float p = UiKit.ContentPad;
            float left = pad.x + p, top = pad.w + p, inner = Width - left - pad.z - p;
            _height = top + contentH + pad.y + p;

            _root = UiFactory.Rect("Inventory", parent, mid, tl, Vector2.zero, new Vector2(Width, _height));
            _group = _root.gameObject.AddComponent<CanvasGroup>();
            var frame = UiKit.Frame("Frame", _root, FrameKind.Bronze, mid, mid, Vector2.zero, Vector2.zero, Color.white);
            UiFactory.Stretch(frame.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            _title = UiKit.Text("Title", _root, TitleWord, TextRole.Body, TitleSize, _head, TextAlignmentOptions.MidlineLeft, tl, tl,
                new Vector2(left, -top), new Vector2(inner, TitleH));
            _sub = UiKit.Text("Sub", _root, HintWord, TextRole.Body, SubSize, _dim, TextAlignmentOptions.MidlineLeft, tl, tl,
                new Vector2(left, -top - TitleH), new Vector2(inner, SubH));
            _weaponsHead = UiKit.Text("WeaponsHead", _root, WeaponsWord, TextRole.Body, HeadSize, _head, TextAlignmentOptions.MidlineLeft, tl, tl,
                new Vector2(left, -top - yWeaponsHead), new Vector2(inner, HeadH));
            _weaponsCount = UiKit.Text("WeaponsCount", _root, "0/4", TextRole.Body, HeadSize, _dim, TextAlignmentOptions.MidlineRight, tl, tl,
                new Vector2(left, -top - yWeaponsHead), new Vector2(inner, HeadH));
            _boonsHead = UiKit.Text("BoonsHead", _root, BoonsWord, TextRole.Body, HeadSize, _head, TextAlignmentOptions.MidlineLeft, tl, tl,
                new Vector2(left, -top - yBoonsHead), new Vector2(inner, HeadH));
            _boonsCount = UiKit.Text("BoonsCount", _root, "0/6", TextRole.Body, HeadSize, _dim, TextAlignmentOptions.MidlineRight, tl, tl,
                new Vector2(left, -top - yBoonsHead), new Vector2(inner, HeadH));

            // the lit slot's glow, behind every slot (moved to the one it lights)
            _halo = UiKit.Frame("Lit", _root, FrameKind.Halo, mid, mid, Vector2.zero, new Vector2(SlotSize + 2f * HaloOut, SlotSize + 2f * HaloOut), Color.clear);
            var hl = _halo.rectTransform;
            hl.anchorMin = hl.anchorMax = tl;

            float pitch = SlotSize + SlotGap;
            float boonsLeft = left + (inner - (3f * SlotSize + 2f * SlotGap)) * 0.5f; // two rows of three, centred
            for (int i = 0; i < Slots; i++)
            {
                Vector2 centre;
                if (i < WeaponSlots) centre = new Vector2(left + SlotSize * 0.5f + i * pitch, -(top + yWeapons + SlotSize * 0.5f));
                else
                {
                    int b = i - WeaponSlots;
                    centre = new Vector2(boonsLeft + SlotSize * 0.5f + (b % 3) * pitch, -(top + yBoons + SlotSize * 0.5f + (b / 3) * pitch));
                }
                _slots[i] = BuildSlot(i, centre);
            }

            _ghost = new UpgradeIconView(_root, "Ghost", tl, Vector2.zero, IconSize, lively: true);
            _ghost.Root.pivot = mid; // placed by its centre, on a slot's
            _ghostIcon = _ghost.Root.Find("Icon").GetComponent<RawImage>();
            _ghostAura = _ghost.Root.Find("Aura").GetComponent<RawImage>();
            _ghostIcon.color = GhostTint;

            _tip = new UiTooltip(parent, "ItemTip", Width);
            foreach (var t in new[] { _weaponsCount, _boonsCount }) { t.SetText("88/88"); t.ForceMeshUpdate(true, true); }
            _weaponsCount.SetText("0/4");
            _boonsCount.SetText("0/6");
            SetVisible(false);
        }

        SlotView BuildSlot(int i, Vector2 centre)
        {
            var tl = new Vector2(0f, 1f);
            var mid = new Vector2(0.5f, 0.5f);
            var v = new SlotView();
            var size = new Vector2(SlotSize, SlotSize);
            v.Root = UiFactory.Rect("Slot" + i, _root, tl, mid, centre, size);
            v.Well = UiKit.Frame("Well", v.Root, FrameKind.Stone, mid, mid, Vector2.zero, size, WellTint);
            v.Chip = UiKit.Frame("Chip", v.Root, FrameKind.Chip, mid, mid, Vector2.zero, size, Color.clear);
            v.Line = UiFactory.Image("Line", v.Root, Color.clear, mid, mid, Vector2.zero, size - 2f * LineInset * Vector2.one, PixelIcons.White);
            v.Inner = UiFactory.Image("Inner", v.Root, Color.clear, mid, mid, Vector2.zero, size - 2f * InnerInset * Vector2.one, PixelIcons.White);
            v.Line.raycastTarget = v.Inner.raycastTarget = false;
            v.Icon = new UpgradeIconView(v.Root, "Icon", mid, Vector2.zero, IconSize, lively: false);
            // the badge in the chip's ContentRect's bottom-right corner (the padding rule)
            var content = UiKit.ContentRect(FrameKind.Chip, size);
            v.Badge = UiKit.Text("Level", v.Root, "", TextRole.Number, BadgeSize, _gold, TextAlignmentOptions.BottomRight, mid, mid,
                content.center, content.size, TextLook.Outline);
            v.Badge.SetText("EVO!");
            v.Badge.ForceMeshUpdate(true, true);
            v.Badge.SetText("");
            // the pointer: a clear hit area over the whole slot
            var hit = UiFactory.Image("Hit", v.Root, Color.clear, mid, mid, Vector2.zero, size, PixelIcons.White);
            hit.raycastTarget = true;
            var hover = hit.gameObject.AddComponent<UiHover>();
            hover.Target = this;
            hover.Id = i;
            return v;
        }

        /// <summary>Shown with alpha; it takes the pointer only while shown.</summary>
        public void SetVisible(bool on)
        {
            _group.alpha = on ? 1f : 0f;
            _group.blocksRaycasts = on;
            _group.interactable = false;
            if (!on)
            {
                _hover = -1;
                _tip.Hide();
                _tipFor = int.MinValue;
            }
        }

        /// <summary>Layout A: left of the row the fit was made for, hanging from ColumnTop at the columns' scale.</summary>
        public void Place(Vector2 canvas, in LevelUpLayout.Fit fit)
        {
            if (canvas.x < 1f || canvas.y < 1f) canvas = MenuLayout.Reference;
            _canvas = canvas;
            _scale = fit.StatsScale;
            _topLeft = new Vector2(LevelUpLayout.InventoryLeft(canvas, fit) - canvas.x * 0.5f, LevelUpLayout.ColumnTop(canvas.y));
            _root.localScale = new Vector3(_scale, _scale, 1f);
            _root.anchoredPosition = _topLeft;
            _tipFor = int.MinValue; // re-place the tooltip
        }

        public void Hover(int id, bool on)
        {
            if (on) _hover = id;
            else if (_hover == id) _hover = -1;
        }

        bool Bind()
        {
            var p = Player.Main;
            if (p == null) return false;
            if (p != _player)
            {
                _player = p;
                _build = p.GetComponent<PlayerBuild>();
                _weapons = p.GetComponent<WeaponSystem>();
                _drawnVersion = int.MinValue;
            }
            return _build != null;
        }

        /// <summary>The level-up's selected card (hasOffer false in the pause or with no cards).</summary>
        public void Tick(float now, bool hasOffer, in CardOffer offer)
        {
            if (!Bind()) return;
            var state = _build.State;
            bool redraw = state.Version != _drawnVersion;
            if (redraw) Fill(state);
            for (int i = 0; i < Slots; i++) _slots[i].Icon.Tick(now);
            _ghost.Tick(now);

            if (redraw || hasOffer != _hasOffer || (hasOffer && (offer.Def != _offer.Def || offer.Rarity != _offer.Rarity || offer.Level != _offer.Level)))
            {
                _hasOffer = hasOffer;
                _offer = hasOffer ? offer : default;
                Focus(state);
            }
            // the tooltip: the hovered slot, else the selected card's
            int tip = _hover >= 0 ? _hover : _ghostAt >= 0 ? _ghostAt : _lit;
            if (tip >= 0 && _slots[tip].Def == null && tip != _ghostAt) tip = -1; // an empty well says nothing
            if (tip != _tipFor || _tipVersion != _drawnVersion)
            {
                _tipFor = tip;
                _tipVersion = _drawnVersion;
                WriteTip(tip, state);
            }
        }

        void Fill(BuildState state)
        {
            _drawnVersion = state.Version;
            var cfg = _build.config;
            int w = Row(state, UpgradeKind.Weapon, 0, WeaponSlots, WeaponInner, cfg);
            int b = Row(state, UpgradeKind.Boon, WeaponSlots, Slots, BoonInner, cfg);
            _weaponsCount.SetText("{0}/{1}", (float)w, (float)WeaponSlots);
            _boonsCount.SetText("{0}/{1}", (float)b, (float)BoonSlots);
        }

        int Row(BuildState state, UpgradeKind kind, int first, int end, Color inner, UpgradeConfig cfg)
        {
            int k = first;
            for (int i = 0; i < state.OwnedCount && k < end; i++)
            {
                var o = state.GetOwned(i);
                if (o.Def == null || o.Def.kind != kind) continue;
                var v = _slots[k];
                var rarity = cfg != null ? cfg.RarityColor(o.BestRarity) : Color.white;
                v.Def = o.Def;
                v.Level = o.Level;
                v.Rarity = o.BestRarity;
                v.Potency = o.Potency;
                v.Well.color = Color.clear;
                v.Chip.color = Color.white;
                v.Line.color = rarity;
                v.Inner.color = inner;
                if (!v.Icon.Set(o.Def, rarity, k * 0.137f)) v.Icon.Clear();
                if (o.Def.evolvesFrom != null) v.Badge.SetText("EVO");
                else v.Badge.SetText("{0}", o.Level);
                k++;
            }
            int filled = k - first;
            for (; k < end; k++)
            {
                var v = _slots[k];
                v.Def = null;
                v.Level = 0;
                v.Well.color = WellTint;
                v.Chip.color = Color.clear;
                v.Line.color = Color.clear;
                v.Inner.color = Color.clear;
                v.Icon.Clear();
                v.Badge.SetText("");
            }
            return filled;
        }

        int SlotOf(UpgradeDef d)
        {
            for (int i = 0; i < Slots; i++) if (_slots[i].Def == d && d != null) return i;
            return -1;
        }

        int NextFree(UpgradeKind kind)
        {
            int first = kind == UpgradeKind.Weapon ? 0 : WeaponSlots, end = kind == UpgradeKind.Weapon ? WeaponSlots : Slots;
            for (int i = first; i < end; i++) if (_slots[i].Def == null) return i;
            return -1;
        }

        /// <summary>The selected card's slot: lit if you own its upgrade (or the weapon it evolves), a ghost if it's new.</summary>
        void Focus(BuildState state)
        {
            _lit = _ghostAt = -1;
            _offerIsTip = false;
            var d = _hasOffer ? _offer.Def : null;
            if (d != null && d.kind != UpgradeKind.Consumable)
            {
                if (d.evolvesFrom != null) { _lit = SlotOf(d.evolvesFrom); _offerIsTip = _lit >= 0; }
                else
                {
                    _lit = SlotOf(d);
                    if (_lit < 0) _ghostAt = NextFree(d.kind);
                }
            }
            if (_lit >= 0)
            {
                _halo.rectTransform.anchoredPosition = _slots[_lit].Root.anchoredPosition;
                var c = _gold;
                c.a = 0.75f;
                _halo.color = c;
            }
            else _halo.color = Color.clear;
            if (_ghostAt >= 0 && _ghost.Set(d, Color.white, 0f))
            {
                _ghost.Root.anchoredPosition = _slots[_ghostAt].Root.anchoredPosition;
                _ghostIcon.color = GhostTint;
                _ghostAura.color = Color.clear; // no rarity aura: it isn't yours yet
            }
            else
            {
                if (_ghostAt >= 0) _ghostAt = -1;
                _ghost.Clear();
            }
            _tipFor = int.MinValue;
        }

        // ---------- the tooltip ----------

        void WriteTip(int slot, BuildState state)
        {
            if (slot < 0) { _tip.Hide(); return; }
            var cfg = _build.config;
            bool offer = (slot == _ghostAt || (slot == _lit && _offerIsTip)) && _hasOffer && _offer.Def != null;
            _tip.Title.Clear();
            _tip.Sub.Clear();
            var body = _tip.Body.Clear();
            if (offer)
            {
                var o = _offer;
                var d = o.Def;
                _tip.Title.Append(d.displayName);
                if (d.evolvesFrom != null) _tip.Sub.Append("EVOLUTION");
                else if (d.IsVow) _tip.Sub.Append("A VOW");
                else _tip.Sub.Append(d.kind == UpgradeKind.Weapon ? "NEW WEAPON" : "NEW");
                _tip.Sub.Append(" · ").Append(CardText.RarityName(o.Rarity));
                if (cfg != null)
                {
                    CardText.Describe(o, cfg, body);
                    CardText.Hints(o, state, _build.catalog, body);
                }
            }
            else
            {
                var v = _slots[slot];
                var d = v.Def;
                if (d == null) { _tip.Hide(); return; }
                _tip.Title.Append(d.displayName);
                if (d.evolvesFrom != null) _tip.Sub.Append("EVOLVED");
                else
                {
                    if (d.IsVow) _tip.Sub.Append("VOW ");
                    _tip.Sub.Append("LV ").Append(v.Level, 0);
                }
                _tip.Sub.Append(" · ").Append(CardText.RarityName(v.Rarity));
                if (d.kind == UpgradeKind.Weapon) WeaponBody(d, body);
                else if (cfg != null) BoonBody(v, cfg, body);
            }
            _tip.Apply();
            // under the column, its width (clamped on the screen)
            var pos = new Vector2(_topLeft.x, _topLeft.y - (_height + TipGap) * _scale);
            _tip.Place(pos, new Vector2(0f, 1f), _scale, _canvas);
        }

        void WeaponBody(UpgradeDef d, CharBuffer t)
        {
            var wd = d.weapon;
            if (wd == null) return;
            t.Append(wd.description);
            var w = _weapons != null ? _weapons.Find(wd) : null;
            if (w == null) return;
            if (t.Length > 0) t.Append('\n');
            t.Append("<color=").Append(StatText.Dim).Append(">hit ");
            float hit = _weapons.Damage(w);
            if (hit > 0f) t.Append(hit, hit >= 10f ? 0 : 1).Append(" · ");
            t.Append(1f / Mathf.Max(0.01f, _weapons.CooldownOf(w)), 2).Append("/s</color>");
        }

        /// <summary>A Boon's totals: every level's stats and effects at its potency (the sum of its takes' rarities); a
        /// Vow's text per level (its price scales by each take's rarity, which isn't kept).</summary>
        static void BoonBody(SlotView v, UpgradeConfig cfg, CharBuffer t)
        {
            var d = v.Def;
            if (d.IsVow)
            {
                CardText.Describe(new CardOffer { Def = d, Rarity = v.Rarity, Level = v.Level }, cfg, t);
                if (v.Level > 1) t.Append("\n<color=").Append(StatText.Dim).Append(">each level</color>");
                return;
            }
            float potency = v.Potency > 0f ? v.Potency : cfg.Magnitude(v.Rarity);
            for (int i = 0; d.perLevel != null && i < d.perLevel.Length; i++)
            {
                if (t.Length > 0) t.Append('\n');
                CardText.Mod(d.perLevel[i], potency, t);
            }
            for (int i = 0; d.effects != null && i < d.effects.Length; i++)
            {
                if (t.Length > 0) t.Append('\n');
                CardText.Effect(d.effects[i], potency, t);
            }
        }

        // ---------- tests ----------

        /// <summary>Tests: what slot `i` holds (weapons 0-3, Boons 4-9; null when it's an empty well) and its badge.</summary>
        public UpgradeDef SlotDef(int i) => _slots[i].Def;
        public bool SlotFilled(int i) => _slots[i].Def != null;
        public string SlotBadge(int i) => _slots[i].Badge.text;
        public bool SlotShowsIcon(int i) => _slots[i].Icon.Showing;
        public RectTransform SlotRect(int i) => _slots[i].Root;
        /// <summary>Tests: an empty well shows (the slot is drawn even when nothing is in it).</summary>
        public bool SlotShowsWell(int i) => _slots[i].Well.color.a > 0f;
        /// <summary>The slot the selected card's ghost shows in (-1: none), the lit slot (-1: none).</summary>
        public int GhostSlot => _ghostAt;
        public int LitSlot => _lit;
        public bool GhostShowing => _ghostAt >= 0 && _ghost.Showing;
        /// <summary>The ghost's icon tint (translucent: alpha under 1).</summary>
        public Color GhostColor => _ghostIcon.color;
        public UiTooltip Tooltip => _tip;
        public bool TooltipShown => _tip.Shown;
        /// <summary>The slot whose tooltip shows (-1: none).</summary>
        public int TooltipSlot => _tip.Shown ? _tipFor : -1;
        public string TooltipTitle => _tip.TitleText;
        public string TooltipText => _tip.Text;
        /// <summary>Tests: as the mouse entering (on) or leaving slot `i`.</summary>
        public void HoverSlot(int i, bool on) => Hover(i, on);
        /// <summary>Tests: the labels (role checks), the column's frame size at scale 1.</summary>
        public Component TitleLabel => _title;
        public string TitleText => _title.text;
        public string WeaponsCountText => _weaponsCount.text;
        public string BoonsCountText => _boonsCount.text;
    }
}
