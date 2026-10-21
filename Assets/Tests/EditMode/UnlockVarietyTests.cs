using Gladiator.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace Gladiator.Tests
{
    /// <summary>Phase 5.6 (docs/UNLOCKS.md): Market purchases, bounties, the new condition kinds, progress bars, run records.</summary>
    public class UnlockVarietyTests
    {
        MetaConfig _meta;
        UnlockDef _market, _challenge;

        [SetUp]
        public void SetUp()
        {
            _market = ScriptableObject.CreateInstance<UnlockDef>();
            _market.id = "wax_tablet";
            _market.category = UnlockCategory.Market;
            _market.renownCost = 300;
            _challenge = ScriptableObject.CreateInstance<UnlockDef>();
            _challenge.id = "first_evolution";
            _challenge.renownBounty = 100;
            _challenge.conditions = new[] { new UnlockCondition { kind = UnlockConditionKind.Evolve, amount = 1f } };
            _meta = ScriptableObject.CreateInstance<MetaConfig>();
            _meta.unlocks = new[] { _market, _challenge };
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_meta);
            Object.DestroyImmediate(_market);
            Object.DestroyImmediate(_challenge);
        }

        [Test]
        public void Market_BuySpendsRenown_RefusesWhenPoor_ConditionsNeverGrantIt()
        {
            var d = new SaveData { renown = 250 };
            Assert.AreEqual(BuyResult.TooExpensive, MetaProgress.Buy(_market, d));
            Assert.AreEqual(250, d.renown, "nothing spent");
            _market.conditions = new[] { new UnlockCondition { kind = UnlockConditionKind.LifetimeKills, amount = 0f } };
            MetaProgress.Apply(new RunSummary { Outcome = RunOutcome.Died, Elapsed = 60f }, _meta, d);
            Assert.IsFalse(d.IsUnlocked(_market.id), "a Market unlock is bought, never earned");
            d.renown = 320;
            Assert.AreEqual(BuyResult.Bought, MetaProgress.Buy(_market, d));
            Assert.AreEqual(20, d.renown);
            Assert.IsTrue(d.IsUnlocked(_market.id));
            Assert.AreEqual(BuyResult.AlreadyOwned, MetaProgress.Buy(_market, d));
            Assert.AreEqual(BuyResult.NotForSale, MetaProgress.Buy(_challenge, d), "challenges aren't for sale");
        }

        [Test]
        public void Bounty_IsPaidOnce_AndCountsAsLifetimeRenown()
        {
            var d = new SaveData();
            var run = new RunSummary { Outcome = RunOutcome.Died, Elapsed = 0f, Evolutions = new[] { "evo_impaler" } };
            var r = MetaProgress.Apply(run, _meta, d);
            CollectionAssert.AreEqual(new[] { _challenge }, r.NewUnlocks);
            Assert.AreEqual(100, r.Bounty);
            Assert.AreEqual(r.Renown + 100, d.renown);
            Assert.AreEqual(d.renown, r.RenownTotal, "the end screen shows the total after the bounty");
            Assert.AreEqual(d.renown, d.lifetimeRenown);
            var again = MetaProgress.Apply(run, _meta, d);
            Assert.AreEqual(0, again.Bounty, "once");
            Assert.AreEqual(0, again.NewUnlocks.Count);
        }

        [Test]
        public void RunRecords_FeedTheSave_AndTheNewConditions()
        {
            var d = new SaveData();
            bool M(UnlockConditionKind k, string id, float n) => MetaProgress.Met(new UnlockCondition { kind = k, id = id, amount = n }, d);
            var run = new RunSummary
            {
                Outcome = RunOutcome.Survived, Elapsed = 480f, Kills = 5200,
                EnemyKills = new[] { new IdCount { id = "convict", count = 4000 }, new IdCount { id = "gorm", count = 1 } },
                Upgrades = new[] { "weapon_spear", "evo_impaler", "long_reach" },
                Evolutions = new[] { "evo_impaler" },
                Synergies = new[] { "juggernaut", "wildfire" },
            };
            MetaProgress.Apply(run, null, d);
            Assert.AreEqual(5200, d.bestRunKills);
            Assert.AreEqual(4000, SaveData.Count(d.enemyKills, "convict"));
            Assert.AreEqual(1, SaveData.Count(d.upgradesTaken, "evo_impaler"));
            Assert.IsTrue(M(UnlockConditionKind.RunKills, null, 5000));
            Assert.IsFalse(M(UnlockConditionKind.RunKills, null, 5201));
            Assert.IsTrue(M(UnlockConditionKind.Evolve, null, 1), "any evolution");
            Assert.IsTrue(M(UnlockConditionKind.Evolve, "evo_impaler", 1));
            Assert.IsFalse(M(UnlockConditionKind.Evolve, "evo_twin_moons", 1));
            Assert.IsFalse(M(UnlockConditionKind.SynergiesFound, null, 3));
            MetaProgress.Apply(new RunSummary { Kills = 10, Synergies = new[] { "wildfire", "bulwark" } }, null, d);
            Assert.AreEqual(5200, d.bestRunKills, "a best, not a sum");
            Assert.AreEqual(3, d.synergies.Count, "three different ones");
            Assert.IsTrue(M(UnlockConditionKind.SynergiesFound, null, 3));
        }

        [Test]
        public void Progress_ShowsTheBar_SeveralConditionsCountMet()
        {
            var d = new SaveData { bestRunKills = 1250 };
            MetaProgress.Progress(new UnlockCondition { kind = UnlockConditionKind.RunKills, amount = 5000f }, d, out float cur, out float tgt);
            Assert.AreEqual(1250f, cur);
            Assert.AreEqual(5000f, tgt);
            SaveData.Add(d.bossKills, "gorm", 5);
            MetaProgress.Progress(new UnlockCondition { kind = UnlockConditionKind.DefeatBoss, id = "gorm", amount = 3f }, d, out cur, out tgt);
            Assert.AreEqual(3f, cur, "capped at the target");
            MetaProgress.Progress(new UnlockCondition { kind = UnlockConditionKind.Flag, id = "golden_rat" }, d, out cur, out tgt);
            Assert.AreEqual(0f, cur);
            Assert.AreEqual(1f, tgt, "yes/no conditions are 0 or 1 of 1");

            var all = ScriptableObject.CreateInstance<UnlockDef>();
            all.id = "every_gladiator";
            all.conditions = new[]
            {
                new UnlockCondition { kind = UnlockConditionKind.WinWithGladiator, id = "nobody", amount = 1f },
                new UnlockCondition { kind = UnlockConditionKind.WinWithGladiator, id = "pit_goblin", amount = 1f },
                new UnlockCondition { kind = UnlockConditionKind.WinWithGladiator, id = "blood_knight", amount = 1f },
            };
            SaveData.Add(d.gladiatorWins, "nobody", 2);
            SaveData.Add(d.gladiatorWins, "blood_knight", 1);
            Assert.AreEqual(2f / 3f, MetaProgress.Progress01(all, d), 1e-4f);
            Object.DestroyImmediate(all);
        }

        [Test]
        public void Migrate_FillsTheNewLists()
        {
            var d = JsonUtility.FromJson<SaveData>("{\"version\":1,\"renown\":5}");
            d.evolutions = null; d.synergies = null; d.enemyKills = null; d.upgradesTaken = null; d.codexSeen = null;
            d.Migrate();
            Assert.IsNotNull(d.evolutions);
            Assert.IsNotNull(d.synergies);
            Assert.IsNotNull(d.enemyKills);
            Assert.IsNotNull(d.upgradesTaken);
            Assert.IsNotNull(d.codexSeen);
            Assert.AreEqual(5, d.renown);
        }

        /// <summary>Phase 5.14: WinWithWeapon counts won runs that ended with the weapon in hand; "a|b" sums both ids.</summary>
        [Test]
        public void WinWithWeapon_CountsWonRunsHoldingIt_AndAcceptsItsEvolution()
        {
            var d = new SaveData();
            bool M(string id, float n) => MetaProgress.Met(new UnlockCondition { kind = UnlockConditionKind.WinWithWeapon, id = id, amount = n }, d);
            WeaponKills[] W(params string[] ids)
            {
                var w = new WeaponKills[ids.Length];
                for (int k = 0; k < ids.Length; k++) w[k] = new WeaponKills { Id = ids[k], Kills = 10 };
                return w;
            }
            MetaProgress.Apply(new RunSummary { Outcome = RunOutcome.Died, Elapsed = 300f, Weapons = W("spear") }, _meta, d);
            Assert.IsFalse(M("spear", 1), "a lost run doesn't count");
            MetaProgress.Apply(new RunSummary { Outcome = RunOutcome.Survived, Elapsed = 480f, Weapons = W("gladius") }, _meta, d);
            Assert.IsFalse(M("spear|impaler", 1), "a win without it doesn't either");
            Assert.AreEqual(10, SaveData.Count(d.weaponKills, "spear"), "its kills still count");
            MetaProgress.Apply(new RunSummary { Outcome = RunOutcome.Survived, Elapsed = 480f, Weapons = W("gladius", "impaler") }, _meta, d);
            Assert.IsTrue(M("spear|impaler", 1), "a win holding its evolution counts");
            Assert.IsFalse(M("spear", 1), "but not for the base id alone");
            Assert.IsFalse(M("spear|impaler", 2));
            float cur, target;
            MetaProgress.Progress(new UnlockCondition { kind = UnlockConditionKind.WinWithWeapon, id = "spear|impaler", amount = 2f }, d, out cur, out target);
            Assert.AreEqual(1f, cur);
            Assert.AreEqual(2f, target);
        }

        /// <summary>Phase 5.15: KillEnemies counts lifetime kills of an enemy type from the Bestiary.</summary>
        [Test]
        public void KillEnemies_CountsLifetimeKillsOfThatType()
        {
            var d = new SaveData();
            bool M(string id, float n) => MetaProgress.Met(new UnlockCondition { kind = UnlockConditionKind.KillEnemies, id = id, amount = n }, d);
            var run = new RunSummary { Outcome = RunOutcome.Died, Elapsed = 300f, EnemyKills = new[] { new IdCount { id = "beasthandler", count = 10 }, new IdCount { id = "convict", count = 500 } } };
            MetaProgress.Apply(run, _meta, d);
            Assert.IsFalse(M("beasthandler", 25));
            MetaProgress.Apply(run, _meta, d);
            MetaProgress.Apply(run, _meta, d);
            Assert.IsTrue(M("beasthandler", 25), "30 over three runs");
            Assert.IsFalse(M("warboar", 1));
            MetaProgress.Progress(new UnlockCondition { kind = UnlockConditionKind.KillEnemies, id = "beasthandler", amount = 25f }, d, out float cur, out float target);
            Assert.AreEqual(25f, cur, "capped at the target");
            Assert.AreEqual(25f, target);
        }
    }
}
