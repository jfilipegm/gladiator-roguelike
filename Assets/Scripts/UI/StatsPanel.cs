using Gladiator.Core;
using Gladiator.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gladiator.UI
{
    /// <summary>
    /// P1 stats page (findings 10, 10b). Three faces, one panel:
    /// - Pause: on the right, every stat's total and where it comes from (base, each card, synergies, traits and their
    ///   live bonuses, the admin layer in dev builds), the gladiator's traits and their weapons' numbers.
    /// - Pick (level-up, chest, altar, boss cards): a narrower "your stats" column beside the cards; the selected card
    ///   (mouse hover, keys or gamepad) previews each stat it changes, "Damage 117% → 134%", gains green, losses red.
    /// - Prompt: walking up to the Blood Altar or an Armory Chest shows what it costs ("Max HP 120 → 102").
    /// Created by LevelUpPanel (so every arena scene has one without a scene rebuild). Built once; the text is rewritten
    /// only when what it shows changes (stats, offers, selection) into a char buffer handed to TMP: no allocation per
    /// frame. Hidden with a CanvasGroup, never SetActive (LevelUpPanel's note on canvas rebuilds).
    /// R8: the Pause face's header names the run's seed ("SEED 7F3A-91C2"), written digit by digit (no allocation).
    /// U1: the Pick and Prompt faces sit on riveted bronze, the Pause face on carved marble (UiFrames, one Image whose
    /// frame changes with the face). The page is one Body (Marcellus) label; on marble its colour tags are swapped in
    /// place for darker inks of the same hues (MarbleInks: same length, no allocation), the text itself unchanged.
    /// Layout A: the Pick column is 410 wide, hangs from LevelUpLayout.ColumnTop and scales by SetPickScale.
    /// </summary>
    public sealed class StatsPanel : MonoBehaviour, ITickable
    {
        public enum Mode : byte { Hidden, Pause, Pick }

        public static StatsPanel Instance { get; private set; }

        public TMP_FontAsset font;
        public Material fontMaterial;
        [Tooltip("The level-up cards whose selection this previews (LevelUpPanel sets itself).")]
        public LevelUpPanel cards;

        public const float PageWidth = 560f, PickWidth = LevelUpLayout.StatsW, Margin = 24f;
        const float PageHeight = 920f, PromptWidth = 680f, PromptHeight = 60f;
        // the text's insets inside the frame, at least (left, bottom, right, top); the frame's padding wins if larger
        static readonly Vector4 MinInset = new Vector4(20f, 16f, 18f, 16f);

        Canvas _canvas;
        CanvasGroup _group, _promptGroup;
        RectTransform _box, _promptBox;
        Image _frame, _promptFrame;
        TextMeshProUGUI _body, _prompt;
        Color _ink, _marbleInk;
        float _pickScale = 1f;
        readonly CharBuffer _text = new CharBuffer(6144);
        readonly CharBuffer _promptText = new CharBuffer(160);
        readonly StatShare[] _shares = new StatShare[24];

        /// <summary>The preview the Pick face shows (tests read it before and after a pick).</summary>
        public StatPreview Preview { get; } = new StatPreview();
        /// <summary>The Prompt face's preview (altar, chest).</summary>
        public StatPreview PromptPreview { get; } = new StatPreview();
        public Mode Current { get; private set; }
        public bool PromptShown { get; private set; }
        /// <summary>Tests: the page's text and the prompt's, as shown.</summary>
        public string Text => _text.ToString();
        public string PromptText => _promptText.ToString();
        /// <summary>How many times the page's text has been rewritten (tests: it must not rewrite every frame).</summary>
        public int Rewrites { get; private set; }
        /// <summary>The page's box (tests: the cards never run under it).</summary>
        public RectTransform Page => _box;
        /// <summary>The width the Pick face takes from the right of the screen at its current scale.</summary>
        public float PickReserve => PickWidth * _pickScale + Margin;
        /// <summary>The Pick face's scale (LevelUpLayout: 0.7-1).</summary>
        public float PickScale => _pickScale;
        /// <summary>Tests: the FrameKind the page shows (Bronze for Pick, Marble for Pause; UiFrames.KindOf), and the prompt's.</summary>
        public int FrameShown => UiFrames.KindOf(_frame);
        public int PromptFrameShown => UiFrames.KindOf(_promptFrame);
        public Component BodyLabel => _body;

        /// <summary>Layout A: the level-up scales the Pick face to fit the right margin (it hangs from its top-right corner).</summary>
        public void SetPickScale(float k)
        {
            _pickScale = Mathf.Clamp(k, 0.1f, 1f);
            if (_layoutMode == Mode.Pick) _box.localScale = new Vector3(_pickScale, _pickScale, 1f);
        }

        int _key = int.MinValue, _promptKey = int.MinValue;
        Mode _layoutMode = (Mode)255;

        PlayerStats _stats;
        PlayerBuild _build;
        PlayerHealth _health;
        WeaponSystem _weapons;
        Player _player;

        void Awake()
        {
            Instance = this;
            var th = UiTheme.Current;
            _ink = th.ink;
            _marbleInk = th.marbleInk;
            _canvas = UiFactory.Canvas("StatsCanvas", transform, UiSortOrder.LevelUp + 1); // over the card dim, under menus
            var root = _canvas.transform;
            var right = new Vector2(1f, 0.5f);
            var mid = new Vector2(0.5f, 0.5f);
            _box = UiFactory.Rect("Page", root, right, right, new Vector2(-Margin, -16f), new Vector2(PageWidth, PageHeight));
            _frame = UiKit.Frame("Frame", _box, FrameKind.Bronze, mid, mid, Vector2.zero, Vector2.zero, Color.white);
            UiFactory.Stretch(_frame.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _body = UiKit.Text("Body", _box, "", TextRole.Body, 21f, _ink, TextAlignmentOptions.TopLeft, mid, mid, Vector2.zero, Vector2.zero);
            StyleBody(TextLook.Flat);
            Inset(_body.rectTransform, FrameKind.Bronze);
            _group = _box.gameObject.AddComponent<CanvasGroup>();

            var bottom = new Vector2(0.5f, 0f);
            _promptBox = UiFactory.Rect("Prompt", root, bottom, bottom, new Vector2(0f, 196f), new Vector2(PromptWidth, PromptHeight));
            _promptFrame = UiKit.Frame("Frame", _promptBox, FrameKind.Bronze, mid, mid, Vector2.zero, Vector2.zero, Color.white);
            UiFactory.Stretch(_promptFrame.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _prompt = UiKit.Text("Text", _promptBox, "", TextRole.Body, 28f, _ink, TextAlignmentOptions.Center, mid, mid, Vector2.zero, Vector2.zero);
            UiFactory.Stretch(_prompt.rectTransform, Vector2.zero, Vector2.one, new Vector2(18f, 0f), new Vector2(-18f, 0f));
            _prompt.richText = true;
            _prompt.enableAutoSizing = true; // "BLOOD ALTAR   Max HP 120 → 102" in Marcellus: shrink before it's cut
            _prompt.fontSizeMin = 20f;
            _prompt.fontSizeMax = 28f;
            _promptGroup = _promptBox.gameObject.AddComponent<CanvasGroup>();

            foreach (var g in new[] { _group, _promptGroup }) { g.blocksRaycasts = false; g.interactable = false; }
            Presize();
            Show(_group, false);
            Show(_promptGroup, false);
        }

        /// <summary>The page's look (Flat on bronze, Engraved on marble), then its own rules back (UiKit.Style sets NoWrap).</summary>
        void StyleBody(TextLook look)
        {
            UiKit.Style(_body, TextRole.Body, look);
            _body.richText = true;
            _body.textWrappingMode = TextWrappingModes.Normal;
            _body.overflowMode = TextOverflowModes.Truncate;
            _body.enableAutoSizing = true; // a late-run build has many sources: shrink to fit, never below 13 (a dense data page)
            _body.fontSizeMin = 13f;
            _body.fontSizeMax = 21f;
        }

        /// <summary>Stretch `rt` over its parent, inset by the frame's padding (at least MinInset).</summary>
        static void Inset(RectTransform rt, FrameKind kind)
        {
            var pad = UiFrames.Padding(kind);
            UiFactory.Stretch(rt, Vector2.zero, Vector2.one,
                new Vector2(Mathf.Max(pad.x, MinInset.x), Mathf.Max(pad.y, MinInset.y)),
                new Vector2(-Mathf.Max(pad.z, MinInset.z), -Mathf.Max(pad.w, MinInset.w)));
        }

        static void Show(CanvasGroup g, bool on) { if (g.alpha != (on ? 1f : 0f)) g.alpha = on ? 1f : 0f; }

        /// <summary>
        /// Grow the TMP buffers once past the longest page, with every glyph the page uses (→ and − come from the
        /// dynamic fallback font: adding them now keeps the first preview from growing its atlas mid-run).
        /// </summary>
        void Presize()
        {
            const string Glyphs = "ABCDEFGHIJKLMNOPQRSTUVWXYZ abcdefghijklmnopqrstuvwxyz 0123456789 +-−→×·%/()[]:'’!?.,&—";
            var sb = new System.Text.StringBuilder(_text.Chars.Length + 64);
            while (sb.Length < _text.Chars.Length) sb.Append(Glyphs).Append('\n');
            _body.SetText(sb.ToString());
            _body.ForceMeshUpdate(true, true);
            sb.Length = 0;
            while (sb.Length < _promptText.Chars.Length) sb.Append(Glyphs);
            _prompt.SetText(sb.ToString());
            _prompt.ForceMeshUpdate(true, true);
            _body.SetText("");
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
                _key = _promptKey = int.MinValue;
            }
            return _stats != null;
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
                _key = int.MinValue;
                if (mode != Mode.Pick) Preview.Clear();
            }
            if (mode != Mode.Hidden)
            {
                int key = Key(mode, lvl);
                if (key != _key)
                {
                    _key = key;
                    Layout(mode);
                    if (mode == Mode.Pick) WritePick(lvl);
                    else { WritePage(); Remap(_text, StatText_Colors, MarbleInks); } // the page's colours, readable on marble
                    _body.SetCharArray(_text.Chars, 0, _text.Length);
                    Rewrites++;
                }
            }
            DrawPrompt(live && mode == Mode.Hidden && !TimeController.IsPaused, run);
        }

        /// <summary>Everything the text depends on, folded into one number: it's rewritten only when this changes.</summary>
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
            k = k * 31 + (DevSettings.AnyBuff ? Mathf.RoundToInt(DevSettings.DamagePct + 3f * DevSettings.AttackSpeedPct + 7f * DevSettings.RangePct +
                                                                  11f * DevSettings.MaxHpPct + 13f * DevSettings.MoveSpeedPct + 17f * DevSettings.PickupRangePct + 19f * DevSettings.CritPct) : 0);
            if (_weapons != null) k = k * 31 + _weapons.Weapons.Count + (_weapons.Temporary != null ? 100 + Mathf.CeilToInt(_weapons.TemporaryLeft) : 0);
            return k;
        }

        /// <summary>
        /// Pick: the 410-wide bronze column hanging from LevelUpLayout.ColumnTop at the Pick scale. Pause: the 560-wide
        /// marble page, centred on the right edge. Only when the face changes.
        /// </summary>
        void Layout(Mode mode)
        {
            if (mode == _layoutMode) return;
            _layoutMode = mode;
            bool pick = mode == Mode.Pick;
            var kind = pick ? FrameKind.Bronze : FrameKind.Marble;
            UiFrames.Apply(_frame, kind);
            Inset(_body.rectTransform, kind);
            StyleBody(pick ? TextLook.Flat : TextLook.Engraved);
            _body.color = pick ? _ink : _marbleInk;
            float h = ((RectTransform)_canvas.transform).rect.height;
            _box.pivot = pick ? new Vector2(1f, 1f) : new Vector2(1f, 0.5f);
            _box.sizeDelta = pick ? new Vector2(PickWidth, LevelUpLayout.StatsH) : new Vector2(PageWidth, PageHeight);
            _box.anchoredPosition = pick ? new Vector2(-Margin, LevelUpLayout.ColumnTop(h)) : new Vector2(-Margin, -16f);
            float k = pick ? _pickScale : 1f;
            _box.localScale = new Vector3(k, k, 1f);
            _body.fontSizeMax = pick ? 22f : 21f;
        }

        // StatText's colours are made for a dark page; on marble each becomes a darker ink of its hue (same 7 characters)
        static readonly string[] StatText_Colors =
        {
            StatText.ValueCol, StatText.CardCol, StatText.Dim, StatText.Head, StatText.Gain, StatText.Loss, StatText.SynergyCol,
            StatText.TraitCol, StatText.TempCol, StatText.AltarCol, StatText.AdminCol, StatText.VowCol, StatText.EdictCol,
        };
        static readonly string[] MarbleInks =
        {
            "#3D342B", "#3D342B", "#6E6253", "#5A3A1B", "#2F7A3A", "#A8321F", "#1D6A78",
            "#8A5A10", "#9A4A0E", "#9B2E4E", "#8E2A80", "#9B3A26", "#C23A1E", // edicts stay orange-red: they hurt you
        };

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

        // ---------- the Pick face: a "your stats" column with the selected card's preview ----------

        void WritePick(LevelUpController lvl)
        {
            var t = _text.Clear();
            int sel = cards != null ? cards.Selected : 0;
            if (sel >= 0 && sel < lvl.ChoiceCount)
                Preview.ForCard(lvl.Offers[sel], _stats, _build != null ? _build.State : null, _build != null ? _build.catalog : null,
                    lvl.Config, _weapons, _health, RunState.Current);
            else Preview.Clear();

            t.Append("<size=127%><color=").Append(StatText.Head).Append(">YOUR STATS</color></size>\n");
            t.Append("<size=77%><color=").Append(StatText.Dim).Append('>');
            if (Preview.Active && (Preview.ChangedCount > 0 || Preview.LineCount > 0)) t.Append("if you take ").Append(lvl.Offers[sel].Def.displayName);
            else t.Append("no stat changes (its effect is on the card)");
            t.Append("</color></size>\n");
            for (int i = 0; i < Preview.LineCount; i++)
            {
                t.Append("<size=86%>");
                StatPreview.LineText(t, Preview.Lines[i]);
                t.Append("</size>\n");
            }
            for (int sct = 0; sct < StatText.SectionStart.Length; sct++)
            {
                int from = StatText.SectionStart[sct], to = sct + 1 < StatText.SectionStart.Length ? StatText.SectionStart[sct + 1] : StatText.Order.Length;
                t.Append("<size=36%>\n</size><size=77%><color=").Append(StatText.Head).Append('>').Append(StatText.SectionTitle[sct]).Append("</color></size>\n");
                for (int i = from; i < to; i++)
                {
                    StatText.StatLine(t, _stats, StatText.Order[i], Preview, false, _shares, "52%");
                    t.Append('\n');
                }
            }
        }

        // ---------- the Pause face: every stat and its sources ----------

        void WritePage()
        {
            var t = _text.Clear();
            var g = _stats.gladiator;
            t.Append("<size=143%><color=").Append(StatText.Head).Append('>').Append("<uppercase>").Append(g != null ? g.displayName : "Stats").Append("</uppercase></color></size>\n");
            t.Append("<size=81%><color=").Append(StatText.Dim).Append(">every stat, then where it comes from<pos=68%>");
            AppendSeed(t, GameRandom.RunSeed);
            t.Append("</color></size>\n");
            if (g != null && g.traits != null && g.traits.Length > 0)
            {
                t.Append("<size=86%><color=").Append(StatText.TraitCol).Append(">Traits: ");
                for (int i = 0; i < g.traits.Length; i++)
                {
                    if (g.traits[i] == null) continue;
                    if (i > 0) t.Append(" · ");
                    t.Append(g.traits[i].displayName);
                }
                t.Append("</color></size>\n");
            }
            WriteWeapons(t);
            for (int sct = 0; sct < StatText.SectionStart.Length; sct++)
            {
                int from = StatText.SectionStart[sct], to = sct + 1 < StatText.SectionStart.Length ? StatText.SectionStart[sct + 1] : StatText.Order.Length;
                t.Append("<size=38%>\n</size><size=90%><color=").Append(StatText.Head).Append('>').Append(StatText.SectionTitle[sct]).Append("</color></size>\n");
                for (int i = from; i < to; i++)
                {
                    StatText.StatLine(t, _stats, StatText.Order[i], null, true, _shares, "62%");
                    t.Append('\n');
                }
            }
            if (DevSettings.AnyBuff && StatText.AdminVisible)
                t.Append("<size=76%><color=").Append(StatText.AdminCol).Append(">ADMIN buffs (F1) are on: they're the pink shares.</color></size>");
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

        void WriteWeapons(CharBuffer t)
        {
            if (_weapons == null) return;
            var list = _weapons.Weapons;
            if (list.Count == 0 && _weapons.Temporary == null) return;
            t.Append("<size=38%>\n</size><size=90%><color=").Append(StatText.Head).Append(">WEAPONS</color></size>\n<size=86%>");
            for (int i = 0; i < list.Count; i++) Weapon(t, list[i], false);
            var loan = _weapons.Temporary;
            if (loan != null) Weapon(t, loan, true);
            t.Append("</size>");
        }

        void Weapon(CharBuffer t, WeaponInstance w, bool loan)
        {
            if (w == null || w.Def == null) return;
            bool evo = IsEvolution(w.Def);
            t.Append("<color=").Append(evo ? StatText.TraitCol : StatText.CardCol).Append('>').Append(w.Def.displayName).Append("</color><pos=50%>");
            if (loan) t.Append("<color=").Append(StatText.TempCol).Append(">gift ").Append(Mathf.Ceil(_weapons.TemporaryLeft), 0).Append(" s</color>");
            else if (evo) t.Append("<color=").Append(StatText.TraitCol).Append(">EVOLVED</color>");
            else t.Append("Lv ").Append(w.Level, 0);
            t.Append("<pos=72%>");
            float hit = _weapons.Damage(w);
            if (hit > 0f) t.Append(hit, hit >= 10f ? 0 : 1).Append(" · ");
            t.Append(1f / Mathf.Max(0.01f, _weapons.CooldownOf(w)), 2).Append("/s\n");
        }

        bool IsEvolution(WeaponDef def)
        {
            if (_build == null) return false;
            var s = _build.State;
            for (int i = 0; i < s.OwnedCount; i++)
            {
                var d = s.GetOwned(i).Def;
                if (d != null && d.evolvesFrom != null && d.weapon == def) return true;
            }
            return false;
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
    }
}
