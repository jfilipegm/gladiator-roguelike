using System;
using Gladiator.Core;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>Attack patterns that weapon data composes (CLAUDE.md §6.8). Implemented: MeleeArc, Thrust, Projectile, Returning, Orbit, Lob.
    /// Append new patterns at the end (assets serialize the index).</summary>
    public enum WeaponPattern { MeleeArc, Thrust, MultiHit, GroundSlam, Projectile, Returning, Orbit, Chain, Summon, Lob }

    /// <summary>
    /// A weapon as data. Its numbers are the level-1 base; each level 2-8 adds `levels[level-2].mods` to that weapon only
    /// (scaled by the card's rarity, like Boons), and the player's stats multiply everything on top.
    /// Implemented patterns: MeleeArc (Gladius), Thrust (Spear), Projectile (Javelins), Returning (Chakram), Orbit (Gorm's
    /// Flail: range = orbit radius, width = ball radius, speed = m/s along the circle, cooldown = re-hit interval per enemy),
    /// Lob (Bomb Satchel: acquireRange = throw range, width = blast radius, flightTime/fuse shortened by Projectile Speed),
    /// GroundSlam (the Cleaver), Chain (the Storm Spear), MultiHit (the Trident: `prongs` thrust lines fanned across
    /// arcDegrees, `prongInterval` s apart), Summon (the Wolf Pack: companions driven by CompanionPack).
    /// </summary>
    [CreateAssetMenu(menuName = "Gladiator/Weapon", fileName = "WP_")]
    public sealed class WeaponDef : ScriptableObject
    {
        [Serializable]
        public struct Level
        {
            [Tooltip("Weapon-local modifiers: Damage/AttackSpeed/Area/ProjectileSpeed/Knockback (Percent), Amount/Pierce (Flat), CritChance/CritDamage (Flat).")]
            public StatMod[] mods;
        }

        /// <summary>Stable numeric id derived from `id` (events, stats, saves).</summary>
        public int RuntimeId => GameInfo.StableId(id);
        public string id = "gladius";
        public string displayName = "Rusty Gladius";
        [TextArea] public string description = "Fast arcs at the nearest enemy.";
        public WeaponPattern pattern = WeaponPattern.MeleeArc;

        [Header("Damage")]
        public float damage = 12f;
        [Range(0f, 1f)] public float critChance = 0.12f;
        public float critMultiplier = 2f;
        public float knockback = 6f;

        [Header("On hit (MeleeArc / Thrust)")]
        [Tooltip("Status every hit applies (the Crimson Greatsword's Bleed). Empty = none.")]
        public StatusDef onHitStatus;
        [Tooltip("Damage per second per stack, as a fraction of the hit's damage (0 for pure debuffs).")]
        public float onHitStatusPower = 0.1f;
        [Min(1)] public int onHitStacks = 1;

        [Header("Timing")]
        [Tooltip("Seconds between attacks (before attack-speed bonuses).")]
        public float cooldown = 0.8f;
        [Tooltip("Swing / thrust animation length (s).")]
        public float swingDuration = 0.2f;
        [Tooltip("Delay from swing start to the damage frame (s).")]
        public float windup = 0.06f;

        [Header("Shape")]
        [Tooltip("Arc: reach from the player's centre. Thrust: line length. Projectile: max travel. Returning: throw distance (m).")]
        public float range = 2.6f;
        public float arcDegrees = 150f;
        [Tooltip("Thrust: line width. Projectiles: hit radius (m).")]
        public float width = 0.5f;
        [Tooltip("Auto-aim acquires the nearest enemy within this range; nothing in range = no attack.")]
        public float acquireRange = 3.2f;

        [Header("Amount / projectiles")]
        [Tooltip("Arc/Thrust: attacks per trigger (extra ones follow at the next targets). Projectiles: per volley.")]
        public int amount = 1;
        [Tooltip("Projectile speed (m/s).")]
        public float speed = 16f;
        [Tooltip("Extra enemies a projectile passes through after the first (-1 = unlimited).")]
        public int pierce = 0;
        [Tooltip("Mesh drawn for the spear / projectile (instanced, palette material). Gladius: none (it's in the hand).")]
        public Mesh mesh;
        public float meshScale = 1f;

        [Header("Lob (bombs)")]
        [Tooltip("Lob: seconds in the air (divided by Projectile Speed).")]
        public float flightTime = 0.6f;
        [Tooltip("Lob: fuse after landing, s (divided by Projectile Speed).")]
        public float fuse = 0.4f;
        [Tooltip("Lob: height of the arc's apex (m).")]
        public float arcHeight = 2.2f;

        [Header("Echo (evolutions, Phase 5.5)")]
        [Tooltip("MeleeArc / Thrust / Returning: the attack strikes again this long after its impact (s); 0 = no echo.")]
        public float echoDelay = 0f;
        [Tooltip("The echo's direction relative to the first strike (deg): 180 = behind you, 0 = the same way.")]
        public float echoAngle = 180f;
        [Tooltip("The echo's size relative to the first strike.")]
        public float echoScale = 1f;

        [Header("Targeting and sweet spots (Phase 5.6)")]
        [Tooltip("Projectile: aim at the farthest enemies within acquireRange instead of the nearest (the Sling).")]
        public bool targetFarthest;
        [Tooltip("Thrust: hits in the last tipLength m of the line always crit (the Whip's crack); 0 = off.")]
        public float tipLength;
        [Tooltip("MeleeArc / Thrust with Amount > 1: each extra strike goes for the next-nearest enemy instead of the nearest again.")]
        public bool spreadStrikes;

        [Header("MultiHit (the Trident, Phase 5.14)")]
        [Tooltip("MultiHit: jabs per flurry, fanned evenly across arcDegrees (each a Thrust line: range long, width wide).")]
        [Min(1)] public int prongs = 3;
        [Tooltip("MultiHit: seconds between two jabs of one flurry.")]
        public float prongInterval = 0.07f;

        [Header("Summon (the Wolf Pack, Phase 5.15)")]
        [Tooltip("Summon: the companion model (rigid parts: Body, Head, ArmL/R = front legs, LegL/R). amount = how many, cooldown = seconds between one companion's bites, speed = run speed, acquireRange = how far from the player they hunt, range = the leash.")]
        public GameObject summon;

        [Header("Hit effects (every pattern, Phase 5.5)")]
        [Tooltip("Run by BoonEffects on this weapon's direct hits only, at potency 1: StatusOnHit (chance 1 = always), ChainSpark, GroundOnHit.")]
        public BoonEffect[] hitEffects = Array.Empty<BoonEffect>();

        [Header("Levels 2-8")]
        public Level[] levels = Array.Empty<Level>();

        public int MaxLevel => 1 + (levels != null ? levels.Length : 0);
    }
}
