using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>Attack patterns that weapon data composes (CLAUDE.md §6.8). Slice implements MeleeArc first.</summary>
    public enum WeaponPattern { MeleeArc, Thrust, MultiHit, GroundSlam, Projectile, Returning, Orbit, Chain, Summon }

    /// <summary>A weapon as data. Levels/upgrades layer on top (M2.5).</summary>
    [CreateAssetMenu(menuName = "Gladiator/Weapon", fileName = "WP_")]
    public sealed class WeaponDef : ScriptableObject
    {
        /// <summary>Stable numeric id derived from `id` (events, stats, saves).</summary>
        public int RuntimeId => Gladiator.Core.GameInfo.StableId(id);
        public string id = "gladius";
        public string displayName = "Rusty Gladius";
        [TextArea] public string description = "Fast arcs at the nearest enemy.";
        public WeaponPattern pattern = WeaponPattern.MeleeArc;

        [Header("Damage")]
        public float damage = 12f;
        [Range(0f, 1f)] public float critChance = 0.12f;
        public float critMultiplier = 2f;
        public float knockback = 6f;

        [Header("Timing")]
        [Tooltip("Seconds between attacks (before attack-speed bonuses).")]
        public float cooldown = 0.8f;
        [Tooltip("Swing animation length (s).")]
        public float swingDuration = 0.2f;
        [Tooltip("Delay from swing start to the damage frame (s).")]
        public float windup = 0.06f;

        [Header("Shape")]
        [Tooltip("Reach from the player's centre (m).")]
        public float range = 2.6f;
        public float arcDegrees = 150f;
        [Tooltip("Auto-aim acquires the nearest enemy within this range; nothing in range = no swing.")]
        public float acquireRange = 3.2f;
    }
}
