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
            if (regen > 0f && Hp < MaxHp) Hp = Mathf.Min(MaxHp, Hp + regen * dt);
        }

        public void Heal(float amount)
        {
            if (IsDead) return;
            Hp = Mathf.Min(MaxHp, Hp + amount);
        }

        public void HealFull() => Heal(MaxHp);

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
