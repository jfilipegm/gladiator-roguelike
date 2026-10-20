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
        [Tooltip("Meta progression: upgrades held back by unearned unlocks are never offered this run.")]
        public MetaConfig meta;
        [Tooltip("Announcer colour for a tag synergy switching on (player palette: cool cyan-white).")]
        public Color synergyColor = new Color(0.55f, 0.95f, 1f);

        PlayerStats _stats;
        PlayerHealth _health;
        WeaponSystem _weapons;

        public BuildState State { get; } = new BuildState();
        public PlayerStats Stats => _stats;

        /// <summary>Evolutions taken this run (bot log, tests).</summary>
        public int Evolutions { get; private set; }

        public int RerollsLeft => Mathf.Max(0, (int)_stats.Get(StatId.Rerolls) - State.RerollsUsed);
        public int BanishesLeft => Mathf.Max(0, (int)_stats.Get(StatId.Banishes) - State.BanishesUsed);

        void Awake()
        {
            _stats = GetComponent<PlayerStats>();
            _health = GetComponent<PlayerHealth>();
            _weapons = GetComponent<WeaponSystem>();
            if (meta != null && catalog != null)
                foreach (var u in catalog.upgrades)
                    if (MetaProgress.IsLocked(u, meta)) State.Lock(u);
        }

        // The starting weapon is owned at level 1, so its level-up cards come up and it fills a weapon slot.
        void Start()
        {
            if (_weapons == null || catalog == null) return;
            foreach (var w in _weapons.Weapons)
            {
                var card = FindWeaponCard(w.Def);
                if (card != null && State.LevelOf(card) == 0 && !State.IsEvolved(card)) State.Take(card, Rarity.Common, null, config);
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
            int level;
            if (d.evolvesFrom != null)
            {
                // Phase 5.5: the weapon becomes its evolution in the same slot, keeping its level-8 numbers
                if (!State.Evolve(d.evolvesFrom, d)) return 0;
                if (_weapons != null && d.evolvesFrom.weapon != null) _weapons.Evolve(d.evolvesFrom.weapon, d.weapon);
                Evolutions++;
                level = 1;
                GameEvents.OnAnnounce.Raise(new AnnounceEvent { Text = d.displayName, Color = config.RarityColor(Rarity.Legendary), Seconds = 2.5f });
                if (CrowdExcitement.Instance != null) CrowdExcitement.Instance.Spike(0.8f);
            }
            else
            {
                level = State.Take(d, card.Rarity, _stats.Block, config);
                if (d.kind == UpgradeKind.Weapon && d.weapon != null && _weapons != null)
                {
                    if (level == 1) _weapons.Equip(d.weapon);
                    else _weapons.LevelUp(d.weapon, config.Magnitude(card.Rarity));
                }
            }
            UpdateSynergies();
            if (_health != null && !Mathf.Approximately(oldMax, _health.MaxHp)) _health.OnMaxHpChanged(oldMax);
            return level;
        }

        /// <summary>Switches on the tag synergies the build has reached (Phase 5.5) and announces them.</summary>
        void UpdateSynergies()
        {
            if (catalog == null) return;
            int added = State.UpdateSynergies(catalog.synergies, _stats != null ? _stats.Block : null);
            for (int i = State.SynergyCount - added; i < State.SynergyCount; i++)
            {
                var s = State.GetSynergy(i);
                GameEvents.OnAnnounce.Raise(new AnnounceEvent { Text = s.announce, Color = synergyColor, Seconds = 2.5f });
                if (CrowdExcitement.Instance != null) CrowdExcitement.Instance.Spike(0.6f);
            }
        }
    }
}
