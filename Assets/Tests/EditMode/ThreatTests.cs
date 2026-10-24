using Gladiator.Gameplay;
using NUnit.Framework;
using UnityEditor;

namespace Gladiator.Tests
{
    /// <summary>Note 39 (v0.29.0): the horde grows with the player's level (Threat, DirectorConfig's threat numbers).</summary>
    public class ThreatTests
    {
        static DirectorConfig Config()
        {
            var c = AssetDatabase.LoadAssetAtPath<DirectorConfig>("Assets/Data/Config/DirectorConfig.asset");
            Assert.IsNotNull(c);
            return c;
        }

        [Test]
        public void EarlyLevels_AreUntouched()
        {
            var c = Config();
            Assert.AreEqual(1f, Threat.HpMulAt(c, 1));
            Assert.AreEqual(1f, Threat.HpMulAt(c, c.threatFromLevel), "HP grows only past threatFromLevel");
            Assert.AreEqual(1f, Threat.DamageMulAt(c, c.threatDamageFromLevel));
            Assert.AreEqual(1f, Threat.SpeedMulAt(c, c.threatDamageFromLevel));
            Assert.AreEqual(1f, Threat.HpMulAt(null, 40), "no config (the Playground): no threat");
        }

        [Test]
        public void TheHorde_GrowsWithTheLevel_SpeedCapped()
        {
            var c = Config();
            Assert.Greater(Threat.HpMulAt(c, 30), Threat.HpMulAt(c, 20));
            Assert.Greater(Threat.HpMulAt(c, 20), 1f);
            Assert.AreEqual(1f + c.threatHpPerLevel * (30 - c.threatFromLevel), Threat.HpMulAt(c, 30), 1e-4f);
            Assert.AreEqual(1f + c.threatDamagePerLevel * (30 - c.threatDamageFromLevel), Threat.DamageMulAt(c, 30), 1e-4f);
            Assert.AreEqual(1f + c.threatSpeedMax, Threat.SpeedMulAt(c, 200), 1e-4f, "speed stops at its cap");
            Assert.Greater(Threat.HpMulAt(c, 30) - 1f, Threat.DamageMulAt(c, 30) - 1f, "HP grows faster than damage");
        }

        [Test]
        public void Tiers_RiseInOrder()
        {
            var c = Config();
            Assert.Greater(c.threatTiers.Length, 0);
            for (int i = 1; i < c.threatTiers.Length; i++) Assert.Greater(c.threatTiers[i], c.threatTiers[i - 1]);
            Assert.Greater(c.threatTiers[0], c.threatFromLevel, "the first warning comes once the horde has grown");
        }
    }
}
