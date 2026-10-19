using Gladiator.Core;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// Runs the horde (CLAUDE.md §6.5): keeps `targetAlive(t)` enemies alive by picking from the arena's weighted
    /// spawn table (time windows, ramping weights, per-type caps), pours in a swarm from one side every so often, and
    /// sends an elite every ~90 s. Uses the run clock, so Timeless freezes the pacing and the admin timer jumps it.
    /// Spawns on a ring near the arena edge, away from the player and preferably off-screen. Zero allocation.
    /// </summary>
    public sealed class Director : MonoBehaviour, ITickable
    {
        public static Director Instance { get; private set; }

        public DirectorConfig config;
        public SpawnTableDef table;
        [Tooltip("Multiplies the target-alive curve (difficulty knob; admin/tests).")]
        public float densityMultiplier = 1f;

        [Tooltip("Enemy views created per frame while filling the pools at the start of the run (spreads the load hitch).")]
        public int prewarmPerFrame = 16;

        float _spawnAcc, _nextSwarm, _nextElite;
        int _swarmLeft, _prewarmIndex;
        float _swarmAcc, _swarmAngle, _swarmRate;
        int _swarmEntry = -1, _swarmGate = -1;
        ArenaGates _gates;
        int[] _poolTargets;
        Transform _cam;

        public int SwarmsStarted { get; private set; }
        public int ElitesSent { get; private set; }
        public float Clock => RunState.Current != null ? RunState.Current.Elapsed : Time.timeSinceLevelLoad;
        [Tooltip("This arena's cap on enemies alive (0 = DirectorConfig.hardCap). Set from the arena layout.")]
        public int maxAliveOverride;
        int HardCap => maxAliveOverride > 0 ? Mathf.Min(config.hardCap, maxAliveOverride) : config.hardCap;

        public int TargetAlive => config == null ? 0 : Mathf.Min(HardCap, Mathf.RoundToInt(config.targetAlive.Evaluate(Clock) * densityMultiplier));
        public float NextSwarmAt => _nextSwarm;
        public float NextEliteAt => _nextElite;

        void Awake()
        {
            Instance = this;
            if (config != null) { _nextSwarm = config.firstSwarm; _nextElite = config.firstElite; }
        }

        /// <summary>
        /// Views each table entry needs over the whole run. Simulates the head-count every 5 s: the target curve is
        /// shared out by weight among the active entries, a type at its cap hands its leftover to the others (so the
        /// uncapped convicts end up with far more than their weight share), then a full swarm is added on top for
        /// swarm types (swarms ignore caps) plus slack for enemies still playing their death. Pools fill a few views
        /// per frame from the first frame, so the run never instantiates mid-fight and the load hitch stays small.
        /// </summary>
        public int[] ComputePoolTargets(float runLength)
        {
            var entries = table.entries;
            int n = entries.Length;
            var need = new int[n];
            var share = new float[n];
            var open = new bool[n];
            for (float t = 0f; t <= runLength + 0.01f; t += 5f)
            {
                float total = Mathf.Min(HardCap, config.targetAlive.Evaluate(t) * densityMultiplier);
                for (int i = 0; i < n; i++)
                {
                    share[i] = 0f;
                    open[i] = SpawnTableDef.Active(entries[i], t) && SpawnTableDef.Weight(entries[i], t, runLength) > 0f;
                }
                float left = total;
                for (int pass = 0; pass < n && left > 0.01f; pass++)
                {
                    float wsum = 0f;
                    for (int i = 0; i < n; i++) if (open[i]) wsum += SpawnTableDef.Weight(entries[i], t, runLength);
                    if (wsum <= 0f) break;
                    bool hitCap = false;
                    for (int i = 0; i < n; i++)
                    {
                        if (!open[i] || entries[i].maxAlive <= 0) continue;
                        float want = left * SpawnTableDef.Weight(entries[i], t, runLength) / wsum;
                        float room = entries[i].maxAlive - share[i];
                        if (want < room) continue;
                        share[i] += room;
                        open[i] = false;
                        hitCap = true;
                    }
                    if (!hitCap)
                    {
                        for (int i = 0; i < n; i++)
                            if (open[i]) share[i] += left * SpawnTableDef.Weight(entries[i], t, runLength) / wsum;
                        break;
                    }
                    left = total;
                    for (int i = 0; i < n; i++) left -= share[i];
                }
                for (int i = 0; i < n; i++) need[i] = Mathf.Max(need[i], Mathf.CeilToInt(share[i]));
            }
            int swarm = Mathf.CeilToInt(config.swarmSize.Evaluate(runLength) * densityMultiplier);
            for (int i = 0; i < n; i++)
            {
                if (entries[i].swarm) need[i] += swarm;
                need[i] = Mathf.CeilToInt(need[i] * 1.1f) + 8;
            }
            return need;
        }

        /// <summary>True once every table type has its pool.</summary>
        public bool Prewarmed => table == null || _prewarmIndex >= table.entries.Length;

        void PrewarmStep(EnemyManager em)
        {
            if (_poolTargets == null)
                _poolTargets = ComputePoolTargets(RunState.Current != null ? RunState.Current.config.runDuration : 480f);
            int budget = prewarmPerFrame;
            while (budget > 0 && _prewarmIndex < table.entries.Length)
            {
                var e = table.entries[_prewarmIndex];
                int made = e.enemy != null ? em.PrewarmStep(e.enemy, _poolTargets[_prewarmIndex], budget) : 0;
                budget -= made;
                if (made == 0 || budget > 0) _prewarmIndex++;
            }
        }

        void OnEnable() => GameLoop.Register(this, TickGroup.Director, -10);

        void OnDisable()
        {
            GameLoop.Unregister(this);
            if (Instance == this) Instance = null;
        }

        /// <summary>Admin/tests: start the next swarm / elite now.</summary>
        public void TriggerSwarmNow() => _nextSwarm = Clock;
        public void TriggerEliteNow() => _nextElite = Clock;

        public void Tick(float dt, float unscaledDt)
        {
            var em = EnemyManager.Instance;
            var run = RunState.Current;
            if (em == null || config == null || table == null || table.entries.Length == 0) return;
            if (!Prewarmed) { PrewarmStep(em); return; } // pools first (also while the arena intro holds the game paused)
            if (dt <= 0f) return;
            if (run != null && run.Ended) { _swarmLeft = 0; return; }
            if (_cam == null && Camera.main != null) _cam = Camera.main.transform;
            float t = Clock;
            float runLength = run != null ? run.config.runDuration : 480f;
            // the clock jumped (admin timer, tests): reschedule instead of firing every missed event at once
            if (t > _nextSwarm + 2f) _nextSwarm = t + config.swarmEvery * 0.5f;
            if (t > _nextElite + 2f) _nextElite = t + config.eliteEvery * 0.5f;

            // steady pressure
            int target = TargetAlive;
            if (em.AliveCount < target)
            {
                _spawnAcc = Mathf.Min(_spawnAcc + config.spawnPerSecond.Evaluate(t) * dt, 8f);
                while (_spawnAcc >= 1f && em.AliveCount < target)
                {
                    _spawnAcc -= 1f;
                    int k = Pick(em, t, runLength, swarmOnly: false, eliteOnly: false);
                    if (k < 0) break;
                    SpawnAt(em, table.entries[k].enemy, RandomAngle(), null);
                }
            }
            else _spawnAcc = 0f;

            // swarm: a burst from one direction on top of the target
            if (t >= _nextSwarm)
            {
                _nextSwarm = t + config.swarmEvery;
                _swarmEntry = Pick(em, t, runLength, swarmOnly: true, eliteOnly: false);
                if (_swarmEntry >= 0)
                {
                    _swarmLeft = Mathf.RoundToInt(config.swarmSize.Evaluate(t) * densityMultiplier);
                    _swarmRate = _swarmLeft / Mathf.Max(0.1f, config.swarmDuration);
                    _swarmAcc = 1f; // first one right away
                    _swarmAngle = RandomAngle();
                    _swarmGate = PickGate(em);
                    SwarmsStarted++;
                    Vector3 at = _swarmGate >= 0 ? _gates.Position(_swarmGate) : RingPoint(em, _swarmAngle);
                    if (_swarmGate >= 0) _gates.Open(_swarmGate, config.swarmDuration + 1.2f);
                    GameEvents.OnDirector.Raise(new DirectorEvent { Kind = DirectorEventKind.Swarm, Position = at, Color = em.telegraphColor });
                }
            }
            if (_swarmLeft > 0 && _swarmEntry >= 0)
            {
                _swarmAcc += dt * _swarmRate;
                float halfArc = config.swarmArc * 0.5f * Mathf.Deg2Rad;
                var def = table.entries[_swarmEntry].enemy;
                while (_swarmAcc >= 1f && _swarmLeft > 0 && em.AliveCount < HardCap)
                {
                    _swarmAcc -= 1f;
                    _swarmLeft--;
                    if (_swarmGate >= 0) em.Spawn(def, _gates.SpawnPoint(_swarmGate));
                    else SpawnAt(em, def, _swarmAngle + GameRandom.Range(-halfArc, halfArc), null, skipOffscreenTest: true);
                }
                if (em.AliveCount >= HardCap) _swarmLeft = 0;
            }

            // elite cadence
            if (t >= _nextElite)
            {
                _nextElite = t + config.eliteEvery;
                int k = Pick(em, t, runLength, swarmOnly: false, eliteOnly: true);
                if (k >= 0 && table.affixes.Length > 0)
                {
                    var affix = table.affixes[GameRandom.Range(0, table.affixes.Length)];
                    int gate = PickGate(em);
                    int slot;
                    if (gate >= 0) { slot = em.Spawn(table.entries[k].enemy, _gates.SpawnPoint(gate), affix); _gates.Open(gate, 2.5f); }
                    else slot = SpawnAt(em, table.entries[k].enemy, RandomAngle(), affix);
                    if (slot >= 0)
                    {
                        ElitesSent++;
                        GameEvents.OnDirector.Raise(new DirectorEvent { Kind = DirectorEventKind.Elite, Position = em.GetPosition(slot), Color = affix.color });
                    }
                }
            }
        }

        /// <summary>Weighted pick among entries active at t and under their cap. -1 if none.</summary>
        int Pick(EnemyManager em, float t, float runLength, bool swarmOnly, bool eliteOnly)
        {
            var entries = table.entries;
            float total = 0f;
            for (int i = 0; i < entries.Length; i++)
            {
                if (!Eligible(em, entries[i], t, swarmOnly, eliteOnly)) continue;
                total += SpawnTableDef.Weight(entries[i], t, runLength);
            }
            if (total <= 0f) return -1;
            float r = GameRandom.Value * total;
            int last = -1;
            for (int i = 0; i < entries.Length; i++)
            {
                if (!Eligible(em, entries[i], t, swarmOnly, eliteOnly)) continue;
                float w = SpawnTableDef.Weight(entries[i], t, runLength);
                if (w <= 0f) continue;
                last = i;
                if (r < w) return i;
                r -= w;
            }
            return last;
        }

        static bool Eligible(EnemyManager em, in SpawnTableDef.Entry e, float t, bool swarmOnly, bool eliteOnly)
        {
            if (!SpawnTableDef.Active(e, t)) return false;
            if (swarmOnly && !e.swarm) return false;
            if (eliteOnly) return e.elite; // the elite ignores the cap: it's one special enemy
            return e.maxAlive <= 0 || em.AliveOf(e.enemy) < e.maxAlive;
        }

        static float RandomAngle() => GameRandom.Range(0f, Mathf.PI * 2f);

        /// <summary>A gate away from the player for a swarm or an elite (-1 = no gates: use the ring).</summary>
        int PickGate(EnemyManager em)
        {
            if (_gates == null) _gates = ArenaGates.Instance;
            if (_gates == null || _gates.Count == 0) return -1;
            Vector3 player = Player.Main != null ? Player.Main.transform.position : em.arenaCenter;
            return _gates.Pick(player, config.minPlayerDistance);
        }

        Vector3 RingPoint(EnemyManager em, float angle)
        {
            float r = em.arenaRadius - config.ringInset;
            return em.arenaCenter + new Vector3(Mathf.Cos(angle) * r, 0f, Mathf.Sin(angle) * r);
        }

        /// <summary>Spawns near `angle` on the ring, nudged away from the player and (if asked) out of view.</summary>
        int SpawnAt(EnemyManager em, EnemyDef def, float angle, AffixDef affix, bool skipOffscreenTest = false)
        {
            Vector3 player = Player.Main != null ? Player.Main.transform.position : em.arenaCenter;
            float min2 = config.minPlayerDistance * config.minPlayerDistance;
            Vector3 best = RingPoint(em, angle);
            for (int attempt = 0; attempt < 6; attempt++)
            {
                float a = attempt == 0 ? angle : (skipOffscreenTest ? angle + GameRandom.Range(-0.4f, 0.4f) : RandomAngle());
                Vector3 p = RingPoint(em, a);
                // a little depth so a ring of spawns doesn't look like a fence of enemies
                p += (em.arenaCenter - p).normalized * GameRandom.Range(0f, 2.5f);
                float dx = p.x - player.x, dz = p.z - player.z;
                if (dx * dx + dz * dz < min2) continue;
                best = p;
                if (skipOffscreenTest || !config.preferOffscreen || _cam == null || !InView(p)) break;
            }
            return em.Spawn(def, best, affix);
        }

        bool InView(Vector3 p)
        {
            Vector3 d = p - _cam.position;
            if (Vector3.Dot(d, _cam.forward) <= 0f) return false;
            var cam = Camera.main;
            if (cam == null) return false;
            Vector3 vp = cam.WorldToViewportPoint(p + Vector3.up * 0.8f);
            return vp.x > -0.05f && vp.x < 1.05f && vp.y > -0.05f && vp.y < 1.05f;
        }
    }
}
