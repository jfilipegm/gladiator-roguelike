using System.Collections;
using Gladiator.Core;
using Gladiator.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Gladiator.Tests
{
    /// <summary>M2.4: real level-up cards — pick applies the rarity's numbers, reroll/banish/skip, max-HP heal, panel text.</summary>
    public class LevelUpTests
    {
        const string Scene = "Assets/Scenes/Dev/TestArena.unity";

        RunState _run;
        LevelUpController _lvl;
        PlayerBuild _build;
        PlayerStats _stats;
        Gladiator.UI.LevelUpPanel _panel;

        IEnumerator Boot()
        {
            DevSettings.ResetAll();
            TimeController.GameSpeed = 1f;
            TimeController.ResetForNewRun();
            var op = SceneManager.LoadSceneAsync(Scene, LoadSceneMode.Single);
            while (!op.isDone) yield return null;
            yield return null;
            if (Director.Instance != null) Director.Instance.enabled = false;
            Player.Main.OverrideSource = new ScriptedInput();
            Player.Main.GetComponent<WeaponSystem>().enabled = false;
            DevSettings.GodMode = true;
            EnemyManager.Instance.KillAll();
            yield return new WaitForSeconds(0.3f);
            PickupSystem.Instance.ClearAll();
            _run = RunState.Current;
            _lvl = LevelUpController.Instance;
            _build = Player.Main.GetComponent<PlayerBuild>();
            _stats = Player.Main.GetComponent<PlayerStats>();
            _panel = Object.FindAnyObjectByType<Gladiator.UI.LevelUpPanel>();
        }

        IEnumerator OpenCards()
        {
            _run.AddXp(_run.XpToNext - _run.Xp + 0.01f);
            float end = Time.realtimeSinceStartup + 3f;
            while (!_lvl.IsOpen && Time.realtimeSinceStartup < end) yield return null;
            Assert.IsTrue(_lvl.IsOpen, "level-up cards should open");
            yield return null; // panel draws
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Cards_ShowTheOffers_PickAppliesRarityNumbers()
        {
            yield return Boot();
            yield return OpenCards();
            Assert.AreEqual(_build.config.cards, _lvl.ChoiceCount, "3 cards without Luck");
            for (int i = 0; i < _lvl.ChoiceCount; i++)
            {
                var o = _lvl.Offers[i];
                Assert.IsNotNull(o.Def);
                Assert.AreEqual(UpgradeKind.Boon, o.Def.kind);
                Assert.AreEqual(1, o.Level, "nothing owned yet: every card is NEW");
                Assert.AreEqual(o.Def.displayName, _panel.CardTitle(i));
                Assert.AreEqual(CardText.RarityName(o.Rarity), _panel.CardRarity(i));
                StringAssert.Contains(StatInfo.Name(o.Def.perLevel[0].stat), _panel.CardDescription(i));
            }
            var pick = _lvl.Offers[0];
            var mod = pick.Def.perLevel[0];
            float before = _stats.Block[mod.stat];
            _lvl.Choose(0);
            yield return null;
            Assert.IsFalse(_lvl.IsOpen);
            Assert.AreEqual(1f, Time.timeScale, "game resumes");
            Assert.AreEqual(1, _build.State.LevelOf(pick.Def));
            float scaled = mod.Scaled(_build.config.Magnitude(pick.Rarity));
            float expected = mod.op == ModOp.Flat ? before + scaled : mod.op == ModOp.Percent ? before + scaled * _stats.Block.GetBase(mod.stat) : before * scaled;
            Assert.AreEqual(expected, _stats.Block[mod.stat], 1e-4f, pick.Def.id + " at " + pick.Rarity);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Reroll_NewCards_LimitedPerRun()
        {
            yield return Boot();
            yield return OpenCards();
            int left = _lvl.RerollsLeft;
            Assert.AreEqual((int)StatInfo.DefaultBase(StatId.Rerolls), left);
            for (int k = 0; k < left; k++)
            {
                int v = _lvl.OffersVersion;
                Assert.IsTrue(_lvl.Reroll());
                Assert.AreEqual(v + 1, _lvl.OffersVersion);
                Assert.AreEqual(left - k - 1, _lvl.RerollsLeft);
                Assert.IsTrue(_lvl.IsOpen, "rerolling keeps the cards open");
            }
            Assert.IsFalse(_lvl.Reroll(), "no rerolls left");
            Assert.AreEqual(0f, Time.timeScale, "still paused while choosing");
            _lvl.Choose(0);
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator Banish_RemovesTheUpgradeForTheRestOfTheRun()
        {
            yield return Boot();
            yield return OpenCards();
            var gone = _lvl.Offers[1].Def;
            Assert.IsTrue(_lvl.Banish(1));
            Assert.IsTrue(_build.State.IsBanished(gone));
            Assert.AreEqual(0, _lvl.BanishesLeft);
            Assert.AreEqual(_build.config.cards, _lvl.ChoiceCount, "the banished card is replaced");
            for (int i = 0; i < _lvl.ChoiceCount; i++) Assert.AreNotEqual(gone, _lvl.Offers[i].Def);
            Assert.IsFalse(_lvl.Banish(0), "no banishes left");
            _lvl.Choose(0);
            for (int lv = 0; lv < 12; lv++)
            {
                yield return OpenCards();
                for (int i = 0; i < _lvl.ChoiceCount; i++) Assert.AreNotEqual(gone, _lvl.Offers[i].Def, "banished card came back");
                _lvl.Choose(0);
            }
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Skip_ClosesWithoutTakingAnything()
        {
            yield return Boot();
            yield return OpenCards();
            int version = _stats.Block.Version;
            int pending = _run.PendingLevelUps;
            _lvl.Skip();
            yield return null;
            Assert.IsFalse(_lvl.IsOpen);
            Assert.AreEqual(pending - 1, _run.PendingLevelUps);
            Assert.AreEqual(version, _stats.Block.Version, "no stats changed");
            Assert.AreEqual(0, _build.State.OwnedCount);
            Assert.AreEqual(1f, Time.timeScale);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator MaxHpBoon_RaisesMaxAndHealsTheIncrease_StatsReachTheGame()
        {
            yield return Boot();
            var health = Player.Main.GetComponent<PlayerHealth>();
            var weapons = Player.Main.GetComponent<WeaponSystem>();
            var motor = Player.Main.GetComponent<PlayerMotor>();
            UpgradeDef thick = null, brute = null;
            foreach (var d in _build.catalog.upgrades)
            {
                if (d.id == "thick_skin") thick = d;
                if (d.id == "brute_force") brute = d;
            }
            Assert.IsNotNull(thick);
            Assert.IsNotNull(brute);
            health.TakeDamage(new DamageInfo { Amount = 30f });
            float hp0 = health.Hp, max0 = health.MaxHp;
            _build.Take(new CardOffer { Def = thick, Rarity = Rarity.Common, Level = 1 });
            Assert.AreEqual(max0 + 20f, health.MaxHp, 1e-3f);
            Assert.AreEqual(hp0 + 20f, health.Hp, 1e-3f, "a max-HP raise heals by the same amount");
            float dmg0 = weapons.DamageMul;
            _build.Take(new CardOffer { Def = brute, Rarity = Rarity.Legendary, Level = 1 });
            Assert.AreEqual(dmg0 * (1f + 0.12f * _build.config.Magnitude(Rarity.Legendary)), weapons.DamageMul, 1e-4f, "weapons read the pipeline");
            Assert.AreEqual(2, _build.State.OwnedCount);
            yield return null;
        }
    }
}
