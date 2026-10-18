using System.Collections;
using Gladiator.Core;
using Gladiator.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Gladiator.Tests
{
    /// <summary>M1.2: the gladius arc, crits/hit-stop/kill events, and enemies respecting static obstacles.</summary>
    public class CombatTests
    {
        const string Scene = "Assets/Scenes/Dev/TestArena.unity";
        EnemyManager _em;
        Player _player;
        WeaponSystem _weapons;
        EnemyDef _convict;

        IEnumerator Boot()
        {
            DevSettings.ResetAll(); // static test switches must not leak between tests
            Gladiator.Core.TimeController.GameSpeed = 1f;
            Gladiator.Core.TimeController.ResetForNewRun();
            var op = SceneManager.LoadSceneAsync(Scene, LoadSceneMode.Single);
            while (!op.isDone) yield return null;
            yield return null;
            _em = EnemyManager.Instance;
            _player = Player.Main;
            _weapons = _player.GetComponent<WeaponSystem>();
            var spawner = Object.FindAnyObjectByType<DebugEnemySpawner>();
            _convict = spawner.enemy;
            spawner.enabled = false;              // the test places enemies itself
            _player.OverrideSource = new ScriptedInput(); // stand still
            yield return new WaitForSeconds(0.3f);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Gladius_HitsInFront_NotBehind()
        {
            yield return Boot();
            Vector3 p = _player.transform.position;
            int front = _em.Spawn(_convict, p + new Vector3(0f, 0f, 1.5f));
            int behind = _em.Spawn(_convict, p + new Vector3(0f, 0f, -2.3f));
            float hpFront = _em.GetHp(front), hpBehind = _em.GetHp(behind);
            float end = Time.time + 0.6f;
            while (Time.time < end && _em.GetHp(front) >= hpFront) yield return null;
            Assert.Less(_em.GetHp(front), hpFront, "enemy in front should be hit");
            Assert.AreEqual(hpBehind, _em.GetHp(behind), 0.001f, "enemy behind the swing should not be hit");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Crit_HitStops_And_Kill_RaisesEvent()
        {
            yield return Boot();
            _weapons.critChanceBonus = 1f;
            int kills = 0, crits = 0;
            EventHandler<KillEvent> onKill = (in KillEvent e) => kills++;
            EventHandler<HitEvent> onCrit = (in HitEvent e) => crits++;
            GameEvents.OnKill.Subscribe(onKill);
            GameEvents.OnCrit.Subscribe(onCrit);
            bool sawHitStop = false;
            try
            {
                _em.Spawn(_convict, _player.transform.position + new Vector3(0f, 0f, 1.4f));
                float end = Time.realtimeSinceStartup + 2f;
                float afterKill = -1f;
                while (Time.realtimeSinceStartup < end)
                {
                    if (TimeController.InHitStop && Time.timeScale < 0.5f) sawHitStop = true;
                    if (kills > 0 && afterKill < 0f) afterKill = Time.realtimeSinceStartup;
                    if (afterKill > 0f && Time.realtimeSinceStartup - afterKill > 0.1f) break;
                    yield return null;
                }
            }
            finally
            {
                GameEvents.OnKill.Unsubscribe(onKill);
                GameEvents.OnCrit.Unsubscribe(onCrit);
            }
            Assert.GreaterOrEqual(crits, 1, "forced crit should raise OnCrit");
            Assert.IsTrue(sawHitStop, "crit should trigger a brief hit-stop");
            Assert.AreEqual(1, kills, "a crit (24) kills a 20 HP convict and raises OnKill");
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.AreEqual(1f, Time.timeScale, "hit-stop must end");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Enemies_DoNotWalkThroughWalls()
        {
            yield return Boot();
            // Player behind the wall corner; enemies spawned on the far side must go around, never through.
            var wallCols = new System.Collections.Generic.List<Collider>();
            foreach (var c in Object.FindObjectsByType<MeshCollider>())
                if (c.name == "SM_Wall_Stone_4m") wallCols.Add(c);
            Assert.Greater(wallCols.Count, 0, "test arena walls not found");
            _player.GetComponent<PlayerMotor>().Teleport(new Vector3(-3f, 0.1f, -15.5f));
            DevSettings.GodMode = true; // eight convicts reach the player while the test watches the wall
            _weapons.enabled = false;   // no kills → no XP → no level-up pause freezing Time.time mid-test
            for (int k = 0; k < 8; k++) _em.Spawn(_convict, new Vector3(-4.5f + k * 0.6f, 0f, -10.5f));
            int violations = 0;
            float end = Time.time + 4f, realEnd = Time.realtimeSinceStartup + 20f;
            while (Time.time < end && Time.realtimeSinceStartup < realEnd)
            {
                for (int i = 0; i < _em.HighWater; i++)
                {
                    if (!_em.IsAlive(i)) continue;
                    Vector3 ep = _em.GetPosition(i) + Vector3.up * 0.8f;
                    foreach (var c in wallCols)
                    {
                        var b = c.bounds;
                        b.Expand(-0.05f);
                        if (b.Contains(ep)) violations++;
                    }
                }
                yield return null;
            }
            Assert.AreEqual(0, violations, "an enemy entered a wall");
        }
    }
}
