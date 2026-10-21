using Gladiator.Gameplay;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Gladiator.Tests
{
    /// <summary>
    /// R1: the BossDef fields for a third phase default to "off", so the four older bosses' assets keep their behaviour,
    /// and the Emperor's Champion's data (BossSetup.EnsureEmperorsChampion) matches docs/BOSSES.md.
    /// </summary>
    public class BossPhaseDataTests
    {
        [Test]
        public void ANewBossDef_HasNoPhaseThree_AndKeepsTodaysGuardsAndVolley()
        {
            var def = ScriptableObject.CreateInstance<BossDef>();
            try
            {
                Assert.AreEqual(0f, def.phase3At, "no phase 3");
                Assert.IsEmpty(def.phase3Attacks, "phase 3 would use phase 2's attacks");
                Assert.AreEqual(0, def.phase3ChargeRepeats);
                Assert.AreEqual(0f, def.phase3MoveSpeed);
                Assert.AreEqual(1, def.guardsFromPhase, "the guard clock runs from the start");
                Assert.IsFalse(def.guardsNearestGates, "gates 0, 1, 2... in order");
                Assert.IsFalse(def.volleyJavelins, "coins");
                Assert.AreEqual(0f, def.closeInRange, "no stride");
            }
            finally { Object.DestroyImmediate(def); }
        }

        [TestCase("Assets/Data/Bosses/BD_Gorm.asset")]
        [TestCase("Assets/Data/Bosses/BD_Champion.asset")]
        [TestCase("Assets/Data/Bosses/BD_BeastLord.asset")]
        [TestCase("Assets/Data/Bosses/BD_GildedPatron.asset")]
        public void TheOlderBosses_StayTwoPhaseBosses(string path)
        {
            var def = AssetDatabase.LoadAssetAtPath<BossDef>(path);
            Assert.IsNotNull(def, path);
            Assert.AreEqual(0f, def.phase3At, "no phase 3");
            Assert.AreEqual(1, def.guardsFromPhase);
            Assert.IsFalse(def.guardsNearestGates);
            Assert.IsFalse(def.volleyJavelins);
            Assert.AreEqual(0f, def.closeInRange);
        }

        [Test]
        public void TheEmperorsChampion_MatchesTheSpec()
        {
            var def = AssetDatabase.LoadAssetAtPath<BossDef>("Assets/Data/Bosses/BD_EmperorsChampion.asset");
            Assert.IsNotNull(def, "made by Gladiator/Setup/Bosses/Create Emperor's Champion Data (ImperialArenaSetup.Build)");
            Assert.AreEqual("emperors_champion", def.id);
            var body = def.enemy;
            Assert.IsNotNull(body);
            Assert.AreEqual("emperors_champion", body.id);
            Assert.AreEqual(EnemyBehaviour.Scripted, body.behaviour);
            Assert.AreEqual(18000f, body.maxHp);
            Assert.AreEqual(1.25f, body.radius, 1e-4f);
            Assert.AreEqual(100f, body.blockArc);
            Assert.AreEqual(0.5f, body.blockReduction, 1e-4f);
            Assert.IsNotNull(body.model);
            Assert.AreEqual("EN_EmperorsChampion", body.model.name);

            CollectionAssert.AreEqual(new[] { BossAttack.Whip, BossAttack.Bash, BossAttack.Volley }, def.phase1Attacks);
            CollectionAssert.Contains(def.phase2Attacks, BossAttack.Charge);
            CollectionAssert.Contains(def.phase3Attacks, BossAttack.Charge);
            Assert.AreEqual(0.6f, def.phase2At, 1e-4f);
            Assert.AreEqual(0.3f, def.phase3At, 1e-4f);
            Assert.AreEqual(1.2f, def.enrageSpeed, 1e-4f);
            Assert.AreEqual(1.4f, def.phase3Speed, 1e-4f);
            Assert.AreEqual(4.6f, def.phase3MoveSpeed, 1e-4f);
            Assert.AreEqual(2, def.phase3ChargeRepeats);
            Assert.AreEqual(1, def.chargeRepeats.y, "the charge once in phase 2");
            Assert.AreEqual(1f, def.whipSlow, "the spear doesn't slow");
            Assert.IsTrue(def.volleyJavelins);
            Assert.AreEqual(new Vector2Int(5, 7), def.volleyCount);
            Assert.AreEqual(28f, def.closeInRange);
            Assert.AreEqual(9.5f, def.closeInSpeed, 1e-4f);
            Assert.AreEqual(2, def.guardsFromPhase);
            Assert.IsTrue(def.guardsNearestGates);
            Assert.AreEqual(2, def.guardSpotsUsed);
            Assert.AreEqual(new Vector2Int(2, 2), def.guardCount);
            Assert.AreEqual(26f, def.guardsEvery);
            Assert.IsTrue(def.guards != null && def.guards.Length > 0 && def.guards[0] != null, "EN_Centurion as his guard (rerun the setup once it exists)");
            Assert.AreEqual("centurion", def.guards[0].id);
            Assert.IsFalse(def.phase2FireRing || def.phase2SpikeVolley || def.phase2FireJets || def.phase2Blades, "no phase-2 arena extras");
            Assert.IsTrue(def.scatterOnDefeat, "the Guard stands down at his fall");
            Assert.AreEqual(Rarity.Rare, def.rewardFloor);
        }
    }
}
