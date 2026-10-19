using System.Collections;
using Gladiator.Core;
using Gladiator.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Gladiator.Tests
{
    /// <summary>
    /// Phase 5.1 (docs/FAVOR.md): the Favor meter fills from its sources and throws gifts; gifts land near the player
    /// and do what they say; the weapon in the sand fights beside the slots and leaves; dull play brings a whim and
    /// fighting doesn't. TestArena, Director off unless a test needs it.
    /// </summary>
    public class FavorPlayTests
    {
        const string Scene = "Assets/Scenes/Dev/TestArena.unity";
        FavorConfig _cfg;
        Director _director;

        IEnumerator Boot()
        {
            DevSettings.ResetAll();
            DevSettings.GodMode = true;
            TimeController.GameSpeed = 1f;
            TimeController.ResetForNewRun();
            var op = SceneManager.LoadSceneAsync(Scene, LoadSceneMode.Single);
            while (!op.isDone) yield return null;
            yield return null;
            var spawner = Object.FindAnyObjectByType<DebugEnemySpawner>();
            if (spawner != null) spawner.enabled = false;
            _director = Object.FindAnyObjectByType<Director>(); // a disabled Director clears Director.Instance: keep our own handle
            if (_director != null) _director.enabled = false;
            var lvl = Object.FindAnyObjectByType<LevelUpController>();
            if (lvl != null) lvl.enabled = false;
            Player.Main.OverrideSource = new ScriptedInput();
            // a private copy of the config each test can bend
            _cfg = Object.Instantiate(Favor.Instance.config);
            Favor.Instance.config = _cfg;
            CrowdGifts.Instance.config = _cfg;
            ArenaMaster.Instance.config = _cfg;
            yield return new WaitForSeconds(0.2f);
        }

        [TearDown]
        public void TearDown()
        {
            DevSettings.ResetAll();
            if (_cfg != null) Object.Destroy(_cfg);
        }

        static PlayerMotor Motor => Player.Main.GetComponent<PlayerMotor>();

        /// <summary>The Director running but adding nothing on its own (whims need it to spawn).</summary>
        void QuietDirector()
        {
            _director.densityMultiplier = 0f;
            _director.enabled = true;
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Sources_FillTheMeter_AndAFullMeterThrowsAGift()
        {
            yield return Boot();
            var favor = Favor.Instance;
            var gifts = CrowdGifts.Instance;
            Assert.IsNotNull(favor, "every arena has Favor");
            Assert.IsNotNull(gifts, "and the gifts");
            float before = favor.TotalEarned;
            GameEvents.OnKill.Raise(new KillEvent { Position = Vector3.zero, EnemyTypeId = 11 });
            Assert.AreEqual(_cfg.variety, favor.TotalEarned - before, 1e-3f, "a new type is variety");
            GameEvents.OnKill.Raise(new KillEvent { Position = Vector3.zero, EnemyTypeId = 11 });
            Assert.AreEqual(_cfg.variety, favor.TotalEarned - before, 1e-3f, "the same type again isn't");
            GameEvents.OnAnnounce.Raise(new AnnounceEvent { Text = "X", Kind = AnnounceKind.Streak, Tier = 1 });
            Assert.AreEqual(_cfg.variety + _cfg.StreakFavor(1), favor.TotalEarned - before, 1e-3f, "an announced streak rung");

            favor.Add(favor.Threshold - favor.Value - 1f);
            Assert.AreEqual(0, favor.GiftsEarned, "one short of a gift");
            GameEvents.OnKill.Raise(new KillEvent { Position = Vector3.zero, EnemyTypeId = 11, Elite = true });
            Assert.AreEqual(1, favor.GiftsEarned, "an elite kill tips it over");
            Assert.AreEqual(1, gifts.Thrown, "and the crowd throws");
            Assert.AreEqual(_cfg.eliteKill - 1f, favor.Value, 1e-3f, "the overflow carries into the next gift");
            Assert.AreEqual(_cfg.ThresholdFor(1), favor.Threshold, 1e-3f, "the next gift costs more");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Gift_LandsNearby_AndTheAmphoraHeals()
        {
            yield return Boot();
            var health = Player.Main.GetComponent<PlayerHealth>();
            health.TakeDamage(new DamageInfo { Amount = health.MaxHp * 0.6f, Flags = DamageFlags.Melee, Point = Player.Main.transform.position });
            yield return new WaitForSeconds(0.5f);
            float hurt = health.Hp;
            var gifts = CrowdGifts.Instance;
            int slot = gifts.Throw(GiftKind.Amphora);
            Assert.GreaterOrEqual(slot, 0);
            Assert.IsFalse(gifts.IsWaiting(slot), "in the air first");
            yield return new WaitForSeconds(_cfg.throwTime + 0.3f);
            Assert.IsTrue(gifts.IsWaiting(slot), "then on the ground");
            Vector3 at = gifts.PositionOf(slot), p = Player.Main.transform.position;
            float d = new Vector2(at.x - p.x, at.z - p.z).magnitude;
            Assert.LessOrEqual(d, _cfg.landDistance.y + 0.5f, "within reach");
            Assert.GreaterOrEqual(d, 1.5f, "not on the player's head");
            Motor.Teleport(at + Vector3.up * 0.1f);
            yield return new WaitForSeconds(0.3f);
            Assert.IsTrue(gifts.IsFree(slot), "walked over: collected");
            Assert.AreEqual(1, gifts.Collected);
            Assert.Greater(health.Hp, hurt + health.MaxHp * _cfg.amphoraHeal * 0.9f, "the amphora heals");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Gift_LeftAlone_GoesAway()
        {
            yield return Boot();
            _cfg.giftLifetime = 1f;
            var gifts = CrowdGifts.Instance;
            int slot = gifts.Throw(GiftKind.GoldShower);
            yield return new WaitForSeconds(_cfg.throwTime + 0.2f);
            Assert.IsTrue(gifts.IsWaiting(slot));
            yield return new WaitForSeconds(1.2f);
            Assert.IsTrue(gifts.IsFree(slot), "gone when its time runs out");
            Assert.AreEqual(0, gifts.Collected);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator WeaponInTheSand_FightsBesideTheSlots_ThenLeaves()
        {
            yield return Boot();
            var weapons = Player.Main.GetComponent<WeaponSystem>();
            var build = Player.Main.GetComponent<PlayerBuild>();
            WeaponDef loan = null;
            foreach (var u in build.catalog.upgrades)
                if (u != null && u.kind == UpgradeKind.Weapon && u.weapon != null && weapons.Find(u.weapon) == null && !build.State.IsLocked(u)) { loan = u.weapon; break; }
            Assert.IsNotNull(loan, "a pool weapon the player doesn't hold");
            int slots = weapons.Weapons.Count;
            weapons.GrantTemporary(loan, 4, 1.5f);
            Assert.IsNotNull(weapons.Temporary);
            Assert.AreEqual(4, weapons.Temporary.Level, "lent at level 4");
            Assert.AreEqual(slots, weapons.Weapons.Count, "it doesn't take a slot");
            var em = EnemyManager.Instance;
            var convict = Object.FindAnyObjectByType<DebugEnemySpawner>().enemy;
            Vector3 p = Player.Main.transform.position;
            weapons.RemoveAll(); // only the loan fights
            for (int i = 0; i < 6; i++) em.Spawn(convict, p + Quaternion.Euler(0f, i * 60f, 0f) * Vector3.forward * 2f);
            int hits = 0;
            EventHandler<HitEvent> count = (in HitEvent e) => hits++;
            GameEvents.OnHit.Subscribe(count);
            yield return new WaitForSeconds(1f);
            GameEvents.OnHit.Unsubscribe(count);
            Assert.Greater(hits, 0, "the loaned weapon fights (" + loan.id + ")");
            yield return new WaitForSeconds(0.8f);
            Assert.IsNull(weapons.Temporary, "and leaves when its time is up");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator DullPlay_BringsAChallenge_WinningItPays()
        {
            yield return Boot();
            var run = RunState.Current;
            run.SetTimeRemaining(run.config.runDuration - 120f); // past the quiet start
            _cfg.boredomRise = 400f;
            _cfg.whims = new[] { WhimKind.ChampionChallenge };
            QuietDirector();
            var master = ArenaMaster.Instance;
            yield return new WaitForSeconds(0.6f);
            Assert.AreEqual(1, master.WhimsFired, "nothing near, nothing dying: the master steps in");
            Assert.IsTrue(master.ChallengeActive, "a champion was sent");
            float favor = Favor.Instance.TotalEarned;
            var em = EnemyManager.Instance;
            em.KillAll();
            yield return null;
            Assert.AreEqual(1, master.ChallengesWon, "killed in time");
            Assert.GreaterOrEqual(Favor.Instance.TotalEarned - favor, _cfg.challengeFavor - 0.01f, "the bounty is Favor");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Fighting_IsNeverDull_AndTheGatesWhimSendsASwarm()
        {
            yield return Boot();
            var run = RunState.Current;
            run.SetTimeRemaining(run.config.runDuration - 120f);
            _cfg.boredomRise = 400f;
            var master = ArenaMaster.Instance;
            var em = EnemyManager.Instance;
            var convict = Object.FindAnyObjectByType<DebugEnemySpawner>().enemy;
            Player.Main.GetComponent<WeaponSystem>().RemoveAll(); // keep them alive and close
            Vector3 p = Player.Main.transform.position;
            for (int i = 0; i < 4; i++) em.Spawn(convict, p + Quaternion.Euler(0f, i * 90f, 0f) * Vector3.forward * 3f);
            yield return new WaitForSeconds(0.6f);
            Assert.IsFalse(master.Dull, "enemies in your face aren't dull");
            Assert.AreEqual(0, master.WhimsFired);

            QuietDirector();
            _cfg.whims = new[] { WhimKind.GatesOpen };
            int swarms = _director.SwarmsStarted;
            Assert.IsTrue(master.FireWhim());
            Assert.AreEqual(swarms + 1, _director.SwarmsStarted, "the gates whim starts a swarm");
        }
    }
}
