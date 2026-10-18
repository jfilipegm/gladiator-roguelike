using Gladiator.Core;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// The player's hub: reads ONE input frame per tick (human or PlaytestBot) that the motor, camera and weapons share,
    /// owns the camera-yaw reference for camera-relative movement, and handles pause / cursor lock.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public sealed class Player : MonoBehaviour, ITickable
    {
        public static Player Main { get; private set; }

        /// <summary>This tick's input (zeroed while input is disabled, e.g. during level-up).</summary>
        public PlayerInputFrame Input { get; private set; }
        /// <summary>Yaw (deg) that "forward" on the stick means. Set from the gameplay camera.</summary>
        public float ReferenceYaw { get; set; }
        public bool InputEnabled { get; set; } = true;
        public bool IsBotControlled => _source is PlaytestBot || _source == OverrideSource && OverrideSource != null;
        /// <summary>Takes precedence over bot and human (scripted input for automated feel tests).</summary>
        public IPlayerInputSource OverrideSource { get; set; }

        IPlayerInputSource _source;
        HumanInputSource _human;
        bool _paused;

        void OnEnable()
        {
            Main = this;
            GameLoop.Register(this, TickGroup.Input);
        }

        void OnDisable()
        {
            GameLoop.Unregister(this);
            if (Main == this) Main = null;
            _human?.Dispose();
            _human = null;
            CursorUnlock();
        }

        public void Tick(float dt, float unscaledDt)
        {
            // Bot wins when present (command line, smoke test, stress run).
            var bot = PlaytestBot.Active;
            if (OverrideSource != null) _source = OverrideSource;
            else if (bot != null)
            {
                if (!ReferenceEquals(_source, bot)) { bot.Bind(transform); _source = bot; }
            }
            else
            {
                _human ??= new HumanInputSource();
                _source = _human;
            }

            var frame = _source.Read();

            if (frame.PausePressed && !IsBotControlled) SetPaused(!_paused);
            if (_paused && !IsBotControlled && UnityEngine.InputSystem.Mouse.current != null &&
                UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame)
                SetPaused(false);

            if (!InputEnabled || _paused) frame = default;
            Input = frame;

            if (!IsBotControlled)
            {
                bool wantLock = !_paused && InputEnabled && Application.isFocused;
                if (wantLock && Cursor.lockState != CursorLockMode.Locked) CursorLock();
                else if (!wantLock && Cursor.lockState == CursorLockMode.Locked) CursorUnlock();
            }
        }

        public void SetPaused(bool paused)
        {
            if (paused == _paused) return;
            _paused = paused;
            if (paused) TimeController.PushPause(); else TimeController.PopPause();
        }

        public bool IsPaused => _paused;

        static void CursorLock() { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
        static void CursorUnlock() { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
    }
}
