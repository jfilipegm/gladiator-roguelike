using Gladiator.Core;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// The Imperial Arena's chariot races (docs/ARENAS.md §5, docs/tracks/R1-design.md). Every `chariotEvery` seconds from
    /// `chariotFirst`: trumpets twice, "CHARIOTS!", and the whole track (its run-in and run-out included) fills
    /// orange-red over `chariotTelegraph` seconds. Then `chariotCount` quadrigae burst out of the carceres one after the
    /// other (opposite phases), run `chariotLaps` laps counter-clockwise round the spina and leave through the carceres.
    /// They trample everything in the lane, once per pass: the player unless their feet clear `jumpClear`, the horde
    /// (ordinary enemies double: they all die), a boss for a flat `bossDamage`. During a race only the lane's rims stay
    /// lit, plus a bright tongue where each chariot will be in the next `tongueSeconds`. The races run back to back from
    /// the boss's phase `backToBackFromBossPhase` and halt at his fall; one egg on the lap counter tips per race.
    /// Central tick, telegraphs through TelegraphRenderer, enemies through the spatial query, no allocation per frame.
    /// </summary>
    public sealed class ChariotRace : MonoBehaviour, ITickable
    {
        public static ChariotRace Instance { get; private set; }

        [Header("Track (ArenaBuilder copies these from ArenaLayoutDef)")]
        [Tooltip("The turns' radius round the metae (m; 0 = no races). The straights run at z = meta.z ∓ this.")]
        public float trackRadius = 14f;
        [Tooltip("The turns' centres (world XZ). The track runs along X, west to east.")]
        public Vector2 trackWestMeta = new Vector2(-60f, 0f);
        public Vector2 trackEastMeta = new Vector2(40f, 0f);
        [Tooltip("The lane's width (m): all of it hurts.")]
        public float trackWidth = 4f;
        [Tooltip("World x of the stalls inside the carceres: chariots appear at (x, west.z − radius) and vanish at (x, west.z + radius).")]
        public float trackStallX = -97f;

        [Header("Schedule (s, m/s)")]
        [Tooltip("Seconds of scene game time before the first race's telegraph.")]
        public float chariotFirst = 80f;
        [Tooltip("From one race's start (its telegraph) to the next one's (0 = only StartNow and back to back).")]
        public float chariotEvery = 60f;
        public float chariotTelegraph = 3f;
        public float chariotSpeed = 22f;
        [Tooltip("Chariots per race: the n-th leaves its stall n × lap ÷ count later (two = half a lap apart).")]
        public int chariotCount = 2;
        [Tooltip("Laps per race: the last leaves along the north straight's run-out instead of taking the west turn.")]
        public int chariotLaps = 2;

        [Header("Hits")]
        [Tooltip("The hit box's length along the track (m): the 4.44 m model plus a margin. Its width is the lane's.")]
        public float footprint = 5f;
        [Tooltip("Feet higher than this above the lane clear a chariot (m).")]
        public float jumpClear = 1.1f;
        public float playerDamage = 28f;
        [Tooltip("The player is thrown sideways out of the lane (m/s) and up.")]
        public float playerKnockback = 13f;
        public float playerKnockUp = 5f;
        [Tooltip("Per pass; ordinary (non-elite) enemies take ordinaryEnemyMul times this.")]
        public float enemyDamage = 200f;
        public float ordinaryEnemyMul = 2f;
        [Tooltip("A boss (Scripted) takes this, flat: no shield, no knockback.")]
        public float bossDamage = 600f;
        [Tooltip("Survivors are thrown sideways out of the lane (m/s).")]
        public float enemyKnockback = 10f;

        [Header("Back to back and halting")]
        [Tooltip("Back to back: the next telegraph starts this long after the last chariot is back in its stall.")]
        public float backToBackGap = 2f;
        [Tooltip("Back to back while the boss is alive in this phase or later (0 = never).")]
        public int backToBackFromBossPhase = 3;

        [Header("Telegraph")]
        public Color telegraphColor = new Color(1f, 0.32f, 0.12f, 0.95f);
        [Tooltip("During a race: the tongue ahead of each chariot shows where it will be within this many seconds.")]
        public float tongueSeconds = 1.2f;
        [Tooltip("During a race the rest of the lane keeps its rims lit at this alpha.")]
        public float rimAlpha = 0.35f;
        [Tooltip("Quads per half-circle turn.")]
        public int turnChords = 16;

        [Header("Look")]
        [Tooltip("The chariot models (PR_Chariot roots, made by ArenaBuilder). Missing ones run as logic only.")]
        public Transform[] chariots = System.Array.Empty<Transform>();
        [Tooltip("The PR_LapCounter piece (children Egg0…Egg6): one egg tips per race.")]
        public Transform lapCounter;
        [Tooltip("Degrees the chariot leans into the (left) turns.")]
        public float leanDegrees = 8f;
        public float wheelRadius = 0.54f;
        [Tooltip("The spokes' cross repeats every 90°: past ~45° a frame the wheels look like they spin backward.")]
        public float maxWheelDegreesPerFrame = 40f;
        public float gallopHz = 2.6f;
        public float horseBob = 0.08f;
        public float horseRock = 5f;
        public float legSwing = 30f;
        [Tooltip("Seconds between two dust puffs behind each chariot (0 = none).")]
        public float dustEvery = 0.08f;
        public Color dustA = new Color(0.62f, 0.52f, 0.4f), dustB = new Color(0.8f, 0.7f, 0.55f);
        public float eggTipDegrees = 95f;
        public float eggTipSeconds = 0.45f;

        [Header("Feedback")]
        public string announceText = "CHARIOTS!";
        [Tooltip("Seconds between the two trumpet calls.")]
        public float hornGap = 0.5f;
        public float crowdSpikeStart = 0.5f;
        [Tooltip("A pass that tramples this many enemies makes the crowd roar (to crowdSpikePass).")]
        public int roarTrampled = 8;
        public float crowdSpikePass = 0.3f;

        enum Phase : byte { Idle, Telegraph, Running }
        enum Pass : byte { None, Over, Hit }
        const byte Straight = 0, Turn = 1;
        const float PlayerRadius = 0.35f;
        const float CurveSlack = 0.5f;  // the box is square to the chariot; on a 14 m turn the lane bends 0.3 m inside it

        // the path, metres from the stall: the run-in, `laps` × (south straight, east turn, north straight, west turn)
        // minus the last west turn, then the run-out (BuildPath)
        byte[] _kind = System.Array.Empty<byte>();
        float[] _start = System.Array.Empty<float>(), _len = System.Array.Empty<float>(), _phi0 = System.Array.Empty<float>();
        Vector3[] _origin = System.Array.Empty<Vector3>(), _dir = System.Array.Empty<Vector3>();
        int _pieces, _laps;
        float _r, _lap, _length;
        bool _built;

        Phase _phase;
        float _clock, _t, _nextStart, _lastEnd = float.NegativeInfinity, _hornAt;
        float _raceBase = 1e4f;  // added to s for the once-per-pass records, so last race's records never match
        int _hornsLeft;
        bool _halted, _wasBackToBack;

        // per chariot
        float[] _s = System.Array.Empty<float>(), _lean = System.Array.Empty<float>(), _gallop = System.Array.Empty<float>();
        float[] _dustT = System.Array.Empty<float>(), _soundT = System.Array.Empty<float>(), _passS = System.Array.Empty<float>();
        float[] _passClock = System.Array.Empty<float>();
        int[] _passKills = System.Array.Empty<int>();
        bool[] _on = System.Array.Empty<bool>();
        Pass[] _pass = System.Array.Empty<Pass>();
        Vector3[] _pos = System.Array.Empty<Vector3>(), _fwd = System.Array.Empty<Vector3>();
        // per chariot × enemy slot: the last pass that hit it
        float[] _hitS = System.Array.Empty<float>();
        int[] _hitUid = System.Array.Empty<int>();
        readonly int[] _query = new int[512];

        // model parts (per chariot; horses and legs per chariot × 4)
        Transform[] _wheelL = System.Array.Empty<Transform>(), _wheelR = System.Array.Empty<Transform>();
        Transform[] _horse = System.Array.Empty<Transform>(), _legF = System.Array.Empty<Transform>(), _legB = System.Array.Empty<Transform>();
        Vector3[] _horseHome = System.Array.Empty<Vector3>();
        Quaternion[] _horseRot = System.Array.Empty<Quaternion>(), _legFRot = System.Array.Empty<Quaternion>(), _legBRot = System.Array.Empty<Quaternion>();
        Transform[] _eggs = System.Array.Empty<Transform>();
        Quaternion[] _eggHome = System.Array.Empty<Quaternion>();
        float[] _eggT = System.Array.Empty<float>();
        bool[] _eggDown = System.Array.Empty<bool>();
        int _eggNext;

        float _trackY;
        bool _placed;
        Transform _player;
        PlayerHealth _health;
        PlayerMotor _motor;

        // ------------------------------------------------------------------ public state (tests, the bot, the boss)

        public bool Running => _phase == Phase.Running;
        public bool Telegraphing => _phase == Phase.Telegraph;
        /// <summary>Halt() or the boss's fall: no new race until StartNow.</summary>
        public bool Halted => _halted;
        /// <summary>Back to back on demand (tests); the boss's phase turns it on by itself (BackToBackNow).</summary>
        public bool BackToBack { get; set; }
        public bool BackToBackNow
        {
            get
            {
                if (BackToBack) return true;
                var boss = BossController.Active;
                return backToBackFromBossPhase > 0 && boss != null && !boss.Defeated && boss.Alive && boss.Phase >= backToBackFromBossPhase;
            }
        }
        /// <summary>Races started (telegraphs), enemies hit, trampled to death, hits on the player, chariots the player
        /// jumped, hits on a boss (tests, bot log).</summary>
        public int Races { get; private set; }
        public int EnemiesHit { get; private set; }
        public int Kills { get; private set; }
        public int Trampled => Kills;
        public int PlayerHits { get; private set; }
        public int Jumped { get; private set; }
        public int BossHits { get; private set; }
        public int Count => _s.Length;
        /// <summary>Scene game time of the next scheduled race (back to back aside).</summary>
        public float NextStart => _nextStart;
        public float Clock => _clock;
        public int EggsDown
        {
            get { int n = 0; for (int i = 0; i < _eggDown.Length; i++) if (_eggDown[i]) n++; return n; }
        }
        public int EggCount => _eggs.Length;

        public bool ChariotActive(int c) => c >= 0 && c < _on.Length && _on[c];
        public Vector3 ChariotPosition(int c) => c >= 0 && c < _pos.Length ? _pos[c] : Vector3.zero;
        public Vector3 ChariotForward(int c) => c >= 0 && c < _fwd.Length ? _fwd[c] : Vector3.forward;
        /// <summary>Chariot c's distance along its path (m; negative = still waiting in its stall). During a telegraph,
        /// where it will be when it starts.</summary>
        public float ChariotDistance(int c)
        {
            if (c < 0 || c >= _s.Length) return 0f;
            if (_phase == Phase.Telegraph) return -(chariotTelegraph - _t) * Mathf.Max(0.1f, chariotSpeed) - c * Spacing;
            return _s[c];
        }

        // ------------------------------------------------------------------ path math (pure, no allocation)

        /// <summary>One lap of the loop (m): two straights and two half-circles.</summary>
        public float LapLength { get { EnsurePath(); return _lap; } }
        /// <summary>One chariot's whole run (m): run-in + laps × lap − the last west turn + run-out.</summary>
        public float PathLength { get { EnsurePath(); return _length; } }
        /// <summary>Metres between two chariots of a race (a lap ÷ count).</summary>
        public float Spacing => LapLength / Mathf.Max(1, chariotCount);
        /// <summary>Seconds between two launches.</summary>
        public float LaunchGap => Spacing / Mathf.Max(0.1f, chariotSpeed);
        /// <summary>Seconds one chariot is out of its stall.</summary>
        public float RunSeconds => PathLength / Mathf.Max(0.1f, chariotSpeed);

        /// <summary>Rebuilds the path from the fields (call after changing them; Configure does).</summary>
        public void BuildPath()
        {
            _r = Mathf.Max(0.01f, trackRadius);
            _laps = Mathf.Max(1, chariotLaps);
            int n = 4 * _laps + 1;
            if (_kind.Length != n)
            {
                _kind = new byte[n];
                _start = new float[n];
                _len = new float[n];
                _phi0 = new float[n];
                _origin = new Vector3[n];
                _dir = new Vector3[n];
            }
            var w = new Vector3(trackWestMeta.x, 0f, trackWestMeta.y);
            var e = new Vector3(trackEastMeta.x, 0f, trackEastMeta.y);
            Vector3 sw = w - Vector3.forward * _r, se = e - Vector3.forward * _r;
            Vector3 ne = e + Vector3.forward * _r, nw = w + Vector3.forward * _r;
            var stallIn = new Vector3(trackStallX, 0f, sw.z);
            var stallOut = new Vector3(trackStallX, 0f, nw.z);
            _pieces = 0;
            float s = 0f;
            AddStraight(ref s, stallIn, sw);
            for (int lap = 0; lap < _laps; lap++)
            {
                AddStraight(ref s, sw, se);
                AddTurn(ref s, e, -Mathf.PI * 0.5f); // from the south, round the east, to the north: counter-clockwise
                AddStraight(ref s, ne, nw);
                if (lap < _laps - 1) AddTurn(ref s, w, Mathf.PI * 0.5f);
            }
            AddStraight(ref s, nw, stallOut);
            _length = s;
            _lap = (se - sw).magnitude + (nw - ne).magnitude + 2f * Mathf.PI * _r;
            _built = true;
        }

        void AddStraight(ref float s, Vector3 from, Vector3 to)
        {
            int k = _pieces++;
            Vector3 d = to - from;
            float len = d.magnitude;
            _kind[k] = Straight;
            _start[k] = s;
            _len[k] = len;
            _origin[k] = from;
            _dir[k] = len > 1e-4f ? d / len : Vector3.right;
            s += len;
        }

        void AddTurn(ref float s, Vector3 centre, float phi0)
        {
            int k = _pieces++;
            _kind[k] = Turn;
            _start[k] = s;
            _len[k] = Mathf.PI * _r;
            _origin[k] = centre;
            _phi0[k] = phi0;
            s += _len[k];
        }

        void EnsurePath() { if (!_built) BuildPath(); }

        int PieceAt(float s)
        {
            for (int k = 0; k < _pieces - 1; k++)
                if (s < _start[k] + _len[k]) return k;
            return _pieces - 1;
        }

        Vector3 PiecePoint(int k, float u, out Vector3 forward)
        {
            if (_kind[k] == Straight)
            {
                forward = _dir[k];
                return _origin[k] + _dir[k] * u;
            }
            float phi = _phi0[k] + u / _r;
            float c = Mathf.Cos(phi), sn = Mathf.Sin(phi);
            forward = new Vector3(-sn, 0f, c); // counter-clockwise seen from above (x east, z north)
            return _origin[k] + new Vector3(c, 0f, sn) * _r;
        }

        /// <summary>The point `s` metres along a chariot's path from its stall (y = 0: the caller samples the ground) and
        /// its heading there. Clamped to the path.</summary>
        public Vector3 PointAt(float s, out Vector3 forward)
        {
            EnsurePath();
            s = Mathf.Clamp(s, 0f, _length);
            int k = PieceAt(s);
            return PiecePoint(k, s - _start[k], out forward);
        }

        /// <summary>Distance (XZ) from `p` to piece k's centre line, and where along the path that nearest point is.</summary>
        float PieceNearest(int k, Vector3 p, out float sigma)
        {
            if (_kind[k] == Straight)
            {
                Vector3 o = _origin[k], d = _dir[k];
                float u = Mathf.Clamp((p.x - o.x) * d.x + (p.z - o.z) * d.z, 0f, _len[k]);
                sigma = _start[k] + u;
                float dx = p.x - (o.x + d.x * u), dz = p.z - (o.z + d.z * u);
                return Mathf.Sqrt(dx * dx + dz * dz);
            }
            Vector3 c = _origin[k];
            float rx = p.x - c.x, rz = p.z - c.z;
            float span = _len[k] / _r;
            float rel = Mathf.Repeat(Mathf.Atan2(rz, rx) - _phi0[k], 2f * Mathf.PI);
            if (rel <= span)
            {
                sigma = _start[k] + rel * _r;
                return Mathf.Abs(Mathf.Sqrt(rx * rx + rz * rz) - _r);
            }
            // past either end of the half-circle: the nearer end point
            Vector3 a = PiecePoint(k, 0f, out _), b = PiecePoint(k, _len[k], out _);
            float da = (new Vector3(p.x - a.x, 0f, p.z - a.z)).magnitude, db = (new Vector3(p.x - b.x, 0f, p.z - b.z)).magnitude;
            sigma = da <= db ? _start[k] : _start[k] + _len[k];
            return Mathf.Min(da, db);
        }

        /// <summary>Distance (XZ) from `p` to the nearest point of the track's centre line, run-in and run-out included.</summary>
        public float LaneOffset(Vector3 p)
        {
            EnsurePath();
            float best = float.PositiveInfinity;
            for (int k = 0; k < _pieces; k++)
            {
                float d = PieceNearest(k, p, out _);
                if (d < best) best = d;
            }
            return best;
        }

        /// <summary>True if `p` is on the lane (its half-width grown by `pad` metres).</summary>
        public bool InLane(Vector3 p, float pad) => LaneOffset(p) <= trackWidth * 0.5f + pad;

        /// <summary>Seconds until a chariot of this race reaches `p` (0 = one is on it now); +∞ if none will (no race, or
        /// `p` isn't on the lane). Counts chariots still in their stalls and during the telegraph. For the bot.</summary>
        public float Danger(Vector3 p)
        {
            if (_phase == Phase.Idle || _s.Length == 0) return float.PositiveInfinity;
            EnsurePath();
            float half = footprint * 0.5f + PlayerRadius, reach = trackWidth * 0.5f + PlayerRadius;
            float speed = Mathf.Max(0.1f, chariotSpeed);
            float best = float.PositiveInfinity;
            for (int k = 0; k < _pieces; k++)
            {
                if (_len[k] <= 0f || PieceNearest(k, p, out float sigma) > reach) continue;
                for (int c = 0; c < _s.Length; c++)
                {
                    float sc = ChariotDistance(c);
                    if (sc >= _length) continue; // back in its stall
                    float ds = sigma - sc;
                    if (ds < -half) continue;    // already past
                    float t = Mathf.Max(0f, ds - half) / speed;
                    if (t < best) best = t;
                }
            }
            return best;
        }

        // ------------------------------------------------------------------ lifecycle

        void Awake()
        {
            Instance = this;
            Configure();
        }

        void OnEnable() => GameLoop.Register(this, TickGroup.Enemies, 32); // after FireJets (31)
        void OnDisable() => GameLoop.Unregister(this);
        void OnDestroy() { if (Instance == this) Instance = null; }
        void OnValidate() => _built = false;

        /// <summary>(Re)reads the fields: the path, the per-chariot state, the model parts and the eggs. Awake calls it;
        /// tests that change the fields afterwards call it again (while no race runs).</summary>
        public void Configure()
        {
            BuildPath();
            int n = Mathf.Max(1, chariotCount);
            _s = new float[n];
            _lean = new float[n];
            _gallop = new float[n];
            _dustT = new float[n];
            _soundT = new float[n];
            _passS = new float[n];
            _passClock = new float[n];
            _passKills = new int[n];
            _on = new bool[n];
            _pass = new Pass[n];
            _pos = new Vector3[n];
            _fwd = new Vector3[n];
            _hitS = System.Array.Empty<float>();
            _hitUid = System.Array.Empty<int>();
            _wheelL = new Transform[n];
            _wheelR = new Transform[n];
            _horse = new Transform[n * 4];
            _legF = new Transform[n * 4];
            _legB = new Transform[n * 4];
            _horseHome = new Vector3[n * 4];
            _horseRot = new Quaternion[n * 4];
            _legFRot = new Quaternion[n * 4];
            _legBRot = new Quaternion[n * 4];
            for (int c = 0; c < n; c++)
            {
                _passS[c] = float.NegativeInfinity;
                _fwd[c] = Vector3.right;
                var root = Model(c);
                if (root == null) continue;
                var all = root.GetComponentsInChildren<Transform>(true);
                _wheelL[c] = Find(all, "WheelL");
                _wheelR[c] = Find(all, "WheelR");
                for (int h = 0; h < 4; h++)
                {
                    int i = c * 4 + h;
                    _horse[i] = Find(all, "Horse" + h);
                    _legF[i] = Find(all, "Horse" + h + "LegsF");
                    _legB[i] = Find(all, "Horse" + h + "LegsB");
                    if (_horse[i] != null) { _horseHome[i] = _horse[i].localPosition; _horseRot[i] = _horse[i].localRotation; }
                    if (_legF[i] != null) _legFRot[i] = _legF[i].localRotation;
                    if (_legB[i] != null) _legBRot[i] = _legB[i].localRotation;
                }
                root.gameObject.SetActive(false); // in its stall until it launches
            }
            int eggs = 0;
            var parts = lapCounter != null ? lapCounter.GetComponentsInChildren<Transform>(true) : System.Array.Empty<Transform>();
            while (eggs < 16 && Find(parts, "Egg" + eggs) != null) eggs++;
            _eggs = new Transform[eggs];
            _eggHome = new Quaternion[eggs];
            _eggT = new float[eggs];
            _eggDown = new bool[eggs];
            for (int i = 0; i < eggs; i++) { _eggs[i] = Find(parts, "Egg" + i); _eggHome[i] = _eggs[i].localRotation; }
            _eggNext = 0;
            _phase = Phase.Idle;
            _nextStart = chariotFirst;
        }

        static Transform Find(Transform[] all, string name)
        {
            for (int i = 0; i < all.Length; i++) if (all[i].name == name) return all[i];
            return null;
        }

        Transform Model(int c) => chariots != null && c < chariots.Length ? chariots[c] : null;

        // ------------------------------------------------------------------ control

        /// <summary>A race telegraphs now (tests; also lifts a Halt). False if one is already on.</summary>
        public bool StartNow()
        {
            if (_phase != Phase.Idle) return false;
            EnsurePath();
            _halted = false;
            Begin(scheduled: false);
            return true;
        }

        /// <summary>No new race; the chariots on the track vanish in a dust burst (the boss's fall calls it by itself).</summary>
        public void Halt()
        {
            _halted = true;
            _hornsLeft = 0;
            if (_phase == Phase.Idle) return;
            var pop = DeathPopFx.Instance;
            for (int c = 0; c < _s.Length; c++)
            {
                if (_on[c] && pop != null) pop.Burst(_pos[c] + Vector3.up, 36, 3.2f, 0.32f, dustA, dustB);
                Park(c);
                _s[c] = _length;
            }
            _phase = Phase.Idle;
            _lastEnd = _clock;
        }

        void Begin(bool scheduled)
        {
            _phase = Phase.Telegraph;
            _t = 0f;
            Races++;
            if (chariotEvery <= 0f) _nextStart = float.PositiveInfinity;
            else
            {
                _nextStart = scheduled ? _nextStart + chariotEvery : _clock + chariotEvery;
                if (_nextStart <= _clock) _nextStart = _clock + chariotEvery;
            }
            SfxPlayer.Play2D(SfxId.Horn);
            _hornsLeft = 1;
            _hornAt = _clock + hornGap;
            GameEvents.OnAnnounce.Raise(new AnnounceEvent { Kind = AnnounceKind.Line, Text = announceText, Color = telegraphColor, Seconds = 1.8f });
            if (CrowdExcitement.Instance != null) CrowdExcitement.Instance.Spike(crowdSpikeStart);
            TipEgg();
        }

        void Launch()
        {
            _phase = Phase.Running;
            _raceBase += _length + 100f;
            float gap = Spacing;
            for (int c = 0; c < _s.Length; c++)
            {
                _s[c] = -c * gap; // the n-th waits n × gap ÷ speed in its stall
                _on[c] = false;
                _pass[c] = Pass.None;
                _passS[c] = float.NegativeInfinity;
                _passKills[c] = 0;
                _dustT[c] = 0f;
            }
        }

        void Park(int c)
        {
            _on[c] = false;
            var root = Model(c);
            if (root != null) root.gameObject.SetActive(false);
        }

        void Place()
        {
            var em = EnemyManager.Instance;
            if (em == null || em.Height == null) return;
            _placed = true;
            Vector3 p = PointAt(_len.Length > 0 ? _len[0] + 1f : 0f, out _);
            _trackY = em.Height.Sample(p.x, p.z);
        }

        float Ground(Vector3 p)
        {
            var em = EnemyManager.Instance;
            return em != null && em.Height != null ? em.Height.Sample(p.x, p.z) : _trackY;
        }

        // ------------------------------------------------------------------ tick

        public void Tick(float dt, float unscaledDt)
        {
            if (dt <= 0f || trackRadius <= 0f || _s.Length == 0) return;
            EnsurePath();
            if (!_placed) Place();
            if (_player == null && Player.Main != null)
            {
                _player = Player.Main.transform;
                _health = Player.Main.GetComponent<PlayerHealth>();
                _motor = Player.Main.GetComponent<PlayerMotor>();
            }
            _clock += dt;
            var boss = BossController.Active;
            if (!_halted && boss != null && boss.Defeated) Halt(); // the races stop at his fall
            bool b2b = BackToBackNow;
            if (b2b && !_wasBackToBack) RightEggs();
            _wasBackToBack = b2b;
            if (_hornsLeft > 0 && _clock >= _hornAt) { _hornsLeft--; _hornAt = _clock + hornGap; SfxPlayer.Play2D(SfxId.Horn); }

            switch (_phase)
            {
                case Phase.Idle:
                    if (!_halted && _clock >= (b2b ? _lastEnd + backToBackGap : _nextStart)) Begin(scheduled: !b2b);
                    break;
                case Phase.Telegraph:
                    _t += dt;
                    DrawTrack(Mathf.Clamp01(_t / Mathf.Max(0.01f, chariotTelegraph)), telegraphColor);
                    if (_t >= chariotTelegraph) Launch();
                    break;
                case Phase.Running:
                    Run(dt);
                    break;
            }
            TickEggs(dt);
        }

        void Run(float dt)
        {
            var em = EnemyManager.Instance;
            if (em != null && _hitUid.Length != _s.Length * em.capacity)
            {
                _hitUid = new int[_s.Length * em.capacity];
                _hitS = new float[_hitUid.Length];
            }
            var tele = TelegraphRenderer.Instance;
            float speed = chariotSpeed, half = footprint * 0.5f;
            var tongue = telegraphColor;
            bool racing = false;
            // the tongues first: if the decal budget runs out, they matter more than the rims
            for (int c = 0; c < _s.Length; c++)
            {
                float s = _s[c] += speed * dt;
                if (s >= _length)
                {
                    if (_on[c]) Park(c); // back in its stall
                    continue;
                }
                racing = true;
                float ahead = s + half + speed * tongueSeconds;
                if (tele != null && ahead > 0f) DrawSpan(tele, Mathf.Max(0f, s - half), Mathf.Min(_length, ahead), tongue);
                if (s < 0f) continue; // still waiting in its stall
                if (!_on[c])
                {
                    _on[c] = true;
                    var root = Model(c);
                    if (root != null) root.gameObject.SetActive(true);
                    SfxPlayer.Play(SfxId.Gate, PointAt(0f, out _));
                }
                Vector3 pos = PointAt(s, out Vector3 fwd);
                pos.y = Ground(pos);
                _pos[c] = pos;
                _fwd[c] = fwd;
                Pose(c, s, dt);
                if (em != null) Trample(c, em, s);
                HitPlayer(c, s);
                Dust(c, dt);
            }
            var rim = telegraphColor;
            rim.a *= rimAlpha;
            DrawTrack(0f, rim);
            if (!racing) { _phase = Phase.Idle; _lastEnd = _clock; }
        }

        // ------------------------------------------------------------------ hits

        float PassGap => footprint + trackWidth + 4f; // two hits by one chariot closer than this are the same pass

        void Trample(int c, EnemyManager em, float s)
        {
            float half = footprint * 0.5f, hw = trackWidth * 0.5f;
            Vector3 pos = _pos[c], f = _fwd[c];
            var right = new Vector3(f.z, 0f, -f.x);
            int n = em.QueryAlive(pos, Mathf.Sqrt(half * half + hw * hw) + CurveSlack, _query);
            float odo = _raceBase + s;
            int cap = em.capacity;
            for (int q = 0; q < n; q++)
            {
                int e = _query[q];
                int uid = em.GetUid(e), idx = c * cap + e;
                if (_hitUid[idx] == uid && odo - _hitS[idx] <= PassGap) continue; // once per pass
                Vector3 p = em.GetPosition(e);
                float r = em.GetRadius(e);
                float dx = p.x - pos.x, dz = p.z - pos.z;
                float along = dx * f.x + dz * f.z, lat = dx * right.x + dz * right.z;
                if (Mathf.Abs(along) > half + r || Mathf.Abs(lat) > hw + r + CurveSlack) continue;
                if (LaneOffset(p) > hw + r || p.y - pos.y >= jumpClear) continue;
                _hitUid[idx] = uid;
                _hitS[idx] = odo;
                var def = em.GetDef(e);
                bool boss = def != null && def.behaviour == EnemyBehaviour.Scripted;
                Vector3 side = lat >= 0f ? right : -right;
                // four horses don't care about a shield: a hit it would block lands from above instead (no push)
                bool flat = boss || em.IsBlocking(e, side);
                float amount = boss ? bossDamage : em.IsElite(e) ? enemyDamage : enemyDamage * ordinaryEnemyMul;
                float before = em.GetHp(e);
                em.ApplyDamage(e, new DamageInfo
                {
                    Amount = amount, Flags = DamageFlags.Area, Point = p, Direction = flat ? Vector3.up : side, Knockback = flat ? 0f : enemyKnockback,
                });
                bool dead = !em.IsAlive(e);
                if (!dead && em.GetHp(e) >= before) continue; // invulnerable (a boss mid-roar)
                EnemiesHit++;
                if (boss) BossHits++;
                if (dead) CountKill(c);
                if (_clock >= _soundT[c]) { _soundT[c] = _clock + 0.12f; SfxPlayer.Play(SfxId.Slam, p, 0.8f); }
            }
        }

        void CountKill(int c)
        {
            Kills++;
            if (_clock - _passClock[c] > 1.5f) _passKills[c] = 0;
            _passClock[c] = _clock;
            if (++_passKills[c] == roarTrampled && CrowdExcitement.Instance != null) CrowdExcitement.Instance.Spike(crowdSpikePass);
        }

        void HitPlayer(int c, float s)
        {
            if (_player == null) return;
            Vector3 pp = _player.position, pos = _pos[c], f = _fwd[c];
            var right = new Vector3(f.z, 0f, -f.x);
            float dx = pp.x - pos.x, dz = pp.z - pos.z;
            float along = dx * f.x + dz * f.z, lat = dx * right.x + dz * right.z;
            float hw = trackWidth * 0.5f + PlayerRadius;
            bool inBox = Mathf.Abs(along) <= footprint * 0.5f + PlayerRadius && Mathf.Abs(lat) <= hw + CurveSlack && LaneOffset(pp) <= hw;
            if (!inBox)
            {
                if (_pass[c] == Pass.Over) Jumped++; // it ran under the player's feet
                _pass[c] = Pass.None;
                return;
            }
            float odo = _raceBase + s;
            if (odo - _passS[c] > PassGap) { _passS[c] = odo; _pass[c] = Pass.Over; } // a new pass
            if (_pass[c] != Pass.Over || pp.y - pos.y >= jumpClear) return;
            _pass[c] = Pass.Hit;
            Vector3 away = lat >= 0f ? right : -right; // out of the lane, the nearer way
            if (_health != null && _health.TakeDamage(new DamageInfo { Amount = playerDamage, Flags = DamageFlags.Area, Point = pp, Direction = away }))
            {
                PlayerHits++;
                if (_motor != null) _motor.AddImpulse(away * playerKnockback + Vector3.up * playerKnockUp);
            }
        }

        // ------------------------------------------------------------------ look

        void Pose(int c, float s, float dt)
        {
            int k = PieceAt(s);
            _lean[c] = Mathf.MoveTowards(_lean[c], _kind[k] == Turn ? leanDegrees : 0f, leanDegrees * 4f * dt);
            var root = Model(c);
            if (root == null) return;
            // +z about its own forward tips the top left: into the (counter-clockwise, left) turns
            root.SetPositionAndRotation(_pos[c], Quaternion.LookRotation(_fwd[c]) * Quaternion.Euler(0f, 0f, _lean[c]));
            float deg = Mathf.Min(chariotSpeed * dt / (2f * Mathf.PI * Mathf.Max(0.05f, wheelRadius)) * 360f, maxWheelDegreesPerFrame);
            Vector3 axis = root.right; // + about the axle rolls the top forward
            if (_wheelL[c] != null) _wheelL[c].Rotate(axis, deg, Space.World);
            if (_wheelR[c] != null) _wheelR[c].Rotate(axis, deg, Space.World);
            _gallop[c] += dt * gallopHz * 2f * Mathf.PI;
            for (int h = 0; h < 4; h++)
            {
                int i = c * 4 + h;
                float ph = _gallop[c] + h * 1.3f, sn = Mathf.Sin(ph);
                if (_horse[i] != null)
                    _horse[i].SetLocalPositionAndRotation(_horseHome[i] + Vector3.up * ((0.5f + 0.5f * sn) * horseBob),
                        _horseRot[i] * Quaternion.Euler(Mathf.Sin(ph + 0.8f) * horseRock, 0f, 0f));
                if (_legF[i] != null) _legF[i].localRotation = _legFRot[i] * Quaternion.Euler(sn * legSwing, 0f, 0f);
                if (_legB[i] != null) _legB[i].localRotation = _legBRot[i] * Quaternion.Euler(-sn * legSwing, 0f, 0f);
            }
        }

        void Dust(int c, float dt)
        {
            var pop = DeathPopFx.Instance;
            if (dustEvery <= 0f || pop == null) return;
            _dustT[c] -= dt;
            if (_dustT[c] > 0f) return;
            _dustT[c] += dustEvery;
            pop.Burst(_pos[c] - _fwd[c] * 1.9f + Vector3.up * 0.25f, 2, 1.1f, 0.22f, dustA, dustB);
        }

        /// <summary>The whole lane: the south line (run-in + straight) filling east, the east turn, the north line
        /// (straight + run-out) filling west, and the west turn if a race takes it. `fill` 0 = rims only.</summary>
        void DrawTrack(float fill, Color color)
        {
            var tele = TelegraphRenderer.Instance;
            if (tele == null) return;
            float y = _trackY, w = trackWidth;
            var west = new Vector3(trackWestMeta.x, y, trackWestMeta.y);
            var east = new Vector3(trackEastMeta.x, y, trackEastMeta.y);
            Vector3 se = east - Vector3.forward * _r, ne = east + Vector3.forward * _r;
            var stallIn = new Vector3(trackStallX, y, west.z - _r);
            var stallOut = new Vector3(trackStallX, y, west.z + _r);
            tele.Lane(stallIn, se - stallIn, (se - stallIn).magnitude, w, fill, color);
            Chords(tele, east, -Mathf.PI * 0.5f, fill, color);
            tele.Lane(ne, stallOut - ne, (stallOut - ne).magnitude, w, fill, color);
            if (_laps > 1) Chords(tele, west, Mathf.PI * 0.5f, fill, color);
        }

        /// <summary>A half-circle turn as straight quads, each a little longer so the outer edge has no gaps; they light
        /// one after the other as `fill` grows.</summary>
        void Chords(TelegraphRenderer tele, Vector3 centre, float phi0, float fill, Color color)
        {
            int n = Mathf.Max(2, turnChords);
            float step = Mathf.PI / n, ext = trackWidth * 0.5f * Mathf.Tan(step * 0.5f);
            for (int i = 0; i < n; i++)
            {
                float a = phi0 + step * i, b = a + step;
                var pa = centre + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * _r;
                var pb = centre + new Vector3(Mathf.Cos(b), 0f, Mathf.Sin(b)) * _r;
                Vector3 d = pb - pa;
                float len = d.magnitude;
                d /= len;
                tele.Lane(pa - d * ext, d, len + 2f * ext, trackWidth, Mathf.Clamp01(fill * n - i), color);
            }
        }

        /// <summary>The path from a to b (m from the stall), fully lit: straights as one quad, turns as chords.</summary>
        void DrawSpan(TelegraphRenderer tele, float a, float b, Color color)
        {
            float y = _trackY, chord = Mathf.PI * _r / Mathf.Max(2, turnChords);
            for (int k = 0; k < _pieces; k++)
            {
                float lo = Mathf.Max(a, _start[k]), hi = Mathf.Min(b, _start[k] + _len[k]);
                if (hi - lo < 0.05f) continue;
                if (_kind[k] == Straight)
                {
                    Vector3 from = PiecePoint(k, lo - _start[k], out Vector3 d);
                    from.y = y;
                    tele.Lane(from, d, hi - lo, trackWidth, 1f, color);
                    continue;
                }
                int m = Mathf.Max(1, Mathf.CeilToInt((hi - lo) / chord));
                float step = (hi - lo) / m, ext = trackWidth * 0.5f * Mathf.Tan(step / _r * 0.5f);
                for (int i = 0; i < m; i++)
                {
                    Vector3 pa = PiecePoint(k, lo - _start[k] + step * i, out _), pb = PiecePoint(k, lo - _start[k] + step * (i + 1), out _);
                    pa.y = pb.y = y;
                    Vector3 d = pb - pa;
                    float len = d.magnitude;
                    if (len < 1e-3f) continue;
                    d /= len;
                    tele.Lane(pa - d * ext, d, len + 2f * ext, trackWidth, 1f, color);
                }
            }
        }

        // ------------------------------------------------------------------ the lap counter

        void TipEgg()
        {
            if (_eggs.Length == 0) return;
            if (_eggNext >= _eggs.Length) RightEggs(); // a new set of seven
            _eggDown[_eggNext++] = true;
        }

        void RightEggs()
        {
            for (int i = 0; i < _eggDown.Length; i++) _eggDown[i] = false;
            _eggNext = 0;
        }

        void TickEggs(float dt)
        {
            float rate = dt / Mathf.Max(0.01f, eggTipSeconds);
            for (int i = 0; i < _eggs.Length; i++)
            {
                float target = _eggDown[i] ? 1f : 0f;
                if (_eggT[i] == target || _eggs[i] == null) continue;
                _eggT[i] = Mathf.MoveTowards(_eggT[i], target, rate);
                _eggs[i].localRotation = _eggHome[i] * Quaternion.Euler(eggTipDegrees * Mathf.SmoothStep(0f, 1f, _eggT[i]), 0f, 0f);
            }
        }
    }
}
