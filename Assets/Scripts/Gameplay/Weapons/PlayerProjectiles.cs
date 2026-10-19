using Gladiator.Core;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>What a player projectile carries (computed by WeaponSystem from the weapon + its level + player stats).</summary>
    public struct ShotParams
    {
        public float Speed, Range, Radius, Damage, CritChance, CritMultiplier, Knockback, Scale;
        public int Pierce, SourceId, Mesh;
    }

    /// <summary>
    /// Every player projectile and thrust visual, one central tick (CLAUDE.md §4: pooled, stepped centrally, hits via
    /// the enemy spatial hash, no physics). Straight = javelin (pierces N enemies; stopped by walls, mounds and its
    /// range). Returning = chakram (decelerates out to its range, then flies back to the player, hitting each enemy once
    /// per leg). Thrust = the spear's visual (follows the player, no hits — WeaponSystem resolves the line hit).
    /// Struct-of-arrays, fixed capacity, instanced draw with the palette material. Zero allocation in steady state.
    /// </summary>
    public sealed class PlayerProjectiles : MonoBehaviour, ITickable
    {
        public enum Kind : byte { Straight, Returning, Thrust }

        public static PlayerProjectiles Instance { get; private set; }

        public int capacity = 256;
        public Material material;
        [Tooltip("Enemies remembered per projectile so it never hits the same one twice per leg.")]
        public int hitMemory = 16;
        [Tooltip("Height above the player's feet that projectiles fly at (m).")]
        public float flightHeight = 0.9f;
        [Tooltip("Chakram return speed as a multiple of its throw speed.")]
        public float returnSpeedMul = 1.3f;
        [Tooltip("Spear thrust: fraction of the visual spent extending (the rest retracts).")]
        public float thrustExtendFraction = 0.35f;
        [Tooltip("Spear mesh: distance from its pivot (grip) to the tip (m, before scale).")]
        public float spearTipOffset = 1.6f;

        const int MaxMeshes = 8;

        Kind[] _kind;
        Vector3[] _pos, _dir;
        float[] _speed, _t, _life, _range, _radius, _damage, _crit, _critMul, _knock, _scale, _spin, _travel;
        int[] _pierceLeft, _source, _mesh, _hitCount, _hits;
        bool[] _used, _back;
        int _high, _count;

        readonly Mesh[] _meshes = new Mesh[MaxMeshes];
        int _meshCount;
        Matrix4x4[][] _matrices;
        int[] _batch, _lastBatch;
        RenderParams _rp;
        readonly int[] _query = new int[256];
        Transform _player;

        public int Count => _count;
        public int Hits { get; private set; }

        void Awake()
        {
            Instance = this;
            _kind = new Kind[capacity];
            _pos = new Vector3[capacity]; _dir = new Vector3[capacity];
            _speed = new float[capacity]; _t = new float[capacity]; _life = new float[capacity]; _range = new float[capacity];
            _radius = new float[capacity]; _damage = new float[capacity]; _crit = new float[capacity]; _critMul = new float[capacity];
            _knock = new float[capacity]; _scale = new float[capacity]; _spin = new float[capacity]; _travel = new float[capacity];
            _pierceLeft = new int[capacity]; _source = new int[capacity]; _mesh = new int[capacity]; _hitCount = new int[capacity];
            _hits = new int[capacity * hitMemory];
            _used = new bool[capacity]; _back = new bool[capacity];
            _matrices = new Matrix4x4[MaxMeshes][];
            for (int m = 0; m < MaxMeshes; m++) _matrices[m] = new Matrix4x4[capacity];
            _batch = new int[MaxMeshes];
            _lastBatch = new int[MaxMeshes];
            _rp = new RenderParams(material)
            {
                shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off,
                receiveShadows = true,
                worldBounds = new Bounds(Vector3.zero, Vector3.one * 400f),
            };
        }

        void OnEnable() => GameLoop.Register(this, TickGroup.Projectiles, 10);

        void OnDisable()
        {
            GameLoop.Unregister(this);
            if (Instance == this) Instance = null;
        }

        /// <summary>Index for drawing `m` (registered once per weapon at equip; -1 = nothing to draw).</summary>
        public int RegisterMesh(Mesh m)
        {
            if (m == null) return -1;
            for (int i = 0; i < _meshCount; i++) if (_meshes[i] == m) return i;
            if (_meshCount >= MaxMeshes) return -1;
            _meshes[_meshCount] = m;
            return _meshCount++;
        }

        int Alloc()
        {
            for (int i = 0; i < capacity; i++)
            {
                if (_used[i]) continue;
                _used[i] = true;
                if (i >= _high) _high = i + 1;
                _count++;
                _t[i] = _travel[i] = _spin[i] = 0f;
                _hitCount[i] = 0;
                _back[i] = false;
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

        void Set(int i, Kind kind, Vector3 from, Vector3 dir, in ShotParams p)
        {
            _kind[i] = kind;
            _pos[i] = from;
            _dir[i] = dir;
            _speed[i] = p.Speed; _range[i] = p.Range; _radius[i] = p.Radius; _damage[i] = p.Damage;
            _crit[i] = p.CritChance; _critMul[i] = p.CritMultiplier; _knock[i] = p.Knockback; _scale[i] = p.Scale;
            _pierceLeft[i] = p.Pierce; _source[i] = p.SourceId; _mesh[i] = p.Mesh;
        }

        /// <summary>A javelin: flies along `dir` (horizontal) from `from`.</summary>
        public bool FireStraight(Vector3 from, Vector3 dir, in ShotParams p)
        {
            int i = Alloc();
            if (i < 0) return false;
            Set(i, Kind.Straight, from, dir, p);
            return true;
        }

        /// <summary>A chakram: out along `dir` to p.Range, then back to the player.</summary>
        public bool FireReturning(Vector3 from, Vector3 dir, in ShotParams p)
        {
            int i = Alloc();
            if (i < 0) return false;
            Set(i, Kind.Returning, from, dir, p);
            _life[i] = 2f * p.Range / Mathf.Max(0.1f, p.Speed); // out-leg time: uniform deceleration to 0 at the range
            return true;
        }

        /// <summary>The spear's thrust visual: the tip reaches `length` along `dir` and retracts over `duration`.</summary>
        public void Thrust(Vector3 dir, float length, float duration, int mesh, float scale)
        {
            int i = Alloc();
            if (i < 0) return;
            var p = new ShotParams { Range = length, Mesh = mesh, Scale = scale };
            Set(i, Kind.Thrust, Vector3.zero, dir, p);
            _life[i] = Mathf.Max(0.05f, duration);
        }

        public void Clear()
        {
            for (int i = 0; i < _high; i++) _used[i] = false;
            _high = _count = 0;
        }

        public void Tick(float dt, float unscaledDt)
        {
            if (dt <= 0f) { Draw(true); return; } // paused: keep the last frame on screen
            if (_player == null && Player.Main != null) _player = Player.Main.transform;
            var em = EnemyManager.Instance;
            Vector3 hand = _player != null ? _player.position + Vector3.up * flightHeight : Vector3.zero;
            for (int k = 0; k < MaxMeshes; k++) _batch[k] = 0;

            for (int i = 0; i < _high; i++)
            {
                if (!_used[i]) continue;
                switch (_kind[i])
                {
                    case Kind.Straight: if (!StepStraight(i, em, dt)) { Free(i); continue; } break;
                    case Kind.Returning: if (!StepReturning(i, em, hand, dt)) { Free(i); continue; } break;
                    case Kind.Thrust: if (!StepThrust(i, hand, dt)) { Free(i); continue; } break;
                }
                AddMatrix(i);
            }
            Draw(false);
        }

        bool StepStraight(int i, EnemyManager em, float dt)
        {
            float step = _speed[i] * dt;
            _pos[i] += _dir[i] * step;
            _travel[i] += step;
            if (_travel[i] >= _range[i]) return false;
            if (em != null)
            {
                var p = _pos[i];
                if (em.Height != null && p.y < em.Height.Sample(p.x, p.z) + 0.15f) return false; // into a mound/ramp
                if (em.Obstacles != null && em.Obstacles.Overlaps(new Vector2(p.x, p.z), _radius[i] * 0.4f)) return false; // a wall/pillar
                if (!HitEnemies(i, em)) return false;
            }
            return true;
        }

        bool StepReturning(int i, EnemyManager em, Vector3 hand, float dt)
        {
            _t[i] += dt;
            _spin[i] += dt * 900f;
            if (!_back[i])
            {
                float tOut = _life[i];
                float v = _speed[i] * Mathf.Max(0f, 1f - _t[i] / tOut);
                _pos[i] += _dir[i] * v * dt;
                if (_t[i] >= tOut) { _back[i] = true; _hitCount[i] = 0; } // a fresh leg: everyone can be hit again
            }
            else
            {
                Vector3 to = hand - _pos[i];
                float d = to.magnitude;
                float v = _speed[i] * returnSpeedMul * Mathf.Min(1f, (_t[i] - _life[i]) * 3f + 0.3f);
                if (d <= Mathf.Max(0.6f, v * dt) || _t[i] > _life[i] + 6f) return false; // caught
                _pos[i] += to / d * v * dt;
            }
            if (em != null) HitEnemies(i, em);
            return true;
        }

        bool StepThrust(int i, Vector3 hand, float dt)
        {
            _t[i] += dt;
            float u = _t[i] / _life[i];
            if (u >= 1f) return false;
            float f = thrustExtendFraction;
            float ext = u < f ? 1f - (1f - u / f) * (1f - u / f) : 1f - (u - f) / (1f - f);
            float tipReach = spearTipOffset * _scale[i];
            _pos[i] = hand + _dir[i] * (Mathf.Lerp(0.2f, _range[i], ext) - tipReach);
            return true;
        }

        /// <summary>Damages enemies touching projectile i. Returns false when a straight shot has used up its pierce.</summary>
        bool HitEnemies(int i, EnemyManager em)
        {
            var p = _pos[i];
            float r = _radius[i];
            int n = em.QueryAlive(p, r + 1f, _query);
            for (int k = 0; k < n; k++)
            {
                int e = _query[k];
                Vector3 ep = em.GetPosition(e);
                float dx = ep.x - p.x, dz = ep.z - p.z, rr = r + em.GetRadius(e);
                if (dx * dx + dz * dz > rr * rr) continue;
                float dy = p.y - ep.y;
                if (dy < -0.6f || dy > 2.4f) continue; // flew over / under it
                int uid = em.GetUid(e);
                if (Remembered(i, uid)) continue;
                Remember(i, uid);
                bool crit = GameRandom.Chance(_crit[i]);
                Vector3 push = _kind[i] == Kind.Straight ? _dir[i] : new Vector3(dx, 0f, dz);
                float pl = push.magnitude;
                em.ApplyDamage(e, new DamageInfo
                {
                    Amount = _damage[i] * (crit ? _critMul[i] : 1f),
                    Flags = DamageFlags.Projectile | (crit ? DamageFlags.Crit : 0),
                    Point = ep,
                    Direction = pl > 1e-4f ? push / pl : _dir[i],
                    Knockback = _knock[i],
                    SourceId = _source[i],
                });
                Hits++;
                if (_kind[i] == Kind.Straight)
                {
                    if (_pierceLeft[i] == 0) return false;
                    if (_pierceLeft[i] > 0) _pierceLeft[i]--;
                }
            }
            return true;
        }

        bool Remembered(int i, int uid)
        {
            int n = Mathf.Min(_hitCount[i], hitMemory), b = i * hitMemory;
            for (int k = 0; k < n; k++) if (_hits[b + k] == uid) return true;
            return false;
        }

        void Remember(int i, int uid)
        {
            _hits[i * hitMemory + _hitCount[i] % hitMemory] = uid;
            _hitCount[i]++;
        }

        void AddMatrix(int i)
        {
            int m = _mesh[i];
            if (m < 0 || m >= _meshCount) return;
            Quaternion rot = _kind[i] == Kind.Returning
                ? Quaternion.Euler(0f, _spin[i], 0f)
                : Quaternion.LookRotation(_dir[i], Vector3.up);
            _matrices[m][_batch[m]++] = Matrix4x4.TRS(_pos[i], rot, Vector3.one * _scale[i]);
        }

        void Draw(bool paused)
        {
            for (int m = 0; m < _meshCount; m++)
            {
                int n = paused ? _lastBatch[m] : _batch[m];
                if (!paused) _lastBatch[m] = n;
                if (n > 0) Graphics.RenderMeshInstanced(_rp, _meshes[m], 0, _matrices[m], n);
            }
        }
    }
}
