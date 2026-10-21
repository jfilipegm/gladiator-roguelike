using System;
using Gladiator.Core;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// A run as data (CLAUDE.md §4): an ordered list of arenas. The slice's run has exactly one (the Slave Pit); whether
    /// a full run chains several arenas is still an open question, so the flow already walks the list.
    /// </summary>
    [CreateAssetMenu(menuName = "Gladiator/Run", fileName = "RD_")]
    public sealed class RunDef : ScriptableObject
    {
        [Serializable]
        public struct Arena
        {
            public string displayName;
            [Tooltip("Scene to load (in the build settings).")]
            public string scenePath;
        }

        public string id = "slice";
        [Tooltip("Menu card text (arena select).")]
        [TextArea] public string description;
        [Tooltip("Empty = open from the start. Locked runs show their unlock's hint on the arena select.")]
        public UnlockDef unlock;
        public Arena[] arenas = Array.Empty<Arena>();

        [Header("Arena select (R9: the ladder and the tablet; filled by Gladiator/Setup/UI/Bake Portraits)")]
        [Tooltip("The first arena from a fixed high three-quarter camera, 192×108, point filtered.")]
        public Sprite thumbnail;
        [Tooltip("How the climb feels here (docs/GDD.md's ladder): \"I need to survive.\"")]
        public string tagline;
        [Tooltip("The arena's hazards in one line.")]
        public string hazards;
        [Tooltip("The first arena's layout: floor size, difficulty, tier (Renown) and its boss's records.")]
        public ArenaLayoutDef layout;
        public Sprite bossPortrait;
        [Tooltip("\"Gorm, the Pit Master\" (empty = the layout's boss).")]
        public string bossName;

        public string DisplayName => arenas.Length > 0 ? arenas[0].displayName : id;
        public BossDef Boss => layout != null ? layout.boss : null;
    }

    /// <summary>
    /// What survives scene loads during a session: the chosen gladiator, the run and which arena of it we're in. The
    /// menu starts a run, arenas read the gladiator, the end screen retries or goes back to the menu.
    /// </summary>
    public static class GameSession
    {
        public const string MenuScene = "Assets/Scenes/MainMenu.unity";

        public static GladiatorDef Gladiator { get; private set; }
        public static RunDef Run { get; private set; }
        public static int ArenaIndex { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { Gladiator = null; Run = null; ArenaIndex = 0; }

        /// <summary>Forget the chosen gladiator and run (tests: one that picks a gladiator must not leak it into the next).</summary>
        public static void Clear() => Reset();

        public static string ArenaName =>
            Run != null && ArenaIndex < Run.arenas.Length ? Run.arenas[ArenaIndex].displayName : null;

        public static void StartRun(GladiatorDef gladiator, RunDef run)
        {
            Gladiator = gladiator;
            Run = run;
            ArenaIndex = 0;
            Load(run != null && run.arenas.Length > 0 ? run.arenas[0].scenePath : null);
        }

        /// <summary>Same arena, same gladiator, a fresh run.</summary>
        public static void Retry()
        {
            TimeController.ResetForNewRun();
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.buildIndex >= 0) UnityEngine.SceneManagement.SceneManager.LoadScene(scene.buildIndex);
            else UnityEngine.SceneManagement.SceneManager.LoadScene(scene.path);
        }

        /// <summary>Back to the main menu (or quit if it isn't in the build, e.g. a bare arena build).</summary>
        public static void ToMenu()
        {
            TimeController.ResetForNewRun();
            if (UnityEngine.SceneManagement.SceneUtility.GetBuildIndexByScenePath(MenuScene) >= 0) Load(MenuScene);
            else
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            }
        }

        static void Load(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            TimeController.ResetForNewRun();
            int index = UnityEngine.SceneManagement.SceneUtility.GetBuildIndexByScenePath(path);
            if (index >= 0) UnityEngine.SceneManagement.SceneManager.LoadScene(index);
            else UnityEngine.SceneManagement.SceneManager.LoadScene(path);
        }
    }
}
