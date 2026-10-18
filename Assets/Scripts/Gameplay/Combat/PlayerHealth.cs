using Gladiator.Core;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// Player HP with brief invulnerability after a hit. Raises OnDamaged (numbers, shake, crowd). At 0 HP the player
    /// is dead (RunEndController plays the death beat and shows the end screen) unless DevSettings.GodMode, which
    /// floors HP at 1 so hits still read.
    /// </summary>
    public sealed class PlayerHealth : MonoBehaviour
    {
        [Tooltip("Base max HP (level-up cards add to this; the admin % buff multiplies it).")]
        public float maxHp = 100f;
        [Tooltip("Invulnerability after taking a hit (s). Keeps swarms from shredding you in one frame.")]
        public float invulnerableTime = 0.4f;
        public HitFeedbackConfig feedback;

        public float Hp { get; private set; }
        public float MaxHp => maxHp * DevSettings.MaxHpMul;
        public float Hp01 => MaxHp > 0 ? Hp / MaxHp : 0f;
        public bool IsDead { get; private set; }
        public bool IsInvulnerable => Time.time < _invulnerableUntil;
        public int Deaths { get; private set; }
        public float DamageTaken { get; private set; }

        float _invulnerableUntil;

        void Awake() => Hp = MaxHp;

        public void Heal(float amount)
        {
            if (IsDead) return;
            Hp = Mathf.Min(MaxHp, Hp + amount);
        }

        public void HealFull() => Heal(MaxHp);

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
            Hp = Mathf.Max(floor, Hp - info.Amount);
            DamageTaken += before - Hp;
            if (!status) _invulnerableUntil = Time.time + invulnerableTime;
            GameEvents.OnDamaged.Raise(new DamagedEvent { Position = transform.position + Vector3.up * 1.4f, Damage = info.Amount, HealthAfter = Hp, HealthMax = MaxHp });
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
