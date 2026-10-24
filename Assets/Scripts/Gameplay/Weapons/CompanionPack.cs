using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// The Summon pattern (CLAUDE.md §6.8 "Summon", Phase 5.15): companions that fight beside the player, driven by
    /// WeaponSystem from one weapon's numbers. Amount = how many; Damage / crit / on-hit status = each bite (through
    /// WeaponSystem.CompanionHit, so Boons proc and kills count for the weapon); cooldown = seconds between one
    /// companion's bites; speed = run speed; range = the leash; acquireRange = how far from the player they pick
    /// targets (capped at the leash). Enemies don't target them and they can't be hurt: they're a weapon, not units to
    /// babysit.
    /// Note 37 (they got stuck on a couple of tough mobs while he had nothing else to fight with): they fight near him.
    /// A target is the enemy nearest the player (and a little nearer the companion; one that other companions are on
    /// costs more), marked enemies (HuntersMark: the Hunting Spear's hits) before any other, re-checked every
    /// retargetInterval s (staggered, through the spatial hash). Hit and move: after bitesPerTarget bites (or
    /// holdSeconds there, or chaseSeconds without reaching it) a companion moves on to another. Past the leash it drops
    /// the fight and runs back faster than it chases, and the farther it is from him the faster it runs, so when he runs
    /// off the pack follows.
    /// A handful of GameObjects with rigid parts, driven centrally (no per-companion Update), no allocation after spawn.
    /// meshScale = the companions' size. One Summon weapon at a time.
    /// </summary>
    public sealed class CompanionPack : MonoBehaviour
    {
        public const int Capacity = 8;
        public static CompanionPack Instance { get; private set; }

        [Tooltip("Bite reach beyond the target's radius (m).")]
        public float biteReach = 0.7f;
        public float bodyRadius = 0.35f;
        [Tooltip("With nothing to hunt they circle the player this far out (m).")]
        public float heelRadius = 2.2f;
        public float turnSpeed = 720f;
        [Tooltip("Hunting Horn: seconds in the air for a pounce, and its apex (m).")]
        public float pounceTime = 0.4f;
        public float pounceHeight = 1.3f;

        [Header("Hunting (note 37: near him, never stuck)")]
        [Tooltip("Seconds between one companion's target re-checks (staggered across the pack).")]
        public float retargetInterval = 0.6f;
        [Tooltip("Hit and move: bites on one target before it picks another…")]
        public int bitesPerTarget = 2;
        [Tooltip("…or seconds on one target (from the pick), once it has bitten it…")]
        public float holdSeconds = 1.5f;
        [Tooltip("…or seconds on one it hasn't bitten yet (can't reach it).")]
        public float chaseSeconds = 2.5f;
        [Tooltip("A pick scores the enemy's distance to the player + this × its distance to the companion (m per m).")]
        public float nearSelfWeight = 0.3f;
        [Tooltip("…+ this many metres for every other companion already on it (the pack spreads a little).")]
        public float sharePenalty = 2.5f;
        [Tooltip("The periodic re-check only switches to an enemy this much better (m), or to a marked one.")]
        public float switchMargin = 1.5f;
        [Tooltip("This far past the leash (m) a companion drops its fight and runs back to him…")]
        public float leashSlack = 1.5f;
        [Tooltip("…at its run speed × this (also the most it speeds up to keep near him while hunting)…")]
        public float returnSpeedMul = 1.6f;
        [Tooltip("…until it's within this fraction of the leash of him (at least heelRadius + 1 m).")]
        public float regroupFraction = 0.45f;

        struct Pal
        {
            public Transform T;
            public PartRig Rig;
            public Vector3 Pos, PounceFrom, PounceTo;
            public float Yaw, Phase, BiteCd, BiteT, PounceT, PounceMul, Speed;
            public int Target, TargetUid;
            /// <summary>Bites on the current target and seconds since it was picked; seconds to the next re-check; the
            /// uid of the one it last moved on from; running back to him (past the leash).</summary>
            public int Bitten, Left;
            public float OnTargetT, RetargetT;
            public bool Returning;
        }

        readonly Pal[] _pals = new Pal[Capacity];
        readonly int[] _query = new int[64];
        readonly int[] _scan = new int[512]; // target picks: every enemy within the leash (a dense crowd overfills 64)
        int _count;
        GameObject _model;
        Material _material;
        Transform _root;
        float _drivenAt = -1f, _frenzyT, _frenzyBite = 1f, _frenzySpeed = 1f, _scale = 1f;
        StatusDef _pounceStatus;

        public int Count => _count;
        public int Bites { get; private set; }
        public int Pounces { get; private set; }
        public int Frenzies { get; private set; }
        /// <summary>Targets picked and how many of them were marked; moves on after a bite window; runs back from past
        /// the leash (tests, bot log).</summary>
        public int Picks { get; private set; }
        public int MarkedPicks { get; private set; }
        public int Switches { get; private set; }
        public int Recalls { get; private set; }
        /// <summary>The leash at the last Drive (m): the weapon's range × Area.</summary>
        public float Leash { get; private set; }
        public bool Frenzied => _frenzyT > 0f;
        public Vector3 PositionOf(int k) => _pals[k].Pos;
        public bool IsPouncing(int k) => _pals[k].PounceT >= 0f;
        public int TargetOf(int k) => _pals[k].Target;
        public int TargetUidOf(int k) => _pals[k].Target >= 0 ? _pals[k].TargetUid : 0;
        public bool IsReturning(int k) => _pals[k].Returning;

        void OnEnable() => Instance = this;

        void OnDisable()
        {
            if (Instance == this) Instance = null;
            DespawnAll();
        }

        void OnDestroy()
        {
            if (_root != null) Destroy(_root.gameObject);
        }

        /// <summary>Nobody drove them for a while (the weapon was removed, a loan ran out): they leave. Game time, so a
        /// pause (level-up cards) keeps them.</summary>
        void LateUpdate()
        {
            if (_count > 0 && Time.time - _drivenAt > 0.25f) DespawnAll();
        }

        /// <summary>Called by WeaponSystem every frame the Summon weapon ticks.</summary>
        public void Drive(WeaponInstance w, WeaponSystem ws, EnemyManager em, float dt)
        {
            _drivenAt = Time.time;
            var d = w.Def;
            if (d.summon != _model) { DespawnAll(); _model = d.summon; }
            if (_model == null) return;
            _scale = d.meshScale > 0f ? d.meshScale : 1f;
            int want = Mathf.Min(ws.AmountOf(w), Capacity);
            while (_count < want) Spawn();
            while (_count > want) Despawn(_count - 1);
            if (dt <= 0f || em == null) return;
            if (_frenzyT > 0f) _frenzyT -= dt;
            bool frenzy = _frenzyT > 0f;
            float area = ws.AreaOf(w);
            float leash = Mathf.Max(1f, d.range * area);
            float pick = d.acquireRange > 0f ? Mathf.Min(d.acquireRange * area, leash) : leash;
            Leash = leash;
            float speed = ws.SpeedOf(w) * (frenzy ? _frenzySpeed : 1f);
            float interval = ws.CooldownOf(w) / (frenzy ? _frenzyBite : 1f);
            Vector3 pp = transform.position;
            var marks = HuntersMark.Instance;
            for (int k = 0; k < _count; k++) Step(ref _pals[k], k, w, ws, em, marks, dt, pp, pick, leash, speed, interval);
        }

        /// <summary>
        /// P1: the player went down (WeaponSystem.Halted). The companions stop where they are and turn to the player: a
        /// pounce in the air lands without biting, no bite starts. Called instead of Drive, so they don't leave either.
        /// </summary>
        public void Hold(float dt)
        {
            _drivenAt = Time.time;
            _frenzyT = 0f;
            var em = EnemyManager.Instance;
            Vector3 pp = transform.position;
            for (int k = 0; k < _count; k++)
            {
                ref var p = ref _pals[k];
                if (p.PounceT >= 0f)
                {
                    p.PounceT = -1f;
                    p.Pos = p.PounceTo;
                    if (em != null) Settle(ref p.Pos, em);
                }
                p.Target = -1;
                p.Returning = false;
                p.BiteT = 0f;
                p.Speed = 0f;
                Vector3 to = Flat(pp - p.Pos);
                if (to.sqrMagnitude > 0.01f) p.Yaw = Mathf.MoveTowardsAngle(p.Yaw, Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg, turnSpeed * 0.25f * dt);
                Animate(ref p, dt, 0f);
            }
        }

        void Step(ref Pal p, int k, WeaponInstance w, WeaponSystem ws, EnemyManager em, HuntersMark marks, float dt, Vector3 pp,
                  float pick, float leash, float speed, float interval)
        {
            p.BiteCd -= dt;
            p.BiteT -= dt;
            p.RetargetT -= dt;
            if (p.PounceT >= 0f) { StepPounce(ref p, w, ws, em, dt); return; }

            // the leash: too far from him, it drops the fight and runs back; it hunts again once it's close
            float dp = Flat(pp - p.Pos).magnitude;
            if (!p.Returning && dp > leash + leashSlack)
            {
                p.Returning = true;
                p.Target = -1;
                Recalls++;
            }
            else if (p.Returning && dp <= Mathf.Max(heelRadius + 1f, leash * regroupFraction)) p.Returning = false;
            if (!p.Returning) Hunt(ref p, k, em, marks, pp, pick, dt);

            Vector3 goal;
            float stopAt;
            if (p.Target >= 0)
            {
                goal = em.GetPosition(p.Target);
                stopAt = em.GetRadius(p.Target) + biteReach * 0.8f;
            }
            else if (p.Returning)
            {
                goal = pp;
                stopAt = heelRadius;
            }
            else
            {
                float a = k * 360f / Mathf.Max(1, _count) + Time.time * 25f; // heel: circle the player
                goal = pp + Quaternion.Euler(0f, a, 0f) * Vector3.forward * heelRadius;
                stopAt = 0.2f;
            }
            // running back: returnSpeedMul; hunting: faster the farther it is from him (×1 within two heel radii, up to
            // ×returnSpeedMul at the leash), so a pack fighting behind a running player keeps up
            float near = heelRadius * 2f;
            float run = speed * (p.Returning ? returnSpeedMul
                : dp <= near || leash <= near ? 1f : Mathf.Lerp(1f, returnSpeedMul, (dp - near) / (leash - near)));
            Vector3 to = Flat(goal - p.Pos);
            float dist = to.magnitude, step = 0f;
            if (dist > stopAt)
            {
                step = Mathf.Min(dist - stopAt, run * dt);
                p.Pos += to / dist * step;
            }
            for (int j = 0; j < _count; j++) // a little room between them
            {
                if (j == k) continue;
                Vector3 o = Flat(p.Pos - _pals[j].Pos);
                float m = o.sqrMagnitude, min = bodyRadius * 2f;
                if (m > 1e-6f && m < min * min) p.Pos += o / Mathf.Sqrt(m) * ((min - Mathf.Sqrt(m)) * 0.5f);
            }
            Settle(ref p.Pos, em);
            if (dist > 0.05f) p.Yaw = Mathf.MoveTowardsAngle(p.Yaw, Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg, turnSpeed * dt);

            if (p.Target >= 0 && dist <= stopAt + 0.25f && p.BiteCd <= 0f)
            {
                Vector3 dir = dist > 1e-3f ? to / dist : Vector3.forward;
                ws.CompanionHit(w, em, p.Target, dir, 1f);
                p.BiteCd = interval;
                p.BiteT = 0.18f;
                p.Bitten++;
                Bites++;
            }
            p.Speed = step / dt;
            Animate(ref p, dt, 0f);
        }

        /// <summary>
        /// Keeps, drops or changes companion k's target: a dead one, or one past `pick` m from him, is dropped; after its
        /// bite window it moves on (that enemy is the last choice for its next pick); with no target, or every
        /// retargetInterval s, it picks again, switching only to a marked enemy or one switchMargin m better.
        /// </summary>
        void Hunt(ref Pal p, int k, EnemyManager em, HuntersMark marks, Vector3 pp, float pick, float dt)
        {
            if (p.Target >= 0)
            {
                bool valid = em.IsAlive(p.Target) && em.GetUid(p.Target) == p.TargetUid
                             && Flat(em.GetPosition(p.Target) - pp).sqrMagnitude <= pick * pick;
                if (!valid) p.Target = -1;
                else
                {
                    p.OnTargetT += dt;
                    if (p.Bitten >= bitesPerTarget || p.OnTargetT >= (p.Bitten > 0 ? holdSeconds : chaseSeconds))
                    {
                        p.Left = p.TargetUid; // hit and move: a tough one can't hold it
                        p.Target = -1;
                        Switches++;
                    }
                }
            }
            bool due = p.RetargetT <= 0f; // its own clock, so the pack's re-checks stay staggered
            if (due)
            {
                float every = Mathf.Max(0.05f, retargetInterval);
                while (p.RetargetT <= 0f) p.RetargetT += every;
            }
            if (p.Target >= 0 && !due) return;
            int t = Pick(k, em, marks, pp, pick, p.Left, out bool marked);
            if (t < 0) return;
            if (p.Target >= 0)
            {
                if (t == p.Target) return;
                bool cur = marks != null && marks.IsMarked(p.Target, p.TargetUid);
                if (cur && !marked) return;
                if (!(marked && !cur)) // same kind: only for a clearly better one
                {
                    float now = Score(k, p.Target, em.GetPosition(p.Target), pp, p.Pos);
                    if (Score(k, t, em.GetPosition(t), pp, p.Pos) > now - switchMargin) return;
                }
            }
            p.Target = t;
            p.TargetUid = em.GetUid(t);
            p.Bitten = 0;
            p.OnTargetT = 0f;
            Picks++;
            if (marked) MarkedPicks++;
        }

        /// <summary>
        /// Companion k's best target within `radius` of the player: the marked enemies first (HuntersMark's list), then
        /// every enemy there (the spatial hash). Lowest Score wins; the one with uid `avoid` (just left) only if nothing
        /// else is there. -1 = nothing to hunt.
        /// </summary>
        int Pick(int k, EnemyManager em, HuntersMark marks, Vector3 pp, float radius, int avoid, out bool marked)
        {
            marked = false;
            int best = -1, spare = -1;
            float bestS = float.MaxValue, spareS = float.MaxValue, r2 = radius * radius;
            Vector3 self = _pals[k].Pos;
            if (marks != null)
            {
                for (int m = 0; m < marks.Count; m++)
                {
                    int e = marks.SlotAt(m);
                    if (!em.IsAlive(e)) continue;
                    int uid = em.GetUid(e);
                    if (!marks.IsMarked(e, uid)) continue;
                    Vector3 ep = em.GetPosition(e);
                    if (Flat(ep - pp).sqrMagnitude > r2) continue;
                    float s = Score(k, e, ep, pp, self);
                    if (uid == avoid) { if (s < spareS) { spareS = s; spare = e; } }
                    else if (s < bestS) { bestS = s; best = e; }
                }
                if (best >= 0) { marked = true; return best; }
            }
            int n = em.QueryAlive(pp, radius, _scan);
            for (int q = 0; q < n; q++)
            {
                int e = _scan[q];
                Vector3 ep = em.GetPosition(e);
                if (Flat(ep - pp).sqrMagnitude > r2) continue; // its centre, as Hunt keeps it (not just its edge)
                float s = Score(k, e, ep, pp, self);
                if (em.GetUid(e) == avoid) { if (s < spareS) { spareS = s; spare = e; } }
                else if (s < bestS) { bestS = s; best = e; }
            }
            if (best >= 0) return best;
            if (spare >= 0) marked = marks != null && marks.IsMarked(spare, em.GetUid(spare));
            return spare;
        }

        /// <summary>Metres from the player + nearSelfWeight × metres from the companion + sharePenalty per other companion on it.</summary>
        float Score(int k, int e, Vector3 ep, Vector3 pp, Vector3 self)
        {
            float s = Flat(ep - pp).magnitude + nearSelfWeight * Flat(ep - self).magnitude;
            for (int j = 0; j < _count; j++)
                if (j != k && _pals[j].Target == e) s += sharePenalty;
            return s;
        }

        void StepPounce(ref Pal p, WeaponInstance w, WeaponSystem ws, EnemyManager em, float dt)
        {
            p.PounceT += dt / Mathf.Max(0.05f, pounceTime);
            float u = Mathf.Min(1f, p.PounceT);
            Vector3 flat = Vector3.Lerp(p.PounceFrom, p.PounceTo, u);
            p.Pos = flat;
            Vector3 to = Flat(p.PounceTo - p.PounceFrom);
            if (to.sqrMagnitude > 1e-4f) p.Yaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
            float lift = 4f * pounceHeight * u * (1f - u);
            if (u >= 1f)
            {
                p.PounceT = -1f;
                Settle(ref p.Pos, em);
                if (p.Target >= 0 && em.IsAlive(p.Target) && em.GetUid(p.Target) == p.TargetUid)
                {
                    int uid = p.TargetUid;
                    ws.CompanionHit(w, em, p.Target, to.sqrMagnitude > 1e-4f ? to.normalized : Vector3.forward, p.PounceMul);
                    if (_pounceStatus != null && em.IsAlive(p.Target) && em.GetUid(p.Target) == uid) em.ApplyStatus(p.Target, _pounceStatus, 0f);
                    Bites++;
                    p.Bitten = 1; // the pounce is its first bite on this one: one more, and it moves on
                    p.OnTargetT = 0f;
                }
                var pop = DeathPopFx.Instance;
                if (pop != null) pop.Burst(p.Pos + Vector3.up * 0.4f, 8, 3f, 0.1f, new Color(0.72f, 0.95f, 1f), Color.white);
                p.BiteT = 0.18f;
                p.BiteCd = 0.3f;
                lift = 0f;
            }
            p.Speed = 0f;
            Animate(ref p, dt, lift);
        }

        /// <summary>Hunting Horn: every companion leaps at a different enemy among the ones nearest `focus` (within
        /// `range`); landing bites for `damageMul` bites and applies `status`. Returns how many leapt.</summary>
        public int PounceAll(Vector3 focus, float range, float damageMul, StatusDef status)
        {
            var em = EnemyManager.Instance;
            if (em == null || _count == 0) return 0;
            _pounceStatus = status;
            int n = em.QueryAlive(focus, range, _query), leapt = 0;
            if (n == 0) return 0;
            for (int k = 0; k < _count; k++)
            {
                int best = -1;
                float bestD = float.MaxValue;
                for (int q = 0; q < n; q++) // the nearest one no earlier companion took
                {
                    int e = _query[q];
                    if (e < 0) continue;
                    float dd = Flat(em.GetPosition(e) - focus).sqrMagnitude;
                    if (dd < bestD) { bestD = dd; best = q; }
                }
                if (best < 0) // more companions than enemies: they share
                {
                    for (int q = 0; q < n; q++) if (_query[q] < 0) _query[q] = -_query[q] - 1;
                    k--;
                    continue;
                }
                int target = _query[best];
                _query[best] = -target - 1;
                ref var p = ref _pals[k];
                p.Target = target;
                p.TargetUid = em.GetUid(target);
                p.Returning = false;
                p.Bitten = 0;
                p.OnTargetT = 0f;
                p.PounceFrom = p.Pos;
                Vector3 tp = em.GetPosition(target), back = Flat(p.Pos - tp);
                p.PounceTo = tp + (back.sqrMagnitude > 1e-4f ? back.normalized : Vector3.back) * (em.GetRadius(target) + 0.3f);
                p.PounceT = 0f;
                p.PounceMul = damageMul;
                leapt++;
                Pounces++;
            }
            return leapt;
        }

        /// <summary>Feral Bond: `seconds` of faster bites (×biteMul) and runs (×speedMul).</summary>
        public void Frenzy(float seconds, float biteMul, float speedMul)
        {
            _frenzyT = Mathf.Max(_frenzyT, seconds);
            _frenzyBite = Mathf.Max(1f, biteMul);
            _frenzySpeed = Mathf.Max(1f, speedMul);
            Frenzies++;
            var pop = DeathPopFx.Instance;
            if (pop == null) return;
            for (int k = 0; k < _count; k++) pop.Burst(_pals[k].Pos + Vector3.up * 0.7f, 6, 2.5f, 0.1f, new Color(0.72f, 0.95f, 1f), Color.white);
        }

        void Settle(ref Vector3 pos, EnemyManager em)
        {
            if (em.Obstacles != null) em.Obstacles.PushOut(ref pos, bodyRadius);
            em.ClampInside(ref pos, bodyRadius);
            if (em.Height != null) pos.y = em.Height.Sample(pos.x, pos.z);
        }

        void Animate(ref Pal p, float dt, float lift)
        {
            float run = Mathf.Clamp01(p.Speed / 3f);
            p.Phase += p.Speed * dt * 2.4f;
            float s = Mathf.Sin(p.Phase) * run * 35f;
            var pose = PartPose.Identity;
            pose.ArmL = PartPose.Swing(s);
            pose.ArmR = PartPose.Swing(-s);
            pose.LegL = PartPose.Swing(-s);
            pose.LegR = PartPose.Swing(s);
            pose.RootY = Mathf.Abs(Mathf.Sin(p.Phase)) * 0.05f * run + lift;
            if (p.BiteT > 0f) { pose.Head = PartPose.Lean(28f); pose.Body = PartPose.Lean(6f); }
            if (lift > 0f) { pose.ArmL = pose.ArmR = PartPose.Swing(45f); pose.LegL = pose.LegR = PartPose.Swing(-40f); }
            p.T.SetPositionAndRotation(p.Pos, Quaternion.Euler(0f, p.Yaw, 0f));
            p.Rig.Apply(pose);
        }

        void Spawn()
        {
            if (_root == null) _root = new GameObject("[Companions]").transform;
            int k = _count;
            ref var p = ref _pals[k];
            if (p.T == null || p.T.gameObject == null)
            {
                // a holder that moves, with the model under it: PartRig.Apply owns the model root's local transform
                var holder = new GameObject(_model.name + "_" + k).transform;
                holder.SetParent(_root, false);
                var go = Instantiate(_model, holder);
                go.name = _model.name;
                // the FBX carries no material (ModelImportRules): use the player's own palette material
                if (_material == null)
                {
                    var visual = transform.Find("Visual");
                    var r = visual != null ? visual.GetComponentInChildren<Renderer>() : null;
                    _material = r != null ? r.sharedMaterial : null;
                }
                if (_material != null)
                    foreach (var r in go.GetComponentsInChildren<Renderer>(true)) r.sharedMaterial = _material;
                p.T = holder;
                p.Rig = new PartRig(go.transform);
            }
            float a = k * 360f / Capacity;
            p.Pos = transform.position + Quaternion.Euler(0f, a, 0f) * Vector3.forward * heelRadius;
            var em = EnemyManager.Instance;
            if (em != null) Settle(ref p.Pos, em);
            p.Yaw = a;
            p.Target = -1;
            p.BiteCd = 0.3f;
            p.PounceT = -1f;
            p.Returning = false;
            p.Bitten = p.Left = 0;
            p.OnTargetT = 0f;
            p.RetargetT = k * retargetInterval / Capacity; // staggered re-checks
            p.T.gameObject.SetActive(true);
            p.T.SetPositionAndRotation(p.Pos, Quaternion.Euler(0f, p.Yaw, 0f));
            p.T.localScale = Vector3.one * _scale; // WeaponDef.meshScale: the wolves are drawn a little bigger than life to read in a crowd
            var pop = DeathPopFx.Instance;
            if (pop != null) pop.Burst(p.Pos + Vector3.up * 0.5f, 10, 3f, 0.12f, new Color(0.72f, 0.95f, 1f), Color.white);
            _count++;
        }

        void Despawn(int k)
        {
            if (_pals[k].T != null) _pals[k].T.gameObject.SetActive(false);
            _count = Mathf.Min(_count, k);
        }

        public void DespawnAll()
        {
            for (int k = Capacity - 1; k >= 0; k--)
            {
                if (_pals[k].T != null) _pals[k].T.gameObject.SetActive(false);
                _pals[k].PounceT = -1f;
            }
            _count = 0;
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
    }
}
