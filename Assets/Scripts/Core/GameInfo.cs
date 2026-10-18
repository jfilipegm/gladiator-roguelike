namespace Gladiator.Core
{
    /// <summary>Build-wide constants.</summary>
    public static class GameInfo
    {
        public const string Version = "0.0.1";
        public const int SaveVersion = 1;

        /// <summary>Stable 32-bit FNV-1a hash of a content id (same in every session/build — usable in saves and stats).</summary>
        public static int StableId(string id)
        {
            unchecked
            {
                uint h = 2166136261;
                if (id != null) foreach (char c in id) { h ^= c; h *= 16777619; }
                return (int)h;
            }
        }
    }
}
