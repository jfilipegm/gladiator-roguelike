using System.Collections;
using System.Collections.Generic;
using Gladiator.Core;
using Gladiator.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Gladiator.Tests
{
    /// <summary>
    /// R7 Edicts in a run (docs/tracks/R7-contract.md "R7-A rules"): set before the load, each edict's number reaches the
    /// game once (RunModifiers.ApplyTo at sceneLoaded) and shows in what the game does, measured against the same
    /// measurements without edicts (cached once per fixture). With no edict nothing changes; Retry doesn't compound. The
    /// defs are made here with the spec's numbers (§2.3), not loaded, so the test doesn't wait on the setup script.
    /// </summary>
    public class EdictRunTests
    {
        const string Scene = "Assets/Scenes/Dev/TestArena.unity";
        const float PinnedClock = 60f; // the run clock while measuring (Timeless)
        const float BoredomRise = 5f;  // the master's rise while measuring (slow: the meter never fills)

        /// <summary>Everything Measure reads, with and without edicts.</summary>
        sealed class Snap
        {
            // the hooks
            public float HpMul, ScriptedHpMul, SpeedMul, DamageTakenMul, Density, Swarm, ClockScale, EliteEvery,
                PriceMul, GainMul, BoredomMul, HealingScale;
            public int CardDelta;
            // the player
            public float MaxHp, Hp, GoldGain, Luck, Armor, HealingMul;
            public int Rerolls;
            // the Director at the pinned clock, the chests
            public int TargetAlive, AliveCap, ChestPrice, ChestsOpened;
            public float PaceClock, Clock, Elapsed;
            // what the game did
            public float ConvictHp, RunnerSpeed, BossHp, Damage, Healed, HealedUnscaled, Regen, Favor, BoredomRise;
            public int Gold, LevelUpCards, ChestCards;
        }

        static Snap s_baseline;

        readonly List<Object> _made = new List<Object>();
        Player _player;
        PlayerHealth _health;
        PlayerStats _stats;
        EnemyManager _em;
        Director _dir;
        RunState _run;
        EnemyDef _convict, _runner, _boss;

        [OneTimeSetUp] public void OneTimeSetUp() => s_baseline = null;

        [SetUp]
        public void SetUp()
        {
            RunModifiers.Clear();
            _convict = R2TestArena.Load<EnemyDef>("Assets/Data/Enemies/EN_Convict.asset");
            _runner = R2TestArena.Dummy("edict_runner"); // no jitter: its speed is exact
            _runner.moveSpeed = 3f;
            _boss = R2TestArena.Dummy("edict_boss", EnemyBehaviour.Scripted); // bosses are Scripted: they skip hpMul
        }

        [TearDown]
        public void TearDown()
        {
            RunModifiers.Clear();
            DevSettings.ResetAll();
            GameLoop.TrackHeapPerSystem = false;
            foreach (var o in _made) if (o != null) Object.Destroy(o);
            _made.Clear();
        }

        // ------------------------------------------------------------------ the eleven edicts (spec §2.3)

        RunModifierDef Make(string id, string name, float bonus)
        {
            var d = ScriptableObject.CreateInstance<RunModifierDef>();
            d.id = d.name = id;
            d.displayName = name;
            d.renownBonus = bonus;
            _made.Add(d);
            return d;
        }

        RunModifierDef IronHides() { var d = Make("iron_hides", "Iron Hides", 0.25f); d.enemyHpMul = 1.5f; d.bossHpMul = 1.25f; return d; }
        RunModifierDef HungryHorde() { var d = Make("hungry_horde", "The Hungry Horde", 0.25f); d.densityMul = 1.3f; d.swarmMul = 1.3f; return d; }
        RunModifierDef BloodSport() { var d = Make("blood_sport", "Blood Sport", 0.2f); d.playerDamageTakenMul = 1.4f; return d; }
        RunModifierDef GlassJaw() { var d = Make("glass_jaw", "Glass Jaw", 0.25f); d.playerStats = new[] { new StatMod(StatId.MaxHp, ModOp.Multiply, 0.6f) }; return d; }
        RunModifierDef SwiftFoes() { var d = Make("swift_foes", "Swift Foes", 0.2f); d.enemySpeedMul = 1.15f; return d; }
        RunModifierDef EmptyPurses() { var d = Make("empty_purses", "Empty Purses", 0.15f); d.playerStats = new[] { new StatMod(StatId.GoldGain, ModOp.Multiply, 0.5f) }; d.chestPriceMul = 1.5f; return d; }
        RunModifierDef FickleCrowd() { var d = Make("fickle_crowd", "A Fickle Crowd", 0.15f); d.favorGainMul = 0.5f; d.boredomMul = 2f; return d; }
        RunModifierDef MeagreChoices() { var d = Make("meagre_choices", "Meagre Choices", 0.2f); d.levelUpCardDelta = -1; d.playerStats = new[] { new StatMod(StatId.Rerolls, ModOp.Multiply, 0f) }; return d; }
        RunModifierDef ImpatientEmperor() { var d = Make("impatient_emperor", "The Impatient Emperor", 0.2f); d.clockScale = 1.33f; return d; }
        RunModifierDef ChampionsDay() { var d = Make("champions_day", "Champions' Day", 0.2f); d.eliteEveryMul = 0.5f; return d; }
        RunModifierDef NoMercy() { var d = Make("no_mercy", "No Mercy", 0.3f); d.healingScale = 0f; d.dailyEligible = false; return d; }

        RunModifierDef[] AllEleven() => new[]
        {
            IronHides(), HungryHorde(), BloodSport(), GlassJaw(), SwiftFoes(), EmptyPurses(), FickleCrowd(), MeagreChoices(),
            ImpatientEmperor(), ChampionsDay(), NoMercy(),
        };

        // ------------------------------------------------------------------ boot and measure

        static IEnumerator Until(System.Func<bool> done, float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!done() && Time.realtimeSinceStartup < end) yield return null;
        }

        /// <summary>The test arena with `edicts` set before the load; the run clock pinned at PinnedClock; nobody fighting.</summary>
        IEnumerator Boot(params RunModifierDef[] edicts)
        {
            DevSettings.ResetAll();
            DevSettings.GodMode = true;
            DevSettings.Timeless = true; // pin the run clock
            TimeController.GameSpeed = 1f;
            TimeController.ResetForNewRun();
            RunModifiers.Set(edicts);
            var op = SceneManager.LoadSceneAsync(Scene, LoadSceneMode.Single);
            while (!op.isDone) yield return null;
            yield return null;
            var spawner = Object.FindAnyObjectByType<DebugEnemySpawner>();
            if (spawner != null) spawner.enabled = false;
            _dir = Object.FindAnyObjectByType<Director>(); // a disabled Director clears Director.Instance: keep our own handle
            if (_dir != null) _dir.enabled = false;
            _player = Player.Main;
            _player.OverrideSource = new ScriptedInput();
            _player.GetComponent<WeaponSystem>().RemoveAll();
            _health = _player.GetComponent<PlayerHealth>();
            _health.invulnerableTime = 0f;
            _stats = _player.GetComponent<PlayerStats>();
            _em = EnemyManager.Instance;
            _run = RunState.Current;
            _em.DespawnAll(); // quietly: no kills, no XP
            _run.SetTimeRemaining(_run.config.runDuration - PinnedClock);
            yield return new WaitForSeconds(0.3f); // the starting chests are placed on the first ticks
            PickupSystem.Instance.ClearAll();
        }

        /// <summary>The numbers without edicts, measured once per fixture run.</summary>
        IEnumerator Baseline()
        {
            if (s_baseline != null) yield break;
            yield return Boot();
            var s = new Snap();
            yield return Measure(s);
            s_baseline = s;
        }

        IEnumerator Measure(Snap s)
        {
            var props = Interactables.Instance;
            var favor = Favor.Instance;
            var master = ArenaMaster.Instance;
            var lvl = LevelUpController.Instance;
            Assert.IsNotNull(props, "the test arena has chests");
            Assert.IsNotNull(favor, "and Favor");
            Assert.IsNotNull(master, "and the Arena Master");
            Assert.IsNotNull(lvl, "and level-ups");

            // the hooks
            s.HpMul = _em.hpMul; s.ScriptedHpMul = _em.scriptedHpMul; s.SpeedMul = _em.speedMul;
            s.DamageTakenMul = _run.playerDamageTakenMul;
            s.Density = _dir.densityMultiplier; s.Swarm = _dir.swarmMultiplier; s.ClockScale = _dir.clockScale;
            s.EliteEvery = _dir.eliteEveryOverride > 0f ? _dir.eliteEveryOverride : _dir.config.eliteEvery;
            s.PriceMul = props.priceMul; s.GainMul = favor.gainMul; s.BoredomMul = master.boredomMul;
            s.CardDelta = lvl.levelUpCardDelta; s.HealingScale = _health.HealingScale;

            // the player at the start
            s.MaxHp = _health.MaxHp; s.Hp = _health.Hp; s.HealingMul = _health.HealingMul;
            s.GoldGain = _stats.Get(StatId.GoldGain); s.Luck = _stats.Luck; s.Rerolls = lvl.RerollsLeft;
            s.Armor = Mathf.Clamp(_stats.Get(StatId.Armor), 0f, _health.maxArmor);

            // the Director at the pinned clock, the chests
            s.TargetAlive = _dir.TargetAlive; s.AliveCap = _dir.AliveCap;
            s.PaceClock = _dir.PaceClock; s.Clock = _dir.Clock; s.Elapsed = _run.Elapsed;
            s.ChestPrice = props.ChestPrice; s.ChestsOpened = props.ChestsOpened;

            // enemies as they spawn
            Vector3 at = _player.transform.position + new Vector3(0f, 0f, 7f);
            int c = _em.Spawn(_convict, at);
            int r = _em.Spawn(_runner, at + Vector3.right * 2f);
            int b = _em.Spawn(_boss, at - Vector3.right * 2f);
            Assert.IsTrue(c >= 0 && r >= 0 && b >= 0, "spawned");
            s.ConvictHp = _em.GetMaxHp(c);
            s.RunnerSpeed = _em.GetSpeed(r);
            s.BossHp = _em.GetMaxHp(b);
            _em.DespawnAll();

            // a hit, then healing (in one frame: nothing else heals in between)
            float taken = _health.DamageTaken;
            _health.TakeDamage(new DamageInfo { Amount = 30f, Point = _player.transform.position });
            s.Damage = _health.DamageTaken - taken;
            s.Healed = _health.Heal(5f);
            s.HealedUnscaled = _health.HealUnscaled(5f);

            // Favor from a scripted gain
            float earned = favor.TotalEarned;
            favor.Add(10f);
            s.Favor = favor.TotalEarned - earned;

            // regen (still 20 HP short of full: it has room to heal)
            var regen = new StatMod(StatId.HpRegen, ModOp.Flat, 20f);
            _stats.Block.Add(regen);
            float hp0 = _health.Hp;
            yield return new WaitForSeconds(0.3f);
            s.Regen = _health.Hp - hp0;
            _stats.Block.Remove(regen);

            // a coin worth 10 at the player's feet
            int gold0 = _run.Gold;
            PickupSystem.Instance.Spawn(PickupSystem.Kind.Gold, _player.transform.position, 10f);
            yield return Until(() => _run.Gold != gold0, 3f);
            s.Gold = _run.Gold - gold0;

            // a level-up's cards
            _run.AddXp(_run.XpToNext - _run.Xp + 0.01f);
            yield return Until(() => lvl.IsOpen, 3f);
            Assert.IsTrue(lvl.IsOpen, "the level-up opened");
            Assert.AreEqual(PickSource.LevelUp, lvl.Source);
            s.LevelUpCards = lvl.ChoiceCount;
            lvl.Skip();
            yield return null;

            // a chest's reward cards
            lvl.QueueReward(PickSource.Chest, Rarity.Uncommon);
            yield return Until(() => lvl.IsOpen, 3f);
            Assert.IsTrue(lvl.IsOpen, "the chest's cards opened");
            Assert.AreEqual(PickSource.Chest, lvl.Source);
            s.ChestCards = lvl.ChoiceCount;
            lvl.Skip();
            yield return null;

            // boredom's rise per second while the show is dull (nothing near, nothing dying)
            var cfg = Object.Instantiate(master.config);
            _made.Add(cfg);
            cfg.boredomRise = BoredomRise;
            cfg.boredomFall = BoredomRise;
            master.config = cfg;
            float rise = 0f, time = 0f, b0 = master.Boredom;
            float end = Time.realtimeSinceStartup + 3f;
            while (time < 0.6f && Time.realtimeSinceStartup < end)
            {
                yield return null;
                float b1 = master.Boredom, dt = Time.deltaTime;
                if (master.Dull && !TimeController.IsPaused && dt > 0f && b1 < 99f) { rise += b1 - b0; time += dt; }
                b0 = b1;
            }
            s.BoredomRise = time > 0f ? rise / time : -1f;
        }

        // ------------------------------------------------------------------ the tests

        [UnityTest, Timeout(120000)]
        public IEnumerator NoEdicts_EveryHookIsOne_AndEveryNumberIsTodays()
        {
            yield return Baseline();
            var b = s_baseline;
            yield return Boot(); // a second load without edicts: nothing leaks from one load to the next
            Assert.IsNull(RunModifiers.AppliedTo, "no edicts: nothing applied");
            Assert.IsFalse(RunModifiers.ApplyTo(_run));
            var s = new Snap();
            yield return Measure(s);

            Assert.AreEqual(1f, s.ScriptedHpMul);
            Assert.AreEqual(1f, s.SpeedMul);
            Assert.AreEqual(1f, s.ClockScale);
            Assert.AreEqual(1f, s.PriceMul);
            Assert.AreEqual(1f, s.GainMul);
            Assert.AreEqual(1f, s.BoredomMul);
            Assert.AreEqual(0, s.CardDelta);
            Assert.AreEqual(1f, s.HealingScale);

            // today's formulas
            Assert.AreEqual(_convict.maxHp * s.HpMul, s.ConvictHp, 1e-3f, "a Convict: its HP × the arena's");
            Assert.AreEqual(_runner.moveSpeed, s.RunnerSpeed, 1e-4f, "speed as defined");
            Assert.AreEqual(_boss.maxHp, s.BossHp, 1e-3f, "a boss: its own HP");
            Assert.AreEqual(Interactables.Instance.config.PriceFor(s.ChestsOpened), s.ChestPrice);
            Assert.AreEqual(s.Elapsed, s.PaceClock, 1e-4f, "the Director's clock is the run clock");
            Assert.AreEqual(s.Elapsed, s.Clock, 1e-4f);
            Assert.AreEqual(s.MaxHp, s.Hp, 1e-3f, "full HP");
            Assert.AreEqual(30f * s.DamageTakenMul * (1f - s.Armor), s.Damage, 0.01f, "a hit × the arena's multiplier × armor");
            Assert.AreEqual(5f * s.HealingMul, s.Healed, 1e-3f);
            Assert.AreEqual(5f, s.HealedUnscaled, 1e-3f);
            Assert.Greater(s.Regen, 0f, "regen heals");
            Assert.AreEqual(10f, s.Favor, 1e-3f);
            Assert.AreEqual(10f * s.GoldGain, s.Gold, 1f);
            Assert.AreEqual(BoredomRise, s.BoredomRise, BoredomRise * 0.05f, "boredom rises at the config's rate");
            var cfg = Player.Main.GetComponent<PlayerBuild>().config;
            Assert.GreaterOrEqual(s.LevelUpCards, cfg.CardCount(s.Luck, 0.9999f));
            Assert.LessOrEqual(s.LevelUpCards, cfg.CardCount(s.Luck, 0f));
            Assert.Greater(s.Rerolls, 0);

            // and the same as the first load
            Assert.AreEqual(b.HpMul, s.HpMul, 1e-5f);
            Assert.AreEqual(b.DamageTakenMul, s.DamageTakenMul, 1e-5f);
            Assert.AreEqual(b.Density, s.Density, 1e-5f);
            Assert.AreEqual(b.Swarm, s.Swarm, 1e-5f);
            Assert.AreEqual(b.EliteEvery, s.EliteEvery, 1e-4f);
            Assert.AreEqual(b.MaxHp, s.MaxHp, 1e-3f);
            Assert.AreEqual(b.ConvictHp, s.ConvictHp, 1e-3f);
            Assert.AreEqual(b.TargetAlive, s.TargetAlive);
            Assert.AreEqual(b.ChestPrice, s.ChestPrice);
            Assert.AreEqual(b.Damage, s.Damage, 0.01f);
            Assert.AreEqual(b.Gold, s.Gold);
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator IronHides_SwiftFoes_HungryHorde_ReachTheEnemiesAndTheDirector()
        {
            yield return Baseline();
            var b = s_baseline;
            yield return Boot(IronHides(), SwiftFoes(), HungryHorde());
            Assert.AreSame(_run, RunModifiers.AppliedTo, "applied when the arena loaded");
            var e = new Snap();
            yield return Measure(e);

            Assert.AreEqual(b.HpMul * 1.5f, e.HpMul, 1e-5f, "on top of the arena's own multiplier");
            Assert.AreEqual(b.ConvictHp * 1.5f, e.ConvictHp, b.ConvictHp * 1e-4f, "a Convict spawns with +50 % HP");
            Assert.AreEqual(b.BossHp * 1.25f, e.BossHp, b.BossHp * 1e-4f, "a boss with +25 %");
            Assert.AreEqual(b.RunnerSpeed * 1.15f, e.RunnerSpeed, 1e-3f, "+15 % speed");
            Assert.AreEqual(b.Density * 1.3f, e.Density, 1e-5f);
            Assert.AreEqual(b.Swarm * 1.3f, e.Swarm, 1e-5f, "swarms +30 %");
            if (b.TargetAlive * 1.3f + 2f >= e.AliveCap) Assert.Inconclusive("the test arena's curve is at its cap at " + PinnedClock + " s");
            Assert.AreEqual(b.TargetAlive * 1.3f, e.TargetAlive, 1.5f, "+30 % enemies alive below the cap");
            // nothing else moved
            Assert.AreEqual(b.Damage, e.Damage, 0.01f);
            Assert.AreEqual(b.ChestPrice, e.ChestPrice);
            Assert.AreEqual(b.LevelUpCards, e.LevelUpCards);
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator BloodSport_GlassJaw_NoMercy_ReachThePlayer()
        {
            yield return Baseline();
            var b = s_baseline;
            yield return Boot(BloodSport(), GlassJaw(), NoMercy());

            // the stats page names the edict
            var shares = new StatShare[16];
            int n = _stats.Breakdown(StatId.MaxHp, shares);
            string named = null;
            for (int i = 0; i < n; i++)
                if (shares[i].Source.Kind == StatSourceKind.Edict) named = StatText.SourceName(shares[i].Source, null);
            Assert.AreEqual("Edict: Glass Jaw", named, "the pause stats page lists the edict as a source");

            var e = new Snap();
            yield return Measure(e);
            Assert.AreEqual(b.DamageTakenMul * 1.4f, e.DamageTakenMul, 1e-5f);
            Assert.AreEqual(b.Damage * 1.4f, e.Damage, 0.01f, "+40 % damage taken");
            Assert.AreEqual(b.MaxHp * 0.6f, e.MaxHp, 0.01f, "max HP × 0.6");
            Assert.AreEqual(e.MaxHp, e.Hp, 0.01f, "and HP starts full at the new max");
            Assert.AreEqual(0f, e.HealingScale);
            Assert.AreEqual(0f, e.Healed, "No Mercy: Heal adds 0");
            Assert.AreEqual(0f, e.HealedUnscaled, "HealUnscaled adds 0 (heals on kill)");
            Assert.Greater(b.Regen, 0f);
            Assert.AreEqual(0f, e.Regen, 1e-4f, "regen adds 0");
            Assert.AreEqual(b.ConvictHp, e.ConvictHp, 1e-3f, "the enemies are untouched");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator EmptyPurses_FickleCrowd_MeagreChoices_ReachTheEconomyTheCrowdAndTheCards()
        {
            yield return Baseline();
            var b = s_baseline;
            yield return Boot(EmptyPurses(), FickleCrowd(), MeagreChoices());
            var e = new Snap();
            yield return Measure(e);

            Assert.AreEqual(Mathf.RoundToInt(Interactables.Instance.config.PriceFor(e.ChestsOpened) * 1.5f), e.ChestPrice, "chests cost × 1.5");
            Assert.AreEqual(Mathf.RoundToInt(b.ChestPrice * 1.5f), e.ChestPrice);
            Assert.AreEqual(b.GoldGain * 0.5f, e.GoldGain, 1e-4f);
            Assert.AreEqual(b.Gold * 0.5f, e.Gold, 0.51f, "a coin's gold × 0.5");
            Assert.AreEqual(b.Favor * 0.5f, e.Favor, 1e-3f, "Add(10) gives 5 Favor");
            Assert.Greater(b.BoredomRise, 0f);
            Assert.AreEqual(b.BoredomRise * 2f, e.BoredomRise, b.BoredomRise * 0.1f, "boredom rises twice as fast");
            Assert.AreEqual(0, e.Rerolls, "no rerolls");
            Assert.Greater(b.Rerolls, 0);
            if (e.Luck > 0f || b.Luck > 0f)
            {
                var cfg = Player.Main.GetComponent<PlayerBuild>().config;
                Assert.GreaterOrEqual(e.LevelUpCards, Mathf.Max(1, cfg.CardCount(e.Luck, 0.9999f) - 1));
                Assert.LessOrEqual(e.LevelUpCards, cfg.CardCount(e.Luck, 0f) - 1);
            }
            else
            {
                Assert.AreEqual(b.LevelUpCards - 1, e.LevelUpCards, "a level-up offers one card fewer");
                Assert.AreEqual(b.ChestCards, e.ChestCards, "a chest's reward is unchanged");
            }
            Assert.AreEqual(b.ConvictHp, e.ConvictHp, 1e-3f, "the enemies are untouched");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator ImpatientEmperor_PaceClockRunsFast_ClockCapsAtTheRunLength_SwarmsGoOn()
        {
            yield return Boot(ImpatientEmperor());
            float length = _run.config.runDuration;
            Assert.AreEqual(1.33f, _dir.clockScale, 1e-5f);

            _run.SetTimeRemaining(length - 6f);
            Assert.AreEqual(6f * 1.33f, _dir.PaceClock, 1e-3f, "8 s on the Director's clock at 6 s");
            Assert.AreEqual(_dir.PaceClock, _dir.Clock, 1e-4f, "below the run's length the curves read the fast clock");

            _run.SetTimeRemaining(length * 0.1f); // 90 % of the run: the fast clock is past its end
            Assert.AreEqual(length * 0.9f * 1.33f, _dir.PaceClock, 0.01f, "PaceClock is uncapped");
            Assert.AreEqual(length, _dir.Clock, 1e-3f, "Clock is capped at the run's length (the minute-8 horde holds)");
            float density = _dir.densityMultiplier * (_dir.arenaDensity > 0f ? _dir.arenaDensity : 1f);
            Assert.AreEqual(Mathf.Min(_dir.AliveCap, Mathf.RoundToInt(_dir.config.targetAlive.Evaluate(length) * density)), _dir.TargetAlive);

            // the swarm schedule runs on PaceClock, so swarms keep coming after the curves' clock stopped
            _dir.enabled = true;
            yield return Until(() => _dir.Prewarmed, 15f);
            Assert.IsTrue(_dir.Prewarmed, "the pools filled");
            yield return null; // the jumped clock reschedules the next swarm
            Assert.Greater(_dir.NextSwarmAt, length, "the next swarm is due past the run's length, in PaceClock seconds");
            int swarms = _dir.SwarmsStarted;
            _dir.TriggerSwarmNow();
            yield return null;
            yield return null;
            Assert.AreEqual(swarms + 1, _dir.SwarmsStarted, "a swarm starts after the cap");
            Assert.AreEqual(_dir.PaceClock + _dir.config.swarmEvery, _dir.NextSwarmAt, 0.05f, "and the next is scheduled on PaceClock");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator ChampionsDay_HalvesTheEliteInterval()
        {
            yield return Baseline();
            var b = s_baseline;
            yield return Boot(ChampionsDay());
            float every = _dir.eliteEveryOverride > 0f ? _dir.eliteEveryOverride : _dir.config.eliteEvery;
            Assert.AreEqual(b.EliteEvery * 0.5f, every, 1e-3f, "elites twice as often");

            _run.SetTimeRemaining(_run.config.runDuration - 200f); // elites are in the table by now
            _dir.enabled = true;
            yield return Until(() => _dir.Prewarmed, 15f);
            yield return null;
            int elites = _dir.ElitesSent;
            _dir.TriggerEliteNow();
            yield return null;
            yield return null;
            Assert.AreEqual(elites + 1, _dir.ElitesSent, "an elite came");
            Assert.AreEqual(_dir.PaceClock + b.EliteEvery * 0.5f, _dir.NextEliteAt, 0.05f, "the next one in half the time");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator Retry_ReloadsTheArena_TheEdictsApplyOnce()
        {
            yield return Boot(IronHides(), BloodSport());
            float hpMul = _em.hpMul, taken = _run.playerDamageTakenMul;
            var first = _run;
            Assert.AreSame(first, RunModifiers.AppliedTo);
            Assert.IsFalse(RunModifiers.ApplyTo(first), "never twice to one run");
            Assert.AreEqual(hpMul, _em.hpMul, 1e-6f);

            GameSession.Retry();
            yield return Until(() => RunState.Current != null && !ReferenceEquals(RunState.Current, first), 15f);
            yield return null;
            var second = RunState.Current;
            Assert.IsNotNull(second);
            Assert.AreNotSame(first, second, "a fresh run");
            Assert.AreSame(second, RunModifiers.AppliedTo, "Retry keeps the edicts");
            Assert.AreEqual(hpMul, EnemyManager.Instance.hpMul, 1e-5f, "× 1.5 once, not × 2.25");
            Assert.AreEqual(taken, second.playerDamageTakenMul, 1e-5f, "× 1.4 once");
            Assert.AreEqual(2, RunModifiers.Active.Length);
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator AllEleven_EveryHookAtItsNumber_200Enemies_HeapReport()
        {
            yield return Baseline();
            var b = s_baseline;
            var all = AllEleven();
            yield return Boot(all);
            Assert.AreEqual(11, RunModifiers.Active.Length);
            Assert.AreEqual(2.35f, RunModifiers.RenownBonus, 1e-4f, "×3.35 Renown");
            Assert.AreSame(_run, RunModifiers.AppliedTo);

            var t = RunModifiers.Combine(all);
            Assert.AreEqual(b.HpMul * t.EnemyHpMul, _em.hpMul, 1e-5f);
            Assert.AreEqual(b.ScriptedHpMul * t.BossHpMul, _em.scriptedHpMul, 1e-5f);
            Assert.AreEqual(b.SpeedMul * t.EnemySpeedMul, _em.speedMul, 1e-5f);
            Assert.AreEqual(b.DamageTakenMul * t.PlayerDamageTakenMul, _run.playerDamageTakenMul, 1e-5f);
            Assert.AreEqual(b.Density * t.DensityMul, _dir.densityMultiplier, 1e-5f);
            Assert.AreEqual(b.Swarm * t.SwarmMul, _dir.swarmMultiplier, 1e-5f);
            Assert.AreEqual(b.ClockScale * t.ClockScale, _dir.clockScale, 1e-5f);
            Assert.AreEqual(b.EliteEvery * t.EliteEveryMul, _dir.eliteEveryOverride, 1e-3f);
            Assert.AreEqual(b.PriceMul * t.ChestPriceMul, Interactables.Instance.priceMul, 1e-5f);
            Assert.AreEqual(b.GainMul * t.FavorGainMul, Favor.Instance.gainMul, 1e-5f);
            Assert.AreEqual(b.BoredomMul * t.BoredomMul, ArenaMaster.Instance.boredomMul, 1e-5f);
            Assert.AreEqual(b.CardDelta + t.LevelUpCardDelta, LevelUpController.Instance.levelUpCardDelta);
            Assert.AreEqual(0f, _health.HealingScale);
            Assert.AreEqual(b.MaxHp * 0.6f, _health.MaxHp, 0.01f);
            Assert.AreEqual(_health.MaxHp, _health.Hp, 0.01f);
            Assert.AreEqual(b.GoldGain * 0.5f, _stats.Get(StatId.GoldGain), 1e-4f);
            Assert.AreEqual(0, LevelUpController.Instance.RerollsLeft);

            // 5 s with 200 enemies on the player: log the per-system heap growth. Not asserted: in the Editor the heap
            // counter moves with the Editor's own allocations (R3); zero GC is checked in the player by a bot run.
            GameLoop.TrackHeapPerSystem = true;
            for (int i = 0; i < 200; i++) _em.Spawn(_convict, _em.EdgePoint(i * Mathf.PI * 2f / 200f, 3f));
            yield return new WaitForSeconds(5f);
            Debug.Log("[EdictRunTests] all eleven edicts, " + _em.AliveCount + " enemies, 5 s; heap growth per system:\n" + GameLoop.HeapReport());
            GameLoop.TrackHeapPerSystem = false;
            Assert.Greater(_em.AliveCount, 0);
            Assert.IsFalse(_health.IsDead, "god mode: the run goes on");
        }
    }
}
