using Gladiator.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace Gladiator.Tests
{
    /// <summary>
    /// R3 (docs/tracks/R3-design.md, "Chain"): the Labyrinth Chain's Sweep pattern, the parts that are pure maths. A sweep
    /// covers the bearings it passed between two frames (padded by an enemy's body, wrapping round ±180° for a whirl), its
    /// push is the chain's tangent (plus a little outward), and bearings are positive to the right.
    /// </summary>
    public class SweepMathTests
    {
        [Test]
        public void Sweep_IsAppendedAfterLob_AtIndex10()
        {
            Assert.AreEqual(10, (int)WeaponPattern.Sweep, "assets serialize the index");
            Assert.AreEqual(9, (int)WeaponPattern.Lob, "nothing before it moved");
        }

        [Test]
        public void WeaponDef_SweepDefaults_AreTheChainsNumbers()
        {
            var w = ScriptableObject.CreateInstance<WeaponDef>();
            Assert.AreEqual(0.3f, w.sweepTime, 1e-6f);
            Assert.AreEqual(1.5f, w.whirlTimeMul, 1e-6f);
            Assert.AreEqual(0.35f, w.sweepOutward, 1e-6f);
            Assert.AreEqual(1.6f, w.tipKnockbackMul, 1e-6f);
            Assert.IsNull(w.linkMesh);
            Assert.AreEqual(0.6f, w.linkLength, 1e-6f);
            Assert.AreEqual(new Vector2(1.05f, 0.55f), w.chainHeight);
            Assert.AreEqual(0.33f, w.weightOffset, 1e-6f);
            Assert.AreEqual("", w.modelPart, "no model part: every other weapon hides nothing");
            Object.DestroyImmediate(w);
        }

        [Test]
        public void PartAnimProfile_R3Defaults_LeaveEveryoneElseAsTheyWere()
        {
            var p = ScriptableObject.CreateInstance<PartAnimProfile>();
            Assert.AreEqual(0.2f, p.bodyTwistScale, 1e-6f, "today's constant");
            Assert.AreEqual(0f, p.idleHeadLift, "no head lift");
            Assert.AreEqual(1.2f, p.idleHeadDelay, 1e-6f);
            Object.DestroyImmediate(p);
        }

        [Test]
        public void SweepCovers_ABearingBetweenTwoFrames_EitherDirection()
        {
            Assert.IsTrue(WeaponSystem.SweepCovers(120f, 100f, 110f, 0f), "passed it");
            Assert.IsFalse(WeaponSystem.SweepCovers(120f, 100f, 90f, 0f), "not there yet");
            Assert.IsFalse(WeaponSystem.SweepCovers(120f, 100f, 125f, 0f), "already past before this frame");
            Assert.IsTrue(WeaponSystem.SweepCovers(120f, 100f, 98f, 3f), "its body pads it");
            Assert.IsTrue(WeaponSystem.SweepCovers(-100f, -80f, -90f, 0f), "the other direction");
            Assert.IsTrue(WeaponSystem.SweepCovers(110f, 110f, 110f, 0f), "a frame that didn't move still covers its bearing");
        }

        [Test]
        public void SweepCovers_WrapsRoundTheBack()
        {
            Assert.IsTrue(WeaponSystem.SweepCovers(170f, 180f, -178f, 3f), "-178 is 182");
            Assert.IsTrue(WeaponSystem.SweepCovers(-170f, -180f, 178f, 3f), "178 is -182");
            Assert.IsFalse(WeaponSystem.SweepCovers(170f, 180f, -170f, 3f), "-170 is 190: past the pad");
        }

        [Test]
        public void SweepCovers_AWholeWhirl_CoversEveryBearing()
        {
            for (float x = -180f; x <= 180f; x += 7.5f)
            {
                Assert.IsTrue(WeaponSystem.SweepCovers(180f, -180f, x, 0f), "right to left: " + x);
                Assert.IsTrue(WeaponSystem.SweepCovers(-180f, 180f, x, 0f), "left to right: " + x);
            }
        }

        [Test]
        public void SweepPush_IsTheChainsTangent_PlusALittleOutward()
        {
            Vector3 outward = new Vector3(0.6f, 0f, 0.8f);
            Vector3 right = WeaponSystem.SweepPush(outward, 1f, 0f), left = WeaponSystem.SweepPush(outward, -1f, 0f);
            Assert.AreEqual(1f, right.magnitude, 1e-5f, "unit length");
            Assert.AreEqual(1f, left.magnitude, 1e-5f);
            Assert.AreEqual(0f, Vector3.Dot(right, outward), 1e-5f, "no mix: perpendicular to outward");
            Assert.AreEqual(0f, right.y, 1e-6f, "flat");
            Assert.AreEqual(-1f, Vector3.Dot(right, left), 1e-5f, "it flips with the sign");
            // turning toward positive bearings (to the right), a point straight ahead moves to the right (+X)
            Assert.Greater(Vector3.Dot(WeaponSystem.SweepPush(Vector3.forward, 1f, 0f), Vector3.right), 0.99f);
            Assert.Greater(Vector3.Dot(WeaponSystem.SweepPush(Vector3.forward, -1f, 0f), Vector3.left), 0.99f);

            Vector3 mixed = WeaponSystem.SweepPush(outward, -1f, 0.35f);
            Assert.AreEqual(1f, mixed.magnitude, 1e-5f);
            float outPart = Vector3.Dot(mixed, outward);
            Assert.AreEqual(0.35f / Mathf.Sqrt(1f + 0.35f * 0.35f), outPart, 1e-4f, "the outward part (cos 70.7°)");
            Assert.Greater(Vector3.Dot(mixed, left), outPart, "still mostly sideways");
        }

        [Test]
        public void SweepBearing_IsPositiveToTheRight()
        {
            Assert.AreEqual(90f, WeaponSystem.SweepBearing(Vector3.forward, Vector3.right), 1e-3f);
            Assert.AreEqual(-90f, WeaponSystem.SweepBearing(Vector3.forward, Vector3.left), 1e-3f);
            Assert.AreEqual(0f, WeaponSystem.SweepBearing(Vector3.forward, new Vector3(0f, 3f, 5f)), 1e-3f, "on the ground: height ignored");
            Assert.AreEqual(180f, Mathf.Abs(WeaponSystem.SweepBearing(Vector3.forward, Vector3.back)), 1e-3f);
            Assert.AreEqual(90f, WeaponSystem.SweepBearing(Vector3.right, Vector3.back), 1e-3f, "relative to the aim");
            // the bearing the chain draws at agrees: turning the aim by b about Y gives back b
            for (float b = -170f; b <= 170f; b += 34f)
                Assert.AreEqual(b, WeaponSystem.SweepBearing(Vector3.forward, Quaternion.Euler(0f, b, 0f) * Vector3.forward), 1e-3f);
        }
    }
}
