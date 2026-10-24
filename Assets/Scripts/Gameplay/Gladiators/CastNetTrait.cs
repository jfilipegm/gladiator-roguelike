using Gladiator.Core;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// The Net-Weaver's ability (docs/GLADIATORS.md): starting a slide with the net ready flings a weighted net at the
    /// thickest crowd within `throwRange` (along the slide, `throwDistance` ahead, when nobody is in reach). It flies a
    /// high arc for `flightTime` s, balled up and opening as it falls, with a cyan ring where it will land; it lands,
    /// spreads on the sand, and everything within `radius` is hauled toward the middle (a knockback toward the landing
    /// point), takes `damage` × the player's Damage and is Rooted. It lies there `holdTime` s, its rim drawn in cyan:
    /// what it caught stays held until it goes, and until its last Root-duration seconds it snares whatever walks onto
    /// it too. Scripted enemies (bosses) can't be held: they're Slowed instead. Then it blinks and is gone.
    /// Note 34 (v0.28.0 playtest, "the net throw doesn't really work"): it used to land blind, 5 m along the slide (the
    /// way the player escapes, so mostly on empty sand: 12 caught in 8 nets in a 55 s bot run), fly 0.3 s, and lie flat
    /// in rope colours, half sunk in the floor, for only Root's 2 s.
    /// </summary>
    [CreateAssetMenu(menuName = "Gladiator/Traits/Cast Net", fileName = "TR_CastNet")]
    public sealed class CastNetTrait : GladiatorTrait
    {
        public float cooldown = 4f;
        [Tooltip("Nobody within throwRange: how far ahead along the slide the net lands (m).")]
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
        [Tooltip("Root status (what the net holds with; the net refreshes it while it lies there).")]
        public StatusDef root;
        [Tooltip("Slow status for bosses, who can't be held.")]
        public StatusDef slow;

        [Header("Look")]
        public Mesh mesh;
        public Material material;
        [Tooltip("Scale of the net mesh when it's fully open on the sand, per metre of radius (WP_CastNet is 1 m in radius: 1).")]
        public float meshScale = 1f;
        [Tooltip("The landing ring while it flies: player colours, never orange-red.")]
        public Color ring = new Color(0.72f, 0.95f, 1f, 0.7f);

        [Header("Aim and hold (note 34)")]
        [Tooltip("The net goes for the thickest crowd within this range of the player (m); a tie goes the slide's way. Nobody in range: throwDistance ahead along the slide.")]
        public float throwRange = 9f;
        [Tooltip("The nearest enemies weighed as the crowd's centre (each one's neighbours within radius are counted).")]
        [Min(1)] public int aimCandidates = 12;
        [Tooltip("How long the net lies on the sand (s; at least Root's duration). What it caught is held until it goes; until its last Root-duration seconds it also snares whatever walks onto it.")]
        public float holdTime = 3f;
        [Tooltip("Seconds between two snares of what's on the net.")]
        public float snareInterval = 0.25f;
        [Tooltip("In flight it's balled up to this fraction of its size at the throw, opening as it falls.")]
        [Range(0.1f, 1f)] public float flightSize = 0.3f;
        [Tooltip("Landing: it spreads from its flight size past full (an overshoot) over this many seconds, then settles.")]
        public float unfoldTime = 0.18f;
        [Tooltip("On the sand it's flattened to this fraction of its height (the cords lie flat).")]
        public float groundFlatten = 0.7f;
        [Tooltip("Its last seconds on the sand: it blinks (about to tear).")]
        public float blinkTime = 0.5f;
        [Tooltip("Its rim while it holds: player cyan, never orange-red.")]
        public Color holdRing = new Color(0.55f, 0.95f, 1f, 0.75f);
        [Tooltip("A few cyan motes at his back when the net is ready again.")]
        public bool readyPuff = true;

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
        Vector3 _from, _to, _normal = Vector3.up;
        float _flightT = -1f, _holdT = -1f, _landT, _snareT, _yaw;

        public float CooldownLeft { get; private set; }
        public bool Ready => CooldownLeft <= 0f;
        public bool InFlight => _flightT >= 0f;
        /// <summary>The net lies on the sand, holding what's under it.</summary>
        public bool Holding => _holdT >= 0f;
        public int Casts { get; private set; }
        /// <summary>Enemies caught when the nets landed (bot log, tests).</summary>
        public int Hauled { get; private set; }
        /// <summary>Enemies that walked onto a lying net and were caught (bot log, tests).</summary>
        public int Snared { get; private set; }
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

        bool CanThrow => trait != null && Ready && !(_health != null && _health.IsDead);

        /// <summary>Flings the net at the thickest crowd in reach (`dir` = the slide's way: it wins a tie, and with nobody
        /// in reach the net lands throwDistance along it), if it's ready. True if it was thrown.</summary>
        public bool Perform(Vector3 dir)
        {
            if (!CanThrow) return false;
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-4f) return false;
            dir.Normalize();
            if (!AimAtCrowd(dir, out Vector3 at)) at = transform.position + dir * trait.throwDistance;
            Throw(at);
            return true;
        }

        /// <summary>Flings the net to land at `point` (tests, admin), if it's ready. True if it was thrown.</summary>
        public bool CastAt(Vector3 point)
        {
            if (!CanThrow) return false;
            Throw(point);
            return true;
        }

        void Throw(Vector3 point)
        {
            CooldownLeft = trait.cooldown;
            Casts++;
            _from = transform.position + Vector3.up * 1.1f;
            _to = point;
            var em = EnemyManager.Instance;
            if (em != null && em.Height != null) _to.y = em.Height.Sample(_to.x, _to.z);
            _flightT = 0f;
            _holdT = -1f;
            _yaw = GameRandom.Range(0f, 360f);
            SfxPlayer.Play(SfxId.Throw, transform.position);
        }

        /// <summary>
        /// The thickest crowd within throwRange: of the nearest aimCandidates enemies, the one with the most others
        /// within the net's radius (the slide's way breaks a tie); the net lands on the middle of that crowd (the mean
        /// of the enemies within its radius of that one). False if nobody is in reach. No allocation.
        /// </summary>
        bool AimAtCrowd(Vector3 dir, out Vector3 at)
        {
            at = default;
            var em = EnemyManager.Instance;
            if (em == null) return false;
            Vector3 o = transform.position;
            int n = em.QueryAlive(o, trait.throwRange, _hits);
            if (n == 0) return false;
            int c = Mathf.Min(n, Mathf.Clamp(trait.aimCandidates, 1, 32));
            for (int pick = 0; pick < c; pick++) // partial selection: the nearest c first
            {
                int best = pick;
                float bestD = float.MaxValue;
                for (int k = pick; k < n; k++)
                {
                    Vector3 to = em.GetPosition(_hits[k]) - o;
                    float d2 = to.x * to.x + to.z * to.z;
                    if (d2 < bestD) { bestD = d2; best = k; }
                }
                (_hits[pick], _hits[best]) = (_hits[best], _hits[pick]);
            }
            float r2 = trait.radius * trait.radius, bestScore = float.MinValue;
            int centre = -1;
            for (int a = 0; a < c; a++)
            {
                Vector3 pa = em.GetPosition(_hits[a]);
                int count = 0;
                for (int q = 0; q < n; q++)
                {
                    Vector3 pq = em.GetPosition(_hits[q]);
                    if ((pq.x - pa.x) * (pq.x - pa.x) + (pq.z - pa.z) * (pq.z - pa.z) <= r2) count++;
                }
                Vector3 off = pa - o;
                off.y = 0f;
                float m = off.magnitude;
                float score = count + 0.25f * (m > 1e-3f ? Vector3.Dot(off / m, dir) : 0f);
                if (score > bestScore) { bestScore = score; centre = a; }
            }
            Vector3 pc = em.GetPosition(_hits[centre]), sum = Vector3.zero;
            int inside = 0;
            for (int q = 0; q < n; q++)
            {
                Vector3 pq = em.GetPosition(_hits[q]);
                if ((pq.x - pc.x) * (pq.x - pc.x) + (pq.z - pc.z) * (pq.z - pc.z) > r2) continue;
                sum += pq;
                inside++;
            }
            at = inside > 0 ? sum / inside : pc;
            return true;
        }

        public void Tick(float dt, float unscaledDt)
        {
            if (trait == null) return;
            if (dt > 0f)
            {
                if (CooldownLeft > 0f)
                {
                    CooldownLeft = Mathf.Max(0f, CooldownLeft - dt);
                    if (CooldownLeft <= 0f) ReadyAgain();
                }
                if (_flightT >= 0f)
                {
                    _flightT += dt / Mathf.Max(0.01f, trait.flightTime);
                    if (_flightT >= 1f)
                    {
                        _flightT = -1f;
                        Land();
                    }
                }
                else if (_holdT >= 0f) Hold(dt);
            }
            Draw();
        }

        float RootDuration => trait.root != null ? trait.root.duration : 2f;

        void Land()
        {
            var em = EnemyManager.Instance;
            _holdT = Mathf.Max(trait.holdTime, RootDuration);
            _landT = 0f;
            _snareT = trait.snareInterval;
            Settle(em);
            SfxPlayer.Play(SfxId.Snare, _to);
            var pop = DeathPopFx.Instance;
            if (pop != null) pop.Burst(_to + Vector3.up * 0.3f, 14, 4f, 0.12f, trait.ring, Color.white);
            var pp = PlayerProjectiles.Instance;
            if (pp != null) pp.Shockwave(_to, trait.radius); // the net spreading over the sand
            if (em == null || RunState.CombatOver) return; // P1: thrown just before the player fell, it lands harmless
            float dmg = trait.damage * (_stats != null ? _stats.Damage : 1f);
            float decay = em.feedback != null ? em.feedback.knockbackDecay : 9f; // a knockback k travels k / decay metres
            int n = em.QueryAlive(_to, trait.radius, _hits);
            for (int q = 0; q < n; q++)
            {
                int i = _hits[q];
                Vector3 to = _to - em.GetPosition(i);
                to.y = 0f;
                float dist = to.magnitude;
                var def = em.GetDef(i);
                bool scripted = def != null && def.behaviour == EnemyBehaviour.Scripted;
                int uid = em.GetUid(i);
                Vector3 dir = dist > 1e-3f ? to / dist : Vector3.forward;
                em.ApplyDamage(i, new DamageInfo
                {
                    Amount = dmg, Flags = DamageFlags.Area, Point = em.GetPosition(i), Direction = dir,
                    Knockback = scripted ? 0f : dist * trait.pull * decay, SourceId = SourceId,
                });
                Hauled++;
                if (!em.IsAlive(i) || em.GetUid(i) != uid) continue;
                var status = scripted ? trait.slow : trait.root;
                if (status != null) em.ApplyStatus(i, status, 0f);
            }
        }

        /// <summary>
        /// Where it lies: on the ground under its middle, tilted to the slope through four points at 70 % of its radius
        /// (at most 30°), raised to the highest of the five so a mound doesn't swallow it.
        /// </summary>
        void Settle(EnemyManager em)
        {
            _normal = Vector3.up;
            if (em == null || em.Height == null) return;
            var h = em.Height;
            float s = trait.radius * 0.7f;
            float c = h.Sample(_to.x, _to.z);
            float x0 = h.Sample(_to.x - s, _to.z), x1 = h.Sample(_to.x + s, _to.z);
            float z0 = h.Sample(_to.x, _to.z - s), z1 = h.Sample(_to.x, _to.z + s);
            var n = new Vector3(x0 - x1, 2f * s, z0 - z1).normalized;
            _normal = Vector3.Angle(Vector3.up, n) <= 30f ? n : Vector3.Slerp(Vector3.up, n, 30f / Mathf.Max(30f, Vector3.Angle(Vector3.up, n)));
            _to.y = Mathf.Max(c, (x0 + x1 + z0 + z1) * 0.25f);
        }

        /// <summary>
        /// The net on the sand: it counts down; until its last Root-duration seconds it snares everything on it every
        /// snareInterval, and once more as it enters them (a refresh of the Root for what it already holds, so all of it
        /// is held until the net goes, and not longer; a boss is only Slowed), then it's torn away.
        /// </summary>
        void Hold(float dt)
        {
            float before = _holdT;
            _holdT -= dt;
            _landT += dt;
            if (_holdT < 0f)
            {
                _holdT = -1f;
                var pop = DeathPopFx.Instance;
                if (pop != null) pop.Burst(_to + Vector3.up * 0.2f, 10, 2f, 0.1f, trait.ring, Color.white); // torn away
                return;
            }
            float rootTime = RootDuration;
            bool last = before >= rootTime && _holdT < rootTime; // the last snare: its Root ends with the net
            if (_holdT < rootTime && !last) return; // a Root now would outlast the net
            _snareT -= dt;
            if (_snareT > 0f && !last) return;
            _snareT += Mathf.Max(0.05f, trait.snareInterval);
            var em = EnemyManager.Instance;
            if (em == null || RunState.CombatOver) return;
            int n = em.QueryAlive(_to, trait.radius, _hits);
            for (int q = 0; q < n; q++)
            {
                int i = _hits[q];
                var def = em.GetDef(i);
                bool scripted = def != null && def.behaviour == EnemyBehaviour.Scripted;
                var status = scripted ? trait.slow : trait.root;
                if (status == null) continue;
                if (!em.HasStatus(i, status.kind)) Snared++;
                em.ApplyStatus(i, status, 0f);
            }
        }

        void ReadyAgain()
        {
            if (!trait.readyPuff) return;
            var pop = DeathPopFx.Instance;
            if (pop != null) pop.Burst(transform.position + Vector3.up * 1.2f - transform.forward * 0.3f, 8, 1.4f, 0.1f, trait.ring, Color.white);
        }

        void Draw()
        {
            if (trait.mesh == null || trait.material == null) return;
            float size = trait.meshScale * trait.radius;
            Matrix4x4 m;
            var tele = TelegraphRenderer.Instance;
            if (_flightT >= 0f)
            {
                float u = _flightT;
                Vector3 p = Vector3.Lerp(_from, _to, u) + Vector3.up * (4f * trait.arcHeight * u * (1f - u));
                float open = Mathf.Lerp(trait.flightSize, 0.85f, u * u); // balled up at the throw, opening as it falls
                float tumble = 1f - u; // it tumbles, then settles flat for the landing
                var rot = Quaternion.Euler(Mathf.Sin(u * 9f) * 30f * tumble, _yaw + u * 540f, Mathf.Cos(u * 7f) * 25f * tumble);
                float bundle = Mathf.Lerp(1.8f, 1f, u); // a bundle is taller than it is wide
                m = Matrix4x4.TRS(p, rot, new Vector3(size * open, size * open * bundle, size * open));
                if (tele != null) tele.Circle(_to, trait.radius, 1f, trait.ring, TelegraphRenderer.Style.Ring, 0.05f, 0f); // where it lands
            }
            else if (_holdT >= 0f)
            {
                bool blinkOff = _holdT < trait.blinkTime && Mathf.Repeat(_holdT * 8f, 1f) < 0.4f; // about to tear: blink
                if (blinkOff) return;
                float unfold = Mathf.Max(0.01f, trait.unfoldTime);
                float spread = _landT < unfold
                    ? Mathf.Lerp(0.85f, 1.08f, _landT / unfold)
                    : Mathf.Lerp(1.08f, 1f, Mathf.Clamp01((_landT - unfold) / 0.12f));
                float flat = _landT < unfold ? Mathf.Lerp(1f, trait.groundFlatten * 0.7f, _landT / unfold)
                    : Mathf.Lerp(trait.groundFlatten * 0.7f, trait.groundFlatten, Mathf.Clamp01((_landT - unfold) / 0.12f)); // the thump
                var rot = Quaternion.FromToRotation(Vector3.up, _normal) * Quaternion.Euler(0f, _yaw + 540f, 0f);
                m = Matrix4x4.TRS(_to + _normal * 0.03f, rot, new Vector3(size * spread, size * flat, size * spread));
                if (tele != null)
                {
                    var c = trait.holdRing;
                    if (_holdT < RootDuration) c.a *= 0.5f + 0.5f * (_holdT / RootDuration); // loosening
                    tele.Circle(_to, trait.radius, 0f, c, TelegraphRenderer.Style.Ring, 0.07f, 0f);
                }
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
