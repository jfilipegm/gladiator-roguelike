using System;
using Gladiator.Core;
using Gladiator.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gladiator.UI
{
    /// <summary>
    /// The lanista's wax tablet (R9): the detail panel of the select screens and the Codex roster. A dark wooden frame
    /// (bronze pegs in the corners, the cords of a diptych's hinge on the left) round a warm wax panel, the words
    /// scratched in dark. Pages are built into the wax once (GladiatorTablet, ArenaTablet) and only swap their words
    /// and pictures on a pick, never per frame. Coordinates are from the wax's top-left corner, y down.
    /// </summary>
    public static class LanistaTablet
    {
        // the game's palette (palette_index.md): wood_dark, leather_d, ochre, ochre_d, bronze, rope, crimson_d
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
        public const float Rim = 16f;

        public static Vector2 WaxSize => MenuLayout.Tablet - new Vector2(Rim * 2f, Rim * 2f);

        /// <summary>Builds the frame at `pos` from `anchor` (the tablet's `pivot` sits there); returns its root and the wax.</summary>
        public static RectTransform Frame(Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, out RectTransform wax)
        {
            var size = MenuLayout.Tablet;
            var mid = new Vector2(0.5f, 0.5f);
            var wood = UiFactory.Image("Tablet", parent, Wood, anchor, pivot, pos, size, PixelIcons.White);
            var t = wood.transform;
            UiFactory.Image("Lip", t, WoodDark, mid, mid, Vector2.zero, size - new Vector2(Rim * 2f - 8f, Rim * 2f - 8f), PixelIcons.White);
            var w = UiFactory.Image("Wax", t, Wax, mid, mid, Vector2.zero, WaxSize, PixelIcons.White);
            float px = size.x * 0.5f - Rim * 0.5f, py = size.y * 0.5f - Rim * 0.5f;
            for (int i = 0; i < 4; i++)
                UiFactory.Image("Peg", t, Peg, mid, mid, new Vector2(i % 2 == 0 ? -px : px, i < 2 ? py : -py), new Vector2(8f, 8f), PixelIcons.White);
            for (int i = 0; i < 2; i++)
                UiFactory.Image("Cord", t, Cord, mid, mid, new Vector2(-size.x * 0.5f + Rim * 0.5f, (i == 0 ? 1f : -1f) * size.y * 0.28f), new Vector2(Rim + 10f, 6f), PixelIcons.White);
            wax = w.rectTransform;
            return wood.rectTransform;
        }

        static RectTransform At(RectTransform rt, float x, float y, float w, float h)
        {
            var tl = new Vector2(0f, 1f);
            rt.anchorMin = rt.anchorMax = tl;
            rt.pivot = tl;
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        /// <summary>Scratched words (the font's plain material: no outline on wax).</summary>
        public static TextMeshProUGUI Text(Transform wax, string name, float x, float y, float w, float h, float size, Color color,
            TextAlignmentOptions align, FontStyles style, TMP_FontAsset font, float minSize = 0f)
        {
            var t = UiFactory.Text(name, wax, "", size, color, align, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, font, null);
            At(t.rectTransform, x, y, w, h);
            t.fontStyle = style;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Ellipsis;
            if (minSize > 0f)
            {
                t.enableAutoSizing = true;
                t.fontSizeMin = minSize;
                t.fontSizeMax = size;
            }
            return t;
        }

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

        /// <summary>A sunken plate in the wax for a picture: a dark rim, a darker wax floor.</summary>
        public static void Plate(Transform wax, float x, float y, float w, float h)
        {
            Image(wax, "PlateRim", x - 4f, y - 4f, w + 8f, h + 8f, Wood);
            Image(wax, "Plate", x, y, w, h, WaxDark);
        }

        /// <summary>A scratched rule across the wax.</summary>
        public static void Rule(Transform wax, float y) => Image(wax, "Rule", 14f, y, WaxSize.x - 28f, 2f, Faint);

        public static string Count(long n, string one, string many) => n + " " + (n == 1 ? one : many);

        /// <summary>A whole-number count with thousands separators past 9,999 (as the Codex shows progress).</summary>
        public static string Num(float v) => v >= 10000f ? Mathf.RoundToInt(v).ToString("N0") : Mathf.RoundToInt(v).ToString();
    }

    /// <summary>
    /// The tablet's gladiator page: the portrait at 2× (128 px, point filtered), the name, the select line, runs and
    /// victories with them, the stats and the tradeoff beside it; under the rule the description, the starting weapon
    /// (icon, name, its line) and each trait (mark, name, its line). Locked: the silhouette, "???", LOCKED and the
    /// unlock's hint (with its progress when it's a count).
    /// </summary>
    public sealed class GladiatorTablet
    {
        const int MaxTraits = 3;
        const float Portrait = 256f;

        readonly TMP_FontAsset _font;
        CodexConfig _codex;
        RawImage _portrait, _weaponIcon;
        TextMeshProUGUI _name, _select, _runs, _stats, _tradeoff, _desc, _weaponName, _weaponLine, _progress;
        GameObject _weapon;
        readonly GameObject[] _traits = new GameObject[MaxTraits];
        readonly TextMeshProUGUI[] _traitNames = new TextMeshProUGUI[MaxTraits], _traitLines = new TextMeshProUGUI[MaxTraits];

        public GladiatorTablet(TMP_FontAsset font) => _font = font;

        public RectTransform Root { get; private set; }
        /// <summary>Tests: what the tablet shows.</summary>
        public string NameText => _name.text;
        public string BodyText => _desc.text;
        public string RunsText => _runs.text;
        public bool ShowsWeapon => _weapon.activeSelf;

        public void Build(Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, CodexConfig codex)
        {
            _codex = codex;
            Root = LanistaTablet.Frame(parent, anchor, pivot, pos, out var waxRt);
            var wax = waxRt.transform;
            float w = LanistaTablet.WaxSize.x;
            var f = _font;

            LanistaTablet.Plate(wax, 14f, 14f, Portrait, Portrait);
            _portrait = LanistaTablet.Raw(wax, "Portrait", 14f, 14f, Portrait, Portrait);
            float x = 14f + Portrait + 18f, cw = w - x - 14f;
            _name = LanistaTablet.Text(wax, "Name", x, 12f, cw, 78f, 32f, LanistaTablet.Heading, TextAlignmentOptions.TopLeft, FontStyles.Bold, f, 20f);
            _select = LanistaTablet.Text(wax, "Select", x, 94f, cw, 26f, 18f, LanistaTablet.Scratch, TextAlignmentOptions.TopLeft, FontStyles.Italic, f, 13f);
            _runs = LanistaTablet.Text(wax, "Runs", x, 124f, cw, 26f, 19f, LanistaTablet.Scratch, TextAlignmentOptions.TopLeft, FontStyles.Bold, f, 13f);
            _stats = LanistaTablet.Text(wax, "Stats", x, 152f, cw, 26f, 19f, LanistaTablet.Scratch, TextAlignmentOptions.TopLeft, FontStyles.Normal, f, 13f);
            _tradeoff = LanistaTablet.Text(wax, "Tradeoff", x, 184f, cw, 90f, 17f, LanistaTablet.Heading, TextAlignmentOptions.TopLeft, FontStyles.Italic, f, 12f);

            LanistaTablet.Rule(wax, 290f);
            _desc = LanistaTablet.Text(wax, "Description", 14f, 300f, w - 28f, 84f, 20f, LanistaTablet.Scratch, TextAlignmentOptions.TopLeft, FontStyles.Normal, f, 14f);
            _progress = LanistaTablet.Text(wax, "Progress", 14f, 392f, w - 28f, 40f, 30f, LanistaTablet.Heading, TextAlignmentOptions.TopLeft, FontStyles.Bold, f);

            // the starting weapon: its card's icon (20 px art at 2×), name and line
            _weapon = LanistaTablet.Group(wax, "Weapon", 14f, 392f, w - 28f, 56f).gameObject;
            var wt = _weapon.transform;
            LanistaTablet.Plate(wt, 4f, 8f, 40f, 40f);
            _weaponIcon = LanistaTablet.Raw(wt, "Icon", 4f, 8f, 40f, 40f);
            _weaponName = LanistaTablet.Text(wt, "Name", 58f, 2f, w - 28f - 58f, 24f, 20f, LanistaTablet.Heading, TextAlignmentOptions.TopLeft, FontStyles.Bold, f, 14f);
            _weaponLine = LanistaTablet.Text(wt, "Line", 58f, 26f, w - 28f - 58f, 30f, 16f, LanistaTablet.Scratch, TextAlignmentOptions.TopLeft, FontStyles.Normal, f, 12f);

            for (int k = 0; k < MaxTraits; k++)
            {
                var row = LanistaTablet.Group(wax, "Trait", 14f, 456f + k * 60f, w - 28f, 56f).gameObject;
                var rt = row.transform;
                LanistaTablet.Image(rt, "Mark", 8f, 6f, 32f, 32f, Color.white, PixelIcons.Sigil);
                _traitNames[k] = LanistaTablet.Text(rt, "Name", 58f, 0f, w - 28f - 58f, 22f, 18f, LanistaTablet.Heading, TextAlignmentOptions.TopLeft, FontStyles.Bold, f, 13f);
                _traitLines[k] = LanistaTablet.Text(rt, "Line", 58f, 22f, w - 28f - 58f, 34f, 15f, LanistaTablet.Scratch, TextAlignmentOptions.TopLeft, FontStyles.Normal, f, 11f);
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
            if (!open || g == null)
            {
                _name.text = "???";
                _select.text = "LOCKED";
                _runs.text = _stats.text = _tradeoff.text = "";
                _desc.text = string.IsNullOrEmpty(hint) ? "???" : hint;
                _desc.fontStyle = FontStyles.Italic;
                _progress.text = Progress(g, save);
                _weapon.SetActive(false);
                for (int k = 0; k < MaxTraits; k++) _traits[k].SetActive(false);
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
            _desc.fontStyle = FontStyles.Normal;
            _progress.text = "";

            var wd = g.startingWeapon;
            _weapon.SetActive(wd != null);
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
                _traits[shown++].SetActive(true);
            }
            for (int k = shown; k < MaxTraits; k++) _traits[k].SetActive(false);
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
