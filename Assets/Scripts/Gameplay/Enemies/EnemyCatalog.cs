using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// Every EnemyDef and AffixDef in the project, kept up to date automatically by an Editor postprocessor whenever an EnemyDef
    /// asset is created, changed or deleted. The admin spawn menu (and later the Director) read it, so a new enemy or
    /// boss is spawnable the moment its asset exists.
    /// </summary>
    [CreateAssetMenu(menuName = "Gladiator/Enemy Catalog", fileName = "EnemyCatalog")]
    public sealed class EnemyCatalog : ScriptableObject
    {
        public EnemyDef[] enemies = new EnemyDef[0];
        public AffixDef[] affixes = new AffixDef[0];
    }
}
