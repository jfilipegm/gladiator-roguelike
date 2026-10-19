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
    /// M3.4: Gorm, the Pit Master. The timer brings him in (the run waits, the horde is gone), his telegraphed attacks
    /// hit a standing player and the flail can be jumped, phase 2 lights the fence and unlocks the charge, he calls
    /// guards through the gates, and his death leads to the hoard pick and a victory; dying to him is still a loss.
    /// </summary>
    public class BossTests
    {
        const string Scene = "Assets/Scenes/Arenas/SlavePit.unity";
        Player _player;
        PlayerMotor _motor;
        PlayerHealth _health;
        EnemyManager _em;
        RunState _run;
        BossFight _fight;
        int _damaged;

        IEnumerator BootToBoss()
        {
            DevSettings.ResetAll();
            DevSettings.GodMode = true;
            TimeController.GameSpeed = 1f;
            TimeController.ResetForNewRun();
            var op = SceneManager.LoadSceneAsync(Scene, LoadSceneMode.Single);
            while (!op.isDone) yield return null;
            yield return null;
            while (RunIntro.Instance != null && !RunIntro.Instance.Done) yield return null; // the arena intro holds the game
            _player = Player.Main;
            _motor = _player.GetComponent<PlayerMotor>();
            _health = _player.GetComponent<PlayerHealth>();
            _health.invulnerableTime = 0f;
            _player.OverrideSource = new ScriptedInput();
            _player.GetComponent<WeaponSystem>().RemoveAll(); // no kills, no XP, no level-up cards mid-test
            _em = EnemyManager.Instance;
            _run = RunState.Current;
            _fight = Object.FindAnyObjectByType<BossFight>();
            Assert.IsNotNull(_fight, "the Slave Pit has its boss fight");
            _damaged = 0;
            GameEvents.OnDamaged.Subscribe(OnDamaged);
            yield return new WaitForSeconds(0.5f);
            _fight.StartNow();
            float end = Time.time + 8f;
            while (Time.time < end && (BossController.Active == null || BossController.Active.CurrentState == BossController.State.Entering
                                       || BossController.Active.CurrentState == BossController.State.Roaring)) yield return null;
        }

        void OnDamaged(in DamagedEvent e) => _damaged++;

        [TearDown]
        public void TearDown()
        {
            GameEvents.OnDamaged.Unsubscribe(OnDamaged);
            TimeController.ResetForNewRun();
            DevSettings.ResetAll();
        }

        BossController Boss => BossController.Active;

        [UnityTest, Timeout(90000)]
        public IEnumerator TimerEnd_BringsGorm_TheRunWaits()
        {
            yield return BootToBoss();
            Assert.IsNotNull(Boss);
            Assert.IsTrue(Boss.Alive);
            Assert.IsTrue(_run.TimerDone, "the timer ran out");
            Assert.IsFalse(_run.Ended, "the run waits for the fight");
            Assert.AreEqual(1, _em.AliveCount, "the horde was dragged off: only Gorm");
            Assert.AreEqual(BossFight.Stage.Fight, _fight.Current);
            float hp = Boss.Hp01;
            _em.ApplyDamage(Boss.Slot, new DamageInfo { Amount = 50f, Flags = DamageFlags.Melee });
            Assert.Less(Boss.Hp01, hp, "after the entrance he can be hurt");
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator FlailSweep_HitsAStandingPlayer_JumpingClearsIt()
        {
            yield return BootToBoss();
            var def = Boss.def;
            _motor.Teleport(Boss.Position + new Vector3(0f, 0.1f, -3f));
            yield return null;
            int before = _damaged;
            Boss.ForceAttack(BossAttack.Sweep);
            yield return new WaitForSeconds(def.sweepWindup + 0.25f);
            Assert.Greater(_damaged, before, "standing in the ring gets you hit");

            yield return new WaitForSeconds(def.recover + 0.2f);
            _motor.Teleport(Boss.Position + new Vector3(0f, 0.1f, -3f));
            yield return null;
            before = _damaged;
            Boss.ForceAttack(BossAttack.Sweep);
            yield return new WaitForSeconds(def.sweepWindup - 0.12f);
            _motor.Teleport(_player.transform.position + Vector3.up * 2.2f); // mid-jump when the flail comes round
            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(before, _damaged, "feet above the flail: no hit");
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator Phase2_LightsTheFence_AndCharges()
        {
            yield return BootToBoss();
            int zones = HazardZones.Instance.Count;
            _em.ApplyDamage(Boss.Slot, new DamageInfo { Amount = _em.GetMaxHp(Boss.Slot) * (1f - Boss.def.phase2At) + 10f, Flags = DamageFlags.Melee });
            yield return null;
            Assert.AreEqual(BossController.State.Enraging, Boss.CurrentState);
            float hp = Boss.Hp01;
            _em.ApplyDamage(Boss.Slot, new DamageInfo { Amount = 100f, Flags = DamageFlags.Melee });
            Assert.AreEqual(hp, Boss.Hp01, 1e-5f, "invulnerable while he roars");
            yield return new WaitForSeconds(Boss.def.enrageRoar + 0.3f);
            Assert.AreEqual(2, Boss.Phase);
            Assert.Greater(HazardZones.Instance.Count, zones + 30, "a ring of fire along the wall");
            _motor.Teleport(Boss.Position + new Vector3(0f, 0.1f, -9f));
            yield return null;
            Boss.ForceAttack(BossAttack.Charge);
            Assert.AreEqual(BossAttack.Charge, Boss.CurrentAttack);
            Vector3 start = Boss.Position;
            yield return new WaitForSeconds(Boss.def.chargeWindup / Boss.def.enrageSpeed + 0.5f);
            Assert.Greater((Boss.Position - start).magnitude, 3f, "he barrels down the lane");
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator CallsTheGuards_ThroughTheGates()
        {
            yield return BootToBoss();
            int alive = _em.AliveCount;
            Boss.ForceGuards();
            yield return null;
            int added = _em.AliveCount - alive;
            Assert.GreaterOrEqual(added, Boss.def.guardCount.x);
            Assert.LessOrEqual(added, Boss.def.guardCount.y);
            yield return new WaitForSeconds(0.4f);
            bool open = false;
            for (int i = 0; i < ArenaGates.Instance.Count; i++) open |= ArenaGates.Instance.IsOpen(i);
            Assert.IsTrue(open, "they come through the gates");
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator Defeat_HoardPick_ThenVictory()
        {
            yield return BootToBoss();
            _em.ApplyDamage(Boss.Slot, new DamageInfo { Amount = _em.GetMaxHp(Boss.Slot) * 2f, Flags = DamageFlags.Melee });
            yield return null;
            yield return null; // BossFight ticks before BossController: it sees the death on the next frame
            Assert.IsTrue(Boss.Defeated);
            Assert.AreEqual(BossFight.Stage.Falling, _fight.Current);
            var lvl = LevelUpController.Instance;
            float end = Time.realtimeSinceStartup + 6f;
            while (Time.realtimeSinceStartup < end && !lvl.IsOpen) yield return null;
            Assert.IsTrue(lvl.IsOpen, "the hoard opens");
            Assert.AreEqual(PickSource.Boss, lvl.Source);
            for (int i = 0; i < lvl.ChoiceCount; i++)
                if (lvl.Offers[i].Def.kind != UpgradeKind.Consumable) Assert.GreaterOrEqual(lvl.Offers[i].Rarity, Rarity.Rare);
            Assert.IsFalse(_run.Ended, "not over until you take it");
            lvl.Choose(0);
            yield return null;
            yield return null;
            Assert.IsTrue(_run.Ended);
            Assert.AreEqual(RunOutcome.Survived, _run.Outcome);
            Assert.AreEqual(1f, TimeController.SlowMotion, "slow motion released");
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator DyingToGorm_IsStillALoss()
        {
            yield return BootToBoss();
            DevSettings.GodMode = false;
            _health.TakeDamage(new DamageInfo { Amount = 100000f, Flags = DamageFlags.Melee, Point = _player.transform.position });
            yield return null;
            yield return null;
            Assert.IsTrue(_run.Ended);
            Assert.AreEqual(RunOutcome.Died, _run.Outcome);
            Assert.AreEqual(BossFight.Stage.Done, _fight.Current);
        }
    }
}
