using Gladiator.Core;
using Gladiator.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Gladiator.UI
{
    /// <summary>
    /// End-of-run screen: "YOU DIED" (orange-red), "SURVIVED!" or "VICTORY!" (gold), the run's stats, the Renown it
    /// earned, any unlock it earned (gold banner), Retry (button / R / Enter / gamepad South) and Main Menu.
    /// Shown while RunEndController.ScreenShown. R7: under edicts the Renown row shows the multiplier,
    /// "+124 ×1.65 = +205  (2310)" (bounties after it, never multiplied).
    /// R8: the note line names the run's seed ("SEED 7F3A-91C2") or the daily ("DAILY 22 OCT · SCORED" / "· PRACTICE");
    /// the scored daily's ×1.5 joins the Renown breakdown ("+124 ×1.45 ×1.5 daily = +270"); the buttons are RETRY (a
    /// fresh seed) · SAME SEED (S / gamepad West: this seed again) · MAIN MENU, and in a daily PRACTICE (the same seed,
    /// never scored) · MAIN MENU.
    /// </summary>
    public sealed class RunEndPanel : MonoBehaviour, ITickable
    {
        public TMP_FontAsset font;
        public Material fontMaterial;

        static readonly Color Hurt = new Color(1f, 0.33f, 0.2f);
        static readonly Color Gold = new Color(1f, 0.84f, 0.3f);
        static readonly Color Soft = new Color(1f, 1f, 1f, 0.75f);

        GameObject _root;
        Button _retry, _sameSeed, _quit;
        const float SameSeedKeyDelay = 1f;
        bool _daily;
        TextMeshProUGUI _title, _subtitle, _note, _time, _level, _kills, _gold, _damage, _boss, _bossLabel, _renown, _unlock;
        bool _shown;
        float _shownAt;

        void Awake()
        {
            var canvas = UiFactory.Canvas("RunEndCanvas", transform, UiSortOrder.Menus).transform;
            _root = canvas.gameObject;
            var dim = UiFactory.Image("Dim", canvas, new Color(0.02f, 0.02f, 0.04f, 0.72f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, PixelIcons.White);
            UiFactory.Stretch(dim.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            dim.raycastTarget = true;
            var mid = new Vector2(0.5f, 0.5f);
            _title = UiFactory.Text("Title", canvas, "YOU DIED", 120, Hurt, TextAlignmentOptions.Center, mid, mid, new Vector2(0, 300), new Vector2(1400, 150), font, fontMaterial);
            _subtitle = UiFactory.Text("Subtitle", canvas, "", 36, Soft, TextAlignmentOptions.Center, mid, mid, new Vector2(0, 205), new Vector2(1400, 50), font, fontMaterial);
            _subtitle.fontStyle = FontStyles.Normal;

            var card = UiFactory.Image("Stats", canvas, new Color(0.08f, 0.1f, 0.14f, 0.95f), mid, mid, new Vector2(0, -44), new Vector2(620, 408), PixelIcons.White);
            _time = StatRow(card.transform, "Time survived", 0);
            _level = StatRow(card.transform, "Level", 1);
            _kills = StatRow(card.transform, "Kills", 2);
            _gold = StatRow(card.transform, "Gold", 3);
            _damage = StatRow(card.transform, "Damage dealt", 4);
            _boss = StatRow(card.transform, "Boss", 5);
            _bossLabel = card.transform.Find("Boss").GetComponent<TextMeshProUGUI>(); // named per arena in Fill
            _renown = StatRow(card.transform, "Renown", 6);
            _renown.color = Gold;
            // R7: wider than the other values (it may read "+124 ×1.65 = +205 +50  (2310)"), shrinking before it overflows;
            // R8: wider still for a scored daily ("+124 ×1.45 ×1.5 daily = +270 +150  (2410)"), from just right of its label
            var rr = _renown.rectTransform;
            rr.anchoredPosition = new Vector2(65f, rr.anchoredPosition.y);
            rr.sizeDelta = new Vector2(450f, rr.sizeDelta.y);
            _renown.enableAutoSizing = true;
            _renown.fontSizeMin = 18f;
            _renown.fontSizeMax = 34f;
            _renown.overflowMode = TextOverflowModes.Truncate;
            _unlock = UiFactory.Text("Unlock", canvas, "", 30, Gold, TextAlignmentOptions.Center, mid, mid, new Vector2(0, -296), new Vector2(1500, 84), font, fontMaterial);
            _unlock.enableAutoSizing = true; _unlock.fontSizeMin = 20; _unlock.fontSizeMax = 30;

            // R8: three buttons for a normal run, two for a daily (Fill places them)
            _retry = UiFactory.Button("Retry", canvas, "Retry  [R]", mid, mid, new Vector2(-290, -365), new Vector2(270, 70), font, fontMaterial, OnRetry, 32f);
            _sameSeed = UiFactory.Button("SameSeed", canvas, "Same Seed  [S]", mid, mid, new Vector2(0, -365), new Vector2(270, 70), font, fontMaterial, OnSameSeed, 32f);
            _quit = UiFactory.Button("Quit", canvas, "Main Menu  [M]", mid, mid, new Vector2(290, -365), new Vector2(270, 70), font, fontMaterial, OnQuit, 32f);
            _note = UiFactory.Text("Note", canvas, "", 22, new Color(1f, 1f, 1f, 0.7f), TextAlignmentOptions.Center, mid, mid, new Vector2(0, -430), new Vector2(1400, 40), font, fontMaterial);
            _note.fontStyle = FontStyles.Normal;
            _note.overflowMode = TextOverflowModes.Truncate;
            _root.SetActive(false);
        }

        TextMeshProUGUI StatRow(Transform card, string label, int row)
        {
            float y = 166f - row * 54f;
            var mid = new Vector2(0.5f, 0.5f);
            var l = UiFactory.Text(label, card, label, 32, Soft, TextAlignmentOptions.Left, mid, mid, new Vector2(-110, y), new Vector2(340, 50), font, fontMaterial);
            l.fontStyle = FontStyles.Normal;
            return UiFactory.Text(label + "Value", card, "0", 34, Color.white, TextAlignmentOptions.Right, mid, mid, new Vector2(170, y), new Vector2(220, 50), font, fontMaterial);
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
            }
            if (!show) return;
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
            _title.text = died ? "YOU DIED" : bossDown ? "VICTORY!" : "SURVIVED!";
            _title.color = died ? Hurt : Gold;
            _subtitle.text = died
                ? (RunState.Current != null && RunState.Current.TimerDone && bdef != null ? bdef.deathLine : "The crowd wanted more.")
                : bossDown && bdef != null ? bdef.victoryLine : "The pit falls silent.";
            // R8: the seed (or the daily) and the buttons that go with it
            _daily = GameSession.HasDaily; // R8-STUB(b)
            string seedLine = _daily
                ? SeedLine(true, GameSession.Daily.Date, GameSession.DailyScored, 0u) // R8-STUB(b)
                : SeedLine(false, 0, false, GameRandom.RunSeed); // R8-STUB(a)
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
            if (rewards == null) { _renown.text = "not recorded"; _unlock.text = ""; return; }
            _renown.text = RenownLine(rewards);
            _unlock.text = UnlockLines(rewards);
        }

        /// <summary>The Renown row's words (tests).</summary>
        public string RenownText => _renown != null ? _renown.text : "";
        /// <summary>The note's seed part, "SEED 7F3A-91C2" or "DAILY 22 OCT · SCORED" (tests).</summary>
        public string SeedText { get; private set; } = "";
        /// <summary>The whole note line (tests).</summary>
        public string NoteText => _note != null ? _note.text : "";
        /// <summary>SAME SEED is offered (a normal run's end).</summary>
        public bool SameSeedShown => _sameSeed != null && _sameSeed.gameObject.activeSelf;
        public RectTransform SameSeedRect => _sameSeed != null ? (RectTransform)_sameSeed.transform : null;
        public RectTransform RetryRect => _retry != null ? (RectTransform)_retry.transform : null;
        /// <summary>RETRY's words: "Retry  [R]", or "Practice  [R]" in a daily.</summary>
        public string RetryText => _retry != null ? UiFactory.Label(_retry).text : "";

        /// <summary>"SEED 7F3A-91C2" for a normal run; "DAILY 22 OCT · SCORED" or "DAILY 22 OCT · PRACTICE" for a daily.</summary>
        public static string SeedLine(bool daily, int date, bool scored, uint seed) => daily
            ? "DAILY " + DailyPanel.DateShort(date) + " · " + (scored ? "SCORED" : "PRACTICE")
            : "SEED " + GameRandom.SeedText(seed); // R8-STUB(a)

        /// <summary>A daily: PRACTICE · MAIN MENU; a normal run: RETRY · SAME SEED · MAIN MENU.</summary>
        void PlaceButtons(bool daily)
        {
            _sameSeed.gameObject.SetActive(!daily);
            UiFactory.Label(_retry).text = daily ? "Practice  [R]" : "Retry  [R]";
            ((RectTransform)_retry.transform).anchoredPosition = new Vector2(daily ? -150f : -290f, -365f);
            ((RectTransform)_quit.transform).anchoredPosition = new Vector2(daily ? 150f : 290f, -365f);
        }

        /// <summary>"+124  (2310)", "+124 +50  (2360)" with a bounty, "+124 ×1.65 = +205 +50  (2410)" under edicts,
        /// "+124 ×1.65 ×1.5 daily = +307 +50  (2512)" in a scored daily (R8).</summary>
        public static string RenownLine(RunRewards r)
        {
            float daily = r.DailyBonus; // R8-STUB(b)
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
        void OnSameSeed() { if (_shown && !_daily) GameSession.RetrySameSeed(); } // R8-STUB(b)
        void OnQuit() { if (_shown) RunEndController.Instance?.Quit(); }
    }
}
