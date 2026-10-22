using System;
using Gladiator.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Gladiator.UI
{
    /// <summary>
    /// Options (CLAUDE.md §4): master / effects / crowd / music volume, look sensitivity, invert Y, field of view, screen
    /// shake, render scale. Opened from the main menu and the pause screen; changes apply live (GameOptions.Apply) and
    /// are saved on close. Mouse drags the sliders; keyboard / gamepad: up-down picks a row, left-right adjusts,
    /// Esc / B / Enter closes.
    /// U1: a riveted `Bronze` panel (scaled down with UiKit.FitScale on small canvases), the header in Title 48; each row a
    /// Title 24 label, a bronze trough with a knob, the value in Number 26; the focused row in a gold focus ring; BACK is
    /// the panel's main action (bronze).
    /// </summary>
    public sealed class OptionsPanel : MonoBehaviour
    {
        public static OptionsPanel Instance { get; private set; }
        public static bool IsOpen => Instance != null && Instance._open;

        public TMP_FontAsset font;
        public Material fontMaterial;

        /// <summary>The panel's box at 1:1 (scaled down, never up, to fit the canvas).</summary>
        public static readonly Vector2 Size = new Vector2(1240f, 960f);
        const float RowTop = 320f, RowPitch = 70f, TroughH = 24f, KnobW = 18f, KnobH = 30f;

        struct Row
        {
            public string Label;
            public float Min, Max, Step;
            public Func<float> Get;
            public Action<float> Set;
            public Func<float, string> Show;
            public Slider Slider;
            public Image Fill;
            public TextMeshProUGUI Value;
            public Image Focus;
        }

        Row[] _rows;
        CanvasGroup _group;
        RectTransform _panel;
        bool _open, _syncing;
        int _focus, _screenW, _screenH;
        float _openedAt;

        // ---- tests (UiMenuLookTests) ------------------------------------------------------------------------------
        /// <summary>The frame the panel is drawn on (`FrameKind.Bronze`).</summary>
        public int PanelFrame => _panel != null ? UiFrames.KindOf(_panel.GetComponent<Image>()) : -1;
        public RectTransform Panel => _panel;
        public int RowCount => _rows != null ? _rows.Length : 0;
        /// <summary>Row `i`'s value label (its theme role) and its trough's fill (0–1).</summary>
        public Component ValueLabel(int i) => _rows[i].Value;
        public float FillAmount(int i) => _rows[i].Fill.fillAmount;

        void Awake()
        {
            Instance = this;
            var canvas = UiFactory.Canvas("OptionsCanvas", transform, UiSortOrder.Menus + 20).transform;
            var go = new GameObject("Options", typeof(RectTransform), typeof(CanvasGroup));
            go.transform.SetParent(canvas, false);
            UiFactory.Stretch((RectTransform)go.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _group = go.GetComponent<CanvasGroup>();
            var root = go.transform;
            var th = UiTheme.Current;
            var night = th.night;
            night.a = 0.85f;
            var dim = UiFactory.Image("Dim", root, night, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, PixelIcons.White);
            UiFactory.Stretch(dim.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            dim.raycastTarget = true;
            var mid = new Vector2(0.5f, 0.5f);
            var panel = UiKit.Frame("Panel", root, FrameKind.Bronze, mid, mid, Vector2.zero, Size, Color.white);
            panel.raycastTarget = true;
            _panel = panel.rectTransform;
            var box = _panel.transform;
            UiKit.Text("Title", box, "OPTIONS", TextRole.Title, 48f, th.gold, TextAlignmentOptions.Center, mid, mid, new Vector2(0, 410), new Vector2(1000, 62));

            var o = GameOptions.Data;
            _rows = new[]
            {
                Pct("Master volume", () => o.master, v => o.master = v),
                Pct("Effects volume", () => o.sfx, v => o.sfx = v),
                Pct("Crowd volume", () => o.crowd, v => o.crowd = v),
                Pct("Music volume  (no music yet)", () => o.music, v => o.music = v),
                new Row { Label = "Look sensitivity", Min = 0.2f, Max = 3f, Step = 0.1f, Get = () => o.lookSensitivity, Set = v => o.lookSensitivity = v, Show = v => v.ToString("0.0") + "×" },
                new Row { Label = "Invert look Y", Min = 0f, Max = 1f, Step = 1f, Get = () => o.invertY ? 1f : 0f, Set = v => o.invertY = v > 0.5f, Show = v => v > 0.5f ? "On" : "Off" },
                new Row { Label = "Field of view", Min = 50f, Max = 90f, Step = 1f, Get = () => o.fov, Set = v => o.fov = v, Show = v => Mathf.RoundToInt(v) + "°" },
                Pct("Screen shake", () => o.screenShake, v => o.screenShake = v),
                new Row { Label = "Render scale", Min = 0.5f, Max = 1f, Step = 0.05f, Get = () => o.renderScale, Set = v => o.renderScale = v, Show = v => Mathf.RoundToInt(v * 100f) + "%" },
            };
            for (int i = 0; i < _rows.Length; i++) BuildRow(box, i);
            UiKit.Button("Back", box, "BACK", ButtonStyle.Primary, mid, mid, new Vector2(0, -372), new Vector2(240, 66), Close, 30f);
            UiKit.Text("Keys", box, "Up / Down: pick     Left / Right: adjust     Esc / B: back", TextRole.Body, 22f, th.dust, TextAlignmentOptions.Center, mid, mid, new Vector2(0, -432), new Vector2(1100, 30));
            Set(false);
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        static Row Pct(string label, Func<float> get, Action<float> set) =>
            new Row { Label = label, Min = 0f, Max = 1f, Step = 0.05f, Get = get, Set = set, Show = v => Mathf.RoundToInt(v * 100f) + "%" };

        void BuildRow(Transform root, int i)
        {
            var th = UiTheme.Current;
            var mid = new Vector2(0.5f, 0.5f);
            float y = RowTop - i * RowPitch;
            ref var r = ref _rows[i];
            r.Focus = UiKit.Frame("Focus", root, FrameKind.Focus, mid, mid, new Vector2(0, y), new Vector2(1180, 62), th.gold);
            r.Focus.enabled = false;
            UiKit.Text("Label", root, r.Label, TextRole.Title, 24f, th.ink, TextAlignmentOptions.Left, mid, mid, new Vector2(-330, y), new Vector2(500, 34));
            r.Slider = MakeSlider(root, new Vector2(170, y), new Vector2(380, TroughH), out r.Fill);
            r.Slider.minValue = r.Min;
            r.Slider.maxValue = r.Max;
            r.Slider.wholeNumbers = false;
            r.Value = UiKit.Text("Value", root, "", TextRole.Number, 26f, th.ink, TextAlignmentOptions.Right, mid, mid, new Vector2(490, y), new Vector2(160, 34), TextLook.Outline);
            int index = i;
            r.Slider.onValueChanged.AddListener(v => OnSlider(index, v));
        }

        /// <summary>A bronze trough (UiKit.Bar: its fill follows the value) with a knob the mouse drags. The Slider has no
        /// fill rect of its own: the trough's Filled image shows the value.</summary>
        Slider MakeSlider(Transform parent, Vector2 pos, Vector2 size, out Image fill)
        {
            var mid = new Vector2(0.5f, 0.5f);
            var hit = UiFactory.Image("Slider", parent, Color.clear, mid, mid, pos, size + new Vector2(0f, 12f), PixelIcons.White);
            hit.raycastTarget = true; // the pointer grabs the whole row of the trough, not just its few pixels
            fill = UiKit.Bar("Trough", hit.transform, UiTheme.Current.gold, mid, mid, Vector2.zero, size);
            var handleArea = UiFactory.Rect("HandleArea", hit.transform, mid, mid, Vector2.zero, size);
            UiFactory.Stretch(handleArea, Vector2.zero, Vector2.one, new Vector2(KnobW * 0.5f, 0f), new Vector2(-KnobW * 0.5f, 0f));
            var knob = UiKit.Frame("Knob", handleArea, FrameKind.Knob, mid, mid, Vector2.zero, new Vector2(KnobW, KnobH), Color.white);
            var s = hit.gameObject.AddComponent<Slider>();
            s.handleRect = knob.rectTransform;
            s.targetGraphic = knob;
            s.direction = Slider.Direction.LeftToRight;
            s.navigation = new Navigation { mode = Navigation.Mode.None };
            // the Slider stretches the handle over its area's height: keep the knob's own size on top of that
            knob.rectTransform.sizeDelta = new Vector2(KnobW, KnobH - (size.y + 12f));
            return s;
        }

        /// <summary>Scales the panel down to the canvas (MenuLayout's canvas maths; never up).</summary>
        void Refit()
        {
            _screenW = Screen.width;
            _screenH = Screen.height;
            var canvas = Gladiator.Core.GridMath.CanvasSize(_screenW, _screenH, Gladiator.Core.MenuLayout.Reference, Gladiator.Core.MenuLayout.Match);
            float k = UiKit.FitScale(Size, canvas);
            _panel.localScale = new Vector3(k, k, 1f);
        }

        public void Open()
        {
            _openedAt = Time.unscaledTime;
            _focus = 0;
            Refit();
            Sync();
            Set(true);
        }

        public void Close()
        {
            if (!_open) return;
            GameOptions.Save();
            Set(false);
        }

        void Set(bool on)
        {
            _open = on;
            GameOptions.PanelOpen = on;
            _group.alpha = on ? 1f : 0f;
            _group.interactable = _group.blocksRaycasts = on;
        }

        void Sync()
        {
            _syncing = true;
            for (int i = 0; i < _rows.Length; i++)
            {
                float v = _rows[i].Get();
                _rows[i].Slider.value = v;
                _rows[i].Fill.fillAmount = Mathf.InverseLerp(_rows[i].Min, _rows[i].Max, v);
                _rows[i].Value.text = _rows[i].Show(v);
            }
            _syncing = false;
        }

        void OnSlider(int i, float v)
        {
            if (_syncing) return;
            ref var r = ref _rows[i];
            if (r.Step > 0f) v = Mathf.Clamp(Mathf.Round((v - r.Min) / r.Step) * r.Step + r.Min, r.Min, r.Max);
            r.Set(v);
            r.Fill.fillAmount = Mathf.InverseLerp(r.Min, r.Max, v);
            r.Value.text = r.Show(v);
            _focus = i;
            GameOptions.Apply();
        }

        void Update()
        {
            if (!_open) return;
            if (Screen.width != _screenW || Screen.height != _screenH) Refit();
            for (int i = 0; i < _rows.Length; i++) _rows[i].Focus.enabled = i == _focus;
            if (Time.unscaledTime - _openedAt < 0.15f) return; // the key that opened the panel
            var kb = Keyboard.current;
            var pad = Gamepad.current;
            bool up = (kb != null && (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame)) || (pad != null && pad.dpad.up.wasPressedThisFrame);
            bool down = (kb != null && (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame)) || (pad != null && pad.dpad.down.wasPressedThisFrame);
            bool left = (kb != null && (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame)) || (pad != null && pad.dpad.left.wasPressedThisFrame);
            bool right = (kb != null && (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame)) || (pad != null && pad.dpad.right.wasPressedThisFrame);
            bool back = (kb != null && (kb.escapeKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame)) || (pad != null && (pad.buttonEast.wasPressedThisFrame || pad.startButton.wasPressedThisFrame));
            if (up) _focus = (_focus + _rows.Length - 1) % _rows.Length;
            if (down) _focus = (_focus + 1) % _rows.Length;
            if (left || right)
            {
                ref var r = ref _rows[_focus];
                float v = Mathf.Clamp(r.Get() + (right ? r.Step : -r.Step), r.Min, r.Max);
                r.Slider.value = v; // fires OnSlider → applies
            }
            if (back) Close();
        }
    }
}
