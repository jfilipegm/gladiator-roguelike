using System;
using Gladiator.Core;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// T1 (docs/tracks/T1-playground.md §1): admin mode, the producer's switch for the test tools in any build. Always on
    /// in the Editor and development builds (as F1 always was); in a release build it's on with `-admin` on the command
    /// line, or once the player has typed ADMIN on the main menu (Toggle; remembered in PlayerPrefs `gladiator.admin`, not
    /// in the save, so it never touches progress). While it's on, F1 opens the admin panel in any arena
    /// (AdminController.Available), and the menu offers every gladiator, skin and arena (MainMenu.IsPickable: a view, never
    /// written) and the PLAYGROUND button. On reads cached values: no allocation, safe to ask every frame.
    /// </summary>
    public static class AdminMode
    {
        /// <summary>The PlayerPrefs key of the typed switch (1 = on).</summary>
        public const string PrefKey = "gladiator.admin";
        /// <summary>The command-line switch that turns it on for one launch.</summary>
        public const string CommandLineFlag = "-admin";
        /// <summary>The letters typed on the main menu, in order (AdminCode).</summary>
        public const string Code = "ADMIN";
        /// <summary>The most seconds between two of the code's keys.</summary>
        public const float CodeGap = 3f;

        /// <summary>Toggle flipped the switch (listeners re-read On).</summary>
        public static event Action Changed;

        static int _saved = -1, _cmd = -1; // -1 = not read yet

        /// <summary>Admin mode: the build or the command line forces it, or the player switched it on.</summary>
        public static bool On => Forced || Switched;

        /// <summary>On whatever the switch says: the Editor, a development build, or `-admin`.</summary>
        public static bool Forced => BuildAlwaysOn || FromCommandLine;

        /// <summary>The Editor and development builds (`Debug.isDebugBuild`) always have it (unless a test says release).</summary>
        public static bool BuildAlwaysOn => !SimulateRelease && (Application.isEditor || Debug.isDebugBuild);

        /// <summary>`-admin` is on the command line (CommandLineOverride in tests).</summary>
        public static bool FromCommandLine
        {
            get
            {
                if (CommandLineOverride.HasValue) return CommandLineOverride.Value;
                if (_cmd < 0) _cmd = CommandLine.Has(CommandLineFlag) ? 1 : 0;
                return _cmd == 1;
            }
        }

        /// <summary>The player's switch (typed ADMIN), as remembered in PlayerPrefs.</summary>
        public static bool Switched
        {
            get
            {
                if (_saved < 0) _saved = PlayerPrefs.GetInt(Key, 0) != 0 ? 1 : 0;
                return _saved == 1;
            }
        }

        /// <summary>Flips the player's switch, saves it and raises Changed. A forced build stays on either way (the
        /// switch still flips, for the next release launch).</summary>
        public static void Toggle()
        {
            _saved = Switched ? 0 : 1;
            PlayerPrefs.SetInt(Key, _saved);
            PlayerPrefs.Save();
            Debug.Log("[Admin] admin mode switched " + (_saved == 1 ? "on" : "off") + (Forced ? " (this build keeps it on)" : ""));
            Changed?.Invoke();
        }

        /// <summary>The toast typing the code shows: "ADMIN MODE ON" / "ADMIN MODE OFF", and why it stays on when the
        /// build or `-admin` keeps it on.</summary>
        public static string StatusText =>
            !On ? "ADMIN MODE OFF"
            : Switched ? "ADMIN MODE ON"
            : BuildAlwaysOn ? "ADMIN MODE ON (DEV BUILD)"
            : "ADMIN MODE ON (-admin)";

        // ---- tests ----------------------------------------------------------------------------------------------------

        /// <summary>Tests: pretend this is a release build (the Editor / development clause off), so On follows `-admin`
        /// and the switch only.</summary>
        public static bool SimulateRelease;
        /// <summary>Tests: stands in for `-admin` on the command line (null = read the command line).</summary>
        public static bool? CommandLineOverride;
        /// <summary>Tests: the PlayerPrefs key the switch lives under (null = PrefKey), so a test never flips the
        /// player's own switch. Call Reload after changing it.</summary>
        public static string PrefKeyOverride;

        static string Key => string.IsNullOrEmpty(PrefKeyOverride) ? PrefKey : PrefKeyOverride;

        /// <summary>Forget the cached switch and command line (they're read again, as on a new launch).</summary>
        public static void Reload() { _saved = -1; _cmd = -1; }

        /// <summary>Tests: back to the real build, command line and key (and forget the cache).</summary>
        public static void ResetTestHooks()
        {
            SimulateRelease = false;
            CommandLineOverride = null;
            PrefKeyOverride = null;
            Reload();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            ResetTestHooks();
            Changed = null;
        }
    }

    /// <summary>
    /// T1: the typed code's progress (A-D-M-I-N in order, at most AdminMode.CodeGap seconds between two keys; no hint
    /// on screen). The menu feeds it each key press: one of the code's letters, or any other char for any other key (a
    /// wrong key starts over, an A starts again at once). A struct with two fields: no allocation.
    /// </summary>
    public struct AdminCode
    {
        int _next;
        float _last;

        /// <summary>How many of the code's letters are typed so far.</summary>
        public int Progress => _next;

        /// <summary>A key press at `time` (unscaled seconds). True when it completes the code (it then starts over).</summary>
        public bool Feed(char c, float time)
        {
            if (_next > 0 && time - _last > AdminMode.CodeGap) _next = 0;
            _last = time;
            c = char.ToUpperInvariant(c);
            if (c == AdminMode.Code[_next])
            {
                if (++_next < AdminMode.Code.Length) return false;
                _next = 0;
                return true;
            }
            _next = c == AdminMode.Code[0] ? 1 : 0;
            return false;
        }

        public void Reset() => _next = 0;
    }
}
