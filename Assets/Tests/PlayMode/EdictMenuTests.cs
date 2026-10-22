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
    /// R7-C (docs/tracks/R7-contract.md, "R7-C menu"): the Edicts on the real main menu and in a run. Hidden until The
    /// Editor Takes Notice; then the arena screen always shows, and its EDICTS button opens a page with a card per edict:
    /// the three free ones toggle, a Market one is locked until bought (Renown spent; short of it, TooExpensive), a
    /// challenge one never toggles; the button, the footer and the arena tablet show the multiplier; closing saves the
    /// choice and FIGHT! takes it; the daily's read-only page refuses toggles and saves nothing; the keyboard / gamepad
    /// moves, toggles, buys, clears and closes. In the test arena the HUD shows one icon per edict and the end screen's
    /// Renown row the multiplier. Runs on the data EdictSetup makes (Gladiator/Setup/Meta/Create Edict Data); the save is
    /// the sandbox's (SaveSandbox). LogAssert.NoUnexpectedReceived (which fails on any log, even "[Meta] bought ...") only
    /// closes the tests that buy, save and unlock nothing: there it catches TMP warnings (an Ellipsis, a missing glyph).
    /// </summary>
    public class EdictMenuTests
    {
        const string Arena = "Assets/Scenes/Dev/TestArena.unity";

        MainMenu _menu;

        [TearDown]
        public void TearDown()
        {
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
                Assert.IsNotNull(m.edicts, "MetaConfig.edicts (run Gladiator/Setup/Meta/Create Edict Data)");
                Assert.IsNotNull(m.edictsUnlock, "MetaConfig.edictsUnlock (UL_Edicts)");
                return m;
            }
        }

        static RunModifierDef Edict(string id)
        {
            foreach (var e in Meta.edicts) if (e != null && e.id == id) return e;
            Assert.Fail("no edict " + id + " in MetaConfig.edicts");
            return null;
        }

        /// <summary>The Edicts earned: The Editor Takes Notice is in the save.</summary>
        static void EarnEdicts(SaveData d) => d.unlocked.Add(Meta.edictsUnlock.id);

        static void Invoke(object target, string method) =>
            target.GetType().GetMethod(method, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(target, null);

        IEnumerator Boot(Action<SaveData> stage = null)
        {
            SaveSandbox.Wipe();
            stage?.Invoke(MetaProgress.Data);
            SaveSystem.Save(MetaProgress.Data);
            yield return LoadMenu(); // the screens are built from the save on Awake
        }

        IEnumerator LoadMenu()
        {
            var op = SceneManager.LoadSceneAsync(GameSession.MenuScene, LoadSceneMode.Single);
            while (!op.isDone) yield return null;
            yield return null;
            _menu = Object.FindAnyObjectByType<MainMenu>();
            Assert.IsNotNull(_menu);
            Assert.IsNotNull(_menu.Edicts, "the menu built its Edicts page");
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

        /// <summary>Gladiator screen, then FIGHT!: the arena screen (it always shows once the Edicts are earned).</summary>
        IEnumerator ToArenaScreen()
        {
            Invoke(_menu, "OpenSelect");
            yield return null;
            Invoke(_menu, "StartRun");
            yield return null;
            yield return null; // the canvas scaler has scaled the canvas
            Canvas.ForceUpdateCanvases();
        }

        static IEnumerator WaitRealtime(Func<bool> done, float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!done() && Time.realtimeSinceStartup < end) yield return null;
        }

        // ------------------------------------------------------------------ the menu

        [UnityTest, Timeout(60000)]
        public IEnumerator BeforeTheEditorTakesNotice_ThereIsNoButton_AndNoPage()
        {
            yield return Boot();
            Assert.IsFalse(_menu.EdictsButtonShown, "the Edicts are unknown until Gorm falls");
            Assert.IsFalse(_menu.AsksForArena, "one arena and no Edicts: FIGHT! goes straight in");
            _menu.OpenEdicts();
            Assert.IsFalse(_menu.Edicts.IsOpen, "no page before the Edicts are earned");
            CollectionAssert.IsEmpty(_menu.EdictsForLaunch(), "nothing chosen, nothing applied");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Earned_TheButtonShowsOnTheArenaScreen_AndOpensACardPerEdict()
        {
            yield return Boot(EarnEdicts);
            int n = 0;
            foreach (var e in Meta.edicts) if (e != null) n++;
            Assert.GreaterOrEqual(n, 11, "the eleven edicts of the spec (§2.3)");
            Assert.IsTrue(_menu.EdictsButtonShown);
            Assert.IsTrue(_menu.AsksForArena, "the arena screen hosts the button, even with one arena earned");
            Assert.AreEqual("EDICTS", _menu.EdictsButtonText, "none on: just the word");
            Assert.AreEqual(n, _menu.Edicts.Count, "a card per edict, in MetaConfig.edicts order");
            Assert.AreEqual("iron_hides", _menu.Edicts.Def(0).id, "the spec's order");
            Assert.AreEqual("hungry_horde", _menu.Edicts.Def(1).id);

            yield return ToArenaScreen();
            StringAssert.Contains("E / Y: edicts", _menu.ArenaKeysText);
            var button = _menu.transform.Find("MenuCanvas/Arena/Edicts") as RectTransform;
            Assert.IsNotNull(button, "the EDICTS button");
            AssertOnScreen(button, "the EDICTS button");
            Click(button); // the mouse opens it
            yield return null;
            Canvas.ForceUpdateCanvases();
            var p = _menu.Edicts;
            Assert.IsTrue(p.IsOpen, "a click on EDICTS opens the page");
            Assert.IsFalse(p.ReadOnly);
            Assert.AreEqual("THE EDITOR'S EDICTS", p.HeaderText);
            AssertOnScreen(p.Box, "the page");
            for (int i = 0; i < p.Count; i++) AssertOnScreen(p.CardRect(i), "card " + p.Def(i).id);
            StringAssert.Contains("NO EDICTS", p.FooterText);
            StringAssert.Contains("Esc / B: done", p.KeysText);

            // a click on a free card turns it on: an orange-red frame (it hurts you)
            Click(p.CardRect(0));
            Assert.IsTrue(p.IsOn(0), "a click toggles a free edict");
            Assert.AreEqual(EdictsPanel.Hurt, p.CardFrame(0));
            Click(p.CardRect(0));
            Assert.IsFalse(p.IsOn(0), "a second click turns it off");
            LogAssert.NoUnexpectedReceived(); // no TMP warnings (no Ellipsis, no missing glyphs)
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator FreeOnesToggle_AMarketOneIsBoughtFirst_AChallengeOneNeverToggles()
        {
            yield return Boot(d => { EarnEdicts(d); d.renown = 400; });
            var p = _menu.Edicts;
            _menu.OpenEdicts();
            Assert.IsTrue(p.IsOpen);
            foreach (var id in new[] { "iron_hides", "hungry_horde", "blood_sport" })
            {
                int i = p.IndexOf(id);
                Assert.GreaterOrEqual(i, 0, id);
                Assert.IsFalse(p.IsLocked(i), id + " opens with the Edicts");
                Assert.IsTrue(p.Toggle(i), id + " toggles on");
                Assert.IsTrue(p.IsOn(i));
                Assert.IsTrue(p.Toggle(i), id + " toggles off");
                Assert.IsFalse(p.IsOn(i));
            }

            int jaw = p.IndexOf("glass_jaw");
            var u = p.Def(jaw).unlock;
            Assert.IsNotNull(u, "Glass Jaw is sold in the Market");
            Assert.AreEqual(UnlockCategory.Market, u.category);
            Assert.IsTrue(p.IsLocked(jaw), "not before it's bought");
            Assert.IsFalse(p.Toggle(jaw));
            Assert.IsFalse(p.IsOn(jaw));
            StringAssert.StartsWith("BUY", p.CardState(jaw), "affordable: BUY · price");
            Assert.AreEqual(p.Def(jaw).effectLine, p.CardLine(jaw), "a Market card is lit: you see what you buy");
            int before = MetaProgress.Data.renown;
            Assert.AreEqual(BuyResult.Bought, p.Buy(jaw));
            Assert.AreEqual(before - u.renownCost, MetaProgress.Data.renown, "the price is spent");
            Assert.IsTrue(p.BoughtAny);
            StringAssert.Contains(MetaProgress.Data.renown.ToString(), p.RenownText, "the balance follows");
            Assert.IsFalse(p.IsLocked(jaw));
            Assert.IsFalse(p.IsOn(jaw), "buying never turns a handicap on by itself");
            Assert.IsTrue(p.Toggle(jaw), "bought: it toggles");
            Assert.AreEqual(BuyResult.AlreadyOwned, p.Buy(jaw));
            CollectionAssert.Contains(SavedFile().unlocked, u.id, "the purchase is saved at once");

            int meagre = p.IndexOf("meagre_choices");
            var cu = p.Def(meagre).unlock;
            Assert.IsNotNull(cu);
            Assert.AreEqual(UnlockCategory.Challenge, cu.category, "Meagre Choices is earned (Under the Editor's Eye)");
            Assert.IsTrue(p.IsLocked(meagre));
            Assert.IsFalse(p.Toggle(meagre), "a challenge edict never toggles before it's earned");
            Assert.AreEqual(BuyResult.NotForSale, p.Buy(meagre));
            Assert.IsFalse(p.IsOn(meagre));
            Assert.AreEqual(cu.hint, p.CardLine(meagre), "a locked challenge card shows its unlock's hint");

            p.Close();
            yield return null;
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator AMarketEdict_WithoutTheRenown_IsTooExpensive()
        {
            yield return Boot(d => { EarnEdicts(d); d.renown = 10; });
            var p = _menu.Edicts;
            _menu.OpenEdicts();
            int foes = p.IndexOf("swift_foes");
            Assert.IsTrue(p.IsLocked(foes));
            Assert.AreEqual(BuyResult.TooExpensive, p.Buy(foes));
            Assert.AreEqual(10, MetaProgress.Data.renown, "nothing spent");
            Assert.IsTrue(p.IsLocked(foes), "still locked");
            Assert.IsFalse(p.BoughtAny);
            StringAssert.DoesNotContain("BUY", p.CardState(foes), "short of Renown: the price in grey");
            StringAssert.Contains("RENOWN", p.CardState(foes));
            p.Close();
            yield return null;
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator TwoOn_TheButtonAndTabletShowTheMultiplier_CloseSavesThem_AndFightTakesThem()
        {
            yield return Boot(EarnEdicts);
            yield return ToArenaScreen();
            var p = _menu.Edicts;
            _menu.OpenEdicts();
            int a = p.IndexOf("iron_hides"), b = p.IndexOf("blood_sport");
            Assert.IsTrue(p.Toggle(a));
            Assert.IsTrue(p.Toggle(b));
            string mul = EdictsPanel.MulText(1f + p.Def(a).renownBonus + p.Def(b).renownBonus);
            Assert.AreEqual("×1.45", mul, "the spec's numbers: +25 % and +20 %");
            Assert.AreEqual(1.45f, p.RenownMul, 1e-4f);
            Assert.AreEqual("EDICTS · RENOWN ×1.45", _menu.EdictsButtonText, "the button follows the page live");
            StringAssert.Contains("2 EDICTS", p.FooterText);
            StringAssert.Contains(mul, p.FooterText);
            var line = _menu.ArenaTablet.EdictsText;
            StringAssert.Contains(p.Def(a).displayName, line, "the tablet names them");
            StringAssert.Contains(p.Def(b).displayName, line);
            StringAssert.Contains("RENOWN " + mul, line);

            Assert.IsTrue(p.HandleInput(false, false, false, false, false, true, false), "Esc / B closes");
            Assert.IsFalse(p.IsOpen);
            CollectionAssert.AreEquivalent(new[] { "iron_hides", "blood_sport" }, SavedFile().edictsChosen, "closing saves the choice");
            CollectionAssert.AreEqual(new[] { p.Def(a), p.Def(b) }, _menu.EdictsForLaunch(), "FIGHT! takes the chosen, in the page's order");

            yield return LoadMenu(); // remembered between visits
            Assert.AreEqual("EDICTS · RENOWN ×1.45", _menu.EdictsButtonText);
            _menu.OpenEdicts();
            Assert.IsTrue(_menu.Edicts.IsOn(a) && _menu.Edicts.IsOn(b), "the page opens on the saved choice");
            _menu.Edicts.Close();
            yield return null;
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Keys_MoveToggleBuyClearAndClose()
        {
            yield return Boot(d => { EarnEdicts(d); d.renown = 400; });
            var p = _menu.Edicts;
            _menu.OpenEdicts();
            Assert.AreEqual(0, p.Focus, "the page opens on the first card");
            p.HandleInput(false, false, false, false, true, false, false); // Enter / A: Iron Hides on
            Assert.IsTrue(p.IsOn(0));
            p.HandleInput(false, true, false, false, false, false, false); // right
            Assert.AreEqual(1, p.Focus);
            // P3: the focus is a brighter, lifted card (no gold ring)
            Assert.IsTrue(p.CardBright(1) && p.CardLifted(1), "the focused card brightens and lifts");
            Assert.IsFalse(p.CardLifted(0), "the card it left settles");
            p.HandleInput(false, false, false, true, false, false, false); // down
            Assert.AreEqual(1 + EdictsPanel.Columns, p.Focus);
            p.HandleInput(false, false, true, false, false, false, false); // up
            Assert.AreEqual(1, p.Focus);
            p.HandleInput(false, false, false, false, true, false, false); // the Hungry Horde on
            Assert.IsTrue(p.IsOn(1));
            StringAssert.Contains("2 EDICTS", p.FooterText);

            int jaw = p.IndexOf("glass_jaw");
            while (p.Focus != jaw) // walk there: down the right column or along the row
            {
                int before = p.Focus;
                if (p.Focus / EdictsPanel.Columns < jaw / EdictsPanel.Columns) p.HandleInput(false, false, false, true, false, false, false);
                else p.HandleInput(p.Focus > jaw, p.Focus < jaw, false, false, false, false, false);
                Assert.AreNotEqual(before, p.Focus, "the cursor reaches Glass Jaw");
            }
            p.HandleInput(false, false, false, false, true, false, false); // Enter on a locked Market card buys it
            Assert.IsFalse(p.IsLocked(jaw), "bought");
            Assert.IsFalse(p.IsOn(jaw));

            p.HandleInput(false, false, false, false, false, false, true); // X / Backspace
            for (int i = 0; i < p.Count; i++) Assert.IsFalse(p.IsOn(i), "cleared: " + p.Def(i).id);
            StringAssert.Contains("NO EDICTS", p.FooterText);
            Assert.AreEqual("EDICTS", _menu.EdictsButtonText);
            Assert.IsTrue(p.HandleInput(false, false, false, false, false, true, false), "Esc / B closes");
            CollectionAssert.IsEmpty(SavedFile().edictsChosen, "an empty choice is saved too");
            yield return null;
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator TheDailysPage_IsReadOnly_AndSavesNothing()
        {
            yield return Boot(EarnEdicts);
            var p = _menu.Edicts;
            var forced = new[] { Edict("swift_foes"), Edict("champions_day") }; // not owned: the daily proclaims them anyway
            p.Open(forced);
            Assert.IsTrue(p.IsOpen);
            Assert.IsTrue(p.ReadOnly);
            Assert.AreEqual("PROCLAIMED BY THE EDITOR", p.HeaderText);
            StringAssert.Contains("Esc / B", p.KeysText);
            int foes = p.IndexOf("swift_foes"), iron = p.IndexOf("iron_hides");
            Assert.IsTrue(p.IsOn(foes));
            Assert.IsFalse(p.IsOn(iron));
            // P3 (the producer's note 5): nothing to press on the read-only page
            Assert.AreEqual(EdictsPanel.TodaysEdict, p.CardState(foes), "the day's edicts say so");
            Assert.IsTrue(p.OnRingShown(foes), "and keep their orange-red ring");
            for (int i = 0; i < p.Count; i++)
            {
                Assert.IsFalse(p.ChipShown(i), p.Def(i).id + ": no ON chip on the read-only page");
                Assert.IsFalse(p.PadlockShown(i), p.Def(i).id + ": no padlock");
                if (!p.IsOn(i)) Assert.AreEqual("", p.CardState(i), p.Def(i).id + ": no OFF / LOCKED / progress");
                Assert.IsFalse(p.CardLifted(i), "no focus on the read-only page");
            }
            Assert.IsFalse(p.Toggle(iron), "read-only: nothing toggles on");
            Assert.IsFalse(p.Toggle(foes), "read-only: nothing toggles off");
            p.ClearAll();
            Assert.IsTrue(p.IsOn(foes), "read-only: nothing clears");
            Assert.AreEqual(BuyResult.NotForSale, p.Buy(foes), "read-only: nothing is sold");
            CollectionAssert.AreEqual(forced, p.Chosen);
            float bonus = forced[0].renownBonus + forced[1].renownBonus;
            Assert.AreEqual(1f + bonus, p.RenownMul, 1e-4f);
            Assert.IsTrue(p.HandleInput(false, false, false, false, false, true, false));
            Assert.IsFalse(p.IsOpen);
            CollectionAssert.IsEmpty(MetaProgress.Data.edictsChosen, "the daily's edicts aren't the player's choice");
            Assert.AreEqual("EDICTS", _menu.EdictsButtonText);
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator TheCodex_NamesTheEdictsAnUnlockOpens_AndRecordsTheMostInAWin()
        {
            yield return Boot(d => d.bestEdictWin = 3);
            var codex = _menu.Codex;
            var meta = Meta;
            string notice = codex.RewardText(meta.edictsUnlock);
            StringAssert.Contains("Edicts — ", notice, "The Editor Takes Notice opens several");
            foreach (var e in meta.edicts)
                if (e != null && e.unlock == null) StringAssert.Contains(e.displayName, notice, "it opens " + e.id);
            var jaw = Edict("glass_jaw");
            StringAssert.Contains("Edict — " + jaw.displayName, codex.RewardText(jaw.unlock), "a Market edict's unlock names it");

            codex.Select(CodexPanel.Tab.Records);
            yield return null;
            Assert.AreEqual("3", codex.RecordValue("Most edicts in a win"), "RECORDS shows SaveData.bestEdictWin");
        }

        // ------------------------------------------------------------------ the run

        IEnumerator BootArena(RunModifierDef[] edicts)
        {
            SaveSandbox.Wipe();
            DevSettings.ResetAll();
            TimeController.GameSpeed = 1f;
            TimeController.ResetForNewRun();
            RunModifiers.Set(edicts);
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

        [UnityTest, Timeout(60000)]
        public IEnumerator UnderTwoEdicts_TheHudShowsTwoIcons_AndTheEndScreenTheMultiplier()
        {
            var edicts = new[] { Edict("iron_hides"), Edict("blood_sport") };
            yield return BootArena(edicts);
            var hud = Object.FindAnyObjectByType<Hud>();
            Assert.IsNotNull(hud);
            Assert.AreEqual(2, hud.EdictIconCount, "one icon per edict");
            Assert.IsTrue(hud.EdictRowShown);
            Assert.AreEqual(2, hud.EdictRow.childCount);
            AssertOnScreen(hud.EdictRow, "the edict row");

            yield return EndRun();
            var rewards = RunEndController.Instance.Rewards;
            Assert.Greater(rewards.EdictBonus, 0f, "the summary carried the edicts' bonus");
            var panel = Object.FindAnyObjectByType<RunEndPanel>();
            Assert.IsNotNull(panel);
            StringAssert.Contains("×", panel.RenownText, "the Renown row shows the multiplier");
            StringAssert.Contains(EdictsPanel.MulText(1f + rewards.EdictBonus), panel.RenownText);
            StringAssert.Contains("= +" + rewards.Renown, panel.RenownText);
            Assert.AreEqual(RunEndPanel.RenownLine(rewards), panel.RenownText);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator WithoutEdicts_TheHudHasNoRow_AndTheRenownRowReadsAsBefore()
        {
            yield return BootArena(Array.Empty<RunModifierDef>());
            var hud = Object.FindAnyObjectByType<Hud>();
            Assert.AreEqual(0, hud.EdictIconCount);
            Assert.IsFalse(hud.EdictRowShown);

            yield return EndRun();
            var rewards = RunEndController.Instance.Rewards;
            var panel = Object.FindAnyObjectByType<RunEndPanel>();
            StringAssert.DoesNotContain("×", panel.RenownText);
            Assert.AreEqual("+" + rewards.Renown + (rewards.Bounty > 0 ? " +" + rewards.Bounty : "") + "  (" + rewards.RenownTotal + ")", panel.RenownText,
                "today's line, unchanged");
        }
    }
}
