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

    /// <summary>
    /// Everything that survives between runs (CLAUDE.md §4: versioned JSON in persistentDataPath). Lifetime records feed
    /// the unlock conditions; `unlocked` holds UnlockDef ids, `flags` secrets and discoveries. Add fields freely: missing
    /// ones load as their defaults, and SaveSystem.Migrate bumps old files.
    /// </summary>
    [Serializable]
    public sealed class SaveData
    {
        public const int CurrentVersion = 1;

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
        public List<IdCount> weaponKills = new List<IdCount>();
        /// <summary>Per boss: the lowest HP the player had when it fell, in ‰ (0–1000). Added in 0.4.0 (a missing
        /// list in an older file is filled by Migrate; no version bump needed).</summary>
        public List<IdCount> bossKillLowestHp = new List<IdCount>();
        public List<string> unlocked = new List<string>();
        public List<string> flags = new List<string>();

        public static long Count(List<IdCount> list, string id)
        {
            if (list == null || string.IsNullOrEmpty(id)) return 0;
            for (int i = 0; i < list.Count; i++) if (list[i].id == id) return list[i].count;
            return 0;
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
            weaponKills ??= new List<IdCount>();
            bossKillLowestHp ??= new List<IdCount>();
            unlocked ??= new List<string>();
            flags ??= new List<string>();
            if (renown < 0) renown = 0;
            // v0 (no version field) → v1: nothing to convert yet.
            if (version < CurrentVersion) version = CurrentVersion;
        }
    }
}
