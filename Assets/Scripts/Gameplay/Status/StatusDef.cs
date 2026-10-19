using Gladiator.Core;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>The status slots an enemy can carry at once (one of each). None = "not a status" in damage/hit events.</summary>
    public enum StatusKind : byte { None = 0, Burn = 1, Bleed = 2, Shock = 3, Slow = 4, Root = 5 }

    /// <summary>Refresh: one instance, re-applying resets the timer and keeps the stronger power. Stack: each
    /// application adds stacks (up to maxStacks) and resets the timer; all stacks fall off together.</summary>
    public enum StatusStacking { Refresh, Stack }

    /// <summary>
    /// A status effect as data (CLAUDE.md §4): duration, stacking rule, what it does while active. Damage over time
    /// comes from whoever applies it (power = damage per second per stack), so the same Burn can be weak or strong.
    /// Ticks are shared per enemy (EnemyManager.statusTickInterval). Player-owned statuses use player VFX colours
    /// (gold / crimson / cyan), never the orange-red reserved for things that hurt the player.
    /// </summary>
    [CreateAssetMenu(menuName = "Gladiator/Status", fileName = "SE_")]
    public sealed class StatusDef : ScriptableObject
    {
        public string id = "burn";
        public string displayName = "Burn";
        [Tooltip("Card text: \"Burning enemies…\".")]
        public string adjective = "Burning";
        public StatusKind kind = StatusKind.Burn;
        public StatusStacking stacking = StatusStacking.Refresh;
        [Min(0.1f)] public float duration = 3f;
        [Min(1)] public int maxStacks = 1;
        [Tooltip("Extra damage the enemy takes from everything while this is on it (Shock: 0.2 = +20 %).")]
        public float damageTakenBonus;
        [Tooltip("Enemy move speed multiplier while active (Slow; Root = 0). Scripted enemies (bosses) ignore it.")]
        [Range(0f, 1f)] public float speedMultiplier = 1f;
        [Tooltip("Particles puffed on each status tick and the colour of its damage numbers.")]
        public Color color = new Color(1f, 0.85f, 0.3f);

        public int RuntimeId => GameInfo.StableId(id);
        public int Slot => (int)kind - 1;
    }

    /// <summary>Stacking maths, pure so EditMode tests can pin the rules.</summary>
    public static class StatusMath
    {
        /// <summary>Applies `addStacks` of a status with `addPower` (dps per stack) onto the current state.</summary>
        public static void Apply(StatusStacking rule, float duration, int maxStacks,
            ref float time, ref float power, ref int stacks, float addPower, int addStacks)
        {
            bool active = stacks > 0 && time > 0f;
            power = active ? Mathf.Max(power, addPower) : addPower;
            if (rule == StatusStacking.Refresh) stacks = 1;
            else stacks = Mathf.Clamp((active ? stacks : 0) + Mathf.Max(1, addStacks), 1, Mathf.Max(1, maxStacks));
            time = duration;
        }

        /// <summary>Damage one tick deals: power per stack per second × stacks × tick length.</summary>
        public static float TickDamage(float power, int stacks, float interval) => power * stacks * interval;
    }
}
