using Gladiator.Core;
using Gladiator.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gladiator.UI
{
    /// <summary>
    /// Draws AdminController (F1): buffs, run switches, enemy spawning and FREEZE, crowd and game speed; the second column
    /// has the upgrades and FIGHT A BOSS (one button per menu run, T1). Dev tool: built in code, labels refreshed only
    /// while open; the game is paused while it's shown.
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
        Button _god, _timeless, _director, _freeze;
        TextMeshProUGUI _enemyName, _affix, _count, _crowd, _speed, _status, _upgradeName, _upgradeInfo;
        Button _rarity;
        static readonly string[] RarityLabels = { "Rarity: Common", "Rarity: Uncommon", "Rarity: Rare", "Rarity: Legendary" };
        bool _shown;
        float _y;
        const float Width = 600f;
        // FIGHT A BOSS: a button per GameSession.KnownRuns entry (rebuilt when that list changes), the column sized to them
        const int MaxBossRuns = 8;
        readonly Button[] _bossButtons = new Button[MaxBossRuns];
        RectTransform _panel2;
        float _panel2Base;
        RunDef[] _bossRunsShown;
        int _bossCountShown = -1;

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
            Buttons(("Favor +50", () => Ctl.AddFavor(50f)), ("Gift now", () => Ctl.GiftNow()), ("Whim now", () => Ctl.WhimNow()));

            Section("ENEMIES");
            _director = Buttons(("Director", () => Ctl.ToggleDirector()), ("Swarm now", () => Ctl.SwarmNow()), ("Elite now", () => Ctl.EliteNow()))[0];
            Row("Type", out _enemyName, () => Ctl.CycleEnemy(-1), () => Ctl.CycleEnemy(+1), 300f);
            Row("Elite", out _affix, () => Ctl.CycleAffix(-1), () => Ctl.CycleAffix(+1), 300f);
            Row("Spawn count", out _count, () => Ctl.CycleCount(-1), () => Ctl.CycleCount(+1));
            Buttons(("Spawn near me", () => Ctl.SpawnNearPlayer(Ctl.SpawnCount)), ("Spawn around arena", () => Ctl.SpawnAroundArena(Ctl.SpawnCount)));
            Row("Crowd (keeps alive)", out _crowd, () => Ctl.AdjustCrowd(-25), () => Ctl.AdjustCrowd(+25));
            _freeze = Buttons(("Freeze", () => Ctl.ToggleFreeze()), ("Kill all", () => Ctl.KillAllEnemies()), ("Clear pickups", () => Ctl.ClearPickups()))[0];

            Section("GAME");
            Row("Game speed", out _speed, () => Ctl.CycleSpeed(-1), () => Ctl.CycleSpeed(+1));
            Buttons(("Restart run", () => Ctl.Restart()));
            Line("Settings and buffs survive Restart/Retry (not a new play session).", 18f, new Color(1f, 1f, 1f, 0.45f));

            // second column, left of the first: build tools (weapons and chests join it later), then the boss fights
            var bg2 = UiFactory.Image("AdminPanel2", canvas, PanelColor, Vector2.one, Vector2.one, new Vector2(-20f - Width - 12f, -80f), new Vector2(Width, 190f), PixelIcons.White);
            bg2.raycastTarget = true;
            _panel = bg2.transform;
            _panel2 = bg2.rectTransform;
            _y = -12f;
            Section("UPGRADES  (skip the level-up cards)");
            Row("Upgrade", out _upgradeName, () => Ctl.CycleUpgrade(-1), () => Ctl.CycleUpgrade(+1), 300f);
            _upgradeInfo = Line("", 20f, new Color(1f, 1f, 1f, 0.7f));
            _rarity = Buttons((RarityLabels[0], () => Ctl.CycleRarity()), ("Give 1 level", () => Ctl.GiveUpgrade()))[0];
            Section("FIGHT A BOSS  (as you are; boss 3 s in)");
            _panel2Base = -_y; // the column's height above the boss buttons (each row adds 40)
            for (int k = 0; k < MaxBossRuns; k++)
            {
                int run = k;
                _bossButtons[k] = Buttons(("Boss", () => Ctl.FightBoss(run)))[0];
            }

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
            RefreshBossRuns();
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
            Tint(_freeze, DevSettings.FreezeEnemies, "Freeze: ON", "Freeze: off");
            var def = Ctl.Selected;
            _enemyName.text = def != null ? def.displayName : "(no EnemyDefs)";
            var affix = Ctl.Affix;
            _affix.text = affix != null ? affix.displayName : "None";
            _affix.color = affix != null ? affix.color : Color.white;
            _count.SetText("{0}", Ctl.SpawnCount);
            _crowd.SetText("{0}", Ctl.CrowdSize);
            _speed.SetText("x{0:2}", TimeController.GameSpeed);
            var up = Ctl.Upgrade;
            _upgradeName.text = up != null ? up.displayName : "(no upgrades)";
            var build = Ctl.Build;
            if (up != null && build != null) _upgradeInfo.SetText("owned: level {0} of {1}", (float)build.State.LevelOf(up), (float)up.maxLevel);
            var rarityLabel = UiFactory.Label(_rarity);
            rarityLabel.text = RarityLabels[(int)Ctl.SelectedRarity];
            if (build != null && build.config != null) rarityLabel.color = build.config.RarityColor(Ctl.SelectedRarity);
            var em = EnemyManager.Instance;
            var run = RunState.Current;
            if (run != null) _status.SetText("enemies alive {0}   |   level {1}   |   {2}:{3:00} left", em != null ? em.AliveCount : 0, run.Level, Mathf.Floor(run.TimeRemaining / 60f), Mathf.Floor(run.TimeRemaining % 60f));
        }

        /// <summary>
        /// FIGHT A BOSS: one button per GameSession.KnownRuns entry, "THE SLAVE PIT: Gorm". Labels are rebuilt only when
        /// that list changes (the menu sets it once). No list (an arena started from the Editor): one greyed-out row.
        /// </summary>
        void RefreshBossRuns()
        {
            var runs = GameSession.KnownRuns;
            int count = Mathf.Min(AdminController.BossRunCount, MaxBossRuns);
            if (runs == _bossRunsShown && count == _bossCountShown) return;
            _bossRunsShown = runs;
            _bossCountShown = count;
            int rows = Mathf.Max(1, count);
            for (int k = 0; k < MaxBossRuns; k++)
            {
                var b = _bossButtons[k];
                b.gameObject.SetActive(k < rows);
                if (k >= rows) continue;
                var label = UiFactory.Label(b);
                if (count == 0)
                {
                    b.interactable = false;
                    label.text = "No arena list: start the run from the menu";
                    continue;
                }
                b.interactable = AdminController.CanFightBoss(k);
                label.text = BossRunLabel(AdminController.BossRun(k));
            }
            _panel2.sizeDelta = new Vector2(Width, _panel2Base + rows * 40f + 6f);
        }

        /// <summary>"THE SLAVE PIT: Gorm" (the arena, then its boss's short name).</summary>
        static string BossRunLabel(RunDef run)
        {
            if (run == null) return "(missing run)";
            var boss = run.Boss;
            string who = boss != null ? (!string.IsNullOrEmpty(boss.shortName) ? boss.shortName : boss.displayName) : "no boss";
            return run.DisplayName + ": " + who;
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
