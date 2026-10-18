using System.Collections;
using System.Text;
using Gladiator.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Gladiator.Tests
{
    /// <summary>Scripted input: the test writes Next each frame; press edges are consumed once read.</summary>
    public sealed class ScriptedInput : IPlayerInputSource
    {
        public PlayerInputFrame Next;
        public PlayerInputFrame Read()
        {
            var f = Next;
            Next.JumpPressed = false;
            Next.SlidePressed = false;
            return f;
        }
    }

    /// <summary>
    /// M1.1 acceptance: measures the controller against MovementConfig in the test arena and logs the numbers
    /// ("[Feel] ..." + Profiling/feel_*.txt). Tolerances are loose on purpose: this checks the controller does what
    /// the config says; whether it FEELS right is the producer's call at Gate 1.
    /// </summary>
    public class MovementFeelTests
    {
        const string Scene = "Assets/Scenes/Dev/TestArena.unity";
        readonly StringBuilder _log = new StringBuilder();
        Player _player;
        PlayerMotor _motor;
        PlayerCameraRig _rig;
        ScriptedInput _in;

        void Log(string s) { _log.AppendLine(s); Debug.Log("[Feel] " + s); }

        IEnumerator Boot()
        {
            DevSettings.ResetAll(); // static test switches must not leak between tests
            Gladiator.Core.TimeController.GameSpeed = 1f;
            Gladiator.Core.TimeController.ResetForNewRun();
            var op = SceneManager.LoadSceneAsync(Scene, LoadSceneMode.Single);
            while (!op.isDone) yield return null;
            yield return null;
            _player = Player.Main;
            _motor = _player.GetComponent<PlayerMotor>();
            _rig = Object.FindAnyObjectByType<PlayerCameraRig>();
            // Movement is measured without enemies: kills would grant XP, and a level-up pauses the game.
            var spawner = Object.FindAnyObjectByType<DebugEnemySpawner>();
            if (spawner != null) spawner.enabled = false;
            EnemyManager.Instance?.KillAll();
            _in = new ScriptedInput();
            _player.OverrideSource = _in;
            yield return new WaitForSeconds(0.3f);
        }

        IEnumerator Hold(float seconds, PlayerInputFrame f)
        {
            float end = Time.time + seconds;
            while (Time.time < end) { var keep = f; keep.JumpPressed = false; keep.SlidePressed = false; _in.Next = keep; yield return null; }
            _in.Next = default;
        }

        IEnumerator Place(Vector3 pos, float yaw)
        {
            _in.Next = default;
            _motor.Teleport(pos);
            _player.transform.rotation = Quaternion.Euler(0, yaw, 0);
            _rig.SetAngles(yaw, 20f);
            _motor.Telemetry = default;
            yield return new WaitForSeconds(0.4f); // settle / land
            _motor.Telemetry = default;
        }

        IEnumerator WaitGrounded(float timeout)
        {
            float end = Time.time + timeout;
            yield return null;
            while (!_motor.IsGrounded && Time.time < end) yield return null;
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator Controller_MatchesMovementConfig()
        {
            yield return Boot();
            var c = _motor.config;
            var fwd = new PlayerInputFrame { Move = new Vector2(0, 1) };

            // 1) Run: top speed + time to 90 %.
            yield return Place(new Vector3(-14, 0.1f, -10), 0);
            float t0 = Time.time, t90 = -1f;
            float end = Time.time + 1.5f;
            while (Time.time < end)
            {
                _in.Next = fwd;
                if (t90 < 0 && _motor.HorizontalSpeed >= c.maxSpeed * 0.9f) t90 = Time.time - t0;
                yield return null;
            }
            _in.Next = default;
            float top = _motor.Telemetry.TopHorizontalSpeed;
            Log($"run: top speed {top:0.00} m/s (config {c.maxSpeed}), 0→90% in {t90:0.000}s");
            Assert.AreEqual(c.maxSpeed, top, c.maxSpeed * 0.03f, "run top speed");
            // stop distance
            Vector3 p0 = _player.transform.position;
            yield return new WaitForSeconds(0.6f);
            Log($"run: stop distance {(Vector3.Distance(p0, _player.transform.position)):0.00} m");

            // 2) Full (held) jump vs tapped jump.
            yield return Place(new Vector3(-14, 0.1f, -10), 0);
            _in.Next = new PlayerInputFrame { JumpPressed = true, JumpHeld = true };
            yield return null;
            yield return Hold(0.8f, new PlayerInputFrame { JumpHeld = true });
            yield return WaitGrounded(2f);
            float full = _motor.Telemetry.LastJumpHeight;
            yield return Place(new Vector3(-14, 0.1f, -10), 0);
            _in.Next = new PlayerInputFrame { JumpPressed = true, JumpHeld = true };
            yield return null;
            _in.Next = default; // released immediately
            yield return WaitGrounded(2f);
            float tap = _motor.Telemetry.LastJumpHeight;
            Log($"jump: held {full:0.00} m (config {c.jumpHeight}), tapped {tap:0.00} m ({tap / Mathf.Max(0.01f, full):P0} of held)");
            Assert.AreEqual(c.jumpHeight, full, c.jumpHeight * 0.08f, "held jump height");
            Assert.Less(tap, full * 0.75f, "variable jump: tap should be clearly lower");

            // 3) Slide on flat: boost and how long it carries.
            yield return Place(new Vector3(-14, 0.1f, -14), 0);
            yield return Hold(0.8f, fwd);
            float before = _motor.HorizontalSpeed;
            _in.Next = new PlayerInputFrame { Move = new Vector2(0, 1), SlidePressed = true, SlideHeld = true };
            yield return null;
            float slideStart = Time.time;
            while (_motor.IsSliding && Time.time - slideStart < 4f)
            {
                _in.Next = new PlayerInputFrame { Move = new Vector2(0, 1), SlideHeld = true };
                yield return null;
            }
            _in.Next = default;
            Log($"slide (flat): speed {before:0.00} → +{_motor.Telemetry.LastSlideBoost:0.00} boost (config {c.slideStartBoost}), top {_motor.Telemetry.TopSlideSpeed:0.00} m/s, lasted {Time.time - slideStart:0.00}s");
            Assert.AreEqual(c.slideStartBoost, _motor.Telemetry.LastSlideBoost, 0.3f, "slide boost");
            float flatTop = _motor.Telemetry.TopSlideSpeed;

            // 4) Slide down the 30° face of the hill: must gain speed.
            var hillTop = TestArenaLayout.HillPos + new Vector3(0, 3.5f, -0.6f);
            yield return Place(hillTop, 0);
            yield return Hold(0.25f, fwd);
            _in.Next = new PlayerInputFrame { Move = new Vector2(0, 1), SlidePressed = true, SlideHeld = true };
            yield return null;
            yield return Hold(1.6f, new PlayerInputFrame { Move = new Vector2(0, 1), SlideHeld = true });
            float hillTopSpeed = _motor.Telemetry.TopSlideSpeed;
            Log($"slide (30° downhill): top {hillTopSpeed:0.00} m/s vs flat {flatTop:0.00} m/s (cap {c.slideMaxSpeed})");
            Assert.Greater(hillTopSpeed, flatTop + 2f, "downhill slide should gain speed");

            // 5) Stairs up to the platform (deck at 1.2 m).
            yield return Place(new Vector3(-10, 0.1f, 9.5f), 180);
            float stairY = 0f;
            end = Time.time + 1.2f;
            while (Time.time < end) { _in.Next = fwd; stairY = Mathf.Max(stairY, _player.transform.position.y); yield return null; }
            _in.Next = default;
            Log($"stairs: reached y={stairY:0.00} (platform deck 1.2 m)");
            Assert.Greater(stairY, 1.0f, "should walk up the stairs onto the platform");

            // 6) Coyote time: run off the platform edge and jump shortly after leaving it.
            yield return Place(new Vector3(-10, 1.3f, 4f), 90); // on deck, facing +X (edge at x=-8)
            bool leftGround = false; float leftAt = 0f; bool jumped = false;
            end = Time.time + 2f;
            while (Time.time < end && !jumped)
            {
                var f = new PlayerInputFrame { Move = new Vector2(0, 1), JumpHeld = true };
                if (!leftGround && !_motor.IsGrounded) { leftGround = true; leftAt = Time.time; }
                if (leftGround && Time.time - leftAt >= c.coyoteTime * 0.5f) { f.JumpPressed = true; jumped = true; }
                _in.Next = f;
                yield return null;
            }
            yield return null;
            Log($"coyote: jumped {(Time.time - leftAt) * 1000f:0}ms after leaving the edge → jumps={_motor.Telemetry.Jumps} (window {c.coyoteTime * 1000f:0}ms)");
            Assert.AreEqual(1, _motor.Telemetry.Jumps, "coyote jump should fire");

            System.IO.Directory.CreateDirectory(Gladiator.Core.FrameStatsRecorder.OutputDirectory());
            System.IO.File.WriteAllText(System.IO.Path.Combine(Gladiator.Core.FrameStatsRecorder.OutputDirectory(),
                "feel_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt"), _log.ToString());
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator Camera_NeverInsideGeometry_DuringBotRun()
        {
            yield return Boot();
            _player.OverrideSource = null;
            Object.FindAnyObjectByType<DebugEnemySpawner>().enabled = true; // camera squeeze cases came from fleeing enemies
            var botGo = Object.FindAnyObjectByType<PlaytestBot>(FindObjectsInactive.Include);
            botGo.gameObject.SetActive(true);
            botGo.cameraOrbitAmount = 0.8f;
            var cam = Camera.main;
            var target = _player.transform.Find("CameraTarget");
            var probe = new GameObject("[CameraClipProbe]").AddComponent<CameraClipProbe>();
            probe.radius = cam.nearClipPlane;
            int frames = 0, blocked = 0;
            float minDist = 99f;
            float end = Time.time + 25f;
            while (Time.time < end)
            {
                yield return new WaitForEndOfFrame();
                frames++;
                var cp = cam.transform.position;
                if (Physics.Linecast(cp, target.position, Layers.EnvironmentMask, QueryTriggerInteraction.Ignore)) blocked++;
                minDist = Mathf.Min(minDist, Vector3.Distance(cp, target.position));
            }
            Log($"camera: {frames} frames, line of sight blocked {blocked}, closest {minDist:0.00} m, probe: {probe.Report()}");
            Assert.AreEqual(0, probe.InsideFrames, "camera touched geometry: " + probe.Report());
            Assert.LessOrEqual(blocked, frames / 100, "line of sight blocked on more than 1% of frames");
        }
    }

    public static class TestArenaLayout
    {
        public static readonly Vector3 HillPos = new Vector3(12f, 0f, 0f);
    }
}
