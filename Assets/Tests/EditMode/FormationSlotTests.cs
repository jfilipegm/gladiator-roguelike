using Gladiator.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace Gladiator.Tests
{
    /// <summary>Phase 5.16 (docs/ARENAS.md §5): the formation's pure maths (slots in the leader's frame) and the data
    /// defaults that keep every existing escort (the Beast Handler's curs) on a leash, freed when he dies.</summary>
    public class FormationSlotTests
    {
        static void Near(Vector3 expected, Vector3 actual, string what)
        {
            Assert.AreEqual(expected.x, actual.x, 1e-4f, what + " (x)");
            Assert.AreEqual(expected.y, actual.y, 1e-4f, what + " (y)");
            Assert.AreEqual(expected.z, actual.z, 1e-4f, what + " (z)");
        }

        [Test]
        public void SlotWorld_RotatesWithTheLeader()
        {
            var at = new Vector3(10f, 1.2f, -4f);
            var slot = new Vector2(2.6f, 1.8f); // the right end of the Centurion's line
            Near(at + new Vector3(2.6f, 0f, 1.8f), EnemyManager.SlotWorld(at, 0f, slot), "facing +Z: x right, y forward");
            Near(at + new Vector3(1.8f, 0f, -2.6f), EnemyManager.SlotWorld(at, 90f, slot), "facing +X: his right is -Z");
            Near(at + new Vector3(-2.6f, 0f, -1.8f), EnemyManager.SlotWorld(at, 180f, slot), "facing -Z");
            Near(at + new Vector3(-1.8f, 0f, 2.6f), EnemyManager.SlotWorld(at, -90f, slot), "facing -X");
            Near(EnemyManager.SlotWorld(at, 270f, slot), EnemyManager.SlotWorld(at, -90f, slot), "angles wrap");

            // it agrees with how the manager faces a body (yaw = atan2(x, z): Quaternion.Euler(0, yaw, 0))
            for (float yaw = -170f; yaw < 180f; yaw += 37f)
            {
                var q = Quaternion.Euler(0f, yaw, 0f);
                Near(at + q * new Vector3(slot.x, 0f, slot.y), EnemyManager.SlotWorld(at, yaw, slot), "yaw " + yaw);
                Assert.AreEqual(slot.magnitude, Vector3.Distance(at, EnemyManager.SlotWorld(at, yaw, slot)), 1e-4f, "a rigid line");
            }
            Near(at, EnemyManager.SlotWorld(at, 33f, Vector2.zero), "the origin is the leader");
        }

        [Test]
        public void ANewEnemy_KeepsItsEscortsOnALeash_AndFreesThemWhenItDies()
        {
            var def = ScriptableObject.CreateInstance<EnemyDef>();
            try
            {
                Assert.IsNotNull(def.formationSlots);
                Assert.AreEqual(0, def.formationSlots.Length, "no slots: a leash");
                Assert.AreEqual(0f, def.formationHold, "walks on");
                Assert.IsTrue(def.freeEscortsOnDeath, "freed on death, as the Beast Handler's curs always were");
                Assert.IsNull(def.breakStatus);
                Assert.AreEqual(0f, def.breakShieldsDown);
                Assert.AreEqual(0.8f, def.pushPad, 1e-6f);
                Assert.AreEqual(2.6f, def.pushDepth, 1e-6f);
                Assert.AreEqual(9f, def.pushKnockback, 1e-6f);
            }
            finally { Object.DestroyImmediate(def); }

            var handler = UnityEditor.AssetDatabase.LoadAssetAtPath<EnemyDef>("Assets/Data/Enemies/EN_BeastHandler.asset");
            Assert.IsNotNull(handler, "EN_BeastHandler");
            Assert.IsTrue(handler.freeEscortsOnDeath, "the handler's asset predates the field: it takes the default");
            Assert.IsTrue(handler.formationSlots == null || handler.formationSlots.Length == 0, "his curs stay on leashes");
        }
    }
}
