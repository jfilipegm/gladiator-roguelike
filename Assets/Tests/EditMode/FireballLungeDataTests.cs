using Gladiator.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace Gladiator.Tests
{
    /// <summary>
    /// Notes 33 and 34 (the v0.28.0 playtest): the Ashen Censer's Fireball pattern is appended after Sweep (assets
    /// serialize the index), the new WeaponDef fields default to the trident's lunge and a homing fireball, and the
    /// Cast Net lies at least as long as it holds what it caught.
    /// </summary>
    public class FireballLungeDataTests
    {
        [Test]
        public void Fireball_IsAppendedAfterSweep_AtIndex11()
        {
            Assert.AreEqual(11, (int)WeaponPattern.Fireball, "assets serialize the index");
            Assert.AreEqual(10, (int)WeaponPattern.Sweep, "nothing before it moved");
            Assert.AreEqual(2, (int)WeaponPattern.MultiHit, "the trident keeps its pattern");
        }

        [Test]
        public void WeaponDef_NewFieldDefaults_AreTheLungeAndTheFireball()
        {
            var w = ScriptableObject.CreateInstance<WeaponDef>();
            Assert.AreEqual(0.45f, w.prongSpacing, 1e-6f, "the lanes 0.45 m apart");
            Assert.AreEqual(150f, w.homing, 1e-6f);
            Assert.AreEqual(0f, w.castHeight, 1e-6f, "0: the projectiles' flight height");
            Object.DestroyImmediate(w);
        }

        [Test]
        public void CastNet_TheNetOnTheSand_OutlastsNothingItHolds()
        {
            var t = ScriptableObject.CreateInstance<CastNetTrait>();
            Assert.GreaterOrEqual(t.holdTime, 2f, "at least Root's 2 s");
            Assert.Greater(t.throwRange, t.throwDistance, "it reaches for a crowd beyond where a blind throw lands");
            Assert.Greater(t.snareInterval, 0f);
            Object.DestroyImmediate(t);
        }
    }
}
