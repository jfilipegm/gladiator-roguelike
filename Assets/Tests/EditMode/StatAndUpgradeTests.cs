using System.Collections.Generic;
using Gladiator.Core;
using Gladiator.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace Gladiator.Tests
{
    /// <summary>M2.4: stat pipeline maths, upgrade rolling (eligibility, slots, banish, fallbacks), rarity odds and Luck.</summary>
    public class StatAndUpgradeTests
    {
        readonly List<Object> _made = new List<Object>();

        T Make<T>() where T : ScriptableObject
        {
            var o = ScriptableObject.CreateInstance<T>();
            _made.Add(o);
            return o;
        }

        UpgradeDef Boon(string id, float weight = 10f, int maxLevel = 5, params StatMod[] mods)
        {
            var d = Make<UpgradeDef>();
            d.id = d.displayName = d.name = id;
            d.weight = weight;
            d.maxLevel = maxLevel;
            d.perLevel = mods;
            return d;
        }

        UpgradeDef Filler(string id)
        {
            var d = Boon(id, 0f, 1);
            d.kind = UpgradeKind.Consumable;
            return d;
        }

        [SetUp] public void SetUp() => GameRandom.Seed(12345);

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _made) Object.DestroyImmediate(o);
            _made.Clear();
        }

        // ---------- StatBlock ----------

        [Test]
        public void StatInfo_CountMatchesEnum() =>
            Assert.AreEqual(System.Enum.GetValues(typeof(StatId)).Length, StatInfo.Count);

        [Test]
        public void Defaults_MultipliersAtOne_ChancesAtZero()
        {
            var b = new StatBlock();
            Assert.AreEqual(1f, b[StatId.Damage]);
            Assert.AreEqual(1f, b[StatId.MoveSpeed]);
            Assert.AreEqual(100f, b[StatId.MaxHp]);
            Assert.AreEqual(0f, b[StatId.CritChance]);
            Assert.AreEqual(2f, b[StatId.Rerolls]);
        }

        [Test]
        public void Layers_FlatThenPercentThenMultiply()
        {
            var b = new StatBlock();
            b.SetBase(StatId.MaxHp, 10f);
            b.Add(new StatMod(StatId.MaxHp, ModOp.Flat, 5f));
            b.Add(new StatMod(StatId.MaxHp, ModOp.Percent, 0.5f));
            b.Add(new StatMod(StatId.MaxHp, ModOp.Percent, 0.5f));      // percents add: +100 %
            b.Add(new StatMod(StatId.MaxHp, ModOp.Multiply, 2f));
            Assert.AreEqual((10f + 5f) * 2f * 2f, b[StatId.MaxHp], 1e-4f);
        }

        [Test]
        public void Remove_UndoesAdd_AndVersionBumps()
        {
            var b = new StatBlock();
            var m1 = new StatMod(StatId.Damage, ModOp.Percent, 0.12f);
            var m2 = new StatMod(StatId.Damage, ModOp.Multiply, 1.5f);
            int v0 = b.Version;
            b.Add(m1, 1.8f);
            b.Add(m2, 1.4f);
            Assert.Greater(b.Version, v0);
            Assert.AreEqual((1f + 0.12f * 1.8f) * (1f + 0.5f * 1.4f), b[StatId.Damage], 1e-4f);
            b.Remove(m1, 1.8f);
            b.Remove(m2, 1.4f);
            Assert.AreEqual(1f, b[StatId.Damage], 1e-5f);
        }

        [Test]
        public void Scaling_SkipsWholeNumberStats()
        {
            Assert.AreEqual(1f, new StatMod(StatId.ExtraJumps, ModOp.Flat, 1f).Scaled(2.5f));
            Assert.AreEqual(0.25f, new StatMod(StatId.Damage, ModOp.Percent, 0.1f).Scaled(2.5f), 1e-6f);
            Assert.AreEqual(1.5f, new StatMod(StatId.Area, ModOp.Multiply, 1.2f).Scaled(2.5f), 1e-6f);
        }

        // ---------- taking cards ----------

        [Test]
        public void Take_AppliesRarityMagnitude_AndLevelsUp()
        {
            var cfg = Make<UpgradeConfig>();
            var stats = new StatBlock();
            var build = new BuildState();
            var bf = Boon("brute", 10f, 5, new StatMod(StatId.Damage, ModOp.Percent, 0.12f));
            Assert.AreEqual(1, build.Take(bf, Rarity.Common, stats, cfg));
            Assert.AreEqual(2, build.Take(bf, Rarity.Rare, stats, cfg));
            Assert.AreEqual(1f + 0.12f + 0.12f * cfg.Magnitude(Rarity.Rare), stats[StatId.Damage], 1e-5f);
            Assert.AreEqual(Rarity.Rare, build.GetOwned(0).BestRarity);
            Assert.AreEqual(1, build.OwnedCount);
        }

        [Test]
        public void CardText_ShowsTheRaritysRealNumbers()
        {
            var cfg = Make<UpgradeConfig>();
            var t = new CharBuffer(128);
            var d = Boon("lucky", 10f, 5, new StatMod(StatId.CritChance, ModOp.Flat, 0.05f), new StatMod(StatId.MaxHp, ModOp.Flat, 20f));
            CardText.Describe(new CardOffer { Def = d, Rarity = Rarity.Uncommon, Level = 1 }, cfg, t);
            Assert.AreEqual("+7% Crit Chance\n+28 Max HP", t.ToString());
            var regen = Boon("regen", 10f, 5, new StatMod(StatId.HpRegen, ModOp.Flat, 0.4f));
            CardText.Describe(new CardOffer { Def = regen, Rarity = Rarity.Common, Level = 1 }, cfg, t);
            Assert.AreEqual("+0.4 HP Regen/s", t.ToString());
        }

        // ---------- rolling ----------

        [Test]
        public void Roll_DistinctEligibleCards_NeverBanishedOrMaxed()
        {
            var cfg = Make<UpgradeConfig>();
            var build = new BuildState();
            var pool = new List<UpgradeDef>();
            for (int i = 0; i < 6; i++) pool.Add(Boon("b" + i));
            var maxed = pool[0];
            maxed.maxLevel = 1;
            build.Take(maxed, Rarity.Common, null, cfg);
            build.Banish(pool[1]);
            var roller = new UpgradeRoller();
            var offers = new CardOffer[4];
            for (int r = 0; r < 300; r++)
            {
                int n = roller.Roll(pool, null, build, cfg, 0f, offers, 3);
                Assert.AreEqual(3, n);
                for (int i = 0; i < n; i++)
                {
                    Assert.AreNotEqual(maxed, offers[i].Def, "maxed upgrade offered");
                    Assert.AreNotEqual(pool[1], offers[i].Def, "banished upgrade offered");
                    for (int j = 0; j < i; j++) Assert.AreNotEqual(offers[j].Def, offers[i].Def, "duplicate card");
                    Assert.AreEqual(1, offers[i].Level);
                }
            }
        }

        [Test]
        public void Roll_FullBoonSlots_OnlyOffersOwnedOnes()
        {
            var cfg = Make<UpgradeConfig>();
            cfg.boonSlots = 2;
            var build = new BuildState();
            var pool = new List<UpgradeDef> { Boon("a"), Boon("b"), Boon("c"), Boon("d") };
            build.Take(pool[0], Rarity.Common, null, cfg);
            build.Take(pool[1], Rarity.Common, null, cfg);
            var roller = new UpgradeRoller();
            var offers = new CardOffer[4];
            for (int r = 0; r < 100; r++)
            {
                int n = roller.Roll(pool, null, build, cfg, 0f, offers, 3);
                Assert.AreEqual(2, n, "only the two owned boons can come up");
                for (int i = 0; i < n; i++)
                {
                    Assert.IsTrue(offers[i].Def == pool[0] || offers[i].Def == pool[1]);
                    Assert.AreEqual(2, offers[i].Level);
                }
            }
        }

        [Test]
        public void Roll_EmptyPool_FallsBackToFillers()
        {
            var cfg = Make<UpgradeConfig>();
            var build = new BuildState();
            var pool = new List<UpgradeDef> { Boon("only", 10f, 1) };
            build.Take(pool[0], Rarity.Common, null, cfg);
            var fillers = new List<UpgradeDef> { Filler("bread"), Filler("purse") };
            var offers = new CardOffer[4];
            int n = new UpgradeRoller().Roll(pool, fillers, build, cfg, 0f, offers, 3);
            Assert.AreEqual(2, n);
            Assert.AreEqual(fillers[0], offers[0].Def);
            Assert.AreEqual(fillers[1], offers[1].Def);
        }

        [Test]
        public void Replace_AfterBanish_GivesACardNotAlreadyShown()
        {
            var cfg = Make<UpgradeConfig>();
            var build = new BuildState();
            var pool = new List<UpgradeDef>();
            for (int i = 0; i < 5; i++) pool.Add(Boon("b" + i));
            var roller = new UpgradeRoller();
            var offers = new CardOffer[4];
            for (int r = 0; r < 100; r++)
            {
                build.Clear();
                int n = roller.Roll(pool, null, build, cfg, 0f, offers, 3);
                var gone = offers[1].Def;
                build.Banish(gone);
                n = roller.Replace(pool, null, build, cfg, 0f, offers, n, 1);
                Assert.AreEqual(3, n);
                Assert.AreNotEqual(gone, offers[1].Def);
                Assert.AreNotEqual(offers[0].Def, offers[1].Def);
                Assert.AreNotEqual(offers[2].Def, offers[1].Def);
            }
            // nothing left to replace with: the card is removed
            build.Clear();
            var small = new List<UpgradeDef> { pool[0], pool[1] };
            int m = roller.Roll(small, null, build, cfg, 0f, offers, 3);
            Assert.AreEqual(2, m);
            build.Banish(offers[0].Def);
            var kept = offers[1].Def;
            m = roller.Replace(small, null, build, cfg, 0f, offers, m, 0);
            Assert.AreEqual(1, m);
            Assert.AreEqual(kept, offers[0].Def);
        }

        [Test]
        public void Rarity_MatchesWeights_AndLuckShiftsUp()
        {
            var cfg = Make<UpgradeConfig>();
            const int N = 20000;
            var noLuck = new int[4];
            var lucky = new int[4];
            for (int i = 0; i < N; i++) noLuck[(int)UpgradeRoller.RollRarity(cfg, 0f, Rarity.Common, Rarity.Legendary)]++;
            for (int i = 0; i < N; i++) lucky[(int)UpgradeRoller.RollRarity(cfg, 0.5f, Rarity.Common, Rarity.Legendary)]++;
            float total = 0f;
            foreach (var w in cfg.rarityWeights) total += w;
            for (int r = 0; r < 4; r++)
                Assert.AreEqual(cfg.rarityWeights[r] / total, noLuck[r] / (float)N, 0.015f, "rarity " + (Rarity)r);
            Assert.Less(lucky[0], noLuck[0], "luck makes Common rarer");
            Assert.Greater(lucky[2] + lucky[3], (noLuck[2] + noLuck[3]) * 1.8f, "and Rare+Legendary much likelier");
        }

        [Test]
        public void Rarity_RespectsUpgradeBounds()
        {
            var cfg = Make<UpgradeConfig>();
            for (int i = 0; i < 2000; i++)
            {
                var r = UpgradeRoller.RollRarity(cfg, 2f, Rarity.Uncommon, Rarity.Rare);
                Assert.IsTrue(r == Rarity.Uncommon || r == Rarity.Rare, r.ToString());
            }
        }

        [Test]
        public void CardCount_ThreeWithoutLuck_FourWithEnough()
        {
            var cfg = Make<UpgradeConfig>();
            for (int i = 0; i < 100; i++)
            {
                float roll = i / 100f;
                Assert.AreEqual(3, cfg.CardCount(0f, roll));
                Assert.AreEqual(4, cfg.CardCount(cfg.fourthCardLuck, roll));
            }
        }
    }
}
