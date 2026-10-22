using Gladiator.Core;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// Runs the behavioural Boons (M2.6) off the event bus: status on hit / on crit, chain sparks, bursts from dying
    /// statused enemies, slide trails, and the damage bonuses against statused enemies (written into EnemyManager).
    /// Phase 5.5 adds two more sources of effects, the active tag synergies and the equipped weapons' hit effects
    /// (filtered to that weapon's own hits), and three kinds: status spread, execute on crit, ground marks on hit.
    /// Effects only react to direct hits — damage from an effect carries DamageFlags.Proc and never procs again — and
    /// bursts are queued and resolved in the tick, so a chain of burning enemies can't recurse. Fixed arrays, no GC.
    /// </summary>
    public sealed class BoonEffects : MonoBehaviour, ITickable
    {
        [Tooltip("Spark and burst damage scale with the player's Damage stat.")]
        public Color sparkColorA = new Color(0.55f, 0.95f, 1f), sparkColorB = Color.white;
        public Color burstColorA = new Color(1f, 0.85f, 0.3f), burstColorB = new Color(1f, 0.97f, 0.8f);
        [Tooltip("Slide-trail marks: cool player colour even for Blood (orange-red is reserved for player danger).")]
        public Color trailColor = new Color(0.85f, 0.35f, 0.55f, 0.8f);
        [Tooltip("Distance slid between two trail marks (m).")]
        public float trailSpacing = 0.8f;
        [Tooltip("Seconds between a trail mark's status applications.")]
        public float trailTick = 0.3f;
        public int maxBurstsPerFrame = 12;
        [Tooltip("StatusSpread: most new carriers per spread tick (a field of burning enemies must not spike a frame).")]
        public int maxSpreadsPerTick = 48;

        /// <summary>One running effect. SourceFilter: only hits from this source id trigger it (a weapon's own hit effects); 0 = any hit.</summary>
        struct Active { public BoonEffect E; public float Potency; public int Source; public int SourceFilter; public int HitCount; }
        struct Burst { public Vector3 Pos; public float StatusPower; public int Fx; }
        /// <summary>A slide-trail or ground mark: carries what it applies, so marks outlive a Refresh.</summary>
        struct Mark { public Vector3 Pos; public float Life, MaxLife, Radius, Dps; public int Stacks; public StatusDef Status; public bool Ground; }

        const int MaxEffects = 32;
        readonly Active[] _fx = new Active[MaxEffects];
        int _fxCount, _version = -1, _weaponsVersion = -1;
        bool _anyOnHit, _anyOnCrit, _anyBurst, _anyTrail, _anySpread;

        readonly Burst[] _bursts = new Burst[64];
        int _burstHead, _burstCount;
        readonly Mark[] _marks = new Mark[64];
        int _markCount;
        float _trailClock, _slidDist, _spreadClock;
        Vector3 _lastSlidePos;

        readonly int[] _query = new int[256];
        readonly int[] _struck = new int[16];
        readonly int[] _spreadTo = new int[128];
        readonly float[] _spreadPower = new float[128];

        PlayerBuild _build;
        PlayerStats _stats;
        PlayerMotor _motor;
        WeaponSystem _weapons;

        public int SparksFired { get; private set; }
        public int BurstsFired { get; private set; }
        public int Spreads { get; private set; }
        public int Executions { get; private set; }
        public int MarkCount => _markCount;

        void Awake()
        {
            _build = GetComponent<PlayerBuild>();
            _stats = GetComponent<PlayerStats>();
            _motor = GetComponent<PlayerMotor>();
            _weapons = GetComponent<WeaponSystem>();
        }

        void OnEnable()
        {
            GameLoop.Register(this, TickGroup.Weapons, 50);
            GameEvents.OnHit.Subscribe(OnHit);
            GameEvents.OnCrit.Subscribe(OnCrit);
            GameEvents.OnKill.Subscribe(OnKill);
        }

        void OnDisable()
        {
            GameLoop.Unregister(this);
            GameEvents.OnHit.Unsubscribe(OnHit);
            GameEvents.OnCrit.Unsubscribe(OnCrit);
            GameEvents.OnKill.Unsubscribe(OnKill);
        }

        float DamageMul => _stats != null ? _stats.Damage : 1f;

        /// <summary>Re-reads the effects of the owned Boons, the active synergies and the equipped weapons (only when they changed).</summary>
        void Refresh()
        {
            var state = _build.State;
            _version = state.Version;
            _weaponsVersion = WeaponsVersion();
            _fxCount = 0;
            _anyOnHit = _anyOnCrit = _anyBurst = _anyTrail = _anySpread = false;
            var em = EnemyManager.Instance;
            if (em != null)
                for (int k = 0; k < EnemyManager.StatusSlots; k++) em.BonusVsStatus[k] = em.BonusPerStack[k] = 0f;
            for (int i = 0; i < state.OwnedCount; i++)
            {
                var o = state.GetOwned(i);
                Add(em, o.Def.effects, o.Potency, GameInfo.StableId(o.Def.id), 0);
            }
            for (int i = 0; i < state.SynergyCount; i++)
            {
                var syn = state.GetSynergy(i);
                Add(em, syn.effects, 1f, GameInfo.StableId(syn.id), 0);
            }
            if (_weapons != null)
                for (int i = 0; i < _weapons.Weapons.Count; i++)
                {
                    var d = _weapons.Weapons[i].Def;
                    Add(em, d.hitEffects, 1f, d.RuntimeId, d.RuntimeId); // kills by the weapon's chain/ground count as the weapon's
                }
            if (_traitFx == null) _traitFx = GetComponents<TraitEffects>(); // the gladiator's (installed at spawn, once)
            for (int i = 0; i < _traitFx.Length; i++)
                if (_traitFx[i] != null && _traitFx[i].trait != null)
                    Add(em, _traitFx[i].trait.effects, 1f, GameInfo.StableId(_traitFx[i].trait.id), 0);
        }

        TraitEffects[] _traitFx;

        void Add(EnemyManager em, BoonEffect[] list, float potency, int source, int filter)
        {
            if (list == null) return;
            for (int j = 0; j < list.Length && _fxCount < MaxEffects; j++)
            {
                var e = list[j];
                _fx[_fxCount] = new Active { E = e, Potency = potency, Source = source, SourceFilter = filter };
                int slot = e.status != null ? e.status.Slot : -1;
                switch (e.kind)
                {
                    case EffectKind.StatusOnHit:
                    case EffectKind.ChainSpark:
                    case EffectKind.GroundOnHit: _anyOnHit = true; break;
                    case EffectKind.StatusOnCrit:
                    case EffectKind.ExecuteOnCrit: _anyOnCrit = true; break;
                    case EffectKind.BurstOnDeath: _anyBurst = true; break;
                    case EffectKind.SlideTrail: _anyTrail = true; break;
                    case EffectKind.StatusSpread: _anySpread = true; break;
                    case EffectKind.DamagePerStack: if (em != null && slot >= 0) em.BonusPerStack[slot] += e.bonus * potency; break;
                    case EffectKind.DamageVsStatus: if (em != null && slot >= 0) em.BonusVsStatus[slot] += e.bonus * potency; break;
                }
                _fxCount++;
            }
        }

        /// <summary>A cheap fingerprint of the equipped weapons (an evolution swaps a Def without touching the count).</summary>
        int WeaponsVersion()
        {
            if (_weapons == null) return 0;
            int h = _weapons.Weapons.Count;
            for (int i = 0; i < _weapons.Weapons.Count; i++) h = h * 31 + _weapons.Weapons[i].Def.RuntimeId;
            return h;
        }

        // ---------- events ----------

        void OnHit(in HitEvent e)
        {
            if (!_anyOnHit || (e.Flags & DamageFlags.Proc) != 0 || RunState.CombatOver) return; // P1: nothing procs once the player is down
            var em = EnemyManager.Instance;
            if (em == null) return;
            bool alive = em.IsAlive(e.Index) && em.GetUid(e.Index) == e.TargetId;
            for (int k = 0; k < _fxCount; k++)
            {
                ref var a = ref _fx[k];
                if (a.SourceFilter != 0 && a.SourceFilter != e.SourceId) continue;
                switch (a.E.kind)
                {
                    case EffectKind.StatusOnHit:
                        if (alive && GameRandom.Chance(RngStream.Combat, a.E.chance * a.Potency))
                            em.ApplyStatus(e.Index, a.E.status, e.Damage * a.E.power, Mathf.Max(1, a.E.stacks));
                        break;
                    case EffectKind.ChainSpark:
                        if (++a.HitCount >= Mathf.Max(1, a.E.everyNth))
                        {
                            a.HitCount = 0;
                            Spark(em, k, e.Index, e.Position, alive);
                        }
                        break;
                    case EffectKind.GroundOnHit:
                        Ground(em, ref a, e.Index);
                        break;
                }
            }
        }

        void OnCrit(in HitEvent e)
        {
            if (!_anyOnCrit || (e.Flags & DamageFlags.Proc) != 0 || RunState.CombatOver) return;
            var em = EnemyManager.Instance;
            if (em == null || !em.IsAlive(e.Index) || em.GetUid(e.Index) != e.TargetId) return;
            for (int k = 0; k < _fxCount; k++)
            {
                ref var a = ref _fx[k];
                if (a.E.kind == EffectKind.StatusOnCrit)
                    em.ApplyStatus(e.Index, a.E.status, a.E.power * a.Potency * DamageMul, Mathf.Max(1, a.E.stacks));
                else if (a.E.kind == EffectKind.ExecuteOnCrit && em.IsAlive(e.Index))
                {
                    var def = em.GetDef(e.Index);
                    bool boss = def != null && def.behaviour == EnemyBehaviour.Scripted;
                    if (!boss && em.GetHp(e.Index) <= em.GetMaxHp(e.Index) * a.E.bonus && em.Execute(e.Index, a.Source)) Executions++;
                }
            }
        }

        void OnKill(in KillEvent e)
        {
            if (!_anyBurst || RunState.CombatOver) return;
            var em = EnemyManager.Instance;
            if (em == null) return;
            for (int k = 0; k < _fxCount; k++)
            {
                ref var a = ref _fx[k];
                if (a.E.kind != EffectKind.BurstOnDeath || a.E.status == null || !em.HasStatus(e.Index, a.E.status.kind)) continue;
                if (_burstCount >= _bursts.Length) return; // a huge chain: drop the overflow rather than allocate
                int at = (_burstHead + _burstCount++) % _bursts.Length;
                _bursts[at] = new Burst { Pos = e.Position, StatusPower = em.StatusPower(e.Index, a.E.status.kind), Fx = k };
            }
        }

        // ---------- effects ----------

        /// <summary>A spark (effect `fx`) from enemy `from` jumps to the nearest un-struck enemies, one after another.</summary>
        void Spark(EnemyManager em, int fx, int from, Vector3 fromPos, bool fromAlive)
        {
            ref var a = ref _fx[fx];
            SparksFired++;
            float damage = a.E.power * a.Potency * DamageMul;
            int struck = 0;
            if (fromAlive) { _struck[struck++] = from; StrikeSpark(em, from, damage, ref a); }
            Vector3 at = fromPos;
            int jumps = Mathf.Min(a.E.count, _struck.Length - 1);
            var pp = PlayerProjectiles.Instance;
            for (int j = 0; j < jumps; j++)
            {
                int n = em.QueryAlive(at, a.E.radius, _query);
                int best = -1;
                float bestD = float.MaxValue;
                for (int q = 0; q < n; q++)
                {
                    int c = _query[q];
                    bool seen = c == from;
                    for (int s = 0; s < struck && !seen; s++) seen = _struck[s] == c;
                    if (seen) continue;
                    float d = (em.GetPosition(c) - at).sqrMagnitude;
                    if (d < bestD) { bestD = d; best = c; }
                }
                if (best < 0) break;
                Vector3 to = em.GetPosition(best) + Vector3.up * 1f;
                if (pp != null) pp.Bolt(at, to);
                _struck[struck++] = best;
                at = to;
                StrikeSpark(em, best, damage, ref a);
            }
        }

        void StrikeSpark(EnemyManager em, int i, float damage, ref Active spark)
        {
            if (!em.IsAlive(i)) return;
            // SparkBonus Boons: extra damage against their status, and the spark applies it
            float mul = 1f;
            for (int k = 0; k < _fxCount; k++)
            {
                ref var b = ref _fx[k];
                if (b.E.kind != EffectKind.SparkBonus || b.E.status == null) continue;
                if (em.HasStatus(i, b.E.status.kind)) mul += b.E.bonus * b.Potency;
            }
            if (spark.E.status != null) em.ApplyStatus(i, spark.E.status, 0f, 1);
            Vector3 p = em.GetPosition(i);
            em.ApplyDamage(i, new DamageInfo { Amount = damage * mul, Flags = DamageFlags.Proc | DamageFlags.Area, Point = p, SourceId = spark.Source });
            if (!em.IsAlive(i)) return;
            for (int k = 0; k < _fxCount; k++)
            {
                ref var b = ref _fx[k];
                if (b.E.kind == EffectKind.SparkBonus && b.E.status != null) em.ApplyStatus(i, b.E.status, damage * 0.5f, 1);
            }
        }

        void ResolveBurst(EnemyManager em, in Burst burst)
        {
            ref var a = ref _fx[burst.Fx];
            BurstsFired++;
            float damage = a.E.power * a.Potency * DamageMul;
            int n = em.QueryAlive(burst.Pos, a.E.radius, _query);
            for (int q = 0; q < n; q++)
            {
                int i = _query[q];
                if (!em.IsAlive(i)) continue;
                em.ApplyStatus(i, a.E.status, burst.StatusPower, Mathf.Max(1, a.E.stacks)); // pass it on first: the hit may kill it, chaining
                em.ApplyDamage(i, new DamageInfo { Amount = damage, Flags = DamageFlags.Proc | DamageFlags.Area, Point = burst.Pos, SourceId = a.Source });
            }
            if (em.deathPop != null) em.deathPop.Burst(burst.Pos + Vector3.up * 0.6f, 10, 5f, 0.22f, burstColorA, burstColorB);
        }

        /// <summary>Adds a mark (full: the oldest goes first).</summary>
        void AddMark(in Mark mark)
        {
            if (_markCount == _marks.Length)
            {
                for (int s = 1; s < _markCount; s++) _marks[s - 1] = _marks[s];
                _markCount--;
            }
            _marks[_markCount++] = mark;
        }

        Mark MarkFor(in Active a, Vector3 pos, bool ground) => new Mark
        {
            Pos = pos, Life = a.E.duration, MaxLife = Mathf.Max(0.1f, a.E.duration), Radius = a.E.radius,
            Dps = a.E.power * a.Potency * DamageMul, Stacks = Mathf.Max(1, a.E.stacks), Status = a.E.status, Ground = ground,
        };

        /// <summary>GroundOnHit: a mark where enemy `i` was hit, unless a fresh ground mark already covers that spot (renewed instead).</summary>
        void Ground(EnemyManager em, ref Active a, int i)
        {
            if (a.E.status == null || a.E.radius <= 0f) return;
            Vector3 p = em.GetPosition(i);
            float merge = a.E.radius * 0.75f;
            for (int m = 0; m < _markCount; m++)
            {
                ref var mk = ref _marks[m];
                if (!mk.Ground || mk.Status != a.E.status) continue;
                if ((mk.Pos.x - p.x) * (mk.Pos.x - p.x) + (mk.Pos.z - p.z) * (mk.Pos.z - p.z) > merge * merge) continue;
                mk.Life = mk.MaxLife;
                return;
            }
            AddMark(MarkFor(a, p, ground: true));
        }

        /// <summary>Slide-trail marks while sliding, then every mark (trail or ground) ages and applies its status.</summary>
        void TickMarks(EnemyManager em, float dt)
        {
            var tele = TelegraphRenderer.Instance;
            if (_anyTrail && _motor != null && _motor.IsSliding)
            {
                Vector3 p = transform.position;
                _slidDist += (p - _lastSlidePos).magnitude;
                if (_slidDist >= trailSpacing || _markCount == 0)
                {
                    _slidDist = 0f;
                    // every trail lays its own mark (Blood Slide's Bleed and Whirlwind's Slow side by side, Phase 5.6)
                    for (int k = 0; k < _fxCount; k++)
                        if (_fx[k].E.kind == EffectKind.SlideTrail) AddMark(MarkFor(_fx[k], p, ground: false));
                }
            }
            _lastSlidePos = transform.position;
            if (_markCount == 0) return;

            _trailClock -= dt;
            bool apply = _trailClock <= 0f;
            if (apply) _trailClock += trailTick;
            for (int m = 0; m < _markCount; m++)
            {
                ref var mk = ref _marks[m];
                mk.Life -= dt;
                if (mk.Life <= 0f)
                {
                    for (int s = m + 1; s < _markCount; s++) _marks[s - 1] = _marks[s]; // keep age order
                    _markCount--;
                    m--;
                    continue;
                }
                if (tele != null)
                {
                    var c = mk.Ground && mk.Status != null ? mk.Status.color : trailColor;
                    c.a = (mk.Ground ? 0.75f : trailColor.a) * Mathf.Clamp01(mk.Life / mk.MaxLife * 2f);
                    tele.Circle(mk.Pos, mk.Radius, 0f, c, TelegraphRenderer.Style.Ring, 0.18f, m * 0.31f);
                }
                if (!apply || em == null || mk.Status == null) continue;
                int n = em.QueryAlive(mk.Pos, mk.Radius, _query);
                for (int q = 0; q < n; q++) em.ApplyStatus(_query[q], mk.Status, mk.Dps, mk.Stacks);
            }
        }

        /// <summary>StatusSpread: every carrier passes its status to `count` neighbours without it (collected first, so one tick spreads one step).</summary>
        void TickSpread(EnemyManager em, float dt)
        {
            _spreadClock -= dt;
            if (_spreadClock > 0f) return;
            float interval = 1f;
            int found = 0, cap = Mathf.Min(maxSpreadsPerTick, _spreadTo.Length);
            for (int k = 0; k < _fxCount; k++)
            {
                ref var a = ref _fx[k];
                if (a.E.kind != EffectKind.StatusSpread || a.E.status == null) continue;
                interval = Mathf.Max(0.1f, a.E.duration);
                var kind = a.E.status.kind;
                for (int i = 0; i < em.HighWater && found < cap; i++)
                {
                    if (!em.IsAlive(i) || !em.HasStatus(i, kind)) continue;
                    int n = em.QueryAlive(em.GetPosition(i), a.E.radius, _query);
                    int given = 0;
                    for (int q = 0; q < n && given < Mathf.Max(1, a.E.count) && found < cap; q++)
                    {
                        int j = _query[q];
                        if (j == i || em.HasStatus(j, kind)) continue;
                        bool queued = false;
                        for (int f = 0; f < found && !queued; f++) queued = _spreadTo[f] == j;
                        if (queued) continue;
                        _spreadTo[found] = j;
                        _spreadPower[found++] = em.StatusPower(i, kind);
                        given++;
                    }
                }
                for (int f = 0; f < found; f++) em.ApplyStatus(_spreadTo[f], a.E.status, _spreadPower[f], 1);
                Spreads += found;
                found = 0;
            }
            _spreadClock = interval;
        }

        /// <summary>Drops the trail marks at `pos` as if the player slid there (tests).</summary>
        public void DebugMark(Vector3 pos)
        {
            for (int k = 0; k < _fxCount; k++)
                if (_fx[k].E.kind == EffectKind.SlideTrail) AddMark(MarkFor(_fx[k], pos, ground: false));
        }

        public void Tick(float dt, float unscaledDt)
        {
            if (_build == null) return;
            if (_build.State.Version != _version || WeaponsVersion() != _weaponsVersion) Refresh();
            if (dt <= 0f) return;
            if (RunState.CombatOver) { if (_burstCount > 0 || _markCount > 0) Clear(); return; } // P1: bursts, trails and spreads stop with the player
            var em = EnemyManager.Instance;
            if (em == null) return;
            for (int b = 0; b < maxBurstsPerFrame && _burstCount > 0; b++)
            {
                var burst = _bursts[_burstHead];
                _burstHead = (_burstHead + 1) % _bursts.Length;
                _burstCount--;
                ResolveBurst(em, burst);
            }
            if (_anyTrail || _markCount > 0) TickMarks(em, dt);
            if (_anySpread) TickSpread(em, dt);
        }

        /// <summary>Run end / tests: forget queued bursts and trail marks.</summary>
        public void Clear()
        {
            _burstCount = _burstHead = _markCount = 0;
            _spreadClock = 0f;
            for (int k = 0; k < _fxCount; k++) _fx[k].HitCount = 0;
        }
    }
}
