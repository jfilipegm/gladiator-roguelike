using Gladiator.Core;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// The player's stat pipeline: the gladiator's base values + every upgrade's modifiers (StatBlock), then the admin
    /// buff layer (DevSettings) on top, so "reset buffs" never undoes a real pick. Every system reads its numbers here.
    /// P1: Breakdown splits each total into its base and sources (cards, synergies, traits, the admin layer) for the
    /// stats page.
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
        public float Get(StatId s) => WithAdmin(s, Block.Get(s));

        /// <summary>`v`, a pipeline value of `s`, with the admin buffs on top: what Get returns (previews use it on their copy).</summary>
        public static float WithAdmin(StatId s, float v)
        {
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

        /// <summary>
        /// Get(s) split into shares that add up to it: into[0] the base, then one per source (StatBlock.Breakdown), then
        /// the admin layer as its own share when it changes the stat. Returns how many were written (no allocation).
        /// </summary>
        public int Breakdown(StatId s, StatShare[] into)
        {
            int n = Block.Breakdown(s, into);
            float admin = Get(s) - Block.Get(s);
            if (n < into.Length && Mathf.Abs(admin) > 1e-6f)
                into[n++] = new StatShare { Source = new StatSource(StatSourceKind.Admin), Amount = admin, Count = 1 };
            return n;
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
