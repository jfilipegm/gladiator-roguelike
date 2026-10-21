using Gladiator.Core;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// The Colosseum's hypogeum (docs/ARENAS.md §6, docs/tracks/R2-design.md): the floor that opens. From `dropFirst`,
    /// every `dropEvery` seconds the group of trapdoors with the door nearest the player (never the same group twice
    /// running) telegraphs in orange-red with its leaves rattling, then drops open for `dropOpen` seconds and closes.
    /// Whatever walks onto an open door falls: an ordinary enemy dies (`Execute` with source "hypogeum": the dead don't
    /// reassemble), an elite takes `eliteDamage`, a boss `bossDamage`. The player is never swallowed: an invisible floor
    /// stays over every hole, and a grounded player in an open door is hit once and thrown over the nearer long edge.
    /// From `liftFirst`, every `liftEvery` seconds the lifts nearest the player (but not under them) telegraph, sink and
    /// rise with a pack each (`liftPacks`). In the boss fight it reads `BossController.Active` (the boss never calls it):
    /// the stage's lift follows his rise, drops come faster, his phase 2 drops every door, his phase 3 leaves them all
    /// open (walk-only boxes: enemies walk round them, shots fly over), his guard waves raise the lifts they appear at,
    /// the sun sinks to dusk then last light, and at his fall the doors close one after another and the day comes back.
    /// Central tick, telegraphs through TelegraphRenderer, enemies through the spatial query, no allocation per frame.
    /// </summary>
    public sealed class Hypogeum : MonoBehaviour, ITickable
    {
        public static Hypogeum Instance { get; private set; }

        /// <summary>A fall into an open trapdoor (the source of its kills): EnemyManager's revive rule skips it.</summary>
        public static readonly int SourceId = GameInfo.StableId("hypogeum");

        [Header("Trapdoors (ArenaBuilder copies these from ArenaLayoutDef)")]
        [Tooltip("(x, z, yaw, group): centres at y 0, yaw of the long side (local X), the group that drops together.")]
        public Vector4[] trapdoors = System.Array.Empty<Vector4>();
        [Tooltip("Along local X, across local Z (m).")]
        public Vector2 trapdoorSize = new Vector2(8f, 4f);
        [Tooltip("The trapdoor models (SM_ColTrapdoor roots, one per entry; LeafF / LeafB children swing). Missing ones run as logic only.")]
        public Transform[] doors = System.Array.Empty<Transform>();

        [Header("Lifts")]
        [Tooltip("Lift centres (y = the floor).")]
        public Vector3[] liftSpots = System.Array.Empty<Vector3>();
        public float liftSize = 4f;
        [Tooltip("The lift models (SM_ColLift roots, one per spot; the Platform child moves).")]
        public Transform[] lifts = System.Array.Empty<Transform>();
        [Tooltip("The boss's lift in the stage: its Platform follows him up as he rises.")]
        public bool hasCenterLift;
        public Vector3 centerLiftSpot = new Vector3(0f, 1.2f, 0f);
        public float centerLiftScale = 1.25f;
        public Transform centerLift;

        [Header("Schedule (s, scene game time)")]
        public float dropFirst = 40f;
        public float dropEvery = 14f;
        public float dropTelegraph = 2f;
        public float dropOpen = 4f;
        public float liftFirst = 90f;
        public float liftEvery = 45f;
        [Tooltip("Lifts per wave: the idle ones nearest the player at least liftMinDistance away.")]
        public int liftsPerWave = 3;
        public float liftMinDistance = 12f;
        public ArenaLayoutDef.LiftPack[] liftPacks = System.Array.Empty<ArenaLayoutDef.LiftPack>();

        [Header("Doors")]
        [Tooltip("Seconds a door takes to close.")]
        public float closeTime = 0.6f;
        [Tooltip("How far the leaves swing down into the shaft (deg) and how long it takes (s).")]
        public float leafAngle = 100f;
        public float leafSwing = 0.35f;
        [Tooltip("The leaves bump up this much (deg) while a door telegraphs (and every door during the boss's rise).")]
        public float rattleDeg = 3f;
        public float rattleHz = 9f;

        [Header("Hits")]
        public float playerDamage = 22f;
        [Tooltip("The player is thrown across the door toward the nearer long edge (m/s) and up.")]
        public float playerKnockback = 12f;
        public float playerKnockUp = 5f;
        [Tooltip("Grounded = feet within this of the floor (m): higher feet as a door opens are safe.")]
        public float feetClear = 0.35f;
        [Tooltip("A door left open (the boss's phase 3): stepping in throws you out every heldPushEvery s, hurting at most every heldHitEvery s.")]
        public float heldPlayerDamage = 12f;
        public float heldHitEvery = 1f;
        public float heldPushEvery = 0.35f;
        public float eliteDamage = 300f;
        [Tooltip("An elite that survives its fall is pushed across the door (m/s): it claws its way out.")]
        public float eliteKnockback = 4f;
        [Tooltip("A boss (Scripted) takes this flat, once per opening, no knockback.")]
        public float bossDamage = 500f;

        [Header("Lifts (s, m)")]
        public float liftTelegraph = 1.5f;
        public float liftSink = 0.5f;
        public float liftRise = 0.9f;
        public float liftDepth = 3f;
        [Tooltip("A grounded player on a lift as it starts sinking takes this and is pushed off.")]
        public float liftPlayerDamage = 10f;
        [Tooltip("A pack stands in a ring of this radius on the platform.")]
        public float packRing = 1.2f;

        [Header("The boss fight")]
        [Tooltip("Seconds between drops near the player in his phase 1 / phase 2.")]
        public Vector2 bossDropEvery = new Vector2(12f, 8f);
        [Tooltip("As his roar into this phase ends, every closed door drops (0 = never).")]
        public int openAllOnPhase = 2;
        [Tooltip("As his roar into this phase ends, every door drops and stays open (0 = never).")]
        public int holdOnPhase = 3;
        [Tooltip("At his fall door k closes k × this many seconds after the first.")]
        public float closeStagger = 0.07f;

        [Header("Look")]
        public Color telegraphColor = new Color(1f, 0.32f, 0.12f, 0.95f);
        [Tooltip("An open door's rim stays lit at this alpha (the black hole is the warning), `rimWidth` m wide.")]
        public float rimAlpha = 0.35f;
        public float rimWidth = 0.15f;
        [Tooltip("Rims, dust and leaves only within this distance of the player (m): 30 doors × 4 rims would eat the decal budget.")]
        public float drawHeldWithin = 60f;
        public Color dustA = new Color(0.62f, 0.52f, 0.4f), dustB = new Color(0.8f, 0.7f, 0.55f);

        [Header("Light")]
        public Color duskColor = new Color(1f, 0.62f, 0.42f);
        public float duskIntensity = 0.85f;
        public Color lastLightColor = new Color(0.95f, 0.48f, 0.36f);
        public float lastLightIntensity = 0.72f;
        [Tooltip("Seconds the sun takes to change.")]
        public float lightLerp = 3f;

        [Header("Feedback")]
        public float crowdSpikeWave = 0.5f;
        [Tooltip("A drop that swallows this many makes the crowd roar (to crowdSpikeDrop).")]
        public int roarFallen = 10;
        public float crowdSpikeDrop = 0.4f;
        public float announceSeconds = 1.8f;

        enum DoorState : byte { Closed, Telegraph, Open, Closing, Held }
        enum LiftState : byte { Idle, Telegraph, Sinking, Rising }
        const float PlayerRadius = 0.35f;
        const int MaxDustPerFrame = 12;

        // doors
        DoorState[] _door = System.Array.Empty<DoorState>();
        float[] _t = System.Array.Empty<float>(), _closeAt = System.Array.Empty<float>(), _leaf = System.Array.Empty<float>();
        float[] _setF = System.Array.Empty<float>(), _setB = System.Array.Empty<float>();
        bool[] _hold = System.Array.Empty<bool>(), _playerHit = System.Array.Empty<bool>();
        int[] _serial = System.Array.Empty<int>(), _box = System.Array.Empty<int>(), _group = System.Array.Empty<int>();
        Vector3[] _center = System.Array.Empty<Vector3>(), _ax = System.Array.Empty<Vector3>(), _az = System.Array.Empty<Vector3>();
        Transform[] _leafF = System.Array.Empty<Transform>(), _leafB = System.Array.Empty<Transform>();
        Quaternion[] _leafFBind = System.Array.Empty<Quaternion>(), _leafBBind = System.Array.Empty<Quaternion>();
        int[] _groupFallen = System.Array.Empty<int>();
        int _serialSeq;
        float _reach;
        // per enemy slot: the last opening that took it
        int[] _hitSerial = System.Array.Empty<int>(), _hitUid = System.Array.Empty<int>();
        readonly int[] _query = new int[512];

        // lifts
        LiftState[] _lift = System.Array.Empty<LiftState>();
        float[] _lt = System.Array.Empty<float>(), _platY = System.Array.Empty<float>(), _platSet = System.Array.Empty<float>();
        EnemyDef[] _ldef = System.Array.Empty<EnemyDef>();
        int[] _lcount = System.Array.Empty<int>(), _lcap = System.Array.Empty<int>();
        Vector3[] _lcenter = System.Array.Empty<Vector3>(), _platHome = System.Array.Empty<Vector3>();
        Transform[] _plat = System.Array.Empty<Transform>();
        readonly int[] _pick = new int[16];
        Transform _cplat;
        Vector3 _cplatHome;
        float _cplatY, _cplatSet;

        // schedule and the boss
        float _clock, _nextDrop, _nextWave, _bossNextDrop;
        bool _halted, _bossSeen, _bossDone, _rattleAll;
        int _seenPhase, _seenGuards, _prewarmIndex, _dust;
        bool _prewarmed, _placed, _boxesAdded, _directorLooked;
        Director _director;

        // light
        Light _sun;
        bool _sunLooked;
        Color _dayColor, _lightFrom, _lightTo;
        float _dayIntensity, _intFrom, _intTo, _lightT = 1f;

        Transform _player;
        PlayerHealth _health;
        PlayerMotor _motor;
        float _heldNextHit, _heldNextPush, _centerNextPush;

        // ------------------------------------------------------------------ public state (tests, the bot, the enemies)

        public int Count => _door.Length;
        public int LiftCount => _lift.Length;
        public int GroupCount => _groupFallen.Length;
        public bool IsTelegraphing(int d) => d >= 0 && d < _door.Length && _door[d] == DoorState.Telegraph;
        /// <summary>Open or held open: whatever walks onto it falls.</summary>
        public bool IsOpen(int d) => d >= 0 && d < _door.Length && (_door[d] == DoorState.Open || _door[d] == DoorState.Held);
        public bool IsHeld(int d) => d >= 0 && d < _door.Length && _door[d] == DoorState.Held;
        public bool IsClosing(int d) => d >= 0 && d < _door.Length && _door[d] == DoorState.Closing;
        public bool IsClosed(int d) => d >= 0 && d < _door.Length && _door[d] == DoorState.Closed;
        public int GroupOf(int d) => d >= 0 && d < _group.Length ? _group[d] : -1;
        /// <summary>The group the last Drop dropped (-1 before the first).</summary>
        public int LastGroup { get; private set; } = -1;
        /// <summary>Door d's centre (y = the floor), its long axis (local X) and across it (local Z: the way out).</summary>
        public Vector3 DoorCenter(int d) => _center[d];
        public Vector3 DoorAlong(int d) => _ax[d];
        public Vector3 DoorAcross(int d) => _az[d];
        public Vector3 LiftCenter(int l) => _lcenter[l];
        /// <summary>The lift is telegraphing, sinking or rising.</summary>
        public bool LiftBusy(int l) => l >= 0 && l < _lift.Length && _lift[l] != LiftState.Idle;
        public bool LiftTelegraphing(int l) => l >= 0 && l < _lift.Length && _lift[l] == LiftState.Telegraph;
        /// <summary>Lift l's platform offset below its top (m, ≤ 0).</summary>
        public float LiftOffset(int l) => l >= 0 && l < _platY.Length ? _platY[l] : 0f;
        /// <summary>The stage lift's platform offset below its top (m, ≤ 0): the boss's rise.</summary>
        public float CenterLiftOffset => _cplatY;
        /// <summary>Halt() or the boss's fall: no more scheduled drops or waves.</summary>
        public bool Halted => _halted;
        public float Clock => _clock;
        public float NextDropAt => _nextDrop;
        public float NextWaveAt => _nextWave;
        /// <summary>0 = the day, 1 = dusk, 2 = last light (the light's target).</summary>
        public int LightStage { get; private set; }
        public Light Sun => _sun;
        /// <summary>Drops (a group, a single door, every door at once), enemies swallowed, elites hurt, hits on a boss, on
        /// the player, the player's feet in the air as a door under them opened, lift waves, lifts raised, enemies they
        /// brought up (tests, bot log).</summary>
        public int Drops { get; private set; }
        public int Fallen { get; private set; }
        public int ElitesHurt { get; private set; }
        public int BossHits { get; private set; }
        public int PlayerHits { get; private set; }
        public int Jumped { get; private set; }
        public int LiftWaves { get; private set; }
        public int LiftsRaised { get; private set; }
        public int Released { get; private set; }

        // ------------------------------------------------------------------ pure maths (EditMode tests)

        /// <summary>True if `p` (XZ) is inside the rectangle at `centre` (world XZ) turned `yawDeg` about +Y (local X =
        /// (cos, −sin), local Z = (sin, cos)), `size` along local X and Z, grown by `pad` metres.</summary>
        public static bool InRect(Vector2 centre, float yawDeg, Vector2 size, Vector3 p, float pad)
        {
            float yaw = yawDeg * Mathf.Deg2Rad, c = Mathf.Cos(yaw), s = Mathf.Sin(yaw);
            float dx = p.x - centre.x, dz = p.z - centre.y;
            float lx = dx * c - dz * s, lz = dx * s + dz * c;
            return Mathf.Abs(lx) <= size.x * 0.5f + pad && Mathf.Abs(lz) <= size.y * 0.5f + pad;
        }

        /// <summary>The unit push (world, y 0) across the rectangle toward its nearer long edge: along ±local Z when the
        /// rectangle is longer along X (a trapdoor), else ±local X.</summary>
        public static Vector3 OutOfRect(Vector2 centre, float yawDeg, Vector2 size, Vector3 p)
        {
            float yaw = yawDeg * Mathf.Deg2Rad, c = Mathf.Cos(yaw), s = Mathf.Sin(yaw);
            float dx = p.x - centre.x, dz = p.z - centre.y;
            if (size.x >= size.y)
            {
                float lz = dx * s + dz * c;
                return new Vector3(s, 0f, c) * (lz >= 0f ? 1f : -1f);
            }
            float lx = dx * c - dz * s;
            return new Vector3(c, 0f, -s) * (lx >= 0f ? 1f : -1f);
        }

        /// <summary>The group of the door nearest `player` (XZ), or the nearest door of another group when that group is
        /// `lastGroup` and another exists. -1 without doors. No allocation.</summary>
        public static int PickGroup(Vector4[] doors, Vector3 player, int lastGroup)
        {
            if (doors == null || doors.Length == 0) return -1;
            int best = -1, other = -1;
            float bd = float.PositiveInfinity, od = float.PositiveInfinity;
            for (int i = 0; i < doors.Length; i++)
            {
                float dx = doors[i].x - player.x, dz = doors[i].y - player.z, d2 = dx * dx + dz * dz;
                int g = (int)doors[i].w;
                if (d2 < bd) { bd = d2; best = g; }
                if (g != lastGroup && d2 < od) { od = d2; other = g; }
            }
            return best == lastGroup && other >= 0 ? other : best;
        }

        // ------------------------------------------------------------------ queries

        /// <summary>True if `p` (XZ) is over door d, grown by `pad` metres.</summary>
        public bool InDoor(int d, Vector3 p, float pad)
        {
            Vector3 c = _center[d];
            float dx = p.x - c.x, dz = p.z - c.z;
            float lx = dx * _ax[d].x + dz * _ax[d].z, lz = dx * _az[d].x + dz * _az[d].z;
            return Mathf.Abs(lx) <= trapdoorSize.x * 0.5f + pad && Mathf.Abs(lz) <= trapdoorSize.y * 0.5f + pad;
        }

        /// <summary>The door `p` is over (grown by `pad`), or -1.</summary>
        public int DoorAt(Vector3 p, float pad)
        {
            for (int d = 0; d < _door.Length; d++) if (InDoor(d, p, pad)) return d;
            return -1;
        }

        /// <summary>The unit push out of door d for `p`: across, toward the nearer long edge.</summary>
        public Vector3 OutOfDoor(int d, Vector3 p)
        {
            Vector3 c = _center[d];
            float lz = (p.x - c.x) * _az[d].x + (p.z - c.z) * _az[d].z;
            return lz >= 0f ? _az[d] : -_az[d];
        }

        public bool InLift(int l, Vector3 p, float pad) => InSquare(_lcenter[l], liftSize, p, pad);

        static bool InSquare(Vector3 c, float side, Vector3 p, float pad)
        {
            float h = side * 0.5f + pad;
            return Mathf.Abs(p.x - c.x) <= h && Mathf.Abs(p.z - c.z) <= h;
        }

        /// <summary>The unit push off a square at `c` toward its nearest edge (lifts are yaw 0).</summary>
        static Vector3 OffSquare(Vector3 c, Vector3 p)
        {
            float dx = p.x - c.x, dz = p.z - c.z;
            if (Mathf.Abs(dx) >= Mathf.Abs(dz)) return new Vector3(dx >= 0f ? 1f : -1f, 0f, 0f);
            return new Vector3(0f, 0f, dz >= 0f ? 1f : -1f);
        }

        /// <summary>The door whose centre is nearest `p` (XZ), or -1.</summary>
        public int NearestDoor(Vector3 p)
        {
            int best = -1;
            float bd = float.PositiveInfinity;
            for (int d = 0; d < _center.Length; d++)
            {
                float dx = _center[d].x - p.x, dz = _center[d].z - p.z, d2 = dx * dx + dz * dz;
                if (d2 < bd) { bd = d2; best = d; }
            }
            return best;
        }

        /// <summary>The lift nearest `p` (XZ) at least `minDistance` from it, or -1.</summary>
        public int NearestLift(Vector3 p, float minDistance)
        {
            int best = -1;
            float bd = float.PositiveInfinity, min2 = minDistance * minDistance;
            for (int l = 0; l < _lcenter.Length; l++)
            {
                float dx = _lcenter[l].x - p.x, dz = _lcenter[l].z - p.z, d2 = dx * dx + dz * dz;
                if (d2 >= min2 && d2 < bd) { bd = d2; best = l; }
            }
            return best;
        }

        /// <summary>Seconds until a door or lift under `p` opens or moves (0 if one is open or moving now; +∞ if none
        /// will). For the bot: steer off below ~2.5 s, treat held doors as walls.</summary>
        public float Danger(Vector3 p)
        {
            float best = float.PositiveInfinity;
            for (int d = 0; d < _door.Length; d++)
            {
                var s = _door[d];
                if ((s != DoorState.Telegraph && s != DoorState.Open && s != DoorState.Held) || !InDoor(d, p, PlayerRadius)) continue;
                float t = s == DoorState.Telegraph ? Mathf.Max(0f, dropTelegraph - _t[d]) : 0f;
                if (t < best) best = t;
            }
            for (int l = 0; l < _lift.Length; l++)
            {
                if (_lift[l] == LiftState.Idle || !InLift(l, p, PlayerRadius)) continue;
                float t = _lift[l] == LiftState.Telegraph ? Mathf.Max(0f, liftTelegraph - _lt[l]) : 0f;
                if (t < best) best = t;
            }
            if (_cplat != null && _cplatY < -0.01f && InSquare(centerLiftSpot, liftSize * centerLiftScale, p, PlayerRadius)) best = 0f;
            return best;
        }

        // ------------------------------------------------------------------ lifecycle

        void Awake()
        {
            Instance = this;
            Configure();
        }

        void OnEnable() => GameLoop.Register(this, TickGroup.Enemies, 33); // after ChariotRace (32)
        void OnDisable() => GameLoop.Unregister(this);
        void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>(Re)reads the fields: doors, lifts, model parts and the schedule. Awake calls it; tests that change
        /// the fields afterwards call it again.</summary>
        public void Configure()
        {
            var obstacles = EnemyManager.Instance != null ? EnemyManager.Instance.Obstacles : null;
            if (obstacles != null) for (int d = 0; d < _box.Length; d++) if (_box[d] >= 0) obstacles.SetBoxActive(_box[d], false);
            int n = trapdoors != null ? trapdoors.Length : 0;
            _door = new DoorState[n];
            _t = new float[n];
            _closeAt = new float[n];
            _leaf = new float[n];
            _setF = new float[n];
            _setB = new float[n];
            _hold = new bool[n];
            _playerHit = new bool[n];
            _serial = new int[n];
            _box = new int[n];
            _group = new int[n];
            _center = new Vector3[n];
            _ax = new Vector3[n];
            _az = new Vector3[n];
            _leafF = new Transform[n];
            _leafB = new Transform[n];
            _leafFBind = new Quaternion[n];
            _leafBBind = new Quaternion[n];
            int groups = 0;
            for (int d = 0; d < n; d++)
            {
                var v = trapdoors[d];
                float yaw = v.z * Mathf.Deg2Rad;
                _center[d] = new Vector3(v.x, 0f, v.y);
                _ax[d] = new Vector3(Mathf.Cos(yaw), 0f, -Mathf.Sin(yaw));
                _az[d] = new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
                _group[d] = Mathf.Max(0, (int)v.w);
                groups = Mathf.Max(groups, _group[d] + 1);
                _closeAt[d] = -1f;
                _box[d] = -1;
                _setF[d] = _setB[d] = float.NaN; // write the leaves once
                var root = doors != null && d < doors.Length ? doors[d] : null;
                if (root == null) continue;
                var all = root.GetComponentsInChildren<Transform>(true);
                _leafF[d] = Find(all, "LeafF");
                _leafB[d] = Find(all, "LeafB");
                if (_leafF[d] != null) _leafFBind[d] = _leafF[d].rotation;
                if (_leafB[d] != null) _leafBBind[d] = _leafB[d].rotation;
            }
            _groupFallen = new int[groups];
            _reach = 0.5f * Mathf.Sqrt(trapdoorSize.x * trapdoorSize.x + trapdoorSize.y * trapdoorSize.y);

            int m = liftSpots != null ? liftSpots.Length : 0;
            _lift = new LiftState[m];
            _lt = new float[m];
            _platY = new float[m];
            _platSet = new float[m];
            _ldef = new EnemyDef[m];
            _lcount = new int[m];
            _lcap = new int[m];
            _lcenter = new Vector3[m];
            _platHome = new Vector3[m];
            _plat = new Transform[m];
            for (int l = 0; l < m; l++)
            {
                _lcenter[l] = liftSpots[l];
                var root = lifts != null && l < lifts.Length ? lifts[l] : null;
                _plat[l] = root != null ? Find(root.GetComponentsInChildren<Transform>(true), "Platform") : null;
                if (_plat[l] != null) _platHome[l] = _plat[l].position;
            }
            _cplat = hasCenterLift && centerLift != null ? Find(centerLift.GetComponentsInChildren<Transform>(true), "Platform") : null;
            if (_cplat != null) _cplatHome = _cplat.position;
            _cplatY = _cplatSet = 0f;

            _nextDrop = n > 0 && dropFirst >= 0f ? dropFirst : float.PositiveInfinity;
            _nextWave = m > 0 && liftFirst >= 0f ? liftFirst : float.PositiveInfinity;
            _placed = _boxesAdded = false;
        }

        static Transform Find(Transform[] all, string name)
        {
            for (int i = 0; i < all.Length; i++) if (all[i].name == name) return all[i];
            return null;
        }

        /// <summary>Once EnemyManager is up: floor heights, the walk-only boxes (switched on only while a door is held),
        /// the player and the sun.</summary>
        void Place(EnemyManager em)
        {
            if (_player == null && Player.Main != null)
            {
                _player = Player.Main.transform;
                _health = Player.Main.GetComponent<PlayerHealth>();
                _motor = Player.Main.GetComponent<PlayerMotor>();
            }
            if (!_sunLooked)
            {
                _sunLooked = true;
                _sun = RenderSettings.sun;
                if (_sun == null)
                    foreach (var l in FindObjectsByType<Light>())
                        if (l.type == LightType.Directional) { _sun = l; break; }
                if (_sun != null) { _dayColor = _sun.color; _dayIntensity = _sun.intensity; }
            }
            if (em == null) return;
            if (!_placed && em.Height != null)
            {
                _placed = true;
                for (int d = 0; d < _center.Length; d++) _center[d].y = em.Height.Sample(_center[d].x, _center[d].z);
                for (int l = 0; l < _lcenter.Length; l++) _lcenter[l].y = em.Height.Sample(_lcenter[l].x, _lcenter[l].z);
            }
            if (!_boxesAdded && em.Obstacles != null)
            {
                _boxesAdded = true;
                var half = new Vector2(trapdoorSize.x * 0.5f + 0.05f, trapdoorSize.y * 0.5f + 0.05f);
                for (int d = 0; d < _box.Length; d++)
                {
                    _box[d] = em.Obstacles.AddBox(new Vector2(_center[d].x, _center[d].z), trapdoors[d].z, half, active: false, walkOnly: true);
                    if (_door[d] == DoorState.Held) em.Obstacles.SetBoxActive(_box[d], true);
                }
            }
        }

        /// <summary>The packs' enemies get their pools during the intro, after the Director's (the War Boar isn't in the
        /// Colosseum's table: without this its pool would grow mid-run).</summary>
        void Prewarm(EnemyManager em)
        {
            if (!_directorLooked) LookUpDirector();
            if (_director != null && _director.isActiveAndEnabled && !_director.Prewarmed) return;
            int budget = 8;
            while (budget > 0 && _prewarmIndex < liftPacks.Length)
            {
                var p = liftPacks[_prewarmIndex];
                int wave = Mathf.Max(1, p.count.y) * Mathf.Max(1, liftsPerWave) * 2;
                int want = (p.maxAlive > 0 ? Mathf.Min(p.maxAlive, wave) : wave) + 4;
                int made = p.enemy != null ? em.PrewarmStep(p.enemy, want, budget) : 0;
                budget -= made;
                if (made == 0 || budget > 0) _prewarmIndex++;
            }
            _prewarmed = _prewarmIndex >= liftPacks.Length;
        }

        void LookUpDirector()
        {
            _directorLooked = true;
            _director = Director.Instance != null ? Director.Instance : FindAnyObjectByType<Director>();
        }

        int ArenaCap(EnemyManager em)
        {
            if (!_directorLooked) LookUpDirector();
            int cap = _director != null ? _director.AliveCap : 0;
            return cap > 0 ? cap : em.capacity;
        }

        float RunTime => RunState.Current != null ? RunState.Current.Elapsed : _clock;

        // ------------------------------------------------------------------ control

        /// <summary>Every door of `group` telegraphs now, then drops (the schedule, the boss fight, tests).</summary>
        public void Drop(int group)
        {
            if (group < 0) return;
            bool any = false;
            int near = -1;
            float bd = float.PositiveInfinity;
            Vector3 pp = PlayerPos;
            for (int d = 0; d < _door.Length; d++)
            {
                if (_group[d] != group || !Telegraph(d, false)) continue;
                any = true;
                float dx = _center[d].x - pp.x, dz = _center[d].z - pp.z, d2 = dx * dx + dz * dz;
                if (d2 < bd) { bd = d2; near = d; }
            }
            LastGroup = group;
            if (!any) return;
            Drops++;
            if (group < _groupFallen.Length) _groupFallen[group] = 0;
            SfxPlayer.Play(SfxId.Rattle, _center[near]);
        }

        /// <summary>Door d alone telegraphs now, then drops (tests).</summary>
        public void DropDoor(int d)
        {
            if (d < 0 || d >= _door.Length || !Telegraph(d, false)) return;
            Drops++;
            SfxPlayer.Play(SfxId.Rattle, _center[d]);
        }

        /// <summary>Every closed door telegraphs at once, then drops (the boss's phase 2).</summary>
        public void OpenAll()
        {
            bool any = false;
            for (int d = 0; d < _door.Length; d++) any |= Telegraph(d, false);
            if (!any) return;
            Drops++;
            for (int g = 0; g < _groupFallen.Length; g++) _groupFallen[g] = 0;
            SfxPlayer.Play2D(SfxId.Rattle);
        }

        /// <summary>Every door telegraphs (an open one stays open), then stays open: held, its walk-only box on (the boss's
        /// phase 3). No more drops while the boss lives.</summary>
        public void OpenForGood()
        {
            for (int d = 0; d < _door.Length; d++) { _closeAt[d] = -1f; Telegraph(d, true); }
            Drops++;
            SfxPlayer.Play2D(SfxId.Rattle);
        }

        /// <summary>Every door closes, one after another (`closeStagger` apart), boxes off (the boss's fall).</summary>
        public void CloseAll()
        {
            float at = _clock;
            for (int d = 0; d < _door.Length; d++)
            {
                if (_door[d] == DoorState.Closed || _door[d] == DoorState.Closing) continue;
                _hold[d] = false;
                _closeAt[d] = at;
                at += Mathf.Max(0f, closeStagger);
            }
        }

        /// <summary>No more scheduled drops or lift waves, in the run or the boss fight (doors already moving finish; his
        /// phases still open the floor and raise his guards' lifts).</summary>
        public void Halt() => _halted = true;

        /// <summary>A lift wave now (tests; also from Halt): the idle lifts nearest the player at least liftMinDistance
        /// away, each with a pack from the active table. False if none rose.</summary>
        public bool StartWaveNow() => StartWave();

        /// <summary>Lift `lift` raises `count` of `def` (telegraph, sink, rise, then they step off), or with def null a
        /// visual raise (no telegraph, no pack; not under the player). False if it's busy or under the player.</summary>
        public bool RaiseLift(int lift, EnemyDef def, int count)
        {
            if (lift < 0 || lift >= _lift.Length || _lift[lift] != LiftState.Idle) return false;
            if (def == null && _player != null && InLift(lift, _player.position, PlayerRadius)) return false;
            BeginRaise(lift, def, count, 0);
            return true;
        }

        /// <summary>Door d starts its telegraph (then Open, or Held when `hold`). False if it was already telegraphing or
        /// open (a held request still holds it).</summary>
        bool Telegraph(int d, bool hold)
        {
            switch (_door[d])
            {
                case DoorState.Closed:
                case DoorState.Closing:
                    _door[d] = DoorState.Telegraph;
                    _t[d] = 0f;
                    _hold[d] = hold;
                    _closeAt[d] = -1f;
                    Dust(_center[d], 4);
                    return true;
                case DoorState.Telegraph:
                    _hold[d] |= hold;
                    _closeAt[d] = -1f;
                    return false;
                case DoorState.Open:
                    if (hold) Hold(d);
                    return false;
                default:
                    return false;
            }
        }

        void Open(int d)
        {
            _t[d] = 0f;
            _serial[d] = ++_serialSeq;
            _playerHit[d] = false;
            if (_hold[d]) Hold(d);
            else _door[d] = DoorState.Open;
            if (_player != null && InDoor(d, _player.position, 0f) && _player.position.y - _center[d].y > feetClear) Jumped++; // in the air as it opens
            if (Near(_center[d]))
            {
                Dust(_center[d] - _ax[d] * (trapdoorSize.x * 0.33f), 10);
                Dust(_center[d], 10);
                Dust(_center[d] + _ax[d] * (trapdoorSize.x * 0.33f), 10);
            }
        }

        void Hold(int d)
        {
            _door[d] = DoorState.Held;
            _hold[d] = true;
            var em = EnemyManager.Instance;
            if (em != null && em.Obstacles != null && _box[d] >= 0) em.Obstacles.SetBoxActive(_box[d], true);
        }

        void Close(int d)
        {
            var s = _door[d];
            _hold[d] = false;
            if (s == DoorState.Telegraph) { _door[d] = DoorState.Closed; _t[d] = 0f; return; }
            if (s != DoorState.Open && s != DoorState.Held) return;
            _door[d] = DoorState.Closing;
            _t[d] = 0f;
            var em = EnemyManager.Instance;
            if (em != null && em.Obstacles != null && _box[d] >= 0) em.Obstacles.SetBoxActive(_box[d], false);
        }

        void BeginRaise(int l, EnemyDef def, int count, int cap)
        {
            _ldef[l] = def;
            _lcount[l] = def != null ? Mathf.Max(0, count) : 0;
            _lcap[l] = cap;
            _lt[l] = 0f;
            _lift[l] = def != null ? LiftState.Telegraph : LiftState.Sinking;
            LiftsRaised++;
        }

        bool StartWave()
        {
            var em = EnemyManager.Instance;
            if (em == null || _lift.Length == 0) return false;
            Vector3 pp = PlayerPos;
            int picked = PickLifts(pp, liftMinDistance, Mathf.Min(liftsPerWave, _pick.Length), idleOnly: true);
            float t = RunTime;
            int cap = ArenaCap(em);
            int raised = 0;
            string call = null;
            for (int i = 0; i < picked; i++)
            {
                int pk = PickPack(em, t, cap);
                if (pk < 0) continue; // nothing may come up: this lift stays down
                var p = liftPacks[pk];
                BeginRaise(_pick[i], p.enemy, GameRandom.Range(p.count.x, p.count.y + 1), p.maxAlive);
                raised++;
                if (call == null) call = p.call;
            }
            if (raised == 0) return false;
            LiftWaves++;
            SfxPlayer.Play2D(SfxId.Horn);
            if (!string.IsNullOrEmpty(call))
                GameEvents.OnAnnounce.Raise(new AnnounceEvent { Kind = AnnounceKind.Line, Text = call, Color = telegraphColor, Seconds = announceSeconds });
            if (CrowdExcitement.Instance != null) CrowdExcitement.Instance.Spike(crowdSpikeWave);
            return true;
        }

        /// <summary>Fills _pick with up to `want` lifts nearest `p` at least `minDistance` away (idle ones only if asked),
        /// nearest first. Returns how many.</summary>
        int PickLifts(Vector3 p, float minDistance, int want, bool idleOnly)
        {
            float min2 = minDistance * minDistance;
            int n = 0;
            for (int r = 0; r < want; r++)
            {
                int best = -1;
                float bd = float.PositiveInfinity;
                for (int l = 0; l < _lcenter.Length; l++)
                {
                    if (idleOnly && _lift[l] != LiftState.Idle) continue;
                    float dx = _lcenter[l].x - p.x, dz = _lcenter[l].z - p.z, d2 = dx * dx + dz * dz;
                    if (d2 < min2 || d2 >= bd) continue;
                    bool taken = false;
                    for (int q = 0; q < n; q++) taken |= _pick[q] == l;
                    if (taken) continue;
                    bd = d2;
                    best = l;
                }
                if (best < 0) break;
                _pick[n++] = best;
            }
            return n;
        }

        /// <summary>A weighted pick among the packs active at run time `t`, under their caps and the arena's (counting
        /// what the lifts already on their way carry). -1 if none.</summary>
        int PickPack(EnemyManager em, float t, int cap)
        {
            float total = 0f;
            for (int i = 0; i < liftPacks.Length; i++) if (PackEligible(em, i, t, cap)) total += liftPacks[i].weight;
            if (total <= 0f) return -1;
            float r = GameRandom.Value * total;
            int last = -1;
            for (int i = 0; i < liftPacks.Length; i++)
            {
                if (!PackEligible(em, i, t, cap)) continue;
                last = i;
                if (r < liftPacks[i].weight) return i;
                r -= liftPacks[i].weight;
            }
            return last;
        }

        bool PackEligible(EnemyManager em, int i, float t, int cap)
        {
            var p = liftPacks[i];
            if (p.enemy == null || p.weight <= 0f || t < p.from || (p.to > 0f && t > p.to)) return false;
            if (p.maxAlive > 0 && em.AliveOf(p.enemy) + Pending(p.enemy) + p.count.y > p.maxAlive) return false;
            return em.AliveCount + Pending(null) + Mathf.Max(1, p.count.x) <= cap;
        }

        /// <summary>Enemies the lifts on their way carry: of `def`, or all with def null.</summary>
        int Pending(EnemyDef def)
        {
            int n = 0;
            for (int l = 0; l < _lift.Length; l++)
                if (_lift[l] != LiftState.Idle && _ldef[l] != null && (def == null || _ldef[l] == def)) n += _lcount[l];
            return n;
        }

        Vector3 PlayerPos
        {
            get
            {
                if (_player != null) return _player.position;
                var em = EnemyManager.Instance;
                return em != null ? em.arenaCenter : Vector3.zero;
            }
        }

        bool Near(Vector3 p)
        {
            Vector3 pp = PlayerPos;
            float dx = p.x - pp.x, dz = p.z - pp.z;
            return dx * dx + dz * dz <= drawHeldWithin * drawHeldWithin;
        }

        void Dust(Vector3 at, int count)
        {
            var pop = DeathPopFx.Instance;
            if (pop == null || _dust >= MaxDustPerFrame || !Near(at)) return;
            _dust++;
            pop.Burst(at + Vector3.up * 0.2f, count, 2.6f, 0.24f, dustA, dustB);
        }

        // ------------------------------------------------------------------ tick

        public void Tick(float dt, float unscaledDt)
        {
            var em = EnemyManager.Instance;
            if (!_prewarmed && em != null) Prewarm(em); // pools first (also while the arena intro holds the game paused)
            // after his fall the doors close and the sun comes back in real time: the fall is in slow motion, and the
            // reward or the summary may pause the game before the last door is down
            float step = _bossDone ? unscaledDt : dt;
            if (step <= 0f || (_door.Length == 0 && _lift.Length == 0 && _cplat == null)) return;
            Place(em);
            _dust = 0;
            _clock += step;
            var boss = BossController.Active;
            bool bossOn = TickBoss(boss);
            if (!_halted && !bossOn) Schedule();
            TickDoors(step, em);
            TickLifts(step, em);
            TickCenterLift(boss, step);
            TickLight(step);
        }

        void Schedule()
        {
            if (_clock >= _nextDrop)
            {
                _nextDrop = dropEvery > 0f ? _nextDrop + dropEvery : float.PositiveInfinity;
                if (_nextDrop <= _clock) _nextDrop = _clock + dropEvery;
                Drop(PickGroup(trapdoors, PlayerPos, LastGroup));
            }
            if (_clock >= _nextWave)
            {
                _nextWave = liftEvery > 0f ? _nextWave + liftEvery : float.PositiveInfinity;
                if (_nextWave <= _clock) _nextWave = _clock + liftEvery;
                StartWave();
            }
        }

        /// <summary>Follows the boss (never called by him). True while he's in the fight (the run schedule stops).</summary>
        bool TickBoss(BossController boss)
        {
            if (boss == null) return false;
            if (!_bossSeen)
            {
                _bossSeen = true;
                _seenPhase = boss.Phase;
                _seenGuards = boss.GuardWaves;
                _bossNextDrop = float.PositiveInfinity;
                for (int l = 0; l < _lift.Length; l++) { _ldef[l] = null; _lcount[l] = 0; } // the horde is dragged off: lifts on their way come up empty
                SfxPlayer.Play2D(SfxId.Rattle);
            }
            if (boss.Defeated)
            {
                _rattleAll = false;
                if (!_bossDone)
                {
                    _bossDone = true;
                    _halted = true;
                    CloseAll();
                    SetLight(0);
                }
                return false;
            }
            _rattleAll = boss.CurrentState == BossController.State.Entering; // every door rattles as he comes up
            if (_rattleAll) return true;
            if (float.IsPositiveInfinity(_bossNextDrop)) _bossNextDrop = _clock + bossDropEvery.x; // his entrance is over

            int phase = boss.Phase;
            if (phase > _seenPhase)
            {
                if (openAllOnPhase > 0 && phase >= openAllOnPhase && _seenPhase < openAllOnPhase)
                {
                    OpenAll();
                    SetLight(1);
                    _bossNextDrop = _clock + bossDropEvery.y;
                }
                if (holdOnPhase > 0 && phase >= holdOnPhase && _seenPhase < holdOnPhase)
                {
                    OpenForGood();
                    SetLight(2);
                }
                _seenPhase = phase;
            }
            if (boss.GuardWaves > _seenGuards)
            {
                _seenGuards = boss.GuardWaves;
                RaiseForGuards(boss);
            }
            if (holdOnPhase > 0 && phase >= holdOnPhase) return true; // the floor is open for good: no more drops
            if (!_halted && _clock >= _bossNextDrop)
            {
                _bossNextDrop = _clock + (phase >= 2 ? bossDropEvery.y : bossDropEvery.x);
                Drop(PickGroup(trapdoors, PlayerPos, LastGroup));
            }
            return true;
        }

        /// <summary>His guards appeared at the guard spots (the lifts) nearest the player: those lifts sink and rise under
        /// them (visual), except one the player stands on.</summary>
        void RaiseForGuards(BossController boss)
        {
            int use = boss.def != null ? Mathf.Max(1, boss.def.guardSpotsUsed) : 2;
            Vector3 pp = PlayerPos;
            int n = PickLifts(pp, 0f, Mathf.Min(use, _pick.Length), idleOnly: false);
            for (int i = 0; i < n; i++)
            {
                int l = _pick[i];
                if (_lift[l] != LiftState.Idle || (_player != null && InLift(l, pp, PlayerRadius))) continue;
                BeginRaise(l, null, 0, 0);
            }
        }

        void TickDoors(float dt, EnemyManager em)
        {
            if (_door.Length == 0) return;
            if (em != null && _hitSerial.Length != em.capacity) { _hitSerial = new int[em.capacity]; _hitUid = new int[em.capacity]; }
            var tele = TelegraphRenderer.Instance;
            Vector3 pp = PlayerPos;
            bool playerOk = _player != null && _health != null && !_health.IsDead;
            var rim = telegraphColor;
            rim.a *= rimAlpha;
            float hx = trapdoorSize.x * 0.5f, hz = trapdoorSize.y * 0.5f;
            for (int d = 0; d < _door.Length; d++)
            {
                if (_closeAt[d] >= 0f && _clock >= _closeAt[d]) { _closeAt[d] = -1f; Close(d); }
                float t = _t[d] += dt;
                Vector3 c = _center[d];
                float rattle = 0f;
                switch (_door[d])
                {
                    case DoorState.Closed:
                        if (_rattleAll) rattle = 1f;
                        break;
                    case DoorState.Telegraph:
                        _leaf[d] = Mathf.MoveTowards(_leaf[d], 0f, dt / Mathf.Max(0.01f, closeTime));
                        rattle = 0.4f + 0.6f * Mathf.Clamp01(t / Mathf.Max(0.01f, dropTelegraph));
                        if (tele != null) tele.Lane(c - _ax[d] * hx, _ax[d], trapdoorSize.x, trapdoorSize.y, Mathf.Clamp01(t / Mathf.Max(0.01f, dropTelegraph)), telegraphColor);
                        if (t >= dropTelegraph) Open(d);
                        break;
                    case DoorState.Open:
                    case DoorState.Held:
                        _leaf[d] = Mathf.MoveTowards(_leaf[d], 1f, dt / Mathf.Max(0.01f, leafSwing));
                        if (tele != null && Near(c)) DrawRim(tele, d, hx, hz, rim);
                        if (em != null) HitEnemies(d, em);
                        if (playerOk) HitPlayer(d, pp);
                        if (_door[d] == DoorState.Open && t >= dropOpen) { if (_hold[d]) Hold(d); else Close(d); }
                        break;
                    case DoorState.Closing:
                        _leaf[d] = Mathf.MoveTowards(_leaf[d], 0f, dt / Mathf.Max(0.01f, closeTime));
                        if (t >= closeTime) { _door[d] = DoorState.Closed; _t[d] = 0f; _leaf[d] = 0f; SfxIfNear(SfxId.Slam, c, 0.5f); }
                        break;
                }
                PoseLeaves(d, rattle);
            }
        }

        void SfxIfNear(SfxId id, Vector3 at, float volume)
        {
            if (Near(at)) SfxPlayer.Play(id, at, volume);
        }

        /// <summary>Four thin lanes round the hole.</summary>
        void DrawRim(TelegraphRenderer tele, int d, float hx, float hz, Color color)
        {
            Vector3 c = _center[d], ax = _ax[d], az = _az[d];
            tele.Lane(c + az * hz - ax * hx, ax, trapdoorSize.x, rimWidth, 1f, color);
            tele.Lane(c - az * hz - ax * hx, ax, trapdoorSize.x, rimWidth, 1f, color);
            tele.Lane(c + ax * hx - az * hz, az, trapdoorSize.y, rimWidth, 1f, color);
            tele.Lane(c - ax * hx - az * hz, az, trapdoorSize.y, rimWidth, 1f, color);
        }

        /// <summary>The leaves: open fraction → swing down about the door's long axis (LeafF hinged at +z drops with a
        /// negative angle, LeafB at −z with a positive one), plus a rattle that bumps their free edges up. Writes the
        /// transforms only when the angle changes.</summary>
        void PoseLeaves(int d, float rattle)
        {
            float u = _leaf[d];
            float open = _door[d] == DoorState.Closing || _door[d] == DoorState.Telegraph
                ? leafAngle * Mathf.SmoothStep(0f, 1f, u)
                : leafAngle * u * u; // falling: it accelerates
            float bumpF = 0f, bumpB = 0f;
            if (rattle > 0f && Near(_center[d]))
            {
                float w = _clock * rattleHz * 2f * Mathf.PI + d * 1.7f;
                bumpF = rattleDeg * rattle * Mathf.Abs(Mathf.Sin(w));
                bumpB = rattleDeg * rattle * Mathf.Abs(Mathf.Sin(w + 2.1f));
            }
            float f = open - bumpF, b = open - bumpB;
            if (_leafF[d] != null && !Mathf.Approximately(f, _setF[d])) { _setF[d] = f; _leafF[d].rotation = Quaternion.AngleAxis(-f, _ax[d]) * _leafFBind[d]; }
            if (_leafB[d] != null && !Mathf.Approximately(b, _setB[d])) { _setB[d] = b; _leafB[d].rotation = Quaternion.AngleAxis(b, _ax[d]) * _leafBBind[d]; }
        }

        void HitEnemies(int d, EnemyManager em)
        {
            Vector3 c = _center[d];
            int n = em.QueryAlive(c, _reach, _query);
            for (int k = 0; k < n; k++)
            {
                int e = _query[k];
                int uid = em.GetUid(e);
                if (_hitSerial[e] == _serial[d] && _hitUid[e] == uid) continue; // once per opening
                Vector3 p = em.GetPosition(e);
                if (!InDoor(d, p, 0f) || p.y - c.y > feetClear) continue; // its centre over the hole, its feet on the floor
                var def = em.GetDef(e);
                if (def != null && def.behaviour == EnemyBehaviour.Scripted)
                {
                    float before = em.GetHp(e);
                    em.ApplyDamage(e, new DamageInfo { Amount = bossDamage, Flags = DamageFlags.Area, Point = p, Direction = Vector3.up, SourceId = SourceId });
                    if (em.IsAlive(e) && em.GetHp(e) >= before) continue; // invulnerable (mid-roar): try again next frame
                    Mark(e, uid, d);
                    BossHits++;
                    continue;
                }
                Mark(e, uid, d);
                if (em.IsElite(e))
                {
                    Vector3 away = OutOfDoor(d, p);
                    bool flat = em.IsBlocking(e, away); // a fall doesn't care about a shield
                    em.ApplyDamage(e, new DamageInfo
                    {
                        Amount = eliteDamage, Flags = DamageFlags.Area, Point = p, Direction = flat ? Vector3.up : away,
                        Knockback = flat ? 0f : eliteKnockback, SourceId = SourceId,
                    });
                    ElitesHurt++;
                    if (!em.IsAlive(e)) Swallowed(d, p);
                    continue;
                }
                if (em.Execute(e, SourceId)) Swallowed(d, p);
            }
        }

        void Mark(int e, int uid, int d)
        {
            _hitSerial[e] = _serial[d];
            _hitUid[e] = uid;
        }

        void Swallowed(int d, Vector3 p)
        {
            Fallen++;
            Dust(p, 6);
            int g = _group[d];
            if (g >= _groupFallen.Length) return;
            if (++_groupFallen[g] == roarFallen && CrowdExcitement.Instance != null) CrowdExcitement.Instance.Spike(crowdSpikeDrop);
        }

        void HitPlayer(int d, Vector3 pp)
        {
            if (!InDoor(d, pp, 0f) || pp.y - _center[d].y > feetClear) return; // its centre over the hole, its feet down
            Vector3 away = OutOfDoor(d, pp);
            if (_door[d] == DoorState.Open)
            {
                if (_playerHit[d]) return; // once per opening
                _playerHit[d] = true;
                if (_health.TakeDamage(new DamageInfo { Amount = playerDamage, Flags = DamageFlags.Area, Point = pp, Direction = away })) PlayerHits++;
                Throw(away);
                return;
            }
            // held open: thrown out every heldPushEvery, hurt at most every heldHitEvery
            if (_clock >= _heldNextPush)
            {
                _heldNextPush = _clock + heldPushEvery;
                Throw(away);
            }
            if (_clock >= _heldNextHit)
            {
                _heldNextHit = _clock + heldHitEvery;
                if (_health.TakeDamage(new DamageInfo { Amount = heldPlayerDamage, Flags = DamageFlags.Area, Point = pp, Direction = away })) PlayerHits++;
            }
        }

        void Throw(Vector3 away)
        {
            if (_motor != null) _motor.AddImpulse(away * playerKnockback + Vector3.up * playerKnockUp);
        }

        void TickLifts(float dt, EnemyManager em)
        {
            var tele = TelegraphRenderer.Instance;
            float h = liftSize * 0.5f;
            for (int l = 0; l < _lift.Length; l++)
            {
                if (_lift[l] == LiftState.Idle) continue;
                float t = _lt[l] += dt;
                Vector3 c = _lcenter[l];
                switch (_lift[l])
                {
                    case LiftState.Telegraph:
                        if (tele != null) tele.Lane(c - Vector3.right * h, Vector3.right, liftSize, liftSize, Mathf.Clamp01(t / Mathf.Max(0.01f, liftTelegraph)), telegraphColor);
                        if (t >= liftTelegraph) { _lift[l] = LiftState.Sinking; _lt[l] = 0f; Sink(l); }
                        break;
                    case LiftState.Sinking:
                        _platY[l] = -liftDepth * Mathf.Clamp01(t / Mathf.Max(0.01f, liftSink));
                        if (t >= liftSink) { _lift[l] = LiftState.Rising; _lt[l] = 0f; }
                        break;
                    case LiftState.Rising:
                        _platY[l] = -liftDepth * (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / Mathf.Max(0.01f, liftRise))));
                        if (t >= liftRise) { _platY[l] = 0f; _lift[l] = LiftState.Idle; Arrive(l, em); }
                        break;
                }
                if (_plat[l] != null && !Mathf.Approximately(_platY[l], _platSet[l]))
                {
                    _platSet[l] = _platY[l];
                    _plat[l].position = _platHome[l] + Vector3.up * _platY[l];
                }
            }
        }

        /// <summary>A lift starts sinking: a grounded player on it is hurt and pushed off.</summary>
        void Sink(int l)
        {
            SfxIfNear(SfxId.Gate, _lcenter[l], 0.7f);
            if (_player == null || _health == null || _health.IsDead) return;
            Vector3 pp = _player.position;
            if (!InLift(l, pp, 0f) || pp.y - _lcenter[l].y > feetClear) return;
            Vector3 away = OffSquare(_lcenter[l], pp);
            if (_health.TakeDamage(new DamageInfo { Amount = liftPlayerDamage, Flags = DamageFlags.Area, Point = pp, Direction = away })) PlayerHits++;
            Throw(away);
        }

        /// <summary>The platform is back at the top: its pack steps off (in a ring, within the caps).</summary>
        void Arrive(int l, EnemyManager em)
        {
            var def = _ldef[l];
            int n = _lcount[l];
            _ldef[l] = null;
            _lcount[l] = 0;
            Vector3 c = _lcenter[l];
            Dust(c, 12);
            if (def == null || n <= 0 || em == null) return;
            n = Mathf.Min(n, ArenaCap(em) - em.AliveCount);
            if (_lcap[l] > 0) n = Mathf.Min(n, _lcap[l] - em.AliveOf(def));
            float a0 = GameRandom.Range(0f, Mathf.PI * 2f);
            for (int k = 0; k < n; k++)
            {
                float a = a0 + k * (Mathf.PI * 2f / n);
                Vector3 at = n == 1 ? c : c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * packRing;
                if (em.Spawn(def, at) >= 0) Released++;
            }
        }

        /// <summary>The stage's lift: its platform follows the boss up while he rises through it (it sinks first to meet
        /// him), then stays at the top. A player on it during his entrance is pushed off.</summary>
        void TickCenterLift(BossController boss, float dt)
        {
            if (_cplat == null) return;
            float side = liftSize * centerLiftScale, target = 0f;
            if (boss != null && !boss.Defeated && boss.CurrentState == BossController.State.Entering)
            {
                Vector3 bp = boss.Position;
                if (InSquare(centerLiftSpot, side, bp, 0f) && bp.y < centerLiftSpot.y - 0.05f)
                    target = Mathf.Clamp(bp.y - centerLiftSpot.y, -liftDepth * centerLiftScale, 0f);
                if (_player != null && _clock >= _centerNextPush)
                {
                    Vector3 pp = _player.position;
                    if (InSquare(centerLiftSpot, side, pp, 0f) && pp.y - centerLiftSpot.y <= feetClear)
                    {
                        _centerNextPush = _clock + heldPushEvery;
                        Throw(OffSquare(centerLiftSpot, pp) * 0.7f);
                    }
                }
            }
            _cplatY = Mathf.MoveTowards(_cplatY, target, liftDepth * centerLiftScale / Mathf.Max(0.05f, liftSink) * dt);
            if (Mathf.Approximately(_cplatY, _cplatSet)) return;
            _cplatSet = _cplatY;
            _cplat.position = _cplatHome + Vector3.up * _cplatY;
        }

        // ------------------------------------------------------------------ light

        /// <summary>The sun lerps to 0 = the day, 1 = dusk, 2 = last light over `lightLerp` seconds.</summary>
        void SetLight(int stage)
        {
            LightStage = stage;
            if (_sun == null) return;
            _lightFrom = _sun.color;
            _intFrom = _sun.intensity;
            _lightTo = stage == 1 ? duskColor : stage == 2 ? lastLightColor : _dayColor;
            _intTo = _dayIntensity * (stage == 1 ? duskIntensity : stage == 2 ? lastLightIntensity : 1f);
            _lightT = 0f;
        }

        void TickLight(float dt)
        {
            if (_sun == null || _lightT >= 1f) return;
            _lightT = Mathf.Min(1f, _lightT + dt / Mathf.Max(0.01f, lightLerp));
            float s = Mathf.SmoothStep(0f, 1f, _lightT);
            _sun.color = Color.Lerp(_lightFrom, _lightTo, s);
            _sun.intensity = Mathf.Lerp(_intFrom, _intTo, s);
        }
    }
}
