using Gladiator.Core;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// The Stone Amphitheater's beast cages (docs/ARENAS.md §3): every `every` seconds one cage telegraphs (the lane out
    /// of its pen glows orange-red, its portcullis rattles), then the bars lift and a pack of war boars charges out, one
    /// every `releaseSpacing` seconds; the bars drop again after. The Beast Lord opens them on demand (Open, OpenAll),
    /// and while a boss fights the automatic releases stop. Central tick, no allocation.
    /// </summary>
    public sealed class BeastCages : MonoBehaviour, ITickable
    {
        public static BeastCages Instance { get; private set; }

        public Transform[] cages = System.Array.Empty<Transform>();
        [Tooltip("Each cage's portcullis (lifted to release, dropped after).")]
        public Transform[] bars = System.Array.Empty<Transform>();
        public EnemyDef beast;

        [Header("Automatic releases (0 = only on demand)")]
        public float first = 75f;
        public float every = 40f;
        [Tooltip("Beasts per pack at the start of the run and at the end of its timer.")]
        public Vector2Int pack = new Vector2Int(3, 6);

        [Header("Timing and look")]
        public float telegraph = 2f;
        public float liftTime = 0.35f;
        public float releaseSpacing = 0.3f;
        public float holdOpen = 2.5f;
        public float liftHeight = 3.1f;
        [Tooltip("The lane drawn out of the pen while it telegraphs (m).")]
        public float laneLength = 12f;
        public float laneWidth = 3f;
        [Tooltip("How far in front of the cage the beasts appear (m).")]
        public float spawnDepth = 2.6f;
        public Color telegraphColor = new Color(1f, 0.32f, 0.12f, 0.95f);

        enum Phase : byte { Idle, Telegraph, Release, Hold, Closing }

        Phase[] _phase = System.Array.Empty<Phase>();
        float[] _t = System.Array.Empty<float>();
        float[] _nextOut = System.Array.Empty<float>();
        int[] _left = System.Array.Empty<int>();
        Vector3[] _home = System.Array.Empty<Vector3>();
        float _nextAuto, _rattle;
        Rng _rng; // R8: which cage opens first and where the beasts land, forked from the run seed

        /// <summary>Cages opened and beasts released this run (tests, bot log).</summary>
        public int Opened { get; private set; }
        public int Released { get; private set; }
        public int Count => cages.Length;
        public bool IsBusy(int i) => i >= 0 && i < _phase.Length && _phase[i] != Phase.Idle;
        public bool IsTelegraphing(int i) => i >= 0 && i < _phase.Length && _phase[i] == Phase.Telegraph;
        /// <summary>Where cage i's beasts appear.</summary>
        public Vector3 SpawnPoint(int i) => cages[i].position + cages[i].forward * spawnDepth;

        void Awake()
        {
            Instance = this;
            _rng = GameRandom.Fork(RngStream.Hazards, GameRandom.KeyOf("beast_cages"));
            int n = cages.Length;
            _phase = new Phase[n];
            _t = new float[n];
            _nextOut = new float[n];
            _left = new int[n];
            _home = new Vector3[n];
            for (int i = 0; i < n; i++) _home[i] = i < bars.Length && bars[i] != null ? bars[i].localPosition : Vector3.zero;
            _nextAuto = first;
        }

        void Start()
        {
            // the beasts aren't in the Director's table: their pool fills here, before the fight (no allocation at a release)
            var em = EnemyManager.Instance;
            if (em != null && beast != null) em.Prewarm(beast, Mathf.Max(beast.prewarm, pack.y * 2));
        }

        void OnEnable() => GameLoop.Register(this, TickGroup.Enemies, 35);
        void OnDisable() => GameLoop.Unregister(this);
        void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>The pack size now: grows from pack.x to pack.y over the run's timer.</summary>
        public int PackNow()
        {
            var run = RunState.Current;
            float k = run != null && run.config != null ? run.Elapsed / Mathf.Max(1f, run.config.runDuration) : 0f;
            return Mathf.RoundToInt(Mathf.Lerp(pack.x, pack.y, Mathf.Clamp01(k)));
        }

        /// <summary>Cage i telegraphs, then releases `count` beasts (0 = the run's pack size). False if it's busy.</summary>
        public bool Open(int i, int count = 0)
        {
            if (i < 0 || i >= cages.Length || IsBusy(i) || beast == null) return false;
            _phase[i] = Phase.Telegraph;
            _t[i] = 0f;
            _left[i] = count > 0 ? count : PackNow();
            Opened++;
            SfxPlayer.Play(SfxId.Rattle, cages[i].position);
            return true;
        }

        /// <summary>Every idle cage opens at once (the Beast Lord's phase 2). Returns how many did.</summary>
        public int OpenAll(int count = 0)
        {
            int n = 0;
            for (int i = 0; i < cages.Length; i++) if (Open(i, count)) n++;
            return n;
        }

        /// <summary>Every idle cage's bars rattle for `seconds` without opening (the Beast Lord's entrance).</summary>
        public void RattleAll(float seconds)
        {
            _rattle = seconds;
            if (cages.Length > 0) SfxPlayer.Play2D(SfxId.Rattle);
        }

        /// <summary>The idle cage nearest `p` (-1 if all are busy).</summary>
        public int NearestIdle(Vector3 p)
        {
            int best = -1;
            float bd = float.MaxValue;
            for (int i = 0; i < cages.Length; i++)
            {
                if (IsBusy(i)) continue;
                float d = (cages[i].position - p).sqrMagnitude;
                if (d < bd) { bd = d; best = i; }
            }
            return best;
        }

        public void Tick(float dt, float unscaledDt)
        {
            if (dt <= 0f || cages.Length == 0) return;
            var run = RunState.Current;
            // not once the clock is out (TimerDone lags a frame behind a jump to 0:00: "boss now")
            if (every > 0f && run != null && !run.TimerDone && run.TimeRemaining > 0f && run.Elapsed >= _nextAuto)
            {
                _nextAuto = run.Elapsed + every;
                int start = (int)_rng.Range(0f, cages.Length - 0.001f);
                for (int k = 0; k < cages.Length; k++) if (Open((start + k) % cages.Length)) break;
            }
            var tele = TelegraphRenderer.Instance;
            var em = EnemyManager.Instance;
            if (_rattle > 0f) _rattle -= dt;
            for (int i = 0; i < cages.Length; i++)
            {
                if (_phase[i] == Phase.Idle)
                {
                    var b = i < bars.Length ? bars[i] : null;
                    if (b != null) b.localPosition = _home[i] + (_rattle > 0f ? new Vector3(Mathf.Sin((_rattle + i) * 70f) * 0.05f, 0f, 0f) : Vector3.zero);
                    continue;
                }
                _t[i] += dt;
                var cage = cages[i];
                var bar = i < bars.Length ? bars[i] : null;
                switch (_phase[i])
                {
                    case Phase.Telegraph:
                    {
                        Vector3 from = cage.position + cage.forward * 0.4f;
                        if (tele != null) tele.Lane(from, cage.forward, laneLength, laneWidth, _t[i] / telegraph, telegraphColor);
                        if (bar != null) bar.localPosition = _home[i] + new Vector3(Mathf.Sin(_t[i] * 70f) * 0.05f, 0f, 0f);
                        if (_t[i] >= telegraph)
                        {
                            _phase[i] = Phase.Release;
                            _t[i] = 0f;
                            _nextOut[i] = liftTime;
                            SfxPlayer.Play(SfxId.Gate, cage.position);
                            if (CrowdExcitement.Instance != null) CrowdExcitement.Instance.Spike(0.5f);
                        }
                        break;
                    }
                    case Phase.Release:
                        if (bar != null) bar.localPosition = _home[i] + Vector3.up * (liftHeight * Mathf.Min(1f, _t[i] / liftTime));
                        if (_t[i] >= _nextOut[i] && _left[i] > 0)
                        {
                            _nextOut[i] += releaseSpacing;
                            _left[i]--;
                            if (em != null)
                            {
                                Vector3 p = SpawnPoint(i) + cage.right * _rng.Range(-1.1f, 1.1f);
                                if (em.Spawn(beast, p) >= 0) Released++;
                            }
                        }
                        if (_left[i] <= 0) { _phase[i] = Phase.Hold; _t[i] = 0f; }
                        break;
                    case Phase.Hold:
                        if (_t[i] >= holdOpen) { _phase[i] = Phase.Closing; _t[i] = 0f; }
                        break;
                    case Phase.Closing:
                        if (bar != null) bar.localPosition = _home[i] + Vector3.up * (liftHeight * Mathf.Max(0f, 1f - _t[i] / liftTime));
                        if (_t[i] >= liftTime) _phase[i] = Phase.Idle;
                        break;
                }
            }
        }
    }
}
