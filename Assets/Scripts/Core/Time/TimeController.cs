using UnityEngine;

namespace Gladiator.Core
{
    /// <summary>
    /// Owns Time.timeScale. Pause (level-up, menus) beats hit-stop beats normal speed.
    /// Hit-stop runs on unscaled time so it always ends.
    /// </summary>
    public static class TimeController
    {
        static int _pauseCount;
        static float _hitStopUntil;   // unscaled time
        static float _hitStopScale = 0.05f;

        public static bool IsPaused => _pauseCount > 0;
        public static bool InHitStop => UnityEngine.Time.unscaledTime < _hitStopUntil;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _pauseCount = 0;
            _hitStopUntil = 0f;
            UnityEngine.Time.timeScale = 1f;
        }

        public static void PushPause() { _pauseCount++; Apply(); }
        public static void PopPause() { _pauseCount = Mathf.Max(0, _pauseCount - 1); Apply(); }

        /// <summary>Short freeze-frame. Overlapping requests extend to the latest end time.</summary>
        public static void HitStop(float seconds, float scale = 0.05f)
        {
            if (seconds <= 0f) return;
            float end = UnityEngine.Time.unscaledTime + seconds;
            if (end > _hitStopUntil) _hitStopUntil = end;
            _hitStopScale = scale;
            Apply();
        }

        /// <summary>Call once per frame (GameLoop does it via TimeTicker) to release hit-stop.</summary>
        public static void Update() => Apply();

        static void Apply()
        {
            float s = IsPaused ? 0f : InHitStop ? _hitStopScale : 1f;
            if (!Mathf.Approximately(UnityEngine.Time.timeScale, s)) UnityEngine.Time.timeScale = s;
        }
    }
}
