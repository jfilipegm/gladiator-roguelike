using UnityEngine;

namespace Gladiator.Core
{
    /// <summary>
    /// Owns Time.timeScale. Pause (level-up, menus) beats hit-stop beats normal speed. Slow motion (death beat) and
    /// the dev game-speed setting multiply on top. Hit-stop runs on unscaled time so it always ends.
    /// </summary>
    public static class TimeController
    {
        static int _pauseCount;
        static float _hitStopUntil;   // unscaled time
        static float _hitStopScale = 0.05f;
        static float _slowMotion = 1f;
        static float _gameSpeed = 1f;

        /// <summary>Hit-stops started and real seconds spent in them this run (bot log, tests).</summary>
        public static int HitStopCount { get; private set; }
        public static float HitStopSeconds { get; private set; }

        public static bool IsPaused => _pauseCount > 0;
        public static bool InHitStop => UnityEngine.Time.unscaledTime < _hitStopUntil;

        /// <summary>Dev/admin global speed (0.25×–2×). Survives scene reloads within a session.</summary>
        public static float GameSpeed
        {
            get => _gameSpeed;
            set { _gameSpeed = Mathf.Clamp(value, 0.05f, 4f); Apply(); }
        }

        /// <summary>Gameplay slow motion (e.g. the death beat); 1 = off.</summary>
        public static float SlowMotion
        {
            get => _slowMotion;
            set { _slowMotion = Mathf.Clamp(value, 0.01f, 1f); Apply(); }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _gameSpeed = 1f;
            ResetForNewRun();
        }

        /// <summary>Clears pauses, hit-stop and slow motion (scene reload / retry). Keeps GameSpeed.</summary>
        public static void ResetForNewRun()
        {
            _pauseCount = 0;
            _hitStopUntil = 0f;
            _slowMotion = 1f;
            HitStopCount = 0;
            HitStopSeconds = 0f;
            Apply();
        }

        public static void PushPause() { _pauseCount++; Apply(); }
        public static void PopPause() { _pauseCount = Mathf.Max(0, _pauseCount - 1); Apply(); }

        /// <summary>Short freeze-frame. Overlapping requests extend to the latest end time.</summary>
        public static void HitStop(float seconds, float scale = 0.05f)
        {
            if (seconds <= 0f) return;
            float end = UnityEngine.Time.unscaledTime + seconds;
            if (!InHitStop) HitStopCount++;
            if (end > _hitStopUntil) _hitStopUntil = end;
            _hitStopScale = scale;
            Apply();
        }

        /// <summary>Call once per frame (GameLoop does it via TimeTicker) to release hit-stop.</summary>
        public static void Update()
        {
            if (InHitStop && !IsPaused) HitStopSeconds += UnityEngine.Time.unscaledDeltaTime;
            Apply();
        }

        static void Apply()
        {
            float s = IsPaused ? 0f : (InHitStop ? _hitStopScale : 1f) * _slowMotion * _gameSpeed;
            if (!Mathf.Approximately(UnityEngine.Time.timeScale, s)) UnityEngine.Time.timeScale = s;
        }
    }
}
