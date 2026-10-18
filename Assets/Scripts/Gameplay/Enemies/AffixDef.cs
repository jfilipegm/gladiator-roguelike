using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// Elite affix (CLAUDE.md §6.3): any enemy + one affix = elite. Elites are bigger, show a ring in the affix colour,
    /// give more XP, and trigger the elite hit-stop. Numbers are data; the special effects (explosion) are code.
    /// </summary>
    [CreateAssetMenu(menuName = "Gladiator/Elite Affix", fileName = "AF_")]
    public sealed class AffixDef : ScriptableObject
    {
        public string id = "armored";
        public string displayName = "Armored";
        [Tooltip("Ring / marker colour (affix palette row).")]
        public Color color = Color.white;
        public float scale = 1.2f;
        public float hpMultiplier = 2f;
        public float speedMultiplier = 1f;
        [Tooltip("Multiplies damage taken (0.7 = takes 30% less).")]
        public float damageTakenMultiplier = 1f;
        public float knockbackMultiplier = 1f;
        [Tooltip("Multiplies attack cooldown and wind-up (0.6 = attacks much faster).")]
        public float attackTimeMultiplier = 1f;
        public float xpMultiplier = 3f;

        [Header("Explosive")]
        public bool explodeOnDeath;
        [Tooltip("Fuse after death before the blast (s) — the ring flashes meanwhile.")]
        public float fuse = 1f;
        public float explosionRadius = 2.6f;
        public float explosionDamageToPlayer = 18f;
        public float explosionDamageToEnemies = 60f;
    }
}
