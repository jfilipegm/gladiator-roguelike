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
    /// Phase 5.4 (docs/BOSSES.md): the Provincial Champion. His tower shield blocks from the front, not from behind;
    /// the Shield Bash hits inside its cone, not beside it; the Flurry is several re-aimed thrusts; the Charge smashes a
    /// barricade in its lane and, meeting stone, stuns him with his guard down; phase 2's roar fires every spike trap
    /// and the charge comes twice.
    /// </summary>
    public class ChampionTests
    {
        const string Scene = "Assets/Scenes/Arenas/ProvincialPit.unity";
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
            _em = EnemyManager.Instance;
            _fight = Object.FindAnyObjectByType<BossFight>();
            Assert.IsNotNull(_fight, "the Provincial Pit has its boss fight");
            _damaged = 0;
            GameEvents.OnDamaged.Subscribe(OnDamaged);
            yield return new WaitForSeconds(0.3f);
            _fight.clearHorde = true; // the boss alone (the game keeps the horde: note 38)
            _fight.StartNow();
            float end = Time.time + 10f;
            while (Time.time < end && (BossController.Active == null || BossController.Active.CurrentState == BossController.State.Entering
                                       || BossController.Active.CurrentState == BossController.State.Roaring)) yield return null;
            Assert.AreEqual("champion", Boss.def.id);
        }

        [TearDown]
        public void TearDown()
        {
            GameEvents.OnDamaged.Unsubscribe(OnDamaged);
            TimeController.ResetForNewRun();
            DevSettings.ResetAll();
        }

        Vector3 Forward => Quaternion.Euler(0f, _em.GetYaw(Boss.Slot), 0f) * Vector3.forward;

        /// <summary>Stands the player `d` m in front of him (he faces +X from `at`) and waits a frame.</summary>
        IEnumerator Face(Vector3 at, float d)
        {
            Boss.Warp(at, 90f);
            _motor.Teleport(at + Vector3.right * d + Vector3.up * 0.1f);
            yield return null;
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator Enters_ShieldUp_BlocksFromTheFront_NotFromBehind()
        {
            yield return BootToBoss();
            Assert.IsTrue(Boss.ShieldUp);
            Assert.AreEqual(1, _em.AliveCount, "the horde was dragged off: only him");
            Vector3 fwd = Forward;
            float hp = _em.GetHp(Boss.Slot);
            _em.ApplyDamage(Boss.Slot, new DamageInfo { Amount = 100f, Flags = DamageFlags.Melee, Direction = -fwd }); // from in front
            Assert.AreEqual(hp - 100f * (1f - Boss.def.enemy.blockReduction), _em.GetHp(Boss.Slot), 0.01f, "the scutum takes it");
            hp = _em.GetHp(Boss.Slot);
            _em.ApplyDamage(Boss.Slot, new DamageInfo { Amount = 100f, Flags = DamageFlags.Melee, Direction = fwd }); // from behind
            Assert.AreEqual(hp - 100f, _em.GetHp(Boss.Slot), 0.01f, "his back is open");
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator ShieldBash_HitsInsideItsCone_NotBesideIt()
        {
            yield return BootToBoss();
            var at = new Vector3(-8f, 0f, 4f);
            yield return Face(at, 2.6f);
            int before = _damaged;
            Boss.ForceAttack(BossAttack.Bash);
            Assert.AreEqual(BossController.State.Windup, Boss.CurrentState);
            yield return new WaitForSeconds(Boss.def.bashWindup + 0.15f);
            Assert.AreEqual(before + 1, _damaged, "in the cone: bashed");
            yield return new WaitForSeconds(Boss.def.recover + 0.6f);

            yield return Face(at, 2.6f);
            before = _damaged;
            Boss.ForceAttack(BossAttack.Bash);
            yield return null;
            _motor.Teleport(at + Vector3.forward * 3.2f + Vector3.up * 0.1f); // beside him: 90° off the cone
            yield return new WaitForSeconds(Boss.def.bashWindup + 0.15f);
            Assert.AreEqual(before, _damaged, "beside the cone: missed");
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator Flurry_IsThreeReAimedThrusts_EachHitsAStandingPlayer()
        {
            yield return BootToBoss();
            yield return Face(new Vector3(-8f, 0f, 4f), 3.5f);
            int before = _damaged, done = Boss.AttacksDone;
            Boss.ForceAttack(BossAttack.Flurry);
            float end = Time.time + 4f;
            while (Time.time < end && Boss.AttacksDone == done) yield return null;
            Assert.AreEqual(done + 1, Boss.AttacksDone, "one flurry");
            Assert.AreEqual(before + Boss.def.flurryThrusts.x, _damaged, "every thrust lands on a player who stands in the lane");
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator Charge_SmashesTheBarricadeInItsLane_StoneStunsHim_GuardDown()
        {
            yield return BootToBoss();
            var br = ArenaBreakables.Instance;
            // ProvincialPitLayout: barricade 8 stands on the west floor at (-24, 3); the pillar toward the west end (-31, 0)
            // is just beyond it. Line him up so the lane runs through the barricade into the pillar.
            const int b = 8;
            Vector3 bar = br.barricades[b].position;
            Vector3 pillar = new Vector3(-31f, 0f, 0f);
            Vector3 dir = pillar - bar; dir.y = 0f; dir.Normalize();
            Vector3 start = bar - dir * 4f;
            Boss.Warp(start, Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg);
            _motor.Teleport(pillar + dir * 2.5f + Vector3.up * 0.1f); // behind the pillar
            yield return null;
            Boss.ForceAttack(BossAttack.Charge);
            float end = Time.time + 5f;
            while (Time.time < end && Boss.CurrentState != BossController.State.Stunned) yield return null;
            Assert.AreEqual(BossController.State.Stunned, Boss.CurrentState, "the pillar stops him dead");
            Assert.IsTrue(br.IsBroken(b), "the barricade in his lane is gone");
            Assert.AreEqual(1, Boss.Stuns);
            Assert.IsFalse(Boss.ShieldUp, "guard down while stunned");
            Vector3 fwd = Forward;
            float hp = _em.GetHp(Boss.Slot);
            _em.ApplyDamage(Boss.Slot, new DamageInfo { Amount = 100f, Flags = DamageFlags.Melee, Direction = -fwd });
            Assert.AreEqual(hp - 100f * Boss.def.stunDamageTakenMul, _em.GetHp(Boss.Slot), 0.01f, "from the front, unblocked, +50 %");
            yield return new WaitForSeconds(Boss.def.chargeStun + 0.2f);
            Assert.AreNotEqual(BossController.State.Stunned, Boss.CurrentState);
            Assert.IsTrue(Boss.ShieldUp, "guard back up");
            hp = _em.GetHp(Boss.Slot);
            _em.ApplyDamage(Boss.Slot, new DamageInfo { Amount = 100f, Flags = DamageFlags.Melee, Direction = fwd });
            Assert.AreEqual(hp - 100f, _em.GetHp(Boss.Slot), 0.01f, "the bonus ends with the stun");
        }

        /// <summary>The guard clock runs while he attacks too (the 10-minute bot run saw no waves when it only ran
        /// between attacks).</summary>
        [UnityTest, Timeout(90000)]
        public IEnumerator CallsHisGuards_OnTheClock_EvenWhileAttackingNonStop()
        {
            yield return BootToBoss();
            Assert.AreEqual(0, Boss.GuardWaves);
            _motor.Teleport(Boss.Position + new Vector3(0f, 0.1f, -3f)); // close: he bashes and thrusts back to back
            TimeController.GameSpeed = 3f;
            float end = Time.time + Boss.def.guardsEvery + 4f;
            while (Time.time < end && Boss.GuardWaves == 0) yield return null;
            TimeController.GameSpeed = 1f;
            Assert.AreEqual(1, Boss.GuardWaves, "one wave inside guardsEvery + a little");
            Assert.Greater(Boss.AttacksDone, 3, "he kept attacking meanwhile");
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator Phase2_RoarFiresEverySpikeTrap_AndTheChargeComesTwice()
        {
            yield return BootToBoss();
            var traps = SpikeTraps.Instance;
            _em.ApplyDamage(Boss.Slot, new DamageInfo { Amount = _em.GetMaxHp(Boss.Slot) * (1f - Boss.def.phase2At) + 10f, Flags = DamageFlags.Status });
            yield return null;
            Assert.AreEqual(BossController.State.Enraging, Boss.CurrentState);
            yield return null;
            for (int i = 0; i < traps.traps.Length; i++)
                Assert.IsTrue(traps.IsTelegraphing(i) || traps.IsUp(i), "trap " + i + " fires on his roar");
            yield return new WaitForSeconds(Boss.def.enrageRoar + 0.2f);
            Assert.AreEqual(2, Boss.Phase);

            // open ground: a charge along z = 4 from x = -8 meets nothing (see ProvincialPitLayout)
            yield return Face(new Vector3(-8f, 0f, 4f), 6f);
            Boss.ForceAttack(BossAttack.Charge);
            float end = Time.time + 4f;
            while (Time.time < end && Boss.CurrentState != BossController.State.Strike) yield return null;
            Assert.AreEqual(BossController.State.Strike, Boss.CurrentState, "first charge");
            while (Time.time < end && Boss.CurrentState == BossController.State.Strike) yield return null;
            Assert.AreEqual(BossController.State.Windup, Boss.CurrentState, "re-aims for a second charge");
            Assert.AreEqual(BossAttack.Charge, Boss.CurrentAttack);
        }
    }
}
