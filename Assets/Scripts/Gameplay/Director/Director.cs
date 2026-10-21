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
        int _swarmLeft, _prewarmIndex, _escortIndex;
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
        [Tooltip("The arena's ArenaLayoutDef.spawnMaxDistance: ring spawns within this distance of the player when the edge allows it (0 = anywhere).")]
        public float maxSpawnDistance;
        [Tooltip("The arena's ArenaLayoutDef.densityMul: multiplies the target curve and the spawn rate (C18).")]
        public float arenaDensity = 1f;
        float Density => densityMultiplier * (arenaDensity > 0f ? arenaDensity : 1f);
        [Tooltip("The arena's ArenaLayoutDef.eliteEvery (0 = DirectorConfig.eliteEvery).")]
        public float eliteEveryOverride;
        float EliteEvery => eliteEveryOverride > 0f ? eliteEveryOverride : config.eliteEvery;
        int HardCap => maxAliveOverride > 0 ? Mathf.Min(config.hardCap, maxAliveOverride) : config.hardCap;
        [Tooltip("The arena's ArenaLayoutDef.swarmMul: multiplies every swarm's size (R2: the Colosseum's huge waves).")]
        public float swarmMultiplier = 1f;
        float SwarmMul => swarmMultiplier > 0f ? swarmMultiplier : 1f;
        /// <summary>This arena's cap on enemies alive (the Hypogeum's lift packs respect it too); 0 without a config.</summary>
        public int AliveCap => config != null ? HardCap : 0;

        public int TargetAlive => config == null ? 0 : Mathf.Min(HardCap, Mathf.RoundToInt(config.targetAlive.Evaluate(Clock) * Density));
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
                float total = Mathf.Min(HardCap, config.targetAlive.Evaluate(t) * Density);
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
            int swarm = Mathf.CeilToInt(config.swarmSize.Evaluate(runLength) * densityMultiplier * SwarmMul);
            for (int i = 0; i < n; i++)
            {
                if (entries[i].swarm) need[i] += swarm;
                need[i] = Mathf.CeilToInt(need[i] * 1.1f) + 8;
            }
            return need;
        }

        /// <summary>True once every table type and every escort they bring has its pool.</summary>
        public bool Prewarmed => table == null || (_prewarmIndex >= table.entries.Length && _escortIndex >= table.entries.Length);

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
            // escorts that aren't table entries themselves (a Centurion's Praetorians) fill to their own prewarm:
            // without this their pool grew at runtime 17-20 times a run (R1 bot runs)
            while (budget > 0 && _prewarmIndex >= table.entries.Length && _escortIndex < table.entries.Length)
            {
                var lead = table.entries[_escortIndex].enemy;
                var escort = lead != null ? lead.escort : null;
                int made = escort != null ? em.PrewarmStep(escort, escort.prewarm, budget) : 0;
                budget -= made;
                if (made == 0 || budget > 0) _escortIndex++;
            }
        }

        void OnEnable()
        {
            Instance = this; // also after a disable/enable (admin toggle): OnDisable cleared it
            GameLoop.Register(this, TickGroup.Director, -10);
        }

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
            if (t > _nextElite + 2f) _nextElite = t + EliteEvery * 0.5f;

            // steady pressure
            int target = TargetAlive;
            if (em.AliveCount < target)
            {
                _spawnAcc = Mathf.Min(_spawnAcc + config.spawnPerSecond.Evaluate(t) * (arenaDensity > 0f ? arenaDensity : 1f) * dt, 8f);
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
                BeginSwarm(em, t, runLength, PickGate(em));
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
                _nextElite = t + EliteEvery;
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

        /// <summary>A swarm from now on through `gate` (-1 = from a random side of the ring). False if no swarm type is active.</summary>
        bool BeginSwarm(EnemyManager em, float t, float runLength, int gate)
        {
            _swarmEntry = Pick(em, t, runLength, swarmOnly: true, eliteOnly: false);
            if (_swarmEntry < 0) return false;
            if (_gates == null) _gates = ArenaGates.Instance;
            _swarmLeft = Mathf.RoundToInt(config.swarmSize.Evaluate(t) * densityMultiplier * SwarmMul);
            _swarmRate = _swarmLeft / Mathf.Max(0.1f, config.swarmDuration);
            _swarmAcc = 1f; // first one right away
            _swarmAngle = RandomAngle();
            _swarmGate = _gates != null && gate >= 0 && gate < _gates.Count ? gate : -1;
            SwarmsStarted++;
            Vector3 at = _swarmGate >= 0 ? _gates.Position(_swarmGate) : RingPoint(em, _swarmAngle);
            if (_swarmGate >= 0) _gates.Open(_swarmGate, config.swarmDuration + 1.2f);
            GameEvents.OnDirector.Raise(new DirectorEvent { Kind = DirectorEventKind.Swarm, Position = at, Color = em.telegraphColor });
            return true;
        }

        float RunLength => RunState.Current != null ? RunState.Current.config.runDuration : 480f;

        /// <summary>
        /// The Arena Master's "gates thrown open" whim (Phase 5.1): a swarm right now through `gate`, any gate, even the one
        /// next to the player (-1 = the ring). The swarm pours in over the next ticks, so the Director must be running.
        /// </summary>
        public bool StartSwarmThrough(int gate)
        {
            var em = EnemyManager.Instance;
            if (em == null || config == null || table == null || table.entries.Length == 0) return false;
            return BeginSwarm(em, Clock, RunLength, gate);
        }

        /// <summary>The Arena Master's champion (Phase 5.1): an elite of the table's elite types, random affix, at `pos`. Slot or -1.</summary>
        public int SpawnEliteAt(Vector3 pos)
        {
            var em = EnemyManager.Instance;
            if (em == null || config == null || table == null || table.affixes.Length == 0) return -1;
            int k = Pick(em, Clock, RunLength, swarmOnly: false, eliteOnly: true);
            if (k < 0) return -1;
            var affix = table.affixes[GameRandom.Range(0, table.affixes.Length)];
            int slot = em.Spawn(table.entries[k].enemy, pos, affix);
            if (slot >= 0)
            {
                ElitesSent++;
                GameEvents.OnDirector.Raise(new DirectorEvent { Kind = DirectorEventKind.Elite, Position = em.GetPosition(slot), Color = affix.color });
            }
            return slot;
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
            return em.EdgePoint(angle, config.ringInset);
        }

        /// <summary>Spawns near `angle` on the ring, nudged away from the player and (if asked) out of view.</summary>
        int SpawnAt(EnemyManager em, EnemyDef def, float angle, AffixDef affix, bool skipOffscreenTest = false)
        {
            Vector3 player = Player.Main != null ? Player.Main.transform.position : em.arenaCenter;
            float min2 = config.minPlayerDistance * config.minPlayerDistance;
            float max2 = maxSpawnDistance > 0f ? maxSpawnDistance * maxSpawnDistance : float.MaxValue;
            Vector3 best = RingPoint(em, angle);
            for (int attempt = 0; attempt < 8; attempt++)
            {
                float a = attempt == 0 ? angle : (skipOffscreenTest ? angle + GameRandom.Range(-0.4f, 0.4f) : RandomAngle());
                Vector3 p = RingPoint(em, a);
                // a little depth so a ring of spawns doesn't look like a fence of enemies
                p += (em.arenaCenter - p).normalized * GameRandom.Range(0f, 2.5f);
                float dx = p.x - player.x, dz = p.z - player.z, d2 = dx * dx + dz * dz;
                if (d2 < min2 || (d2 > max2 && !skipOffscreenTest)) continue; // a swarm keeps its side, however far
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
