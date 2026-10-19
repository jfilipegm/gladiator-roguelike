using System.Collections.Generic;
using Gladiator.Core;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>Pooled visual of one enemy (GameObject + rigid-part rig + renderers).</summary>
    public sealed class EnemyView
    {
        public EnemyDef Def;
        public GameObject Go;
        public Transform Root;
        public PartRig Rig;
        public MeshRenderer[] Renderers;
        public bool Flashing;
    }

    /// <summary>An enemy worth pointing at from the screen edge (winding up an attack, or an elite).</summary>
    public struct EnemyThreat
    {
        public Vector3 Position;
        public Color Color;
        public bool Windup;
    }

    /// <summary>
    /// All enemies, one central tick (CLAUDE.md §4): struct-of-arrays state, pooled views, spatial-hash separation,
    /// static-obstacle push-out, baked height grid for terrain, step-height rule (no wall climbing), contact damage,
    /// hit flash / knockback / recoil, rigid-part run cycle, death pop. Per-enemy attack state machine for the
    /// behaviours in <see cref="EnemyBehaviour"/> (wind-up → attack → recover, telegraphed in orange-red) and elite
    /// affixes (<see cref="AffixDef"/>). Zero allocation in steady state.
    /// </summary>
    public sealed partial class EnemyManager : MonoBehaviour, ITickable
    {
        public static EnemyManager Instance { get; private set; }

        [Header("World")]
        public int capacity = 1024;
        public Vector3 arenaCenter = Vector3.zero;
        public float arenaRadius = 20f;
        public Vector2 boundsMin = new Vector2(-26f, -26f);
        public Vector2 boundsMax = new Vector2(26f, 26f);
        public float gridCellSize = 2f;
        public float heightCellSize = 0.5f;
        [Tooltip("Editor-baked height grid + obstacles for this arena (rebuilt with the arena). Missing/stale → runtime bake.")]
        public ArenaNavData navData;
        [Tooltip("Max climb per tick between height samples (m). Higher steps are walls.")]
        public float maxStepUp = 0.35f;

        [Header("Look")]
        public Material bodyMaterial;
        public Material flashMaterial;
        public HitFeedbackConfig feedback;
        public DeathPopFx deathPop;
        [Tooltip("Rigs further than this from the camera skip part animation.")]
        public float animDistance = 45f;

        [Header("Crowding")]
        public float separationStrength = 8f;
        public int maxNeighbours = 10;
        public float playerRadius = 0.35f;

        [Header("Attacks")]
        [Tooltip("Orange-red: telegraphs, wind-up rings (CLAUDE.md §5 colour language).")]
        public Color telegraphColor = new Color(1f, 0.22f, 0.06f, 0.95f);
        [Tooltip("Brute slam knock-up on a grounded player: outward and upward speed (m/s).")]
        public Vector2 slamImpulse = new Vector2(6f, 6.5f);
        public Color slamDustA = new Color(0.55f, 0.42f, 0.3f), slamDustB = new Color(0.75f, 0.62f, 0.45f);
        public Color blastColorA = new Color(1f, 0.48f, 0.12f), blastColorB = new Color(1f, 0.86f, 0.4f);

        // ---- state (struct of arrays) ----
        Vector3[] _pos, _vel, _knock;
        float[] _hp, _maxHp, _speed, _radius, _flash, _contactCd, _phase, _recoil, _dying, _yaw;
        int[] _uid;
        bool[] _used, _active;
        EnemyView[] _view;
        // attack state machine + affixes
        enum AiState : byte { Move = 0, Windup = 1, Attack = 2, Recover = 3 }
        AiState[] _state;
        float[] _stateT, _stateDur, _attackCd, _strafe, _dmgTakenMul, _knockMul, _atkTimeMul, _scale;
        Vector3[] _aim;
        bool[] _attackHit, _invuln, _scriptedWindup;
        AffixDef[] _affix;

        struct Blast { public Vector3 Pos; public float T, Fuse, Radius, ToPlayer, ToEnemies; }
        readonly Blast[] _blasts = new Blast[64];
        int _blastCount;
        readonly int[] _blastQuery = new int[512];
        readonly EnemyThreat[] _threats = new EnemyThreat[64];
        int _threatCount;
        float _nextHitStop;   // crit and elite-hit stops share one cooldown (unscaled)
        PlayerMotor _playerMotor;
        int[] _free;
        int _freeCount, _high, _nextUid = 1;

        SpatialHashGrid _grid;
        HeightGrid _height;
        StaticObstacles _obstacles;
        readonly int[] _query = new int[512];
        readonly Dictionary<EnemyDef, Stack<EnemyView>> _pools = new Dictionary<EnemyDef, Stack<EnemyView>>();
        // keys are added when a type's pool is created (prewarm), so a type's first spawn mid-run never resizes it
        readonly Dictionary<EnemyDef, int> _aliveByDef = new Dictionary<EnemyDef, int>(32);
        Transform _poolRoot;
        Transform _player;
        PlayerHealth _playerHealth;
        MudZones _mud;
        float _maxRadius = 0.5f;

        public int AliveCount { get; private set; }
        /// <summary>Alive enemies of one type (spawners cap kiting types that would otherwise pile up).</summary>
        public int AliveOf(EnemyDef def) => def != null && _aliveByDef.TryGetValue(def, out int n) ? n : 0;
        /// <summary>Views created during play because a pool was empty (should stay 0 after warm-up).</summary>
        public int PoolGrowths { get; private set; }
        public int HighWater => _high;
        public HeightGrid Height => _height;
        public StaticObstacles Obstacles => _obstacles;
        public SpatialHashGrid Grid => _grid;
        /// <summary>Enemies winding up an attack + elites, rebuilt every tick (threat indicators, bot).</summary>
        public int ThreatCount => _threatCount;
        public EnemyThreat GetThreat(int k) => _threats[k];
        public int PendingBlasts => _blastCount;

        void Awake()
        {
            Instance = this;
            _pos = new Vector3[capacity]; _vel = new Vector3[capacity]; _knock = new Vector3[capacity];
            _hp = new float[capacity]; _maxHp = new float[capacity]; _speed = new float[capacity]; _radius = new float[capacity];
            _flash = new float[capacity]; _contactCd = new float[capacity]; _phase = new float[capacity];
            _recoil = new float[capacity]; _dying = new float[capacity]; _yaw = new float[capacity];
            _uid = new int[capacity]; _used = new bool[capacity]; _active = new bool[capacity];
            _state = new AiState[capacity]; _stateT = new float[capacity]; _stateDur = new float[capacity];
            _attackCd = new float[capacity]; _strafe = new float[capacity]; _dmgTakenMul = new float[capacity];
            _knockMul = new float[capacity]; _atkTimeMul = new float[capacity]; _scale = new float[capacity];
            _aim = new Vector3[capacity]; _attackHit = new bool[capacity]; _affix = new AffixDef[capacity];
            _view = new EnemyView[capacity];
            _free = new int[capacity];
            _invuln = new bool[capacity];
            _scriptedWindup = new bool[capacity];
            AllocStatus();
            for (int i = capacity - 1; i >= 0; i--) _free[_freeCount++] = i;
            _grid = new SpatialHashGrid(boundsMin, boundsMax, gridCellSize, capacity);
            _poolRoot = new GameObject("[EnemyPool]").transform;
            _poolRoot.SetParent(transform, false);
        }

        void Start()
        {
            BakeWorld();
            var p = Player.Main;
            if (p != null) { _player = p.transform; _playerHealth = p.GetComponent<PlayerHealth>(); _playerMotor = p.GetComponent<PlayerMotor>(); }
        }

        /// <summary>Uses the Editor-baked nav data when it matches; otherwise bakes from the scene now (slower, logged).</summary>
        public void BakeWorld()
        {
            if (navData != null && navData.Matches(boundsMin, boundsMax, heightCellSize))
            {
                _height = navData.ToHeightGrid();
                _obstacles = navData.ToObstacles();
                return;
            }
            BakeScene(boundsMin, boundsMax, heightCellSize, out _height, out _obstacles);
            Debug.Log($"[EnemyManager] runtime-baked height grid {_height.W}x{_height.H}, {_obstacles.Count} obstacles (no matching ArenaNavData — rebuild the arena to bake it)");
        }

        /// <summary>
        /// Height grid from walkable surfaces + obstacle list from everything else (same code at runtime and in the
        /// Editor bake). Physics must be in sync with the scene (Physics.SyncTransforms in edit mode).
        /// </summary>
        public static void BakeScene(Vector2 min, Vector2 max, float cell, out HeightGrid height, out StaticObstacles obstacles)
        {
            height = HeightGrid.Bake(min, max, cell, Layers.EnvironmentMask, 50f, 0f,
                col => col.GetComponentInParent<WalkableSurface>() != null);
            var walkables = new HashSet<Collider>();
            foreach (var w in FindObjectsByType<WalkableSurface>())
                foreach (var c in w.GetComponentsInChildren<Collider>()) walkables.Add(c);
            obstacles = StaticObstacles.FromScene(Layers.SolidMask, 0.5f, height, walkables);
        }

        void OnEnable() => GameLoop.Register(this, TickGroup.Enemies);
        void OnDisable() { GameLoop.Unregister(this); if (Instance == this) Instance = null; }

        // ------------------------------------------------------------------ pools / spawning

        public void Prewarm(EnemyDef def, int count)
        {
            var stack = GetPool(def);
            for (int i = stack.Count; i < count; i++) stack.Push(CreateView(def));
        }

        /// <summary>Incremental prewarm: grows `def`'s pool toward `total` views (free + in use), creating at most `maxCreate`. Returns how many it created.</summary>
        public int PrewarmStep(EnemyDef def, int total, int maxCreate)
        {
            var stack = GetPool(def);
            int have = stack.Count + AliveOf(def);
            int n = 0;
            while (have + n < total && n < maxCreate) { stack.Push(CreateView(def)); n++; }
            return n;
        }

        Stack<EnemyView> GetPool(EnemyDef def)
        {
            if (!_pools.TryGetValue(def, out var s))
            {
                s = new Stack<EnemyView>(def.prewarm);
                _pools[def] = s;
                if (!_aliveByDef.ContainsKey(def)) _aliveByDef[def] = 0;
            }
            return s;
        }

        EnemyView CreateView(EnemyDef def)
        {
            var go = new GameObject(def.id);
            go.layer = Layers.Enemy;
            go.transform.SetParent(_poolRoot, false);
            var model = Instantiate(def.model, go.transform);
            model.transform.localScale = Vector3.one * def.scale;
            var renderers = model.GetComponentsInChildren<MeshRenderer>(true);
            foreach (var r in renderers)
            {
                r.sharedMaterial = bodyMaterial;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; // blob shadows instead
                r.receiveShadows = true;
                r.gameObject.layer = Layers.Enemy;
            }
            go.SetActive(false);
            return new EnemyView { Def = def, Go = go, Root = go.transform, Rig = new PartRig(model.transform), Renderers = renderers };
        }

        /// <summary>Returns the slot index, or -1 when full. With an affix the enemy is an elite.</summary>
        public int Spawn(EnemyDef def, Vector3 position, AffixDef affix = null)
        {
            if (_freeCount == 0) return -1;
            int i = _free[--_freeCount];
            var pool = GetPool(def);
            EnemyView v;
            if (pool.Count > 0) v = pool.Pop();
            else
            {
                // Pool ran dry mid-run: creating a view allocates (~4.5 KB). Raise that type's prewarm.
                v = CreateView(def);
                PoolGrowths++;
                Debug.LogWarning($"[EnemyManager] pool grew at runtime: {def.id} ({PoolGrowths} so far)");
            }
            v.Go.SetActive(true);
            if (v.Flashing) SetMaterial(v, bodyMaterial);
            _view[i] = v;
            _used[i] = true;
            _active[i] = true;
            if (_height != null) position.y = _height.Sample(position.x, position.z);
            _pos[i] = position;
            _vel[i] = Vector3.zero;
            _knock[i] = Vector3.zero;
            float scale = affix != null ? affix.scale : 1f;
            _affix[i] = affix;
            _scale[i] = scale;
            _hp[i] = _maxHp[i] = def.maxHp * (affix != null ? affix.hpMultiplier : 1f);
            _speed[i] = def.moveSpeed * (1f + GameRandom.Range(-def.speedJitter, def.speedJitter)) * (affix != null ? affix.speedMultiplier : 1f);
            _radius[i] = def.radius * scale;
            if (_radius[i] > _maxRadius) _maxRadius = _radius[i];
            _dmgTakenMul[i] = affix != null ? affix.damageTakenMultiplier : 1f;
            _knockMul[i] = affix != null ? affix.knockbackMultiplier : 1f;
            _atkTimeMul[i] = affix != null ? affix.attackTimeMultiplier : 1f;
            _state[i] = AiState.Move;
            _stateT[i] = GameRandom.Range(1.5f, 3.5f); // Move: strafe-flip timer
            _stateDur[i] = 1f;
            _attackCd[i] = GameRandom.Range(0.6f, 1.4f) * def.attackCooldown * 0.5f; // don't all fire on the same frame
            _strafe[i] = (GameRandom.NextUInt() & 1) == 0 ? 1f : -1f;
            _attackHit[i] = false;
            _aim[i] = Vector3.forward;
            _flash[i] = 0f; _contactCd[i] = 0.5f; _phase[i] = GameRandom.Range(0f, 6.28f); _recoil[i] = 0f; _dying[i] = 0f;
            _uid[i] = _nextUid++;
            _invuln[i] = _scriptedWindup[i] = false;
            ClearStatus(i);
            Vector3 toPlayer = _player != null ? _player.position - position : Vector3.forward;
            _yaw[i] = Mathf.Atan2(toPlayer.x, toPlayer.z) * Mathf.Rad2Deg;
            v.Root.SetPositionAndRotation(position, Quaternion.Euler(0f, _yaw[i], 0f));
            v.Root.localScale = Vector3.one * scale;
            if (i >= _high) _high = i + 1;
            _aliveByDef[def] = AliveOf(def) + 1;
            AliveCount++;
            DevCounters.Enemies = AliveCount;
            return i;
        }

        void Release(int i)
        {
            var v = _view[i];
            v.Go.SetActive(false);
            GetPool(v.Def).Push(v);
            _view[i] = null;
            _affix[i] = null;
            _used[i] = false;
            _active[i] = false;
            _free[_freeCount++] = i;
            while (_high > 0 && !_used[_high - 1]) _high--;
        }

        /// <summary>Kills everything at once (run end, tests). No explosions: pending blasts are dropped too.</summary>
        /// <summary>Removes every enemy quietly: no kill events (no XP, gold, bursts), just a quick pop (boss entrance).</summary>
        public void DespawnAll()
        {
            for (int i = 0; i < _high; i++)
            {
                if (!_active[i]) continue;
                _active[i] = false;
                _dying[i] = feedback != null ? feedback.deathPopTime : 0.1f;
                AliveCount--;
                var def = _view[i].Def;
                _aliveByDef[def] = AliveOf(def) - 1;
            }
            DevCounters.Enemies = AliveCount;
            _blastCount = 0;
        }

        public void KillAll()
        {
            for (int i = 0; i < _high; i++)
                if (_active[i]) Kill(i, new DamageInfo(), allowBlast: false);
            _blastCount = 0;
        }

        // ------------------------------------------------------------------ queries (weapons, bot, pickups)

        public bool IsAlive(int i) => i >= 0 && i < _high && _active[i];
        public Vector3 GetPosition(int i) => _pos[i];
        public float GetRadius(int i) => _radius[i];
        public int GetUid(int i) => _uid[i];
        public float GetHp(int i) => _hp[i];
        public float GetMaxHp(int i) => _maxHp[i];
        public float GetYaw(int i) => _yaw[i];
        public EnemyDef GetDef(int i) => _view[i] != null ? _view[i].Def : null;
        public AffixDef GetAffix(int i) => _affix[i];
        public bool IsElite(int i) => _affix[i] != null;
        /// <summary>0 move, 1 wind-up, 2 attack, 3 recover (tests / debug).</summary>
        public int GetAiState(int i) => (int)_state[i];
        public float MaxRadius => _maxRadius;

        /// <summary>Alive enemies whose body circle touches the query circle (XZ). Returns count written to results.</summary>
        public int QueryAlive(Vector3 center, float radius, int[] results)
        {
            int n = _grid.QueryCandidates(center, radius + _maxRadius, results);
            int m = 0;
            for (int k = 0; k < n; k++)
            {
                int i = results[k];
                if (!_active[i]) continue;
                float dx = _pos[i].x - center.x, dz = _pos[i].z - center.z;
                float r = radius + _radius[i];
                if (dx * dx + dz * dz <= r * r) results[m++] = i;
            }
            return m;
        }

        public bool TryGetNearest(Vector3 from, float maxRange, out int index)
        {
            index = -1;
            int n = QueryAlive(from, maxRange, _query);
            float best = float.MaxValue;
            for (int k = 0; k < n; k++)
            {
                int i = _query[k];
                float dx = _pos[i].x - from.x, dz = _pos[i].z - from.z;
                float d2 = dx * dx + dz * dz;
                if (d2 < best) { best = d2; index = i; }
            }
            return index >= 0;
        }

        /// <summary>Sum of away-vectors weighted by closeness (for the bot).</summary>
        public Vector3 ThreatVector(Vector3 from, float radius)
        {
            int n = QueryAlive(from, radius, _query);
            Vector3 v = Vector3.zero;
            for (int k = 0; k < n; k++)
            {
                Vector3 d = from - _pos[_query[k]];
                d.y = 0f;
                float len = d.magnitude;
                if (len > 1e-3f) v += d / len * (1f - Mathf.Clamp01(len / radius));
            }
            return v;
        }

        // ------------------------------------------------------------------ damage

        /// <summary>True if a hit pushing along `dir` comes from inside the enemy's shield arc.</summary>
        public bool IsBlocking(int i, Vector3 dir)
        {
            var def = _view[i].Def;
            if (def.behaviour != EnemyBehaviour.ShieldBearer || def.blockArc <= 0f) return false;
            dir.y = 0f;
            float m = dir.magnitude;
            if (m < 1e-3f) return false;
            float yaw = _yaw[i] * Mathf.Deg2Rad;
            // the attacker sits opposite the push direction
            float cos = -(Mathf.Sin(yaw) * dir.x + Mathf.Cos(yaw) * dir.z) / m;
            return cos > Mathf.Cos(def.blockArc * 0.5f * Mathf.Deg2Rad);
        }

        public void ApplyDamage(int i, in DamageInfo info)
        {
            if (!IsAlive(i) || _invuln[i]) return;
            var def = _view[i].Def;
            bool statusTick = (info.Flags & DamageFlags.Status) != 0;
            bool blocked = !statusTick && IsBlocking(i, info.Direction);
            float amount = info.Amount * _dmgTakenMul[i] * (blocked ? 1f - def.blockReduction : 1f);
            if (_stMask[i] != 0) amount *= StatusDamageMul(i, statusTick);
            bool elite = _affix[i] != null;
            _hp[i] -= amount;
            if (!statusTick) // ticks don't flash, recoil or push: hundreds of burning enemies must stay readable
            {
                _knock[i] += info.Direction * (info.Knockback * def.knockbackTaken * _knockMul[i] * (blocked ? 0.4f : 1f));
                _recoil[i] = feedback != null ? feedback.hitRecoilTime * (blocked ? 0.5f : 1f) : 0.15f;
                if (feedback != null)
                {
                    _flash[i] = feedback.flashDuration;
                    if (!_view[i].Flashing) SetMaterial(_view[i], flashMaterial);
                }
            }
            var hit = new HitEvent
            {
                TargetId = _uid[i], Index = i, Position = _pos[i] + Vector3.up * (1.2f * _scale[i]), Damage = amount, Crit = info.IsCrit && !blocked,
                SourceId = info.SourceId, Blocked = blocked, Elite = elite, Flags = info.Flags, Status = info.Status,
            };
            GameEvents.OnHit.Raise(hit);
            if (hit.Crit)
            {
                GameEvents.OnCrit.Raise(hit);
                if (feedback != null)
                {
                    ScreenShake.Add(feedback.shakeOnCrit);
                    if (Time.unscaledTime >= _nextHitStop)
                    {
                        _nextHitStop = Time.unscaledTime + feedback.hitStopInterval;
                        TimeController.HitStop(feedback.hitStopOnCrit);
                    }
                }
            }
            else if (elite && feedback != null && !statusTick && Time.unscaledTime >= _nextHitStop)
            {
                _nextHitStop = Time.unscaledTime + feedback.hitStopInterval;
                TimeController.HitStop(feedback.hitStopOnElite);
            }
            if (_hp[i] <= 0f) Kill(i, info, allowBlast: true);
        }

        void Kill(int i, in DamageInfo info, bool allowBlast)
        {
            // Once only: OnHit hooks (sparks, bursts) can kill this enemy re-entrantly before ApplyDamage's own check.
            if (!_active[i]) return;
            _active[i] = false;
            _dying[i] = feedback != null ? feedback.deathPopTime : 0.1f;
            AliveCount--;
            DevCounters.Enemies = AliveCount;
            var def = _view[i].Def;
            _aliveByDef[def] = AliveOf(def) - 1;
            var affix = _affix[i];
            bool elite = affix != null;
            GameEvents.OnKill.Raise(new KillEvent
            {
                TargetId = _uid[i], Index = i, Position = _pos[i], EnemyTypeId = def.RuntimeId, Elite = elite, SourceId = info.SourceId,
                Xp = def.xp * (elite ? affix.xpMultiplier : 1f), GoldChance = def.goldChance,
            });
            if (deathPop != null && feedback != null)
                deathPop.Emit(_pos[i] + Vector3.up * 0.7f, info.Direction, feedback);
            if (feedback != null && feedback.shakeOnKill > 0f) ScreenShake.Add(feedback.shakeOnKill);
            if (elite && feedback != null && allowBlast)
            {
                TimeController.HitStop(feedback.hitStopOnElite * 1.5f);
                ScreenShake.Add(0.15f);
            }
            if (elite && affix.explodeOnDeath && allowBlast && _blastCount < _blasts.Length)
                _blasts[_blastCount++] = new Blast
                {
                    Pos = _pos[i], T = affix.fuse, Fuse = Mathf.Max(0.05f, affix.fuse), Radius = affix.explosionRadius,
                    ToPlayer = affix.explosionDamageToPlayer, ToEnemies = affix.explosionDamageToEnemies,
                };
        }

        /// <summary>
        /// Explosive elites: a filling orange-red circle during the fuse, then the blast hurts the player and other
        /// enemies (which can chain into more blasts). Runs before the enemy loop; the grid is already built.
        /// </summary>
        void TickBlasts(float dt, Vector3 player)
        {
            var tele = TelegraphRenderer.Instance;
            for (int b = 0; b < _blastCount; b++)
            {
                ref var bl = ref _blasts[b];
                bl.T -= dt;
                if (bl.T > 0f)
                {
                    if (tele != null) tele.Circle(bl.Pos, bl.Radius, 1f - bl.T / bl.Fuse, telegraphColor, TelegraphRenderer.Style.Telegraph, 0.09f);
                    continue;
                }
                Detonate(bl.Pos, bl.Radius, bl.ToPlayer, bl.ToEnemies, player);
                _blasts[b] = _blasts[--_blastCount];
                b--;
            }
        }

        void Detonate(Vector3 pos, float radius, float toPlayer, float toEnemies, Vector3 player)
        {
            if (deathPop != null) deathPop.Burst(pos + Vector3.up * 0.5f, 26, 7.5f, 0.2f, blastColorA, blastColorB);
            SfxPlayer.Play(SfxId.Explosion, pos);
            Vector3 d = player - pos;
            float pd = new Vector2(d.x, d.z).magnitude;
            ScreenShake.Add(pd < radius * 3f ? 0.35f : 0.12f);
            if (_playerHealth != null && pd < radius + playerRadius && d.y > -1f && d.y < 1.6f)
            {
                Vector3 dir = pd > 1e-3f ? new Vector3(d.x, 0f, d.z) / pd : Vector3.forward;
                if (_playerHealth.TakeDamage(new DamageInfo { Amount = toPlayer, Flags = DamageFlags.Area, Point = player, Direction = dir }) && _playerMotor != null)
                    _playerMotor.AddImpulse(dir * 4f + Vector3.up * 3f);
            }
            if (toEnemies <= 0f) return;
            int n = QueryAlive(pos, radius, _blastQuery);
            for (int k = 0; k < n; k++)
            {
                int j = _blastQuery[k];
                Vector3 e = _pos[j] - pos;
                e.y = 0f;
                float m = e.magnitude;
                ApplyDamage(j, new DamageInfo { Amount = toEnemies, Flags = DamageFlags.Area, Point = _pos[j], Direction = m > 1e-3f ? e / m : Vector3.forward, Knockback = 6f });
            }
        }

        void SetMaterial(EnemyView v, Material m)
        {
            var rs = v.Renderers;
            for (int r = 0; r < rs.Length; r++) rs[r].sharedMaterial = m;
            v.Flashing = m == flashMaterial; // reference compare: Object.name would allocate a string per hit
        }

        // ------------------------------------------------------------------ tick

        public void Tick(float dt, float unscaledDt)
        {
            _threatCount = 0;
            _puffsThisFrame = 0;
            if ((_high == 0 && _blastCount == 0) || dt <= 0f) return;
            _grid.Build(_pos, _active, _high);
            if (_player == null && Player.Main != null)
            {
                _player = Player.Main.transform;
                _playerHealth = Player.Main.GetComponent<PlayerHealth>();
                _playerMotor = Player.Main.GetComponent<PlayerMotor>();
            }
            Vector3 player = _player != null ? _player.position : arenaCenter;
            if (_blastCount > 0) TickBlasts(dt, player);
            var cam = Camera.main;
            Vector3 camPos = cam != null ? cam.transform.position : player;
            float animD2 = animDistance * animDistance;
            float knockDecay = feedback != null ? Mathf.Exp(-feedback.knockbackDecay * dt) : 0.85f;
            float recoilTime = feedback != null ? feedback.hitRecoilTime : 0.15f;
            float recoilAngle = feedback != null ? feedback.hitRecoilAngle : 20f;
            var tele = TelegraphRenderer.Instance;
            _mud = MudZones.Instance;

            for (int i = 0; i < _high; i++)
            {
                if (!_used[i]) continue;
                var v = _view[i];

                if (!_active[i])
                {
                    // death pop: quick swell then shrink, then back to the pool
                    _dying[i] -= dt;
                    float popT = feedback != null ? 1f - Mathf.Clamp01(_dying[i] / feedback.deathPopTime) : 1f;
                    float s = (popT < 0.35f ? 1f + popT * 0.8f : Mathf.Lerp(1.28f, 0f, (popT - 0.35f) / 0.65f)) * _scale[i];
                    v.Root.localScale = new Vector3(s, s * (1f - popT * 0.3f), s);
                    if (_dying[i] <= 0f) Release(i);
                    continue;
                }

                if (_stMask[i] != 0 && !TickStatus(i, dt)) continue; // burned/bled to death this tick

                var def = v.Def;
                if (def.behaviour == EnemyBehaviour.Scripted) { TickScripted(i, v, dt); continue; }
                Vector3 p = _pos[i];
                Vector3 to = player - p;
                to.y = 0f;
                float dist = to.magnitude;
                Vector3 dirTo = dist > 0.01f ? to / dist : Vector3.forward;

                // behaviour: desired velocity + where to face
                bool facePlayer;
                Vector3 desired = Think(i, def, p, player, dirTo, dist, dt, tele, out facePlayer);
                if (_stMask[i] != 0) desired *= StatusSpeedMul(i);
                if (_mud != null) desired *= _mud.SpeedAt(p);

                // separation (bounded neighbour checks)
                Vector3 sep = Vector3.zero;
                int n = _grid.QueryCandidates(p, _radius[i] + _maxRadius, _query);
                int checks = 0;
                for (int k = 0; k < n && checks < maxNeighbours; k++)
                {
                    int j = _query[k];
                    if (j == i || !_active[j]) continue;
                    checks++;
                    float dx = p.x - _pos[j].x, dz = p.z - _pos[j].z;
                    float minD = _radius[i] + _radius[j];
                    float d2 = dx * dx + dz * dz;
                    if (d2 >= minD * minD) continue;
                    if (d2 < 1e-6f) { dx = (i & 1) == 0 ? 0.01f : -0.01f; dz = 0.01f; d2 = 2e-4f; }
                    float len = Mathf.Sqrt(d2);
                    float push = (minD - len) / len;
                    sep.x += dx * push;
                    sep.z += dz * push;
                }

                if (_state[i] == AiState.Attack && def.behaviour == EnemyBehaviour.Lunger) _vel[i] = desired; // the leap ignores acceleration
                else _vel[i] = Vector3.MoveTowards(_vel[i], desired, def.acceleration * dt);
                Vector3 np = p + (_vel[i] + _knock[i]) * dt + sep * Mathf.Min(1f, separationStrength * dt);
                _knock[i] *= knockDecay;

                _obstacles?.PushOut(ref np, _radius[i]);

                // keep inside the arena
                float cx = np.x - arenaCenter.x, cz = np.z - arenaCenter.z;
                float cr = arenaRadius - _radius[i];
                float c2 = cx * cx + cz * cz;
                if (c2 > cr * cr) { float k2 = cr / Mathf.Sqrt(c2); np.x = arenaCenter.x + cx * k2; np.z = arenaCenter.z + cz * k2; }

                // terrain: follow walkable height, but never step up a wall
                float h0 = p.y;
                float h1 = _height != null ? _height.Sample(np.x, np.z) : 0f;
                if (h1 - h0 > maxStepUp) { np.x = p.x; np.z = p.z; h1 = _height != null ? _height.Sample(p.x, p.z) : h0; _vel[i] *= 0.5f; }
                np.y = Mathf.MoveTowards(h0, h1, 12f * dt + Mathf.Abs(h1 - h0) * 0.5f);
                _pos[i] = np;

                // contact damage (a lunging cur deals its leap damage instead)
                _contactCd[i] -= dt;
                bool touching = dist < _radius[i] + playerRadius + 0.15f && Mathf.Abs(player.y - np.y) < 1.1f;
                if (_playerHealth != null && touching)
                {
                    if (_state[i] == AiState.Attack && def.behaviour == EnemyBehaviour.Lunger)
                    {
                        if (!_attackHit[i] && _playerHealth.TakeDamage(new DamageInfo { Amount = def.attackDamage, Flags = DamageFlags.Melee, Point = player, Direction = _aim[i] }))
                        {
                            _attackHit[i] = true;
                            _contactCd[i] = def.contactInterval;
                        }
                    }
                    else if (_contactCd[i] <= 0f)
                    {
                        var hit = new DamageInfo { Amount = def.contactDamage, Flags = DamageFlags.Contact, Point = player, Direction = dist > 0.01f ? to / dist : Vector3.forward };
                        if (_playerHealth.TakeDamage(hit)) _contactCd[i] = def.contactInterval;
                    }
                }

                // facing: shield bearers / shooters / wind-ups look at the player (turn-rate limited), others look where they go
                Vector3 face = facePlayer ? dirTo : (_state[i] == AiState.Attack ? _aim[i] : _vel[i]);
                if (face.x * face.x + face.z * face.z > (facePlayer ? 1e-4f : 0.04f))
                    _yaw[i] = Mathf.MoveTowardsAngle(_yaw[i], Mathf.Atan2(face.x, face.z) * Mathf.Rad2Deg, def.turnRate * dt);

                // elite marker + threat list
                var affix = _affix[i];
                if (affix != null)
                {
                    if (tele != null) tele.Circle(np, _radius[i] * 2.1f, 0f, affix.color, TelegraphRenderer.Style.Ring, 0.16f, i * 0.37f);
                    if (_state[i] != AiState.Windup && _threatCount < _threats.Length)
                        _threats[_threatCount++] = new EnemyThreat { Position = np, Color = affix.color, Windup = false };
                }

                // flash restore
                if (_flash[i] > 0f) { _flash[i] -= dt; if (_flash[i] <= 0f && v.Flashing) SetMaterial(v, bodyMaterial); }
                if (_recoil[i] > 0f) _recoil[i] -= dt;

                v.Root.SetPositionAndRotation(np, Quaternion.Euler(0f, _yaw[i], 0f));

                // animation (near the camera only)
                float ex = np.x - camPos.x, ez = np.z - camPos.z;
                if (ex * ex + ez * ez < animD2 && v.Rig.IsValid) Animate(i, v, dt, recoilTime, recoilAngle);
            }
        }

        /// <summary>A scripted enemy (boss): only flash, threat and transform here; its controller does the rest.</summary>
        void TickScripted(int i, EnemyView v, float dt)
        {
            _knock[i] = Vector3.zero;
            if (_flash[i] > 0f) { _flash[i] -= dt; if (_flash[i] <= 0f && v.Flashing) SetMaterial(v, bodyMaterial); }
            if (_recoil[i] > 0f) _recoil[i] -= dt;
            if (_threatCount < _threats.Length)
                _threats[_threatCount++] = new EnemyThreat { Position = _pos[i], Color = telegraphColor, Windup = _scriptedWindup[i] };
            v.Root.SetPositionAndRotation(_pos[i], Quaternion.Euler(0f, _yaw[i], 0f));
        }

        /// <summary>Moves a scripted enemy (its controller owns movement). `windup` marks it as a threat for the bot and arrows.</summary>
        public void SetScripted(int i, Vector3 position, float yaw, bool windup)
        {
            if (!IsAlive(i)) return;
            _pos[i] = position;
            _yaw[i] = yaw;
            _scriptedWindup[i] = windup;
        }

        /// <summary>Takes no damage while on (boss entrance / phase change).</summary>
        public void SetInvulnerable(int i, bool on) { if (i >= 0 && i < capacity) _invuln[i] = on; }

        public PartRig GetRig(int i) => _view[i] != null ? _view[i].Rig : null;
        public Transform GetViewRoot(int i) => _view[i] != null ? _view[i].Root : null;

        void SetState(int i, AiState state, float duration)
        {
            _state[i] = state;
            _stateT[i] = _stateDur[i] = Mathf.Max(0.01f, duration);
        }

        void AddWindupThreat(Vector3 p)
        {
            if (_threatCount < _threats.Length) _threats[_threatCount++] = new EnemyThreat { Position = p, Color = telegraphColor, Windup = true };
        }

        /// <summary>
        /// Per-behaviour state machine: Move → Windup (telegraphed) → Attack → Recover → Move. Returns the desired
        /// velocity; `facePlayer` turns the body toward the player instead of along the velocity.
        /// </summary>
        Vector3 Think(int i, EnemyDef def, Vector3 p, Vector3 player, Vector3 dirTo, float dist, float dt, TelegraphRenderer tele, out bool facePlayer)
        {
            float speed = _speed[i];
            float atkMul = _atkTimeMul[i];
            _attackCd[i] -= dt;
            _stateT[i] -= dt;
            facePlayer = false;
            float dy = player.y - p.y;

            switch (def.behaviour)
            {
                case EnemyBehaviour.ShieldBearer:
                    facePlayer = true;
                    return dirTo * speed;

                case EnemyBehaviour.Lunger:
                    switch (_state[i])
                    {
                        case AiState.Windup:
                            facePlayer = true;
                            if (_stateT[i] <= 0f)
                            {
                                _aim[i] = dirTo;
                                _attackHit[i] = false;
                                SetState(i, AiState.Attack, def.lungeTime);
                                _attackCd[i] = def.attackCooldown * atkMul;
                            }
                            return Vector3.zero;
                        case AiState.Attack:
                            if (_stateT[i] <= 0f) SetState(i, AiState.Recover, def.recover);
                            return _aim[i] * def.lungeSpeed;
                        case AiState.Recover:
                            if (_stateT[i] <= 0f) _state[i] = AiState.Move;
                            return dirTo * (speed * 0.3f);
                        default:
                            if (_attackCd[i] <= 0f && dist < def.attackRange && Mathf.Abs(dy) < 1f)
                                SetState(i, AiState.Windup, def.windup * atkMul);
                            return dirTo * speed;
                    }

                case EnemyBehaviour.Slammer:
                    switch (_state[i])
                    {
                        case AiState.Windup:
                        {
                            facePlayer = true;
                            float fill = 1f - _stateT[i] / _stateDur[i];
                            if (tele != null) tele.Circle(p, def.attackRadius * _scale[i], fill, telegraphColor, TelegraphRenderer.Style.Telegraph, 0.07f);
                            AddWindupThreat(p);
                            if (_stateT[i] <= 0f) { Slam(i, def, p, player, dirTo, dist); SetState(i, AiState.Recover, def.recover); }
                            return Vector3.zero;
                        }
                        case AiState.Recover:
                            if (_stateT[i] <= 0f) _state[i] = AiState.Move;
                            return Vector3.zero;
                        default:
                            if (_attackCd[i] <= 0f && dist < def.attackRange && Mathf.Abs(dy) < 1.5f)
                                SetState(i, AiState.Windup, def.windup * atkMul);
                            return dirTo * speed;
                    }

                case EnemyBehaviour.Ranged:
                case EnemyBehaviour.Lobber:
                    facePlayer = true;
                    switch (_state[i])
                    {
                        case AiState.Windup:
                            if (tele != null) tele.Circle(p, _radius[i] * 2.2f, 1f, telegraphColor, TelegraphRenderer.Style.Ring, 0.14f);
                            AddWindupThreat(p);
                            if (_stateT[i] <= 0f)
                            {
                                Throw(i, def, p, player);
                                SetState(i, AiState.Recover, def.recover);
                                _attackCd[i] = def.attackCooldown * atkMul;
                            }
                            return Vector3.zero;
                        case AiState.Recover:
                            if (_stateT[i] <= 0f) { _state[i] = AiState.Move; _stateT[i] = GameRandom.Range(1.5f, 3.5f); }
                            return Vector3.zero;
                        default:
                        {
                            if (_attackCd[i] <= 0f && dist < def.attackRange && dist > 1.5f)
                            {
                                SetState(i, AiState.Windup, def.windup * atkMul);
                                return Vector3.zero;
                            }
                            // keep-distance band: approach / back off / strafe (flip side now and then)
                            if (_stateT[i] <= 0f) { _strafe[i] = -_strafe[i]; _stateT[i] = GameRandom.Range(1.5f, 3.5f); }
                            if (dist > def.keepDistance.y) return dirTo * speed;
                            if (dist < def.keepDistance.x) return -dirTo * speed;
                            return new Vector3(dirTo.z, 0f, -dirTo.x) * (_strafe[i] * speed * 0.55f);
                        }
                    }

                default: // Chaser
                    return dirTo * speed;
            }
        }

        void Slam(int i, EnemyDef def, Vector3 p, Vector3 player, Vector3 dirTo, float dist)
        {
            float r = def.attackRadius * _scale[i];
            if (deathPop != null) deathPop.Burst(p + Vector3.up * 0.15f, 16, 5f, 0.16f, slamDustA, slamDustB);
            SfxPlayer.Play(SfxId.Slam, p, 0.8f);
            if (dist < 12f) ScreenShake.Add(dist < r * 1.5f ? 0.3f : 0.12f);
            bool grounded = _playerMotor == null || _playerMotor.IsGrounded;
            float dy = player.y - p.y;
            if (_playerHealth == null || !grounded || dist > r + playerRadius || dy > 1.2f || dy < -1.2f) return;
            if (_playerHealth.TakeDamage(new DamageInfo { Amount = def.attackDamage, Flags = DamageFlags.Area | DamageFlags.Melee, Point = player, Direction = dirTo })
                && _playerMotor != null)
                _playerMotor.AddImpulse(dirTo * slamImpulse.x + Vector3.up * slamImpulse.y);
        }

        void Throw(int i, EnemyDef def, Vector3 p, Vector3 player)
        {
            var proj = EnemyProjectiles.Instance;
            if (proj == null) return;
            float yaw = _yaw[i] * Mathf.Deg2Rad;
            Vector3 fwd = new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
            Vector3 hand = p + Vector3.up * (1.25f * _scale[i]) + fwd * (_radius[i] + 0.15f);
            if (def.behaviour == EnemyBehaviour.Lobber)
                proj.Lob(hand + Vector3.up * 0.3f, player, def.lobTime, def.attackRadius, def.zoneDuration, def.zoneDps);
            else
                proj.FireStraight(hand, player + Vector3.up * 0.9f, def.projectileSpeed, def.attackDamage, def.projectileRadius, def.attackRange * 1.25f);
        }

        void Animate(int i, EnemyView v, float dt, float recoilTime, float recoilAngle)
        {
            var def = v.Def;
            var prof = def.anim;
            float speed = new Vector2(_vel[i].x, _vel[i].z).magnitude;
            float run01 = prof != null ? Mathf.Clamp01(speed / Mathf.Max(0.1f, prof.fullRunSpeed)) : 0.5f;
            float stride = prof != null ? prof.strideLength : 1.2f;
            _phase[i] += speed * dt / stride * Mathf.PI * 2f;
            float s = Mathf.Sin(_phase[i]);
            float leg = (prof != null ? prof.legSwing : 40f) * run01;
            float arm = (prof != null ? prof.armSwing : 30f) * run01 * 0.6f;
            var pose = PartPose.Identity;
            pose.LegL = PartPose.Swing(s * leg);
            pose.LegR = PartPose.Swing(-s * leg);
            pose.ArmL = PartPose.Swing(-s * arm);
            pose.ArmR = PartPose.Swing(s * arm);
            pose.RootY = Mathf.Abs(s) * (prof != null ? prof.bobHeight : 0.05f) * run01;
            if (def.behaviour == EnemyBehaviour.ShieldBearer) pose.ArmL = PartPose.Swing(-s * arm * 0.15f); // shield stays up

            // attack poses (the body language IS the telegraph)
            var st = _state[i];
            if (st != AiState.Move)
            {
                float k = st == AiState.Windup ? 1f - Mathf.Clamp01(_stateT[i] / _stateDur[i]) : Mathf.Clamp01(_stateT[i] / _stateDur[i]);
                float shake = st == AiState.Windup ? Mathf.Sin(Time.time * 60f + i) * k : 0f;
                switch (def.behaviour)
                {
                    case EnemyBehaviour.Lunger:
                        if (st == AiState.Windup)
                        {
                            pose.Body = PartPose.Lean(18f * k);
                            pose.ArmL = pose.ArmR = PartPose.Swing(-25f * k);
                            pose.LegL = pose.LegR = PartPose.Swing(35f * k);
                            pose.RootY = -0.1f * k;
                            pose.RootScale = new Vector3(1f + 0.1f * k, 1f - 0.15f * k, 1f);
                        }
                        else if (st == AiState.Attack)
                        {
                            pose.ArmL = pose.ArmR = PartPose.Swing(65f);
                            pose.LegL = pose.LegR = PartPose.Swing(-55f);
                            pose.Body = PartPose.Lean(-8f);
                            pose.RootY = 0.25f;
                            pose.RootScale = new Vector3(0.92f, 0.95f, 1.18f);
                        }
                        break;
                    case EnemyBehaviour.Slammer:
                        if (st == AiState.Windup)
                        {
                            pose.ArmL = PartPose.Swing(165f * k) + new Vector3(0f, 0f, -15f * k);
                            pose.ArmR = PartPose.Swing(165f * k) + new Vector3(0f, 0f, 15f * k);
                            pose.Body = PartPose.Lean(-14f * k + shake * 2f);
                            pose.Head = PartPose.Lean(-10f * k);
                            pose.RootY = 0.05f * k;
                        }
                        else
                        {
                            pose.ArmL = pose.ArmR = PartPose.Swing(60f * k);
                            pose.Body = PartPose.Lean(28f * k);
                            pose.RootY = -0.12f * k;
                            pose.RootScale = new Vector3(1f + 0.08f * k, 1f - 0.08f * k, 1f + 0.08f * k);
                        }
                        break;
                    case EnemyBehaviour.Ranged:
                    case EnemyBehaviour.Lobber:
                        if (st == AiState.Windup)
                        {
                            // throwing arm swings back and up, the sling/pot whirls overhead
                            pose.ArmR = PartPose.Swing(140f * k) + new Vector3(0f, Mathf.Sin(Time.time * 25f) * 25f * k, 0f);
                            pose.ArmL = PartPose.Swing(35f * k);
                            pose.Body = PartPose.Lean(-12f * k);
                        }
                        else
                        {
                            pose.ArmR = PartPose.Swing(70f * k);
                            pose.Body = PartPose.Lean(15f * k);
                        }
                        break;
                }
            }

            if (_recoil[i] > 0f)
            {
                float k = _recoil[i] / recoilTime;
                pose.Body += PartPose.Lean(-recoilAngle * k);
                pose.Head += PartPose.Lean(-recoilAngle * 0.5f * k);
                pose.RootScale = Vector3.Scale(pose.RootScale, new Vector3(1f + 0.12f * k, 1f - 0.12f * k, 1f + 0.12f * k));
            }
            v.Rig.Apply(pose);
        }
    }
}
