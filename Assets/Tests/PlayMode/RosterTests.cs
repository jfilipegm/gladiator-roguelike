using System;
using System.Collections;
using System.Collections.Generic;
using Gladiator.Gameplay;
using Gladiator.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gladiator.Tests
{
    /// <summary>
    /// R9, the producer's playtest: the gladiator select's sliding card row stranded the mouse once the last earned
    /// gladiator was picked. Now a grid of tessera tiles beside the lanista's tablet: every tile is on screen and
    /// clickable whoever is picked, a locked pick shows its hint and disables FIGHT!, the cursor moves in 2D, the arena
    /// ladder climbs bottom to top, the Codex shows the same grid read-only, and idle frames allocate nothing. No run
    /// starts here (the menu scene only).
    /// </summary>
    public class RosterTests
    {
        MainMenu _menu;

        [TearDown]
        public void TearDown()
        {
            GameSession.Clear();
            SaveSandbox.Wipe(); // later tests expect a fresh save
        }

        static void Invoke(object target, string method) =>
            target.GetType().GetMethod(method, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(target, null);

        static string ScreenOf(MainMenu menu) =>
            menu.GetType().GetField("_screen", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(menu).ToString();

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
            _menu = UnityEngine.Object.FindAnyObjectByType<MainMenu>();
            Assert.IsNotNull(_menu);
        }

        IEnumerator OpenSelect()
        {
            Invoke(_menu, "OpenSelect");
            yield return null;
            yield return null; // the canvas scaler has scaled the canvas
            Canvas.ForceUpdateCanvases();
        }

        /// <summary>Every gladiator earned (their unlocks in the save).</summary>
        static void EarnAll(SaveData d, MainMenu menu)
        {
            foreach (var g in menu.gladiators)
                if (g != null && g.unlock != null && !d.unlocked.Contains(g.unlock.id)) d.unlocked.Add(g.unlock.id);
        }

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

        /// <summary>Clicks at a tile's centre through the EventSystem, as the mouse would; asserts the tile takes it.</summary>
        static void Click(RectTransform tile)
        {
            var es = EventSystem.current;
            var r = ScreenRect(tile);
            var ped = new PointerEventData(es) { position = r.center, button = PointerEventData.InputButton.Left };
            var hits = new List<RaycastResult>();
            es.RaycastAll(ped, hits);
            Assert.Greater(hits.Count, 0, tile.name + " takes the pointer");
            var handler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
            Assert.AreSame(tile.gameObject, handler, "the top hit is the tile itself (nothing covers it)");
            ExecuteEvents.Execute(handler, ped, ExecuteEvents.pointerClickHandler);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator TheGrid_HoldsEveryGladiator_OnScreen_BesideTheTablet()
        {
            yield return Boot();
            yield return OpenSelect();
            var grid = _menu.Roster;
            int n = 0;
            foreach (var g in _menu.gladiators) if (g != null) n++;
            Assert.AreEqual(n + _menu.locked.Length, grid.Count, "every gladiator has a tile, locked or not");
            Assert.IsTrue(grid.IsOpen(0), "the earned ones come first");
            var tablet = ScreenRect(_menu.Tablet.Root);
            AssertOnScreen(_menu.Tablet.Root, "the tablet");
            for (int i = 0; i < grid.Count; i++)
            {
                AssertOnScreen(grid.Tile(i), "tile " + i);
                Assert.Less(ScreenRect(grid.Tile(i)).xMax, tablet.xMin, "tile " + i + " is left of the tablet");
                Assert.AreEqual(grid.IsOpen(i) ? "Card" : "Locked", grid.Tile(i).name);
                if (!grid.IsOpen(i))
                    Assert.AreEqual("???", grid.NameOf(i), "a locked tile keeps its name");
                else
                    Assert.AreEqual(RosterGrid.TileName(grid.Def(i)), grid.NameOf(i));
            }
            Assert.IsTrue(grid.HasWreath(0), "the picked tile wears the wreath");
            LogAssert.NoUnexpectedReceived();
        }

        /// <summary>The bug: picking the last earned gladiator slid every earlier card off screen, out of the mouse's reach.</summary>
        [UnityTest, Timeout(60000)]
        public IEnumerator PickingTheLastEarned_KeepsEveryTileOnScreen_AndTheFirstClickable()
        {
            yield return Boot(EarnAll);
            yield return OpenSelect();
            var grid = _menu.Roster;
            int last = 0;
            while (last + 1 < grid.Count && grid.IsOpen(last + 1)) last++;
            Assert.Greater(last, 3, "several gladiators earned");
            Assert.IsTrue(_menu.Pick(grid.Def(last)));
            yield return null;
            Canvas.ForceUpdateCanvases();
            for (int i = 0; i < grid.Count; i++) AssertOnScreen(grid.Tile(i), "tile " + i + " after picking the last earned one");
            Click(grid.Tile(0));
            yield return null;
            Assert.AreSame(grid.Def(0), _menu.Selected, "the mouse brings the first gladiator back");
            Assert.AreEqual(0, _menu.Slot);
            Assert.IsTrue(_menu.FightEnabled);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator ALockedPick_ShowsItsHint_DisablesFight_AndTheDioramaKeepsTheLastEarned()
        {
            yield return Boot();
            yield return OpenSelect();
            var grid = _menu.Roster;
            int locked = -1;
            for (int i = 0; i < grid.Count && locked < 0; i++) if (!grid.IsOpen(i) && grid.Def(i) != null) locked = i;
            Assert.GreaterOrEqual(locked, 0, "a fresh save has locked gladiators");
            var earned = _menu.Selected;
            Assert.IsNotNull(earned);

            Click(grid.Tile(locked));
            yield return null;
            Assert.AreEqual(locked, _menu.Slot, "a locked tile can be picked to read it");
            Assert.IsFalse(_menu.FightEnabled, "FIGHT! waits for an earned gladiator");
            Assert.AreEqual("???", _menu.Tablet.NameText);
            Assert.AreEqual(grid.Def(locked).unlock.hint, _menu.Tablet.BodyText, "the tablet shows the unlock's hint");
            Assert.IsFalse(_menu.Tablet.ShowsWeapon, "nothing more is given away");
            Assert.AreSame(earned, _menu.Selected, "the diorama keeps the last earned gladiator");
            var shown = _menu.hero.parent.Find("Hero_" + earned.id);
            Assert.IsNotNull(shown);
            Assert.IsTrue(shown.gameObject.activeSelf);

            Invoke(_menu, "StartRun"); // FIGHT! (Enter, or a second click) is refused
            Click(grid.Tile(locked));
            yield return null;
            Assert.AreEqual("Select", ScreenOf(_menu));
            Assert.AreEqual("MainMenu", SceneManager.GetActiveScene().name, "no run started");

            Click(grid.Tile(0));
            yield return null;
            Assert.IsTrue(_menu.FightEnabled);
            Assert.AreEqual(grid.Def(0).displayName.ToUpperInvariant(), _menu.Tablet.NameText);
            Assert.IsTrue(_menu.Tablet.ShowsWeapon);
            StringAssert.Contains("0 runs", _menu.Tablet.RunsText);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator TheCursor_MovesIn2D_AndTheTabletFollows()
        {
            yield return Boot(EarnAll);
            yield return OpenSelect();
            var grid = _menu.Roster;
            Assert.GreaterOrEqual(grid.Count, 6);
            _menu.PickSlot(0);
            Assert.IsTrue(_menu.MoveCursor(1, 0));
            Assert.AreEqual(1, _menu.Slot, "right");
            Assert.IsTrue(_menu.MoveCursor(0, 1));
            Assert.AreEqual(1 + grid.Columns, _menu.Slot, "down a row");
            Assert.IsTrue(_menu.MoveCursor(-1, 0));
            Assert.AreEqual(grid.Columns, _menu.Slot, "left");
            Assert.IsFalse(_menu.MoveCursor(-1, 0), "the row's left end stops");
            Assert.AreEqual(grid.Columns, _menu.Slot);
            Assert.IsTrue(_menu.MoveCursor(0, -1));
            Assert.AreEqual(0, _menu.Slot, "up");
            Assert.IsFalse(_menu.MoveCursor(0, -1), "the top stops");
            _menu.PickSlot(grid.Columns + 1);
            Assert.AreEqual(grid.Def(grid.Columns + 1).displayName.ToUpperInvariant(), _menu.Tablet.NameText, "the tablet follows the cursor");
            Assert.AreSame(grid.Def(grid.Columns + 1), _menu.Selected, "an earned tile is the pick");
            Assert.IsTrue(grid.HasWreath(grid.Columns + 1));
            Assert.IsFalse(grid.HasWreath(0));
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator TheLadder_ClimbsBottomToTop_AndALockedStepCantFight()
        {
            yield return Boot();
            var ladder = _menu.Ladder;
            Assert.AreEqual(_menu.runs.Length, ladder.Count, "every arena is a step");
            Assert.Greater(ladder.Count, 1);
            Assert.IsFalse(_menu.AsksForArena, "one arena earned: FIGHT! goes straight in (the hidden screen is still laid out)");
            yield return null;
            Canvas.ForceUpdateCanvases();
            var bottom = ScreenRect(ladder.Tile(0));
            var next = ScreenRect(ladder.Tile(1));
            Assert.Less(bottom.center.y, next.center.y, "the Slave Pit is the bottom step");
            Assert.Less(bottom.center.x, next.center.x, "each step a little further right");
            for (int i = 0; i < ladder.Count; i++) AssertOnScreen(ladder.Tile(i), "step " + i);
            AssertOnScreen(_menu.ArenaTablet.Root, "the arena tablet");

            _menu.PickStep(1); // locked on a fresh save
            Assert.IsFalse(_menu.ArenaFightEnabled);
            Assert.AreEqual("???", _menu.ArenaTablet.NameText);
            Assert.AreEqual(ladder.Run(1).unlock.hint, _menu.ArenaTablet.BodyText);
            Assert.AreSame(ladder.Run(0), _menu.SelectedRun, "FIGHT! keeps the last earned arena");
            _menu.PickStep(0);
            Assert.IsTrue(_menu.ArenaFightEnabled);
            Assert.AreEqual(ladder.Run(0).DisplayName, _menu.ArenaTablet.NameText);
            Assert.AreEqual("×1", _menu.ArenaTablet.RenownText, "tier 0: Renown as earned");
            Assert.IsTrue(_menu.Climb(1));
            Assert.AreEqual(1, _menu.Step, "up climbs");
            Assert.IsTrue(_menu.Climb(-1));
            Assert.IsFalse(_menu.Climb(-1), "the bottom step stops");
            Assert.AreEqual("MainMenu", SceneManager.GetActiveScene().name);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator ABeatenBoss_WreathsItsStep_AndTheNextArenaShowsItsRenown()
        {
            yield return Boot((d, menu) =>
                MetaProgress.Apply(new RunSummary { Outcome = RunOutcome.Survived, Elapsed = 480f, BossDefeated = "gorm", BossDefeatedHp01 = 0.5f, GladiatorId = "nobody" }, menu.meta, d));
            var ladder = _menu.Ladder;
            Assert.IsTrue(_menu.AsksForArena, "beating Gorm earns the Provincial Pit");
            Assert.IsNotNull(ladder.Run(0).Boss, "the Slave Pit's run knows its layout and boss (rebuild the main menu: RunTaglines links them)");
            Assert.IsTrue(ladder.HasWreath(0), "Gorm beaten: the Slave Pit's numeral wears the laurel");
            Assert.IsFalse(ladder.HasWreath(1));
            _menu.PickStep(0);
            StringAssert.StartsWith("Beaten 1", _menu.ArenaTablet.BestText);
            Assert.IsTrue(_menu.PickArena(ladder.Run(1)));
            Assert.AreEqual(1, _menu.Step);
            Assert.IsTrue(_menu.ArenaFightEnabled);
            Assert.AreEqual("×1.5", _menu.ArenaTablet.RenownText, "tier 1: Renown ×1.5");
            StringAssert.Contains("+25 %", _menu.ArenaTablet.DifficultyText, "the Provincial Pit's enemy HP");
            StringAssert.StartsWith("Not beaten", _menu.ArenaTablet.BestText);
            yield return null;
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator TheCodex_ShowsTheSameGrid_ReadOnly_WithNewMarks()
        {
            GladiatorDef knight = null;
            yield return Boot((d, menu) =>
            {
                foreach (var g in menu.gladiators) if (g != null && g.id == "blood_knight") knight = g;
                Assert.IsNotNull(knight);
                d.unlocked.Add(knight.unlock.id);
            });
            yield return OpenSelect();
            int selectCount = _menu.Roster.Count;
            var codex = _menu.Codex;
            _menu.OpenCodex();
            codex.Select(CodexPanel.Tab.Gladiators);
            yield return null;
            var grid = codex.Roster;
            Assert.AreEqual(selectCount, grid.Count, "the same roster as the select screen");
            int k = -1;
            for (int i = 0; i < grid.Count; i++) if (grid.Def(i) == knight) k = i;
            Assert.AreEqual(1, k, "the earned ones first: the Nobody, then the knight");
            Assert.IsTrue(grid.HasNew(k), "newly earned: marked NEW for this visit");
            Assert.IsFalse(grid.HasNew(0));
            foreach (var b in _menu.transform.Find("MenuCanvas/Codex").GetComponentsInChildren<Button>())
                Assert.AreNotEqual("Fight", b.name, "read-only: no FIGHT! in the Codex");
            Assert.AreEqual(0, codex.RosterFocus);
            codex.HandleInput(false, false, false, true, false, false); // down: the next gladiator
            Assert.AreEqual(1, codex.RosterFocus);
            Assert.AreEqual(knight.displayName.ToUpperInvariant(), codex.RosterTablet.NameText);
            codex.HandleInput(false, false, true, false, false, false); // up
            codex.HandleInput(false, false, true, false, false, false); // up again: wraps to the last
            Assert.AreEqual(grid.Count - 1, codex.RosterFocus);
            codex.HandleInput(true, false, false, false, false, false); // left: still the tabs
            Assert.AreEqual(CodexPanel.Tab.Records, codex.Current);
            codex.Select(CodexPanel.Tab.Gladiators);
            Assert.IsFalse(grid.HasNew(k), "seen now");
            Assert.AreEqual(1, codex.PageCount, "one grid, no pages");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator IdleFrames_AndHovers_AllocateNothing()
        {
            yield return Boot(EarnAll);
            yield return OpenSelect();
            var method = typeof(MainMenu).GetMethod("Update", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var update = (Action)Delegate.CreateDelegate(typeof(Action), _menu, method);
            var grid = _menu.Roster;
            for (int i = 0; i < 8; i++) { update(); grid.Hover(i % grid.Count, true); grid.Hover(i % grid.Count, false); } // warm up
            // the heap's used size, as GameLoop tracks its systems (a GC inside the loop could only hide bytes, not add them)
            long before = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong();
            for (int i = 0; i < 2000; i++)
            {
                update();
                grid.Hover(i % grid.Count, true);
                grid.Hover(i % grid.Count, false);
            }
            long bytes = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong() - before;
            Assert.LessOrEqual(bytes, 0, "the menu's idle update and the hover frames allocate nothing");
            yield return null;
        }
    }
}
