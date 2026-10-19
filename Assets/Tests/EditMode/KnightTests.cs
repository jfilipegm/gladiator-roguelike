using Gladiator.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace Gladiator.Tests
{
    /// <summary>Phase 5.3 (docs/GLADIATORS.md): the Blood Knight's rules that are pure maths, and his unlock.</summary>
    public class KnightTests
    {
        [Test]
        public void Bloodlust_IsZeroAtFullHp_LinearToTheCap_FullAtTwentyPercent()
        {
            var t = ScriptableObject.CreateInstance<BloodlustTrait>();
            t.maxDamageBonus = 0.6f;
            t.fullAtHp = 0.2f;
            Assert.AreEqual(0f, t.BonusAt(1f), 1e-5f);
            Assert.AreEqual(0.3f, t.BonusAt(0.6f), 1e-5f, "halfway between 100 % and 20 %");
            Assert.AreEqual(0.6f, t.BonusAt(0.2f), 1e-5f);
            Assert.AreEqual(0.6f, t.BonusAt(0.01f), 1e-5f, "capped");
            Object.DestroyImmediate(t);
        }

        static UnlockCondition Bloodied(float below) =>
            new UnlockCondition { kind = UnlockConditionKind.DefeatBossBelowHp, id = "gorm", amount = below };

        [Test]
        public void DefeatBossBelowHp_NeedsAKill_UnderTheLine()
        {
            var d = new SaveData();
            Assert.IsFalse(MetaProgress.Met(Bloodied(0.3f), d), "never beaten");
            d.RecordBossKillHp("gorm", 450);
            Assert.IsFalse(MetaProgress.Met(Bloodied(0.3f), d), "beaten at 45 %");
            d.RecordBossKillHp("gorm", 700);
            Assert.IsTrue(d.TryBossKillHp("gorm", out int lowest));
            Assert.AreEqual(450, lowest, "a healthier win doesn't overwrite the lowest");
            d.RecordBossKillHp("gorm", 300);
            Assert.IsFalse(MetaProgress.Met(Bloodied(0.3f), d), "exactly 30 % isn't below 30 %");
            d.RecordBossKillHp("gorm", 290);
            Assert.IsTrue(MetaProgress.Met(Bloodied(0.3f), d));
            Assert.IsFalse(MetaProgress.Met(new UnlockCondition { kind = UnlockConditionKind.DefeatBossBelowHp, id = "champion", amount = 0.3f }, d),
                "per boss");
        }

        [Test]
        public void ARun_RecordsTheHpTheBossFellAt_AndUnlocks()
        {
            var u = ScriptableObject.CreateInstance<UnlockDef>();
            u.id = "blood_knight";
            u.conditions = new[] { Bloodied(0.3f) };
            var meta = ScriptableObject.CreateInstance<MetaConfig>();
            meta.unlocks = new[] { u };
            var d = new SaveData();

            var healthy = new RunSummary { Outcome = RunOutcome.Survived, Elapsed = 500f, BossDefeated = "gorm", BossDefeatedHp01 = 0.8f, GladiatorId = "nobody" };
            Assert.AreEqual(0, MetaProgress.Apply(healthy, meta, d).NewUnlocks.Count);
            var unknown = new RunSummary { Outcome = RunOutcome.Survived, Elapsed = 500f, BossDefeated = "gorm", GladiatorId = "nobody" };
            Assert.AreEqual(0, MetaProgress.Apply(unknown, meta, d).NewUnlocks.Count, "a summary without the HP counts as full HP");
            Assert.IsTrue(d.TryBossKillHp("gorm", out int lowest));
            Assert.AreEqual(800, lowest);

            var bloodied = new RunSummary { Outcome = RunOutcome.Survived, Elapsed = 500f, BossDefeated = "gorm", BossDefeatedHp01 = 0.12f, GladiatorId = "nobody" };
            var r = MetaProgress.Apply(bloodied, meta, d);
            Assert.AreEqual(1, r.NewUnlocks.Count);
            Assert.AreSame(u, r.NewUnlocks[0]);
            Object.DestroyImmediate(meta);
            Object.DestroyImmediate(u);
        }

        [Test]
        public void OldSaves_GetTheNewList()
        {
            var d = new SaveData { bossKillLowestHp = null };
            d.Migrate();
            Assert.IsNotNull(d.bossKillLowestHp);
            Assert.IsFalse(d.TryBossKillHp("gorm", out _));
        }
    }
}
