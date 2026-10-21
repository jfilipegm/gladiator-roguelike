using Gladiator.Core;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// The Murder Machine's ability (docs/GLADIATORS.md): a Wrath meter fills only from hits he takes (not status ticks);
    /// when it's full, his next landing is the Verdict: a shockwave round him that hits everything in it (damage × the
    /// player's Damage, knockback × Knockback, radius × √Area) and staggers the survivors (a short Root; bosses ignore
    /// it). The meter empties.
    /// </summary>
    [CreateAssetMenu(menuName = "Gladiator/Traits/Verdict", fileName = "TR_Verdict")]
    public sealed class VerdictTrait : GladiatorTrait
    {
        [Header("Wrath meter")]
        public float meterMax = 100f;
        [Tooltip("Wrath per hit taken (status ticks don't count).")]
        public float perHit = 8f;

        [Header("The Verdict (a landing with a full meter)")]
        [Tooltip("Slowest fall that still counts as a landing (m/s): a hop off a kerb doesn't.")]
        public float minFallSpeed = 2f;
        public float radius = 7f;
        [Tooltip("Damage before the player's Damage multiplier.")]
        public float damage = 60f;
        public float knockback = 14f;
        [Tooltip("Applied to survivors (a Root kind: speed 0 for its duration).")]
        public StatusDef stagger;
        public float critChance = 0f;

        [Header("Look")]
        [Tooltip("Pale steel with the player's cyan: never the orange-red that means \"this hurts you\".")]
        public Color steel = new Color(0.78f, 0.86f, 0.92f);
        public Color edge = new Color(0.55f, 0.95f, 1f);
        public string announce = "VERDICT!";

        public override void Install(GameObject player)
        {
            var c = player.GetComponent<Verdict>();
            if (c == null) c = player.AddComponent<Verdict>();
            c.trait = this;
        }
    }

    /// <summary>Runtime half of VerdictTrait: the meter and the shockwave. The HUD reads Instance.</summary>
    public sealed class Verdict : MonoBehaviour
    {
        public static Verdict Instance { get; private set; }
        public static readonly int SourceId = GameInfo.StableId("verdict");

        public VerdictTrait trait;

        PlayerHealth _health;
        PlayerStats _stats;
        EventHandler<DamagedEvent> _onDamaged;
        EventHandler<LandEvent> _onLand;
        readonly int[] _hits = new int[512];

        public float Wrath { get; private set; }
        public float Fill01 => trait != null && trait.meterMax > 0f ? Mathf.Clamp01(Wrath / trait.meterMax) : 0f;
        public bool Full => trait != null && Wrath >= trait.meterMax;
        public int Verdicts { get; private set; }
        /// <summary>Enemies hit by Verdicts this run (bot log, tests).</summary>
        public int VerdictHits { get; private set; }
        public float Radius => trait == null ? 0f : trait.radius * Mathf.Sqrt(_stats != null ? _stats.Area : 1f);

        void Awake()
        {
            _health = GetComponent<PlayerHealth>();
            _stats = GetComponent<PlayerStats>();
            _onDamaged = OnDamaged;
            _onLand = OnLand;
        }

        void OnEnable()
        {
            Instance = this;
            GameEvents.OnDamaged.Subscribe(_onDamaged);
            GameEvents.OnLand.Subscribe(_onLand);
        }

        void OnDisable()
        {
            if (Instance == this) Instance = null;
            GameEvents.OnDamaged.Unsubscribe(_onDamaged);
            GameEvents.OnLand.Unsubscribe(_onLand);
        }

        void OnDamaged(in DamagedEvent e)
        {
            if (trait == null || e.Status || _health == null || _health.IsDead) return;
            Wrath = Mathf.Min(trait.meterMax, Wrath + trait.perHit);
        }

        void OnLand(in LandEvent e)
        {
            if (Full && e.FallSpeed >= trait.minFallSpeed) Perform();
        }

        /// <summary>Fills the meter (tests, admin).</summary>
        public void Fill() { if (trait != null) Wrath = trait.meterMax; }

        /// <summary>The Verdict, if the meter is full. True if it happened.</summary>
        public bool Perform()
        {
            if (!Full || _health == null || _health.IsDead) return false;
            Wrath = 0f;
            Verdicts++;
            Vector3 at = transform.position;
            float radius = Radius;
            float dmg = trait.damage * (_stats != null ? _stats.Damage : 1f);
            float knock = trait.knockback * (_stats != null ? _stats.Knockback : 1f);
            var em = EnemyManager.Instance;
            if (em != null)
            {
                int n = em.QueryAlive(at, radius + em.MaxRadius, _hits);
                for (int k = 0; k < n; k++)
                {
                    int i = _hits[k];
                    Vector3 to = em.GetPosition(i) - at;
                    to.y = 0f;
                    float rr = radius + em.GetRadius(i);
                    if (to.sqrMagnitude > rr * rr) continue;
                    float d = to.magnitude;
                    bool crit = trait.critChance > 0f && GameRandom.Chance(trait.critChance);
                    int uid = em.GetUid(i);
                    em.ApplyDamage(i, new DamageInfo
                    {
                        Amount = dmg * (crit ? 2f : 1f),
                        Flags = DamageFlags.Area | (crit ? DamageFlags.Crit : 0),
                        Point = em.GetPosition(i),
                        Direction = d > 1e-3f ? to / d : transform.forward,
                        Knockback = knock,
                        SourceId = SourceId,
                    });
                    VerdictHits++;
                    if (trait.stagger != null && em.IsAlive(i) && em.GetUid(i) == uid) em.ApplyStatus(i, trait.stagger, 0f);
                }
            }

            var pp = PlayerProjectiles.Instance;
            if (pp != null)
            {
                pp.Shockwave(at, radius);
                pp.Shockwave(at, radius * 0.6f);
            }
            var pop = DeathPopFx.Instance;
            if (pop != null) pop.Burst(at + Vector3.up * 0.2f, 40, 10f, 0.24f, trait.steel, trait.edge);
            SfxPlayer.Play(SfxId.Slam, at);
            ScreenShake.Add(0.4f);
            if (CrowdExcitement.Instance != null) CrowdExcitement.Instance.Spike(0.35f);
            GameEvents.OnAnnounce.Raise(new AnnounceEvent { Kind = AnnounceKind.Line, Text = trait.announce, Color = trait.edge, Seconds = 1.4f });
            return true;
        }
    }
}
