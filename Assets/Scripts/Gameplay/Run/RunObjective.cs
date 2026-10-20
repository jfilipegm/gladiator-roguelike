using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// A timed goal the HUD shows under the run timer while it's on (Phase 5.6: the Bookmaker's wager). One at a time;
    /// `Label` must be a constant string (the HUD formats the numbers without allocating).
    /// </summary>
    public static class RunObjective
    {
        public static bool Active { get; private set; }
        public static string Label { get; private set; }
        public static int Current { get; private set; }
        public static int Target { get; private set; }
        public static float SecondsLeft { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Clear();

        public static void Begin(string label, int target, float seconds)
        {
            Active = true;
            Label = label;
            Current = 0;
            Target = target;
            SecondsLeft = seconds;
        }

        public static void Set(int current, float secondsLeft)
        {
            Current = current;
            SecondsLeft = Mathf.Max(0f, secondsLeft);
        }

        public static void Clear()
        {
            Active = false;
            Label = null;
            Current = Target = 0;
            SecondsLeft = 0f;
        }
    }
}
