using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// Debug helper for driving the player from the Editor bridge, e.g.
    /// <c>Gladiator.Gameplay.DevInput.Hold(0, 1)</c> … <c>DevInput.Release()</c>. Not used by gameplay.
    /// </summary>
    public sealed class DevInput : IPlayerInputSource
    {
        public static readonly DevInput Instance = new DevInput();
        public PlayerInputFrame Frame;

        public PlayerInputFrame Read()
        {
            var f = Frame;
            Frame.JumpPressed = false;
            Frame.SlidePressed = false;
            return f;
        }

        public static string Hold(float x, float y, bool slide = false)
        {
            Instance.Frame = new PlayerInputFrame { Move = new Vector2(x, y), SlideHeld = slide, SlidePressed = slide };
            if (Player.Main != null) Player.Main.OverrideSource = Instance;
            return "holding";
        }

        public static string Jump() { Instance.Frame.JumpPressed = true; Instance.Frame.JumpHeld = true; return "jump"; }

        public static string Release()
        {
            Instance.Frame = default;
            if (Player.Main != null) Player.Main.OverrideSource = null;
            return "released";
        }
    }
}
