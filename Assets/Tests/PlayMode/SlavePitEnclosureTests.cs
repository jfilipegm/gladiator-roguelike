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
    /// Playtest rebuild of the Slave Pit (an 84 × 70 m oval stockade): the enclosure is closed at every bearing and
    /// height (the producer fell off the old map), the player can't get out over a low fence or the berm even high in
    /// the air, and the arena's oval keeps enemies inside it.
    /// </summary>
    public class SlavePitEnclosureTests
    {
        const string Scene = "Assets/Scenes/Arenas/SlavePit.unity";
        const float RX = 42f, RZ = 35f; // the boundary line (SlavePitLayout)

        [TearDown]
        public void TearDown()
        {
            DevSettings.ResetAll();
            TimeController.ResetForNewRun();
        }

        IEnumerator Boot()
        {
            DevSettings.ResetAll();
            DevSettings.GodMode = true;
            TimeController.ResetForNewRun();
            var op = SceneManager.LoadSceneAsync(Scene, LoadSceneMode.Single);
            while (!op.isDone) yield return null;
            yield return null;
            if (Director.Instance != null) Director.Instance.enabled = false;
            float end = Time.realtimeSinceStartup + 8f;
            while (Time.realtimeSinceStartup < end && RunIntro.Instance != null && !RunIntro.Instance.Done) yield return null;
            yield return null;
        }

        static Vector3 Edge(float bearingDeg)
        {
            float a = bearingDeg * Mathf.Deg2Rad;
            return new Vector3(RX * Mathf.Sin(a), 0f, RZ * Mathf.Cos(a));
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Enclosure_IsClosed_AtEveryBearingAndHeight()
        {
            yield return Boot();
            int mask = (1 << Layers.Props) | (1 << Layers.Environment);
            int gaps = 0;
            var report = new System.Text.StringBuilder();
            foreach (float h in new[] { 1f, 3.5f, 8f, 20f })
            {
                for (int k = 0; k < 144; k++)
                {
                    float bearing = k * 2.5f;
                    var edge = Edge(bearing);
                    var dir = edge.normalized;
                    float lineDist = edge.magnitude;
                    // from 6 m inside the line outward: something must stop the player within 4.5 m past the line
                    var from = dir * (lineDist - 6f) + Vector3.up * h;
                    if (!Physics.Raycast(from, dir, out var hit, 10.5f, mask, QueryTriggerInteraction.Ignore))
                    {
                        gaps++;
                        if (gaps <= 8) report.Append($" {bearing}deg@{h}m;");
                    }
                }
            }
            Assert.AreEqual(0, gaps, "open spots in the enclosure:" + report);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Player_CantGetOut_OverALowFence_OrTheBerm_EvenHighUp()
        {
            yield return Boot();
            var player = Player.Main;
            player.GetComponent<PlayerMotor>().enabled = false; // drive the capsule directly
            var cc = player.GetComponent<CharacterController>();
            // bearing 105 is a patched fence (the lowest piece), 67.5 a berm, 285 a platform
            foreach (float bearing in new[] { 105f, 67.5f, 285f })
            {
                foreach (float height in new[] { 0.2f, 3f, 9f })
                {
                    var edge = Edge(bearing);
                    var dir = edge.normalized;
                    player.transform.position = dir * (edge.magnitude - 4f) + Vector3.up * height;
                    Physics.SyncTransforms();
                    for (int s = 0; s < 40; s++) cc.Move(dir * 0.5f); // 20 m straight at the boundary
                    var p = player.transform.position;
                    float out01 = Mathf.Sqrt(p.x * p.x / (RX * RX) + p.z * p.z / (RZ * RZ));
                    Assert.Less(out01, 1.08f, $"stopped at the boundary (bearing {bearing}, {height} m up): {p}");
                }
            }
            player.GetComponent<PlayerMotor>().enabled = true;
            yield return null;
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator TheArenaIsAnOval_EnemiesAndSpawnsStayInside()
        {
            yield return Boot();
            var em = EnemyManager.Instance;
            Assert.Less(em.arenaAspect, 0.9f, "an oval, wider than deep");
            Assert.Greater(em.arenaRadius, 38f, "about twice the old 20 m pit");
            var north = em.EdgePoint(Mathf.PI * 0.5f, 2f);
            Assert.AreEqual(em.ArenaRadiusZ - 2f, north.z, 0.01f, "the spawn ring follows the oval");
            Assert.AreEqual(0f, north.x, 0.01f);
            var p = new Vector3(0f, 0f, 38f); // inside the old round radius, outside the oval
            Assert.Greater(em.EdgeDistance01(p), 1f);
            Assert.IsTrue(em.ClampInside(ref p, 0.5f));
            Assert.LessOrEqual(em.EdgeDistance01(p, 0.5f), 1.0001f);

            var convict = UnityEditor.AssetDatabase.LoadAssetAtPath<EnemyDef>("Assets/Data/Enemies/EN_Convict.asset");
            int slot = em.Spawn(convict, new Vector3(2f, 0f, 37f));
            Assert.GreaterOrEqual(slot, 0);
            yield return null;
            yield return null;
            Assert.LessOrEqual(em.EdgeDistance01(em.GetPosition(slot)), 1.001f, "an enemy outside the oval is pulled back in");
        }

        /// <summary>The first bot run in the big pit parked for minutes beside a chest near the east wall: its round
        /// leash (31 m) was smaller than the oval, so it never opened a chest, picked up a gem or a gift.</summary>
        [UnityTest, Timeout(60000)]
        public IEnumerator Bot_OpensAChestNearTheEdgeOfTheOval()
        {
            yield return Boot();
            var em = EnemyManager.Instance;
            var props = Interactables.Instance;
            var chest = new Vector3(34f, 0f, -1f); // a layout chest anchor, 34 m out along the long axis
            chest.y = em.Height.Sample(chest.x, chest.z);
            Assert.GreaterOrEqual(props.PlaceChestAt(chest), 0, "a free chest slot");
            RunState.Current.AddGold(200);
            Player.Main.GetComponent<PlayerMotor>().Teleport(new Vector3(29f, em.Height.Sample(29f, -1f) + 0.1f, -1f));
            var bot = Object.FindAnyObjectByType<PlaytestBot>(FindObjectsInactive.Include);
            Assert.IsNotNull(bot, "the scene has a bot");
            bot.gameObject.SetActive(true);
            bot.enabled = true;
            float end = Time.realtimeSinceStartup + 10f;
            while (Time.realtimeSinceStartup < end && props.ChestsOpened == 0) yield return null;
            Vector3 p = Player.Main.transform.position;
            bot.gameObject.SetActive(false);
            Assert.AreEqual(1, props.ChestsOpened, $"the bot walked to the chest by the wall and opened it (bot at {p})");
        }
    }
}
