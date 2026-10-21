using Gladiator.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace Gladiator.Tests
{
    /// <summary>R2 (docs/ARENAS.md §6): the Chimera's cone (<see cref="EnemyManager.InCone"/>) and the data defaults that
    /// keep every existing enemy as it was (no reassembling, the Breather appended after the Caster).</summary>
    public class BreathConeTests
    {
        const float Length = 8f, Angle = 60f; // the Chimera's cone
        static readonly Vector3 Mouth = new Vector3(3f, 1.2f, -2f);

        /// <summary>A point `dist` m from the mouth, `deg` degrees off an aim of +Z.</summary>
        static Vector3 At(float deg, float dist) =>
            Mouth + new Vector3(Mathf.Sin(deg * Mathf.Deg2Rad), 0f, Mathf.Cos(deg * Mathf.Deg2Rad)) * dist;

        static bool In(Vector3 p, float pad = 0f) => EnemyManager.InCone(Mouth, Vector3.forward, Length, Angle, p, pad);

        [Test]
        public void OnTheAxis_IsInside_UpToItsLength()
        {
            Assert.IsTrue(In(At(0f, 0.5f)), "at the mouth");
            Assert.IsTrue(In(At(0f, 4f)), "halfway");
            Assert.IsTrue(In(At(0f, 7.95f)), "at its tip");
        }

        [Test]
        public void At29Degrees_IsInside_At31Outside()
        {
            Assert.IsTrue(In(At(29f, 5f)), "29° right");
            Assert.IsTrue(In(At(-29f, 5f)), "29° left");
            Assert.IsFalse(In(At(31f, 5f)), "31° right");
            Assert.IsFalse(In(At(-31f, 5f)), "31° left");
        }

        [Test]
        public void PastTheLength_IsOutside()
        {
            Assert.IsFalse(In(At(0f, 8.05f)), "just past the tip");
            Assert.IsFalse(In(At(0f, 9f)), "9 m away");
            Assert.IsTrue(In(At(0f, 8.3f), 0.35f), "a body reaching in from just past the tip");
            Assert.IsFalse(In(At(0f, 8.4f), 0.35f), "a body just out of reach");
        }

        [Test]
        public void BehindOrBeside_IsOutside()
        {
            Assert.IsFalse(In(At(180f, 2f)), "behind");
            Assert.IsFalse(In(At(90f, 2f)), "beside");
            Assert.IsFalse(In(At(135f, 6f)), "behind and to the side");
            Assert.IsTrue(In(At(180f, 0.2f), 0.35f), "a body touching the mouth, even from behind");
        }

        [Test]
        public void ABody_ReachesInByTheAngleItSubtends()
        {
            // the player (radius 0.35) at 5 m: asin(0.07) ≈ 4° more on each side
            Assert.IsTrue(In(At(33f, 5f), 0.35f), "33° at 5 m with a 0.35 m body");
            Assert.IsFalse(In(At(40f, 5f), 0.35f), "40° off its aim (the PlayMode test's miss)");
            Assert.IsFalse(In(At(36f, 5f), 0.35f), "36° at 5 m");
        }

        [Test]
        public void TheAimIsFlattened_AndHeightsAreTheCallers()
        {
            Vector3 aim = new Vector3(0f, 5f, 2f); // not unit, tilted up: only its XZ direction counts
            Assert.IsTrue(EnemyManager.InCone(Mouth, aim, Length, Angle, At(20f, 6f) + Vector3.up * 3f, 0f));
            Assert.IsFalse(EnemyManager.InCone(Mouth, aim, Length, Angle, At(-40f, 6f), 0f));
            Assert.IsFalse(EnemyManager.InCone(Mouth, Vector3.up, Length, Angle, At(0f, 3f), 0f), "no horizontal aim: nothing");

            // any aim: a point on it is inside, one turned 31° off it isn't
            for (float yaw = -170f; yaw < 180f; yaw += 23f)
            {
                var q = Quaternion.Euler(0f, yaw, 0f);
                Vector3 a = q * Vector3.forward;
                Assert.IsTrue(EnemyManager.InCone(Mouth, a, Length, Angle, Mouth + a * 6f, 0f), "on the aim, yaw " + yaw);
                Assert.IsFalse(EnemyManager.InCone(Mouth, a, Length, Angle, Mouth + Quaternion.Euler(0f, 31f, 0f) * a * 6f, 0f), "31° off, yaw " + yaw);
            }
        }

        [Test]
        public void ANewEnemy_DoesntReassembleOrBreathe_AndTheBreatherIsAppended()
        {
            var def = ScriptableObject.CreateInstance<EnemyDef>();
            try
            {
                Assert.AreEqual(0, def.reviveCount, "nothing reassembles unless its data says so");
                Assert.AreEqual(2.5f, def.reviveDelay);
                Assert.AreEqual(0.5f, def.reviveHpMul);
                Assert.AreEqual(0.9f, def.stompRadius);
                Assert.IsTrue(def.burnStopsRevive);
                Assert.IsNull(def.pileMesh);
                Assert.AreEqual(EnemyBehaviour.Chaser, def.behaviour);
                Assert.AreEqual(60f, def.breathAngle);
                Assert.AreEqual(1f, def.breathTime);
                Assert.AreEqual(3, def.breathPatches);
                Assert.AreEqual(1.4f, def.breathPatchRadius);
                Assert.AreEqual(4f, def.breathKnockback);
            }
            finally { Object.DestroyImmediate(def); }

            // enum values are serialized: appended, never inserted
            Assert.AreEqual(9, (int)EnemyBehaviour.Caster);
            Assert.AreEqual(10, (int)EnemyBehaviour.Breather);
            Assert.AreNotEqual(EnemyManager.FallSourceId, EnemyManager.StompSourceId);
            Assert.AreNotEqual(EnemyManager.StompSourceId, EnemyManager.BreathSourceId);
            Assert.AreNotEqual(EnemyManager.BreathSourceId, EnemyManager.TrampleSourceId);
            Assert.AreEqual(Gladiator.Core.GameInfo.StableId("hypogeum"), EnemyManager.FallSourceId, "the hypogeum part hashes the same string");
        }
    }
}
