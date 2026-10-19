using Gladiator.Core;
using Gladiator.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace Gladiator.Tests
{
    /// <summary>M2.7: chest price growth and the rarity floor chests and the altar roll with.</summary>
    public class InteractableTests
    {
        [Test]
        public void ChestPrice_GrowsPerChestOpened()
        {
            var c = ScriptableObject.CreateInstance<InteractableConfig>();
            try
            {
                c.basePrice = 15; c.priceGrowth = 1.4f;
                Assert.AreEqual(15, c.PriceFor(0));
                Assert.AreEqual(21, c.PriceFor(1));
                Assert.AreEqual(29, c.PriceFor(2));
                Assert.AreEqual(41, c.PriceFor(3));
            }
            finally { Object.DestroyImmediate(c); }
        }

        [Test]
        public void RarityFloor_NoCardBelowIt_DefaultFloorIsNone()
        {
            var cfg = ScriptableObject.CreateInstance<UpgradeConfig>();
            var pool = new UpgradeDef[6];
            try
            {
                for (int i = 0; i < pool.Length; i++)
                {
                    pool[i] = ScriptableObject.CreateInstance<UpgradeDef>();
                    pool[i].id = pool[i].name = "b" + i;
                    pool[i].perLevel = new[] { new StatMod(StatId.Damage, ModOp.Percent, 0.1f) };
                }
                GameRandom.Seed(777);
                var roller = new UpgradeRoller { MinRarity = Rarity.Rare };
                var offers = new CardOffer[3];
                var build = new BuildState();
                for (int k = 0; k < 200; k++)
                {
                    int n = roller.Roll(pool, null, build, cfg, 0f, offers, 3);
                    for (int i = 0; i < n; i++) Assert.GreaterOrEqual(offers[i].Rarity, Rarity.Rare);
                }
                roller.MinRarity = Rarity.Common;
                bool sawCommon = false;
                for (int k = 0; k < 50 && !sawCommon; k++)
                {
                    int n = roller.Roll(pool, null, build, cfg, 0f, offers, 3);
                    for (int i = 0; i < n; i++) sawCommon |= offers[i].Rarity == Rarity.Common;
                }
                Assert.IsTrue(sawCommon, "without a floor Commons come back");
            }
            finally
            {
                Object.DestroyImmediate(cfg);
                foreach (var d in pool) Object.DestroyImmediate(d);
            }
        }
    }
}
