using System.Collections;
using Gladiator.Core;
using Gladiator.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Gladiator.Tests
{
    /// <summary>M1.4: run end (death / timer / timeless / god mode / retry), admin buffs and spawning, the fence perimeter.</summary>
    public class RunEndAndAdminTests
    {
        const string Scene = "Assets/Scenes/Dev/TestArena.unity";

        Player _player;
        PlayerHealth _health;
        AdminController _admin;

        IEnumerator Boot(bool quiet = true)
        {
            DevSettings.ResetAll();
            TimeController.GameSpeed = 1f;
            TimeController.ResetForNewRun();
            var op = SceneManager.LoadSceneAsync(Scene, LoadSceneMode.Single);
            while (!op.isDone) yield return null;
            yield return null;
            _player = Player.Main;
            _health = _player.GetComponent<PlayerHealth>();
            _admin = AdminController.Instance;
            _player.OverrideSource = new ScriptedInput();
            if (quiet)
            {
                Object.FindAnyObjectByType<DebugEnemySpawner>().enabled = false;
                if (Director.Instance != null) Director.Instance.enabled = false; // tests place their own enemies
                EnemyManager.Instance.KillAll();
                yield return new WaitForSeconds(0.3f);
                PickupSystem.Instance.ClearAll();
            }
        }

        static IEnumerator WaitRealtime(System.Func<bool> done, float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!done() && Time.realtimeSinceStartup < end) yield return null;
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Death_ShowsEndScreen_Pauses_RetryStartsFreshRun()
        {
            yield return Boot();
            var end = RunEndController.Instance;
            _health.Kill();
            yield return WaitRealtime(() => end.ScreenShown, 5f);
            Assert.IsTrue(end.ScreenShown, "end screen should appear after the death beat");
            Assert.AreEqual(RunOutcome.Died, end.Outcome);
            Assert.AreEqual(0f, Time.timeScale, "the end screen pauses the game");
            Assert.IsFalse(_player.InputEnabled, "no gameplay input on the end screen");

            DevSettings.GodMode = true; // must survive Retry
            end.Retry();
            yield return null;
            yield return WaitRealtime(() => RunEndController.Instance != null && RunEndController.Instance != end && Player.Main != null, 10f);
            yield return null;
            var run = RunState.Current;
            var h = Player.Main.GetComponent<PlayerHealth>();
            Assert.AreEqual(RunEndController.Phase.Playing, RunEndController.Instance.CurrentPhase);
            Assert.AreEqual(RunOutcome.None, run.Outcome);
            Assert.Less(run.Elapsed, 2f, "a fresh run starts at the full timer");
            Assert.IsFalse(h.IsDead);
            Assert.AreEqual(h.MaxHp, h.Hp, "full HP after retry");
            Assert.AreEqual(1f, Time.timeScale, "retry clears the pause");
            Assert.IsTrue(Player.Main.InputEnabled);
            Assert.IsTrue(DevSettings.GodMode, "admin switches survive Retry");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator TimerZero_EndsRunSurvived_UnlessTimeless()
        {
            yield return Boot();
            var run = RunState.Current;
            DevSettings.Timeless = true;
            run.SetTimeRemaining(0.4f);
            yield return new WaitForSeconds(1f);
            Assert.IsFalse(run.Ended, "Timeless: the timer never ends the run");

            DevSettings.Timeless = false;
            var spawner = Object.FindAnyObjectByType<DebugEnemySpawner>();
            spawner.enabled = true;  // the crowd and the Director must stop once the run is over
            Object.FindAnyObjectByType<Director>(FindObjectsInactive.Include).enabled = true;
            var end = RunEndController.Instance;
            yield return WaitRealtime(() => end.ScreenShown, 5f);
            Assert.IsTrue(end.ScreenShown);
            Assert.AreEqual(RunOutcome.Survived, end.Outcome);
            Assert.AreEqual(0, EnemyManager.Instance.AliveCount, "survived: the arena falls silent and stays silent");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator GodMode_FloorsHpAtOne()
        {
            yield return Boot();
            DevSettings.GodMode = true;
            _health.TakeDamage(new DamageInfo { Amount = 10000f });
            Assert.AreEqual(1f, _health.Hp);
            Assert.IsFalse(_health.IsDead);
            yield return new WaitForSeconds(0.5f);
            Assert.IsFalse(RunState.Current.Ended);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator AdminBuffs_ScaleDamageAndHp_AndReset()
        {
            yield return Boot();
            var weapons = _player.GetComponent<WeaponSystem>();
            var stats = _player.GetComponent<PlayerStats>();
            float baseHp = _health.MaxHp;
            float runDamage = stats.Block[Gladiator.Core.StatId.Damage]; // the run's own bonuses (no admin layer)
            _admin.SetBuff(AdminController.Buff.Damage, 100f);
            _admin.SetBuff(AdminController.Buff.MaxHp, 50f);
            Assert.AreEqual(runDamage * 2f, weapons.DamageMul, 1e-4f);
            Assert.AreEqual(baseHp * 1.5f, _health.MaxHp, 1e-3f);
            Assert.AreEqual(_health.MaxHp, _health.Hp, 1e-3f, "HP keeps its fraction when max HP is buffed");

            // A real swing lands for 2× (or 4× on a crit).
            float hitDamage = -1f;
            EventHandler<HitEvent> onHit = (in HitEvent e) => { if (hitDamage < 0f) hitDamage = e.Damage; };
            GameEvents.OnHit.Subscribe(onHit);
            var def = Object.FindAnyObjectByType<DebugEnemySpawner>().enemy;
            EnemyManager.Instance.Spawn(def, _player.transform.position + new Vector3(0f, 0f, 1.6f));
            yield return WaitRealtime(() => hitDamage >= 0f, 4f);
            GameEvents.OnHit.Unsubscribe(onHit);
            float baseDamage = weapons.startingWeapons[0].damage * runDamage;
            Assert.Greater(hitDamage, 0f, "the gladius should hit");
            float ratio = hitDamage / baseDamage;
            Assert.IsTrue(Mathf.Abs(ratio - 2f) < 0.01f || Mathf.Abs(ratio - 2f * weapons.startingWeapons[0].critMultiplier) < 0.01f, "hit was " + hitDamage);

            _admin.ResetBuffs();
            Assert.AreEqual(runDamage, weapons.DamageMul, 1e-4f);
            Assert.AreEqual(baseHp, _health.MaxHp, 1e-3f);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator AdminSpawn_NearPlayerAndAroundArena()
        {
            yield return Boot();
            Assert.IsNotNull(_admin.Selected, "the enemy catalog should list at least the convict");
            var em = EnemyManager.Instance;
            Vector3 p = _player.transform.position;
            int alive0 = em.AliveCount;
            Assert.AreEqual(10, _admin.SpawnNearPlayer(10));
            Assert.AreEqual(alive0 + 10, em.AliveCount);
            yield return null; // the spatial hash is rebuilt on the next tick
            int[] idx = new int[64];
            int near = em.QueryAlive(p, 7.6f, idx);
            Assert.GreaterOrEqual(near, 10, "near spawns land within ~7 m of the player");
            Assert.AreEqual(20, _admin.SpawnAroundArena(20));
            Assert.AreEqual(alive0 + 30, em.AliveCount);
            yield return null;
            _admin.KillAllEnemies();
            Assert.AreEqual(0, em.AliveCount);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Fence_IsSolidForPlayerAndPickups()
        {
            yield return Boot();
            var motor = _player.GetComponent<PlayerMotor>();
            var input = new ScriptedInput();
            _player.OverrideSource = input;
            Object.FindAnyObjectByType<PlayerCameraRig>().SetAngles(0f, 20f); // stick-forward = +Z
            motor.Teleport(new Vector3(0f, 0.1f, 17f));
            yield return null;
            input.Next.Move = new Vector2(0f, 1f);
            float maxR = 0f;
            float end = Time.time + 2.5f;
            float nextJump = Time.time + 0.8f;
            while (Time.time < end)
            {
                if (Time.time > nextJump) { input.Next.JumpPressed = true; input.Next.JumpHeld = true; nextJump = Time.time + 0.7f; }
                var q = _player.transform.position;
                maxR = Mathf.Max(maxR, new Vector2(q.x, q.z).magnitude);
                yield return null;
            }
            Assert.Less(maxR, 20.9f, "the player must stay inside the fence line (r≈21) even when jumping into it");
            Assert.Greater(maxR, 20f, "the player should reach the fence");

            PickupSystem.Instance.SpawnXp(new Vector3(3f, 0.5f, 26f), 1f);
            yield return new WaitForSeconds(0.2f);
            var (found, pos) = RunBotSensor.NearestPickup(new Vector3(0f, 0f, 30f), 100f);
            Assert.IsTrue(found);
            Assert.Less(new Vector2(pos.x, pos.z).magnitude, 20.5f, "pickups never end up outside the fence");
        }
    }
}
