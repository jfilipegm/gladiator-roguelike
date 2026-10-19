using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>XP curve, pickups and run timing (CLAUDE.md §6.5). Tune freely; log changes in DECISIONS.</summary>
    [CreateAssetMenu(menuName = "Gladiator/Config/Progression", fileName = "ProgressionConfig")]
    public sealed class ProgressionConfig : ScriptableObject
    {
        [Header("XP curve: xpToNext(L) = base + perLevel * (L-1)^exponent")]
        public float baseXp = 5f;
        public float perLevelXp = 4f;
        public float exponent = 1.35f;

        [Header("Pickups")]
        [Tooltip("Gems inside this radius get vacuumed to the player (m).")]
        public float magnetRadius = 2.2f;
        public float collectRadius = 0.7f;
        [Tooltip("Seconds a new gem pops/bounces before the magnet may grab it (keeps the drop readable).")]
        public float magnetDelay = 0.25f;
        public float vacuumStartSpeed = 6f;
        public float vacuumAcceleration = 40f;
        [Tooltip("Gem values per tier: small / medium / large.")]
        public Vector3 gemTierValues = new Vector3(1f, 5f, 25f);
        public int goldPerCoin = 1;
        public int maxPickups = 700;

        [Header("Run")]
        [Tooltip("Seconds until the boss (slice: 8 min). Arenas will override.")]
        public float runDuration = 480f;

        public float XpForLevel(int level) => Mathf.Round(baseXp + perLevelXp * Mathf.Pow(Mathf.Max(0, level - 1), exponent));
    }
}
