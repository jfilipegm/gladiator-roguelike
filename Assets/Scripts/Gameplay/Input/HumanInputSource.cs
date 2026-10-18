using System;
using UnityEngine.InputSystem;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// Keyboard+mouse and gamepad input. Actions are defined in code (reproducible, no generated class).
    /// KBM: WASD/arrows move, mouse look, Space jump, Shift/Ctrl/C slide, Esc pause.
    /// Pad: left stick move, right stick look, South jump, East/left trigger slide, Start pause.
    /// </summary>
    public sealed class HumanInputSource : IPlayerInputSource, IDisposable
    {
        readonly InputActionMap _map;
        readonly InputAction _move, _lookMouse, _lookStick, _jump, _slide, _pause;

        public HumanInputSource()
        {
            _map = new InputActionMap("Gameplay");

            _move = _map.AddAction("Move", InputActionType.Value);
            _move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            _move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            _move.AddBinding("<Gamepad>/leftStick");

            _lookMouse = _map.AddAction("LookMouse", InputActionType.Value, "<Mouse>/delta");
            _lookStick = _map.AddAction("LookStick", InputActionType.Value, "<Gamepad>/rightStick");

            _jump = _map.AddAction("Jump", InputActionType.Button, "<Keyboard>/space");
            _jump.AddBinding("<Gamepad>/buttonSouth");

            _slide = _map.AddAction("Slide", InputActionType.Button, "<Keyboard>/leftShift");
            _slide.AddBinding("<Keyboard>/leftCtrl");
            _slide.AddBinding("<Keyboard>/c");
            _slide.AddBinding("<Gamepad>/buttonEast");
            _slide.AddBinding("<Gamepad>/leftTrigger");

            _pause = _map.AddAction("Pause", InputActionType.Button, "<Keyboard>/escape");
            _pause.AddBinding("<Gamepad>/start");

            _map.Enable();
        }

        public PlayerInputFrame Read()
        {
            return new PlayerInputFrame
            {
                Move = Clamp01(_move.ReadValue<UnityEngine.Vector2>()),
                LookMouse = _lookMouse.ReadValue<UnityEngine.Vector2>(),
                LookStick = _lookStick.ReadValue<UnityEngine.Vector2>(),
                JumpPressed = _jump.WasPressedThisFrame(),
                JumpHeld = _jump.IsPressed(),
                SlidePressed = _slide.WasPressedThisFrame(),
                SlideHeld = _slide.IsPressed(),
                PausePressed = _pause.WasPressedThisFrame(),
            };
        }

        static UnityEngine.Vector2 Clamp01(UnityEngine.Vector2 v) => v.sqrMagnitude > 1f ? v.normalized : v;

        public void Dispose()
        {
            _map.Disable();
            _map.Dispose();
        }
    }
}
