using System.Collections.Generic;
using Gladiator.Core;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>Runtime state of one equipped weapon.</summary>
    public sealed class WeaponInstance
    {
        public WeaponDef Def;
        public int Level = 1;
        public float Cooldown;
        public float SwingT = -1f;
        public bool Impacted;
        public Vector3 AimDir = Vector3.forward;
    }

    /// <summary>
    /// Fires the player's weapons automatically (CLAUDE.md §5): nearest-enemy auto-aim, central tick, hits via the
    /// enemy spatial hash (never Physics.Overlap), damage through EnemyManager.ApplyDamage. Up to 4 slots.
    /// </summary>
    public sealed class WeaponSystem : MonoBehaviour, ITickable
    {
        public const int MaxSlots = 4;

        public WeaponDef[] startingWeapons;
        public PlayerPartAnimator animator;
        public SlashFx slashFx;
        [Header("Player stat multipliers (stat pipeline replaces these in M2.4)")]
        public float damageMultiplier = 1f;
        public float attackSpeedMultiplier = 1f;
        public float critChanceBonus = 0f;
        public float rangeMultiplier = 1f;

        // Effective values: the run's own bonuses × the admin/dev buff layer.
        public float DamageMul => damageMultiplier * DevSettings.DamageMul;
        public float AttackSpeedMul => attackSpeedMultiplier * DevSettings.AttackSpeedMul;
        public float RangeMul => rangeMultiplier * DevSettings.RangeMul;
        public float CritBonus => critChanceBonus + DevSettings.CritBonus;

        readonly List<WeaponInstance> _weapons = new List<WeaponInstance>(MaxSlots);
        readonly int[] _hits = new int[256];

        public IReadOnlyList<WeaponInstance> Weapons => _weapons;
        public int Swings { get; private set; }
        public int Hits { get; private set; }

        void Awake()
        {
            if (startingWeapons != null)
                foreach (var w in startingWeapons) if (w != null) Equip(w);
        }

        void OnEnable() => GameLoop.Register(this, TickGroup.Weapons);
        void OnDisable() => GameLoop.Unregister(this);

        public bool Equip(WeaponDef def)
        {
            if (_weapons.Count >= MaxSlots) return false;
            _weapons.Add(new WeaponInstance { Def = def, Cooldown = 0.3f });
            return true;
        }

        public void Tick(float dt, float unscaledDt)
        {
            if (dt <= 0f) return;
            var em = EnemyManager.Instance;
            bool anySwing = false;
            for (int w = 0; w < _weapons.Count; w++)
            {
                var inst = _weapons[w];
                switch (inst.Def.pattern)
                {
                    case WeaponPattern.MeleeArc:
                        TickMeleeArc(inst, em, dt);
                        if (inst.SwingT >= 0f && animator != null && !anySwing)
                        {
                            anySwing = true;
                            animator.AttackT = inst.SwingT;
                            animator.AttackDirection = inst.AimDir;
                        }
                        break;
                }
            }
            if (!anySwing && animator != null) animator.AttackT = -1f;
        }

        void TickMeleeArc(WeaponInstance w, EnemyManager em, float dt)
        {
            var d = w.Def;
            w.Cooldown -= dt;
            if (w.SwingT >= 0f)
            {
                w.SwingT += dt / Mathf.Max(0.01f, d.swingDuration);
                if (!w.Impacted && w.SwingT * d.swingDuration >= d.windup) { MeleeArcHit(w, em); w.Impacted = true; }
                if (w.SwingT >= 1f) w.SwingT = -1f;
                return;
            }
            if (w.Cooldown > 0f || em == null) return;
            Vector3 origin = transform.position;
            if (!em.TryGetNearest(origin, d.acquireRange * RangeMul, out int target)) return;
            Vector3 aim = em.GetPosition(target) - origin;
            aim.y = 0f;
            w.AimDir = aim.sqrMagnitude > 1e-4f ? aim.normalized : transform.forward;
            w.SwingT = 0f;
            w.Impacted = false;
            w.Cooldown = d.cooldown / Mathf.Max(0.1f, AttackSpeedMul);
            Swings++;
            if (slashFx != null)
                slashFx.Play(origin + Vector3.up * 0.75f, w.AimDir, d.range * RangeMul, d.arcDegrees, d.swingDuration);
        }

        void MeleeArcHit(WeaponInstance w, EnemyManager em)
        {
            var d = w.Def;
            Vector3 origin = transform.position;
            float range = d.range * RangeMul;
            float halfArc = d.arcDegrees * 0.5f;
            int n = em.QueryAlive(origin, range, _hits);
            int sourceId = d.RuntimeId;
            for (int k = 0; k < n; k++)
            {
                int i = _hits[k];
                Vector3 to = em.GetPosition(i) - origin;
                to.y = 0f;
                float dist = to.magnitude;
                if (dist > 0.7f && Vector3.Angle(w.AimDir, to) > halfArc) continue; // point-blank always connects
                bool crit = GameRandom.Chance(d.critChance + CritBonus);
                var info = new DamageInfo
                {
                    Amount = d.damage * DamageMul * (crit ? d.critMultiplier : 1f),
                    Flags = DamageFlags.Melee | (crit ? DamageFlags.Crit : 0),
                    Point = em.GetPosition(i),
                    Direction = dist > 1e-3f ? to / dist : w.AimDir,
                    Knockback = d.knockback,
                    SourceId = sourceId,
                };
                em.ApplyDamage(i, info);
                Hits++;
            }
        }
    }
}
