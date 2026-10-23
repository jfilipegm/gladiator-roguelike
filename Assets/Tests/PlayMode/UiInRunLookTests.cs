using System;
using System.Collections;
using System.Collections.Generic;
using Gladiator.Core;
using Gladiator.Gameplay;
using Gladiator.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

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
    /// P3 (docs/tracks/P3-contract.md, P3-B): the HUD's labels and bars inside their plates' ContentRects, each icon
    /// centred on its trough or number, the level plate's LEVEL word, the 21-tall XP bar; the card labels inside the card
    /// frame's ContentRect; the run end's rows inside the tablet's; a banner never rotates and a second one's drop and
    /// wave allocate nothing per frame.
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
    
        // ---------- P3 ----------

        static Rect World(RectTransform rt)
        {
            var c = new Vector3[4];
            rt.GetWorldCorners(c);
            return Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y);
        }

        static void AssertInside(Rect outer, Rect inner, float tolerance, string what)
        {
            Assert.GreaterOrEqual(inner.xMin, outer.xMin - tolerance, what + ": left");
            Assert.LessOrEqual(inner.xMax, outer.xMax + tolerance, what + ": right");
            Assert.GreaterOrEqual(inner.yMin, outer.yMin - tolerance, what + ": bottom");
            Assert.LessOrEqual(inner.yMax, outer.yMax + tolerance, what + ": top");
        }

        /// <summary>Notes 13, 14, 18: the HUD's labels and bars sit inside their plates' ContentRects, the HP trough as far
        /// from the plate's top as the last row from its bottom, each icon centred on its trough or number, the level plate
        /// reads LEVEL, the XP bar is 21 tall, and the icons are the new 16 px art.</summary>
        [UnityTest, Timeout(60000)]
        public IEnumerator Hud_LabelsInsideTheirPlates_IconsCentred_LevelWord()
        {
            yield return Boot();
            _run.AddGold(1234);
            yield return Frames(2);
            Canvas.ForceUpdateCanvases();
            float unit = _hud.CanvasRect.lossyScale.x; // screen pixels per canvas unit
            float tol = unit + 0.5f;
            var status = Hud.ContentWorldRect(_hud.StatusPlateRect, FrameKind.Bronze);
            var level = Hud.ContentWorldRect(_hud.LevelPlateRect, FrameKind.Bronze);
            AssertInside(status, World(_hud.HpBarRect), tol, "the HP trough");
            AssertInside(status, World(_hud.HpIconRect), tol, "the heart");
            AssertInside(status, Hud.TextWorldRect(_hud.GoldLabel), tol, "the gold");
            AssertInside(status, Hud.TextWorldRect(_hud.KillsLabel), tol, "the kills");
            AssertInside(status, World(_hud.GoldIconRect), tol, "the aureus");
            AssertInside(status, World(_hud.KillIconRect), tol, "the skull");
            AssertInside(level, Hud.TextWorldRect(_hud.LevelLabel), tol, "the level");
            AssertInside(level, World(_hud.FavorBarRect), tol, "the Favor trough");
            AssertInside(level, World(_hud.FavorIconRect), tol, "the laurel");
            var plate = World(_hud.StatusPlateRect);
            float topGap = plate.yMax - World(_hud.HpBarRect).yMax;
            float lowest = Mathf.Min(World(_hud.GoldIconRect).yMin, Hud.TextWorldRect(_hud.GoldLabel).yMin);
            Debug.Log($"[P3] HUD at {Screen.width}x{Screen.height}: HP trough {topGap:0.#} px under the plate's top, the gold row's lowest pixel {lowest - plate.yMin:0.#} px over its bottom");
            Assert.AreEqual(status.yMax, World(_hud.HpBarRect).yMax, tol, "the HP trough starts at the ContentRect's top: the same inset as at its bottom");
            Assert.LessOrEqual(lowest - status.yMin, 12f * unit, "and the last row ends near the ContentRect's bottom (no empty band)");
            // icons on their trough's or number's centre line (±1 unit)
            Assert.AreEqual(World(_hud.HpBarRect).center.y, World(_hud.HpIconRect).center.y, tol, "the heart on the HP trough's centre line");
            Assert.AreEqual(Hud.TextWorldRect(_hud.GoldLabel).center.y, World(_hud.GoldIconRect).center.y, tol, "the aureus on the gold's");
            Assert.AreEqual(Hud.TextWorldRect(_hud.KillsLabel).center.y, World(_hud.KillIconRect).center.y, tol, "the skull on the kills'");
            Assert.AreEqual(new Vector2(32f, 32f), _hud.HpIconRect.rect.size, "32×32: 2 units per art pixel");
            Assert.AreEqual((int)FrameKind.IconHeart, _hud.HpIconFrame);
            Assert.AreEqual((int)FrameKind.IconAureus, _hud.GoldIconFrame);
            Assert.AreEqual((int)FrameKind.IconSkullGalea, _hud.KillIconFrame);
            Assert.AreEqual((int)FrameKind.IconAureus, _hud.PriceTagIconFrame, "the price tags' coin too");
            Assert.AreEqual(21f, _hud.XpBarRect.rect.height, 0.01f, "the XP bar 18 → 21");
            StringAssert.StartsWith(Hud.LevelWord + " ", _hud.LevelText, "the level plate's word");
            Assert.IsTrue(UiKit.HasRole(_hud.LevelLabel, TextRole.Number));
        }

        /// <summary>
        /// The producer's note 21 (after P3): both top plates are one height, so the level-up's two columns hang the same
        /// gap under them; the Nobody's status plate has no empty meter row (11 shorter than P3's 171); the Blood Knight's
        /// meter row sits between the HP trough and the gold row, inside the plate's ContentRect.
        /// </summary>
        [UnityTest, Timeout(90000)]
        public IEnumerator Hud_BothTopPlatesOneHeight_TheMeterRowOnlyWithAMeter()
        {
            yield return Boot();
            yield return Frames(2);
            Canvas.ForceUpdateCanvases();
            float tol = _hud.CanvasRect.lossyScale.x + 0.5f;
            Assert.AreEqual(_hud.LevelPlateRect.rect.height, _hud.StatusPlateRect.rect.height, 0.01f, "one height");
            Assert.AreEqual(World(_hud.LevelPlateRect).yMin, World(_hud.StatusPlateRect).yMin, tol, "one bottom line");
            Assert.LessOrEqual(_hud.StatusPlateRect.rect.height, 160.5f, "the status plate lost its empty meter row");
            Assert.IsFalse(_hud.BloodBarRect.gameObject.activeSelf, "the Nobody has no meter");
            try
            {
                GladiatorLoadout.NextOverride = UnityEditor.AssetDatabase.LoadAssetAtPath<GladiatorDef>("Assets/Data/Gladiators/GL_BloodKnight.asset");
                yield return Boot();
                yield return Frames(3);
                Canvas.ForceUpdateCanvases();
                Assert.IsTrue(_hud.BloodBarRect.gameObject.activeSelf, "the Blood Knight's meter shows");
                Player.Main.GetComponent<PlayerHealth>().Spend(60f); // half his HP: Bloodlust's "+n% DMG" label shows
                yield return Frames(3);
                Canvas.ForceUpdateCanvases();
                StringAssert.EndsWith("% DMG", _hud.BloodLabel, "the meter's label has words to measure");
                Assert.AreEqual(_hud.LevelPlateRect.rect.height, _hud.StatusPlateRect.rect.height, 0.01f, "still one height");
                var status = Hud.ContentWorldRect(_hud.StatusPlateRect, FrameKind.Bronze);
                var meter = World(_hud.BloodBarRect);
                AssertInside(status, meter, tol, "the meter's trough");
                var label = Hud.TextWorldRect(_hud.BloodLabelText);
                AssertInside(status, label, tol, "the meter's label");
                float goldTop = Mathf.Max(World(_hud.GoldIconRect).yMax, Hud.TextWorldRect(_hud.GoldLabel).yMax);
                Assert.Less(meter.yMax, World(_hud.HpBarRect).yMin, "the meter under the HP trough");
                Assert.Greater(meter.yMin, goldTop, "and over the gold row");
                Assert.Greater(label.yMin, goldTop, "its label too");
            }
            finally { GladiatorLoadout.NextOverride = null; }
        }

        /// <summary>The padding rule on the cards: every label's glyphs inside the card frame's ContentRect.</summary>
        [UnityTest, Timeout(60000)]
        public IEnumerator Cards_EveryLabelInsideTheFramesContentRect()
        {
            yield return Boot();
            yield return OpenCards();
            Canvas.ForceUpdateCanvases();
            float tol = _panel.CanvasRect.lossyScale.x + 0.5f;
            for (int i = 0; i < _lvl.ChoiceCount; i++)
            {
                var content = Hud.ContentWorldRect(_panel.CardRect(i), FrameKind.CardStone);
                foreach (var label in new[] { _panel.CardNameLabel(i), _panel.CardDescriptionLabel(i), _panel.CardKeyLabel(i) })
                {
                    var r = Hud.TextWorldRect(label);
                    if (r.width <= 0f) continue;
                    AssertInside(content, r, tol, "card " + i + " " + label.name);
                }
            }
            Assert.IsTrue(_panel.Inventory.Visible, "P3: the inventory shows with the cards");
            _lvl.Choose(0);
        }

        /// <summary>The run end's seven rows inside the wax tablet's ContentRect.</summary>
        [UnityTest, Timeout(60000)]
        public IEnumerator RunEnd_RowsInsideTheTablet()
        {
            yield return Boot();
            var end = RunEndController.Instance;
            _run.End(RunOutcome.Survived);
            yield return WaitRealtime(() => end.ScreenShown, 8f);
            Assert.IsTrue(end.ScreenShown);
            yield return Frames(2);
            var panel = Object.FindAnyObjectByType<RunEndPanel>();
            Canvas.ForceUpdateCanvases();
            float tol = panel.CanvasRect.lossyScale.x + 0.5f;
            var content = Hud.ContentWorldRect(panel.StatsRect, FrameKind.Wax);
            int checkedRows = 0;
            foreach (var t in panel.StatsRect.GetComponentsInChildren<Transform>())
            {
                var label = t.GetComponent("TextMeshProUGUI");
                if (label == null) continue;
                var r = Hud.TextWorldRect(label);
                if (r.width <= 0f) continue;
                AssertInside(content, r, tol, t.name);
                checkedRows++;
            }
            Assert.GreaterOrEqual(checkedRows, 14, "seven names and seven values");
        }

        /// <summary>Note 16: a banner never rotates; after the first banner has grown every buffer, a second banner's
        /// drop and wave allocate nothing per frame (its own canvas, ticked and rebuilt here, so nothing else counts).</summary>
        [UnityTest, Timeout(60000)]
        public IEnumerator Banner_ASecondDrop_AllocatesNothingPerFrame_AndNeverRotates()
        {
            var canvas = UiFactory.Canvas("P3BannerCanvas", null, 0);
            var b = new VexillumBanner(canvas.transform, new Vector2(0.5f, 1f), new Vector2(0f, -207f));
            yield return null;
            const float dt = 1f / 60f;
            b.Show("SWARM INCOMING!", BannerCloth.Crimson);
            for (int i = 0; i < 600 && b.Visible; i++)
            {
                b.Tick(dt);
                Canvas.ForceUpdateCanvases();
                Assert.AreEqual(0f, b.SwayDegrees, "no rotation");
            }
            Assert.IsFalse(b.Visible, "the first banner ran its course");

            long start = GC.GetAllocatedBytesForCurrentThread();
            b.Show("AN ELITE ENTERS!", BannerCloth.Crimson); // new words: laid out once, on the next tick
            b.Tick(dt);
            Canvas.ForceUpdateCanvases();
            long showing = GC.GetAllocatedBytesForCurrentThread() - start;
            float peak = 0f;
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 600 && b.Visible; i++)
            {
                b.Tick(dt);
                Canvas.ForceUpdateCanvases();
                peak = Mathf.Max(peak, b.WaveAmplitude);
            }
            long perFrames = GC.GetAllocatedBytesForCurrentThread() - before;
            Debug.Log($"[P3] the second banner: {showing} B to show it and lay its words out, then {perFrames} B over its drop, wave, hold and roll (wave peak {peak:0.#} units)");
            Assert.IsFalse(b.Visible);
            Assert.Greater(peak, 0f, "it waved");
            Assert.AreEqual(0, perFrames, "the drop and the wave allocated");
            Object.Destroy(canvas.gameObject);
        }
}
}
