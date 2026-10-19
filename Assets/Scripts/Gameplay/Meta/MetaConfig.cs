using System;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// Meta progression rules: how much Renown a run earns, and every UnlockDef in the game. Renown is per run, more for
    /// time survived, kills, deeper arenas and bosses (CLAUDE.md §6.6); in the slice it's a score that will gate arena
    /// tiers and cosmetics later.
    /// </summary>
    [CreateAssetMenu(menuName = "Gladiator/Meta Config", fileName = "MetaConfig")]
    public sealed class MetaConfig : ScriptableObject
    {
        [Header("Renown per run")]
        public float renownPerMinute = 4f;
        public float renownPerHundredKills = 2f;
        public int renownPerBoss = 40;
        [Tooltip("Surviving the run (the timer, or the boss in arenas that have one).")]
        public int renownForVictory = 20;
        [Tooltip("Each arena deeper in the run multiplies the Renown earned there by 1 + this × index.")]
        public float deeperArenaBonus = 0.5f;

        [Header("Unlocks")]
        public UnlockDef[] unlocks = Array.Empty<UnlockDef>();

        public int RenownFor(in RunSummary run)
        {
            float r = run.Elapsed / 60f * renownPerMinute + run.Kills / 100f * renownPerHundredKills;
            if (!string.IsNullOrEmpty(run.BossDefeated)) r += renownPerBoss;
            if (run.Outcome == RunOutcome.Survived) r += renownForVictory;
            r *= 1f + deeperArenaBonus * Mathf.Max(0, run.ArenaIndex);
            return Mathf.Max(0, Mathf.RoundToInt(r));
        }
    }
}
