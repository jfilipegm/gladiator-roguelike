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
        public const string ReleaseOutput = "Builds/Linux/Gladiator.x86_64";
        public const string WindowsOutput = "Builds/Windows/Gladiator.exe";
        public const string MacOutput = "Builds/Mac/Gladiator.app";

        /// <summary>What a player gets: the menu, the arenas and the Playground (T1: admin mode, switched on by typing ADMIN
        /// on the menu or -admin, opens it in any build). No dev test arena.</summary>
        static string[] ReleaseScenes => new[] { Gladiator.Gameplay.GameSession.MenuScene, ArenaBuilder.SlavePitScene, ProvincialPitSetup.Scene, AmphitheaterSetup.Scene, GrandArenaSetup.Scene, ImperialArenaSetup.Scene, ColosseumSetup.Scene, PlaygroundSetup.ScenePath };

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

        /// <summary>Main menu first (-bot / -stress jump straight into the run), then the arenas, then the dev test arena
        /// and the Playground.</summary>
        [MenuItem("Gladiator/Build/Linux Development Build")]
        public static string BuildLinuxDev() => Build(new[] { Gladiator.Gameplay.GameSession.MenuScene, ArenaBuilder.SlavePitScene, ProvincialPitSetup.Scene, AmphitheaterSetup.Scene, GrandArenaSetup.Scene, ImperialArenaSetup.Scene, ColosseumSetup.Scene, TestArenaScene.ScenePath, PlaygroundSetup.ScenePath }, DevOutput, BuildOptions.Development);

        /// <summary>The playtest build: what a player gets (menu + the arenas + the Playground; the admin panel and the
        /// Playground only once admin mode is on).</summary>
        [MenuItem("Gladiator/Build/Linux Release Build")]
        public static string BuildLinuxRelease() => Build(ReleaseScenes, ReleaseOutput, BuildOptions.None);

        /// <summary>The release build for Windows (64-bit, Mono). Needs the "Windows Build Support (Mono)" module.</summary>
        [MenuItem("Gladiator/Build/Windows Release Build")]
        public static string BuildWindowsRelease() => Build(ReleaseScenes, WindowsOutput, BuildOptions.None, BuildTarget.StandaloneWindows64);

        /// <summary>The release build for macOS (Mono). Needs the "Mac Build Support (Mono)" module; unsigned (see the release notes).</summary>
        [MenuItem("Gladiator/Build/macOS Release Build")]
        public static string BuildMacRelease()
        {
            UseUniversalMacBuild();
            return Build(ReleaseScenes, MacOutput, BuildOptions.None, BuildTarget.StandaloneOSX);
        }

        /// <summary>
        /// One app for Intel and Apple-silicon Macs (the Mac module defaults to Apple silicon only). Set by reflection:
        /// the setting lives in the Mac module's own assembly, which isn't there when the module isn't installed.
        /// </summary>
        static void UseUniversalMacBuild()
        {
            var settings = System.Type.GetType("UnityEditor.OSXStandalone.UserBuildSettings, UnityEditor.OSXStandalone.Extensions");
            var arch = settings?.GetProperty("architecture");
            if (arch != null) arch.SetValue(null, UnityEditor.Build.OSArchitecture.x64ARM64);
            else Debug.LogWarning("[Build] couldn't set the macOS architecture; the app may run on Apple silicon only");
        }

        /// <summary>
        /// Every player for a GitHub release (<c>Tools/release</c>): Linux release + dev, Windows and macOS release.
        /// Ends with one "[Build] release set OK|FAILED" log line the script waits for.
        /// </summary>
        public static string BuildReleaseSet()
        {
            var results = new[] { BuildLinuxRelease(), BuildLinuxDev(), BuildWindowsRelease(), BuildMacRelease() };
            bool ok = true;
            foreach (var r in results) ok &= r.StartsWith("Succeeded");
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneLinux64);
            Debug.Log("[Build] release set " + (ok ? "OK" : "FAILED") + " v" + Gladiator.Core.GameInfo.Version);
            return string.Join("\n", results);
        }

        public static string Build(string[] scenes, string output, BuildOptions options, BuildTarget target = BuildTarget.StandaloneLinux64)
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, target))
            {
                string skip = $"Failed: {target} build support is not installed → {output}";
                Debug.LogError("[Build] " + skip);
                return skip;
            }
            ConfigurePlayer();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = output,
                target = target,
                targetGroup = BuildTargetGroup.Standalone,
                options = options,
            });
            var s = report.summary;
            string msg = $"{s.result} in {s.totalTime.TotalSeconds:0}s, {s.totalSize / (1024 * 1024)} MB, errors={s.totalErrors} warnings={s.totalWarnings} → {s.outputPath}";
            Debug.Log("[Build] " + msg);
            return msg;
        }
    }
}
