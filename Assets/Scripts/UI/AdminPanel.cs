using Gladiator.Core;
using Gladiator.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gladiator.UI
{
    /// <summary>
    /// Draws AdminController (F1): buffs, run switches, enemy spawning, crowd and game speed. Dev tool: built in code,
    /// labels refreshed only while open; the game is paused while it's shown.
    /// </summary>
    public sealed class AdminPanel : MonoBehaviour, ITickable
    {
        public TMP_FontAsset font;
        public Material fontMaterial;

        static readonly string[] BuffNames = { "Damage", "Attack speed", "Range", "Max HP", "Move speed", "Pickup range", "Crit chance" };
        static readonly Color PanelColor = new Color(0.05f, 0.07f, 0.1f, 0.94f);
        static readonly Color Header = new Color(0.31f, 0.89f, 0.82f);
        static readonly Color On = new Color(0.25f, 0.62f, 0.32f, 1f);
        static readonly Color Off = new Color(0.16f, 0.2f, 0.25f, 0.98f);

        GameObject _root;
        Transform _panel;
        readonly TextMeshProUGUI[] _buffValues = new TextMeshProUGUI[AdminController.BuffCount];
        Button _god, _timeless, _director;
        TextMeshProUGUI _enemyName, _affix, _count, _crowd, _speed, _status;
        bool _shown;
        float _y;
        const float Width = 600f;

        void Awake()
        {
            var canvas = UiFactory.Canvas("AdminCanvas", transform, UiSortOrder.Debug).transform;
            _root = canvas.gameObject;
            var bg = UiFactory.Image("AdminPanel", canvas, PanelColor, Vector2.one, Vector2.one, new Vector2(-20f, -80f), new Vector2(Width, 1050f), PixelIcons.White); // right side, below LV: HP/gold stay visible
            bg.raycastTarget = true;
            _panel = bg.transform;
            _y = -12f;

            Title("ADMIN   |   F1 to close   |   game paused", 26f, Color.white);
            _status = Line("", 20f, new Color(1f, 1f, 1f, 0.6f));

            Section("PLAYER BUFFS  (on top of level-up cards)");
            for (int i = 0; i < AdminController.BuffCount; i++)
            {
                var b = (AdminController.Buff)i;
                Row(BuffNames[i], out _buffValues[i], () => Ctl.AdjustBuff(b, -1), () => Ctl.AdjustBuff(b, +1));
            }
            Buttons(("Reset buffs", () => Ctl.ResetBuffs()), ("+50% all", () => { for (int k = 0; k < AdminController.BuffCount - 1; k++) Ctl.AdjustBuff((AdminController.Buff)k, 5); }));

            Section("RUN");
            var r = Buttons(("God mode", () => Ctl.ToggleGodMode()), ("Timeless", () => Ctl.ToggleTimeless()));
            _god = r[0]; _timeless = r[1];
            Buttons(("Level up", () => Ctl.LevelUp()), ("+100 gold", () => Ctl.AddGold(100)), ("Heal", () => Ctl.Heal()));
            Buttons(("Timer to 0:10", () => Ctl.SetTimer(10f)), ("Timer +1:00", () => Ctl.AddTime(60f)), ("Kill me", () => Ctl.KillPlayer()));

            Section("ENEMIES");
            _director = Buttons(("Director", () => Ctl.ToggleDirector()), ("Swarm now", () => Ctl.SwarmNow()), ("Elite now", () => Ctl.EliteNow()))[0];
            Row("Type", out _enemyName, () => Ctl.CycleEnemy(-1), () => Ctl.CycleEnemy(+1), 300f);
            Row("Elite", out _affix, () => Ctl.CycleAffix(-1), () => Ctl.CycleAffix(+1), 300f);
            Row("Spawn count", out _count, () => Ctl.CycleCount(-1), () => Ctl.CycleCount(+1));
            Buttons(("Spawn near me", () => Ctl.SpawnNearPlayer(Ctl.SpawnCount)), ("Spawn around arena", () => Ctl.SpawnAroundArena(Ctl.SpawnCount)));
            Row("Crowd (keeps alive)", out _crowd, () => Ctl.AdjustCrowd(-25), () => Ctl.AdjustCrowd(+25));
            Buttons(("Kill all enemies", () => Ctl.KillAllEnemies()), ("Clear pickups", () => Ctl.ClearPickups()));

            Section("GAME");
            Row("Game speed", out _speed, () => Ctl.CycleSpeed(-1), () => Ctl.CycleSpeed(+1));
            Buttons(("Restart run", () => Ctl.Restart()));
            Line("Settings and buffs survive Restart/Retry (not a new play session).", 18f, new Color(1f, 1f, 1f, 0.45f));

            _root.SetActive(false);
        }

        static AdminController Ctl => AdminController.Instance;

        void OnEnable() => GameLoop.Register(this, TickGroup.Late, 70);
        void OnDisable() => GameLoop.Unregister(this);

        public void Tick(float dt, float unscaledDt)
        {
            bool open = Ctl != null && Ctl.IsOpen;
            if (open != _shown) { _shown = open; _root.SetActive(open); }
            if (open) Refresh();
        }

        void Refresh()
        {
            for (int i = 0; i < AdminController.BuffCount; i++)
            {
                var b = (AdminController.Buff)i;
                float v = AdminController.GetBuff(b);
                if (b == AdminController.Buff.Crit) _buffValues[i].SetText(v >= 0 ? "+{0} pts" : "{0} pts", v);
                else _buffValues[i].SetText(v >= 0 ? "+{0}%" : "{0}%", v);
            }
            Tint(_god, DevSettings.GodMode, "God mode: ON", "God mode: off");
            Tint(_timeless, DevSettings.Timeless, "Timeless: ON", "Timeless: off");
            Tint(_director, Ctl.DirectorOn, "Director: ON", "Director: off");
            var def = Ctl.Selected;
            _enemyName.text = def != null ? def.displayName : "(no EnemyDefs)";
            var affix = Ctl.Affix;
            _affix.text = affix != null ? affix.displayName : "None";
            _affix.color = affix != null ? affix.color : Color.white;
            _count.SetText("{0}", Ctl.SpawnCount);
            _crowd.SetText("{0}", Ctl.CrowdSize);
            _speed.SetText("x{0:2}", TimeController.GameSpeed);
            var em = EnemyManager.Instance;
            var run = RunState.Current;
            if (run != null) _status.SetText("enemies alive {0}   |   level {1}   |   {2}:{3:00} left", em != null ? em.AliveCount : 0, run.Level, Mathf.Floor(run.TimeRemaining / 60f), Mathf.Floor(run.TimeRemaining % 60f));
        }

        static void Tint(Button b, bool on, string onText, string offText)
        {
            ((Image)b.targetGraphic).color = on ? On : Off;
            UiFactory.Label(b).text = on ? onText : offText;
        }

        // ---------- layout helpers (top-down) ----------
        static readonly Vector2 TopLeft = new Vector2(0f, 1f);

        void Title(string text, float size, Color color)
        {
            UiFactory.Text("Title", _panel, text, size, color, TextAlignmentOptions.Left, TopLeft, TopLeft, new Vector2(20f, _y), new Vector2(Width - 40f, size + 10f), font, fontMaterial);
            _y -= size + 8f;
        }

        TextMeshProUGUI Line(string text, float size, Color color)
        {
            var t = UiFactory.Text("Line", _panel, text, size, color, TextAlignmentOptions.Left, TopLeft, TopLeft, new Vector2(20f, _y), new Vector2(Width - 40f, size + 8f), font, fontMaterial);
            t.fontStyle = FontStyles.Normal;
            _y -= size + 8f;
            return t;
        }

        void Section(string text)
        {
            _y -= 6f;
            UiFactory.Text("Section", _panel, text, 22f, Header, TextAlignmentOptions.Left, TopLeft, TopLeft, new Vector2(20f, _y), new Vector2(Width - 40f, 30f), font, fontMaterial);
            _y -= 30f;
        }

        void Row(string label, out TextMeshProUGUI value, UnityEngine.Events.UnityAction minus, UnityEngine.Events.UnityAction plus, float valueWidth = 150f)
        {
            const float h = 32f;
            var l = UiFactory.Text("RowLabel", _panel, label, 22f, Color.white, TextAlignmentOptions.Left, TopLeft, TopLeft, new Vector2(20f, _y), new Vector2(230f, h), font, fontMaterial);
            l.fontStyle = FontStyles.Normal;
            float x = Width - 20f - 50f - valueWidth - 50f;
            UiFactory.Button("Minus", _panel, "-", TopLeft, TopLeft, new Vector2(x, _y), new Vector2(46f, h), font, fontMaterial, minus, 28f);
            value = UiFactory.Text("Value", _panel, "", 22f, Color.white, TextAlignmentOptions.Center, TopLeft, TopLeft, new Vector2(x + 50f, _y), new Vector2(valueWidth, h), font, fontMaterial);
            UiFactory.Button("Plus", _panel, "+", TopLeft, TopLeft, new Vector2(x + 54f + valueWidth, _y), new Vector2(46f, h), font, fontMaterial, plus, 28f);
            _y -= h + 4f;
        }

        Button[] Buttons(params (string label, UnityEngine.Events.UnityAction onClick)[] items)
        {
            const float h = 34f, gap = 8f;
            float w = (Width - 40f - gap * (items.Length - 1)) / items.Length;
            var result = new Button[items.Length];
            for (int i = 0; i < items.Length; i++)
                result[i] = UiFactory.Button(items[i].label, _panel, items[i].label, TopLeft, TopLeft, new Vector2(20f + i * (w + gap), _y), new Vector2(w, h), font, fontMaterial, items[i].onClick, 21f);
            _y -= h + 6f;
            return result;
        }
    }
}
