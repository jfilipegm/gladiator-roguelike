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
    /// Phase 5.8 (docs/BOSSES.md): the Beast Lord. He enters through the great gate with no shield; the Hook Thrust
    /// hits and slows a player in its lane; the Pounce lands where its circle showed, hits a player who stayed, misses
    /// one who left, and he's slow to get up; his horn opens the cage nearest him and brings two curs; phase 2 opens
    /// every cage and the pounce comes twice; at his fall the beasts scatter.
    /// </summary>
    public class BeastLordTests
    {
        const string Scene = "Assets/Scenes/Arenas/StoneAmphitheater.unity";
        Player _player;
        PlayerMotor _motor;
        PlayerHealth _health;
        EnemyManager _em;
        BossFight _fight;
        int _damaged;

        void OnDamaged(in DamagedEvent e) => _damaged++;

        BossController Boss => BossController.Active;

        IEnumerator BootToBoss()
        {
            DevSettings.ResetAll();
            DevSettings.GodMode = true;
            TimeController.GameSpeed = 1f;
            TimeController.ResetForNewRun();
            var op = SceneManager.LoadSceneAsync(Scene, LoadSceneMode.Single);
            while (!op.isDone) yield return null;
            yield return null;
            while (RunIntro.Instance != null && !RunIntro.Instance.Done) yield return null;
            _player = Player.Main;
            _motor = _player.GetComponent<PlayerMotor>();
            _health = _player.GetComponent<PlayerHealth>();
            _health.invulnerableTime = 0f;
            _player.OverrideSource = new ScriptedInput();
            _player.GetComponent<WeaponSystem>().RemoveAll();
            var lvl = Object.FindAnyObjectByType<LevelUpController>();
            if (lvl != null) lvl.enabled = false;
            _em = EnemyManager.Instance;
            _fight = Object.FindAnyObjectByType<BossFight>();
            Assert.IsNotNull(_fight, "the Stone Amphitheater has its boss fight");
            _damaged = 0;
            GameEvents.OnDamaged.Subscribe(OnDamaged);
            yield return new WaitForSeconds(0.3f);
            _fight.StartNow();
            float end = Time.time + 10f;
            while (Time.time < end && (BossController.Active == null || BossController.Active.CurrentState == BossController.State.Entering
                                       || BossController.Active.CurrentState == BossController.State.Roaring)) yield return null;
            Assert.AreEqual("beastlord", Boss.def.id);
        }

        [TearDown]
        public void TearDown()
        {
            GameEvents.OnDamaged.Unsubscribe(OnDamaged);
            TimeController.ResetForNewRun();
            DevSettings.ResetAll();
        }

        /// <summary>Puts him at `at` facing +X and the player `d` m in front of him; waits a frame.</summary>
        IEnumerator Face(Vector3 at, float d)
        {
            Boss.Warp(at, 90f);
            _motor.Teleport(at + Vector3.right * d + Vector3.up * 0.1f);
            yield return null;
        }

        void KillAllButHim()
        {
            for (int i = 0; i < _em.HighWater; i++)
                if (i != Boss.Slot && _em.IsAlive(i)) _em.ApplyDamage(i, new DamageInfo { Amount = 1e6f, Flags = DamageFlags.Status });
        }

        static readonly Vector3 Open = new Vector3(-8f, 0f, -4f); // clear sand west of the middle (AmphitheaterLayout)

        [UnityTest, Timeout(90000)]
        public IEnumerator Enters_AloneWithNoShield()
        {
            yield return BootToBoss();
            Assert.AreEqual(1, _em.AliveCount, "the horde was dragged off: only him");
            Assert.IsFalse(Boss.ShieldUp, "a goad, no shield");
            float hp = _em.GetHp(Boss.Slot);
            Vector3 fwd = Quaternion.Euler(0f, _em.GetYaw(Boss.Slot), 0f) * Vector3.forward;
            _em.ApplyDamage(Boss.Slot, new DamageInfo { Amount = 100f, Flags = DamageFlags.Melee, Direction = -fwd });
            Assert.AreEqual(hp - 100f, _em.GetHp(Boss.Slot), 0.01f, "hits from the front land in full");
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator HookThrust_HitsAndSlowsAPlayerInTheLane()
        {
            yield return BootToBoss();
            yield return Face(Open, 6f);
            int before = _damaged;
            Boss.ForceAttack(BossAttack.Whip);
            yield return new WaitForSeconds(Boss.def.whipWindup + 0.15f);
            Assert.AreEqual(before + 1, _damaged, "in the lane: hooked");
            Assert.AreEqual(Boss.def.whipSlow, _motor.SlowMultiplier, 1e-3f, "and dragged (Slow)");
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator Pounce_LandsWhereItsCircleShowed_HitsAPlayerWhoStayed_AndHesSlowToGetUp()
        {
            yield return BootToBoss();
            yield return Face(Open, 10f);
            Vector3 playerAt = _player.transform.position;
            int before = _damaged;
            Boss.ForceAttack(BossAttack.Pounce);
            Assert.AreEqual(BossController.State.Windup, Boss.CurrentState);
            float end = Time.time + 4f;
            while (Time.time < end && Boss.CurrentState == BossController.State.Windup) yield return null;
            Assert.AreEqual(BossController.State.Strike, Boss.CurrentState, "the leap");
            Vector3 shown = Boss.PounceTarget;
            Assert.Less(Vector2.Distance(new Vector2(shown.x, shown.z), new Vector2(playerAt.x, playerAt.z)), 0.8f, "the circle sat on the player");
            while (Time.time < end && Boss.CurrentState == BossController.State.Strike) yield return null;
            Assert.AreEqual(1, Boss.Pounces);
            Assert.Less(Vector2.Distance(new Vector2(Boss.Position.x, Boss.Position.z), new Vector2(shown.x, shown.z)), 1.5f, "he lands where it showed");
            Assert.AreEqual(before + 1, _damaged, "a player who stayed is hit");
            Assert.AreEqual(BossController.State.Recover, Boss.CurrentState, "slow to get up: the window");
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator Pounce_MissesAPlayerWhoLeftTheCircleAfterItLocked()
        {
            yield return BootToBoss();
            yield return Face(Open, 10f);
            int before = _damaged;
            Boss.ForceAttack(BossAttack.Pounce);
            yield return new WaitForSeconds(Boss.def.pounceWindup * Boss.def.pounceTrack + 0.1f); // the circle has locked
            _motor.Teleport(Open + new Vector3(10f, 0.1f, 7f)); // 7 m to the side
            float end = Time.time + 4f;
            while (Time.time < end && Boss.Pounces == 0) yield return null;
            Assert.AreEqual(1, Boss.Pounces);
            Assert.AreEqual(before, _damaged, "out of the circle: missed");
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator HisHorn_OpensTheCageNearestHim_AndCallsTwoCurs()
        {
            yield return BootToBoss();
            var cages = BeastCages.Instance;
            int nearest = cages.NearestIdle(Boss.Position);
            Assert.GreaterOrEqual(nearest, 0);
            Boss.ForceGuards();
            Assert.AreEqual(1, Boss.GuardWaves);
            Assert.IsTrue(cages.IsBusy(nearest), "the pen nearest him opens");
            Assert.AreEqual(1, cages.Opened);
            Assert.AreEqual(2, _em.AliveOf(UnityEditor.AssetDatabase.LoadAssetAtPath<EnemyDef>("Assets/Data/Enemies/EN_PitCur.asset")));
            yield return new WaitForSeconds(cages.telegraph + cages.liftTime + 5f * cages.releaseSpacing + 0.4f);
            Assert.GreaterOrEqual(cages.Released, Boss.def.beastPack.x, "his boars are out");
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator Phase2_OpensEveryCage_AndThePounceComesTwice()
        {
            yield return BootToBoss();
            var cages = BeastCages.Instance;
            _em.ApplyDamage(Boss.Slot, new DamageInfo { Amount = _em.GetMaxHp(Boss.Slot) * (1f - Boss.def.phase2At) + 10f, Flags = DamageFlags.Status });
            yield return null;
            Assert.AreEqual(BossController.State.Enraging, Boss.CurrentState);
            yield return null;
            for (int i = 0; i < cages.Count; i++) Assert.IsTrue(cages.IsBusy(i), "cage " + i + " opens on his roar");
            yield return new WaitForSeconds(Boss.def.enrageRoar + 0.2f);
            Assert.AreEqual(2, Boss.Phase);

            KillAllButHim(); // the stampede out of the way
            yield return Face(Open, 10f);
            Boss.ForceAttack(BossAttack.Pounce);
            float end = Time.time + 5f;
            while (Time.time < end && Boss.Pounces == 0) yield return null;
            Assert.AreEqual(1, Boss.Pounces);
            Assert.AreEqual(BossController.State.Windup, Boss.CurrentState, "re-aims for a second pounce");
            Assert.AreEqual(BossAttack.Pounce, Boss.CurrentAttack);
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator HisFall_ScattersTheBeasts()
        {
            yield return BootToBoss();
            var cages = BeastCages.Instance;
            cages.OpenAll(2);
            yield return new WaitForSeconds(cages.telegraph + cages.liftTime + 1f);
            Assert.Greater(_em.AliveCount, 4, "boars out");
            _em.ApplyDamage(Boss.Slot, new DamageInfo { Amount = 1e6f, Flags = DamageFlags.Status });
            yield return null;
            yield return null;
            Assert.IsTrue(Boss.Defeated);
            Assert.AreEqual(0, _em.AliveCount, "every beast is dragged off with him");
            Assert.AreEqual(BossFight.Stage.Falling, _fight.Current);
        }
    }
}
