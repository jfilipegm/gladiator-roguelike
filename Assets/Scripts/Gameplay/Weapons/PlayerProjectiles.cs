using Gladiator.Core;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>What a player projectile carries (computed by WeaponSystem from the weapon + its level + player stats).</summary>
    public struct ShotParams
    {
        public float Speed, Range, Radius, Damage, CritChance, CritMultiplier, Knockback, Scale;
        public int Pierce, SourceId, Mesh;
        /// <summary>Lob only: seconds in the air, fuse after landing (s), arc apex (m). Radius = blast radius.</summary>
        public float Flight, Fuse, Arc;
        /// <summary>Fireball only: turn rate toward its target (deg/s). Radius = burst radius.</summary>
        public float Homing;
    }

    /// <summary>
    /// Every player projectile and thrust visual, one central tick (CLAUDE.md §4: pooled, stepped centrally, hits via
    /// the enemy spatial hash, no physics). Straight = javelin (pierces N enemies; stopped by walls, mounds and its
    /// range). Returning = chakram (decelerates out to its range, then flies back to the player, hitting each enemy once
    /// per leg). Thrust = the spear's visual (follows the player, no hits — WeaponSystem resolves the line hit).
    /// Lob = a bomb: arcs to a ground point, sits there while a cyan-white fuse ring shrinks, then blasts every enemy in
    /// its radius (never the player). Lunge = the trident's visual (note 34): drawn back, driven out ahead of the hand
    /// and pulled back, with a streak down each prong's lane; no hits (WeaponSystem resolves the lanes). Fireball = the
    /// Ashen Censer's (note 33): a violet comet that steers toward its target, trailing ash and embers, and bursts in a
    /// curse-purple ring on the first enemy it touches (or the ground, a wall, its range), hitting all in its radius.
    /// Struct-of-arrays, fixed capacity, instanced draw with the palette material. Javelins and thrusts also get a
    /// white→cyan streak (player VFX language, §5) drawn with the slash material so they read at gameplay distance.
    /// Meshes point along +Z from their pivot; the tip (bounds max Z) is where hits happen. Zero allocation in steady
    /// state.
    /// </summary>
    public sealed class PlayerProjectiles : MonoBehaviour, ITickable
    {
        public enum Kind : byte { Straight, Returning, Thrust, Lob, Lunge, Fireball }

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
        [Header("Bombs (Lob)")]
        [Tooltip("Landing ring and fuse ring: player colours (cyan-white), never the orange-red of threats.")]
        public Color bombRing = new Color(0.72f, 0.95f, 1f, 0.9f);
        public Color blastColorA = new Color(1f, 0.92f, 0.55f), blastColorB = new Color(0.7f, 0.95f, 1f);
        [Tooltip("Particles for the blast (the shared death-pop system).")]
        public DeathPopFx burst;
        public float blastShake = 0.07f;
        [Tooltip("P1: shots in flight when the player goes down fizzle out in a puff (at most this many puffs).")]
        public int maxFizzles = 24;
        [Header("Shockwaves (GroundSlam, the Verdict)")]
        [Tooltip("A pale ring that runs out from a slam over `shockwaveTime` and fades: player colours, never orange-red.")]
        public Color shockwaveColor = new Color(0.82f, 0.97f, 1f, 0.95f);
        public float shockwaveTime = 0.28f;
        [Header("Trident lunge (MultiHit, note 34)")]
        [Tooltip("How far the grip is drawn back behind the hand before the lunge, and how far ahead of it the grip is driven at full stretch (m).")]
        public float lungeDrawBack = 0.35f, lungeReach = 1.0f;
        [Tooltip("Width of each prong's streak down its lane (m, × the weapon's scale).")]
        public float prongStreakWidth = 0.16f;
        [Tooltip("The prongs' lanes flashed on the ground at the strike: player white-cyan, never orange-red. Alpha 0 = off.")]
        public Color prongLaneColor = new Color(0.75f, 0.97f, 1f, 0.5f);
        public float prongLaneTime = 0.16f;
        [Header("Fireballs (the Ashen Censer, note 33)")]
        [Tooltip("A fireball touches an enemy within this × its scale (m, plus the enemy's radius).")]
        public float fireballContact = 0.3f;
        [Tooltip("Height above an enemy's feet a fireball steers for (m).")]
        public float fireballAimHeight = 0.8f;
        [Tooltip("The violet glow streak behind a fireball: length and width (m). Additive: HDR head and trail colours.")]
        public float fireballTrail = 1.6f, fireballTrailWidth = 0.5f;
        [ColorUsage(false, true)] public Color fireballStreakHead = new Color(1.5f, 0.7f, 2.2f, 1f);
        [ColorUsage(false, true)] public Color fireballStreakTrail = new Color(0.55f, 0.12f, 1.0f, 1f);
        [Tooltip("Ash and ember motes it sheds every this many seconds (the shared particle system).")]
        public float fireballPuffEvery = 0.045f;
        public Color fireballSmoke = new Color(0.36f, 0.32f, 0.42f), fireballEmber = new Color(0.8f, 0.55f, 1f);
        [Tooltip("The burst: a curse-purple ring running out to its radius, and purple / lilac particles. The Oracle's colours, never orange-red.")]
        public Color fireballRing = new Color(0.72f, 0.45f, 1f, 0.95f);
        public Color fireballBurstA = new Color(0.66f, 0.38f, 0.95f), fireballBurstB = new Color(0.88f, 0.78f, 1f);
        public float fireballShake = 0.03f;
        [Tooltip("Roll of a fireball about its flight (deg/s), for show.")]
        public float fireballSpin = 540f;

        const int MaxMeshes = 8;

        Kind[] _kind;
        Vector3[] _pos, _dir, _origin;
        float[] _speed, _t, _life, _range, _radius, _damage, _crit, _critMul, _knock, _scale, _spin, _travel, _fuse, _arc, _homing;
        int[] _pierceLeft, _source, _mesh, _hitCount, _hits, _target, _targetUid, _prongs;
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
        const int MaxOrbiters = 40; // R3: a 7 m Labyrinth Chain is 12 links + the stone, beside a flail's 8 and a loan
        readonly Vector3[] _orbPos = new Vector3[MaxOrbiters], _orbTravel = new Vector3[MaxOrbiters];
        readonly Quaternion[] _orbRot = new Quaternion[MaxOrbiters];
        readonly Vector3[] _orbScale = new Vector3[MaxOrbiters];
        readonly int[] _orbMesh = new int[MaxOrbiters];
        int _orbCount;
        const int MaxShockwaves = 12;
        readonly Vector3[] _waveAt = new Vector3[MaxShockwaves];
        readonly float[] _waveR = new float[MaxShockwaves], _waveT = new float[MaxShockwaves];
        readonly Color[] _waveColor = new Color[MaxShockwaves];
        int _waveCount;
        const int MaxLanes = 24; // the trident's lanes flashed on the ground (3 a lunge, a few lunges at once)
        readonly Vector3[] _laneFrom = new Vector3[MaxLanes], _laneDir = new Vector3[MaxLanes];
        readonly float[] _laneLen = new float[MaxLanes], _laneWidth = new float[MaxLanes], _laneT = new float[MaxLanes];
        int _laneCount;
        RenderParams _streakRp, _curseStreakRp;
        Matrix4x4[] _curseStreaks;
        int _curseBatch, _lastCurseBatch;
        Matrix4x4[][] _matrices;
        int[] _batch, _lastBatch;
        RenderParams _rp;
        readonly int[] _query = new int[256];
        Transform _player;

        public int Count => _count;
        public int Hits { get; private set; }
        /// <summary>Bombs that have gone off (tests).</summary>
        public int Blasts { get; private set; }
        /// <summary>Fireballs that have burst, and where the last one did (tests, bot log).</summary>
        public int Fireballs { get; private set; }
        public Vector3 LastFireballBurst { get; private set; }
        /// <summary>Trident lunges drawn (tests).</summary>
        public int Lunges { get; private set; }

        void Awake()
        {
            Instance = this;
            _kind = new Kind[capacity];
            _pos = new Vector3[capacity]; _dir = new Vector3[capacity]; _origin = new Vector3[capacity];
            _fuse = new float[capacity]; _arc = new float[capacity]; _homing = new float[capacity];
            _target = new int[capacity]; _targetUid = new int[capacity]; _prongs = new int[capacity];
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
                // the fireballs' violet streaks: the same ribbon and material in the Oracle's colours, one more draw call
                _curseStreaks = new Matrix4x4[capacity];
                var curse = new MaterialPropertyBlock();
                curse.SetFloat("_Progress", 0.55f);
                curse.SetFloat("_ArcFrac", 1f);
                curse.SetColor("_Color", fireballStreakHead);
                curse.SetColor("_TrailColor", fireballStreakTrail);
                _curseStreakRp = new RenderParams(streakMaterial)
                {
                    shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off,
                    receiveShadows = false,
                    worldBounds = _rp.worldBounds,
                    matProps = curse,
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

        /// <summary>A bomb: arcs from `from` to the ground point `to` in p.Flight s, then goes off p.Fuse s later.</summary>
        public bool FireLob(Vector3 from, Vector3 to, in ShotParams p)
        {
            int i = Alloc();
            if (i < 0) return false;
            Set(i, Kind.Lob, from, to, p); // _dir holds the landing point
            _origin[i] = from;
            _life[i] = Mathf.Max(0.05f, p.Flight);
            _fuse[i] = Mathf.Max(0.05f, p.Fuse);
            _arc[i] = p.Arc;
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

        /// <summary>
        /// The trident's lunge visual (note 34): over `duration` the trident is drawn back, driven out at `strikeAt`
        /// (0..1 of it: the wind-up, when the lanes hit) and pulled back, held at the hand the whole time; a streak runs
        /// down each of `prongs` parallel lanes, `spacing` m apart, to `length` along `dir`. No hits.
        /// </summary>
        public void Lunge(Vector3 dir, float length, float duration, float strikeAt, int prongs, float spacing, int mesh, float scale)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-6f) return;
            int i = Alloc();
            if (i < 0) return;
            var p = new ShotParams { Range = length, Mesh = mesh, Scale = scale, Radius = spacing };
            Set(i, Kind.Lunge, Vector3.zero, dir.normalized, p);
            _life[i] = Mathf.Max(0.05f, duration);
            _fuse[i] = Mathf.Clamp(strikeAt, 0.05f, 0.9f);
            _prongs[i] = Mathf.Max(1, prongs);
            Lunges++;
        }

        /// <summary>The trident's lanes flashed on the ground for prongLaneTime s: `prongs` lanes `spacing` apart, from
        /// `from` (the player's feet) along `dir`, `length` long and `width` wide.</summary>
        public void ProngLanes(Vector3 from, Vector3 dir, float length, float width, float spacing, int prongs)
        {
            if (prongLaneColor.a <= 0f) return;
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-6f) return;
            dir.Normalize();
            var right = new Vector3(dir.z, 0f, -dir.x);
            for (int k = 0; k < prongs; k++)
            {
                int l = _laneCount < MaxLanes ? _laneCount++ : k; // full: the oldest go
                _laneFrom[l] = from + right * ((k - (prongs - 1) * 0.5f) * spacing);
                _laneDir[l] = dir;
                _laneLen[l] = length;
                _laneWidth[l] = width;
                _laneT[l] = 0f;
            }
        }

        /// <summary>
        /// A fireball from `from` along `dir` (3D, normalised here), steering p.Homing deg/s toward enemy `target` (its
        /// slot and uid, so a recycled slot isn't chased; -1 = none, it flies straight). It bursts on the first enemy it
        /// touches, the ground, a wall, or after p.Range m, hitting everything within p.Radius.
        /// </summary>
        public bool FireFireball(Vector3 from, Vector3 dir, int target, int targetUid, in ShotParams p)
        {
            if (dir.sqrMagnitude < 1e-6f) return false;
            int i = Alloc();
            if (i < 0) return false;
            Set(i, Kind.Fireball, from, dir.normalized, p);
            _homing[i] = p.Homing;
            _target[i] = target;
            _targetUid[i] = targetUid;
            _spin[i] = i * 47f;
            return true;
        }

        /// <summary>A spark jumping from `from` to `to` (Boon chain lightning): a bright streak for a moment.</summary>
        public void Bolt(Vector3 from, Vector3 to)
        {
            if (_boltCount >= MaxBolts) return;
            _boltFrom[_boltCount] = from;
            _boltTo[_boltCount] = to;
            _boltT[_boltCount++] = boltLife;
        }

        /// <summary>Draws one orbiting weapon piece this frame (a flail ball, a chain's link or stone) with a streak behind
        /// it along `travel` (none when zero). WeaponSystem calls it every gameplay frame, before this system's tick.</summary>
        public void Orbiter(int mesh, Vector3 pos, Quaternion rot, float scale, Vector3 travel) =>
            Orbiter(mesh, pos, rot, new Vector3(scale, scale, scale), travel);

        /// <summary>Orbiter with a per-axis scale (a chain's links, stretched along Z to fit the chain exactly).</summary>
        public void Orbiter(int mesh, Vector3 pos, Quaternion rot, Vector3 scale, Vector3 travel)
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
                    _matrices[m][_batch[m]++] = Matrix4x4.TRS(_orbPos[o], _orbRot[o], _orbScale[o]);
                if (_streaks != null && _streakBatch < _streaks.Length && _orbTravel[o].sqrMagnitude > 1e-4f)
                    _streaks[_streakBatch++] = Matrix4x4.TRS(_orbPos[o], Quaternion.LookRotation(_orbTravel[o], Vector3.up),
                        new Vector3(orbitStreakWidth, orbitStreakWidth, orbitStreakLength));
            }
            _orbCount = 0;
        }

        /// <summary>A pale ring that runs out to `radius` round `at` and fades (the cleaver's chop, the Verdict).</summary>
        public void Shockwave(Vector3 at, float radius) => Shockwave(at, radius, shockwaveColor);

        /// <summary>A ring that runs out to `radius` round `at` and fades, in `color` (a fireball's curse burst).</summary>
        public void Shockwave(Vector3 at, float radius, Color color)
        {
            int k = _waveCount < MaxShockwaves ? _waveCount++ : 0; // full: the oldest goes
            _waveAt[k] = at; _waveR[k] = radius; _waveT[k] = 0f; _waveColor[k] = color;
        }

        public int Shockwaves => _waveCount;

        void StepShockwaves(float dt)
        {
            var tele = TelegraphRenderer.Instance;
            for (int k = _waveCount - 1; k >= 0; k--)
            {
                _waveT[k] += dt;
                float u = _waveT[k] / Mathf.Max(0.01f, shockwaveTime);
                if (u >= 1f)
                {
                    _waveCount--;
                    _waveAt[k] = _waveAt[_waveCount]; _waveR[k] = _waveR[_waveCount]; _waveT[k] = _waveT[_waveCount];
                    _waveColor[k] = _waveColor[_waveCount];
                    continue;
                }
                if (tele == null) continue;
                var c = _waveColor[k];
                c.a *= 1f - u * u;
                float r = _waveR[k] * (0.25f + 0.75f * Mathf.Sqrt(u));
                tele.Circle(_waveAt[k], r, 1f, c, TelegraphRenderer.Style.Ring, 0.12f + 0.1f * (1f - u), k * 0.41f);
            }
        }

        /// <summary>The trident's lanes on the ground: full at the strike, fading over prongLaneTime.</summary>
        void StepLanes(float dt)
        {
            var tele = TelegraphRenderer.Instance;
            for (int k = _laneCount - 1; k >= 0; k--)
            {
                _laneT[k] += dt;
                float u = _laneT[k] / Mathf.Max(0.01f, prongLaneTime);
                if (u >= 1f)
                {
                    _laneCount--;
                    _laneFrom[k] = _laneFrom[_laneCount]; _laneDir[k] = _laneDir[_laneCount]; _laneLen[k] = _laneLen[_laneCount];
                    _laneWidth[k] = _laneWidth[_laneCount]; _laneT[k] = _laneT[_laneCount];
                    continue;
                }
                if (tele == null) continue;
                var c = prongLaneColor;
                c.a *= 1f - u * u;
                tele.Lane(_laneFrom[k], _laneDir[k], _laneLen[k], _laneWidth[k], 1f, c, 0.3f);
            }
        }

        public void Clear()
        {
            ClearFlight();
            _orbCount = 0;
        }

        /// <summary>Everything in flight, bolts and shockwaves (not this frame's orbiters).</summary>
        void ClearFlight()
        {
            _waveCount = 0;
            _laneCount = 0;
            _boltCount = 0;
            for (int i = 0; i < _high; i++) _used[i] = false;
            _high = _count = 0;
        }

        public void Tick(float dt, float unscaledDt)
        {
            if (dt <= 0f) { Draw(true); return; } // paused: keep the last frame on screen
            if (RunState.CombatOver) { TickHalted(); return; }
            if (_player == null && Player.Main != null) _player = Player.Main.transform;
            var em = EnemyManager.Instance;
            Vector3 hand = _player != null ? _player.position + Vector3.up * flightHeight : Vector3.zero;
            for (int k = 0; k < MaxMeshes; k++) _batch[k] = 0;
            _streakBatch = _curseBatch = 0;

            for (int i = 0; i < _high; i++)
            {
                if (!_used[i]) continue;
                switch (_kind[i])
                {
                    case Kind.Straight: if (!StepStraight(i, em, dt)) { Free(i); continue; } break;
                    case Kind.Returning: if (!StepReturning(i, em, hand, dt)) { Free(i); continue; } break;
                    case Kind.Thrust: if (!StepThrust(i, hand, dt)) { Free(i); continue; } break;
                    case Kind.Lob: if (!StepLob(i, em, dt)) { Free(i); continue; } break;
                    case Kind.Lunge: if (!StepLunge(i, hand, dt)) { Free(i); continue; } break;
                    case Kind.Fireball: if (!StepFireball(i, em, dt)) { Free(i); continue; } break;
                }
                AddMatrix(i);
            }
            AddOrbiters();
            StepBolts(dt);
            StepShockwaves(dt);
            StepLanes(dt);
            Draw(false);
        }

        /// <summary>
        /// P1: the player went down or the run is over. Whatever is in flight fizzles out at once (no hits, no blasts);
        /// a halted flail's fallen balls (WeaponSystem queues them) are still drawn.
        /// </summary>
        void TickHalted()
        {
            if (_count > 0 || _boltCount > 0 || _waveCount > 0 || _laneCount > 0)
            {
                for (int i = 0, puffs = 0; i < _high && burst != null && puffs < maxFizzles; i++)
                    if (_used[i] && _kind[i] != Kind.Thrust && _kind[i] != Kind.Lunge)
                    {
                        bool curse = _kind[i] == Kind.Fireball;
                        burst.Burst(_pos[i], 4, 1.5f, 0.08f, curse ? fireballSmoke : blastColorB, curse ? fireballEmber : Color.white);
                        puffs++;
                    }
                ClearFlight();
            }
            for (int k = 0; k < MaxMeshes; k++) _batch[k] = 0;
            _streakBatch = _curseBatch = 0;
            AddOrbiters();
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
            if (_streaks != null && u < f * 1.6f && _streakBatch < _streaks.Length) // the lunge: a streak from the hand to the tip while it extends
                _streaks[_streakBatch++] = Matrix4x4.TRS(hand + _dir[i] * reach, Quaternion.LookRotation(_dir[i], Vector3.up),
                    new Vector3(thrustStreakWidth * _scale[i], thrustStreakWidth * _scale[i], reach * 0.9f));
            return true;
        }

        /// <summary>
        /// The trident's lunge (note 34). The grip (the mesh's pivot) is drawn lungeDrawBack behind the hand until
        /// strikeAt × 0.55, driven out to lungeReach ahead of it by strikeAt (the strike), held there a moment, then
        /// pulled back to the hand: the trident never leaves his hands (a javelin does). While it strikes and holds, a
        /// streak shoots from each prong down its lane to the lanes' end and thins away: three parallel tines, never one
        /// spear's line.
        /// </summary>
        bool StepLunge(int i, Vector3 hand, float dt)
        {
            _t[i] += dt;
            float u = _t[i] / _life[i];
            if (u >= 1f) return false;
            float strike = _fuse[i], back = strike * 0.55f, hold = strike + (1f - strike) * 0.3f;
            float grip, run; // run: how far the prongs' streaks have shot down their lanes (0..1; 0 = none)
            if (u < back)
            {
                float k = u / back;
                grip = -lungeDrawBack * k * (2f - k);
                run = 0f;
            }
            else if (u < strike)
            {
                float k = (u - back) / Mathf.Max(1e-3f, strike - back);
                k = 1f - (1f - k) * (1f - k) * (1f - k); // a snap forward
                grip = Mathf.Lerp(-lungeDrawBack, lungeReach, k);
                run = k;
            }
            else if (u < hold)
            {
                grip = lungeReach;
                run = 1f;
            }
            else
            {
                float k = (u - hold) / (1f - hold);
                grip = Mathf.Lerp(lungeReach, 0f, k * k * (3f - 2f * k));
                run = 0f;
            }
            Vector3 dir = _dir[i];
            _pos[i] = hand + dir * grip;
            if (run <= 0f || _streaks == null) return true;
            float from = grip + TipOf(i) * 0.7f, to = Mathf.Max(from + 0.2f, _range[i]);
            float head = Mathf.Lerp(from, to, run), len = head - from + 0.35f;
            float thin = u < strike ? 1f : 1f - 0.7f * (u - strike) / Mathf.Max(1e-3f, hold - strike);
            float w = prongStreakWidth * _scale[i] * thin;
            var right = new Vector3(dir.z, 0f, -dir.x);
            var rot = Quaternion.LookRotation(dir, Vector3.up);
            int n = _prongs[i];
            for (int k = 0; k < n && _streakBatch < _streaks.Length; k++)
            {
                Vector3 side = right * ((k - (n - 1) * 0.5f) * _radius[i]);
                _streaks[_streakBatch++] = Matrix4x4.TRS(hand + dir * head + side, rot, new Vector3(w, w, len));
            }
            return true;
        }

        bool StepLob(int i, EnemyManager em, float dt)
        {
            _t[i] += dt;
            Vector3 to = _dir[i];
            float flight = _life[i], blast = _radius[i];
            var tele = TelegraphRenderer.Instance;
            if (_t[i] < flight)
            {
                float u = _t[i] / flight;
                _pos[i] = Vector3.LerpUnclamped(_origin[i], to, u) + Vector3.up * (_arc[i] * 4f * u * (1f - u));
                _spin[i] += dt * 720f;
                if (tele != null) tele.Circle(to, blast, 1f, bombRing, TelegraphRenderer.Style.Ring, 0.05f, i * 0.37f); // where it lands
                return true;
            }
            float left = flight + _fuse[i] - _t[i];
            _pos[i] = to;
            if (left > 0f)
            {
                if (tele != null)
                {
                    tele.Circle(to, blast, 1f, bombRing, TelegraphRenderer.Style.Ring, 0.05f, i * 0.37f);
                    tele.Circle(to, Mathf.Max(0.15f, blast * left / _fuse[i]), 1f, bombRing, TelegraphRenderer.Style.Ring, 0.12f, i * 0.37f); // the fuse
                }
                return true;
            }
            Explode(i, em);
            return false;
        }

        void Explode(int i, EnemyManager em)
        {
            Vector3 at = _dir[i];
            Blasts++;
            if (burst != null) burst.Burst(at + Vector3.up * 0.4f, 22, 7f, 0.2f, blastColorA, blastColorB);
            SfxPlayer.Play(SfxId.Bomb, at);
            ScreenShake.Add(blastShake);
            if (em != null) BlastHits(i, em, at);
        }

        /// <summary>
        /// A fireball's flight: it turns toward its target (homing deg/s, aimed fireballAimHeight above its feet; a dead
        /// or recycled target and it flies on straight), sheds ash and ember motes, and bursts on the first enemy it
        /// touches, the ground, a wall, or at its range.
        /// </summary>
        bool StepFireball(int i, EnemyManager em, float dt)
        {
            float t0 = _t[i];
            _t[i] += dt;
            _spin[i] += dt * fireballSpin;
            Vector3 dir = _dir[i];
            if (em != null && _target[i] >= 0 && _homing[i] > 0f)
            {
                int tg = _target[i];
                if (em.IsAlive(tg) && em.GetUid(tg) == _targetUid[i])
                {
                    Vector3 to = em.GetPosition(tg) + Vector3.up * fireballAimHeight - _pos[i];
                    float m = to.magnitude;
                    if (m > 1e-3f) dir = Vector3.RotateTowards(dir, to / m, _homing[i] * Mathf.Deg2Rad * dt, 0f);
                }
                else _target[i] = -1;
            }
            _dir[i] = dir;
            float step = _speed[i] * dt;
            _pos[i] += dir * step;
            _travel[i] += step;
            Vector3 p = _pos[i];
            if (burst != null && fireballPuffEvery > 0f && (int)(_t[i] / fireballPuffEvery) != (int)(t0 / fireballPuffEvery))
                burst.Burst(p - dir * (0.3f * _scale[i]), 2, 0.6f, 0.16f * _scale[i], fireballSmoke, fireballEmber);
            if (_travel[i] >= _range[i]) { CurseBurst(i, em); return false; }
            if (em == null) return true;
            if (em.Height != null && p.y < em.Height.Sample(p.x, p.z) + 0.1f) { CurseBurst(i, em); return false; } // into the sand / a mound
            float contact = fireballContact * _scale[i];
            if (em.Obstacles != null && em.Obstacles.Overlaps(new Vector2(p.x, p.z), contact * 0.5f)) { CurseBurst(i, em); return false; } // a wall
            int n = em.QueryAlive(p, contact, _query); // bodies touching it (XZ)
            for (int k = 0; k < n; k++)
            {
                float dy = p.y - em.GetPosition(_query[k]).y;
                if (dy >= -0.6f && dy <= 2.4f) { CurseBurst(i, em); return false; }
            }
            return true;
        }

        /// <summary>A fireball bursts on the ground under it: a curse-purple ring runs out to its radius, purple and
        /// lilac sparks and ash fly, and everything within the radius is hit (its weapon's hit effects curse them).</summary>
        void CurseBurst(int i, EnemyManager em)
        {
            Vector3 at = _pos[i];
            if (em != null && em.Height != null) at.y = em.Height.Sample(at.x, at.z);
            Fireballs++;
            LastFireballBurst = at;
            Shockwave(at, _radius[i], fireballRing);
            if (burst != null)
            {
                burst.Burst(_pos[i], 16, 5f, 0.2f, fireballBurstA, fireballBurstB);
                burst.Burst(at + Vector3.up * 0.3f, 8, 2f, 0.3f, fireballSmoke, fireballBurstA);
            }
            SfxPlayer.Play(SfxId.Explosion, at);
            ScreenShake.Add(fireballShake);
            if (em != null) BlastHits(i, em, at);
        }

        /// <summary>Hits every enemy within projectile i's radius of `at` (a bomb's blast, a fireball's burst).</summary>
        void BlastHits(int i, EnemyManager em, Vector3 at)
        {
            float blast = _radius[i];
            int n = em.QueryAlive(at, blast + em.MaxRadius, _query);
            for (int k = 0; k < n; k++)
            {
                int e = _query[k];
                Vector3 ep = em.GetPosition(e);
                float dx = ep.x - at.x, dz = ep.z - at.z, rr = blast + em.GetRadius(e);
                if (dx * dx + dz * dz > rr * rr) continue;
                float d = Mathf.Sqrt(dx * dx + dz * dz);
                bool crit = GameRandom.Chance(RngStream.Combat, _crit[i]);
                em.ApplyDamage(e, new DamageInfo
                {
                    Amount = _damage[i] * (crit ? _critMul[i] : 1f),
                    Flags = DamageFlags.Area | DamageFlags.Projectile | (crit ? DamageFlags.Crit : 0),
                    Point = ep,
                    Direction = d > 1e-3f ? new Vector3(dx / d, 0f, dz / d) : Vector3.forward,
                    Knockback = _knock[i],
                    SourceId = _source[i],
                });
                Hits++;
            }
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
                bool crit = GameRandom.Chance(RngStream.Combat, _crit[i]);
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
            if (_kind[i] == Kind.Straight && _streaks != null && _streakBatch < _streaks.Length) // streak behind the tip, growing over the first metres
            {
                float len = Mathf.Min(streakLength, _travel[i] + 0.5f);
                _streaks[_streakBatch++] = Matrix4x4.TRS(_pos[i], Quaternion.LookRotation(_dir[i], Vector3.up),
                    new Vector3(streakWidth, streakWidth, len));
            }
            if (_kind[i] == Kind.Fireball)
            {
                var look = Quaternion.LookRotation(_dir[i], Mathf.Abs(_dir[i].y) > 0.98f ? Vector3.forward : Vector3.up);
                if (_curseStreaks != null && _curseBatch < _curseStreaks.Length) // the violet glow behind it
                    _curseStreaks[_curseBatch++] = Matrix4x4.TRS(_pos[i], look,
                        new Vector3(fireballTrailWidth, fireballTrailWidth, Mathf.Min(fireballTrail, _travel[i] + 0.3f)));
                if (m < 0 || m >= _meshCount) return;
                float flicker = 1f + 0.08f * Mathf.Sin(_t[i] * 31f + i); // the flames lick
                _matrices[m][_batch[m]++] = Matrix4x4.TRS(_pos[i], look * Quaternion.Euler(0f, 0f, _spin[i]), Vector3.one * (_scale[i] * flicker));
                return;
            }
            if (m < 0 || m >= _meshCount) return;
            if (_kind[i] == Kind.Lob)
            {
                float landed = _t[i] - _life[i];
                var rotL = landed < 0f ? Quaternion.Euler(_spin[i], _spin[i] * 0.6f, 0f) : Quaternion.identity;
                float pulse = landed < 0f ? 1f : 1f + 0.18f * Mathf.Abs(Mathf.Sin(landed * 30f)); // the fuse fizzing
                _matrices[m][_batch[m]++] = Matrix4x4.TRS(_pos[i], rotL, Vector3.one * (_scale[i] * pulse));
                return;
            }
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
            if (_curseStreaks == null) return;
            int cn = paused ? _lastCurseBatch : _curseBatch;
            if (!paused) _lastCurseBatch = cn;
            if (cn > 0) Graphics.RenderMeshInstanced(_curseStreakRp, _streakMesh, 0, _curseStreaks, cn);
        }
    }
}
