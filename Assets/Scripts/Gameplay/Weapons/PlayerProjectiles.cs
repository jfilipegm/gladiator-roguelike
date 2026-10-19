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
    /// Struct-of-arrays, fixed capacity, instanced draw with the palette material. Javelins and thrusts also get a
    /// white→cyan streak (player VFX language, §5) drawn with the slash material so they read at gameplay distance.
    /// Meshes point along +Z from their pivot; the tip (bounds max Z) is where hits happen. Zero allocation in steady
    /// state.
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
        [Tooltip("Additive streak material (M_FxSlash); none = no streaks.")]
        public Material streakMaterial;
        [Tooltip("Javelin streak: length behind the tip (m) and width (m).")]
        public float streakLength = 2.6f, streakWidth = 0.32f;
        [Tooltip("Spear thrust streak width (m).")]
        public float thrustStreakWidth = 0.45f;
        [Tooltip("Boon sparks: bolt width (m) and how long one stays on screen (s).")]
        public float boltWidth = 0.2f, boltLife = 0.14f;
        [Tooltip("Orbit weapons (flail balls): motion streak behind each ball, width and length (m).")]
        public float orbitStreakWidth = 0.3f, orbitStreakLength = 1.4f;

        const int MaxMeshes = 8;

        Kind[] _kind;
        Vector3[] _pos, _dir;
        float[] _speed, _t, _life, _range, _radius, _damage, _crit, _critMul, _knock, _scale, _spin, _travel;
        int[] _pierceLeft, _source, _mesh, _hitCount, _hits;
        bool[] _used, _back;
        int _high, _count;

        readonly Mesh[] _meshes = new Mesh[MaxMeshes];
        readonly float[] _tip = new float[MaxMeshes];
        int _meshCount;
        Mesh _streakMesh;
        Matrix4x4[] _streaks;
        int _streakBatch, _lastStreakBatch;
        const int MaxBolts = 48;
        readonly Vector3[] _boltFrom = new Vector3[MaxBolts], _boltTo = new Vector3[MaxBolts];
        readonly float[] _boltT = new float[MaxBolts];
        int _boltCount;
        const int MaxOrbiters = 16;
        readonly Vector3[] _orbPos = new Vector3[MaxOrbiters], _orbTravel = new Vector3[MaxOrbiters];
        readonly Quaternion[] _orbRot = new Quaternion[MaxOrbiters];
        readonly float[] _orbScale = new float[MaxOrbiters];
        readonly int[] _orbMesh = new int[MaxOrbiters];
        int _orbCount;
        RenderParams _streakRp;
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
            if (streakMaterial != null)
            {
                _streakMesh = BuildStreakMesh();
                _streaks = new Matrix4x4[capacity];
                // FxSlash with a fixed sweep: arc = the whole ribbon, the head sits at the streak's front edge
                var props = new MaterialPropertyBlock();
                props.SetFloat("_Progress", 0.55f);
                props.SetFloat("_ArcFrac", 1f);
                _streakRp = new RenderParams(streakMaterial)
                {
                    shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off,
                    receiveShadows = false,
                    worldBounds = _rp.worldBounds,
                    matProps = props,
                };
            }
        }

        /// <summary>Two crossed ribbons, z from -1 (tail) to 0 (head), uv laid out for FxSlash at _Progress 0.55.</summary>
        static Mesh BuildStreakMesh()
        {
            var v = new Vector3[12];
            var uv = new Vector2[12];
            var tris = new int[24];
            for (int q = 0; q < 2; q++)
            {
                for (int c = 0; c < 3; c++)
                {
                    float off = (c - 1) * 0.5f;
                    var side = q == 0 ? new Vector3(off, 0f, 0f) : new Vector3(0f, off, 0f);
                    float across = c == 1 ? 0.68f : 0f; // soft edges, full brightness down the middle
                    v[q * 6 + c] = side + new Vector3(0f, 0f, -1f); uv[q * 6 + c] = new Vector2(0.29f, across);
                    v[q * 6 + 3 + c] = side; uv[q * 6 + 3 + c] = new Vector2(0.74f, across);
                }
                for (int c = 0; c < 2; c++)
                {
                    int b = q * 6 + c, t = (q * 2 + c) * 6;
                    tris[t] = b; tris[t + 1] = b + 3; tris[t + 2] = b + 1;
                    tris[t + 3] = b + 1; tris[t + 4] = b + 3; tris[t + 5] = b + 4;
                }
            }
            var m = new Mesh { name = "FX_Streak", vertices = v, uv = uv, triangles = tris };
            m.bounds = new Bounds(new Vector3(0f, 0f, -0.5f), new Vector3(1f, 1f, 1f));
            return m;
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
            _tip[_meshCount] = m.bounds.max.z;
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

        /// <summary>A spark jumping from `from` to `to` (Boon chain lightning): a bright streak for a moment.</summary>
        public void Bolt(Vector3 from, Vector3 to)
        {
            if (_boltCount >= MaxBolts) return;
            _boltFrom[_boltCount] = from;
            _boltTo[_boltCount] = to;
            _boltT[_boltCount++] = boltLife;
        }

        /// <summary>Draws one orbiting weapon piece this frame (a flail ball) with a streak behind it along `travel`.
        /// WeaponSystem calls it every gameplay frame, before this system's tick.</summary>
        public void Orbiter(int mesh, Vector3 pos, Quaternion rot, float scale, Vector3 travel)
        {
            if (_orbCount >= MaxOrbiters) return;
            _orbMesh[_orbCount] = mesh;
            _orbPos[_orbCount] = pos;
            _orbRot[_orbCount] = rot;
            _orbScale[_orbCount] = scale;
            _orbTravel[_orbCount++] = travel;
        }

        void AddOrbiters()
        {
            for (int o = 0; o < _orbCount; o++)
            {
                int m = _orbMesh[o];
                if (m >= 0 && m < _meshCount && _batch[m] < _matrices[m].Length)
                    _matrices[m][_batch[m]++] = Matrix4x4.TRS(_orbPos[o], _orbRot[o], Vector3.one * _orbScale[o]);
                if (_streaks != null && _streakBatch < _streaks.Length && _orbTravel[o].sqrMagnitude > 1e-4f)
                    _streaks[_streakBatch++] = Matrix4x4.TRS(_orbPos[o], Quaternion.LookRotation(_orbTravel[o], Vector3.up),
                        new Vector3(orbitStreakWidth, orbitStreakWidth, orbitStreakLength));
            }
            _orbCount = 0;
        }

        public void Clear()
        {
            _orbCount = 0;
            _boltCount = 0;
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
            _streakBatch = 0;

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
            AddOrbiters();
            StepBolts(dt);
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
            float tip = TipOf(i);
            float reach = Mathf.Lerp(tip * 0.55f, _range[i], ext); // starts held (grip just behind the hand)
            _pos[i] = hand + _dir[i] * (reach - tip);
            if (_streaks != null && u < f * 1.6f) // the lunge: a streak from the hand to the tip while it extends
                _streaks[_streakBatch++] = Matrix4x4.TRS(hand + _dir[i] * reach, Quaternion.LookRotation(_dir[i], Vector3.up),
                    new Vector3(thrustStreakWidth * _scale[i], thrustStreakWidth * _scale[i], reach * 0.9f));
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

        void StepBolts(float dt)
        {
            for (int b = 0; b < _boltCount; b++)
            {
                _boltT[b] -= dt;
                if (_boltT[b] <= 0f)
                {
                    _boltCount--;
                    _boltFrom[b] = _boltFrom[_boltCount]; _boltTo[b] = _boltTo[_boltCount]; _boltT[b] = _boltT[_boltCount];
                    b--;
                    continue;
                }
                if (_streaks == null || _streakBatch >= _streaks.Length) continue;
                Vector3 d = _boltTo[b] - _boltFrom[b];
                float len = d.magnitude;
                if (len < 0.05f) continue;
                float w = boltWidth * (0.7f + 0.6f * (_boltT[b] / boltLife));
                _streaks[_streakBatch++] = Matrix4x4.TRS(_boltTo[b], Quaternion.LookRotation(d / len, Vector3.up), new Vector3(w, w, len * 1.15f));
            }
        }

        float TipOf(int i) => _mesh[i] >= 0 && _mesh[i] < _meshCount ? _tip[_mesh[i]] * _scale[i] : 0f;

        void AddMatrix(int i)
        {
            int m = _mesh[i];
            if (_kind[i] == Kind.Straight && _streaks != null) // streak behind the tip, growing over the first metres
            {
                float len = Mathf.Min(streakLength, _travel[i] + 0.5f);
                _streaks[_streakBatch++] = Matrix4x4.TRS(_pos[i], Quaternion.LookRotation(_dir[i], Vector3.up),
                    new Vector3(streakWidth, streakWidth, len));
            }
            if (m < 0 || m >= _meshCount) return;
            if (_kind[i] == Kind.Returning)
            {
                _matrices[m][_batch[m]++] = Matrix4x4.TRS(_pos[i], Quaternion.Euler(0f, _spin[i], 0f), Vector3.one * _scale[i]);
                return;
            }
            var rot = Quaternion.LookRotation(_dir[i], Vector3.up);
            // javelins: the tip is the hit point (the thrust already placed its pivot)
            var at = _kind[i] == Kind.Straight ? _pos[i] - _dir[i] * TipOf(i) : _pos[i];
            _matrices[m][_batch[m]++] = Matrix4x4.TRS(at, rot, Vector3.one * _scale[i]);
        }

        void Draw(bool paused)
        {
            for (int m = 0; m < _meshCount; m++)
            {
                int n = paused ? _lastBatch[m] : _batch[m];
                if (!paused) _lastBatch[m] = n;
                if (n > 0) Graphics.RenderMeshInstanced(_rp, _meshes[m], 0, _matrices[m], n);
            }
            if (_streaks == null) return;
            int sn = paused ? _lastStreakBatch : _streakBatch;
            if (!paused) _lastStreakBatch = sn;
            if (sn > 0) Graphics.RenderMeshInstanced(_streakRp, _streakMesh, 0, _streaks, sn);
        }
    }
}
