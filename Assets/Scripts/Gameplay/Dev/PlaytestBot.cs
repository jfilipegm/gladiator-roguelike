using Gladiator.Core;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// Automated player for perf runs and smoke tests: steers to pickups, flees enemies, stays inside the
    /// arena, jumps/slides/orbits the camera, picks random level-up cards. Deterministic (seeded).
    /// Command line (standalone or batch): -bot  -stress [-stressCount 1000]  -botSeconds 600  -profile
    /// </summary>
    [DefaultExecutionOrder(-900)]
    public sealed class PlaytestBot : MonoBehaviour, IPlayerInputSource
    {
        public enum Mode { Normal, Stress }

        public static PlaytestBot Active { get; private set; }
        /// <summary>Set by gameplay systems; null until they exist.</summary>
        public static IBotSensor Sensor;

        [Header("Run")]
        public Mode mode = Mode.Normal;
        [Tooltip("0 = run until stopped. When elapsed: stop the FrameStatsRecorder and quit (standalone) / log (editor).")]
        public float runSeconds = 0f;
        public int stressCount = 1000;
        public int seed = 1234;
        [Tooltip("Perf/smoke runs shouldn't end early: God mode + Timeless while the bot plays (-mortal / -timed turn them off).")]
        public bool godMode = true;
        public bool timeless = true;

        [Header("Arena")]
        [Tooltip("Fallback circle when there is no EnemyManager; otherwise the bot uses the arena's oval.")]
        public Vector3 arenaCenter = Vector3.zero;
        public float arenaRadius = 15f;
        [Tooltip("Stays this far inside the arena's oval (m).")]
        public float edgeMargin = 4f;

        [Header("Behaviour")]
        public float pickupSeekRange = 25f;
        [Tooltip("Goes for a chest it can afford within this range (m).")]
        public float chestSeekRange = 30f;
        [Tooltip("A chest not reached in this many seconds is skipped for as long (the bot must never park on one).")]
        public float chestGiveUp = 15f;
        public float threatRadius = 5f;
        public float threatWeight = 2.5f;
        [Tooltip("Caps the flee push so a goal can still steer: uncapped, ten enemies outweighed any goal and the bot kited the horde around the big pit, rarely hitting anything.")]
        public float threatMax = 1.5f;
        [Tooltip("Within this distance of a chest, gift or gem it mostly ignores threats (a player commits to the grab).")]
        public float commitRange = 4f;
        [Tooltip("With nothing to collect it heads for the enemies within this range instead of wandering (0 = wander).")]
        public float engageRange = 18f;
        public Vector2 jumpEvery = new Vector2(1.5f, 4f);
        public Vector2 slideEvery = new Vector2(2.5f, 6f);
        public Vector2 slideHold = new Vector2(0.4f, 1.1f);
        public float cameraOrbitAmount = 0.35f;
        [Tooltip("Seconds between [BotTrace] lines (where it is, what it goes for, gold); 0 = off. Command line: -botTrace 5.")]
        public float traceEvery = 0f;

        Transform _body;
        uint _rng;
        float _elapsed, _nextJump, _nextSlide, _slideUntil, _nextWander, _cardDelay = -1f;
        float _chestSince = -1f, _chestIgnoreUntil, _nextTrace;
        Vector3 _wanderTarget, _chestTarget, _goalPos;
        char _goal = '-';
        bool _stressRequested, _finished;

        public float Elapsed => _elapsed;
        public bool Finished => _finished;

        /// <summary>The body the bot steers (the player calls this when it adopts the bot as input).</summary>
        public void Bind(Transform body) => _body = body;

        bool _prevGod, _prevTimeless;

        void OnEnable()
        {
            Active = this;
            _prevGod = DevSettings.GodMode; _prevTimeless = DevSettings.Timeless;
            if (godMode) DevSettings.GodMode = true;
            if (timeless) DevSettings.Timeless = true;
            _rng = (uint)Mathf.Max(1, seed);
            _nextJump = Rand(jumpEvery);
            _nextSlide = Rand(slideEvery);
        }

        // Fields can be set right after AddComponent (bootstrap), i.e. after OnEnable: apply them again.
        void Start()
        {
            DevSettings.GodMode = godMode || _prevGod;
            DevSettings.Timeless = timeless || _prevTimeless;
        }

        void OnDisable()
        {
            if (Active == this) Active = null;
            DevSettings.GodMode = _prevGod; DevSettings.Timeless = _prevTimeless;
        }

        void Update()
        {
            if (_finished) return;
            _elapsed += UnityEngine.Time.unscaledDeltaTime;

            if (mode == Mode.Stress && !_stressRequested && Sensor != null && _elapsed > 1f)
            {
                int n = Sensor.RequestStressSpawn(stressCount);
                _stressRequested = true;
                Debug.Log($"[PlaytestBot] stress spawn requested: {n}");
            }

            // Level-up cards: pick after a short (unscaled) beat, like a human would.
            if (Sensor != null && Sensor.LevelUpPending(out int cards) && cards > 0)
            {
                if (_cardDelay < 0f) _cardDelay = 0.25f;
                _cardDelay -= UnityEngine.Time.unscaledDeltaTime;
                if (_cardDelay <= 0f)
                {
                    Sensor.ChooseCard((int)(Next() % (uint)cards));
                    _cardDelay = -1f;
                }
            }

            if (traceEvery > 0f && _elapsed >= _nextTrace && _body != null) Trace();
            if (runSeconds > 0f && _elapsed >= runSeconds) Finish();
        }

        /// <summary>What the bot ended up with (perf runs record which upgrades were exercised).</summary>
        static void LogBuild()
        {
            var player = Player.Main;
            var build = player != null ? player.GetComponent<PlayerBuild>() : null;
            if (build == null) return;
            var sb = new System.Text.StringBuilder("[PlaytestBot] build:");
            for (int i = 0; i < build.State.OwnedCount; i++)
            {
                var o = build.State.GetOwned(i);
                sb.Append(' ').Append(o.Def.id).Append(" L").Append(o.Level);
            }
            var run = RunState.Current;
            if (run != null) sb.Append(" | level ").Append(run.Level).Append(" gold ").Append(run.Gold).Append(" kills ").Append(run.Kills);
            var props = Interactables.Instance;
            if (props != null) sb.Append(" chests ").Append(props.ChestsOpened).Append(" altars ").Append(props.AltarsUsed);
            var gifts = CrowdGifts.Instance;
            if (gifts != null) sb.Append(" | favor gifts ").Append(gifts.Collected).Append('/').Append(gifts.Thrown);
            var master = ArenaMaster.Instance;
            if (master != null) sb.Append(" whims ").Append(master.WhimsFired).Append(" challenges won ").Append(master.ChallengesWon);
            var boss = BossController.Active;
            if (boss != null)
                sb.Append(" | boss ").Append(boss.Defeated ? "DEFEATED" : "alive").Append(" hp ").Append((boss.Hp01 * 100f).ToString("0")).Append("% phase ")
                  .Append(boss.Phase).Append(" attacks ").Append(boss.AttacksDone).Append(" guardWaves ").Append(boss.GuardWaves).Append(" stuns ").Append(boss.Stuns);
            var loadout = player.GetComponent<GladiatorLoadout>();
            if (loadout != null && loadout.Current != null) sb.Append(" | as ").Append(loadout.Current.id);
            var scav = player.GetComponent<Scavenger>();
            if (scav != null) sb.Append(" scavenged ").Append(scav.Fired);
            var snares = player.GetComponent<SnareLayer>();
            if (snares != null) sb.Append(" snares ").Append(snares.Snapped).Append('/').Append(snares.Placed);
            if (PlayerProjectiles.Instance != null && PlayerProjectiles.Instance.Blasts > 0) sb.Append(" bombs ").Append(PlayerProjectiles.Instance.Blasts);
            var rite = player.GetComponent<BloodRite>();
            if (rite != null) sb.Append(" rites ").Append(rite.Rites).Append(" (paid ").Append(rite.HpPaid.ToString("0")).Append(" HP)");
            var thirst = player.GetComponent<Bloodthirst>();
            if (thirst != null) sb.Append(" kill-healed ").Append(thirst.Healed.ToString("0")).Append(" HP");
            sb.Append(" | hitstops ").Append(TimeController.HitStopCount).Append(" (").Append(TimeController.HitStopSeconds.ToString("0.0")).Append(" s)");
            if (run != null) sb.Append(" | outcome ").Append(run.Outcome).Append(" at ").Append(run.Elapsed.ToString("0"));
            Debug.Log(sb.ToString());
        }

        /// <summary>Opt-in (it allocates): where the bot is and what it goes for, to read a headless run afterwards.</summary>
        void Trace()
        {
            _nextTrace = _elapsed + traceEvery;
            var run = RunState.Current;
            var props = Interactables.Instance;
            Debug.Log($"[BotTrace] t={_elapsed:0} run={(run != null ? run.Elapsed : 0f):0} pos={_body.position:F1} goal={_goal} {_goalPos:F0} " +
                      $"gold={(run != null ? run.Gold : 0)} price={(props != null ? props.ChestPrice : 0)} chests={(props != null ? props.ChestsOpened : 0)} " +
                      $"level={(run != null ? run.Level : 0)} enemies={(EnemyManager.Instance != null ? EnemyManager.Instance.AliveCount : 0)} ts={Time.timeScale:0.00}");
        }

        void Finish()
        {
            _finished = true;
            var rec = FindAnyObjectByType<FrameStatsRecorder>();
            if (rec != null && rec.IsRecording) rec.End();
            Debug.Log($"[PlaytestBot] finished after {_elapsed:0.0}s");
            LogBuild(); // after the recorder stopped: this string building is not part of the measurement
#if !UNITY_EDITOR
            Application.Quit(0);
#endif
        }

        public PlayerInputFrame Read()
        {
            var f = new PlayerInputFrame();
            if (_body == null || _finished) return f;
            float t = _elapsed;
            Vector3 pos = _body.position;

            // 1) Where to go: a chest it can pay for (and open it), else the nearest pickup, else a wander point.
            var em = EnemyManager.Instance;
            Vector3 desired, toCrowd;
            if (t >= _chestIgnoreUntil && Sensor != null && Sensor.TryGetAffordableChest(pos, chestSeekRange, out var chest, out bool atChest))
            {
                if (_chestSince < 0f || Flat(chest - _chestTarget).sqrMagnitude > 0.25f) { _chestSince = t; _chestTarget = chest; }
                else if (t - _chestSince > chestGiveUp) { _chestIgnoreUntil = t + chestGiveUp; _chestSince = -1f; }
                desired = Flat(chest - pos);
                f.InteractPressed = atChest;
                _goal = 'C'; _goalPos = chest;
            }
            else if (Sensor != null && Sensor.TryGetNearestPickup(pos, pickupSeekRange, out var pickup))
            {
                desired = Flat(pickup - pos);
                _goal = 'P'; _goalPos = pickup;
            }
            else if (engageRange > 0f && Sensor != null && (toCrowd = -Flat(Sensor.ThreatVector(pos, engageRange))).sqrMagnitude > 1e-4f)
            {
                desired = toCrowd; // toward the nearest of the crowd
                _goal = 'E'; _goalPos = pos + toCrowd.normalized * 5f;
            }
            else
            {
                if (t >= _nextWander || Flat(_wanderTarget - pos).sqrMagnitude < 1f)
                {
                    float ang = RandF() * Mathf.PI * 2f, k = Mathf.Sqrt(RandF()) * 0.8f;
                    _wanderTarget = em != null
                        ? em.arenaCenter + (em.EdgePoint(ang, edgeMargin) - em.arenaCenter) * k
                        : arenaCenter + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * (arenaRadius * k);
                    _nextWander = t + 2f + RandF() * 3f;
                }
                desired = Flat(_wanderTarget - pos);
                _goal = 'W'; _goalPos = _wanderTarget;
            }
            if (desired.sqrMagnitude > 1e-4f) desired.Normalize();

            // 2) Flee threats, capped, and mostly not while grabbing something close.
            if (Sensor != null)
            {
                Vector3 threat = Vector3.ClampMagnitude(Sensor.ThreatVector(pos, threatRadius) * threatWeight, threatMax);
                if (_goal != 'W' && _goal != 'E' && Flat(_goalPos - pos).sqrMagnitude < commitRange * commitRange) threat *= 0.2f;
                desired += threat;
            }

            // 3) Stay inside the arena: its oval shrunk by edgeMargin (chest anchors sit near the edge of the big pit).
            if (em != null)
            {
                Vector3 inside = pos;
                if (em.ClampInside(ref inside, edgeMargin)) desired += Flat(inside - pos) * 2f;
            }
            else
            {
                Vector3 fromCenter = Flat(pos - arenaCenter);
                float d = fromCenter.magnitude;
                if (d > arenaRadius * 0.85f) desired -= fromCenter / Mathf.Max(d, 0.001f) * ((d - arenaRadius * 0.85f) * 2f);
            }

            if (desired.sqrMagnitude > 1f) desired.Normalize();

            // World direction -> camera-relative stick.
            var cam = Camera.main;
            float yaw = cam != null ? cam.transform.eulerAngles.y : 0f;
            Vector3 local = Quaternion.Euler(0f, -yaw, 0f) * desired;
            f.Move = new Vector2(local.x, local.z);

            // 4) Jump / slide rhythm.
            if (t >= _nextJump) { f.JumpPressed = true; _nextJump = t + Rand(jumpEvery); }
            f.JumpHeld = f.JumpPressed;
            if (t >= _nextSlide)
            {
                f.SlidePressed = true;
                _slideUntil = t + Rand(slideHold);
                _nextSlide = t + Rand(slideEvery);
            }
            f.SlideHeld = t < _slideUntil;

            // 5) Slow camera orbit to exercise the camera collision.
            f.LookStick = new Vector2(Mathf.Sin(t * 0.37f) * cameraOrbitAmount, Mathf.Sin(t * 0.21f) * cameraOrbitAmount * 0.3f);
            return f;
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        uint Next()
        {
            // xorshift32
            _rng ^= _rng << 13; _rng ^= _rng >> 17; _rng ^= _rng << 5;
            return _rng;
        }

        float RandF() => (Next() & 0xFFFFFF) / (float)0x1000000;
        float Rand(Vector2 range) => Mathf.Lerp(range.x, range.y, RandF());
    }
}
