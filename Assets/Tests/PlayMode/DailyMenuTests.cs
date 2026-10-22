using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Gladiator.Core;
using Gladiator.Gameplay;
using Gladiator.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Gladiator.Tests
{
    /// <summary>
    /// R8-C (docs/tracks/R7-contract.md, "R8-C daily screens"): the Daily Games on the real main menu and in a run. DAILY
    /// is greyed with its hint until Gorm falls, then opens a screen proclaiming today's plan (an earned arena and
    /// gladiator, two edicts, the Renown, SCORED ATTEMPT, the clock); FIGHT! starts that plan (gladiator, run, edicts,
    /// seed) and marks the day's attempt at once, so the next opening says PRACTICE and spends nothing; the ledger lists
    /// the save's records newest first. In the test arena: a normal run's end screen shows its seed and SAME SEED keeps it
    /// (RETRY draws a fresh one); a daily shows the HUD chip, the pause page shows the seed, and the end screen reads
    /// "DAILY 22 OCT · SCORED" with ×1.5 in the Renown row and PRACTICE in place of RETRY / SAME SEED.
    /// GameSession.SuppressLoad keeps FIGHT! / RETRY from loading scenes (they record GameSession.LastLoadPath). Runs on
    /// the data DailySetup and EdictSetup make (Gladiator/Setup/Meta/Create Daily Data); the save is the sandbox's.
    /// LogAssert.NoUnexpectedReceived closes only the tests that start, save and unlock nothing (there it catches TMP
    /// warnings: an Ellipsis, a missing glyph).
    /// </summary>
    public class DailyMenuTests
    {
        const string Arena = "Assets/Scenes/Dev/TestArena.unity";
        const string SlavePit = "Assets/Data/Runs/RD_Slice.asset";
        const string Nobody = "Assets/Data/Gladiators/GL_Nobody.asset";
        const uint SeedA = 0x7F3A91C2u;

        MainMenu _menu;

        [SetUp]
        public void SetUp() => GameSession.SuppressLoad = true; // R8-STUB(b)

        [TearDown]
        public void TearDown()
        {
            if (Player.Main != null) Player.Main.SetPaused(false);
            GameSession.SuppressLoad = false; // R8-STUB(b)
            RunModifiers.Clear();
            GameSession.Clear();
            DevSettings.ResetAll();
            TimeController.GameSpeed = 1f;
            TimeController.ResetForNewRun();
            SaveSandbox.Wipe(); // later tests expect a fresh save
        }

        static MetaConfig Meta
        {
            get
            {
                var m = AssetDatabase.LoadAssetAtPath<MetaConfig>("Assets/Data/Meta/MetaConfig.asset");
                Assert.IsNotNull(m, "MetaConfig");
                Assert.IsNotNull(m.dailyUnlock, "MetaConfig.dailyUnlock (run Gladiator/Setup/Meta/Create Daily Data)"); // R8-STUB(b)
                return m;
            }
        }

        static RunModifierDef Edict(string id)
        {
            foreach (var e in Meta.edicts) if (e != null && e.id == id) return e;
            Assert.Fail("no edict " + id + " in MetaConfig.edicts");
            return null;
        }

        /// <summary>The Daily Games earned: Gorm has fallen (UL_Daily is in the save).</summary>
        static void EarnDaily(SaveData d) => d.unlocked.Add(Meta.dailyUnlock.id); // R8-STUB(b)

        static int Today => DailyGames.Today(); // R8-STUB(b)
        static int DaysAgo(int n) => DailyGames.DateKey(DateTime.UtcNow.Date.AddDays(-n)); // R8-STUB(b)

        IEnumerator Boot(Action<SaveData> stage = null)
        {
            SaveSandbox.Wipe();
            stage?.Invoke(MetaProgress.Data);
            SaveSystem.Save(MetaProgress.Data);
            var op = SceneManager.LoadSceneAsync(GameSession.MenuScene, LoadSceneMode.Single);
            while (!op.isDone) yield return null;
            yield return null;
            _menu = Object.FindAnyObjectByType<MainMenu>();
            Assert.IsNotNull(_menu);
            Assert.IsNotNull(_menu.Daily, "the menu built its Daily screen");
        }

        IEnumerator OpenDaily()
        {
            _menu.OpenDaily();
            yield return null;
            yield return null; // the canvas scaler has scaled the canvas
            Canvas.ForceUpdateCanvases();
        }

        RectTransform MenuPart(string path)
        {
            var t = _menu.transform.Find("MenuCanvas/" + path) as RectTransform;
            Assert.IsNotNull(t, path);
            return t;
        }

        static SaveData SavedFile() => JsonUtility.FromJson<SaveData>(File.ReadAllText(SaveSandbox.TestPath));

        static Rect ScreenRect(RectTransform rt)
        {
            var c = new Vector3[4];
            rt.GetWorldCorners(c); // a screen-space overlay canvas: world = screen pixels
            return Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y);
        }

        static void AssertOnScreen(RectTransform rt, string what)
        {
            var r = ScreenRect(rt);
            Assert.GreaterOrEqual(r.xMin, -0.5f, what + " is off the left edge");
            Assert.GreaterOrEqual(r.yMin, -0.5f, what + " is off the bottom");
            Assert.LessOrEqual(r.xMax, Screen.width + 0.5f, what + " is off the right edge");
            Assert.LessOrEqual(r.yMax, Screen.height + 0.5f, what + " is off the top");
        }

        /// <summary>No text under `root` is cut off (autosize shrinks first; Truncate is the last resort).</summary>
        static void AssertNothingTruncated(RectTransform root, string what)
        {
            Assert.IsNotNull(root, what);
            string cut = DailyPanel.FirstTruncated(root);
            Assert.IsNull(cut, what + ": a text is cut off (" + cut + ")");
        }

        /// <summary>Clicks at `target`'s centre through the EventSystem, as the mouse would; asserts nothing covers it.</summary>
        static void Click(RectTransform target)
        {
            var es = EventSystem.current;
            Assert.IsNotNull(es, "the menu has an EventSystem");
            var ped = new PointerEventData(es) { position = ScreenRect(target).center, button = PointerEventData.InputButton.Left };
            var hits = new List<RaycastResult>();
            es.RaycastAll(ped, hits);
            Assert.Greater(hits.Count, 0, target.name + " takes the pointer");
            var handler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
            Assert.AreSame(target.gameObject, handler, target.name + " is the top hit (nothing covers it)");
            ExecuteEvents.Execute(handler, ped, ExecuteEvents.pointerClickHandler);
        }

        static IEnumerator WaitRealtime(Func<bool> done, float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!done() && Time.realtimeSinceStartup < end) yield return null;
        }

        // ------------------------------------------------------------------ words (pure)

        [Test]
        public void Words_Dates_TheClock_TheSeedLine_AndTheRenownBreakdown()
        {
            Assert.AreEqual("22 OCT", DailyPanel.DateShort(20261022));
            Assert.AreEqual("1 JAN 2027", DailyPanel.DateLong(20270101));
            Assert.AreEqual("THE DAILY GAMES — 22 OCT 2026", DailyPanel.Header(20261022));
            Assert.AreEqual(DailyGames.DateText(20261022), DailyPanel.DateLong(20261022), "the menu's dates read as the daily core's"); // R8-STUB(b)
            Assert.AreEqual("5 H 12 M", DailyPanel.Countdown(new TimeSpan(5, 12, 40)));
            Assert.AreEqual("12 M", DailyPanel.Countdown(new TimeSpan(0, 12, 0)));
            var lastHalfMinute = new DateTime(2026, 10, 22, 23, 59, 30, DateTimeKind.Utc);
            Assert.AreEqual("LESS THAN A MINUTE", DailyPanel.Countdown(DailyGames.UntilNext(lastHalfMinute))); // R8-STUB(b)
            Assert.AreEqual("8:00", DailyPanel.Clock(480f));
            Assert.AreEqual("12:07", DailyPanel.Clock(727.9f));

            Assert.AreEqual("SEED " + GameRandom.SeedText(SeedA), RunEndPanel.SeedLine(false, 0, false, SeedA)); // R8-STUB(a)
            Assert.AreEqual("SEED 7F3A-91C2", RunEndPanel.SeedLine(false, 0, false, SeedA), "the spec's 8 hex digits");
            Assert.AreEqual("DAILY 22 OCT · SCORED", RunEndPanel.SeedLine(true, 20261022, true, 0u));
            Assert.AreEqual("DAILY 22 OCT · PRACTICE", RunEndPanel.SeedLine(true, 20261022, false, 0u));
            foreach (uint s in new[] { 0u, 1u, SeedA, 0xFFFFFFFFu, 0x0000ABCDu })
            {
                var t = new CharBuffer(32);
                StatsPanel.AppendSeed(t, s);
                Assert.AreEqual("SEED " + GameRandom.SeedText(s), t.ToString(), "the pause page writes the seed as GameRandom does"); // R8-STUB(a)
            }

            var daily = new RunRewards { BaseRenown = 124, EdictBonus = 0.45f, DailyBonus = 0.5f, Renown = 270, Bounty = 150, RenownTotal = 2410 }; // R8-STUB(b)
            Assert.AreEqual("+124 ×1.45 ×1.5 daily = +270 +150  (2410)", RunEndPanel.RenownLine(daily));
            var dailyOnly = new RunRewards { BaseRenown = 100, DailyBonus = 0.5f, Renown = 150, RenownTotal = 900 }; // R8-STUB(b)
            Assert.AreEqual("+100 ×1.5 daily = +150  (900)", RunEndPanel.RenownLine(dailyOnly));
            var plain = new RunRewards { BaseRenown = 100, Renown = 100, RenownTotal = 900 };
            Assert.AreEqual("+100  (900)", RunEndPanel.RenownLine(plain), "a normal run's line is unchanged");
        }

        // ------------------------------------------------------------------ the menu

        [UnityTest, Timeout(60000)]
        public IEnumerator BeforeGorm_DailyIsGreyedWithItsHint_AndWontOpen()
        {
            yield return Boot();
            Assert.IsFalse(_menu.DailyButtonOpen, "the Daily Games wait for Gorm");
            Assert.IsFalse(_menu.DailyDot);
            Assert.AreEqual("DAILY", _menu.DailyButtonText);
            Assert.IsNotEmpty(_menu.DailyHintText, "the greyed button says how to open it");
            var hint = Meta.dailyUnlock.hint; // R8-STUB(b)
            if (!string.IsNullOrEmpty(hint) && hint != "???") Assert.AreEqual(hint, _menu.DailyHintText, "its unlock's hint");
            _menu.OpenDaily();
            Assert.IsFalse(_menu.DailyShown, "no Daily screen before it's earned");

            // PLAY · DAILY · CODEX · OPTIONS · QUIT, top to bottom, all on screen
            yield return null;
            Canvas.ForceUpdateCanvases();
            float above = float.MaxValue;
            foreach (var name in new[] { "Play", "Daily", "Codex", "Options", "Quit" })
            {
                var b = MenuPart("Menu/" + name);
                AssertOnScreen(b, name);
                float y = ScreenRect(b).center.y;
                Assert.Less(y, above, name + " sits under the button before it");
                above = y;
            }
            AssertOnScreen(MenuPart("Menu/DailyHint"), "the hint");
            AssertNothingTruncated(MenuPart("Menu/DailyHint"), "the hint");
            Assert.IsFalse(_menu.CodexButtonText.Contains("DAILY"), "the CODEX button is still the CODEX button");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Earned_TheScreenProclaimsTodaysPlan()
        {
            yield return Boot(EarnDaily);
            var meta = Meta;
            Assert.IsTrue(_menu.DailyButtonOpen);
            Assert.IsTrue(_menu.DailyDot, "today's scored attempt is open: the gold dot");
            StringAssert.Contains("•", _menu.DailyButtonText);
            Assert.AreEqual("", _menu.DailyHintText, "no hint once open");

            Click(MenuPart("Menu/Daily")); // the mouse opens it
            yield return null;
            yield return null;
            Canvas.ForceUpdateCanvases();
            Assert.IsTrue(_menu.DailyShown, "a click on DAILY opens the Daily screen");
            var p = _menu.Daily;
            var plan = _menu.TodayPlan; // R8-STUB(b)
            Assert.IsTrue(plan.IsValid, "a fresh save still has an arena and a gladiator");
            Assert.AreEqual(Today, plan.Date, "today, UTC");
            Assert.IsTrue(MainMenu.IsAvailable(plan.Run), "the arena is one the save has earned");
            Assert.IsTrue(MainMenu.IsAvailable(plan.Gladiator), "so is the gladiator");
            Assert.AreEqual("THE DAILY GAMES — " + DailyPanel.DateLong(plan.Date), _menu.DailyHeaderText);
            Assert.AreEqual(plan.Run.DisplayName.ToUpperInvariant(), p.ArenaText);
            Assert.AreEqual(plan.Gladiator.displayName.ToUpperInvariant(), p.GladiatorText);

            Assert.AreEqual(meta.dailyEdicts, plan.Edicts.Length, "two edicts"); // R8-STUB(b)
            Assert.AreEqual(plan.Edicts.Length, p.EdictCount);
            Assert.AreNotSame(plan.Edicts[0], plan.Edicts[1], "two different edicts");
            for (int i = 0; i < plan.Edicts.Length; i++) Assert.AreEqual(plan.Edicts[i].displayName, p.EdictName(i));

            Assert.IsTrue(p.Scored);
            Assert.AreEqual("SCORED ATTEMPT", p.StatusText);
            StringAssert.Contains("RENOWN " + EdictsPanel.MulText(1f + meta.dailyRenownBonus), p.RenownText); // R8-STUB(b)
            StringAssert.Contains("EDICTS " + EdictsPanel.MulText(1f + RunModifiers.Bonus(plan.Edicts)), p.RenownText);
            StringAssert.DoesNotContain("<s>", p.RenownText, "the ×1.5 pays on the scored attempt");
            StringAssert.StartsWith("NEXT DAILY IN ", p.NextText);
            Assert.AreEqual(0, p.LedgerRows, "a fresh save has no ledger");
            Assert.IsTrue(p.Tick(DateTime.UtcNow.AddDays(1)), "the clock notices a new day");
            p.Tick(DateTime.UtcNow);

            // the layout: both blocks, the edicts and the buttons on screen, no text cut off
            AssertOnScreen(p.Box, "the Daily screen");
            AssertOnScreen(p.EdictsRect, "the edicts block");
            AssertOnScreen(MenuPart("Daily/Fight"), "FIGHT!");
            AssertOnScreen(MenuPart("Daily/Back"), "BACK");
            Assert.Less(ScreenRect(p.Box).yMax, ScreenRect(MenuPart("Daily/Keys")).yMin + 0.5f, "the blocks sit under the key line");
            Assert.Greater(ScreenRect(p.Box).yMin, ScreenRect(MenuPart("Daily/Fight")).yMax - 0.5f, "and above the buttons");
            AssertNothingTruncated(p.Box, "the Daily screen");
            AssertNothingTruncated(MenuPart("Daily/Header"), "the header");

            // the edicts block opens the Edicts page read-only on the day's two; Esc closes it back to the Daily screen
            Click(p.EdictsRect);
            Assert.IsTrue(_menu.Edicts.IsOpen, "a click on the edicts opens the page");
            Assert.IsTrue(_menu.Edicts.ReadOnly, "read-only: the daily's edicts are fixed");
            CollectionAssert.AreEquivalent(plan.Edicts, _menu.Edicts.Chosen);
            Assert.IsTrue(_menu.Edicts.HandleInput(false, false, false, false, false, true, false));
            Assert.IsFalse(_menu.Edicts.IsOpen);
            Assert.IsTrue(_menu.DailyShown, "back on the Daily screen");
            CollectionAssert.IsEmpty(MetaProgress.Data.edictsChosen, "the day's edicts aren't the player's choice");

            Click(MenuPart("Daily/Back"));
            Assert.IsFalse(_menu.DailyShown, "BACK returns to the main menu");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Fight_StartsThePlan_MarksTheAttempt_AndTheNextIsPractice()
        {
            yield return Boot(EarnDaily);
            yield return OpenDaily();
            var plan = _menu.TodayPlan; // R8-STUB(b)
            Assert.IsTrue(_menu.DailyFightEnabled);
            Click(MenuPart("Daily/Fight"));

            Assert.IsTrue(GameSession.HasDaily, "FIGHT! started the daily"); // R8-STUB(b)
            Assert.IsTrue(GameSession.DailyScored, "the day's first attempt is scored"); // R8-STUB(b)
            Assert.AreEqual(plan.Date, GameSession.Daily.Date); // R8-STUB(b)
            Assert.AreEqual(plan.Seed, GameSession.Seed, "the day's seed"); // R8-STUB(b)
            Assert.AreSame(plan.Gladiator, GameSession.Gladiator, "the day's gladiator, no select screen");
            Assert.AreSame(plan.Run, GameSession.Run, "the day's arena, no arena screen");
            CollectionAssert.AreEqual(plan.Edicts, RunModifiers.Active, "under the day's edicts");
            Assert.AreEqual(plan.Run.arenas[0].scenePath, GameSession.LastLoadPath, "its first arena would load"); // R8-STUB(b)
            Assert.IsTrue(MetaProgress.DailyAttempted(plan.Date), "the attempt is marked at once"); // R8-STUB(b)
            Assert.AreEqual(plan.Date, SavedFile().dailyAttempt, "and saved at once (quitting doesn't give another)"); // R8-STUB(b)
            Assert.IsFalse(_menu.DailyDot, "no gold dot once the attempt is spent");
            StringAssert.DoesNotContain("•", _menu.DailyButtonText);

            // opening it again: practice, the scored attempt unfinished; FIGHT! spends nothing more
            yield return OpenDaily();
            var p = _menu.Daily;
            Assert.IsFalse(p.Scored);
            StringAssert.StartsWith("PRACTICE", p.StatusText);
            StringAssert.Contains("<s>", p.RenownText, "practice: the daily's ×1.5 struck through");
            AssertNothingTruncated(p.Box, "the practice screen");
            _menu.FightDaily();
            Assert.IsTrue(GameSession.HasDaily); // R8-STUB(b)
            Assert.IsFalse(GameSession.DailyScored, "a second attempt is practice"); // R8-STUB(b)
            Assert.AreEqual(plan.Seed, GameSession.Seed, "practice replays the day's seed"); // R8-STUB(b)
            var saved = SavedFile();
            Assert.AreEqual(plan.Date, saved.dailyAttempt); // R8-STUB(b)
            Assert.AreEqual(0, saved.dailiesPlayed, "nothing counted before a run ends"); // R8-STUB(b)
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator TheLedger_ShowsTheSavesRecords_NewestFirst_AndTodaysResult()
        {
            int today = Today;
            yield return Boot(d =>
            {
                EarnDaily(d);
                d.dailies ??= new List<DailyRecord>(); // R8-STUB(b)
                for (int k = 8; k >= 1; k--) // oldest first, as the save keeps them
                    d.dailies.Add(new DailyRecord { date = DaysAgo(k), run = k == 3 ? "old_arena" : "slice", gladiator = "nobody", edicts = "iron_hides|blood_sport", outcome = (int)RunOutcome.Died, elapsed = 60f * k + 7f, kills = 100 * k, renown = 10 * k, seed = k });
                d.dailies.Add(new DailyRecord { date = today, run = "slice", gladiator = "nobody", edicts = "iron_hides|blood_sport", outcome = (int)RunOutcome.Survived, elapsed = 480f, kills = 1904, renown = 412, seed = SeedA });
                d.dailyAttempt = today; // R8-STUB(b)
                d.dailiesPlayed = 9; // R8-STUB(b)
                d.dailiesWon = 1; // R8-STUB(b)
            });
            Assert.IsFalse(_menu.DailyDot, "today's attempt is spent");
            yield return OpenDaily();
            var p = _menu.Daily;
            var nobody = AssetDatabase.LoadAssetAtPath<GladiatorDef>(Nobody);
            var pit = AssetDatabase.LoadAssetAtPath<RunDef>(SlavePit);
            Assert.AreEqual(DailyPanel.LedgerShown, p.LedgerRows, "the last seven of nine");
            string top = p.LedgerText(0);
            StringAssert.Contains(DailyPanel.DateShort(today), top, "newest first: today's on top");
            StringAssert.Contains("VICTORY", top);
            StringAssert.Contains("8:00", top);
            StringAssert.Contains("1,904 kills", top);
            StringAssert.Contains("+412", top);
            StringAssert.Contains(nobody.displayName, top, "the gladiator by name");
            StringAssert.Contains(pit.DisplayName, top, "the arena by name");
            Assert.IsTrue(p.LedgerIsToday(0), "today's line is framed");
            Assert.IsFalse(p.LedgerIsToday(1));
            StringAssert.Contains(DailyPanel.DateShort(DaysAgo(1)), p.LedgerText(1));
            StringAssert.Contains("DIED", p.LedgerText(1));
            StringAssert.Contains("Old Arena", p.LedgerText(3), "an id no data knows reads as words");
            StringAssert.Contains(DailyPanel.DateShort(DaysAgo(6)), p.LedgerText(6), "the seventh line is six days ago");
            for (int i = 0; i < p.LedgerRows; i++) AssertOnScreen(p.LedgerRect(i), "ledger line " + i);
            AssertNothingTruncated(p.Box, "the ledger");

            Assert.IsFalse(p.Scored);
            Assert.AreEqual("PRACTICE · scored: VICTORY 8:00, 1,904 kills, +412", p.StatusText, "practice names the scored result");

            // the Codex's RECORDS count them
            _menu.OpenCodex();
            _menu.Codex.Select(CodexPanel.Tab.Records);
            yield return null;
            Assert.AreEqual("9", _menu.Codex.RecordValue("Dailies played"));
            Assert.AreEqual("1", _menu.Codex.RecordValue("Dailies won"));
            LogAssert.NoUnexpectedReceived();
        }

        // ------------------------------------------------------------------ the run

        /// <summary>Loads the test arena seeded with `seed` (GameSession has already been started: suppressed, it loaded
        /// nothing). RunModifiers stay as the session set them.</summary>
        IEnumerator BootArena(uint seed)
        {
            DevSettings.ResetAll();
            TimeController.GameSpeed = 1f;
            TimeController.ResetForNewRun();
            GameRandom.BeginRun(seed); // R8-STUB(a): what GameSession.Load does before a real load
            var op = SceneManager.LoadSceneAsync(Arena, LoadSceneMode.Single);
            while (!op.isDone) yield return null;
            yield return null;
            yield return null; // every Start has run
            Assert.IsNotNull(Player.Main);
            Player.Main.OverrideSource = new ScriptedInput();
        }

        /// <summary>Ends the run and waits for the end screen (its panel has filled).</summary>
        static IEnumerator EndRun()
        {
            var end = RunEndController.Instance;
            Assert.IsNotNull(end);
            RunState.Current.End(RunOutcome.Died);
            yield return WaitRealtime(() => end.ScreenShown, 8f);
            Assert.IsTrue(end.ScreenShown, "the end screen shows");
            yield return null;
            yield return null; // the panel's tick has filled it
            Assert.IsNotNull(end.Rewards, "the run was committed (the test arena has a MetaConfig)");
        }

        static void Press(RectTransform button)
        {
            Assert.IsNotNull(button);
            Assert.IsTrue(button.gameObject.activeInHierarchy, button.name + " shows");
            button.GetComponent<Button>().onClick.Invoke();
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator ANormalRun_ShowsItsSeed_SameSeedKeepsIt_AndRetryDrawsAFreshOne()
        {
            SaveSandbox.Wipe();
            RunModifiers.Clear();
            var nobody = AssetDatabase.LoadAssetAtPath<GladiatorDef>(Nobody);
            var pit = AssetDatabase.LoadAssetAtPath<RunDef>(SlavePit);
            GameSession.StartRun(nobody, pit, SeedA); // R8-STUB(b): suppressed, it only records
            Assert.AreEqual(SeedA, GameSession.Seed); // R8-STUB(b)
            Assert.IsFalse(GameSession.HasDaily); // R8-STUB(b)
            yield return BootArena(SeedA);

            var hud = Object.FindAnyObjectByType<Hud>();
            Assert.IsNotNull(hud);
            Assert.IsFalse(hud.DailyChipShown, "no daily, no chip");

            // the pause page's header names the seed
            var page = StatsPanel.Instance;
            Assert.IsNotNull(page);
            Player.Main.SetPaused(true);
            yield return null;
            yield return null;
            Assert.AreEqual(StatsPanel.Mode.Pause, page.Current);
            StringAssert.Contains("SEED " + GameRandom.SeedText(SeedA), page.Text, "the pause page shows the seed"); // R8-STUB(a)
            Player.Main.SetPaused(false);
            yield return null;

            yield return EndRun();
            var panel = Object.FindAnyObjectByType<RunEndPanel>();
            Assert.IsNotNull(panel);
            Assert.AreEqual("SEED " + GameRandom.SeedText(SeedA), panel.SeedText, "the end screen names the seed"); // R8-STUB(a)
            StringAssert.Contains(panel.SeedText, panel.NoteText);
            Assert.IsTrue(panel.SameSeedShown, "SAME SEED beside RETRY");
            Assert.AreEqual("Retry  [R]", panel.RetryText);
            AssertOnScreen(panel.RetryRect, "RETRY");
            AssertOnScreen(panel.SameSeedRect, "SAME SEED");
            Assert.Less(ScreenRect(panel.RetryRect).xMax, ScreenRect(panel.SameSeedRect).xMin, "the buttons don't overlap");
            StringAssert.DoesNotContain("daily", panel.RenownText, "no daily bonus in a normal run");
            AssertNothingTruncated(panel.SameSeedRect, "SAME SEED");

            string arenaPath = SceneManager.GetActiveScene().path;
            Press(panel.SameSeedRect);
            yield return null;
            Assert.AreEqual(SeedA, GameSession.Seed, "SAME SEED replays this seed"); // R8-STUB(b)
            Assert.IsFalse(GameSession.HasDaily); // R8-STUB(b)
            Assert.IsTrue(panel != null, "SuppressLoad also holds RETRY's reload (the scene is still this one)");
            Assert.AreEqual(arenaPath, GameSession.LastLoadPath, "the reload it recorded: this arena again"); // R8-STUB(b)

            Press(panel.RetryRect);
            yield return null;
            Assert.AreNotEqual(SeedA, GameSession.Seed, "RETRY is a new run: a fresh seed"); // R8-STUB(b)
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator AScoredDaily_ShowsTheChip_TheDailyLine_TheBonus_AndOnlyPractice()
        {
            SaveSandbox.Wipe();
            var plan = new DailyPlan // R8-STUB(b)
            {
                Date = Today,
                Seed = SeedA,
                Run = AssetDatabase.LoadAssetAtPath<RunDef>(SlavePit),
                Gladiator = AssetDatabase.LoadAssetAtPath<GladiatorDef>(Nobody),
                Edicts = new[] { Edict("iron_hides"), Edict("blood_sport") },
            };
            GameSession.StartDaily(plan, true); // R8-STUB(b): suppressed, it sets the session and the edicts
            CollectionAssert.AreEqual(plan.Edicts, RunModifiers.Active);
            yield return BootArena(plan.Seed);

            var hud = Object.FindAnyObjectByType<Hud>();
            Assert.IsTrue(hud.DailyChipShown, "the HUD's DAILY chip");
            Assert.AreEqual("DAILY", hud.DailyChipText);
            Assert.IsTrue(hud.DailyChipScored, "gold: the scored attempt");
            AssertOnScreen(hud.DailyChip, "the chip");
            AssertNothingTruncated(hud.DailyChip, "the chip");
            Assert.AreEqual(2, hud.EdictIconCount, "the day's edicts under the timer");

            yield return EndRun();
            var rewards = RunEndController.Instance.Rewards;
            var panel = Object.FindAnyObjectByType<RunEndPanel>();
            Assert.AreEqual("DAILY " + DailyPanel.DateShort(plan.Date) + " · SCORED", panel.SeedText);
            Assert.IsFalse(panel.SameSeedShown, "a daily's retry already keeps the seed");
            Assert.AreEqual("Practice  [R]", panel.RetryText);
            AssertOnScreen(panel.RetryRect, "PRACTICE");
            Assert.Greater(rewards.DailyBonus, 0f, "the scored daily's bonus reached the rewards"); // R8-STUB(b)
            StringAssert.Contains(EdictsPanel.MulText(1f + rewards.DailyBonus) + " daily", panel.RenownText); // R8-STUB(b)
            Assert.AreEqual(RunEndPanel.RenownLine(rewards), panel.RenownText);
            AssertNothingTruncated((RectTransform)panel.transform.Find("RunEndCanvas"), "the end screen");

            Press(panel.RetryRect); // PRACTICE
            yield return null;
            Assert.IsTrue(GameSession.HasDaily, "practice stays in the daily"); // R8-STUB(b)
            Assert.IsFalse(GameSession.DailyScored, "and is never scored"); // R8-STUB(b)
            Assert.AreEqual(plan.Seed, GameSession.Seed, "on the day's seed"); // R8-STUB(b)
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator APracticeDaily_HasAGreyChip_AndNoBonus()
        {
            SaveSandbox.Wipe();
            var plan = new DailyPlan // R8-STUB(b)
            {
                Date = Today,
                Seed = SeedA,
                Run = AssetDatabase.LoadAssetAtPath<RunDef>(SlavePit),
                Gladiator = AssetDatabase.LoadAssetAtPath<GladiatorDef>(Nobody),
                Edicts = Array.Empty<RunModifierDef>(),
            };
            GameSession.StartDaily(plan, false); // R8-STUB(b)
            yield return BootArena(plan.Seed);
            var hud = Object.FindAnyObjectByType<Hud>();
            Assert.IsTrue(hud.DailyChipShown);
            Assert.IsFalse(hud.DailyChipScored, "grey: practice");

            yield return EndRun();
            var rewards = RunEndController.Instance.Rewards;
            var panel = Object.FindAnyObjectByType<RunEndPanel>();
            Assert.AreEqual("DAILY " + DailyPanel.DateShort(plan.Date) + " · PRACTICE", panel.SeedText);
            Assert.AreEqual(0f, rewards.DailyBonus, "practice pays as a normal run"); // R8-STUB(b)
            StringAssert.DoesNotContain("daily", panel.RenownText);
        }
    }
}
