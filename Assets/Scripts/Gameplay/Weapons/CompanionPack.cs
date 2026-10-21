using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// The Summon pattern (CLAUDE.md §6.8 "Summon", Phase 5.15): companions that fight beside the player, driven by
    /// WeaponSystem from one weapon's numbers. Amount = how many; Damage / crit / on-hit status = each bite (through
    /// WeaponSystem.CompanionHit, so Boons proc and kills count for the weapon); cooldown = seconds between one
    /// companion's bites; speed = run speed; acquireRange = how far from the player they hunt; range = the leash (past
    /// it they run back). Enemies don't target them and they can't be hurt: they're a weapon, not units to babysit.
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

        struct Pal
        {
            public Transform T;
            public PartRig Rig;
            public Vector3 Pos, PounceFrom, PounceTo;
            public float Yaw, Phase, BiteCd, BiteT, PounceT, PounceMul, Speed;
            public int Target, TargetUid;
        }

        readonly Pal[] _pals = new Pal[Capacity];
        readonly int[] _query = new int[64];
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
        public bool Frenzied => _frenzyT > 0f;
        public Vector3 PositionOf(int k) => _pals[k].Pos;
        public bool IsPouncing(int k) => _pals[k].PounceT >= 0f;
        public int TargetOf(int k) => _pals[k].Target;

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
            float hunt = d.acquireRange * area, leash = Mathf.Max(hunt, d.range * area);
            float speed = ws.SpeedOf(w) * (frenzy ? _frenzySpeed : 1f);
            float interval = ws.CooldownOf(w) / (frenzy ? _frenzyBite : 1f);
            Vector3 pp = transform.position;
            for (int k = 0; k < _count; k++) Step(ref _pals[k], k, w, ws, em, dt, pp, hunt, leash, speed, interval);
        }

        void Step(ref Pal p, int k, WeaponInstance w, WeaponSystem ws, EnemyManager em, float dt, Vector3 pp,
                  float hunt, float leash, float speed, float interval)
        {
            p.BiteCd -= dt;
            p.BiteT -= dt;
            if (p.PounceT >= 0f) { StepPounce(ref p, w, ws, em, dt); return; }

            float dp = Flat(pp - p.Pos).magnitude;
            bool far = dp > leash + 1f;
            bool valid = !far && p.Target >= 0 && em.IsAlive(p.Target) && em.GetUid(p.Target) == p.TargetUid
                         && Flat(em.GetPosition(p.Target) - pp).sqrMagnitude <= leash * leash;
            if (!valid)
            {
                p.Target = -1;
                if (!far && em.TryGetNearest(p.Pos, hunt, out int t) && Flat(em.GetPosition(t) - pp).sqrMagnitude <= leash * leash)
                {
                    p.Target = t;
                    p.TargetUid = em.GetUid(t);
                }
            }

            Vector3 goal;
            float stopAt;
            if (p.Target >= 0)
            {
                goal = em.GetPosition(p.Target);
                stopAt = em.GetRadius(p.Target) + biteReach * 0.8f;
            }
            else
            {
                float a = k * 360f / Mathf.Max(1, _count) + Time.time * 25f; // heel: circle the player
                goal = pp + Quaternion.Euler(0f, a, 0f) * Vector3.forward * heelRadius;
                stopAt = 0.2f;
            }
            Vector3 to = Flat(goal - p.Pos);
            float dist = to.magnitude, step = 0f;
            if (dist > stopAt)
            {
                step = Mathf.Min(dist - stopAt, (far ? speed * 1.3f : speed) * dt);
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
                Bites++;
            }
            p.Speed = step / dt;
            Animate(ref p, dt, 0f);
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
