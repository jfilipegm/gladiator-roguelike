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

        static readonly Color Gold = new Color(1f, 0.84f, 0.3f);
        static readonly Color Soft = new Color(1f, 1f, 1f, 0.75f);
        static readonly Color Dim = new Color(1f, 1f, 1f, 0.5f);

        enum Screen { Menu, Select, Arena, Codex }

        CanvasGroup _menu, _select, _arena, _codex;
        Button[] _menuButtons;
        int _focus;
        Screen _screen;
        Image[] _menuFrames;
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

        /// <summary>Earned: no unlock, or its unlock is in the save.</summary>
        public static bool IsAvailable(GladiatorDef g) => g != null && (g.unlock == null || MetaProgress.IsUnlocked(g.unlock));
        public static bool IsAvailable(RunDef r) => r != null && (r.unlock == null || MetaProgress.IsUnlocked(r.unlock));

        /// <summary>The arena FIGHT! starts in.</summary>
        public RunDef SelectedRun => _openRuns.Length > 0 ? _openRuns[Mathf.Clamp(_runPick, 0, _openRuns.Length - 1)] : run;
        /// <summary>True when FIGHT! on the gladiator screen goes on to the arena screen (two or more arenas earned).</summary>
        public bool AsksForArena => _openRuns.Length > 1;
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

        /// <summary>Up (+1) climbs the ladder, down (-1) descends. False at either end.</summary>
        public bool Climb(int d)
        {
            if (_ladder == null) return false;
            int s = GridMath.Climb(_step, _ladder.Count, d);
            if (s == _step) return false;
            SfxPlayer.Play2D(SfxId.Card);
            PickStep(s);
            return true;
        }

        // ---- the Codex --------------------------------------------------------------------------------------------

        /// <summary>The Codex screen (tests drive it).</summary>
        public CodexPanel Codex { get; private set; }
        public bool CodexOpen => _screen == Screen.Codex;
        /// <summary>The menu's CODEX button text ("CODEX • 3 NEW").</summary>
        public string CodexButtonText => UiFactory.Label(_menuButtons[1]).text;

        void RefreshCodexButton()
        {
            int n = Codex != null ? Codex.RemainingNew : 0;
            var label = UiFactory.Label(_menuButtons[1]);
            label.text = n > 0 ? "CODEX  <color=#FFD84A>• " + n + " NEW</color>" : "CODEX";
        }

        void Awake()
        {
            MetaProgress.CheckUnlocks(meta); // a discovery from a run that was quit, not finished
            var canvas = UiFactory.Canvas("MenuCanvas", transform, UiSortOrder.Menus).transform;
            _menu = Group("Menu", canvas);
            _select = Group("Select", canvas);
            _arena = Group("Arena", canvas);
            _codex = Group("Codex", canvas);
            BuildMenu(_menu.transform);
            BuildSelect(_select.transform);
            BuildArena(_arena.transform);
            var dim = UiFactory.Image("Dim", _codex.transform, new Color(0.02f, 0.02f, 0.04f, 0.88f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, PixelIcons.White);
            UiFactory.Stretch(dim.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Codex = new CodexPanel(font, fontMaterial);
            Codex.Build(_codex.transform, MetaProgress.Data, codex, meta, gladiators, locked, CloseCodex);
            RefreshCodexButton();
            Refit();
            Show(Screen.Menu);
        }

        void Start()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            TimeController.ResetForNewRun();
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
            var left = new Vector2(0f, 0.5f);
            var t = UiFactory.Text("Title", root, title, 130, Gold, TextAlignmentOptions.Left, left, left, new Vector2(110, 230), new Vector2(1100, 160), font, fontMaterial);
            t.fontStyle = FontStyles.Bold;
            var s = UiFactory.Text("Subtitle", root, subtitle, 38, Soft, TextAlignmentOptions.Left, left, left, new Vector2(116, 140), new Vector2(1100, 50), font, fontMaterial);
            s.fontStyle = FontStyles.Italic;
            _menuButtons = new[]
            {
                UiFactory.Button("Play", root, "PLAY", left, left, new Vector2(110, 10), new Vector2(360, 76), font, fontMaterial, OpenSelect, 40f),
                UiFactory.Button("Codex", root, "CODEX", left, left, new Vector2(110, -82), new Vector2(360, 76), font, fontMaterial, OpenCodex, 40f),
                UiFactory.Button("Options", root, "OPTIONS", left, left, new Vector2(110, -174), new Vector2(360, 76), font, fontMaterial, OpenOptions, 40f),
                UiFactory.Button("Quit", root, "QUIT", left, left, new Vector2(110, -266), new Vector2(360, 76), font, fontMaterial, Quit, 40f),
            };
            _menuFrames = new Image[_menuButtons.Length];
            for (int i = 0; i < _menuButtons.Length; i++)
            {
                var f = UiFactory.Image("Focus", _menuButtons[i].transform, Gold, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-10, 0), new Vector2(10, 60), PixelIcons.White);
                f.raycastTarget = false;
                _menuFrames[i] = f;
            }
            var hint = UiFactory.Text("Keys", root, "Enter / A: select     Esc / B: back", 24, new Color(1f, 1f, 1f, 0.45f), TextAlignmentOptions.Left, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(116, 40), new Vector2(900, 34), font, fontMaterial);
            hint.fontStyle = FontStyles.Normal;
            var bottomRight = new Vector2(1f, 0f);
            UiFactory.Text("Version", root, GameInfo.VersionLabel, 22, new Color(1f, 1f, 1f, 0.35f), TextAlignmentOptions.Right, bottomRight, bottomRight, new Vector2(-60, 40), new Vector2(400, 34), font, fontMaterial).fontStyle = FontStyles.Normal;
            var save = MetaProgress.Data;
            var corner = new Vector2(1f, 1f);
            var renown = UiFactory.Text("Renown", root, "RENOWN  " + save.renown, 34, Gold, TextAlignmentOptions.Right, corner, corner, new Vector2(-60, -60), new Vector2(600, 50), font, fontMaterial);
            renown.fontStyle = FontStyles.Bold;
            if (save.runs > 0)
                UiFactory.Text("Runs", root, save.runs + (save.runs == 1 ? " run  ·  " : " runs  ·  ") + save.victories + (save.victories == 1 ? " victory" : " victories"), 24, Soft, TextAlignmentOptions.Right, corner, corner, new Vector2(-60, -102), new Vector2(600, 36), font, fontMaterial).fontStyle = FontStyles.Normal;
        }

        /// <summary>The header, the key line under it, and BACK / FIGHT! in the bottom corners (both select screens).</summary>
        Button Chrome(Transform root, string header, string keys, UnityEngine.Events.UnityAction fight, UnityEngine.Events.UnityAction back)
        {
            var top = new Vector2(0.5f, 1f);
            UiFactory.Text("Header", root, header, 56, Gold, TextAlignmentOptions.Center, top, top, new Vector2(0, -54), new Vector2(1400, 76), font, fontMaterial);
            UiFactory.Text("Keys", root, keys, 22, Dim, TextAlignmentOptions.Center, top, top, new Vector2(0, -106), new Vector2(1400, 30), font, fontMaterial).fontStyle = FontStyles.Normal;
            UiFactory.Button("Back", root, "BACK", Vector2.zero, Vector2.zero, new Vector2(MenuLayout.Margin, 40), new Vector2(220, 76), font, fontMaterial, back, 34f);
            var corner = new Vector2(1f, 0f);
            return UiFactory.Button("Fight", root, "FIGHT!", corner, corner, new Vector2(-MenuLayout.Margin, 40), new Vector2(300, 80), font, fontMaterial, fight, 44f);
        }

        void BuildSelect(Transform root)
        {
            var slots = RosterGrid.Slots(gladiators, locked);
            int open = 0;
            while (open < slots.Length && slots[open].Open) open++;
            _available = new GladiatorDef[open];
            for (int i = 0; i < open; i++) _available[i] = slots[i].Def;

            _fight = Chrome(root, "CHOOSE YOUR GLADIATOR", "Arrows / d-pad: move     Enter / A: fight     Esc / B: back", StartRun, CloseSelect);
            _roster = new RosterGrid(font);
            _roster.Build(root, new Vector2(0f, 1f), new Vector2(MenuLayout.Margin, -MenuLayout.Top), slots, MenuLayout.Columns, codex, Choose);
            _tablet = new GladiatorTablet(font);
            var corner = new Vector2(1f, 1f);
            _tablet.Build(root, corner, corner, new Vector2(-MenuLayout.Margin, -MenuLayout.Top), codex);

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

            _arenaFight = Chrome(root, "CHOOSE YOUR ARENA", "Up / Down: climb     Enter / A: fight     Esc / B: back", Launch, BackToSelect);
            _ladder = new ArenaLadder(font);
            _ladder.Build(root, new Vector2(MenuLayout.Margin, MenuLayout.Bottom), steps, MetaProgress.Data, ChooseArena);
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

        /// <summary>Scales the grid, the ladder and the tablets to the canvas (the window may have changed size).</summary>
        void Refit()
        {
            _screenW = UnityEngine.Screen.width;
            _screenH = UnityEngine.Screen.height;
            var canvas = GridMath.CanvasSize(_screenW, _screenH, MenuLayout.Reference, MenuLayout.Match);
            float tablet = MenuLayout.TabletScale(canvas);
            if (_roster != null) _roster.Fit(MenuLayout.GridScale(canvas, _roster.Count));
            if (_tablet != null) _tablet.Fit(tablet);
            if (_ladder != null) _ladder.Fit(MenuLayout.LadderScale(canvas, _ladder.Count));
            if (_arenaTablet != null) _arenaTablet.Fit(tablet);
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
            _arenaTablet.Show(_ladder.Run(_step), open, ArenaLadder.Tier(_ladder.Run(_step), _step), MetaProgress.Data, meta);
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
            ShowHero(Selected); // a locked pick leaves the last earned gladiator standing
        }

        /// <summary>The diorama shows whoever is picked (the scene's own hero is the Nobody, hidden once another stands in).</summary>
        void ShowHero(GladiatorDef g)
        {
            if (hero == null || g == null || g.model == null) return;
            if (!_heroModels.TryGetValue(g.model, out var shown))
            {
                shown = Instantiate(g.model, hero.parent, false);
                shown.name = "Hero_" + g.id;
                shown.transform.SetPositionAndRotation(hero.position, hero.rotation);
                if (heroMaterial != null)
                    foreach (var r in shown.GetComponentsInChildren<Renderer>(true)) r.sharedMaterial = heroMaterial;
                _heroModels[g.model] = shown;
            }
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
            string wanted = CommandLine.Get("-gladiator");
            if (!string.IsNullOrEmpty(wanted)) // bot / perf runs: anyone, earned or not
                foreach (var d in gladiators) if (d != null && d.id == wanted) g = d;
            var r = SelectedRun;
            string arena = CommandLine.Get("-arena");
            if (!string.IsNullOrEmpty(arena))
                foreach (var d in AllRuns) if (d != null && d.id == arena) r = d;
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
            if (_screen == Screen.Select)
            {
                bool left = (kb != null && (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame)) || (pad != null && pad.dpad.left.wasPressedThisFrame);
                bool right = (kb != null && (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame)) || (pad != null && pad.dpad.right.wasPressedThisFrame);
                int dx = (right ? 1 : 0) - (left ? 1 : 0), dy = (down ? 1 : 0) - (up ? 1 : 0);
                if (dx != 0) MoveCursor(dx, 0);
                else if (dy != 0) MoveCursor(0, dy);
                if (confirm) StartRun();
                else if (back) CloseSelect();
                return;
            }
            if (_screen == Screen.Arena)
            {
                bool left = (kb != null && (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame)) || (pad != null && pad.dpad.left.wasPressedThisFrame);
                bool right = (kb != null && (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame)) || (pad != null && pad.dpad.right.wasPressedThisFrame);
                // the ladder climbs up and to the right
                if (up || right) Climb(1);
                else if (down || left) Climb(-1);
                if (confirm)
                {
                    if (ArenaFightEnabled) Launch();
                    else SfxPlayer.Play2D(SfxId.Blocked);
                }
                else if (back) BackToSelect();
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
            for (int i = 0; i < _menuFrames.Length; i++) _menuFrames[i].color = i == _focus ? Gold : Color.clear;
            if (confirm) _menuButtons[_focus].onClick.Invoke();
            else if (back) Quit();
        }
    }
}
