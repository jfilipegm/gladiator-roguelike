using Gladiator.Core;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// The Blood Knight's passive (docs/GLADIATORS.md): kills heal him, up to `maxHealPerSecond` (elites always heal in
    /// full), and every other healing is multiplied by `otherHealingMul` (PlayerHealth.HealingMul): he lives on kills.
    /// </summary>
    [CreateAssetMenu(menuName = "Gladiator/Traits/Bloodthirst", fileName = "TR_Bloodthirst")]
    public sealed class BloodthirstTrait : GladiatorTrait
    {
        [Tooltip("HP healed per kill.")]
        public float healPerKill = 1f;
        [Tooltip("HP healed per elite kill (not capped).")]
        public float healPerElite = 8f;
        [Tooltip("Most HP ordinary kills can heal per second (with 300 enemies, uncapped would be immortal).")]
        public float maxHealPerSecond = 6f;
        [Tooltip("Every other healing (regen, amphorae, healing cards) is multiplied by this.")]
        [Range(0f, 1f)] public float otherHealingMul = 0.5f;

        public override void Install(GameObject player)
        {
            var c = player.GetComponent<Bloodthirst>();
            if (c == null) c = player.AddComponent<Bloodthirst>();
            c.Init(this);
        }
    }

    /// <summary>Runtime half of BloodthirstTrait: a heal budget that refills at maxHealPerSecond (× HealMul).</summary>
    public sealed class Bloodthirst : MonoBehaviour, ITickable
    {
        public BloodthirstTrait trait;

        PlayerHealth _health;
        EventHandler<KillEvent> _onKill;
        float _budget;

        /// <summary>Multiplies kill heals and the cap (the Blood Rite's frenzy doubles it).</summary>
        public float HealMul { get; set; } = 1f;
        /// <summary>HP healed by kills this run.</summary>
        public float Healed { get; private set; }

        void Awake()
        {
            _health = GetComponent<PlayerHealth>();
            _onKill = OnKill;
        }

        public void Init(BloodthirstTrait t)
        {
            trait = t;
            _budget = t.maxHealPerSecond;
            if (_health != null) _health.HealingMul = t.otherHealingMul;
        }

        void OnEnable()
        {
            GameLoop.Register(this, TickGroup.Player, 55);
            GameEvents.OnKill.Subscribe(_onKill);
            if (trait != null && _health != null) _health.HealingMul = trait.otherHealingMul;
        }

        void OnDisable()
        {
            GameLoop.Unregister(this);
            GameEvents.OnKill.Unsubscribe(_onKill);
            if (_health != null) _health.HealingMul = 1f;
        }

        public void Tick(float dt, float unscaledDt)
        {
            if (trait == null || dt <= 0f) return;
            float cap = trait.maxHealPerSecond * HealMul;
            _budget = Mathf.Min(cap, _budget + cap * dt);
        }

        void OnKill(in KillEvent e)
        {
            if (trait == null || _health == null || _health.IsDead) return;
            float heal;
            if (e.Elite) heal = trait.healPerElite * HealMul;
            else
            {
                heal = Mathf.Min(trait.healPerKill * HealMul, _budget);
                _budget -= heal;
            }
            Healed += _health.HealUnscaled(heal);
        }
    }
}
