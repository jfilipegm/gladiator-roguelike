using Gladiator.Core;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// M0.3 stand-in so the bot/profiling pipeline can be verified before the real controller (M1.1).
    /// Delete once PlayerMotor exists.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class DevKinematicMover : MonoBehaviour, ITickable
    {
        public float speed = 7f;
        public float jumpSpeed = 7f;
        public float gravity = 25f;

        CharacterController _cc;
        IPlayerInputSource _input;
        HumanInputSource _human;
        float _vy;

        void OnEnable()
        {
            _cc = GetComponent<CharacterController>();
            GameLoop.Register(this, TickGroup.Player);
        }

        void OnDisable()
        {
            GameLoop.Unregister(this);
            _human?.Dispose();
            _human = null;
        }

        public void Tick(float dt, float unscaledDt)
        {
            if (PlaytestBot.Active != null) { PlaytestBot.Active.Bind(transform); _input = PlaytestBot.Active; }
            else _input ??= _human ??= new HumanInputSource();
            var f = _input.Read();
            var cam = Camera.main;
            float yaw = cam != null ? cam.transform.eulerAngles.y : 0f;
            Vector3 move = Quaternion.Euler(0f, yaw, 0f) * new Vector3(f.Move.x, 0f, f.Move.y) * speed;
            if (_cc.isGrounded) { _vy = -1f; if (f.JumpPressed) _vy = jumpSpeed; }
            _vy -= gravity * dt;
            move.y = _vy;
            _cc.Move(move * dt);
        }
    }
}
