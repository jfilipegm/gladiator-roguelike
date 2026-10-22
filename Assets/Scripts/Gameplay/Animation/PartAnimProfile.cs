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
        [Tooltip("After idleHeadDelay s standing still (and not attacking), the head lifts this many degrees (Asterion looks at the sky). 0 = off.")]
        public float idleHeadLift = 0f;
        public float idleHeadDelay = 1.2f;

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
        [Tooltip("An overhead chop instead of a sideways sweep: the arm pitches from chopPitchStart (over the head) to " +
                 "chopPitchEnd (down in front), and the body bends into it. The Murder Machine's cleaver.")]
        public bool overheadChop;
        public float chopPitchStart = 165f;
        public float chopPitchEnd = 35f;
        [Tooltip("The left arm follows the weapon arm (a two-handed grip).")]
        public bool twoHanded;
        [Tooltip("How much of the arm's swing yaw the upper body twists along with it.")]
        public float bodyTwistScale = 0.2f;

        [Header("Blend")]
        [Tooltip("How fast poses blend (1/s).")]
        public float poseSharpness = 16f;
    }
}
