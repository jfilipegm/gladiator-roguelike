namespace Gladiator.Core
{
    /// <summary>Build-wide constants.</summary>
    public static class GameInfo
    {
        /// <summary>Bumped in the PR that cuts a GitHub release; <c>Tools/release</c> refuses a tag that doesn't match.</summary>
        public const string Version = "0.13.0";
        public const int SaveVersion = 1;

        /// <summary>"v0.1.0", or "v0.1.0 dev" in development builds and the Editor.</summary>
        public static string VersionLabel => UnityEngine.Debug.isDebugBuild ? "v" + Version + " dev" : "v" + Version;

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void LogVersion() => UnityEngine.Debug.Log("[Gladiator] " + VersionLabel);

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
