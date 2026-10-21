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
        TextMeshProUGUI _objective; // a timed goal under the timer (Phase 5.6: the Bookmaker's wager)
        string _objectiveLabel, _objectiveFormat;
        TextMeshProUGUI _timer, _level, _hpText, _gold, _kills, _toast, _announce, _pause, _debug, _tags;
        readonly char[] _timeBuf = new char[8];
        int _lastSecond = -1, _lastLevel = -1, _lastGold = -1, _lastKills = -1, _lastHp = -1, _lastMaxHp = -1;
        float _toastT = 10f, _announceT = 10f, _announceLen = 2f, _debugT;
        AnnounceKind _announceKind = AnnounceKind.Line;
        const float ChatterHoldOff = 1f;
        static bool Chatter(AnnounceKind k) => k == AnnounceKind.Streak || k == AnnounceKind.Roar || k == AnnounceKind.CloseCall;

        // boss (M3.4): name + HP bar under the timer, phase-2 notch; the timer reads BOSS
        CanvasGroup _bossGroup;
        Image _bossFill;
        TextMeshProUGUI _bossName;
        TextMeshProUGUI _bossGuard; // the Gilded Patron's payroll (Phase 5.13)
        bool _bossGuardOn;
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
        readonly UpgradeIconView[] _tileIcon = new UpgradeIconView[StripTiles]; // playtest: icons, not letters
        UpgradeIconView _loanIcon;
        int _lastBuildVersion = -1;
        // Phase 5.5: a weapon tile whose evolution is ready pulses gold ("EVO!"); evolved ones read "EVO"; active synergies sit in a row of chips below
        readonly bool[] _tileReady = new bool[WeaponTiles];
        readonly Color[] _tileColor = new Color[WeaponTiles];
        const int SynergyChips = 6;
        readonly Image[] _chip = new Image[SynergyChips];
        readonly TextMeshProUGUI[] _chipText = new TextMeshProUGUI[SynergyChips];
        readonly UpgradeIconView[] _chipIcon = new UpgradeIconView[SynergyChips];
        static readonly Color ChipColor = new Color(0.06f, 0.16f, 0.2f, 0.85f), ChipText = new Color(0.55f, 0.95f, 1f);
        // P1: each chip is its icon then its name, as wide as the name; they flow in rows under the Boon row (8 px gap)
        const float ChipLeft = 24f, ChipTop = -240f, ChipH = 30f, ChipGap = 6f, ChipRowWidth = 380f;
        const float ChipIconLeft = 4f, ChipIconSize = 24f, ChipNameLeft = 33f, ChipNamePad = 9f;
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
        // the Bookmaker (P1): "[E]  WAGER 30 GOLD" over him while he calls you over, "NEED 30 GOLD" in red when you're short
        TextMeshProUGUI _bookTag;
        int _bookKey = int.MinValue;
        const float BookmakerTagHeight = 2.3f;
        static readonly Color CantAfford = new Color(1f, 0.45f, 0.4f);
        static readonly Color AltarColor = new Color(1f, 0.4f, 0.55f);

        static readonly Color WeaponTileInner = new Color(0.2f, 0.13f, 0.07f, 0.92f); // warm bronze: weapons vs Boons

        // Favor (Phase 5.1): a gold bar under the level (pulses when a gift is near) + the loaned weapon's tile after the
        // weapon row, its inner draining as the loan runs out
        Image _favorFill, _loanBorder, _loanInner;
        TextMeshProUGUI _loanGlyph, _loanTime;
        int _lastLoanSecond = -1;
        WeaponDef _loanShown;
        WeaponSystem _weapons;
        static readonly Color FavorColor = new Color(1f, 0.78f, 0.25f);
        static readonly Color FavorHot = new Color(1f, 0.95f, 0.6f);

        // Blood Knight (Phase 5.3): the Blood meter under the HP bar (drains through a frenzy) + beside it the Bloodlust
        // bonus, "SLIDE!" when the meter is full, "FRENZY" during the Rite. Hidden for other gladiators.
        Image _bloodFill;
        GameObject _bloodRoot;
        TextMeshProUGUI _bloodText;
        int _bloodKey = int.MinValue;
        BloodRite _riteShown;
        Bloodlust _lust;
        static readonly Color BloodColor = new Color(0.9f, 0.16f, 0.36f);
        static readonly Color BloodHot = new Color(1f, 0.78f, 0.84f);
        // Murder Machine (Phase 5.9): the same bar is his Wrath meter (pale steel, cyan-white when full: "JUMP!").
        static readonly Color WrathColor = new Color(0.7f, 0.8f, 0.88f);
        static readonly Color WrathHot = new Color(0.62f, 0.97f, 1f);
        // Thunder Champion (Phase 5.10): the same bar is his Charge (electric blue; "+N% SPD").
        static readonly Color ChargeColor = new Color(0.35f, 0.62f, 1f);
        static readonly Color ChargeHot = new Color(0.88f, 0.97f, 1f);
        // Golden Idol (Phase 5.11): the same bar is Midas Body's damage bonus from gold carried ("+N% DMG").
        static readonly Color MidasColor = new Color(1f, 0.8f, 0.25f);
        static readonly Color MidasHot = new Color(1f, 0.97f, 0.7f);

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
            // P1: the dev switches in effect, spelled out, small, in the bottom-right corner (under the timer, a bare "BUFFS"
            // read as part of the game)
            _tags = UiFactory.Text("DevTags", canvas, "", 18, new Color(1f, 0.84f, 0.3f, 0.85f), TextAlignmentOptions.BottomRight, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-16, 12), new Vector2(1100, 26), font, fontMaterial);
            _objective = UiFactory.Text("Objective", canvas, "", 30, new Color(1f, 0.84f, 0.3f), TextAlignmentOptions.Center, top, top, new Vector2(0, -122), new Vector2(700, 40), font, fontMaterial);
            _level = UiFactory.Text("Level", canvas, "LV 1", 46, Color.white, TextAlignmentOptions.Right, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-28, -26), new Vector2(300, 60), font, fontMaterial);

            // HP bar + icon
            var tl = new Vector2(0, 1);
            UiFactory.Image("HpIcon", canvas, Color.white, tl, tl, new Vector2(24, -32), new Vector2(34, 34), PixelIcons.Heart);
            _hpFill = UiFactory.Bar("HpBar", canvas, new Color(0.08f, 0.05f, 0.06f, 0.8f), HpColor, tl, tl, new Vector2(66, -32), new Vector2(340, 34));
            _hpText = UiFactory.Text("HpText", _hpFill.transform.parent, "100 / 100", 24, Color.white, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(340, 34), font, fontMaterial);

            _bloodFill = UiFactory.Bar("BloodBar", canvas, new Color(0.08f, 0.03f, 0.05f, 0.8f), BloodColor, tl, tl, new Vector2(66, -68), new Vector2(340, 12));
            _bloodFill.fillAmount = 0f;
            _bloodRoot = _bloodFill.transform.parent.gameObject;
            _bloodText = UiFactory.Text("BloodText", canvas, "", 20, BloodColor, TextAlignmentOptions.Left, tl, tl, new Vector2(414, -60), new Vector2(170, 28), font, fontMaterial);
            PreSize(_bloodText, "+100% DMG");
            _bloodRoot.SetActive(false);
            _bloodText.gameObject.SetActive(false);

            UiFactory.Image("GoldIcon", canvas, Color.white, tl, tl, new Vector2(26, -82), new Vector2(30, 30), PixelIcons.Coin);
            _gold = UiFactory.Text("Gold", canvas, "0", 32, Gold, TextAlignmentOptions.Left, tl, tl, new Vector2(66, -78), new Vector2(150, 40), font, fontMaterial);
            UiFactory.Image("KillIcon", canvas, Color.white, tl, tl, new Vector2(200, -82), new Vector2(30, 30), PixelIcons.Skull);

            var tr = new Vector2(1, 1);
            UiFactory.Image("FavorIcon", canvas, Color.white, tr, tr, new Vector2(-292, -86), new Vector2(30, 26), PixelIcons.Laurel);
            _favorFill = UiFactory.Bar("FavorBar", canvas, new Color(0.08f, 0.06f, 0.03f, 0.75f), FavorColor, tr, tr, new Vector2(-28, -90), new Vector2(256, 18));
            _favorFill.fillAmount = 0f;

            var loanPos = new Vector2(24 + WeaponTiles * 58 + 10, -122);
            _loanBorder = UiFactory.Image("LoanTile", canvas, Color.clear, tl, tl, loanPos, new Vector2(52, 52), PixelIcons.White);
            _loanInner = UiFactory.Image("Inner", _loanBorder.transform, Color.clear, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(44, 44), PixelIcons.White);
            _loanInner.type = Image.Type.Filled;
            _loanInner.fillMethod = Image.FillMethod.Vertical;
            _loanIcon = new UpgradeIconView(_loanBorder.transform, "Icon", new Vector2(0.5f, 0.5f), Vector2.zero, 40f, lively: false);
            _loanGlyph = UiFactory.Text("Glyph", _loanBorder.transform, "", 22, Color.white, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 4), new Vector2(44, 30), font, fontMaterial);
            _loanTime = UiFactory.Text("Time", _loanBorder.transform, "", 16, FavorHot, TextAlignmentOptions.BottomRight, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-2, 0), new Vector2(30, 20), font, fontMaterial);
            PreSize(_loanGlyph, "WWWW");
            PreSize(_loanTime, "88");
            _kills = UiFactory.Text("Kills", canvas, "0", 32, Color.white, TextAlignmentOptions.Left, tl, tl, new Vector2(240, -78), new Vector2(150, 40), font, fontMaterial);

            for (int i = 0; i < StripTiles; i++)
            {
                var pos = i < WeaponTiles ? new Vector2(24 + i * 58, -122) : new Vector2(24 + (i - WeaponTiles) * 58, -180);
                _tileBorder[i] = UiFactory.Image("Tile" + i, canvas, Color.clear, tl, tl, pos, new Vector2(52, 52), PixelIcons.White);
                var inner = _tileInner[i] = UiFactory.Image("Inner", _tileBorder[i].transform, Color.clear, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(44, 44), PixelIcons.White);
                _tileIcon[i] = new UpgradeIconView(inner.transform, "Icon", new Vector2(0.5f, 0.5f), Vector2.zero, 40f, lively: false);
                _tileGlyph[i] = UiFactory.Text("Glyph", inner.transform, "", 22, Color.white, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 4), new Vector2(44, 30), font, fontMaterial);
                _tileLevel[i] = UiFactory.Text("Level", inner.transform, "", 16, Gold, TextAlignmentOptions.BottomRight, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-2, 0), new Vector2(44, 20), font, fontMaterial);
                PreSize(_tileGlyph[i], "WWWW");
                PreSize(_tileLevel[i], "EVO!");
            }
            var ml = new Vector2(0f, 0.5f);
            for (int j = 0; j < SynergyChips; j++)
            {
                _chip[j] = UiFactory.Image("Synergy" + j, canvas, Color.clear, tl, tl, new Vector2(ChipLeft, ChipTop), new Vector2(144, ChipH), PixelIcons.White);
                _chipIcon[j] = new UpgradeIconView(_chip[j].transform, "Icon", ml, new Vector2(ChipIconLeft, 0), ChipIconSize, lively: false);
                _chipText[j] = UiFactory.Text("Name", _chip[j].transform, "", 17, ChipText, TextAlignmentOptions.Left, ml, ml, new Vector2(ChipNameLeft, 1), new Vector2(110, 26), font, fontMaterial);
                PreSize(_chipText[j], "EXECUTIONER!");
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
            _bookTag = UiFactory.Text("BookmakerTag", canvas, "", 28, Gold, TextAlignmentOptions.Center, mid, mid, Vector2.zero, new Vector2(460, 40), font, fontMaterial);
            PreSize(_bookTag, "[E]  WAGER 8888 GOLD");
            PreSize(_bookTag, "NEED 8888 GOLD");
            _bookTag.rectTransform.localScale = Vector3.zero;
            var bottom = new Vector2(0.5f, 0f);
            _prompt = UiFactory.Text("Prompt", canvas, "", 32, Color.white, TextAlignmentOptions.Center, bottom, bottom, new Vector2(0, 150), new Vector2(1300, 46), font, fontMaterial);
            PreSize(_prompt, "[E] Blood Altar: lose 88% max HP for a Rare+ upgrade");
            PreSize(_prompt, "[E] Armory Chest  88888 gold  (not enough gold)");
            PreSize(_prompt, "[E] Wager 8888 gold: beat your own pace, win 8888");

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
            _bossGuard = UiFactory.Text("BossGuard", bossRoot.transform, GuardedText, 22, BossGold, TextAlignmentOptions.Center, top, top, new Vector2(0, -180), new Vector2(900, 28), font, fontMaterial);
            _bossGuard.enabled = false;
            // P1: the stats page (StatsPanel) opens on the right with the pause menu, so PAUSED and its hint (on two short
            // lines) are centred in the space left of it. Stretched across the screen, so any aspect ratio works.
            _pause = UiFactory.Text("Pause", canvas, PauseText, 90, Color.white, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900, 300), font, fontMaterial);
            UiFactory.Stretch(_pause.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(PauseInset, -150f), new Vector2(-PauseInset, 150f));
            _pause.textWrappingMode = TextWrappingModes.Normal;
            _pause.alpha = 0f;
            _debug = UiFactory.Text("Debug", canvas, "", 20, new Color(1f, 1f, 1f, 0.75f), TextAlignmentOptions.BottomLeft, Vector2.zero, Vector2.zero, new Vector2(16, 12), new Vector2(900, 30), font, fontMaterial);
            _debug.fontStyle = FontStyles.Normal;
            // Pre-size TMP buffers with the longest strings each label shows and parse them now: in players the first
            // frame's huge delta fires the first debug update before anything renders, and TMP then grew its
            // character array one char at a time (~12 KB per growth) as the line got longer.
            PreSize(_debug, "88888 fps   88888 enemies   88888 pickups   F1 admin");
            if (!Debug.isDebugBuild) showDebug = false; // F3 shows the fps line in release builds
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
            // Crowd chatter (streaks, roars, close calls) comes constantly in a big fight: it never cuts another line
            // (a Rite, a boss beat, a gift) off in its first second, or the player never reads it.
            if (Chatter(e.Kind) && !Chatter(_announceKind) && _announceT < ChatterHoldOff && _announce.alpha > 0f) return;
            _announceKind = e.Kind;
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

        /// <summary>"WAGER  42 / 120   18s" while a RunObjective is on (the format string is built once per label).</summary>
        void DrawObjective()
        {
            if (!RunObjective.Active)
            {
                if (_objectiveLabel != null) { _objectiveLabel = null; _objective.SetText(""); }
                return;
            }
            if (!ReferenceEquals(_objectiveLabel, RunObjective.Label))
            {
                _objectiveLabel = RunObjective.Label;
                _objectiveFormat = _objectiveLabel + "  {0} / {1}   {2}s";
            }
            _objective.SetText(_objectiveFormat, RunObjective.Current, RunObjective.Target, Mathf.CeilToInt(RunObjective.SecondsLeft));
        }

        /// <summary>The objective line's text (tests).</summary>
        public string ObjectiveText => _objective != null ? _objective.text : "";

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
                if (b.def.phase3At > 0f) // a third phase (the Emperor's Champion)
                {
                    var notch3 = UiFactory.Image("Phase3", _bossFill.transform.parent, new Color(1f, 0.85f, 0.4f, 0.9f), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(760f * b.def.phase3At, 0f), new Vector2(3f, 30f), PixelIcons.White);
                    notch3.raycastTarget = false;
                }
            }
            float a = show ? 1f : 0f;
            if (!Mathf.Approximately(_bossGroup.alpha, a)) _bossGroup.alpha = a;
            if (show) _bossFill.fillAmount = Mathf.MoveTowards(_bossFill.fillAmount, b.Hp01, Time.unscaledDeltaTime * 1.5f);
            bool guard = show && b.Guarded;
            if (guard != _bossGuardOn)
            {
                _bossGuardOn = guard;
                _bossGuard.enabled = guard;
                _bossFill.color = guard ? BossGold : BossRed; // a gilded bar: he's paying someone to take the hits
            }
        }

        const string GuardedText = "GUARDED: KILL HIS HIRED SWORDS";
        static readonly Color BossGold = new Color(1f, 0.82f, 0.3f);
        public bool BossGuardShown => _bossGuardOn;

        const string SwarmText = "SWARM INCOMING!";
        const string EliteText = "AN ELITE ENTERS!";

        void OnDirector(in DirectorEvent e)
        {
            _announceLen = 2f;
            _announceKind = AnnounceKind.Line;
            _announce.text = e.Kind == DirectorEventKind.Swarm ? SwarmText : EliteText;
            var c = e.Color;
            c.a = 1f;
            _announce.color = c;
            _announceT = 0f;
        }

        /// <summary>Tests: the icon in build-strip tile `i` (weapons 0-3, Boons 4-9).</summary>
        public UpgradeIconView TileIcon(int i) => _tileIcon[i];

        public void Tick(float dt, float unscaledDt)
        {
            var run = RunState.Current;
            var player = Player.Main;
            float now = Time.unscaledTime;
            for (int i = 0; i < StripTiles; i++) _tileIcon[i].Tick(now); // a rare shine; frames change a few times a minute
            for (int j = 0; j < SynergyChips; j++) _chipIcon[j].Tick(now);
            for (int i = 0; i < WeaponTiles; i++)
                if (_tileReady[i]) _tileBorder[i].color = Color.Lerp(_tileColor[i], Color.white, 0.5f + 0.5f * Mathf.Sin(now * 6f));
            _loanIcon.Tick(now);
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
                LayoutPause(player.IsPaused && StatsPanel.Instance != null); // the page shows whenever the pause menu does
                if (_build == null) _build = player.GetComponent<PlayerBuild>();
                if (_build != null && _build.State.Version != _lastBuildVersion) RefreshStrip();
            }

            DrawInteractables(run);
            DrawFavor(unscaledDt);
            DrawBlood(unscaledDt);
            DrawBoss(run);
            DrawObjective();
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
                _tags.text = tags == 0 ? "" : AdminTags(tags, TimeController.GameSpeed); // only when they change
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
                if (showDebug && Debug.isDebugBuild) _debug.SetText("{0} fps   {1} enemies   {2} pickups   F1 admin", Mathf.Round(_frames / _debugT), DevCounters.Enemies, DevCounters.Pickups);
                else if (showDebug) _debug.SetText("{0} fps", Mathf.Round(_frames / _debugT)); // release builds have no admin panel
                else _debug.SetText("");
                _frames = 0;
                _debugT = 0f;
            }
        }

        const string PauseText = "PAUSED\n<size=36>click to resume\nO: options  \u00B7  Q: main menu</size>";
        const float PauseInset = 40f;
        int _pauseBeside = -1;

        /// <summary>Centres the pause text in the space left of the stats page while it's up (only when that changes).</summary>
        void LayoutPause(bool beside)
        {
            int b = beside ? 1 : 0;
            if (b == _pauseBeside) return;
            _pauseBeside = b;
            float right = beside ? StatsPanel.PageWidth + StatsPanel.Margin : 0f;
            _pause.rectTransform.offsetMax = new Vector2(-right - PauseInset, 150f);
        }

        /// <summary>Tests: the pause text's box and the widest line it needs (canvas units).</summary>
        public RectTransform PauseRect => _pause.rectTransform;
        public float PauseTextWidth => _pause.GetPreferredValues(PauseText).x;
        public int PauseLineCount { get { _pause.ForceMeshUpdate(); return _pause.textInfo.lineCount; } }

        /// <summary>"ADMIN: GOD MODE  ·  BUFFS ON  (F1)": every dev switch in effect, spelled out (P1: a bare "BUFFS"
        /// under the timer confused the producer).</summary>
        static string AdminTags(int tags, float speed)
        {
            var sb = new System.Text.StringBuilder("ADMIN: ", 80);
            void Add(System.Text.StringBuilder b, string s) { if (b.Length > 7) b.Append("  \u00B7  "); b.Append(s); }
            if ((tags & 1) != 0) Add(sb, "GOD MODE");
            if ((tags & 4) != 0) Add(sb, "BUFFS ON");
            if ((tags & 2) != 0) Add(sb, "TIMELESS");
            if ((tags & 8) != 0) Add(sb, "SPEED x" + speed.ToString("0.##"));
            return sb.Append("  (F1)").ToString();
        }

        /// <summary>The dev-switch line and where it sits (tests).</summary>
        public string AdminTagText => _tags != null ? _tags.text : "";
        public RectTransform AdminTagRect => _tags.rectTransform;

        /// <summary>The Blood Knight's meter and its label (only while a BloodRite exists).</summary>
        void DrawBlood(float unscaledDt)
        {
            var rite = BloodRite.Instance;
            var verdict = Verdict.Instance;
            var storm = Stormcaller.Instance;
            var midas = MidasBody.Instance;
            bool wrath = rite == null && verdict != null && verdict.trait != null;
            bool charge = rite == null && !wrath && storm != null && storm.trait != null;
            bool gold = rite == null && !wrath && !charge && midas != null && midas.trait != null;
            bool on = (rite != null && rite.trait != null) || wrath || charge || gold;
            if (_bloodRoot.activeSelf != on) { _bloodRoot.SetActive(on); _bloodText.gameObject.SetActive(on); }
            if (!on) return;
            if (wrath) { DrawWrath(verdict, unscaledDt); return; }
            if (charge) { DrawCharge(storm, unscaledDt); return; }
            if (gold) { DrawMidas(midas, unscaledDt); return; }
            if (_riteShown != rite) { _riteShown = rite; _lust = rite.GetComponent<Bloodlust>(); _bloodKey = int.MinValue; }
            float target = rite.Frenzied ? rite.FrenzyLeft / Mathf.Max(0.01f, rite.trait.frenzySeconds) : rite.Fill01;
            _bloodFill.fillAmount = Mathf.MoveTowards(_bloodFill.fillAmount, target, unscaledDt * 2f);
            bool hot = rite.Full || rite.Frenzied;
            _bloodFill.color = hot ? Color.Lerp(BloodColor, BloodHot, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * (rite.Frenzied ? 14f : 8f))) : BloodColor;

            int pct = _lust != null ? Mathf.RoundToInt(_lust.Bonus * 100f) : 0;
            int key = rite.Frenzied ? -2 : rite.Full ? -1 : pct;
            if (key != _bloodKey)
            {
                _bloodKey = key;
                if (key == -2) _bloodText.SetText("FRENZY");
                else if (key == -1) _bloodText.SetText("SLIDE!");
                else if (key > 0) _bloodText.SetText("+{0}% DMG", key);
                else _bloodText.SetText("");
            }
            _bloodText.color = key == -1 ? Color.Lerp(BloodColor, BloodHot, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 8f)) : BloodColor;
        }

        /// <summary>The Golden Idol's Midas Body bonus on the Blood bar (it shines when it's maxed).</summary>
        void DrawMidas(MidasBody m, float unscaledDt)
        {
            _bloodFill.fillAmount = Mathf.MoveTowards(_bloodFill.fillAmount, m.Bonus01, unscaledDt * 2f);
            _bloodFill.color = m.Bonus01 >= 1f ? Color.Lerp(MidasColor, MidasHot, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f)) : MidasColor;
            int key = Mathf.RoundToInt(m.Bonus * 100f);
            if (key != _bloodKey)
            {
                _bloodKey = key;
                if (key > 0) _bloodText.SetText("+{0}% DMG", key);
                else _bloodText.SetText("");
            }
            _bloodText.color = MidasColor;
        }

        /// <summary>The Thunder Champion's Charge on the Blood bar, with its attack-speed bonus.</summary>
        void DrawCharge(Stormcaller c, float unscaledDt)
        {
            _bloodFill.fillAmount = Mathf.MoveTowards(_bloodFill.fillAmount, c.Charge01, unscaledDt * 4f);
            float hot = Mathf.Clamp01((c.Charge01 - 0.75f) * 4f) * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 16f));
            _bloodFill.color = Color.Lerp(ChargeColor, ChargeHot, hot);
            int key = Mathf.RoundToInt(c.AttackSpeedBonus * 100f);
            if (key != _bloodKey)
            {
                _bloodKey = key;
                if (key > 0) _bloodText.SetText("+{0}% SPD", key);
                else _bloodText.SetText("");
            }
            _bloodText.color = ChargeColor;
        }

        /// <summary>The Murder Machine's Wrath meter on the Blood bar: "JUMP!" when full (the next landing is the Verdict).</summary>
        void DrawWrath(Verdict v, float unscaledDt)
        {
            _bloodFill.fillAmount = Mathf.MoveTowards(_bloodFill.fillAmount, v.Fill01, unscaledDt * 3f);
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 8f);
            _bloodFill.color = v.Full ? Color.Lerp(WrathColor, WrathHot, pulse) : WrathColor;
            int key = v.Full ? -3 : 0;
            if (key != _bloodKey)
            {
                _bloodKey = key;
                _bloodText.SetText(key == -3 ? "JUMP!" : "");
            }
            _bloodText.color = v.Full ? Color.Lerp(WrathColor, WrathHot, pulse) : WrathColor;
        }

        /// <summary>The Favor bar (pulses gold-white when a gift is close) and the loaned weapon's tile + seconds left.</summary>
        void DrawFavor(float unscaledDt)
        {
            var favor = Favor.Instance;
            if (favor != null)
            {
                float f = favor.Fill01;
                _favorFill.fillAmount = Mathf.MoveTowards(_favorFill.fillAmount, f, unscaledDt * 1.5f);
                _favorFill.color = f > 0.85f ? Color.Lerp(FavorColor, FavorHot, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 8f)) : FavorColor;
            }
            if (_weapons == null && Player.Main != null) _weapons = Player.Main.GetComponent<WeaponSystem>();
            var loan = _weapons != null ? _weapons.Temporary : null;
            var def = loan != null ? loan.Def : null;
            if (def != _loanShown)
            {
                _loanShown = def;
                var card = def != null && _build != null ? _build.FindWeaponCard(def) : null;
                _loanBorder.color = def != null ? FavorColor : Color.clear;
                _loanInner.color = def != null ? WeaponTileInner : Color.clear;
                if (card != null && _loanIcon.Set(card, FavorColor)) _loanGlyph.text = "";
                else { _loanIcon.Clear(); _loanGlyph.text = card != null ? card.glyph : ""; }
                _lastLoanSecond = -1;
                if (def == null) _loanTime.text = "";
            }
            if (loan == null) return;
            _loanInner.fillAmount = _weapons.TemporaryLeft / Mathf.Max(0.01f, _weapons.TemporaryDuration);
            int sec = Mathf.CeilToInt(_weapons.TemporaryLeft);
            if (sec != _lastLoanSecond) { _lastLoanSecond = sec; _loanTime.SetText("{0}", sec); }
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

            DrawBookmaker(cam, run);
            int reach = live ? props.InReach : -1;
            var bm = Bookmaker.Instance;
            bool bet = reach < 0 && bm != null && bm.InReach && run != null && !run.Ended; // a chest in reach takes E first
            int pkey = reach >= 0 ? (props.KindOf(reach) == Interactables.Kind.Altar ? -2 : price * 2 + (afford ? 1 : 0))
                     : bet ? (bm.CanAfford ? -3 : -4) : -1;
            if (pkey == _promptKey) return;
            _promptKey = pkey;
            if (pkey == -1) { _prompt.text = ""; return; }
            if (pkey == -3 || pkey == -4)
            {
                if (pkey == -3) _prompt.SetText("[E] Wager {0} gold: beat your own pace, win {1}", (float)bm.stake, (float)bm.payout);
                else _prompt.SetText("[E] Wager {0} gold  (not enough gold)", bm.stake);
                _prompt.color = pkey == -3 ? Gold : CantAfford;
                return;
            }
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

        /// <summary>The Bookmaker's world prompt over his head while he calls you (chests show their price the same way).</summary>
        void DrawBookmaker(Camera cam, RunState run)
        {
            var bm = Bookmaker.Instance;
            var rt = _bookTag.rectTransform;
            bool show = bm != null && cam != null && run != null && !run.Ended && bm.Calling;
            Vector3 sp = default;
            if (show)
            {
                sp = cam.WorldToScreenPoint(bm.Position + Vector3.up * BookmakerTagHeight);
                show = sp.z > 0f;
            }
            if (!show)
            {
                if (rt.localScale.x != 0f) rt.localScale = Vector3.zero;
                return;
            }
            rt.position = new Vector3(sp.x, sp.y, 0f);
            if (rt.localScale.x == 0f) rt.localScale = Vector3.one;
            bool afford = bm.CanAfford;
            int key = bm.stake * 2 + (afford ? 1 : 0);
            if (key == _bookKey) return;
            _bookKey = key;
            if (afford) _bookTag.SetText("[E]  WAGER {0} GOLD", bm.stake);
            else _bookTag.SetText("NEED {0} GOLD", bm.stake);
            _bookTag.color = afford ? Gold : CantAfford;
        }

        /// <summary>The Bookmaker's world prompt ("" when hidden) and the bottom prompt (tests).</summary>
        public string BookmakerTagText => _bookTag != null && _bookTag.rectTransform.localScale.x > 0f ? _bookTag.text : "";
        public string PromptText => _prompt != null ? _prompt.text : "";

        void RefreshStrip()
        {
            var state = _build.State;
            _lastBuildVersion = state.Version;
            FillRow(state, UpgradeKind.Weapon, 0, WeaponTiles, WeaponTileInner);
            FillRow(state, UpgradeKind.Boon, WeaponTiles, StripTiles, TileInner);
            float x = 0f, y = 0f;
            for (int j = 0; j < SynergyChips; j++)
            {
                bool on = j < state.SynergyCount;
                _chip[j].color = on ? ChipColor : Color.clear;
                _chipText[j].text = on ? state.GetSynergy(j).displayName : "";
                if (!on) { _chipIcon[j].Clear(); continue; }
                _chipIcon[j].Set(state.GetSynergy(j).icon, state.GetSynergy(j).iconRow, ChipText, j * 0.4f);
                // P1: the name starts after the icon and the chip is as wide as its name; a full row wraps below
                float nameW = Mathf.Ceil(_chipText[j].preferredWidth);
                float w = ChipNameLeft + nameW + ChipNamePad;
                if (x > 0f && x + w > ChipRowWidth) { x = 0f; y -= ChipH + ChipGap; }
                var rt = _chip[j].rectTransform;
                rt.anchoredPosition = new Vector2(ChipLeft + x, ChipTop + y);
                rt.sizeDelta = new Vector2(w, ChipH);
                _chipText[j].rectTransform.sizeDelta = new Vector2(nameW + 2f, 26f);
                x += w + ChipGap;
            }
        }

        /// <summary>Tests: synergy chip `j`'s background, icon and name rects, and the width its name needs (P1 layout).</summary>
        public RectTransform ChipRect(int j) => _chip[j].rectTransform;
        public RectTransform ChipIconRect(int j) => _chipIcon[j].Root;
        public RectTransform ChipNameRect(int j) => _chipText[j].rectTransform;
        public float ChipNameWidth(int j) => _chipText[j].preferredWidth;

        /// <summary>Phase 5.5: the evolution card of owned weapon card `d` that is ready to be offered (null if none).</summary>
        UpgradeDef ReadyEvolution(BuildState state, UpgradeDef d)
        {
            var ups = _build.catalog != null ? _build.catalog.upgrades : null;
            for (int i = 0; ups != null && i < ups.Length; i++)
                if (ups[i] != null && ups[i].evolvesFrom == d && UpgradeRoller.IsEvolutionReady(ups[i], state)) return ups[i];
            return null;
        }

        /// <summary>Tests: what weapon tile `i`'s level label reads ("EVO!" when its evolution is ready).</summary>
        public string TileLevel(int i) => _tileLevel[i].text;
        /// <summary>Tests: the name on synergy chip `j` ("" when empty).</summary>
        public string SynergyChip(int j) => _chipText[j].text;

        void FillRow(BuildState state, UpgradeKind kind, int first, int end, Color inner)
        {
            int k = first;
            for (int i = 0; i < state.OwnedCount && k < end; i++)
            {
                var o = state.GetOwned(i);
                if (o.Def.kind != kind) continue;
                var rarity = _build.config.RarityColor(o.BestRarity);
                _tileBorder[k].color = rarity;
                _tileInner[k].color = inner;
                _tileGlyph[k].text = _tileIcon[k].Set(o.Def, rarity, k * 0.137f) ? "" : o.Def.glyph;
                bool ready = kind == UpgradeKind.Weapon && ReadyEvolution(state, o.Def) != null;
                if (o.Def.evolvesFrom != null) _tileLevel[k].text = "EVO";
                else if (ready) _tileLevel[k].text = "EVO!";
                else _tileLevel[k].SetText("{0}", o.Level);
                if (kind == UpgradeKind.Weapon) { _tileReady[k] = ready; _tileColor[k] = rarity; }
                k++;
            }
            for (; k < end; k++)
            {
                if (kind == UpgradeKind.Weapon) _tileReady[k] = false;
                _tileBorder[k].color = Color.clear;
                _tileInner[k].color = Color.clear;
                _tileGlyph[k].text = "";
                _tileIcon[k].Clear();
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
