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
    /// M2.3: the Director keeps the curve's head-count, honours the table's time windows and per-type caps, pours a
    /// swarm in from one side, sends elites, and never spawns on top of the player. The run clock is pinned with
    /// Timeless; the weapon is off so nothing dies.
    /// </summary>
    public class DirectorTests
    {
        const string Scene = "Assets/Scenes/Dev/TestArena.unity";

        Director _dir;
        EnemyManager _em;
        RunState _run;
        readonly List<DirectorEvent> _events = new List<DirectorEvent>();
        void OnDirector(in DirectorEvent e) => _events.Add(e);

        IEnumerator Boot(float clock)
        {
            DevSettings.ResetAll();
            TimeController.GameSpeed = 1f;
            TimeController.ResetForNewRun();
            var op = SceneManager.LoadSceneAsync(Scene, LoadSceneMode.Single);
            while (!op.isDone) yield return null;
            var player = Player.Main;
            player.OverrideSource = new ScriptedInput();
            player.GetComponent<WeaponSystem>().enabled = false;
            DevSettings.GodMode = true;
            DevSettings.Timeless = true; // pin the run clock
            _dir = Director.Instance;
            _em = EnemyManager.Instance;
            _run = RunState.Current;
            _dir.enabled = false;
            yield return null;
            _em.KillAll();
            _run.SetTimeRemaining(_run.config.runDuration - clock);
            yield return new WaitForSeconds(0.3f);
            PickupSystem.Instance.ClearAll();
            _events.Clear();
            GameEvents.OnDirector.Subscribe(OnDirector);
            _dir.enabled = true;
            yield return Until(() => _dir.Prewarmed, 10f);
        }

        [TearDown]
        public void TearDown()
        {
            GameEvents.OnDirector.Unsubscribe(OnDirector);
            DevSettings.ResetAll();
        }

        int CountOf(string id)
        {
            int n = 0;
            for (int i = 0; i < _em.HighWater; i++) if (_em.IsAlive(i) && _em.GetDef(i).id == id) n++;
            return n;
        }

        static IEnumerator Until(System.Func<bool> done, float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!done() && Time.realtimeSinceStartup < end) yield return null;
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator EarlyRun_OnlyConvicts_AwayFromThePlayer()
        {
            yield return Boot(5f);
            var seen = new HashSet<int>();
            float minDist = float.MaxValue;
            float end = Time.realtimeSinceStartup + 3f;
            while (Time.realtimeSinceStartup < end)
            {
                Vector3 p = Player.Main.transform.position;
                for (int i = 0; i < _em.HighWater; i++)
                {
                    if (!_em.IsAlive(i) || !seen.Add(_em.GetUid(i))) continue;
                    Vector3 d = _em.GetPosition(i) - p;
                    minDist = Mathf.Min(minDist, new Vector2(d.x, d.z).magnitude);
                }
                yield return null;
            }
            Assert.Greater(_em.AliveCount, 5, "fills toward the early target");
            Assert.LessOrEqual(_em.AliveCount, _dir.TargetAlive, "never above the steady target");
            Assert.AreEqual(_em.AliveCount, CountOf("convict"), "at 0:05 the table only has convicts");
            Assert.GreaterOrEqual(minDist, _dir.config.minPlayerDistance - 0.6f, "never spawns on top of the player");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator MinuteFive_FillsToTarget_RespectingTypeCaps()
        {
            yield return Boot(300f);
            int target = _dir.TargetAlive;
            Assert.AreEqual(300, target, 5, "§8: ~300 alive at minute 5");
            yield return Until(() => _em.AliveCount >= target - 2, 40f);
            Assert.GreaterOrEqual(_em.AliveCount, target - 2, "reaches the target");
            yield return new WaitForSeconds(1f);
            Assert.LessOrEqual(_em.AliveCount, target, "and holds there");
            foreach (var e in _dir.table.entries)
                if (e.maxAlive > 0)
                    Assert.LessOrEqual(_em.AliveOf(e.enemy), e.maxAlive, e.enemy.id + " over its cap");
            Assert.Greater(CountOf("pitcur"), 0);
            Assert.Greater(CountOf("shieldthug"), 0);
            Assert.Greater(CountOf("slinger"), 0, "kiting types do appear, just capped");
            Assert.AreEqual(0, _em.PoolGrowths, "pools were prewarmed to the table");
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator Swarm_PoursInFromOneSide()
        {
            yield return Boot(60f);
            yield return Until(() => _em.AliveCount >= _dir.TargetAlive, 15f);
            var before = new HashSet<int>();
            for (int i = 0; i < _em.HighWater; i++) if (_em.IsAlive(i)) before.Add(_em.GetUid(i));
            int alive0 = _em.AliveCount;
            _dir.TriggerSwarmNow();
            // record each newcomer where it first appears (they walk toward the player right away)
            var spawnDirs = new Dictionary<int, Vector3>();
            float until = Time.time + _dir.config.swarmDuration + 0.4f;
            while (Time.time < until)
            {
                for (int i = 0; i < _em.HighWater; i++)
                {
                    if (!_em.IsAlive(i) || before.Contains(_em.GetUid(i)) || spawnDirs.ContainsKey(_em.GetUid(i))) continue;
                    Vector3 d = _em.GetPosition(i) - _em.arenaCenter; d.y = 0f;
                    spawnDirs[_em.GetUid(i)] = d.normalized;
                }
                yield return null;
            }
            Assert.AreEqual(1, _dir.SwarmsStarted);
            int swarmEvents = 0;
            foreach (var e in _events) if (e.Kind == DirectorEventKind.Swarm) swarmEvents++;
            Assert.AreEqual(1, swarmEvents, "announced once"); // (0:60 is also when the first elite is due)
            int expected = Mathf.RoundToInt(_dir.config.swarmSize.Evaluate(60f));
            Assert.GreaterOrEqual(_em.AliveCount - alive0, expected - 1, "the swarm adds its size on top of the target");
            Assert.LessOrEqual(_em.AliveCount - alive0, expected + 1);

            // all newcomers came from one arc of the ring
            Vector3 sum = Vector3.zero;
            foreach (var d in spawnDirs.Values) sum += d;
            Assert.Greater(spawnDirs.Count, 0);
            float worst = 0f;
            foreach (var d in spawnDirs.Values) worst = Mathf.Max(worst, Vector3.Angle(sum, d));
            Assert.Less(worst, 60f, "a swarm arrives from one side, not all around");
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator Elite_IsSentOnCadence_AndAnnounced()
        {
            yield return Boot(200f);
            _dir.TriggerEliteNow();
            yield return null;
            yield return null;
            Assert.AreEqual(1, _dir.ElitesSent);
            Assert.AreEqual(1, _events.Count);
            Assert.AreEqual(DirectorEventKind.Elite, _events[0].Kind);
            int elites = 0;
            for (int i = 0; i < _em.HighWater; i++) if (_em.IsAlive(i) && _em.IsElite(i)) elites++;
            Assert.AreEqual(1, elites);
            Assert.AreEqual(_run.Elapsed + _dir.config.eliteEvery, _dir.NextEliteAt, 0.05f, "next one ~90 s later");
        }
    }
}
