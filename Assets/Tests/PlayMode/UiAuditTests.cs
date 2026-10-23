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
    /// P3 (the producer's notes 4, 7 and 8; docs/tracks/P3-contract.md "Surfaces and contrast"): on every screen, every
    /// visible label's rendered text sits inside the content rect of the frame it's on (UiAudit.PaddingViolations), and
    /// reads against it at 3:1 or better, or, over the scene, is outlined (UiAudit.ContrastViolations). The menus on the
    /// real main menu scene with the sandbox's save: the main menu (fresh and seasoned), the gladiator select (every
    /// tablet page, the skins), the arena select (every arena), the Edicts page, the Daily screen and its read-only Edicts
    /// page, every page of every Codex tab, Options. In a run (the test arena): the HUD, the level-up with 3 and with 4
    /// cards, the pause, the boss's reward and both end screens. A failure lists each label as "parent/label: why".
    /// The in-run screens change in P3-B: these cases are written against today's screens and run after both merge.
    /// </summary>
    public class UiAuditTests
    {
        const string ArenaScene = "Assets/Scenes/Dev/TestArena.unity";

        MainMenu _menu;

        [TearDown]
        public void TearDown()
        {
            if (OptionsPanel.IsOpen) OptionsPanel.Instance.Close();
            RunModifiers.Clear();
            GameSession.Clear();
            DevSettings.ResetAll();
            SaveSandbox.Wipe(); // later tests expect a fresh save
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>Both checks over every visible root canvas (or just `root`), the reasons listed on failure.</summary>
        static void AssertClean(string what, Transform root = null)
        {
            Canvas.ForceUpdateCanvases();
            var why = new List<string>();
            int n = 0;
            if (root != null)
            {
                n += UiAudit.PaddingViolations(root, why);
                n += UiAudit.ContrastViolations(root, why);
            }
            else
            {
                foreach (var c in Object.FindObjectsByType<Canvas>())
                {
                    if (!c.isActiveAndEnabled || !c.isRootCanvas) continue;
                    n += UiAudit.PaddingViolations(c.transform, why);
                    n += UiAudit.ContrastViolations(c.transform, why);
                }
            }
            Assert.AreEqual(0, n, what + ": " + n + " label(s) break the padding or contrast rule:\n  " + string.Join("\n  ", why));
        }

        static void Invoke(object target, string method) =>
            target.GetType().GetMethod(method, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(target, null);

        IEnumerator Boot(Action<SaveData, MainMenu> stage = null)
        {
            SaveSandbox.Wipe();
            yield return LoadMenu();
            if (stage != null)
            {
                stage(MetaProgress.Data, _menu);
                SaveSystem.Save(MetaProgress.Data);
                yield return LoadMenu(); // the screens are built from the save on Awake
            }
            yield return Settle();
        }

        IEnumerator LoadMenu()
        {
            var op = SceneManager.LoadSceneAsync(GameSession.MenuScene, LoadSceneMode.Single);
            while (!op.isDone) yield return null;
            yield return null;
            _menu = Object.FindAnyObjectByType<MainMenu>();
            Assert.IsNotNull(_menu);
        }

        /// <summary>Two frames (the canvas scaler, then the layout; a Codex page's old entries are destroyed at the frame's
        /// end), then the canvases rebuilt.</summary>
        static IEnumerator Settle()
        {
            yield return null;
            yield return null;
            Canvas.ForceUpdateCanvases();
        }

        static void EarnAll(SaveData d, MainMenu menu)
        {
            foreach (var g in menu.gladiators)
                if (g != null && g.unlock != null && !d.unlocked.Contains(g.unlock.id)) d.unlocked.Add(g.unlock.id);
        }

        static void EarnArenasAndEdicts(SaveData d, MainMenu menu)
        {
            if (menu.meta != null && menu.meta.edictsUnlock != null) d.unlocked.Add(menu.meta.edictsUnlock.id);
            foreach (var r in menu.runs)
                if (r != null && r.unlock != null && !d.unlocked.Contains(r.unlock.id)) d.unlocked.Add(r.unlock.id);
        }

        static int DaysAgo(int n) => DailyGames.DateKey(DateTime.UtcNow.Date.AddDays(-n));

        /// <summary>A seasoned save: runs and wins, Renown to spend, the Daily Games with a ledger, NEW Codex entries.</summary>
        static void Seasoned(SaveData d, MainMenu menu)
        {
            EarnAll(d, menu);
            EarnArenasAndEdicts(d, menu);
            d.runs = 128;
            d.victories = 17;
            d.renown = 12345;
            if (menu.meta.dailyUnlock != null) d.unlocked.Add(menu.meta.dailyUnlock.id);
            d.dailies ??= new List<DailyRecord>();
            d.dailies.Add(new DailyRecord { date = DaysAgo(2), run = "slice", gladiator = "murder_machine", edicts = "iron_hides|blood_sport", outcome = (int)RunOutcome.Died, elapsed = 200f, kills = 300, renown = 40, seed = 1 });
            d.dailies.Add(new DailyRecord { date = DaysAgo(1), run = "provincial", gladiator = "thunder_champion", edicts = "iron_hides|blood_sport", outcome = (int)RunOutcome.Survived, elapsed = 480f, kills = 1904, renown = 412, seed = 2 });
            d.dailiesPlayed = 2;
            d.dailiesWon = 1;
            d.unlocked.Add("ch_first_evolution");
            d.flags.Add("golden_rat");
        }

        // ------------------------------------------------------------------ the menus

        [UnityTest, Timeout(60000)]
        public IEnumerator TheMainMenu_FreshAndSeasoned()
        {
            yield return Boot();
            AssertClean("the main menu (a fresh save)");
            _menu.MoveMenuFocus(1);
            yield return Settle();
            AssertClean("the main menu, CODEX focused");

            yield return Boot(Seasoned);
            Assert.IsTrue(_menu.DailyButtonOpen);
            Assert.IsTrue(_menu.CodexSealShown, "the seasoned save has NEW Codex entries");
            AssertClean("the main menu (a seasoned save: Renown, runs, the daily's dot, the NEW seal)");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator TheGladiatorSelect_EveryTabletPage_AndTheSkins()
        {
            yield return Boot(); // a fresh save: the locked tiles and their hints
            Invoke(_menu, "OpenSelect");
            yield return Settle();
            var grid = _menu.Roster;
            for (int i = 0; i < grid.Count; i++)
            {
                _menu.PickSlot(i);
                yield return Settle();
                AssertClean("the gladiator select, slot " + i + (grid.IsOpen(i) ? "" : " (locked)"));
            }

            yield return Boot((d, menu) => { EarnAll(d, menu); d.renown = 50; }); // everyone earned; skins too dear to buy
            Invoke(_menu, "OpenSelect");
            yield return Settle();
            grid = _menu.Roster;
            for (int i = 0; i < grid.Count; i++)
            {
                if (!grid.IsOpen(i)) continue;
                _menu.PickSlot(i);
                yield return Settle();
                string who = grid.Def(i) != null ? grid.Def(i).id : "slot " + i;
                AssertClean("the gladiator select: " + who);
                var skins = _menu.Skins;
                if (skins == null || !skins.Shown) continue;
                for (int k = 1; k < skins.Count; k++) // every skin on the strip: equipped, a price, a goal
                {
                    skins.Cycle(1);
                    yield return Settle();
                    AssertClean("the skin strip: " + who + " skin " + k + " (" + skins.StatusText + ")");
                }
                skins.Cycle(1); // back to the classic look
            }
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator TheArenaSelect_EveryArena_AndTheEdictsPage()
        {
            yield return Boot(EarnArenasAndEdicts);
            Invoke(_menu, "OpenSelect");
            yield return null;
            Invoke(_menu, "StartRun"); // FIGHT!: on to the arena screen
            yield return Settle();
            var grid = _menu.Ladder;
            for (int i = 0; i < grid.Count; i++)
            {
                _menu.PickStep(i);
                yield return Settle();
                AssertClean("the arena select: " + (grid.Run(i) != null ? grid.Run(i).id : "tile " + i));
            }

            _menu.OpenEdicts();
            yield return Settle();
            AssertClean("the Edicts page (nothing on)");
            var p = _menu.Edicts;
            Assert.IsTrue(p.Toggle(p.IndexOf("iron_hides")));
            Assert.IsTrue(p.Toggle(p.IndexOf("blood_sport")));
            p.HandleInput(false, true, false, false, false, false, false); // the focus moves (lifted, brighter)
            yield return Settle();
            AssertClean("the Edicts page (two on, the focus moved)");
            p.Close();
            yield return Settle();
            AssertClean("the arena select with two edicts on (the EDICTS button and the tablet's row)");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator TheArenaSelect_AFreshSave_LockedTiles()
        {
            yield return Boot((d, menu) => { if (menu.meta.edictsUnlock != null) d.unlocked.Add(menu.meta.edictsUnlock.id); });
            Invoke(_menu, "OpenSelect");
            yield return null;
            Invoke(_menu, "StartRun");
            yield return Settle();
            for (int i = 0; i < _menu.Ladder.Count; i++)
            {
                _menu.PickStep(i);
                yield return Settle();
                AssertClean("the arena select (fresh), tile " + i + (_menu.Ladder.IsOpen(i) ? "" : " (locked)"));
            }
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator TheDailyScreen_AndItsReadOnlyEdictsPage()
        {
            yield return Boot(Seasoned);
            _menu.OpenDaily();
            yield return Settle();
            Assert.IsTrue(_menu.DailyShown);
            AssertClean("the Daily screen (scored attempt, a ledger)");
            _menu.OpenDailyEdicts();
            yield return Settle();
            Assert.IsTrue(_menu.Edicts.IsOpen && _menu.Edicts.ReadOnly, "the day's edicts, read-only");
            AssertClean("the daily's read-only Edicts page");
            _menu.Edicts.Close();
            yield return Settle();

            // practice: today's scored attempt spent
            MetaProgress.BeginDaily(_menu.TodayPlan.Date);
            _menu.OpenDaily();
            yield return Settle();
            AssertClean("the Daily screen (practice)");
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator TheCodex_EveryPageOfEveryTab()
        {
            yield return Boot((d, menu) => { Seasoned(d, menu); d.renown = 300; }); // some wares affordable, some not
            _menu.OpenCodex();
            yield return Settle();
            var c = _menu.Codex;
            for (int t = 0; t < 7; t++)
            {
                var tab = (CodexPanel.Tab)t;
                c.Select(tab);
                yield return Settle();
                for (int page = 0; page < c.PageCount; page++)
                {
                    if (page > 0)
                    {
                        c.Turn(1);
                        yield return Settle();
                    }
                    AssertClean("the Codex, " + tab + " page " + (page + 1) + " / " + c.PageCount);
                }
            }
            c.Select(CodexPanel.Tab.Market);
            c.HandleInput(false, false, false, true, false, false); // the Market's keyboard focus (band and marker)
            yield return Settle();
            AssertClean("the Codex Market with a focused ware");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Options()
        {
            yield return Boot();
            var o = OptionsPanel.Instance;
            Assert.IsNotNull(o, "the menu scene has the options panel");
            o.Open();
            yield return Settle();
            AssertClean("Options");
            o.Close();
        }

        // ------------------------------------------------------------------ in a run

        RunState _run;
        LevelUpController _lvl;
        Hud _hud;

        IEnumerator BootRun()
        {
            DevSettings.ResetAll();
            TimeController.GameSpeed = 1f;
            TimeController.ResetForNewRun();
            var op = SceneManager.LoadSceneAsync(ArenaScene, LoadSceneMode.Single);
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
            _hud = Object.FindAnyObjectByType<Hud>();
            Assert.IsNotNull(_hud, "the arena has the HUD");
            // the dev overlay's debug line (Plain, under -admin): a tool's readout, not part of the game's look
            UiAudit.Exempt(_hud.DebugLabel);
        }

        static IEnumerator WaitRealtime(Func<bool> done, float seconds)
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
            yield return Frames(3); // the cards draw, then the stats column
            Canvas.ForceUpdateCanvases();
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator TheHud_TheLevelUp_ThreeAndFourCards_ThePause_AndTheBossReward()
        {
            yield return BootRun();
            yield return Frames(2);
            AssertClean("the HUD");

            yield return OpenCards();
            AssertClean("the level-up, " + _lvl.ChoiceCount + " cards");
            _lvl.Choose(0);
            yield return WaitRealtime(() => !_lvl.IsOpen, 2f);
            yield return Frames(2);
            AssertClean("the HUD with an upgrade in its strip");

            _lvl.levelUpCardDelta = 4 - _lvl.ChoiceCount; // the next level-up deals four (Luck's fourth card)
            yield return OpenCards();
            Assert.AreEqual(4, _lvl.ChoiceCount, "four cards");
            AssertClean("the level-up, 4 cards");
            _lvl.Choose(1);
            yield return WaitRealtime(() => !_lvl.IsOpen, 2f);
            _lvl.levelUpCardDelta = 0;

            Player.Main.SetPaused(true);
            yield return Frames(3);
            AssertClean("the pause");
            Player.Main.SetPaused(false);
            yield return Frames(2);

            Assert.IsTrue(_lvl.QueueReward(PickSource.Boss, Rarity.Rare));
            yield return WaitRealtime(() => _lvl.IsOpen && _lvl.Source == PickSource.Boss, 3f);
            Assert.IsTrue(_lvl.IsOpen, "the boss's reward opens");
            yield return Frames(3);
            AssertClean("the boss's reward (SPOILS OF VICTORY)");
            _lvl.Choose(0);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator TheEndScreen_Death()
        {
            yield return BootRun();
            DevSettings.GodMode = false;
            var end = RunEndController.Instance;
            Player.Main.GetComponent<PlayerHealth>().Kill();
            yield return WaitRealtime(() => end.ScreenShown, 8f);
            Assert.IsTrue(end.ScreenShown, "the end screen shows");
            yield return new WaitForSecondsRealtime(VexillumBanner.TotalSeconds() + 0.3f); // YOU DIED has dropped and holds
            AssertClean("the end screen (YOU DIED)");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator TheEndScreen_Survived()
        {
            yield return BootRun();
            var end = RunEndController.Instance;
            _run.End(RunOutcome.Survived);
            yield return WaitRealtime(() => end.ScreenShown, 8f);
            Assert.IsTrue(end.ScreenShown, "the end screen shows");
            yield return Frames(3);
            AssertClean("the end screen (SURVIVED!)");
        }
    }
}
