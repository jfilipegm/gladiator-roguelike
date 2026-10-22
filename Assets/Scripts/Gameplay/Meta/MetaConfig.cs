using System;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// Meta progression rules: how much Renown a run earns, and every UnlockDef in the game. Renown is per run, more for
    /// time survived, kills, deeper arenas and bosses (CLAUDE.md §6.6), × the edicts' bonus (R7); in the slice it's a
    /// score that will gate arena tiers and cosmetics later. Also the list of edicts and the unlock that opens them, and
    /// the Daily Games' rules (R8: the scored attempt pays × (1 + dailyRenownBonus) on top of the edicts).
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

        [Header("Edicts (R7)")]
        [Tooltip("Every edict, in the order the Edicts panel shows them.")]
        public RunModifierDef[] edicts = Array.Empty<RunModifierDef>();
        [Tooltip("The Edicts open with this (The Editor Takes Notice). Empty = open.")]
        public UnlockDef edictsUnlock;

        [Header("Daily Games (R8)")]
        [Tooltip("The DAILY button opens with this (The Daily Games). Empty = open.")]
        public UnlockDef dailyUnlock;
        [Tooltip("The scored attempt's Renown × (1 + this), on top of the edicts (0.5 = ×1.5). Practice gets none.")]
        public float dailyRenownBonus = 0.5f;
        [Tooltip("Edicts the daily draws (among the dailyEligible ones, owned or not).")]
        public int dailyEdicts = 2;
        [Tooltip("Mixed into every daily's seed. Change it after a content change so old dailies stop pretending to be comparable.")]
        public string dailySalt = DailyGames.DefaultSalt;
        [Tooltip("Scored dailies kept in the save's ledger (the oldest drop off).")]
        public int dailyLedger = 30;

        [Header("Skins (R6)")] public SkinDef[] skins = Array.Empty<SkinDef>(); // R6-STUB(b)

        /// <summary>The run's Renown before its edicts: time, kills, the boss, victory, × the arena's depth.</summary>
        public int BaseRenownFor(in RunSummary run) => Mathf.Max(0, Mathf.RoundToInt(BaseRenown(run)));

        /// <summary>What the run pays: the base × (1 + the edicts' bonus) × (1 + dailyRenownBonus) on the scored daily, win
        /// or lose (bounties are never multiplied). The rounded base the end screen shows is what's multiplied, so its line
        /// adds up ("+4 ×1.45 = +6", not +5).</summary>
        public int RenownFor(in RunSummary run) =>
            Mathf.Max(0, Mathf.RoundToInt(BaseRenownFor(run) * (1f + Mathf.Max(0f, run.EdictBonus)) * DailyMultiplier(run)));

        /// <summary>1 + dailyRenownBonus on the day's scored attempt, else 1 (practice and normal runs).</summary>
        public float DailyMultiplier(in RunSummary run) => run.DailyScored ? 1f + Mathf.Max(0f, dailyRenownBonus) : 1f;

        float BaseRenown(in RunSummary run)
        {
            float r = run.Elapsed / 60f * renownPerMinute + run.Kills / 100f * renownPerHundredKills;
            if (!string.IsNullOrEmpty(run.BossDefeated)) r += renownPerBoss;
            if (run.Outcome == RunOutcome.Survived) r += renownForVictory;
            return r * (1f + deeperArenaBonus * Mathf.Max(0, run.ArenaIndex));
        }
    }
}
