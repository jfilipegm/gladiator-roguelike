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
        public Vector3 arenaCenter = Vector3.zero;
        public float arenaRadius = 15f;

        [Header("Behaviour")]
        public float pickupSeekRange = 25f;
        public float threatRadius = 5f;
        public float threatWeight = 2.5f;
        public Vector2 jumpEvery = new Vector2(1.5f, 4f);
        public Vector2 slideEvery = new Vector2(2.5f, 6f);
        public Vector2 slideHold = new Vector2(0.4f, 1.1f);
        public float cameraOrbitAmount = 0.35f;

        Transform _body;
        uint _rng;
        float _elapsed, _nextJump, _nextSlide, _slideUntil, _nextWander, _cardDelay = -1f;
        Vector3 _wanderTarget;
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
            Debug.Log(sb.ToString());
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

            // 1) Where to go: nearest pickup, else a wander point.
            Vector3 desired;
            if (Sensor != null && Sensor.TryGetNearestPickup(pos, pickupSeekRange, out var pickup))
                desired = Flat(pickup - pos);
            else
            {
                if (t >= _nextWander || Flat(_wanderTarget - pos).sqrMagnitude < 1f)
                {
                    float ang = RandF() * Mathf.PI * 2f;
                    float rad = Mathf.Sqrt(RandF()) * arenaRadius * 0.8f;
                    _wanderTarget = arenaCenter + new Vector3(Mathf.Cos(ang) * rad, 0f, Mathf.Sin(ang) * rad);
                    _nextWander = t + 2f + RandF() * 3f;
                }
                desired = Flat(_wanderTarget - pos);
            }
            if (desired.sqrMagnitude > 1e-4f) desired.Normalize();

            // 2) Flee threats.
            if (Sensor != null) desired += Sensor.ThreatVector(pos, threatRadius) * threatWeight;

            // 3) Stay inside the arena.
            Vector3 fromCenter = Flat(pos - arenaCenter);
            float d = fromCenter.magnitude;
            if (d > arenaRadius * 0.85f) desired -= fromCenter / Mathf.Max(d, 0.001f) * ((d - arenaRadius * 0.85f) * 2f);

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
