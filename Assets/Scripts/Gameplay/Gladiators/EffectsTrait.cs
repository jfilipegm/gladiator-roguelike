using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// A gladiator passive made of Boon effects (Phase 5.12: the Ashen Oracle's Contagion is a BurstOnDeath on Curse):
    /// BoonEffects runs them alongside the Boons', at potency 1, for the whole run.
    /// </summary>
    [CreateAssetMenu(menuName = "Gladiator/Traits/Effects", fileName = "TR_Effects")]
    public sealed class EffectsTrait : GladiatorTrait
    {
        public BoonEffect[] effects = System.Array.Empty<BoonEffect>();

        public override void Install(GameObject player)
        {
            var c = player.AddComponent<TraitEffects>();
            c.trait = this;
        }
    }

    /// <summary>Runtime half of EffectsTrait: just the reference BoonEffects reads.</summary>
    public sealed class TraitEffects : MonoBehaviour
    {
        public EffectsTrait trait;
    }
}
