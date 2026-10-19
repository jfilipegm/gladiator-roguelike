using Gladiator.Core;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// Runs the behavioural Boons (M2.6) off the event bus: status on hit / on crit, chain sparks, bursts from dying
    /// statused enemies, slide trails, and the damage bonuses against statused enemies (written into EnemyManager).
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

        struct Active { public BoonEffect E; public float Potency; public int Source; }
        struct Burst { public Vector3 Pos; public float StatusPower; public int Fx; }
        struct Mark { public Vector3 Pos; public float Life, MaxLife; }

        const int MaxEffects = 24;
        readonly Active[] _fx = new Active[MaxEffects];
        int _fxCount, _version = -1;
        int _spark = -1, _hitCounter;
        bool _anyOnHit, _anyOnCrit, _anyBurst, _anyTrail;

        readonly Burst[] _bursts = new Burst[64];
        int _burstHead, _burstCount;
        readonly Mark[] _marks = new Mark[48];
        int _markCount;
        float _trailClock, _slidDist;
        Vector3 _lastSlidePos;

        readonly int[] _query = new int[256];
        readonly int[] _struck = new int[16];

        PlayerBuild _build;
        PlayerStats _stats;
        PlayerMotor _motor;

        public int SparksFired { get; private set; }
        public int BurstsFired { get; private set; }
        public int MarkCount => _markCount;

        void Awake()
        {
            _build = GetComponent<PlayerBuild>();
            _stats = GetComponent<PlayerStats>();
            _motor = GetComponent<PlayerMotor>();
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

        /// <summary>Re-reads the owned Boons' effects (only when the build changed).</summary>
        void Refresh()
        {
            var state = _build.State;
            _version = state.Version;
            _fxCount = 0;
            _spark = -1;
            _anyOnHit = _anyOnCrit = _anyBurst = _anyTrail = false;
            var em = EnemyManager.Instance;
            if (em != null)
                for (int k = 0; k < EnemyManager.StatusSlots; k++) em.BonusVsStatus[k] = em.BonusPerStack[k] = 0f;
            for (int i = 0; i < state.OwnedCount; i++)
            {
                var o = state.GetOwned(i);
                var list = o.Def.effects;
                if (list == null) continue;
                for (int j = 0; j < list.Length && _fxCount < MaxEffects; j++)
                {
                    var e = list[j];
                    _fx[_fxCount] = new Active { E = e, Potency = o.Potency, Source = GameInfo.StableId(o.Def.id) };
                    int slot = e.status != null ? e.status.Slot : -1;
                    switch (e.kind)
                    {
                        case EffectKind.StatusOnHit: _anyOnHit = true; break;
                        case EffectKind.StatusOnCrit: _anyOnCrit = true; break;
                        case EffectKind.BurstOnDeath: _anyBurst = true; break;
                        case EffectKind.SlideTrail: _anyTrail = true; break;
                        case EffectKind.ChainSpark: _spark = _fxCount; _anyOnHit = true; break;
                        case EffectKind.DamagePerStack: if (em != null && slot >= 0) em.BonusPerStack[slot] += e.bonus * o.Potency; break;
                        case EffectKind.DamageVsStatus: if (em != null && slot >= 0) em.BonusVsStatus[slot] += e.bonus * o.Potency; break;
                    }
                    _fxCount++;
                }
            }
        }

        // ---------- events ----------

        void OnHit(in HitEvent e)
        {
            if (!_anyOnHit || (e.Flags & DamageFlags.Proc) != 0) return;
            var em = EnemyManager.Instance;
            if (em == null) return;
            bool alive = em.IsAlive(e.Index) && em.GetUid(e.Index) == e.TargetId;
            for (int k = 0; k < _fxCount; k++)
            {
                ref var a = ref _fx[k];
                if (a.E.kind != EffectKind.StatusOnHit || !alive) continue;
                if (GameRandom.Chance(a.E.chance * a.Potency))
                    em.ApplyStatus(e.Index, a.E.status, e.Damage * a.E.power, Mathf.Max(1, a.E.stacks));
            }
            if (_spark >= 0 && ++_hitCounter >= Mathf.Max(1, _fx[_spark].E.everyNth))
            {
                _hitCounter = 0;
                Spark(em, e.Index, e.Position, alive);
            }
        }

        void OnCrit(in HitEvent e)
        {
            if (!_anyOnCrit || (e.Flags & DamageFlags.Proc) != 0) return;
            var em = EnemyManager.Instance;
            if (em == null || !em.IsAlive(e.Index) || em.GetUid(e.Index) != e.TargetId) return;
            for (int k = 0; k < _fxCount; k++)
            {
                ref var a = ref _fx[k];
                if (a.E.kind == EffectKind.StatusOnCrit)
                    em.ApplyStatus(e.Index, a.E.status, a.E.power * a.Potency * DamageMul, Mathf.Max(1, a.E.stacks));
            }
        }

        void OnKill(in KillEvent e)
        {
            if (!_anyBurst) return;
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

        /// <summary>A spark from enemy `from` jumps to the nearest un-struck enemies, one after another.</summary>
        void Spark(EnemyManager em, int from, Vector3 fromPos, bool fromAlive)
        {
            ref var a = ref _fx[_spark];
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
                em.ApplyStatus(i, a.E.status, burst.StatusPower, 1); // pass the fire on first: the hit may kill it, chaining
                em.ApplyDamage(i, new DamageInfo { Amount = damage, Flags = DamageFlags.Proc | DamageFlags.Area, Point = burst.Pos, SourceId = a.Source });
            }
            if (em.deathPop != null) em.deathPop.Burst(burst.Pos + Vector3.up * 0.6f, 10, 5f, 0.22f, burstColorA, burstColorB);
        }

        void TickTrail(EnemyManager em, float dt)
        {
            var tele = TelegraphRenderer.Instance;
            Active trail = default;
            bool has = false;
            for (int k = 0; k < _fxCount && !has; k++) if (_fx[k].E.kind == EffectKind.SlideTrail) { trail = _fx[k]; has = true; }
            if (has && _motor != null && _motor.IsSliding)
            {
                Vector3 p = transform.position;
                _slidDist += (p - _lastSlidePos).magnitude;
                if (_slidDist >= trailSpacing || _markCount == 0)
                {
                    _slidDist = 0f;
                    if (_markCount == _marks.Length) // full: drop the oldest (front)
                    {
                        for (int s = 1; s < _markCount; s++) _marks[s - 1] = _marks[s];
                        _markCount--;
                    }
                    _marks[_markCount++] = new Mark { Pos = p, Life = trail.E.duration, MaxLife = Mathf.Max(0.1f, trail.E.duration) };
                }
            }
            _lastSlidePos = transform.position;
            if (_markCount == 0) return;

            _trailClock -= dt;
            bool apply = _trailClock <= 0f && has;
            if (apply) _trailClock += trailTick;
            float dps = trail.E.power * trail.Potency * DamageMul;
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
                    var c = trailColor;
                    c.a *= Mathf.Clamp01(mk.Life / mk.MaxLife * 2f);
                    tele.Circle(mk.Pos, trail.E.radius, 0f, c, TelegraphRenderer.Style.Ring, 0.18f, m * 0.31f);
                }
                if (!apply || em == null) continue;
                int n = em.QueryAlive(mk.Pos, trail.E.radius, _query);
                for (int q = 0; q < n; q++) em.ApplyStatus(_query[q], trail.E.status, dps, Mathf.Max(1, trail.E.stacks));
            }
        }

        /// <summary>Drops a trail mark at `pos` as if the player slid there (tests).</summary>
        public void DebugMark(Vector3 pos)
        {
            for (int k = 0; k < _fxCount; k++)
            {
                if (_fx[k].E.kind != EffectKind.SlideTrail) continue;
                if (_markCount < _marks.Length) _marks[_markCount++] = new Mark { Pos = pos, Life = _fx[k].E.duration, MaxLife = _fx[k].E.duration };
                return;
            }
        }

        public void Tick(float dt, float unscaledDt)
        {
            if (_build == null) return;
            if (_build.State.Version != _version) Refresh();
            if (dt <= 0f) return;
            var em = EnemyManager.Instance;
            if (em == null) return;
            for (int b = 0; b < maxBurstsPerFrame && _burstCount > 0; b++)
            {
                var burst = _bursts[_burstHead];
                _burstHead = (_burstHead + 1) % _bursts.Length;
                _burstCount--;
                ResolveBurst(em, burst);
            }
            if (_anyTrail || _markCount > 0) TickTrail(em, dt);
        }

        /// <summary>Run end / tests: forget queued bursts and trail marks.</summary>
        public void Clear()
        {
            _burstCount = _burstHead = _markCount = 0;
            _hitCounter = 0;
        }
    }
}
