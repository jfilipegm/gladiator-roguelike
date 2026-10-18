using Gladiator.Core;
using Unity.Cinemachine;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// Free third-person orbit (no lock-on) on Cinemachine 3: OrbitalFollow (sphere, world-space binding) +
    /// RotationComposer + Deoccluder (pulls the camera in front of walls). Look input comes from the shared
    /// player input frame and drives the orbit axes directly; the resulting yaw is the player's movement reference.
    /// Adds a speed-based FOV kick. Ticked centrally (TickGroup.Camera, before CinemachineBrain's LateUpdate).
    /// </summary>
    public sealed class PlayerCameraRig : MonoBehaviour, ITickable
    {
        public CameraConfig config;
        public Player player;
        public PlayerMotor motor;
        public CinemachineCamera vcam;
        public Transform lookTarget;
        public HitFeedbackConfig feedback;

        CinemachineOrbitalFollow _orbit;
        CinemachineRotationComposer _composer;
        CinemachineDeoccluder _deoccluder;
        CinemachineDecollider _decollider;
        float _yaw, _pitch, _fov;

        public float Yaw => _yaw;
        public float Pitch => _pitch;

        void Awake()
        {
            _orbit = vcam.GetComponent<CinemachineOrbitalFollow>();
            _composer = vcam.GetComponent<CinemachineRotationComposer>();
            _deoccluder = vcam.GetComponent<CinemachineDeoccluder>();
            _decollider = vcam.GetComponent<CinemachineDecollider>();
            _yaw = player != null ? player.transform.eulerAngles.y : 0f;
            _pitch = config.startPitch;
            _fov = config.baseFov;
            ApplyConfig();
        }

        void OnEnable() => GameLoop.Register(this, TickGroup.Camera);
        void OnDisable() => GameLoop.Unregister(this);

        public void SetAngles(float yaw, float pitch)
        {
            _yaw = yaw;
            _pitch = Mathf.Clamp(pitch, config.minPitch, config.maxPitch);
            _orbit.HorizontalAxis.Value = _yaw;
            _orbit.VerticalAxis.Value = _pitch;
            if (player != null) player.ReferenceYaw = _yaw;
        }

        public void Tick(float dt, float unscaledDt)
        {
#if UNITY_EDITOR
            ApplyConfig(); // live tuning while playing
#endif
            var c = config;
            var input = player.Input;
            float inv = c.invertY ? -1f : 1f;
            _yaw += input.LookMouse.x * c.mouseSensitivity + input.LookStick.x * c.stickYawSpeed * unscaledDt;
            _pitch -= inv * (input.LookMouse.y * c.mouseSensitivity + input.LookStick.y * c.stickPitchSpeed * unscaledDt);
            _pitch = Mathf.Clamp(_pitch, c.minPitch, c.maxPitch);
            if (_yaw > 360f || _yaw < -360f) _yaw %= 360f;

            // Screen shake = small orbit jitter + roll (never a positional offset → can't push into walls).
            ScreenShake.Update(unscaledDt, feedback);
            _orbit.HorizontalAxis.Value = _yaw + ScreenShake.Yaw;
            _orbit.VerticalAxis.Value = Mathf.Clamp(_pitch + ScreenShake.Pitch, c.minPitch, c.maxPitch);
            player.ReferenceYaw = _yaw;

            // Speed FOV kick (slides/slide-jumps feel fast).
            float speed = motor != null ? motor.HorizontalSpeed : 0f;
            float t = Mathf.InverseLerp(c.speedFovRange.x, c.speedFovRange.y, speed);
            float targetFov = c.baseFov + c.speedFovKick * t * t;
            _fov = Mathf.Lerp(_fov, targetFov, 1f - Mathf.Exp(-c.fovSharpness * unscaledDt));
            var lens = vcam.Lens;
            lens.FieldOfView = _fov;
            lens.Dutch = ScreenShake.Roll;
            vcam.Lens = lens;
        }

        void ApplyConfig()
        {
            var c = config;
            if (_orbit != null)
            {
                _orbit.OrbitStyle = CinemachineOrbitalFollow.OrbitStyles.Sphere;
                _orbit.Radius = c.distance;
                var ts = _orbit.TrackerSettings;
                ts.BindingMode = Unity.Cinemachine.TargetTracking.BindingMode.WorldSpace;
                ts.PositionDamping = c.followDamping;
                _orbit.TrackerSettings = ts;
                _orbit.HorizontalAxis.Wrap = true;
                _orbit.HorizontalAxis.Range = new Vector2(-180f, 180f);
                _orbit.HorizontalAxis.Recentering.Enabled = false;
                _orbit.VerticalAxis.Range = new Vector2(c.minPitch, c.maxPitch);
                _orbit.VerticalAxis.Recentering.Enabled = false;
            }
            if (_composer != null)
            {
                _composer.Damping = Vector2.zero;
                _composer.TargetOffset = Vector3.zero;
            }
            if (_deoccluder != null)
            {
                _deoccluder.CollideAgainst = Layers.EnvironmentMask;
                _deoccluder.MinimumDistanceFromTarget = c.minDistance;
                var a = _deoccluder.AvoidObstacles;
                a.Enabled = true;
                a.CameraRadius = c.cameraRadius;
                a.DistanceLimit = 0f;
                a.MinimumOcclusionTime = 0f;
                a.Strategy = CinemachineDeoccluder.ObstacleAvoidance.ResolutionStrategy.PullCameraForward;
                a.MaximumEffort = 4;
                a.SmoothingTime = 0f;
                a.Damping = c.occlusionReturnDamping;
                a.DampingWhenOccluded = 0f;
                _deoccluder.AvoidObstacles = a;
                var q = _deoccluder.ShotQualityEvaluation;
                q.Enabled = false;
                _deoccluder.ShotQualityEvaluation = q;
            }
            if (_decollider != null)
            {
                // Deoccluder fixes blocked line of sight; Decollider keeps the camera sphere out of surfaces it grazes
                // (platform edges, mounds) and above walkable ground.
                _decollider.CameraRadius = c.cameraRadius;
                var d = _decollider.Decollision;
                d.Enabled = true;
                d.ObstacleLayers = Layers.EnvironmentMask;
                d.Damping = 0f;
                d.SmoothingTime = 0f;
                _decollider.Decollision = d;
                var tr = _decollider.TerrainResolution;
                tr.Enabled = true;
                tr.TerrainLayers = Layers.EnvironmentMask;
                tr.MaximumRaycast = 8f;
                tr.Damping = 0f;
                _decollider.TerrainResolution = tr;
            }
            if (lookTarget != null) lookTarget.localPosition = new Vector3(0f, c.targetHeight, 0f);
        }
    }
}
