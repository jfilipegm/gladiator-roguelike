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
    /// Behavioural Boons add `effects` (BoonEffect), run by BoonEffects through the event bus.
    /// </summary>
    [CreateAssetMenu(menuName = "Gladiator/Upgrade", fileName = "UP_")]
    public sealed class UpgradeDef : ScriptableObject
    {
        public string id = "upgrade";
        public string displayName = "Upgrade";
        [Tooltip("Two letters shown where there's no icon.")]
        public string glyph = "UP";
        [Tooltip("The pixel-icon atlas (PixelSrc/upgrade_icons.py → Assets/Art/Textures/UI/UpgradeIcons.png) and this upgrade's row in it; set by IconSetup.")]
        public Texture2D icon;
        public int iconRow = -1;
        public UpgradeKind kind = UpgradeKind.Boon;
        public UpgradeTag tags;

        [Header("Offering")]
        [Tooltip("Relative chance to be offered when eligible.")]
        public float weight = 10f;
        [Range(1, 8)] public int maxLevel = 5;
        public Rarity minRarity = Rarity.Common;
        public Rarity maxRarity = Rarity.Legendary;
        [Tooltip("Only offered once you own at least one of these (synergy Boons that do nothing on their own).")]
        public UpgradeDef[] requiresAny = Array.Empty<UpgradeDef>();

        [Header("Effect")]
        [Tooltip("Weapon cards: the weapon equipped at level 1; levels 2+ apply its own level table (max level = its MaxLevel).")]
        public WeaponDef weapon;

        [Header("Evolution (Phase 5.5)")]
        [Tooltip("Evolution cards: the weapon card this evolves. Offered in the first slot of chest, altar and boss rewards once that weapon is at its max level and the catalyst is owned; taking it swaps the weapon in its slot (docs/EVOLUTIONS.md).")]
        public UpgradeDef evolvesFrom;
        [Tooltip("Evolution cards: the Boon that must be owned (any level).")]
        public UpgradeDef catalyst;

        public bool IsEvolution => evolvesFrom != null;
        [Tooltip("Added to the player's stats each time this is taken, scaled by the card's rarity.")]
        public StatMod[] perLevel = Array.Empty<StatMod>();

        [Header("Vow (P1: trade-off cards)")]
        [Tooltip("A Vow: a big upside (perLevel) sworn with a downside (drawbacks). Split red/gold frame; its own share of the level-up odds (UpgradeConfig.vowShare).")]
        public bool vow;
        [Tooltip("Vows: the price, added with each take like perLevel but scaled by the gentler UpgradeConfig.vowDrawbackMagnitude.")]
        public StatMod[] drawbacks = Array.Empty<StatMod>();

        public bool IsVow => vow;

        /// <summary>
        /// One take's stat modifiers into `stats`: perLevel at the rarity's magnitude, a Vow's drawbacks at the gentler
        /// drawback magnitude. Taking a card (BuildState) and previewing it (StatPreview) both go through here.
        /// </summary>
        public void AddStats(StatBlock stats, Rarity rarity, UpgradeConfig config, in StatSource source)
        {
            if (stats == null || kind == UpgradeKind.Consumable) return;
            float scale = config != null ? config.Magnitude(rarity) : 1f;
            if (perLevel != null) // weapon cards carry their levels on the WeaponDef instead
                for (int m = 0; m < perLevel.Length; m++) stats.Add(perLevel[m], scale, source);
            if (!vow || drawbacks == null) return;
            float price = config != null ? config.DrawbackMagnitude(rarity) : 1f;
            for (int m = 0; m < drawbacks.Length; m++) stats.Add(drawbacks[m], price, source);
        }
        [Tooltip("Behaviour (M2.6): status on hit, sparks, bursts, slide trails… handled by BoonEffects.")]
        public BoonEffect[] effects = Array.Empty<BoonEffect>();
        [Tooltip("Consumables: heal this fraction of max HP.")]
        [Range(0f, 1f)] public float healFraction;
        [Tooltip("Consumables: gold given.")]
        public int gold;
    }
}
