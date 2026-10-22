using System;
using Gladiator.Core;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// An Edict (R7, docs/tracks/R7-design.md §2): a decree of the Editor that makes one run harder for a share of extra
    /// Renown. Only takes power away (CLAUDE.md §6.6). Every effect is a number that defaults to "no change"; RunModifiers
    /// multiplies them together and applies them once when the arena loads. A new edict is a new asset; only a new kind
    /// of effect needs code.
    /// </summary>
    [CreateAssetMenu(menuName = "Gladiator/Edict", fileName = "ED_")]
    public sealed class RunModifierDef : ScriptableObject
    {
        public string id = "edict";
        public string displayName = "Edict";
        [Tooltip("Flavour (the Codex, the panel's focus line).")]
        [TextArea] public string description;
        [Tooltip("The rule in one line: \"Enemies +50 % HP; bosses +25 %\".")]
        public string effectLine;
        [Tooltip("The upgrade icon atlas (Assets/Art/Textures/UI/UpgradeIcons.png).")]
        public Texture2D icon;
        [Tooltip("This edict's row in the atlas (\"ed_<id>\"); -1 = none.")]
        public int iconRow = -1;
        [Tooltip("Extra Renown this edict pays: the run's Renown × (1 + Σ bonus).")]
        [Range(0f, 1f)] public float renownBonus = 0.2f;
        [Tooltip("null = open once MetaConfig.edictsUnlock is earned.")]
        public UnlockDef unlock;
        [Tooltip("The Daily Games may proclaim it (R8).")]
        public bool dailyEligible = true;

        [Header("Player")]
        [Tooltip("Added to the player's stats at load (source: Edict), e.g. (MaxHp, Multiply, 0.6).")]
        public StatMod[] playerStats = Array.Empty<StatMod>();
        [Tooltip("Damage the player takes × this (on top of the arena's).")]
        public float playerDamageTakenMul = 1f;
        [Tooltip("Regen, amphorae, healing cards and heals on kill × this (0 = no healing). Never max-HP growth.")]
        public float healingScale = 1f;

        [Header("Enemies")]
        [Tooltip("Every non-boss enemy's HP × this (on top of the arena's).")]
        public float enemyHpMul = 1f;
        [Tooltip("Bosses' HP × this (Scripted enemies).")]
        public float bossHpMul = 1f;
        [Tooltip("Every enemy's move speed × this.")]
        public float enemySpeedMul = 1f;

        [Header("Director")]
        [Tooltip("Enemies alive × this (the arena's cap always holds).")]
        public float densityMul = 1f;
        [Tooltip("Every swarm's size × this (the arena's cap always holds).")]
        public float swarmMul = 1f;
        [Tooltip("Seconds between elites × this (0.5 = twice as often).")]
        public float eliteEveryMul = 1f;
        [Tooltip("The Director's clock × this: spawn windows, the alive curve, swarms and elites come sooner. The run timer and the boss don't change.")]
        public float clockScale = 1f;

        [Header("Economy and crowd")]
        [Tooltip("Armory Chest prices × this.")]
        public float chestPriceMul = 1f;
        [Tooltip("Favor gained × this.")]
        public float favorGainMul = 1f;
        [Tooltip("The Arena Master's boredom rises × this (the fall is unchanged).")]
        public float boredomMul = 1f;

        [Header("Cards")]
        [Tooltip("Cards a level-up offers + this (clamped 1–4). Chests, the altar and the boss's reward are unchanged.")]
        public int levelUpCardDelta;
    }
}
