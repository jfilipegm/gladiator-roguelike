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

    /// <summary>
    /// All enemies, one central tick (CLAUDE.md §4): struct-of-arrays state, pooled views, spatial-hash separation,
    /// static-obstacle push-out, baked height grid for terrain, step-height rule (no wall climbing), contact damage,
    /// hit flash / knockback / recoil, rigid-part run cycle, death pop. Zero allocation in steady state.
    /// </summary>
    public sealed class EnemyManager : MonoBehaviour, ITickable
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

        // ---- state (struct of arrays) ----
        Vector3[] _pos, _vel, _knock;
        float[] _hp, _maxHp, _speed, _radius, _flash, _contactCd, _phase, _recoil, _dying, _yaw;
        int[] _uid;
        bool[] _used, _active;
        EnemyView[] _view;
        int[] _free;
        int _freeCount, _high, _nextUid = 1;

        SpatialHashGrid _grid;
        HeightGrid _height;
        StaticObstacles _obstacles;
        readonly int[] _query = new int[512];
        readonly Dictionary<EnemyDef, Stack<EnemyView>> _pools = new Dictionary<EnemyDef, Stack<EnemyView>>();
        Transform _poolRoot;
        Transform _player;
        PlayerHealth _playerHealth;
        float _maxRadius = 0.5f;

        public int AliveCount { get; private set; }
        public int HighWater => _high;
        public HeightGrid Height => _height;
        public SpatialHashGrid Grid => _grid;

        void Awake()
        {
            Instance = this;
            _pos = new Vector3[capacity]; _vel = new Vector3[capacity]; _knock = new Vector3[capacity];
            _hp = new float[capacity]; _maxHp = new float[capacity]; _speed = new float[capacity]; _radius = new float[capacity];
            _flash = new float[capacity]; _contactCd = new float[capacity]; _phase = new float[capacity];
            _recoil = new float[capacity]; _dying = new float[capacity]; _yaw = new float[capacity];
            _uid = new int[capacity]; _used = new bool[capacity]; _active = new bool[capacity];
            _view = new EnemyView[capacity];
            _free = new int[capacity];
            for (int i = capacity - 1; i >= 0; i--) _free[_freeCount++] = i;
            _grid = new SpatialHashGrid(boundsMin, boundsMax, gridCellSize, capacity);
            _poolRoot = new GameObject("[EnemyPool]").transform;
            _poolRoot.SetParent(transform, false);
        }

        void Start()
        {
            BakeWorld();
            var p = Player.Main;
            if (p != null) { _player = p.transform; _playerHealth = p.GetComponent<PlayerHealth>(); }
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

        Stack<EnemyView> GetPool(EnemyDef def)
        {
            if (!_pools.TryGetValue(def, out var s)) { s = new Stack<EnemyView>(def.prewarm); _pools[def] = s; }
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

        /// <summary>Returns the slot index, or -1 when full.</summary>
        public int Spawn(EnemyDef def, Vector3 position)
        {
            if (_freeCount == 0) return -1;
            int i = _free[--_freeCount];
            var pool = GetPool(def);
            var v = pool.Count > 0 ? pool.Pop() : CreateView(def);
            v.Go.SetActive(true);
            if (v.Flashing) SetMaterial(v, bodyMaterial);
            _view[i] = v;
            _used[i] = true;
            _active[i] = true;
            if (_height != null) position.y = _height.Sample(position.x, position.z);
            _pos[i] = position;
            _vel[i] = Vector3.zero;
            _knock[i] = Vector3.zero;
            _hp[i] = _maxHp[i] = def.maxHp;
            _speed[i] = def.moveSpeed * (1f + GameRandom.Range(-def.speedJitter, def.speedJitter));
            _radius[i] = def.radius;
            if (def.radius > _maxRadius) _maxRadius = def.radius;
            _flash[i] = 0f; _contactCd[i] = 0.5f; _phase[i] = GameRandom.Range(0f, 6.28f); _recoil[i] = 0f; _dying[i] = 0f;
            _uid[i] = _nextUid++;
            Vector3 toPlayer = _player != null ? _player.position - position : Vector3.forward;
            _yaw[i] = Mathf.Atan2(toPlayer.x, toPlayer.z) * Mathf.Rad2Deg;
            v.Root.SetPositionAndRotation(position, Quaternion.Euler(0f, _yaw[i], 0f));
            v.Root.localScale = Vector3.one;
            if (i >= _high) _high = i + 1;
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
            _used[i] = false;
            _active[i] = false;
            _free[_freeCount++] = i;
            while (_high > 0 && !_used[_high - 1]) _high--;
        }

        public void KillAll()
        {
            for (int i = 0; i < _high; i++)
                if (_active[i]) Kill(i, new DamageInfo());
        }

        // ------------------------------------------------------------------ queries (weapons, bot, pickups)

        public bool IsAlive(int i) => i >= 0 && i < _high && _active[i];
        public Vector3 GetPosition(int i) => _pos[i];
        public float GetRadius(int i) => _radius[i];
        public int GetUid(int i) => _uid[i];
        public float GetHp(int i) => _hp[i];
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

        public void ApplyDamage(int i, in DamageInfo info)
        {
            if (!IsAlive(i)) return;
            var def = _view[i].Def;
            _hp[i] -= info.Amount;
            _knock[i] += info.Direction * (info.Knockback * def.knockbackTaken);
            _recoil[i] = feedback != null ? feedback.hitRecoilTime : 0.15f;
            if (feedback != null)
            {
                _flash[i] = feedback.flashDuration;
                if (!_view[i].Flashing) SetMaterial(_view[i], flashMaterial);
            }
            var hit = new HitEvent { TargetId = _uid[i], Position = _pos[i] + Vector3.up * 1.2f, Damage = info.Amount, Crit = info.IsCrit, SourceId = info.SourceId };
            GameEvents.OnHit.Raise(hit);
            if (info.IsCrit)
            {
                GameEvents.OnCrit.Raise(hit);
                if (feedback != null) { TimeController.HitStop(feedback.hitStopOnCrit); ScreenShake.Add(feedback.shakeOnCrit); }
            }
            if (_hp[i] <= 0f) Kill(i, info);
        }

        void Kill(int i, in DamageInfo info)
        {
            _active[i] = false;
            _dying[i] = feedback != null ? feedback.deathPopTime : 0.1f;
            AliveCount--;
            DevCounters.Enemies = AliveCount;
            var def = _view[i].Def;
            GameEvents.OnKill.Raise(new KillEvent { TargetId = _uid[i], Position = _pos[i], EnemyTypeId = def.RuntimeId, Elite = false, SourceId = info.SourceId, Xp = def.xp, GoldChance = def.goldChance });
            if (deathPop != null && feedback != null)
                deathPop.Emit(_pos[i] + Vector3.up * 0.7f, info.Direction, feedback);
            if (feedback != null && feedback.shakeOnKill > 0f) ScreenShake.Add(feedback.shakeOnKill);
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
            if (_high == 0 || dt <= 0f) return;
            _grid.Build(_pos, _active, _high);
            if (_player == null && Player.Main != null) { _player = Player.Main.transform; _playerHealth = Player.Main.GetComponent<PlayerHealth>(); }
            Vector3 player = _player != null ? _player.position : arenaCenter;
            var cam = Camera.main;
            Vector3 camPos = cam != null ? cam.transform.position : player;
            float animD2 = animDistance * animDistance;
            float knockDecay = feedback != null ? Mathf.Exp(-feedback.knockbackDecay * dt) : 0.85f;
            float recoilTime = feedback != null ? feedback.hitRecoilTime : 0.15f;
            float recoilAngle = feedback != null ? feedback.hitRecoilAngle : 20f;

            for (int i = 0; i < _high; i++)
            {
                if (!_used[i]) continue;
                var v = _view[i];

                if (!_active[i])
                {
                    // death pop: quick swell then shrink, then back to the pool
                    _dying[i] -= dt;
                    float popT = feedback != null ? 1f - Mathf.Clamp01(_dying[i] / feedback.deathPopTime) : 1f;
                    float s = popT < 0.35f ? 1f + popT * 0.8f : Mathf.Lerp(1.28f, 0f, (popT - 0.35f) / 0.65f);
                    v.Root.localScale = new Vector3(s, s * (1f - popT * 0.3f), s);
                    if (_dying[i] <= 0f) Release(i);
                    continue;
                }

                var def = v.Def;
                Vector3 p = _pos[i];
                Vector3 to = player - p;
                to.y = 0f;
                float dist = to.magnitude;
                Vector3 desired = dist > 0.05f ? to * (_speed[i] / dist) : Vector3.zero;

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

                _vel[i] = Vector3.MoveTowards(_vel[i], desired, def.acceleration * dt);
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

                // contact damage
                _contactCd[i] -= dt;
                if (_playerHealth != null && _contactCd[i] <= 0f && dist < _radius[i] + playerRadius + 0.15f && Mathf.Abs(player.y - np.y) < 1.1f)
                {
                    var hit = new DamageInfo { Amount = def.contactDamage, Flags = DamageFlags.Contact, Point = player, Direction = dist > 0.01f ? to / dist : Vector3.forward };
                    if (_playerHealth.TakeDamage(hit)) _contactCd[i] = def.contactInterval;
                }

                // facing
                Vector3 face = _vel[i];
                if (face.x * face.x + face.z * face.z > 0.04f)
                    _yaw[i] = Mathf.MoveTowardsAngle(_yaw[i], Mathf.Atan2(face.x, face.z) * Mathf.Rad2Deg, 540f * dt);

                // flash restore
                if (_flash[i] > 0f) { _flash[i] -= dt; if (_flash[i] <= 0f && v.Flashing) SetMaterial(v, bodyMaterial); }
                if (_recoil[i] > 0f) _recoil[i] -= dt;

                v.Root.SetPositionAndRotation(np, Quaternion.Euler(0f, _yaw[i], 0f));

                // animation (near the camera only)
                float ex = np.x - camPos.x, ez = np.z - camPos.z;
                if (ex * ex + ez * ez < animD2 && v.Rig.IsValid) Animate(i, v, dt, recoilTime, recoilAngle);
            }
        }

        void Animate(int i, EnemyView v, float dt, float recoilTime, float recoilAngle)
        {
            var prof = v.Def.anim;
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
            if (_recoil[i] > 0f)
            {
                float k = _recoil[i] / recoilTime;
                pose.Body = PartPose.Lean(-recoilAngle * k);
                pose.Head = PartPose.Lean(-recoilAngle * 0.5f * k);
                pose.RootScale = new Vector3(1f + 0.12f * k, 1f - 0.12f * k, 1f + 0.12f * k);
            }
            v.Rig.Apply(pose);
        }
    }
}
