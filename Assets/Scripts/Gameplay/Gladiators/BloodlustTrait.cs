using Gladiator.Core;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// The Blood Knight's passive (docs/GLADIATORS.md): stronger at low HP. A damage multiplier that rises as HP falls,
    /// 0 at full HP and `maxDamageBonus` at `fullAtHp` or below. A Multiply modifier, so it stays felt late in a run.
    /// </summary>
    [CreateAssetMenu(menuName = "Gladiator/Traits/Bloodlust", fileName = "TR_Bloodlust")]
    public sealed class BloodlustTrait : GladiatorTrait
    {
        [Tooltip("Damage bonus at fullAtHp or below (0.6 = ×1.6 damage).")]
        public float maxDamageBonus = 0.6f;
        [Tooltip("HP fraction at which the bonus is full; at full HP it is 0, linear in between.")]
        [Range(0f, 0.95f)] public float fullAtHp = 0.2f;

        public float BonusAt(float hp01) => maxDamageBonus * Mathf.Clamp01((1f - hp01) / Mathf.Max(0.01f, 1f - fullAtHp));

        public override void Install(GameObject player)
        {
            var c = player.GetComponent<Bloodlust>();
            if (c == null) c = player.AddComponent<Bloodlust>();
            c.trait = this;
        }
    }

    /// <summary>Runtime half of BloodlustTrait: keeps one Damage modifier in step with HP, in 1 % steps.</summary>
    public sealed class Bloodlust : MonoBehaviour, ITickable
    {
        public BloodlustTrait trait;

        PlayerHealth _health;
        PlayerStats _stats;
        StatMod _applied;
        bool _has;

        /// <summary>The bonus in effect (0.34 = +34 % damage).</summary>
        public float Bonus { get; private set; }

        void Awake()
        {
            _health = GetComponent<PlayerHealth>();
            _stats = GetComponent<PlayerStats>();
        }

        void OnEnable() => GameLoop.Register(this, TickGroup.Player, 56);

        void OnDisable()
        {
            GameLoop.Unregister(this);
            Apply(0f);
        }

        public void Tick(float dt, float unscaledDt)
        {
            if (trait == null || _health == null) return;
            Refresh();
        }

        /// <summary>Re-reads HP now (tests; the tick does it every frame).</summary>
        public void Refresh()
        {
            if (trait == null || _health == null) return;
            Apply(_health.IsDead ? 0f : Mathf.Round(trait.BonusAt(_health.Hp01) * 100f) / 100f);
        }

        void Apply(float bonus)
        {
            if (_stats == null || (bonus == Bonus && _has == bonus > 0f)) return;
            if (_has) _stats.Block.Remove(_applied);
            _has = bonus > 0f;
            _applied = new StatMod(StatId.Damage, ModOp.Multiply, 1f + bonus);
            if (_has) _stats.Block.Add(_applied);
            Bonus = bonus;
        }
    }
}
