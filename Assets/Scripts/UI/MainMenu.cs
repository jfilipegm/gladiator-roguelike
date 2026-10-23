using System;
using Gladiator.Core;
using Gladiator.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Gladiator.UI
{
    /// <summary>
    /// Main menu → gladiator select → arena select (Phase 5.4, only when two or more arenas are earned) → the run
    /// (M3.5), and the Codex (M3.6: records, unlocks, gladiators; CodexPanel).
    /// R9 (the producer's playtest: the sliding card row stranded the mouse): the gladiator select is a grid of tessera
    /// tiles on the left (RosterGrid: every gladiator, the earned ones first, locked ones as dark silhouettes), the 3D
    /// diorama in the middle, and the lanista's wax tablet on the right (GladiatorTablet). The arena select is the climb
    /// (ArenaLadder: steps from the Slave Pit at the bottom to the newest at the top) beside its tablet page. A locked tile
    /// or step can be picked to read its hint; FIGHT! waits for an earned one, and the diorama keeps the last earned
    /// gladiator. Nothing slides: every block is anchored to its edge and scaled to fit (MenuLayout), re-fitted when the
    /// window changes size. `locked` lists gladiators that don't exist as data yet. Renown from the save shows in the corner.
    /// Mouse (click picks, a second click fights), keyboard (arrows/WASD move in 2D, Enter / Space fight, Esc back) and
    /// gamepad (d-pad, South, East). Under -bot the run starts at once (`-gladiator &lt;id&gt;` picks who and
    /// `-arena &lt;run id&gt;` where, earned or not).
    /// R7: once the Edicts are earned (The Editor Takes Notice), the arena screen always shows, with an EDICTS button
    /// between BACK and FIGHT! ("EDICTS · RENOWN ×1.65"; E / gamepad North) that opens the EdictsPanel over it; input goes
    /// to the panel while it's open. FIGHT! starts the run under the chosen edicts (`-edicts a,b` for bot runs: any).
    /// R8: the Daily Games. DAILY is the second button (PLAY · DAILY · CODEX · OPTIONS · QUIT): greyed with its unlock's
    /// hint until Gorm falls (MetaConfig.dailyUnlock), a gold dot while today's scored attempt is still open. It opens the
    /// Daily screen (DailyPanel: the day's arena, gladiator and edicts, the attempt, the clock, the ledger); FIGHT! there
    /// starts the plan, skipping both select screens (the first attempt of the UTC day is scored, later ones practice),
    /// and the edicts block (E / gamepad North) opens the Edicts page read-only. `-daily yyyymmdd` starts a practice daily
    /// of that date at once (bot and perf runs; it never spends the save's attempt).
    /// R6: the skins. Under the diorama the SkinStrip shows the picked gladiator's skins ("◀ FREEDMAN · 2 / 3 ▶"):
    /// Q / E (LB / RB, or its arrows) cycle them on the diorama, an owned one is equipped at once, F / gamepad North (or
    /// its button) buys a Market one. The roster tiles show each gladiator's equipped skin, and FIGHT! (and the daily's)
    /// fights in it (`-skin &lt;id&gt;` for bot runs: any skin, owned or not; it picks its gladiator when -gladiator doesn't).
    /// U1 (docs/tracks/U1-design.md §5): the Roman look. The title on a mosaic plaque over a gold meander; the buttons a
    /// column of tabulae ansatae (PLAY bronze, the rest marble) whose focus is the plate's gold ring; Renown on a bronze
    /// plate. Every screen's header in Title over a meander, its key line in Body, BACK marble and FIGHT! bronze. The
    /// Codex, the Edicts page and the Daily screen scale with UiKit.FitScale. Words, keys and test hooks unchanged.
    /// P3 (the producer's v0.24.0 notes, docs/tracks/P3-notes.md): the title is the word itself in gold over the sky
    /// between laurels, a thin gold meander under it and the line under that (no mosaic box); "RENOWN" in marble on its
    /// bronze plate; the CODEX plate wears a crimson NEW seal on its top-right corner instead of "• n NEW"; the plates'
    /// focus is the ansata's glow (UiKit). The arena screen is a grid of arena tiles (ArenaLadder, three to a row) moved
    /// in 2D like the gladiator grid. Every label sits inside its frame's content rect (UiAudit).
    /// </summary>
    public sealed class MainMenu : MonoBehaviour
    {
        [Serializable]
        public struct Locked
        {
            public string name;
            [TextArea] public string hint;
        }

        public TMP_FontAsset font;
        public Material fontMaterial;
        [Tooltip("The default run (the Slave Pit) when `runs` is empty.")]
        public RunDef run;
        [Tooltip("Every arena the menu can offer, in ladder order. Locked ones show their unlock's hint.")]
        public RunDef[] runs = Array.Empty<RunDef>();
        public MetaConfig meta;
        [Tooltip("The Codex's Bestiary, portraits and Arsenal source (Phase 5.6).")]
        public CodexConfig codex;
        public GladiatorDef[] gladiators = Array.Empty<GladiatorDef>();
        public Locked[] locked = Array.Empty<Locked>();
        public string title = "GLADIATOR";
        public string subtitle = "from the pit to the pantheon";
        [Tooltip("The gladiator standing in the diorama: swapped for the picked one's model.")]
        public Transform hero;
        [Tooltip("Palette material for the swapped models (they import without materials).")]
        public Material heroMaterial;

        /// <summary>The gold of a dot or a multiplier written on a marble plate (the bright gold doesn't read on marble).</summary>
        const string MarbleGoldHex = "#7E5514"; // P3: #A8741F was 2.7:1 on the marble plate; 4.6:1 now
        /// <summary>The orange-red of what hurts you, written on marble: darker, so it reads there (4.3:1; the bright one is 2.6).</summary>
        public const string MarbleHurtHex = "#B8361C";
        /// <summary>P3: the title's size (it was 140 on the mosaic plaque).</summary>
        const float TitleSize = 92f;
        /// <summary>P3: the Renown plate (tall enough that both words sit inside its content rect).</summary>
        static readonly Vector2 RenownPlateSize = new Vector2(340f, 86f);

        enum Screen { Menu, Select, Arena, Codex, Daily }

        CanvasGroup _menu, _select, _arena, _codex, _daily;
        Button[] _menuButtons;
        UiPlate[] _menuPlates;
        int _focus, _shownFocus = -1;
        bool _shownDailyOpen;
        Screen _screen;
        readonly System.Collections.Generic.Dictionary<GameObject, GameObject> _heroModels = new System.Collections.Generic.Dictionary<GameObject, GameObject>();
        int _screenW, _screenH;

        // the gladiator select: grid slots are the earned gladiators (in `_available`'s order) then the locked ones, so
        // slot i < _available.Length is _available[i]
        GladiatorDef[] _available = Array.Empty<GladiatorDef>();
        int _pick;    // into _available: who FIGHT! takes and the diorama shows
        int _slot;    // the grid's cursor (may rest on a locked tile)
        RosterGrid _roster;
        GladiatorTablet _tablet;
        Button _fight;

        // the arena select: ladder steps in ladder order; `_openRuns` are the earned ones in the same order
        RunDef[] _openRuns = Array.Empty<RunDef>();
        int[] _stepToOpen = Array.Empty<int>();
        int _runPick; // into _openRuns
        int _step;    // the ladder's cursor
        ArenaLadder _ladder;
        ArenaTablet _arenaTablet;
        Button _arenaFight;

        // R7: the Edicts (a layer over every screen, used from the arena screen)
        Button _edictsButton;
        string _edictsButtonText = "";
        RunModifierDef[] _chosenEdicts = Array.Empty<RunModifierDef>();
        bool _edictsShown;
        TextMeshProUGUI _menuRenown, _arenaKeys;
        UiPlate _edictsPlate;

        // R8: the Daily Games (the second menu button and its screen)
        Button _dailyButton, _codexButton, _dailyFight;
        Image _codexSeal;
        TextMeshProUGUI _dailyHint, _dailyHeader;
        DailyPlan _todayPlan;
        bool _dailyOpen;

        // R6: the skins (the strip under the diorama; the prefab the diorama shows)
        SkinStrip _skins;
        GameObject _heroPrefab, _heroShown;
        TextMeshProUGUI _selectKeys;

        /// <summary>Earned: no unlock, or its unlock is in the save.</summary>
        public static bool IsAvailable(GladiatorDef g) => g != null && (g.unlock == null || MetaProgress.IsUnlocked(g.unlock));
        public static bool IsAvailable(RunDef r) => r != null && (r.unlock == null || MetaProgress.IsUnlocked(r.unlock));

        /// <summary>The arena FIGHT! starts in.</summary>
        public RunDef SelectedRun => _openRuns.Length > 0 ? _openRuns[Mathf.Clamp(_runPick, 0, _openRuns.Length - 1)] : run;
        /// <summary>True when FIGHT! on the gladiator screen goes on to the arena screen (two or more arenas earned, or the
        /// Edicts: their button lives there).</summary>
        public bool AsksForArena => _openRuns.Length > 1 || _edictsShown;
        RunDef[] AllRuns => runs != null && runs.Length > 0 ? runs : new[] { run };

        /// <summary>The gladiator FIGHT! starts with.</summary>
        public GladiatorDef Selected => _available.Length > 0 ? _available[Mathf.Clamp(_pick, 0, _available.Length - 1)] : null;

        // ---- tests (RosterTests) ------------------------------------------------------------------------------------
        public RosterGrid Roster => _roster;
        public GladiatorTablet Tablet => _tablet;
        public ArenaLadder Ladder => _ladder;
        public ArenaTablet ArenaTablet => _arenaTablet;
        /// <summary>The grid's cursor (a slot index: earned gladiators first, then the locked).</summary>
        public int Slot => _slot;
        public int Step => _step;
        public bool FightEnabled => _fight != null && _fight.interactable;
        public bool ArenaFightEnabled => _arenaFight != null && _arenaFight.interactable;

        // ---- tests (UiMenuLookTests) ---------------------------------------------------------------------------------
        /// <summary>The main menu's plate `i` (PLAY = 0, DAILY, CODEX, OPTIONS, QUIT), or null.</summary>
        public UiPlate MenuPlate(int i) => _menuPlates != null && i >= 0 && i < _menuPlates.Length ? _menuPlates[i] : null;
        public int MenuPlateCount => _menuPlates != null ? _menuPlates.Length : 0;
        /// <summary>The main menu's keyboard / gamepad focus (a MenuPlate index).</summary>
        public int MenuFocus => _focus;
        /// <summary>The arena screen's EDICTS plate (null before the arena screen is built).</summary>
        public UiPlate EdictsPlate => _edictsPlate;
        /// <summary>The main menu's title and the Renown number (their theme roles).</summary>
        public Component TitleLabel { get; private set; }
        public Component RenownLabel => _menuRenown;
        /// <summary>The frame under the title (P3: the gold meander rule, `MeanderGold`; it was the mosaic plaque) and the
        /// one behind the Renown (`Bronze`).</summary>
        public Component TitlePlaque { get; private set; }
        public Component RenownPlate { get; private set; }

        /// <summary>A screen's BACK and FIGHT! plates ("Select", "Arena", "Daily"), or null.</summary>
        public UiPlate BackPlateOf(string screen) => _chrome.TryGetValue(screen, out var p) ? p[0] : null;
        public UiPlate FightPlateOf(string screen) => _chrome.TryGetValue(screen, out var p) ? p[1] : null;
        readonly System.Collections.Generic.Dictionary<string, UiPlate[]> _chrome = new System.Collections.Generic.Dictionary<string, UiPlate[]>();

        /// <summary>The keyboard / gamepad move on the main menu's column (up -1, down +1), as Update does it.</summary>
        public void MoveMenuFocus(int d)
        {
            if (_menuButtons == null || _menuButtons.Length == 0 || d == 0) return;
            int n = _menuButtons.Length;
            _focus = (_focus + (d > 0 ? 1 : n - 1)) % n;
            if (!_menuButtons[_focus].interactable) _focus = (_focus + (d > 0 ? 1 : n - 1)) % n;
            RefreshMenuFocus();
        }

        /// <summary>Moves the grid's cursor to `slot`, earned or locked (a keyboard/gamepad move; never fights).</summary>
        public void PickSlot(int slot)
        {
            if (_roster == null || _roster.Count == 0) return;
            _slot = Mathf.Clamp(slot, 0, _roster.Count - 1);
            if (_slot < _available.Length) _pick = _slot;
            RefreshPick();
        }

        /// <summary>The keyboard/gamepad move on the gladiator grid (dx right, dy down). False when blocked by an edge.</summary>
        public bool MoveCursor(int dx, int dy)
        {
            if (_roster == null) return false;
            int s = GridMath.Step(_slot, _roster.Count, _roster.Columns, dx, dy);
            if (s == _slot) return false;
            SfxPlayer.Play2D(SfxId.Card);
            PickSlot(s);
            return true;
        }

        /// <summary>Moves the ladder's cursor to `step`, earned or locked.</summary>
        public void PickStep(int step)
        {
            if (_ladder == null || _ladder.Count == 0) return;
            _step = Mathf.Clamp(step, 0, _ladder.Count - 1);
            if (_stepToOpen[_step] >= 0) _runPick = _stepToOpen[_step];
            RefreshRunPick();
        }

        /// <summary>The next (+1) or previous (-1) arena in climb order (reading order on the grid). False at either end.</summary>
        public bool Climb(int d)
        {
            if (_ladder == null) return false;
            int s = GridMath.Climb(_step, _ladder.Count, d);
            if (s == _step) return false;
            SfxPlayer.Play2D(SfxId.Card);
            PickStep(s);
            return true;
        }

        /// <summary>The keyboard/gamepad move on the arena grid (dx right, dy down), as on the gladiator grid. False when
        /// an edge blocks it.</summary>
        public bool MoveArena(int dx, int dy)
        {
            if (_ladder == null) return false;
            int s = GridMath.Step(_step, _ladder.Count, ArenaLadder.Columns, dx, dy);
            if (s == _step) return false;
            SfxPlayer.Play2D(SfxId.Card);
            PickStep(s);
            return true;
        }

        // ---- the skins (R6) -----------------------------------------------------------------------------------------

        /// <summary>The strip under the diorama (tests drive it).</summary>
        public SkinStrip Skins => _skins;
        /// <summary>The model prefab the diorama shows: the picked gladiator's, or the skin on the strip.</summary>
        public GameObject HeroPrefab => _heroPrefab;
        /// <summary>The diorama's instance of HeroPrefab.</summary>
        public GameObject HeroShown => _heroShown;
        /// <summary>The gladiator screen's key line (tests).</summary>
        public string SelectKeysText => _selectKeys != null ? _selectKeys.text : "";

        /// <summary>Q / E, LB / RB, the strip's arrows: the next (dir &gt; 0) or previous skin. False when there's none.</summary>
        public bool CycleSkin(int dir) => _skins != null && _skins.Cycle(dir);

        /// <summary>F, gamepad North, the strip's button: buys the Market skin on show and equips it.</summary>
        public BuyResult BuySkin() => _skins != null && _skins.Shown ? _skins.Buy() : BuyResult.NotForSale;

        /// <summary>The strip follows the gladiator the diorama shows (re-opened on his equipped skin when he changes).</summary>
        void RefreshSkins()
        {
            var g = Selected;
            if (_skins != null && _skins.Gladiator != g) _skins.Show(g, meta, MetaProgress.Data);
            ShowHero(g, _skins != null && _skins.Gladiator == g ? _skins.Current : null);
        }

        /// <summary>The strip cycled: the diorama wears the skin on show, owned or not (FIGHT! takes the equipped one).</summary>
        void PreviewSkin(SkinDef s) => ShowHero(_skins.Gladiator, s);

        /// <summary>The equipped skin changed (bought = Renown was spent): the tile, the corner's Renown and the Codex follow.</summary>
        void OnSkinChanged(bool bought)
        {
            for (int i = 0; i < _available.Length; i++) if (_available[i] == _skins.Gladiator) RefreshTilePortrait(i);
            if (!bought) return;
            SetMenuRenown();
            if (Codex != null) Codex.Refresh(); // its Market was built before the purchase
            RefreshCodexButton();
        }

        /// <summary>An earned tile shows its gladiator's equipped skin (its baked portrait), else his own.</summary>
        void RefreshTilePortrait(int i)
        {
            if (_roster == null || i < 0 || i >= _available.Length) return;
            var g = _available[i];
            var s = meta != null ? MetaProgress.EquippedSkin(g, meta) : null;
            _roster.SetPortrait(i, s != null && s.portrait != null ? s.portrait : g.portrait);
        }

        /// <summary>`-skin &lt;id&gt;` (bot / perf runs: any skin, owned or not), or null.</summary>
        SkinDef CommandLineSkin()
        {
            string id = CommandLine.Get("-skin");
            if (string.IsNullOrEmpty(id)) return null;
            if (meta != null && meta.skins != null)
                foreach (var s in meta.skins) if (s != null && s.id == id) return s;
            Debug.LogWarning("[Skins] -skin: no skin \"" + id + "\"");
            return null;
        }

        /// <summary>The skin `g` fights in: `-skin`'s when it's his, else his equipped one (null = his own colours).</summary>
        SkinDef SkinFor(GladiatorDef g, SkinDef commandLine)
        {
            if (commandLine != null)
            {
                if (commandLine.gladiator == g) return commandLine;
                Debug.LogWarning("[Skins] -skin " + commandLine.id + " belongs to " + (commandLine.gladiator != null ? commandLine.gladiator.id : "nobody") +
                                 ", not " + (g != null ? g.id : "nobody") + ": his equipped skin instead");
            }
            return g != null && meta != null ? MetaProgress.EquippedSkin(g, meta) : null;
        }

        /// <summary>Drops equipped skin ids no SkinDef knows (a skin taken out of the data; spec §5). Not while there are
        /// no skins as data at all (before SkinSetup has run), which would wipe every choice.</summary>
        void PruneSkins()
        {
            var d = MetaProgress.Data;
            if (meta == null || meta.skins == null || meta.skins.Length == 0 || d.equippedSkins == null) return;
            int dropped = d.equippedSkins.RemoveAll(id =>
            {
                foreach (var s in meta.skins) if (s != null && s.id == id) return false;
                return true;
            });
            if (dropped == 0) return;
            SaveSystem.Save(d);
            Debug.Log("[Skins] dropped " + dropped + " equipped skin id(s) no SkinDef knows");
        }

        // ---- the Codex --------------------------------------------------------------------------------------------

        /// <summary>The Codex screen (tests drive it).</summary>
        public CodexPanel Codex { get; private set; }
        public bool CodexOpen => _screen == Screen.Codex;
        /// <summary>The menu's CODEX button text ("CODEX": P3 shows what's new with the seal).</summary>
        public string CodexButtonText => UiFactory.Label(_codexButton).text;
        /// <summary>The CODEX plate wears the NEW seal (entries opened since the Codex was last viewed).</summary>
        public bool CodexSealShown => _codexSeal != null && _codexSeal.enabled;
        /// <summary>Tests: the seal (`FrameKind.SealNew`) on the CODEX plate.</summary>
        public Component CodexSeal => _codexSeal;

        // ---- the Daily Games (R8) ---------------------------------------------------------------------------------

        /// <summary>The Daily screen (tests drive it).</summary>
        public DailyPanel Daily { get; private set; }
        /// <summary>The Daily screen is the one showing.</summary>
        public bool DailyShown => _screen == Screen.Daily;
        /// <summary>The DAILY button can be pressed (the Daily Games are earned).</summary>
        public bool DailyButtonOpen => _dailyButton != null && _dailyButton.interactable;
        /// <summary>The DAILY button's label ("DAILY", or "DAILY  •" with the dot in a gold colour tag while today's scored attempt is open).</summary>
        public string DailyButtonText => _dailyButton != null ? UiFactory.Label(_dailyButton).text : "";
        /// <summary>The gold dot: today's scored attempt is still open.</summary>
        public bool DailyDot { get; private set; }
        /// <summary>The greyed button's hint (its unlock's), "" once open.</summary>
        public string DailyHintText => _dailyHint != null && _dailyHint.gameObject.activeSelf ? _dailyHint.text : "";
        /// <summary>The Daily screen's header ("THE DAILY GAMES — 22 OCT 2026").</summary>
        public string DailyHeaderText => _dailyHeader != null ? _dailyHeader.text : "";
        /// <summary>Today's plan (UTC), drawn from what the save has earned.</summary>
        public DailyPlan TodayPlan => _todayPlan;
        public bool DailyFightEnabled => _dailyFight != null && _dailyFight.interactable;

        /// <summary>The plan for `date` from the earned arenas and gladiators (DailyGames sorts them by id itself).</summary>
        DailyPlan PlanFor(int date) => DailyGames.For(date, meta, _openRuns, _available);

        /// <summary>Opens the Daily screen on today's plan (DAILY). Refused (a blocked sound) before the Daily Games are earned.</summary>
        public void OpenDaily()
        {
            if (!_dailyOpen || Daily == null)
            {
                SfxPlayer.Play2D(SfxId.Blocked);
                return;
            }
            SfxPlayer.Play2D(SfxId.Card);
            ShowDaily(DateTime.UtcNow);
            Show(Screen.Daily);
        }

        void ShowDaily(DateTime utcNow)
        {
            _todayPlan = PlanFor(DailyGames.DateKey(utcNow));
            int date = _todayPlan.Date > 0 ? _todayPlan.Date : DailyGames.DateKey(utcNow);
            if (_dailyHeader != null) _dailyHeader.text = DailyPanel.Header(date);
            Daily.Show(_todayPlan, MetaProgress.Data, meta, utcNow);
            if (_dailyFight != null) _dailyFight.interactable = _todayPlan.IsValid;
            RefreshDailyButton();
        }

        /// <summary>FIGHT! on the Daily screen: today's plan, scored if it's the day's first attempt (marked in the save at
        /// once, so quitting doesn't give another), else practice.</summary>
        public void FightDaily()
        {
            if (!_dailyOpen || !_todayPlan.IsValid)
            {
                SfxPlayer.Play2D(SfxId.Blocked);
                return;
            }
            if (DailyGames.DateKey(DateTime.UtcNow) != _todayPlan.Date)
            {
                ShowDaily(DateTime.UtcNow);
                if (!_todayPlan.IsValid) { SfxPlayer.Play2D(SfxId.Blocked); return; }
            }
            SfxPlayer.Play2D(SfxId.Pick);
            if (Edicts != null && Edicts.IsOpen) Edicts.Close(); // read-only: closing saves nothing
            bool scored = MetaProgress.BeginDaily(_todayPlan.Date);
            GameSession.Skin = SkinFor(_todayPlan.Gladiator, null); // R6: the day fixes the gladiator, not his colours
            GameSession.StartDaily(_todayPlan, scored);
            RefreshDailyButton(); // a test's suppressed load leaves the menu up
        }

        /// <summary>The day's edicts, read-only, over the Daily screen (the edicts block, E, gamepad North).</summary>
        public void OpenDailyEdicts()
        {
            var edicts = _todayPlan.Edicts;
            if (Edicts == null || Edicts.IsOpen || edicts == null || edicts.Length == 0) { SfxPlayer.Play2D(SfxId.Blocked); return; }
            SfxPlayer.Play2D(SfxId.Card);
            Edicts.Open(edicts);
        }

        void CloseDaily() => Show(Screen.Menu);

        void RefreshDailyButton()
        {
            if (_dailyButton == null) return;
            _dailyButton.interactable = _dailyOpen;
            DailyDot = _dailyOpen && !MetaProgress.DailyAttempted(DailyGames.Today());
            var label = UiFactory.Label(_dailyButton);
            label.text = DailyDot ? "DAILY  <color=" + MarbleGoldHex + ">•</color>" : "DAILY"; // the marble plate dims it when shut
            RefreshMenuFocus();
            if (_dailyHint != null)
            {
                _dailyHint.gameObject.SetActive(!_dailyOpen);
                var u = meta != null ? meta.dailyUnlock : null;
                _dailyHint.text = u != null && !string.IsNullOrEmpty(u.hint) && u.hint != "???" ? u.hint : "Defeat Gorm, the Pit Master, to open the Daily Games.";
            }
        }

        /// <summary>`-daily yyyymmdd` (or `-daily today`): that date's plan as practice, at once. False when there's nothing
        /// to fight (the run then starts as a normal bot run, if it is one).</summary>
        bool LaunchDailyFromCommandLine()
        {
            string v = CommandLine.Get("-daily");
            int date = DailyGames.Today();
            if (!string.IsNullOrEmpty(v) && DateTime.TryParseExact(v, "yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var day))
                date = DailyGames.DateKey(day);
            else if (!string.IsNullOrEmpty(v) && !v.StartsWith("-") && !string.Equals(v, "today", StringComparison.OrdinalIgnoreCase))
                Debug.LogWarning("[Daily] -daily " + v + ": not a yyyymmdd date, playing today's");
            var plan = PlanFor(date);
            if (!plan.IsValid)
            {
                Debug.LogWarning("[Daily] -daily: no arena or gladiator to fight with");
                return false;
            }
            Debug.Log("[Daily] practice daily " + date + ": " + plan.Run.id + ", " + plan.Gladiator.id + ", " + (plan.Edicts != null ? plan.Edicts.Length : 0) + " edicts");
            _todayPlan = plan;
            GameSession.Skin = SkinFor(plan.Gladiator, CommandLineSkin());
            GameSession.StartDaily(plan, false);
            return true;
        }

        // ---- the Edicts (R7) -----------------------------------------------------------------------------------

        /// <summary>The Edicts page (tests drive it).</summary>
        public EdictsPanel Edicts { get; private set; }
        /// <summary>The arena screen's EDICTS button is there (the Edicts are earned and exist as data).</summary>
        public bool EdictsButtonShown => _edictsPlate != null && _edictsPlate.Root.gameObject.activeSelf;
        /// <summary>The button's words without colour tags ("EDICTS · RENOWN ×1.45").</summary>
        public string EdictsButtonText => _edictsButtonText;
        /// <summary>The arena screen's key line (tests).</summary>
        public string ArenaKeysText => _arenaKeys != null ? _arenaKeys.text : "";

        /// <summary>Opens the Edicts page over the current screen (the EDICTS button, E, gamepad North).</summary>
        public void OpenEdicts()
        {
            if (Edicts == null || !EdictsButtonShown || Edicts.IsOpen) return;
            SfxPlayer.Play2D(SfxId.Card);
            Edicts.Open();
        }

        /// <summary>The edicts FIGHT! starts under: `-edicts a,b` (bot / perf runs: any, earned or not), else the saved choice.
        /// A bot or perf run without `-edicts` fights under none, whatever the save chose (its numbers stay comparable).</summary>
        public RunModifierDef[] EdictsForLaunch()
        {
            string wanted = CommandLine.Get("-edicts");
            if (!string.IsNullOrEmpty(wanted))
            {
                var list = new System.Collections.Generic.List<RunModifierDef>();
                foreach (var raw in wanted.Split(','))
                {
                    string id = raw.Trim();
                    if (id.Length == 0) continue;
                    RunModifierDef found = null;
                    if (meta != null && meta.edicts != null)
                        foreach (var e in meta.edicts) if (e != null && e.id == id) found = e;
                    if (found == null) Debug.LogWarning("[Edicts] -edicts: no edict \"" + id + "\"");
                    else if (!list.Contains(found)) list.Add(found);
                }
                return list.ToArray();
            }
            if (CommandLine.Has("-bot") || CommandLine.Has("-stress")) return Array.Empty<RunModifierDef>();
            return _edictsShown ? MetaProgress.ChosenEdicts(meta) : Array.Empty<RunModifierDef>();
        }

        /// <summary>The page changed (a toggle, a clear, a purchase): the button, the tablet and the Renown follow it.</summary>
        void OnEdictsChanged()
        {
            if (!Edicts.ReadOnly) _chosenEdicts = Edicts.Chosen;
            RefreshEdictsButton();
            RefreshRunPick();
            SetMenuRenown();
        }

        void CloseEdicts()
        {
            if (!Edicts.ReadOnly) _chosenEdicts = MetaProgress.ChosenEdicts(meta);
            RefreshEdictsButton();
            RefreshRunPick();
            if (Edicts.BoughtAny) // the Codex's Market and the corner's Renown were built before the purchase
            {
                SetMenuRenown();
                if (Codex != null) Codex.Refresh();
                RefreshCodexButton();
            }
        }

        void RefreshEdictsButton()
        {
            if (_edictsButton == null) return;
            int n = _chosenEdicts.Length;
            float mul = 1f + RunModifiers.Bonus(_chosenEdicts);
            _edictsButtonText = EdictsPanel.ButtonText(n, mul);
            UiFactory.Label(_edictsButton).text = n <= 0
                ? _edictsButtonText
                : "<color=" + MarbleHurtHex + ">EDICTS</color> · RENOWN <color=" + MarbleGoldHex + ">" + EdictsPanel.MulText(mul) + "</color>"; // orange-red: it hurts you (the marble's darker one)
        }

        /// <summary>P3 (note 6): the crimson NEW seal on the CODEX plate's top-right corner while anything is new.</summary>
        void RefreshCodexButton()
        {
            int n = Codex != null ? Codex.RemainingNew : 0;
            var label = UiFactory.Label(_codexButton);
            label.text = "CODEX";
            if (_codexSeal == null) return;
            _codexSeal.enabled = n > 0;
            if (n > 0) UiKit.PlaceSeal(_codexSeal, (RectTransform)_codexButton.transform, label);
        }

        void Awake()
        {
            MetaProgress.CheckUnlocks(meta); // a discovery from a run that was quit, not finished
            PruneSkins(); // R6
            // R7: the Edicts, once earned and once they exist as data (before EdictSetup there are none to show)
            _edictsShown = meta != null && meta.edicts != null && meta.edicts.Length > 0 && MetaProgress.EdictsOpen(meta);
            _chosenEdicts = _edictsShown ? MetaProgress.ChosenEdicts(meta) : Array.Empty<RunModifierDef>();
            _dailyOpen = meta != null && MetaProgress.DailyOpen(meta);
            var canvas = UiFactory.Canvas("MenuCanvas", transform, UiSortOrder.Menus).transform;
            _menu = Group("Menu", canvas);
            _select = Group("Select", canvas);
            _arena = Group("Arena", canvas);
            _daily = Group("Daily", canvas);
            _codex = Group("Codex", canvas);
            var edicts = Group("Edicts", canvas); // over every screen; empty (no clicks taken) while the page is closed
            BuildMenu(_menu.transform);
            BuildSelect(_select.transform);
            BuildArena(_arena.transform);
            BuildDaily(_daily.transform); // after the select screens: the plan draws from what they found earned
            var night = UiTheme.Current.night;
            night.a = 0.88f;
            var dim = UiFactory.Image("Dim", _codex.transform, night, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, PixelIcons.White);
            UiFactory.Stretch(dim.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Codex = new CodexPanel(font, fontMaterial);
            Codex.Build(_codex.transform, MetaProgress.Data, codex, meta, gladiators, locked, CloseCodex);
            RefreshCodexButton();
            Edicts = new EdictsPanel(font, fontMaterial);
            Edicts.Build(edicts.transform, meta, MetaProgress.Data, CloseEdicts);
            Edicts.OnChanged = OnEdictsChanged;
            Refit();
            Show(Screen.Menu);
        }

        void BuildDaily(Transform root)
        {
            _dailyFight = Chrome(root, DailyPanel.Header(DailyGames.Today()),
                "Enter / A: fight     E / Y: edicts     Esc / B: back", FightDaily, CloseDaily);
            _dailyHeader = root.Find("Header").GetComponent<TextMeshProUGUI>();
            _dailyHeader.overflowMode = TextOverflowModes.Truncate;
            Daily = new DailyPanel(font, fontMaterial);
            Daily.Build(root, codex);
            Daily.Names(AllRuns, gladiators);
            Daily.OnEdicts = OpenDailyEdicts;
            if (_dailyOpen) ShowDaily(DateTime.UtcNow);
            else RefreshDailyButton();
        }

        void Start()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            TimeController.ResetForNewRun();
            if (CommandLine.Has("-daily") && LaunchDailyFromCommandLine()) return; // R8: a practice daily, at once
            if (CommandLine.Has("-bot") || CommandLine.Has("-stress")) StartRun(); // perf runs skip the menu
        }

        static CanvasGroup Group(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            return go.GetComponent<CanvasGroup>();
        }

        void BuildMenu(Transform root)
        {
            var th = UiTheme.Current;
            var left = new Vector2(0f, 0.5f);
            // P3 (note 3): no plaque. The word in Marcellus SC gold, outlined over the sky, between laurels; a thin gold
            // meander under it, the line under that. Centred where the plaque stood, above the plates. (A real logo: BACKLOG.)
            const float titleX = 556f, titleY = 262f, ruleY = 172f, lineY = 132f;
            var word = UiKit.Text("Title", root, title, TextRole.Title, TitleSize, th.gold, TextAlignmentOptions.Center, left, new Vector2(0.5f, 0.5f),
                new Vector2(titleX, titleY), new Vector2(900f, 130f), TextLook.Outline);
            TitleLabel = word;
            UiKit.Laurels("Laurels", word);
            float ruleW = UiAudit.TryInkBounds(word, out var ink) ? Mathf.Max(200f, Mathf.Round(ink.width / 6f) * 6f) : 540f;
            TitlePlaque = UiKit.Meander("Meander", root, true, left, new Vector2(0.5f, 0.5f), new Vector2(titleX, ruleY), ruleW);
            UiKit.Text("Subtitle", root, subtitle, TextRole.Body, 36f, th.ink, TextAlignmentOptions.Center, left, new Vector2(0.5f, 0.5f),
                new Vector2(titleX, lineY), new Vector2(900f, 50f), TextLook.Outline);

            // R8: DAILY under PLAY; the others one slot down. PLAY is the screen's main action (bronze), the rest marble.
            var size = new Vector2(360f, 66f);
            _menuPlates = new[]
            {
                UiKit.Button("Play", root, "PLAY", ButtonStyle.Primary, left, left, new Vector2(110, 10), size, OpenSelect, 34f),
                UiKit.Button("Daily", root, "DAILY", ButtonStyle.Secondary, left, left, new Vector2(110, -82), size, OpenDaily, 34f),
                UiKit.Button("Codex", root, "CODEX", ButtonStyle.Secondary, left, left, new Vector2(110, -174), size, OpenCodex, 34f),
                UiKit.Button("Options", root, "OPTIONS", ButtonStyle.Secondary, left, left, new Vector2(110, -266), size, OpenOptions, 34f),
                UiKit.Button("Quit", root, "QUIT", ButtonStyle.Secondary, left, left, new Vector2(110, -358), size, Quit, 34f),
            };
            _menuButtons = new Button[_menuPlates.Length];
            for (int i = 0; i < _menuPlates.Length; i++) _menuButtons[i] = _menuPlates[i].Button;
            _dailyButton = _menuButtons[1];
            _codexButton = _menuButtons[2];
            _codexSeal = UiKit.Seal("New", _menuPlates[2].Root);
            // the greyed DAILY's hint, beside it (its unlock's hint)
            _dailyHint = UiKit.Text("DailyHint", root, "", TextRole.Body, 22f, th.dust, TextAlignmentOptions.Left, left, left,
                new Vector2(494, -82), new Vector2(760, 40), TextLook.Outline);
            _dailyHint.enableAutoSizing = true; _dailyHint.fontSizeMin = 18f; _dailyHint.fontSizeMax = 22f;
            _dailyHint.overflowMode = TextOverflowModes.Truncate;
            UiKit.Text("Keys", root, "Enter / A: select     Esc / B: back", TextRole.Body, 22f, th.dust, TextAlignmentOptions.Left,
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(116, 40), new Vector2(900, 34), TextLook.Outline);
            var bottomRight = new Vector2(1f, 0f);
            var faint = th.dust;
            faint.a = 0.7f; // quiet, but outlined: it's over the scene (UiAudit)
            UiKit.Text("Version", root, GameInfo.VersionLabel, TextRole.Body, 22f, faint, TextAlignmentOptions.Right, bottomRight, bottomRight,
                new Vector2(-60, 40), new Vector2(400, 34), TextLook.Outline);

            // Renown on a small bronze plate in the top-right corner, the runs line under it
            var save = MetaProgress.Data;
            var corner = new Vector2(1f, 1f);
            // P3 (note 2): "RENOWN" in the marble colour (light on the dark plate), both words inside the content rect,
            // their ink centred on the plate (Midline)
            var plate = UiKit.Frame("RenownPlate", root, FrameKind.Bronze, corner, corner, new Vector2(-MenuLayout.Margin, -40f), RenownPlateSize, Color.white);
            RenownPlate = plate;
            var content = UiKit.ContentRect(FrameKind.Bronze, RenownPlateSize);
            var pl = new Vector2(0f, 0.5f);
            float inX = content.xMin + RenownPlateSize.x * 0.5f; // from the plate's left edge
            UiKit.Text("RenownLabel", plate.transform, "RENOWN", TextRole.Title, 22f, th.marble, TextAlignmentOptions.MidlineLeft, pl, pl,
                new Vector2(inX, 0f), new Vector2(120f, content.height + 4f));
            _menuRenown = UiKit.Text("Renown", plate.transform, "", TextRole.Number, 34f, th.goldBright, TextAlignmentOptions.MidlineRight, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-inX, 0f), new Vector2(content.width - 130f, 42f), TextLook.Outline);
            _menuRenown.enableAutoSizing = true; _menuRenown.fontSizeMin = 22f; _menuRenown.fontSizeMax = 34f;
            SetMenuRenown();
            if (save.runs > 0)
                UiKit.Text("Runs", root, save.runs + (save.runs == 1 ? " run  ·  " : " runs  ·  ") + save.victories + (save.victories == 1 ? " victory" : " victories"),
                    TextRole.Body, 22f, th.ink, TextAlignmentOptions.Right, corner, corner, new Vector2(-MenuLayout.Margin, -40f - RenownPlateSize.y - 10f), new Vector2(600, 30), TextLook.Outline);
            RefreshMenuFocus();
        }

        /// <summary>The Renown plate's number (after a purchase or an edict changed it).</summary>
        void SetMenuRenown()
        {
            if (_menuRenown != null) _menuRenown.text = MetaProgress.Data.renown.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>The menu column's plates: the focused one shows its gold ring, a shut one (DAILY before Gorm) is dimmed.
        /// Only on a change (no per-frame recolour).</summary>
        void RefreshMenuFocus()
        {
            if (_menuPlates == null) return;
            bool daily = _dailyButton != null && _dailyButton.interactable;
            if (_focus == _shownFocus && daily == _shownDailyOpen) return;
            _shownFocus = _focus;
            _shownDailyOpen = daily;
            for (int i = 0; i < _menuPlates.Length; i++)
                _menuPlates[i].SetState(!_menuButtons[i].interactable ? UiState.Disabled : i == _focus ? UiState.Focused : UiState.Normal);
        }

        /// <summary>The header over a gold meander, the key line under it, and BACK (marble) / FIGHT! (bronze) in the bottom
        /// corners (the select screens and the Daily screen). Returns FIGHT!'s button.</summary>
        Button Chrome(Transform root, string header, string keys, UnityEngine.Events.UnityAction fight, UnityEngine.Events.UnityAction back)
        {
            var th = UiTheme.Current;
            var top = new Vector2(0.5f, 1f);
            UiKit.Text("Header", root, header, TextRole.Title, 56f, th.gold, TextAlignmentOptions.Center, top, top, new Vector2(0, -14), new Vector2(1400, 72), TextLook.Outline);
            UiKit.Meander("Meander", root, true, top, top, new Vector2(0, -88), 540f);
            UiKit.Text("Keys", root, keys, TextRole.Body, 22f, th.dust, TextAlignmentOptions.Center, top, top, new Vector2(0, -106), new Vector2(1400, 30), TextLook.Outline);
            var backPlate = UiKit.Button("Back", root, "BACK", ButtonStyle.Secondary, Vector2.zero, Vector2.zero, new Vector2(MenuLayout.Margin, 40), new Vector2(220, 66), back, 30f);
            var corner = new Vector2(1f, 0f);
            var fightPlate = UiKit.Button("Fight", root, "FIGHT!", ButtonStyle.Primary, corner, corner, new Vector2(-MenuLayout.Margin, 40), new Vector2(300, 78), fight, 40f);
            _chrome[root.name] = new[] { backPlate, fightPlate };
            return fightPlate.Button;
        }

        void BuildSelect(Transform root)
        {
            var slots = RosterGrid.Slots(gladiators, locked);
            int open = 0;
            while (open < slots.Length && slots[open].Open) open++;
            _available = new GladiatorDef[open];
            for (int i = 0; i < open; i++) _available[i] = slots[i].Def;

            bool skins = meta != null && meta.skins != null && meta.skins.Length > 0; // R6: once they exist as data
            _fight = Chrome(root, "CHOOSE YOUR GLADIATOR", skins
                ? "Arrows / d-pad: move     Q / E: skin     Enter / A: fight     Esc / B: back"
                : "Arrows / d-pad: move     Enter / A: fight     Esc / B: back", StartRun, CloseSelect);
            _selectKeys = root.Find("Keys").GetComponent<TextMeshProUGUI>();
            _roster = new RosterGrid(font);
            // P3: the picked tile's wreath rises over the grid's top: the grid starts that much lower
            _roster.Build(root, new Vector2(0f, 1f), new Vector2(MenuLayout.Margin, -MenuLayout.Top - RosterGrid.WreathRoom), slots, MenuLayout.Columns, codex, Choose);
            _tablet = new GladiatorTablet(font);
            var corner = new Vector2(1f, 1f);
            _tablet.Build(root, corner, corner, new Vector2(-MenuLayout.Margin, -MenuLayout.Top), codex);
            // R6: the skin strip under the diorama, between BACK and FIGHT!; the tiles in their equipped skins
            _skins = new SkinStrip(font, fontMaterial);
            _skins.Build(root, new Vector2(0.5f, 0f), new Vector2(0f, 28f), PreviewSkin);
            _skins.OnChanged = OnSkinChanged;
            for (int i = 0; i < _available.Length; i++) RefreshTilePortrait(i);

            // the last gladiator played this session stays picked
            _pick = 0;
            for (int i = 0; i < _available.Length; i++) if (_available[i] == GameSession.Gladiator) _pick = i;
            _slot = _pick;
            RefreshPick();
        }

        void BuildArena(Transform root)
        {
            var all = AllRuns;
            int open = 0, n = 0;
            foreach (var r in all) if (r != null) { n++; if (IsAvailable(r)) open++; }
            _openRuns = new RunDef[open];
            _stepToOpen = new int[n];
            var steps = new ArenaLadder.Step[n];
            int k = 0, a = 0;
            foreach (var r in all)
            {
                if (r == null) continue;
                bool earned = IsAvailable(r);
                _stepToOpen[k] = earned ? a : -1;
                if (earned) _openRuns[a++] = r;
                steps[k++] = new ArenaLadder.Step { Run = r, Open = earned };
            }

            _arenaFight = Chrome(root, "CHOOSE YOUR ARENA", _edictsShown
                ? "Arrows / d-pad: move     Enter / A: fight     E / Y: edicts     Esc / B: back"
                : "Arrows / d-pad: move     Enter / A: fight     Esc / B: back", Launch, BackToSelect);
            _arenaKeys = root.Find("Keys").GetComponent<TextMeshProUGUI>();
            // R7: between BACK and FIGHT!, only once the Edicts are earned
            var bottom = new Vector2(0.5f, 0f);
            _edictsPlate = UiKit.Button("Edicts", root, "EDICTS", ButtonStyle.Secondary, bottom, bottom, new Vector2(0, 40), new Vector2(520, 66), OpenEdicts, 28f);
            _edictsButton = _edictsPlate.Button;
            _edictsPlate.Root.gameObject.SetActive(_edictsShown);
            RefreshEdictsButton();
            // P3 (note 12): the arenas as a grid of tiles under the header, like the gladiators
            _ladder = new ArenaLadder(font);
            var topLeft = new Vector2(0f, 1f);
            _ladder.Build(root, topLeft, topLeft, new Vector2(MenuLayout.Margin, -MenuLayout.Top), steps, MetaProgress.Data, ChooseArena);
            _arenaTablet = new ArenaTablet(font);
            var corner = new Vector2(1f, 1f);
            _arenaTablet.Build(root, corner, corner, new Vector2(-MenuLayout.Margin, -MenuLayout.Top));

            // the last arena played this session stays picked
            _runPick = 0;
            for (int i = 0; i < _openRuns.Length; i++) if (_openRuns[i] == GameSession.Run) _runPick = i;
            _step = StepOf(_runPick);
            RefreshRunPick();
        }

        int StepOf(int openIndex)
        {
            for (int i = 0; i < _stepToOpen.Length; i++) if (_stepToOpen[i] == openIndex) return i;
            return 0;
        }

        /// <summary>Scales the grids and the tablets to the canvas (the window may have changed size).</summary>
        void Refit()
        {
            _screenW = UnityEngine.Screen.width;
            _screenH = UnityEngine.Screen.height;
            var canvas = GridMath.CanvasSize(_screenW, _screenH, MenuLayout.Reference, MenuLayout.Match);
            float tablet = MenuLayout.TabletScale(canvas);
            if (_roster != null) _roster.Fit(RosterScale(canvas, _roster.Count));
            if (_tablet != null) _tablet.Fit(tablet);
            if (_ladder != null) _ladder.Fit(ArenaLadder.GridScale(canvas, _ladder.Count));
            if (_arenaTablet != null) _arenaTablet.Fit(tablet);
            if (Edicts != null) Edicts.Fit(UiKit.FitScale(EdictsPanel.Size, canvas));
            // the Daily screen's blocks sit between the header and the buttons, inside the side margins
            if (Daily != null) Daily.Fit(UiKit.FitScale(DailyPanel.Size, new Vector2(canvas.x - 2f * MenuLayout.Margin, MenuLayout.BlockHeight(canvas)), 0f));
            if (Codex != null) Codex.Fit(UiKit.FitScale(CodexPanel.Size, canvas));
        }

        /// <summary>The roster's scale (MenuLayout.GridScale) with room above it for the picked tile's wreath.</summary>
        public static float RosterScale(Vector2 canvas, int count)
        {
            var size = GridMath.Size(count, MenuLayout.Columns, MenuLayout.Tile, MenuLayout.Gap);
            var area = new Vector2(GridMath.Size(MenuLayout.Columns, MenuLayout.Columns, MenuLayout.Tile, MenuLayout.Gap).x,
                MenuLayout.BlockHeight(canvas) - RosterGrid.WreathRoom);
            return Mathf.Min(MenuLayout.GridScale(canvas, count), GridMath.Fit(size, area));
        }

        /// <summary>Picks arena `r` if it's earned (tests). False if it's locked or unknown.</summary>
        public bool PickArena(RunDef r)
        {
            for (int i = 0; i < _openRuns.Length; i++)
                if (_openRuns[i] == r) { _runPick = i; _step = StepOf(i); RefreshRunPick(); return true; }
            return false;
        }

        void ChooseArena(int step)
        {
            if (step == _step) // a second click on the picked step fights
            {
                if (_stepToOpen[step] >= 0) Launch();
                else SfxPlayer.Play2D(SfxId.Blocked);
                return;
            }
            SfxPlayer.Play2D(SfxId.Card);
            PickStep(step);
        }

        void RefreshRunPick()
        {
            if (_ladder == null || _ladder.Count == 0) return;
            _ladder.SetPick(_step);
            bool open = _stepToOpen[_step] >= 0;
            _arenaTablet.Show(_ladder.Run(_step), open, ArenaLadder.Tier(_ladder.Run(_step), _step), MetaProgress.Data, meta, _chosenEdicts);
            _arenaFight.interactable = open;
        }

        /// <summary>Picks `g` if it's earned (tests, and later a gamepad cursor). False if it's locked or unknown.</summary>
        public bool Pick(GladiatorDef g)
        {
            for (int i = 0; i < _available.Length; i++)
                if (_available[i] == g) { _pick = _slot = i; RefreshPick(); return true; }
            return false;
        }

        void Choose(int slot)
        {
            if (slot == _slot) { StartRun(); return; } // a second click on the picked tile fights
            SfxPlayer.Play2D(SfxId.Card);
            PickSlot(slot);
        }

        void RefreshPick()
        {
            if (_roster != null && _roster.Count > 0)
            {
                _roster.SetPick(_slot);
                bool open = _slot < _available.Length;
                _tablet.Show(_roster.Def(_slot), open, MetaProgress.Data, _roster.Hint(_slot));
                _fight.interactable = open;
            }
            RefreshSkins(); // the diorama and the strip: a locked pick leaves the last earned gladiator standing
        }

        /// <summary>The diorama shows whoever is picked, in `skin` when it's his (the scene's own hero is the Nobody, hidden
        /// once another stands in). Instances are cached by model prefab, so each skin is built once.</summary>
        void ShowHero(GladiatorDef g, SkinDef skin)
        {
            if (hero == null || g == null) return;
            bool his = skin != null && skin.gladiator == g && skin.model != null;
            var model = his ? skin.model : g.model;
            if (model == null) // the Nobody's own look: his GladiatorDef has no model, the scene's hero is him
            {
                _heroPrefab = null;
                _heroShown = hero.gameObject;
                foreach (var kv in _heroModels) kv.Value.SetActive(false);
                hero.gameObject.SetActive(true);
                return;
            }
            if (!_heroModels.TryGetValue(model, out var shown))
            {
                shown = Instantiate(model, hero.parent, false);
                shown.name = "Hero_" + g.id + (his ? "_" + skin.id : "");
                shown.transform.SetPositionAndRotation(hero.position, hero.rotation);
                if (heroMaterial != null) // a skin is the same palette with other cells: the one shared material
                    foreach (var r in shown.GetComponentsInChildren<Renderer>(true)) r.sharedMaterial = heroMaterial;
                _heroModels[model] = shown;
            }
            _heroPrefab = model;
            _heroShown = shown;
            hero.gameObject.SetActive(false);
            foreach (var kv in _heroModels) kv.Value.SetActive(kv.Value == shown);
        }

        void Show(Screen screen)
        {
            _screen = screen;
            Set(_menu, screen == Screen.Menu);
            Set(_select, screen == Screen.Select);
            Set(_arena, screen == Screen.Arena);
            Set(_codex, screen == Screen.Codex);
            Set(_daily, screen == Screen.Daily);
            if (screen == Screen.Select && _skins != null) // R6: re-read the save (a skin bought in the Codex, Renown spent on an edict)
            {
                _skins.Show(Selected, meta, MetaProgress.Data);
                ShowHero(Selected, _skins.Current);
            }
        }

        static void Set(CanvasGroup g, bool on)
        {
            g.alpha = on ? 1f : 0f;
            g.interactable = g.blocksRaycasts = on;
        }

        void OpenSelect() { SfxPlayer.Play2D(SfxId.Card); Show(Screen.Select); }
        void CloseSelect() => Show(Screen.Menu);
        void BackToSelect() => Show(Screen.Select);
        public void OpenCodex() { SfxPlayer.Play2D(SfxId.Card); Show(Screen.Codex); }
        void OpenOptions() { SfxPlayer.Play2D(SfxId.Card); if (OptionsPanel.Instance != null) OptionsPanel.Instance.Open(); }
        void CloseCodex() { Show(Screen.Menu); RefreshCodexButton(); }

        /// <summary>FIGHT! on the gladiator screen: on to the arena screen when there's a choice, else straight in.
        /// Refused while a locked tile is picked. Bot and perf runs always go straight in.</summary>
        void StartRun()
        {
            bool bot = CommandLine.Has("-bot") || CommandLine.Has("-stress");
            if (!bot && _slot >= _available.Length)
            {
                SfxPlayer.Play2D(SfxId.Blocked);
                return;
            }
            if (!bot && AsksForArena && _screen != Screen.Arena)
            {
                SfxPlayer.Play2D(SfxId.Card);
                Show(Screen.Arena);
                return;
            }
            Launch();
        }

        void Launch()
        {
            SfxPlayer.Play2D(SfxId.Pick);
            var g = Selected;
            var cmdSkin = CommandLineSkin(); // R6: bot / perf runs
            string wanted = CommandLine.Get("-gladiator");
            if (!string.IsNullOrEmpty(wanted)) // bot / perf runs: anyone, earned or not
            {
                foreach (var d in gladiators) if (d != null && d.id == wanted) g = d;
            }
            else if (cmdSkin != null && cmdSkin.gladiator != null) g = cmdSkin.gladiator; // -skin alone: its wearer
            var r = SelectedRun;
            string arena = CommandLine.Get("-arena");
            if (!string.IsNullOrEmpty(arena))
                foreach (var d in AllRuns) if (d != null && d.id == arena) r = d;
            if (Edicts != null && Edicts.IsOpen) Edicts.Close(); // the page covers FIGHT!, but a launch from code keeps its choice
            RunModifiers.Set(EdictsForLaunch());
            GameSession.Skin = SkinFor(g, cmdSkin); // R6: the equipped (owned) skin, never the strip's preview
            GameSession.StartRun(g, r);
        }

        void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void Update()
        {
            if (UnityEngine.Screen.width != _screenW || UnityEngine.Screen.height != _screenH) Refit();
            if (GameOptions.PanelOwnsInput) return;
            var kb = Keyboard.current;
            var pad = Gamepad.current;
            bool confirm = (kb != null && (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)) || (pad != null && pad.buttonSouth.wasPressedThisFrame);
            bool back = (kb != null && kb.escapeKey.wasPressedThisFrame) || (pad != null && pad.buttonEast.wasPressedThisFrame);
            bool up = (kb != null && (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame)) || (pad != null && pad.dpad.up.wasPressedThisFrame);
            bool down = (kb != null && (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame)) || (pad != null && pad.dpad.down.wasPressedThisFrame);
            bool edictsKey = (kb != null && kb.eKey.wasPressedThisFrame) || (pad != null && pad.buttonNorth.wasPressedThisFrame);
            if (Edicts != null && Edicts.IsOpen) // R7: the page owns the input; E / North closes it as it opened it
            {
                bool left = (kb != null && (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame)) || (pad != null && pad.dpad.left.wasPressedThisFrame);
                bool right = (kb != null && (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame)) || (pad != null && pad.dpad.right.wasPressedThisFrame);
                bool clear = (kb != null && (kb.backspaceKey.wasPressedThisFrame || kb.deleteKey.wasPressedThisFrame)) || (pad != null && pad.buttonWest.wasPressedThisFrame);
                Edicts.HandleInput(left, right, up, down, confirm, back || edictsKey, clear);
                return;
            }
            if (_screen == Screen.Select)
            {
                bool left = (kb != null && (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame)) || (pad != null && pad.dpad.left.wasPressedThisFrame);
                bool right = (kb != null && (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame)) || (pad != null && pad.dpad.right.wasPressedThisFrame);
                int dx = (right ? 1 : 0) - (left ? 1 : 0), dy = (down ? 1 : 0) - (up ? 1 : 0);
                if (dx != 0) MoveCursor(dx, 0);
                else if (dy != 0) MoveCursor(0, dy);
                // R6: Q / E and LB / RB cycle the skins, F / North buys
                bool prevSkin = (kb != null && kb.qKey.wasPressedThisFrame) || (pad != null && pad.leftShoulder.wasPressedThisFrame);
                bool nextSkin = (kb != null && kb.eKey.wasPressedThisFrame) || (pad != null && pad.rightShoulder.wasPressedThisFrame);
                bool buySkin = (kb != null && kb.fKey.wasPressedThisFrame) || (pad != null && pad.buttonNorth.wasPressedThisFrame);
                if (prevSkin != nextSkin) CycleSkin(nextSkin ? 1 : -1);
                if (buySkin) BuySkin();
                if (confirm) StartRun();
                else if (back) CloseSelect();
                return;
            }
            if (_screen == Screen.Arena)
            {
                bool left = (kb != null && (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame)) || (pad != null && pad.dpad.left.wasPressedThisFrame);
                bool right = (kb != null && (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame)) || (pad != null && pad.dpad.right.wasPressedThisFrame);
                if (edictsKey && EdictsButtonShown) { OpenEdicts(); return; }
                // P3: the arena grid moves in 2D, like the gladiator grid
                int dx = (right ? 1 : 0) - (left ? 1 : 0), dy = (down ? 1 : 0) - (up ? 1 : 0);
                if (dx != 0) MoveArena(dx, 0);
                else if (dy != 0) MoveArena(0, dy);
                if (confirm)
                {
                    if (ArenaFightEnabled) Launch();
                    else SfxPlayer.Play2D(SfxId.Blocked);
                }
                else if (back) BackToSelect();
                return;
            }
            if (_screen == Screen.Daily)
            {
                if (Daily.Tick(DateTime.UtcNow)) ShowDaily(DateTime.UtcNow); // midnight UTC: the new day's plan
                if (edictsKey) OpenDailyEdicts();
                else if (confirm)
                {
                    if (DailyFightEnabled) FightDaily();
                    else SfxPlayer.Play2D(SfxId.Blocked);
                }
                else if (back) CloseDaily();
                return;
            }
            if (_screen == Screen.Codex)
            {
                bool left = (kb != null && (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame)) || (pad != null && (pad.dpad.left.wasPressedThisFrame || pad.leftShoulder.wasPressedThisFrame));
                bool right = (kb != null && (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame)) || (pad != null && (pad.dpad.right.wasPressedThisFrame || pad.rightShoulder.wasPressedThisFrame));
                Codex.HandleInput(left, right, up, down, confirm, back);
                return;
            }
            if (up) _focus = (_focus + _menuButtons.Length - 1) % _menuButtons.Length;
            if (down) _focus = (_focus + 1) % _menuButtons.Length;
            if (!_menuButtons[_focus].interactable) _focus = down || !up ? (_focus + 1) % _menuButtons.Length : (_focus + _menuButtons.Length - 1) % _menuButtons.Length;
            RefreshMenuFocus(); // the focused plate's gold ring (recoloured only when the focus moves)
            if (confirm) _menuButtons[_focus].onClick.Invoke();
            else if (back) Quit();
        }
    }
}
