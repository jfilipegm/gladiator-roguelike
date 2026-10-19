using Gladiator.Core;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// The player's run build: owned upgrades, banishes, rerolls (BuildState) applied to PlayerStats, plus the side
    /// effects of taking a card (max-HP raises heal, consumables heal/give gold). Lives on the player.
    /// </summary>
    public sealed class PlayerBuild : MonoBehaviour
    {
        public UpgradeConfig config;
        public UpgradeCatalog catalog;

        PlayerStats _stats;
        PlayerHealth _health;
        WeaponSystem _weapons;

        public BuildState State { get; } = new BuildState();
        public PlayerStats Stats => _stats;

        public int RerollsLeft => Mathf.Max(0, (int)_stats.Get(StatId.Rerolls) - State.RerollsUsed);
        public int BanishesLeft => Mathf.Max(0, (int)_stats.Get(StatId.Banishes) - State.BanishesUsed);

        void Awake()
        {
            _stats = GetComponent<PlayerStats>();
            _health = GetComponent<PlayerHealth>();
            _weapons = GetComponent<WeaponSystem>();
        }

        // The starting weapon is owned at level 1, so its level-up cards come up and it fills a weapon slot.
        void Start()
        {
            if (_weapons == null || catalog == null) return;
            foreach (var w in _weapons.Weapons)
            {
                var card = FindWeaponCard(w.Def);
                if (card != null && State.LevelOf(card) == 0) State.Take(card, Rarity.Common, null, config);
            }
        }

        public UpgradeDef FindWeaponCard(WeaponDef weapon)
        {
            foreach (var u in catalog.upgrades) if (u != null && u.kind == UpgradeKind.Weapon && u.weapon == weapon) return u;
            return null;
        }

        /// <summary>Applies one card. Returns the upgrade's new level (0 for a consumable).</summary>
        public int Take(in CardOffer card)
        {
            var d = card.Def;
            if (d == null) return 0;
            if (d.kind == UpgradeKind.Consumable)
            {
                if (d.healFraction > 0f && _health != null) _health.Heal(_health.MaxHp * d.healFraction);
                if (d.gold > 0 && RunState.Current != null) RunState.Current.AddGold(d.gold);
                return 0;
            }
            float oldMax = _health != null ? _health.MaxHp : 0f;
            int level = State.Take(d, card.Rarity, _stats.Block, config);
            if (d.kind == UpgradeKind.Weapon && d.weapon != null && _weapons != null)
            {
                if (level == 1) _weapons.Equip(d.weapon);
                else _weapons.LevelUp(d.weapon, config.Magnitude(card.Rarity));
            }
            if (_health != null && !Mathf.Approximately(oldMax, _health.MaxHp)) _health.OnMaxHpChanged(oldMax);
            return level;
        }
    }
}
