using System;
using Gladiator.Core;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// A tag synergy (Phase 5.5, docs/EVOLUTIONS.md): owning `count` distinct upgrades that carry `tag` switches it on
    /// for the rest of the run. `mods` are added to the player's stats once; `effects` run in BoonEffects like a Boon's
    /// (potency 1).
    /// </summary>
    [CreateAssetMenu(menuName = "Gladiator/Tag Synergy", fileName = "SY_")]
    public sealed class TagSynergyDef : ScriptableObject
    {
        public string id = "synergy";
        public string displayName = "Synergy";
        [Tooltip("Announced when it switches on (a constant string, e.g. \"FIRE SYNERGY: WILDFIRE!\").")]
        public string announce = "SYNERGY!";
        [TextArea] public string description = "";
        public UpgradeTag tag;
        [Min(1)] public int count = 3;
        [Tooltip("The pixel-icon atlas and this synergy's row in it; set by IconSetup.")]
        public Texture2D icon;
        public int iconRow = -1;
        public StatMod[] mods = Array.Empty<StatMod>();
        public BoonEffect[] effects = Array.Empty<BoonEffect>();
    }
}
