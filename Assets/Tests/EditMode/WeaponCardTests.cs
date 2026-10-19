using System.Collections.Generic;
using Gladiator.Core;
using Gladiator.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace Gladiator.Tests
{
    /// <summary>M2.5: weapon card text (new weapon vs level step, rarity scaling) and weapon-slot eligibility.</summary>
    public class WeaponCardTests
    {
        readonly List<Object> _made = new List<Object>();

        T Make<T>() where T : ScriptableObject
        {
            var o = ScriptableObject.CreateInstance<T>();
            _made.Add(o);
            return o;
        }

        static StatMod Mod(StatId s, ModOp op, float v) => new StatMod { stat = s, op = op, value = v };

        WeaponDef Weapon(string id)
        {
            var w = Make<WeaponDef>();
            w.id = w.displayName = w.name = id;
            w.description = "Throws " + id;
            w.levels = new[]
            {
                new WeaponDef.Level { mods = new[] { Mod(StatId.Amount, ModOp.Flat, 1f) } },
                new WeaponDef.Level { mods = new[] { Mod(StatId.Damage, ModOp.Percent, 0.2f), Mod(StatId.Pierce, ModOp.Flat, 1f) } },
            };
            return w;
        }

        UpgradeDef Card(WeaponDef w)
        {
            var d = Make<UpgradeDef>();
            d.id = d.displayName = d.name = "UP_W_" + w.id;
            d.kind = UpgradeKind.Weapon;
            d.weapon = w;
            d.weight = 8f;
            d.maxLevel = w.MaxLevel;
            return d;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _made) Object.DestroyImmediate(o);
            _made.Clear();
        }

        string Text(UpgradeDef d, Rarity r, int level)
        {
            var t = new CharBuffer(128);
            CardText.Describe(new CardOffer { Def = d, Rarity = r, Level = level }, Make<UpgradeConfig>(), t);
            return t.ToString();
        }

        [Test]
        public void MaxLevel_IsOnePlusLevelTable() => Assert.AreEqual(3, Weapon("jav").MaxLevel);

        [Test]
        public void NewWeaponCard_ShowsDescription() =>
            Assert.AreEqual("Throws jav", Text(Card(Weapon("jav")), Rarity.Rare, 1));

        [Test]
        public void LevelCard_ShowsThatStep_IntegersNeverScale()
        {
            var card = Card(Weapon("jav"));
            Assert.AreEqual("+1 Projectiles", Text(card, Rarity.Legendary, 2), "whole-number stats ignore rarity");
            Assert.AreEqual("+28% Damage\n+1 Pierce", Text(card, Rarity.Uncommon, 3), "percent steps scale x1.4 at Uncommon");
        }

        [Test]
        public void NewWeapon_IneligibleWhenSlotsFull_OwnedStillLevels()
        {
            var cfg = Make<UpgradeConfig>();
            var build = new BuildState();
            var owned = new UpgradeDef[cfg.weaponSlots];
            for (int i = 0; i < owned.Length; i++)
            {
                owned[i] = Card(Weapon("w" + i));
                Assert.IsTrue(UpgradeRoller.IsEligible(owned[i], build, cfg), "free slot");
                build.Take(owned[i], Rarity.Common, null, cfg);
            }
            var fifth = Card(Weapon("extra"));
            Assert.IsFalse(UpgradeRoller.IsEligible(fifth, build, cfg), "all weapon slots used");
            Assert.IsTrue(UpgradeRoller.IsEligible(owned[0], build, cfg), "an owned weapon below max level can still level");
            build.Take(owned[0], Rarity.Common, null, cfg);
            build.Take(owned[0], Rarity.Common, null, cfg);
            Assert.IsFalse(UpgradeRoller.IsEligible(owned[0], build, cfg), "max level reached");
        }
    }
}
