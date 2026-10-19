using System;
using Gladiator.Core;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// A playable gladiator as data (CLAUDE.md §6.7). M2.4 only needs the base stats; the starting weapon, signature
    /// behaviour, unlock and select-screen data join in M3.5.
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
    }
}
