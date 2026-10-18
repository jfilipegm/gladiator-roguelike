using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Gladiator.EditorTools
{
    /// <summary>
    /// Reproducible player builds. The development build is what perf numbers come from (CLAUDE.md §8):
    ///   Builds/LinuxDev/Gladiator.x86_64 -bot -profile -botSeconds 60 -screen-fullscreen 0 -screen-width 1920 -screen-height 1080
    /// CSV lands in ~/.config/unity3d/&lt;company&gt;/&lt;product&gt;/Profiling (or -profileDir &lt;dir&gt;).
    /// </summary>
    public static class BuildScripts
    {
        public const string DevOutput = "Builds/LinuxDev/Gladiator.x86_64";

        public static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "SoloDev";
            PlayerSettings.productName = "Gladiator";
            PlayerSettings.runInBackground = true;          // bots/perf runs must not throttle when unfocused
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.visibleInBackground = true;
            PlayerSettings.bakeCollisionMeshes = true;      // pre-cook MeshColliders at build time (no load-time cooking)
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
        }

        [MenuItem("Gladiator/Build/Linux Development Build (Test Arena)")]
        public static string BuildLinuxDev() => Build(new[] { TestArenaScene.ScenePath }, DevOutput, BuildOptions.Development);

        public static string Build(string[] scenes, string output, BuildOptions options)
        {
            ConfigurePlayer();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = output,
                target = BuildTarget.StandaloneLinux64,
                options = options,
            });
            var s = report.summary;
            string msg = $"{s.result} in {s.totalTime.TotalSeconds:0}s, {s.totalSize / (1024 * 1024)} MB, errors={s.totalErrors} warnings={s.totalWarnings} → {s.outputPath}";
            Debug.Log("[Build] " + msg);
            return msg;
        }
    }
}
