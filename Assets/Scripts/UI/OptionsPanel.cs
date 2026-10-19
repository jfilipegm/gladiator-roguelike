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
    /// </summary>
    public sealed class OptionsPanel : MonoBehaviour
    {
        public static OptionsPanel Instance { get; private set; }
        public static bool IsOpen => Instance != null && Instance._open;

        public TMP_FontAsset font;
        public Material fontMaterial;

        static readonly Color Gold = new Color(1f, 0.84f, 0.3f);
        static readonly Color Soft = new Color(1f, 1f, 1f, 0.75f);

        struct Row
        {
            public string Label;
            public float Min, Max, Step;
            public Func<float> Get;
            public Action<float> Set;
            public Func<float, string> Show;
            public Slider Slider;
            public TextMeshProUGUI Value;
            public Image Focus;
        }

        Row[] _rows;
        CanvasGroup _group;
        bool _open, _syncing;
        int _focus;
        float _openedAt;

        void Awake()
        {
            Instance = this;
            var canvas = UiFactory.Canvas("OptionsCanvas", transform, UiSortOrder.Menus + 20).transform;
            var go = new GameObject("Options", typeof(RectTransform), typeof(CanvasGroup));
            go.transform.SetParent(canvas, false);
            UiFactory.Stretch((RectTransform)go.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _group = go.GetComponent<CanvasGroup>();
            var root = go.transform;
            var dim = UiFactory.Image("Dim", root, new Color(0.02f, 0.02f, 0.04f, 0.85f), Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, PixelIcons.White);
            UiFactory.Stretch(dim.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            dim.raycastTarget = true;
            var mid = new Vector2(0.5f, 0.5f);
            UiFactory.Text("Title", root, "OPTIONS", 64, Gold, TextAlignmentOptions.Center, mid, mid, new Vector2(0, 400), new Vector2(1000, 90), font, fontMaterial).fontStyle = FontStyles.Bold;

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
            for (int i = 0; i < _rows.Length; i++) BuildRow(root, i);
            UiFactory.Button("Back", root, "BACK", mid, mid, new Vector2(0, -420), new Vector2(240, 76), font, fontMaterial, Close, 34f);
            UiFactory.Text("Keys", root, "Up / Down: pick     Left / Right: adjust     Esc / B: back", 22, new Color(1f, 1f, 1f, 0.45f), TextAlignmentOptions.Center, mid, mid, new Vector2(0, -480), new Vector2(1200, 34), font, fontMaterial).fontStyle = FontStyles.Normal;
            Set(false);
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        static Row Pct(string label, Func<float> get, Action<float> set) =>
            new Row { Label = label, Min = 0f, Max = 1f, Step = 0.05f, Get = get, Set = set, Show = v => Mathf.RoundToInt(v * 100f) + "%" };

        void BuildRow(Transform root, int i)
        {
            var mid = new Vector2(0.5f, 0.5f);
            float y = 300f - i * 76f;
            ref var r = ref _rows[i];
            r.Focus = UiFactory.Image("Focus", root, new Color(1f, 0.84f, 0.3f, 0.12f), mid, mid, new Vector2(0, y), new Vector2(1180, 66), PixelIcons.White);
            r.Focus.raycastTarget = false;
            var l = UiFactory.Text("Label", root, r.Label, 30, Soft, TextAlignmentOptions.Left, mid, mid, new Vector2(-330, y), new Vector2(500, 50), font, fontMaterial);
            l.fontStyle = FontStyles.Normal;
            r.Slider = MakeSlider(root, new Vector2(170, y), new Vector2(380, 18));
            r.Slider.minValue = r.Min;
            r.Slider.maxValue = r.Max;
            r.Slider.wholeNumbers = false;
            r.Value = UiFactory.Text("Value", root, "", 30, Color.white, TextAlignmentOptions.Right, mid, mid, new Vector2(490, y), new Vector2(160, 50), font, fontMaterial);
            int index = i;
            r.Slider.onValueChanged.AddListener(v => OnSlider(index, v));
        }

        Slider MakeSlider(Transform parent, Vector2 pos, Vector2 size)
        {
            var mid = new Vector2(0.5f, 0.5f);
            var bg = UiFactory.Image("Slider", parent, new Color(0.16f, 0.18f, 0.24f, 1f), mid, mid, pos, size, PixelIcons.White);
            bg.raycastTarget = true;
            var fillArea = UiFactory.Rect("FillArea", bg.transform, mid, mid, Vector2.zero, size);
            UiFactory.Stretch(fillArea, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var fill = UiFactory.Image("Fill", fillArea, Gold, mid, mid, Vector2.zero, Vector2.zero, PixelIcons.White);
            fill.raycastTarget = false;
            var handleArea = UiFactory.Rect("HandleArea", bg.transform, mid, mid, Vector2.zero, size);
            UiFactory.Stretch(handleArea, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var handle = UiFactory.Image("Handle", handleArea, Color.white, mid, mid, Vector2.zero, new Vector2(16f, 16f), PixelIcons.White);
            var s = bg.gameObject.AddComponent<Slider>();
            s.fillRect = fill.rectTransform;
            s.handleRect = handle.rectTransform;
            s.targetGraphic = handle;
            s.direction = Slider.Direction.LeftToRight;
            s.navigation = new Navigation { mode = Navigation.Mode.None };
            fill.rectTransform.sizeDelta = Vector2.zero;
            handle.rectTransform.sizeDelta = new Vector2(16f, 16f);
            return s;
        }

        public void Open()
        {
            _openedAt = Time.unscaledTime;
            _focus = 0;
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
            r.Value.text = r.Show(v);
            _focus = i;
            GameOptions.Apply();
        }

        void Update()
        {
            if (!_open) return;
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
