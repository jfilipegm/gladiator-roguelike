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
using Object = UnityEngine.Object;

namespace Gladiator.Tests
{
    /// <summary>
    /// R6-C (docs/tracks/R7-contract.md, "R6-C menu"): the skins on the real main menu. The strip under the diorama shows
    /// the Nobody's classic look and his skins; cycling (its arrows, CycleSkin) changes the diorama's model in the one
    /// shared material; a Market skin shows its price and BUY owns and equips it (short of Renown: TooExpensive, nothing
    /// changes); a champion skin shows its goal and can't be bought or equipped before its wins; the equipped skin
    /// survives a menu reload; FIGHT! and the daily's FIGHT! set GameSession.Skin to the equipped skin, never the preview;
    /// the roster tile shows the equipped skin's portrait; the Codex names the skin an unlock gives.
    /// Runs on the data SkinSetup makes (Gladiator/Setup/Gladiators/Create Skin Data) and the portraits PortraitBaker bakes
    /// (Gladiator/Setup/UI/Bake Portraits); the save is the sandbox's (SaveSandbox). GameSession.SuppressLoad keeps FIGHT!
    /// from loading the arena (it records GameSession.LastLoadPath). LogAssert.NoUnexpectedReceived fails on any log, so
    /// only the tests that buy, equip and save nothing end with it.
    /// </summary>
    public class SkinMenuTests
    {
        MainMenu _menu;

        [SetUp]
        public void SetUp() => GameSession.SuppressLoad = true;

        [TearDown]
        public void TearDown()
        {
            GameSession.Clear();
            GameSession.SuppressLoad = false;
            GameSession.Skin = null;
            RunModifiers.Clear();
            TimeController.ResetForNewRun();
            SaveSandbox.Wipe(); // later tests expect a fresh save
        }

        // ------------------------------------------------------------------ data

        static MetaConfig Meta
        {
            get
            {
                var m = AssetDatabase.LoadAssetAtPath<MetaConfig>("Assets/Data/Meta/MetaConfig.asset");
                Assert.IsNotNull(m, "MetaConfig");
                Assert.IsNotNull(m.skins, "MetaConfig.skins");
                Assert.Greater(m.skins.Length, 0, "MetaConfig.skins (run Gladiator/Setup/Gladiators/Create Skin Data)");
                return m;
            }
        }

        static GladiatorDef Nobody
        {
            get
            {
                var g = AssetDatabase.LoadAssetAtPath<GladiatorDef>("Assets/Data/Gladiators/GL_Nobody.asset");
                Assert.IsNotNull(g, "GL_Nobody");
                Assert.IsNull(g.unlock, "the Nobody is earned from the start");
                return g;
            }
        }

        static List<SkinDef> SkinsOf(GladiatorDef g)
        {
            var list = new List<SkinDef>();
            MetaProgress.SkinsOf(g, Meta, list);
            Assert.GreaterOrEqual(list.Count, 2, g.id + " has a Market skin and a champion skin (spec §4.4)");
            return list;
        }

        /// <summary>The Nobody's Market skin (the Freedman in the spec).</summary>
        static SkinDef MarketSkin()
        {
            foreach (var s in SkinsOf(Nobody))
                if (s.unlock != null && s.unlock.category == UnlockCategory.Market) return s;
            Assert.Fail("the Nobody has no Market skin");
            return null;
        }

        /// <summary>The Nobody's champion skin (Unbowed): a challenge to win N runs with him.</summary>
        static SkinDef ChampionSkin()
        {
            foreach (var s in SkinsOf(Nobody))
                if (s.unlock != null && s.unlock.category == UnlockCategory.Challenge && s.unlock.conditions.Length == 1 &&
                    s.unlock.conditions[0].kind == UnlockConditionKind.WinWithGladiator && s.unlock.conditions[0].id == "nobody")
                    return s;
            Assert.Fail("the Nobody has no champion skin");
            return null;
        }

        static int Wins(SkinDef champion) => Mathf.RoundToInt(champion.unlock.conditions[0].amount);

        static void Own(SaveData d, SkinDef s) => d.unlocked.Add(s.unlock.id);

        static SaveData SavedFile() => JsonUtility.FromJson<SaveData>(File.ReadAllText(SaveSandbox.TestPath));

        // ------------------------------------------------------------------ the menu

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
            Assert.IsNotNull(_menu.Skins, "the menu built its skin strip");
        }

        /// <summary>The gladiator screen on the Nobody, the canvas scaled.</summary>
        IEnumerator ToSelect()
        {
            Invoke(_menu, "OpenSelect");
            Assert.IsTrue(_menu.Pick(Nobody), "the Nobody is earned");
            yield return null;
            yield return null; // the canvas scaler has scaled the canvas
            Canvas.ForceUpdateCanvases();
        }

        /// <summary>Cycles the strip forward to `s` (asserts it gets there within one lap).</summary>
        void CycleTo(SkinDef s)
        {
            var strip = _menu.Skins;
            for (int i = 0; i < strip.Count && strip.Current != s; i++) Assert.IsTrue(_menu.CycleSkin(1), "the strip cycles");
            Assert.AreSame(s, strip.Current, "the strip reaches " + (s != null ? s.id : "the classic look"));
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

        int NobodySlot()
        {
            for (int i = 0; i < _menu.Roster.Count; i++) if (_menu.Roster.Def(i) == Nobody) return i;
            Assert.Fail("the Nobody has no tile");
            return -1;
        }

        // ------------------------------------------------------------------ tests

        [UnityTest, Timeout(60000)]
        public IEnumerator TheStrip_SitsUnderTheDiorama_AndCyclingChangesTheModel()
        {
            yield return Boot();
            yield return ToSelect();
            var strip = _menu.Skins;
            var nobody = Nobody;
            var skins = SkinsOf(nobody);
            Assert.IsTrue(strip.Shown, "the Nobody has skins: the strip is up");
            Assert.AreSame(nobody, strip.Gladiator);
            Assert.AreEqual(1 + skins.Count, strip.Count, "his classic look, then his skins");
            Assert.AreEqual(0, strip.Index, "a fresh save: the classic look");
            Assert.IsNull(strip.Current);
            Assert.AreEqual(SkinStrip.ClassicName + " · 1 / " + strip.Count, strip.NameText);
            Assert.AreEqual("EQUIPPED", strip.StatusText);
            Assert.AreEqual(0, strip.ChipCount, "the classic look has no chips");
            Assert.AreSame(nobody.model, _menu.HeroPrefab, "the diorama shows his own model");
            StringAssert.Contains("Q / E: skin", _menu.SelectKeysText);

            // under the diorama: on screen, between BACK and FIGHT!, covering neither
            AssertOnScreen(strip.Root, "the strip");
            var back = _menu.transform.Find("MenuCanvas/Select/Back") as RectTransform;
            var fight = _menu.transform.Find("MenuCanvas/Select/Fight") as RectTransform;
            Assert.IsNotNull(back);
            Assert.IsNotNull(fight);
            Assert.IsFalse(ScreenRect(strip.Root).Overlaps(ScreenRect(back)), "the strip clears BACK");
            Assert.IsFalse(ScreenRect(strip.Root).Overlaps(ScreenRect(fight)), "the strip clears FIGHT!");
            Assert.AreEqual(Screen.width * 0.5f, ScreenRect(strip.Root).center.x, 1f, "centred under the diorama");
            Assert.Less(ScreenRect(strip.Root).yMax, Screen.height * 0.25f, "in the bottom band");

            // the mouse: the right arrow shows his first skin on the diorama
            Click(strip.NextRect);
            var first = skins[0];
            Assert.AreEqual(1, strip.Index);
            Assert.AreSame(first, strip.Current);
            Assert.AreEqual(first.displayName.ToUpperInvariant() + " · 2 / " + strip.Count, strip.NameText);
            Assert.IsNotNull(first.model, first.id + " has its model");
            Assert.AreNotSame(nobody.model, first.model, "a skin is its own FBX");
            Assert.AreSame(first.model, _menu.HeroPrefab, "the diorama wears the skin");
            Assert.IsTrue(_menu.HeroShown.activeInHierarchy);
            if (_menu.heroMaterial != null)
                foreach (var r in _menu.HeroShown.GetComponentsInChildren<Renderer>(true))
                    Assert.AreSame(_menu.heroMaterial, r.sharedMaterial, r.name + ": the one shared palette material");
            int chips = Mathf.Min(3, first.chips != null ? first.chips.Length : 0);
            Assert.AreEqual(chips, strip.ChipCount, "the skin's chips");
            for (int i = 0; i < chips; i++)
            {
                var want = first.chips[i];
                want.a = 1f;
                Assert.AreEqual(want, strip.Chip(i));
            }
            Assert.IsNull(MetaProgress.EquippedSkin(nobody, Meta), "a preview of an unowned skin equips nothing");

            // the left arrow and the keys' API: back to the classic look, then round the end to the last skin
            Click(strip.PrevRect);
            Assert.AreEqual(0, strip.Index);
            Assert.AreSame(nobody.model, _menu.HeroPrefab, "the classic look again");
            Assert.IsTrue(_menu.CycleSkin(-1));
            Assert.AreEqual(strip.Count - 1, strip.Index, "the strip wraps round");
            Assert.AreSame(skins[skins.Count - 1].model, _menu.HeroPrefab);
            Assert.IsTrue(_menu.CycleSkin(1));
            Assert.AreEqual(0, strip.Index, "and back");
            LogAssert.NoUnexpectedReceived(); // no TMP warnings (no Ellipsis, no missing glyph)
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator AMarketSkin_ShowsItsPrice_AndBuyOwnsAndEquipsIt()
        {
            var market = MarketSkin();
            int cost = market.unlock.renownCost;
            Assert.Greater(cost, 0, "a Market skin has a price");
            yield return Boot(d => d.renown = cost + 40);
            yield return ToSelect();
            var strip = _menu.Skins;
            CycleTo(market);
            Assert.IsFalse(strip.CurrentOwned);
            Assert.IsTrue(strip.BuyShown, "a Market skin shows BUY");
            Assert.IsTrue(strip.BuyEnabled, "affordable");
            Assert.AreEqual("BUY · " + cost + " RENOWN", strip.StatusText, "the spec's text (§4.1)");
            AssertOnScreen(strip.BuyRect, "BUY");
            Assert.IsNull(MetaProgress.EquippedSkin(Nobody, Meta), "not equipped before it's bought");

            Click(strip.BuyRect); // the mouse buys it
            Assert.IsTrue(MetaProgress.IsOwned(market), "bought");
            Assert.AreEqual(40, MetaProgress.Data.renown, "the price is spent");
            Assert.AreSame(market, MetaProgress.EquippedSkin(Nobody, Meta), "buying equips it");
            Assert.IsTrue(strip.CurrentOwned);
            Assert.IsFalse(strip.BuyShown);
            Assert.AreEqual("EQUIPPED", strip.StatusText);
            Assert.AreSame(market.model, _menu.HeroPrefab);
            var saved = SavedFile();
            CollectionAssert.Contains(saved.unlocked, market.unlock.id, "the purchase is saved at once");
            CollectionAssert.Contains(saved.equippedSkins, market.id, "and the choice");
            Assert.AreEqual(BuyResult.AlreadyOwned, _menu.BuySkin());
            Assert.AreEqual(40, MetaProgress.Data.renown, "nothing spent twice");
            Assert.AreSame(market.portrait != null ? market.portrait : Nobody.portrait, _menu.Roster.PortraitOf(NobodySlot()),
                "the tile follows at once");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator AMarketSkin_WithoutTheRenown_IsTooExpensive_AndNothingChanges()
        {
            var market = MarketSkin();
            yield return Boot(d => d.renown = 10);
            yield return ToSelect();
            var strip = _menu.Skins;
            CycleTo(market);
            Assert.IsTrue(strip.BuyShown);
            Assert.IsFalse(strip.BuyEnabled, "short of Renown: the button is off");
            StringAssert.Contains(market.unlock.renownCost + " RENOWN", strip.StatusText);
            StringAssert.Contains("NEED " + (market.unlock.renownCost - 10) + " MORE", strip.StatusText);
            Assert.AreEqual(BuyResult.TooExpensive, _menu.BuySkin());
            Assert.AreEqual(10, MetaProgress.Data.renown, "nothing spent");
            Assert.IsFalse(MetaProgress.IsOwned(market));
            Assert.IsNull(MetaProgress.EquippedSkin(Nobody, Meta), "nothing equipped");
            Assert.AreSame(market, strip.Current, "still previewed");
            CollectionAssert.DoesNotContain(SavedFile().unlocked, market.unlock.id);
            CollectionAssert.IsEmpty(SavedFile().equippedSkins);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator AChampionSkin_CantBeBoughtOrEquipped_BeforeItsWins()
        {
            var champion = ChampionSkin();
            int wins = Wins(champion);
            Assert.GreaterOrEqual(wins, 2, "the spec's champion skins: win 3 runs with him");
            yield return Boot(d => { d.renown = 5000; SaveData.Add(d.gladiatorWins, "nobody", 1); });
            yield return ToSelect();
            var strip = _menu.Skins;
            CycleTo(champion);
            Assert.IsFalse(strip.CurrentOwned);
            Assert.IsFalse(strip.BuyShown, "a champion skin isn't sold");
            Assert.AreEqual("Win " + wins + " runs as " + SkinStrip.AsName(Nobody) + " · 1 / " + wins, strip.StatusText,
                "its goal and progress (spec §4.1: \"Win 3 runs as the Nobody · 1 / 3\")");
            Assert.AreEqual(BuyResult.NotForSale, _menu.BuySkin());
            Assert.AreEqual(5000, MetaProgress.Data.renown, "nothing spent");
            Assert.IsNull(MetaProgress.EquippedSkin(Nobody, Meta), "a preview never equips a locked skin");
            Assert.AreSame(champion.model, _menu.HeroPrefab, "but the diorama shows it");

            // the wins: the menu grants it on load (CheckUnlocks), and cycling to it equips it
            yield return Boot(d => SaveData.Add(d.gladiatorWins, "nobody", wins));
            Assert.IsTrue(MetaProgress.IsOwned(champion), "earned by the wins");
            yield return ToSelect();
            CycleTo(champion);
            Assert.IsTrue(_menu.Skins.CurrentOwned);
            Assert.AreEqual("EQUIPPED", _menu.Skins.StatusText);
            Assert.AreSame(champion, MetaProgress.EquippedSkin(Nobody, Meta), "owned: equipped at once");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator TheEquippedSkin_SurvivesAMenuReload()
        {
            var market = MarketSkin();
            yield return Boot(d => Own(d, market));
            yield return ToSelect();
            CycleTo(market);
            Assert.AreSame(market, MetaProgress.EquippedSkin(Nobody, Meta), "an owned skin is equipped as it's shown");
            CollectionAssert.Contains(SavedFile().equippedSkins, market.id, "and saved");

            MetaProgress.Reload(); // read it back from the file
            yield return LoadMenu();
            Assert.AreSame(market.model, _menu.HeroPrefab, "the diorama wears it from the main screen on");
            yield return ToSelect();
            var strip = _menu.Skins;
            Assert.AreSame(market, strip.Current, "the strip opens on the equipped skin");
            Assert.AreEqual("EQUIPPED", strip.StatusText);

            CycleTo(null); // the classic look: owned, so equipped (Unequip)
            Assert.IsNull(MetaProgress.EquippedSkin(Nobody, Meta));
            CollectionAssert.DoesNotContain(SavedFile().equippedSkins, market.id, "the classic look is saved too");
            Assert.AreSame(Nobody.model, _menu.HeroPrefab);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Fight_SetsGameSessionSkin_ToTheEquippedOne_NotThePreview()
        {
            var market = MarketSkin();
            var champion = ChampionSkin();
            yield return Boot(d => Own(d, market));
            yield return ToSelect();
            CycleTo(market); // equipped
            CycleTo(champion); // previewed, not owned
            Assert.AreSame(champion.model, _menu.HeroPrefab);
            Assert.IsFalse(_menu.AsksForArena, "one arena, no Edicts: FIGHT! goes straight in");
            Invoke(_menu, "StartRun");
            Assert.AreSame(Nobody, GameSession.Gladiator);
            Assert.AreSame(market, GameSession.Skin, "FIGHT! fights in the equipped (owned) skin");
            Assert.IsNotNull(GameSession.LastLoadPath, "the run would load");

            CycleTo(null); // the classic look
            Invoke(_menu, "StartRun");
            Assert.IsNull(GameSession.Skin, "the classic look: no skin");
            yield return null;
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator TheDaily_FightsInTheEquippedSkin()
        {
            var market = MarketSkin();
            var meta = Meta;
            Assert.IsNotNull(meta.dailyUnlock, "MetaConfig.dailyUnlock (run Gladiator/Setup/Meta/Create Daily Data)");
            yield return Boot(d =>
            {
                d.unlocked.Add(meta.dailyUnlock.id);
                Own(d, market);
                (d.equippedSkins ??= new List<string>()).Add(market.id);
            });
            _menu.OpenDaily();
            Assert.IsTrue(_menu.DailyShown);
            Assert.IsTrue(_menu.TodayPlan.IsValid);
            Assert.AreSame(Nobody, _menu.TodayPlan.Gladiator, "only the Nobody is earned: the day fixes him");
            _menu.FightDaily();
            Assert.IsTrue(GameSession.HasDaily);
            Assert.AreSame(market, GameSession.Skin, "the day fixes the gladiator, not his colours");
            yield return null;
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator TheRosterTile_ShowsTheEquippedSkinsPortrait()
        {
            var market = MarketSkin();
            Assert.IsNotNull(market.portrait, market.id + "'s portrait (run Gladiator/Setup/UI/Bake Portraits)");
            yield return Boot(d => { Own(d, market); (d.equippedSkins ??= new List<string>()).Add(market.id); });
            int slot = NobodySlot();
            Assert.AreSame(market.portrait, _menu.Roster.PortraitOf(slot), "built in the equipped skin");
            Assert.AreSame(market.portrait.texture, _menu.Roster.FaceImage(slot).texture);
            yield return ToSelect();
            CycleTo(null);
            Assert.AreSame(Nobody.portrait, _menu.Roster.PortraitOf(slot), "the classic look: his own portrait");
            if (Nobody.portrait != null) Assert.AreSame(Nobody.portrait.texture, _menu.Roster.FaceImage(slot).texture);
            CycleTo(market);
            Assert.AreSame(market.portrait, _menu.Roster.PortraitOf(slot), "and back");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator TheCodex_NamesTheSkinAnUnlockGives()
        {
            var market = MarketSkin();
            var champion = ChampionSkin();
            yield return Boot();
            var codex = _menu.Codex;
            StringAssert.StartsWith("Reward: Skin — " + market.displayName + " (" + Nobody.displayName + ")", codex.RewardText(market.unlock),
                "spec §4.5: \"Reward: Skin — Freedman (The Nobody)\"");
            StringAssert.StartsWith("Reward: Skin — " + champion.displayName + " (" + Nobody.displayName + ")", codex.RewardText(champion.unlock));

            bool listed = false;
            for (int i = 0; i < codex.EntryCount(CodexPanel.Tab.Market); i++)
                if (codex.TryEntry(CodexPanel.Tab.Market, i, out string title, out _, out _) && title == market.unlock.displayName) listed = true;
            Assert.IsTrue(listed, "the Market lists the skin by itself (its unlock)");
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }
    }
}
