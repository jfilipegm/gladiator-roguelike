using System;
using Gladiator.Core;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// A playable gladiator as data (CLAUDE.md §6.7): base stats, the rigid-part model and its feel (capsule, camera,
    /// animation), the starting weapon, signature traits, the unlock and the select-screen text. GladiatorLoadout applies
    /// it to the player at spawn; a new gladiator is a new asset plus, for a new mechanic, a GladiatorTrait.
    /// </summary>
    [CreateAssetMenu(menuName = "Gladiator/Gladiator", fileName = "GL_")]
    public sealed class GladiatorDef : ScriptableObject
    {
        [Serializable]
        public struct BaseStat
        {
            public StatId stat;
            public float value;
        }

        public string id = "nobody";
        public string displayName = "The Nobody";
        [TextArea] public string description;
        [Tooltip("Overrides of StatInfo.DefaultBase (stats not listed keep the default).")]
        public BaseStat[] baseStats = Array.Empty<BaseStat>();

        [Header("Look and feel (empty = keep the player prefab's)")]
        [Tooltip("Rigid-part model (CH_*.fbx) with parts named Body, Head, ArmL/R, LegL/R so the animator drives it unchanged.")]
        public GameObject model;
        [Tooltip("Capsule size, jump, speeds… A smaller gladiator needs its own (capsuleHeight).")]
        public MovementConfig movement;
        [Tooltip("Run cycle / swing amplitudes.")]
        public PartAnimProfile animProfile;
        [Tooltip("Camera look-at height above the feet (m).")]
        public float cameraTargetHeight = 1.25f;

        [Header("Kit")]
        public WeaponDef startingWeapon;
        [Tooltip("Signature mechanics (Scavenger, Trap Slide…), installed on the player at spawn.")]
        public GladiatorTrait[] traits = Array.Empty<GladiatorTrait>();

        [Header("Unlock and select screen")]
        [Tooltip("Empty = available from the start. Locked gladiators show as a silhouette with the unlock's hint.")]
        public UnlockDef unlock;
        [Tooltip("Short line under the stats on the select card (empty = \"Starts with <weapon>\").")]
        public string selectLine;
        [Tooltip("Strength / weakness line on the select card.")]
        public string tradeoff;

        public string SelectLine => !string.IsNullOrEmpty(selectLine) ? selectLine
            : startingWeapon != null ? "Starts with " + startingWeapon.displayName : "";

        public float BaseValue(StatId stat, float fallback)
        {
            foreach (var b in baseStats) if (b.stat == stat) return b.value;
            return fallback;
        }
    }
}
