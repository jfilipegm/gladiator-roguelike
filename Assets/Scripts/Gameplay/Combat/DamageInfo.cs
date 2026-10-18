using UnityEngine;

namespace Gladiator.Gameplay
{
    [System.Flags]
    public enum DamageFlags { None = 0, Crit = 1, Melee = 2, Projectile = 4, Status = 8, Contact = 16, Area = 32 }

    /// <summary>Everything a hit carries through the central damage pipeline (CLAUDE.md §4).</summary>
    public struct DamageInfo
    {
        public float Amount;
        public DamageFlags Flags;
        public Vector3 Point;
        /// <summary>Horizontal push direction (normalised) and strength (m/s added to knockback velocity).</summary>
        public Vector3 Direction;
        public float Knockback;
        /// <summary>Weapon/source id for upgrade hooks and stats (kills with weapon W).</summary>
        public int SourceId;

        public bool IsCrit => (Flags & DamageFlags.Crit) != 0;
    }

    /// <summary>Deterministic per-run RNG (xorshift32): same seed → same crits, same rolls. Allocation-free.</summary>
    public static class GameRandom
    {
        static uint _state = 0x9E3779B9u;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() => _state = 0x9E3779B9u;

        public static void Seed(int seed) => _state = seed == 0 ? 0x9E3779B9u : (uint)seed;

        public static uint NextUInt()
        {
            _state ^= _state << 13; _state ^= _state >> 17; _state ^= _state << 5;
            return _state;
        }

        /// <summary>[0, 1)</summary>
        public static float Value => (NextUInt() & 0xFFFFFF) / (float)0x1000000;
        public static float Range(float min, float max) => min + (max - min) * Value;
        public static int Range(int min, int maxExclusive) => min + (int)(NextUInt() % (uint)Mathf.Max(1, maxExclusive - min));
        public static bool Chance(float p) => Value < p;
    }
}
