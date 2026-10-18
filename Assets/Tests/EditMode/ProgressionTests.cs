using Gladiator.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace Gladiator.Tests
{
    public class ProgressionTests
    {
        [Test]
        public void XpCurve_StartsAtBase_AndIsStrictlyIncreasing()
        {
            var c = ScriptableObject.CreateInstance<ProgressionConfig>();
            Assert.AreEqual(c.baseXp, c.XpForLevel(1));
            float prev = 0f;
            for (int l = 1; l <= 60; l++)
            {
                float x = c.XpForLevel(l);
                Assert.Greater(x, prev, "level " + l);
                prev = x;
            }
            Object.DestroyImmediate(c);
        }
    }
}
