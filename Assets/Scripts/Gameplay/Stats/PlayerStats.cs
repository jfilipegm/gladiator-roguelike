using Gladiator.Core;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// The player's stat pipeline: the gladiator's base values + every upgrade's modifiers (StatBlock), then the admin
    /// buff layer (DevSettings) on top, so "reset buffs" never undoes a real pick. Every system reads its numbers here.
    /// </summary>
    public sealed class PlayerStats : MonoBehaviour
    {
        public GladiatorDef gladiator;

        StatBlock _block;

        /// <summary>Upgrades add/remove modifiers here. Created on first use, so other Awakes can read it in any order.</summary>
        public StatBlock Block
        {
            get
            {
                if (_block != null) return _block;
                _block = new StatBlock();
                var loadout = GetComponent<GladiatorLoadout>();
                if (loadout != null && loadout.Current != null) gladiator = loadout.Current; // the gladiator this player spawned as
                else if (GameSession.Gladiator != null) gladiator = GameSession.Gladiator; // picked in the menu
                if (gladiator != null)
                    foreach (var b in gladiator.baseStats) _block.SetBase(b.stat, b.value);
                return _block;
            }
        }

        /// <summary>Final value: pipeline × admin buffs.</summary>
        public float Get(StatId s)
        {
            float v = Block.Get(s);
            switch (s)
            {
                case StatId.Damage: return v * DevSettings.DamageMul;
                case StatId.AttackSpeed: return v * DevSettings.AttackSpeedMul;
                case StatId.Area: return v * DevSettings.RangeMul;
                case StatId.MaxHp: return v * DevSettings.MaxHpMul;
                case StatId.MoveSpeed: return v * DevSettings.MoveSpeedMul;
                case StatId.PickupRange: return v * DevSettings.PickupRangeMul;
                case StatId.CritChance: return v + DevSettings.CritBonus;
                default: return v;
            }
        }

        public float MaxHp => Get(StatId.MaxHp);
        public float Damage => Get(StatId.Damage);
        public float AttackSpeed => Get(StatId.AttackSpeed);
        public float Area => Get(StatId.Area);
        public float CritChance => Get(StatId.CritChance);
        public float CritDamage => Get(StatId.CritDamage);
        public float Knockback => Get(StatId.Knockback);
        public float MoveSpeed => Get(StatId.MoveSpeed);
        public float PickupRange => Get(StatId.PickupRange);
        public float Luck => Get(StatId.Luck);
    }
}
