using Gladiator.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace Gladiator.Tests
{
    /// <summary>Phase 5.1 (docs/FAVOR.md): Favor thresholds, the no-hit chain, gift weights and the Arena Master's rules.</summary>
    public class FavorTests
    {
        FavorConfig _c;

        [SetUp] public void SetUp() => _c = ScriptableObject.CreateInstance<FavorConfig>();
        [TearDown] public void TearDown() => Object.DestroyImmediate(_c);

        [Test]
        public void Threshold_GrowsWithEachGift()
        {
            Assert.AreEqual(100f, _c.ThresholdFor(0), 1e-3f);
            Assert.AreEqual(125f, _c.ThresholdFor(1), 1e-3f);
            Assert.AreEqual(156.25f, _c.ThresholdFor(2), 1e-3f);
        }

        [Test]
        public void NoHitChain_Climbs_ThenTheLastStepRepeats()
        {
            Assert.AreEqual(5f, _c.NoHitFavor(0));
            Assert.AreEqual(7f, _c.NoHitFavor(1));
            Assert.AreEqual(10f, _c.NoHitFavor(2));
            Assert.AreEqual(10f, _c.NoHitFavor(9));
        }

        [Test]
        public void StreakFavor_ByRung_NothingOutOfRange()
        {
            Assert.AreEqual(8f, _c.StreakFavor(0));
            Assert.AreEqual(20f, _c.StreakFavor(3));
            Assert.AreEqual(0f, _c.StreakFavor(4));
            Assert.AreEqual(0f, _c.StreakFavor(-1));
        }

        [Test]
        public void GiftPick_FollowsTheWeights()
        {
            // amphora 1, gold 1, weapon 0.8, laurel 0.6 → 3.4 in all
            Assert.AreEqual(GiftKind.Amphora, _c.PickGift(0f, 1f, true));
            Assert.AreEqual(GiftKind.GoldShower, _c.PickGift(1.5f / 3.4f, 1f, true));
            Assert.AreEqual(GiftKind.WeaponInSand, _c.PickGift(2.5f / 3.4f, 1f, true));
            Assert.AreEqual(GiftKind.Laurel, _c.PickGift(0.999f, 1f, true));
        }

        [Test]
        public void LowHp_MakesTheAmphoraLikelier()
        {
            float r = 2.9f / 5.4f;
            Assert.AreEqual(GiftKind.GoldShower, _c.PickGift(r, 1f, true), "at full HP this roll is gold");
            Assert.AreEqual(GiftKind.Amphora, _c.PickGift(r, 0.3f, true), "hurt, the amphora weighs 3 and takes it");
        }

        [Test]
        public void NoWeaponToLend_NeverRollsTheWeapon()
        {
            for (int i = 0; i < 100; i++) Assert.AreNotEqual(GiftKind.WeaponInSand, _c.PickGift(i / 100f, 1f, false));
        }

        [Test]
        public void Dull_MeansFewKills_AndNobodyClose()
        {
            Assert.IsTrue(_c.IsDull(5, 100, float.MaxValue), "5 kills against a 100-enemy horde, nothing near");
            Assert.IsFalse(_c.IsDull(15, 100, float.MaxValue), "fighting");
            Assert.IsFalse(_c.IsDull(0, 100, 3f), "enemies in reach: in trouble, not dull");
        }

        [Test]
        public void Whims_NeverEarly_NeverDuringTheBoss_NeverInsideTheCooldown()
        {
            Assert.IsFalse(_c.WhimAllowed(60f, 999f, false), "too early");
            Assert.IsFalse(_c.WhimAllowed(200f, 999f, true), "the boss");
            Assert.IsFalse(_c.WhimAllowed(200f, 30f, false), "cooldown");
            Assert.IsTrue(_c.WhimAllowed(200f, 61f, false));
        }
    }
}
