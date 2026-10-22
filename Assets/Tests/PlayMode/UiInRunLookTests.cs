using System.Collections;
using System.Collections.Generic;
using Gladiator.Core;
using Gladiator.Gameplay;
using Gladiator.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Gladiator.Tests
{
    /// <summary>
    /// U1-B in the test arena (docs/tracks/U1-contract.md, "U1-B in-run screens"): the HUD's numbers are Luckiest Guy
    /// (Number), the callout Caesar Dressing (Shout), the boss's name Marcellus SC (Title); the announcer routes swarms and
    /// elites to the crimson banner and the boss's beats to the purple one (purple outranks crimson, a lower one waits in
    /// one slot), streaks to the callout; the level-up cards wear their rarity's frame (the Legendary its wreath) and sit
    /// centred with the buttons (layout A); banish and the boss reward retitle the panel; nothing is cut off on the HUD or
    /// on the longest card; death holds the crimson banner, a survived run gets laurels, the stats sit on wax. Inspected
    /// through UiKit.HasRole / UiFrames.KindOf / UiKit.TruncatedLabels (this assembly has no TMP or UGUI reference).
    /// </summary>
    public class UiInRunLookTests
    {
        const string Scene = "Assets/Scenes/Dev/TestArena.unity";

        RunState _run;
        LevelUpController _lvl;
        PlayerBuild _build;
        LevelUpPanel _panel;
        Hud _hud;

        IEnumerator Boot()
        {
            DevSettings.ResetAll();
            TimeController.GameSpeed = 1f;
            TimeController.ResetForNewRun();
            var op = SceneManager.LoadSceneAsync(Scene, LoadSceneMode.Single);
            while (!op.isDone) yield return null;
            yield return null;
            if (Director.Instance != null) Director.Instance.enabled = false;
            var spawner = Object.FindAnyObjectByType<DebugEnemySpawner>();
            if (spawner != null) spawner.enabled = false;
            Player.Main.OverrideSource = new ScriptedInput();
            Player.Main.GetComponent<WeaponSystem>().enabled = false;
            DevSettings.GodMode = true;
            EnemyManager.Instance.KillAll();
            yield return new WaitForSeconds(0.3f);
            PickupSystem.Instance.ClearAll();
            yield return WaitRealtime(() => RunIntro.Instance == null || RunIntro.Instance.Done, 15f);
            _run = RunState.Current;
            _lvl = LevelUpController.Instance;
            _build = Player.Main.GetComponent<PlayerBuild>();
            _panel = Object.FindAnyObjectByType<LevelUpPanel>();
            _hud = Object.FindAnyObjectByType<Hud>();
            Assert.IsNotNull(_panel, "the arena has the level-up panel");
            Assert.IsNotNull(_hud, "the arena has the HUD");
        }

        [TearDown]
        public void TearDown() => DevSettings.ResetAll();

        static IEnumerator WaitRealtime(System.Func<bool> done, float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!done() && Time.realtimeSinceStartup < end) yield return null;
        }

        static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        IEnumerator OpenCards()
        {
            _run.AddXp(_run.XpToNext - _run.Xp + 0.01f);
            yield return WaitRealtime(() => _lvl.IsOpen, 3f);
            Assert.IsTrue(_lvl.IsOpen, "level-up cards should open");
            yield return Frames(2); // the cards draw, then the stats column
        }

        static float CentreX(RectTransform rt) => rt.TransformPoint(rt.rect.center).x;

        static void AssertNothingCut(Transform root, string what, string except = null)
        {
            var cut = new List<string>();
            UiKit.TruncatedLabels(root, cut);
            if (except != null) cut.RemoveAll(n => n == except);
            Assert.AreEqual(0, cut.Count, what + ": cut off: " + string.Join(", ", cut));
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Hud_NumbersCalloutAndBossName_HaveTheirRoles()
        {
            yield return Boot();
            Assert.IsTrue(UiKit.HasRole(_hud.TimerLabel, TextRole.Number), "the timer is Luckiest Guy");
            Assert.IsTrue(UiKit.HasRole(_hud.LevelLabel, TextRole.Number), "the level");
            Assert.IsTrue(UiKit.HasRole(_hud.GoldLabel, TextRole.Number), "the gold");
            Assert.IsTrue(UiKit.HasRole(_hud.KillsLabel, TextRole.Number), "the kills");
            Assert.IsTrue(UiKit.HasRole(_hud.CalloutLabel, TextRole.Shout), "the callout is Caesar Dressing");
            Assert.IsTrue(UiKit.HasRole(_hud.BossNameLabel, TextRole.Title), "the boss's name is Marcellus SC");
            var numbers = Object.FindAnyObjectByType<DamageNumbers>();
            Assert.IsNotNull(numbers, "the arena has damage numbers");
            Assert.IsTrue(UiKit.HasRole(numbers.PoolLabel(0), TextRole.Number), "damage numbers are Luckiest Guy");
            Assert.IsTrue(UiKit.HasRole(_panel.TitleLabel, TextRole.Shout), "LEVEL UP! is a Shout");
            Assert.IsTrue(UiKit.HasRole(_panel.CardNameLabel(0), TextRole.Title), "card names are Titles");
            Assert.IsTrue(UiKit.HasRole(_panel.CardDescriptionLabel(0), TextRole.Body), "card text is Body");
            Assert.IsTrue(UiKit.HasRole(_panel.CardKeyLabel(0), TextRole.Number), "card keys are Numbers");

            // a few upgrades in the strip, then nothing on the HUD is cut off (the Plain debug line aside)
            for (int k = 0; k < 3; k++)
            {
                yield return OpenCards();
                _lvl.Choose(0);
                yield return Frames(2);
            }
            yield return WaitRealtime(() => !_lvl.IsOpen, 2f);
            Canvas.ForceUpdateCanvases();
            AssertNothingCut(_hud.CanvasRect, "the HUD", ((Component)_hud.DebugLabel).name);
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator Announcer_BannersByKind_PurpleOutranksCrimson_StreaksAreCallouts()
        {
            yield return Boot();
            var banner = _hud.Banner;
            Assert.IsNotNull(banner);
            var master = Object.FindAnyObjectByType<ArenaMaster>();
            if (master != null) master.enabled = false; // no whim may take the banner's waiting slot mid-test
            yield return WaitRealtime(() => !_hud.CalloutShown, 6f); // whatever the crowd said at the start has faded

            GameEvents.OnDirector.Raise(new DirectorEvent { Kind = DirectorEventKind.Swarm, Color = Color.red });
            yield return null;
            Assert.IsTrue(banner.Visible, "a swarm drops the banner");
            Assert.AreEqual(BannerCloth.Crimson, banner.Cloth);
            Assert.AreEqual("SWARM INCOMING!", banner.Text);
            Assert.IsFalse(_hud.CalloutShown, "a swarm is not a callout");
            yield return WaitRealtime(() => banner.Phase == BannerPhase.Holding, 3f);
            Assert.AreEqual(BannerPhase.Holding, banner.Phase);

            const string BossLine = "THE PIT MASTER ENTERS!";
            GameEvents.OnAnnounce.Raise(new AnnounceEvent { Text = BossLine, Kind = AnnounceKind.BossEnters, Color = Color.white, Seconds = 3f });
            yield return null;
            Assert.AreEqual(BannerPhase.Rolling, banner.Phase, "the boss outranks the swarm: the crimson banner rolls up quickly");
            yield return WaitRealtime(() => banner.Text == BossLine && banner.Phase != BannerPhase.Rolling, 3f);
            Assert.AreEqual(BossLine, banner.Text, "then the boss's line drops");
            Assert.AreEqual(BannerCloth.Purple, banner.Cloth, "on purple");
            yield return WaitRealtime(() => banner.Phase == BannerPhase.Holding, 3f);

            // a streak is a callout: the banner is untouched
            GameEvents.OnAnnounce.Raise(new AnnounceEvent { Text = "KILLING SPREE!", Kind = AnnounceKind.Streak, Color = Color.white, Seconds = 2f, Tier = 1 });
            yield return null;
            Assert.IsTrue(_hud.CalloutShown, "a streak shows as a callout");
            Assert.AreEqual("KILLING SPREE!", _hud.CalloutText);
            Assert.AreEqual(BossLine, banner.Text, "the banner keeps the boss's line");
            Assert.AreEqual(BannerCloth.Purple, banner.Cloth);

            // an elite while the purple banner holds: it waits in the slot, then drops
            GameEvents.OnDirector.Raise(new DirectorEvent { Kind = DirectorEventKind.Elite, Color = Color.red });
            yield return null;
            Assert.AreEqual("AN ELITE ENTERS!", _hud.PendingBanner, "the lower banner waits");
            Assert.AreEqual(BossLine, banner.Text, "the boss's banner stays down");
            yield return WaitRealtime(() => banner.Text == "AN ELITE ENTERS!", VexillumBanner.TotalSeconds(3f) + 2f);
            Assert.AreEqual("AN ELITE ENTERS!", banner.Text, "shown once the purple one is up");
            Assert.AreEqual(BannerCloth.Crimson, banner.Cloth);
            Assert.IsNull(_hud.PendingBanner, "the slot is empty again");

            // the run ends: no banner
            _run.End(RunOutcome.Survived);
            yield return Frames(2);
            Assert.IsFalse(banner.Visible, "the end screen owns the centre");
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator Cards_RarityFrames_Wreath_CentredWithTheButtons_AndTitles()
        {
            yield return Boot();
            yield return OpenCards();
            Assert.GreaterOrEqual(_lvl.ChoiceCount, 3, "three cards");
            _lvl.Offers[0].Rarity = Rarity.Common;
            _lvl.Offers[1].Rarity = Rarity.Rare;
            _lvl.Offers[2].Rarity = Rarity.Legendary;
            _panel.ForceRedraw();
            yield return Frames(2);
            var frames = new[] { FrameKind.CardStone, FrameKind.CardSilver, FrameKind.CardGold };
            for (int i = 0; i < 3; i++)
            {
                Assert.AreEqual(new Vector2(300f, 520f), _panel.CardRect(i).rect.size, "layout A's 300×520 card");
                Assert.AreEqual((int)frames[i], _panel.CardFrame(i), "card " + i + " wears its rarity's frame");
                Assert.AreEqual(i == 2, _panel.CardHasWreath(i), "the laurel wreath only behind the Legendary's icon");
            }
            var fit = _panel.Fit;
            Debug.Log($"[U1] {Screen.width}x{Screen.height}: content scale {fit.ContentScale:0.###}, stats {fit.StatsScale:0.###}, x {fit.ContentX:0.#}, centred {fit.Centred}");
            Assert.AreEqual(fit.StatsScale, _panel.StatsScale, 1e-4f);
            int n = _lvl.ChoiceCount;
            float row = (CentreX(_panel.CardRect(0)) + CentreX(_panel.CardRect(n - 1))) * 0.5f;
            float buttons = CentreX(_panel.ActionRect(1));
            float buttonsSpan = (CentreX(_panel.ActionRect(0)) + CentreX(_panel.ActionRect(2))) * 0.5f;
            if (fit.Centred)
            {
                Assert.AreEqual(Screen.width * 0.5f, row, 1f, "the cards are centred on the screen");
                Assert.AreEqual(row, buttons, 1f, "and the buttons share their centre");
                Assert.AreEqual(row, buttonsSpan, 1f);
            }
            else Assert.AreEqual(row, buttons, 1f, "slid left together");

            // banish mode retitles the panel (crimson, not orange-red)
            yield return new WaitForSecondsRealtime(0.4f); // the input grace
            Assert.Greater(_lvl.BanishesLeft, 0);
            _panel.Click(LevelUpPanel.BanishId);
            yield return Frames(2);
            Assert.IsTrue(_panel.BanishMode);
            Assert.AreEqual("BANISH", _panel.TitleText);
            _panel.Click(LevelUpPanel.BanishId);
            yield return Frames(2);
            Assert.IsFalse(_panel.BanishMode);
            Assert.AreEqual("LEVEL UP!", _panel.TitleText);
            _lvl.Choose(0);
            yield return WaitRealtime(() => !_lvl.IsOpen, 2f);

            // the boss's reward: SPOILS OF VICTORY (every boss; it read "THE PIT MASTER'S HOARD")
            Assert.IsTrue(_lvl.QueueReward(PickSource.Boss, Rarity.Rare));
            yield return WaitRealtime(() => _lvl.IsOpen && _lvl.Source == PickSource.Boss, 3f);
            Assert.IsTrue(_lvl.IsOpen, "the boss's reward opens");
            yield return Frames(2);
            Assert.AreEqual("SPOILS OF VICTORY", _panel.TitleText);
            Assert.AreEqual("take one: rare or better", _panel.SubtitleText, "the subtitle is unchanged");
            _lvl.Choose(0);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Cards_TheLongestDescription_IsNotCutOff()
        {
            yield return Boot();
            var cfg = _build.config;
            var buf = new CharBuffer(1024);
            CardOffer longest = default;
            int most = -1;
            foreach (var d in _build.catalog.upgrades)
            {
                if (d == null) continue;
                var o = new CardOffer { Def = d, Rarity = Rarity.Legendary, Level = Mathf.Min(Mathf.Max(1, d.maxLevel), _build.State.LevelOf(d) + 1) };
                buf.Clear();
                CardText.Describe(o, cfg, buf);
                CardText.Hints(o, _build.State, _build.catalog, buf);
                if (buf.Length > most) { most = buf.Length; longest = o; }
            }
            Assert.IsNotNull(longest.Def, "the catalogue has upgrades");
            yield return OpenCards();
            _lvl.Offers[0] = longest;
            _panel.ForceRedraw();
            yield return Frames(2);
            Canvas.ForceUpdateCanvases();
            Debug.Log($"[U1] longest card: {longest.Def.id} ({most} chars with tags): {_panel.CardDescription(0)}");
            AssertNothingCut(_panel.CanvasRect, "the level-up with " + longest.Def.id);
            _lvl.Choose(1);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Death_HoldsTheCrimsonBanner_StatsOnWax()
        {
            yield return Boot();
            DevSettings.GodMode = false;
            var end = RunEndController.Instance;
            Player.Main.GetComponent<PlayerHealth>().Kill();
            yield return WaitRealtime(() => end.ScreenShown, 8f);
            Assert.IsTrue(end.ScreenShown, "the end screen shows");
            yield return Frames(2);
            var panel = Object.FindAnyObjectByType<RunEndPanel>();
            Assert.IsNotNull(panel);
            Assert.IsNotNull(panel.TitleBanner);
            Assert.IsTrue(panel.TitleBanner.Held, "YOU DIED drops and stays");
            Assert.AreEqual(BannerCloth.Crimson, panel.TitleBanner.Cloth);
            Assert.AreEqual("YOU DIED", panel.TitleBanner.Text);
            Assert.IsFalse(panel.TitleHasLaurels, "no laurels for a death");
            Assert.AreEqual((int)FrameKind.Wax, panel.StatsFrame, "the stats on a wax tablet");
            yield return new WaitForSecondsRealtime(VexillumBanner.TotalSeconds() + 0.5f);
            Assert.IsTrue(panel.TitleBanner.Visible && panel.TitleBanner.Held, "still there after a whole banner's time (the screen is paused: real time)");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Survived_GetsLaurels_NoBanner()
        {
            yield return Boot();
            var end = RunEndController.Instance;
            _run.End(RunOutcome.Survived);
            yield return WaitRealtime(() => end.ScreenShown, 8f);
            Assert.IsTrue(end.ScreenShown);
            yield return Frames(2);
            var panel = Object.FindAnyObjectByType<RunEndPanel>();
            Assert.IsTrue(panel.TitleHasLaurels, "SURVIVED! / VICTORY! between laurel branches");
            Assert.IsFalse(panel.TitleBanner.Visible, "no banner");
            StringAssert.IsMatch("^(SURVIVED!|VICTORY!)$", panel.TitleText);
            Assert.IsTrue(UiKit.HasRole(panel.TitleLabel, TextRole.Shout));
            Assert.AreEqual((int)FrameKind.Wax, panel.StatsFrame);
        }

        /// <summary>R7 lesson: the player bot run checks GC; here 5 s of fighting with a banner every 2 s is only logged.</summary>
        [UnityTest, Timeout(60000)]
        public IEnumerator Fighting_WithBanners_LogsTheHeapPerSystem()
        {
            yield return Boot();
            Player.Main.GetComponent<WeaponSystem>().enabled = true;
            var spawner = Object.FindAnyObjectByType<DebugEnemySpawner>();
            if (spawner != null) spawner.enabled = true;
            if (Director.Instance != null) Director.Instance.enabled = true;
            if (_lvl != null) _lvl.enabled = false; // no cards: the fight runs on
            yield return new WaitForSeconds(1f);
            GameLoop.TrackHeapPerSystem = true;
            float end = Time.time + 5f, next = 0f;
            bool swarm = true;
            while (Time.time < end)
            {
                if (Time.time >= next)
                {
                    GameEvents.OnDirector.Raise(new DirectorEvent { Kind = swarm ? DirectorEventKind.Swarm : DirectorEventKind.Elite, Color = Color.red });
                    swarm = !swarm;
                    next = Time.time + 2f;
                }
                yield return null;
            }
            Debug.Log("[U1] 5 s of fighting with a banner every 2 s, " + EnemyManager.Instance.AliveCount + " enemies; heap growth per system:\n" + GameLoop.HeapReport());
            GameLoop.TrackHeapPerSystem = false;
            Assert.IsTrue(_hud.Banner.Visible || _hud.PendingBanner != null || _hud.Banner.Phase == BannerPhase.Hidden, "the banner ran");
        }
    }
}
