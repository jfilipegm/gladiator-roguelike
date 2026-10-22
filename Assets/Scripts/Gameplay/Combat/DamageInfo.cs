using UnityEngine;

namespace Gladiator.Gameplay
{
    [System.Flags]
    public enum DamageFlags
    {
        None = 0, Crit = 1, Melee = 2, Projectile = 4, Status = 8, Contact = 16, Area = 32,
        /// <summary>Damage from a Boon effect (spark, burst, trail): it never triggers on-hit effects itself (no proc loops).</summary>
        Proc = 64,
    }

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
        /// <summary>Which status this tick belongs to (None for ordinary hits): number colour, no hit flash.</summary>
        public StatusKind Status;

        public bool IsCrit => (Flags & DamageFlags.Crit) != 0;
    }
}
