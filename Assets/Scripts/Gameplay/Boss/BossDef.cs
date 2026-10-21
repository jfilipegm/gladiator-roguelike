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
        [Tooltip("The run summary: his row's label, the line when he beats you, the line when you beat him.")]
        public string shortName = "Gorm";
        public string deathLine = "Gorm drags you off by the chain.";
        public string victoryLine = "The Pit Master lies in the dirt. The crowd chants a name: yours.";

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

        [Header("Pounce (any range) — keep moving")]
        [Tooltip("The circle he lands in (m).")]
        public float pounceRadius = 3.5f;
        public float pounceWindup = 1.0f;
        [Tooltip("The circle follows you (lagging) for this fraction of the wind-up, then locks.")]
        [Range(0f, 1f)] public float pounceTrack = 0.7f;
        [Tooltip("Seconds in the air for a 10 m leap (longer leaps take a little longer).")]
        public float pounceFlight = 0.55f;
        public float pounceHeight = 3f;
        [Tooltip("Farthest he leaps (m): beyond it he lands short, toward you.")]
        public float pounceRange = 24f;
        public float pounceDamage = 28f;
        public float pounceKnockback = 9f;
        [Tooltip("Slow to get up after landing (s): the window to punish him.")]
        public float pounceRecover = 0.8f;
        [Tooltip("Pounces in a row in phase 1 / phase 2 (he re-aims between them).")]
        public Vector2Int pounceRepeats = new Vector2Int(1, 2);

        [Header("Coin Bombs (any range) — keep moving (the Gilded Patron)")]
        [Tooltip("Purses per volley in phase 1 / phase 2: the first on you, the rest around you.")]
        public Vector2Int bombCount = new Vector2Int(3, 5);
        public float bombWindup = 0.6f;
        [Tooltip("Seconds between two purses leaving his hand.")]
        public float bombInterval = 0.35f;
        [Tooltip("Seconds a purse flies: its circle fills meanwhile.")]
        public float bombFlight = 1.0f;
        public float bombRadius = 2.4f;
        public float bombDamage = 22f;
        [Tooltip("Gold coins each purse scatters when it bursts.")]
        public int bombCoins = 3;
        [Tooltip("How far around you the later purses land (m).")]
        public float bombSpread = 3.5f;

        [Header("Golden Volley (mid) — step into a gap")]
        [Tooltip("Red-hot coins in the fan, phase 1 / phase 2.")]
        public Vector2Int volleyCount = new Vector2Int(7, 11);
        [Tooltip("The fan's width (deg), phase 1 / phase 2.")]
        public Vector2 volleyArc = new Vector2(50f, 70f);
        public float volleyWindup = 0.8f;
        public float volleySpeed = 15f;
        public float volleyDamage = 12f;
        public float volleyRange = 24f;

        [Header("Payroll (the Gilded Patron's guard)")]
        [Tooltip("While any enemy of this type lives he takes guardedDamageMul of the damage (none = never guarded).")]
        public EnemyDef guardedBy;
        [Range(0.05f, 1f)] public float guardedDamageMul = 0.4f;
        public Color guardThreadColor = new Color(1f, 0.82f, 0.25f, 0.9f);
        [Tooltip("The arena's guard spots (patron boxes): guards appear at this many of the spots nearest the player.")]
        public int guardSpotsUsed = 2;
        [Tooltip("His fortune, paid into the run when he falls (0 = none).")]
        public int defeatGold;

        [Header("Beasts (the Stone Amphitheater's cages)")]
        [Tooltip("Calling the guards also opens the idle cage nearest him with this many beasts (0 = no cages). Every cage rattles at his entrance.")]
        public Vector2Int beastPack = Vector2Int.zero;
        [Tooltip("Phase 2's roar opens every cage at once, this many beasts each (0 = none).")]
        public int phase2CagePack;
        [Tooltip("At his defeat every enemy left is dragged off (the beasts scatter).")]
        public bool scatterOnDefeat;

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
        [Tooltip("Phase 2's roar fires every fire-jet row at once, and the blade sweepers spin for good (the Gilded Patron).")]
        public bool phase2FireJets;
        public bool phase2Blades;
        [Tooltip("Attack wind-ups and recovery are this much faster.")]
        public float enrageSpeed = 1.3f;
        public float enrageRoar = 1.5f;
        [Tooltip("Ring of burning ground along the wall foot (metres inside the arena's edge, patch spacing, dps). It follows the arena's shape.")]
        public float fireRingInset = 1.1f;
        public float fireRingSpacing = 2.4f;
        public float fireRingPatch = 1.5f;
        public float fireRingDps = 12f;

        [Header("Defeat")]
        public Rarity rewardFloor = Rarity.Rare;
    }
}
