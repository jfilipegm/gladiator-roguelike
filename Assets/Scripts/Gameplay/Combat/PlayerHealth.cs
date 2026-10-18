using Gladiator.Core;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>Player HP with brief invulnerability after a hit. Raises OnDamaged (numbers, shake, crowd).</summary>
    public sealed class PlayerHealth : MonoBehaviour
    {
        public float maxHp = 100f;
        [Tooltip("Invulnerability after taking a hit (s). Keeps swarms from shredding you in one frame.")]
        public float invulnerableTime = 0.4f;
        public HitFeedbackConfig feedback;
        [Tooltip("Dev/feel-test only: refill instead of dying.")]
        public bool refillOnDeath = true;

        public float Hp { get; private set; }
        public float Hp01 => maxHp > 0 ? Hp / maxHp : 0f;
        public bool IsInvulnerable => Time.time < _invulnerableUntil;
        public int Deaths { get; private set; }

        float _invulnerableUntil;

        void Awake() => Hp = maxHp;

        public void Heal(float amount) => Hp = Mathf.Min(maxHp, Hp + amount);

        /// <summary>Returns true if damage was applied.</summary>
        public bool TakeDamage(in DamageInfo info)
        {
            if (IsInvulnerable || Hp <= 0f) return false;
            Hp = Mathf.Max(0f, Hp - info.Amount);
            _invulnerableUntil = Time.time + invulnerableTime;
            GameEvents.OnDamaged.Raise(new DamagedEvent { Position = transform.position + Vector3.up * 1.4f, Damage = info.Amount, HealthAfter = Hp, HealthMax = maxHp });
            if (feedback != null)
            {
                ScreenShake.Add(feedback.shakeOnPlayerHit);
                TimeController.HitStop(feedback.hitStopOnPlayerHit);
            }
            if (Hp <= 0f)
            {
                Deaths++;
                Debug.Log("[PlayerHealth] died");
                if (refillOnDeath) Hp = maxHp;
            }
            return true;
        }
    }
}
