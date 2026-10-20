using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>How an enemy fights. Behaviour = code (EnemyManager); numbers = data (below).</summary>
    public enum EnemyBehaviour
    {
        /// <summary>Walk at the player, hurt on contact (Convict).</summary>
        Chaser,
        /// <summary>Close in fast, crouch, leap (Pit Cur).</summary>
        Lunger,
        /// <summary>Chaser that turns to face you and blocks most frontal damage (Shield Thug).</summary>
        ShieldBearer,
        /// <summary>Keeps its distance, winds up, throws a straight projectile (Slinger).</summary>
        Ranged,
        /// <summary>Slow; telegraphed ground slam around itself — jump to avoid (Brute).</summary>
        Slammer,
        /// <summary>Keeps its distance, lobs onto a marked spot that burns for a while (Fire-Pot Thrower).</summary>
        Lobber,
        /// <summary>Driven from outside (bosses, BossController): EnemyManager keeps its HP, hits, statuses and death but
        /// never moves, animates or attacks it.</summary>
        Scripted,
        /// <summary>Keeps its distance behind a tall shield (frontal hits blocked, except while it recovers from a
        /// shot), draws along a thin aim line that stops tracking just before release, then looses a fast straight
        /// arrow (Shield Archer). Appended: enum values are serialized.</summary>
        ShieldedRanged,
        /// <summary>Circles to your side instead of your face, crouches behind a telegraphed lane, dashes through you,
        /// then stands winded: the window to kill it (Assassin).</summary>
        Dasher,
    }

    /// <summary>An enemy type as data (CLAUDE.md §2.4). New enemies = new assets; new behaviours = new code.</summary>
    [CreateAssetMenu(menuName = "Gladiator/Enemy", fileName = "EN_")]
    public sealed class EnemyDef : ScriptableObject
    {
        /// <summary>Stable numeric id derived from `id` (events, stats, saves).</summary>
        public int RuntimeId => Gladiator.Core.GameInfo.StableId(id);
        public string id = "convict";
        public string displayName = "Starving Convict";
        [Tooltip("Model prefab (FBX) — rigid parts named Body/Head/ArmL/ArmR/LegL/LegR.")]
        public GameObject model;
        public PartAnimProfile anim;
        public float scale = 1f;

        [Header("Stats")]
        public float maxHp = 20f;
        public float moveSpeed = 3.6f;
        [Tooltip("Speed variation ± fraction, so swarms don't move in lockstep.")]
        [Range(0f, 0.5f)] public float speedJitter = 0.15f;
        public float acceleration = 14f;
        [Tooltip("Body radius for separation, hits and contact (m).")]
        public float radius = 0.38f;
        [Tooltip("1 = normal knockback, 0 = immovable.")]
        [Range(0f, 2f)] public float knockbackTaken = 1f;

        [Header("Attack")]
        public float contactDamage = 8f;
        [Tooltip("Seconds between contact hits from the same enemy.")]
        public float contactInterval = 0.9f;

        [Header("Behaviour")]
        public EnemyBehaviour behaviour = EnemyBehaviour.Chaser;
        [Tooltip("Wind-up / telegraph before the attack lands (s). Readability lives here.")]
        public float windup = 0.6f;
        [Tooltip("Seconds between special attacks.")]
        public float attackCooldown = 2.5f;
        [Tooltip("Lunger/Slammer: distance that triggers the attack. Ranged/Lobber: max throw distance.")]
        public float attackRange = 3f;
        public float attackDamage = 10f;
        [Tooltip("Slammer: slam radius. Lobber: fire patch radius.")]
        public float attackRadius = 2.5f;
        [Tooltip("Recovery after the attack (vulnerable, slow) (s).")]
        public float recover = 0.4f;
        [Tooltip("Max turn rate (deg/s). Shield bearers turn slowly so flanking works.")]
        public float turnRate = 540f;

        [Header("Lunger")]
        public float lungeSpeed = 13f;
        public float lungeTime = 0.28f;

        [Header("Shield bearer")]
        [Tooltip("Hits from within this frontal arc (deg) are blocked.")]
        [Range(0f, 360f)] public float blockArc = 120f;
        [Range(0f, 1f)] public float blockReduction = 0.85f;

        [Header("Ranged / Lobber")]
        [Tooltip("Preferred distance band to the player (m).")]
        public Vector2 keepDistance = new Vector2(6f, 10f);
        public float projectileSpeed = 13f;
        [Tooltip("Lobber: flight time of the lob (s).")]
        public float lobTime = 0.95f;
        public float projectileRadius = 0.25f;
        [Tooltip("Shield Archer: seconds before the shot when the aim line stops tracking you (sidestep then).")]
        public float aimLockTime = 0.3f;
        [Tooltip("Lobber: fire patch duration (s) and damage per second while you stand in it.")]
        public float zoneDuration = 3.5f;
        public float zoneDps = 10f;

        [Header("Dasher")]
        [Tooltip("Dash speed (m/s) and length (m): the lane it telegraphs.")]
        public float dashSpeed = 18f;
        public float dashLength = 7f;
        [Tooltip("How far to your side it circles before it dashes (m).")]
        public float flankRadius = 5.5f;
        [Tooltip("Gives up flanking and dashes from wherever it is after this long (s).")]
        public float flankTimeout = 4f;
        [Tooltip("Circles to your side before it dashes (Assassin). Off: makes straight for you and charges once in range (War Boar).")]
        public bool flank = true;
        [Tooltip("War Boar: enemies in its charge take this × its hit, once per charge (0 = it passes through them).")]
        public float trampleMul;

        [Header("Net-Thrower")]
        [Tooltip("Lobber: a weighted net instead of a fire pot. Whoever stands inside on the ground when it lands is caught (no fire patch).")]
        public bool lobNet;
        [Tooltip("Caught: move speed × this (the player's slows bottom out at 0.1) for netTime seconds; attackDamage on landing.")]
        [Range(0.1f, 1f)] public float netSlow = 0.1f;
        public float netTime = 1.2f;

        [Header("Rewards")]
        public float xp = 1f;
        [Range(0f, 1f)] public float goldChance = 0.05f;

        [Header("Pool")]
        public int prewarm = 64;
    }
}
