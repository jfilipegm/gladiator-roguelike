using System;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// What a behavioural Boon does (M2.6). New effects need code (BoonEffects); new Boons that combine them are data.
    /// `chance`, `power` and `bonus` scale with the Boon's potency (Σ rarity magnitudes of its takes, so a Rare take
    /// adds 1.8 steps); radius, duration and counts are fixed.
    /// </summary>
    public enum EffectKind
    {
        /// <summary>`chance` on a direct hit to apply `status`; its dps = `power` × the hit's damage.</summary>
        StatusOnHit,
        /// <summary>Crits apply `stacks` of `status`, `power` dps per stack (× player damage).</summary>
        StatusOnCrit,
        /// <summary>Enemies dying with `status` burst for `power` (× player damage) within `radius` and pass the status on.</summary>
        BurstOnDeath,
        /// <summary>Every `everyNth` direct hit, a spark jumps to `count` more enemies within `radius`: `power` (× player damage), applies `status`.</summary>
        ChainSpark,
        /// <summary>Sparks deal +`bonus` to enemies with `status` and apply it to the ones they hit (dps = half the spark).</summary>
        SparkBonus,
        /// <summary>+`bonus` damage per stack of `status` on the target.</summary>
        DamagePerStack,
        /// <summary>+`bonus` damage against enemies with `status`.</summary>
        DamageVsStatus,
        /// <summary>While sliding, leave marks (`radius`, `duration` s) that apply `stacks` of `status` with `power` dps (× player damage).</summary>
        SlideTrail,
    }

    [Serializable]
    public struct BoonEffect
    {
        public EffectKind kind;
        public StatusDef status;
        [Tooltip("Per potency (StatusOnHit).")] public float chance;
        [Tooltip("Per potency: dps factor or damage (see the kind).")] public float power;
        [Tooltip("Per potency: damage bonus fraction (SparkBonus, DamagePerStack, DamageVsStatus).")] public float bonus;
        public float radius;
        public float duration;
        public int count;
        public int stacks;
        public int everyNth;
    }
}
