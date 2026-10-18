using Gladiator.Core;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>Spawns PlaytestBot + FrameStatsRecorder from the command line in any gameplay scene.</summary>
    static class PlaytestBotBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            bool bot = CommandLine.Has("-bot");
            bool stress = CommandLine.Has("-stress");
            bool profile = CommandLine.Has("-profile") || bot || stress;
            if (!bot && !stress && !profile) return;
            int fps = CommandLine.GetInt("-fps", 0); // cap (e.g. for profiler captures); 0 = uncapped
            if (fps > 0) { QualitySettings.vSyncCount = 0; Application.targetFrameRate = fps; }

            if ((bot || stress) && Object.FindAnyObjectByType<PlaytestBot>() == null)
            {
                var go = new GameObject("[PlaytestBot]");
                var b = go.AddComponent<PlaytestBot>();
                b.mode = stress ? PlaytestBot.Mode.Stress : PlaytestBot.Mode.Normal;
                b.stressCount = CommandLine.GetInt("-stressCount", b.stressCount);
                b.runSeconds = CommandLine.GetFloat("-botSeconds", 600f);
                b.seed = CommandLine.GetInt("-seed", b.seed);
                b.godMode = !CommandLine.Has("-mortal");
                b.timeless = !CommandLine.Has("-timed");
                Object.DontDestroyOnLoad(go);
            }
            if (profile && Object.FindAnyObjectByType<FrameStatsRecorder>() == null)
            {
                var go = new GameObject("[FrameStats]");
                var r = go.AddComponent<FrameStatsRecorder>();
                r.label = (stress ? "stress_" : bot ? "bot_" : "") + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                Object.DontDestroyOnLoad(go);
            }
        }
    }
}
