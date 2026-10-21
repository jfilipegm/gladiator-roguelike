using Gladiator.Core;
using Gladiator.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace Gladiator.Tests
{
    /// <summary>
    /// R2 (docs/ARENAS.md §6, "the trapdoors"; docs/tracks/R2-design.md § Hypogeum): the hypogeum's pure maths with the
    /// Colosseum's numbers. A door is 8 m along its local X and 4 m across (local X = (cos, −sin), local Z = (sin, cos)
    /// of its yaw): a point 3.9 m along or 1.9 m across is on it, 4.1 along or 2.1 across isn't; the player is thrown
    /// across, toward the nearer long edge; a drop takes the group of the door nearest the player, never the same group
    /// twice running; none of it allocates. A walk-only box (a door left open) pushes bodies out only while switched on,
    /// never stops a shot, and is never baked. The Director's swarm multiplier grows the swarm types' pools. No scene.
    /// </summary>
    public class HypogeumMathTests
    {
        static readonly Vector2 Size = new Vector2(8f, 4f);

        /// <summary>The contract's 30 trapdoors (x, z, yaw, group): docs/tracks/R2-colosseum.md.</summary>
        static readonly Vector4[] Doors =
        {
            new Vector4(61.18f, 6.80f, 76.4f, 0), new Vector4(55.23f, 19.08f, 53.0f, 0), new Vector4(45.40f, 28.61f, 36.1f, 0),
            new Vector4(33.46f, 35.36f, 23.5f, 0), new Vector4(20.43f, 39.65f, 13.3f, 0), new Vector4(6.86f, 41.74f, 4.3f, 0),
            new Vector4(-6.86f, 41.74f, -4.3f, 1), new Vector4(-20.43f, 39.65f, -13.3f, 1), new Vector4(-33.46f, 35.36f, -23.5f, 1),
            new Vector4(-45.40f, 28.61f, -36.1f, 1), new Vector4(-55.23f, 19.08f, -53.0f, 1), new Vector4(-61.18f, 6.80f, -76.4f, 1),
            new Vector4(-61.18f, -6.80f, 76.4f, 2), new Vector4(-55.23f, -19.08f, 53.0f, 2), new Vector4(-45.40f, -28.61f, 36.1f, 2),
            new Vector4(-33.46f, -35.36f, 23.5f, 2), new Vector4(-20.43f, -39.65f, 13.3f, 2), new Vector4(-6.86f, -41.74f, 4.3f, 2),
            new Vector4(6.86f, -41.74f, -4.3f, 3), new Vector4(20.43f, -39.65f, -13.3f, 3), new Vector4(33.46f, -35.36f, -23.5f, 3),
            new Vector4(45.40f, -28.61f, -36.1f, 3), new Vector4(55.23f, -19.08f, -53.0f, 3), new Vector4(61.18f, -6.80f, -76.4f, 3),
            new Vector4(-20f, 0f, 0f, 4), new Vector4(-32f, 0f, 0f, 4), new Vector4(-44f, 0f, 0f, 4),
            new Vector4(20f, 0f, 0f, 5), new Vector4(32f, 0f, 0f, 5), new Vector4(44f, 0f, 0f, 5),
        };

        /// <summary>A point `along` m down the door's local X and `across` m down its local Z from its centre.</summary>
        static Vector3 Local(Vector2 centre, float yawDeg, float along, float across)
        {
            float yaw = yawDeg * Mathf.Deg2Rad, c = Mathf.Cos(yaw), s = Mathf.Sin(yaw);
            return new Vector3(centre.x, 0f, centre.y) + new Vector3(c, 0f, -s) * along + new Vector3(s, 0f, c) * across;
        }

        [TestCase(0f, 32f, 0f)]
        [TestCase(76.4f, 61.18f, 6.80f)]
        [TestCase(-23.5f, -33.46f, 35.36f)]
        public void InRect_TheDoorIs8mAlongAnd4mAcross_AtAnyYaw(float yaw, float x, float z)
        {
            var c = new Vector2(x, z);
            Assert.IsTrue(Hypogeum.InRect(c, yaw, Size, new Vector3(x, 0f, z), 0f), "the centre");
            Assert.IsTrue(Hypogeum.InRect(c, yaw, Size, Local(c, yaw, 3.9f, 0f), 0f), "3.9 m along");
            Assert.IsTrue(Hypogeum.InRect(c, yaw, Size, Local(c, yaw, -3.9f, 0f), 0f), "3.9 m the other way");
            Assert.IsTrue(Hypogeum.InRect(c, yaw, Size, Local(c, yaw, 0f, 1.9f), 0f), "1.9 m across");
            Assert.IsTrue(Hypogeum.InRect(c, yaw, Size, Local(c, yaw, 3.9f, -1.9f), 0f), "a corner");
            Assert.IsFalse(Hypogeum.InRect(c, yaw, Size, Local(c, yaw, 4.1f, 0f), 0f), "4.1 m along");
            Assert.IsFalse(Hypogeum.InRect(c, yaw, Size, Local(c, yaw, 0f, -2.1f), 0f), "2.1 m across");
            Assert.IsFalse(Hypogeum.InRect(c, yaw, Size, Local(c, yaw, 0f, 2.1f), 0f), "2.1 m across the other way");
            Assert.IsTrue(Hypogeum.InRect(c, yaw, Size, Local(c, yaw, 0f, 2.1f), 0.2f), "the pad grows it");
            Assert.IsFalse(Hypogeum.InRect(c, yaw, Size, Local(c, yaw, 2.5f, 2.5f), 0f), "0.5 m past the long edge, 2.5 m along");
        }

        [TestCase(0f)]
        [TestCase(76.4f)]
        [TestCase(-53f)]
        public void OutOfRect_PointsAcrossTheDoor_TowardTheNearerLongEdge(float yaw)
        {
            var c = new Vector2(10f, -4f);
            float r = yaw * Mathf.Deg2Rad;
            var across = new Vector3(Mathf.Sin(r), 0f, Mathf.Cos(r));
            var north = Hypogeum.OutOfRect(c, yaw, Size, Local(c, yaw, 3f, 0.4f));
            var south = Hypogeum.OutOfRect(c, yaw, Size, Local(c, yaw, -3.5f, -0.4f));
            Assert.AreEqual(1f, north.magnitude, 1e-4f, "a unit push");
            Assert.AreEqual(0f, north.y, 1e-6f, "flat");
            Assert.Less(Vector3.Distance(across, north), 1e-4f, "+local Z side → +local Z, whatever the point's place along the door");
            Assert.Less(Vector3.Distance(-across, south), 1e-4f, "−local Z side → −local Z");
            Assert.Less(Vector3.Distance(across, Hypogeum.OutOfRect(c, yaw, Size, new Vector3(c.x, 0f, c.y))), 1e-4f, "dead centre: one way, not zero");
            // a rectangle longer along Z is crossed along X (the same rule: toward the nearer long edge)
            var tall = Hypogeum.OutOfRect(c, yaw, new Vector2(4f, 8f), Local(c, yaw, -0.5f, 3f));
            Assert.Less(Vector3.Distance(-new Vector3(Mathf.Cos(r), 0f, -Mathf.Sin(r)), tall), 1e-4f);
        }

        [Test]
        public void PickGroup_TakesTheGroupOfTheNearestDoor_AndAnotherGroupWhenThatOneWasLast()
        {
            Assert.AreEqual(3, Hypogeum.PickGroup(Doors, new Vector3(10f, 0f, -30f), -1), "the ring's SE quarter is nearest");
            Assert.AreEqual(2, Hypogeum.PickGroup(Doors, new Vector3(10f, 0f, -30f), 3), "SE just dropped: the nearest door of another group (SW)");
            Assert.AreEqual(5, Hypogeum.PickGroup(Doors, new Vector3(25f, 0f, 2f), -1), "the east axis");
            Assert.AreEqual(0, Hypogeum.PickGroup(Doors, new Vector3(25f, 0f, 2f), 5), "then the ring's NE quarter");
            Assert.AreEqual(4, Hypogeum.PickGroup(Doors, new Vector3(-50f, 0f, 1f), 0), "a last group that isn't the nearest changes nothing");
            Assert.AreEqual(1, Hypogeum.PickGroup(Doors, new Vector3(-50f, 0f, 1f), 4));
            Assert.AreEqual(-1, Hypogeum.PickGroup(new Vector4[0], Vector3.zero, -1), "no doors");
            Assert.AreEqual(-1, Hypogeum.PickGroup(null, Vector3.zero, -1));
            var single = new[] { new Vector4(0f, 0f, 0f, 2) };
            Assert.AreEqual(2, Hypogeum.PickGroup(single, Vector3.zero, 2), "only one group: it drops again");
        }

        [Test]
        public void TheMaths_AllocateNothing()
        {
            var p = new Vector3(10f, 0f, -30f);
            int sum = 0;
            TestDelegate run = () =>
            {
                for (int i = 0; i < 1000; i++)
                {
                    sum += Hypogeum.PickGroup(Doors, p, i % 6);
                    if (Hypogeum.InRect(new Vector2(i, 0f), i, Size, p, 0f)) sum++;
                    sum += (int)Hypogeum.OutOfRect(new Vector2(0f, i), i, Size, p).x;
                }
            };
            run(); // warm up (JIT)
            Assert.That(run, Is.Not.AllocatingGCMemory());
        }

        [Test]
        public void AWalkOnlyBox_PushesBodiesOutOnlyWhileOn_NeverStopsAShot_AndIsNeverBaked()
        {
            // one baked box (a wall) far away, then a door's walk-only box at (10, 5), yaw 36.1
            var wallA = new[] { new Vector4(-30f, 0f, 1f, 0f) };
            var wallB = new[] { new Vector4(0f, 1f, 1f, 3f) };
            var obstacles = StaticObstacles.FromArrays(new Vector4[0], wallA, wallB);
            var centre = new Vector2(10f, 5f);
            const float yaw = 36.1f;
            int box = obstacles.AddBox(centre, yaw, new Vector2(4.05f, 2.05f), active: false, walkOnly: true);
            Assert.AreEqual(1, box, "appended after the baked box");
            Assert.IsTrue(obstacles.WalkOnly(box));
            Assert.IsFalse(obstacles.WalkOnly(0));
            Assert.IsFalse(obstacles.BoxActive(box), "added switched off");
            Assert.AreEqual(2, obstacles.Count);

            var body = Local(centre, yaw, 1f, 0.5f);
            var p = body;
            Assert.IsFalse(obstacles.PushOut(ref p, 0.4f), "off: walks over the closed door");
            Assert.AreEqual(body, p);

            obstacles.SetBoxActive(box, true);
            Assert.IsTrue(obstacles.PushOut(ref p, 0.4f), "on: pushed out");
            Assert.IsFalse(Hypogeum.InRect(centre, yaw, new Vector2(8.1f, 4.1f), p, 0.39f), "out of the box and its body clear: " + p);
            Assert.Less(Vector3.Distance(p, Local(centre, yaw, 1f, 2.05f + 0.4f)), 0.01f, "out across the nearer long edge, the shortest way");

            Assert.IsFalse(obstacles.Overlaps(new Vector2(body.x, body.z), 0.2f), "a shot over the open door flies on");
            Assert.IsTrue(obstacles.Overlaps(new Vector2(-30f, 0f), 0.2f), "the wall still stops it");
            Assert.AreEqual(-1, obstacles.FindBox(centre, 1f), "FindBox (a barricade's lookup) skips it");

            obstacles.ToArrays(out var circles, out var boxesA, out var boxesB);
            Assert.AreEqual(0, circles.Length);
            Assert.AreEqual(1, boxesA.Length, "baked: the wall only");
            Assert.AreEqual(1, boxesB.Length);
            Assert.AreEqual(-30f, boxesA[0].x, 1e-4f);

            obstacles.SetBoxActive(box, false);
            p = body;
            Assert.IsFalse(obstacles.PushOut(ref p, 0.4f), "closed again: walks over it");

            // a solid box added the same way blocks shots and is baked
            int solid = obstacles.AddBox(new Vector2(40f, 0f), 0f, new Vector2(1f, 1f), active: true, walkOnly: false);
            Assert.IsFalse(obstacles.WalkOnly(solid));
            Assert.IsTrue(obstacles.Overlaps(new Vector2(40f, 0f), 0.1f));
            obstacles.ToArrays(out _, out boxesA, out _);
            Assert.AreEqual(2, boxesA.Length, "the wall and the solid box");
        }

        [Test]
        public void TheSwarmMultiplier_GrowsTheSwarmTypesPools_AndTheCapReadsTheArenas()
        {
            var go = new GameObject("Director (test)");
            var config = ScriptableObject.CreateInstance<DirectorConfig>();
            var table = ScriptableObject.CreateInstance<SpawnTableDef>();
            var swarmer = ScriptableObject.CreateInstance<EnemyDef>();
            var loner = ScriptableObject.CreateInstance<EnemyDef>();
            try
            {
                var director = go.AddComponent<Director>();
                director.config = config;
                director.table = table;
                table.entries = new[]
                {
                    new SpawnTableDef.Entry { enemy = swarmer, weightStart = 1f, weightEnd = 1f, swarm = true },
                    new SpawnTableDef.Entry { enemy = loner, weightStart = 1f, weightEnd = 1f, maxAlive = 10 },
                };
                int[] plain = director.ComputePoolTargets(480f);
                director.swarmMultiplier = 1.5f;
                int[] huge = director.ComputePoolTargets(480f);
                float swarm = config.swarmSize.Evaluate(480f);
                Assert.AreEqual(Mathf.CeilToInt(swarm * 0.5f) * 1.1f, huge[0] - plain[0], 2f, "the swarm type's pool grows by half a swarm (× the 10 % slack)");
                Assert.AreEqual(plain[1], huge[1], "a type that never swarms is unchanged");
                director.swarmMultiplier = 0f;
                Assert.AreEqual(plain[0], director.ComputePoolTargets(480f)[0], "0 reads as 1");

                Assert.AreEqual(config.hardCap, director.AliveCap, "no arena cap: the config's");
                director.maxAliveOverride = 450;
                Assert.AreEqual(Mathf.Min(config.hardCap, 450), director.AliveCap, "the Colosseum's 450");
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(config);
                Object.DestroyImmediate(table);
                Object.DestroyImmediate(swarmer);
                Object.DestroyImmediate(loner);
            }
        }
    }
}
