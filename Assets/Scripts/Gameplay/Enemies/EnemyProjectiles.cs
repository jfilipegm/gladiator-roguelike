using Gladiator.Core;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// Every enemy projectile, one central tick (CLAUDE.md §4: pooled, stepped centrally, no physics). Three kinds:
    /// straight shots (Slinger stone, Shield Archer arrow: hits the player's capsule, stopped by walls and the ground), lobs (Fire-Pot:
    /// ballistic arc onto a marked spot whose telegraph fills during the flight, then a fire patch) and nets (Net-Thrower: the
    /// same arc, then whoever stands inside on the ground is caught and the net lies there a moment). Struct-of-arrays,
    /// fixed capacity, instanced draw with the shared palette material. Zero allocation in steady state.
    /// </summary>
    public sealed class EnemyProjectiles : MonoBehaviour, ITickable
    {
        public enum Kind : byte { Straight = 0, Lob = 1, Net = 2, NetDown = 3 }

        public static EnemyProjectiles Instance { get; private set; }

        public int capacity = 256;
        public Material material;
        public Mesh stoneMesh;
        public Mesh potMesh;
        [Tooltip("Shield Archer arrow (points along +Z, drawn along its velocity).")]
        public Mesh arrowMesh;
        [Tooltip("Net-Thrower's net (WP_Net: flat, ~1.35 m across): balled up in flight, spread over the ring where it lands.")]
        public Mesh netMesh;
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
        bool[] _used, _arrow;
        int _high, _count;

        Transform _player;
        PlayerHealth _health;
        PlayerMotor _motor;
        Matrix4x4[] _stoneM, _potM, _arrowM, _netM;
        RenderParams _rp;

        public int Count => _count;
        /// <summary>Nets that caught the player this run (tests, bot log).</summary>
        public int NetsCaught { get; private set; }

        void Awake()
        {
            Instance = this;
            _kind = new Kind[capacity];
            _pos = new Vector3[capacity]; _vel = new Vector3[capacity]; _from = new Vector3[capacity]; _to = new Vector3[capacity];
            _t = new float[capacity]; _dur = new float[capacity]; _radius = new float[capacity]; _damage = new float[capacity];
            _apex = new float[capacity]; _zoneRadius = new float[capacity]; _zoneTime = new float[capacity]; _zoneDps = new float[capacity];
            _spin = new float[capacity];
            _used = new bool[capacity];
            _arrow = new bool[capacity];
            _stoneM = new Matrix4x4[capacity];
            _arrowM = new Matrix4x4[capacity];
            _potM = new Matrix4x4[capacity];
            _netM = new Matrix4x4[capacity];
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

        /// <summary>Straight shot from `from` toward `target` (aimed once — no homing). `arrow` draws the Shield Archer's
        /// arrow along the flight instead of a tumbling stone. Returns the slot or -1.</summary>
        public int FireStraight(Vector3 from, Vector3 target, float speed, float damage, float radius, float range, bool arrow = false)
        {
            int i = Alloc();
            if (i < 0) return -1;
            _arrow[i] = arrow;
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
            _arrow[i] = false;
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

        /// <summary>A net lobbed onto the ground at `target` (ringed in orange-red during the flight): a player standing inside
        /// when it lands takes `damage` and moves at `slow` × speed for `holdTime` s. A jumping player is never caught.</summary>
        public int LobNet(Vector3 from, Vector3 target, float flightTime, float radius, float damage, float slow, float holdTime)
        {
            int i = Lob(from, target, flightTime, radius, holdTime, slow); // a net's zone time = its hold, its dps slot = the slow
            if (i < 0) return -1;
            _kind[i] = Kind.Net;
            _damage[i] = damage;
            return i;
        }

        int _lastStones, _lastPots, _lastArrows, _lastNets;

        public void Tick(float dt, float unscaledDt)
        {
            if (_high == 0) return;
            if (dt <= 0f) { Draw(_lastStones, _lastPots, _lastArrows, _lastNets); return; } // paused: keep them on screen
            if (_player == null && Player.Main != null)
            {
                _player = Player.Main.transform;
                _health = Player.Main.GetComponent<PlayerHealth>();
                _motor = Player.Main.GetComponent<PlayerMotor>();
            }
            var em = EnemyManager.Instance;
            var height = em != null ? em.Height : null;
            var obstacles = em != null ? em.Obstacles : null;
            var tele = TelegraphRenderer.Instance;
            var shadows = BlobShadowRenderer.Instance;
            Vector3 pp = _player != null ? _player.position : new Vector3(0f, -1000f, 0f);
            Vector3 segA = pp + Vector3.up * playerBottom, segB = pp + Vector3.up * playerTop;
            int stones = 0, pots = 0, arrows = 0, nets = 0;

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
                    if (_arrow[i]) _arrowM[arrows++] = Matrix4x4.TRS(p, Quaternion.LookRotation(_vel[i]), Vector3.one);
                    else _stoneM[stones++] = Matrix4x4.TRS(p, Quaternion.Euler(_spin[i], _spin[i] * 0.7f, 0f), Vector3.one * stoneScale);
                }
                else if (_kind[i] == Kind.NetDown)
                {
                    if (_t[i] >= _dur[i]) { Free(i); continue; }
                    float k = _zoneRadius[i] * 2f / 1.35f; // WP_Net is ~1.35 m across
                    _netM[nets++] = Matrix4x4.TRS(_to[i] + Vector3.up * 0.07f, Quaternion.Euler(0f, _spin[i] * 0.05f, 0f), new Vector3(k, 1f, k));
                }
                else
                {
                    float u = Mathf.Clamp01(_t[i] / _dur[i]);
                    Vector3 p = Vector3.Lerp(_from[i], _to[i], u);
                    p.y += 4f * _apex[i] * u * (1f - u);
                    _pos[i] = p;
                    if (tele != null) tele.Circle(_to[i], _zoneRadius[i], u, telegraphColor, TelegraphRenderer.Style.Telegraph);
                    if (u >= 1f && _kind[i] == Kind.Net)
                    {
                        NetLands(i, pp);
                        continue;
                    }
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
                    if (_kind[i] == Kind.Net) _netM[nets++] = Matrix4x4.TRS(p, Quaternion.Euler(_spin[i] * 0.5f, _spin[i], 0f), new Vector3(0.45f, 0.9f, 0.45f));
                    else _potM[pots++] = Matrix4x4.TRS(p, Quaternion.Euler(_spin[i] * 0.5f, _spin[i], 0f), Vector3.one * potScale);
                }
            }

            _lastStones = stones;
            _lastPots = pots;
            _lastArrows = arrows;
            _lastNets = nets;
            Draw(stones, pots, arrows, nets);
        }

        /// <summary>A net lands: a grounded player inside the ring is caught (hurt, slowed to a crawl); the net then lies
        /// spread on the ground for its hold time either way.</summary>
        void NetLands(int i, Vector3 pp)
        {
            float dx = pp.x - _to[i].x, dz = pp.z - _to[i].z, dy = pp.y - _to[i].y;
            bool grounded = _motor == null || _motor.IsGrounded;
            if (_health != null && grounded && dy < 1.2f && dy > -1.2f && dx * dx + dz * dz < Sq(_zoneRadius[i] + playerRadius))
            {
                NetsCaught++;
                float m = Mathf.Sqrt(dx * dx + dz * dz);
                _health.TakeDamage(new DamageInfo { Amount = _damage[i], Flags = DamageFlags.Projectile | DamageFlags.Area, Point = pp, Direction = m > 1e-3f ? new Vector3(dx / m, 0f, dz / m) : Vector3.forward });
                if (_motor != null) _motor.Slow(_zoneDps[i], _zoneTime[i]);
                SfxPlayer.Play(SfxId.Snare, pp);
            }
            else SfxPlayer.Play(SfxId.Throw, _to[i], 0.6f);
            if (burst != null) burst.Burst(_to[i] + Vector3.up * 0.1f, 6, 2.5f, 0.1f, new Color(0.62f, 0.5f, 0.32f), new Color(0.85f, 0.75f, 0.55f));
            _kind[i] = Kind.NetDown;
            _t[i] = 0f;
            _dur[i] = Mathf.Max(0.3f, _zoneTime[i]);
        }

        void Draw(int stones, int pots, int arrows, int nets)
        {
            if (material == null) return;
            if (nets > 0 && netMesh != null) Graphics.RenderMeshInstanced(_rp, netMesh, 0, _netM, nets);
            if (arrows > 0 && arrowMesh != null) Graphics.RenderMeshInstanced(_rp, arrowMesh, 0, _arrowM, arrows);
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
        public bool IsArrow(int i) => _arrow[i];
        public Kind KindOf(int i) => _kind[i];
    }
}
