using Gladiator.Core;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// Mud patches (Slave Pit, M3.2): slow the grounded player and the enemies walking through them. Sliding keeps its
    /// own momentum, so a slide is the way across. Static circles from the arena layout; enemies query SpeedAt each tick.
    /// </summary>
    public sealed class MudZones : MonoBehaviour, ITickable
    {
        public static MudZones Instance { get; private set; }

        [Tooltip("x = centre x, y = centre z, z = radius.")]
        public Vector3[] circles = System.Array.Empty<Vector3>();
        [Range(0.2f, 1f)] public float playerSpeed = 0.62f;
        [Range(0.2f, 1f)] public float enemySpeed = 0.7f;

        PlayerMotor _motor;

        public bool PlayerInside { get; private set; }

        void Awake() => Instance = this;
        void OnEnable() => GameLoop.Register(this, TickGroup.Player, -10); // before the motor moves

        void OnDisable()
        {
            GameLoop.Unregister(this);
            if (Instance == this) Instance = null;
            if (_motor != null) _motor.SpeedMultiplier = 1f;
        }

        public bool Inside(Vector3 p)
        {
            for (int i = 0; i < circles.Length; i++)
            {
                float dx = p.x - circles[i].x, dz = p.z - circles[i].y, r = circles[i].z;
                if (dx * dx + dz * dz < r * r) return true;
            }
            return false;
        }

        /// <summary>Enemy move speed multiplier at `p` (1 outside mud).</summary>
        public float SpeedAt(Vector3 p) => Inside(p) ? enemySpeed : 1f;

        public void Tick(float dt, float unscaledDt)
        {
            if (_motor == null && Player.Main != null) _motor = Player.Main.GetComponent<PlayerMotor>();
            if (_motor == null) return;
            PlayerInside = _motor.IsGrounded && Inside(_motor.transform.position);
            _motor.SpeedMultiplier = PlayerInside ? playerSpeed : 1f;
        }
    }
}
