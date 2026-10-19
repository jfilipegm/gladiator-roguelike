using System;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// Every upgrade the level-up cards can offer (unlocks will filter this in M3.6), plus the filler cards used when
    /// nothing else is eligible.
    /// </summary>
    [CreateAssetMenu(menuName = "Gladiator/Upgrade Catalog", fileName = "UpgradeCatalog")]
    public sealed class UpgradeCatalog : ScriptableObject
    {
        public UpgradeDef[] upgrades = Array.Empty<UpgradeDef>();
        [Tooltip("Consumables offered when the pool runs dry (all slots full and maxed).")]
        public UpgradeDef[] fallbacks = Array.Empty<UpgradeDef>();
    }
}
