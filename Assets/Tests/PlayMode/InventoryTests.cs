using System;
using System.Collections;
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
    /// P3-B (docs/tracks/P3-contract.md, the producer's notes 15, 17, 19) in the test arena: "your items" sits left of the
    /// cards as the stats column's mirror image; its four weapon and six Boon slots always show (empty ones as wells);
    /// a selected card you own lights its slot with its tooltip, a new one shows as a translucent ghost in the next free
    /// slot of its row; hovering a slot shows its tooltip on the screen; the pause shows the inventory, the stats column
    /// and its three plates (RESUME resumes); a stat row's tooltip names its sources; and hovering cards and slots
    /// allocates nothing once every buffer has grown.
    /// </summary>
    public class InventoryTests
    {
        const string Scene = "Assets/Scenes/Dev/TestArena.unity";

        RunState _run;
        LevelUpController _lvl;
        PlayerBuild _build;
        LevelUpPanel _cards;
        StatsPanel _page;
        InventoryPanel _inv;

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
            float end = Time.realtimeSinceStartup + 15f;
            while (RunIntro.Instance != null && !RunIntro.Instance.Done && Time.realtimeSinceStartup < end) yield return null;
            _run = RunState.Current;
            _lvl = LevelUpController.Instance;
            _build = Player.Main.GetComponent<PlayerBuild>();
            _cards = Object.FindAnyObjectByType<LevelUpPanel>();
            _page = StatsPanel.Instance;
            Assert.IsNotNull(_cards, "the arena has the level-up panel");
            _inv = _cards.Inventory;
            Assert.IsNotNull(_inv, "the level-up panel makes the inventory");
        }

        [TearDown]
        public void TearDown()
        {
            if (Player.Main != null) Player.Main.SetPaused(false);
            DevSettings.ResetAll();
        }

        static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        IEnumerator OpenCards()
        {
            _run.AddXp(_run.XpToNext - _run.Xp + 0.01f);
            float end = Time.realtimeSinceStartup + 3f;
            while (!_lvl.IsOpen && Time.realtimeSinceStartup < end) yield return null;
            Assert.IsTrue(_lvl.IsOpen, "level-up cards should open");
            yield return Frames(2);
        }

        UpgradeDef Find(string id)
        {
            foreach (var d in _build.catalog.upgrades) if (d != null && d.id == id) return d;
            return null;
        }

        void BanishAllBut(Func<UpgradeDef, bool> keep)
        {
            foreach (var d in _build.catalog.upgrades)
                if (d != null && !keep(d)) _build.State.Banish(d);
        }

        int OfferOf(UpgradeDef d)
        {
            for (int i = 0; i < _lvl.ChoiceCount; i++) if (_lvl.Offers[i].Def == d) return i;
            return -1;
        }

        static Rect World(RectTransform rt)
        {
            var c = new Vector3[4];
            rt.GetWorldCorners(c);
            return Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y);
        }

        UpgradeDef StartingWeaponCard()
        {
            var w = Player.Main.GetComponent<WeaponSystem>().Weapons;
            Assert.Greater(w.Count, 0, "the gladiator starts with a weapon");
            var card = _build.FindWeaponCard(w[0].Def);
            Assert.IsNotNull(card, "its weapon card is in the catalogue");
            return card;
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Inventory_ShowsEverySlot_TheStartingWeapon_AndMirrorsTheStats()
        {
            yield return Boot();
            yield return OpenCards();
            Assert.IsTrue(_inv.Visible, "the inventory shows with the cards");
            Assert.AreEqual(InventoryPanel.TitleWord, _inv.TitleText);
            var gladius = StartingWeaponCard();
            Assert.AreEqual(gladius, _inv.SlotDef(0), "the starting weapon fills the first weapon slot");
            Assert.AreEqual("1", _inv.SlotBadge(0), "with its level");
            Assert.IsTrue(_inv.SlotShowsIcon(0), "and its icon");
            for (int i = 1; i < InventoryPanel.Slots; i++)
            {
                Assert.IsFalse(_inv.SlotFilled(i), "slot " + i + " is free");
                Assert.IsTrue(_inv.SlotShowsWell(i), "an empty slot still shows (a stone well)");
                Assert.AreEqual("", _inv.SlotBadge(i));
            }
            Assert.AreEqual("1/4", _inv.WeaponsCountText);
            Assert.AreEqual("0/6", _inv.BoonsCountText);

            // the stats column's mirror image: the same width and top, as far left of the screen's centre as it is right
            Canvas.ForceUpdateCanvases();
            var inv = World(_inv.Root);
            var stats = World(_page.Page);
            float unit = _cards.CanvasRect.lossyScale.x;
            Debug.Log($"[P3] {Screen.width}x{Screen.height}: inventory x {inv.xMin:0}-{inv.xMax:0}, stats x {stats.xMin:0}-{stats.xMax:0}, top {inv.yMax:0}/{stats.yMax:0}");
            Assert.AreEqual(stats.width, inv.width, unit, "the same width");
            Assert.AreEqual(stats.yMax, inv.yMax, unit, "the same top");
            Assert.AreEqual(Screen.width * 0.5f - inv.xMax, stats.xMin - Screen.width * 0.5f, unit, "mirrored about the centre");
            Assert.LessOrEqual(inv.height, stats.height + unit, "never taller than the stats column");
            var first = World(_cards.CardRect(0));
            var last = World(_cards.CardRect(_lvl.ChoiceCount - 1));
            Assert.LessOrEqual(inv.xMax, first.xMin, "the inventory ends before the first card");
            Assert.LessOrEqual(last.xMax, stats.xMin, "the last card ends before the stats");
            Assert.GreaterOrEqual(inv.xMin, 0f, "on the screen");
            Assert.LessOrEqual(stats.xMax, Screen.width, "on the screen");
            _lvl.Choose(0);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator SelectingAnOwnedUpgrade_LightsItsSlot_WithItsTooltip()
        {
            yield return Boot();
            var skin = Find("thick_skin");
            Assert.IsNotNull(skin);
            _build.Take(new CardOffer { Def = skin, Rarity = Rarity.Common, Level = 1 });
            BanishAllBut(d => d == skin); // its level 2, then filler cards
            yield return OpenCards();
            int at = OfferOf(skin);
            Assert.GreaterOrEqual(at, 0, "it is offered again");
            _cards.Hover(at);
            yield return Frames(2);
            Assert.AreEqual(skin, _inv.SlotDef(InventoryPanel.WeaponSlots), "the Boon fills the first Boon slot");
            Assert.AreEqual(InventoryPanel.WeaponSlots, _inv.LitSlot, "its slot lights");
            Assert.AreEqual(-1, _inv.GhostSlot, "no ghost: you own it");
            Assert.IsTrue(_inv.TooltipShown, "its tooltip shows");
            Assert.AreEqual(InventoryPanel.WeaponSlots, _inv.TooltipSlot);
            Assert.AreEqual(skin.displayName, _inv.TooltipTitle);
            StringAssert.Contains("LV 1", _inv.TooltipText);
            StringAssert.Contains("COMMON", _inv.TooltipText);

            // a filler card: no slot lights, no tooltip
            int other = at == 0 ? 1 : 0;
            if (other < _lvl.ChoiceCount && _lvl.Offers[other].Def.kind == UpgradeKind.Consumable)
            {
                _cards.Hover(other);
                yield return Frames(2);
                Assert.AreEqual(-1, _inv.LitSlot, "supplies light nothing");
                Assert.IsFalse(_inv.TooltipShown);
            }
            _lvl.Choose(0);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator SelectingANewBoon_ShowsItsGhost_InTheNextFreeSlot()
        {
            yield return Boot();
            UpgradeDef boon = null;
            foreach (var d in _build.catalog.upgrades)
                if (d != null && d.kind == UpgradeKind.Boon && !d.IsVow && d.evolvesFrom == null && (d.requiresAny == null || d.requiresAny.Length == 0) &&
                    _build.State.LevelOf(d) == 0 && !_build.State.IsLocked(d)) { boon = d; break; }
            Assert.IsNotNull(boon, "a new Boon to offer");
            BanishAllBut(d => d == boon);
            yield return OpenCards();
            int at = OfferOf(boon);
            Assert.GreaterOrEqual(at, 0, "the new Boon is offered");
            _cards.Hover(at);
            yield return Frames(2);
            Assert.AreEqual(InventoryPanel.WeaponSlots, _inv.GhostSlot, "its ghost in the first free Boon slot");
            Assert.IsTrue(_inv.GhostShowing);
            Assert.Less(_inv.GhostColor.a, 1f, "translucent");
            Assert.AreEqual(-1, _inv.LitSlot);
            Assert.IsFalse(_inv.SlotFilled(InventoryPanel.WeaponSlots), "the slot is still free");
            Assert.IsTrue(_inv.TooltipShown, "the card's tooltip shows on the ghost");
            Assert.AreEqual(boon.displayName, _inv.TooltipTitle);
            StringAssert.Contains("NEW", _inv.TooltipText);
            _lvl.Choose(at);
            yield return Frames(2);
            yield return OpenCards(); // filler cards now: the Boon is in its slot
            Assert.AreEqual(boon, _inv.SlotDef(InventoryPanel.WeaponSlots), "taken: it fills the slot its ghost was in");
            Assert.AreEqual("1/6", _inv.BoonsCountText);
            _lvl.Choose(0);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator SelectingANewWeapon_ShowsItsGhost_NextToTheStartingWeapon()
        {
            yield return Boot();
            var start = StartingWeaponCard();
            UpgradeDef weapon = null;
            foreach (var d in _build.catalog.upgrades)
                if (d != null && d.kind == UpgradeKind.Weapon && d != start && d.evolvesFrom == null && _build.State.LevelOf(d) == 0 &&
                    !_build.State.IsLocked(d) && d.icon != null) { weapon = d; break; }
            if (weapon == null) Assert.Ignore("no second weapon in the catalogue");
            BanishAllBut(d => d == weapon);
            yield return OpenCards();
            int at = OfferOf(weapon);
            Assert.GreaterOrEqual(at, 0, "the new weapon is offered");
            _cards.Hover(at);
            yield return Frames(2);
            Assert.AreEqual(1, _inv.GhostSlot, "the next free weapon slot");
            StringAssert.Contains("NEW WEAPON", _inv.TooltipText);
            // the stats column doesn't make room for it (note 19): no weapon lines in the page
            StringAssert.DoesNotContain("attacks/s", _page.Text);
            StringAssert.DoesNotContain(weapon.displayName + " hit", _page.Text);
            _lvl.Choose(0);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator HoveringASlot_ShowsItsTooltip_OnTheScreen()
        {
            yield return Boot();
            yield return OpenCards();
            var gladius = StartingWeaponCard();
            _inv.HoverSlot(0, true);
            yield return Frames(2);
            Assert.IsTrue(_inv.TooltipShown, "hovering a slot shows its tooltip");
            Assert.AreEqual(0, _inv.TooltipSlot);
            Assert.AreEqual(gladius.displayName, _inv.TooltipTitle);
            StringAssert.Contains("LV 1", _inv.TooltipText);
            StringAssert.Contains("hit ", _inv.TooltipText, "a weapon's tooltip has its hit and rate (they left the stats page)");
            Canvas.ForceUpdateCanvases();
            var tip = World(_inv.Tooltip.Root);
            Assert.GreaterOrEqual(tip.xMin, 0f, "on the screen");
            Assert.LessOrEqual(tip.xMax, Screen.width + 0.5f);
            Assert.GreaterOrEqual(tip.yMin, -0.5f);
            Assert.LessOrEqual(tip.yMax, Screen.height + 0.5f);
            var inv = World(_inv.Root);
            Assert.LessOrEqual(tip.yMax, inv.yMin + 0.5f, "under the column: it never covers a slot");
            _inv.HoverSlot(0, false);
            _inv.HoverSlot(5, true); // an empty well
            yield return Frames(2);
            Assert.AreNotEqual(5, _inv.TooltipSlot, "an empty well says nothing");
            _inv.HoverSlot(5, false);
            _lvl.Choose(0);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Pause_ShowsTheInventoryStatsAndPlates_ResumeResumes()
        {
            yield return Boot();
            if (_lvl != null) _lvl.enabled = false;
            Player.Main.SetPaused(true);
            yield return Frames(2);
            Assert.IsTrue(_cards.PauseShown, "the pause is the level-up's frame");
            Assert.AreEqual(LevelUpPanel.PausedWord, _cards.TitleText);
            Assert.IsTrue(_inv.Visible, "the inventory on the left");
            Assert.AreEqual(StatsPanel.Mode.Pause, _page.Current, "the stats on the right");
            Assert.AreEqual((int)FrameKind.Bronze, _page.FrameShown, "the same bronze column as the level-up's");
            string[] words = { "Resume", "Options", "Main Menu" }, keys = { "ESC", "O", "Q" };
            Canvas.ForceUpdateCanvases();
            var inv = World(_inv.Root);
            var stats = World(_page.Page);
            float unit = _cards.CanvasRect.lossyScale.x;
            for (int i = 0; i < LevelUpPanel.PausePlates; i++)
            {
                Assert.AreEqual(words[i], _cards.PauseLabel(i));
                Assert.AreEqual(keys[i], _cards.PauseKey(i));
                var r = World(_cards.PausePlateRect(i));
                Assert.AreEqual(Screen.width * 0.5f, r.center.x, unit, words[i] + " is centred");
                Assert.Less(inv.xMax, r.xMin, words[i] + " is right of the inventory");
                Assert.Less(r.xMax, stats.xMin, words[i] + " is left of the stats");
                if (i > 0) Assert.Less(r.yMax, World(_cards.PausePlateRect(i - 1)).yMin, "stacked, top to bottom");
            }
            Assert.AreEqual(0, _cards.PauseFocus, "RESUME is focused first");
            Assert.AreEqual(UiState.Focused, _cards.PausePlate(0).State);

            // a stat row's tooltip: where it comes from
            _page.HoverRow(StatId.MaxHp, true);
            yield return Frames(2);
            Assert.IsTrue(_page.TooltipShown, "hovering a stat row shows its sources");
            StringAssert.Contains("Base", _page.TooltipText);
            var tip = World(_page.Tooltip.Root);
            Assert.LessOrEqual(tip.xMax, stats.xMin + unit, "left of the column");
            _page.HoverRow(StatId.MaxHp, false);
            yield return Frames(2);
            Assert.IsFalse(_page.TooltipShown);

            yield return new WaitForSecondsRealtime(0.25f); // the pause's grace
            _cards.Click(LevelUpPanel.ResumeId);
            yield return Frames(2);
            Assert.IsFalse(Player.Main.IsPaused, "RESUME resumes");
            Assert.IsFalse(_cards.PauseShown);
            Assert.IsFalse(_inv.Visible);
            Assert.AreEqual(StatsPanel.Mode.Hidden, _page.Current);
        }

        /// <summary>Allocation-free in a live run: once every buffer has grown (each card and slot hovered once), moving
        /// between cards and slots, the stats preview, the ghost and the tooltips allocate nothing (ticked here directly,
        /// so nothing else in the frame counts).</summary>
        [UnityTest, Timeout(60000)]
        public IEnumerator HoveringCardsAndSlots_AllocatesNothing()
        {
            yield return Boot();
            yield return OpenCards();
            int n = _lvl.ChoiceCount;
            void Cycle()
            {
                for (int i = 0; i < n; i++)
                {
                    _cards.Hover(i);
                    _cards.Tick(0f, 0.016f);
                    _page.Tick(0f, 0.016f);
                }
                for (int s = 0; s < InventoryPanel.Slots; s++)
                {
                    _inv.HoverSlot(s, true);
                    _cards.Tick(0f, 0.016f);
                    _inv.HoverSlot(s, false);
                    _cards.Tick(0f, 0.016f);
                }
            }
            Cycle(); // grows every buffer
            Cycle();
            long before = GC.GetAllocatedBytesForCurrentThread();
            Cycle();
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Debug.Log($"[P3] a cycle of hovers over {n} cards and {InventoryPanel.Slots} slots allocated {allocated} B");
            Assert.AreEqual(0, allocated, "hovering allocated");
            _lvl.Choose(0);
        }
    }
}
