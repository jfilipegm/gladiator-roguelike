using System;

namespace Gladiator.Core
{
    /// <summary>Reads -flag / -key value pairs from the process command line (standalone builds, batch runs).</summary>
    public static class CommandLine
    {
        static string[] _args;
        static string[] Args => _args ??= Environment.GetCommandLineArgs();

        public static bool Has(string flag)
        {
            foreach (var a in Args)
                if (string.Equals(a, flag, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        public static string Get(string key, string fallback = null)
        {
            var a = Args;
            for (int i = 0; i < a.Length - 1; i++)
                if (string.Equals(a[i], key, StringComparison.OrdinalIgnoreCase)) return a[i + 1];
            return fallback;
        }

        public static int GetInt(string key, int fallback) => int.TryParse(Get(key), out var v) ? v : fallback;
        public static float GetFloat(string key, float fallback) =>
            float.TryParse(Get(key), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : fallback;
    }
}
