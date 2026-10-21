using Gladiator.Core;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// The Net-Weaver's ability (docs/GLADIATORS.md): starting a slide with the net ready flings a weighted net
    /// `throwDistance` ahead along the slide. It flies for `flightTime` s; where it lands, everything within `radius` is
    /// hauled toward the middle (a knockback toward the landing point), takes `damage` × the player's Damage and is
    /// Rooted. Scripted enemies (bosses) can't be held: they're Slowed instead. The net lies on the sand while it holds.
    /// </summary>
    [CreateAssetMenu(menuName = "Gladiator/Traits/Cast Net", fileName = "TR_CastNet")]
    public sealed class CastNetTrait : GladiatorTrait
    {
        public float cooldown = 4f;
        [Tooltip("How far ahead of the player the net lands (m).")]
        public float throwDistance = 5f;
        [Tooltip("Seconds in the air.")]
        public float flightTime = 0.3f;
        public float arcHeight = 1.6f;
        [Tooltip("Everything within this radius of the landing point is caught (m, plus the enemy's radius).")]
        public float radius = 3.2f;
        [Tooltip("How much of the way to the middle a caught enemy is hauled (0-1, before its own knockback resistance).")]
        [Range(0f, 1f)] public float pull = 0.7f;
        [Tooltip("Damage when it lands, before the player's Damage multiplier.")]
        public float damage = 6f;
        [Tooltip("Root status (its duration = how long they're held, and how long the net lies on the sand).")]
        public StatusDef root;
        [Tooltip("Slow status for bosses, who can't be held.")]
        public StatusDef slow;

        [Header("Look")]
        public Mesh mesh;
        public Material material;
        [Tooltip("Scale of the net mesh when it's fully open on the sand (the mesh is ~1 m across per unit of radius).")]
        public float meshScale = 1f;
        [Tooltip("The landing ring while it flies: player colours, never orange-red.")]
        public Color ring = new Color(0.72f, 0.95f, 1f, 0.7f);

        public override void Install(GameObject player)
        {
            var c = player.GetComponent<CastNet>();
            if (c == null) c = player.AddComponent<CastNet>();
            c.trait = this;
        }
    }

    /// <summary>Runtime half of CastNetTrait: one net at a time, central tick, drawn with RenderMesh.</summary>
    public sealed class CastNet : MonoBehaviour, ITickable
    {
        public static CastNet Instance { get; private set; }
        public static readonly int SourceId = GameInfo.StableId("cast_net");

        public CastNetTrait trait;

        PlayerStats _stats;
        PlayerHealth _health;
        EventHandler<SlideEvent> _onSlide;
        readonly int[] _hits = new int[256];
        RenderParams _rp;
        bool _rpReady;
        Vector3 _from, _to;
        float _flightT = -1f, _holdT = -1f, _yaw;

        public float CooldownLeft { get; private set; }
        public bool Ready => CooldownLeft <= 0f;
        public bool InFlight => _flightT >= 0f;
        public int Casts { get; private set; }
        /// <summary>Enemies caught by the nets (bot log, tests).</summary>
        public int Hauled { get; private set; }
        /// <summary>Where the last net landed (or will land).</summary>
        public Vector3 LastLanding => _to;

        void Awake()
        {
            _stats = GetComponent<PlayerStats>();
            _health = GetComponent<PlayerHealth>();
            _onSlide = OnSlide;
        }

        void OnEnable()
        {
            Instance = this;
            GameLoop.Register(this, TickGroup.Weapons, 6);
            GameEvents.OnSlide.Subscribe(_onSlide);
        }

        void OnDisable()
        {
            if (Instance == this) Instance = null;
            GameLoop.Unregister(this);
            GameEvents.OnSlide.Unsubscribe(_onSlide);
        }

        void OnSlide(in SlideEvent e)
        {
            Vector3 dir = e.Velocity;
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-4f) dir = transform.forward;
            Perform(dir.normalized);
        }

        /// <summary>Flings the net along `dir`, if it's ready. True if it was thrown.</summary>
        public bool Perform(Vector3 dir)
        {
            if (trait == null || !Ready || (_health != null && _health.IsDead)) return false;
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-4f) return false;
            dir.Normalize();
            CooldownLeft = trait.cooldown;
            Casts++;
            _from = transform.position + Vector3.up * 1.1f;
            _to = transform.position + dir * trait.throwDistance;
            var em = EnemyManager.Instance;
            if (em != null && em.Height != null) _to.y = em.Height.Sample(_to.x, _to.z);
            _flightT = 0f;
            _holdT = -1f;
            _yaw = GameRandom.Range(0f, 360f);
            SfxPlayer.Play(SfxId.Throw, transform.position);
            return true;
        }

        public void Tick(float dt, float unscaledDt)
        {
            if (trait == null) return;
            if (dt > 0f)
            {
                if (CooldownLeft > 0f) CooldownLeft = Mathf.Max(0f, CooldownLeft - dt);
                if (_flightT >= 0f)
                {
                    _flightT += dt / Mathf.Max(0.01f, trait.flightTime);
                    if (_flightT >= 1f)
                    {
                        _flightT = -1f;
                        Land();
                    }
                }
                else if (_holdT >= 0f)
                {
                    _holdT -= dt;
                    if (_holdT < 0f) _holdT = -1f;
                }
            }
            Draw();
        }

        void Land()
        {
            var em = EnemyManager.Instance;
            float hold = trait.root != null ? trait.root.duration : 2f;
            _holdT = hold;
            SfxPlayer.Play(SfxId.Snare, _to);
            var pop = DeathPopFx.Instance;
            if (pop != null) pop.Burst(_to + Vector3.up * 0.3f, 14, 4f, 0.12f, trait.ring, Color.white);
            if (em == null || RunState.CombatOver) return; // P1: thrown just before the player fell, it lands harmless
            float dmg = trait.damage * (_stats != null ? _stats.Damage : 1f);
            float decay = em.feedback != null ? em.feedback.knockbackDecay : 9f; // a knockback k travels k / decay metres
            int n = em.QueryAlive(_to, trait.radius + em.MaxRadius, _hits);
            for (int q = 0; q < n; q++)
            {
                int i = _hits[q];
                Vector3 to = _to - em.GetPosition(i);
                to.y = 0f;
                float dist = to.magnitude, reach = trait.radius + em.GetRadius(i);
                if (dist > reach) continue;
                var def = em.GetDef(i);
                bool scripted = def != null && def.behaviour == EnemyBehaviour.Scripted;
                int uid = em.GetUid(i);
                Vector3 dir = dist > 1e-3f ? to / dist : Vector3.forward;
                em.ApplyDamage(i, new DamageInfo
                {
                    Amount = dmg, Flags = DamageFlags.Area, Point = em.GetPosition(i), Direction = dir,
                    Knockback = scripted ? 0f : dist * trait.pull * decay, SourceId = SourceId,
                });
                if (!em.IsAlive(i) || em.GetUid(i) != uid) { Hauled++; continue; }
                var status = scripted ? trait.slow : trait.root;
                if (status != null) em.ApplyStatus(i, status, 0f);
                Hauled++;
            }
        }

        void Draw()
        {
            if (trait.mesh == null || trait.material == null) return;
            Matrix4x4 m;
            if (_flightT >= 0f)
            {
                float u = _flightT;
                Vector3 p = Vector3.Lerp(_from, _to, u) + Vector3.up * (4f * trait.arcHeight * u * (1f - u));
                float s = trait.meshScale * trait.radius * Mathf.Lerp(0.25f, 1f, u); // it opens as it flies
                m = Matrix4x4.TRS(p, Quaternion.Euler(0f, _yaw + u * 180f, 0f), new Vector3(s, s, s));
                var tele = TelegraphRenderer.Instance;
                if (tele != null) tele.Circle(_to, trait.radius, 1f, trait.ring, TelegraphRenderer.Style.Ring, 0.05f, 0f);
            }
            else if (_holdT >= 0f)
            {
                bool blinkOff = _holdT < 0.5f && Mathf.Repeat(_holdT * 8f, 1f) < 0.4f; // about to tear: blink
                if (blinkOff) return;
                float s = trait.meshScale * trait.radius;
                m = Matrix4x4.TRS(_to + Vector3.up * 0.04f, Quaternion.Euler(0f, _yaw + 180f, 0f), new Vector3(s, s, s));
            }
            else return;
            if (!_rpReady)
            {
                _rp = new RenderParams(trait.material)
                {
                    shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off,
                    receiveShadows = true,
                    worldBounds = new Bounds(Vector3.zero, Vector3.one * 400f),
                };
                _rpReady = true;
            }
            Graphics.RenderMesh(_rp, trait.mesh, 0, m);
        }

        /// <summary>Makes the net ready now (tests, admin).</summary>
        public void Recharge() => CooldownLeft = 0f;
    }
}
