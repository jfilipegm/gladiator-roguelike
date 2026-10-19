using Gladiator.Core;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// Every enemy projectile, one central tick (CLAUDE.md §4: pooled, stepped centrally, no physics). Two kinds:
    /// straight shots (Slinger stone: hits the player's capsule, stopped by walls and the ground) and lobs (Fire-Pot:
    /// ballistic arc onto a marked spot whose telegraph fills during the flight, then a fire patch). Struct-of-arrays,
    /// fixed capacity, instanced draw with the shared palette material. Zero allocation in steady state.
    /// </summary>
    public sealed class EnemyProjectiles : MonoBehaviour, ITickable
    {
        public enum Kind : byte { Straight = 0, Lob = 1 }

        public static EnemyProjectiles Instance { get; private set; }

        public int capacity = 256;
        public Material material;
        public Mesh stoneMesh;
        public Mesh potMesh;
        public float stoneScale = 1f;
        public float potScale = 1f;
        [Tooltip("Player capsule used for hits: radius, and the segment from feet+bottom to feet+top.")]
        public float playerRadius = 0.35f;
        public float playerBottom = 0.35f, playerTop = 1.35f;
        public Color telegraphColor = new Color(1f, 0.22f, 0.06f, 0.95f);
        public Color fireColor = new Color(1f, 0.45f, 0.12f, 0.95f);
        public HitFeedbackConfig feedback;
        public DeathPopFx burst;

        // ---- state (struct of arrays) ----
        Kind[] _kind;
        Vector3[] _pos, _vel, _from, _to;
        float[] _t, _dur, _radius, _damage, _apex, _zoneRadius, _zoneTime, _zoneDps, _spin;
        bool[] _used;
        int _high, _count;

        Transform _player;
        PlayerHealth _health;
        Matrix4x4[] _stoneM, _potM;
        RenderParams _rp;

        public int Count => _count;

        void Awake()
        {
            Instance = this;
            _kind = new Kind[capacity];
            _pos = new Vector3[capacity]; _vel = new Vector3[capacity]; _from = new Vector3[capacity]; _to = new Vector3[capacity];
            _t = new float[capacity]; _dur = new float[capacity]; _radius = new float[capacity]; _damage = new float[capacity];
            _apex = new float[capacity]; _zoneRadius = new float[capacity]; _zoneTime = new float[capacity]; _zoneDps = new float[capacity];
            _spin = new float[capacity];
            _used = new bool[capacity];
            _stoneM = new Matrix4x4[capacity];
            _potM = new Matrix4x4[capacity];
            _rp = new RenderParams(material)
            {
                shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off,
                receiveShadows = true,
                worldBounds = new Bounds(Vector3.zero, Vector3.one * 400f),
            };
        }

        void OnEnable() => GameLoop.Register(this, TickGroup.Projectiles);

        void OnDisable()
        {
            GameLoop.Unregister(this);
            if (Instance == this) Instance = null;
        }

        int Alloc()
        {
            for (int i = 0; i < capacity; i++)
            {
                if (_used[i]) continue;
                _used[i] = true;
                if (i >= _high) _high = i + 1;
                _count++;
                return i;
            }
            return -1;
        }

        void Free(int i)
        {
            _used[i] = false;
            _count--;
            while (_high > 0 && !_used[_high - 1]) _high--;
        }

        public void Clear()
        {
            for (int i = 0; i < _high; i++) _used[i] = false;
            _high = _count = 0;
        }

        /// <summary>Straight shot from `from` toward `target` (aimed once — no homing). Returns the slot or -1.</summary>
        public int FireStraight(Vector3 from, Vector3 target, float speed, float damage, float radius, float range)
        {
            int i = Alloc();
            if (i < 0) return -1;
            Vector3 d = target - from;
            float len = d.magnitude;
            _kind[i] = Kind.Straight;
            _pos[i] = _from[i] = from;
            _vel[i] = len > 1e-3f ? d / len * speed : Vector3.forward * speed;
            _t[i] = 0f;
            _dur[i] = range / Mathf.Max(0.1f, speed);
            _damage[i] = damage;
            _radius[i] = radius;
            _spin[i] = GameRandom.Range(0f, 360f);
            return i;
        }

        /// <summary>Lob from `from` onto the ground at `target` in `flightTime`; leaves a fire patch there.</summary>
        public int Lob(Vector3 from, Vector3 target, float flightTime, float zoneRadius, float zoneTime, float zoneDps)
        {
            int i = Alloc();
            if (i < 0) return -1;
            var em = EnemyManager.Instance;
            if (em != null && em.Height != null) target.y = em.Height.Sample(target.x, target.z);
            _kind[i] = Kind.Lob;
            _pos[i] = _from[i] = from;
            _to[i] = target;
            _vel[i] = Vector3.zero;
            _t[i] = 0f;
            _dur[i] = Mathf.Max(0.2f, flightTime);
            float dist = new Vector2(target.x - from.x, target.z - from.z).magnitude;
            _apex[i] = Mathf.Max(2f, dist * 0.35f);
            _zoneRadius[i] = zoneRadius;
            _zoneTime[i] = zoneTime;
            _zoneDps[i] = zoneDps;
            _spin[i] = GameRandom.Range(0f, 360f);
            return i;
        }

        int _lastStones, _lastPots;

        public void Tick(float dt, float unscaledDt)
        {
            if (_high == 0) return;
            if (dt <= 0f) { Draw(_lastStones, _lastPots); return; } // paused: keep them on screen
            if (_player == null && Player.Main != null)
            {
                _player = Player.Main.transform;
                _health = Player.Main.GetComponent<PlayerHealth>();
            }
            var em = EnemyManager.Instance;
            var height = em != null ? em.Height : null;
            var obstacles = em != null ? em.Obstacles : null;
            var tele = TelegraphRenderer.Instance;
            var shadows = BlobShadowRenderer.Instance;
            Vector3 pp = _player != null ? _player.position : new Vector3(0f, -1000f, 0f);
            Vector3 segA = pp + Vector3.up * playerBottom, segB = pp + Vector3.up * playerTop;
            int stones = 0, pots = 0;

            for (int i = 0; i < _high; i++)
            {
                if (!_used[i]) continue;
                _t[i] += dt;
                _spin[i] += 540f * dt;

                if (_kind[i] == Kind.Straight)
                {
                    Vector3 p = _pos[i] + _vel[i] * dt;
                    _pos[i] = p;
                    float ground = height != null ? height.Sample(p.x, p.z) : 0f;
                    bool dead = _t[i] >= _dur[i] || p.y < ground + 0.05f
                                || (obstacles != null && obstacles.Overlaps(new Vector2(p.x, p.z), _radius[i]));
                    if (!dead && _health != null && SegmentDistanceSq(p, segA, segB) < Sq(_radius[i] + playerRadius))
                    {
                        Vector3 dir = new Vector3(_vel[i].x, 0f, _vel[i].z).normalized;
                        _health.TakeDamage(new DamageInfo { Amount = _damage[i], Flags = DamageFlags.Projectile, Point = p, Direction = dir });
                        dead = true;
                    }
                    if (dead)
                    {
                        if (burst != null) burst.Burst(p, 5, 2.5f, 0.09f, telegraphColor, new Color(0.55f, 0.5f, 0.45f));
                        Free(i);
                        continue;
                    }
                    if (shadows != null) shadows.Add(p, ground, 0.45f);
                    _stoneM[stones++] = Matrix4x4.TRS(p, Quaternion.Euler(_spin[i], _spin[i] * 0.7f, 0f), Vector3.one * stoneScale);
                }
                else
                {
                    float u = Mathf.Clamp01(_t[i] / _dur[i]);
                    Vector3 p = Vector3.Lerp(_from[i], _to[i], u);
                    p.y += 4f * _apex[i] * u * (1f - u);
                    _pos[i] = p;
                    if (tele != null) tele.Circle(_to[i], _zoneRadius[i], u, telegraphColor, TelegraphRenderer.Style.Telegraph);
                    if (u >= 1f)
                    {
                        if (HazardZones.Instance != null)
                            HazardZones.Instance.Add(_to[i], _zoneRadius[i], _zoneTime[i], _zoneDps[i]);
                            SfxPlayer.Play(SfxId.FirePot, _to[i]);
                        if (burst != null) burst.Burst(_to[i] + Vector3.up * 0.2f, 10, 4f, 0.13f, fireColor, new Color(1f, 0.85f, 0.4f));
                        Free(i);
                        continue;
                    }
                    float ground = height != null ? height.Sample(p.x, p.z) : 0f;
                    if (shadows != null) shadows.Add(p, ground, 0.6f);
                    _potM[pots++] = Matrix4x4.TRS(p, Quaternion.Euler(_spin[i] * 0.5f, _spin[i], 0f), Vector3.one * potScale);
                }
            }

            _lastStones = stones;
            _lastPots = pots;
            Draw(stones, pots);
        }

        void Draw(int stones, int pots)
        {
            if (material == null) return;
            if (stones > 0 && stoneMesh != null) Graphics.RenderMeshInstanced(_rp, stoneMesh, 0, _stoneM, stones);
            if (pots > 0 && potMesh != null) Graphics.RenderMeshInstanced(_rp, potMesh, 0, _potM, pots);
        }

        static float Sq(float x) => x * x;

        static float SegmentDistanceSq(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
            return (a + ab * t - p).sqrMagnitude;
        }

        // ---- test / debug access ----
        public bool IsActive(int i) => i >= 0 && i < _high && _used[i];
        public Vector3 GetPosition(int i) => _pos[i];
    }
}
