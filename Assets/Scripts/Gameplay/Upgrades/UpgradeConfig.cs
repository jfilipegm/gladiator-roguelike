using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// Card and rarity rules (CLAUDE.md §6.5). Arrays are indexed by Rarity (Common, Uncommon, Rare, Legendary).
    /// Tune freely; log changes in DECISIONS.
    /// </summary>
    [CreateAssetMenu(menuName = "Gladiator/Config/Upgrades", fileName = "UpgradeConfig")]
    public sealed class UpgradeConfig : ScriptableObject
    {
        public const int RarityCount = 4;

        [Header("Slots")]
        public int weaponSlots = 4;
        public int boonSlots = 6;

        [Header("Rarity")]
        [Tooltip("Base odds per rarity (relative).")]
        public float[] rarityWeights = { 62f, 26f, 10f, 2f };
        [Tooltip("How much bigger each rarity's step is: Flat/Percent values are multiplied by this.")]
        public float[] rarityMagnitude = { 1f, 1.4f, 1.8f, 2.5f };
        public Color[] rarityColors =
        {
            new Color(0.82f, 0.85f, 0.9f),
            new Color(0.38f, 0.9f, 0.45f),
            new Color(0.32f, 0.62f, 1f),
            new Color(1f, 0.74f, 0.2f),
        };
        [Tooltip("Luck multiplies rarity r's weight by exp(luck × strength × r): luck 0.3 ≈ ×1.6 Uncommon, ×2.5 Rare, ×3.9 Legendary.")]
        public float luckStrength = 1.5f;

        [Header("Cards")]
        [Tooltip("Cards per level-up.")]
        public int cards = 3;
        [Tooltip("Luck at which a 4th card is guaranteed (chance = luck / this).")]
        public float fourthCardLuck = 0.5f;
        [Tooltip("Ignore card input for this long after the cards appear (s), so a held jump/click doesn't pick blind.")]
        public float inputGrace = 0.3f;

        [Header("Vows (P1: trade-off cards)")]
        [Tooltip("Vows' combined share of the odds for each level-up card while other cards are eligible (0.15 ≈ a Vow in 2 of every 5 level-ups). Their own weights only split this share.")]
        [Range(0f, 0.5f)] public float vowShare = 0.15f;
        [Tooltip("At most this many Vows among one pick's cards.")]
        [Range(0, 4)] public int maxVowsPerPick = 1;
        [Tooltip("Vows in chest, altar and boss rewards too (off: rewards you paid for are pure upside).")]
        public bool vowsInRewards = false;
        [Tooltip("A Vow's drawbacks scale with rarity by this (gentler than rarityMagnitude): a Legendary Vow gives far more for a little more.")]
        public float[] vowDrawbackMagnitude = { 1f, 1.1f, 1.2f, 1.3f };

        [Header("Evolutions (Phase 5.5)")]
        [Tooltip("Chest, altar and boss rewards put a ready evolution in their first slot.")]
        public bool evolutionInRewards = true;
        [Tooltip("Level-ups may offer a ready evolution too (off: evolving is a chest moment).")]
        public bool evolutionInLevelUps = false;

        public float RarityWeight(Rarity r, float luck) =>
            rarityWeights[(int)r] * Mathf.Exp(Mathf.Max(0f, luck) * luckStrength * (int)r);

        public float Magnitude(Rarity r) => rarityMagnitude[(int)r];
        public float DrawbackMagnitude(Rarity r) =>
            vowDrawbackMagnitude != null && (int)r < vowDrawbackMagnitude.Length ? vowDrawbackMagnitude[(int)r] : 1f;
        public Color RarityColor(Rarity r) => rarityColors[(int)r];

        /// <summary>3 cards, or 4 with Luck (`roll01` uniform in [0,1)).</summary>
        public int CardCount(float luck, float roll01)
        {
            bool fourth = fourthCardLuck > 0f && roll01 < luck / fourthCardLuck;
            return Mathf.Clamp(cards + (fourth ? 1 : 0), 1, 4);
        }

        void OnValidate()
        {
            if (rarityWeights == null || rarityWeights.Length != RarityCount) System.Array.Resize(ref rarityWeights, RarityCount);
            if (rarityMagnitude == null || rarityMagnitude.Length != RarityCount) System.Array.Resize(ref rarityMagnitude, RarityCount);
            if (rarityColors == null || rarityColors.Length != RarityCount) System.Array.Resize(ref rarityColors, RarityCount);
            if (vowDrawbackMagnitude == null || vowDrawbackMagnitude.Length != RarityCount) System.Array.Resize(ref vowDrawbackMagnitude, RarityCount);
        }
    }
}
