using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// Map interactables (CLAUDE.md §6.5, M2.7): Armory Chests cost gold that rises per chest opened and give a pick of
    /// Uncommon-or-better cards; the Blood Altar trades max HP for a pick of Rare-or-better cards, once.
    /// </summary>
    [CreateAssetMenu(menuName = "Gladiator/Config/Interactables", fileName = "InteractableConfig")]
    public sealed class InteractableConfig : ScriptableObject
    {
        [Header("Armory Chests")]
        [Tooltip("Price of the first chest (gold).")]
        public int basePrice = 15;
        [Tooltip("Each chest opened multiplies the next price by this.")]
        public float priceGrowth = 1.4f;
        [Tooltip("Chests standing on the map at once.")]
        [Range(1, 6)] public int maxChests = 3;
        [Tooltip("Chests placed when the run starts.")]
        [Range(0, 6)] public int startingChests = 2;
        [Tooltip("Seconds before a new chest appears after one is opened (and between top-ups).")]
        public float respawnDelay = 35f;
        public Rarity chestFloor = Rarity.Uncommon;

        [Header("Blood Altar")]
        [Range(0, 2)] public int altars = 1;
        [Tooltip("Fraction of max HP lost for good when the altar is used.")]
        [Range(0.05f, 0.5f)] public float altarMaxHpCost = 0.15f;
        public Rarity altarFloor = Rarity.Rare;

        [Header("Placement and use")]
        [Tooltip("Reach (m) within which Interact uses it.")]
        public float interactRadius = 1.8f;
        [Tooltip("New interactables appear at least this far from the player (m).")]
        public float minPlayerDistance = 9f;
        [Tooltip("…and this far from each other (m).")]
        public float minSpacing = 7f;
        [Tooltip("Keep this much clear ground around a spot (m): obstacles and slopes are rejected.")]
        public float clearance = 1.3f;

        /// <summary>Price of the next chest after `opened` chests: base × growth^opened, rounded.</summary>
        public int PriceFor(int opened) => Mathf.Max(0, Mathf.RoundToInt(basePrice * Mathf.Pow(priceGrowth, opened)));
    }
}
