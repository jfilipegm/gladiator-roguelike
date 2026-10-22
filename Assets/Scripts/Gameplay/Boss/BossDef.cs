using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// A boss's tuning (docs/BOSSES.md). The body (HP, radius, model, shield arc) is its EnemyDef (behaviour Scripted);
    /// this holds his words, which attacks he uses in each phase, movement, each attack's wind-up / damage / size, the
    /// guards he calls, what phase 2 does to the arena, an optional phase 3 (the Emperor's Champion), an optional rise
    /// and false death (the Eternal Victor), the reward and an optional tease at his fall (R3: the Eternal Victor's
    /// chain snapping while Asterion is locked). Every number here is a starting guess for a playtest. Defaults
    /// are Gorm's, so his asset predates most of these fields; every newer field defaults to "off".
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
        [Tooltip("While the player is farther than this (m) he strides at closeInSpeed and doesn't attack (0 = never): a big floor mustn't be a minute of walking.")]
        public float closeInRange;
        public float closeInSpeed;

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
        [Tooltip("Grasp of the Dead (the Eternal Victor): bone hands burst along the lane instead of the crack, and his falx is driven into the deck. whipSlow 0.1 = held.")]
        public bool whipGrasp;

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
        [Tooltip("Javelins instead of coins (the Emperor's Champion): drawn as arrows and thrown at the player's height, so a raised platform is no shelter.")]
        public bool volleyJavelins;

        [Header("Falx Throw (the Eternal Victor) — step out of the lane until it's back")]
        [Tooltip("The lane (m): the falx flies this far, then back to his hand. It turns back early on stone (statues, walls); it flies over pits.")]
        public float discLength = 18f;
        public float discWidth = 1.6f;
        public float discWindup = 0.8f;
        [Tooltip("m/s, out and back.")]
        public float discSpeed = 24f;
        [Tooltip("Once out, once back.")]
        public float discDamage = 18f;
        public float discKnockback = 6f;
        [Tooltip("deg/s about the vertical.")]
        public float discSpin = 1080f;
        [Tooltip("Throws in a row in phase 1 / phase 2 (he re-aims between them).")]
        public Vector2Int discRepeats = new Vector2Int(1, 1);
        [Tooltip("The model part that flies (pivot at the grip). Without it the throw still hurts, unseen.")]
        public string discPart = "Falx";

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
        [Tooltip("The guard clock runs from this phase on (1 = the whole fight); reaching a later phase calls them at once, at the end of its roar.")]
        [Min(1)] public int guardsFromPhase = 1;
        [Tooltip("Through the guardSpotsUsed gates nearest the player (guard k at gate k % that), instead of gate 0, 1, 2... in order.")]
        public bool guardsNearestGates;

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

        [Header("Phase 3 (the Emperor's Champion)")]
        [Tooltip("Phase 3 starts below this fraction of his HP, after phase 2 (0 = no phase 3). Phase 2's extras (fire ring, spike volley, cages, jets, blades) don't fire again.")]
        [Range(0f, 0.9f)] public float phase3At;
        [Tooltip("Attacks he picks from in phase 3 (empty = phase 2's). Pair values such as volleyCount use their phase-2 value.")]
        public BossAttack[] phase3Attacks = System.Array.Empty<BossAttack>();
        public string phase3Text = "";
        [Tooltip("Attack wind-ups and recovery are this much faster in phase 3.")]
        public float phase3Speed = 1.4f;
        [Tooltip("Phase 3 move speed (0 = enragedMoveSpeed).")]
        public float phase3MoveSpeed;
        [Tooltip("Charges in a row in phase 3 (0 = chargeRepeats' phase-2 value).")]
        public int phase3ChargeRepeats;
        [Tooltip("Falx throws in a row in phase 3 (0 = discRepeats' phase-2 value).")]
        public int phase3DiscRepeats;
        [Tooltip("Pounces in a row in phase 3 (0 = pounceRepeats' phase-2 value).")]
        public int phase3PounceRepeats;

        [Header("Entrance")]
        [Tooltip("He rises at the arena's centre (the Colosseum's stage lift) instead of walking in through a gate; bossGate is the fallback.")]
        public bool entranceRise;
        [Tooltip("He starts this far below the ground there (m) and rises over riseTime (s), kneeling, then stands and salutes.")]
        public float riseDepth = 3f;
        public float riseTime = 2.6f;

        [Header("Eternal (false death)")]
        [Tooltip("The first time he reaches 0 HP (phase 3) he collapses instead: his remains have remainsHp for remainsTime. Destroy them and he's gone; else he rises with riseHp01 of his HP.")]
        public bool falseDeath;
        public float remainsHp = 1500f;
        public float remainsTime = 8f;
        [Range(0.01f, 1f)] public float riseHp01 = 0.2f;
        public string remainsText = "";
        public string riseText = "";
        [Tooltip("As he rises: a burst round him (m, damage, knockback), warned by an orange-red circle for its last riseBurstWarn seconds.")]
        public float riseBurstRadius = 4f;
        public float riseBurstDamage = 20f;
        public float riseBurstKnockback = 10f;
        public float riseBurstWarn = 1f;
        [Tooltip("The ring round his remains (it doesn't hurt: bone, not orange-red).")]
        public Color remainsColor = new Color(0.89f, 0.84f, 0.74f, 0.85f);

        [Header("Defeat")]
        public Rarity rewardFloor = Rarity.Rare;

        [Header("Reveal (R3: a tease at his fall)")]
        [Tooltip("A line after the fall line (the Eternal Victor: \"BELOW THE SAND, A CHAIN SNAPS.\"); empty = none.")]
        public string revealText = "";
        [Tooltip("The line shows only while this unlock is locked (none = always).")]
        public UnlockDef revealUnlock;
        [Tooltip("Seconds into the fall (real time) before the line.")]
        public float revealDelay = 1.2f;
        [Tooltip("Seconds the fall is held longer when the line shows, so it's read before the reward pick.")]
        public float revealHold;
        [Tooltip("Parchment, not orange-red: it doesn't hurt.")]
        public Color revealColor = new Color(0.86f, 0.78f, 0.62f);
    }
}
