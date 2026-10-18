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
        TextMeshProUGUI _timer, _level, _hpText, _gold, _kills, _toast, _announce, _pause, _debug, _tags;
        readonly char[] _timeBuf = new char[8];
        int _lastSecond = -1, _lastLevel = -1, _lastGold = -1, _lastKills = -1, _lastHp = -1, _lastMaxHp = -1;
        float _toastT = 10f, _announceT = 10f, _debugT;
        int _frames;
        int _lastTags = -1;
        float _lastSpeed = 1f;

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
            _tags = UiFactory.Text("DevTags", canvas, "", 22, new Color(1f, 0.84f, 0.3f), TextAlignmentOptions.Center, top, top, new Vector2(0, -88), new Vector2(700, 30), font, fontMaterial);
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
            // Director announcer (swarm / elite). Constant strings, so swapping them never allocates.
            _announce = UiFactory.Text("Announce", canvas, "THE PACK IS LOOSE!", 56, Color.white, TextAlignmentOptions.Center, top, top, new Vector2(0, -280), new Vector2(1100, 80), font, fontMaterial);
            _announce.alpha = 0f;
            _pause = UiFactory.Text("Pause", canvas, "PAUSED\n<size=36>click to resume</size>", 90, Color.white, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900, 260), font, fontMaterial);
            _pause.textWrappingMode = TextWrappingModes.Normal;
            _pause.alpha = 0f;
            _debug = UiFactory.Text("Debug", canvas, "", 20, new Color(1f, 1f, 1f, 0.75f), TextAlignmentOptions.BottomLeft, Vector2.zero, Vector2.zero, new Vector2(16, 12), new Vector2(900, 30), font, fontMaterial);
            _debug.fontStyle = FontStyles.Normal;
            // Pre-size TMP buffers with the longest strings each label shows and parse them now: in players the first
            // frame's huge delta fires the first debug update before anything renders, and TMP then grew its
            // character array one char at a time (~12 KB per growth) as the line got longer.
            PreSize(_debug, "88888 fps   88888 enemies   88888 pickups   F1 admin");
            PreSize(_hpText, "8888 / 8888");
            PreSize(_gold, "88888888");
            PreSize(_kills, "88888888");
            PreSize(_level, "LV 8888");
            PreSize(_announce, SwarmText);
            PreSize(_announce, EliteText);
        }

        static void PreSize(TMP_Text t, string longest)
        {
            string current = t.text;
            t.SetText(longest);
            t.ForceMeshUpdate(true, true);
            t.SetText(current);
        }

        void OnEnable()
        {
            GameLoop.Register(this, TickGroup.Late, 50);
            GameEvents.OnLevelUp.Subscribe(OnLevelUp);
            GameEvents.OnDirector.Subscribe(OnDirector);
        }

        void OnDisable()
        {
            GameLoop.Unregister(this);
            GameEvents.OnLevelUp.Unsubscribe(OnLevelUp);
            GameEvents.OnDirector.Unsubscribe(OnDirector);
        }

        void OnLevelUp(in LevelUpEvent e) => _toastT = 0f;

        const string SwarmText = "SWARM INCOMING!";
        const string EliteText = "AN ELITE ENTERS!";

        void OnDirector(in DirectorEvent e)
        {
            _announce.text = e.Kind == DirectorEventKind.Swarm ? SwarmText : EliteText;
            var c = e.Color;
            c.a = 1f;
            _announce.color = c;
            _announceT = 0f;
        }

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
                    int hp = Mathf.CeilToInt(h.Hp), max = Mathf.CeilToInt(h.MaxHp);
                    if (hp != _lastHp || max != _lastMaxHp) { _lastHp = hp; _lastMaxHp = max; _hpText.SetText("{0} / {1}", (float)hp, (float)max); }
                }
                _pause.alpha = player.IsPaused ? 1f : 0f;
            }

            // Dev switches in effect (so a test never silently runs with God mode on). Rebuilt only when they change.
            int tags = (DevSettings.GodMode ? 1 : 0) | (DevSettings.Timeless ? 2 : 0) | (DevSettings.AnyBuff ? 4 : 0) |
                       (!Mathf.Approximately(TimeController.GameSpeed, 1f) ? 8 : 0);
            if (tags != _lastTags || TimeController.GameSpeed != _lastSpeed)
            {
                _lastTags = tags;
                _lastSpeed = TimeController.GameSpeed;
                _tags.text = tags == 0 ? "" :
                    ((tags & 1) != 0 ? "GOD   " : "") + ((tags & 2) != 0 ? "TIMELESS   " : "") + ((tags & 4) != 0 ? "BUFFS   " : "") +
                    ((tags & 8) != 0 ? "SPEED x" + TimeController.GameSpeed.ToString("0.##") : "");
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

            // announcer: slide in, hold, fade (game time, so it waits during level-ups)
            _announceT += dt;
            if (_announceT < 2f)
            {
                float t = _announceT / 2f;
                _announce.alpha = t < 0.8f ? 1f : 1f - (t - 0.8f) / 0.2f;
                float s = t < 0.08f ? Mathf.Lerp(1.4f, 1f, t / 0.08f) : 1f;
                _announce.rectTransform.localScale = new Vector3(s, s, 1f);
            }
            else if (_announce.alpha > 0f) _announce.alpha = 0f;

            if (Keyboard.current != null && Keyboard.current.f3Key.wasPressedThisFrame) showDebug = !showDebug;
            _frames++;
            _debugT += unscaledDt;
            if (_debugT >= 0.5f)
            {
                if (showDebug) _debug.SetText("{0} fps   {1} enemies   {2} pickups   F1 admin", Mathf.Round(_frames / _debugT), DevCounters.Enemies, DevCounters.Pickups);
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
