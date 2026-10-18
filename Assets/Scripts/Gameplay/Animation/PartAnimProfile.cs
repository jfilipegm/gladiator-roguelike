using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>Procedural rigid-part animation numbers (CLAUDE.md §7.2). One profile per body type.</summary>
    [CreateAssetMenu(menuName = "Gladiator/Config/Part Anim Profile", fileName = "PartAnimProfile")]
    public sealed class PartAnimProfile : ScriptableObject
    {
        [Header("Run")]
        [Tooltip("Metres per full leg cycle (two steps). Smaller = faster, choppier steps.")]
        public float strideLength = 1.5f;
        public float legSwing = 45f;
        public float armSwing = 38f;
        [Tooltip("Sword-arm swing relative to armSwing.")]
        [Range(0f, 1f)] public float weaponArmSwingScale = 0.4f;
        public float bobHeight = 0.07f;
        public float runLean = 12f;
        [Tooltip("Speed (m/s) treated as full run for amplitudes.")]
        public float fullRunSpeed = 7.5f;

        [Header("Idle")]
        public float idleBobHeight = 0.015f;
        public float idleBobHz = 1.4f;
        public float idleArmSway = 4f;

        [Header("Air")]
        public float airLegTuck = 30f;
        public float airArmRaise = 55f;
        public float jumpStretch = 0.14f;
        public float jumpStretchTime = 0.18f;

        [Header("Slide")]
        public float slideBodyLean = -28f;
        public float slideLegForward = 70f;
        public float slideDrop = 0.32f;
        public float slideArmBack = 35f;

        [Header("Land")]
        public float landSquash = 0.2f;
        public float landSquashTime = 0.16f;

        [Header("Attack (weapon arm)")]
        [Tooltip("Sword arm yaw sweep across the swing (deg): from +start to -end.")]
        public float swingYawStart = 75f;
        public float swingYawEnd = -95f;
        [Tooltip("Arm raised forward during the swing (deg).")]
        public float swingRaise = 80f;
        public AnimationCurve swingCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Blend")]
        [Tooltip("How fast poses blend (1/s).")]
        public float poseSharpness = 16f;
    }
}
