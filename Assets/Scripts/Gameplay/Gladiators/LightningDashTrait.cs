using Gladiator.Core;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// The Thunder Champion's ability (docs/GLADIATORS.md): starting a slide with the dash ready is a blink up to
    /// `distance` along the slide (CharacterController.Move, so walls stop it); everything within `pathRadius` of the
    /// path takes `damage` × the player's Damage and is Shocked. Then the slide goes on; the dash recharges.
    /// </summary>
    [CreateAssetMenu(menuName = "Gladiator/Traits/Lightning Dash", fileName = "TR_LightningDash")]
    public sealed class LightningDashTrait : GladiatorTrait
    {
        public float distance = 7f;
        public float cooldown = 3f;
        public float pathRadius = 1.2f;
        [Tooltip("Damage before the player's Damage multiplier.")]
        public float damage = 25f;
        public float knockback = 3f;
        public StatusDef shock;

        public override void Install(GameObject player)
        {
            var c = player.GetComponent<LightningDash>();
            if (c == null) c = player.AddComponent<LightningDash>();
            c.trait = this;
        }
    }

    /// <summary>Runtime half of LightningDashTrait.</summary>
    public sealed class LightningDash : MonoBehaviour, ITickable
    {
        public static LightningDash Instance { get; private set; }
        public static readonly int SourceId = GameInfo.StableId("lightning_dash");

        public LightningDashTrait trait;

        CharacterController _cc;
        PlayerStats _stats;
        PlayerHealth _health;
        EventHandler<SlideEvent> _onSlide;
        readonly int[] _hits = new int[256];

        public float CooldownLeft { get; private set; }
        public bool Ready => CooldownLeft <= 0f;
        public int Dashes { get; private set; }
        public int DashHits { get; private set; }
        /// <summary>Where the last blink started and ended (tests).</summary>
        public Vector3 LastFrom { get; private set; }
        public Vector3 LastTo { get; private set; }

        void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _stats = GetComponent<PlayerStats>();
            _health = GetComponent<PlayerHealth>();
            _onSlide = OnSlide;
        }

        void OnEnable()
        {
            Instance = this;
            GameLoop.Register(this, TickGroup.Player, 59);
            GameEvents.OnSlide.Subscribe(_onSlide);
        }

        void OnDisable()
        {
            if (Instance == this) Instance = null;
            GameLoop.Unregister(this);
            GameEvents.OnSlide.Unsubscribe(_onSlide);
        }

        public void Tick(float dt, float unscaledDt)
        {
            if (CooldownLeft > 0f && dt > 0f) CooldownLeft = Mathf.Max(0f, CooldownLeft - dt);
        }

        void OnSlide(in SlideEvent e)
        {
            Vector3 dir = e.Velocity;
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-4f) dir = transform.forward;
            Perform(dir.normalized);
        }

        /// <summary>The blink along `dir`, if the dash is ready. True if it happened.</summary>
        public bool Perform(Vector3 dir)
        {
            if (trait == null || !Ready || _cc == null || (_health != null && _health.IsDead)) return false;
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-4f) return false;
            dir.Normalize();
            CooldownLeft = trait.cooldown;
            Dashes++;
            Vector3 from = transform.position;
            _cc.Move(dir * trait.distance); // collides: walls and stone stop the blink
            Vector3 to = transform.position;
            LastFrom = from;
            LastTo = to;

            Vector3 path = to - from;
            path.y = 0f;
            float len = path.magnitude;
            Vector3 along = len > 1e-3f ? path / len : dir;
            float dmg = trait.damage * (_stats != null ? _stats.Damage : 1f);
            var em = EnemyManager.Instance;
            if (em != null)
            {
                Vector3 mid = (from + to) * 0.5f;
                int n = em.QueryAlive(mid, len * 0.5f + trait.pathRadius + em.MaxRadius, _hits);
                for (int k = 0; k < n; k++)
                {
                    int i = _hits[k];
                    Vector3 rel = em.GetPosition(i) - from;
                    rel.y = 0f;
                    float t = Mathf.Clamp(Vector3.Dot(rel, along), 0f, len);
                    Vector3 side = rel - along * t;
                    float reach = trait.pathRadius + em.GetRadius(i);
                    if (side.sqrMagnitude > reach * reach) continue;
                    int uid = em.GetUid(i);
                    Vector3 push = side.sqrMagnitude > 1e-4f ? side.normalized : along;
                    em.ApplyDamage(i, new DamageInfo
                    {
                        Amount = dmg, Flags = DamageFlags.Area, Point = em.GetPosition(i), Direction = push,
                        Knockback = trait.knockback, SourceId = SourceId,
                    });
                    DashHits++;
                    if (trait.shock != null && em.IsAlive(i) && em.GetUid(i) == uid) em.ApplyStatus(i, trait.shock, 0f);
                }
            }

            var pp = PlayerProjectiles.Instance;
            if (pp != null)
            {
                Vector3 up = Vector3.up * 0.9f, sideways = Vector3.Cross(Vector3.up, along) * 0.35f;
                pp.Bolt(from + up, to + up);
                pp.Bolt(from + up * 0.5f + sideways, to + up * 1.3f - sideways);
            }
            var pop = DeathPopFx.Instance;
            if (pop != null) pop.Burst(to + Vector3.up * 0.9f, 14, 5f, 0.12f, new Color(0.75f, 0.95f, 1f), Color.white);
            SfxPlayer.Play(SfxId.Spark, to);
            ScreenShake.Add(0.08f);
            return true;
        }

        /// <summary>Makes the dash ready now (tests, admin).</summary>
        public void Recharge() => CooldownLeft = 0f;
    }
}
