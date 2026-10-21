using Gladiator.Gameplay;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Gladiator.Tests
{
    /// <summary>
    /// R1: the BossDef fields for a third phase default to "off", so the four older bosses' assets keep their behaviour,
    /// and the Emperor's Champion's data (BossSetup.EnsureEmperorsChampion) matches docs/BOSSES.md. R2: the serialized
    /// enums only grew at their ends (BossAttack.Disc, State.Remains), the Eternal Victor's fields (the falx, the rise, the
    /// false death, the grasp, the phase-3 repeats) default to "off" and stay off on the five older bosses, and his data
    /// (BossSetup.EnsureEternalVictor) matches docs/BOSSES.md.
    /// </summary>
    public class BossPhaseDataTests
    {
        [Test]
        public void TheSerializedEnums_OnlyGrewAtTheirEnds()
        {
            Assert.AreEqual(9, (int)BossAttack.Volley, "BossDef assets store attacks as numbers");
            Assert.AreEqual(10, (int)BossAttack.Disc, "the falx throw appended after Volley");
            Assert.AreEqual(9, (int)BossController.State.Dead);
            Assert.AreEqual(10, (int)BossController.State.Remains, "appended after Dead");
        }

        [Test]
        public void ANewBossDef_HasNoRiseNoFalseDeathNoGrasp_AndNoPhaseThreeRepeats()
        {
            var def = ScriptableObject.CreateInstance<BossDef>();
            try
            {
                Assert.IsFalse(def.entranceRise, "he walks in through a gate");
                Assert.IsFalse(def.falseDeath, "0 HP is death");
                Assert.IsFalse(def.whipGrasp, "the whip cracks");
                Assert.AreEqual(0, def.phase3DiscRepeats, "phase 3 uses discRepeats' phase-2 value");
                Assert.AreEqual(0, def.phase3PounceRepeats, "phase 3 uses pounceRepeats' phase-2 value");
                Assert.AreEqual(new Vector2Int(1, 1), def.discRepeats);
                Assert.AreEqual("Falx", def.discPart);
                CollectionAssert.DoesNotContain(def.phase1Attacks, BossAttack.Disc);
                CollectionAssert.DoesNotContain(def.phase2Attacks, BossAttack.Disc);
            }
            finally { Object.DestroyImmediate(def); }
        }

        [TestCase("Assets/Data/Bosses/BD_Gorm.asset")]
        [TestCase("Assets/Data/Bosses/BD_Champion.asset")]
        [TestCase("Assets/Data/Bosses/BD_BeastLord.asset")]
        [TestCase("Assets/Data/Bosses/BD_GildedPatron.asset")]
        [TestCase("Assets/Data/Bosses/BD_EmperorsChampion.asset")]
        public void TheOlderBosses_KeepWalkingIn_AndDieAtZero(string path)
        {
            var def = AssetDatabase.LoadAssetAtPath<BossDef>(path);
            Assert.IsNotNull(def, path);
            Assert.IsFalse(def.entranceRise, "through his gate");
            Assert.IsFalse(def.falseDeath, "no remains");
            Assert.IsFalse(def.whipGrasp);
            Assert.AreEqual(0, def.phase3DiscRepeats);
            Assert.AreEqual(0, def.phase3PounceRepeats, "his pounce repeats unchanged in phase 3");
            foreach (var list in new[] { def.phase1Attacks, def.phase2Attacks, def.phase3Attacks })
                if (list != null) CollectionAssert.DoesNotContain(list, BossAttack.Disc, "no falx");
        }

        [Test]
        public void TheEternalVictor_MatchesTheSpec()
        {
            var def = AssetDatabase.LoadAssetAtPath<BossDef>("Assets/Data/Bosses/BD_EternalVictor.asset");
            Assert.IsNotNull(def, "made by Gladiator/Setup/Bosses/Create Eternal Victor Data (ColosseumSetup.Build)");
            Assert.AreEqual("eternal_victor", def.id);
            var body = def.enemy;
            Assert.IsNotNull(body);
            Assert.AreEqual("eternal_victor", body.id);
            Assert.AreEqual(EnemyBehaviour.Scripted, body.behaviour);
            Assert.AreEqual(22000f, body.maxHp);
            Assert.AreEqual(1.0f, body.radius, 1e-4f);
            Assert.AreEqual(0f, body.blockArc, "no shield");
            Assert.AreEqual(0f, body.knockbackTaken);
            Assert.IsNotNull(body.model);
            Assert.AreEqual("EN_EternalVictor", body.model.name);

            Assert.AreEqual("THE ETERNAL VICTOR", def.displayName);
            Assert.AreEqual("\"RISE, BROTHERS!\"", def.phase2Text);
            Assert.AreEqual("\"THE ARENA IS MINE!\"", def.phase3Text);
            Assert.AreEqual("HE WILL NOT STAY DOWN!", def.remainsText);
            Assert.AreEqual("\"NOT YET.\"", def.riseText);
            Assert.LessOrEqual(def.shortName.Length, 14, "the summary row's label must stay short (\"Emperor's Champion\" filled it)");
            Assert.LessOrEqual(def.deathLine.Length, 80, "the summary's subtitle holds one line");
            Assert.LessOrEqual(def.victoryLine.Length, 80);

            CollectionAssert.AreEqual(new[] { BossAttack.Sweep, BossAttack.Flurry, BossAttack.Disc, BossAttack.Pounce }, def.phase1Attacks);
            var later = new[] { BossAttack.Sweep, BossAttack.Flurry, BossAttack.Whip, BossAttack.Disc, BossAttack.Pounce };
            CollectionAssert.AreEqual(later, def.phase2Attacks);
            CollectionAssert.AreEqual(later, def.phase3Attacks);
            Assert.AreEqual(0.65f, def.phase2At, 1e-4f);
            Assert.AreEqual(0.3f, def.phase3At, 1e-4f);
            Assert.AreEqual(1.2f, def.enrageSpeed, 1e-4f);
            Assert.AreEqual(1.4f, def.phase3Speed, 1e-4f);
            Assert.AreEqual(3.6f, def.moveSpeed, 1e-4f);
            Assert.AreEqual(4.6f, def.phase3MoveSpeed, 1e-4f);
            Assert.AreEqual(30f, def.closeInRange);
            Assert.AreEqual(7f, def.closeInSpeed, 1e-4f);
            Assert.AreEqual(new Vector2Int(2, 3), def.flurryThrusts);

            Assert.IsTrue(def.whipGrasp, "the Grasp of the Dead");
            Assert.AreEqual(0.1f, def.whipSlow, 1e-4f, "held");
            Assert.AreEqual(1f, def.whipSlowSeconds, 1e-4f);
            Assert.AreEqual(20f, def.discLength);
            Assert.AreEqual(1.6f, def.discWidth, 1e-4f);
            Assert.AreEqual(26f, def.discSpeed);
            Assert.AreEqual(20f, def.discDamage);
            Assert.AreEqual(new Vector2Int(1, 1), def.discRepeats);
            Assert.AreEqual(2, def.phase3DiscRepeats, "two throws in phase 3");
            Assert.AreEqual(new Vector2Int(1, 1), def.pounceRepeats);
            Assert.AreEqual(2, def.phase3PounceRepeats, "two leaps in phase 3");
            Assert.AreEqual(26f, def.pounceRange);

            Assert.AreEqual(2, def.guardsFromPhase);
            Assert.IsFalse(def.guardsNearestGates, "at the arena's guard spots (the lifts)");
            Assert.AreEqual(2, def.guardSpotsUsed);
            Assert.AreEqual(new Vector2Int(6, 8), def.guardCount);
            Assert.AreEqual(24f, def.guardsEvery);
            Assert.IsTrue(def.guards != null && def.guards.Length > 0 && def.guards[0] != null, "EN_SkeletalLegionnaire as his guard (rerun the setup once it exists)");
            Assert.AreEqual("skeletal_legionnaire", def.guards[0].id);
            Assert.IsFalse(def.phase2FireRing || def.phase2SpikeVolley || def.phase2FireJets || def.phase2Blades, "no phase-2 arena extras (the Hypogeum's)");
            Assert.AreEqual(0, def.phase2CagePack);
            Assert.AreEqual(Vector2Int.zero, def.beastPack);

            Assert.IsTrue(def.entranceRise, "he rises from the stage's lift");
            Assert.AreEqual(3f, def.riseDepth);
            Assert.AreEqual(2.6f, def.riseTime, 1e-4f);
            Assert.IsTrue(def.falseDeath, "eternal");
            Assert.AreEqual(1500f, def.remainsHp);
            Assert.AreEqual(8f, def.remainsTime);
            Assert.AreEqual(0.2f, def.riseHp01, 1e-4f);
            Assert.AreEqual(4f, def.riseBurstRadius);
            Assert.AreEqual(20f, def.riseBurstDamage);
            Assert.IsTrue(def.scatterOnDefeat, "his dead crumble at his fall");
            Assert.AreEqual(Rarity.Rare, def.rewardFloor);
            Assert.AreEqual(0, def.defeatGold);
        }

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
