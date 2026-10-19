using System.Collections.Generic;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>Kills one equipped weapon made this run (unlock conditions: "kill N enemies with weapon W").</summary>
    public struct WeaponKills
    {
        public string Id;
        public int Kills;
    }

    /// <summary>One finished run, as the meta layer sees it.</summary>
    public struct RunSummary
    {
        public RunOutcome Outcome;
        public float Elapsed;
        public int Kills;
        public int GoldEarned;
        public int Level;
        public int ArenaIndex;
        public string GladiatorId;
        /// <summary>BossDef.id of the boss beaten this run (null = none).</summary>
        public string BossDefeated;
        public WeaponKills[] Weapons;
    }

    /// <summary>What a run earned: shown on the end screen.</summary>
    public sealed class RunRewards
    {
        public int Renown;
        public int RenownTotal;
        public readonly List<UnlockDef> NewUnlocks = new List<UnlockDef>();
    }

    /// <summary>
    /// The save file in memory plus the rules that move it: a finished run adds its records and Renown, then every
    /// UnlockDef whose conditions now hold unlocks (once). Upgrades an unearned unlock holds back never reach the cards.
    /// </summary>
    public static class MetaProgress
    {
        static SaveData _data;

        public static SaveData Data => _data ??= SaveSystem.Load();
        /// <summary>The last committed run's rewards (null until a run ends this session).</summary>
        public static RunRewards Last { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _data = null; Last = null; }

        /// <summary>Forget the in-memory copy (tests, or after SaveSystem.PathOverride changes).</summary>
        public static void Reload() { _data = null; Last = null; }

        public static bool IsUnlocked(UnlockDef u) => u != null && Data.IsUnlocked(u.id);

        /// <summary>Is `upgrade` held back by an unlock in `meta` that `data` hasn't earned?</summary>
        public static bool IsLocked(UpgradeDef upgrade, MetaConfig meta, SaveData data)
        {
            if (upgrade == null || meta == null || meta.unlocks == null || data == null) return false;
            foreach (var u in meta.unlocks)
            {
                if (u == null || u.addsToPool == null || data.IsUnlocked(u.id)) continue;
                foreach (var a in u.addsToPool) if (a == upgrade) return true;
            }
            return false;
        }

        public static bool IsLocked(UpgradeDef upgrade, MetaConfig meta) => IsLocked(upgrade, meta, Data);

        /// <summary>A discovery (the rattling crate…): into the save at once, so it's kept even if the run is lost or
        /// quit. Unlocks waiting on it are granted when the run ends (or by CheckUnlocks). False if already set.</summary>
        public static bool SetFlag(string id)
        {
            if (string.IsNullOrEmpty(id) || Data.HasFlag(id)) return false;
            Data.flags.Add(id);
            SaveSystem.Save(Data);
            Debug.Log("[Meta] flag set: " + id);
            return true;
        }

        /// <summary>Grants every unlock whose conditions already hold (the menu calls it, for runs that never ended).</summary>
        public static int CheckUnlocks(MetaConfig meta)
        {
            if (meta == null || meta.unlocks == null) return 0;
            int n = 0;
            foreach (var u in meta.unlocks)
            {
                if (u == null || Data.IsUnlocked(u.id) || !AllMet(u, Data)) continue;
                Data.unlocked.Add(u.id);
                n++;
            }
            if (n > 0) SaveSystem.Save(Data);
            return n;
        }

        /// <summary>Records the run, saves, and returns what it earned (also kept in Last).</summary>
        public static RunRewards CommitRun(in RunSummary run, MetaConfig meta)
        {
            var rewards = Apply(run, meta, Data);
            SaveSystem.Save(Data);
            Last = rewards;
            Debug.Log($"[Meta] run committed: +{rewards.Renown} Renown (total {rewards.RenownTotal}), {rewards.NewUnlocks.Count} new unlock(s)");
            return rewards;
        }

        /// <summary>Pure: applies `run` to `data` (no file IO).</summary>
        public static RunRewards Apply(in RunSummary run, MetaConfig meta, SaveData data)
        {
            var r = new RunRewards();
            data.runs++;
            bool won = run.Outcome == RunOutcome.Survived;
            if (won) data.victories++;
            if (run.Elapsed > data.bestTime) data.bestTime = run.Elapsed;
            data.lifetimeKills += Mathf.Max(0, run.Kills);
            data.lifetimeGold += Mathf.Max(0, run.GoldEarned);
            if (run.ArenaIndex > data.deepestArena) data.deepestArena = run.ArenaIndex;
            if (!string.IsNullOrEmpty(run.BossDefeated)) SaveData.Add(data.bossKills, run.BossDefeated, 1);
            if (won) SaveData.Add(data.gladiatorWins, run.GladiatorId, 1);
            if (run.Weapons != null)
                foreach (var w in run.Weapons) SaveData.Add(data.weaponKills, w.Id, w.Kills);

            r.Renown = meta != null ? meta.RenownFor(run) : 0;
            data.renown += r.Renown;
            data.lifetimeRenown += r.Renown;
            r.RenownTotal = data.renown;

            if (meta != null && meta.unlocks != null)
                foreach (var u in meta.unlocks)
                {
                    if (u == null || data.IsUnlocked(u.id) || !AllMet(u, data)) continue;
                    data.unlocked.Add(u.id);
                    r.NewUnlocks.Add(u);
                }
            return r;
        }

        public static bool AllMet(UnlockDef u, SaveData data)
        {
            if (u.conditions == null || u.conditions.Length == 0) return false; // no conditions = not earnable yet
            foreach (var c in u.conditions) if (!Met(c, data)) return false;
            return true;
        }

        public static bool Met(in UnlockCondition c, SaveData d)
        {
            long times = (long)Mathf.Max(1f, c.amount);
            switch (c.kind)
            {
                case UnlockConditionKind.DefeatBoss: return SaveData.Count(d.bossKills, c.id) >= times;
                case UnlockConditionKind.ReachArena: return d.deepestArena >= c.amount;
                case UnlockConditionKind.SurviveSeconds: return d.bestTime >= c.amount;
                case UnlockConditionKind.WinWithGladiator: return SaveData.Count(d.gladiatorWins, c.id) >= times;
                case UnlockConditionKind.KillsWithWeapon: return SaveData.Count(d.weaponKills, c.id) >= c.amount;
                case UnlockConditionKind.LifetimeKills: return d.lifetimeKills >= c.amount;
                case UnlockConditionKind.LifetimeGold: return d.lifetimeGold >= c.amount;
                case UnlockConditionKind.LifetimeRenown: return d.lifetimeRenown >= c.amount;
                case UnlockConditionKind.Flag: return d.HasFlag(c.id);
                default: return false;
            }
        }
    }
}
