using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Gladiator.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;



namespace Gladiator.Tests
{
    /// <summary>
    /// R8 the Daily Games, the core (docs/tracks/R7-contract.md "R8-B"): the day's plan (DailyGames.For: deterministic,
    /// order-free, only what's earned, distinct eligible edicts), the UTC day and its text, the seed and its salt, one scored
    /// attempt a day (BeginDaily), the ledger and the counts (Apply), Renown ×1.5 on the scored attempt, the two condition
    /// kinds, save v2 → v3, and GameSession's seeds (fresh, SAME SEED, the daily's retry, -runSeed) under SuppressLoad.
    /// </summary>
    public class DailyTests
    {
        const int Day = 20261022;
        const string ScenePath = "Assets/Scenes/DailyTestArena.unity";

        string _path;
        MetaConfig _meta;
        RunDef[] _runs;
        GladiatorDef[] _gladiators;
        RunModifierDef[] _edicts; // [0..4] eligible, [5] not (No Mercy's flag)
        readonly List<Object> _made = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            _path = Path.Combine(Application.temporaryCachePath, "daily_test_save.json");
            DeleteFiles();
            SaveSystem.PathOverride = _path;
            MetaProgress.Reload();
            GameSession.Clear();
            RunModifiers.Clear();
            _runs = new[] { RunAsset("slave_pit"), RunAsset("provincial_pit"), RunAsset("amphitheater"), RunAsset("grand_arena") };
            _gladiators = new[] { Gladiator("nobody"), Gladiator("pit_goblin"), Gladiator("blood_knight"), Gladiator("murder_machine") };
            _edicts = new[]
            {
                Edict("iron_hides", 0.25f, true), Edict("hungry_horde", 0.25f, true), Edict("blood_sport", 0.2f, true),
                Edict("glass_jaw", 0.25f, true), Edict("swift_foes", 0.2f, true), Edict("no_mercy", 0.3f, false),
            };
            _meta = Make<MetaConfig>();
            _meta.edicts = _edicts;
        }

        [TearDown]
        public void TearDown()
        {
            GameSession.Clear();
            RunModifiers.Clear();
            SaveSystem.PathOverride = null;
            MetaProgress.Reload();
            DeleteFiles();
            foreach (var o in _made) if (o != null) Object.DestroyImmediate(o);
            _made.Clear();
        }

        void DeleteFiles()
        {
            foreach (var f in new[] { _path, _path + ".corrupt", _path + ".tmp" }) if (File.Exists(f)) File.Delete(f);
        }

        T Make<T>() where T : ScriptableObject
        {
            var o = ScriptableObject.CreateInstance<T>();
            _made.Add(o);
            return o;
        }

        RunDef RunAsset(string id)
        {
            var r = Make<RunDef>();
            r.id = id; r.name = "RD_" + id;
            r.arenas = new[] { new RunDef.Arena { displayName = id, scenePath = "Assets/Scenes/" + id + ".unity" } };
            return r;
        }

        GladiatorDef Gladiator(string id)
        {
            var g = Make<GladiatorDef>();
            g.id = id; g.name = "GL_" + id;
            return g;
        }

        RunModifierDef Edict(string id, float bonus, bool eligible)
        {
            var e = Make<RunModifierDef>();
            e.id = id; e.displayName = id; e.name = "ED_" + id; e.renownBonus = bonus; e.dailyEligible = eligible;
            return e;
        }

        DailyPlan PlanFor(int date) => DailyGames.For(date, _meta, _runs, _gladiators);

        static T[] Reversed<T>(T[] a)
        {
            var r = (T[])a.Clone();
            Array.Reverse(r);
            return r;
        }

        static string EdictKey(in DailyPlan p)
        {
            var ids = new string[p.Edicts.Length];
            for (int i = 0; i < ids.Length; i++) ids[i] = p.Edicts[i].id;
            return string.Join("|", ids);
        }

        static int NextDay(int date)
        {
            Assert.IsTrue(DailyGames.TryDate(date, out var d));
            return DailyGames.DateKey(d.AddDays(1));
        }

        static RunSummary DailyRun(RunOutcome outcome, int date, bool scored, float edictBonus = 0f) =>
            new RunSummary
            {
                Outcome = outcome, Elapsed = 480f, Kills = 1000, GoldEarned = 50, ArenaIndex = 0, GladiatorId = "nobody",
                BossDefeated = outcome == RunOutcome.Survived ? "gorm" : null, RunId = "slave_pit",
                Edicts = new[] { "iron_hides", "blood_sport" }, EdictBonus = edictBonus,
                Seed = 0xDEADBEEFu, DailyDate = date, DailyScored = scored,
            };

        // ---------------------------------------------------------------- the plan

        [Test]
        public void For_IsTheSame_ForTheSameDateAndLists()
        {
            var a = PlanFor(Day);
            var b = PlanFor(Day);
            Assert.IsTrue(a.IsValid);
            Assert.AreEqual(Day, a.Date);
            Assert.AreEqual(DailyGames.SeedFor(Day, _meta.dailySalt), a.Seed, "the seed is the day's");
            Assert.AreEqual(a.Seed, b.Seed);
            Assert.AreSame(a.Run, b.Run);
            Assert.AreSame(a.Gladiator, b.Gladiator);
            CollectionAssert.AreEqual(a.Edicts, b.Edicts);
        }

        [Test]
        public void For_ChangesWithTheDate()
        {
            var seeds = new HashSet<uint>();
            var runs = new HashSet<RunDef>();
            var gladiators = new HashSet<GladiatorDef>();
            var edicts = new HashSet<string>();
            int date = 20261001;
            for (int i = 0; i < 30; i++, date = NextDay(date))
            {
                var p = PlanFor(date);
                seeds.Add(p.Seed);
                runs.Add(p.Run);
                gladiators.Add(p.Gladiator);
                edicts.Add(EdictKey(p));
            }
            Assert.AreEqual(30, seeds.Count, "a new seed every day");
            Assert.Greater(runs.Count, 1, "the arena changes over a month");
            Assert.Greater(gladiators.Count, 1, "the gladiator changes over a month");
            Assert.Greater(edicts.Count, 1, "the edicts change over a month");
        }

        [Test]
        public void For_IgnoresTheListsOrder_NullsAndRepeats()
        {
            var shuffledRuns = new[] { _runs[2], null, _runs[0], _runs[3], _runs[1], _runs[2] };
            var shuffledGladiators = new List<GladiatorDef> { null, _gladiators[3], _gladiators[1], _gladiators[0], _gladiators[2], _gladiators[1] };
            var reversedMeta = Make<MetaConfig>();
            reversedMeta.edicts = Reversed(_edicts);
            int date = Day;
            for (int i = 0; i < 20; i++, date = NextDay(date))
            {
                var a = PlanFor(date);
                var b = DailyGames.For(date, reversedMeta, shuffledRuns, shuffledGladiators);
                Assert.AreSame(a.Run, b.Run, "arena on " + date);
                Assert.AreSame(a.Gladiator, b.Gladiator, "gladiator on " + date);
                CollectionAssert.AreEqual(a.Edicts, b.Edicts, "edicts on " + date);
            }
        }

        [Test]
        public void For_PicksOnlyFromTheEarnedLists()
        {
            var earnedRuns = new[] { _runs[0], _runs[2] };
            var earnedGladiators = new[] { _gladiators[1] };
            int date = Day;
            var seen = new HashSet<RunDef>();
            for (int i = 0; i < 40; i++, date = NextDay(date))
            {
                var p = DailyGames.For(date, _meta, earnedRuns, earnedGladiators);
                CollectionAssert.Contains(earnedRuns, p.Run);
                Assert.AreSame(_gladiators[1], p.Gladiator, "the only earned gladiator");
                seen.Add(p.Run);
            }
            Assert.AreEqual(2, seen.Count, "both earned arenas come up");
        }

        [Test]
        public void For_GivesDistinctEligibleEdicts()
        {
            Assert.AreEqual(2, _meta.dailyEdicts, "the spec's two a day");
            var dup = Edict("iron_hides", 0.25f, true); // a second def with Iron Hides' id: never twice in one day
            _meta.edicts = new[] { _edicts[0], _edicts[1], _edicts[2], _edicts[3], _edicts[4], _edicts[5], dup, null };
            int date = 20260101;
            var all = new HashSet<RunModifierDef>();
            for (int i = 0; i < 60; i++, date = NextDay(date))
            {
                var p = PlanFor(date);
                Assert.AreEqual(2, p.Edicts.Length, "two on " + date);
                Assert.AreNotEqual(p.Edicts[0].id, p.Edicts[1].id, "distinct on " + date);
                foreach (var e in p.Edicts)
                {
                    Assert.IsTrue(e.dailyEligible, e.id + " isn't daily-eligible");
                    all.Add(e);
                }
            }
            Assert.Greater(all.Count, 3, "the draw ranges over the eligible edicts");

            _meta.dailyEdicts = 3;
            Assert.AreEqual(3, PlanFor(Day).Edicts.Length);
            _meta.dailyEdicts = 9; // more than there are: every eligible one, once each
            Assert.AreEqual(5, PlanFor(Day).Edicts.Length);
            _meta.dailyEdicts = 0;
            Assert.AreEqual(0, PlanFor(Day).Edicts.Length);
            _meta.edicts = Array.Empty<RunModifierDef>();
            _meta.dailyEdicts = 2;
            var none = PlanFor(Day);
            Assert.IsTrue(none.IsValid, "a daily without edicts is still a daily");
            Assert.IsNotNull(none.Edicts);
            Assert.AreEqual(0, none.Edicts.Length);
        }

        [Test]
        public void For_IsInvalid_WithNoArenaOrGladiator()
        {
            Assert.IsFalse(DailyGames.For(Day, _meta, Array.Empty<RunDef>(), _gladiators).IsValid, "no arena");
            Assert.IsFalse(DailyGames.For(Day, _meta, _runs, Array.Empty<GladiatorDef>()).IsValid, "no gladiator");
            Assert.IsFalse(DailyGames.For(Day, _meta, null, null).IsValid, "null lists");
            Assert.IsFalse(DailyGames.For(Day, _meta, new RunDef[] { null }, _gladiators).IsValid, "only nulls");
            Assert.IsFalse(DailyGames.For(0, _meta, _runs, _gladiators).IsValid, "no date");
            Assert.IsFalse(default(DailyPlan).IsValid);
            Assert.IsTrue(DailyGames.For(Day, null, _runs, _gladiators).IsValid, "no MetaConfig: the default salt, no edicts");
            Assert.AreEqual(0, DailyGames.For(Day, null, _runs, _gladiators).Edicts.Length);
        }

        // ---------------------------------------------------------------- the day, the seed

        [Test]
        public void DateKey_DateText_UntilNext_AtTheEndOfTheUtcDay()
        {
            var late = new DateTime(2026, 10, 22, 23, 59, 30, DateTimeKind.Utc);
            Assert.AreEqual(20261022, DailyGames.DateKey(late));
            Assert.AreEqual(20261023, DailyGames.DateKey(late.AddSeconds(30)), "midnight UTC is the next day");
            Assert.AreEqual(20261022, DailyGames.DateKey(new DateTime(2026, 10, 22, 12, 0, 0, DateTimeKind.Unspecified)));
            Assert.AreEqual(30.0, DailyGames.UntilNext(late).TotalSeconds, 1e-6);
            Assert.AreEqual(24.0, DailyGames.UntilNext(new DateTime(2026, 10, 23, 0, 0, 0, DateTimeKind.Utc)).TotalHours, 1e-6);
            Assert.AreEqual(20270101, NextDay(20261231), "the year rolls over");

            var culture = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("tr-TR"); // a culture with its own month names and casing
                Assert.AreEqual("22 OCT 2026", DailyGames.DateText(20261022));
                Assert.AreEqual("5 JAN 2027", DailyGames.DateText(20270105));
                Assert.AreEqual("29 FEB 2028", DailyGames.DateText(20280229));
            }
            finally { CultureInfo.CurrentCulture = culture; }
            Assert.AreEqual("", DailyGames.DateText(0));
            Assert.AreEqual("20261332", DailyGames.DateText(20261332), "not a day: the number as it is");
            Assert.IsFalse(DailyGames.TryDate(20270229, out _), "2027 isn't a leap year");

            int today = DailyGames.Today();
            Assert.AreEqual(DailyGames.DateKey(DateTime.UtcNow), today);
        }

        [Test]
        public void SeedFor_ChangesWithTheSaltAndTheDate()
        {
            uint a = DailyGames.SeedFor(Day, "gladiator-daily-1");
            Assert.AreEqual(a, DailyGames.SeedFor(Day, "gladiator-daily-1"));
            Assert.AreNotEqual(a, DailyGames.SeedFor(Day, "gladiator-daily-2"), "a new salt, a new seed");
            Assert.AreNotEqual(a, DailyGames.SeedFor(NextDay(Day), "gladiator-daily-1"), "a new day, a new seed");
            Assert.AreEqual(DailyGames.DefaultSalt, _meta.dailySalt);
            Assert.AreEqual("gladiator-daily-1", DailyGames.DefaultSalt);

            _meta.dailySalt = "gladiator-daily-2";
            Assert.AreEqual(DailyGames.SeedFor(Day, "gladiator-daily-2"), PlanFor(Day).Seed, "the plan uses MetaConfig's salt");
        }

        // ---------------------------------------------------------------- one scored attempt a day

        [Test]
        public void BeginDaily_ScoredOnce_ThenPractice_ThenScoredTheNextDay()
        {
            var d = new SaveData();
            Assert.IsFalse(MetaProgress.DailyAttempted(Day, d));
            Assert.IsTrue(MetaProgress.BeginDaily(Day, d), "the first attempt is scored");
            Assert.AreEqual(Day, d.dailyAttempt);
            Assert.IsTrue(MetaProgress.DailyAttempted(Day, d));
            Assert.IsFalse(MetaProgress.BeginDaily(Day, d), "the second is practice");
            Assert.IsFalse(MetaProgress.BeginDaily(Day, d));
            int tomorrow = NextDay(Day);
            Assert.IsFalse(MetaProgress.DailyAttempted(tomorrow, d));
            Assert.IsTrue(MetaProgress.BeginDaily(tomorrow, d), "a new day, a new scored attempt");
            Assert.IsFalse(MetaProgress.BeginDaily(Day, d), "setting the clock back gives nothing");
            Assert.IsFalse(MetaProgress.BeginDaily(0, d), "no date: practice");
            Assert.IsFalse(MetaProgress.BeginDaily(Day, null));
        }

        [Test]
        public void BeginDaily_SavesTheAttemptAtOnce()
        {
            Assert.IsTrue(MetaProgress.BeginDaily(Day));
            Assert.IsTrue(File.Exists(_path), "written before the run even loads");
            MetaProgress.Reload(); // as if the game was quit mid-run
            Assert.AreEqual(Day, MetaProgress.Data.dailyAttempt);
            Assert.IsTrue(MetaProgress.DailyAttempted(Day));
            Assert.IsFalse(MetaProgress.BeginDaily(Day), "quitting doesn't give another");
        }

        [Test]
        public void DailyOpen_NeedsItsUnlock()
        {
            var d = new SaveData();
            Assert.IsFalse(MetaProgress.DailyOpen(null, d), "no MetaConfig");
            Assert.IsTrue(MetaProgress.DailyOpen(_meta, d), "no unlock set: open");
            var feature = Make<UnlockDef>();
            feature.id = "daily";
            feature.conditions = new[] { new UnlockCondition { kind = UnlockConditionKind.DefeatBoss, id = "gorm", amount = 1f } };
            _meta.dailyUnlock = feature;
            _meta.unlocks = new[] { feature };
            Assert.IsFalse(MetaProgress.DailyOpen(_meta, d), "before Gorm falls");
            var r = MetaProgress.Apply(new RunSummary { Outcome = RunOutcome.Survived, Elapsed = 480f, BossDefeated = "gorm" }, _meta, d);
            CollectionAssert.Contains(r.NewUnlocks, feature, "beating Gorm opens it");
            Assert.IsTrue(MetaProgress.DailyOpen(_meta, d));
        }

        // ---------------------------------------------------------------- the ledger, the counts, the Renown

        [Test]
        public void Apply_ScoredDaily_CountsAndRecords()
        {
            var d = new SaveData();
            var won = MetaProgress.Apply(DailyRun(RunOutcome.Survived, Day, true), _meta, d);
            Assert.AreEqual(1, d.dailiesPlayed);
            Assert.AreEqual(1, d.dailiesWon);
            Assert.AreEqual(1, d.dailies.Count);
            var rec = d.dailies[0];
            Assert.AreEqual(Day, rec.date);
            Assert.AreEqual("slave_pit", rec.run);
            Assert.AreEqual("nobody", rec.gladiator);
            Assert.AreEqual("iron_hides|blood_sport", rec.edicts);
            Assert.AreEqual((int)RunOutcome.Survived, rec.outcome);
            Assert.AreEqual(480f, rec.elapsed);
            Assert.AreEqual(1000, rec.kills);
            Assert.AreEqual(won.Renown, rec.renown, "the Renown the run paid");
            Assert.AreEqual(0xDEADBEEFL, rec.seed);
            Assert.AreEqual(0.5f, won.DailyBonus);

            MetaProgress.Apply(DailyRun(RunOutcome.Died, NextDay(Day), true), _meta, d);
            Assert.AreEqual(2, d.dailiesPlayed, "a loss is played");
            Assert.AreEqual(1, d.dailiesWon, "but not won");
            Assert.AreEqual((int)RunOutcome.Died, d.dailies[1].outcome);
            Assert.AreEqual(NextDay(Day), d.dailies[1].date, "oldest first");
        }

        [Test]
        public void Apply_TrimsTheLedger_ToMetaConfigsLength()
        {
            Assert.AreEqual(30, _meta.dailyLedger);
            var d = new SaveData();
            int date = 20260101;
            var dates = new List<int>();
            for (int i = 0; i < 35; i++, date = NextDay(date))
            {
                dates.Add(date);
                MetaProgress.Apply(DailyRun(i % 2 == 0 ? RunOutcome.Survived : RunOutcome.Died, date, true), _meta, d);
            }
            Assert.AreEqual(35, d.dailiesPlayed, "the counts keep everything");
            Assert.AreEqual(18, d.dailiesWon);
            Assert.AreEqual(30, d.dailies.Count, "the ledger keeps the last 30");
            Assert.AreEqual(dates[5], d.dailies[0].date, "the five oldest dropped off");
            Assert.AreEqual(dates[34], d.dailies[29].date);

            _meta.dailyLedger = 7;
            MetaProgress.Apply(DailyRun(RunOutcome.Died, date, true), _meta, d);
            Assert.AreEqual(7, d.dailies.Count);
            Assert.AreEqual(date, d.dailies[6].date);
        }

        [Test]
        public void Apply_PracticeOrNormalRun_TouchesNoDailyRecord()
        {
            var d = new SaveData();
            var practice = MetaProgress.Apply(DailyRun(RunOutcome.Survived, Day, false), _meta, d);
            var normal = MetaProgress.Apply(DailyRun(RunOutcome.Survived, 0, false), _meta, d);
            Assert.AreEqual(0, d.dailiesPlayed);
            Assert.AreEqual(0, d.dailiesWon);
            Assert.AreEqual(0, d.dailies.Count);
            Assert.AreEqual(0f, practice.DailyBonus);
            Assert.AreEqual(0f, normal.DailyBonus);
            Assert.AreEqual(practice.BaseRenown, practice.Renown, "practice pays a normal run's Renown");
            Assert.AreEqual(2, d.runs, "the runs still count as runs");
        }

        [Test]
        public void RenownFor_TimesOneAndAHalf_OnlyWhenScored_AndStacksWithEdicts()
        {
            // 8 min (32) + 1,000 kills (20) + Gorm (40) + victory (20) = 112
            Assert.AreEqual(112, _meta.BaseRenownFor(DailyRun(RunOutcome.Survived, Day, true)));
            Assert.AreEqual(168, _meta.RenownFor(DailyRun(RunOutcome.Survived, Day, true)), "112 × 1.5");
            Assert.AreEqual(112, _meta.RenownFor(DailyRun(RunOutcome.Survived, Day, false)), "practice");
            Assert.AreEqual(112, _meta.RenownFor(DailyRun(RunOutcome.Survived, 0, false)), "a normal run");
            Assert.AreEqual(244, _meta.RenownFor(DailyRun(RunOutcome.Survived, Day, true, 0.45f)), "112 × 1.45 × 1.5 = 243.6");
            Assert.AreEqual(162, _meta.RenownFor(DailyRun(RunOutcome.Survived, Day, false, 0.45f)), "112 × 1.45");
            Assert.AreEqual(1.5f, _meta.DailyMultiplier(DailyRun(RunOutcome.Died, Day, true)));
            Assert.AreEqual(1f, _meta.DailyMultiplier(DailyRun(RunOutcome.Died, Day, false)));

            var d = new SaveData();
            var r = MetaProgress.Apply(DailyRun(RunOutcome.Survived, Day, true, 0.45f), _meta, d);
            Assert.AreEqual(112, r.BaseRenown);
            Assert.AreEqual(0.45f, r.EdictBonus, 1e-6f);
            Assert.AreEqual(0.5f, r.DailyBonus, 1e-6f);
            Assert.AreEqual(244, r.Renown);
            Assert.AreEqual(244, d.renown);

            _meta.dailyRenownBonus = 0f;
            Assert.AreEqual(112, _meta.RenownFor(DailyRun(RunOutcome.Survived, Day, true)), "the bonus is data");
        }

        // ---------------------------------------------------------------- the conditions

        [Test]
        public void DailiesPlayedAndWon_MetAndProgress()
        {
            Assert.AreEqual(16, (int)UnlockConditionKind.DailiesPlayed, "appended after WinWithEdicts");
            Assert.AreEqual(17, (int)UnlockConditionKind.DailiesWon);
            var played = new UnlockCondition { kind = UnlockConditionKind.DailiesPlayed, amount = 3f };
            var won = new UnlockCondition { kind = UnlockConditionKind.DailiesWon, amount = 1f };
            var d = new SaveData { dailiesPlayed = 2, dailiesWon = 0 };
            Assert.IsFalse(MetaProgress.Met(played, d));
            Assert.IsFalse(MetaProgress.Met(won, d));
            MetaProgress.Progress(played, d, out float cur, out float tgt);
            Assert.AreEqual(2f, cur);
            Assert.AreEqual(3f, tgt);
            MetaProgress.Progress(won, d, out cur, out tgt);
            Assert.AreEqual(0f, cur);
            Assert.AreEqual(1f, tgt);
            d.dailiesPlayed = 3; d.dailiesWon = 4;
            Assert.IsTrue(MetaProgress.Met(played, d));
            Assert.IsTrue(MetaProgress.Met(won, d));
            MetaProgress.Progress(won, d, out cur, out tgt);
            Assert.AreEqual(1f, cur, "capped at the target");
        }

        [Test]
        public void DailyChallenges_UnlockAtTheRunsEnd_WithTheirBounties()
        {
            var bread = Make<UnlockDef>();
            bread.id = "daily_bread"; bread.renownBounty = 100;
            bread.conditions = new[] { new UnlockCondition { kind = UnlockConditionKind.DailiesPlayed, amount = 3f } };
            var champion = Make<UnlockDef>();
            champion.id = "champion_of_the_day"; champion.renownBounty = 150;
            champion.conditions = new[] { new UnlockCondition { kind = UnlockConditionKind.DailiesWon, amount = 1f } };
            _meta.unlocks = new[] { bread, champion };
            var d = new SaveData();

            var r1 = MetaProgress.Apply(DailyRun(RunOutcome.Died, 20261020, true), _meta, d);
            Assert.AreEqual(0, r1.NewUnlocks.Count);
            var practice = MetaProgress.Apply(DailyRun(RunOutcome.Survived, 20261020, false), _meta, d);
            Assert.AreEqual(0, practice.NewUnlocks.Count, "a practice win isn't a daily won");
            var r2 = MetaProgress.Apply(DailyRun(RunOutcome.Died, 20261021, true), _meta, d);
            Assert.AreEqual(0, r2.NewUnlocks.Count, "two played, none won");
            var r3 = MetaProgress.Apply(DailyRun(RunOutcome.Survived, 20261022, true), _meta, d);
            CollectionAssert.AreEquivalent(new[] { bread, champion }, r3.NewUnlocks, "the third daily, and the first won");
            Assert.AreEqual(250, r3.Bounty);
            Assert.AreEqual(r1.Renown + practice.Renown + r2.Renown + r3.Renown + 250, d.renown, "bounties paid on top, never multiplied");
        }

        // ---------------------------------------------------------------- the save

        [Test]
        public void Save_V2File_MigratesToV3()
        {
            Assert.AreEqual(3, SaveData.CurrentVersion);
            File.WriteAllText(_path, "{\"version\":2,\"renown\":7,\"runs\":4,\"edictsChosen\":[\"iron_hides\"],\"bestEdictWin\":1," +
                                     "\"unlocked\":[\"edicts\"]}");
            var d = SaveSystem.Load(_path);
            Assert.AreEqual(3, d.version);
            Assert.AreEqual(7, d.renown);
            Assert.AreEqual(4, d.runs);
            CollectionAssert.AreEqual(new[] { "iron_hides" }, d.edictsChosen, "nothing lost");
            Assert.AreEqual(1, d.bestEdictWin);
            Assert.IsTrue(d.IsUnlocked("edicts"));
            Assert.IsNotNull(d.dailies, "Migrate fills the ledger");
            Assert.AreEqual(0, d.dailies.Count);
            Assert.AreEqual(0, d.dailyAttempt);
            Assert.AreEqual(0, d.dailiesPlayed);
            Assert.AreEqual(0, d.dailiesWon);

            d.dailyAttempt = Day; d.dailiesPlayed = 1; d.dailiesWon = 1;
            d.dailies.Add(new DailyRecord { date = Day, run = "slave_pit", gladiator = "nobody", edicts = "a|b", outcome = 2,
                                            elapsed = 480f, kills = 1904, renown = 412, seed = 0xFFFFFFFFL });
            Assert.IsTrue(SaveSystem.Save(d, _path));
            string json = File.ReadAllText(_path);
            StringAssert.Contains("\"dailies\"", json);
            StringAssert.Contains("\"dailyAttempt\"", json);
            var back = SaveSystem.Load(_path);
            Assert.AreEqual(3, back.version, "written as v3");
            Assert.AreEqual(Day, back.dailyAttempt);
            Assert.AreEqual(1, back.dailies.Count);
            Assert.AreEqual(1904, back.dailies[0].kills);
            Assert.AreEqual("a|b", back.dailies[0].edicts);
            Assert.AreEqual(4294967295L, back.dailies[0].seed, "a full uint seed survives the file");

            var bare = new SaveData { version = 2, dailies = null, dailyAttempt = -1, dailiesPlayed = -2, dailiesWon = -3 };
            bare.Migrate();
            Assert.IsNotNull(bare.dailies);
            Assert.AreEqual(0, bare.dailyAttempt);
            Assert.AreEqual(0, bare.dailiesPlayed);
            Assert.AreEqual(0, bare.dailiesWon);
            Assert.AreEqual(3, bare.version);
            bare.Migrate();
            Assert.AreEqual(3, bare.version, "idempotent");
        }

        [Test]
        public void Save_V1File_RunsEveryStep()
        {
            File.WriteAllText(_path, "{\"version\":1,\"renown\":5,\"unlocked\":[\"gorms_flail\"]}");
            var d = SaveSystem.Load(_path);
            Assert.AreEqual(3, d.version);
            Assert.IsNotNull(d.edictsChosen, "the v2 step");
            Assert.IsNotNull(d.dailies, "the v3 step");
            Assert.IsTrue(d.IsUnlocked("gorms_flail"));
        }

        // ---------------------------------------------------------------- GameSession's seeds (SuppressLoad: no scene changes)

        [Test]
        public void StartRun_SetsAFreshSeed_AndRecordsTheLoad()
        {
            GameSession.SuppressLoad = true;
            GameSession.StartRun(_gladiators[0], _runs[0]);
            Assert.AreNotEqual(0u, GameSession.Seed, "a seed for every run");
            Assert.AreEqual(GameSession.Seed, GameRandom.RunSeed, "GameRandom.BeginRun(Seed) before the load");
            Assert.AreEqual(_runs[0].arenas[0].scenePath, GameSession.LastLoadPath);
            Assert.AreSame(_gladiators[0], GameSession.Gladiator);
            Assert.AreSame(_runs[0], GameSession.Run);
            Assert.IsFalse(GameSession.HasDaily);
            Assert.IsFalse(GameSession.DailyScored);
            uint first = GameSession.Seed;
            GameSession.StartRun(_gladiators[0], _runs[0]);
            Assert.AreNotEqual(first, GameSession.Seed, "the next run, a new seed");

            GameSession.StartRun(_gladiators[1], _runs[1], 0x7F3A91C2u);
            Assert.AreEqual(0x7F3A91C2u, GameSession.Seed, "an explicit seed");
            Assert.AreEqual(0x7F3A91C2u, GameRandom.RunSeed);
            Assert.AreEqual(_runs[1].arenas[0].scenePath, GameSession.LastLoadPath);
        }

        [Test]
        public void FreshSeed_DiffersEveryTime_AndIsNeverZero()
        {
            var seen = new HashSet<uint>();
            for (int i = 0; i < 200; i++)
            {
                uint s = GameSession.FreshSeed();
                Assert.AreNotEqual(0u, s);
                seen.Add(s);
            }
            Assert.AreEqual(200, seen.Count);
        }

        [Test]
        public void Retry_ANormalRun_GetsAFreshSeed_SameSeedKeepsIt()
        {
            GameSession.SuppressLoad = true;
            GameSession.StartRun(_gladiators[0], _runs[0], 0x12345678u);
            GameSession.Retry();
            Assert.AreNotEqual(0x12345678u, GameSession.Seed, "RETRY is a new run");
            Assert.AreEqual(GameSession.Seed, GameRandom.RunSeed, "and reseeds before the reload");
            Assert.IsNotNull(GameSession.LastLoadPath, "the reload is recorded");
            uint retried = GameSession.Seed;
            GameSession.RetrySameSeed();
            Assert.AreEqual(retried, GameSession.Seed, "SAME SEED replays it");
            Assert.AreEqual(retried, GameRandom.RunSeed);
            Assert.AreSame(_gladiators[0], GameSession.Gladiator, "the same gladiator");
            Assert.AreSame(_runs[0], GameSession.Run, "and the same run");
        }

        [Test]
        public void StartDaily_ThePlan_AndItsRetryIsPracticeOnTheSameSeed()
        {
            GameSession.SuppressLoad = true;
            var plan = PlanFor(Day);
            GameSession.StartDaily(plan, true);
            Assert.IsTrue(GameSession.HasDaily);
            Assert.IsTrue(GameSession.DailyScored);
            Assert.AreEqual(Day, GameSession.Daily.Date);
            Assert.AreEqual(plan.Seed, GameSession.Seed);
            Assert.AreEqual(plan.Seed, GameRandom.RunSeed);
            Assert.AreSame(plan.Gladiator, GameSession.Gladiator);
            Assert.AreSame(plan.Run, GameSession.Run);
            Assert.AreEqual(plan.Run.arenas[0].scenePath, GameSession.LastLoadPath);
            CollectionAssert.AreEqual(plan.Edicts, RunModifiers.Active, "the day's edicts");

            GameSession.Retry();
            Assert.AreEqual(plan.Seed, GameSession.Seed, "the daily's retry keeps its seed");
            Assert.IsTrue(GameSession.HasDaily);
            Assert.IsFalse(GameSession.DailyScored, "but it's practice now");
            CollectionAssert.AreEqual(plan.Edicts, RunModifiers.Active, "the edicts stay on");

            GameSession.StartDaily(plan, true);
            GameSession.RetrySameSeed();
            Assert.AreEqual(plan.Seed, GameSession.Seed);
            Assert.IsFalse(GameSession.DailyScored, "SAME SEED on a daily is practice too");

            GameSession.StartRun(_gladiators[0], _runs[0], 1u);
            Assert.IsFalse(GameSession.HasDaily, "a normal run leaves the daily");
            Assert.IsFalse(GameSession.DailyScored);
        }

        [Test]
        public void StartDaily_AnInvalidPlan_DoesNothing()
        {
            GameSession.SuppressLoad = true;
            LogAssert.Expect(LogType.Warning, "[GameSession] StartDaily: the plan has no arena or gladiator");
            GameSession.StartDaily(DailyGames.For(Day, _meta, null, _gladiators), true);
            Assert.IsFalse(GameSession.HasDaily);
            Assert.IsNull(GameSession.LastLoadPath);
            Assert.IsNull(GameSession.Run);
        }

        [Test]
        public void RunSeedOverride_StandsInForTheCommandLine()
        {
            GameSession.SuppressLoad = true;
            Assert.IsFalse(GameSession.TryCommandLineSeed(out _), "the Editor's command line has no -runSeed");
            GameSession.RunSeedOverride = 0x7F3A91C2u;
            GameSession.StartRun(_gladiators[0], _runs[0]);
            Assert.AreEqual(0x7F3A91C2u, GameSession.Seed, "-runSeed is honoured");
            Assert.AreEqual(0x7F3A91C2u, GameRandom.RunSeed);
            GameSession.StartRun(_gladiators[0], _runs[0]);
            Assert.AreEqual(0x7F3A91C2u, GameSession.Seed, "every run started from the menu");
            GameSession.Retry();
            Assert.AreNotEqual(0x7F3A91C2u, GameSession.Seed, "RETRY is still a new run");

            GameSession.Clear();
            Assert.IsNull(GameSession.RunSeedOverride, "Clear resets the hook");
            Assert.IsFalse(GameSession.SuppressLoad, "and SuppressLoad");
            Assert.AreEqual(0u, GameSession.Seed);
            Assert.IsNull(GameSession.LastLoadPath);
        }

        [Test]
        public void StartRun_WithoutAnArena_LoadsNothing()
        {
            GameSession.SuppressLoad = true;
            var empty = Make<RunDef>();
            GameSession.StartRun(_gladiators[0], empty, 5u);
            Assert.IsNull(GameSession.LastLoadPath);
            Assert.AreEqual(5u, GameSession.Seed);
        }
    }
}
