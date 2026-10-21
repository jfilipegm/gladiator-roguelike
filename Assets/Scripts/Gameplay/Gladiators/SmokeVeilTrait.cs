using Gladiator.Core;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// The Ashen Oracle's ability (docs/GLADIATORS.md): the first hit he takes every `cooldown` seconds (not a status
    /// tick) makes smoke burst out of him: everything within `radius` is Cursed, Slowed and pushed back.
    /// </summary>
    [CreateAssetMenu(menuName = "Gladiator/Traits/Smoke Veil", fileName = "TR_SmokeVeil")]
    public sealed class SmokeVeilTrait : GladiatorTrait
    {
        public float cooldown = 5f;
        public float radius = 4.5f;
        public StatusDef curse;
        public int curseStacks = 3;
        [Tooltip("Curse dps per stack before the player's Damage multiplier.")]
        public float curseDps = 4f;
        public StatusDef slow;
        public float knockback = 9f;
        public Color smokeA = new Color(0.45f, 0.42f, 0.5f), smokeB = new Color(0.72f, 0.45f, 0.95f);

        public override void Install(GameObject player)
        {
            var c = player.GetComponent<SmokeVeil>();
            if (c == null) c = player.AddComponent<SmokeVeil>();
            c.trait = this;
        }
    }

    /// <summary>Runtime half of SmokeVeilTrait.</summary>
    public sealed class SmokeVeil : MonoBehaviour, ITickable
    {
        public static readonly int SourceId = GameInfo.StableId("smoke_veil");

        public SmokeVeilTrait trait;
        PlayerStats _stats;
        PlayerHealth _health;
        EventHandler<DamagedEvent> _onDamaged;
        readonly int[] _hits = new int[512];

        public float CooldownLeft { get; private set; }
        public int Veils { get; private set; }
        public int VeilHits { get; private set; }

        void Awake()
        {
            _stats = GetComponent<PlayerStats>();
            _health = GetComponent<PlayerHealth>();
            _onDamaged = OnDamaged;
        }

        void OnEnable()
        {
            GameLoop.Register(this, TickGroup.Player, 63);
            GameEvents.OnDamaged.Subscribe(_onDamaged);
        }

        void OnDisable()
        {
            GameLoop.Unregister(this);
            GameEvents.OnDamaged.Unsubscribe(_onDamaged);
        }

        public void Tick(float dt, float unscaledDt)
        {
            if (CooldownLeft > 0f && dt > 0f) CooldownLeft = Mathf.Max(0f, CooldownLeft - dt);
        }

        void OnDamaged(in DamagedEvent e)
        {
            if (!e.Status) Perform();
        }

        /// <summary>The veil, if it's ready. True if it happened.</summary>
        public bool Perform()
        {
            if (trait == null || CooldownLeft > 0f || (_health != null && _health.IsDead)) return false;
            CooldownLeft = trait.cooldown;
            Veils++;
            Vector3 at = transform.position;
            var em = EnemyManager.Instance;
            if (em != null)
            {
                float dps = trait.curseDps * (_stats != null ? _stats.Damage : 1f);
                int n = em.QueryAlive(at, trait.radius + em.MaxRadius, _hits);
                for (int k = 0; k < n; k++)
                {
                    int i = _hits[k];
                    Vector3 to = em.GetPosition(i) - at;
                    to.y = 0f;
                    float rr = trait.radius + em.GetRadius(i);
                    if (to.sqrMagnitude > rr * rr) continue;
                    if (trait.curse != null) em.ApplyStatus(i, trait.curse, dps, trait.curseStacks);
                    if (trait.slow != null) em.ApplyStatus(i, trait.slow, 0f);
                    float d = to.magnitude;
                    em.ApplyDamage(i, new DamageInfo
                    {
                        Amount = 1f, Flags = DamageFlags.Area | DamageFlags.Proc, Point = em.GetPosition(i),
                        Direction = d > 1e-3f ? to / d : transform.forward, Knockback = trait.knockback, SourceId = SourceId,
                    });
                    VeilHits++;
                }
            }
            var pp = PlayerProjectiles.Instance;
            if (pp != null) pp.Shockwave(at, trait.radius);
            var pop = DeathPopFx.Instance;
            if (pop != null) pop.Burst(at + Vector3.up * 0.8f, 30, 6f, 0.3f, trait.smokeA, trait.smokeB);
            SfxPlayer.Play(SfxId.Explosion, at);
            return true;
        }

        public void Recharge() => CooldownLeft = 0f;
    }
}
