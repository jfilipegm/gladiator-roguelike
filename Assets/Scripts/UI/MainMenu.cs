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
    /// Built in code over the menu diorama. Every GladiatorDef gets a card: earned ones can be picked (click, or
    /// left/right), locked ones show as a dark silhouette with their unlock's hint; `locked` lists gladiators that don't
    /// exist as data yet. Renown from the save shows in the corner.
    /// Mouse, keyboard (Enter / Esc / arrows) and gamepad (South / East / d-pad). Under -bot the run starts at once
    /// (`-gladiator &lt;id&gt;` picks who and `-arena &lt;run id&gt;` where, earned or not).
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
        static readonly Color CardColor = new Color(0.08f, 0.1f, 0.14f, 0.94f);

        enum Screen { Menu, Select, Arena, Codex }

        CanvasGroup _menu, _select, _arena, _codex;
        RunDef[] _openRuns = Array.Empty<RunDef>();
        Image[] _runBorders = Array.Empty<Image>();
        int _runPick;
        Button[] _menuButtons;
        int _focus;
        GladiatorDef[] _available = Array.Empty<GladiatorDef>();
        Image[] _cardBorders = Array.Empty<Image>();
        int _pick;
        readonly System.Collections.Generic.Dictionary<GameObject, GameObject> _heroModels = new System.Collections.Generic.Dictionary<GameObject, GameObject>();

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
        Screen _screen;
        Image[] _menuFrames;

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

        void BuildSelect(Transform root)
        {
            var mid = new Vector2(0.5f, 0.5f);
            UiFactory.Text("Header", root, "CHOOSE YOUR GLADIATOR", 64, Gold, TextAlignmentOptions.Center, mid, mid, new Vector2(0, 360), new Vector2(1400, 90), font, fontMaterial);
            int open = 0, n = locked.Length;
            foreach (var g in gladiators) if (g != null) { n++; if (IsAvailable(g)) open++; }
            _available = new GladiatorDef[open];
            _cardBorders = new Image[open];
            float w = 330f, gap = 30f, x0 = -(n - 1) * (w + gap) * 0.5f;
            int k = 0, a = 0;
            foreach (var g in gladiators)
            {
                if (g == null) continue;
                float x = x0 + k++ * (w + gap);
                if (!IsAvailable(g)) { LockedCard(root, x, w, g.unlock != null ? g.unlock.hint : "???"); continue; }
                int index = a;
                var card = Card(root, x, w, true, out var border);
                var hit = border.gameObject.AddComponent<Button>();
                hit.transition = Selectable.Transition.None;
                hit.onClick.AddListener(() => Choose(index));
                _available[a] = g;
                _cardBorders[a++] = border;
                var title = Line(card, g.displayName.ToUpperInvariant(), 36, Gold, 205);
                title.textWrappingMode = TextWrappingModes.NoWrap; // long names shrink instead of spilling onto the text
                title.enableAutoSizing = true;
                title.fontSizeMin = 20f;
                title.fontSizeMax = 36f;
                Line(card, g.description, 25, Soft, 105, 150f).fontStyle = FontStyles.Normal;
                Line(card, StatsLine(g), 24, new Color(0.6f, 0.95f, 1f), -15, 60f);
                if (!string.IsNullOrEmpty(g.tradeoff)) Line(card, g.tradeoff, 21, Soft, -85, 70f).fontStyle = FontStyles.Italic;
                Line(card, g.SelectLine, 22, Soft, -170, 60f).fontStyle = FontStyles.Normal;
            }
            foreach (var l in locked) LockedCard(root, x0 + k++ * (w + gap), w, l.hint);
            // the last gladiator played this session stays picked
            _pick = 0;
            for (int i = 0; i < _available.Length; i++) if (_available[i] == GameSession.Gladiator) _pick = i;
            RefreshPick();
            UiFactory.Button("Fight", root, "FIGHT!", mid, mid, new Vector2(140, -380), new Vector2(300, 80), font, fontMaterial, StartRun, 44f);
            UiFactory.Button("Back", root, "BACK", mid, mid, new Vector2(-200, -380), new Vector2(220, 80), font, fontMaterial, CloseSelect, 34f);
        }

        void BuildArena(Transform root)
        {
            var mid = new Vector2(0.5f, 0.5f);
            UiFactory.Text("Header", root, "CHOOSE YOUR ARENA", 64, Gold, TextAlignmentOptions.Center, mid, mid, new Vector2(0, 360), new Vector2(1400, 90), font, fontMaterial);
            var all = AllRuns;
            int open = 0, n = 0;
            foreach (var r in all) if (r != null) { n++; if (IsAvailable(r)) open++; }
            _openRuns = new RunDef[open];
            _runBorders = new Image[open];
            float w = 420f, gap = 40f, x0 = -(n - 1) * (w + gap) * 0.5f;
            int k = 0, a = 0;
            foreach (var r in all)
            {
                if (r == null) continue;
                float x = x0 + k++ * (w + gap);
                if (!IsAvailable(r)) { LockedCard(root, x, w, r.unlock != null ? r.unlock.hint : "???"); continue; }
                int index = a;
                var card = Card(root, x, w, true, out var border);
                var hit = border.gameObject.AddComponent<Button>();
                hit.transition = Selectable.Transition.None;
                hit.onClick.AddListener(() => ChooseArena(index));
                _openRuns[a] = r;
                _runBorders[a++] = border;
                var title = Line(card, r.DisplayName.ToUpperInvariant(), 36, Gold, 205);
                title.textWrappingMode = TextWrappingModes.NoWrap;
                title.enableAutoSizing = true;
                title.fontSizeMin = 20f;
                title.fontSizeMax = 36f;
                Line(card, r.description, 25, Soft, 20, 300f).fontStyle = FontStyles.Normal;
            }
            // the last arena played this session stays picked
            _runPick = 0;
            for (int i = 0; i < _openRuns.Length; i++) if (_openRuns[i] == GameSession.Run) _runPick = i;
            RefreshRunPick();
            UiFactory.Button("Fight", root, "FIGHT!", mid, mid, new Vector2(140, -380), new Vector2(300, 80), font, fontMaterial, Launch, 44f);
            UiFactory.Button("Back", root, "BACK", mid, mid, new Vector2(-200, -380), new Vector2(220, 80), font, fontMaterial, BackToSelect, 34f);
        }

        /// <summary>Picks arena `r` if it's earned (tests). False if it's locked or unknown.</summary>
        public bool PickArena(RunDef r)
        {
            for (int i = 0; i < _openRuns.Length; i++)
                if (_openRuns[i] == r) { _runPick = i; RefreshRunPick(); return true; }
            return false;
        }

        void ChooseArena(int index)
        {
            if (index == _runPick) { Launch(); return; }
            _runPick = index;
            SfxPlayer.Play2D(SfxId.Card);
            RefreshRunPick();
        }

        void RefreshRunPick()
        {
            for (int i = 0; i < _runBorders.Length; i++)
                _runBorders[i].color = i == _runPick ? Gold : new Color(0.55f, 0.48f, 0.3f, 0.9f);
        }

        void LockedCard(Transform root, float x, float w, string hint)
        {
            var mid = new Vector2(0.5f, 0.5f);
            var card = Card(root, x, w, false, out _);
            var sil = UiFactory.Image("Silhouette", card, new Color(0f, 0f, 0f, 0.85f), mid, mid, new Vector2(0, 70), new Vector2(150, 230), PixelIcons.White);
            sil.raycastTarget = false;
            Line(card, "???", 44, new Color(1f, 1f, 1f, 0.5f), 70);
            Line(card, hint, 22, new Color(1f, 0.84f, 0.3f, 0.65f), -150, 90f).fontStyle = FontStyles.Italic;
        }

        /// <summary>Picks `g` if it's earned (tests, and later a gamepad cursor). False if it's locked or unknown.</summary>
        public bool Pick(GladiatorDef g)
        {
            for (int i = 0; i < _available.Length; i++)
                if (_available[i] == g) { _pick = i; RefreshPick(); return true; }
            return false;
        }

        void Choose(int index)
        {
            if (index == _pick) { StartRun(); return; } // a second click on the picked card fights
            _pick = index;
            SfxPlayer.Play2D(SfxId.Card);
            RefreshPick();
        }

        void RefreshPick()
        {
            for (int i = 0; i < _cardBorders.Length; i++)
                _cardBorders[i].color = i == _pick ? Gold : new Color(0.55f, 0.48f, 0.3f, 0.9f);
            ShowHero(Selected);
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

        static string StatsLine(GladiatorDef g)
        {
            float hp = g.BaseValue(StatId.MaxHp, 100f), move = g.BaseValue(StatId.MoveSpeed, 1f);
            return Mathf.RoundToInt(hp) + " HP  ·  " + Mathf.RoundToInt(move * 100f) + "% speed";
        }

        Transform Card(Transform root, float x, float w, bool open, out Image border)
        {
            var mid = new Vector2(0.5f, 0.5f);
            border = UiFactory.Image("Card", root, open ? Gold : new Color(0.3f, 0.3f, 0.34f, 0.9f), mid, mid, new Vector2(x, 20), new Vector2(w, 520), PixelIcons.White);
            border.raycastTarget = open; // the card's button needs the hit (UiFactory images ignore the pointer)
            var inner = UiFactory.Image("Inner", border.transform, CardColor, mid, mid, Vector2.zero, new Vector2(w - 10, 510), PixelIcons.White);
            inner.raycastTarget = false; // clicks reach the border's button
            return inner.transform;
        }

        TextMeshProUGUI Line(Transform card, string text, float size, Color color, float y, float height = 50f)
        {
            var mid = new Vector2(0.5f, 0.5f);
            var t = UiFactory.Text("Line", card, text, size, color, TextAlignmentOptions.Center, mid, mid, new Vector2(0, y), new Vector2(300, height), font, fontMaterial);
            t.textWrappingMode = TextWrappingModes.Normal;
            return t;
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
        /// Bot and perf runs always go straight in.</summary>
        void StartRun()
        {
            bool bot = CommandLine.Has("-bot") || CommandLine.Has("-stress");
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
                if (_available.Length > 1 && (left || right))
                {
                    _pick = (_pick + (right ? 1 : _available.Length - 1)) % _available.Length;
                    SfxPlayer.Play2D(SfxId.Card);
                    RefreshPick();
                }
                if (confirm) StartRun();
                else if (back) CloseSelect();
                return;
            }
            if (_screen == Screen.Arena)
            {
                bool left = (kb != null && (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame)) || (pad != null && pad.dpad.left.wasPressedThisFrame);
                bool right = (kb != null && (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame)) || (pad != null && pad.dpad.right.wasPressedThisFrame);
                if (_openRuns.Length > 1 && (left || right))
                {
                    _runPick = (_runPick + (right ? 1 : _openRuns.Length - 1)) % _openRuns.Length;
                    SfxPlayer.Play2D(SfxId.Card);
                    RefreshRunPick();
                }
                if (confirm) Launch();
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
