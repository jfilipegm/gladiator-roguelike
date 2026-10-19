using System;
using Gladiator.Core;
using UnityEngine;

namespace Gladiator.Gameplay
{
    public enum Rarity { Common, Uncommon, Rare, Legendary }

    /// <summary>Boon = passive (6 slots, levels 1-5). Weapon = M2.5. Consumable = filler card when the pool runs dry.</summary>
    public enum UpgradeKind { Boon, Weapon, Consumable }

    /// <summary>Synergy tags (CLAUDE.md §6.5). Statuses and tag thresholds key off these.</summary>
    [Flags]
    public enum UpgradeTag
    {
        None = 0, Fire = 1, Lightning = 2, Blood = 4, Curse = 8, Summon = 16, Heavy = 32, Crit = 64, Gold = 128,
        Movement = 256, Defense = 512, Xp = 1024,
    }

    /// <summary>
    /// A level-up card's content as data. Every time it's taken, `perLevel` is added to the player's stats scaled by
    /// the card's rarity (UpgradeConfig.rarityMagnitude), so a Rare "Brute Force" is a bigger step than a Common one.
    /// Weapon cards equip their weapon at level 1 and apply the weapon's own level table after that.
    /// Behaviour hooks (on-hit, on-slide...) arrive with the M2.6 Boons.
    /// </summary>
    [CreateAssetMenu(menuName = "Gladiator/Upgrade", fileName = "UP_")]
    public sealed class UpgradeDef : ScriptableObject
    {
        public string id = "upgrade";
        public string displayName = "Upgrade";
        [Tooltip("Two letters shown in the HUD build strip until real icons exist.")]
        public string glyph = "UP";
        public UpgradeKind kind = UpgradeKind.Boon;
        public UpgradeTag tags;

        [Header("Offering")]
        [Tooltip("Relative chance to be offered when eligible.")]
        public float weight = 10f;
        [Range(1, 8)] public int maxLevel = 5;
        public Rarity minRarity = Rarity.Common;
        public Rarity maxRarity = Rarity.Legendary;

        [Header("Effect")]
        [Tooltip("Weapon cards: the weapon equipped at level 1; levels 2+ apply its own level table (max level = its MaxLevel).")]
        public WeaponDef weapon;
        [Tooltip("Added to the player's stats each time this is taken, scaled by the card's rarity.")]
        public StatMod[] perLevel = Array.Empty<StatMod>();
        [Tooltip("Consumables: heal this fraction of max HP.")]
        [Range(0f, 1f)] public float healFraction;
        [Tooltip("Consumables: gold given.")]
        public int gold;
    }
}
