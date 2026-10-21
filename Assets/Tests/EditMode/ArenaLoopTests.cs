using Gladiator.Gameplay;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Gladiator.Tests
{
    /// <summary>
    /// P1 (arena integrity), on the six layout assets (regenerate them first: "Gladiator/Arenas/Regenerate ... Layout"):
    /// each arena's player boundary is ONE closed, simple loop that holds the enemies' whole play area and everything
    /// the player has to reach (spawn, chests, altars, secrets), the old separate walls are gone, and the gates that
    /// used to open onto the outside (the Slave Pit's road, the Provincial Pit's town) have a pen or a tunnel behind.
    /// </summary>
    public class ArenaLoopTests
    {
        static readonly string[] Layouts =
        {
            "AL_SlavePit", "AL_ProvincialPit", "AL_StoneAmphitheater", "AL_GrandArena", "AL_ImperialArena", "AL_Colosseum",
        };

        static ArenaLayoutDef Load(string name)
        {
            var l = AssetDatabase.LoadAssetAtPath<ArenaLayoutDef>("Assets/Data/Arenas/" + name + ".asset");
            Assert.IsNotNull(l, name + " missing");
            return l;
        }

        static bool Inside(Vector2[] poly, Vector2 p)
        {
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
                if ((poly[i].y > p.y) != (poly[j].y > p.y) && p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                    inside = !inside;
            return inside;
        }

        static float EdgeDistance(Vector2[] poly, Vector2 p)
        {
            float best = float.MaxValue;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                Vector2 a = poly[j], ab = poly[i] - a;
                float s = ab.sqrMagnitude > 1e-8f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
                best = Mathf.Min(best, (p - (a + ab * s)).magnitude);
            }
            return best;
        }

        static float Cross(Vector2 o, Vector2 a, Vector2 b) => (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);

        static bool Crosses(Vector2 a, Vector2 b, Vector2 c, Vector2 d) =>
            Cross(a, b, c) * Cross(a, b, d) < 0f && Cross(c, d, a) * Cross(c, d, b) < 0f;

        /// <summary>The enemies' play area's edge: the outline, or points round the ellipse.</summary>
        static Vector2[] PlayEdge(ArenaLayoutDef l)
        {
            if (l.outline != null && l.outline.Length >= 3) return l.outline;
            var e = new Vector2[72];
            float aspect = l.aspect > 0f ? l.aspect : 1f;
            for (int k = 0; k < e.Length; k++)
            {
                float a = k * Mathf.PI * 2f / e.Length;
                e[k] = new Vector2(l.center.x + l.radius * Mathf.Cos(a), l.center.z + l.radius * aspect * Mathf.Sin(a));
            }
            return e;
        }

        [Test]
        public void EveryArena_HasOneClosedSimpleLoop_AroundEverythingThePlayerNeeds()
        {
            var report = new System.Text.StringBuilder();
            foreach (var name in Layouts)
            {
                var l = Load(name);
                var loop = l.boundaryLoop;
                Assert.IsNotNull(loop, name);
                Assert.GreaterOrEqual(loop.Length, 3, name + ": no boundary loop (regenerate the layout)");
                Assert.AreEqual(0, l.boundary.Length, name + ": the old separate walls are still there");
                int n = loop.Length, crossings = 0;
                float perimeter = 0f;
                for (int i = 0; i < n; i++)
                {
                    perimeter += (loop[(i + 1) % n] - loop[i]).magnitude;
                    Assert.Greater((loop[(i + 1) % n] - loop[i]).sqrMagnitude, 1e-6f, $"{name}: a repeated corner at {i}");
                    for (int j = i + 2; j < n; j++)
                    {
                        if (i == 0 && j == n - 1) continue; // neighbours through the closing edge
                        if (Crosses(loop[i], loop[(i + 1) % n], loop[j], loop[(j + 1) % n])) crossings++;
                    }
                }
                Assert.AreEqual(0, crossings, name + ": the loop crosses itself");
                foreach (var p in PlayEdge(l))
                    Assert.IsTrue(Inside(loop, p), $"{name}: the enemies' play area reaches out of the player's loop at {p}");
                void Reach(string what, Vector3 p)
                {
                    var xz = new Vector2(p.x, p.z);
                    Assert.IsTrue(Inside(loop, xz) && EdgeDistance(loop, xz) >= 0.4f, $"{name}: the {what} at {p} is out of the player's reach");
                }
                Reach("player spawn", l.playerSpawn);
                foreach (var p in l.chestAnchors) Reach("chest", p);
                foreach (var p in l.altarAnchors) Reach("altar", p);
                foreach (var p in l.secretSpots) Reach("secret spot", p);
                report.Append($" {name}: {n} corners, {perimeter:0} m;");
            }
            Debug.Log("[ArenaLoopTests]" + report);
        }

        [Test]
        public void GatesThatOpenedOntoTheOutside_HaveAPenOrATunnelBehind()
        {
            foreach (var (name, back) in new[] { ("AL_SlavePit", "SM_YardPen"), ("AL_ProvincialPit", "SM_ProvGateTunnel") })
            {
                var l = Load(name);
                Assert.Greater(l.gates.Length, 0, name);
                foreach (var g in l.gates)
                {
                    bool found = false;
                    foreach (var p in l.pieces)
                        if (p.model.EndsWith(back) && (p.position - g.position).sqrMagnitude < 0.01f && Mathf.Abs(Mathf.DeltaAngle(p.yaw, g.yaw)) < 0.5f)
                            found = true;
                    Assert.IsTrue(found, $"{name}: no {back} behind the gate at {g.position}");
                }
            }
            int convicts = 0;
            foreach (var p in Load("AL_SlavePit").pieces) if (p.model.EndsWith("EN_Convict") && p.role == PieceRole.Decor) convicts++;
            Assert.AreEqual(15, convicts, "five convicts waiting in each of the three pens");
        }

        [Test]
        public void TheSlavePitsPlatformsAndBermCrests_AreOutsideTheLoop()
        {
            var l = Load("AL_SlavePit");
            int decks = 0;
            foreach (var p in l.pieces)
            {
                bool platform = p.model.EndsWith("SM_YardPlatform") || p.model.EndsWith("SM_YardPlatformB");
                bool berm = p.model.EndsWith("SM_YardBerm");
                if (!platform && !berm) continue;
                // the piece's local +Z faces the yard: its deck (platform, 0.04 to 3 m behind its origin) or its crest
                // (berm, 1.2 to 2.2 m behind) is outward along -Z
                var back = Quaternion.Euler(0f, p.yaw, 0f) * Vector3.back;
                var right = Quaternion.Euler(0f, p.yaw, 0f) * Vector3.right;
                float near = platform ? 0.04f : 1.2f;
                foreach (float x in new[] { -2f, 0f, 2f })
                    foreach (float d in new[] { near, near + 0.5f, near + 1.5f })
                    {
                        var w = p.position + back * d + right * x;
                        Assert.IsFalse(Inside(l.boundaryLoop, new Vector2(w.x, w.z)) && EdgeDistance(l.boundaryLoop, new Vector2(w.x, w.z)) > 0.05f,
                            $"{(platform ? "a platform's deck" : "a berm's crest")} at {w} is inside the player's loop");
                    }
                decks++;
            }
            Assert.Greater(decks, 20, "platform and berm sections round the oval");
        }
    }
}
