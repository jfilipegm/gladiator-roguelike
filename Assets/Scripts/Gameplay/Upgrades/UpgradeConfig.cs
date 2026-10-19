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

        public float RarityWeight(Rarity r, float luck) =>
            rarityWeights[(int)r] * Mathf.Exp(Mathf.Max(0f, luck) * luckStrength * (int)r);

        public float Magnitude(Rarity r) => rarityMagnitude[(int)r];
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
        }
    }
}
