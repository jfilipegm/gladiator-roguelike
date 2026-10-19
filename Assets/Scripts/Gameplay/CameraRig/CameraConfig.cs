using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>Third-person orbit camera feel (CLAUDE.md §4, §5). Live-tunable in Play mode.</summary>
    [CreateAssetMenu(menuName = "Gladiator/Config/Camera", fileName = "CameraConfig")]
    public sealed class CameraConfig : ScriptableObject
    {
        [Header("Orbit")]
        [Tooltip("Distance from the look target (m).")]
        public float distance = 6.2f;
        [Tooltip("Look target height above the player's feet (m).")]
        public float targetHeight = 1.25f;
        public float minPitch = -8f;
        public float maxPitch = 70f;
        public float startPitch = 20f;

        [Header("Input")]
        [Tooltip("Degrees per mouse pixel.")]
        public float mouseSensitivity = 0.11f;
        [Tooltip("Stick yaw speed (deg/s at full deflection).")]
        public float stickYawSpeed = 220f;
        [Tooltip("Stick pitch speed (deg/s at full deflection).")]
        public float stickPitchSpeed = 130f;
        public bool invertY = false;

        [Header("Follow")]
        [Tooltip("Position damping per axis (s). Small = tight.")]
        public Vector3 followDamping = new Vector3(0.05f, 0.12f, 0.05f);

        [Header("Lens")]
        [Tooltip("Reference only: the resting FOV is the player's option (GameOptions, default 62, 50–90).")]
        public float baseFov = 62f;
        [Tooltip("Extra FOV at slideMaxSpeed (speed feel).")]
        public float speedFovKick = 12f;
        [Tooltip("Speed (m/s) where the FOV kick starts / is full.")]
        public Vector2 speedFovRange = new Vector2(8f, 20f);
        [Tooltip("FOV smoothing (1/s).")]
        public float fovSharpness = 5f;

        [Header("Walls")]
        [Tooltip("Camera sphere radius used to keep it out of geometry. Keep it below the player capsule radius (0.32) so the target itself is always a legal camera spot.")]
        public float cameraRadius = 0.25f;
        [Tooltip("Never pull the camera closer than this to the target.")]
        public float minDistance = 0.05f;
        [Tooltip("Return-to-distance damping after an occlusion clears (s).")]
        public float occlusionReturnDamping = 0.35f;
    }
}
