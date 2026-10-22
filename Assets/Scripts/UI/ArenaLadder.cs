using System;
using System.Globalization;
using Gladiator.Core;
using Gladiator.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gladiator.UI
{
    /// <summary>
    /// The arena select as the climb itself (R9): every arena a wide stone step, stacked bottom to top and each a little
    /// further right, the Slave Pit at the bottom and the newest at the top. A step is cut from its arena's palette
    /// family (mud, ochre, sandstone, marble, gold, travertine: CLAUDE.md §7.1) and carries its thumbnail, name, the
    /// ladder's line ("I need to survive.") and its Roman numeral, wreathed in laurel once its boss has fallen. Locked
    /// steps are dark with "???" and a padlock. Picked: a gold focus ring and a small lift; hovered: a pale ring. Built
    /// once; picks and hovers only recolour and move steps.
    /// U1: the steps keep their palette art; the words are engraved (names Title, up to 30, all steps at the size the
    /// longest name needs; lines Body 20; numerals Title).
    /// </summary>
    public sealed class ArenaLadder : IUiHoverTarget
    {
        public struct Step
        {
            public RunDef Run;
            public bool Open;
        }

        // the palette families by arena tier (A1 mud … A6 travertine) and their accents (palette_index.md)
        static readonly Color[] Stone =
        {
            new Color32(0xA8, 0x7C, 0x55, 0xFF), new Color32(0xE0, 0xC3, 0x6A, 0xFF), new Color32(0xE8, 0xCF, 0x9C, 0xFF),
            new Color32(0xED, 0xEB, 0xE6, 0xFF), new Color32(0xF7, 0xD9, 0x6B, 0xFF), new Color32(0xDC, 0xCF, 0xB4, 0xFF),
        };
        static readonly Color[] Accent =
        {
            new Color32(0xA0, 0x48, 0x2A, 0xFF), new Color32(0x7D, 0x7A, 0x35, 0xFF), new Color32(0x94, 0x65, 0x3F, 0xFF),
            new Color32(0xC4, 0x30, 0x2B, 0xFF), new Color32(0xA5, 0x1C, 0x30, 0xFF), new Color32(0x6B, 0x2D, 0x7E, 0xFF),
        };
        static readonly string[] Numerals = { "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X", "XI", "XII" };

        const float Lift = 4f, Ring = 6f, NameMax = 30f, NameMin = 18f;

        Step[] _steps = Array.Empty<Step>();
        RectTransform[] _tiles = Array.Empty<RectTransform>(), _frames = Array.Empty<RectTransform>();
        Image[] _frameImages = Array.Empty<Image>(), _wreaths = Array.Empty<Image>();
        Button[] _buttons = Array.Empty<Button>();
        TextMeshProUGUI[] _names = Array.Empty<TextMeshProUGUI>();
        Vector2[] _home = Array.Empty<Vector2>();
        int _pick = -1, _hover = -1;
        Action<int> _onClick;

        /// <summary>The font argument is ignored (U1: the theme decides).</summary>
        public ArenaLadder(TMP_FontAsset font) { }

        public RectTransform Root { get; private set; }
        public int Count => _steps.Length;
        public int Pick => _pick;
        public bool IsOpen(int i) => i >= 0 && i < _steps.Length && _steps[i].Open;
        public RunDef Run(int i) => i >= 0 && i < _steps.Length ? _steps[i].Run : null;
        public RectTransform Tile(int i) => _tiles[i];
        public Button Button(int i) => _buttons[i];
        public bool HasWreath(int i) => i >= 0 && i < _wreaths.Length && _wreaths[i].enabled;
        /// <summary>Tests: step `i`'s name label (its role and its size).</summary>
        public Component NameLabel(int i) => i >= 0 && i < _names.Length ? _names[i] : null;
        /// <summary>Tests: the colour of step `i`'s focus ring (gold picked, pale hovered, clear otherwise).</summary>
        public Color RingColor(int i) => _frameImages[i].color;

        /// <summary>The arena's tier (its layout's, else its place on the ladder).</summary>
        public static int Tier(RunDef r, int index) => r != null && r.layout != null ? r.layout.tier : index;

        public static string Numeral(int tier) => tier >= 0 && tier < Numerals.Length ? Numerals[tier] : (tier + 1).ToString();

        public static bool BossBeaten(RunDef r, SaveData save, out long kills)
        {
            var b = r != null ? r.Boss : null;
            kills = b != null && save != null ? SaveData.Count(save.bossKills, b.id) : 0;
            return kills > 0;
        }

        /// <summary>Builds every step once. The ladder's bottom-left corner sits at `pos` from the canvas's bottom-left.</summary>
        public void Build(Transform parent, Vector2 pos, Step[] steps, SaveData save, Action<int> onClick)
        {
            _steps = steps ?? Array.Empty<Step>();
            _onClick = onClick;
            int n = _steps.Length;
            Root = UiFactory.Rect("Ladder", parent, Vector2.zero, Vector2.zero, pos, GridMath.LadderSize(Mathf.Max(n, 1), MenuLayout.Step, MenuLayout.StepGap, MenuLayout.StepShift));
            _tiles = new RectTransform[n];
            _frames = new RectTransform[n];
            _frameImages = new Image[n];
            _wreaths = new Image[n];
            _buttons = new Button[n];
            _names = new TextMeshProUGUI[n];
            _home = new Vector2[n];
            for (int i = 0; i < n; i++) BuildStep(i, save);
            ShareNameSize();
            Refresh();
        }

        void BuildStep(int i, SaveData save)
        {
            var s = _steps[i];
            var th = UiTheme.Current;
            var tile = MenuLayout.Step;
            var bottomLeft = Vector2.zero;
            var mid = new Vector2(0.5f, 0.5f);
            var c = GridMath.LadderCell(i, tile, MenuLayout.StepGap, MenuLayout.StepShift);
            _home[i] = c;
            int tier = Tier(s.Run, i);
            int family = Mathf.Clamp(tier, 0, Stone.Length - 1);

            // the focus ring round the step: gold while picked, pale while hovered
            var frame = UiKit.Frame("Frame", Root, FrameKind.Focus, bottomLeft, mid, c, tile + new Vector2(Ring * 2f, Ring * 2f), Color.clear);
            _frameImages[i] = frame;
            _frames[i] = frame.rectTransform;

            var block = UiFactory.Image(s.Open ? "Arena" : "Locked", Root, s.Open ? Stone[family] : RosterGrid.BoneLocked, bottomLeft, mid, c, tile, PixelIcons.Tessera);
            block.type = Image.Type.Sliced;
            block.raycastTarget = true;
            var button = block.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            int index = i;
            button.onClick.AddListener(() => _onClick?.Invoke(index));
            var hover = block.gameObject.AddComponent<UiHover>();
            hover.Target = this;
            hover.Id = i;
            _buttons[i] = button;
            _tiles[i] = block.rectTransform;

            var t = block.transform;
            float half = tile.x * 0.5f;
            var thumbAt = new Vector2(-half + 112f, 0f);
            UiFactory.Image("ThumbRim", t, s.Open ? th.waxWood : RosterGrid.Silhouette, mid, mid, thumbAt, new Vector2(200f, 116f), PixelIcons.White);
            var thumb = UiFactory.Rect("Thumbnail", t, mid, mid, thumbAt, new Vector2(192f, 108f)).gameObject.AddComponent<RawImage>();
            thumb.raycastTarget = false;
            SetThumbnail(thumb, s.Run, s.Open, Accent[family]);
            if (!s.Open) UiFactory.Image("Padlock", t, Color.white, mid, mid, thumbAt, new Vector2(28f, 36f), PixelIcons.Lock);

            var numAt = new Vector2(half - 42f, 0f);
            var wreath = UiFactory.Image("Wreath", t, Color.white, mid, mid, numAt, new Vector2(76f, 57f), PixelIcons.Wreath);
            wreath.enabled = s.Open && BossBeaten(s.Run, save, out _);
            _wreaths[i] = wreath;
            var num = UiKit.Text("Numeral", t, Numeral(tier), TextRole.Title, 34f, s.Open ? Accent[family] : th.dust, TextAlignmentOptions.Center,
                mid, mid, numAt + new Vector2(0f, 2f), new Vector2(70f, 50f), s.Open ? TextLook.Engraved : TextLook.Flat);
            num.enableAutoSizing = true;
            num.fontSizeMin = NameMin;
            num.fontSizeMax = 34f;

            // the name above the middle, the ladder's line below it: they meet at the step's centre line
            float x0 = -half + 224f, x1 = half - 84f;
            var ink = s.Open ? th.marbleInk : th.dust;
            var look = s.Open ? TextLook.Engraved : TextLook.Flat;
            var name = UiKit.Text("Name", t, s.Open ? StepName(s.Run) : "???", TextRole.Title, NameMax, ink, TextAlignmentOptions.BottomLeft,
                mid, mid, new Vector2((x0 + x1) * 0.5f, 25f), new Vector2(x1 - x0, 58f), look);
            name.textWrappingMode = TextWrappingModes.Normal;
            name.enableAutoSizing = true;
            name.fontSizeMin = NameMin;
            name.fontSizeMax = NameMax;
            _names[i] = name;
            if (s.Open && s.Run != null && !string.IsNullOrEmpty(s.Run.tagline))
            {
                var line = UiKit.Text("Tagline", t, s.Run.tagline, TextRole.Body, 20f, ink, TextAlignmentOptions.TopLeft,
                    mid, mid, new Vector2((x0 + x1) * 0.5f, -32f), new Vector2(x1 - x0, 50f), look);
                line.textWrappingMode = TextWrappingModes.Normal;
                line.enableAutoSizing = true;
                line.fontSizeMin = NameMin;
                line.fontSizeMax = 20f;
            }
        }

        /// <summary>Every step's name at the size the longest needs (autosize per step would mix 30 and 23 on one ladder).</summary>
        void ShareNameSize()
        {
            if (_names.Length == 0) return;
            float size = NameMax;
            foreach (var n in _names)
            {
                n.ForceMeshUpdate(true, true);
                size = Mathf.Min(size, n.fontSize);
            }
            size = Mathf.Max(NameMin, Mathf.Floor(size));
            foreach (var n in _names)
            {
                n.enableAutoSizing = false;
                n.fontSize = size;
            }
        }

        /// <summary>"THE SLAVE PIT" → "SLAVE PIT" (the step is narrow; the tablet keeps the article).</summary>
        public static string StepName(RunDef r)
        {
            if (r == null) return "???";
            string n = r.DisplayName ?? "";
            if (n.StartsWith("THE ", StringComparison.OrdinalIgnoreCase)) n = n.Substring(4);
            return n.ToUpperInvariant();
        }

        /// <summary>The arena's baked thumbnail (near-black while locked); before the bake, a flat swatch of its accent.</summary>
        public static void SetThumbnail(RawImage img, RunDef r, bool open, Color fallback)
        {
            var dark = new Color(0.12f, 0.11f, 0.11f, 1f);
            if (r != null && r.thumbnail != null) { RosterGrid.Show(img, r.thumbnail, open ? Color.white : dark); return; }
            RosterGrid.Show(img, PixelIcons.White, open ? fallback : dark);
        }

        public void SetPick(int i)
        {
            _pick = _steps.Length == 0 ? -1 : Mathf.Clamp(i, 0, _steps.Length - 1);
            Refresh();
        }

        /// <summary>Scales the whole ladder (MenuLayout.LadderScale) about its bottom-left corner.</summary>
        public void Fit(float scale)
        {
            if (Root != null) Root.localScale = new Vector3(scale, scale, 1f);
        }

        public void Hover(int id, bool on)
        {
            if (on) _hover = id;
            else if (_hover == id) _hover = -1;
            Refresh();
        }

        void Refresh()
        {
            var th = UiTheme.Current;
            for (int i = 0; i < _tiles.Length; i++)
            {
                bool picked = i == _pick;
                _frameImages[i].color = picked ? th.gold : i == _hover ? RosterGrid.Pale : Color.clear;
                var at = _home[i] + (picked ? new Vector2(0f, Lift) : Vector2.zero);
                if (_tiles[i].anchoredPosition != at)
                {
                    _tiles[i].anchoredPosition = at;
                    _frames[i].anchoredPosition = at;
                }
            }
        }
    }

    /// <summary>
    /// The tablet's arena page: the name, the ladder's line, the thumbnail (288 × 162: the bake at 1.5×, so the words fit
    /// at the 18 floor), the description; the boss (portrait, name, beaten or not, with a laurel once beaten); then the
    /// floor (size and area), the hazards, the difficulty (enemy HP and damage taken) and the Renown multiplier. Locked:
    /// "???", a dark thumbnail, the padlock and the hint.
    /// R7: with edicts chosen, one more row under them, "EDICTS  Iron Hides · Blood Sport   RENOWN ×1.45"; the rows close
    /// up a little to make room in the wax (and sit exactly as before with none). U1: the names on the row give way
    /// (fewer named, "· +2 more") until the line fits at the 18 floor.
    /// </summary>
    public sealed class ArenaTablet
    {
        // the rows: FLOOR, HAZARDS, DIFFICULTY, RENOWN, EDICTS (y and height in the group; the group's top in the wax)
        static readonly float[] RowY = { 0f, 28f, 76f, 104f }, RowH = { 26f, 46f, 26f, 26f };
        static readonly float[] TightY = { 0f, 26f, 74f, 100f, 126f }, TightH = { 24f, 46f, 24f, 24f, 24f };
        const float RowsTop = 510f, RowsH = 130f, TightTop = 506f, TightRowsH = 150f;
        const float M = 14f, ThumbW = 288f, ThumbH = 162f, LabelW = 112f;
        /// <summary>The edicts' names on the tablet stop at about this many characters ("· +2 more" after them); fewer when
        /// the line doesn't fit the row.</summary>
        const int NamesBudget = 38;

        RawImage _thumb, _boss;
        Image _lock, _wreath;
        TextMeshProUGUI _name, _tagline, _desc, _bossName, _best, _floor, _hazards, _difficulty, _renown, _edicts;
        GameObject _rows;
        readonly RectTransform[] _rowLabels = new RectTransform[5], _rowValues = new RectTransform[5];
        bool _tight;

        /// <summary>The font argument is ignored (U1: the theme decides).</summary>
        public ArenaTablet(TMP_FontAsset font) { }

        public RectTransform Root { get; private set; }
        public string NameText => _name.text;
        public string BodyText => _desc.text;
        public string BestText => _best.text;
        public string RenownText => _renown.text;
        public string DifficultyText => _difficulty.text;
        /// <summary>The EDICTS row's words ("" when it's hidden).</summary>
        public string EdictsText => _edicts.gameObject.activeSelf ? _edicts.text : "";
        /// <summary>Tests: the frame the tablet is drawn in (`FrameKind.Wax`).</summary>
        public int FrameKindShown => LanistaTablet.FrameOf(Root);

        public void Build(Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos)
        {
            Root = LanistaTablet.Frame(parent, anchor, pivot, pos, out var waxRt);
            var wax = waxRt.transform;
            float w = LanistaTablet.WaxSize.x, inner = w - 2f * M;
            var ink = LanistaTablet.Ink;
            var label = LanistaTablet.Label;
            var tl = TextAlignmentOptions.TopLeft;
            float floor = LanistaTablet.Floor;

            _name = LanistaTablet.Text(wax, "Name", M, 8f, inner, 52f, TextRole.Title, 40f, ink, tl, 24f);
            _name.textWrappingMode = TextWrappingModes.NoWrap;
            _tagline = LanistaTablet.Text(wax, "Tagline", M, 60f, inner, 28f, TextRole.Body, 22f, LanistaTablet.Hint, tl, floor);
            float tx = (w - ThumbW) * 0.5f, ty = 94f;
            LanistaTablet.Plate(wax, tx, ty, ThumbW, ThumbH);
            _thumb = LanistaTablet.Raw(wax, "Thumbnail", tx, ty, ThumbW, ThumbH);
            _lock = LanistaTablet.Image(wax, "Padlock", w * 0.5f - 21f, ty + ThumbH * 0.5f - 27f, 42f, 54f, Color.white, PixelIcons.Lock);
            _desc = LanistaTablet.Text(wax, "Description", M, 264f, inner, 92f, TextRole.Body, 22f, ink, tl, floor);
            LanistaTablet.Rule(wax, 362f);

            const float by = 372f, bx = 158f;
            LanistaTablet.Plate(wax, M, by, 128f, 128f);
            _boss = LanistaTablet.Raw(wax, "Boss", M, by, 128f, 128f);
            LanistaTablet.Text(wax, "BossLabel", bx, by, w - bx - M, 24f, TextRole.Title, 18f, label, tl).text = "BOSS";
            _bossName = LanistaTablet.Text(wax, "BossName", bx, by + 24f, w - bx - M, 56f, TextRole.Body, 22f, ink, tl, floor);
            _wreath = LanistaTablet.Image(wax, "Wreath", bx, by + 86f, 48f, 36f, Color.white, PixelIcons.Wreath);
            _best = LanistaTablet.Text(wax, "Best", bx + 56f, by + 90f, w - bx - 56f - M, 28f, TextRole.Body, 20f, ink, tl, floor);

            _rows = LanistaTablet.Group(wax, "Rows", M, RowsTop, inner, RowsH).gameObject;
            var rows = _rows.transform;
            float vx = LabelW + 8f, vw = inner - vx;
            _floor = Row(rows, 0, "FLOOR", RowY[0], RowH[0], vx, vw);
            _hazards = Row(rows, 1, "HAZARDS", RowY[1], RowH[1], vx, vw);
            _difficulty = Row(rows, 2, "DIFFICULTY", RowY[2], RowH[2], vx, vw);
            _renown = Row(rows, 3, "RENOWN", RowY[3], RowH[3], vx, vw);
            _edicts = Row(rows, 4, "EDICTS", TightY[4], TightH[4], vx, vw);
            _edicts.textWrappingMode = TextWrappingModes.NoWrap; // one line: the names give way, not the multiplier
            _rowLabels[4].gameObject.SetActive(false);
            _edicts.gameObject.SetActive(false);
        }

        TextMeshProUGUI Row(Transform rows, int i, string label, float y, float h, float vx, float vw)
        {
            var l = LanistaTablet.Text(rows, label, 0f, y + 1f, LabelW, 24f, TextRole.Title, 18f, LanistaTablet.Label, TextAlignmentOptions.TopLeft);
            l.text = label;
            var v = LanistaTablet.Text(rows, label + "Value", vx, y, vw, h, TextRole.Body, 20f, LanistaTablet.Ink, TextAlignmentOptions.TopLeft, LanistaTablet.Floor);
            _rowLabels[i] = l.rectTransform;
            _rowValues[i] = v.rectTransform;
            return v;
        }

        /// <summary>Four rows as they always were, or five closed up a little (the EDICTS row under the others).</summary>
        void LayRows(bool tight)
        {
            if (tight == _tight) return;
            _tight = tight;
            var group = (RectTransform)_rows.transform;
            group.anchoredPosition = new Vector2(group.anchoredPosition.x, -(tight ? TightTop : RowsTop));
            group.sizeDelta = new Vector2(group.sizeDelta.x, tight ? TightRowsH : RowsH);
            for (int i = 0; i < 4; i++)
            {
                float y = tight ? TightY[i] : RowY[i], h = tight ? TightH[i] : RowH[i];
                _rowLabels[i].anchoredPosition = new Vector2(_rowLabels[i].anchoredPosition.x, -(y + 1f));
                _rowValues[i].anchoredPosition = new Vector2(_rowValues[i].anchoredPosition.x, -y);
                _rowValues[i].sizeDelta = new Vector2(_rowValues[i].sizeDelta.x, h);
            }
            _rowLabels[4].gameObject.SetActive(tight);
            _edicts.gameObject.SetActive(tight);
        }

        public void Fit(float scale)
        {
            if (Root != null) Root.localScale = new Vector3(scale, scale, 1f);
        }

        /// <param name="tier">The arena's tier (ArenaLadder.Tier): its numeral's colour and its Renown.</param>
        public void Show(RunDef r, bool open, int tier, SaveData save, MetaConfig meta) => Show(r, open, tier, save, meta, null);

        /// <param name="edicts">The edicts the run would start under (R7): an EDICTS row when any.</param>
        public void Show(RunDef r, bool open, int tier, SaveData save, MetaConfig meta, RunModifierDef[] edicts)
        {
            var boss = r != null ? r.Boss : null;
            ArenaLadder.SetThumbnail(_thumb, r, open, LanistaTablet.PlateFloor);
            _lock.enabled = !open;
            if (r != null && r.bossPortrait != null) RosterGrid.Show(_boss, r.bossPortrait, open ? Color.white : RosterGrid.Silhouette);
            else RosterGrid.Show(_boss, PixelIcons.Bust, RosterGrid.Silhouette);
            if (!open || r == null)
            {
                _name.text = "???";
                _tagline.text = "";
                _desc.text = r != null && r.unlock != null ? r.unlock.hint : "???";
                _desc.color = LanistaTablet.Hint;
                _bossName.text = "???";
                _best.text = "";
                _wreath.enabled = false;
                _rows.SetActive(false);
                return;
            }
            _name.text = r.DisplayName;
            _tagline.text = string.IsNullOrEmpty(r.tagline) ? "" : "“" + r.tagline + "”";
            _desc.text = r.description ?? "";
            _desc.color = LanistaTablet.Ink;
            string bossLabel = BossLabel(r);
            _bossName.text = bossLabel.Length > 0 ? bossLabel : "None: survive the clock";
            bool beaten = ArenaLadder.BossBeaten(r, save, out long kills);
            _wreath.enabled = boss != null;
            _wreath.color = beaten ? Color.white : new Color(1f, 1f, 1f, 0.25f); // an unearned laurel, faint
            _best.text = boss == null ? "" : beaten ? "Beaten " + kills + "×" : "Not beaten yet";
            _rows.SetActive(true);
            var l = r.layout;
            _floor.text = l != null ? FloorLine(l) : "—";
            _hazards.text = string.IsNullOrEmpty(r.hazards) ? "—" : r.hazards;
            _difficulty.text = l != null ? DifficultyLine(l) : "—";
            _renown.text = meta != null ? RenownLine(meta, tier) : "—";
            bool any = edicts != null && edicts.Length > 0;
            LayRows(any);
            if (any) _edicts.text = FittingEdictLine(edicts);
        }

        /// <summary>The EDICTS row's words, naming as many edicts as fit the row at the 18 floor (menu-only: it measures).</summary>
        string FittingEdictLine(RunModifierDef[] edicts)
        {
            float width = _rowValues[4].sizeDelta.x;
            _edicts.enableAutoSizing = false;
            _edicts.fontSize = LanistaTablet.Floor;
            string line = EdictLine(edicts, NamesBudget);
            for (int budget = NamesBudget - 4; budget > 0 && _edicts.GetPreferredValues(line, 0f, 0f).x > width; budget -= 4)
                line = EdictLine(edicts, budget);
            _edicts.enableAutoSizing = true;
            _edicts.fontSizeMin = LanistaTablet.Floor;
            _edicts.fontSizeMax = 20f;
            return line;
        }

        /// <summary>"Iron Hides · Blood Sport   RENOWN ×1.45" (the names stop at about NamesBudget characters: "· +2 more").</summary>
        public static string EdictLine(RunModifierDef[] edicts) => EdictLine(edicts, NamesBudget);

        /// <summary>As EdictLine, the names stopping at about `namesBudget` characters (the first is always named).</summary>
        public static string EdictLine(RunModifierDef[] edicts, int namesBudget)
        {
            var sb = new System.Text.StringBuilder(96);
            int named = 0, n = 0;
            bool full = false;
            foreach (var e in edicts)
            {
                if (e == null) continue;
                n++;
                string name = e.displayName ?? e.id;
                full |= named > 0 && sb.Length + 3 + name.Length > namesBudget; // in order: the rest are counted, not named
                if (full) continue;
                if (named > 0) sb.Append(" · ");
                sb.Append(name);
                named++;
            }
            if (n > named) sb.Append(" · +").Append(n - named).Append(" more");
            float mul = 1f + RunModifiers.Bonus(edicts);
            return sb.Append("   RENOWN ").Append(EdictsPanel.MulText(mul)).ToString();
        }

        /// <summary>"Gorm, the Pit Master": the run's bossName, else its layout's boss.</summary>
        public static string BossLabel(RunDef r)
        {
            if (r == null) return "";
            if (!string.IsNullOrEmpty(r.bossName)) return r.bossName;
            return r.Boss != null ? BossLabel(r.Boss) : "";
        }

        /// <summary>"GORM" + "THE PIT MASTER" → "Gorm, the Pit Master".</summary>
        public static string BossLabel(BossDef b)
        {
            if (b == null) return "";
            string name = Title(b.displayName), title = Title(b.title);
            if (title.StartsWith("The ", StringComparison.Ordinal)) title = "the " + title.Substring(4);
            return title.Length > 0 ? name + ", " + title : name;
        }

        static string Title(string s) => string.IsNullOrEmpty(s) ? "" : CultureInfo.InvariantCulture.TextInfo.ToTitleCase(s.ToLowerInvariant());

        /// <summary>"84 × 70 m  ·  4,617 m²" (the play area's bounding box and its area).</summary>
        public static string FloorLine(ArenaLayoutDef l)
        {
            Vector2 size;
            if (l.outline != null && l.outline.Length >= 3)
            {
                Vector2 min = l.outline[0], max = l.outline[0];
                foreach (var p in l.outline) { min = Vector2.Min(min, p); max = Vector2.Max(max, p); }
                size = max - min;
            }
            else size = new Vector2(l.radius * 2f, l.radius * 2f * (l.aspect > 0f ? l.aspect : 1f));
            return Mathf.RoundToInt(size.x) + " × " + Mathf.RoundToInt(size.y) + " m  ·  "
                + Mathf.RoundToInt(l.PlayArea()).ToString("N0", CultureInfo.InvariantCulture) + " m²";
        }

        /// <summary>"Enemy HP +25 %  ·  damage taken +15 %", or the base line for the Slave Pit.</summary>
        public static string DifficultyLine(ArenaLayoutDef l)
        {
            int hp = Mathf.RoundToInt((l.enemyHpMul - 1f) * 100f), dmg = Mathf.RoundToInt((l.playerDamageTakenMul - 1f) * 100f);
            if (hp == 0 && dmg == 0) return "The base line: no bonuses";
            return "Enemy HP " + Signed(hp) + " %  ·  damage taken " + Signed(dmg) + " %";
        }

        /// <summary>"×1.5": what this tier multiplies a run's Renown by (MetaConfig.deeperArenaBonus).</summary>
        public static string RenownLine(MetaConfig meta, int tier) =>
            "×" + (1f + meta.deeperArenaBonus * Mathf.Max(0, tier)).ToString("0.##", CultureInfo.InvariantCulture);

        static string Signed(int v) => v >= 0 ? "+" + v : v.ToString();
    }
}
