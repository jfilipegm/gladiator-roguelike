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
    /// build strip (owned weapons, then Boons below, each with its level, under the gold), LEVEL UP toast, pause overlay, F3 debug line. Built in
    /// code; texts only change when values change and use allocation-free TMP calls.
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
        float _toastT = 10f, _announceT = 10f, _announceLen = 2f, _debugT;

        // boss (M3.4): name + HP bar under the timer, phase-2 notch; the timer reads BOSS
        CanvasGroup _bossGroup;
        Image _bossFill;
        TextMeshProUGUI _bossName;
        BossController _bossShown;
        bool _timerBoss;
        static readonly Color BossRed = new Color(0.86f, 0.16f, 0.12f);

        // arena intro (M3.5): name + tagline over a dim screen while the pools fill, then FIGHT!
        CanvasGroup _introGroup;
        Image _introDim;
        TextMeshProUGUI _introName, _introTag, _introFight;
        RunIntro _introShown;
        int _frames;
        int _lastTags = -1;
        float _lastSpeed = 1f;

        // build strip: a row of weapon tiles, a row of Boon tiles (border = best rarity taken, glyph, level)
        const int WeaponTiles = 4, BoonTiles = 6, StripTiles = WeaponTiles + BoonTiles;
        readonly Image[] _tileBorder = new Image[StripTiles];
        readonly Image[] _tileInner = new Image[StripTiles];
        readonly TextMeshProUGUI[] _tileGlyph = new TextMeshProUGUI[StripTiles];
        readonly TextMeshProUGUI[] _tileLevel = new TextMeshProUGUI[StripTiles];
        int _lastBuildVersion = -1;
        PlayerBuild _build;
        static readonly Color TileInner = new Color(0.1f, 0.12f, 0.15f, 0.92f);
        // interactables (M2.7): prompt at the bottom + floating price tags over ready chests / the altar
        const int Tags = 6;
        readonly RectTransform[] _tagRoot = new RectTransform[Tags];
        readonly Image[] _tagIcon = new Image[Tags];
        readonly TextMeshProUGUI[] _tagText = new TextMeshProUGUI[Tags];
        readonly int[] _tagKey = new int[Tags];
        TextMeshProUGUI _prompt;
        int _promptKey = int.MinValue;
        static readonly Color CantAfford = new Color(1f, 0.45f, 0.4f);
        static readonly Color AltarColor = new Color(1f, 0.4f, 0.55f);

        static readonly Color WeaponTileInner = new Color(0.2f, 0.13f, 0.07f, 0.92f); // warm bronze: weapons vs Boons

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

            for (int i = 0; i < StripTiles; i++)
            {
                var pos = i < WeaponTiles ? new Vector2(24 + i * 58, -122) : new Vector2(24 + (i - WeaponTiles) * 58, -180);
                _tileBorder[i] = UiFactory.Image("Tile" + i, canvas, Color.clear, tl, tl, pos, new Vector2(52, 52), PixelIcons.White);
                var inner = _tileInner[i] = UiFactory.Image("Inner", _tileBorder[i].transform, Color.clear, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(44, 44), PixelIcons.White);
                _tileGlyph[i] = UiFactory.Text("Glyph", inner.transform, "", 22, Color.white, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 4), new Vector2(44, 30), font, fontMaterial);
                _tileLevel[i] = UiFactory.Text("Level", inner.transform, "", 16, Gold, TextAlignmentOptions.BottomRight, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-2, 0), new Vector2(30, 20), font, fontMaterial);
                PreSize(_tileGlyph[i], "WWWW");
                PreSize(_tileLevel[i], "88");
            }

            var mid = new Vector2(0.5f, 0.5f);
            for (int i = 0; i < Tags; i++)
            {
                var root = UiFactory.Image("PriceTag" + i, canvas, Color.clear, mid, mid, Vector2.zero, new Vector2(120, 36), PixelIcons.White);
                _tagRoot[i] = root.rectTransform;
                _tagIcon[i] = UiFactory.Image("Coin", root.transform, Color.white, mid, mid, new Vector2(-26, 0), new Vector2(26, 26), PixelIcons.Coin);
                _tagText[i] = UiFactory.Text("Price", root.transform, "", 30, Gold, TextAlignmentOptions.Left, mid, mid, new Vector2(30, 2), new Vector2(90, 36), font, fontMaterial);
                PreSize(_tagText[i], "ALTAR");
                PreSize(_tagText[i], "88888");
                _tagRoot[i].localScale = Vector3.zero;
                _tagKey[i] = int.MinValue;
            }
            var bottom = new Vector2(0.5f, 0f);
            _prompt = UiFactory.Text("Prompt", canvas, "", 32, Color.white, TextAlignmentOptions.Center, bottom, bottom, new Vector2(0, 150), new Vector2(1300, 46), font, fontMaterial);
            PreSize(_prompt, "[E] Blood Altar: lose 88% max HP for a Rare+ upgrade");
            PreSize(_prompt, "[E] Armory Chest  88888 gold  (not enough gold)");

            _toast = UiFactory.Text("Toast", canvas, "LEVEL UP!", 84, Gold, TextAlignmentOptions.Center, top, top, new Vector2(0, -170), new Vector2(900, 110), font, fontMaterial);
            _toast.alpha = 0f;
            // Director announcer (swarm / elite). Constant strings, so swapping them never allocates.
            _announce = UiFactory.Text("Announce", canvas, "THE PACK IS LOOSE!", 56, Color.white, TextAlignmentOptions.Center, top, top, new Vector2(0, -280), new Vector2(1100, 80), font, fontMaterial);
            _announce.alpha = 0f;

            var introRoot = new GameObject("Intro", typeof(RectTransform), typeof(CanvasGroup));
            introRoot.transform.SetParent(canvas, false);
            var irt = (RectTransform)introRoot.transform;
            irt.anchorMin = Vector2.zero; irt.anchorMax = Vector2.one; irt.offsetMin = irt.offsetMax = Vector2.zero;
            _introGroup = introRoot.GetComponent<CanvasGroup>();
            _introGroup.alpha = 0f;
            _introGroup.blocksRaycasts = _introGroup.interactable = false;
            var centre = new Vector2(0.5f, 0.5f);
            _introDim = UiFactory.Image("Dim", introRoot.transform, new Color(0.03f, 0.02f, 0.02f, 0.86f), centre, centre, Vector2.zero, Vector2.zero, PixelIcons.White);
            UiFactory.Stretch(_introDim.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _introName = UiFactory.Text("ArenaName", introRoot.transform, "", 96, Gold, TextAlignmentOptions.Center, centre, centre, new Vector2(0, 70), new Vector2(1600, 120), font, fontMaterial);
            _introTag = UiFactory.Text("Tagline", introRoot.transform, "", 34, new Color(1f, 1f, 1f, 0.8f), TextAlignmentOptions.Center, centre, centre, new Vector2(0, -20), new Vector2(1600, 50), font, fontMaterial);
            _introTag.fontStyle = FontStyles.Italic;
            _introFight = UiFactory.Text("Fight", introRoot.transform, "FIGHT!", 150, new Color(1f, 0.35f, 0.2f), TextAlignmentOptions.Center, centre, centre, new Vector2(0, 20), new Vector2(1600, 180), font, fontMaterial);
            _introFight.alpha = 0f;

            var bossRoot = new GameObject("BossHud", typeof(RectTransform), typeof(CanvasGroup));
            bossRoot.transform.SetParent(canvas, false);
            var brt = (RectTransform)bossRoot.transform;
            brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one; brt.offsetMin = brt.offsetMax = Vector2.zero;
            _bossGroup = bossRoot.GetComponent<CanvasGroup>();
            _bossGroup.alpha = 0f;
            _bossGroup.blocksRaycasts = _bossGroup.interactable = false;
            _bossName = UiFactory.Text("BossName", bossRoot.transform, "", 30, new Color(1f, 0.9f, 0.8f), TextAlignmentOptions.Center, top, top, new Vector2(0, -112), new Vector2(900, 36), font, fontMaterial);
            PreSize(_bossName, "GORM, THE PIT MASTER OF THE SLAVE PITS");
            _bossFill = UiFactory.Bar("BossBar", bossRoot.transform, new Color(0.08f, 0.05f, 0.06f, 0.85f), BossRed, top, top, new Vector2(0, -150), new Vector2(760, 26));
            _pause = UiFactory.Text("Pause", canvas, "PAUSED\n<size=36>click to resume  ·  O: options  ·  Q: main menu</size>", 90, Color.white, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900, 260), font, fontMaterial);
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
            PreSize(_announce, "GORM CALLS HIS GUARDS!");
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
            GameEvents.OnAnnounce.Subscribe(OnAnnounce);
        }

        void OnDisable()
        {
            GameLoop.Unregister(this);
            GameEvents.OnLevelUp.Unsubscribe(OnLevelUp);
            GameEvents.OnDirector.Unsubscribe(OnDirector);
            GameEvents.OnAnnounce.Unsubscribe(OnAnnounce);
        }

        void OnLevelUp(in LevelUpEvent e) => _toastT = 0f;

        void OnAnnounce(in AnnounceEvent e)
        {
            _announce.text = e.Text; // constant strings only: no allocation
            var c = e.Color;
            c.a = 1f;
            _announce.color = c;
            _announceLen = Mathf.Max(0.5f, e.Seconds);
            _announceT = 0f;
        }

        void DrawIntro(float unscaledDt)
        {
            var intro = RunIntro.Instance;
            if (intro == null || intro.Done)
            {
                if (_introGroup.alpha > 0f) _introGroup.alpha = 0f;
                return;
            }
            if (_introShown != intro)
            {
                _introShown = intro;
                _introName.text = intro.ArenaName; // once per arena
                _introTag.text = intro.tagline;
            }
            _introGroup.alpha = 1f;
            if (intro.Current == RunIntro.Stage.Holding)
            {
                _introDim.color = new Color(0.03f, 0.02f, 0.02f, 0.86f);
                _introName.alpha = Mathf.Clamp01(intro.T * 3f);
                _introTag.alpha = Mathf.Clamp01(intro.T * 2f - 0.6f) * 0.8f;
                _introFight.alpha = 0f;
            }
            else
            {
                float k = Mathf.Clamp01(intro.T / Mathf.Max(0.1f, intro.fightBanner));
                _introDim.color = new Color(0.03f, 0.02f, 0.02f, 0.86f * (1f - k));
                _introName.alpha = _introTag.alpha = 0f;
                _introFight.alpha = 1f - k * k;
                float sc = 1.6f - 0.6f * Mathf.Min(1f, k * 5f);
                _introFight.rectTransform.localScale = new Vector3(sc, sc, 1f);
            }
        }

        void DrawBoss(RunState run)
        {
            if (run != null && run.TimerDone && !_timerBoss) { _timerBoss = true; _timer.text = "BOSS"; _timer.color = BossRed; }
            var b = BossController.Active;
            bool show = b != null && b.Alive && b.def != null;
            if (show && _bossShown != b)
            {
                _bossShown = b;
                _bossName.text = b.def.displayName + ", " + b.def.title; // once per boss
                var notch = UiFactory.Image("Phase2", _bossFill.transform.parent, new Color(1f, 0.85f, 0.4f, 0.9f), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(760f * b.def.phase2At, 0f), new Vector2(3f, 30f), PixelIcons.White);
                notch.raycastTarget = false;
            }
            float a = show ? 1f : 0f;
            if (!Mathf.Approximately(_bossGroup.alpha, a)) _bossGroup.alpha = a;
            if (show) _bossFill.fillAmount = Mathf.MoveTowards(_bossFill.fillAmount, b.Hp01, Time.unscaledDeltaTime * 1.5f);
        }

        const string SwarmText = "SWARM INCOMING!";
        const string EliteText = "AN ELITE ENTERS!";

        void OnDirector(in DirectorEvent e)
        {
            _announceLen = 2f;
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
                if (sec != _lastSecond && !run.TimerDone) { _lastSecond = sec; WriteTime(sec); }
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
                if (_build == null) _build = player.GetComponent<PlayerBuild>();
                if (_build != null && _build.State.Version != _lastBuildVersion) RefreshStrip();
            }

            DrawInteractables(run);
            DrawBoss(run);
            DrawIntro(unscaledDt);
            if (player != null && player.IsPaused && !OptionsPanel.IsOpen && Keyboard.current != null)
            {
                if (Keyboard.current.qKey.wasPressedThisFrame) GameSession.ToMenu();
                else if (Keyboard.current.oKey.wasPressedThisFrame && OptionsPanel.Instance != null) OptionsPanel.Instance.Open();
            }
            if (OptionsPanel.IsOpen && _pause.alpha > 0f) _pause.alpha = 0f;

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
            if (_announceT < _announceLen)
            {
                float t = _announceT / _announceLen;
                _announce.alpha = t < 0.8f ? 1f : 1f - (t - 0.8f) / 0.2f;
                float s = t < 0.08f ? Mathf.Lerp(1.4f, 1f, t / 0.08f) : 1f;
                _announce.rectTransform.localScale = new Vector3(s, s, 1f);
            }
            else if (_announce.alpha > 0f) _announce.alpha = 0f;
            if (run != null && run.Ended && _announce.alpha > 0f) { _announce.alpha = 0f; _announceT = _announceLen; } // the end screen owns the centre

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

        /// <summary>Price tags over ready chests (gold, red when you can't pay), "ALTAR" over the altar, and the
        /// bottom prompt for whatever is in reach. Texts change only when what they show changes.</summary>
        void DrawInteractables(RunState run)
        {
            var props = Interactables.Instance;
            var cam = Camera.main;
            int shown = 0;
            bool live = props != null && cam != null && run != null && !run.Ended;
            int price = live ? props.ChestPrice : 0;
            bool afford = live && run.Gold >= price;
            for (int i = 0; live && i < props.SlotCount && shown < Tags; i++)
            {
                if (!props.IsReady(i)) continue;
                bool chest = props.KindOf(i) == Interactables.Kind.Chest;
                Vector3 sp = cam.WorldToScreenPoint(props.PositionOf(i) + Vector3.up * (chest ? 1.5f : 2.1f));
                if (sp.z <= 0f) continue;
                var rt = _tagRoot[shown];
                rt.position = new Vector3(sp.x, sp.y, 0f);
                if (rt.localScale.x == 0f) rt.localScale = Vector3.one;
                int key = chest ? price * 2 + (afford ? 1 : 0) : -1;
                if (key != _tagKey[shown])
                {
                    _tagKey[shown] = key;
                    if (chest)
                    {
                        _tagText[shown].SetText("{0}", price);
                        _tagText[shown].color = afford ? Gold : CantAfford;
                        _tagIcon[shown].color = Color.white;
                    }
                    else
                    {
                        _tagText[shown].text = "ALTAR";
                        _tagText[shown].color = AltarColor;
                        _tagIcon[shown].color = Color.clear;
                    }
                }
                shown++;
            }
            for (int k = shown; k < Tags; k++)
                if (_tagRoot[k].localScale.x != 0f) _tagRoot[k].localScale = Vector3.zero;

            int reach = live ? props.InReach : -1;
            int pkey = reach < 0 ? -1 : props.KindOf(reach) == Interactables.Kind.Altar ? -2 : price * 2 + (afford ? 1 : 0);
            if (pkey == _promptKey) return;
            _promptKey = pkey;
            if (pkey == -1) { _prompt.text = ""; return; }
            if (pkey == -2)
            {
                _prompt.SetText("[E] Blood Altar: lose {0}% max HP for a Rare+ upgrade", Mathf.Round(props.config.altarMaxHpCost * 100f));
                _prompt.color = AltarColor;
                return;
            }
            if (afford) _prompt.SetText("[E] Armory Chest  {0} gold", price);
            else _prompt.SetText("[E] Armory Chest  {0} gold  (not enough gold)", price);
            _prompt.color = afford ? Gold : CantAfford;
        }

        void RefreshStrip()
        {
            var state = _build.State;
            _lastBuildVersion = state.Version;
            FillRow(state, UpgradeKind.Weapon, 0, WeaponTiles, WeaponTileInner);
            FillRow(state, UpgradeKind.Boon, WeaponTiles, StripTiles, TileInner);
        }

        void FillRow(BuildState state, UpgradeKind kind, int first, int end, Color inner)
        {
            int k = first;
            for (int i = 0; i < state.OwnedCount && k < end; i++)
            {
                var o = state.GetOwned(i);
                if (o.Def.kind != kind) continue;
                _tileBorder[k].color = _build.config.RarityColor(o.BestRarity);
                _tileInner[k].color = inner;
                _tileGlyph[k].text = o.Def.glyph;
                _tileLevel[k].SetText("{0}", o.Level);
                k++;
            }
            for (; k < end; k++)
            {
                _tileBorder[k].color = Color.clear;
                _tileInner[k].color = Color.clear;
                _tileGlyph[k].text = "";
                _tileLevel[k].text = "";
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
