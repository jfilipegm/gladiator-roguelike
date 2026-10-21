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
    /// </summary>
    public sealed class StatsPanel : MonoBehaviour, ITickable
    {
        public enum Mode : byte { Hidden, Pause, Pick }

        public static StatsPanel Instance { get; private set; }

        public TMP_FontAsset font;
        public Material fontMaterial;
        [Tooltip("The level-up cards whose selection this previews (LevelUpPanel sets itself).")]
        public LevelUpPanel cards;

        public const float PageWidth = 560f, PickWidth = 380f, Margin = 24f;

        static readonly Color Back = new Color(0.05f, 0.06f, 0.09f, 0.9f);
        static readonly Color Edge = new Color(1f, 0.84f, 0.3f, 0.55f);
        static readonly Color PromptBack = new Color(0.05f, 0.06f, 0.09f, 0.82f);

        Canvas _canvas;
        CanvasGroup _group, _promptGroup;
        RectTransform _box, _promptBox;
        Image _edge;
        TextMeshProUGUI _body, _prompt;
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
        /// <summary>The width the Pick face takes from the right of the screen (LevelUpPanel moves its cards left by it).</summary>
        public float PickReserve => PickWidth + Margin;

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
            _canvas = UiFactory.Canvas("StatsCanvas", transform, UiSortOrder.LevelUp + 1); // over the card dim, under menus
            var root = _canvas.transform;
            var right = new Vector2(1f, 0.5f);
            _box = UiFactory.Rect("Page", root, right, right, new Vector2(-Margin, -10f), new Vector2(PageWidth, 900f));
            _edge = UiFactory.Image("Edge", _box, Edge, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, PixelIcons.White);
            UiFactory.Stretch(_edge.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var bg = UiFactory.Image("Back", _box, Back, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, PixelIcons.White);
            UiFactory.Stretch(bg.rectTransform, Vector2.zero, Vector2.one, new Vector2(3f, 3f), new Vector2(-3f, -3f));
            _body = UiFactory.Text("Body", _box, "", 21, Color.white, TextAlignmentOptions.TopLeft, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, font, fontMaterial);
            UiFactory.Stretch(_body.rectTransform, Vector2.zero, Vector2.one, new Vector2(20f, 16f), new Vector2(-18f, -16f));
            _body.fontStyle = FontStyles.Normal;
            _body.richText = true;
            _body.textWrappingMode = TextWrappingModes.Normal;
            _body.overflowMode = TextOverflowModes.Truncate;
            _body.enableAutoSizing = true; // a late-run build has many sources: shrink to fit, never below 13
            _body.fontSizeMin = 13f;
            _body.fontSizeMax = 21f;
            _group = _box.gameObject.AddComponent<CanvasGroup>();

            var bottom = new Vector2(0.5f, 0f);
            _promptBox = UiFactory.Rect("Prompt", root, bottom, bottom, new Vector2(0f, 196f), new Vector2(640f, 46f));
            var pbg = UiFactory.Image("Back", _promptBox, PromptBack, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, PixelIcons.White);
            UiFactory.Stretch(pbg.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _prompt = UiFactory.Text("Text", _promptBox, "", 28, Color.white, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, font, fontMaterial);
            UiFactory.Stretch(_prompt.rectTransform, Vector2.zero, Vector2.one, new Vector2(10f, 0f), new Vector2(-10f, 0f));
            _prompt.richText = true;
            _promptGroup = _promptBox.gameObject.AddComponent<CanvasGroup>();

            foreach (var g in new[] { _group, _promptGroup }) { g.blocksRaycasts = false; g.interactable = false; }
            Presize();
            Show(_group, false);
            Show(_promptGroup, false);
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
            bool live = Bind() && run != null && !run.Ended;
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
                    else WritePage();
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

        void Layout(Mode mode)
        {
            if (mode == _layoutMode) return;
            _layoutMode = mode;
            bool pick = mode == Mode.Pick;
            _box.sizeDelta = new Vector2(pick ? PickWidth : PageWidth, pick ? 760f : 920f);
            _box.anchoredPosition = new Vector2(-Margin, pick ? 0f : -16f);
            _body.fontSizeMax = pick ? 22f : 21f;
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
            t.Append("<size=81%><color=").Append(StatText.Dim).Append(">every stat, then where it comes from</color></size>\n");
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
