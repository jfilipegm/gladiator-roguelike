using System.IO;
using Gladiator.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace Gladiator.Tests
{
    /// <summary>M3.6: save file round trip / migration / corruption, Renown math, unlock conditions, locked upgrades.</summary>
    public class MetaTests
    {
        string _path;
        MetaConfig _meta;
        UnlockDef _flail;
        UpgradeDef _card;

        [SetUp]
        public void SetUp()
        {
            _path = Path.Combine(Application.temporaryCachePath, "meta_test_save.json");
            foreach (var f in new[] { _path, _path + ".corrupt", _path + ".tmp" }) if (File.Exists(f)) File.Delete(f);
            _card = ScriptableObject.CreateInstance<UpgradeDef>();
            _card.id = "weapon_flail";
            _card.kind = UpgradeKind.Weapon;
            _flail = ScriptableObject.CreateInstance<UnlockDef>();
            _flail.id = "gorms_flail";
            _flail.conditions = new[] { new UnlockCondition { kind = UnlockConditionKind.DefeatBoss, id = "gorm", amount = 1f } };
            _flail.addsToPool = new[] { _card };
            _meta = ScriptableObject.CreateInstance<MetaConfig>();
            _meta.unlocks = new[] { _flail };
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var f in new[] { _path, _path + ".corrupt", _path + ".tmp" }) if (File.Exists(f)) File.Delete(f);
            Object.DestroyImmediate(_meta);
            Object.DestroyImmediate(_flail);
            Object.DestroyImmediate(_card);
        }

        static RunSummary Run(RunOutcome outcome, float seconds, int kills, string boss = null, int arena = 0) =>
            new RunSummary { Outcome = outcome, Elapsed = seconds, Kills = kills, GoldEarned = 30, BossDefeated = boss, ArenaIndex = arena, GladiatorId = "nobody" };

        [Test]
        public void Save_RoundTrips()
        {
            var d = new SaveData { renown = 42, runs = 3, bestTime = 401.5f, lifetimeKills = 12345 };
            SaveData.Add(d.bossKills, "gorm", 2);
            d.unlocked.Add("gorms_flail");
            Assert.IsTrue(SaveSystem.Save(d, _path));
            var back = SaveSystem.Load(_path);
            Assert.AreEqual(42, back.renown);
            Assert.AreEqual(3, back.runs);
            Assert.AreEqual(401.5f, back.bestTime, 1e-3f);
            Assert.AreEqual(12345, back.lifetimeKills);
            Assert.AreEqual(2, SaveData.Count(back.bossKills, "gorm"));
            Assert.IsTrue(back.IsUnlocked("gorms_flail"));
            Assert.AreEqual(SaveData.CurrentVersion, back.version);
            Assert.IsFalse(File.Exists(_path + ".tmp"), "the temp file replaced the save");
        }

        [Test]
        public void Save_MissingFile_IsAFreshSave()
        {
            var d = SaveSystem.Load(_path);
            Assert.AreEqual(0, d.runs);
            Assert.IsNotNull(d.unlocked);
        }

        [Test]
        public void Save_OldVersion_Migrates()
        {
            File.WriteAllText(_path, "{\"renown\": 7, \"runs\": 2}"); // a v0 file: no version, no lists
            var d = SaveSystem.Load(_path);
            Assert.AreEqual(7, d.renown);
            Assert.AreEqual(SaveData.CurrentVersion, d.version);
            Assert.IsNotNull(d.bossKills);
            Assert.IsNotNull(d.unlocked);
            Assert.IsFalse(d.IsUnlocked("gorms_flail"));
        }

        [Test]
        public void Save_Corrupt_StartsFreshAndKeepsACopy()
        {
            File.WriteAllText(_path, "{ this is not json");
            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("unreadable"));
            var d = SaveSystem.Load(_path);
            Assert.AreEqual(0, d.runs);
            Assert.IsTrue(File.Exists(_path + ".corrupt"));
        }

        [Test]
        public void Renown_TimeKillsBossVictoryAndDepth()
        {
            _meta.renownPerMinute = 4f; _meta.renownPerHundredKills = 2f; _meta.renownPerBoss = 40; _meta.renownForVictory = 20; _meta.deeperArenaBonus = 0.5f;
            Assert.AreEqual(16 + 10, _meta.RenownFor(Run(RunOutcome.Died, 240f, 500)));
            Assert.AreEqual(32 + 30 + 40 + 20, _meta.RenownFor(Run(RunOutcome.Survived, 480f, 1500, "gorm")));
            Assert.AreEqual(Mathf.RoundToInt((16 + 10) * 1.5f), _meta.RenownFor(Run(RunOutcome.Died, 240f, 500, null, 1)));
        }

        [Test]
        public void Apply_RecordsTheRunAndAddsRenown()
        {
            var d = new SaveData();
            var r = MetaProgress.Apply(Run(RunOutcome.Died, 300f, 800), _meta, d);
            Assert.AreEqual(1, d.runs);
            Assert.AreEqual(0, d.victories);
            Assert.AreEqual(300f, d.bestTime);
            Assert.AreEqual(800, d.lifetimeKills);
            Assert.AreEqual(30, d.lifetimeGold);
            Assert.Greater(r.Renown, 0);
            Assert.AreEqual(r.Renown, d.renown);
            Assert.AreEqual(0, r.NewUnlocks.Count, "no boss, no flail");
            MetaProgress.Apply(Run(RunOutcome.Died, 200f, 100), _meta, d);
            Assert.AreEqual(300f, d.bestTime, "best time keeps the record");
            Assert.AreEqual(900, d.lifetimeKills);
        }

        [Test]
        public void DefeatingGorm_UnlocksTheFlailOnce()
        {
            var d = new SaveData();
            Assert.IsTrue(MetaProgress.IsLocked(_card, _meta, d), "the flail card waits for its unlock");
            var r = MetaProgress.Apply(Run(RunOutcome.Survived, 560f, 2000, "gorm"), _meta, d);
            CollectionAssert.AreEqual(new[] { _flail }, r.NewUnlocks);
            Assert.IsTrue(d.IsUnlocked("gorms_flail"));
            Assert.IsFalse(MetaProgress.IsLocked(_card, _meta, d));
            Assert.AreEqual(1, SaveData.Count(d.gladiatorWins, "nobody"));
            r = MetaProgress.Apply(Run(RunOutcome.Survived, 560f, 2000, "gorm"), _meta, d);
            Assert.AreEqual(0, r.NewUnlocks.Count, "unlocks happen once");
            Assert.AreEqual(2, SaveData.Count(d.bossKills, "gorm"));
        }

        [Test]
        public void Conditions_EachKind()
        {
            var d = new SaveData { deepestArena = 2, bestTime = 300f, lifetimeKills = 5000, lifetimeGold = 900, lifetimeRenown = 250 };
            SaveData.Add(d.bossKills, "gorm", 1);
            SaveData.Add(d.gladiatorWins, "nobody", 3);
            SaveData.Add(d.weaponKills, "spear", 120);
            d.flags.Add("rattling_crate");
            bool M(UnlockConditionKind k, string id, float n) => MetaProgress.Met(new UnlockCondition { kind = k, id = id, amount = n }, d);
            Assert.IsTrue(M(UnlockConditionKind.DefeatBoss, "gorm", 1));
            Assert.IsFalse(M(UnlockConditionKind.DefeatBoss, "gorm", 2));
            Assert.IsFalse(M(UnlockConditionKind.DefeatBoss, "beast_lord", 1));
            Assert.IsTrue(M(UnlockConditionKind.ReachArena, null, 2));
            Assert.IsFalse(M(UnlockConditionKind.ReachArena, null, 3));
            Assert.IsTrue(M(UnlockConditionKind.SurviveSeconds, null, 300));
            Assert.IsFalse(M(UnlockConditionKind.SurviveSeconds, null, 301));
            Assert.IsTrue(M(UnlockConditionKind.WinWithGladiator, "nobody", 3));
            Assert.IsFalse(M(UnlockConditionKind.WinWithGladiator, "blood_knight", 1));
            Assert.IsTrue(M(UnlockConditionKind.KillsWithWeapon, "spear", 100));
            Assert.IsFalse(M(UnlockConditionKind.KillsWithWeapon, "spear", 121));
            Assert.IsTrue(M(UnlockConditionKind.LifetimeKills, null, 5000));
            Assert.IsFalse(M(UnlockConditionKind.LifetimeGold, null, 901));
            Assert.IsTrue(M(UnlockConditionKind.LifetimeRenown, null, 250));
            Assert.IsTrue(M(UnlockConditionKind.Flag, "rattling_crate", 0));
            Assert.IsFalse(M(UnlockConditionKind.Flag, "secret_door", 0));
        }

        [Test]
        public void Unlock_AllConditionsMustHold_AndNoneMeansNever()
        {
            var d = new SaveData { bestTime = 500f };
            _flail.conditions = new[]
            {
                new UnlockCondition { kind = UnlockConditionKind.SurviveSeconds, amount = 400f },
                new UnlockCondition { kind = UnlockConditionKind.DefeatBoss, id = "gorm", amount = 1f },
            };
            Assert.IsFalse(MetaProgress.AllMet(_flail, d));
            SaveData.Add(d.bossKills, "gorm", 1);
            Assert.IsTrue(MetaProgress.AllMet(_flail, d));
            _flail.conditions = new UnlockCondition[0];
            Assert.IsFalse(MetaProgress.AllMet(_flail, d), "an unlock without conditions is never earned by accident");
        }

        [Test]
        public void LockedUpgrade_IsNeverOffered()
        {
            var config = ScriptableObject.CreateInstance<UpgradeConfig>();
            var build = new BuildState();
            Assert.IsTrue(UpgradeRoller.IsEligible(_card, build, config));
            build.Lock(_card);
            Assert.IsFalse(UpgradeRoller.IsEligible(_card, build, config));
            build.Clear();
            Assert.IsTrue(build.IsLocked(_card), "a new run's Clear keeps the meta locks");
            Object.DestroyImmediate(config);
        }

        [Test]
        public void LockedButOwned_StillLevels()
        {
            // a gladiator that starts with a locked weapon (the goblin's Bomb Satchel before its unlock) can still level it
            var config = ScriptableObject.CreateInstance<UpgradeConfig>();
            var build = new BuildState();
            _card.maxLevel = 8;
            build.Lock(_card);
            build.Take(_card, Rarity.Common, null, config);
            Assert.IsTrue(UpgradeRoller.IsEligible(_card, build, config), "held: its level-ups come up");
            Object.DestroyImmediate(config);
        }

        [Test]
        public void FlagUnlock_WaitsForTheDiscovery()
        {
            var d = new SaveData();
            _flail.conditions = new[] { new UnlockCondition { kind = UnlockConditionKind.Flag, id = "rattling_crate" } };
            Assert.IsFalse(MetaProgress.AllMet(_flail, d));
            d.flags.Add("rattling_crate");
            Assert.IsTrue(MetaProgress.AllMet(_flail, d));
            var r = MetaProgress.Apply(new RunSummary { Outcome = RunOutcome.Died, Elapsed = 30f }, _meta, d);
            CollectionAssert.Contains(r.NewUnlocks, _flail, "a lost run still grants it");
        }
    }
}
