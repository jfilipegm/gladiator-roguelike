using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>Every juice number (CLAUDE.md §5): flash, knockback, hit-stop, numbers, death pop, shake.</summary>
    [CreateAssetMenu(menuName = "Gladiator/Config/Hit Feedback", fileName = "HitFeedbackConfig")]
    public sealed class HitFeedbackConfig : ScriptableObject
    {
        [Header("Enemy hit")]
        public float flashDuration = 0.08f;
        [Tooltip("Knockback velocity decay (1/s).")]
        public float knockbackDecay = 9f;
        [Tooltip("Lean-back on hit (deg) and its recovery time (s).")]
        public float hitRecoilAngle = 22f;
        public float hitRecoilTime = 0.18f;

        [Header("Hit-stop (only crit / elite / boss — keep it tiny)")]
        public float hitStopOnCrit = 0.035f;
        public float hitStopOnElite = 0.06f;
        [Tooltip("Min seconds between elite-hit hit-stops (fast weapons would otherwise stutter constantly).")]
        public float eliteHitStopInterval = 0.35f;
        public float hitStopOnPlayerHit = 0.05f;

        [Header("Death pop")]
        public float deathPopTime = 0.12f;
        public int deathPopParticles = 9;
        public float deathPopSpeed = 4.5f;
        public float deathPopSize = 0.16f;
        public Color deathPopColor = new Color(0.92f, 0.86f, 0.74f);
        public Color deathPopAccent = new Color(0.91f, 0.23f, 0.56f);

        [Header("Damage numbers")]
        public float numberLifetime = 0.7f;
        public float numberRise = 1.4f;
        public float numberScale = 1f;
        public float critNumberScale = 1.55f;
        [Tooltip("Status ticks (Burn, Bleed…): smaller numbers in the status colour.")]
        public float statusNumberScale = 0.72f;
        public Color numberColor = Color.white;
        public Color critNumberColor = new Color(1f, 0.82f, 0.29f);
        [Tooltip("Hit soaked by a shield (grey, smaller): tells you to flank.")]
        public Color blockedNumberColor = new Color(0.62f, 0.66f, 0.7f);
        public float blockedNumberScale = 0.8f;
        [Tooltip("Damage the PLAYER takes — orange-red: hurts you.")]
        public Color playerDamageColor = new Color(1f, 0.33f, 0.2f);

        [Header("Screen shake (trauma 0..1; scaled by the options slider)")]
        [Header("Player hurt (screen edges, orange-red)")]
        [Tooltip("Edge flash opacity when the player is hit (scaled up for big hits).")]
        [Range(0f, 1f)] public float hurtFlashAlpha = 0.32f;
        [Tooltip("Seconds for the flash to fade.")]
        public float hurtFlashTime = 0.4f;
        [Tooltip("Below this HP fraction the edges pulse.")]
        [Range(0f, 1f)] public float lowHpThreshold = 0.3f;
        [Range(0f, 1f)] public float lowHpPulseAlpha = 0.2f;
        [Tooltip("Pulses per second (a slow heartbeat).")]
        public float lowHpPulseRate = 1.2f;

        public float shakeOnCrit = 0.12f;
        public float shakeOnPlayerHit = 0.35f;
        public float shakeOnKill = 0f;
        public float shakeDecay = 2.2f;
        public float shakeMaxRoll = 2.5f;
        public float shakeMaxAngle = 0.7f;
        public float shakeFrequency = 22f;
    }
}
