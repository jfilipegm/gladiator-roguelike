using System.Collections.Generic;
using Gladiator.Core;
using Gladiator.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace Gladiator.Tests
{
    /// <summary>
    /// Phase 5.5 (docs/EVOLUTIONS.md): an evolution needs its weapon at max level and its catalyst, comes only first in
    /// chest/altar/boss rewards, swaps the weapon in its slot; tag synergies count distinct upgrades and switch on once.
    /// </summary>
    public class EvolutionTests
    {
        readonly List<Object> _made = new List<Object>();

        T Make<T>() where T : ScriptableObject
        {
            var o = ScriptableObject.CreateInstance<T>();
            _made.Add(o);
            return o;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _made) Object.DestroyImmediate(o);
            _made.Clear();
        }

        UpgradeConfig _cfg;
        UpgradeDef _gladius, _luckyCut, _brute, _evo;
        List<UpgradeDef> _pool;

        [SetUp]
        public void SetUp()
        {
            GameRandom.Seed(7);
            _cfg = Make<UpgradeConfig>();
            var w = Make<WeaponDef>();
            w.id = "gladius";
            w.levels = new WeaponDef.Level[2]; // max level 3
            _gladius = Card("weapon_gladius", UpgradeKind.Weapon, UpgradeTag.Crit, 3);
            _gladius.weapon = w;
            _luckyCut = Card("lucky_cut", UpgradeKind.Boon, UpgradeTag.Crit, 5);
            _brute = Card("brute_force", UpgradeKind.Boon, UpgradeTag.Heavy, 5);
            _evo = Card("evo_champions_gladius", UpgradeKind.Weapon, UpgradeTag.Crit, 1);
            _evo.evolvesFrom = _gladius;
            _evo.catalyst = _luckyCut;
            _evo.weapon = Make<WeaponDef>();
            _pool = new List<UpgradeDef> { _gladius, _luckyCut, _brute, _evo };
        }

        UpgradeDef Card(string id, UpgradeKind kind, UpgradeTag tags, int maxLevel)
        {
            var d = Make<UpgradeDef>();
            d.id = d.displayName = d.name = id;
            d.kind = kind;
            d.tags = tags;
            d.maxLevel = maxLevel;
            d.weight = 10f;
            return d;
        }

        BuildState Build(int gladiusLevel, bool catalyst)
        {
            var b = new BuildState();
            for (int i = 0; i < gladiusLevel; i++) b.Take(_gladius, Rarity.Common, null, _cfg);
            if (catalyst) b.Take(_luckyCut, Rarity.Common, null, _cfg);
            return b;
        }

        bool Offers(BuildState b, bool rewards, out int slot)
        {
            var roller = new UpgradeRoller { OfferEvolutions = rewards };
            var offers = new CardOffer[4];
            int n = roller.Roll(_pool, null, b, _cfg, 0f, offers, 3);
            slot = -1;
            for (int i = 0; i < n; i++) if (offers[i].Def == _evo) { slot = i; break; }
            return slot >= 0;
        }

        [Test]
        public void Ready_NeedsTheWeaponAtMaxLevel_AndTheCatalyst()
        {
            Assert.IsFalse(UpgradeRoller.IsEvolutionReady(_evo, Build(2, true)), "weapon not maxed");
            Assert.IsFalse(UpgradeRoller.IsEvolutionReady(_evo, Build(3, false)), "no catalyst");
            Assert.IsTrue(UpgradeRoller.IsEvolutionReady(_evo, Build(3, true)));
            var banished = Build(3, true);
            banished.Banish(_evo);
            Assert.IsFalse(UpgradeRoller.IsEvolutionReady(_evo, banished), "banished");
            Assert.IsFalse(UpgradeRoller.IsEligible(_evo, Build(3, true), _cfg), "never an ordinary card");
        }

        [Test]
        public void Rewards_PutItFirst_LevelUpsNeverOfferIt()
        {
            var ready = Build(3, true);
            for (int k = 0; k < 20; k++) Assert.IsFalse(Offers(ready, rewards: false, out _), "level-ups don't offer evolutions");
            Assert.IsTrue(Offers(ready, rewards: true, out int slot), "a chest offers it");
            Assert.AreEqual(0, slot, "in the first slot");
            var roller = new UpgradeRoller { OfferEvolutions = true };
            var offers = new CardOffer[4];
            roller.Roll(_pool, null, ready, _cfg, 0f, offers, 3);
            Assert.AreEqual(Rarity.Legendary, offers[0].Rarity);
            Assert.AreEqual(1, offers[0].Level);
            Assert.IsFalse(Offers(Build(3, false), rewards: true, out _), "not ready: not offered");
        }

        [Test]
        public void Evolve_SwapsInPlace_FreesNoSlot_NeverOfferedAgain()
        {
            var b = Build(3, true);
            int weaponsBefore = b.CountOf(UpgradeKind.Weapon);
            Assert.IsTrue(b.Evolve(_gladius, _evo));
            Assert.AreEqual(0, b.LevelOf(_gladius), "the base weapon is gone");
            Assert.AreEqual(1, b.LevelOf(_evo));
            Assert.AreEqual(_evo, b.GetOwned(0).Def, "same slot (first owned)");
            Assert.AreEqual(weaponsBefore, b.CountOf(UpgradeKind.Weapon), "still one weapon slot used");
            Assert.AreEqual(1, b.LevelOf(_luckyCut), "the catalyst stays");
            Assert.IsFalse(UpgradeRoller.IsEvolutionReady(_evo, b), "taken");
            Assert.IsFalse(UpgradeRoller.IsEligible(_gladius, b, _cfg), "the base weapon is never offered again: it lives on as its evolution");
            Assert.IsFalse(b.Evolve(_gladius, _evo), "nothing left to evolve");
        }

        [Test]
        public void TagCount_CountsDistinctUpgrades_WeaponsIncluded()
        {
            var b = new BuildState();
            b.Take(_gladius, Rarity.Common, null, _cfg);
            b.Take(_gladius, Rarity.Common, null, _cfg);
            b.Take(_luckyCut, Rarity.Common, null, _cfg);
            b.Take(_luckyCut, Rarity.Rare, null, _cfg);
            b.Take(_brute, Rarity.Common, null, _cfg);
            Assert.AreEqual(2, b.TagCount(UpgradeTag.Crit), "levels don't count, upgrades do");
            Assert.AreEqual(1, b.TagCount(UpgradeTag.Heavy));
            Assert.AreEqual(0, b.TagCount(UpgradeTag.Fire));
        }

        [Test]
        public void Synergy_SwitchesOnOnceAtItsCount_AddsItsModsOnce()
        {
            var syn = Make<TagSynergyDef>();
            syn.id = "executioner";
            syn.tag = UpgradeTag.Crit;
            syn.count = 3;
            syn.mods = new[] { new StatMod(StatId.CritDamage, ModOp.Flat, 0.5f) };
            var all = new List<TagSynergyDef> { syn };
            var stats = new StatBlock();
            float before = stats[StatId.CritDamage];
            var b = new BuildState();
            b.Take(_gladius, Rarity.Common, null, _cfg);
            b.Take(_luckyCut, Rarity.Common, null, _cfg);
            Assert.AreEqual(0, b.UpdateSynergies(all, stats), "2 of 3");
            var third = Card("serrated_edge", UpgradeKind.Boon, UpgradeTag.Crit | UpgradeTag.Blood, 5);
            b.Take(third, Rarity.Common, null, _cfg);
            int version = b.Version;
            Assert.AreEqual(1, b.UpdateSynergies(all, stats), "3 of 3: on");
            Assert.Greater(b.Version, version, "the build changed (effects refresh)");
            Assert.IsTrue(b.HasSynergy(syn));
            Assert.AreEqual(before + 0.5f, stats[StatId.CritDamage], 1e-4f);
            b.Take(third, Rarity.Common, null, _cfg);
            Assert.AreEqual(0, b.UpdateSynergies(all, stats), "once only");
            Assert.AreEqual(before + 0.5f, stats[StatId.CritDamage], 1e-4f, "mods added once");
            b.Clear();
            Assert.AreEqual(0, b.SynergyCount, "a new run starts without synergies");
        }
    }
}
