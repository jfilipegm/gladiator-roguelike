using System.Collections;
using System.IO;
using Gladiator.Gameplay;
using Gladiator.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Gladiator.Tests
{
    /// <summary>
    /// Phase 5.6 Codex (docs/UNLOCKS.md) on the real main menu: every tab is built from data, the menu counts NEW
    /// entries and viewing them clears the mark (saved on close), the Market spends Renown (and refuses when you're
    /// short), found secrets show themselves, and the keyboard/gamepad switches tabs and closes it.
    /// </summary>
    public class CodexPlayTests
    {
        static T Load<T>(string path) where T : Object
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.IsNotNull(a, path);
            return a;
        }

        static UnlockDef Ul(string file) => Load<UnlockDef>("Assets/Data/Meta/" + file + ".asset");

        MainMenu _menu;

        IEnumerator Boot(System.Action<SaveData> stage)
        {
            SaveSandbox.Wipe();
            stage?.Invoke(MetaProgress.Data);
            SaveSystem.Save(MetaProgress.Data);
            var op = SceneManager.LoadSceneAsync(GameSession.MenuScene, LoadSceneMode.Single);
            while (!op.isDone) yield return null;
            yield return null;
            _menu = Object.FindAnyObjectByType<MainMenu>();
            Assert.IsNotNull(_menu);
            Assert.IsNotNull(_menu.Codex, "the menu built its Codex");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator EveryTab_IsBuiltFromData()
        {
            yield return Boot(null);
            var codex = _menu.Codex;
            var cfg = _menu.codex;
            int arsenal = 0;
            foreach (var d in cfg.catalog.upgrades) if (d != null && d.kind != UpgradeKind.Consumable) arsenal++;
            arsenal += cfg.catalog.synergies.Length;
            Assert.AreEqual(arsenal, codex.EntryCount(CodexPanel.Tab.Arsenal), "every weapon, evolution, Boon and synergy");
            Assert.AreEqual(cfg.bestiary.Length, codex.EntryCount(CodexPanel.Tab.Bestiary));
            int ch = 0, se = 0, ma = 0;
            var hidden = new System.Collections.Generic.HashSet<UnlockDef>(); // R6: a skin hides until its gladiator is earned
            if (cfg.meta.skins != null)
                foreach (var s in cfg.meta.skins)
                    if (s != null && s.unlock != null && s.gladiator != null && s.gladiator.unlock != null) hidden.Add(s.unlock);
            foreach (var u in cfg.meta.unlocks)
            {
                if (hidden.Contains(u)) continue;
                if (u.category == UnlockCategory.Challenge) ch++;
                else if (u.category == UnlockCategory.Secret) se++;
                else ma++;
            }
            Assert.AreEqual(ch, codex.EntryCount(CodexPanel.Tab.Challenges));
            Assert.AreEqual(se, codex.EntryCount(CodexPanel.Tab.Secrets));
            Assert.AreEqual(ma, codex.EntryCount(CodexPanel.Tab.Market));
            Assert.GreaterOrEqual(se, 3, "the crate, the rat, the bookmaker");
            Assert.AreEqual(0, codex.NewCount, "a fresh save has nothing new");
            Assert.IsFalse(_menu.CodexSealShown, "no NEW seal on the CODEX plate");
            Assert.AreEqual("CODEX", _menu.CodexButtonText);
            Assert.IsTrue(codex.TryEntry(CodexPanel.Tab.Bestiary, 0, out string title, out bool open, out _));
            Assert.AreEqual("???", title, "nothing met yet");
            Assert.IsFalse(open);
            _menu.OpenCodex();
            for (int t = 0; t < 7; t++) { codex.Select((CodexPanel.Tab)t); yield return null; } // every tab draws
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator NewEntries_AreCountedOnTheMenu_AndClearOnceViewed()
        {
            yield return Boot(d =>
            {
                d.unlocked.Add("ch_first_evolution");
                d.flags.Add("golden_rat");
                d.unlocked.Add("sling");
                d.runs = 1;
            });
            var codex = _menu.Codex;
            Assert.Greater(codex.NewCount, 0);
            Assert.IsTrue(_menu.CodexSealShown, "P3: the crimson NEW seal on the CODEX plate (it read \"• n NEW\")");
            Assert.AreEqual("CODEX", _menu.CodexButtonText, "the word stays CODEX");
            int secret = -1;
            for (int i = 0; i < codex.EntryCount(CodexPanel.Tab.Secrets); i++)
                if (codex.TryEntry(CodexPanel.Tab.Secrets, i, out string t, out bool open, out bool isNew) && open)
                {
                    secret = i;
                    Assert.AreEqual("The Sling", t, "a found secret shows what it was");
                    Assert.IsTrue(isNew);
                }
            Assert.GreaterOrEqual(secret, 0);

            _menu.OpenCodex();
            codex.Select(CodexPanel.Tab.Secrets);
            yield return null;
            codex.TryEntry(CodexPanel.Tab.Secrets, secret, out _, out _, out bool stillNew);
            Assert.IsFalse(stillNew, "seen now");
            Assert.IsTrue(codex.TabHasNew(CodexPanel.Tab.Challenges), "the challenge isn't viewed yet");
            Assert.IsTrue(codex.TabSealShown(CodexPanel.Tab.Challenges), "its tab wears the seal");
            Assert.IsFalse(codex.TabSealShown(CodexPanel.Tab.Records), "RECORDS has nothing new");
            codex.HandleInput(false, false, false, false, false, true); // Esc / B
            yield return null;
            Assert.IsFalse(_menu.CodexOpen, "back closes it");
            var saved = JsonUtility.FromJson<SaveData>(File.ReadAllText(SaveSandbox.TestPath));
            CollectionAssert.Contains(saved.codexSeen, "ul:sling", "seen entries are saved on close");
            CollectionAssert.DoesNotContain(saved.codexSeen, "ul:ch_first_evolution");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Market_SpendsRenown_RefusesWhenShort_AndTheArsenalOpensIt()
        {
            yield return Boot(d => d.renown = 350);
            var codex = _menu.Codex;
            var tablet = Ul("UL_WaxTablet");
            var palm = Ul("UL_GildedPalm");
            _menu.OpenCodex();
            codex.Select(CodexPanel.Tab.Market);
            yield return null;
            Assert.AreEqual(BuyResult.Bought, codex.Buy(tablet));
            Assert.AreEqual(350 - tablet.renownCost, MetaProgress.Data.renown);
            Assert.IsTrue(MetaProgress.IsUnlocked(tablet));
            Assert.AreEqual(BuyResult.TooExpensive, codex.Buy(palm), "50 Renown left");
            Assert.AreEqual(BuyResult.AlreadyOwned, codex.Buy(tablet));
            Assert.IsFalse(MetaProgress.IsLocked(tablet.addsToPool[0], _menu.meta), "Wax Tablet joins the pool");
            bool found = false;
            for (int i = 0; i < codex.EntryCount(CodexPanel.Tab.Arsenal); i++)
                if (codex.TryEntry(CodexPanel.Tab.Arsenal, i, out string t, out bool open, out _) && t == "Wax Tablet") { found = true; Assert.IsTrue(open); }
            Assert.IsTrue(found, "the Arsenal shows it unlocked");
            var saved = JsonUtility.FromJson<SaveData>(File.ReadAllText(SaveSandbox.TestPath));
            CollectionAssert.Contains(saved.unlocked, "wax_tablet", "the purchase is saved");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Keys_SwitchTabs_AndPages()
        {
            yield return Boot(null);
            var codex = _menu.Codex;
            _menu.OpenCodex();
            Assert.AreEqual(CodexPanel.Tab.Records, codex.Current);
            codex.HandleInput(false, true, false, false, false, false);
            codex.HandleInput(false, true, false, false, false, false);
            Assert.AreEqual(CodexPanel.Tab.Arsenal, codex.Current);
            Assert.Greater(codex.PageCount, 1);
            codex.HandleInput(false, false, false, true, false, false);
            Assert.AreEqual(1, codex.Page, "down: next page");
            codex.HandleInput(true, false, false, false, false, false);
            Assert.AreEqual(CodexPanel.Tab.Gladiators, codex.Current, "left: previous tab");
            yield return null;
        }
    }
}
