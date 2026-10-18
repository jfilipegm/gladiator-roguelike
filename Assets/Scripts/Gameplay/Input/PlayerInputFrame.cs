using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>One frame of player intent. Produced by a human (Input System) or by PlaytestBot.</summary>
    public struct PlayerInputFrame
    {
        /// <summary>Move intent, magnitude 0..1, relative to the camera yaw (x = right, y = forward).</summary>
        public Vector2 Move;
        /// <summary>Mouse delta in pixels this frame.</summary>
        public Vector2 LookMouse;
        /// <summary>Right stick, -1..1 (rate input).</summary>
        public Vector2 LookStick;
        public bool JumpPressed;
        public bool JumpHeld;
        public bool SlidePressed;
        public bool SlideHeld;
        public bool PausePressed;
    }

    public interface IPlayerInputSource
    {
        /// <summary>Called once per gameplay tick by the player.</summary>
        PlayerInputFrame Read();
    }
}
