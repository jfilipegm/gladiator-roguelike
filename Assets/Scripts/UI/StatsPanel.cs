using Gladiator.Core;
using Gladiator.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gladiator.UI
{
    /// <summary>
    /// P1 stats page (findings 10, 10b), one column, three faces:
    /// - Pick (level-up, chest, altar, boss cards): "your stats" beside the cards; the selected card (mouse hover, keys or
    ///   gamepad) previews each stat it changes, "Damage 117% → 134%", gains green, losses red.
    /// - Pause: the same column (P3, the producer's note 17: the pause looks like the level-up): the gladiator's name, the
    ///   run's seed, every stat's total; hovering a stat row shows where it comes from (base, each card, synergies,
    ///   traits and their live bonuses, edicts, the admin layer in dev builds) in a UiTooltip left of the column, and
    ///   hovering the name shows the gladiator's traits.
    /// - Prompt: walking up to the Blood Altar or an Armory Chest shows what it costs ("Max HP 120 → 102").
    /// P3 (notes 15, 17, 19): the column sits on riveted bronze in both faces, right of the cards at LevelUpLayout's
    /// symmetric fit (InventoryPanel mirrors it on the left). Its rows are fixed: a title, one line under it, three
    /// sections and one row per stat, each a label at its own place, so nothing moves while you go from card to card;
    /// only the numbers change. The weapons' numbers left the page (the inventory's tooltips have them).
    /// Created by LevelUpPanel (so every arena scene has one without a scene rebuild). Built once; the words are
    /// rewritten only when what they show changes (stats, offers, selection) into one char buffer whose slices are handed
    /// to the labels: no allocation per frame. Hidden with a CanvasGroup, never SetActive (LevelUpPanel's note on canvas
    /// rebuilds). R8: the Pause face names the run's seed ("SEED 7F3A-91C2"), written digit by digit.
    /// </summary>
    public sealed class StatsPanel : MonoBehaviour, ITickable, IUiHoverTarget
    {
        public enum Mode : byte { Hidden, Pause, Pick }

        public static StatsPanel Instance { get; private set; }

        public TMP_FontAsset font;
        public Material fontMaterial;
        [Tooltip("The level-up cards whose selection this previews (LevelUpPanel sets itself).")]
        public LevelUpPanel cards;

        /// <summary>Both faces show the 410-wide column since P3 (the pause's 560-wide marble page is gone).</summary>
        public const float PageWidth = LevelUpLayout.StatsW, PickWidth = LevelUpLayout.StatsW, Margin = LevelUpLayout.Edge;
        const float PromptWidth = 680f, PromptHeight = 88f; // P3: tall enough for its words inside the bronze's ContentRect
        /// <summary>A stat row's height and font size, and the gap before each section (scale 1).</summary>
        public const float RowH = 24f, RowSize = 20f, SectionGap = 8f;
        const float ValueShare = 0.52f, ValueGap = 5f, TipGap = 12f, MinRowSize = 16f;
        /// <summary>The hover id of the title row (the pause: the gladiator's traits).</summary>
        public const int TitleHover = 100;
        const string PickTitle = "YOUR STATS", NoChange = "no stat changes (its effect is on the card)", HoverHint = " · hover for sources";

        Canvas _canvas;
        CanvasGroup _group, _promptGroup;
        RectTransform _box, _promptBox;
        Image _frame, _promptFrame, _rowLit;
        TextMeshProUGUI _title, _sub, _prompt;
        TextMeshProUGUI[] _heads, _names, _values;
        float[] _rowTop; // each stat row's top below the column's top, scale 1
        float _titleTop;
        int _rows;
        Color _headC, _dimC, _cardC, _valueC;
        float _pickScale = 1f;
        readonly CharBuffer _text = new CharBuffer(4096);
        readonly CharBuffer _promptText = new CharBuffer(160);
        readonly StatShare[] _shares = new StatShare[24];
        // slices of _text: 0 the title, 1 the line under it, 2-4 the section heads, then each row's name and value
        int[] _start, _len;
        const int SliceTitle = 0, SliceSub = 1, SliceHead = 2;
        static int SliceName(int row) => 5 + 2 * row;
        static int SliceValue(int row) => 6 + 2 * row;

        UiTooltip _tip;
        int _hover = -1, _tipKey = int.MinValue;
        LevelUpLayout.Fit _fit;
        Vector2 _laidOutFor;
        int _laidCards = -1;

        /// <summary>The preview the Pick face shows (tests read it before and after a pick).</summary>
        public StatPreview Preview { get; } = new StatPreview();
        /// <summary>The Prompt face's preview (altar, chest).</summary>
        public StatPreview PromptPreview { get; } = new StatPreview();
        public Mode Current { get; private set; }
        public bool PromptShown { get; private set; }
        /// <summary>Tests: the page's words (title, line under it, section heads, each row's name and value, one per line)
        /// and the prompt's, as shown.</summary>
        public string Text => _text.ToString();
        public string PromptText => _promptText.ToString();
        /// <summary>How many times the page's text has been rewritten (tests: it must not rewrite every frame).</summary>
        public int Rewrites { get; private set; }
        /// <summary>The column's box (tests: the cards never run under it).</summary>
        public RectTransform Page => _box;
        /// <summary>The width the column takes beside the cards at its current scale, with its gap.</summary>
        public float PickReserve => PickWidth * _pickScale + Margin;
        /// <summary>The column's scale (LevelUpLayout: both faces).</summary>
        public float PickScale => _pickScale;
        /// <summary>Tests: the FrameKind the page shows (Bronze, both faces since P3; UiFrames.KindOf), and the prompt's.</summary>
        public int FrameShown => UiFrames.KindOf(_frame);
        public int PromptFrameShown => UiFrames.KindOf(_promptFrame);
        /// <summary>A stat row's value label (role checks: Body).</summary>
        public Component BodyLabel => _values != null && _values.Length > 0 ? _values[0] : null;
        public Component TitleLabel => _title;

        /// <summary>
        /// Since P3 the column places itself from LevelUpLayout (the same fit LevelUpPanel uses); this only asks it to
        /// re-place (kept for callers of the U1 API).
        /// </summary>
        public void SetPickScale(float k) => _laidCards = -1;

        int _key = int.MinValue, _promptKey = int.MinValue;

        PlayerStats _stats;
        PlayerBuild _build;
        PlayerHealth _health;
        WeaponSystem _weapons;
        Player _player;

        void Awake()
        {
            Instance = this;
            var th = UiTheme.Current;
            ColorUtility.TryParseHtmlString(StatText.Head, out _headC);
            ColorUtility.TryParseHtmlString(StatText.Dim, out _dimC);
            ColorUtility.TryParseHtmlString(StatText.CardCol, out _cardC);
            ColorUtility.TryParseHtmlString(StatText.ValueCol, out _valueC);
            _canvas = UiFactory.Canvas("StatsCanvas", transform, UiSortOrder.LevelUp + 1); // over the card dim, under menus
            var root = _canvas.transform;
            var left = new Vector2(0f, 0.5f);
            var tl = new Vector2(0f, 1f);
            var mid = new Vector2(0.5f, 0.5f);
            _box = UiFactory.Rect("Page", root, left, tl, Vector2.zero, new Vector2(PickWidth, LevelUpLayout.StatsH));
            _frame = UiKit.Frame("Frame", _box, FrameKind.Bronze, mid, mid, Vector2.zero, Vector2.zero, Color.white);
            UiFactory.Stretch(_frame.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _group = _box.gameObject.AddComponent<CanvasGroup>();
            BuildRows(tl);

            var bottom = new Vector2(0.5f, 0f);
            _promptBox = UiFactory.Rect("Prompt", root, bottom, bottom, new Vector2(0f, 196f), new Vector2(PromptWidth, PromptHeight));
            _promptFrame = UiKit.Frame("Frame", _promptBox, FrameKind.Bronze, mid, mid, Vector2.zero, Vector2.zero, Color.white);
            UiFactory.Stretch(_promptFrame.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            // the prompt's words inside the bronze's ContentRect (the padding rule)
            var pc = UiKit.ContentRect(FrameKind.Bronze, new Vector2(PromptWidth, PromptHeight));
            _prompt = UiKit.Text("Text", _promptBox, "", TextRole.Body, 28f, th.ink, TextAlignmentOptions.Midline, mid, mid,
                pc.center, pc.size);
            _prompt.richText = true;
            _prompt.enableAutoSizing = true; // "BLOOD ALTAR   Max HP 120 → 102" in Marcellus: shrink before it's cut
            _prompt.fontSizeMin = 20f;
            _prompt.fontSizeMax = 28f;
            _promptGroup = _promptBox.gameObject.AddComponent<CanvasGroup>();
            _promptGroup.blocksRaycasts = false;
            _promptGroup.interactable = false;

            _tip = new UiTooltip(root, "StatTip");
            _group.blocksRaycasts = false;
            _group.interactable = false;
            Presize();
            Show(_group, false);
            Show(_promptGroup, false);
        }

        /// <summary>The fixed rows (scale 1, from the bronze's ContentRect): title, the line under it, then per section
        /// a gap, its head and one row per stat. Each stat row takes the pointer (the pause's tooltips).</summary>
        void BuildRows(Vector2 tl)
        {
            var pad = UiFrames.Padding(FrameKind.Bronze);
            float p = UiKit.ContentPad;
            float left = pad.x + p, top = pad.w + p, inner = PickWidth - left - pad.z - p;
            float nameW = inner * ValueShare - ValueGap, valueX = left + inner * ValueShare, valueW = inner * (1f - ValueShare);
            _rows = StatText.Order.Length;
            int sections = StatText.SectionStart.Length;
            _start = new int[SliceValue(_rows - 1) + 1];
            _len = new int[_start.Length];
            _names = new TextMeshProUGUI[_rows];
            _values = new TextMeshProUGUI[_rows];
            _heads = new TextMeshProUGUI[sections];
            _rowTop = new float[_rows];

            _titleTop = top;
            _title = UiKit.Text("Title", _box, PickTitle, TextRole.Body, InventoryPanel.TitleSize, _headC, TextAlignmentOptions.MidlineLeft, tl, tl,
                new Vector2(left, -top), new Vector2(inner, InventoryPanel.TitleH));
            _title.richText = true;
            _sub = UiKit.Text("Sub", _box, "", TextRole.Body, InventoryPanel.SubSize, _dimC, TextAlignmentOptions.MidlineLeft, tl, tl,
                new Vector2(left, -top - InventoryPanel.TitleH), new Vector2(inner, InventoryPanel.SubH));
            _sub.richText = true;
            AutoSize(_sub, 13f, InventoryPanel.SubSize); // "if you take Gilded Mercenary's Purse: HP 54 → 100"
            // the title takes the pointer too (the pause: the gladiator's traits)
            Hit(_box, new Vector2(left, -top), new Vector2(inner, InventoryPanel.TitleH), TitleHover);

            _rowLit = UiFactory.Image("Lit", _box, new Color(1f, 1f, 1f, 0f), tl, tl, new Vector2(left - 4f, -top), new Vector2(inner + 8f, RowH), PixelIcons.White);
            _rowLit.raycastTarget = false;

            float y = top + InventoryPanel.TitleH + InventoryPanel.SubH;
            for (int sct = 0; sct < sections; sct++)
            {
                int from = StatText.SectionStart[sct], to = sct + 1 < sections ? StatText.SectionStart[sct + 1] : _rows;
                y += SectionGap;
                _heads[sct] = UiKit.Text("Head" + sct, _box, StatText.SectionTitle[sct], TextRole.Body, InventoryPanel.HeadSize, _headC,
                    TextAlignmentOptions.MidlineLeft, tl, tl, new Vector2(left, -y), new Vector2(inner, InventoryPanel.HeadH));
                y += InventoryPanel.HeadH;
                for (int i = from; i < to; i++)
                {
                    _rowTop[i] = y;
                    var s = StatText.Order[i];
                    _names[i] = UiKit.Text("Name_" + s, _box, StatInfo.Name(s), TextRole.Body, RowSize, _cardC, TextAlignmentOptions.MidlineLeft, tl, tl,
                        new Vector2(left, -y), new Vector2(nameW, RowH));
                    AutoSize(_names[i], MinRowSize, RowSize);
                    _values[i] = UiKit.Text("Value_" + s, _box, "", TextRole.Body, RowSize, _valueC, TextAlignmentOptions.MidlineLeft, tl, tl,
                        new Vector2(valueX, -y), new Vector2(valueW, RowH));
                    _values[i].richText = true;
                    AutoSize(_values[i], MinRowSize, RowSize); // "+100% → +125%" shrinks before it's cut
                    Hit(_box, new Vector2(left, -y), new Vector2(inner, RowH), i);
                    y += RowH;
                }
            }
        }

        static void AutoSize(TMP_Text t, float min, float max)
        {
            t.enableAutoSizing = true;
            t.fontSizeMin = min;
            t.fontSizeMax = max;
        }

        /// <summary>A clear pointer area forwarding enter/exit with `id` (UiHover).</summary>
        void Hit(Transform parent, Vector2 pos, Vector2 size, int id)
        {
            var tl = new Vector2(0f, 1f);
            var img = UiFactory.Image("Hit" + id, parent, Color.clear, tl, tl, pos, size, PixelIcons.White);
            img.raycastTarget = true;
            var h = img.gameObject.AddComponent<UiHover>();
            h.Target = this;
            h.Id = id;
        }

        static void Show(CanvasGroup g, bool on) { if (g.alpha != (on ? 1f : 0f)) g.alpha = on ? 1f : 0f; }

        /// <summary>
        /// Grow the TMP buffers once past the longest words, with every glyph the page uses (→ and − come from the
        /// dynamic fallback font: adding them now keeps the first preview from growing its atlas mid-run).
        /// </summary>
        void Presize()
        {
            const string Glyphs = "ABCDEFGHIJKLMNOPQRSTUVWXYZ abcdefghijklmnopqrstuvwxyz 0123456789 +-−→×·%/()[]:'’!?.,&—<>=#";
            var sb = new System.Text.StringBuilder(256);
            while (sb.Length < 200) sb.Append(Glyphs);
            string line = sb.ToString(0, 200);
            foreach (var t in new[] { _title, _sub })
            {
                t.SetText(line);
                t.ForceMeshUpdate(true, true);
            }
            for (int i = 0; i < _rows; i++)
                foreach (var t in new[] { _names[i], _values[i] })
                {
                    string keep = t.text;
                    t.SetText(line);
                    t.ForceMeshUpdate(true, true);
                    t.SetText(keep);
                }
            sb.Length = 0;
            while (sb.Length < _promptText.Chars.Length) sb.Append(Glyphs);
            _prompt.SetText(sb.ToString());
            _prompt.ForceMeshUpdate(true, true);
            _title.SetText(PickTitle);
            _sub.SetText("");
            _prompt.SetText("");
        }

        void Start() => Canvas.ForceUpdateCanvases();

        void OnEnable() => GameLoop.Register(this, TickGroup.Late, 61); // after LevelUpPanel (60) has read this frame's input

        void OnDisable()
        {
            GameLoop.Unregister(this);
            if (Instance == this) Instance = null;
        }

        bool Bind()
        {
            var p = Player.Main;
            if (p == null) return false;
            if (p != _player)
            {
                _player = p;
                _stats = p.GetComponent<PlayerStats>();
                _build = p.GetComponent<PlayerBuild>();
                _health = p.GetComponent<PlayerHealth>();
                _weapons = p.GetComponent<WeaponSystem>();
                _key = _promptKey = _tipKey = int.MinValue;
            }
            return _stats != null;
        }

        public void Hover(int id, bool on)
        {
            if (on) _hover = id;
            else if (_hover == id) _hover = -1;
        }

        public void Tick(float dt, float unscaledDt)
        {
            var run = RunState.Current;
            bool live = Bind() && run != null && !run.Ended && !RunState.CombatOver; // not through the death beat either
            var lvl = LevelUpController.Instance;
            var mode = Mode.Hidden;
            if (live && !OptionsPanel.IsOpen && (AdminController.Instance == null || !AdminController.Instance.IsOpen))
            {
                if (lvl != null && lvl.IsOpen && lvl.ChoiceCount > 0) mode = Mode.Pick;
                else if (_player.IsPaused) mode = Mode.Pause;
            }
            if (mode != Current)
            {
                Current = mode;
                Show(_group, mode != Mode.Hidden);
                _group.blocksRaycasts = mode == Mode.Pause; // the stat rows' tooltips: the pause only
                _key = int.MinValue;
                _hover = -1;
                _tipKey = int.MinValue;
                if (mode != Mode.Pick) Preview.Clear();
            }
            if (mode != Mode.Hidden)
            {
                Place(mode == Mode.Pick ? Mathf.Max(LevelUpLayout.PauseCards, lvl.ChoiceCount) : LevelUpLayout.PauseCards);
                int key = Key(mode, lvl);
                if (key != _key)
                {
                    _key = key;
                    if (mode == Mode.Pick) WritePick(lvl);
                    else WritePause();
                    Push(mode);
                    Rewrites++;
                }
            }
            DrawTip(mode);
            DrawPrompt(live && mode == Mode.Hidden && !TimeController.IsPaused, run);
        }

        /// <summary>Layout A's fit for `cards` (the pause: a three-card level-up's): right of the row, hanging from
        /// ColumnTop at the columns' scale. Only when the canvas or the card count changes.</summary>
        void Place(int cardCount)
        {
            var size = ((RectTransform)_canvas.transform).rect.size;
            if (size == _laidOutFor && cardCount == _laidCards) return;
            _laidOutFor = size;
            _laidCards = cardCount;
            if (size.x < 1f || size.y < 1f) size = MenuLayout.Reference;
            _fit = LevelUpLayout.For(size, cardCount);
            _pickScale = _fit.StatsScale;
            _box.localScale = new Vector3(_pickScale, _pickScale, 1f);
            _box.anchoredPosition = new Vector2(LevelUpLayout.ColumnLeft(size, _fit), LevelUpLayout.ColumnTop(size.y));
            _tipKey = int.MinValue; // re-place the tooltip
        }

        /// <summary>Everything the words depend on, folded into one number: they're rewritten only when this changes.</summary>
        int Key(Mode mode, LevelUpController lvl)
        {
            int k = (int)mode;
            k = k * 31 + _stats.Block.Version;
            if (_build != null) k = k * 31 + _build.State.Version;
            if (mode == Mode.Pick && lvl != null)
            {
                k = k * 31 + lvl.OffersVersion;
                k = k * 31 + (cards != null ? cards.Selected : 0);
            }
            k = k * 31 + AdminKey();
            return k;
        }

        static int AdminKey() => DevSettings.AnyBuff
            ? Mathf.RoundToInt(DevSettings.DamagePct + 3f * DevSettings.AttackSpeedPct + 7f * DevSettings.RangePct + 11f * DevSettings.MaxHpPct +
                               13f * DevSettings.MoveSpeedPct + 17f * DevSettings.PickupRangePct + 19f * DevSettings.CritPct)
            : 0;

        // ---------- the words: one buffer, a slice per label ----------

        void Begin(int slice) => _start[slice] = _text.Length;

        void End(int slice)
        {
            _len[slice] = _text.Length - _start[slice];
            _text.Append('\n');
        }

        /// <summary>Hands each label its slice of the buffer (and the names their colours).</summary>
        void Push(Mode mode)
        {
            var c = _text.Chars;
            _title.SetCharArray(c, _start[SliceTitle], _len[SliceTitle]);
            _sub.SetCharArray(c, _start[SliceSub], _len[SliceSub]);
            for (int i = 0; i < _rows; i++)
            {
                _names[i].SetCharArray(c, _start[SliceName(i)], _len[SliceName(i)]);
                _values[i].SetCharArray(c, _start[SliceValue(i)], _len[SliceValue(i)]);
                bool previewing = mode == Mode.Pick && Preview.Active;
                var nameColor = !previewing ? _cardC : Preview.Changed(StatText.Order[i]) ? _valueC : _dimC;
                if (_names[i].color != nameColor) _names[i].color = nameColor;
            }
        }

        void WriteHeadsAndNames()
        {
            int sections = StatText.SectionStart.Length;
            for (int sct = 0; sct < sections; sct++)
            {
                Begin(SliceHead + sct);
                _text.Append(StatText.SectionTitle[sct]);
                End(SliceHead + sct);
            }
        }

        // ---------- the Pick face: "your stats" with the selected card's preview ----------

        void WritePick(LevelUpController lvl)
        {
            var t = _text.Clear();
            int sel = cards != null ? cards.Selected : 0;
            if (sel >= 0 && sel < lvl.ChoiceCount)
                Preview.ForCard(lvl.Offers[sel], _stats, _build != null ? _build.State : null, _build != null ? _build.catalog : null,
                    lvl.Config, _weapons, _health, RunState.Current);
            else Preview.Clear();

            Begin(SliceTitle);
            t.Append(PickTitle);
            End(SliceTitle);
            Begin(SliceSub);
            int extra = FirstSupplyLine();
            if (Preview.Active && Preview.ChangedCount > 0) t.Append("if you take ").Append(lvl.Offers[sel].Def.displayName);
            else if (extra >= 0)
            {
                t.Append("if you take ").Append(lvl.Offers[sel].Def.displayName).Append(": ");
                StatPreview.LineText(t, Preview.Lines[extra]);
            }
            else t.Append(NoChange);
            End(SliceSub);
            WriteHeadsAndNames();
            for (int i = 0; i < _rows; i++)
            {
                var s = StatText.Order[i];
                bool previewing = Preview.Active;
                bool changed = previewing && Preview.Changed(s);
                Begin(SliceName(i));
                t.Append(StatInfo.Name(s));
                End(SliceName(i));
                Begin(SliceValue(i));
                if (changed) StatText.Change(t, s, Preview.Before[(int)s], Preview.After[(int)s]);
                else
                {
                    t.Append("<color=").Append(previewing ? StatText.Dim : StatText.ValueCol).Append('>');
                    StatText.Value(t, s, _stats.Get(s)).Append("</color>");
                }
                End(SliceValue(i));
            }
        }

        /// <summary>A supplies card's line (HP after the heal, gold after the purse): the line under the title shows it,
        /// so the rows never move. Weapon lines are left out (the inventory's ghost and tooltips show weapons).</summary>
        int FirstSupplyLine()
        {
            for (int i = 0; i < Preview.LineCount; i++)
            {
                var m = Preview.Lines[i].Metric;
                if (m == PreviewMetric.Hp || m == PreviewMetric.Gold) return i;
            }
            return -1;
        }

        // ---------- the Pause face: the gladiator, the seed, every stat's total ----------

        void WritePause()
        {
            var t = _text.Clear();
            var g = _stats.gladiator;
            Begin(SliceTitle);
            t.Append("<uppercase>").Append(g != null ? g.displayName : "Stats").Append("</uppercase>");
            End(SliceTitle);
            Begin(SliceSub);
            AppendSeed(t, GameRandom.RunSeed);
            t.Append(HoverHint);
            End(SliceSub);
            WriteHeadsAndNames();
            for (int i = 0; i < _rows; i++)
            {
                var s = StatText.Order[i];
                Begin(SliceName(i));
                t.Append(StatInfo.Name(s));
                End(SliceName(i));
                Begin(SliceValue(i));
                t.Append("<color=").Append(StatText.ValueCol).Append('>');
                StatText.Value(t, s, _stats.Get(s)).Append("</color>");
                End(SliceValue(i));
            }
        }

        /// <summary>"SEED 7F3A-91C2" (GameRandom.SeedText's words, built in the buffer).</summary>
        public static void AppendSeed(CharBuffer t, uint seed)
        {
            t.Append("SEED ");
            for (int i = 7; i >= 0; i--)
            {
                int d = (int)(seed >> (i * 4)) & 0xF;
                t.Append((char)(d < 10 ? '0' + d : 'A' + d - 10));
                if (i == 4) t.Append('-');
            }
        }

        // ---------- the pause's tooltips: where a stat comes from, the gladiator's traits ----------

        void DrawTip(Mode mode)
        {
            int hover = mode == Mode.Pause ? _hover : -1;
            int key = hover < 0 ? -1 : hover * 7919 + _stats.Block.Version * 31 + AdminKey() + (_build != null ? _build.State.Version * 131 : 0);
            if (key == _tipKey) return;
            _tipKey = key;
            if (hover < 0 || (hover != TitleHover && hover >= _rows))
            {
                _tip.Hide();
                _rowLit.color = new Color(1f, 1f, 1f, 0f);
                return;
            }
            float rowTop;
            if (hover == TitleHover)
            {
                if (!WriteTraits()) { _tip.Hide(); _rowLit.color = new Color(1f, 1f, 1f, 0f); return; }
                rowTop = _titleTop;
                _rowLit.rectTransform.anchoredPosition = new Vector2(_rowLit.rectTransform.anchoredPosition.x, -_titleTop);
                _rowLit.rectTransform.sizeDelta = new Vector2(_rowLit.rectTransform.sizeDelta.x, InventoryPanel.TitleH);
            }
            else
            {
                WriteSources(StatText.Order[hover]);
                rowTop = _rowTop[hover];
                _rowLit.rectTransform.anchoredPosition = new Vector2(_rowLit.rectTransform.anchoredPosition.x, -rowTop);
                _rowLit.rectTransform.sizeDelta = new Vector2(_rowLit.rectTransform.sizeDelta.x, RowH);
            }
            _rowLit.color = new Color(1f, 1f, 1f, 0.09f);
            _tip.Apply();
            // left of the column, its top level with the row (clamped on the screen)
            var size = _laidOutFor.x > 1f ? _laidOutFor : MenuLayout.Reference;
            float colLeft = LevelUpLayout.ColumnLeft(size, _fit) - size.x * 0.5f;
            float y = LevelUpLayout.ColumnTop(size.y) - rowTop * _pickScale;
            _tip.Place(new Vector2(colLeft - TipGap * _pickScale, y), new Vector2(1f, 1f), _pickScale, size);
        }

        /// <summary>The stat's total, then one line per source: "Base 100", "Thick Skin ×2 +40", "Blood Altar −21".</summary>
        void WriteSources(StatId s)
        {
            _tip.Title.Clear().Append(StatInfo.Name(s));
            StatText.Value(_tip.Sub.Clear().Append("total "), s, _stats.Get(s));
            var t = _tip.Body.Clear();
            int n = _stats.Breakdown(s, _shares);
            int lines = 0;
            for (int i = 0; i < n; i++)
            {
                var sh = _shares[i];
                if (i > 0 && !StatText.Differs(s, sh.Amount, 0f)) continue;
                if (sh.Source.Kind == StatSourceKind.Admin && !StatText.AdminVisible) continue;
                if (lines++ > 0) t.Append('\n');
                t.Append("<color=").Append(StatText.SourceColor(sh.Source)).Append('>').Append(StatText.SourceName(sh.Source, _stats.gladiator));
                if (sh.Source.Kind == StatSourceKind.Card && sh.Count > 1) t.Append(" ×").Append(sh.Count, 0);
                t.Append("</color>  ");
                if (i == 0) StatText.Value(t, s, sh.Amount);
                else StatText.Delta(t, s, sh.Amount);
            }
            if (lines <= 1)
            {
                if (lines > 0) t.Append('\n');
                t.Append("<color=").Append(StatText.Dim).Append(">nothing else changes it yet</color>");
            }
        }

        /// <summary>The gladiator's traits, each name then what it does. False if there are none.</summary>
        bool WriteTraits()
        {
            var g = _stats.gladiator;
            if (g == null || g.traits == null || g.traits.Length == 0) return false;
            _tip.Title.Clear().Append(g.displayName);
            _tip.Sub.Clear().Append("TRAITS");
            var t = _tip.Body.Clear();
            for (int i = 0; i < g.traits.Length; i++)
            {
                var tr = g.traits[i];
                if (tr == null) continue;
                if (t.Length > 0) t.Append('\n');
                t.Append("<color=").Append(StatText.TraitCol).Append('>').Append(tr.displayName).Append("</color>");
                if (!string.IsNullOrEmpty(tr.description)) t.Append('\n').Append(tr.description);
            }
            return true;
        }

        /// <summary>Swaps every "&lt;color=#RRGGBB" whose hex is in `from` for the same-index `to`, in place (no allocation).</summary>
        public static void Remap(CharBuffer t, string[] from, string[] to)
        {
            var c = t.Chars;
            int n = t.Length;
            for (int i = 1; i + 7 <= n; i++)
            {
                if (c[i] != '#' || c[i - 1] != '=') continue;
                for (int k = 0; k < from.Length; k++)
                {
                    var f = from[k];
                    if (f.Length != 7 || to[k].Length != 7) continue;
                    int j = 1;
                    while (j < 7 && char.ToUpperInvariant(c[i + j]) == char.ToUpperInvariant(f[j])) j++;
                    if (j < 7) continue;
                    for (j = 1; j < 7; j++) c[i + j] = to[k][j];
                    break;
                }
                i += 6;
            }
        }

        // ---------- the Prompt face: the altar's and chests' cost ----------

        void DrawPrompt(bool allowed, RunState run)
        {
            var props = Interactables.Instance;
            int reach = allowed && props != null ? props.InReach : -1;
            int key = -1;
            bool altar = false;
            int price = 0;
            if (reach >= 0)
            {
                altar = props.KindOf(reach) == Interactables.Kind.Altar;
                price = props.ChestPrice;
                if (altar) key = 1 + _stats.Block.Version * 7;
                else if (run.Gold >= price) key = 2 + run.Gold * 7 + price * 7919;
            }
            bool on = key >= 0;
            if (on != PromptShown) { PromptShown = on; Show(_promptGroup, on); }
            if (!on || key == _promptKey) return;
            _promptKey = key;
            var t = _promptText.Clear();
            if (altar && props.config != null)
            {
                PromptPreview.ForAltar(_stats, props.config.altarMaxHpCost);
                t.Append("<color=").Append(StatText.AltarCol).Append(">BLOOD ALTAR</color>   ");
                StatText.StatLine(t, _stats, StatId.MaxHp, PromptPreview, false, _shares, null);
            }
            else
            {
                PromptPreview.ForGold(_stats, run.Gold, run.Gold - price);
                StatPreview.LineText(t, PromptPreview.Lines[0]);
            }
            _prompt.SetCharArray(t.Chars, 0, t.Length);
        }

        // ---------- tests ----------

        /// <summary>Tests: the title's and the line-under-it's words, a stat row's value words and its label.</summary>
        public string TitleText => _len != null ? new string(_text.Chars, _start[SliceTitle], _len[SliceTitle]) : "";
        public string SubText => _len != null ? new string(_text.Chars, _start[SliceSub], _len[SliceSub]) : "";
        public string ValueText(StatId s)
        {
            int i = RowOf(s);
            return i < 0 ? "" : new string(_text.Chars, _start[SliceValue(i)], _len[SliceValue(i)]);
        }
        /// <summary>Tests: stat `s`'s row (its name label's rect: fixed, whatever the card).</summary>
        public RectTransform RowRect(StatId s)
        {
            int i = RowOf(s);
            return i < 0 ? null : _names[i].rectTransform;
        }
        public static int RowOf(StatId s)
        {
            for (int i = 0; i < StatText.Order.Length; i++) if (StatText.Order[i] == s) return i;
            return -1;
        }
        /// <summary>Tests: as the mouse entering (on) or leaving stat `s`'s row (the pause's tooltips).</summary>
        public void HoverRow(StatId s, bool on) => Hover(RowOf(s), on);
        public void HoverTitle(bool on) => Hover(TitleHover, on);
        public UiTooltip Tooltip => _tip;
        public bool TooltipShown => _tip != null && _tip.Shown;
        public string TooltipText => _tip != null ? _tip.Text : "";
    }
}
