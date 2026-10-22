using System.Globalization;
using Gladiator.Core;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// What a draw shapes (R8, docs/tracks/R7-design.md §3.3). Each stream keeps its own state, so drawing from one never
    /// moves another: a crit (Combat) never changes the next card offer (Cards). Append only: the numbers key the states
    /// and the forks, so reordering would change every seeded run and every daily.
    /// </summary>
    public enum RngStream : byte
    {
        /// <summary>The legacy API (GameRandom.Value, Range, Chance, NextUInt): cosmetic rolls that don't shape the run.</summary>
        Misc,
        Cards, Combat, Loot, Enemies, Spawns, Director, Swarm, Elite, Map, Gifts, Whims, Boss, Hazards, Traits,
    }

    /// <summary>
    /// A small xorshift32 generator held by value: a fork (GameRandom.Fork) or a stream's state. Allocation-free. Keep it in
    /// a field and draw in place (`_rng.Next01()`); a copy, or a property that returns one, draws from the copy and repeats.
    /// </summary>
    public struct Rng
    {
        public uint State;

        /// <summary>0 → GameRandom.DefaultSeed (xorshift never leaves 0).</summary>
        public Rng(uint state) => State = state != 0u ? state : GameRandom.DefaultSeed;

        public uint NextUInt()
        {
            if (State == 0u) State = GameRandom.DefaultSeed; // a field nobody seeded (default(Rng)): random, never stuck at 0
            State ^= State << 13; State ^= State >> 17; State ^= State << 5;
            return State;
        }

        /// <summary>[0, 1)</summary>
        public float Next01() => (NextUInt() & 0xFFFFFF) / (float)0x1000000;
        public float Range(float min, float max) => min + (max - min) * Next01();
        /// <summary>[min, maxExclusive); min when the range is empty.</summary>
        public int Range(int min, int maxExclusive) => min + (int)(NextUInt() % (uint)Mathf.Max(1, maxExclusive - min));
        public bool Chance(float p) => Next01() < p;
    }

    /// <summary>
    /// The run's randomness (R8, docs/tracks/R7-design.md §3.3): one run seed gives every RngStream its own xorshift32
    /// state (BeginRun); keyed reseeds and forks make events the same however the run went (the nth swarm, the nth card
    /// pick, a component's own rolls). The legacy API draws from Misc, and Seed(int) keeps its old meaning for tests: every
    /// stream starts at that seed, so a test that seeds 12345 and rolls cards sees the sequence it always saw.
    /// Allocation-free, except the seed text helpers (menus).
    /// </summary>
    public static class GameRandom
    {
        public const uint DefaultSeed = 0x9E3779B9u;
        /// <summary>The states kept (RngStream has room to grow: append below this).</summary>
        public const int StreamCount = 16;

        /// <summary>The seed of the current run (BeginRun or Seed).</summary>
        public static uint RunSeed { get; private set; } = DefaultSeed;

        // the streams' states, in place: Stream(s) hands out a ref, so a stream and a fork share one implementation
        static readonly Rng[] _streams = Fresh(DefaultSeed);

        static Rng[] Fresh(uint seed)
        {
            var a = new Rng[StreamCount];
            for (int s = 0; s < StreamCount; s++) a[s].State = Hash(seed, (uint)s, 0u);
            return a;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() => BeginRun(DefaultSeed);

        /// <summary>A new run with `seed`: every stream gets its own state, Hash(seed, stream, 0).</summary>
        public static void BeginRun(uint seed)
        {
            RunSeed = seed;
            for (int s = 0; s < StreamCount; s++) _streams[s].State = Hash(seed, (uint)s, 0u);
        }

        /// <summary>Legacy (tests): every stream's state = `seed` (0 → DefaultSeed), exactly the old single generator per stream.</summary>
        public static void Seed(int seed)
        {
            RunSeed = (uint)seed;
            uint state = seed == 0 ? DefaultSeed : (uint)seed;
            for (int s = 0; s < StreamCount; s++) _streams[s].State = state;
        }

        /// <summary>Restarts stream `s` from (run seed, s, key): the same event key gives the same rolls however the run went.</summary>
        public static void Reseed(RngStream s, uint key) => _streams[(int)s].State = Hash(RunSeed, (uint)s, key);

        /// <summary>A generator of its own for a component or an event, from (run seed, s, key). Never equal to a stream's state.</summary>
        public static Rng Fork(RngStream s, uint key) => new Rng(Hash(RunSeed, (uint)s | 0x100u, key));

        /// <summary>Stream `s` itself, by reference (`ref GameRandom.Stream(s)` for code that takes a `ref Rng`).</summary>
        public static ref Rng Stream(RngStream s) => ref _streams[(int)s];

        // ------------------------------------------------------------------ stream draws

        public static uint NextUInt(RngStream s) => _streams[(int)s].NextUInt();
        /// <summary>[0, 1)</summary>
        public static float Next01(RngStream s) => _streams[(int)s].Next01();
        public static float Range(RngStream s, float min, float max) => _streams[(int)s].Range(min, max);
        public static int Range(RngStream s, int min, int maxExclusive) => _streams[(int)s].Range(min, maxExclusive);
        public static bool Chance(RngStream s, float p) => _streams[(int)s].Chance(p);

        // ------------------------------------------------------------------ legacy draws (the Misc stream)

        public static uint NextUInt() => _streams[(int)RngStream.Misc].NextUInt();
        /// <summary>[0, 1)</summary>
        public static float Value => _streams[(int)RngStream.Misc].Next01();
        public static float Range(float min, float max) => _streams[(int)RngStream.Misc].Range(min, max);
        public static int Range(int min, int maxExclusive) => _streams[(int)RngStream.Misc].Range(min, maxExclusive);
        public static bool Chance(float p) => _streams[(int)RngStream.Misc].Chance(p);

        // ------------------------------------------------------------------ hashing and seed text

        /// <summary>
        /// murmur3_32 of the three words (little-endian, seed 0), never 0. It fixes every seeded run and every daily: change
        /// it and they all change (RandomStreamTests pins two values).
        /// </summary>
        public static uint Hash(uint a, uint b, uint c)
        {
            unchecked
            {
                uint h = Mix(Mix(Mix(0u, a), b), c);
                h ^= 12u; // the length in bytes
                h ^= h >> 16; h *= 0x85EBCA6Bu; h ^= h >> 13; h *= 0xC2B2AE35u; h ^= h >> 16;
                return h != 0u ? h : DefaultSeed;
            }
        }

        static uint Mix(uint h, uint k)
        {
            unchecked
            {
                k *= 0xCC9E2D51u; k = (k << 15) | (k >> 17); k *= 0x1B873593u;
                h ^= k; h = (h << 13) | (h >> 19);
                return h * 5u + 0xE6546B64u;
            }
        }

        /// <summary>A fork or reseed key for a name (the content ids' FNV-1a). Hash it once (Awake/Start), not per frame.</summary>
        public static uint KeyOf(string name) => (uint)GameInfo.StableId(name);

        /// <summary>"7F3A-91C2" (menus, the end screen, logs).</summary>
        public static string SeedText(uint seed)
        {
            string h = seed.ToString("X8", CultureInfo.InvariantCulture);
            return h.Substring(0, 4) + "-" + h.Substring(4, 4);
        }

        /// <summary>
        /// Reads a seed as SeedText writes it, or as typed: 1–8 hex digits, any case, an optional 0x, dashes or spaces between
        /// ("7F3A-91C2", "7f3a91c2", "0x7F3A91C2"). False for anything else.
        /// </summary>
        public static bool TryParseSeed(string text, out uint seed)
        {
            seed = 0u;
            if (text == null) return false;
            int i = 0, n = text.Length;
            while (i < n && char.IsWhiteSpace(text[i])) i++;
            while (n > i && char.IsWhiteSpace(text[n - 1])) n--;
            if (n - i >= 2 && text[i] == '0' && (text[i + 1] == 'x' || text[i + 1] == 'X')) i += 2;
            uint v = 0u;
            int digits = 0;
            for (; i < n; i++)
            {
                char c = text[i];
                uint d;
                if (c >= '0' && c <= '9') d = (uint)(c - '0');
                else if (c >= 'a' && c <= 'f') d = (uint)(c - 'a' + 10);
                else if (c >= 'A' && c <= 'F') d = (uint)(c - 'A' + 10);
                else if (c == '-' || c == ' ') continue;
                else return false;
                if (++digits > 8) return false;
                v = (v << 4) | d;
            }
            if (digits == 0) return false;
            seed = v;
            return true;
        }
    }
}
