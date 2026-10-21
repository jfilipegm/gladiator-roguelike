using System.Collections.Generic;
using Gladiator.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace Gladiator.Tests
{
    /// <summary>
    /// P1 (arena integrity): the player's last-resort boundary (ArenaBounds) on a square, both windings, and a concave
    /// L: inside tests (the fast circle and the crossing number agree), a clamp puts anything outside back inside at the
    /// inset with the inward direction, even past a reflex corner, and counts each leak. NoPerch slides a player off a
    /// prop's top, toward the arena's centre when "away from its middle" would leave the arena.
    /// </summary>
    public class ArenaBoundsTests
    {
        readonly List<GameObject> _made = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _made) Object.DestroyImmediate(go);
            _made.Clear();
        }

        ArenaBounds Make(Vector2 center, params Vector2[] poly)
        {
            var go = new GameObject("Bounds");
            _made.Add(go);
            var b = go.AddComponent<ArenaBounds>();
            b.polygon = poly;
            b.center = center;
            b.Prepare();
            return b;
        }

        static readonly Vector2[] SquareCcw = { new Vector2(-10f, -10f), new Vector2(10f, -10f), new Vector2(10f, 10f), new Vector2(-10f, 10f) };
        // an L: the square less its north-east quarter (a reflex corner at the origin)
        static readonly Vector2[] L =
        {
            new Vector2(-10f, -10f), new Vector2(10f, -10f), new Vector2(10f, 0f), new Vector2(0f, 0f), new Vector2(0f, 10f), new Vector2(-10f, 10f),
        };

        static Vector2[] Reversed(Vector2[] p) { var r = (Vector2[])p.Clone(); System.Array.Reverse(r); return r; }

        static bool CrossingNumber(Vector2[] poly, float x, float z)
        {
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
                if ((poly[i].y > z) != (poly[j].y > z) && x < (poly[j].x - poly[i].x) * (z - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                    inside = !inside;
            return inside;
        }

        [Test]
        public void Contains_SquareBothWindings_AndAConcaveL()
        {
            foreach (var poly in new[] { SquareCcw, Reversed(SquareCcw) })
            {
                var b = Make(Vector2.zero, poly);
                Assert.IsTrue(b.Contains(0f, 0f));
                Assert.IsTrue(b.Contains(9.9f, -9.9f));
                Assert.IsFalse(b.Contains(10.1f, 0f));
                Assert.IsFalse(b.Contains(0f, -30f));
            }
            var l = Make(new Vector2(-5f, -5f), L);
            Assert.IsTrue(l.Contains(-5f, 5f), "the north-west arm");
            Assert.IsTrue(l.Contains(5f, -5f), "the south-east arm");
            Assert.IsFalse(l.Contains(5f, 5f), "the missing quarter");
        }

        [Test]
        public void FastCircle_AgreesWithTheCrossingNumber()
        {
            var rng = new System.Random(5);
            foreach (var (poly, c) in new[] { (SquareCcw, Vector2.zero), (L, new Vector2(-5f, -5f)), (Reversed(L), new Vector2(-4f, -6f)) })
            {
                var b = Make(c, poly);
                for (int k = 0; k < 2000; k++)
                {
                    float x = (float)(rng.NextDouble() * 30 - 15), z = (float)(rng.NextDouble() * 30 - 15);
                    Assert.AreEqual(CrossingNumber(poly, x, z), b.Contains(x, z), $"({x}, {z})");
                }
            }
        }

        [Test]
        public void Clamp_PutsAnOutsidePointBackInside_AtTheInset_AndCountsTheLeak()
        {
            var b = Make(Vector2.zero, SquareCcw);
            var p = new Vector3(12f, 3f, 4f);
            Assert.IsTrue(b.Clamp(ref p, 0.4f, out var inward));
            Assert.AreEqual(9.6f, p.x, 1e-4f);
            Assert.AreEqual(4f, p.z, 1e-4f);
            Assert.AreEqual(3f, p.y, 1e-6f, "height untouched");
            Assert.AreEqual(-1f, inward.x, 1e-4f);
            Assert.AreEqual(0f, inward.y, 1e-4f);
            Assert.AreEqual(1, b.Clamps);

            var q = new Vector3(2f, 0f, 2f);
            Assert.IsFalse(b.Clamp(ref q, 0.4f, out _), "inside: untouched");
            Assert.AreEqual(new Vector3(2f, 0f, 2f), q);
            Assert.AreEqual(1, b.Clamps);
        }

        [Test]
        public void Clamp_AnywhereOutside_LandsInside_EvenPastAReflexCorner()
        {
            var rng = new System.Random(9);
            foreach (var poly in new[] { L, Reversed(L) })
            {
                var b = Make(new Vector2(-5f, -5f), poly);
                // just past the reflex corner, in the missing quarter
                var p = new Vector3(0.2f, 0f, 0.3f);
                Assert.IsTrue(b.Clamp(ref p, 0.4f, out _));
                Assert.IsTrue(b.Contains(p.x, p.z), $"back inside from the notch: {p}");
                for (int k = 0; k < 500; k++)
                {
                    var r = new Vector3((float)(rng.NextDouble() * 60 - 30), 0f, (float)(rng.NextDouble() * 60 - 30));
                    bool wasOut = !b.Contains(r.x, r.z);
                    Assert.AreEqual(wasOut, b.Clamp(ref r, 0.4f, out _));
                    Assert.IsTrue(b.Contains(r.x, r.z), $"inside after the clamp: {r}");
                    if (wasOut) Assert.GreaterOrEqual(b.DistanceToEdge(r.x, r.z), 0.35f, "at about the inset from the edge");
                }
            }
        }

        [Test]
        public void NoPerch_SlidesAwayFromTheTopsMiddle_ButNeverOutOfTheArena()
        {
            var b = Make(Vector2.zero, SquareCcw);
            var prop = new GameObject("Prop");
            _made.Add(prop);
            var box = prop.AddComponent<BoxCollider>();
            box.size = new Vector3(2f, 2f, 2f);
            box.center = new Vector3(0f, 1f, 0f);
            prop.AddComponent<NoPerch>();
            prop.transform.position = new Vector3(-3f, 0f, 0f);
            Physics.SyncTransforms();
            var east = NoPerch.SlideDirection(new Vector3(-2.5f, 2f, 0f), box, b);
            Assert.Greater(east.x, 0.9f, "on its east half: off the east side");
            var west = NoPerch.SlideDirection(new Vector3(-3.5f, 2f, 0f), box, b);
            Assert.Less(west.x, -0.9f, "on its west half: off the west side");

            // the same prop against the west wall: off its west side would leave the arena, so toward the centre
            prop.transform.position = new Vector3(-9.5f, 0f, 0f);
            Physics.SyncTransforms();
            var walled = NoPerch.SlideDirection(new Vector3(-9.8f, 2f, 0.1f), box, b);
            Assert.Greater(walled.x, 0.9f, "toward the centre, not into the wall");
        }
    }
}
