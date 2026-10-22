using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// A gladiator's alternative look (R6, docs/tracks/R7-design.md §4): the same rigid-part model exported with some
    /// palette swatches swapped in Blender (BlenderSrc/characters/skins.json), so the silhouette, the parts and the
    /// animation are the gladiator's own and GladiatorLoadout gives it the one shared character material. Cosmetic only:
    /// it changes nothing in play. Owned when its unlock (Market or a champion challenge) is earned, or free with none;
    /// the equipped one per gladiator lives in SaveData.equippedSkins. Made by Gladiator/Setup/Gladiators/Create Skin Data.
    /// </summary>
    [CreateAssetMenu(menuName = "Gladiator/Skin", fileName = "SK_")]
    public sealed class SkinDef : ScriptableObject
    {
        [Tooltip("<gladiator id>_<name in snake case> (nobody_freedman): MetaProgress finds a gladiator's equipped skins by the prefix.")]
        public string id;
        public string displayName;
        [TextArea] public string description;
        [Tooltip("Whose skin this is. A skin is never applied to anyone else.")]
        public GladiatorDef gladiator;
        [Tooltip("The remapped model (Characters/Skins/CH_*_<Skin>.fbx): the gladiator's part names, no materials of its own.")]
        public GameObject model;
        [Tooltip("Head and shoulders, 128 px (baked by Gladiator/Setup/UI/Bake Portraits): the roster tile while equipped.")]
        public Sprite portrait;
        [Tooltip("Empty = free.")]
        public UnlockDef unlock;
        [Tooltip("Three palette colours for the select screen's skin strip.")]
        public Color[] chips = new Color[3];

        /// <summary>Is `skinId` one of `gladiatorId`'s skins by the naming rule ("nobody_freedman" is the Nobody's)?</summary>
        public static bool IdBelongsTo(string skinId, string gladiatorId) =>
            !string.IsNullOrEmpty(skinId) && !string.IsNullOrEmpty(gladiatorId) && skinId.Length > gladiatorId.Length + 1
            && skinId[gladiatorId.Length] == '_' && skinId.StartsWith(gladiatorId, System.StringComparison.Ordinal);
    }
}
