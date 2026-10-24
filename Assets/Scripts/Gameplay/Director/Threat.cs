using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// Note 39 (the producer, v0.28.0: "you can get super strong with some builds, the mobs should get stronger with the
    /// player"): the horde grows with the player's level. Enemies spawn with more HP and speed, and everything that hurts
    /// the player hits harder, by DirectorConfig's threat numbers. The level is the measure because a strong build levels
    /// faster too, and the player can see it. Tiers (DirectorConfig.threatTiers) are announced. No config (the Playground,
    /// arenas without a Director) = no threat.
    /// </summary>
    public static class Threat
    {
        public static DirectorConfig Config;

        static int Level => RunState.Current != null ? RunState.Current.Level : 1;

        /// <summary>Non-boss enemies' HP at spawn.</summary>
        public static float HpMul => HpMulAt(Config, Level);

        /// <summary>Everything that hurts the player (bosses included).</summary>
        public static float DamageMul => DamageMulAt(Config, Level);

        /// <summary>Non-boss enemies' move speed at spawn (capped: past it the horde stops being readable).</summary>
        public static float SpeedMul => SpeedMulAt(Config, Level);

        public static float HpMulAt(DirectorConfig c, int level) =>
            c == null ? 1f : 1f + c.threatHpPerLevel * Mathf.Max(0, level - c.threatFromLevel);

        public static float DamageMulAt(DirectorConfig c, int level) =>
            c == null ? 1f : 1f + c.threatDamagePerLevel * Mathf.Max(0, level - c.threatDamageFromLevel);

        public static float SpeedMulAt(DirectorConfig c, int level) =>
            c == null ? 1f : 1f + Mathf.Min(c.threatSpeedMax, c.threatSpeedPerLevel * Mathf.Max(0, level - c.threatDamageFromLevel));

        /// <summary>How many of DirectorConfig.threatTiers the player's level has reached.</summary>
        public static int Tier
        {
            get
            {
                if (Config == null || Config.threatTiers == null) return 0;
                int n = 0, level = Level;
                for (int i = 0; i < Config.threatTiers.Length; i++) if (level >= Config.threatTiers[i]) n++;
                return n;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Config = null;
    }
}
