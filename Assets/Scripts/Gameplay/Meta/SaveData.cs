using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>A named counter (JsonUtility can't serialise dictionaries).</summary>
    [Serializable]
    public struct IdCount
    {
        public string id;
        public long count;
    }

    /// <summary>One scored daily in the ledger (R8): which day, who, where, how it went. Oldest first in SaveData.dailies.</summary>
    [Serializable]
    public struct DailyRecord
    {
        /// <summary>yyyymmdd (UTC).</summary>
        public int date;
        /// <summary>RunDef id.</summary>
        public string run;
        /// <summary>GladiatorDef id.</summary>
        public string gladiator;
        /// <summary>The day's edict ids, "a|b".</summary>
        public string edicts;
        /// <summary>(int)RunOutcome: 1 died, 2 survived.</summary>
        public int outcome;
        public float elapsed;
        public int kills;
        /// <summary>The Renown the run paid (bounties not included).</summary>
        public int renown;
        /// <summary>The run seed (a uint; long because JsonUtility and the file stay readable either way).</summary>
        public long seed;
    }

    /// <summary>
    /// Everything that survives between runs (CLAUDE.md §4: versioned JSON in persistentDataPath). Lifetime records feed
    /// the unlock conditions; `unlocked` holds UnlockDef ids, `flags` secrets and discoveries. Add fields freely: missing
    /// ones load as their defaults, and SaveSystem.Migrate bumps old files.
    /// </summary>
    [Serializable]
    public sealed class SaveData
    {
        /// <summary>1: everything up to v0.20.0. 2: R7's edicts (edictsChosen, edictWins, bestEdictWin). 3: R8's Daily
        /// Games (dailyAttempt, dailiesPlayed, dailiesWon, dailies).</summary>
        public const int CurrentVersion = 3;

        public int version = CurrentVersion;
        public int renown;
        public int lifetimeRenown;
        public int runs;
        public int victories;
        public float bestTime;
        public long lifetimeKills;
        public long lifetimeGold;
        public int deepestArena;
        public List<IdCount> bossKills = new List<IdCount>();
        public List<IdCount> gladiatorWins = new List<IdCount>();
        /// <summary>GladiatorDef id → runs finished with them, won or lost (the select screens' tablet). Added in R9;
        /// Migrate seeds an older file's from its wins (every win was a run).</summary>
        public List<IdCount> gladiatorRuns = new List<IdCount>();
        public List<IdCount> weaponKills = new List<IdCount>();
        /// <summary>WeaponDef id → won runs that ended with it in hand. Added in 0.15.0 (filled by Migrate).</summary>
        public List<IdCount> weaponWins = new List<IdCount>();
        /// <summary>Per boss: the lowest HP the player had when it fell, in ‰ (0–1000). Added in 0.4.0 (a missing
        /// list in an older file is filled by Migrate; no version bump needed).</summary>
        public List<IdCount> bossKillLowestHp = new List<IdCount>();
        public List<string> unlocked = new List<string>();
        public List<string> flags = new List<string>();
        // Phase 5.6 (filled by Migrate in older files): records for challenges and the Codex's discoveries.
        public long bestRunKills;
        /// <summary>Evolution card id → times taken.</summary>
        public List<IdCount> evolutions = new List<IdCount>();
        /// <summary>TagSynergyDef id → runs it was switched on in.</summary>
        public List<IdCount> synergies = new List<IdCount>();
        /// <summary>EnemyDef id → lifetime kills (bosses included).</summary>
        public List<IdCount> enemyKills = new List<IdCount>();
        /// <summary>UpgradeDef id → runs it was taken in.</summary>
        public List<IdCount> upgradesTaken = new List<IdCount>();
        /// <summary>Codex entries already shown unlocked/discovered (anything else that's open is NEW).</summary>
        public List<string> codexSeen = new List<string>();
        // R7 Edicts (save v2; filled by Migrate in older files).
        /// <summary>RunModifierDef ids the player last chose on the arena screen (remembered between runs).</summary>
        public List<string> edictsChosen = new List<string>();
        public List<string> equippedSkins = new List<string>(); // R6-STUB(b)
        /// <summary>RunModifierDef id → won runs with that edict on.</summary>
        public List<IdCount> edictWins = new List<IdCount>();
        /// <summary>The most edicts on in one won run (the WinWithEdicts condition, the Codex's Records).</summary>
        public int bestEdictWin;
        // R8 the Daily Games (save v3; filled by Migrate in older files).
        /// <summary>yyyymmdd of the last day whose scored attempt was started (marked at launch: quitting doesn't give another).</summary>
        public int dailyAttempt;
        /// <summary>Scored dailies finished (won or lost; the DailiesPlayed condition).</summary>
        public int dailiesPlayed;
        /// <summary>Scored dailies won (the DailiesWon condition).</summary>
        public int dailiesWon;
        /// <summary>The ledger: the last MetaConfig.dailyLedger scored dailies, oldest first.</summary>
        public List<DailyRecord> dailies = new List<DailyRecord>();

        public static long Count(List<IdCount> list, string id)
        {
            if (list == null || string.IsNullOrEmpty(id)) return 0;
            for (int i = 0; i < list.Count; i++) if (list[i].id == id) return list[i].count;
            return 0;
        }

        public static long Total(List<IdCount> list)
        {
            long n = 0;
            if (list != null) for (int i = 0; i < list.Count; i++) n += list[i].count;
            return n;
        }

        public static void Add(List<IdCount> list, string id, long n)
        {
            if (list == null || string.IsNullOrEmpty(id) || n == 0) return;
            for (int i = 0; i < list.Count; i++)
                if (list[i].id == id) { list[i] = new IdCount { id = id, count = list[i].count + n }; return; }
            list.Add(new IdCount { id = id, count = n });
        }

        /// <summary>Keeps the lowest ‰ HP `bossId` was beaten at.</summary>
        public void RecordBossKillHp(string bossId, int permille)
        {
            if (string.IsNullOrEmpty(bossId)) return;
            bossKillLowestHp ??= new List<IdCount>();
            permille = Mathf.Clamp(permille, 0, 1000);
            for (int i = 0; i < bossKillLowestHp.Count; i++)
                if (bossKillLowestHp[i].id == bossId)
                {
                    if (permille < bossKillLowestHp[i].count) bossKillLowestHp[i] = new IdCount { id = bossId, count = permille };
                    return;
                }
            bossKillLowestHp.Add(new IdCount { id = bossId, count = permille });
        }

        public bool TryBossKillHp(string bossId, out int permille)
        {
            permille = 1000;
            if (bossKillLowestHp == null || string.IsNullOrEmpty(bossId)) return false;
            for (int i = 0; i < bossKillLowestHp.Count; i++)
                if (bossKillLowestHp[i].id == bossId) { permille = (int)bossKillLowestHp[i].count; return true; }
            return false;
        }

        public bool IsUnlocked(string id) => !string.IsNullOrEmpty(id) && unlocked.Contains(id);
        public bool HasFlag(string id) => !string.IsNullOrEmpty(id) && flags.Contains(id);

        /// <summary>Old or hand-edited files: fill missing lists, bump the version.</summary>
        public void Migrate()
        {
            bossKills ??= new List<IdCount>();
            gladiatorWins ??= new List<IdCount>();
            if (gladiatorRuns == null || (gladiatorRuns.Count == 0 && gladiatorWins.Count > 0))
                gladiatorRuns = new List<IdCount>(gladiatorWins); // a file from before R9: at least its wins were runs
            weaponKills ??= new List<IdCount>();
            weaponWins ??= new List<IdCount>();
            bossKillLowestHp ??= new List<IdCount>();
            unlocked ??= new List<string>();
            flags ??= new List<string>();
            evolutions ??= new List<IdCount>();
            synergies ??= new List<IdCount>();
            enemyKills ??= new List<IdCount>();
            upgradesTaken ??= new List<IdCount>();
            codexSeen ??= new List<string>();
            // v1 → v2 (R7): the edict lists; nothing to convert.
            edictsChosen ??= new List<string>();
            edictWins ??= new List<IdCount>();
            if (bestEdictWin < 0) bestEdictWin = 0;
            // v2 → v3 (R8): the daily ledger; nothing to convert.
            dailies ??= new List<DailyRecord>();
            if (dailyAttempt < 0) dailyAttempt = 0;
            if (dailiesPlayed < 0) dailiesPlayed = 0;
            if (dailiesWon < 0) dailiesWon = 0;
            if (renown < 0) renown = 0;
            // v0 (no version field) → v1: nothing to convert. Each step is an idempotent fill, so any older file runs them all.
            if (version < CurrentVersion) version = CurrentVersion;
        }
    }
}
