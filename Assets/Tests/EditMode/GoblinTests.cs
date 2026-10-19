using Gladiator.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace Gladiator.Tests
{
    /// <summary>Phase 5.2 (docs/GLADIATORS.md): the Pit Goblin's rules that are pure maths.</summary>
    public class GoblinTests
    {
        [Test]
        public void Scavenger_LendsAtHigherLevels_AsTheRunGoes_UpToTheCap()
        {
            var t = ScriptableObject.CreateInstance<ScavengerTrait>();
            Assert.AreEqual(1, t.LevelFor(1));
            Assert.AreEqual(1, t.LevelFor(5));
            Assert.AreEqual(2, t.LevelFor(6));
            Assert.AreEqual(4, t.LevelFor(18));
            Assert.AreEqual(6, t.LevelFor(30), "the cap");
            Assert.AreEqual(6, t.LevelFor(99));
            Object.DestroyImmediate(t);
        }

        [Test]
        public void GladiatorDef_SelectLineAndBaseValues()
        {
            var g = ScriptableObject.CreateInstance<GladiatorDef>();
            var w = ScriptableObject.CreateInstance<WeaponDef>();
            w.displayName = "Bomb Satchel";
            g.startingWeapon = w;
            Assert.AreEqual("Starts with Bomb Satchel", g.SelectLine);
            g.selectLine = "Custom";
            Assert.AreEqual("Custom", g.SelectLine);
            g.baseStats = new[] { new GladiatorDef.BaseStat { stat = Gladiator.Core.StatId.MaxHp, value = 70f } };
            Assert.AreEqual(70f, g.BaseValue(Gladiator.Core.StatId.MaxHp, 100f), "overridden");
            Assert.AreEqual(1f, g.BaseValue(Gladiator.Core.StatId.MoveSpeed, 1f), "not listed: the fallback");
            Object.DestroyImmediate(w);
            Object.DestroyImmediate(g);
        }
    }
}
