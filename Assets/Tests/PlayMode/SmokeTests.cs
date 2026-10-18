using System.Collections;
using System.Collections.Generic;
using Gladiator.Core;
using Gladiator.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Gladiator.Tests
{
    /// <summary>
    /// Boots the gameplay scene, lets PlaytestBot play, asserts no errors/exceptions and a frame-time budget.
    /// Scene and duration: SmokeTestSettings (env GLADIATOR_SMOKE_SECONDS overrides the 60 s default).
    /// </summary>
    public class SmokeTests
    {
        readonly List<string> _errors = new List<string>();

        void OnLog(string msg, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                _errors.Add(type + ": " + msg);
        }

        [UnityTest, Timeout(20 * 60 * 1000)]
        public IEnumerator BootScene_RunBot_NoErrors_WithinBudget()
        {
            Application.logMessageReceived += OnLog;
            try
            {
                var op = SceneManager.LoadSceneAsync(SmokeTestSettings.ScenePath, LoadSceneMode.Single);
                Assert.IsNotNull(op, "scene not in build settings: " + SmokeTestSettings.ScenePath);
                while (!op.isDone) yield return null;

                var bot = Object.FindAnyObjectByType<PlaytestBot>() ?? new GameObject("[PlaytestBot]").AddComponent<PlaytestBot>();
                bot.runSeconds = 0f; // the test owns the duration
                var rec = Object.FindAnyObjectByType<FrameStatsRecorder>() ?? new GameObject("[FrameStats]").AddComponent<FrameStatsRecorder>();
                rec.label = "smoke_" + SceneManager.GetActiveScene().name;
                if (!rec.IsRecording) rec.Begin();

                float seconds = SmokeTestSettings.Seconds;
                float end = Time.realtimeSinceStartup + seconds;
                while (Time.realtimeSinceStartup < end) yield return null;

                var s = rec.End();
                Debug.Log("[Smoke] " + s);

                Assert.IsEmpty(_errors, "errors during run:\n" + string.Join("\n", _errors));
                Assert.Greater(s.Frames, 10, "too few frames recorded");
                Assert.LessOrEqual(s.AvgGameplayMs, SmokeTestSettings.MaxAvgGameplayMs, "gameplay logic over budget");
                Assert.LessOrEqual(s.P95FrameMs, SmokeTestSettings.MaxP95FrameMs, "p95 frame time over budget");
            }
            finally
            {
                Application.logMessageReceived -= OnLog;
            }
        }
    }

    public static class SmokeTestSettings
    {
        /// <summary>Phase 0: bot sandbox. Phase 1: test arena. Phase 3+: Arena 1 (Slave Pit).</summary>
        public const string ScenePath = "Assets/Scenes/Dev/TestArena.unity";

        public static float Seconds
        {
            get
            {
                var env = System.Environment.GetEnvironmentVariable("GLADIATOR_SMOKE_SECONDS");
                return float.TryParse(env, out var v) && v > 0 ? v : 60f;
            }
        }

        /// <summary>CLAUDE.md §8: gameplay logic ≤ 4 ms/frame.</summary>
        public const float MaxAvgGameplayMs = 4f;
        /// <summary>Editor-hosted runs include editor overhead; the real 60 FPS check is the standalone stress run.</summary>
        public const float MaxP95FrameMs = 50f;
    }
}
