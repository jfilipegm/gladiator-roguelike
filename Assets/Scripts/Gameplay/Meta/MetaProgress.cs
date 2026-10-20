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
        /// <summary>The arena's tier (0 = the Slave Pit, 1 = the Provincial Pit…), not its index in the run.</summary>
        public int ArenaIndex;
        public string GladiatorId;
        /// <summary>BossDef.id of the boss beaten this run (null = none).</summary>
        public string BossDefeated;
        /// <summary>The player's HP fraction when that boss fell (0 = a summary built without it counts as full HP).</summary>
        public float BossDefeatedHp01;
        public WeaponKills[] Weapons;
        /// <summary>Kills per enemy type this run, by EnemyDef id (the Bestiary).</summary>
        public IdCount[] EnemyKills;
        /// <summary>UpgradeDef ids taken this run (an evolved weapon's base card too).</summary>
        public string[] Upgrades;
        /// <summary>Evolution card ids taken this run.</summary>
        public string[] Evolutions;
        /// <summary>TagSynergyDef ids switched on this run.</summary>
        public string[] Synergies;
    }

    public enum BuyResult { Bought, NotForSale, AlreadyOwned, TooExpensive }

    /// <summary>What a run earned: shown on the end screen.</summary>
    public sealed class RunRewards
    {
        public int Renown;
        /// <summary>Renown from the bounties of unlocks earned by this run (on top of Renown).</summary>
        public int Bounty;
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
            int n = GrantEarned(meta, Data, null, out _);
            if (n > 0) SaveSystem.Save(Data);
            return n;
        }

        /// <summary>Unlocks every earned, non-Market unlock in `meta` and pays their bounties into `data`. Returns how many.</summary>
        static int GrantEarned(MetaConfig meta, SaveData data, List<UnlockDef> into, out int bounty)
        {
            bounty = 0;
            if (meta == null || meta.unlocks == null) return 0;
            int n = 0;
            foreach (var u in meta.unlocks)
            {
                if (u == null || u.category == UnlockCategory.Market || data.IsUnlocked(u.id) || !AllMet(u, data)) continue;
                data.unlocked.Add(u.id);
                bounty += Mathf.Max(0, u.renownBounty);
                into?.Add(u);
                n++;
            }
            data.renown += bounty;
            data.lifetimeRenown += bounty;
            return n;
        }

        /// <summary>Buys a Market unlock with Renown and saves.</summary>
        public static BuyResult Buy(UnlockDef u)
        {
            var r = Buy(u, Data);
            if (r == BuyResult.Bought)
            {
                SaveSystem.Save(Data);
                Debug.Log($"[Meta] bought {u.id} for {u.renownCost} Renown ({Data.renown} left)");
            }
            return r;
        }

        /// <summary>Pure: spends `u.renownCost` of `data`'s Renown on a Market unlock.</summary>
        public static BuyResult Buy(UnlockDef u, SaveData data)
        {
            if (u == null || u.category != UnlockCategory.Market) return BuyResult.NotForSale;
            if (data.IsUnlocked(u.id)) return BuyResult.AlreadyOwned;
            int cost = Mathf.Max(0, u.renownCost);
            if (data.renown < cost) return BuyResult.TooExpensive;
            data.renown -= cost;
            data.unlocked.Add(u.id);
            return BuyResult.Bought;
        }

        /// <summary>Has the Codex shown `id` (an unlock, enemy, upgrade… id) as open yet? Open and unseen = NEW.</summary>
        public static bool Seen(string id) => string.IsNullOrEmpty(id) || Data.codexSeen.Contains(id);

        /// <summary>Marks Codex entries as seen (no save; call SaveSeen once after a batch).</summary>
        public static void MarkSeen(string id)
        {
            if (!string.IsNullOrEmpty(id) && !Data.codexSeen.Contains(id)) Data.codexSeen.Add(id);
        }

        public static void SaveSeen() => SaveSystem.Save(Data);

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
            if (!string.IsNullOrEmpty(run.BossDefeated))
            {
                SaveData.Add(data.bossKills, run.BossDefeated, 1);
                float hp01 = run.BossDefeatedHp01 > 0f ? run.BossDefeatedHp01 : 1f;
                data.RecordBossKillHp(run.BossDefeated, Mathf.RoundToInt(hp01 * 1000f));
            }
            if (won) SaveData.Add(data.gladiatorWins, run.GladiatorId, 1);
            if (run.Weapons != null)
                foreach (var w in run.Weapons) SaveData.Add(data.weaponKills, w.Id, w.Kills);
            if (run.Kills > data.bestRunKills) data.bestRunKills = run.Kills;
            if (run.EnemyKills != null) foreach (var k in run.EnemyKills) SaveData.Add(data.enemyKills, k.id, k.count);
            if (run.Upgrades != null) foreach (var id in run.Upgrades) SaveData.Add(data.upgradesTaken, id, 1);
            if (run.Evolutions != null) foreach (var id in run.Evolutions) SaveData.Add(data.evolutions, id, 1);
            if (run.Synergies != null) foreach (var id in run.Synergies) SaveData.Add(data.synergies, id, 1);

            r.Renown = meta != null ? meta.RenownFor(run) : 0;
            data.renown += r.Renown;
            data.lifetimeRenown += r.Renown;
            GrantEarned(meta, data, r.NewUnlocks, out r.Bounty);
            r.RenownTotal = data.renown;
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
                case UnlockConditionKind.DefeatBossBelowHp:
                    return d.TryBossKillHp(c.id, out int permille) && permille < Mathf.RoundToInt(c.amount * 1000f);
                case UnlockConditionKind.ReachArena: return d.deepestArena >= c.amount;
                case UnlockConditionKind.SurviveSeconds: return d.bestTime >= c.amount;
                case UnlockConditionKind.WinWithGladiator: return SaveData.Count(d.gladiatorWins, c.id) >= times;
                case UnlockConditionKind.KillsWithWeapon: return SaveData.Count(d.weaponKills, c.id) >= c.amount;
                case UnlockConditionKind.LifetimeKills: return d.lifetimeKills >= c.amount;
                case UnlockConditionKind.LifetimeGold: return d.lifetimeGold >= c.amount;
                case UnlockConditionKind.LifetimeRenown: return d.lifetimeRenown >= c.amount;
                case UnlockConditionKind.Flag: return d.HasFlag(c.id);
                case UnlockConditionKind.RunKills: return d.bestRunKills >= c.amount;
                case UnlockConditionKind.Evolve: return Evolutions(c.id, d) >= times;
                case UnlockConditionKind.SynergiesFound: return d.synergies.Count >= c.amount;
                default: return false;
            }
        }

        static long Evolutions(string id, SaveData d) => string.IsNullOrEmpty(id) ? SaveData.Total(d.evolutions) : SaveData.Count(d.evolutions, id);

        /// <summary>A condition's progress for the Codex bar: `current` of `target` (current is capped at target).</summary>
        public static void Progress(in UnlockCondition c, SaveData d, out float current, out float target)
        {
            float times = Mathf.Max(1f, c.amount);
            switch (c.kind)
            {
                case UnlockConditionKind.DefeatBoss: current = SaveData.Count(d.bossKills, c.id); target = times; break;
                case UnlockConditionKind.WinWithGladiator: current = SaveData.Count(d.gladiatorWins, c.id); target = times; break;
                case UnlockConditionKind.SurviveSeconds: current = d.bestTime; target = c.amount; break;
                case UnlockConditionKind.KillsWithWeapon: current = SaveData.Count(d.weaponKills, c.id); target = c.amount; break;
                case UnlockConditionKind.LifetimeKills: current = d.lifetimeKills; target = c.amount; break;
                case UnlockConditionKind.LifetimeGold: current = d.lifetimeGold; target = c.amount; break;
                case UnlockConditionKind.LifetimeRenown: current = d.lifetimeRenown; target = c.amount; break;
                case UnlockConditionKind.RunKills: current = d.bestRunKills; target = c.amount; break;
                case UnlockConditionKind.Evolve: current = Evolutions(c.id, d); target = times; break;
                case UnlockConditionKind.SynergiesFound: current = d.synergies.Count; target = c.amount; break;
                default: current = Met(c, d) ? 1f : 0f; target = 1f; break; // yes/no conditions
            }
            target = Mathf.Max(1f, target);
            current = Mathf.Clamp(current, 0f, target);
        }

        /// <summary>A whole unlock's progress 0–1: one condition = its bar, several = the share that's met.</summary>
        public static float Progress01(UnlockDef u, SaveData d)
        {
            if (u == null || u.conditions == null || u.conditions.Length == 0) return 0f;
            if (d.IsUnlocked(u.id)) return 1f;
            if (u.conditions.Length == 1)
            {
                Progress(u.conditions[0], d, out float cur, out float tgt);
                return cur / tgt;
            }
            int met = 0;
            foreach (var c in u.conditions) if (Met(c, d)) met++;
            return met / (float)u.conditions.Length;
        }
    }
}
