using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// A boss's tuning (docs/BOSSES.md). The body (HP, radius, model) is its EnemyDef (behaviour Scripted); this holds
    /// movement, each attack's wind-up / damage / size, the guards it calls, phase 2 and the reward. Every number here
    /// is a starting guess for the Gate 2 playtest.
    /// </summary>
    [CreateAssetMenu(menuName = "Gladiator/Boss", fileName = "BD_")]
    public sealed class BossDef : ScriptableObject
    {
        public string id = "gorm";
        public string displayName = "GORM";
        public string title = "THE PIT MASTER";
        [Tooltip("Its body: HP, radius, model. Behaviour must be Scripted.")]
        public EnemyDef enemy;

        [Header("Movement")]
        public float moveSpeed = 3f;
        public float enragedMoveSpeed = 3.7f;
        public float turnRate = 220f;
        [Tooltip("Touching him hurts.")]
        public float contactDamage = 12f;
        public float contactInterval = 0.8f;
        [Tooltip("Pause after each attack before he picks the next (s).")]
        public float recover = 0.7f;

        [Header("Flail Sweep (near) — jump it or get out of the ring")]
        public float sweepRadius = 5.5f;
        public float sweepWindup = 1.1f;
        public float sweepDamage = 28f;
        public float sweepKnockback = 9f;
        [Tooltip("Feet this high above the ground clear the flail (m).")]
        public float sweepJumpClear = 0.7f;

        [Header("Whip Lash (mid) — step aside")]
        public float whipLength = 12f;
        public float whipWidth = 1.2f;
        public float whipWindup = 0.7f;
        public float whipDamage = 18f;
        [Range(0.1f, 1f)] public float whipSlow = 0.55f;
        public float whipSlowSeconds = 2f;

        [Header("Chain Slam (any range) — keep moving")]
        public float slamRadius = 2.6f;
        public float slamWindup = 1.0f;
        public float slamDamage = 30f;
        [Tooltip("The marked spot follows you for this fraction of the wind-up, then locks.")]
        [Range(0f, 1f)] public float slamTrack = 0.6f;
        [Tooltip("Phase 2: the slam leaves burning ground (radius, seconds, dps).")]
        public Vector3 slamFire = new Vector3(2.2f, 4f, 10f);

        [Header("Charge (phase 2) — sidestep the lane")]
        public float chargeWindup = 0.9f;
        public float chargeSpeed = 16f;
        public float chargeLength = 14f;
        public float chargeDamage = 24f;
        public float chargeKnockback = 12f;
        public float chargeStun = 1f;

        [Header("Call the Guards")]
        public float guardsEvery = 22f;
        public EnemyDef[] guards = System.Array.Empty<EnemyDef>();
        public Vector2Int guardCount = new Vector2Int(6, 10);

        [Header("Phase 2 — Burn it!")]
        [Range(0.1f, 0.95f)] public float phase2At = 0.55f;
        [Tooltip("Attack wind-ups and recovery are this much faster.")]
        public float enrageSpeed = 1.3f;
        public float enrageRoar = 1.5f;
        [Tooltip("Ring of burning ground along the wall foot (radius from the arena centre, patch spacing, dps).")]
        public float fireRingRadius = 19.3f;
        public float fireRingSpacing = 2.4f;
        public float fireRingPatch = 1.5f;
        public float fireRingDps = 12f;

        [Header("Defeat")]
        public Rarity rewardFloor = Rarity.Rare;
    }
}
