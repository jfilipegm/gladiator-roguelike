using System.Collections.Generic;
using System.IO;
using Gladiator.Core;
using Gladiator.Gameplay;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;



namespace Gladiator.Tests
{
    /// <summary>
    /// R7 Edicts, the meta side (docs/tracks/R7-contract.md "R7-B"): the Renown formula (base × (1 + the edicts' bonus),
    /// bounties never multiplied), the records and the WinWithEdicts condition, which edicts are open and remembered, the
    /// save's v1 → v2 migration, the Market, and the data Create Edict Data writes.
    /// </summary>
    public class EdictMetaTests
    {
        string _path;
        MetaConfig _meta;
        UnlockDef _feature, _market, _challenge;
        RunModifierDef _freeA, _freeB, _bought, _earned;
        readonly List<Object> _made = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            _path = Path.Combine(Application.temporaryCachePath, "edict_meta_test_save.json");
            foreach (var f in new[] { _path, _path + ".corrupt", _path + ".tmp" }) if (File.Exists(f)) File.Delete(f);
            _feature = Unlock("edicts", UnlockCategory.Challenge, C(UnlockConditionKind.DefeatBoss, "gorm", 1f));
            _market = Unlock("edict_bought", UnlockCategory.Market);
            _market.renownCost = 150;
            _challenge = Unlock("editors_eye", UnlockCategory.Challenge, C(UnlockConditionKind.WinWithEdicts, "", 2f));
            _freeA = Edict("free_a", 0.25f, null);
            _freeB = Edict("free_b", 0.2f, null);
            _bought = Edict("bought", 0.25f, _market);
            _earned = Edict("earned", 0.2f, _challenge);
            _meta = Make<MetaConfig>();
            _meta.unlocks = new[] { _feature, _market, _challenge };
            _meta.edicts = new[] { _freeA, _freeB, _bought, _earned };
            _meta.edictsUnlock = _feature;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var f in new[] { _path, _path + ".corrupt", _path + ".tmp" }) if (File.Exists(f)) File.Delete(f);
            foreach (var o in _made) if (o != null) Object.DestroyImmediate(o);
            _made.Clear();
        }

        T Make<T>() where T : ScriptableObject
        {
            var o = ScriptableObject.CreateInstance<T>();
            _made.Add(o);
            return o;
        }

        UnlockDef Unlock(string id, UnlockCategory category, params UnlockCondition[] conditions)
        {
            var u = Make<UnlockDef>();
            u.id = id; u.category = category; u.conditions = conditions;
            return u;
        }

        RunModifierDef Edict(string id, float bonus, UnlockDef unlock)
        {
            var e = Make<RunModifierDef>();
            e.id = id; e.displayName = id; e.renownBonus = bonus; e.unlock = unlock;
            return e;
        }

        static UnlockCondition C(UnlockConditionKind k, string id, float amount) => new UnlockCondition { kind = k, id = id, amount = amount };

        static RunSummary Run(RunOutcome outcome, float seconds, int kills, string boss = null, int arena = 0, float bonus = 0f,
            params string[] edicts) =>
            new RunSummary
            {
                Outcome = outcome, Elapsed = seconds, Kills = kills, GoldEarned = 30, BossDefeated = boss, ArenaIndex = arena,
                GladiatorId = "nobody", EdictBonus = bonus, Edicts = edicts,
            };

        // ---------------------------------------------------------------- Renown

        [Test]
        public void BaseRenownFor_IsTodaysFormula()
        {
            // 4 / minute, 2 / 100 kills, 40 a boss, 20 a victory, × (1 + 0.5 × arena tier): the MetaTests numbers.
            Assert.AreEqual(16 + 10, _meta.BaseRenownFor(Run(RunOutcome.Died, 240f, 500)));
            Assert.AreEqual(32 + 30 + 40 + 20, _meta.BaseRenownFor(Run(RunOutcome.Survived, 480f, 1500, "gorm")));
            Assert.AreEqual(Mathf.RoundToInt((16 + 10) * 1.5f), _meta.BaseRenownFor(Run(RunOutcome.Died, 240f, 500, null, 1)));
            // no edicts: what the run pays is the base
            Assert.AreEqual(_meta.BaseRenownFor(Run(RunOutcome.Survived, 480f, 1500, "gorm")), _meta.RenownFor(Run(RunOutcome.Survived, 480f, 1500, "gorm")));
            // the bonus never changes the base
            Assert.AreEqual(122, _meta.BaseRenownFor(Run(RunOutcome.Survived, 480f, 1500, "gorm", 0, 0.65f)));
        }

        [Test]
        public void RenownFor_MultipliesByTheBonus_WinOrLose()
        {
            Assert.AreEqual(Mathf.RoundToInt(122 * 1.45f), _meta.RenownFor(Run(RunOutcome.Survived, 480f, 1500, "gorm", 0, 0.45f)));
            Assert.AreEqual(Mathf.RoundToInt(26 * 1.45f), _meta.RenownFor(Run(RunOutcome.Died, 240f, 500, null, 0, 0.45f)), "a death pays the bonus too");
            Assert.AreEqual(Mathf.RoundToInt(26 * 1.5f * 3.35f), _meta.RenownFor(Run(RunOutcome.Died, 240f, 500, null, 1, 2.35f)), "all eleven: ×3.35");
            Assert.AreEqual(26, _meta.RenownFor(Run(RunOutcome.Died, 240f, 500, null, 0, -0.5f)), "a negative bonus never takes Renown away");
            // the rounded base the end screen shows is what's multiplied: 100 s and 37 kills = 7.41 → 7 → ×1.65 = 11.55 → 12;
            // 3.6 → 4 → ×1.45 = 5.8 → 6 (from the unrounded base it would be 5.22 → 5, and "+4 ×1.45 = +5" reads wrong)
            Assert.AreEqual(Mathf.RoundToInt(7 * 1.65f), _meta.RenownFor(Run(RunOutcome.Died, 100f, 37, null, 0, 0.65f)));
            Assert.AreEqual(_meta.BaseRenownFor(Run(RunOutcome.Died, 100f, 37, null, 0, 0.65f)), 7);
        }

        [Test]
        public void Apply_PaysTheBonus_ButNeverOnBounties()
        {
            var bounty = Unlock("ch_first_gorm", UnlockCategory.Challenge, C(UnlockConditionKind.DefeatBoss, "gorm", 1f));
            bounty.renownBounty = 100;
            _meta.unlocks = new[] { bounty };
            var d = new SaveData { renown = 10 };
            var run = Run(RunOutcome.Survived, 480f, 1500, "gorm", 0, 1f, "free_a", "free_b");
            var r = MetaProgress.Apply(run, _meta, d);
            Assert.AreEqual(122, r.BaseRenown);
            Assert.AreEqual(1f, r.EdictBonus, 1e-5f);
            Assert.AreEqual(244, r.Renown, "×2");
            Assert.AreEqual(100, r.Bounty, "the bounty isn't multiplied");
            Assert.AreEqual(10 + 244 + 100, d.renown);
            Assert.AreEqual(r.RenownTotal, d.renown);
            Assert.AreEqual(244 + 100, d.lifetimeRenown);
            // with none on, BaseRenown = Renown and the bonus is 0
            var plain = MetaProgress.Apply(Run(RunOutcome.Died, 240f, 500), _meta, new SaveData());
            Assert.AreEqual(plain.BaseRenown, plain.Renown);
            Assert.AreEqual(0f, plain.EdictBonus);
        }

        // ---------------------------------------------------------------- records and the condition

        [Test]
        public void Apply_RecordsEdictWins_OnlyOnWonRuns()
        {
            var d = new SaveData();
            MetaProgress.Apply(Run(RunOutcome.Died, 300f, 400, null, 0, 0.65f, "free_a", "free_b", "bought"), _meta, d);
            Assert.AreEqual(0, d.edictWins.Count, "a death records no edict win");
            Assert.AreEqual(0, d.bestEdictWin);

            MetaProgress.Apply(Run(RunOutcome.Survived, 480f, 900, null, 0, 0.45f, "free_a", "free_b"), _meta, d);
            Assert.AreEqual(1, SaveData.Count(d.edictWins, "free_a"));
            Assert.AreEqual(1, SaveData.Count(d.edictWins, "free_b"));
            Assert.AreEqual(2, d.bestEdictWin);

            MetaProgress.Apply(Run(RunOutcome.Survived, 480f, 900, null, 0, 0.25f, "free_a"), _meta, d);
            Assert.AreEqual(2, SaveData.Count(d.edictWins, "free_a"));
            Assert.AreEqual(2, d.bestEdictWin, "the record keeps the most");

            MetaProgress.Apply(Run(RunOutcome.Survived, 480f, 900, null, 0, 0.25f, "free_b", "free_b", "", null), _meta, d);
            Assert.AreEqual(2, SaveData.Count(d.edictWins, "free_b"), "a repeat counts once");
            Assert.AreEqual(2, d.bestEdictWin);

            MetaProgress.Apply(Run(RunOutcome.Survived, 480f, 900), _meta, d); // a win with none on
            Assert.AreEqual(2, d.bestEdictWin);
            Assert.AreEqual(2, d.edictWins.Count);
        }

        [Test]
        public void WinWithEdicts_MetAndProgress()
        {
            var c = C(UnlockConditionKind.WinWithEdicts, "", 2f);
            var d = new SaveData();
            Assert.IsFalse(MetaProgress.Met(c, d));
            d.bestEdictWin = 1;
            Assert.IsFalse(MetaProgress.Met(c, d));
            MetaProgress.Progress(c, d, out float cur, out float tgt);
            Assert.AreEqual(1f, cur);
            Assert.AreEqual(2f, tgt);
            d.bestEdictWin = 2;
            Assert.IsTrue(MetaProgress.Met(c, d));
            d.bestEdictWin = 5;
            MetaProgress.Progress(c, d, out cur, out tgt);
            Assert.AreEqual(2f, cur, "capped at the target");
            Assert.AreEqual(UnlockConditionKind.KillEnemies + 1, UnlockConditionKind.WinWithEdicts, "appended (assets store the index)");
            Assert.AreEqual(15, (int)UnlockConditionKind.WinWithEdicts);
        }

        [Test]
        public void WinWithEdicts_UnlocksAChallengeAtTheRunsEnd()
        {
            var d = new SaveData();
            d.unlocked.Add("edicts");
            var r = MetaProgress.Apply(Run(RunOutcome.Survived, 480f, 900, null, 0, 0.45f, "free_a", "free_b"), _meta, d);
            Assert.IsTrue(d.IsUnlocked("editors_eye"));
            CollectionAssert.Contains(r.NewUnlocks, _challenge);
            Assert.IsTrue(MetaProgress.IsOpen(_earned, _meta, d));
        }

        // ---------------------------------------------------------------- open, chosen

        [Test]
        public void IsOpen_NeedsTheFeature_ThenEachEdictsUnlock()
        {
            var d = new SaveData { renown = 500 };
            Assert.IsFalse(MetaProgress.EdictsOpen(_meta, d), "closed until The Editor Takes Notice");
            Assert.IsFalse(MetaProgress.IsOpen(_freeA, _meta, d));
            Assert.IsFalse(MetaProgress.IsOpen(_bought, _meta, d));

            d.unlocked.Add(_feature.id);
            Assert.IsTrue(MetaProgress.EdictsOpen(_meta, d));
            Assert.IsTrue(MetaProgress.IsOpen(_freeA, _meta, d), "a free edict opens with the feature");
            Assert.IsFalse(MetaProgress.IsOpen(_bought, _meta, d), "a Market edict before Buy");
            Assert.IsFalse(MetaProgress.IsOpen(_earned, _meta, d), "a challenge edict before its challenge");
            Assert.AreEqual(BuyResult.Bought, MetaProgress.Buy(_market, d));
            Assert.IsTrue(MetaProgress.IsOpen(_bought, _meta, d), "after Buy");

            Assert.IsFalse(MetaProgress.IsOpen(null, _meta, d));
            Assert.IsFalse(MetaProgress.EdictsOpen(null, d));
            _meta.edictsUnlock = null;
            Assert.IsTrue(MetaProgress.EdictsOpen(_meta, new SaveData()), "no feature unlock set = open");
        }

        [Test]
        public void ChosenEdicts_DropsLockedAndUnknown_KeepsMetaOrder()
        {
            var d = new SaveData();
            d.unlocked.Add(_feature.id);
            d.edictsChosen.AddRange(new[] { "bought", "nobody_knows", "free_b", "free_a", "free_b" });
            var chosen = MetaProgress.ChosenEdicts(_meta, d);
            Assert.AreEqual(2, chosen.Length, "the Market one isn't bought; the unknown id is gone; the repeat counts once");
            Assert.AreSame(_freeA, chosen[0], "meta.edicts order, not the save's");
            Assert.AreSame(_freeB, chosen[1]);

            d.unlocked.Remove(_feature.id);
            Assert.AreEqual(0, MetaProgress.ChosenEdicts(_meta, d).Length, "nothing while the feature is closed");
            Assert.AreEqual(0, MetaProgress.ChosenEdicts(null, d).Length);
        }

        [Test]
        public void SetChosenEdicts_WritesIds_RoundTrips()
        {
            var d = new SaveData();
            d.unlocked.Add(_feature.id);
            d.edictsChosen.Add("stale");
            MetaProgress.SetChosenEdicts(new[] { _freeB, null, _freeA, _freeB }, d);
            CollectionAssert.AreEqual(new[] { "free_b", "free_a" }, d.edictsChosen, "nulls and repeats dropped, the old choice replaced");
            Assert.IsTrue(SaveSystem.Save(d, _path));
            var back = SaveSystem.Load(_path);
            var chosen = MetaProgress.ChosenEdicts(_meta, back);
            Assert.AreEqual(2, chosen.Length);
            Assert.AreSame(_freeA, chosen[0]);
            MetaProgress.SetChosenEdicts(null, d);
            Assert.AreEqual(0, d.edictsChosen.Count, "null clears");
        }

        [Test]
        public void WaitsForEdicts_HidesTheirUnlocksUntilTheFeatureOpens()
        {
            var other = Unlock("winged_sandals", UnlockCategory.Market);
            var d = new SaveData();
            Assert.IsTrue(MetaProgress.WaitsForEdicts(_market, _meta, d), "a Market edict");
            Assert.IsTrue(MetaProgress.WaitsForEdicts(_challenge, _meta, d), "an edict's challenge");
            Assert.IsFalse(MetaProgress.WaitsForEdicts(_feature, _meta, d), "the feature's own challenge shows");
            Assert.IsFalse(MetaProgress.WaitsForEdicts(other, _meta, d), "everything else shows");
            d.unlocked.Add(_feature.id);
            Assert.IsFalse(MetaProgress.WaitsForEdicts(_market, _meta, d));
            Assert.IsFalse(MetaProgress.WaitsForEdicts(_challenge, _meta, d));
        }

        // ---------------------------------------------------------------- save v2, the Market

        [Test]
        public void Save_V1File_MigratesToV2()
        {
            Assert.AreEqual(2, SaveData.CurrentVersion);
            File.WriteAllText(_path, "{\"version\":1,\"renown\":5,\"runs\":3,\"unlocked\":[\"gorms_flail\"]}");
            var d = SaveSystem.Load(_path);
            Assert.AreEqual(2, d.version);
            Assert.AreEqual(5, d.renown);
            Assert.AreEqual(3, d.runs);
            Assert.IsTrue(d.IsUnlocked("gorms_flail"), "nothing lost");
            Assert.IsNotNull(d.edictsChosen);
            Assert.IsNotNull(d.edictWins);
            Assert.AreEqual(0, d.bestEdictWin);
            Assert.IsTrue(SaveSystem.Save(d, _path));
            StringAssert.Contains("\"edictsChosen\"", File.ReadAllText(_path));
            Assert.AreEqual(2, JsonUtility.FromJson<SaveData>(File.ReadAllText(_path)).version, "written as v2");

            var bare = new SaveData { version = 1, edictsChosen = null, edictWins = null, bestEdictWin = -3 };
            bare.Migrate();
            Assert.IsNotNull(bare.edictsChosen, "Migrate fills the lists");
            Assert.IsNotNull(bare.edictWins);
            Assert.AreEqual(0, bare.bestEdictWin);
            Assert.AreEqual(2, bare.version);
            bare.Migrate();
            Assert.AreEqual(2, bare.version, "idempotent");
        }

        [Test]
        public void Buy_AMarketEdict_SpendsRenown()
        {
            var d = new SaveData { renown = 200 };
            Assert.AreEqual(BuyResult.Bought, MetaProgress.Buy(_market, d));
            Assert.AreEqual(50, d.renown);
            Assert.IsTrue(d.IsUnlocked(_market.id));
            Assert.AreEqual(BuyResult.AlreadyOwned, MetaProgress.Buy(_market, d));
            Assert.AreEqual(50, d.renown);
            Assert.AreEqual(BuyResult.TooExpensive, MetaProgress.Buy(_market, new SaveData { renown = 149 }));
            Assert.AreEqual(BuyResult.NotForSale, MetaProgress.Buy(_challenge, d), "a challenge is earned, not bought");
            // conditions never grant a Market edict
            var rich = new SaveData { lifetimeRenown = 99999 };
            MetaProgress.Apply(Run(RunOutcome.Survived, 480f, 1500, "gorm"), _meta, rich);
            Assert.IsFalse(rich.IsUnlocked(_market.id));
        }

        // ---------------------------------------------------------------- the data (Gladiator/Setup/Meta/Create Edict Data)

        [Test]
        public void EdictData_MatchesTheSpec()
        {
            var cfg = AssetDatabase.LoadAssetAtPath<MetaConfig>("Assets/Data/Meta/MetaConfig.asset");
            Assert.IsNotNull(cfg, "MetaConfig");
            Assert.IsNotNull(cfg.edicts);
            var ids = new[]
            {
                "iron_hides", "hungry_horde", "blood_sport", "glass_jaw", "swift_foes", "empty_purses", "fickle_crowd",
                "meagre_choices", "impatient_emperor", "champions_day", "no_mercy",
            };
            Assert.AreEqual(ids.Length, cfg.edicts.Length, "eleven edicts (run Gladiator/Setup/Meta/Create Edict Data)");
            float bonus = 0f;
            for (int i = 0; i < ids.Length; i++)
            {
                var e = cfg.edicts[i];
                Assert.IsNotNull(e, "edict " + i);
                Assert.AreEqual(ids[i], e.id, "the spec's order");
                Assert.IsFalse(string.IsNullOrEmpty(e.displayName) || string.IsNullOrEmpty(e.effectLine) || string.IsNullOrEmpty(e.description), e.id + " has its texts");
                Assert.IsNotNull(e.icon, e.id + " icon");
                Assert.GreaterOrEqual(e.iconRow, 65, e.id + ": an appended atlas row");
                Assert.Greater(e.renownBonus, 0f);
                Assert.AreEqual(e.id != "no_mercy", e.dailyEligible, e.id + " daily");
                bonus += e.renownBonus;
            }
            Assert.AreEqual(2.35f, bonus, 1e-4f, "all eleven: Renown ×3.35");

            RunModifierDef E(string id) => System.Array.Find(cfg.edicts, x => x.id == id);
            Assert.AreEqual(1.5f, E("iron_hides").enemyHpMul, 1e-5f);
            Assert.AreEqual(1.25f, E("iron_hides").bossHpMul, 1e-5f);
            Assert.AreEqual(1.3f, E("hungry_horde").densityMul, 1e-5f);
            Assert.AreEqual(1.3f, E("hungry_horde").swarmMul, 1e-5f);
            Assert.AreEqual(1.4f, E("blood_sport").playerDamageTakenMul, 1e-5f);
            AssertStat(E("glass_jaw"), StatId.MaxHp, 0.6f);
            Assert.AreEqual(1.15f, E("swift_foes").enemySpeedMul, 1e-5f);
            AssertStat(E("empty_purses"), StatId.GoldGain, 0.5f);
            Assert.AreEqual(1.5f, E("empty_purses").chestPriceMul, 1e-5f);
            Assert.AreEqual(0.5f, E("fickle_crowd").favorGainMul, 1e-5f);
            Assert.AreEqual(2f, E("fickle_crowd").boredomMul, 1e-5f);
            Assert.AreEqual(-1, E("meagre_choices").levelUpCardDelta);
            AssertStat(E("meagre_choices"), StatId.Rerolls, 0f);
            Assert.AreEqual(1.33f, E("impatient_emperor").clockScale, 1e-5f);
            Assert.AreEqual(0.5f, E("champions_day").eliteEveryMul, 1e-5f);
            Assert.AreEqual(0f, E("no_mercy").healingScale, 1e-5f);
            Assert.AreEqual(1f, E("iron_hides").playerDamageTakenMul * E("iron_hides").healingScale * E("iron_hides").clockScale, 1e-5f, "nothing else set");

            // the unlocks
            var feature = cfg.edictsUnlock;
            Assert.IsNotNull(feature, "edictsUnlock = UL_Edicts");
            Assert.AreEqual(UnlockCategory.Challenge, feature.category);
            AssertCondition(feature, UnlockConditionKind.DefeatBoss, 1f, "gorm");
            foreach (var id in new[] { "iron_hides", "hungry_horde", "blood_sport" }) Assert.IsNull(E(id).unlock, id + " opens with the feature");
            foreach (var id in new[] { "glass_jaw", "swift_foes", "empty_purses", "fickle_crowd" })
            {
                var u = E(id).unlock;
                Assert.IsNotNull(u, id);
                Assert.AreEqual(UnlockCategory.Market, u.category, id);
                Assert.AreEqual(150, u.renownCost, id);
            }
            Assert.AreSame(E("meagre_choices").unlock, E("impatient_emperor").unlock, "Under the Editor's Eye opens both");
            AssertCondition(E("meagre_choices").unlock, UnlockConditionKind.WinWithEdicts, 2f, null);
            AssertCondition(E("champions_day").unlock, UnlockConditionKind.ReachArena, 3f, null);
            AssertCondition(E("no_mercy").unlock, UnlockConditionKind.WinWithEdicts, 4f, null);
            var all = new HashSet<UnlockDef>(cfg.unlocks);
            Assert.IsTrue(all.Contains(feature), "MetaConfig.unlocks has UL_Edicts");
            foreach (var e in cfg.edicts) if (e.unlock != null) Assert.IsTrue(all.Contains(e.unlock), e.id + "'s unlock is in MetaConfig.unlocks");
        }

        static void AssertStat(RunModifierDef e, StatId stat, float mul)
        {
            Assert.AreEqual(1, e.playerStats.Length, e.id + " stat mods");
            Assert.AreEqual(stat, e.playerStats[0].stat, e.id);
            Assert.AreEqual(ModOp.Multiply, e.playerStats[0].op, e.id);
            Assert.AreEqual(mul, e.playerStats[0].value, 1e-5f, e.id);
        }

        static void AssertCondition(UnlockDef u, UnlockConditionKind kind, float amount, string id)
        {
            Assert.IsNotNull(u);
            Assert.AreEqual(1, u.conditions.Length, u.id);
            Assert.AreEqual(kind, u.conditions[0].kind, u.id);
            Assert.AreEqual(amount, u.conditions[0].amount, 1e-5f, u.id);
            if (id != null) Assert.AreEqual(id, u.conditions[0].id, u.id);
            if (u.category != UnlockCategory.Market) Assert.AreEqual(UnlockCategory.Challenge, u.category, u.id);
        }
    }
}
