using Gladiator.Core;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// The Grand Arena's blade sweepers (docs/ARENAS.md §4): a bronze hub in the plaza with a blade at shin height. Now
    /// and then a pair spins up: the circle it sweeps glows orange-red while the blade turns slowly, then it whirls for a
    /// few seconds. Jump it (feet above `jumpClear`) or stay out of the circle. Whatever the blade passes is hit: the
    /// player (knocked along the blade's path) and every enemy (ordinary ones double, a boss a quarter), so it mows the
    /// horde. The Gilded Patron's phase 2 keeps them all spinning. Central tick, blades drawn instanced, no allocation.
    /// </summary>
    public sealed class BladeSweepers : MonoBehaviour, ITickable
    {
        public static BladeSweepers Instance { get; private set; }

        [Tooltip("Hub centres (y = the floor).")]
        public Vector3[] hubs = System.Array.Empty<Vector3>();
        [Tooltip("The blade's reach from the hub's centre (m).")]
        public float bladeLength = 5.5f;
        [Tooltip("The hub itself: nothing inside this is swept (m).")]
        public float hubRadius = 0.7f;

        [Header("Cycle (s)")]
        public float period = 12f;
        public float telegraph = 1.5f;
        public float spin = 5f;
        [Tooltip("Turns per second at full speed.")]
        public float turnsPerSecond = 0.9f;
        [Tooltip("Seconds into the run before the first pair spins up; pairs are half a period apart.")]
        public float firstAfter = 30f;
        [Tooltip("Spin for good (the Gilded Patron's phase 2).")]
        public bool alwaysSpin;

        [Header("Damage")]
        public float playerDamage = 16f;
        public float playerKnockback = 8f;
        public float enemyDamage = 35f;
        public float ordinaryEnemyMul = 2f;
        public float bossMul = 0.25f;
        [Tooltip("Feet this high above the floor clear the blade (m).")]
        public float jumpClear = 0.55f;
        [Tooltip("A body taller than this above the floor is out of reach (m).")]
        public float reachHeight = 1.2f;

        [Header("Look")]
        public Mesh bladeMesh;
        public Material material;
        public Color telegraphColor = new Color(1f, 0.32f, 0.12f, 0.95f);

        float[] _t = System.Array.Empty<float>();      // seconds into each hub's cycle; < 0 = waiting
        float[] _angle = System.Array.Empty<float>();  // degrees, the blade's heading
        float[] _speed = System.Array.Empty<float>();  // degrees per second now
        float[] _playerCd = System.Array.Empty<float>();
        Matrix4x4[] _matrices = System.Array.Empty<Matrix4x4>();
        readonly int[] _query = new int[256];
        RenderParams _rp;
        bool _rpReady, _placed;
        Transform _player;
        PlayerHealth _health;
        PlayerMotor _motor;

        public int Count => hubs.Length;
        public int Spins { get; private set; }
        public int EnemiesHit { get; private set; }
        public int PlayerHits { get; private set; }
        public float Angle(int i) => _angle[i];
        public bool IsTelegraphing(int i) => !alwaysSpin && i >= 0 && i < _t.Length && _t[i] >= 0f && _t[i] < telegraph;
        /// <summary>The blade is up to speed and cuts.</summary>
        public bool IsSpinning(int i) => i >= 0 && i < _speed.Length && _speed[i] >= FullSpeed * 0.5f;
        float FullSpeed => turnsPerSecond * 360f;

        void Awake()
        {
            Instance = this;
            int n = hubs.Length;
            _t = new float[n];
            _angle = new float[n];
            _speed = new float[n];
            _playerCd = new float[n];
            _matrices = new Matrix4x4[Mathf.Max(1, n)];
            for (int i = 0; i < n; i++)
            {
                _t[i] = -(firstAfter + (i % 2) * period * 0.5f); // diagonal pairs (0 & 2, 1 & 3 for four hubs), half a period apart
                _angle[i] = 45f + 90f * i;
            }
        }

        void OnEnable() => GameLoop.Register(this, TickGroup.Enemies, 32);
        void OnDisable() => GameLoop.Unregister(this);
        void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>Hub `i` spins up now (tests).</summary>
        public void Spin(int i) { if (i >= 0 && i < _t.Length) _t[i] = 0f; }

        /// <summary>Every blade spins up now and never stops (the Gilded Patron's phase 2).</summary>
        public void SpinForever()
        {
            alwaysSpin = true;
            for (int i = 0; i < _t.Length; i++) if (_t[i] < 0f || _t[i] >= telegraph + spin) _t[i] = 0f;
        }

        void Place()
        {
            _placed = true;
            var em = EnemyManager.Instance;
            if (em == null || em.Height == null) return;
            for (int i = 0; i < hubs.Length; i++) hubs[i].y = em.Height.Sample(hubs[i].x, hubs[i].z);
        }

        public void Tick(float dt, float unscaledDt)
        {
            if (dt <= 0f || hubs.Length == 0) return;
            if (!_placed) Place();
            if (_player == null && Player.Main != null)
            {
                _player = Player.Main.transform;
                _health = Player.Main.GetComponent<PlayerHealth>();
                _motor = Player.Main.GetComponent<PlayerMotor>();
            }
            var tele = TelegraphRenderer.Instance;
            var em = EnemyManager.Instance;
            for (int i = 0; i < hubs.Length; i++)
            {
                _t[i] += dt;
                if (!alwaysSpin && _t[i] >= period) _t[i] -= period;
                float t = _t[i];
                float want;
                if (t < 0f) want = 0f;
                else if (t < telegraph)
                {
                    want = FullSpeed * 0.12f * (t / telegraph); // turning slowly: it's coming
                    if (tele != null) tele.Circle(hubs[i], bladeLength, t / telegraph, telegraphColor, TelegraphRenderer.Style.Telegraph, 0.07f);
                }
                else if (alwaysSpin || t < telegraph + spin) want = FullSpeed;
                else want = 0f;
                if (_speed[i] < FullSpeed * 0.5f && want >= FullSpeed) Spins++;
                _speed[i] = Mathf.MoveTowards(_speed[i], want, FullSpeed * (want > _speed[i] ? 2.5f : 1.2f) * dt);
                float a0 = _angle[i];
                _angle[i] = Mathf.Repeat(a0 + _speed[i] * dt, 360f);
                if (IsSpinning(i))
                {
                    if (tele != null) tele.Circle(hubs[i], bladeLength, 1f, telegraphColor, TelegraphRenderer.Style.Ring, 0.1f, i * 0.31f);
                    float swept = _speed[i] * dt;
                    if (em != null) MowEnemies(i, em, a0, swept);
                    HitPlayer(i, a0, swept, dt);
                }
                _matrices[i] = Matrix4x4.TRS(hubs[i], Quaternion.Euler(0f, _angle[i], 0f), Vector3.one);
            }
            Draw(hubs.Length);
        }

        /// <summary>True if the blade, turning from `a0` by `swept` degrees this frame, passed over bearing `bearing`
        /// (degrees, same convention). The frames' arcs tile the circle half-open, so a target is passed once a turn.</summary>
        static bool Passed(float a0, float swept, float bearing) => Mathf.Repeat(bearing - a0, 360f) < swept;

        static float Bearing(Vector3 d) => Mathf.Repeat(Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, 360f);

        void MowEnemies(int i, EnemyManager em, float a0, float swept)
        {
            Vector3 c = hubs[i];
            int n = em.QueryAlive(c, bladeLength + em.MaxRadius, _query);
            for (int k = 0; k < n; k++)
            {
                int e = _query[k];
                Vector3 p = em.GetPosition(e);
                Vector3 d = p - c;
                d.y = 0f;
                float dist = d.magnitude, r = em.GetRadius(e);
                if (dist > bladeLength + r || dist < hubRadius * 0.5f || p.y - c.y > reachHeight) continue;
                if (!Passed(a0, swept, Bearing(d))) continue;
                var def = em.GetDef(e);
                bool boss = def != null && def.behaviour == EnemyBehaviour.Scripted;
                float dmg = enemyDamage * (boss ? bossMul : em.IsElite(e) ? 1f : ordinaryEnemyMul);
                Vector3 along = Quaternion.Euler(0f, a0 + swept, 0f) * Vector3.right; // the way the blade travels
                em.ApplyDamage(e, new DamageInfo { Amount = dmg, Flags = DamageFlags.Area, Point = p, Direction = (along + d.normalized * 0.4f).normalized, Knockback = 6f });
                EnemiesHit++;
            }
        }

        void HitPlayer(int i, float a0, float swept, float dt)
        {
            _playerCd[i] -= dt;
            if (_playerCd[i] > 0f || _player == null || _health == null) return;
            Vector3 c = hubs[i];
            Vector3 pp = _player.position;
            Vector3 d = pp - c;
            d.y = 0f;
            float dist = d.magnitude;
            if (dist > bladeLength + 0.35f || dist < hubRadius * 0.5f) return;
            float feet = pp.y - c.y;
            if (feet > jumpClear || feet < -1f) return; // jumped it
            if (!Passed(a0, swept, Bearing(d))) return;
            _playerCd[i] = 0.5f;
            Vector3 along = Quaternion.Euler(0f, a0 + swept, 0f) * Vector3.right;
            Vector3 dir = (along + d.normalized * 0.6f).normalized;
            if (_health.TakeDamage(new DamageInfo { Amount = playerDamage, Flags = DamageFlags.Area, Point = pp, Direction = dir }))
            {
                PlayerHits++;
                if (_motor != null) _motor.AddImpulse(dir * playerKnockback + Vector3.up * 3.5f);
                SfxPlayer.Play(SfxId.Crit, pp);
            }
        }

        void Draw(int drawn)
        {
            if (bladeMesh == null || material == null || drawn == 0) return;
            if (!_rpReady)
            {
                _rp = new RenderParams(material)
                {
                    shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On,
                    receiveShadows = true,
                    worldBounds = new Bounds(Vector3.zero, Vector3.one * 400f),
                };
                _rpReady = true;
            }
            Graphics.RenderMeshInstanced(_rp, bladeMesh, 0, _matrices, drawn);
        }
    }
}
