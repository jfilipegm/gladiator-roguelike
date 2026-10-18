namespace Gladiator.Core
{
    /// <summary>Live counts written by gameplay systems, read by FrameStatsRecorder / debug HUD.</summary>
    public static class DevCounters
    {
        public static int Enemies;
        public static int Projectiles;
        public static int Pickups;
        public static int DamageNumbers;

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { Enemies = Projectiles = Pickups = DamageNumbers = 0; }
    }
}
