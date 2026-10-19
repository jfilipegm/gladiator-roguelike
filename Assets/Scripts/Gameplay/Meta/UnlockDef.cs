using System;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>What an unlock waits for (CLAUDE.md §6.6). `id` names the boss/gladiator/weapon/flag, `amount` the count.</summary>
    public enum UnlockConditionKind
    {
        DefeatBoss,       // id = BossDef.id, amount = times (≥ 1)
        ReachArena,       // amount = arena index reached (0 = the Slave Pit)
        SurviveSeconds,   // amount = seconds survived in one run
        WinWithGladiator, // id = GladiatorDef.id, amount = wins (≥ 1)
        KillsWithWeapon,  // id = WeaponDef.id, amount = lifetime kills
        LifetimeKills,    // amount
        LifetimeGold,     // amount
        LifetimeRenown,   // amount
        Flag,             // id = a secret / discovery flag set by the world (the rattling crate…)
        DefeatBossBelowHp, // id = BossDef.id, amount = HP fraction (0.3 = beaten with less than 30 % HP left)
    }

    [Serializable]
    public struct UnlockCondition
    {
        public UnlockConditionKind kind;
        public string id;
        public float amount;
    }

    /// <summary>
    /// Meta content that's earned, not bought with flat stats (CLAUDE.md §6.6). All `conditions` must hold; checked when a
    /// run ends. Rewards: upgrades that join the level-up pool (they're never offered while locked). The Codex shows the
    /// `hint` while locked and the `description` once earned.
    /// </summary>
    [CreateAssetMenu(menuName = "Gladiator/Unlock", fileName = "UL_")]
    public sealed class UnlockDef : ScriptableObject
    {
        public string id = "unlock";
        public string displayName = "Unlock";
        [TextArea] public string description = "What you get.";
        [Tooltip("Shown in the Codex while locked: cryptic, never the full answer.")]
        [TextArea] public string hint = "???";
        public UnlockCondition[] conditions = Array.Empty<UnlockCondition>();

        [Header("Rewards")]
        [Tooltip("Upgrades kept out of the level-up pool until this unlocks.")]
        public UpgradeDef[] addsToPool = Array.Empty<UpgradeDef>();
    }
}
