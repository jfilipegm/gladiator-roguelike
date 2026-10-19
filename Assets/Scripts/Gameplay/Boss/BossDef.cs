using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// A boss's tuning (docs/BOSSES.md). The body (HP, radius, model, shield arc) is its EnemyDef (behaviour Scripted);
    /// this holds his words, which attacks he uses in each phase, movement, each attack's wind-up / damage / size, the
    /// guards he calls, what phase 2 does to the arena and the reward. Every number here is a starting guess for a
    /// playtest. Defaults are Gorm's, so his asset predates most of these fields.
    /// </summary>
    [CreateAssetMenu(menuName = "Gladiator/Boss", fileName = "BD_")]
    public sealed class BossDef : ScriptableObject
    {
        public string id = "gorm";
        public string displayName = "GORM";
        public string title = "THE PIT MASTER";
        [Tooltip("Its body: HP, radius, model. Behaviour must be Scripted.")]
        public EnemyDef enemy;

        [Header("Words")]
        public string enterText = "THE PIT MASTER ENTERS!";
        public string fallText = "GORM FALLS!";
        public string guardsText = "GORM CALLS HIS GUARDS!";
        public string phase2Text = "\"BURN IT!\"";

        [Header("Attacks he picks from, per phase")]
        public BossAttack[] phase1Attacks = { BossAttack.Sweep, BossAttack.Whip, BossAttack.Slam };
        public BossAttack[] phase2Attacks = { BossAttack.Sweep, BossAttack.Whip, BossAttack.Slam, BossAttack.Charge };

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

        [Header("Shield Bash (near) — get out of the cone")]
        public float bashRange = 4f;
        [Tooltip("Width of the cone (deg).")]
        public float bashAngle = 100f;
        public float bashWindup = 0.7f;
        public float bashDamage = 22f;
        public float bashKnockback = 11f;

        [Header("Gladius Flurry (near-mid) — step aside each thrust")]
        public float flurryLength = 6f;
        public float flurryWidth = 1.1f;
        [Tooltip("Each thrust's own lane telegraph (s); he re-aims between thrusts.")]
        public float flurryWindup = 0.35f;
        public float flurryDamage = 12f;
        [Tooltip("Thrusts per flurry in phase 1 / phase 2.")]
        public Vector2Int flurryThrusts = new Vector2Int(3, 5);

        [Header("Charge — sidestep the lane")]
        public float chargeWindup = 0.9f;
        public float chargeSpeed = 16f;
        public float chargeLength = 14f;
        [Tooltip("Lane width (m); 0 = his body's width.")]
        public float chargeWidth = 0f;
        public float chargeDamage = 24f;
        public float chargeKnockback = 12f;
        [Tooltip("Stunned this long when the charge hits a wall or pillar (s).")]
        public float chargeStun = 1f;
        [Tooltip("While stunned his guard is down: damage he takes is multiplied by this.")]
        public float stunDamageTakenMul = 1f;
        [Tooltip("Charges in a row in phase 1 / phase 2 (he re-aims between them).")]
        public Vector2Int chargeRepeats = new Vector2Int(1, 1);

        [Header("Call the Guards")]
        public float guardsEvery = 22f;
        public EnemyDef[] guards = System.Array.Empty<EnemyDef>();
        public Vector2Int guardCount = new Vector2Int(6, 10);

        [Header("Phase 2")]
        [Range(0.1f, 0.95f)] public float phase2At = 0.55f;
        [Tooltip("Phase 2 lights a ring of burning ground along the wall foot (Gorm: \"Burn it!\").")]
        public bool phase2FireRing = true;
        [Tooltip("Phase 2's roar fires every spike trap in the arena at once (the Champion).")]
        public bool phase2SpikeVolley;
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
