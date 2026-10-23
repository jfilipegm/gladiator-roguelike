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
    /// build strip (owned weapons, then Boons below, each with its level, under the gold), LEVEL UP toast, F3 debug line. Built in
    /// code; texts only change when values change and use allocation-free TMP calls.
    /// R7: the run's edicts as a row of small icons under the timer, built once at Start (nothing per frame); the boss's
    /// name and bar take that space during the fight, so the row steps aside while they show.
    /// R8: in a daily, a small "DAILY" chip beside the timer (gold on the scored attempt, grey in practice), built once
    /// at Start.
    /// U1 (docs/tracks/U1-design.md §5, §7): the Roman look through UiKit / UiTheme: HP, the special meter, gold and kills
    /// on a riveted bronze plate; level and Favor on another; troughs for every bar; the build strip as dark-bronze chips
    /// with a 2-unit rarity line; numbers in Luckiest Guy (Number), names in Marcellus SC (Title), shouts in Caesar
    /// Dressing (Shout). The announcer splits in two: swarms, elites and whims drop a crimson vexillum banner, the boss's
    /// beats a purple one (purple outranks crimson; a lower one waits in one slot); every other line is a one-line
    /// callout under it (today's pop-and-fade and chatter rule). The debug line and dev tags stay Plain (UiFactory).
    /// P3 (the producer's notes 13, 14, 18): each plate's rows are laid out from its ContentRect (the padding rule), the
    /// heart, aureus and skull-in-a-galea are the 16 px art at 32×32 centred on their trough or number; the level plate
    /// reads LevelWord ("LEVEL 3") and is as wide as "LEVEL 88" needs; the XP bar is 21 tall. The pause moved to
    /// LevelUpPanel (PAUSED, its plates and keys, the inventory and the stats column).
    /// </summary>
    public sealed class Hud : MonoBehaviour, ITickable
    {
        [Tooltip("Unused since U1 (UiTheme decides the fonts) except by the Plain debug line and dev tags; kept so the scene builders' links stay valid.")]
        public TMP_FontAsset font;
        public Material fontMaterial;
        public bool showDebug = true;

        // U1 layout (canvas units; top-left items from the top-left corner, top-right from the top-right)
        // P3 (the producer's notes 13, 14, 18): the XP bar 18 → 21 tall, everything under it 3 lower; each plate's rows
        // laid out from its ContentRect (padding + UiKit.ContentPad, the padding rule), the same gap above its first row
        // as under its last; the icons are the 16 px art (IconHeart, IconAureus, IconSkullGalea) at 32×32, centred on
        // their trough or number (numbers use Midline: their glyphs, not their line box, are centred)
        const float XpH = 21f, Top = -(XpH + 4f);
        const float PlateX = 12f, PlateW = 456f, HpRowH = 32f, BloodRowH = 22f, GoldRowH = 32f, RowGap = 6f, IconPx = 32f, IconGap = 8f;
        const float GoldSize = 32f, BloodSize = 20f, LevelSize = 50f, LabelSlack = 8f;
        const float BloodBarW = 220f, BloodBarH = 12f, KillsX = 219f;
        const float LevelRowH = 44f, FavorRowH = 26f, LevelGap = 10f, LevelMinW = 220f, GoldTextW = 130f;
        static readonly Vector2 FavorIconSize = new Vector2(30f, 26f);
        /// <summary>The top-left plate's right edge from the screen's left (canvas units): titles over the HUD stay clear of it.</summary>
        public const float TopLeftPlateRight = PlateX + PlateW;
        /// <summary>The level plate's word (P3, the producer's note 14: "LV" or "LEVEL"); read once at Awake.</summary>
        public static string LevelWord = "LEVEL";
        const float StripLeft = 24f, StripGap = 10f, TilePitch = 58f;
        float _weaponRowY = -191f, _boonRowY = -249f, _chipTop = -309f, _statusBottom = -181f;
        // the producer's note 21 (after P3): both top plates one height, the status plate's rows spread inside it
        float _plateH, _statusContentTop, _statusContentH;
        bool _meterRow;
        const float TileSize = 52f, TileLine = 44f, TileInnerSize = 40f, TileIconSize = 40f;
        static readonly Vector2 PlaqueSize = new Vector2(1560f, 220f);
        const float PlaquePad = 48f;

        Image _xpFill, _hpFill;
        TextMeshProUGUI _objective; // a timed goal under the timer (Phase 5.6: the Bookmaker's wager)
        string _objectiveLabel, _objectiveFormat;
        TextMeshProUGUI _timer, _level, _hpText, _gold, _kills, _toast, _announce, _debug, _tags;
        string _levelFormat = "LEVEL {0}";
        Image _hpIcon, _goldIcon, _killIcon, _favorIcon;
        RectTransform _statusPlate, _levelPlate;
        readonly char[] _timeBuf = new char[8];
        int _lastSecond = -1, _lastLevel = -1, _lastGold = -1, _lastKills = -1, _lastHp = -1, _lastMaxHp = -1;
        float _toastT = 10f, _announceT = 10f, _announceLen = 2f, _debugT;
        AnnounceKind _announceKind = AnnounceKind.Line;
        const float ChatterHoldOff = 1f;
        static bool Chatter(AnnounceKind k) => k == AnnounceKind.Streak || k == AnnounceKind.Roar || k == AnnounceKind.CloseCall;

        // U1: the announcer's banner (crimson: swarms, elites, whims; purple: the boss's beats) and its one waiting slot
        VexillumBanner _banner;
        CanvasGroup _bannerGroup; // hides the banner while the game is paused
        string _pendingText;
        BannerCloth _pendingCloth;
        float _pendingSeconds;
        int _bannerRank;
        static int Rank(BannerCloth c) => c == BannerCloth.Purple ? 2 : 1;

        // theme colours read once (UiTheme.Current)
        Color _ink, _goldColor, _goldBright, _crimson, _crimsonLight, _coin, _loss;

        // boss (M3.4): name + HP bar under the timer, phase-2 notch; the timer reads BOSS
        CanvasGroup _bossGroup;
        Image _bossFill;
        Vector2 _bossInset; // the fill's left and right insets in its trough (phase notches sit on the fill's span)
        TextMeshProUGUI _bossName;
        TextMeshProUGUI _bossGuard; // the Gilded Patron's payroll (Phase 5.13)
        bool _bossGuardOn;
        BossController _bossShown;
        bool _timerBoss;
        const float BossBarW = 760f;

        // arena intro (M3.5): name + tagline on a mosaic plaque over a dim screen while the pools fill, then FIGHT!
        CanvasGroup _introGroup;
        Image _introDim, _introPlaque;
        TextMeshProUGUI _introName, _introTag, _introFight;
        RunIntro _introShown;
        int _frames;
        int _lastTags = -1;
        float _lastSpeed = 1f;

        // build strip: a row of weapon tiles, a row of Boon tiles (line = best rarity taken, glyph, level)
        const int WeaponTiles = 4, BoonTiles = 6, StripTiles = WeaponTiles + BoonTiles;
        readonly Image[] _tileChip = new Image[StripTiles];
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
        static readonly Color ChipText = new Color(0.55f, 0.95f, 1f);
        // P1: each chip is its icon then its name, as wide as the name; they flow in rows under the Boon row (8 px gap)
        const float ChipLeft = 24f, ChipH = 30f, ChipGap = 6f, ChipRowWidth = 380f;
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
        // the Bookmaker (W1): "[E]  THE BOOK" over him while he calls you over, "COME BACK LATER" during his cooldown
        TextMeshProUGUI _bookTag;
        int _bookKey = int.MinValue;
        const float BookmakerTagHeight = 2.3f;
        const string BookTag = "[E]  THE BOOK", BookLaterTag = "COME BACK LATER", BookPrompt = "[E] Open the Bookmaker's book";
        static readonly Color AltarColor = new Color(1f, 0.4f, 0.55f);

        static readonly Color WeaponTileInner = new Color(0.2f, 0.13f, 0.07f, 0.92f); // warm bronze: weapons vs Boons

        // Favor (Phase 5.1): a gold bar under the level (pulses when a gift is near) + the loaned weapon's tile after the
        // weapon row, its inner draining as the loan runs out
        Image _favorFill, _loanChip, _loanBorder, _loanInner;
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
        // Asterion (R3): the same bar is his Rage (bronze; white-gold while Unchained, draining; grey while Spent).
        static readonly Color RageColor = new Color(0.80f, 0.56f, 0.30f);
        static readonly Color RageHot = new Color(1f, 0.93f, 0.7f);
        static readonly Color RageSpent = new Color(0.45f, 0.45f, 0.48f);

        // R7: the edicts in effect, one 28 px icon each on a dark chip, centred under the timer
        Transform _canvas;
        RectTransform _edictRow;
        CanvasGroup _edictGroup, _stripGroup;
        int _edictIcons;
        const float EdictIcon = 28f, EdictPlate = 30f, EdictGap = 4f, EdictTop = -91f;
        static readonly Color EdictHurt = new Color(1f, 0.33f, 0.2f);

        static TextMeshProUGUI AutoSize(TextMeshProUGUI t, float min, float max)
        {
            t.enableAutoSizing = true;
            t.fontSizeMin = min;
            t.fontSizeMax = max;
            return t;
        }

        /// <summary>
        /// A UiKit trough whose fill stretches inside it, so it follows the trough when that is stretched (the XP bar).
        /// The insets are the trough's padding, capped so a thin bar keeps most of its height.
        /// </summary>
        static Image Trough(string name, Transform parent, Color fill, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var f = UiKit.Bar(name, parent, fill, anchor, pivot, pos, size);
            var pad = UiFrames.Padding(FrameKind.Trough);
            float cap = size.y * 0.2f;
            UiFactory.Stretch(f.rectTransform, Vector2.zero, Vector2.one,
                new Vector2(Mathf.Min(pad.x, 6f), Mathf.Min(pad.y, cap)), new Vector2(-Mathf.Min(pad.z, 6f), -Mathf.Min(pad.w, cap)));
            return f;
        }

        /// <summary>A build-strip tile: a dark-bronze chip, a 2-unit rarity line inside it, the dark inside, the icon (or its letter).</summary>
        static void Tile(string name, Transform parent, Vector2 pos, out Image chip, out Image line, out Image inner, out UpgradeIconView icon,
            out TextMeshProUGUI glyph)
        {
            var tl = new Vector2(0f, 1f);
            var mid = new Vector2(0.5f, 0.5f);
            chip = UiKit.Frame(name, parent, FrameKind.Chip, tl, tl, pos, new Vector2(TileSize, TileSize), Color.clear);
            line = UiFactory.Image("Line", chip.transform, Color.clear, mid, mid, Vector2.zero, new Vector2(TileLine, TileLine), PixelIcons.White);
            inner = UiFactory.Image("Inner", chip.transform, Color.clear, mid, mid, Vector2.zero, new Vector2(TileInnerSize, TileInnerSize), PixelIcons.White);
            icon = new UpgradeIconView(inner.transform, "Icon", mid, Vector2.zero, TileIconSize, lively: false);
            glyph = AutoSize(UiKit.Text("Glyph", inner.transform, "", TextRole.Title, 22f, Color.white, TextAlignmentOptions.Center, mid, mid,
                new Vector2(0f, 2f), new Vector2(TileInnerSize, 30f), TextLook.Outline), 18f, 22f);
        }

        // the numbers' rows as tall as their textBounds with the theme's font, glyphs centred (TextBox.MidlineRow)
        static float BloodRow => Mathf.Max(BloodRowH, Mathf.Ceil(TextBox.MidlineRow(TextRole.Number, BloodSize)));
        static float GoldRow => Mathf.Max(GoldRowH, Mathf.Ceil(TextBox.MidlineRow(TextRole.Number, GoldSize)));
        static float LevelRow => Mathf.Max(LevelRowH, Mathf.Ceil(TextBox.MidlineRow(TextRole.Number, LevelSize)));

        /// <summary>
        /// The producer's note 21 (after P3): both top plates are one height, so the level-up's and the pause's two columns
        /// hang the same gap under them. It is the level plate's (its two rows) or the status plate's three rows packed
        /// (HP, a meter, gold and kills), whichever is more: 160 at the theme's fonts, the status plate 11 shorter than
        /// its P3 171 (it kept an empty meter row for gladiators without a meter).
        /// </summary>
        static float PlateHeight()
        {
            var pad = UiFrames.Padding(FrameKind.Bronze);
            float tb = pad.w + pad.y + 2f * UiKit.ContentPad;
            return Mathf.Max(tb + LevelRow + LevelGap + FavorRowH, tb + HpRowH + BloodRow + GoldRow);
        }

        /// <summary>
        /// P3: the top-left bronze plate, its rows laid out from its ContentRect (LayoutStatusRows): the heart and the HP
        /// trough with its number, the special meter (Blood, Wrath, Charge, Midas, Rage) and its label when there is one,
        /// the aureus and gold, the skull in its galea and kills. The build strip hangs under it.
        /// </summary>
        void BuildStatusPlate(Transform canvas, UiTheme th)
        {
            var tl = new Vector2(0f, 1f);
            var ml = new Vector2(0f, 0.5f);
            var mid = new Vector2(0.5f, 0.5f);
            var pad = UiFrames.Padding(FrameKind.Bronze);
            float p = UiKit.ContentPad;
            float top = pad.w + p, bottom = pad.y + p, left = PlateX + pad.x + p, right = PlateX + PlateW - pad.z - p;
            // the HP number sits on its trough by design (a 32-tall trough has no room for the padding rule)
            float bloodRow = BloodRow, goldRow = GoldRow;
            float h = _plateH;
            _statusPlate = UiKit.Frame("StatusPlate", canvas, FrameKind.Bronze, tl, tl, new Vector2(PlateX, Top), new Vector2(PlateW, h), Color.white).rectTransform;
            _statusBottom = Top - h;
            _statusContentTop = Top - top;
            _statusContentH = h - top - bottom;
            _weaponRowY = _statusBottom - StripGap;
            _boonRowY = _weaponRowY - TilePitch;
            _chipTop = _boonRowY - TileSize - 8f;
            float barLeft = left + IconPx + IconGap;

            // every row at y 0 here: LayoutStatusRows places them
            _hpIcon = UiKit.Frame("HpIcon", canvas, FrameKind.IconHeart, tl, mid, new Vector2(left + IconPx * 0.5f, 0f), new Vector2(IconPx, IconPx), Color.white);
            _hpFill = Trough("HpBar", canvas, th.hp, tl, ml, new Vector2(barLeft, 0f), new Vector2(right - barLeft, HpRowH));
            _hpText = UiKit.Text("HpText", _hpFill.transform.parent, "100 / 100", TextRole.Number, 26f, Color.white, TextAlignmentOptions.Midline, mid, mid,
                Vector2.zero, new Vector2(right - barLeft, HpRowH), TextLook.Outline);
            UiAudit.ExemptPadding(_hpText); // a readout printed on its bar (outlined), not a label inside the trough's padding

            _bloodFill = Trough("BloodBar", canvas, BloodColor, tl, ml, new Vector2(barLeft, 0f), new Vector2(BloodBarW, BloodBarH));
            _bloodFill.fillAmount = 0f;
            _bloodRoot = _bloodFill.transform.parent.gameObject;
            float bloodTextLeft = barLeft + BloodBarW + IconGap;
            _bloodText = AutoSize(UiKit.Text("BloodText", canvas, "", TextRole.Number, BloodSize, BloodColor, TextAlignmentOptions.MidlineLeft, tl, ml,
                new Vector2(bloodTextLeft, 0f), new Vector2(right - bloodTextLeft, bloodRow + LabelSlack), TextLook.Outline), 16f, BloodSize);
            PreSize(_bloodText, "+100% DMG");
            PreSize(_bloodText, "UNCHAINED");
            _bloodRoot.SetActive(false);
            _bloodText.gameObject.SetActive(false);

            _goldIcon = UiKit.Frame("GoldIcon", canvas, FrameKind.IconAureus, tl, mid, new Vector2(left + IconPx * 0.5f, 0f), new Vector2(IconPx, IconPx), Color.white);
            _gold = AutoSize(UiKit.Text("Gold", canvas, "0", TextRole.Number, GoldSize, _coin, TextAlignmentOptions.MidlineLeft, tl, ml,
                new Vector2(barLeft, 0f), new Vector2(GoldTextW, goldRow + LabelSlack), TextLook.Outline), 20f, GoldSize);
            float killIconLeft = barLeft + GoldTextW + IconGap;
            _killIcon = UiKit.Frame("KillIcon", canvas, FrameKind.IconSkullGalea, tl, mid, new Vector2(killIconLeft + IconPx * 0.5f, 0f), new Vector2(IconPx, IconPx), Color.white);
            float killsLeft = killIconLeft + IconPx + IconGap;
            _kills = AutoSize(UiKit.Text("Kills", canvas, "0", TextRole.Number, GoldSize, Color.white, TextAlignmentOptions.MidlineLeft, tl, ml,
                new Vector2(killsLeft, 0f), new Vector2(right - killsLeft, goldRow + LabelSlack), TextLook.Outline), 20f, GoldSize);
            LayoutStatusRows(false);
        }

        /// <summary>
        /// Note 21: the status plate's rows inside its ContentRect: HP at its top, gold-and-kills at its bottom (the same
        /// inset as at the top: the P3 padding rule), and the meter's row centred between them while the gladiator has a
        /// meter (DrawBlood calls this when one appears or goes). Moves rects only: no allocation.
        /// </summary>
        void LayoutStatusRows(bool meter)
        {
            _meterRow = meter;
            float goldRow = GoldRow;
            float hpY = _statusContentTop - HpRowH * 0.5f;
            float goldY = _statusContentTop - _statusContentH + goldRow * 0.5f;
            if (meter)
            {
                float bloodY = (hpY - HpRowH * 0.5f + goldY + goldRow * 0.5f) * 0.5f;
                SetY((RectTransform)_bloodRoot.transform, bloodY);
                SetY(_bloodText.rectTransform, bloodY);
            }
            SetY(_hpIcon.rectTransform, hpY);
            SetY((RectTransform)_hpFill.transform.parent, hpY);
            SetY(_goldIcon.rectTransform, goldY);
            SetY(_gold.rectTransform, goldY);
            SetY(_killIcon.rectTransform, goldY);
            SetY(_kills.rectTransform, goldY);
        }

        static void SetY(RectTransform rt, float y) => rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, y);

        /// <summary>
        /// P3 (the producer's note 14): the top-right bronze plate as wide as LevelWord + " 88" needs at the default size
        /// (it read mostly empty with "LV"), the word in its first row, the laurel and the Favor trough in the second,
        /// both inside its ContentRect.
        /// </summary>
        void BuildLevelPlate(Transform canvas, UiTheme th)
        {
            var tr = new Vector2(1f, 1f);
            var tl = new Vector2(0f, 1f);
            var ml = new Vector2(0f, 0.5f);
            var mid = new Vector2(0.5f, 0.5f);
            var top = new Vector2(0.5f, 1f);
            var pad = UiFrames.Padding(FrameKind.Bronze);
            float p = UiKit.ContentPad;
            float t = pad.w + p, b = pad.y + p, l = pad.x + p, r = pad.z + p;
            float levelRow = LevelRow;
            float h = _plateH;
            // note 21: when the status plate's rows set the height, the spare room goes between the two rows
            float levelGap = Mathf.Max(LevelGap, h - t - levelRow - FavorRowH - b);
            _levelPlate = UiKit.Frame("LevelPlate", canvas, FrameKind.Bronze, tr, tr, new Vector2(-PlateX, Top), new Vector2(LevelMinW, h), Color.white).rectTransform;
            string word = string.IsNullOrEmpty(LevelWord) ? "LV" : LevelWord;
            _levelFormat = word + " {0}";
            _level = UiKit.Text("Level", _levelPlate, word + " 1", TextRole.Number, LevelSize, Color.white, TextAlignmentOptions.Midline, top, mid,
                Vector2.zero, new Vector2(600f, levelRow + LabelSlack), TextLook.Outline);
            float need = Mathf.Ceil(_level.GetPreferredValues(word + " 88").x);
            float w = Mathf.Max(LevelMinW, need + l + r);
            _levelPlate.sizeDelta = new Vector2(w, h);
            _level.rectTransform.anchoredPosition = new Vector2((l - r) * 0.5f, -t - levelRow * 0.5f);
            _level.rectTransform.sizeDelta = new Vector2(w - l - r, levelRow + LabelSlack);
            AutoSize(_level, 36f, LevelSize); // "LEVEL 100" in a long run shrinks before it's cut
            float favorY = -t - levelRow - levelGap - FavorRowH * 0.5f;
            _favorIcon = UiFactory.Image("FavorIcon", _levelPlate, Color.white, tl, mid, new Vector2(l + FavorIconSize.x * 0.5f, favorY), FavorIconSize, PixelIcons.Laurel);
            float barLeft = l + FavorIconSize.x + IconGap;
            _favorFill = Trough("FavorBar", _levelPlate, th.favor, tl, ml, new Vector2(barLeft, favorY), new Vector2(w - r - barLeft, 18f));
            _favorFill.fillAmount = 0f;
        }

        void Awake()
        {
            var th = UiTheme.Current;
            _ink = th.ink;
            _goldColor = th.gold;
            _goldBright = th.goldBright;
            _crimson = th.crimson;
            _crimsonLight = th.crimsonLight;
            _coin = th.coin;
            _loss = th.loss;
            var canvas = UiFactory.Canvas("HudCanvas", transform, UiSortOrder.Hud).transform;
            _canvas = canvas;
            var top = new Vector2(0.5f, 1f);
            var tl = new Vector2(0, 1);
            var tr = new Vector2(1, 1);
            var mid = new Vector2(0.5f, 0.5f);

            // XP: a thin trough across the top
            _xpFill = Trough("XpBar", canvas, th.xp, top, top, Vector2.zero, new Vector2(10, XpH));
            UiFactory.Stretch((RectTransform)_xpFill.transform.parent, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -XpH), Vector2.zero);

            _timer = UiKit.Text("Timer", canvas, "8:00", TextRole.Number, 64f, Color.white, TextAlignmentOptions.Center, top, top,
                new Vector2(0, Top), new Vector2(320, 76), TextLook.Outline);
            // P1: the dev switches in effect, spelled out, small, in the bottom-right corner (under the timer, a bare "BUFFS"
            // read as part of the game). Plain: a dev tool, not the game's look.
            _tags = UiFactory.Text("DevTags", canvas, "", 18, new Color(1f, 0.84f, 0.3f, 0.85f), TextAlignmentOptions.BottomRight, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-16, 12), new Vector2(1100, 26), font, fontMaterial);
            _objective = AutoSize(UiKit.Text("Objective", canvas, "", TextRole.Title, 28f, _goldColor, TextAlignmentOptions.Center, top, top,
                new Vector2(0, -125), new Vector2(760, 40), TextLook.Outline), 20f, 28f);

            _plateH = PlateHeight();
            BuildStatusPlate(canvas, th);
            BuildLevelPlate(canvas, th);

            // P3 integration: the build strip and synergy chips under one fading group (the level-up's and the pause's
            // inventory column stands where they are and shows the same items)
            var stripRoot = UiFactory.Rect("Strip", canvas, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            UiFactory.Stretch(stripRoot, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _stripGroup = stripRoot.gameObject.AddComponent<CanvasGroup>();
            _stripGroup.blocksRaycasts = false;
            var strip = (Transform)stripRoot;

            var loanPos = new Vector2(StripLeft + WeaponTiles * TilePitch + 10, _weaponRowY);
            Tile("LoanTile", strip, loanPos, out _loanChip, out _loanBorder, out _loanInner, out _loanIcon, out _loanGlyph);
            _loanInner.type = Image.Type.Filled;
            _loanInner.fillMethod = Image.FillMethod.Vertical;
            _loanTime = UiKit.Text("Time", _loanChip.transform, "", TextRole.Number, 18f, FavorHot, TextAlignmentOptions.BottomRight, new Vector2(1, 0), new Vector2(1, 0),
                new Vector2(-3, 1), new Vector2(34, 22), TextLook.Outline);
            PreSize(_loanGlyph, "WWWW");
            PreSize(_loanTime, "88");

            for (int i = 0; i < StripTiles; i++)
            {
                var pos = i < WeaponTiles ? new Vector2(StripLeft + i * TilePitch, _weaponRowY) : new Vector2(StripLeft + (i - WeaponTiles) * TilePitch, _boonRowY);
                Tile("Tile" + i, strip, pos, out _tileChip[i], out _tileBorder[i], out _tileInner[i], out _tileIcon[i], out _tileGlyph[i]);
                _tileLevel[i] = AutoSize(UiKit.Text("Level", _tileChip[i].transform, "", TextRole.Number, 20f, _goldBright, TextAlignmentOptions.BottomRight,
                    new Vector2(1, 0), new Vector2(1, 0), new Vector2(-3, 1), new Vector2(TileSize - 2f, 22f), TextLook.Outline), 18f, 20f);
                PreSize(_tileGlyph[i], "WWWW");
                PreSize(_tileLevel[i], "EVO!");
            }
            var ml = new Vector2(0f, 0.5f);
            for (int j = 0; j < SynergyChips; j++)
            {
                _chip[j] = UiKit.Frame("Synergy" + j, strip, FrameKind.Chip, tl, tl, new Vector2(ChipLeft, _chipTop), new Vector2(144, ChipH), Color.clear);
                _chipIcon[j] = new UpgradeIconView(_chip[j].transform, "Icon", ml, new Vector2(ChipIconLeft, 0), ChipIconSize, lively: false);
                _chipText[j] = UiKit.Text("Name", _chip[j].transform, "", TextRole.Title, 18f, ChipText, TextAlignmentOptions.Left, ml, ml,
                    new Vector2(ChipNameLeft, 1), new Vector2(110, 26));
                PreSize(_chipText[j], "EXECUTIONER!");
            }

            for (int i = 0; i < Tags; i++)
            {
                var root = UiFactory.Image("PriceTag" + i, canvas, Color.clear, mid, mid, Vector2.zero, new Vector2(150, 40), PixelIcons.White);
                _tagRoot[i] = root.rectTransform;
                _tagIcon[i] = UiKit.Frame("Coin", root.transform, FrameKind.IconAureus, mid, mid, new Vector2(-40, 0), new Vector2(IconPx, IconPx), Color.white);
                _tagText[i] = AutoSize(UiKit.Text("Price", root.transform, "", TextRole.Number, 30f, _coin, TextAlignmentOptions.MidlineLeft, mid, new Vector2(0f, 0.5f),
                    new Vector2(-40 + IconPx * 0.5f + IconGap, 0), new Vector2(120, 40), TextLook.Outline), 20f, 30f);
                PreSize(_tagText[i], "ALTAR");
                PreSize(_tagText[i], "88888");
                _tagRoot[i].localScale = Vector3.zero;
                _tagKey[i] = int.MinValue;
            }
            _bookTag = UiKit.Text("BookmakerTag", canvas, "", TextRole.Body, 28f, _coin, TextAlignmentOptions.Center, mid, mid, Vector2.zero, new Vector2(460, 40), TextLook.Outline);
            PreSize(_bookTag, BookTag);
            PreSize(_bookTag, BookLaterTag);
            _bookTag.rectTransform.localScale = Vector3.zero;
            var bottom = new Vector2(0.5f, 0f);
            _prompt = AutoSize(UiKit.Text("Prompt", canvas, "", TextRole.Body, 30f, Color.white, TextAlignmentOptions.Center, bottom, bottom,
                new Vector2(0, 150), new Vector2(1300, 46), TextLook.Outline), 22f, 30f);
            PreSize(_prompt, "[E] Blood Altar: lose 88% max HP for a Rare+ upgrade");
            PreSize(_prompt, "[E] Armory Chest  88888 gold  (not enough gold)");
            PreSize(_prompt, BookPrompt);

            _toast = UiKit.Text("Toast", canvas, "LEVEL UP!", TextRole.Shout, 84f, _goldBright, TextAlignmentOptions.Center, top, top,
                new Vector2(0, -239), new Vector2(900, 110), TextLook.Outline);
            _toast.alpha = 0f;
            // the callout: every announcer line that isn't a banner (streaks, roars, close calls, gifts, lines), under the
            // banner. Constant strings, so swapping them never allocates.
            // the banner hangs under the boss bar: crossbar top at y 207 (it has its own canvas: its motion re-batches only itself)
            // under a group that hides it while the game is paused: frozen mid-sway it would hang behind PAUSED or LEVEL UP!
            // P3 integration: the callout sits in the same group (a frozen "GORM FALLS!" showed between the reward's cards)
            var holder = new GameObject("BannerHolder", typeof(RectTransform), typeof(CanvasGroup));
            holder.transform.SetParent(canvas, false);
            UiFactory.Stretch((RectTransform)holder.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _bannerGroup = holder.GetComponent<CanvasGroup>();
            _bannerGroup.blocksRaycasts = _bannerGroup.interactable = false;
            _announce = AutoSize(UiKit.Text("Announce", holder.transform, "THE PACK IS LOOSE!", TextRole.Shout, 48f, Color.white, TextAlignmentOptions.Center, top, top,
                new Vector2(0, -383), new Vector2(1100, 64), TextLook.Outline), 30f, 48f);
            _announce.alpha = 0f;
            _banner = new VexillumBanner(holder.transform, top, new Vector2(0f, -207f));

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
            _introPlaque = UiKit.Frame("Plaque", introRoot.transform, FrameKind.Mosaic, centre, centre, new Vector2(0, 40), PlaqueSize, Color.clear);
            _introName = AutoSize(UiKit.Text("ArenaName", introRoot.transform, "", TextRole.Title, 96f, _goldColor, TextAlignmentOptions.Center, centre, centre,
                new Vector2(0, 70), new Vector2(PlaqueSize.x - 2f * PlaquePad, 120), TextLook.Flat), 56f, 96f);
            var tagColor = _ink;
            tagColor.a = 0.8f;
            _introTag = AutoSize(UiKit.Text("Tagline", introRoot.transform, "", TextRole.Body, 34f, tagColor, TextAlignmentOptions.Center, centre, centre,
                new Vector2(0, -20), new Vector2(PlaqueSize.x - 2f * PlaquePad, 50), TextLook.Flat), 22f, 34f);
            _introFight = UiKit.Text("Fight", introRoot.transform, "FIGHT!", TextRole.Shout, 160f, _goldBright, TextAlignmentOptions.Center, centre, centre,
                new Vector2(0, 20), new Vector2(1600, 190), TextLook.Outline);
            _introFight.alpha = 0f;

            var bossRoot = new GameObject("BossHud", typeof(RectTransform), typeof(CanvasGroup));
            bossRoot.transform.SetParent(canvas, false);
            var brt = (RectTransform)bossRoot.transform;
            brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one; brt.offsetMin = brt.offsetMax = Vector2.zero;
            _bossGroup = bossRoot.GetComponent<CanvasGroup>();
            _bossGroup.alpha = 0f;
            _bossGroup.blocksRaycasts = _bossGroup.interactable = false;
            _bossName = AutoSize(UiKit.Text("BossName", bossRoot.transform, "", TextRole.Title, 30f, _ink, TextAlignmentOptions.Center, top, top,
                new Vector2(0, -115), new Vector2(900, 36), TextLook.Outline), 20f, 30f);
            PreSize(_bossName, "GORM, THE PIT MASTER OF THE SLAVE PITS");
            _bossFill = Trough("BossBar", bossRoot.transform, _crimson, top, top, new Vector2(0, -153), new Vector2(BossBarW, 26));
            _bossInset = new Vector2(_bossFill.rectTransform.offsetMin.x, -_bossFill.rectTransform.offsetMax.x);
            _bossGuard = UiKit.Text("BossGuard", bossRoot.transform, GuardedText, TextRole.Title, 20f, _goldColor, TextAlignmentOptions.Center, top, top,
                new Vector2(0, -181), new Vector2(900, 24), TextLook.Outline);
            _bossGuard.enabled = false;
            // P3: the pause is LevelUpPanel's now (the producer's note 17: PAUSED, its plates, the inventory and the stats)
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
            PreSize(_level, LevelWord + " 8888");
            PreSize(_announce, "THE CROWD IS ON ITS FEET FOR YOU!");
            PreSize(_objective, "WAGER  8888 / 8888   888s");
        }

        // R7: RunModifiers.Set comes before the arena loads, so the edicts are known by every Start
        // R8: so is the daily (GameSession.StartDaily)
        void Start()
        {
            BuildEdictRow(RunModifiers.Active);
            if (GameSession.HasDaily) BuildDailyChip(GameSession.DailyScored);
        }

        // R8: the daily chip, right of the timer's digits (and of "BOSS")
        RectTransform _dailyChip;
        TextMeshProUGUI _dailyChipText;
        // U1: a dark-bronze chip right of the timer's digits (Luckiest Guy is wider: a little further out), "DAILY" gold
        // on the scored attempt, dust-grey on a dimmed chip in practice
        const float DailyChipLeft = 108f, DailyChipY = -63f;
        static readonly Vector2 DailyChipSize = new Vector2(104f, 32f);
        static readonly Color DailyPracticeTint = new Color(0.7f, 0.7f, 0.72f, 1f);

        /// <summary>Tests: the daily chip is there, and its words ("DAILY") and colour.</summary>
        public bool DailyChipShown => _dailyChip != null && _dailyChip.gameObject.activeSelf;
        public string DailyChipText => _dailyChipText != null ? _dailyChipText.text : "";
        public RectTransform DailyChip => _dailyChip;
        public bool DailyChipScored { get; private set; }

        void BuildDailyChip(bool scored)
        {
            if (_canvas == null || _dailyChip != null) return;
            var top = new Vector2(0.5f, 1f);
            var mid = new Vector2(0.5f, 0.5f);
            var th = UiTheme.Current;
            var plate = UiKit.Frame("DailyChip", _canvas, FrameKind.Chip, top, new Vector2(0f, 0.5f), new Vector2(DailyChipLeft, DailyChipY), DailyChipSize,
                scored ? Color.white : DailyPracticeTint);
            _dailyChip = plate.rectTransform;
            _dailyChip.SetSiblingIndex(_timer.transform.GetSiblingIndex() + 1); // under the intro's dim, the boss HUD and PAUSED
            _dailyChipText = UiKit.Text("Label", _dailyChip, "DAILY", TextRole.Title, 20f, scored ? _goldBright : th.dust, TextAlignmentOptions.Center, mid, mid,
                Vector2.zero, DailyChipSize);
            DailyChipScored = scored;
        }

        /// <summary>Tests: how many edict icons the row holds, and whether it shows.</summary>
        public int EdictIconCount => _edictIcons;
        public bool EdictRowShown => _edictRow != null && _edictRow.gameObject.activeSelf;
        public RectTransform EdictRow => _edictRow;

        /// <summary>One icon per edict (the upgrade atlas's first frame, as the weapon strip draws them), built once.</summary>
        void BuildEdictRow(RunModifierDef[] active)
        {
            _edictIcons = 0;
            if (active == null || active.Length == 0 || _canvas == null) return;
            int n = active.Length;
            float w = n * EdictPlate + (n - 1) * EdictGap;
            var top = new Vector2(0.5f, 1f);
            var left = new Vector2(0f, 0.5f);
            var mid = new Vector2(0.5f, 0.5f);
            _edictRow = UiFactory.Rect("Edicts", _canvas, top, top, new Vector2(0f, EdictTop), new Vector2(w, EdictPlate));
            _edictGroup = _edictRow.gameObject.AddComponent<CanvasGroup>(); // hides with the timer (LEVEL UP! / PAUSED sit there)
            _edictGroup.blocksRaycasts = false;
            for (int i = 0; i < n; i++)
            {
                var d = active[i];
                if (d == null) continue;
                bool icon = d.icon != null && d.iconRow >= 0;
                var slot = new Vector2(i * (EdictPlate + EdictGap), 0f);
                var size = new Vector2(EdictPlate, EdictPlate);
                // its icon on a dark-bronze chip; with no atlas row yet, its initial on an orange-red plate (it hurts you)
                var plate = icon
                    ? UiKit.Frame("Edict_" + d.id, _edictRow, FrameKind.Chip, left, left, slot, size, Color.white)
                    : UiFactory.Image("Edict_" + d.id, _edictRow, EdictHurt, left, left, slot, size, PixelIcons.White);
                if (icon)
                {
                    var img = UiFactory.Rect("Icon", plate.transform, mid, mid, Vector2.zero, new Vector2(EdictIcon, EdictIcon)).gameObject.AddComponent<RawImage>();
                    img.raycastTarget = false;
                    img.texture = d.icon;
                    float cw = (float)UpgradeIconView.Cell / d.icon.width, ch = (float)UpgradeIconView.Cell / d.icon.height;
                    img.uvRect = new Rect(0f, 1f - (d.iconRow + 1) * ch, cw, ch);
                }
                else UiKit.Text("Glyph", plate.transform, EdictsPanel.Initial(d), TextRole.Title, 18f, Color.white, TextAlignmentOptions.Center, mid, mid, Vector2.zero, size);
                _edictIcons++;
            }
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
            // U1 routing by kind, never by the sender's colour (spec §7.2): whims drop the crimson banner, the boss's
            // beats the purple one; everything else is a callout
            switch (e.Kind)
            {
                case AnnounceKind.Whim:
                    RouteBanner(e.Text, BannerCloth.Crimson, e.Seconds);
                    return;
                case AnnounceKind.BossEnters:
                case AnnounceKind.BossPhase:
                case AnnounceKind.BossGuards:
                case AnnounceKind.BossFalls:
                    RouteBanner(e.Text, BannerCloth.Purple, e.Seconds);
                    return;
            }
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
                // the mosaic plaque: as wide as the canvas allows (4:3 is 1663 wide)
                float w = Mathf.Min(PlaqueSize.x, ((RectTransform)_canvas).rect.width - 48f);
                if (w < 200f) w = PlaqueSize.x;
                _introPlaque.rectTransform.sizeDelta = new Vector2(w, PlaqueSize.y);
                _introName.rectTransform.sizeDelta = new Vector2(w - 2f * PlaquePad, _introName.rectTransform.sizeDelta.y);
                _introTag.rectTransform.sizeDelta = new Vector2(w - 2f * PlaquePad, _introTag.rectTransform.sizeDelta.y);
            }
            _introGroup.alpha = 1f;
            if (intro.Current == RunIntro.Stage.Holding)
            {
                _introDim.color = new Color(0.03f, 0.02f, 0.02f, 0.86f);
                _introName.alpha = Mathf.Clamp01(intro.T * 3f);
                _introPlaque.color = new Color(1f, 1f, 1f, _introName.alpha);
                _introTag.alpha = Mathf.Clamp01(intro.T * 2f - 0.6f) * 0.8f;
                _introFight.alpha = 0f;
            }
            else
            {
                float k = Mathf.Clamp01(intro.T / Mathf.Max(0.1f, intro.fightBanner));
                _introDim.color = new Color(0.03f, 0.02f, 0.02f, 0.86f * (1f - k));
                _introName.alpha = _introTag.alpha = 0f;
                _introPlaque.color = Color.clear;
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
            if (run != null && run.TimerDone && !_timerBoss) { _timerBoss = true; _timer.text = "BOSS"; _timer.color = _crimsonLight; }
            var b = BossController.Active;
            bool show = b != null && b.Alive && b.def != null;
            if (show && _bossShown != b)
            {
                _bossShown = b;
                _bossName.text = b.def.displayName + ", " + b.def.title; // once per boss
                Notch("Phase2", b.def.phase2At);
                if (b.def.phase3At > 0f) Notch("Phase3", b.def.phase3At); // a third phase (the Emperor's Champion)
            }
            float a = show ? 1f : 0f;
            if (!Mathf.Approximately(_bossGroup.alpha, a))
            {
                _bossGroup.alpha = a;
                if (_edictRow != null) _edictRow.gameObject.SetActive(!show); // the boss's name sits where the edicts do
            }
            if (show) _bossFill.fillAmount = Mathf.MoveTowards(_bossFill.fillAmount, b.Hp01, Time.unscaledDeltaTime * 1.5f);
            bool guard = show && b.Guarded;
            if (guard != _bossGuardOn)
            {
                _bossGuardOn = guard;
                _bossGuard.enabled = guard;
                _bossFill.color = guard ? _goldColor : _crimson; // a gilded bar: he's paying someone to take the hits
            }
        }

        /// <summary>A gold phase notch on the boss bar's fill, at `at` (0-1) of its span (once per boss).</summary>
        void Notch(string name, float at)
        {
            float span = BossBarW - _bossInset.x - _bossInset.y;
            var notch = UiFactory.Image(name, _bossFill.transform.parent, _goldColor, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(_bossInset.x + span * at, 0f), new Vector2(3f, 30f), PixelIcons.White);
            notch.raycastTarget = false;
        }

        const string GuardedText = "GUARDED: KILL HIS HIRED SWORDS";
        public bool BossGuardShown => _bossGuardOn;

        const string SwarmText = "SWARM INCOMING!";
        const string EliteText = "AN ELITE ENTERS!";

        void OnDirector(in DirectorEvent e) => RouteBanner(e.Kind == DirectorEventKind.Swarm ? SwarmText : EliteText, BannerCloth.Crimson, 0f);

        /// <summary>
        /// Purple outranks crimson. The same rank or higher while a banner is down: it rolls up quickly and the new one
        /// drops (VexillumBanner.Show). A lower one waits in the one slot (the newest wins) until the banner is hidden.
        /// Nothing once the run has ended (the end screen owns the centre).
        /// </summary>
        void RouteBanner(string text, BannerCloth cloth, float seconds)
        {
            var run = RunState.Current;
            if (run != null && run.Ended) return;
            int rank = Rank(cloth);
            if (!_banner.Visible || rank >= _bannerRank)
            {
                _banner.Show(text, cloth, seconds);
                _bannerRank = rank;
                return;
            }
            _pendingText = text;
            _pendingCloth = cloth;
            _pendingSeconds = seconds;
        }

        /// <summary>Ticks the banner on game time (it freezes under the level-up and the pause), then drops the waiting one.</summary>
        void TickBanner(float dt, RunState run)
        {
            if (run != null && run.Ended)
            {
                if (_banner.Visible) _banner.Clear();
                _pendingText = null;
                return;
            }
            _banner.Tick(dt);
            float shown = TimeController.IsPaused ? 0f : 1f;
            if (_bannerGroup.alpha != shown) _bannerGroup.alpha = shown;
            if (_banner.Visible || _pendingText == null) return;
            string text = _pendingText;
            _pendingText = null;
            _banner.Show(text, _pendingCloth, _pendingSeconds);
            _bannerRank = Rank(_pendingCloth);
        }

        /// <summary>Tests: the banner, the line waiting for it (null when none), and the callout's words and whether it shows.</summary>
        public VexillumBanner Banner => _banner;
        public string PendingBanner => _pendingText;
        public string CalloutText => _announce != null ? _announce.text : "";
        public bool CalloutShown => _announce != null && _announce.alpha > 0f;
        /// <summary>Tests: themed labels (role checks) and the HUD's canvas (truncation checks; the debug line is Plain).</summary>
        public Component TimerLabel => _timer;
        public Component LevelLabel => _level;
        /// <summary>P3 tests: the level plate's words ("LEVEL 3"), the HP number (it sits on its trough by design: a
        /// 32-tall trough has no room for the padding rule), the plates, the icons and the HP trough.</summary>
        public string LevelText => _level.text;
        public Component HpLabel => _hpText;
        public RectTransform StatusPlateRect => _statusPlate;
        public RectTransform LevelPlateRect => _levelPlate;
        public RectTransform HpIconRect => _hpIcon.rectTransform;
        public RectTransform GoldIconRect => _goldIcon.rectTransform;
        public RectTransform KillIconRect => _killIcon.rectTransform;
        public RectTransform FavorIconRect => _favorIcon.rectTransform;
        public RectTransform HpBarRect => (RectTransform)_hpFill.transform.parent;
        public RectTransform FavorBarRect => (RectTransform)_favorFill.transform.parent;
        /// <summary>The special meter's trough (active only while the gladiator has a meter; note 21).</summary>
        public RectTransform BloodBarRect => (RectTransform)_bloodRoot.transform;
        public RectTransform XpBarRect => (RectTransform)_xpFill.transform.parent;
        public Component BloodLabelText => _bloodText;
        /// <summary>The Boon row's bottom edge (canvas units from the top): the synergy chips start under it.</summary>
        public float BoonRowBottom => _boonRowY - TileSize;
        /// <summary>Tests: the FrameKind the HP, gold and kills icons and a price tag's coin show (UiFrames.KindOf).</summary>
        public int HpIconFrame => UiFrames.KindOf(_hpIcon);
        public int GoldIconFrame => UiFrames.KindOf(_goldIcon);
        public int KillIconFrame => UiFrames.KindOf(_killIcon);
        public int PriceTagIconFrame => UiFrames.KindOf(_tagIcon[0]);

        /// <summary>Tests: a label's rendered glyphs (TMP textBounds, after a mesh update) in world space.</summary>
        public static Rect TextWorldRect(Component label)
        {
            var t = label as TMP_Text;
            if (t == null) return default;
            // the glyphs' ink (as UiAudit measures), not TMP's ascent-to-descent textBounds: the icons centre on what you see
            Vector3 min, max;
            if (UiAudit.TryInkBounds(t, out var ink)) { min = ink.min; max = ink.max; }
            else { t.ForceMeshUpdate(); min = t.textBounds.min; max = t.textBounds.max; }
            var a = t.rectTransform.TransformPoint(min);
            var c = t.rectTransform.TransformPoint(max);
            return Rect.MinMaxRect(Mathf.Min(a.x, c.x), Mathf.Min(a.y, c.y), Mathf.Max(a.x, c.x), Mathf.Max(a.y, c.y));
        }

        /// <summary>Tests: a frame's ContentRect (UiKit.ContentRect) in world space.</summary>
        public static Rect ContentWorldRect(RectTransform frame, FrameKind kind)
        {
            var r = frame.rect;
            var cr = UiKit.ContentRect(kind, r.size);
            var a = frame.TransformPoint(r.center + cr.min);
            var c = frame.TransformPoint(r.center + cr.max);
            return Rect.MinMaxRect(Mathf.Min(a.x, c.x), Mathf.Min(a.y, c.y), Mathf.Max(a.x, c.x), Mathf.Max(a.y, c.y));
        }
        public Component GoldLabel => _gold;
        public Component KillsLabel => _kills;
        public Component CalloutLabel => _announce;
        public Component BossNameLabel => _bossName;
        public Component DebugLabel => _debug;
        public RectTransform CanvasRect => (RectTransform)_canvas;

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
            {
                if (!_tileReady[i]) continue;
                float pulse = 0.5f + 0.5f * Mathf.Sin(now * 6f);
                _tileBorder[i].color = Color.Lerp(_tileColor[i], Color.white, pulse);
                _tileChip[i].color = Color.Lerp(Color.white, _goldBright, pulse); // the chip glows gold too
            }
            _loanIcon.Tick(now);
            if (run != null)
            {
                _xpFill.fillAmount = run.Xp01;
                int sec = Mathf.CeilToInt(run.TimeRemaining);
                if (sec != _lastSecond && !run.TimerDone) { _lastSecond = sec; WriteTime(sec); }
                if (run.Level != _lastLevel) { _lastLevel = run.Level; _level.SetText(_levelFormat, run.Level); }
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
                if (_build == null) _build = player.GetComponent<PlayerBuild>();
                if (_build != null && _build.State.Version != _lastBuildVersion) RefreshStrip();
            }

            // the clock is frozen under the cards and LEVEL UP! (or PAUSED, or W1's THE BOOK) sits over it: hide it until they close
            float timerAlpha = (LevelUpController.Instance != null && LevelUpController.Instance.IsOpen) || (player != null && player.IsPaused)
                               || (Bookmaker.Instance != null && Bookmaker.Instance.BookOpen) ? 0f : 1f;
            if (_timer.alpha != timerAlpha) _timer.alpha = timerAlpha;
            if (_edictGroup != null && _edictGroup.alpha != timerAlpha) _edictGroup.alpha = timerAlpha;
            if (_stripGroup.alpha != timerAlpha) _stripGroup.alpha = timerAlpha;

            DrawInteractables(run);
            DrawFavor(unscaledDt);
            DrawBlood(unscaledDt);
            DrawBoss(run);
            DrawObjective();
            DrawIntro(unscaledDt);
            // P3: the pause's keys (Q, O) are LevelUpPanel's, with its plates

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

            TickBanner(dt, run);

            // the callout: pop in, hold, fade (game time, so it waits during level-ups)
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

        LevelUpPanel _cardsPanel;
        LevelUpPanel Cards => _cardsPanel != null ? _cardsPanel : _cardsPanel = FindAnyObjectByType<LevelUpPanel>();

        /// <summary>Tests: the pause's PAUSED title (LevelUpPanel's since P3), the width its word needs and its lines (1).</summary>
        public RectTransform PauseRect => Cards != null ? Cards.PauseTitleRect : null;
        public float PauseTextWidth => Cards != null ? Cards.PauseTitleWidth : 0f;
        public int PauseLineCount => Cards != null ? Cards.PauseTitleLines : 0;

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
            var rage = Rage.Instance;
            bool wrath = rite == null && verdict != null && verdict.trait != null;
            bool charge = rite == null && !wrath && storm != null && storm.trait != null;
            bool gold = rite == null && !wrath && !charge && midas != null && midas.trait != null;
            bool raging = rite == null && !wrath && !charge && !gold && rage != null && rage.trait != null;
            bool on = (rite != null && rite.trait != null) || wrath || charge || gold || raging;
            if (_bloodRoot.activeSelf != on) { _bloodRoot.SetActive(on); _bloodText.gameObject.SetActive(on); }
            if (_meterRow != on) LayoutStatusRows(on);
            if (!on) return;
            if (wrath) { DrawWrath(verdict, unscaledDt); return; }
            if (charge) { DrawCharge(storm, unscaledDt); return; }
            if (gold) { DrawMidas(midas, unscaledDt); return; }
            if (raging) { DrawRage(rage, unscaledDt); return; }
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

        /// <summary>
        /// Asterion's Rage on the Blood bar: the meter while calm (pulsing hot when CORNERED or nearly full), draining
        /// white-gold while Unchained, empty and grey while Spent; "SLIDE!" when a full meter waits for a slide
        /// (triggerOnSlide).
        /// </summary>
        void DrawRage(Rage r, float unscaledDt)
        {
            float t = Time.unscaledTime;
            float target, rate;
            Color fill;
            int key;
            switch (r.Current)
            {
                case Rage.Mode.Unchained:
                    target = r.Unchained01;
                    rate = 6f;
                    fill = Color.Lerp(RageHot, RageColor, 0.35f * (0.5f + 0.5f * Mathf.Sin(t * 14f)));
                    key = RageKeyUnchained;
                    break;
                case Rage.Mode.Spent:
                    target = 0f;
                    rate = 6f;
                    fill = RageSpent;
                    key = RageKeySpent;
                    break;
                default:
                    target = r.Fill01;
                    rate = 3f;
                    bool hot = r.Cornered || r.Fill01 > 0.85f || r.WaitingForSlide;
                    fill = hot ? Color.Lerp(RageColor, RageHot, 0.5f + 0.5f * Mathf.Sin(t * 8f)) : RageColor;
                    key = r.WaitingForSlide ? RageKeySlide : r.Cornered ? RageKeyCornered : RageKeyNone;
                    break;
            }
            _bloodFill.fillAmount = Mathf.MoveTowards(_bloodFill.fillAmount, target, unscaledDt * rate);
            _bloodFill.color = fill;
            if (key != _bloodKey)
            {
                _bloodKey = key;
                if (key == RageKeyUnchained) _bloodText.SetText("UNCHAINED");
                else if (key == RageKeySpent) _bloodText.SetText("SPENT");
                else if (key == RageKeySlide) _bloodText.SetText("SLIDE!");
                else if (key == RageKeyCornered) _bloodText.SetText("CORNERED");
                else _bloodText.SetText("");
            }
            _bloodText.color = key == RageKeyNone ? RageColor : fill;
        }

        // the Rage bar's label keys (the Blood bar's other gladiators use 0 and up, -1, -2, -3)
        const int RageKeyNone = -20, RageKeyCornered = -21, RageKeyUnchained = -22, RageKeySpent = -23, RageKeySlide = -24;

        /// <summary>The Blood bar's label right now (tests).</summary>
        public string BloodLabel => _bloodText != null ? _bloodText.text : "";

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
                _loanChip.color = def != null ? Color.white : Color.clear;
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
                        _tagText[shown].color = afford ? _coin : _loss;
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
            // a chest in reach takes E first; during his cooldown the tag over him says so and E only gets his line
            bool bet = reach < 0 && bm != null && bm.InReach && !bm.CoolingDown && run != null && !run.Ended;
            int pkey = reach >= 0 ? (props.KindOf(reach) == Interactables.Kind.Altar ? -2 : price * 2 + (afford ? 1 : 0))
                     : bet ? -3 : -1;
            if (pkey == _promptKey) return;
            _promptKey = pkey;
            if (pkey == -1) { _prompt.text = ""; return; }
            if (pkey == -3)
            {
                _prompt.text = BookPrompt; // the book itself shows the stakes (W1)
                _prompt.color = _coin;
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
            _prompt.color = afford ? _coin : _loss;
        }

        /// <summary>The Bookmaker's world prompt over his head while he calls you (chests show their price the same way):
        /// "[E]  THE BOOK" in gold, or "COME BACK LATER" during his cooldown between wagers (W1).</summary>
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
            bool later = bm.CoolingDown;
            int key = later ? 1 : 0;
            if (key == _bookKey) return;
            _bookKey = key;
            _bookTag.text = later ? BookLaterTag : BookTag;
            _bookTag.color = later ? UiTheme.Current.dust : _coin;
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
                _chip[j].color = on ? Color.white : Color.clear;
                _chipText[j].text = on ? state.GetSynergy(j).displayName : "";
                if (!on) { _chipIcon[j].Clear(); continue; }
                _chipIcon[j].Set(state.GetSynergy(j).icon, state.GetSynergy(j).iconRow, ChipText, j * 0.4f);
                // P1: the name starts after the icon and the chip is as wide as its name; a full row wraps below
                float nameW = Mathf.Ceil(_chipText[j].preferredWidth);
                float w = ChipNameLeft + nameW + ChipNamePad;
                if (x > 0f && x + w > ChipRowWidth) { x = 0f; y -= ChipH + ChipGap; }
                var rt = _chip[j].rectTransform;
                rt.anchoredPosition = new Vector2(ChipLeft + x, _chipTop + y);
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
                _tileChip[k].color = Color.white;
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
                _tileChip[k].color = Color.clear;
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
