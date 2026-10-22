using Gladiator.Core;
using Gladiator.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Gladiator.UI
{
    /// <summary>
    /// End-of-run screen: "YOU DIED", "SURVIVED!" or "VICTORY!", the run's stats, the Renown it earned, any unlock it
    /// earned, Retry (button / R / Enter / gamepad South) and Main Menu.
    /// Shown while RunEndController.ScreenShown. R7: under edicts the Renown row shows the multiplier,
    /// "+124 ×1.65 = +205  (2310)" (bounties after it, never multiplied).
    /// R8: the note line names the run's seed ("SEED 7F3A-91C2") or the daily ("DAILY 22 OCT · SCORED" / "· PRACTICE");
    /// the scored daily's ×1.5 joins the Renown breakdown ("+124 ×1.45 ×1.5 daily = +270"); the buttons are RETRY (a
    /// fresh seed) · SAME SEED (S / gamepad West: this seed again) · MAIN MENU, and in a daily PRACTICE (the same seed,
    /// never scored) · MAIN MENU.
    /// U1 (docs/tracks/U1-design.md §5, §7.3): death drops a crimson vexillum that stays, "YOU DIED" on its cloth (no
    /// more orange-red: dying isn't a hazard); survived / victory is a gold Shout title between laurel branches. The stats
    /// sit on a wax tablet, the unlock line between laurels, the buttons are tabulae ansatae (RETRY / PRACTICE bronze,
    /// SAME SEED and MAIN MENU marble) with today's words and keys. The end screen may allocate (the run is over).
    /// </summary>
    public sealed class RunEndPanel : MonoBehaviour, ITickable
    {
        [Tooltip("Unused since U1 (UiTheme decides the fonts); kept so the scene builders' links stay valid.")]
        public TMP_FontAsset font;
        public Material fontMaterial;

        const float TitleY = 300f, BannerWidth = 900f, BannerDrop = 72f; // the banner's bar sits so its cloth centres on the title's place
        const float ButtonsY = -372f, ButtonPitch = 360f, DailyPitch = 180f;
        static readonly Vector2 ButtonSize = new Vector2(340f, 66f);
        const float ButtonFont = 28f;
        const float YouDiedSize = 96f;

        GameObject _root;
        Transform _canvas;
        Button _retry, _sameSeed, _quit;
        UiPlate _retryPlate, _sameSeedPlate, _quitPlate;
        VexillumBanner _banner;
        RectTransform _titleLaurels, _unlockLaurels;
        Image _statsFrame;
        const float SameSeedKeyDelay = 1f;
        bool _daily;
        TextMeshProUGUI _title, _subtitle, _note, _time, _level, _kills, _gold, _damage, _boss, _bossLabel, _renown, _unlock;
        Color _ink, _waxInk, _goldColor, _goldBright;
        bool _shown;
        float _shownAt;

        /// <summary>Tests: the death banner (Held, Crimson, "YOU DIED"), the victory laurels, the stats tablet's frame.</summary>
        public VexillumBanner TitleBanner => _banner;
        public bool TitleHasLaurels => _titleLaurels != null && _titleLaurels.gameObject.activeInHierarchy;
        public int StatsFrame => UiFrames.KindOf(_statsFrame);
        public string TitleText => _title != null ? _title.text : "";
        public Component TitleLabel => _title;
        public RectTransform CanvasRect => (RectTransform)_canvas;

        void Awake()
        {
            var th = UiTheme.Current;
            _ink = th.ink;
            _waxInk = th.waxInk;
            _goldColor = th.gold;
            _goldBright = th.goldBright;
            var canvas = UiFactory.Canvas("RunEndCanvas", transform, UiSortOrder.Menus).transform;
            _canvas = canvas;
            _root = canvas.gameObject;
            var mid = new Vector2(0.5f, 0.5f);
            var dimColor = th.night;
            dimColor.a = 0.78f;
            var dim = UiFactory.Image("Dim", canvas, dimColor, mid, mid, Vector2.zero, Vector2.zero, PixelIcons.White);
            UiFactory.Stretch(dim.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            dim.raycastTarget = true;
            _title = UiKit.Text("Title", canvas, "", TextRole.Shout, 132f, _goldBright, TextAlignmentOptions.Center, mid, mid,
                new Vector2(0f, TitleY), new Vector2(1400f, 150f), TextLook.Outline);
            AutoSize(_title, 72f, 132f);
            _banner = new VexillumBanner(canvas, mid, new Vector2(0f, TitleY + BannerDrop), BannerWidth);
            var soft = _ink;
            soft.a = 0.8f;
            _subtitle = UiKit.Text("Subtitle", canvas, "", TextRole.Body, 36f, soft, TextAlignmentOptions.Center, mid, mid,
                new Vector2(0f, 205f), new Vector2(1400f, 50f), TextLook.Outline);
            AutoSize(_subtitle, 24f, 36f);

            _statsFrame = UiKit.Frame("Stats", canvas, FrameKind.Wax, mid, mid, new Vector2(0f, -44f), new Vector2(640f, 420f), Color.white);
            var card = _statsFrame.transform;
            _time = StatRow(card, "Time survived", 0);
            _level = StatRow(card, "Level", 1);
            _kills = StatRow(card, "Kills", 2);
            _gold = StatRow(card, "Gold", 3);
            _damage = StatRow(card, "Damage dealt", 4);
            _boss = StatRow(card, "Boss", 5);
            _bossLabel = card.Find("Boss").GetComponent<TextMeshProUGUI>(); // named per arena in Fill
            _renown = StatRow(card, "Renown", 6);
            _renown.color = _goldColor;
            // R7: wider than the other values (it may read "+124 ×1.65 = +205 +50  (2310)"), shrinking before it overflows;
            // R8: wider still for a scored daily ("+124 ×1.45 ×1.5 daily = +270 +150  (2410)"), from just right of its label
            var rr = _renown.rectTransform;
            rr.anchoredPosition = new Vector2(65f, rr.anchoredPosition.y);
            rr.sizeDelta = new Vector2(450f, rr.sizeDelta.y);
            AutoSize(_renown, 18f, 30f);
            _unlock = UiKit.Text("Unlock", canvas, "", TextRole.Title, 30f, _goldColor, TextAlignmentOptions.Center, mid, mid,
                new Vector2(0f, -298f), new Vector2(1400f, 80f), TextLook.Outline);
            AutoSize(_unlock, 20f, 30f);

            // R8: three buttons for a normal run, two for a daily (Fill places them)
            _retryPlate = UiKit.Button("Retry", canvas, "Retry  [R]", ButtonStyle.Primary, mid, mid, new Vector2(-ButtonPitch, ButtonsY), ButtonSize, OnRetry, ButtonFont);
            _sameSeedPlate = UiKit.Button("SameSeed", canvas, "Same Seed  [S]", ButtonStyle.Secondary, mid, mid, new Vector2(0f, ButtonsY), ButtonSize, OnSameSeed, ButtonFont);
            _quitPlate = UiKit.Button("Quit", canvas, "Main Menu  [M]", ButtonStyle.Secondary, mid, mid, new Vector2(ButtonPitch, ButtonsY), ButtonSize, OnQuit, ButtonFont);
            _retry = _retryPlate.Button;
            _sameSeed = _sameSeedPlate.Button;
            _quit = _quitPlate.Button;
            foreach (var p in new[] { _retryPlate, _sameSeedPlate, _quitPlate }) AutoSize(p.Label, 20f, ButtonFont);
            var dust = th.dust;
            _note = UiKit.Text("Note", canvas, "", TextRole.Body, 22f, dust, TextAlignmentOptions.Center, mid, mid,
                new Vector2(0f, -440f), new Vector2(1500f, 40f), TextLook.Outline);
            AutoSize(_note, 18f, 22f);
            _root.SetActive(false);
        }

        static void AutoSize(TMP_Text t, float min, float max)
        {
            t.enableAutoSizing = true;
            t.fontSizeMin = min;
            t.fontSizeMax = max;
        }

        TextMeshProUGUI StatRow(Transform card, string label, int row)
        {
            float y = 168f - row * 54f;
            var mid = new Vector2(0.5f, 0.5f);
            UiKit.Text(label, card, label, TextRole.Title, 22f, _waxInk, TextAlignmentOptions.Left, mid, mid, new Vector2(-110f, y), new Vector2(340f, 50f));
            var value = UiKit.Text(label + "Value", card, "0", TextRole.Number, 30f, _ink, TextAlignmentOptions.Right, mid, mid, new Vector2(170f, y), new Vector2(220f, 50f));
            AutoSize(value, 18f, 30f); // "not recorded", "88% left"
            return value;
        }

        void OnEnable() => GameLoop.Register(this, TickGroup.Late, 65);
        void OnDisable() => GameLoop.Unregister(this);

        public void Tick(float dt, float unscaledDt)
        {
            var c = RunEndController.Instance;
            bool show = c != null && c.ScreenShown;
            if (show != _shown)
            {
                _shown = show;
                _root.SetActive(show);
                if (show) Fill(c);
                else _banner.Clear();
            }
            if (!show) return;
            _banner.Tick(unscaledDt); // the screen is paused: the banner drops in real time
            // ignore keys for a moment so a held button doesn't instantly retry
            if (Time.unscaledTime - _shownAt < 0.4f) return;
            var kb = Keyboard.current;
            var pad = Gamepad.current;
            if ((kb != null && (kb.rKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame)) || (pad != null && pad.buttonSouth.wasPressedThisFrame))
                OnRetry();
            // S also walks backwards (WASD): it waits a little longer than R so a last dodge doesn't replay the seed
            else if (!_daily && ((kb != null && kb.sKey.wasPressedThisFrame && Time.unscaledTime - _shownAt >= SameSeedKeyDelay) || (pad != null && pad.buttonWest.wasPressedThisFrame)))
                OnSameSeed();
            else if ((kb != null && (kb.mKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame)) || (pad != null && pad.buttonEast.wasPressedThisFrame))
                OnQuit();
        }

        void Fill(RunEndController c)
        {
            _shownAt = Time.unscaledTime;
            var run = c.run;
            bool died = c.Outcome == RunOutcome.Died;
            bool bossDown = !died && BossController.Active != null && BossController.Active.Defeated;
            // this arena's boss (game over only: the lookup's allocation doesn't matter here)
            var fight = FindAnyObjectByType<BossFight>();
            var bdef = fight != null ? fight.boss : null;
            _bossLabel.text = bdef != null ? bdef.shortName : "Boss";
            ShowTitle(died, bossDown);
            _subtitle.text = died
                ? (RunState.Current != null && RunState.Current.TimerDone && bdef != null ? bdef.deathLine : "The crowd wanted more.")
                : bossDown && bdef != null ? bdef.victoryLine : "The pit falls silent.";
            // R8: the seed (or the daily) and the buttons that go with it
            _daily = GameSession.HasDaily;
            string seedLine = _daily
                ? SeedLine(true, GameSession.Daily.Date, GameSession.DailyScored, 0u)
                : SeedLine(false, 0, false, GameRandom.RunSeed);
            _note.text = Debug.isDebugBuild
                ? seedLine + "     <color=#FFFFFF73>Admin menu: F1 (God mode, buffs, spawns, \"Timer to 0:10\" for the boss)</color>"
                : seedLine;
            SeedText = seedLine;
            PlaceButtons(_daily);
            float t = run != null ? run.Elapsed : 0f;
            _time.SetText("{0}:{1:00}", Mathf.Floor(t / 60f), Mathf.Floor(t % 60f));
            _level.SetText("{0}", run != null ? run.Level : 0);
            _kills.SetText("{0}", run != null ? run.Kills : 0);
            _gold.SetText("{0}", run != null ? run.Gold : 0);
            _damage.SetText("{0}", run != null ? Mathf.Round(run.DamageDealt) : 0);
            var boss = BossController.Active;
            if (boss == null || run == null || !run.TimerDone) _boss.text = "not reached";
            else if (boss.Defeated) _boss.text = "DEFEATED";
            else _boss.SetText("{0}% left", Mathf.Round(boss.Hp01 * 100f));
            var rewards = c.Rewards;
            if (rewards == null) { _renown.text = "not recorded"; SetUnlock(""); return; }
            _renown.text = RenownLine(rewards);
            SetUnlock(UnlockLines(rewards));
        }

        /// <summary>Death: the crimson banner drops and stays, "YOU DIED" on its cloth. Otherwise the gold title between laurels.</summary>
        void ShowTitle(bool died, bool bossDown)
        {
            if (_titleLaurels != null) { Destroy(_titleLaurels.gameObject); _titleLaurels = null; }
            if (died)
            {
                _title.text = "";
                _banner.Hold("YOU DIED", BannerCloth.Crimson);
                StyleBannerWords();
                return;
            }
            _banner.Clear();
            _title.text = bossDown ? "VICTORY!" : "SURVIVED!";
            _title.color = _goldBright;
            _title.ForceMeshUpdate(true, true);
            _titleLaurels = UiKit.Laurels("TitleLaurels", _title);
        }

        /// <summary>
        /// "YOU DIED" is bigger than a run banner's words and in light ink (spec §5: Shout 96 `ink`). The banner keeps
        /// its words in one label under its root, set on Hold; this restyles that label after it (autosize up to 96:
        /// the cloth's room decides).
        /// </summary>
        void StyleBannerWords()
        {
            var words = _banner.Root != null ? _banner.Root.GetComponentInChildren<TextMeshProUGUI>(true) : null;
            if (words == null) return;
            words.color = _ink;
            if (words.enableAutoSizing) words.fontSizeMax = YouDiedSize;
            else words.fontSize = YouDiedSize;
        }

        /// <summary>The unlock line, between laurel branches when there is one (laurel means victory: an unlock earned).</summary>
        void SetUnlock(string text)
        {
            _unlock.text = text;
            if (_unlockLaurels != null) { Destroy(_unlockLaurels.gameObject); _unlockLaurels = null; }
            if (string.IsNullOrEmpty(text)) return;
            _unlock.ForceMeshUpdate(true, true);
            _unlockLaurels = UiKit.Laurels("UnlockLaurels", _unlock, 16f);
        }

        /// <summary>The Renown row's words (tests).</summary>
        public string RenownText => _renown != null ? _renown.text : "";
        /// <summary>The note's seed part, "SEED 7F3A-91C2" or "DAILY 22 OCT · SCORED" (tests).</summary>
        public string SeedText { get; private set; } = "";
        /// <summary>The whole note line (tests).</summary>
        public string NoteText => _note != null ? _note.text : "";
        /// <summary>SAME SEED is offered (a normal run's end).</summary>
        public bool SameSeedShown => _sameSeedPlate != null && _sameSeedPlate.Root.gameObject.activeSelf;
        public RectTransform SameSeedRect => _sameSeed != null ? (RectTransform)_sameSeed.transform : null;
        public RectTransform RetryRect => _retry != null ? (RectTransform)_retry.transform : null;
        /// <summary>RETRY's words: "Retry  [R]", or "Practice  [R]" in a daily.</summary>
        public string RetryText => _retry != null ? UiFactory.Label(_retry).text : "";

        /// <summary>"SEED 7F3A-91C2" for a normal run; "DAILY 22 OCT · SCORED" or "DAILY 22 OCT · PRACTICE" for a daily.</summary>
        public static string SeedLine(bool daily, int date, bool scored, uint seed) => daily
            ? "DAILY " + DailyPanel.DateShort(date) + " · " + (scored ? "SCORED" : "PRACTICE")
            : "SEED " + GameRandom.SeedText(seed);

        /// <summary>A daily: PRACTICE · MAIN MENU; a normal run: RETRY · SAME SEED · MAIN MENU.</summary>
        void PlaceButtons(bool daily)
        {
            _sameSeedPlate.Root.gameObject.SetActive(!daily);
            UiFactory.Label(_retry).text = daily ? "Practice  [R]" : "Retry  [R]";
            _retryPlate.Root.anchoredPosition = new Vector2(daily ? -DailyPitch : -ButtonPitch, ButtonsY);
            _quitPlate.Root.anchoredPosition = new Vector2(daily ? DailyPitch : ButtonPitch, ButtonsY);
        }

        /// <summary>"+124  (2310)", "+124 +50  (2360)" with a bounty, "+124 ×1.65 = +205 +50  (2410)" under edicts,
        /// "+124 ×1.65 ×1.5 daily = +307 +50  (2512)" in a scored daily (R8).</summary>
        public static string RenownLine(RunRewards r)
        {
            float daily = r.DailyBonus;
            string s;
            if (r.EdictBonus > 0f || daily > 0f)
            {
                s = "+" + r.BaseRenown;
                if (r.EdictBonus > 0f) s += " " + EdictsPanel.MulText(1f + r.EdictBonus);
                if (daily > 0f) s += " " + EdictsPanel.MulText(1f + daily) + " daily";
                s += " = +" + r.Renown;
            }
            else s = "+" + r.Renown;
            if (r.Bounty > 0) s += " +" + r.Bounty;
            return s + "  (" + r.RenownTotal + ")";
        }

        /// <summary>One line per new unlock (two at most, then "+N more in the Codex"), named by how it was earned.</summary>
        static string UnlockLines(RunRewards rewards)
        {
            var list = rewards.NewUnlocks;
            if (list.Count == 0) return "";
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < list.Count && i < 2; i++)
            {
                var u = list[i];
                if (i > 0) sb.Append('\n');
                sb.Append(u.category == UnlockCategory.Secret ? "SECRET FOUND: " : u.category == UnlockCategory.Challenge ? "CHALLENGE COMPLETE: " : "UNLOCKED: ");
                sb.Append(u.displayName.ToUpperInvariant());
                if (u.addsToPool != null && u.addsToPool.Length > 0) sb.Append("  —  now in the level-up pool");
                if (u.renownBounty > 0) sb.Append("  +").Append(u.renownBounty).Append(" Renown");
            }
            if (list.Count > 2) sb.Append("\n+").Append(list.Count - 2).Append(" more in the Codex");
            return sb.ToString();
        }

        void OnRetry() { if (_shown) RunEndController.Instance?.Retry(); }
        /// <summary>SAME SEED: this run's seed again (a normal run's end only; a daily's RETRY already is).</summary>
        void OnSameSeed() { if (_shown && !_daily) GameSession.RetrySameSeed(); }
        void OnQuit() { if (_shown) RunEndController.Instance?.Quit(); }
    }
}
