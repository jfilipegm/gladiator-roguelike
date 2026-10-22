using System;
using Gladiator.Gameplay;
using UnityEditor;
using UnityEngine;

namespace Gladiator.EditorTools
{
    /// <summary>
    /// R8 the Daily Games (docs/tracks/R7-design.md §3.6): the three unlocks in MetaSetup.Dir (The Daily Games opens the
    /// DAILY button when Gorm falls; Daily Bread and Champion of the Day are challenges with Renown bounties) and
    /// MetaConfig.dailyUnlock. The daily's numbers (bonus, edicts, salt, ledger) keep their MetaConfig defaults and any
    /// inspector tuning; only an empty salt is filled. Creates or updates: running it twice changes nothing. Ends with
    /// Create Meta Data, which collects the unlocks into MetaConfig.unlocks.
    /// </summary>
    public static class DailySetup
    {
        public const string DailyUnlockPath = MetaSetup.Dir + "UL_Daily.asset";
        public const string DailyBreadPath = MetaSetup.Dir + "UL_DailyBread.asset";
        public const string ChampionOfTheDayPath = MetaSetup.Dir + "UL_ChampionOfTheDay.asset";

        [MenuItem("Gladiator/Setup/Meta/Create Daily Data")]
        public static MetaConfig CreateOrUpdate()
        {
            var feature = Unlock(DailyUnlockPath, "daily", "The Daily Games",
                "The games are proclaimed every day now: one arena, one gladiator, two edicts, the same for everyone. DAILY " +
                "opens on the main menu. The first attempt of the day is scored (Renown ×1.5); after that, it's practice.",
                "Defeat Gorm. The heralds only cry the names of those who have.",
                0, C(UnlockConditionKind.DefeatBoss, "gorm", 1f));
            Unlock(DailyBreadPath, "daily_bread", "Daily Bread",
                "Three days, three proclamations answered. The heralds know your name now. Paid 100 Renown.",
                "Play 3 scored dailies.",
                100, C(UnlockConditionKind.DailiesPlayed, "", 3f));
            Unlock(ChampionOfTheDayPath, "champion_of_the_day", "Champion of the Day",
                "The day's games were yours: the whole city heard it from the heralds. Paid 150 Renown.",
                "Win a scored daily.",
                150, C(UnlockConditionKind.DailiesWon, "", 1f));

            var cfg = PlayerSetup.EnsureAsset<MetaConfig>(MetaSetup.ConfigPath);
            cfg.dailyUnlock = feature;
            if (string.IsNullOrEmpty(cfg.dailySalt)) cfg.dailySalt = DailyGames.DefaultSalt;
            EditorUtility.SetDirty(cfg);
            AssetDatabase.SaveAssets();
            cfg = MetaSetup.CreateOrUpdate(); // collects the three unlocks, saves
            int eligible = 0;
            if (cfg.edicts != null) foreach (var e in cfg.edicts) if (e != null && e.dailyEligible) eligible++;
            Debug.Log($"[DailySetup] 3 daily unlocks; daily: Renown ×{1f + cfg.dailyRenownBonus:0.##}, {cfg.dailyEdicts} of {eligible} " +
                      $"eligible edicts, salt \"{cfg.dailySalt}\", ledger {cfg.dailyLedger}; {cfg.unlocks.Length} unlocks in MetaConfig");
            if (eligible < cfg.dailyEdicts)
                Debug.LogWarning($"[DailySetup] only {eligible} daily-eligible edicts for {cfg.dailyEdicts} a day (run Create Edict Data first)");
            return cfg;
        }

        /// <summary>A challenge (conditions grant it at a run's end or in the menu's CheckUnlocks); every field rewritten.</summary>
        static UnlockDef Unlock(string path, string id, string name, string description, string hint, int bounty,
            params UnlockCondition[] conditions)
        {
            var u = AssetDatabase.LoadAssetAtPath<UnlockDef>(path);
            if (u == null)
            {
                u = ScriptableObject.CreateInstance<UnlockDef>();
                AssetDatabase.CreateAsset(u, path);
            }
            u.id = id; u.displayName = name; u.description = description; u.hint = hint;
            u.category = UnlockCategory.Challenge;
            u.conditions = conditions;
            u.renownCost = 0;
            u.renownBounty = bounty;
            u.addsToPool = Array.Empty<UpgradeDef>();
            EditorUtility.SetDirty(u);
            return u;
        }

        static UnlockCondition C(UnlockConditionKind k, string id, float amount) => new UnlockCondition { kind = k, id = id, amount = amount };
    }
}
