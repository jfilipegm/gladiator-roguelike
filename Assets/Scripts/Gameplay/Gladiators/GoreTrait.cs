using Gladiator.Core;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// Asterion's passive (docs/GLADIATORS.md): running into enemies head-first gores them. Every enemy his horns meet
    /// (within `reach` past his capsule, `halfAngle` either side of where he's going, at `minSpeed` or faster) takes
    /// `damage` × the player's Damage and is tossed sideways out of his path (`knockback` × Knockback), each enemy at most
    /// once per `interval`. Their contact damage still lands: a thin line can't stop him, a thick crowd still costs HP.
    /// Scripted enemies (bosses) only take the damage. While Rage has him Unchained, the `unchained*` numbers apply:
    /// slower, wider (all round him), harder and more often.
    /// </summary>
    [CreateAssetMenu(menuName = "Gladiator/Traits/Gore", fileName = "TR_Gore")]
    public sealed class GoreTrait : GladiatorTrait
    {
        [Header("Calm")]
        [Tooltip("Horizontal speed (m/s) he must be moving at to gore.")]
        public float minSpeed = 4f;
        [Tooltip("Metres past his capsule radius (plus the enemy's radius) the horns reach.")]
        public float reach = 0.5f;
        [Tooltip("Degrees either side of his velocity.")]
        [Range(0f, 180f)] public float halfAngle = 60f;
        [Tooltip("The enemy query's centre, metres ahead along his velocity (the hit test itself is from his centre).")]
        public float ahead = 0.35f;
        [Tooltip("Damage before the player's Damage multiplier.")]
        public float damage = 8f;
        [Tooltip("Before the player's Knockback multiplier: the toss, sideways out of his path.")]
        public float knockback = 10f;
        [Tooltip("The toss = sideways + forward × this, normalised.")]
        public float forward = 0.25f;
        [Tooltip("Seconds before the same enemy can be gored again.")]
        public float interval = 1f;
        [Tooltip("Largest height difference (m) between his feet and the enemy's: the horns don't reach the crowd below a ledge.")]
        public float maxHeight = 1.2f;

        [Header("While Unchained (Rage)")]
        public float unchainedMinSpeed = 2.5f;
        public float unchainedReach = 0.8f;
        [Range(0f, 180f)] public float unchainedHalfAngle = 180f;
        public float unchainedDamage = 14f;
        public float unchainedInterval = 0.5f;

        [Header("Look")]
        [Tooltip("The puff of dust at the horns: never the orange-red that means \"this hurts you\".")]
        public Color puff = new Color(0.95f, 0.95f, 0.92f);

        public override void Install(GameObject player)
        {
            var c = player.GetComponent<Gore>();
            if (c == null) c = player.AddComponent<Gore>();
            c.trait = this;
        }
    }

    /// <summary>Runtime half of GoreTrait: the horns, from the motor's velocity, central tick, no allocation.</summary>
    public sealed class Gore : MonoBehaviour, ITickable
    {
        public static Gore Instance { get; private set; }
        public static readonly int SourceId = GameInfo.StableId("gore");
        const float SfxInterval = 0.1f;

        public GoreTrait trait;

        PlayerStats _stats;
        PlayerHealth _health;
        PlayerMotor _motor;
        WeaponSystem _weapons;
        CharacterController _cc;
        EventHandler<KillEvent> _onKill;
        readonly int[] _hits = new int[256];
        // per enemy slot: the clock time it can be gored again, and the uid it was gored as (a recycled slot is a new enemy)
        float[] _next;
        int[] _uid;
        float _clock, _nextSfx;

        /// <summary>Enemies gored this run (bot log, tests).</summary>
        public int Gored { get; private set; }
        /// <summary>Enemies killed by a goring (bot log).</summary>
        public int GoreKills { get; private set; }
        /// <summary>True if the horns met something this frame.</summary>
        public bool Goring { get; private set; }

        void Awake()
        {
            _stats = GetComponent<PlayerStats>();
            _health = GetComponent<PlayerHealth>();
            _motor = GetComponent<PlayerMotor>();
            _weapons = GetComponent<WeaponSystem>();
            _cc = GetComponent<CharacterController>();
            _onKill = OnKill;
        }

        void OnEnable()
        {
            Instance = this;
            GameLoop.Register(this, TickGroup.Player, 60);
            GameEvents.OnKill.Subscribe(_onKill);
        }

        void OnDisable()
        {
            if (Instance == this) Instance = null;
            GameLoop.Unregister(this);
            GameEvents.OnKill.Unsubscribe(_onKill);
            Goring = false;
        }

        void OnKill(in KillEvent e)
        {
            if (e.SourceId == SourceId) GoreKills++;
        }

        bool Halted => (_health != null && _health.IsDead) || (_weapons != null ? _weapons.Halted : RunState.CombatOver);

        public void Tick(float dt, float unscaledDt)
        {
            if (dt <= 0f) return;
            Goring = false;
            _clock += dt;
            var em = EnemyManager.Instance;
            if (trait == null || _motor == null || em == null || Halted) return;

            var rage = Rage.Instance;
            bool unchained = rage != null && rage.IsUnchained;
            float minSpeed = unchained ? trait.unchainedMinSpeed : trait.minSpeed;
            Vector3 v = _motor.HorizontalVelocity;
            v.y = 0f;
            float speed = v.magnitude;
            if (speed < minSpeed || speed < 1e-3f) return;
            Vector3 heading = v / speed;
            float reach = (_cc != null ? _cc.radius : 0.35f) + (unchained ? trait.unchainedReach : trait.reach);
            float half = unchained ? trait.unchainedHalfAngle : trait.halfAngle;
            float cosHalf = half >= 180f ? -2f : Mathf.Cos(half * Mathf.Deg2Rad); // all round: straight behind must pass too
            float interval = unchained ? trait.unchainedInterval : trait.interval;
            float dmg = (unchained ? trait.unchainedDamage : trait.damage) * (_stats != null ? _stats.Damage : 1f);
            float knock = trait.knockback * (_stats != null ? _stats.Knockback : 1f);

            if (_next == null || _next.Length < em.capacity)
            {
                _next = new float[em.capacity]; // once per run: the manager's capacity is fixed
                _uid = new int[em.capacity];
            }

            Vector3 at = transform.position;
            float ahead = Mathf.Max(0f, trait.ahead);
            int n = em.QueryAlive(at + heading * ahead, reach + ahead, _hits);
            for (int k = 0; k < n; k++)
            {
                int i = _hits[k];
                Vector3 ep = em.GetPosition(i);
                if (Mathf.Abs(ep.y - at.y) > trait.maxHeight) continue;
                Vector3 to = ep - at;
                to.y = 0f;
                float dist = to.magnitude;
                float rr = reach + em.GetRadius(i);
                if (dist > rr) continue; // the horns reach `reach` past his capsule (plus the enemy's radius), from his centre
                Vector3 dir = dist > 1e-3f ? to / dist : heading;
                if (Vector3.Dot(dir, heading) < cosHalf) continue;
                int uid = em.GetUid(i);
                if (_uid[i] == uid && _clock < _next[i]) continue;
                _uid[i] = uid;
                _next[i] = _clock + interval;

                var def = em.GetDef(i);
                bool scripted = def != null && def.behaviour == EnemyBehaviour.Scripted;
                Vector3 push;
                if (scripted) push = dir; // a boss isn't tossed: only the damage
                else
                {
                    // out of his path: the part of `to` across his heading (dead ahead, the side from the uid's low bit)
                    Vector3 side = to - heading * Vector3.Dot(to, heading);
                    if (side.sqrMagnitude < 1e-4f) side = new Vector3(heading.z, 0f, -heading.x) * ((uid & 1) == 0 ? 1f : -1f);
                    push = (side.normalized + heading * trait.forward).normalized;
                }
                em.ApplyDamage(i, new DamageInfo
                {
                    Amount = dmg, Flags = DamageFlags.Melee, Point = ep, Direction = push,
                    Knockback = scripted ? 0f : knock, SourceId = SourceId,
                });
                Gored++;
                Goring = true;

                var pop = DeathPopFx.Instance;
                if (pop != null) pop.Burst(ep - dir * em.GetRadius(i) + Vector3.up * 1f, 3, 2.2f, 0.08f, trait.puff, Color.white);
                if (_clock >= _nextSfx)
                {
                    _nextSfx = _clock + SfxInterval;
                    SfxPlayer.Play(SfxId.Hit, ep);
                }
            }
        }
    }
}
