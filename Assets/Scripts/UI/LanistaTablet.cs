using System;
using Gladiator.Core;
using Gladiator.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gladiator.UI
{
    /// <summary>
    /// The lanista's wax tablet (R9): the detail panel of the select screens and the Codex roster. U1: the theme's `Wax`
    /// frame (a wooden frame round dark wax) with the words scratched in light: names in Title (`waxInk`), labels in Title
    /// gold, lines in Body. Pages are built into the wax once (GladiatorTablet, ArenaTablet) and only swap their words and
    /// pictures on a pick, never per frame. Coordinates are from the wax's top-left corner, y down.
    /// </summary>
    public static class LanistaTablet
    {
        // R9's palette (palette_index.md), kept for callers; U1 draws the tablet from the theme
        public static readonly Color Wood = new Color32(0x5A, 0x3A, 0x22, 0xFF);
        public static readonly Color WoodDark = new Color32(0x3A, 0x24, 0x14, 0xFF);
        public static readonly Color Wax = new Color32(0xC9, 0x9A, 0x3B, 0xFF);
        public static readonly Color WaxDark = new Color32(0xA5, 0x7A, 0x2A, 0xFF);
        public static readonly Color Peg = new Color32(0xD9, 0xA5, 0x5E, 0xFF);
        public static readonly Color Cord = new Color32(0xC9, 0xA6, 0x6B, 0xFF);
        /// <summary>Words scratched into the wax.</summary>
        public static readonly Color Scratch = new Color32(0x2B, 0x1B, 0x0E, 0xFF);
        public static readonly Color Faint = new Color32(0x2B, 0x1B, 0x0E, 0xA8);
        /// <summary>Names and labels.</summary>
        public static readonly Color Heading = new Color32(0x6E, 0x10, 0x20, 0xFF);
        /// <summary>The wooden frame round the wax: the `Wax` frame's border (7 art px at 3 units each).</summary>
        public const float Rim = 21f;
        /// <summary>The smallest text on a tablet (spec §8.2: 12 px at 720p).</summary>
        public const float Floor = 18f;

        public static Vector2 WaxSize => MenuLayout.Tablet - new Vector2(Rim * 2f, Rim * 2f);

        // the theme's tablet colours (spec §3)
        public static Color Ink => UiTheme.Current.waxInk;
        public static Color Label => UiTheme.Current.gold;
        public static Color Hint => UiTheme.Current.dust;
        /// <summary>A sunken picture plate: the wood's colour round the darkest wax.</summary>
        public static Color PlateRim => UiTheme.Current.waxWood;
        public static Color PlateFloor => UiTheme.Current.night;

        /// <summary>Builds the frame at `pos` from `anchor` (the tablet's `pivot` sits there); returns its root and the wax.</summary>
        public static RectTransform Frame(Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, out RectTransform wax)
        {
            var size = MenuLayout.Tablet;
            var mid = new Vector2(0.5f, 0.5f);
            var frame = UiKit.Frame("Tablet", parent, FrameKind.Wax, anchor, pivot, pos, size, Color.white);
            wax = UiFactory.Rect("Wax", frame.transform, mid, mid, Vector2.zero, WaxSize);
            return frame.rectTransform;
        }

        /// <summary>The kind of frame a tablet's root shows (tests).</summary>
        public static int FrameOf(RectTransform root) => root != null ? UiFrames.KindOf(root.GetComponent<Image>()) : -1;

        static RectTransform At(RectTransform rt, float x, float y, float w, float h)
        {
            var tl = new Vector2(0f, 1f);
            rt.anchorMin = rt.anchorMax = tl;
            rt.pivot = tl;
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        /// <summary>Moves a label or group to (x, y) in the wax and gives it height `h` (the tablets' flowing rows).</summary>
        public static void Place(RectTransform rt, float x, float y, float w, float h) => At(rt, x, y, w, h);

        /// <summary>Scratched words in a theme role: wrapped, Truncate (never Ellipsis), autosized down to `minSize`.</summary>
        public static TextMeshProUGUI Text(Transform wax, string name, float x, float y, float w, float h, TextRole role, float size, Color color,
            TextAlignmentOptions align, float minSize = 0f)
        {
            var t = UiKit.Text(name, wax, "", role, size, color, align, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            At(t.rectTransform, x, y, w, h);
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Truncate;
            if (minSize > 0f)
            {
                t.enableAutoSizing = true;
                t.fontSizeMin = minSize;
                t.fontSizeMax = size;
            }
            return t;
        }

        /// <summary>R9's signature, kept: Body in the theme (the font and the style are the theme's now).</summary>
        public static TextMeshProUGUI Text(Transform wax, string name, float x, float y, float w, float h, float size, Color color,
            TextAlignmentOptions align, FontStyles style, TMP_FontAsset font, float minSize = 0f) =>
            Text(wax, name, x, y, w, h, TextRole.Body, size, color, align, minSize);

        public static Image Image(Transform wax, string name, float x, float y, float w, float h, Color color, Sprite sprite = null)
        {
            var img = UiFactory.Image(name, wax, color, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, sprite != null ? sprite : PixelIcons.White);
            At(img.rectTransform, x, y, w, h);
            return img;
        }

        public static RawImage Raw(Transform wax, string name, float x, float y, float w, float h)
        {
            var img = UiFactory.Rect(name, wax, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero).gameObject.AddComponent<RawImage>();
            At(img.rectTransform, x, y, w, h);
            img.raycastTarget = false;
            return img;
        }

        /// <summary>An empty container (a row to show or hide as one).</summary>
        public static RectTransform Group(Transform wax, string name, float x, float y, float w, float h) =>
            At(UiFactory.Rect(name, wax, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero), x, y, w, h);

        /// <summary>A sunken plate in the wax for a picture: a wooden rim, the darkest wax inside.</summary>
        public static void Plate(Transform wax, float x, float y, float w, float h)
        {
            Image(wax, "PlateRim", x - 4f, y - 4f, w + 8f, h + 8f, PlateRim);
            Image(wax, "Plate", x, y, w, h, PlateFloor);
        }

        /// <summary>A scratched rule across the wax.</summary>
        public static void Rule(Transform wax, float y)
        {
            var c = Ink;
            c.a = 0.35f;
            Image(wax, "Rule", 14f, y, WaxSize.x - 28f, 2f, c);
        }

        /// <summary>The label's height for its words at `size` and width (wrapped): the tablets stack their rows by it.
        /// Menu-only (it measures; it allocates).</summary>
        public static float Height(TextMeshProUGUI t, float size, float width)
        {
            t.enableAutoSizing = false;
            t.fontSize = size;
            if (string.IsNullOrEmpty(t.text)) return 0f;
            return Mathf.Ceil(t.GetPreferredValues(t.text, width, 0f).y) + 2f;
        }

        public static string Count(long n, string one, string many) => n + " " + (n == 1 ? one : many);

        /// <summary>A whole-number count with thousands separators past 9,999 (as the Codex shows progress).</summary>
        public static string Num(float v) => v >= 10000f ? Mathf.RoundToInt(v).ToString("N0") : Mathf.RoundToInt(v).ToString();
    }

    /// <summary>
    /// The tablet's gladiator page: the portrait (192: the 128 px bake at 1.5×, so the words below fit at the 18 floor),
    /// the name, the select line, runs and victories with them and the stats beside it; the tradeoff under it; under the
    /// rule the description, the starting weapon (icon, name, its line) and each trait (mark, name, its line), stacked
    /// by their measured heights at the largest size from 22 down to 18 that fits the wax. Locked: the silhouette, "???",
    /// LOCKED and the unlock's hint (with its progress when it's a count).
    /// </summary>
    public sealed class GladiatorTablet
    {
        const int MaxTraits = 3;
        const float Portrait = 192f, M = 14f, Indent = 58f, Gap = 8f, TradeoffY = 214f, RuleY = 274f, FlowTop = 284f;

        CodexConfig _codex;
        RawImage _portrait, _weaponIcon;
        TextMeshProUGUI _name, _select, _runs, _stats, _tradeoff, _desc, _weaponName, _weaponLine, _progress;
        RectTransform _weapon;
        readonly RectTransform[] _traits = new RectTransform[MaxTraits];
        readonly TextMeshProUGUI[] _traitNames = new TextMeshProUGUI[MaxTraits], _traitLines = new TextMeshProUGUI[MaxTraits];

        /// <summary>The font argument is ignored (U1: the theme decides).</summary>
        public GladiatorTablet(TMP_FontAsset font) { }

        public RectTransform Root { get; private set; }
        /// <summary>Tests: what the tablet shows.</summary>
        public string NameText => _name.text;
        public string BodyText => _desc.text;
        public string RunsText => _runs.text;
        public bool ShowsWeapon => _weapon.gameObject.activeSelf;
        /// <summary>Tests: the frame the tablet is drawn in (`FrameKind.Wax`).</summary>
        public int FrameKindShown => LanistaTablet.FrameOf(Root);

        public void Build(Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, CodexConfig codex)
        {
            _codex = codex;
            Root = LanistaTablet.Frame(parent, anchor, pivot, pos, out var waxRt);
            var wax = waxRt.transform;
            float w = LanistaTablet.WaxSize.x, inner = w - 2f * M;
            var ink = LanistaTablet.Ink;
            var tl = TextAlignmentOptions.TopLeft;

            LanistaTablet.Plate(wax, M, M, Portrait, Portrait);
            _portrait = LanistaTablet.Raw(wax, "Portrait", M, M, Portrait, Portrait);
            float x = M + Portrait + 16f, cw = w - x - M;
            _name = LanistaTablet.Text(wax, "Name", x, 10f, cw, 84f, TextRole.Title, 40f, ink, tl, 24f);
            _select = LanistaTablet.Text(wax, "Select", x, 98f, cw, 28f, TextRole.Body, 22f, LanistaTablet.Hint, tl, LanistaTablet.Floor);
            _runs = LanistaTablet.Text(wax, "Runs", x, 128f, cw, 28f, TextRole.Body, 22f, ink, tl, LanistaTablet.Floor);
            _stats = LanistaTablet.Text(wax, "Stats", x, 158f, cw, 28f, TextRole.Body, 22f, ink, tl, LanistaTablet.Floor);
            _tradeoff = LanistaTablet.Text(wax, "Tradeoff", M, TradeoffY, inner, 54f, TextRole.Body, 20f, LanistaTablet.Label, tl, LanistaTablet.Floor);

            LanistaTablet.Rule(wax, RuleY);
            _desc = LanistaTablet.Text(wax, "Description", M, FlowTop, inner, 100f, TextRole.Body, 22f, ink, tl, LanistaTablet.Floor);
            _progress = LanistaTablet.Text(wax, "Progress", M, FlowTop + 108f, inner, 40f, TextRole.Number, 30f, LanistaTablet.Label, tl);

            // the starting weapon: its card's icon (20 px art at 2×), name and line
            _weapon = LanistaTablet.Group(wax, "Weapon", M, FlowTop + 108f, inner, 56f);
            var wt = _weapon.transform;
            LanistaTablet.Plate(wt, 4f, 6f, 40f, 40f);
            _weaponIcon = LanistaTablet.Raw(wt, "Icon", 4f, 6f, 40f, 40f);
            _weaponName = LanistaTablet.Text(wt, "Name", Indent, 0f, inner - Indent, 26f, TextRole.Title, 20f, LanistaTablet.Label, tl);
            _weaponLine = LanistaTablet.Text(wt, "Line", Indent, 26f, inner - Indent, 30f, TextRole.Body, 18f, ink, tl);

            for (int k = 0; k < MaxTraits; k++)
            {
                var row = LanistaTablet.Group(wax, "Trait", M, FlowTop + 180f + k * 60f, inner, 56f);
                var rt = row.transform;
                LanistaTablet.Image(rt, "Mark", 8f, 2f, 32f, 32f, Color.white, PixelIcons.Sigil);
                _traitNames[k] = LanistaTablet.Text(rt, "Name", Indent, 0f, inner - Indent, 26f, TextRole.Title, 20f, LanistaTablet.Label, tl);
                _traitLines[k] = LanistaTablet.Text(rt, "Line", Indent, 26f, inner - Indent, 30f, TextRole.Body, 18f, ink, tl);
                _traits[k] = row;
            }
        }

        /// <summary>Scales the tablet about its pivot (MenuLayout.TabletScale).</summary>
        public void Fit(float scale)
        {
            if (Root != null) Root.localScale = new Vector3(scale, scale, 1f);
        }

        /// <summary>Shows `g` (null: a gladiator that isn't data yet). `hint`: the unlock hint for a locked one.</summary>
        public void Show(GladiatorDef g, bool open, SaveData save, string hint)
        {
            RosterGrid.SetPortrait(_portrait, g, open, _codex);
            float inner = LanistaTablet.WaxSize.x - 2f * M;
            if (!open || g == null)
            {
                _name.text = "???";
                _select.text = "LOCKED";
                _runs.text = _stats.text = _tradeoff.text = "";
                _desc.text = string.IsNullOrEmpty(hint) ? "???" : hint;
                _desc.color = LanistaTablet.Hint;
                float h = Mathf.Min(LanistaTablet.Height(_desc, 22f, inner), 160f);
                LanistaTablet.Place(_desc.rectTransform, M, FlowTop, inner, h);
                _progress.text = Progress(g, save);
                LanistaTablet.Place(_progress.rectTransform, M, FlowTop + h + Gap, inner, 40f);
                _weapon.gameObject.SetActive(false);
                for (int k = 0; k < MaxTraits; k++) _traits[k].gameObject.SetActive(false);
                return;
            }
            _name.text = g.displayName.ToUpperInvariant();
            _select.text = g.SelectLine;
            long wins = save != null ? SaveData.Count(save.gladiatorWins, g.id) : 0;
            long runs = save != null ? Math.Max(SaveData.Count(save.gladiatorRuns, g.id), wins) : 0;
            _runs.text = LanistaTablet.Count(runs, "run", "runs") + "  ·  " + LanistaTablet.Count(wins, "victory", "victories");
            _stats.text = StatsLine(g);
            _tradeoff.text = g.tradeoff ?? "";
            _desc.text = g.description ?? "";
            _desc.color = LanistaTablet.Ink;
            _progress.text = "";

            var wd = g.startingWeapon;
            _weapon.gameObject.SetActive(wd != null);
            if (wd != null)
            {
                _weaponName.text = wd.displayName;
                _weaponLine.text = wd.description;
                var card = WeaponCard(wd);
                _weaponIcon.enabled = card != null && card.icon != null && card.iconRow >= 0;
                if (_weaponIcon.enabled)
                {
                    float cw = (float)UpgradeIconView.Cell / card.icon.width, ch = (float)UpgradeIconView.Cell / card.icon.height;
                    _weaponIcon.texture = card.icon;
                    _weaponIcon.uvRect = new Rect(0f, 1f - (card.iconRow + 1) * ch, cw, ch);
                }
            }
            var traits = g.traits ?? Array.Empty<GladiatorTrait>();
            int shown = 0;
            for (int i = 0; i < traits.Length && shown < MaxTraits; i++)
            {
                if (traits[i] == null) continue;
                _traitNames[shown].text = traits[i].displayName;
                _traitLines[shown].text = traits[i].description;
                _traits[shown++].gameObject.SetActive(true);
            }
            for (int k = shown; k < MaxTraits; k++) _traits[k].gameObject.SetActive(false);
            Flow(shown);
        }

        /// <summary>Stacks the description, the weapon and the traits under the rule at the largest size (22 → 18) at which
        /// they all fit in the wax; at 18 the last rows get what's left (Truncate, never Ellipsis).</summary>
        void Flow(int traits)
        {
            for (float s = 22f; s > LanistaTablet.Floor; s -= 1f)
                if (Lay(traits, s, false)) return;
            Lay(traits, LanistaTablet.Floor, true);
        }

        bool Lay(int traits, float size, bool clamp)
        {
            float w = LanistaTablet.WaxSize.x, inner = w - 2f * M, bottom = LanistaTablet.WaxSize.y - M;
            float y = FlowTop;
            float h = LanistaTablet.Height(_desc, size, inner);
            if (clamp) h = Mathf.Min(h, Mathf.Max(0f, bottom - y));
            LanistaTablet.Place(_desc.rectTransform, M, y, inner, h);
            y += h + Gap;
            if (_weapon.gameObject.activeSelf) y = Row(_weapon, _weaponName, _weaponLine, y, size, bottom, clamp) + Gap;
            for (int k = 0; k < traits; k++) y = Row(_traits[k], _traitNames[k], _traitLines[k], y, size, bottom, clamp) + Gap;
            return y - Gap <= bottom;
        }

        /// <summary>A name (Title 20) over its line (Body `size`): the row's top at `y`; returns its bottom.</summary>
        static float Row(RectTransform row, TextMeshProUGUI name, TextMeshProUGUI line, float y, float size, float bottom, bool clamp)
        {
            float inner = LanistaTablet.WaxSize.x - 2f * M, tw = inner - Indent;
            float nh = Mathf.Max(26f, LanistaTablet.Height(name, 20f, tw));
            float lh = LanistaTablet.Height(line, size, tw);
            if (clamp) lh = Mathf.Min(lh, Mathf.Max(0f, bottom - y - nh));
            LanistaTablet.Place(name.rectTransform, Indent, 0f, tw, nh);
            LanistaTablet.Place(line.rectTransform, Indent, nh, tw, lh);
            LanistaTablet.Place(row, M, y, inner, nh + lh);
            return y + nh + lh;
        }

        /// <summary>"3 / 25" for a locked gladiator whose unlock is a single count (a secret or a yes/no shows nothing).</summary>
        static string Progress(GladiatorDef g, SaveData save)
        {
            var u = g != null ? g.unlock : null;
            if (u == null || save == null || u.conditions == null || u.conditions.Length != 1) return "";
            MetaProgress.Progress(u.conditions[0], save, out float cur, out float tgt);
            return tgt > 1f ? LanistaTablet.Num(cur) + " / " + LanistaTablet.Num(tgt) : "";
        }

        UpgradeDef WeaponCard(WeaponDef w)
        {
            var cat = _codex != null ? _codex.catalog : null;
            if (cat == null || cat.upgrades == null) return null;
            foreach (var d in cat.upgrades)
                if (d != null && d.weapon == w && d.kind == UpgradeKind.Weapon) return d;
            return null;
        }

        public static string StatsLine(GladiatorDef g)
        {
            float hp = g.BaseValue(StatId.MaxHp, 100f), move = g.BaseValue(StatId.MoveSpeed, 1f);
            return Mathf.RoundToInt(hp) + " HP  ·  " + Mathf.RoundToInt(move * 100f) + "% speed";
        }
    }
}
