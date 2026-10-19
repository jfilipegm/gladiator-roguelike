using System.Collections.Generic;
using Gladiator.Core;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>Runtime state of one equipped weapon: its level, its own modifiers (weapon-level cards), and the swing.</summary>
    public sealed class WeaponInstance
    {
        public WeaponDef Def;
        public int Level = 1;
        /// <summary>This weapon's modifiers from its level-up cards (multiplier stats start at 1, Amount/Pierce at 0).</summary>
        public readonly StatBlock Local = new StatBlock();
        public float Cooldown;
        public float SwingT = -1f;
        public bool Impacted;
        public int BurstLeft;
        public int Mesh = -2; // -2 = not registered yet
        public Vector3 AimDir = Vector3.forward;
        /// <summary>Orbit: angle of the first ball (radians) and the weapon's own clock for re-hit intervals.</summary>
        public float Phase, Clock;
        /// <summary>Orbit: per enemy slot, the Clock time it can be hit again (allocated once, on first use).</summary>
        public float[] NextHit;

        /// <summary>Reuses this slot object for `def` at level 1 (slots are allocated once, so equipping is GC-free).</summary>
        internal void Init(WeaponDef def)
        {
            Def = def;
            Level = 1;
            Local.Reset();
            Cooldown = 0.3f;
            SwingT = -1f;
            Impacted = false;
            BurstLeft = 0;
            Mesh = -2;
            AimDir = Vector3.forward;
            Phase = Clock = 0f;
            if (NextHit != null) System.Array.Clear(NextHit, 0, NextHit.Length);
        }
    }

    /// <summary>
    /// Fires the player's weapons automatically (CLAUDE.md §5): nearest-enemy auto-aim, central tick, hits via the
    /// enemy spatial hash (never Physics.Overlap), damage through EnemyManager.ApplyDamage. Up to 4 slots.
    /// Every weapon runs the same timeline (acquire → wind-up → impact → cooldown); the impact is the pattern:
    /// MeleeArc (arc hit around the aim), Thrust (line hit + spear visual), Projectile (javelin volley at the nearest
    /// enemies), Returning (chakram out and back), Lob (bombs at the densest groups in reach, exploding after a fuse).
    /// Orbit (Gorm's Flail) has no timeline: its balls always circle the player and hit whatever they pass, each enemy at
    /// most once per cooldown. Numbers = weapon base × its level modifiers × player stats.
    /// </summary>
    public sealed class WeaponSystem : MonoBehaviour, ITickable
    {
        public const int MaxSlots = 4;

        public WeaponDef[] startingWeapons;
        public PlayerPartAnimator animator;
        public SlashFx slashFx;
        [Tooltip("Degrees between extra projectiles when a volley has more shots than targets.")]
        public float volleySpread = 12f;
        [Tooltip("Orbit weapons: height of the balls above the player's feet (m).")]
        public float orbitHeight = 0.8f;
        [Tooltip("Orbit weapons: spin of each ball around its chain (deg/s), for show.")]
        public float orbitRoll = 540f;
        const int MaxOrbiters = 8;

        PlayerStats _stats;

        // Player stats (upgrades × admin buffs) every weapon scales with.
        public float DamageMul => _stats != null ? _stats.Damage : DevSettings.DamageMul;
        public float AttackSpeedMul => _stats != null ? _stats.AttackSpeed : DevSettings.AttackSpeedMul;
        public float RangeMul => _stats != null ? _stats.Area : DevSettings.RangeMul;
        public float CritBonus => _stats != null ? _stats.CritChance : DevSettings.CritBonus;
        public float CritDamageBonus => _stats != null ? _stats.CritDamage : 0f;
        public float KnockbackMul => _stats != null ? _stats.Knockback : 1f;
        float PlayerStat(StatId s, float fallback) => _stats != null ? _stats.Get(s) : fallback;

        readonly List<WeaponInstance> _weapons = new List<WeaponInstance>(MaxSlots);
        readonly WeaponInstance[] _slots = { new WeaponInstance(), new WeaponInstance(), new WeaponInstance(), new WeaponInstance() };
        readonly WeaponInstance _loan = new WeaponInstance();
        readonly int[] _hits = new int[256];
        readonly int[] _targets = new int[16];
        readonly int[] _lobQuery = new int[256];
        readonly int[] _lobPick = new int[8];
        [Tooltip("Lob: nearest enemies considered as bomb targets (the one whose blast would catch the most wins).")]
        public int lobCandidates = 8;

        public IReadOnlyList<WeaponInstance> Weapons => _weapons;
        public int Swings { get; private set; }
        public int Hits { get; private set; }
        /// <summary>The crowd's weapon in the sand (Phase 5.1): fights beside the slots for a while (null = none).</summary>
        public WeaponInstance Temporary => TemporaryLeft > 0f ? _loan : null;
        public float TemporaryLeft { get; private set; }
        public float TemporaryDuration { get; private set; }

        void Awake()
        {
            _stats = GetComponent<PlayerStats>();
            if (startingWeapons != null)
                foreach (var w in startingWeapons) if (w != null) Equip(w);
        }

        void OnEnable() => GameLoop.Register(this, TickGroup.Weapons);
        void OnDisable() => GameLoop.Unregister(this);

        /// <summary>Adds `def` at level 1 (null when all slots are full or it's already equipped).</summary>
        public WeaponInstance Equip(WeaponDef def)
        {
            if (def == null || _weapons.Count >= MaxSlots || Find(def) != null) return null;
            WeaponInstance inst = null;
            for (int s = 0; s < _slots.Length && inst == null; s++)
                if (!_weapons.Contains(_slots[s])) inst = _slots[s];
            inst.Init(def);
            _weapons.Add(inst);
            return inst;
        }

        /// <summary>Drops every weapon (tests, dev showcase, a future gladiator swap).</summary>
        public void RemoveAll() => _weapons.Clear();

        public WeaponInstance Find(WeaponDef def)
        {
            for (int i = 0; i < _weapons.Count; i++) if (_weapons[i].Def == def) return _weapons[i];
            return null;
        }

        /// <summary>Applies `def`'s next level (its `levels` entry) scaled by `magnitude`. Returns the new level.</summary>
        public int LevelUp(WeaponDef def, float magnitude)
        {
            var w = Find(def);
            if (w == null) return 0;
            ApplyNextLevel(w, magnitude);
            return w.Level;
        }

        static void ApplyNextLevel(WeaponInstance w, float magnitude)
        {
            if (w.Level >= w.Def.MaxLevel) return;
            var mods = w.Def.levels[w.Level - 1].mods;
            if (mods != null) for (int m = 0; m < mods.Length; m++) w.Local.Add(mods[m], magnitude);
            w.Level++;
        }

        /// <summary>
        /// Lends `def` at `level` for `seconds` (the crowd's weapon in the sand): it fights beside the slots, takes none,
        /// and never shows in the cards. A new loan replaces the old one.
        /// </summary>
        public void GrantTemporary(WeaponDef def, int level, float seconds)
        {
            if (def == null || seconds <= 0f) return;
            _loan.Init(def);
            while (_loan.Level < level && _loan.Level < def.MaxLevel) ApplyNextLevel(_loan, 1f);
            TemporaryLeft = TemporaryDuration = seconds;
        }

        /// <summary>Ends the loan now (tests, run reset).</summary>
        public void EndTemporary() => TemporaryLeft = 0f;

        // ---------- effective numbers ----------
        public float Damage(WeaponInstance w) => w.Def.damage * w.Local[StatId.Damage] * DamageMul;
        public float CooldownOf(WeaponInstance w) => w.Def.cooldown / Mathf.Max(0.1f, w.Local[StatId.AttackSpeed] * AttackSpeedMul);
        public float AreaOf(WeaponInstance w) => w.Local[StatId.Area] * RangeMul;
        public int AmountOf(WeaponInstance w) => Mathf.Max(1, w.Def.amount + (int)w.Local[StatId.Amount] + (int)PlayerStat(StatId.Amount, 0f));
        public int PierceOf(WeaponInstance w) => w.Def.pierce < 0 ? -1 : w.Def.pierce + (int)w.Local[StatId.Pierce] + (int)PlayerStat(StatId.Pierce, 0f);
        public float SpeedOf(WeaponInstance w) => w.Def.speed * w.Local[StatId.ProjectileSpeed] * PlayerStat(StatId.ProjectileSpeed, 1f);
        float Crit(WeaponInstance w) => w.Def.critChance + w.Local[StatId.CritChance] + CritBonus;
        float CritMul(WeaponInstance w) => w.Def.critMultiplier + w.Local[StatId.CritDamage] + CritDamageBonus;
        float Knock(WeaponInstance w) => w.Def.knockback * w.Local[StatId.Knockback] * KnockbackMul;
        float Acquire(WeaponInstance w) => w.Def.acquireRange * AreaOf(w);

        public void Tick(float dt, float unscaledDt)
        {
            if (dt <= 0f) return;
            var em = EnemyManager.Instance;
            bool anySwing = false;
            for (int i = 0; i < _weapons.Count; i++)
            {
                var w = _weapons[i];
                TickWeapon(w, em, dt);
                if (w.SwingT >= 0f && animator != null && !anySwing)
                {
                    anySwing = true;
                    animator.AttackT = w.SwingT;
                    animator.AttackDirection = w.AimDir;
                }
            }
            if (!anySwing && animator != null) animator.AttackT = -1f;
            if (TemporaryLeft > 0f)
            {
                TemporaryLeft = Mathf.Max(0f, TemporaryLeft - dt);
                if (TemporaryLeft > 0f) TickWeapon(_loan, em, dt);
            }
        }

        void TickWeapon(WeaponInstance w, EnemyManager em, float dt)
        {
            var d = w.Def;
            if (d.pattern == WeaponPattern.Orbit) { TickOrbit(w, em, dt); return; }
            w.Cooldown -= dt;
            if (w.SwingT >= 0f)
            {
                w.SwingT += dt / Mathf.Max(0.01f, d.swingDuration);
                if (!w.Impacted && w.SwingT * d.swingDuration >= d.windup) { Impact(w, em); w.Impacted = true; }
                if (w.SwingT >= 1f)
                {
                    w.SwingT = -1f;
                    // Arc/Thrust Amount: the extra attacks follow right away at whoever is nearest now
                    if (w.BurstLeft > 0 && em != null) { w.BurstLeft--; StartSwing(w, em); }
                }
                return;
            }
            if (w.Cooldown > 0f || em == null) return;
            if (!StartSwing(w, em)) return;
            w.Cooldown = CooldownOf(w);
            bool melee = d.pattern == WeaponPattern.MeleeArc || d.pattern == WeaponPattern.Thrust;
            w.BurstLeft = melee ? AmountOf(w) - 1 : 0;
        }

        bool StartSwing(WeaponInstance w, EnemyManager em)
        {
            var d = w.Def;
            Vector3 origin = transform.position;
            if (!em.TryGetNearest(origin, Acquire(w), out int target)) return false;
            Vector3 aim = em.GetPosition(target) - origin;
            aim.y = 0f;
            w.AimDir = aim.sqrMagnitude > 1e-4f ? aim.normalized : transform.forward;
            w.SwingT = 0f;
            w.Impacted = false;
            Swings++;
            float area = AreaOf(w);
            if (d.pattern == WeaponPattern.MeleeArc)
            {
                if (slashFx != null) slashFx.Play(origin + Vector3.up * 0.75f, w.AimDir, d.range * area, d.arcDegrees, d.swingDuration);
                SfxPlayer.Play(SfxId.Swing, origin);
            }
            else if (d.pattern == WeaponPattern.Thrust)
            {
                var pp = PlayerProjectiles.Instance;
                if (pp != null) pp.Thrust(w.AimDir, d.range * area, d.swingDuration, MeshOf(w, pp), d.meshScale * Mathf.Sqrt(area));
                SfxPlayer.Play(SfxId.Thrust, origin);
            }
            return true;
        }

        int MeshOf(WeaponInstance w, PlayerProjectiles pp)
        {
            if (w.Mesh == -2) w.Mesh = pp.RegisterMesh(w.Def.mesh);
            return w.Mesh;
        }

        void Impact(WeaponInstance w, EnemyManager em)
        {
            if (em == null) return;
            switch (w.Def.pattern)
            {
                case WeaponPattern.MeleeArc: ArcHit(w, em); break;
                case WeaponPattern.Thrust: ThrustHit(w, em); break;
                case WeaponPattern.Projectile: Volley(w, em, returning: false); break;
                case WeaponPattern.Returning: Volley(w, em, returning: true); break;
                case WeaponPattern.Lob: Lob(w, em); break;
            }
        }

        DamageInfo Hit(WeaponInstance w, EnemyManager em, int i, Vector3 dir, DamageFlags kind)
        {
            bool crit = GameRandom.Chance(Crit(w));
            return new DamageInfo
            {
                Amount = Damage(w) * (crit ? CritMul(w) : 1f),
                Flags = kind | (crit ? DamageFlags.Crit : 0),
                Point = em.GetPosition(i),
                Direction = dir,
                Knockback = Knock(w),
                SourceId = w.Def.RuntimeId,
            };
        }

        void ArcHit(WeaponInstance w, EnemyManager em)
        {
            var d = w.Def;
            Vector3 origin = transform.position;
            float range = d.range * AreaOf(w);
            float halfArc = d.arcDegrees * 0.5f;
            int n = em.QueryAlive(origin, range, _hits);
            for (int k = 0; k < n; k++)
            {
                int i = _hits[k];
                Vector3 to = em.GetPosition(i) - origin;
                to.y = 0f;
                float dist = to.magnitude;
                if (dist > 0.7f && Vector3.Angle(w.AimDir, to) > halfArc) continue; // point-blank always connects
                em.ApplyDamage(i, Hit(w, em, i, dist > 1e-3f ? to / dist : w.AimDir, DamageFlags.Melee));
                Hits++;
            }
        }

        /// <summary>Everything in a line from the player along the aim: length = range × area, width = width × √area.</summary>
        void ThrustHit(WeaponInstance w, EnemyManager em)
        {
            var d = w.Def;
            float area = AreaOf(w);
            float length = d.range * area, half = d.width * 0.5f * Mathf.Sqrt(area);
            Vector3 origin = transform.position;
            Vector3 aim = w.AimDir;
            int n = em.QueryAlive(origin + aim * (length * 0.5f), length * 0.5f + half + 1f, _hits);
            for (int k = 0; k < n; k++)
            {
                int i = _hits[k];
                Vector3 to = em.GetPosition(i) - origin;
                to.y = 0f;
                float r = em.GetRadius(i);
                float along = Vector3.Dot(to, aim);
                if (along < -0.4f || along > length + r) continue;
                float side = (to - aim * along).magnitude;
                if (side > half + r) continue;
                em.ApplyDamage(i, Hit(w, em, i, aim, DamageFlags.Melee));
                Hits++;
            }
        }

        /// <summary>Throws AmountOf(w) projectiles at the nearest distinct enemies; extra shots fan out around them.</summary>
        void Volley(WeaponInstance w, EnemyManager em, bool returning)
        {
            var pp = PlayerProjectiles.Instance;
            if (pp == null) return;
            var d = w.Def;
            Vector3 origin = transform.position;
            int amount = Mathf.Min(AmountOf(w), _targets.Length);
            int found = NearestTargets(em, origin, Acquire(w), amount);
            float area = AreaOf(w);
            var shot = new ShotParams
            {
                Speed = SpeedOf(w),
                Range = returning ? d.range * area : d.range,
                Radius = d.width * (returning ? Mathf.Sqrt(area) : area),
                Damage = Damage(w),
                CritChance = Crit(w),
                CritMultiplier = CritMul(w),
                Knockback = Knock(w),
                Scale = d.meshScale * (returning ? Mathf.Sqrt(area) : 1f),
                Pierce = PierceOf(w),
                SourceId = d.RuntimeId,
                Mesh = MeshOf(w, pp),
            };
            Vector3 from = origin + Vector3.up * pp.flightHeight;
            SfxPlayer.Play(SfxId.Throw, origin);
            for (int k = 0; k < amount; k++)
            {
                Vector3 dir = w.AimDir;
                if (found > 0)
                {
                    Vector3 to = em.GetPosition(_targets[k % found]) - origin;
                    to.y = 0f;
                    if (to.sqrMagnitude > 1e-4f) dir = to.normalized;
                }
                if (found == 0 || k >= found)
                {
                    int extra = found == 0 ? k : k - found + 1;
                    float ang = volleySpread * ((extra + 1) / 2) * (extra % 2 == 0 ? 1f : -1f);
                    dir = Quaternion.Euler(0f, ang, 0f) * dir;
                }
                if (returning) pp.FireReturning(from, dir, shot);
                else pp.FireStraight(from, dir, shot);
            }
        }

        /// <summary>
        /// AmountOf(w) bombs at the densest groups in reach: of the nearest `lobCandidates` enemies, the one whose blast
        /// (width × area) would catch the most comes first, then the best ones outside the blasts already chosen; extra
        /// bombs land around the best spot. Flight and fuse shrink with Projectile Speed. Bombs never hurt the player.
        /// </summary>
        void Lob(WeaponInstance w, EnemyManager em)
        {
            var pp = PlayerProjectiles.Instance;
            if (pp == null) return;
            var d = w.Def;
            Vector3 origin = transform.position;
            float blast = d.width * AreaOf(w);
            int amount = Mathf.Min(AmountOf(w), _lobPick.Length);
            int found = NearestTargets(em, origin, Acquire(w), Mathf.Clamp(lobCandidates, 1, _targets.Length));
            if (found == 0) return;
            int picked = 0;
            for (int b = 0; b < amount && picked < found; b++)
            {
                int best = -1, bestScore = -1;
                for (int c = 0; c < found; c++)
                {
                    Vector3 at = em.GetPosition(_targets[c]);
                    bool covered = false;
                    for (int p = 0; p < picked && !covered; p++)
                    {
                        Vector3 o = em.GetPosition(_lobPick[p]);
                        covered = (o.x - at.x) * (o.x - at.x) + (o.z - at.z) * (o.z - at.z) < blast * blast;
                    }
                    if (covered) continue;
                    int score = em.QueryAlive(at, blast, _lobQuery);
                    if (score > bestScore) { bestScore = score; best = _targets[c]; }
                }
                if (best < 0) break;
                _lobPick[picked++] = best;
            }
            float ps = Mathf.Max(0.1f, w.Local[StatId.ProjectileSpeed] * PlayerStat(StatId.ProjectileSpeed, 1f));
            var shot = new ShotParams
            {
                Radius = blast,
                Damage = Damage(w),
                CritChance = Crit(w),
                CritMultiplier = CritMul(w),
                Knockback = Knock(w),
                Scale = d.meshScale,
                SourceId = d.RuntimeId,
                Mesh = MeshOf(w, pp),
                Flight = d.flightTime / ps,
                Fuse = d.fuse / ps,
                Arc = d.arcHeight,
            };
            Vector3 from = origin + Vector3.up * pp.flightHeight;
            SfxPlayer.Play(SfxId.Throw, origin);
            for (int k = 0; k < amount; k++)
            {
                Vector3 to = em.GetPosition(_lobPick[k % picked]);
                if (k >= picked) // more bombs than groups: scatter around the best spot
                {
                    float a = k * 2.39996f; // golden angle
                    to += new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (blast * 0.8f);
                    if (em.Height != null) to.y = em.Height.Sample(to.x, to.z);
                }
                pp.FireLob(from, to, shot);
            }
        }

        /// <summary>
        /// AmountOf(w) balls evenly spaced on a circle of radius range × area around the player, moving at SpeedOf(w) m/s
        /// along it. Each ball hits enemies its circle (width × √area) touches; an enemy is hit at most once per
        /// CooldownOf(w) by this weapon, and knocked outward.
        /// </summary>
        void TickOrbit(WeaponInstance w, EnemyManager em, float dt)
        {
            var d = w.Def;
            float area = AreaOf(w);
            float radius = Mathf.Max(0.5f, d.range * area);
            float hitR = d.width * Mathf.Sqrt(area);
            int n = Mathf.Min(AmountOf(w), MaxOrbiters);
            w.Clock += dt;
            w.Phase = Mathf.Repeat(w.Phase + SpeedOf(w) / radius * dt, Mathf.PI * 2f);
            if (em != null && (w.NextHit == null || w.NextHit.Length < em.capacity)) w.NextHit = new float[em.capacity];
            var pp = PlayerProjectiles.Instance;
            int mesh = pp != null ? MeshOf(w, pp) : -1;
            float interval = CooldownOf(w), scale = d.meshScale * Mathf.Sqrt(area);
            Vector3 centre = transform.position + Vector3.up * orbitHeight;
            for (int b = 0; b < n; b++)
            {
                float a = w.Phase + b * (Mathf.PI * 2f / n);
                var outward = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Vector3 pos = centre + outward * radius;
                if (em != null)
                {
                    int k = em.QueryAlive(pos, hitR, _hits);
                    for (int j = 0; j < k; j++)
                    {
                        int i = _hits[j];
                        if (w.NextHit[i] > w.Clock) continue;
                        w.NextHit[i] = w.Clock + interval;
                        em.ApplyDamage(i, Hit(w, em, i, outward, DamageFlags.Melee));
                        Hits++;
                    }
                }
                if (pp != null)
                {
                    var rot = Quaternion.LookRotation(outward, Vector3.up) * Quaternion.Euler(0f, 0f, w.Clock * orbitRoll);
                    pp.Orbiter(mesh, pos, rot, scale, new Vector3(-outward.z, 0f, outward.x));
                }
            }
        }

        /// <summary>Fills _targets with up to `max` nearest alive enemies within `range` (partial selection, no allocation).</summary>
        int NearestTargets(EnemyManager em, Vector3 origin, float range, int max)
        {
            int n = em.QueryAlive(origin, range, _hits);
            int found = 0;
            for (int pick = 0; pick < max && pick < n; pick++)
            {
                int best = -1;
                float bestD = float.MaxValue;
                for (int k = pick; k < n; k++)
                {
                    Vector3 to = em.GetPosition(_hits[k]) - origin;
                    float dd = to.x * to.x + to.z * to.z;
                    if (dd < bestD) { bestD = dd; best = k; }
                }
                if (best < 0) break;
                (_hits[pick], _hits[best]) = (_hits[best], _hits[pick]);
                _targets[found++] = _hits[pick];
            }
            return found;
        }
    }
}
