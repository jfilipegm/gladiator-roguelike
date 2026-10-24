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
        /// <summary>Echo (evolutions): seconds until the second strike (-1 = none pending), its direction, and the size boost while it runs.</summary>
        public float EchoT = -1f;
        public Vector3 EchoDir;
        public float AreaBoost = 1f;
        public bool InEcho;
        /// <summary>Chain: the enemy the swing was aimed at (slot + uid, so a recycled slot isn't mistaken for it).</summary>
        public int Target = -1, TargetUid;
        /// <summary>P1: game seconds since the player went down (an Orbit weapon's balls fall to the sand).</summary>
        public float DownT;
        /// <summary>Sweep (R3): seconds into the current sweep (-1 = none); its start, end and last-stepped bearings
        /// (degrees relative to AimDir, positive to the right); +1 = right to left (the first one), flipped after every
        /// sweep; backswings still to come; whether this one is a whirl (360°); its serial number.</summary>
        public float SweepT = -1f, SweepFrom, SweepTo, SweepPrev;
        public int SweepDir = 1, SweepLeft, SweepId;
        public bool SweepWhirl;
        /// <summary>Sweep: the arm's swing is mirrored (a right-to-left sweep is the profile's swing; a left-to-right one
        /// mirrors it). Set at the swing's start and at each sweep, kept until the swing ends (no snap at its tail).</summary>
        public bool SweepMirror;
        /// <summary>Sweep: per enemy slot, the last SweepId that hit it (allocated once, at the pool's capacity).</summary>
        public int[] SweepSeen;
        /// <summary>Sweep: the link mesh's draw index (-2 = not registered yet), the model part it swings and whether
        /// that was looked up.</summary>
        public int LinkMesh = -2;
        public Transform Part;
        public bool PartLooked;

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
            EchoT = -1f;
            AreaBoost = 1f;
            InEcho = false;
            DownT = 0f;
            SweepT = -1f;
            SweepFrom = SweepTo = SweepPrev = 0f;
            SweepDir = 1;
            SweepLeft = SweepId = 0;
            SweepWhirl = SweepMirror = false;
            if (SweepSeen != null) System.Array.Clear(SweepSeen, 0, SweepSeen.Length);
            LinkMesh = -2;
            if (Part != null) Part.gameObject.SetActive(true); // a slot reused mid-sweep: the model's part comes back
            Part = null;
            PartLooked = false;
        }
    }

    /// <summary>
    /// Fires the player's weapons automatically (CLAUDE.md §5): nearest-enemy auto-aim, central tick, hits via the
    /// enemy spatial hash (never Physics.Overlap), damage through EnemyManager.ApplyDamage. Up to 4 slots.
    /// Every weapon runs the same timeline (acquire → wind-up → impact → cooldown); the impact is the pattern:
    /// MeleeArc (arc hit around the aim), Thrust (line hit + spear visual), MultiHit (the trident's lunge, note 34: its
    /// prongs strike parallel lanes at once), Projectile (javelin volley at the nearest enemies), Returning (chakram out
    /// and back), Lob (bombs at the densest groups in reach, exploding after a fuse), Fireball (the Ashen Censer's
    /// cursed fireballs, note 33: homing on the nearest, bursting on the first enemy they touch),
    /// Sweep (the Labyrinth Chain, R3: the impact starts a sweep that carries the chain across its arc over sweepTime,
    /// hitting each enemy it passes once; Amount = backswings straight after, the other way; no new swing until the
    /// last one ends). Orbit (Gorm's Flail) has no timeline: its balls always circle the player and hit whatever they
    /// pass, each enemy at most once per cooldown. Numbers = weapon base × its level modifiers × player stats.
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
        [Header("GroundSlam (the Executioner's Cleaver)")]
        [Tooltip("Enemies this close to the player are hit by a slam wherever it lands: they're under the blade (m).")]
        public float slamPointBlank = 1.2f;
        public float slamShake = 0.08f;
        [Tooltip("Chain: height of the spear's tip the lightning leaves from (m).")]
        public float chainTipHeight = 1.7f;
        public Color slamDustA = new Color(0.78f, 0.68f, 0.5f), slamDustB = new Color(0.82f, 0.97f, 1f);
        [Header("Sweep (the Labyrinth Chain)")]
        [Tooltip("Sweep: the hand's distance from the player's centre along the chain (m).")]
        public float chainHandOffset = 0.45f;
        [Tooltip("Sweep: the stone's tumble about the chain's line while it swings (deg/s), for show.")]
        public float stoneTumble = 360f;
        const int MaxOrbiters = 8;

        PlayerStats _stats;
        PlayerHealth _health;

        /// <summary>P1 (playtest): the player is dead or the run is over. Nothing swings, fires or bites any more.</summary>
        public bool Halted => (_health != null && _health.IsDead) || RunState.CombatOver;

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
        readonly int[] _sweepHits = new int[512]; // a 7 m disc can hold more than 256 at 450 alive
        [Tooltip("Lob: nearest enemies considered as bomb targets (the one whose blast would catch the most wins).")]
        public int lobCandidates = 8;

        public IReadOnlyList<WeaponInstance> Weapons => _weapons;
        public int Swings { get; private set; }
        public int Hits { get; private set; }
        /// <summary>Echo strikes fired (evolutions, tests).</summary>
        public int Echoes { get; private set; }
        /// <summary>Thrust hits that landed on a weapon's tip (the Whip's crack, always crits; tests).</summary>
        public int TipHits { get; private set; }
        /// <summary>MultiHit prongs thrust (the Trident: its prong count every lunge; tests, bot log).</summary>
        public int Prongs { get; private set; }
        /// <summary>Fireballs cast (the Ashen Censer's; tests, bot log).</summary>
        public int FireballsCast { get; private set; }
        /// <summary>GroundSlam impacts (tests).</summary>
        public int Slams { get; private set; }
        /// <summary>Enemies struck by Chain lightning, the first strikes and the jumps (tests, bot log).</summary>
        public int ChainStrikes { get; private set; }
        /// <summary>R3: every sweep is a whirl (360°, at sweepTime × whirlTimeMul) while on. Asterion's Rage sets it while
        /// Unchained; only Sweep weapons read it, so the chain in anyone else's hands never whirls.</summary>
        public bool WhirlSweeps { get; set; }
        /// <summary>Sweep: every sweep, backswings included; whirls among them; enemies hit; stone hits (tests, bot log).</summary>
        public int Sweeps { get; private set; }
        public int Whirls { get; private set; }
        public int SweepHits { get; private set; }
        public int WeightHits { get; private set; }
        /// <summary>The crowd's weapon in the sand (Phase 5.1): fights beside the slots for a while (null = none).</summary>
        public WeaponInstance Temporary => TemporaryLeft > 0f ? _loan : null;
        public float TemporaryLeft { get; private set; }
        public float TemporaryDuration { get; private set; }

        void Awake()
        {
            _stats = GetComponent<PlayerStats>();
            _health = GetComponent<PlayerHealth>();
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

        /// <summary>Drops every weapon (tests, dev showcase, a future gladiator swap). A model part a sweep hid comes back.</summary>
        public void RemoveAll()
        {
            for (int i = 0; i < _weapons.Count; i++) ShowPart(_weapons[i]);
            _weapons.Clear();
        }

        public WeaponInstance Find(WeaponDef def)
        {
            for (int i = 0; i < _weapons.Count; i++) if (_weapons[i].Def == def) return _weapons[i];
            return null;
        }

        /// <summary>
        /// Phase 5.5: the weapon `from` becomes its evolution `to` in the same slot. It keeps its level and level modifiers
        /// (the level-8 numbers); `to` adds its own base numbers and twists. Null if `from` isn't equipped.
        /// </summary>
        public WeaponInstance Evolve(WeaponDef from, WeaponDef to)
        {
            var w = Find(from);
            if (w == null || to == null) return null;
            w.Def = to;
            w.Mesh = w.LinkMesh = -2;
            w.EchoT = -1f;
            return w;
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
        public void EndTemporary()
        {
            TemporaryLeft = 0f;
            StopSweep(_loan);
        }

        // ---------- effective numbers ----------
        public float Damage(WeaponInstance w) => w.Def.damage * w.Local[StatId.Damage] * DamageMul;
        public float CooldownOf(WeaponInstance w) => w.Def.cooldown / Mathf.Max(0.1f, w.Local[StatId.AttackSpeed] * AttackSpeedMul);
        public float AreaOf(WeaponInstance w) => w.Local[StatId.Area] * RangeMul * w.AreaBoost;
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
            if (Halted) { Hold(em, dt); return; }
            bool anySwing = false;
            for (int i = 0; i < _weapons.Count; i++)
            {
                var w = _weapons[i];
                TickWeapon(w, em, dt);
                if ((w.SwingT >= 0f || w.SweepT >= 0f) && animator != null && !anySwing)
                {
                    anySwing = true;
                    Animate(w);
                }
            }
            if (!anySwing && animator != null)
            {
                animator.AttackT = -1f;
                animator.AttackMirror = false;
                animator.AttackSpin = 0f;
            }
            if (TemporaryLeft > 0f)
            {
                TemporaryLeft = Mathf.Max(0f, TemporaryLeft - dt);
                if (TemporaryLeft > 0f) TickWeapon(_loan, em, dt);
                else StopSweep(_loan);
            }
        }

        /// <summary>
        /// P1: the player went down (or the run is over) and every weapon stops at once: the swing in progress never
        /// lands, the jabs, echoes and extra strikes still to come are dropped, a flail's balls fall to the sand where
        /// they were, and the companions stand where they are. Nothing hits any more.
        /// </summary>
        void Hold(EnemyManager em, float dt)
        {
            if (animator != null)
            {
                animator.AttackT = -1f;
                animator.AttackMirror = false;
                animator.AttackSpin = 0f;
            }
            for (int i = 0; i < _weapons.Count; i++) HoldWeapon(_weapons[i], em, dt);
            if (TemporaryLeft > 0f) HoldWeapon(_loan, em, dt);
        }

        void HoldWeapon(WeaponInstance w, EnemyManager em, float dt)
        {
            w.SwingT = -1f;
            w.EchoT = -1f;
            w.BurstLeft = 0;
            w.DownT += dt;
            StopSweep(w); // a chain stops mid-sweep: no more hits, the model's chain is back
            var d = w.Def;
            if (d.pattern == WeaponPattern.Orbit) DropOrbit(w, em);
            else if (d.pattern == WeaponPattern.Summon && _pack != null) _pack.Hold(dt);
        }

        /// <summary>A halted flail: its balls stay at the angle they stopped at and drop to the ground under them.</summary>
        void DropOrbit(WeaponInstance w, EnemyManager em)
        {
            var pp = PlayerProjectiles.Instance;
            if (pp == null) return;
            var d = w.Def;
            float area = AreaOf(w);
            float radius = Mathf.Max(0.5f, d.range * area), scale = d.meshScale * Mathf.Sqrt(area);
            int n = Mathf.Min(AmountOf(w), MaxOrbiters), mesh = MeshOf(w, pp);
            float drop = 0.5f * 9.81f * w.DownT * w.DownT;
            Vector3 centre = transform.position + Vector3.up * orbitHeight;
            for (int b = 0; b < n; b++)
            {
                float a = w.Phase + b * (Mathf.PI * 2f / n);
                var outward = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Vector3 pos = centre + outward * radius;
                float ground = em != null && em.Height != null ? em.Height.Sample(pos.x, pos.z) : transform.position.y;
                pos.y = Mathf.Max(ground + 0.15f, pos.y - drop);
                var rot = Quaternion.LookRotation(outward, Vector3.up) * Quaternion.Euler(0f, 0f, w.Clock * orbitRoll);
                pp.Orbiter(mesh, pos, rot, scale, Vector3.zero);
            }
        }

        void TickWeapon(WeaponInstance w, EnemyManager em, float dt)
        {
            var d = w.Def;
            if (d.pattern == WeaponPattern.Orbit) { TickOrbit(w, em, dt); return; }
            if (d.pattern == WeaponPattern.Summon) { TickSummon(w, em, dt); return; }
            if (w.EchoT >= 0f)
            {
                w.EchoT -= dt;
                if (w.EchoT < 0f && em != null) Echo(w, em);
            }
            w.Cooldown -= dt;
            if (w.SweepT >= 0f && em != null) StepSweep(w, em, dt);
            if (w.SwingT >= 0f)
            {
                w.SwingT += dt / Mathf.Max(0.01f, d.swingDuration);
                if (!w.Impacted && w.SwingT * d.swingDuration >= d.windup) { Impact(w, em); w.Impacted = true; }
                if (w.SwingT >= 1f)
                {
                    w.SwingT = -1f;
                    // Arc/Thrust Amount: the extra attacks follow right away at whoever is nearest now (or the next-nearest: spreadStrikes)
                    if (w.BurstLeft > 0 && em != null) { w.BurstLeft--; StartSwing(w, em, AmountOf(w) - 1 - w.BurstLeft); }
                }
                return;
            }
            if (w.SweepT >= 0f) return; // Sweep: no new swing while the chain is still going round (the cooldown counts on)
            if (w.Cooldown > 0f || em == null) return;
            if (!StartSwing(w, em)) return;
            w.Cooldown = CooldownOf(w);
            bool melee = d.pattern == WeaponPattern.MeleeArc || d.pattern == WeaponPattern.Thrust || d.pattern == WeaponPattern.GroundSlam
                         || d.pattern == WeaponPattern.Chain || d.pattern == WeaponPattern.MultiHit;
            w.BurstLeft = melee ? AmountOf(w) - 1 : 0;
        }

        bool StartSwing(WeaponInstance w, EnemyManager em, int strike = 0)
        {
            var d = w.Def;
            Vector3 origin = transform.position;
            if (!em.TryGetNearest(origin, Acquire(w), out int target)) return false;
            if (d.spreadStrikes && strike > 0)
            {
                int found = PickTargets(em, origin, Acquire(w), strike + 1, farthest: false);
                if (found > 0) target = _targets[Mathf.Min(strike, found - 1)];
            }
            Vector3 aim = em.GetPosition(target) - origin;
            aim.y = 0f;
            w.AimDir = aim.sqrMagnitude > 1e-4f ? aim.normalized : transform.forward;
            w.Target = target;
            w.TargetUid = em.GetUid(target);
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
            else if (d.pattern == WeaponPattern.MultiHit)
            {
                // the trident's lunge (note 34): drawn back, driven out at the wind-up (when LungeHit strikes), pulled back
                var pp = PlayerProjectiles.Instance;
                float root = Mathf.Sqrt(area), dur = Mathf.Max(0.05f, d.swingDuration);
                if (pp != null) pp.Lunge(w.AimDir, d.range * area, dur, d.windup / dur, Mathf.Max(1, d.prongs), d.prongSpacing * root,
                    MeshOf(w, pp), d.meshScale * root);
                SfxPlayer.Play(SfxId.Thrust, origin);
            }
            else if (d.pattern == WeaponPattern.GroundSlam) SfxPlayer.Play(SfxId.Swing, origin); // raised over the head
            else if (d.pattern == WeaponPattern.Sweep)
            {
                w.SweepMirror = w.SweepDir < 0; // the pull goes to the side the sweep starts from
                SfxPlayer.Play(SfxId.Swing, origin);
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
                case WeaponPattern.GroundSlam: SlamHit(w, em); break;
                case WeaponPattern.Chain: ChainHit(w, em); break;
                case WeaponPattern.MultiHit: LungeHit(w, em); break;
                case WeaponPattern.Fireball: CastFireballs(w, em); break;
                case WeaponPattern.Sweep: w.SweepLeft = AmountOf(w) - 1; BeginSweep(w); break;
            }
            var d = w.Def;
            bool echoes = d.pattern == WeaponPattern.MeleeArc || d.pattern == WeaponPattern.Thrust || d.pattern == WeaponPattern.Returning;
            if (d.echoDelay > 0f && echoes)
            {
                w.EchoT = d.echoDelay;
                w.EchoDir = Quaternion.Euler(0f, d.echoAngle, 0f) * w.AimDir;
            }
        }

        /// <summary>An evolved weapon's second strike: the same impact turned by echoAngle, at echoScale size.</summary>
        void Echo(WeaponInstance w, EnemyManager em)
        {
            var d = w.Def;
            Vector3 aim = w.AimDir, origin = transform.position;
            w.AimDir = w.EchoDir;
            w.AreaBoost = Mathf.Max(0.1f, d.echoScale);
            w.InEcho = true;
            float area = AreaOf(w);
            switch (d.pattern)
            {
                case WeaponPattern.MeleeArc:
                    if (slashFx != null) slashFx.Play(origin + Vector3.up * 0.75f, w.AimDir, d.range * area, d.arcDegrees, d.swingDuration);
                    SfxPlayer.Play(SfxId.Swing, origin);
                    ArcHit(w, em);
                    break;
                case WeaponPattern.Thrust:
                    var pp = PlayerProjectiles.Instance;
                    if (pp != null) pp.Thrust(w.AimDir, d.range * area, d.swingDuration, MeshOf(w, pp), d.meshScale * Mathf.Sqrt(area));
                    SfxPlayer.Play(SfxId.Thrust, origin);
                    ThrustHit(w, em);
                    break;
                case WeaponPattern.Returning:
                    Volley(w, em, returning: true, turn: d.echoAngle);
                    break;
            }
            Echoes++;
            w.InEcho = false;
            w.AreaBoost = 1f;
            w.AimDir = aim;
        }

        DamageInfo Hit(WeaponInstance w, EnemyManager em, int i, Vector3 dir, DamageFlags kind, bool forceCrit = false)
        {
            bool crit = forceCrit || GameRandom.Chance(RngStream.Combat, Crit(w));
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

        /// <summary>A melee hit: damage, then the weapon's on-hit status if the enemy survived it.</summary>
        void Strike(WeaponInstance w, EnemyManager em, int i, in DamageInfo info)
        {
            int uid = em.GetUid(i);
            em.ApplyDamage(i, info);
            Hits++;
            var d = w.Def;
            if (d.onHitStatus != null && em.IsAlive(i) && em.GetUid(i) == uid)
                em.ApplyStatus(i, d.onHitStatus, info.Amount * d.onHitStatusPower, d.onHitStacks);
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
                Strike(w, em, i, Hit(w, em, i, dist > 1e-3f ? to / dist : w.AimDir, DamageFlags.Melee));
            }
        }

        /// <summary>Where a GroundSlam lands: `range` ahead along the aim, on the ground.</summary>
        public Vector3 SlamPoint(WeaponInstance w)
        {
            Vector3 at = transform.position + w.AimDir * w.Def.range;
            var em = EnemyManager.Instance;
            if (em != null && em.Height != null) at.y = em.Height.Sample(at.x, at.z);
            return at;
        }

        /// <summary>
        /// The cleaver comes down `range` ahead: everything within width × √area of that point is hit and thrown out
        /// from it; anything within slamPointBlank of the player is hit too (it's under the blade). A pale ring runs out
        /// across the ground.
        /// </summary>
        void SlamHit(WeaponInstance w, EnemyManager em)
        {
            var d = w.Def;
            float radius = d.width * Mathf.Sqrt(AreaOf(w));
            Vector3 origin = transform.position;
            Vector3 at = SlamPoint(w);
            int n = em.QueryAlive(at, radius + slamPointBlank + d.range + em.MaxRadius, _hits);
            for (int k = 0; k < n; k++)
            {
                int i = _hits[k];
                Vector3 ep = em.GetPosition(i);
                Vector3 to = ep - at, near = ep - origin;
                to.y = 0f; near.y = 0f;
                float rr = radius + em.GetRadius(i);
                bool inCircle = to.sqrMagnitude <= rr * rr;
                if (!inCircle && near.sqrMagnitude > slamPointBlank * slamPointBlank) continue;
                Vector3 dir = inCircle ? to : near;
                dir = dir.sqrMagnitude > 1e-4f ? dir.normalized : w.AimDir;
                Strike(w, em, i, Hit(w, em, i, dir, DamageFlags.Melee | DamageFlags.Area));
            }
            Slams++;
            var pp = PlayerProjectiles.Instance;
            if (pp != null)
            {
                pp.Shockwave(at, radius * 1.15f);
                if (pp.burst != null) pp.burst.Burst(at + Vector3.up * 0.15f, 12, 4.5f, 0.16f, slamDustA, slamDustB);
            }
            SfxPlayer.Play(SfxId.Slam, at);
            ScreenShake.Add(slamShake);
        }

        /// <summary>
        /// Lightning from the spear's tip: the enemy the swing aimed at (or the nearest in reach), then Pierce jumps, each
        /// to the nearest enemy not struck yet within width × √area of the last one. Every strike is a melee-style Strike
        /// (on-hit status, Boon procs); a Bolt marks every leg.
        /// </summary>
        void ChainHit(WeaponInstance w, EnemyManager em)
        {
            var d = w.Def;
            Vector3 origin = transform.position;
            int cur = w.Target;
            if (cur < 0 || !em.IsAlive(cur) || em.GetUid(cur) != w.TargetUid)
                if (!em.TryGetNearest(origin, Acquire(w), out cur)) return;
            float jump = d.width * Mathf.Sqrt(AreaOf(w));
            int jumps = Mathf.Clamp(PierceOf(w), 0, _targets.Length - 1);
            var pp = PlayerProjectiles.Instance;
            Vector3 from = origin + Vector3.up * chainTipHeight;
            int struck = 0;
            for (int j = 0; j <= jumps && cur >= 0; j++)
            {
                Vector3 to = em.GetPosition(cur) + Vector3.up * 0.9f;
                if (pp != null) pp.Bolt(from, to);
                _targets[struck++] = cur;
                Vector3 dir = to - from;
                dir.y = 0f;
                Strike(w, em, cur, Hit(w, em, cur, dir.sqrMagnitude > 1e-4f ? dir.normalized : w.AimDir, DamageFlags.Projectile));
                ChainStrikes++;
                from = to;
                int n = em.QueryAlive(to, jump + em.MaxRadius, _hits);
                int best = -1;
                float bestD = float.MaxValue;
                for (int k = 0; k < n; k++)
                {
                    int c = _hits[k];
                    bool seen = false;
                    for (int s = 0; s < struck && !seen; s++) seen = _targets[s] == c;
                    if (seen) continue;
                    Vector3 off = em.GetPosition(c) - to;
                    off.y = 0f;
                    float reach = jump + em.GetRadius(c), d2 = off.sqrMagnitude;
                    if (d2 <= reach * reach && d2 < bestD) { bestD = d2; best = c; }
                }
                cur = best;
            }
            SfxPlayer.Play(SfxId.Spark, origin);
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
                bool tip = d.tipLength > 0f && along >= length - d.tipLength * Mathf.Sqrt(area); // the whip's crack
                if (tip) TipHits++;
                Strike(w, em, i, Hit(w, em, i, aim, DamageFlags.Melee, tip));
            }
        }

        /// <summary>
        /// MultiHit (the Trident, note 34): one lunge. Its `prongs` lanes run parallel along the aim from the player, the
        /// middle one through the target, prongSpacing × √area apart, each range × area long and width × √area wide; every
        /// lane an enemy's body touches strikes it once (a big one, or one between two prongs, takes two or three). The
        /// lanes flash white-cyan on the ground.
        /// </summary>
        void LungeHit(WeaponInstance w, EnemyManager em)
        {
            var d = w.Def;
            float area = AreaOf(w), root = Mathf.Sqrt(area);
            float length = d.range * area, half = d.width * 0.5f * root, spacing = d.prongSpacing * root;
            int prongs = Mathf.Max(1, d.prongs);
            float mid = (prongs - 1) * 0.5f, across = mid * spacing + half;
            Vector3 origin = transform.position, aim = w.AimDir;
            var right = new Vector3(aim.z, 0f, -aim.x);
            int n = em.QueryAlive(origin + aim * (length * 0.5f), Mathf.Sqrt(length * length * 0.25f + across * across) + 0.4f, _hits);
            for (int k = 0; k < n; k++)
            {
                int i = _hits[k];
                Vector3 to = em.GetPosition(i) - origin;
                to.y = 0f;
                float r = em.GetRadius(i);
                float along = Vector3.Dot(to, aim);
                if (along < -0.4f || along > length + r) continue;
                float side = Vector3.Dot(to, right);
                int uid = em.GetUid(i);
                for (int p = 0; p < prongs; p++)
                {
                    if (Mathf.Abs(side - (p - mid) * spacing) > half + r) continue;
                    if (!em.IsAlive(i) || em.GetUid(i) != uid) break; // an earlier prong killed it
                    Strike(w, em, i, Hit(w, em, i, aim, DamageFlags.Melee));
                }
            }
            Prongs += prongs;
            var pp = PlayerProjectiles.Instance;
            if (pp != null) pp.ProngLanes(origin, aim, length, Mathf.Max(0.25f, d.width * root * 1.3f), spacing, prongs);
        }

        /// <summary>
        /// Fireball (the Ashen Censer, note 33): AmountOf(w) cursed fireballs from the censer (castHeight) at the nearest
        /// distinct enemies; extra ones leave fanned out either side and curve back onto a target. Each steers `homing`
        /// deg/s toward its target and bursts on the first enemy it touches, hitting everything within width × area (the
        /// weapon's hit effects curse them and leave the cursed patch).
        /// </summary>
        void CastFireballs(WeaponInstance w, EnemyManager em)
        {
            var pp = PlayerProjectiles.Instance;
            if (pp == null) return;
            var d = w.Def;
            Vector3 origin = transform.position;
            int amount = Mathf.Min(AmountOf(w), _targets.Length);
            int found = PickTargets(em, origin, Acquire(w), amount, d.targetFarthest);
            float area = AreaOf(w);
            var shot = new ShotParams
            {
                Speed = SpeedOf(w),
                Range = d.range,
                Radius = d.width * area,
                Damage = Damage(w),
                CritChance = Crit(w),
                CritMultiplier = CritMul(w),
                Knockback = Knock(w),
                Scale = d.meshScale * Mathf.Sqrt(area),
                SourceId = d.RuntimeId,
                Mesh = MeshOf(w, pp),
                Homing = d.homing,
            };
            Vector3 from = origin + Vector3.up * (d.castHeight > 0f ? d.castHeight : pp.flightHeight) + w.AimDir * 0.5f;
            SfxPlayer.Play(SfxId.Throw, origin);
            if (pp.burst != null) pp.burst.Burst(from, 6, 1.8f, 0.14f, pp.fireballBurstA, pp.fireballSmoke); // the censer flares
            for (int k = 0; k < amount; k++)
            {
                int target = found > 0 ? _targets[k % found] : -1;
                Vector3 dir = w.AimDir;
                if (target >= 0)
                {
                    Vector3 to = em.GetPosition(target) + Vector3.up * pp.fireballAimHeight - from;
                    if (to.sqrMagnitude > 1e-4f) dir = to.normalized;
                }
                if (found == 0 || k >= found) // more fireballs than targets: they leave fanned out and curve in
                {
                    int extra = found == 0 ? k : k - found + 1;
                    float ang = volleySpread * 2f * ((extra + 1) / 2) * (extra % 2 == 0 ? 1f : -1f);
                    dir = Quaternion.Euler(0f, ang, 0f) * dir;
                }
                if (pp.FireFireball(from, dir, target, target >= 0 ? em.GetUid(target) : 0, shot)) FireballsCast++;
            }
        }

        // ---------- Sweep (the Labyrinth Chain, R3) ----------

        /// <summary>
        /// The animator follows the weapon that drives it. A sweep plays the sweep part of the profile's swing (windup to
        /// windup + sweepTime of swingDuration) once per sweep, backswings included, mirrored for a left-to-right one; a
        /// whirl turns the whole model with the chain. Every other weapon leaves the mirror and the spin off.
        /// </summary>
        void Animate(WeaponInstance w)
        {
            var d = w.Def;
            if (w.SweepT >= 0f)
            {
                float dur = Mathf.Max(0.01f, d.swingDuration);
                float w0 = d.windup / dur, w1 = (d.windup + d.sweepTime) / dur;
                animator.AttackT = w0 + (w1 - w0) * Mathf.Clamp01(w.SweepT / SweepTimeOf(w));
                animator.AttackSpin = w.SweepWhirl ? w.SweepPrev - w.SweepFrom : 0f;
            }
            else
            {
                animator.AttackT = w.SwingT;
                animator.AttackSpin = 0f;
            }
            animator.AttackMirror = d.pattern == WeaponPattern.Sweep && w.SweepMirror;
            animator.AttackDirection = w.AimDir;
        }

        float SweepTimeOf(WeaponInstance w) => Mathf.Max(0.01f, w.Def.sweepTime * (w.SweepWhirl ? Mathf.Max(0.01f, w.Def.whirlTimeMul) : 1f));

        /// <summary>
        /// A sweep starts (at the impact, and at once for each backswing): from one end of the arc to the other, the way
        /// SweepDir says (+1 = right to left); a whirl (WhirlSweeps) goes all the way round. The model's chain part is
        /// hidden while the swung one is drawn.
        /// </summary>
        void BeginSweep(WeaponInstance w)
        {
            w.SweepWhirl = WhirlSweeps;
            float half = w.SweepWhirl ? 180f : w.Def.arcDegrees * 0.5f;
            w.SweepFrom = half * w.SweepDir;
            w.SweepTo = -half * w.SweepDir;
            w.SweepPrev = w.SweepFrom;
            w.SweepT = 0f;
            w.SweepId++;
            w.SweepMirror = w.SweepDir < 0;
            HidePart(w);
            SfxPlayer.Play(SfxId.Rattle, transform.position);
            Sweeps++;
            if (w.SweepWhirl) Whirls++;
        }

        /// <summary>
        /// One frame of a sweep: the chain's bearing moves on (SweepFrom to SweepTo over the sweep's time), every enemy it
        /// passed since the last frame is hit (each once per sweep), and the chain is drawn where it is. At the end every
        /// sweep flips SweepDir; a backswing then starts at once (no wind-up), re-aimed at the nearest enemy (the aim kept
        /// if there's none), or the sweeps are over and the model's chain comes back.
        /// </summary>
        void StepSweep(WeaponInstance w, EnemyManager em, float dt)
        {
            w.SweepT += dt;
            float u = Mathf.Clamp01(w.SweepT / SweepTimeOf(w));
            float cur = Mathf.Lerp(w.SweepFrom, w.SweepTo, u);
            SweepStrikes(w, em, w.SweepPrev, cur);
            w.SweepPrev = cur;
            if (u < 1f) { DrawChain(w, cur); return; }
            w.SweepDir = -w.SweepDir; // the next sweep (a backswing, or the next trigger) comes back the other way
            if (w.SweepLeft > 0)
            {
                w.SweepLeft--;
                Vector3 origin = transform.position;
                if (em.TryGetNearest(origin, Acquire(w), out int target))
                {
                    Vector3 aim = em.GetPosition(target) - origin;
                    aim.y = 0f;
                    if (aim.sqrMagnitude > 1e-4f) w.AimDir = aim.normalized;
                }
                BeginSweep(w);
                DrawChain(w, w.SweepPrev);
                return;
            }
            w.SweepT = -1f;
            ShowPart(w); // not drawn this frame: the model's chain is back instead
        }

        /// <summary>Ends a sweep at once (halted, a loan running out): no more hits, no backswings, the model's chain back.</summary>
        static void StopSweep(WeaponInstance w)
        {
            w.SweepT = -1f;
            w.SweepLeft = 0;
            ShowPart(w);
        }

        /// <summary>
        /// Hits every enemy the chain passed between bearings a0 and a1 this frame and not yet this sweep: within the reach
        /// (+ its radius), its bearing inside [a0, a1] padded by the angle its body and half the chain's width span at that
        /// distance. The stone (the last tipLength × √Area m) always crits and shoves tipKnockbackMul harder. The push is
        /// the chain's tangent plus a little outward, so a shield facing the player misses it; a boss's push is radial
        /// (its shield blocks as before).
        /// </summary>
        void SweepStrikes(WeaponInstance w, EnemyManager em, float a0, float a1)
        {
            var d = w.Def;
            if (w.SweepSeen == null || w.SweepSeen.Length < em.capacity) w.SweepSeen = new int[em.capacity];
            float area = AreaOf(w);
            float reach = d.range * area, tip = d.tipLength * Mathf.Sqrt(area), halfWidth = d.width * 0.5f;
            float sign = a1 < a0 ? -1f : 1f;
            Vector3 origin = transform.position;
            int n = em.QueryAlive(origin, reach, _sweepHits);
            for (int k = 0; k < n; k++)
            {
                int i = _sweepHits[k];
                if (i >= w.SweepSeen.Length || w.SweepSeen[i] == w.SweepId) continue;
                Vector3 to = em.GetPosition(i) - origin;
                to.y = 0f;
                float dist = to.magnitude, r = em.GetRadius(i);
                if (dist > reach + r) continue;
                bool atCentre = dist < 1e-3f;
                Vector3 outward = atCentre ? Quaternion.Euler(0f, a1, 0f) * w.AimDir : to / dist;
                float bearing = atCentre ? a1 : SweepBearing(w.AimDir, to);
                float body = r + halfWidth;
                float pad = Mathf.Asin(Mathf.Min(1f, body / Mathf.Max(dist, body))) * Mathf.Rad2Deg;
                if (!SweepCovers(a0, a1, bearing, pad)) continue;
                w.SweepSeen[i] = w.SweepId;
                bool stone = tip > 0f && dist >= reach - tip;
                var def = em.GetDef(i);
                bool boss = def != null && def.behaviour == EnemyBehaviour.Scripted;
                var info = Hit(w, em, i, boss ? outward : SweepPush(outward, sign, d.sweepOutward), DamageFlags.Melee, forceCrit: stone);
                if (stone) info.Knockback *= d.tipKnockbackMul;
                Strike(w, em, i, info);
                SweepHits++;
                if (stone) WeightHits++;
            }
        }

        /// <summary>
        /// The swung chain at `bearing`: Link meshes laid end to end from the hand to weightOffset short of the stone, each
        /// rolled 90° from the last and stretched along Z to fit exactly, then the stone, tumbling about the chain's line,
        /// with a streak behind it the way it moves. Never a fan: the floor's telegraphs round him stay readable. A
        /// missing mesh draws nothing.
        /// </summary>
        void DrawChain(WeaponInstance w, float bearing)
        {
            var pp = PlayerProjectiles.Instance;
            if (pp == null) return;
            var d = w.Def;
            float area = AreaOf(w);
            Vector3 origin = transform.position, dir = Quaternion.Euler(0f, bearing, 0f) * w.AimDir;
            Vector3 hand = origin + Vector3.up * d.chainHeight.x + dir * chainHandOffset;
            Vector3 stone = origin + dir * (d.range * area) + Vector3.up * d.chainHeight.y;
            Vector3 line = stone - hand;
            float len = line.magnitude;
            if (len < 1e-3f) return;
            Vector3 along = line / len;
            var look = Quaternion.LookRotation(along, Vector3.up);
            if (w.LinkMesh == -2) w.LinkMesh = pp.RegisterMesh(d.linkMesh);
            float run = len - d.weightOffset;
            if (w.LinkMesh >= 0 && run > 0f && d.linkLength > 1e-3f)
            {
                int links = Mathf.CeilToInt(run / d.linkLength);
                float each = run / links;
                var stretch = new Vector3(1f, 1f, each / d.linkLength);
                for (int k = 0; k < links; k++)
                    pp.Orbiter(w.LinkMesh, hand + along * (each * k), look * Quaternion.Euler(0f, 0f, (k & 1) * 90f), stretch, Vector3.zero);
            }
            int mesh = MeshOf(w, pp);
            if (mesh < 0) return;
            Vector3 travel = Quaternion.Euler(0f, w.SweepTo < w.SweepFrom ? -90f : 90f, 0f) * dir;
            pp.Orbiter(mesh, stone, look * Quaternion.Euler(0f, 0f, w.SweepT * stoneTumble), d.meshScale * Mathf.Sqrt(area), travel);
        }

        /// <summary>The model part this weapon swings (WeaponDef.modelPart, found once under the visual root) is hidden.
        /// Only for a starting weapon: the model carries its own weapon, and another gladiator who takes the card may
        /// have a part of the same name (the Ashen Oracle's censer has a "Chain").</summary>
        void HidePart(WeaponInstance w)
        {
            if (!w.PartLooked)
            {
                w.PartLooked = true;
                var root = animator != null ? animator.visualRoot : null;
                if (root != null && !string.IsNullOrEmpty(w.Def.modelPart) && IsStarting(w.Def))
                    w.Part = FindPart(root, w.Def.modelPart);
            }
            if (w.Part != null && w.Part.gameObject.activeSelf) w.Part.gameObject.SetActive(false);
        }

        bool IsStarting(WeaponDef def)
        {
            if (startingWeapons != null)
                for (int i = 0; i < startingWeapons.Length; i++)
                    if (startingWeapons[i] == def) return true;
            return false;
        }

        static void ShowPart(WeaponInstance w)
        {
            if (w.Part != null && !w.Part.gameObject.activeSelf) w.Part.gameObject.SetActive(true);
        }

        static Transform FindPart(Transform t, string name)
        {
            if (t.name == name) return t;
            for (int i = 0; i < t.childCount; i++)
            {
                var r = FindPart(t.GetChild(i), name);
                if (r != null) return r;
            }
            return null;
        }

        /// <summary>The bearing of `to` seen from `aim`, on the ground (degrees, −180..180, positive to the right).</summary>
        public static float SweepBearing(Vector3 aim, Vector3 to)
        {
            aim.y = 0f;
            to.y = 0f;
            return Vector3.SignedAngle(aim, to, Vector3.up);
        }

        /// <summary>True if `bearing` (or the same bearing a turn either way) lies between a0 and a1, in either order,
        /// widened by `pad` degrees on each side: a chain that moved from a0 to a1 passed it.</summary>
        public static bool SweepCovers(float a0, float a1, float bearing, float pad)
        {
            float lo = Mathf.Min(a0, a1) - pad, hi = Mathf.Max(a0, a1) + pad;
            return (bearing >= lo && bearing <= hi)
                   || (bearing - 360f >= lo && bearing - 360f <= hi)
                   || (bearing + 360f >= lo && bearing + 360f <= hi);
        }

        /// <summary>The way a point of the chain at `outward` moves while it turns toward `sign` (+1 = toward positive
        /// bearings, the right; −1 = the left), plus a little outward: normalize(tangent + outward × outwardMix).</summary>
        public static Vector3 SweepPush(Vector3 outward, float sign, float outwardMix)
        {
            Vector3 v = Quaternion.Euler(0f, sign < 0f ? -90f : 90f, 0f) * outward + outward * outwardMix;
            return v.sqrMagnitude > 1e-8f ? v.normalized : outward;
        }

        /// <summary>Throws AmountOf(w) projectiles at the nearest distinct enemies; extra shots fan out around them.</summary>
        void Volley(WeaponInstance w, EnemyManager em, bool returning, float turn = 0f)
        {
            var pp = PlayerProjectiles.Instance;
            if (pp == null) return;
            var d = w.Def;
            Vector3 origin = transform.position;
            int amount = Mathf.Min(AmountOf(w), _targets.Length);
            int found = PickTargets(em, origin, Acquire(w), amount, d.targetFarthest);
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
                if (turn != 0f) dir = Quaternion.Euler(0f, turn, 0f) * dir; // an echo: the mirrored side
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
            int found = PickTargets(em, origin, Acquire(w), Mathf.Clamp(lobCandidates, 1, _targets.Length), farthest: false);
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
        CompanionPack _pack;

        /// <summary>Summon (the Wolf Pack): the companions live in a CompanionPack on the player, driven every frame.</summary>
        void TickSummon(WeaponInstance w, EnemyManager em, float dt)
        {
            if (_pack == null)
            {
                _pack = GetComponent<CompanionPack>();
                if (_pack == null) _pack = gameObject.AddComponent<CompanionPack>();
            }
            _pack.Drive(w, this, em, dt);
        }

        /// <summary>A companion's bite on enemy i: the weapon's damage (× mul), crits, on-hit status, Boon procs and kill
        /// credit, like any of its hits.</summary>
        public void CompanionHit(WeaponInstance w, EnemyManager em, int i, Vector3 dir, float mul)
        {
            if (Halted) return;
            var info = Hit(w, em, i, dir, DamageFlags.Melee);
            info.Amount *= mul;
            Strike(w, em, i, info);
        }

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

        /// <summary>Fills _targets with up to `max` alive enemies within `range`, nearest first (or farthest first), by
        /// partial selection, no allocation.</summary>
        int PickTargets(EnemyManager em, Vector3 origin, float range, int max, bool farthest)
        {
            int n = em.QueryAlive(origin, range, _hits);
            int found = 0;
            float r2 = range * range;
            for (int pick = 0; pick < max && pick < n; pick++)
            {
                int best = -1;
                float bestD = farthest ? -1f : float.MaxValue;
                for (int k = pick; k < n; k++)
                {
                    Vector3 to = em.GetPosition(_hits[k]) - origin;
                    float dd = to.x * to.x + to.z * to.z;
                    if (farthest ? (dd > bestD && dd <= r2) : dd < bestD) { bestD = dd; best = k; }
                }
                if (best < 0) break;
                (_hits[pick], _hits[best]) = (_hits[best], _hits[pick]);
                _targets[found++] = _hits[pick];
            }
            return found;
        }
    }
}
