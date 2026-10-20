using Gladiator.Core;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// Spawns PlaytestBot + FrameStatsRecorder from the command line in the first gameplay scene (-mix: all enemy types).
    /// From the main menu it waits for the arena (the menu starts the run itself under -bot), so the recording never
    /// includes the menu or the scene load.
    /// </summary>
    static class PlaytestBotBootstrap
    {
        /// <summary>
        /// -evolved (Phase 5.5 perf runs): the build of a late run from the first second: up to 4 evolved weapons (each
        /// base taken to max level, its catalyst, then the evolution), then Pyre and Serrated Edge so Wildfire, Bloodbath
        /// and Executioner switch on. Cards are taken directly (no eligibility, no meta locks).
        /// </summary>
        internal static void EvolvedBuild()
        {
            var player = Player.Main;
            var build = player != null ? player.GetComponent<PlayerBuild>() : null;
            if (build == null || build.catalog == null) return;
            var state = build.State;
            int evolved = 0;
            foreach (var e in build.catalog.upgrades)
            {
                if (evolved >= 4) break;
                if (e == null || e.evolvesFrom == null || e.evolvesFrom.weapon == null) continue;
                bool owned = state.LevelOf(e.evolvesFrom) > 0;
                if (!owned && state.CountOf(UpgradeKind.Weapon) >= build.config.weaponSlots) continue;
                if (e.catalyst != null && state.LevelOf(e.catalyst) == 0 && state.CountOf(UpgradeKind.Boon) >= build.config.boonSlots) continue;
                while (state.LevelOf(e.evolvesFrom) < e.evolvesFrom.maxLevel) Take(build, e.evolvesFrom, Rarity.Uncommon);
                if (e.catalyst != null && state.LevelOf(e.catalyst) == 0) Take(build, e.catalyst, Rarity.Uncommon);
                Take(build, e, Rarity.Legendary);
                evolved++;
            }
            foreach (var u in build.catalog.upgrades)
                if (u != null && (u.id == "pyre" || u.id == "serrated_edge") && state.LevelOf(u) == 0 && state.CountOf(UpgradeKind.Boon) < build.config.boonSlots)
                    Take(build, u, Rarity.Uncommon);
            Debug.Log($"[PlaytestBot] -evolved: {build.Evolutions} evolutions, {state.SynergyCount} synergies");
        }

        static void Take(PlayerBuild build, UpgradeDef d, Rarity r) =>
            build.Take(new CardOffer { Def = d, Rarity = r, Level = build.State.LevelOf(d) + 1 });

        static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            if (Player.Main == null) return;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            Init();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            bool bot = CommandLine.Has("-bot");
            bool stress = CommandLine.Has("-stress");
            bool profile = CommandLine.Has("-profile") || bot || stress;
            if (!bot && !stress && !profile) return;
            if (Player.Main == null) // a menu: try again when a gameplay scene loads
            {
                UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
                return;
            }
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
                b.evolvedBuild = CommandLine.Has("-evolved");
                b.traceEvery = CommandLine.GetFloat("-botTrace", 0f);
                Object.DontDestroyOnLoad(go);
                // -mix: the debug crowd spawns every enemy type from the catalog, with occasional elites
                var admin = AdminController.Instance;
                var spawner = Object.FindAnyObjectByType<DebugEnemySpawner>();
                if (CommandLine.Has("-mix") && admin != null && admin.catalog != null && spawner != null)
                {
                    spawner.mix = admin.catalog.enemies;
                    spawner.affixes = admin.catalog.affixes;
                    spawner.eliteChance = CommandLine.GetFloat("-eliteChance", 0.03f);
                }
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
