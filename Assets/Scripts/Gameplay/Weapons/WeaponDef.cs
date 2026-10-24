using System;
using Gladiator.Core;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>Attack patterns that weapon data composes (CLAUDE.md §6.8). Every one is implemented; Sweep (R3) is the
    /// Labyrinth Chain, Fireball (note 33) the Ashen Censer. Append new patterns at the end (assets serialize the
    /// index: Sweep = 10, Fireball = 11).</summary>
    public enum WeaponPattern { MeleeArc, Thrust, MultiHit, GroundSlam, Projectile, Returning, Orbit, Chain, Summon, Lob, Sweep, Fireball }

    /// <summary>
    /// A weapon as data. Its numbers are the level-1 base; each level 2-8 adds `levels[level-2].mods` to that weapon only
    /// (scaled by the card's rarity, like Boons), and the player's stats multiply everything on top.
    /// Implemented patterns: MeleeArc (Gladius), Thrust (Spear), Projectile (Javelins), Returning (Chakram), Orbit (Gorm's
    /// Flail: range = orbit radius, width = ball radius, speed = m/s along the circle, cooldown = re-hit interval per enemy),
    /// Lob (Bomb Satchel: acquireRange = throw range, width = blast radius, flightTime/fuse shortened by Projectile Speed),
    /// GroundSlam (the Cleaver), Chain (the Storm Spear), MultiHit (the Trident, note 34: one lunge whose `prongs`
    /// parallel lanes, `prongSpacing` × √Area apart, each range long and width wide, strike at once; an enemy on two or
    /// three lanes takes each), Summon (the Wolf Pack: companions driven by CompanionPack), Sweep (the
    /// Labyrinth Chain, R3: the chain travels across arcDegrees in `sweepTime`, range long, hitting what it passes once
    /// per sweep; its last tipLength m is the stone, which always crits; Amount = backswings, each the other way),
    /// Fireball (the Ashen Censer, note 33: Amount fireballs at the nearest enemies, speed m/s, turning `homing` deg/s
    /// toward their target, range = max travel; each bursts on the first enemy it touches, the ground or a wall, hitting
    /// everything within width × Area).
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
        [Tooltip("Arc: reach from the player's centre. Thrust: line length. Projectile: max travel. Returning: throw distance. Sweep: the chain's reach to the stone (× Area) (m).")]
        public float range = 2.6f;
        public float arcDegrees = 150f;
        [Tooltip("Thrust: line width. Projectiles: hit radius (m).")]
        public float width = 0.5f;
        [Tooltip("Auto-aim acquires the nearest enemy within this range; nothing in range = no attack.")]
        public float acquireRange = 3.2f;

        [Header("Amount / projectiles")]
        [Tooltip("Arc/Thrust: attacks per trigger (extra ones follow at the next targets). Projectiles: per volley. Sweep: sweeps per trigger (the extra ones are backswings, the other way, with no wind-up).")]
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
        [Tooltip("Thrust: hits in the last tipLength m of the line always crit (the Whip's crack). Sweep: the stone, the chain's last tipLength m (× √Area), always crits and shoves tipKnockbackMul harder. 0 = off.")]
        public float tipLength;
        [Tooltip("MeleeArc / Thrust with Amount > 1: each extra strike goes for the next-nearest enemy instead of the nearest again.")]
        public bool spreadStrikes;

        [Header("MultiHit (the Trident, Phase 5.14)")]
        [Tooltip("MultiHit: the trident's prongs, each a parallel lane of the lunge (range long, width wide, prongSpacing apart).")]
        [Min(1)] public int prongs = 3;
        [Tooltip("Unused since note 34 (the lunge strikes with every prong at once; it was the gap between a flurry's jabs). Kept so old assets load.")]
        public float prongInterval = 0.07f;

        [Header("Summon (the Wolf Pack, Phase 5.15)")]
        [Tooltip("Summon: the companion model (rigid parts: Body, Head, ArmL/R = front legs, LegL/R). amount = how many, cooldown = seconds between one companion's bites, speed = run speed, acquireRange = how far from the player they hunt, range = the leash.")]
        public GameObject summon;

        [Header("Sweep (the Labyrinth Chain, R3)")]
        [Tooltip("Sweep: seconds the chain takes to cross arcDegrees (not scaled by attack speed).")]
        public float sweepTime = 0.3f;
        [Tooltip("Sweep: a whirl (360°, WeaponSystem.WhirlSweeps) takes sweepTime × this.")]
        public float whirlTimeMul = 1.5f;
        [Tooltip("Sweep: the push = the chain's tangent (the way it moves) + outward × this, normalised. Bosses are pushed straight out.")]
        public float sweepOutward = 0.35f;
        [Tooltip("Sweep: the stone's knockback × this (its hits crit through tipLength).")]
        public float tipKnockbackMul = 1.6f;
        [Tooltip("Sweep: the link mesh, laid end to end from the hand to `mesh` (the stone). None = no chain drawn.")]
        public Mesh linkMesh;
        [Tooltip("Sweep: the link mesh's length along +Z (m).")]
        public float linkLength = 0.6f;
        [Tooltip("Sweep: the chain's height above the feet at the hand (x) and at the stone (y) (m).")]
        public Vector2 chainHeight = new Vector2(1.05f, 0.55f);
        [Tooltip("Sweep: the stone's centre past the last link's end (m).")]
        public float weightOffset = 0.33f;
        [Tooltip("A part of the gladiator's model this weapon swings (Asterion's \"Chain\"): hidden while the weapon draws it. Empty = none.")]
        public string modelPart = "";

        [Header("Hit effects (every pattern, Phase 5.5)")]
        [Tooltip("Run by BoonEffects on this weapon's direct hits only, at potency 1: StatusOnHit (chance 1 = always), ChainSpark, GroundOnHit.")]
        public BoonEffect[] hitEffects = Array.Empty<BoonEffect>();

        [Header("Levels 2-8")]
        public Level[] levels = Array.Empty<Level>();

        [Header("MultiHit lunge (the Trident, note 34)")]
        [Tooltip("MultiHit: metres between two neighbouring prongs' lanes (× √Area); the lanes run parallel along the aim, the middle one through the target.")]
        public float prongSpacing = 0.45f;

        [Header("Fireball (the Ashen Censer, note 33)")]
        [Tooltip("Fireball: how fast it turns toward its target (deg/s); 0 = straight. A fireball whose target died flies on straight.")]
        public float homing = 150f;
        [Tooltip("Fireball: height above the feet it leaves from (m), the censer's; 0 = the projectiles' flight height.")]
        public float castHeight = 0f;

        public int MaxLevel => 1 + (levels != null ? levels.Length : 0);
    }
}
