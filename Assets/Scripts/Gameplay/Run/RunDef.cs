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
    /// What survives scene loads during a session: the chosen gladiator and their skin, the run, which arena of it we're
    /// in, the run's seed and, in the Daily Games, the day's plan. The menu starts a run (or the daily), arenas read the
    /// gladiator, the end screen retries or goes back to the menu. Every run load seeds GameRandom with Seed right before
    /// LoadScene (R8), so the same seed replays the same chests, swarms, elites, gifts and whims (docs/tracks/R7-design.md
    /// §3.4). T1: the menu's arena list (KnownRuns, for the admin panel's boss fights) and whether this run is the
    /// Playground (StartPlayground).
    /// </summary>
    public static class GameSession
    {
        public const string MenuScene = "Assets/Scenes/MainMenu.unity";

        public static GladiatorDef Gladiator { get; private set; }
        /// <summary>The skin to fight in (R6): the menu sets it at launch (MetaProgress.EquippedSkin); Retry keeps it, Clear
        /// resets it. GladiatorLoadout ignores a skin that isn't the gladiator's own; null = the default look.</summary>
        public static SkinDef Skin { get; set; }
        public static RunDef Run { get; private set; }
        public static int ArenaIndex { get; private set; }
        /// <summary>The run seed the current (or last loaded) run was seeded with; 0 until a run starts here.</summary>
        public static uint Seed { get; private set; }
        /// <summary>The Daily Games' plan while a daily is on (default otherwise: HasDaily false).</summary>
        public static DailyPlan Daily { get; private set; }
        public static bool HasDaily => Daily.IsValid;
        /// <summary>Is this the day's scored attempt (×1.5 Renown, the ledger)? False for practice and normal runs.</summary>
        public static bool DailyScored { get; private set; }
        /// <summary>Tests: record the scene a run would load (LastLoadPath) instead of loading it. Clear() turns it off.</summary>
        public static bool SuppressLoad;
        /// <summary>The scene path the last run load asked for (also set under SuppressLoad).</summary>
        public static string LastLoadPath { get; private set; }
        /// <summary>Tests: stands in for -runSeed's parsed value (null = read the command line). Clear() resets it.</summary>
        public static uint? RunSeedOverride;

        /// <summary>T1: every arena the menu offers, in ladder order (MainMenu sets it on Start), so the admin panel can
        /// jump to any boss. Never null: empty until a menu has been up this session, and after Clear.</summary>
        public static RunDef[] KnownRuns
        {
            get => _knownRuns;
            set => _knownRuns = value ?? Array.Empty<RunDef>();
        }
        /// <summary>T1: the current run is the Playground (the menu's PLAYGROUND launch: StartPlayground). StartRun,
        /// StartDaily and Clear reset it; Retry keeps it.</summary>
        public static bool Playground { get; set; }

        static uint _freshCount;
        static RunDef[] _knownRuns = Array.Empty<RunDef>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            Gladiator = null; Skin = null; Run = null; ArenaIndex = 0;
            Seed = 0u; Daily = default; DailyScored = false;
            SuppressLoad = false; LastLoadPath = null; RunSeedOverride = null;
            _knownRuns = Array.Empty<RunDef>(); Playground = false;
        }

        /// <summary>A build that boots straight into an arena (no menu) still honours -runSeed: GameRandom's own reset
        /// (SubsystemRegistration) runs before this, the first scene's Awakes after.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void SeedFirstScene()
        {
            if (!TryCommandLineSeed(out uint seed)) return;
            Seed = seed;
            GameRandom.BeginRun(seed);
        }

        /// <summary>Forget the gladiator, skin, run, seed and daily, the Playground and the menu's arena list, and the test
        /// switches (tests: one that picks a gladiator must not leak it into the next; set SuppressLoad / RunSeedOverride
        /// after calling this).</summary>
        public static void Clear() => Reset();

        public static string ArenaName =>
            Run != null && ArenaIndex < Run.arenas.Length ? Run.arenas[ArenaIndex].displayName : null;

        /// <summary>A normal run: -runSeed's seed if the command line gives one (bot and perf runs), else a fresh one.</summary>
        public static void StartRun(GladiatorDef gladiator, RunDef run) =>
            StartRun(gladiator, run, TryCommandLineSeed(out uint seed) ? seed : FreshSeed());

        /// <summary>A normal run on `seed` (SAME SEED, -runSeed, tests). Leaves any daily and the Playground.</summary>
        public static void StartRun(GladiatorDef gladiator, RunDef run, uint seed) => Begin(gladiator, run, seed, false);

        /// <summary>T1: the Playground (the admin menu's PLAYGROUND → FIGHT!): `run` is its RunDef, any gladiator. Like a
        /// normal run (-runSeed or a fresh seed), with Playground set until the next StartRun / StartDaily / Clear.</summary>
        public static void StartPlayground(GladiatorDef gladiator, RunDef run)
        {
            if (string.IsNullOrEmpty(FirstArena(run))) Debug.LogWarning("[GameSession] StartPlayground: no Playground run (or it has no scene)");
            Begin(gladiator, run, TryCommandLineSeed(out uint seed) ? seed : FreshSeed(), true);
        }

        static void Begin(GladiatorDef gladiator, RunDef run, uint seed, bool playground)
        {
            Gladiator = gladiator;
            Run = run;
            ArenaIndex = 0;
            Daily = default;
            DailyScored = false;
            Seed = seed;
            Playground = playground;
            LoadRun(FirstArena(run));
        }

        /// <summary>The Daily Games: the plan's gladiator, arena, edicts and seed. `scored` = the day's one scored attempt
        /// (MetaProgress.BeginDaily has already marked it in the save); false = practice.</summary>
        public static void StartDaily(in DailyPlan plan, bool scored)
        {
            if (!plan.IsValid)
            {
                Debug.LogWarning("[GameSession] StartDaily: the plan has no arena or gladiator");
                return;
            }
            Gladiator = plan.Gladiator;
            Run = plan.Run;
            ArenaIndex = 0;
            Daily = plan;
            DailyScored = scored;
            Seed = plan.Seed;
            Playground = false;
            RunModifiers.Set(plan.Edicts);
            Debug.Log($"[GameSession] daily {plan.Date} {(scored ? "SCORED" : "practice")}: {plan.Run.id} as {plan.Gladiator.id}, " +
                      $"{(plan.Edicts != null ? plan.Edicts.Length : 0)} edicts, seed {GameRandom.SeedText(plan.Seed)}");
            LoadRun(FirstArena(plan.Run));
        }

        /// <summary>Same arena, same gladiator, a new attempt: a normal run gets a fresh seed (it's a new run); a daily
        /// replays its seed as practice (the scored attempt is spent).</summary>
        public static void Retry()
        {
            if (HasDaily) DailyScored = false;
            else Seed = FreshSeed();
            Reload();
        }

        /// <summary>SAME SEED: the same arena, gladiator and seed again (a daily: as practice).</summary>
        public static void RetrySameSeed()
        {
            if (HasDaily) DailyScored = false;
            if (Seed == 0u) Seed = GameRandom.RunSeed; // an arena loaded straight from the Editor or a test: replay what it ran on
            Reload();
        }

        /// <summary>A seed that differs every time: the clock's ticks and a counter through GameRandom.Hash (never 0).</summary>
        public static uint FreshSeed()
        {
            ulong ticks = (ulong)System.Diagnostics.Stopwatch.GetTimestamp() ^ (ulong)DateTime.UtcNow.Ticks;
            uint h = GameRandom.Hash((uint)ticks, (uint)(ticks >> 32), ++_freshCount);
            return h != 0u ? h : 0x9E3779B9u;
        }

        /// <summary>-runSeed 7F3A91C2 (hex, "7F3A-91C2" too): a fixed seed for bot and perf runs (RunSeedOverride in tests).
        /// False when absent; an unreadable value is reported and ignored.</summary>
        public static bool TryCommandLineSeed(out uint seed)
        {
            if (RunSeedOverride.HasValue) { seed = RunSeedOverride.Value; return true; }
            seed = 0u;
            string text = CommandLine.Get("-runSeed");
            if (string.IsNullOrEmpty(text)) return false;
            if (GameRandom.TryParseSeed(text, out seed)) return true;
            Debug.LogWarning("[GameSession] -runSeed " + text + " isn't a seed (8 hex digits, e.g. 7F3A91C2): using a fresh one");
            seed = 0u;
            return false;
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

        static string FirstArena(RunDef run) => run != null && run.arenas != null && run.arenas.Length > 0 ? run.arenas[0].scenePath : null;

        /// <summary>Retry's reload: the active scene again, on Seed.</summary>
        static void Reload()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            GameRandom.BeginRun(Seed);
            LastLoadPath = scene.path;
            if (SuppressLoad) return;
            TimeController.ResetForNewRun();
            if (scene.buildIndex >= 0) UnityEngine.SceneManagement.SceneManager.LoadScene(scene.buildIndex);
            else UnityEngine.SceneManagement.SceneManager.LoadScene(scene.path);
        }

        /// <summary>A run's first scene: GameRandom.BeginRun(Seed) right before the load, so every stream starts from the
        /// seed (also under SuppressLoad, which only skips the load itself).</summary>
        static void LoadRun(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            GameRandom.BeginRun(Seed);
            LastLoadPath = path;
            if (SuppressLoad) return;
            Load(path);
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
