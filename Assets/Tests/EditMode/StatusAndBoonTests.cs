using System.Collections.Generic;
using Gladiator.Core;
using Gladiator.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace Gladiator.Tests
{
    /// <summary>M2.6: status stacking rules, Boon potency (rarity steps add up), synergy gating and effect card text.</summary>
    public class StatusAndBoonTests
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

        StatusDef Status(string name, string adj, StatusKind kind, float damageTaken = 0f)
        {
            var s = Make<StatusDef>();
            s.id = s.name = name.ToLowerInvariant();
            s.displayName = name; s.adjective = adj; s.kind = kind; s.damageTakenBonus = damageTaken;
            return s;
        }

        UpgradeDef Boon(string id, params BoonEffect[] fx)
        {
            var d = Make<UpgradeDef>();
            d.id = d.displayName = d.name = id;
            d.effects = fx;
            return d;
        }

        string Text(UpgradeDef d, Rarity r)
        {
            var t = new CharBuffer(256);
            CardText.Describe(new CardOffer { Def = d, Rarity = r, Level = 1 }, Make<UpgradeConfig>(), t);
            return t.ToString();
        }

        [Test]
        public void Refresh_KeepsTheStrongerPower_ResetsTheTimer_OneStack()
        {
            float time = 0f, power = 0f; int stacks = 0;
            StatusMath.Apply(StatusStacking.Refresh, 3f, 1, ref time, ref power, ref stacks, 10f, 1);
            time = 0.5f;
            StatusMath.Apply(StatusStacking.Refresh, 3f, 1, ref time, ref power, ref stacks, 4f, 1);
            Assert.AreEqual(3f, time);
            Assert.AreEqual(10f, power, "a weaker re-application doesn't weaken it");
            Assert.AreEqual(1, stacks);
            StatusMath.Apply(StatusStacking.Refresh, 3f, 1, ref time, ref power, ref stacks, 15f, 1);
            Assert.AreEqual(15f, power, "a stronger one takes over");
        }

        [Test]
        public void Stack_AddsUpToTheCap_ExpiredStartsFresh()
        {
            float time = 0f, power = 0f; int stacks = 0;
            for (int k = 0; k < 7; k++) StatusMath.Apply(StatusStacking.Stack, 4f, 10, ref time, ref power, ref stacks, 2f, 2);
            Assert.AreEqual(10, stacks, "capped at maxStacks");
            Assert.AreEqual(10f, StatusMath.TickDamage(power, stacks, 0.5f), 1e-4f, "2 dps × 10 stacks × 0.5 s");
            time = 0f; // expired
            StatusMath.Apply(StatusStacking.Stack, 4f, 10, ref time, ref power, ref stacks, 1f, 2);
            Assert.AreEqual(2, stacks, "an expired status starts over");
            Assert.AreEqual(1f, power);
        }

        [Test]
        public void TickDamage_IsPowerTimesStacksTimesInterval() =>
            Assert.AreEqual(6f, StatusMath.TickDamage(3f, 4, 0.5f), 1e-5f);

        [Test]
        public void Potency_SumsTheRarityStepsOfEveryTake()
        {
            var cfg = Make<UpgradeConfig>();
            var build = new BuildState();
            var d = Boon("kindling");
            build.Take(d, Rarity.Common, null, cfg);
            build.Take(d, Rarity.Rare, null, cfg);
            Assert.AreEqual(cfg.Magnitude(Rarity.Common) + cfg.Magnitude(Rarity.Rare), build.GetOwned(0).Potency, 1e-5f);
        }

        [Test]
        public void SynergyBoon_OnlyOfferedOnceASourceIsOwned()
        {
            var cfg = Make<UpgradeConfig>();
            var build = new BuildState();
            var kindling = Boon("kindling");
            var oil = Boon("oil");
            var pyre = Boon("pyre");
            pyre.requiresAny = new[] { kindling, oil };
            Assert.IsFalse(UpgradeRoller.IsEligible(pyre, build, cfg), "nothing burns yet");
            build.Take(oil, Rarity.Common, null, cfg);
            Assert.IsTrue(UpgradeRoller.IsEligible(pyre, build, cfg), "any one source unlocks it");
        }

        [Test]
        public void EffectText_HeadlineNumberScalesWithRarity()
        {
            var burn = Status("Burn", "Burning", StatusKind.Burn);
            var shock = Status("Shock", "Shocked", StatusKind.Shock, 0.2f);
            var kindling = Boon("kindling", new BoonEffect { kind = EffectKind.StatusOnHit, status = burn, chance = 0.1f, power = 0.4f });
            Assert.AreEqual("10% chance on hit to Burn", Text(kindling, Rarity.Common));
            Assert.AreEqual("18% chance on hit to Burn", Text(kindling, Rarity.Rare));
            var spark = Boon("static", new BoonEffect { kind = EffectKind.ChainSpark, status = shock, power = 7f, everyNth = 6, count = 3, radius = 5f });
            Assert.AreEqual("Every 6 hits a spark jumps to 3 enemies (7 damage, Shock)\nShocked enemies take +20% damage", Text(spark, Rarity.Common));
            var pyre = Boon("pyre", new BoonEffect { kind = EffectKind.BurstOnDeath, status = burn, power = 8f, radius = 2.8f });
            Assert.AreEqual("Burning enemies burst on death: 20 damage, spreading Burn", Text(pyre, Rarity.Legendary));
        }
    }
}
