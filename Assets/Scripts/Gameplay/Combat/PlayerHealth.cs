using Gladiator.Core;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// Player HP with brief invulnerability after a hit. Raises OnDamaged (numbers, shake, crowd). At 0 HP the player
    /// is dead (RunEndController plays the death beat and shows the end screen) unless DevSettings.GodMode, which
    /// floors HP at 1 so hits still read. Max HP, Armor and HP Regen come from PlayerStats.
    /// </summary>
    public sealed class PlayerHealth : MonoBehaviour, ITickable
    {
        [Tooltip("Invulnerability after taking a hit (s). Keeps swarms from shredding you in one frame.")]
        public float invulnerableTime = 0.4f;
        [Tooltip("Armor is a damage-reduction fraction; this caps it so stacking can't make the player immune.")]
        [Range(0f, 0.95f)] public float maxArmor = 0.75f;
        public HitFeedbackConfig feedback;

        PlayerStats _stats;

        public float Hp { get; private set; }
        public float MaxHp => _stats != null ? _stats.MaxHp : 100f;
        public float Hp01 => MaxHp > 0 ? Hp / MaxHp : 0f;
        public bool IsDead { get; private set; }
        public bool IsInvulnerable => Time.time < _invulnerableUntil;
        public int Deaths { get; private set; }
        public float DamageTaken { get; private set; }
        /// <summary>Scales regen and Heal (the Blood Knight's Bloodthirst halves it); HealUnscaled ignores it.</summary>
        public float HealingMul { get; set; } = 1f;
        /// <summary>HP paid on purpose (Spend: the Blood Rite), not counted as damage taken.</summary>
        public float HpSpent { get; private set; }

        float _invulnerableUntil;

        void Awake()
        {
            _stats = GetComponent<PlayerStats>();
            Hp = MaxHp;
        }

        void OnEnable() => GameLoop.Register(this, TickGroup.Player, 50);
        void OnDisable() => GameLoop.Unregister(this);

        public void Tick(float dt, float unscaledDt)
        {
            if (dt <= 0f || IsDead || _stats == null) return;
            float regen = _stats.Get(StatId.HpRegen);
            if (regen > 0f && Hp < MaxHp) Hp = Mathf.Min(MaxHp, Hp + regen * HealingMul * dt);
        }

        /// <summary>Heals `amount` × HealingMul (amphorae, healing cards, admin). Returns the HP actually gained.</summary>
        public float Heal(float amount) => HealUnscaled(amount * HealingMul);

        /// <summary>Heals ignoring HealingMul (a gladiator's own healing, e.g. Bloodthirst). Returns the HP gained.</summary>
        public float HealUnscaled(float amount)
        {
            if (IsDead || amount <= 0f) return 0f;
            float before = Hp;
            Hp = Mathf.Min(MaxHp, Hp + amount);
            return Hp - before;
        }

        /// <summary>
        /// Pays up to `amount` HP on purpose (the Blood Rite): never below `floor` HP, no invulnerability, no
        /// OnDamaged (no hurt flash, shake or Favor close call). Returns the HP paid.
        /// </summary>
        public float Spend(float amount, float floor = 1f)
        {
            if (IsDead || amount <= 0f) return 0f;
            float paid = Mathf.Clamp(Hp - floor, 0f, amount);
            Hp -= paid;
            HpSpent += paid;
            return paid;
        }

        public void HealFull() => HealUnscaled(MaxHp);

        /// <summary>Call after an upgrade changed MaxHp: a raise also heals by the same amount, a cut only clamps.</summary>
        public void OnMaxHpChanged(float oldMaxHp)
        {
            if (IsDead) return;
            float delta = MaxHp - oldMaxHp;
            Hp = Mathf.Clamp(Hp + Mathf.Max(0f, delta), 1f, MaxHp);
        }

        /// <summary>Call after MaxHp changed for a reason other than healing (admin buff): keeps the HP fraction.</summary>
        public void RescaleFrom(float oldMaxHp)
        {
            if (IsDead || oldMaxHp <= 0f) return;
            Hp = Mathf.Clamp(Hp / oldMaxHp * MaxHp, 1f, MaxHp);
        }

        /// <summary>
        /// Returns true if damage was applied. Status damage (burning ground, later bleed/curse) ticks through the
        /// invulnerability window, doesn't grant one, and gets lighter feedback (no hit-stop) so a DoT doesn't stutter.
        /// </summary>
        public bool TakeDamage(in DamageInfo info)
        {
            bool status = (info.Flags & DamageFlags.Status) != 0;
            if (IsDead || (IsInvulnerable && !status)) return false;
            float floor = DevSettings.GodMode ? 1f : 0f;
            float before = Hp;
            float armor = _stats != null ? Mathf.Clamp(_stats.Get(StatId.Armor), 0f, maxArmor) : 0f;
            float amount = info.Amount * (1f - armor);
            Hp = Mathf.Max(floor, Hp - amount);
            DamageTaken += before - Hp;
            if (!status) _invulnerableUntil = Time.time + invulnerableTime;
            GameEvents.OnDamaged.Raise(new DamagedEvent { Position = transform.position + Vector3.up * 1.4f, Damage = amount, HealthAfter = Hp, HealthMax = MaxHp });
            if (feedback != null)
            {
                ScreenShake.Add(status ? feedback.shakeOnPlayerHit * 0.3f : feedback.shakeOnPlayerHit);
                if (!status) TimeController.HitStop(feedback.hitStopOnPlayerHit);
            }
            if (Hp <= 0f) Die();
            return true;
        }

        /// <summary>Instant death (admin "kill me", hazards later).</summary>
        public void Kill()
        {
            if (IsDead) return;
            Hp = 0f;
            Die();
        }

        void Die()
        {
            IsDead = true;
            Deaths++;
            Debug.Log("[PlayerHealth] died");
        }
    }
}
