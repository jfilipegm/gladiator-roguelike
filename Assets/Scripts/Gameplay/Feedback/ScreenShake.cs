using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// Trauma-based shake (amplitude = trauma²) expressed as camera roll + tiny orbit jitter — no positional offset,
    /// so it can never push the camera into walls. Restrained by default; the options slider scales it.
    /// </summary>
    public static class ScreenShake
    {
        public static float Trauma { get; private set; }
        /// <summary>Options slider 0..1.</summary>
        public static float UserScale = 1f;

        public static float Roll, Yaw, Pitch;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { Trauma = 0f; Roll = Yaw = Pitch = 0f; }

        public static void Add(float trauma) => Trauma = Mathf.Clamp01(Trauma + trauma);

        public static void Update(float unscaledDt, HitFeedbackConfig c)
        {
            if (c == null) return;
            Trauma = Mathf.Max(0f, Trauma - c.shakeDecay * unscaledDt);
            float a = Trauma * Trauma * UserScale;
            float t = Time.unscaledTime * c.shakeFrequency;
            Roll = c.shakeMaxRoll * a * (Mathf.PerlinNoise(t, 0.1f) * 2f - 1f);
            Yaw = c.shakeMaxAngle * a * (Mathf.PerlinNoise(t, 3.7f) * 2f - 1f);
            Pitch = c.shakeMaxAngle * a * (Mathf.PerlinNoise(t, 7.3f) * 2f - 1f);
        }
    }
}
