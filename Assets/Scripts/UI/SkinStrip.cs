using System;
using System.Collections.Generic;
using Gladiator.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gladiator.UI
{
    /// <summary>
    /// R6 Skins (docs/tracks/R7-design.md §4.1): the strip under the select screen's diorama. It reads
    /// "◀ ■■■ FREEDMAN · 2 / 3 ▶" (the arrows are buttons, the squares the skin's three chips) over a status line:
    /// "EQUIPPED" for an owned skin, "BUY · 250 RENOWN" (a button; F / Y) for a Market one, the goal and its progress
    /// for a champion one ("Win 3 runs as the Nobody · 1 / 3"). Slot 0 is the gladiator's own colours ("CLASSIC"), then
    /// his skins in MetaConfig.skins order. Cycling previews each on the diorama (onPreview) and equips it at once when
    /// it's owned (the classic look by Unequip), so FIGHT! always fights in an owned skin; buying a Market skin equips
    /// it. Hidden when the gladiator has no skins. Menu-only UI: it allocates when it's shown, never per frame.
    /// </summary>
    public sealed class SkinStrip
    {
        /// <summary>The strip's box in canvas units (its pivot is its bottom centre).</summary>
        public static readonly Vector2 Size = new Vector2(680f, 104f);
        /// <summary>The name slot 0 shows: the gladiator's own colours.</summary>
        public const string ClassicName = "CLASSIC";

        const float ArrowX = 302f, ContentW = 536f, ChipEdge = 18f, ChipGap = 4f, ChipsToName = 12f, NameY = 22f, StatusY = -26f;

        static readonly Color PlateColor = new Color(0.04f, 0.05f, 0.07f, 0.78f);
        static readonly Color Gold = new Color(1f, 0.84f, 0.3f);
        static readonly Color Soft = new Color(1f, 1f, 1f, 0.78f);
        static readonly Color Dim = new Color(1f, 1f, 1f, 0.5f);
        static readonly Color ChipRim = new Color(0.89f, 0.84f, 0.74f, 0.85f); // bone: a black chip still reads on the plate

        readonly TMP_FontAsset _font;
        readonly Material _fontMat;
        readonly List<SkinDef> _skins = new List<SkinDef>();
        GladiatorDef _g;
        MetaConfig _meta;
        SaveData _save;
        Action<SkinDef> _onPreview;
        RectTransform _root;
        Button _prev, _next, _buy;
        TextMeshProUGUI _name, _status, _buyLabel, _buyKey;
        RectTransform _chipRow;
        Image[] _chips = Array.Empty<Image>();
        int _index;
        string _namePlain = "", _statusPlain = "";

        public SkinStrip(TMP_FontAsset font, Material fontMat)
        {
            _font = font;
            _fontMat = fontMat;
        }

        /// <summary>The equipped skin changed (true: it was bought): the menu redraws the roster tile, the Renown, the Codex.</summary>
        public Action<bool> OnChanged;

        public RectTransform Root => _root;
        /// <summary>The gladiator whose skins the strip shows.</summary>
        public GladiatorDef Gladiator => _g;
        /// <summary>The classic look plus the gladiator's skins (0 before Show).</summary>
        public int Count => _g != null ? _skins.Count + 1 : 0;
        /// <summary>0 = the classic look, i = his i-th skin.</summary>
        public int Index => _index;
        /// <summary>The skin on show (null = the classic look).</summary>
        public SkinDef Current => _index > 0 && _index <= _skins.Count ? _skins[_index - 1] : null;
        /// <summary>The classic look is always owned; a skin when it's free or its unlock is in the save.</summary>
        public bool CurrentOwned => Current == null || MetaProgress.IsOwned(Current, _save);
        /// <summary>The strip is up (the gladiator has skins).</summary>
        public bool Shown => _root != null && _root.gameObject.activeSelf;
        public SkinDef Skin(int i) => i > 0 && i <= _skins.Count ? _skins[i - 1] : null;

        // ---- tests ----------------------------------------------------------------------------------------------
        /// <summary>The name line without colour tags ("FREEDMAN · 2 / 3").</summary>
        public string NameText => _namePlain;
        /// <summary>The status line without colour tags: "EQUIPPED", the BUY button's label, or the champion goal.</summary>
        public string StatusText => _statusPlain;
        public bool BuyShown => _buy != null && _buy.gameObject.activeSelf;
        public bool BuyEnabled => BuyShown && _buy.interactable;
        public RectTransform PrevRect => (RectTransform)_prev.transform;
        public RectTransform NextRect => (RectTransform)_next.transform;
        public RectTransform BuyRect => (RectTransform)_buy.transform;
        public int ChipCount { get { int n = 0; foreach (var c in _chips) if (c.transform.parent.gameObject.activeSelf) n++; return n; } }
        public Color Chip(int i) => _chips[i].color;

        /// <summary>Builds the strip once (hidden until Show). Its bottom centre sits at `pos` from `anchor`.</summary>
        public void Build(Transform root, Vector2 anchor, Vector2 pos, Action<SkinDef> onPreview)
        {
            _onPreview = onPreview;
            var mid = new Vector2(0.5f, 0.5f);
            var plate = UiFactory.Image("SkinStrip", root, PlateColor, anchor, new Vector2(0.5f, 0f), pos, Size, PixelIcons.White);
            _root = plate.rectTransform;
            var t = _root.transform;

            _prev = Arrow("SkinPrev", t, -ArrowX, 90f, () => Cycle(-1));
            _next = Arrow("SkinNext", t, ArrowX, -90f, () => Cycle(1));

            _chipRow = UiFactory.Rect("Chips", t, mid, new Vector2(0f, 0.5f), new Vector2(0f, NameY), new Vector2(3f * ChipEdge + 2f * ChipGap, ChipEdge + 4f));
            _chips = new Image[3];
            for (int i = 0; i < _chips.Length; i++)
            {
                var left = new Vector2(0f, 0.5f);
                var rim = UiFactory.Image("Chip", _chipRow, ChipRim, left, mid, new Vector2(ChipEdge * 0.5f + i * (ChipEdge + ChipGap), 0f), new Vector2(ChipEdge + 4f, ChipEdge + 4f), PixelIcons.White);
                _chips[i] = UiFactory.Image("Colour", rim.transform, Color.white, mid, mid, Vector2.zero, new Vector2(ChipEdge, ChipEdge), PixelIcons.White);
            }

            _name = UiFactory.Text("Name", t, "", 32f, Color.white, TextAlignmentOptions.Center, mid, mid, new Vector2(0f, NameY), new Vector2(ContentW, 48f), _font, _fontMat);
            _name.overflowMode = TextOverflowModes.Truncate; // the font has no ellipsis glyph

            _status = UiFactory.Text("Status", t, "", 24f, Soft, TextAlignmentOptions.Center, mid, mid, new Vector2(0f, StatusY), new Vector2(ContentW, 36f), _font, _fontMat);
            _status.fontStyle = FontStyles.Normal;
            _status.enableAutoSizing = true; _status.fontSizeMin = 18f; _status.fontSizeMax = 24f;
            _status.overflowMode = TextOverflowModes.Truncate;

            _buy = UiFactory.Button("SkinBuy", t, "", mid, mid, new Vector2(-28f, StatusY), new Vector2(340f, 40f), _font, _fontMat, () => Buy(), 24f);
            _buyLabel = UiFactory.Label(_buy);
            _buyLabel.enableAutoSizing = true; _buyLabel.fontSizeMin = 16f; _buyLabel.fontSizeMax = 24f;
            _buyLabel.overflowMode = TextOverflowModes.Truncate;
            _buyKey = UiFactory.Text("BuyKey", t, "F / Y", 20f, Dim, TextAlignmentOptions.Left, mid, mid, new Vector2(196f, StatusY), new Vector2(72f, 30f), _font, _fontMat);
            _buyKey.fontStyle = FontStyles.Normal;

            _root.gameObject.SetActive(false);
        }

        Button Arrow(string name, Transform parent, float x, float angle, UnityEngine.Events.UnityAction onClick)
        {
            var mid = new Vector2(0.5f, 0.5f);
            var b = UiFactory.Button(name, parent, "", mid, mid, new Vector2(x, 0f), new Vector2(60f, 88f), _font, _fontMat, onClick, 24f);
            var glyph = UiFactory.Image("Chevron", b.transform, Color.white, mid, mid, Vector2.zero, new Vector2(30f, 24f), PixelIcons.Chevron);
            glyph.rectTransform.localEulerAngles = new Vector3(0f, 0f, angle); // the chevron points up: turn it left / right
            return b;
        }

        /// <summary>The strip for `g`, opened on his equipped skin (the classic look when none). Hidden when he has none.</summary>
        public void Show(GladiatorDef g, MetaConfig meta, SaveData save)
        {
            _g = g;
            _meta = meta;
            _save = save ?? MetaProgress.Data;
            _skins.Clear();
            if (g != null && meta != null)
            {
                var all = new List<SkinDef>();
                MetaProgress.SkinsOf(g, meta, all);
                foreach (var s in all) if (s != null && !_skins.Contains(s)) _skins.Add(s);
            }
            var equipped = g != null && meta != null ? MetaProgress.EquippedSkin(g, meta, _save) : null;
            _index = equipped != null ? _skins.IndexOf(equipped) + 1 : 0; // IndexOf's -1 (not his) → the classic look
            Refresh();
        }

        /// <summary>The next (dir &gt; 0) or previous skin, round the ends: previewed on the diorama, equipped at once when
        /// owned. False when there's nothing to cycle.</summary>
        public bool Cycle(int dir)
        {
            if (Count <= 1 || dir == 0) return false;
            _index = (_index + (dir > 0 ? 1 : -1) + Count) % Count;
            SfxPlayer.Play2D(SfxId.Card);
            bool equipped = CurrentOwned && EquipCurrent();
            Refresh();
            _onPreview?.Invoke(Current);
            if (equipped) OnChanged?.Invoke(false);
            return true;
        }

        /// <summary>Buys the skin on show with Renown (Market skins only) and equips it. The classic look and champion
        /// skins aren't for sale; short of Renown nothing changes (TooExpensive).</summary>
        public BuyResult Buy()
        {
            var s = Current;
            BuyResult r;
            if (s == null || _g == null) r = BuyResult.NotForSale;
            else if (s.unlock == null) r = BuyResult.AlreadyOwned; // free
            else r = ReferenceEquals(_save, MetaProgress.Data) ? MetaProgress.Buy(s.unlock) : MetaProgress.Buy(s.unlock, _save);
            SfxPlayer.Play2D(r == BuyResult.Bought ? SfxId.Coin : SfxId.Blocked);
            if (r != BuyResult.Bought) return r;
            EquipCurrent();
            Refresh();
            OnChanged?.Invoke(true);
            return r;
        }

        /// <summary>Makes the owned skin on show the equipped one (saved). True when that changed anything.</summary>
        bool EquipCurrent()
        {
            var s = Current;
            if (MetaProgress.EquippedSkin(_g, _meta, _save) == s) return false;
            if (ReferenceEquals(_save, MetaProgress.Data))
            {
                if (s == null) MetaProgress.Unequip(_g);
                else if (!MetaProgress.Equip(s)) return false;
                return true;
            }
            // a save that isn't the game's (tests): the same rule, one skin per gladiator, without writing a file
            _save.equippedSkins ??= new List<string>();
            foreach (var k in _skins) _save.equippedSkins.Remove(k.id);
            if (s != null) _save.equippedSkins.Add(s.id);
            return true;
        }

        void Refresh()
        {
            if (_root == null) return;
            bool on = Count > 1;
            _root.gameObject.SetActive(on);
            if (!on) { _namePlain = _statusPlain = ""; return; }

            var s = Current;
            string name = s != null ? (s.displayName ?? s.id).ToUpperInvariant() : ClassicName;
            string count = (_index + 1) + " / " + Count;
            _namePlain = name + " · " + count;
            _name.text = name + "  <color=#FFFFFF80>· " + count + "</color>";

            // the chips and the name as one centred group
            int chips = 0;
            if (s != null && s.chips != null)
                for (int i = 0; i < _chips.Length && i < s.chips.Length; i++)
                {
                    var c = s.chips[i];
                    c.a = 1f;
                    _chips[i].color = c;
                    chips++;
                }
            for (int i = 0; i < _chips.Length; i++) _chips[i].transform.parent.gameObject.SetActive(i < chips);
            float chipsW = chips > 0 ? chips * ChipEdge + (chips - 1) * ChipGap + ChipsToName : 0f;
            float maxName = ContentW - chipsW;
            float nameW = Mathf.Min(_name.GetPreferredValues(_name.text, maxName, 48f).x + 2f, maxName);
            float left = -(chipsW + nameW) * 0.5f;
            _chipRow.anchoredPosition = new Vector2(left, NameY);
            _name.rectTransform.anchoredPosition = new Vector2(left + chipsW + nameW * 0.5f, NameY);
            _name.rectTransform.sizeDelta = new Vector2(nameW, 48f);

            // the status line: equipped, a price, or a goal
            bool market = s != null && !CurrentOwned && s.unlock != null && s.unlock.category == UnlockCategory.Market;
            _buy.gameObject.SetActive(market);
            _status.gameObject.SetActive(!market);
            if (market)
            {
                int cost = Mathf.Max(0, s.unlock.renownCost), renown = _save.renown;
                bool afford = renown >= cost;
                _statusPlain = afford ? "BUY · " + cost + " RENOWN" : cost + " RENOWN · NEED " + (cost - renown) + " MORE";
                _buyLabel.text = _statusPlain;
                _buyLabel.color = afford ? Color.white : Dim;
                _buy.interactable = afford;
                _buyKey.gameObject.SetActive(afford);
                return;
            }
            _buyKey.gameObject.SetActive(false);
            if (CurrentOwned)
            {
                _statusPlain = "EQUIPPED";
                _status.text = _statusPlain;
                _status.color = Gold;
                return;
            }
            Goal(s.unlock, out _statusPlain, out string rich);
            _status.text = rich;
            _status.color = Soft;
        }

        /// <summary>How a locked, unbuyable skin is earned: "Win 3 runs as the Nobody · 1 / 3" (a champion skin), else its
        /// unlock's hint, with its progress when it has one condition.</summary>
        void Goal(UnlockDef u, out string plain, out string rich)
        {
            plain = rich = u != null ? u.hint ?? "" : "";
            if (u == null || u.conditions == null || u.conditions.Length != 1) return;
            var c = u.conditions[0];
            MetaProgress.Progress(c, _save, out float cur, out float tgt);
            string count = Mathf.RoundToInt(cur) + " / " + Mathf.RoundToInt(tgt);
            string goal = c.kind == UnlockConditionKind.WinWithGladiator
                ? "Win " + Mathf.RoundToInt(tgt) + (Mathf.RoundToInt(tgt) == 1 ? " run as " : " runs as ") + AsName(_g != null && _g.id == c.id ? _g : null, c.id)
                : plain;
            plain = goal + " · " + count;
            rich = goal + " · <color=#FFD84A>" + count + "</color>";
        }

        /// <summary>A gladiator's name mid-sentence: "The Nobody" → "the Nobody", "Asterion, the Chained" → "Asterion".</summary>
        public static string AsName(GladiatorDef g, string fallback = "")
        {
            if (g == null) return fallback ?? "";
            string n = g.displayName ?? g.id;
            int comma = n.IndexOf(',');
            if (comma > 0) n = n.Substring(0, comma);
            return n.StartsWith("The ", StringComparison.Ordinal) ? "the " + n.Substring(4) : n;
        }
    }
}
