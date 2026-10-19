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
    /// Main menu → gladiator select → the run (M3.5), and the Codex (M3.6: records, unlocks, gladiators; CodexPanel).
    /// Built in code over the menu diorama. The slice has one gladiator; locked ones show as dark silhouettes with a
    /// cryptic hint. Renown from the save shows in the corner.
    /// Mouse, keyboard (Enter / Esc / arrows) and gamepad (South / East / d-pad). Under -bot the run starts at once.
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
        public RunDef run;
        public MetaConfig meta;
        public GladiatorDef[] gladiators = Array.Empty<GladiatorDef>();
        public Locked[] locked = Array.Empty<Locked>();
        public string title = "GLADIATOR";
        public string subtitle = "from the pit to the pantheon";

        static readonly Color Gold = new Color(1f, 0.84f, 0.3f);
        static readonly Color Soft = new Color(1f, 1f, 1f, 0.75f);
        static readonly Color CardColor = new Color(0.08f, 0.1f, 0.14f, 0.94f);

        enum Screen { Menu, Select, Codex }

        CanvasGroup _menu, _select, _codex;
        Button[] _menuButtons;
        int _focus;
        Screen _screen;
        Image[] _menuFrames;

        void Awake()
        {
            var canvas = UiFactory.Canvas("MenuCanvas", transform, UiSortOrder.Menus).transform;
            _menu = Group("Menu", canvas);
            _select = Group("Select", canvas);
            _codex = Group("Codex", canvas);
            BuildMenu(_menu.transform);
            BuildSelect(_select.transform);
            var dim = UiFactory.Image("Dim", _codex.transform, new Color(0.02f, 0.02f, 0.04f, 0.8f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, PixelIcons.White);
            UiFactory.Stretch(dim.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            new CodexPanel(font, fontMaterial).Build(_codex.transform, MetaProgress.Data, meta, gladiators, locked, CloseCodex);
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
                UiFactory.Button("Quit", root, "QUIT", left, left, new Vector2(110, -174), new Vector2(360, 76), font, fontMaterial, Quit, 40f),
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
            int n = gladiators.Length + locked.Length;
            float w = 330f, gap = 30f, x0 = -(n - 1) * (w + gap) * 0.5f;
            int k = 0;
            foreach (var g in gladiators)
            {
                if (g == null) continue;
                var card = Card(root, x0 + k++ * (w + gap), w, true);
                Line(card, g.displayName.ToUpperInvariant(), 36, Gold, 205);
                var d = Line(card, g.description, 26, Soft, 95, 150f);
                d.fontStyle = FontStyles.Normal;
                Line(card, StatsLine(g), 24, new Color(0.6f, 0.95f, 1f), -35, 80f);
                Line(card, "Starts with a Rusty Gladius", 22, Soft, -125, 60f).fontStyle = FontStyles.Normal;
            }
            foreach (var l in locked)
            {
                var card = Card(root, x0 + k++ * (w + gap), w, false);
                var sil = UiFactory.Image("Silhouette", card, new Color(0f, 0f, 0f, 0.85f), mid, mid, new Vector2(0, 70), new Vector2(150, 230), PixelIcons.White);
                sil.raycastTarget = false;
                Line(card, "???", 44, new Color(1f, 1f, 1f, 0.5f), 70);
                Line(card, l.name, 26, new Color(1f, 1f, 1f, 0.55f), -90);
                var h = Line(card, l.hint, 22, new Color(1f, 0.84f, 0.3f, 0.65f), -160, 70f);
                h.fontStyle = FontStyles.Italic;
            }
            UiFactory.Button("Fight", root, "FIGHT!", mid, mid, new Vector2(140, -380), new Vector2(300, 80), font, fontMaterial, StartRun, 44f);
            UiFactory.Button("Back", root, "BACK", mid, mid, new Vector2(-200, -380), new Vector2(220, 80), font, fontMaterial, CloseSelect, 34f);
        }

        static string StatsLine(GladiatorDef g)
        {
            float hp = 100f, move = 1f;
            foreach (var b in g.baseStats)
            {
                if (b.stat == StatId.MaxHp) hp = b.value;
                if (b.stat == StatId.MoveSpeed) move = b.value;
            }
            return Mathf.RoundToInt(hp) + " HP  ·  " + Mathf.RoundToInt(move * 100f) + "% speed";
        }

        Transform Card(Transform root, float x, float w, bool open)
        {
            var mid = new Vector2(0.5f, 0.5f);
            var border = UiFactory.Image("Card", root, open ? Gold : new Color(0.3f, 0.3f, 0.34f, 0.9f), mid, mid, new Vector2(x, 20), new Vector2(w, 520), PixelIcons.White);
            var inner = UiFactory.Image("Inner", border.transform, CardColor, mid, mid, Vector2.zero, new Vector2(w - 10, 510), PixelIcons.White);
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
            Set(_codex, screen == Screen.Codex);
        }

        static void Set(CanvasGroup g, bool on)
        {
            g.alpha = on ? 1f : 0f;
            g.interactable = g.blocksRaycasts = on;
        }

        void OpenSelect() => Show(Screen.Select);
        void CloseSelect() => Show(Screen.Menu);
        void OpenCodex() => Show(Screen.Codex);
        void CloseCodex() => Show(Screen.Menu);

        void StartRun()
        {
            GameSession.StartRun(gladiators.Length > 0 ? gladiators[0] : null, run);
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
            var kb = Keyboard.current;
            var pad = Gamepad.current;
            bool confirm = (kb != null && (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)) || (pad != null && pad.buttonSouth.wasPressedThisFrame);
            bool back = (kb != null && kb.escapeKey.wasPressedThisFrame) || (pad != null && pad.buttonEast.wasPressedThisFrame);
            bool up = (kb != null && (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame)) || (pad != null && pad.dpad.up.wasPressedThisFrame);
            bool down = (kb != null && (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame)) || (pad != null && pad.dpad.down.wasPressedThisFrame);
            if (_screen == Screen.Select)
            {
                if (confirm) StartRun();
                else if (back) CloseSelect();
                return;
            }
            if (_screen == Screen.Codex)
            {
                if (confirm || back) CloseCodex();
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
