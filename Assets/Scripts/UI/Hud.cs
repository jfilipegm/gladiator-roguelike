using Gladiator.Core;
using Gladiator.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Gladiator.UI
{
    /// <summary>
    /// In-run HUD (M1.3): XP bar (top), run timer (top centre), level (top right), HP bar + gold + kills (top left),
    /// LEVEL UP toast, pause overlay, F3 debug line. Built in code; texts only change when values change and use
    /// allocation-free TMP calls.
    /// </summary>
    public sealed class Hud : MonoBehaviour, ITickable
    {
        public TMP_FontAsset font;
        public Material fontMaterial;
        public bool showDebug = true;

        Image _xpFill, _hpFill;
        TextMeshProUGUI _timer, _level, _hpText, _gold, _kills, _toast, _pause, _debug;
        readonly char[] _timeBuf = new char[8];
        int _lastSecond = -1, _lastLevel = -1, _lastGold = -1, _lastKills = -1, _lastHp = -1, _lastMaxHp = -1;
        float _toastT = 10f, _debugT;
        int _frames;

        static readonly Color XpColor = new Color(0.31f, 0.89f, 0.82f);
        static readonly Color HpColor = new Color(0.88f, 0.2f, 0.17f);
        static readonly Color Gold = new Color(1f, 0.84f, 0.3f);

        void Awake()
        {
            var canvas = UiFactory.Canvas("HudCanvas", transform, UiSortOrder.Hud).transform;
            var top = new Vector2(0.5f, 1f);

            // XP bar across the top
            var xpBack = UiFactory.Image("XpBar", canvas, new Color(0f, 0f, 0f, 0.55f), top, top, Vector2.zero, new Vector2(10, 18), PixelIcons.White);
            UiFactory.Stretch(xpBack.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -18), Vector2.zero);
            _xpFill = UiFactory.Image("XpFill", xpBack.transform, XpColor, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, PixelIcons.White);
            UiFactory.Stretch(_xpFill.rectTransform, Vector2.zero, Vector2.one, new Vector2(2, 2), new Vector2(-2, -2));
            _xpFill.type = Image.Type.Filled;
            _xpFill.fillMethod = Image.FillMethod.Horizontal;

            _timer = UiFactory.Text("Timer", canvas, "8:00", 58, Color.white, TextAlignmentOptions.Center, top, top, new Vector2(0, -26), new Vector2(300, 70), font, fontMaterial);
            _level = UiFactory.Text("Level", canvas, "LV 1", 46, Color.white, TextAlignmentOptions.Right, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-28, -26), new Vector2(300, 60), font, fontMaterial);

            // HP bar + icon
            var tl = new Vector2(0, 1);
            UiFactory.Image("HpIcon", canvas, Color.white, tl, tl, new Vector2(24, -32), new Vector2(34, 34), PixelIcons.Heart);
            _hpFill = UiFactory.Bar("HpBar", canvas, new Color(0.08f, 0.05f, 0.06f, 0.8f), HpColor, tl, tl, new Vector2(66, -32), new Vector2(340, 34));
            _hpText = UiFactory.Text("HpText", _hpFill.transform.parent, "100 / 100", 24, Color.white, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(340, 34), font, fontMaterial);

            UiFactory.Image("GoldIcon", canvas, Color.white, tl, tl, new Vector2(26, -82), new Vector2(30, 30), PixelIcons.Coin);
            _gold = UiFactory.Text("Gold", canvas, "0", 32, Gold, TextAlignmentOptions.Left, tl, tl, new Vector2(66, -78), new Vector2(150, 40), font, fontMaterial);
            UiFactory.Image("KillIcon", canvas, Color.white, tl, tl, new Vector2(200, -82), new Vector2(30, 30), PixelIcons.Skull);
            _kills = UiFactory.Text("Kills", canvas, "0", 32, Color.white, TextAlignmentOptions.Left, tl, tl, new Vector2(240, -78), new Vector2(150, 40), font, fontMaterial);

            _toast = UiFactory.Text("Toast", canvas, "LEVEL UP!", 84, Gold, TextAlignmentOptions.Center, top, top, new Vector2(0, -170), new Vector2(900, 110), font, fontMaterial);
            _toast.alpha = 0f;
            _pause = UiFactory.Text("Pause", canvas, "PAUSED\n<size=36>click to resume</size>", 90, Color.white, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900, 260), font, fontMaterial);
            _pause.textWrappingMode = TextWrappingModes.Normal;
            _pause.alpha = 0f;
            _debug = UiFactory.Text("Debug", canvas, "", 20, new Color(1f, 1f, 1f, 0.75f), TextAlignmentOptions.BottomLeft, Vector2.zero, Vector2.zero, new Vector2(16, 12), new Vector2(900, 30), font, fontMaterial);
            _debug.fontStyle = FontStyles.Normal;
            _debug.SetText("88888 fps 88888 enemies 88888 pickups"); // pre-size buffers
        }

        void OnEnable()
        {
            GameLoop.Register(this, TickGroup.Late, 50);
            GameEvents.OnLevelUp.Subscribe(OnLevelUp);
        }

        void OnDisable()
        {
            GameLoop.Unregister(this);
            GameEvents.OnLevelUp.Unsubscribe(OnLevelUp);
        }

        void OnLevelUp(in LevelUpEvent e) => _toastT = 0f;

        public void Tick(float dt, float unscaledDt)
        {
            var run = RunState.Current;
            var player = Player.Main;
            if (run != null)
            {
                _xpFill.fillAmount = run.Xp01;
                int sec = Mathf.CeilToInt(run.TimeRemaining);
                if (sec != _lastSecond) { _lastSecond = sec; WriteTime(sec); }
                if (run.Level != _lastLevel) { _lastLevel = run.Level; _level.SetText("LV {0}", run.Level); }
                if (run.Gold != _lastGold) { _lastGold = run.Gold; _gold.SetText("{0}", run.Gold); }
                if (run.Kills != _lastKills) { _lastKills = run.Kills; _kills.SetText("{0}", run.Kills); }
            }
            if (player != null)
            {
                var h = player.GetComponent<PlayerHealth>();
                if (h != null)
                {
                    _hpFill.fillAmount = Mathf.MoveTowards(_hpFill.fillAmount, h.Hp01, unscaledDt * 2.5f);
                    int hp = Mathf.CeilToInt(h.Hp), max = Mathf.CeilToInt(h.maxHp);
                    if (hp != _lastHp || max != _lastMaxHp) { _lastHp = hp; _lastMaxHp = max; _hpText.SetText("{0} / {1}", (float)hp, (float)max); }
                }
                _pause.alpha = player.IsPaused ? 1f : 0f;
            }

            // LEVEL UP toast: pop in, hold, fade
            _toastT += unscaledDt;
            if (_toastT < 1.2f)
            {
                float t = _toastT / 1.2f;
                _toast.alpha = t < 0.8f ? 1f : 1f - (t - 0.8f) / 0.2f;
                float s = t < 0.12f ? Mathf.Lerp(1.6f, 1f, t / 0.12f) : 1f;
                _toast.rectTransform.localScale = new Vector3(s, s, 1f);
            }
            else _toast.alpha = 0f;

            if (Keyboard.current != null && Keyboard.current.f3Key.wasPressedThisFrame) showDebug = !showDebug;
            _frames++;
            _debugT += unscaledDt;
            if (_debugT >= 0.5f)
            {
                if (showDebug) _debug.SetText("{0} fps   {1} enemies   {2} pickups", Mathf.Round(_frames / _debugT), DevCounters.Enemies, DevCounters.Pickups);
                else _debug.SetText("");
                _frames = 0;
                _debugT = 0f;
            }
        }

        void WriteTime(int seconds)
        {
            int m = seconds / 60, s = seconds % 60, n = 0;
            if (m >= 10) _timeBuf[n++] = (char)('0' + m / 10 % 10);
            _timeBuf[n++] = (char)('0' + m % 10);
            _timeBuf[n++] = ':';
            _timeBuf[n++] = (char)('0' + s / 10);
            _timeBuf[n++] = (char)('0' + s % 10);
            _timer.SetCharArray(_timeBuf, 0, n);
        }
    }
}
