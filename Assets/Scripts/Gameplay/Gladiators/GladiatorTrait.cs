using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// A gladiator's signature mechanic as data (CLAUDE.md §6.7 "GladiatorBehaviour"): the asset holds the numbers and
    /// installs the runtime component that does the work. GladiatorLoadout calls Install once, at spawn, before the
    /// player's other components wake.
    /// </summary>
    public abstract class GladiatorTrait : ScriptableObject
    {
        public string id = "trait";
        public string displayName = "Trait";
        [TextArea] public string description;

        /// <summary>Adds and configures this trait's component on the player.</summary>
        public abstract void Install(GameObject player);
    }
}
