using System;
using System.Collections;
using System.Collections.Generic;
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
    /// U1-C (docs/tracks/U1-contract.md, "U1-C menus"): the menus in the Roman look, on the real main menu scene with the
    /// sandbox's save (SaveSandbox). The main menu's PLAY is a bronze tabula ansata and the rest marble, the title is in
    /// the Title role and the Renown in Number, the arrows move one focused plate; the select screen's open gladiators
    /// are scuta and the locked ones dark iron, FIGHT! bronze, BACK marble, the lanista's tablet wax; the arena tablet is
    /// wax, EDICTS marble, the Edicts page marble with a danger ring on an edict that's on; the Daily screen's
    /// proclamation sits in a mosaic and its ledger on wax (laurels on a won day); the Codex's active tab is bronze, the
    /// others marble, RECORDS wax and the entries marble; options bronze. On every screen no label is cut off
    /// (UiKit.TruncatedLabels: every tablet page, every ladder step, every Codex page). The test assembly has no TMP or
    /// UGUI reference of its own: the look is read through UiKit.HasRole, UiFrames.KindOf and the screens' hooks.
    /// Needs U1-A's theme (Gladiator/Setup/UI/All): with the stand-ins every frame reads -1 and every role false.
    /// </summary>
    public class UiMenuLookTests
    {
        static readonly string[] MenuNames = { "PLAY", "DAILY", "CODEX", "OPTIONS", "QUIT" };

        MainMenu _menu;

        [TearDown]
        public void TearDown()
        {
            if (OptionsPanel.IsOpen) OptionsPanel.Instance.Close();
            RunModifiers.Clear();
            GameSession.Clear();
            SaveSandbox.Wipe(); // later tests expect a fresh save
        }

        // ------------------------------------------------------------------ helpers

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
        }

        IEnumerator LoadMenu()
        {
            var op = SceneManager.LoadSceneAsync(GameSession.MenuScene, LoadSceneMode.Single);
            while (!op.isDone) yield return null;
            yield return null;
            _menu = Object.FindAnyObjectByType<MainMenu>();
            Assert.IsNotNull(_menu);
        }

        /// <summary>Two frames (the canvas scaler, then the layout), then the canvases rebuilt.</summary>
        static IEnumerator Settle()
        {
            yield return null;
            yield return null;
            Canvas.ForceUpdateCanvases();
        }

        Transform MenuCanvas
        {
            get
            {
                var t = _menu.transform.Find("MenuCanvas");
                Assert.IsNotNull(t, "the menu's canvas");
                return t;
            }
        }

        /// <summary>Every gladiator earned (their unlocks in the save).</summary>
        static void EarnAll(SaveData d, MainMenu menu)
        {
            foreach (var g in menu.gladiators)
                if (g != null && g.unlock != null && !d.unlocked.Contains(g.unlock.id)) d.unlocked.Add(g.unlock.id);
        }

        static int DaysAgo(int n) => DailyGames.DateKey(DateTime.UtcNow.Date.AddDays(-n));

        static string KindName(int k) => k < 0 ? "no theme frame (" + k + ")" : ((FrameKind)k).ToString();

        static void AssertKind(int shown, FrameKind want, string what) =>
            Assert.AreEqual(want.ToString(), KindName(shown), what + " is drawn on the " + want + " frame");

        static void AssertKind(Component c, FrameKind want, string what)
        {
            Assert.IsNotNull(c, what);
            AssertKind(UiFrames.KindOf(c), want, what);
        }

        static void AssertRole(Component label, TextRole role, string what)
        {
            Assert.IsNotNull(label, what);
            Assert.IsTrue(UiKit.HasRole(label, role), what + " is set in the " + role + " role");
        }

        /// <summary>No visible label under `root` is cut off (autosize shrinks to the floor first; Truncate is the last
        /// resort). U1-C lists no intentional truncation (docs/tracks/U1-menus.md).</summary>
        static void AssertNothingCut(Transform root, string what)
        {
            Assert.IsNotNull(root, what);
            var cut = new List<string>();
            int n = UiKit.TruncatedLabels(root, cut);
            Assert.AreEqual(0, n, what + ": text cut off in " + string.Join(", ", cut));
        }

        void AssertFocused(int want)
        {
            int focused = 0;
            for (int i = 0; i < _menu.MenuPlateCount; i++)
            {
                bool on = _menu.MenuPlate(i).State == UiState.Focused;
                if (on) focused++;
                Assert.AreEqual(i == want, on, MenuNames[i] + (i == want ? " has the focus ring" : " has no focus ring"));
            }
            Assert.AreEqual(1, focused, "one focused plate at a time");
            Assert.AreEqual(want, _menu.MenuFocus);
        }

        // ------------------------------------------------------------------ the main menu

        [UnityTest, Timeout(60000)]
        public IEnumerator TheMainMenu_PlayIsBronze_TheRestMarble_AndTheArrowsMoveOneFocusedPlate()
        {
            yield return Boot();
            yield return Settle();
            Assert.AreEqual(5, _menu.MenuPlateCount, "PLAY · DAILY · CODEX · OPTIONS · QUIT");
            AssertKind(_menu.MenuPlate(0).Frame, FrameKind.AnsataBronze, "PLAY (the screen's main action)");
            for (int i = 1; i < 5; i++) AssertKind(_menu.MenuPlate(i).Frame, FrameKind.AnsataMarble, MenuNames[i]);
            AssertRole(_menu.TitleLabel, TextRole.Title, "the title");
            AssertRole(_menu.RenownLabel, TextRole.Number, "the Renown");
            AssertKind(_menu.TitlePlaque, FrameKind.Mosaic, "the title's plaque");
            AssertKind(_menu.RenownPlate, FrameKind.Bronze, "the Renown's plate");

            // a fresh save: DAILY is shut (dimmed) and the arrows skip it
            Assert.IsFalse(_menu.DailyButtonOpen);
            Assert.AreEqual(UiState.Disabled, _menu.MenuPlate(1).State, "the shut DAILY is dimmed");
            AssertFocused(0);
            _menu.MoveMenuFocus(1);
            AssertFocused(2);
            _menu.MoveMenuFocus(1);
            AssertFocused(3);
            _menu.MoveMenuFocus(-1);
            AssertFocused(2);
            _menu.MoveMenuFocus(-1);
            AssertFocused(0);
            _menu.MoveMenuFocus(-1);
            AssertFocused(4); // round the top to QUIT
            Assert.AreEqual(UiState.Disabled, _menu.MenuPlate(1).State, "DAILY stays dimmed");

            yield return Settle();
            AssertNothingCut(MenuCanvas, "the main menu");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator TheMainMenusIdleFrames_AllocateNothing()
        {
            yield return Boot();
            var method = typeof(MainMenu).GetMethod("Update", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var update = (Action)Delegate.CreateDelegate(typeof(Action), _menu, method);
            for (int i = 0; i < 8; i++) update(); // warm up
            long before = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong();
            for (int i = 0; i < 2000; i++) update();
            long bytes = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong() - before;
            Assert.LessOrEqual(bytes, 0, "the main menu's idle frames (the plates' focus included) allocate nothing");
            yield return null;
        }

        // ------------------------------------------------------------------ the gladiator select

        [UnityTest, Timeout(60000)]
        public IEnumerator TheSelectScreen_ScutumTiles_TheWaxTablet_BronzeFight_MarbleBack()
        {
            yield return Boot(); // a fresh save: the Nobody and the locked
            Invoke(_menu, "OpenSelect");
            yield return Settle();
            var grid = _menu.Roster;
            int open = 0, locked = 0;
            for (int i = 0; i < grid.Count; i++)
            {
                string who = grid.Def(i) != null ? grid.Def(i).id : "slot " + i;
                if (grid.IsOpen(i)) { open++; AssertKind(grid.TileFrame(i), FrameKind.Scutum, who + "'s tile"); }
                else { locked++; AssertKind(grid.TileFrame(i), FrameKind.ScutumLocked, who + "'s tile (locked: dark iron)"); }
                AssertRole(grid.NameLabel(i), TextRole.Title, who + "'s name");
            }
            Assert.GreaterOrEqual(open, 1, "the Nobody is earned");
            Assert.Greater(locked, 0, "a fresh save has locked gladiators");
            Assert.AreEqual(UiTheme.Current.gold, grid.RingColor(_menu.Slot), "the picked tile's ring is gold");
            Assert.IsTrue(grid.HasWreath(_menu.Slot), "and it wears the wreath");
            AssertKind(_menu.FightPlateOf("Select").Frame, FrameKind.AnsataBronze, "FIGHT!");
            AssertKind(_menu.BackPlateOf("Select").Frame, FrameKind.AnsataMarble, "BACK");
            AssertKind(_menu.Tablet.FrameKindShown, FrameKind.Wax, "the lanista's tablet");
            if (_menu.Skins.Shown) AssertKind(_menu.Skins.PlateFrame, FrameKind.Bronze, "the skin strip");
            AssertNothingCut(MenuCanvas, "the gladiator select");

            // every locked page (the silhouette, ???, the hint) fits the wax
            for (int i = 0; i < grid.Count; i++)
            {
                if (grid.IsOpen(i)) continue;
                _menu.PickSlot(i);
                yield return null;
                AssertNothingCut(_menu.Tablet.Root, "the tablet of locked slot " + i);
            }
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator EveryGladiatorsTablet_FitsTheWax_AtTheFloor()
        {
            yield return Boot(EarnAll);
            Invoke(_menu, "OpenSelect");
            yield return Settle();
            var grid = _menu.Roster;
            Assert.Greater(grid.Count, 5, "the whole roster");
            for (int i = 0; i < grid.Count; i++)
            {
                _menu.PickSlot(i);
                yield return null;
                string who = grid.Def(i) != null ? grid.Def(i).id : "slot " + i;
                AssertNothingCut(_menu.Tablet.Root, "the tablet of " + who);
                if (grid.IsOpen(i)) Assert.IsTrue(_menu.Tablet.ShowsWeapon, who + "'s weapon row");
            }
        }

        // ------------------------------------------------------------------ the arena select and the Edicts

        [UnityTest, Timeout(90000)]
        public IEnumerator TheArenaScreen_WaxTablet_MarbleEdicts_AndTheMarbleEdictsPage()
        {
            yield return Boot((d, menu) =>
            {
                Assert.IsNotNull(menu.meta.edictsUnlock, "MetaConfig.edictsUnlock (Gladiator/Setup/Meta/Create Edict Data)");
                d.unlocked.Add(menu.meta.edictsUnlock.id);
                foreach (var r in menu.runs)
                    if (r != null && r.unlock != null && !d.unlocked.Contains(r.unlock.id)) d.unlocked.Add(r.unlock.id);
            });
            Invoke(_menu, "OpenSelect");
            yield return null;
            Invoke(_menu, "StartRun"); // FIGHT!: on to the arena screen
            yield return Settle();
            Assert.IsTrue(_menu.EdictsButtonShown);
            AssertKind(_menu.EdictsPlate.Frame, FrameKind.AnsataMarble, "EDICTS");
            AssertKind(_menu.FightPlateOf("Arena").Frame, FrameKind.AnsataBronze, "FIGHT!");
            AssertKind(_menu.BackPlateOf("Arena").Frame, FrameKind.AnsataMarble, "BACK");
            AssertKind(_menu.ArenaTablet.FrameKindShown, FrameKind.Wax, "the arena tablet");
            var ladder = _menu.Ladder;
            for (int i = 0; i < ladder.Count; i++) AssertRole(ladder.NameLabel(i), TextRole.Title, "step " + i + "'s name");
            AssertNothingCut(ladder.Root, "the ladder");
            for (int i = 0; i < ladder.Count; i++)
            {
                _menu.PickStep(i);
                yield return null;
                AssertNothingCut(_menu.ArenaTablet.Root, "the tablet of " + (ladder.Run(i) != null ? ladder.Run(i).id : "step " + i));
            }
            AssertNothingCut(MenuCanvas, "the arena select");

            // the page: marble, stone cards, an edict that's on ringed in the danger colour
            _menu.OpenEdicts();
            yield return Settle();
            var p = _menu.Edicts;
            Assert.IsTrue(p.IsOpen);
            AssertKind(p.PageFrame, FrameKind.Marble, "the Edicts page");
            AssertRole(p.HeaderLabel, TextRole.Title, "the page's header");
            for (int i = 0; i < p.Count; i++)
            {
                AssertKind(p.CardPlate(i), FrameKind.Stone, p.Def(i).id + "'s card");
                Assert.IsFalse(p.OnRingShown(i), p.Def(i).id + " is off: no danger ring");
            }
            int a = p.IndexOf("iron_hides"), b = p.IndexOf("blood_sport");
            Assert.IsTrue(p.Toggle(a));
            Assert.IsTrue(p.Toggle(b));
            Assert.IsTrue(p.OnRingShown(a), "on: the ring");
            AssertKind(p.OnRingKind(a), FrameKind.Focus, "the ON ring");
            var danger = UiTheme.Current.danger;
            var ring = p.OnRingColor(a);
            Assert.AreEqual(danger.r, ring.r, 0.01f, "the ON ring is the danger colour (it hurts you)");
            Assert.AreEqual(danger.g, ring.g, 0.01f);
            Assert.AreEqual(danger.b, ring.b, 0.01f);
            Assert.AreEqual(EdictsPanel.Hurt, p.CardFrame(a), "CardFrame keeps reading orange-red while on");
            yield return Settle();
            AssertNothingCut(p.Box, "the Edicts page");

            // closed: the tablet's EDICTS row names them and still fits
            p.Close();
            yield return Settle();
            StringAssert.Contains("RENOWN", _menu.ArenaTablet.EdictsText);
            AssertNothingCut(_menu.ArenaTablet.Root, "the arena tablet with two edicts on");
        }

        // ------------------------------------------------------------------ the Daily Games

        [UnityTest, Timeout(60000)]
        public IEnumerator TheDailyScreen_MosaicProclamation_WaxLedger_LaurelsOnAWonDay()
        {
            yield return Boot((d, menu) =>
            {
                Assert.IsNotNull(menu.meta.dailyUnlock, "MetaConfig.dailyUnlock (Gladiator/Setup/Meta/Create Daily Data)");
                d.unlocked.Add(menu.meta.dailyUnlock.id);
                d.dailies ??= new List<DailyRecord>();
                d.dailies.Add(new DailyRecord { date = DaysAgo(2), run = "slice", gladiator = "nobody", edicts = "iron_hides|blood_sport", outcome = (int)RunOutcome.Died, elapsed = 200f, kills = 300, renown = 40, seed = 1 });
                d.dailies.Add(new DailyRecord { date = DaysAgo(1), run = "slice", gladiator = "nobody", edicts = "iron_hides|blood_sport", outcome = (int)RunOutcome.Survived, elapsed = 480f, kills = 1904, renown = 412, seed = 2 });
                d.dailiesPlayed = 2;
                d.dailiesWon = 1;
            });
            _menu.OpenDaily();
            yield return Settle();
            Assert.IsTrue(_menu.DailyShown);
            var p = _menu.Daily;
            AssertKind(p.ProclamationFrame, FrameKind.Mosaic, "the proclamation");
            AssertKind(p.LedgerFrame, FrameKind.Wax, "the ledger");
            Assert.AreEqual("SCORED ATTEMPT", p.StatusText);
            AssertRole(p.StatusLabel, TextRole.Title, "SCORED ATTEMPT");
            AssertRole(p.ClockLabel, TextRole.Number, "the clock");
            Assert.AreEqual(2, p.LedgerRows);
            Assert.IsTrue(p.LedgerHasLaurels(0), "yesterday's win stands between laurels");
            Assert.IsFalse(p.LedgerHasLaurels(1), "a death has none");
            AssertKind(_menu.FightPlateOf("Daily").Frame, FrameKind.AnsataBronze, "FIGHT!");
            AssertKind(_menu.BackPlateOf("Daily").Frame, FrameKind.AnsataMarble, "BACK");
            AssertNothingCut(MenuCanvas, "the Daily screen");
        }

        // ------------------------------------------------------------------ the Codex

        void AssertTabs(CodexPanel c, CodexPanel.Tab active)
        {
            for (int t = 0; t < 7; t++)
                AssertKind(c.TabPlate((CodexPanel.Tab)t).Frame, t == (int)active ? FrameKind.AnsataBronze : FrameKind.AnsataMarble,
                    (CodexPanel.Tab)t + "'s tab" + (t == (int)active ? " (active)" : ""));
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator TheCodex_BronzeActiveTab_WaxRecords_MarbleEntries_AndEveryPageFits()
        {
            yield return Boot((d, menu) =>
            {
                EarnAll(d, menu);
                d.renown = 5000; // the Market's BUY plates lit
            });
            _menu.OpenCodex();
            yield return Settle();
            var c = _menu.Codex;
            Assert.IsTrue(_menu.CodexOpen);

            c.Select(CodexPanel.Tab.Records);
            yield return Settle();
            AssertTabs(c, CodexPanel.Tab.Records);
            AssertKind(c.RecordsFrame, FrameKind.Wax, "RECORDS");

            for (int t = 0; t < 7; t++)
            {
                var tab = (CodexPanel.Tab)t;
                c.Select(tab);
                yield return Settle(); // the last page's entries are destroyed at the frame's end
                AssertTabs(c, tab);
                for (int page = 0; page < c.PageCount; page++)
                {
                    if (page > 0)
                    {
                        c.Turn(1);
                        yield return Settle();
                    }
                    for (int k = 0; k < c.DrawnEntries; k++)
                    {
                        int kind = c.EntryFrame(k);
                        Assert.IsTrue(kind == (int)FrameKind.Marble || kind == (int)FrameKind.Stone,
                            tab + " page " + (page + 1) + " entry " + k + " is on marble (open) or stone (still a hint), not " + KindName(kind));
                    }
                    AssertNothingCut(c.Box, tab + " page " + (page + 1));
                }
            }

            // the Market shows what you buy: every entry lit, on marble
            c.Select(CodexPanel.Tab.Market);
            yield return Settle();
            Assert.Greater(c.DrawnEntries, 0, "the Market has wares");
            for (int k = 0; k < c.DrawnEntries; k++) AssertKind(c.EntryFrame(k), FrameKind.Marble, "Market entry " + k);

            // GLADIATORS: the select screen's scuta and the wax tablet
            c.Select(CodexPanel.Tab.Gladiators);
            yield return Settle();
            var grid = c.Roster;
            for (int i = 0; i < grid.Count; i++)
                AssertKind(grid.TileFrame(i), grid.IsOpen(i) ? FrameKind.Scutum : FrameKind.ScutumLocked, "the Codex tile " + i);
            AssertKind(c.RosterTablet.FrameKindShown, FrameKind.Wax, "the Codex's tablet");
        }

        // ------------------------------------------------------------------ options

        [UnityTest, Timeout(60000)]
        public IEnumerator Options_ABronzePanel_NumbersBesideTroughs()
        {
            yield return Boot();
            var o = OptionsPanel.Instance;
            Assert.IsNotNull(o, "the menu scene has the options panel");
            o.Open();
            yield return Settle();
            Assert.IsTrue(OptionsPanel.IsOpen);
            AssertKind(o.PanelFrame, FrameKind.Bronze, "the options panel");
            Assert.Greater(o.RowCount, 0);
            for (int i = 0; i < o.RowCount; i++) AssertRole(o.ValueLabel(i), TextRole.Number, "row " + i + "'s value");
            Assert.AreEqual(GameOptions.Data.master, o.FillAmount(0), 0.001f, "the trough shows the master volume");
            AssertNothingCut(o.Panel, "options");
            o.Close();
            Assert.IsFalse(OptionsPanel.IsOpen);
        }
    }
}
