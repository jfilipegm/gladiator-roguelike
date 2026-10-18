using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>Every movement feel number (CLAUDE.md §2.3, §4). Tune in the inspector while playing.</summary>
    [CreateAssetMenu(menuName = "Gladiator/Config/Movement", fileName = "MovementConfig")]
    public sealed class MovementConfig : ScriptableObject
    {
        [Header("Ground")]
        [Tooltip("Run speed with full stick (m/s). Always-run; slide/jump are the speed tools.")]
        public float maxSpeed = 7.5f;
        [Tooltip("Acceleration toward the stick direction (m/s²). High = snappy.")]
        public float groundAccel = 70f;
        [Tooltip("Deceleration with no input (m/s²).")]
        public float groundDecel = 55f;
        [Tooltip("How fast speed above maxSpeed bleeds off on the ground (m/s²). Low = keep slide momentum longer.")]
        public float overspeedDecel = 9f;
        [Tooltip("Turn rate while above maxSpeed on the ground (deg/s).")]
        public float highSpeedTurnRate = 540f;
        [Tooltip("Body turn speed toward the movement direction (deg/s).")]
        public float faceTurnSpeed = 900f;

        [Header("Gravity")]
        public float gravity = 30f;
        [Tooltip("Gravity multiplier while falling (snappier arcs).")]
        public float fallGravityMultiplier = 1.35f;
        public float maxFallSpeed = 32f;
        [Tooltip("Downward push while grounded so slopes/steps keep contact (m/s).")]
        public float groundStick = 4f;

        [Header("Jump")]
        [Tooltip("Apex height of a full (held) jump in metres.")]
        public float jumpHeight = 1.7f;
        [Tooltip("Vertical speed multiplier when jump is released early (variable height).")]
        [Range(0.1f, 1f)] public float jumpCutMultiplier = 0.5f;
        [Tooltip("Grace time to still jump after walking off a ledge.")]
        public float coyoteTime = 0.12f;
        [Tooltip("A jump pressed this long before landing still fires.")]
        public float jumpBuffer = 0.15f;
        [Tooltip("Air jumps (double jump comes from an upgrade; base 0).")]
        public int extraJumps = 0;
        [Tooltip("Air-jump height relative to jumpHeight.")]
        public float airJumpHeightMultiplier = 0.85f;

        [Header("Air")]
        [Tooltip("Air acceleration toward the stick (m/s²).")]
        public float airAccel = 30f;
        [Tooltip("Input can accelerate you up to this horizontal speed in the air; momentum above it is kept.")]
        public float airMaxSpeed = 7.5f;
        [Tooltip("Horizontal speed above airMaxSpeed decays at this rate (m/s²).")]
        public float airOverspeedDrag = 2f;
        [Tooltip("Horizontal decay with no input in the air (m/s²).")]
        public float airIdleDrag = 4f;

        [Header("Slide")]
        [Tooltip("Speed added in the travel direction when a slide starts (m/s).")]
        public float slideStartBoost = 4f;
        [Tooltip("Minimum horizontal speed needed to start a slide.")]
        public float slideMinStartSpeed = 3f;
        [Tooltip("Slide ends below this speed (unless still going downhill).")]
        public float slideMinSpeed = 3.5f;
        [Tooltip("Speed loss on flat ground while sliding (m/s²).")]
        public float slideFriction = 5f;
        [Tooltip("How much of the downhill gravity component accelerates a slide (0..1.5).")]
        public float slopeGain = 1.0f;
        public float slideMaxSpeed = 22f;
        [Tooltip("Steering while sliding (deg/s).")]
        public float slideSteerRate = 140f;
        [Tooltip("Cooldown before another slide may start (s).")]
        public float slideCooldown = 0.25f;
        [Tooltip("Hold the button to keep sliding; releasing stands up.")]
        public bool slideRequiresHold = true;
        [Tooltip("Extra horizontal speed when jumping out of a slide (m/s).")]
        public float slideJumpBoost = 1.5f;
        [Tooltip("Landing while holding slide goes straight into a slide (keeps momentum, no start boost).")]
        public bool slideOnLand = true;
        [Tooltip("Capsule height while sliding.")]
        public float slideCapsuleHeight = 0.9f;

        [Header("Capsule")]
        public float capsuleHeight = 1.5f;
        public float capsuleRadius = 0.32f;
        public float stepOffset = 0.35f;
        public float slopeLimit = 50f;

        public float JumpVelocity => Mathf.Sqrt(2f * gravity * jumpHeight);
    }
}
