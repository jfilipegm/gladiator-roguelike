using Gladiator.Core;
using Gladiator.Gameplay;
using NUnit.Framework;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace Gladiator.Tests
{
    /// <summary>
    /// R8 (docs/tracks/R7-design.md §3.3, docs/tracks/R7-contract.md "R8-A streams"): GameRandom's streams and forks.
    /// Seed(int) keeps the old single generator per stream (the seeded tests keep their exact rolls); BeginRun gives every
    /// stream its own state; drawing from one stream never moves another; keyed reseeds and forks repeat for the same run
    /// seed and key; Hash is murmur3 (pinned: it fixes every seeded run and every daily); the seed text round-trips; none
    /// of it allocates.
    /// </summary>
    public class RandomStreamTests
    {
        [TearDown]
        public void TearDown() => GameRandom.BeginRun(GameRandom.DefaultSeed); // what the next test would get at launch

        /// <summary>The old GameRandom, written out: xorshift32 13/17/5, 24 bits to [0, 1).</summary>
        static float HandRolled(ref uint s)
        {
            s ^= s << 13; s ^= s >> 17; s ^= s << 5;
            return (s & 0xFFFFFF) / (float)0x1000000;
        }

        static int Bits(uint x)
        {
            int n = 0;
            for (; x != 0u; x &= x - 1u) n++;
            return n;
        }

        // ---------- Seed(int): the legacy meaning ----------

        [Test]
        public void Seed_EveryStreamReplaysTheOldGenerator()
        {
            GameRandom.Seed(12345);
            uint s = 12345u;
            for (int i = 0; i < 5; i++) Assert.AreEqual(HandRolled(ref s), GameRandom.Next01(RngStream.Cards), "Cards draw " + i);
            s = 12345u; // Misc started from the seed too, and the Cards draws didn't move it
            for (int i = 0; i < 5; i++) Assert.AreEqual(HandRolled(ref s), GameRandom.Value, "Value draw " + i);

            GameRandom.Seed(12345);
            Assert.AreEqual(0.8963085f, GameRandom.Value, 1e-6f, "pinned: the first roll after Seed(12345)");
            Assert.AreEqual(12345u, GameRandom.RunSeed);
        }

        [Test]
        public void SeedZero_IsTheDefaultSeed()
        {
            GameRandom.Seed(0);
            uint s = GameRandom.DefaultSeed;
            for (int i = 0; i < 3; i++) Assert.AreEqual(HandRolled(ref s), GameRandom.Next01(RngStream.Combat));
            s = GameRandom.DefaultSeed;
            for (int i = 0; i < 3; i++) Assert.AreEqual(HandRolled(ref s), GameRandom.Value);
        }

        // ---------- BeginRun: a state per stream ----------

        [Test]
        public void BeginRun_EveryStreamStartsDifferently()
        {
            GameRandom.BeginRun(0x7F3A91C2u);
            Assert.AreEqual(0x7F3A91C2u, GameRandom.RunSeed);
            var first = new uint[GameRandom.StreamCount];
            for (int s = 0; s < first.Length; s++) first[s] = GameRandom.NextUInt((RngStream)s);
            for (int a = 0; a < first.Length; a++)
                for (int b = a + 1; b < first.Length; b++)
                    Assert.AreNotEqual(first[a], first[b], "streams " + (RngStream)a + " and " + (RngStream)b);
        }

        [Test]
        public void BeginRun_SameSeed_SameHundredDrawsPerStream()
        {
            const uint seed = 0xC0FFEEu;
            var a = new uint[GameRandom.StreamCount, 100];
            GameRandom.BeginRun(seed);
            for (int s = 0; s < GameRandom.StreamCount; s++)
                for (int i = 0; i < 100; i++) a[s, i] = GameRandom.NextUInt((RngStream)s);
            GameRandom.BeginRun(seed);
            for (int s = GameRandom.StreamCount - 1; s >= 0; s--) // another order: the streams don't share anything
                for (int i = 0; i < 100; i++) Assert.AreEqual(a[s, i], GameRandom.NextUInt((RngStream)s), (RngStream)s + " draw " + i);

            GameRandom.BeginRun(seed + 1u);
            bool differs = false;
            for (int s = 0; s < GameRandom.StreamCount; s++) differs |= GameRandom.NextUInt((RngStream)s) != a[s, 0];
            Assert.IsTrue(differs, "another seed, other rolls");
        }

        [Test]
        public void CombatDraws_NeverMoveTheCards()
        {
            GameRandom.BeginRun(777u);
            uint expected = GameRandom.NextUInt(RngStream.Cards);
            GameRandom.BeginRun(777u);
            for (int i = 0; i < 1000; i++) GameRandom.Chance(RngStream.Combat, 0.3f);
            for (int i = 0; i < 50; i++) { _ = GameRandom.Value; GameRandom.Range(RngStream.Enemies, 0f, 1f); }
            Assert.AreEqual(expected, GameRandom.NextUInt(RngStream.Cards), "a crit never changes the next card");
        }

        [Test]
        public void TheLegacyApi_DrawsFromMisc()
        {
            GameRandom.BeginRun(31u);
            uint misc = GameRandom.NextUInt(RngStream.Misc);
            GameRandom.BeginRun(31u);
            Assert.AreEqual(misc, GameRandom.NextUInt());
            GameRandom.BeginRun(31u);
            Assert.AreEqual((misc & 0xFFFFFF) / (float)0x1000000, GameRandom.Value);
        }

        // ---------- keyed reseeds and forks ----------

        [Test]
        public void Reseed_SameKey_SameRolls_OtherKey_Other()
        {
            GameRandom.BeginRun(4242u);
            GameRandom.Reseed(RngStream.Cards, 7u);
            uint a0 = GameRandom.NextUInt(RngStream.Cards), a1 = GameRandom.NextUInt(RngStream.Cards);
            for (int i = 0; i < 30; i++) GameRandom.NextUInt(RngStream.Cards); // whatever happened in between
            GameRandom.Reseed(RngStream.Cards, 7u);
            Assert.AreEqual(a0, GameRandom.NextUInt(RngStream.Cards));
            Assert.AreEqual(a1, GameRandom.NextUInt(RngStream.Cards));
            GameRandom.Reseed(RngStream.Cards, 8u);
            Assert.AreNotEqual(a0, GameRandom.NextUInt(RngStream.Cards), "another key, another sequence");

            // a level-up's key: (source << 16) | nth; a chest's first pick isn't the first level-up's
            GameRandom.Reseed(RngStream.Cards, ((uint)PickSource.LevelUp << 16) | 0u);
            uint levelUp = GameRandom.NextUInt(RngStream.Cards);
            GameRandom.Reseed(RngStream.Cards, ((uint)PickSource.Chest << 16) | 0u);
            Assert.AreNotEqual(levelUp, GameRandom.NextUInt(RngStream.Cards));
        }

        [Test]
        public void Reseed_DependsOnTheRunSeed()
        {
            GameRandom.BeginRun(1u);
            GameRandom.Reseed(RngStream.Swarm, 3u);
            uint a = GameRandom.NextUInt(RngStream.Swarm);
            GameRandom.BeginRun(2u);
            GameRandom.Reseed(RngStream.Swarm, 3u);
            Assert.AreNotEqual(a, GameRandom.NextUInt(RngStream.Swarm));
        }

        [Test]
        public void Fork_ByKey_RepeatsAfterTheSameBeginRun()
        {
            GameRandom.BeginRun(99u);
            var a = GameRandom.Fork(RngStream.Map, GameRandom.KeyOf("a"));
            var b = GameRandom.Fork(RngStream.Map, GameRandom.KeyOf("b"));
            Assert.AreNotEqual(a.State, b.State);
            uint a0 = a.NextUInt(), a1 = a.NextUInt();
            Assert.AreNotEqual(a0, b.NextUInt());

            for (int i = 0; i < 100; i++) GameRandom.NextUInt(RngStream.Map); // the stream moving doesn't matter to a fork
            GameRandom.BeginRun(99u);
            var again = GameRandom.Fork(RngStream.Map, GameRandom.KeyOf("a"));
            Assert.AreEqual(a0, again.NextUInt());
            Assert.AreEqual(a1, again.NextUInt());

            GameRandom.BeginRun(100u);
            Assert.AreNotEqual(a0, GameRandom.Fork(RngStream.Map, GameRandom.KeyOf("a")).NextUInt(), "another run seed, another fork");
        }

        [Test]
        public void Fork_NeverEqualsAStream_AndMovesNone()
        {
            GameRandom.BeginRun(5u);
            var states = new uint[GameRandom.StreamCount];
            for (int s = 0; s < states.Length; s++) states[s] = GameRandom.Stream((RngStream)s).State;
            for (int s = 0; s < states.Length; s++)
            {
                var f = GameRandom.Fork((RngStream)s, 0u);
                Assert.AreNotEqual(states[s], f.State, "the fork of " + (RngStream)s + " with key 0");
                f.NextUInt();
            }
            for (int s = 0; s < states.Length; s++) Assert.AreEqual(states[s], GameRandom.Stream((RngStream)s).State, "forking didn't move " + (RngStream)s);
        }

        [Test]
        public void Stream_IsARef_ADrawThroughItMovesTheStream()
        {
            GameRandom.BeginRun(12u);
            GameRandom.NextUInt(RngStream.Spawns);
            uint second = GameRandom.NextUInt(RngStream.Spawns);
            GameRandom.BeginRun(12u);
            ref Rng spawns = ref GameRandom.Stream(RngStream.Spawns);
            spawns.NextUInt();
            Assert.AreEqual(second, GameRandom.NextUInt(RngStream.Spawns));
        }

        [Test]
        public void Rng_ZeroState_StillRolls()
        {
            Assert.AreEqual(GameRandom.DefaultSeed, new Rng(0u).State, "the constructor maps 0 to the default seed");
            var r = default(Rng); // a field nobody seeded
            uint a = r.NextUInt(), b = r.NextUInt();
            Assert.AreNotEqual(0u, a);
            Assert.AreNotEqual(a, b);
            var d = new Rng(GameRandom.DefaultSeed);
            Assert.AreEqual(d.NextUInt(), a, "a zero state behaves as the default seed");
        }

        [Test]
        public void KeyOf_IsTheStableId()
        {
            Assert.AreEqual((uint)GameInfo.StableId("golden_rat"), GameRandom.KeyOf("golden_rat"));
            Assert.AreEqual(0x4F4587A4u, GameRandom.KeyOf("golden_rat"), "pinned FNV-1a");
            Assert.AreNotEqual(GameRandom.KeyOf("hypogeum"), GameRandom.KeyOf("beast_cages"));
        }

        // ---------- Hash ----------

        [Test]
        public void Hash_IsMurmur3_Pinned()
        {
            // murmur3_32 of the three words (little-endian, seed 0): change it and every seeded run and every daily changes
            Assert.AreEqual(0xD941144Bu, GameRandom.Hash(0u, 0u, 0u));
            Assert.AreEqual(0xE9D78AD6u, GameRandom.Hash(1u, 2u, 3u));
            Assert.AreEqual(0x88B5890Bu, GameRandom.Hash(0x7F3A91C2u, 1u, 0u));
            GameRandom.BeginRun(0x7F3A91C2u);
            Assert.AreEqual(0x88B5890Bu, GameRandom.Stream(RngStream.Cards).State, "BeginRun: state[s] = Hash(seed, s, 0)");
        }

        [Test]
        public void Hash_NeverZero_OverTenThousandInputs()
        {
            int n = 0;
            for (uint a = 0; a < 22u; a++)
                for (uint b = 0; b < 22u; b++)
                    for (uint c = 0; c < 22u; c++, n++)
                        Assert.AreNotEqual(0u, GameRandom.Hash(a, b, c));
            var r = new Rng(77u);
            for (int i = 0; i < 1000; i++, n++)
                Assert.AreNotEqual(0u, GameRandom.Hash(r.NextUInt(), i % 3 == 0 ? 0u : r.NextUInt(), i % 2 == 0 ? 0u : uint.MaxValue));
            Assert.GreaterOrEqual(n, 10000);
        }

        [Test]
        public void Hash_OneBitChange_FlipsAboutHalfTheBits()
        {
            var r = new Rng(2024u);
            long flips = 0, n = 0;
            var perBit = new int[32];
            var w = new uint[3];
            for (int i = 0; i < 1000; i++)
            {
                uint a = r.NextUInt(), b = r.NextUInt(), c = r.NextUInt();
                if (i % 10 == 0) a = 0u; // zeros too
                if (i % 7 == 0) b = 0u;
                if (i % 5 == 0) c = 0u;
                uint h = GameRandom.Hash(a, b, c);
                for (int word = 0; word < 3; word++)
                    for (int bit = 0; bit < 32; bit++)
                    {
                        w[0] = a; w[1] = b; w[2] = c;
                        w[word] ^= 1u << bit;
                        uint d = h ^ GameRandom.Hash(w[0], w[1], w[2]);
                        flips += Bits(d);
                        n++;
                        for (int k = 0; k < 32; k++) if (((d >> k) & 1u) != 0u) perBit[k]++;
                    }
            }
            double mean = flips / (double)n;
            Assert.That(mean, NUnit.Framework.Is.InRange(15.0, 17.0), "mean bits flipped by a one-bit change");
            for (int k = 0; k < 32; k++)
                Assert.That(perBit[k] / (double)n, NUnit.Framework.Is.InRange(0.45, 0.55), "output bit " + k);
        }

        // ---------- seed text ----------

        [Test]
        public void SeedText_RoundTrips()
        {
            Assert.AreEqual("7F3A-91C2", GameRandom.SeedText(0x7F3A91C2u));
            Assert.AreEqual("0000-0001", GameRandom.SeedText(1u));
            Assert.AreEqual("FFFF-FFFF", GameRandom.SeedText(uint.MaxValue));
            foreach (var text in new[] { "7F3A-91C2", "7f3a91c2", "0x7F3A91C2", " 7F3A 91C2 ", "0X7f3a-91c2" })
            {
                Assert.IsTrue(GameRandom.TryParseSeed(text, out uint seed), text);
                Assert.AreEqual(0x7F3A91C2u, seed, text);
            }
            var r = new Rng(3u);
            for (int i = 0; i < 200; i++)
            {
                uint s = r.NextUInt();
                Assert.IsTrue(GameRandom.TryParseSeed(GameRandom.SeedText(s), out uint back));
                Assert.AreEqual(s, back);
            }
            Assert.IsTrue(GameRandom.TryParseSeed("1", out uint one));
            Assert.AreEqual(1u, one);
        }

        [Test]
        public void TryParseSeed_RejectsWhatIsNotASeed()
        {
            foreach (var text in new[] { null, "", "   ", "0x", "-", "7F3A-91C2-1", "123456789", "7G3A91C2", "seed", "+12" })
            {
                Assert.IsFalse(GameRandom.TryParseSeed(text, out uint seed), "'" + text + "'");
                Assert.AreEqual(0u, seed);
            }
        }

        // ---------- ranges and allocation ----------

        [Test]
        public void RangeInt_NeverReturnsItsMax()
        {
            GameRandom.BeginRun(8u);
            var r = new Rng(9u);
            int[,] ranges = { { 0, 1 }, { 0, 2 }, { 3, 7 }, { -5, 5 }, { 10, 11 } };
            for (int k = 0; k < ranges.GetLength(0); k++)
            {
                int min = ranges[k, 0], max = ranges[k, 1];
                bool sawMin = false, sawTop = false;
                for (int i = 0; i < 10000; i++)
                {
                    int a = GameRandom.Range(RngStream.Cards, min, max), b = r.Range(min, max), c = GameRandom.Range(min, max);
                    Assert.That(a, NUnit.Framework.Is.InRange(min, max - 1));
                    Assert.That(b, NUnit.Framework.Is.InRange(min, max - 1));
                    Assert.That(c, NUnit.Framework.Is.InRange(min, max - 1));
                    sawMin |= a == min;
                    sawTop |= a == max - 1;
                }
                Assert.IsTrue(sawMin && sawTop, "the whole range comes up: " + min + ".." + max);
            }
            Assert.AreEqual(4, r.Range(4, 4), "an empty range gives its min");
        }

        [Test]
        public void RangeFloat_AndNext01_StayInRange()
        {
            GameRandom.BeginRun(10u);
            for (int i = 0; i < 10000; i++)
            {
                float u = GameRandom.Next01(RngStream.Loot);
                Assert.That(u, NUnit.Framework.Is.GreaterThanOrEqualTo(0f).And.LessThan(1f));
                float f = GameRandom.Range(RngStream.Boss, -2f, 3f);
                Assert.That(f, NUnit.Framework.Is.GreaterThanOrEqualTo(-2f).And.LessThan(3f));
            }
            Assert.IsFalse(GameRandom.Chance(RngStream.Combat, 0f));
            Assert.IsTrue(GameRandom.Chance(RngStream.Combat, 1f));
        }

        [Test]
        public void Draws_Forks_AndReseeds_DontAllocate()
        {
            uint sink = 0u;
            float fsink = 0f;
            TestDelegate run = () =>
            {
                GameRandom.BeginRun(0xBEEFu);
                var f = GameRandom.Fork(RngStream.Map, GameRandom.KeyOf("golden_rat"));
                for (int i = 0; i < 10000; i++)
                {
                    var s = (RngStream)(i % GameRandom.StreamCount);
                    sink += GameRandom.NextUInt(s);
                    fsink += GameRandom.Next01(s) + GameRandom.Range(s, 0f, 1f) + GameRandom.Range(s, 0, 9);
                    if (GameRandom.Chance(s, 0.5f)) sink++;
                    fsink += GameRandom.Value + GameRandom.Range(0f, 1f) + GameRandom.Range(0, 9);
                    if (GameRandom.Chance(0.5f)) sink++;
                    sink += GameRandom.NextUInt();
                    fsink += f.Next01() + f.Range(0f, 2f) + f.Range(0, 5);
                    if ((i & 63) == 0) GameRandom.Reseed(RngStream.Cards, (uint)i);
                    sink ^= GameRandom.Hash((uint)i, sink, 3u);
                }
            };
            run(); // warm up (JIT)
            Assert.That(run, Is.Not.AllocatingGCMemory());
            Assert.AreNotEqual(0f, fsink); // keep the draws alive
        }
    }
}
