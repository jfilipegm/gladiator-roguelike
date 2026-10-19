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
    /// M3.2: Arena 1 built from its layout — crowd, anchors and baked nav are there; mud slows the player and enemies;
    /// fire pits burn a grounded player; swarms come through a gate and its doors open; nobody leaves the pit.
    /// </summary>
    public class SlavePitTests
    {
        const string Scene = "Assets/Scenes/Arenas/SlavePit.unity";
        Player _player;
        PlayerMotor _motor;
        EnemyManager _em;
        ArenaLayoutDef _layout;

        IEnumerator Boot(bool director = false)
        {
            DevSettings.ResetAll();
            DevSettings.GodMode = true;
            TimeController.GameSpeed = 1f;
            TimeController.ResetForNewRun();
            var op = SceneManager.LoadSceneAsync(Scene, LoadSceneMode.Single);
            Assert.IsNotNull(op, "Slave Pit not in the build settings");
            while (!op.isDone) yield return null;
            yield return null;
            while (RunIntro.Instance != null && !RunIntro.Instance.Done) yield return null; // the arena intro holds the game
            _player = Player.Main;
            _motor = _player.GetComponent<PlayerMotor>();
            _em = EnemyManager.Instance;
            if (!director && Director.Instance != null) Director.Instance.enabled = false;
            var lvl = Object.FindAnyObjectByType<LevelUpController>();
            if (lvl != null) lvl.enabled = false;
            _player.OverrideSource = new ScriptedInput();
            _player.GetComponent<WeaponSystem>().RemoveAll();
#if UNITY_EDITOR
            _layout = UnityEditor.AssetDatabase.LoadAssetAtPath<ArenaLayoutDef>("Assets/Data/Arenas/AL_SlavePit.asset");
#endif
            yield return new WaitForSeconds(0.3f);
        }

        [TearDown]
        public void TearDown() => DevSettings.ResetAll();

        [UnityTest, Timeout(60000)]
        public IEnumerator BuiltFromLayout_CrowdAnchorsNavGates()
        {
            yield return Boot();
            Assert.IsNotNull(_layout);
            int crowd = 0;
            foreach (var z in _layout.crowd) crowd += z.count;
            Assert.AreEqual(crowd, Object.FindAnyObjectByType<CrowdRenderer>().Count, "every crowd zone filled");
            Assert.AreEqual(_layout.gates.Length, ArenaGates.Instance.Count);
            Assert.Greater(_em.Obstacles.Count, 30, "walls and props baked as obstacles");
            var props = Interactables.Instance;
            for (int i = 0; i < props.SlotCount; i++)
            {
                if (!props.IsReady(i)) continue;
                var anchors = props.KindOf(i) == Interactables.Kind.Chest ? _layout.chestAnchors : _layout.altarAnchors;
                bool onAnchor = false;
                foreach (var a in anchors) onAnchor |= new Vector2(a.x - props.PositionOf(i).x, a.z - props.PositionOf(i).z).sqrMagnitude < 0.01f;
                Assert.IsTrue(onAnchor, props.KindOf(i) + " placed on a layout anchor");
            }
            Assert.AreEqual(_layout.maxAlive, Object.FindAnyObjectByType<Director>(FindObjectsInactive.Include).maxAliveOverride, "the arena cap reaches the Director");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Mud_SlowsThePlayerAndEnemies()
        {
            yield return Boot();
            var mud = MudZones.Instance;
            var c = mud.circles[0];
            var inMud = new Vector3(c.x, 0.1f, c.y);
            Assert.AreEqual(mud.enemySpeed, mud.SpeedAt(inMud), 1e-4f);
            Assert.AreEqual(1f, mud.SpeedAt(new Vector3(c.x + c.z + 1f, 0f, c.y)), 1e-4f);
            _motor.Teleport(inMud);
            yield return new WaitForSeconds(0.3f);
            Assert.IsTrue(mud.PlayerInside);
            Assert.AreEqual(mud.playerSpeed, _motor.SpeedMultiplier, 1e-4f, "the motor runs slower in mud");
            _motor.Teleport(new Vector3(c.x + c.z + 2.5f, 0.1f, c.y));
            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(1f, _motor.SpeedMultiplier, 1e-4f, "full speed out of it");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator FirePit_BurnsAGroundedPlayer()
        {
            yield return Boot();
            DevSettings.GodMode = false;
            var health = _player.GetComponent<PlayerHealth>();
            Vector3 fire = default;
            foreach (var z in _layout.zones) if (z.kind == ArenaLayoutDef.ZoneKind.Fire) { fire = z.position; break; }
            float hp = health.Hp;
            _motor.Teleport(fire + new Vector3(0.6f, 0.1f, 0f)); // off-centre: the whole pit burns, not just its middle
            yield return new WaitForSeconds(1.2f);
            Assert.IsTrue(HazardZones.Instance.PlayerInside);
            Assert.Less(health.Hp, hp, "standing in the fire pit hurts");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Swarm_ComesThroughAGate_DoorsOpen()
        {
            yield return Boot(director: true);
            _em.KillAll();
            AdminController.Instance.SetTimer(200f); // a few minutes in: swarm types are in the table
            Director.Instance.TriggerSwarmNow();
            yield return new WaitForSeconds(0.8f);
            var gates = ArenaGates.Instance;
            int open = -1;
            for (int i = 0; i < gates.Count; i++) if (gates.IsOpen(i)) open = i;
            Assert.GreaterOrEqual(open, 0, "a gate opened for the swarm");
            int near = 0;
            for (int i = 0; i < _em.HighWater; i++)
            {
                if (!_em.IsAlive(i)) continue;
                Vector3 d = _em.GetPosition(i) - gates.Position(open);
                if (new Vector2(d.x, d.z).magnitude < 7f) near++;
            }
            Assert.Greater(near, 3, "the swarm pours in at that gate");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator NobodyLeavesThePit()
        {
            yield return Boot(director: true);
            AdminController.Instance.SetTimer(250f);
            var input = (ScriptedInput)_player.OverrideSource;
            input.Next.Move = new Vector2(0f, 1f); // run north into the gate (35 m away since the playtest rebuild)
            yield return new WaitForSeconds(9f);
            Vector3 p = _player.transform.position;
            Assert.Greater(p.z, 25f, "got all the way to the north gate");
            Assert.Less(_em.EdgeDistance01(p, -2.5f), 1f, "the player stays inside the walls (the oval + 1.5 m: the gate's door line)");
            for (int i = 0; i < _em.HighWater; i++)
            {
                if (!_em.IsAlive(i)) continue;
                Vector3 e = _em.GetPosition(i);
                Assert.LessOrEqual(new Vector2(e.x, e.z).magnitude, _layout.radius + 0.01f, "enemies stay inside");
            }
        }
    }
}
