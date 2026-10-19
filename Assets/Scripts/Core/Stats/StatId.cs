namespace Gladiator.Core
{
    /// <summary>
    /// Every number an upgrade can touch (CLAUDE.md §4). Multiplier stats start at 1, chance/bonus stats at 0, counts at
    /// their base. Append new stats at the end (assets serialize the index).
    /// </summary>
    public enum StatId
    {
        MaxHp, HpRegen, Armor, MoveSpeed, Damage, AttackSpeed, CritChance, CritDamage, Area,
        Amount, ProjectileSpeed, Duration, Knockback, PickupRange, XpGain, GoldGain, Luck,
        Rerolls, Banishes, ExtraJumps, Pierce,
    }

    /// <summary>Defaults and display rules per stat.</summary>
    public static class StatInfo
    {
        public const int Count = 21;

        static readonly string[] Names =
        {
            "Max HP", "HP Regen/s", "Armor", "Move Speed", "Damage", "Attack Speed", "Crit Chance", "Crit Damage", "Area",
            "Projectiles", "Projectile Speed", "Duration", "Knockback", "Pickup Range", "XP Gain", "Gold Gain", "Luck",
            "Rerolls", "Banishes", "Air Jumps", "Pierce",
        };

        public static string Name(StatId s) => Names[(int)s];

        /// <summary>Value with no modifiers (a GladiatorDef can override it).</summary>
        public static float DefaultBase(StatId s) => s switch
        {
            StatId.MaxHp => 100f,
            StatId.MoveSpeed or StatId.Damage or StatId.AttackSpeed or StatId.Area or StatId.ProjectileSpeed or
                StatId.Duration or StatId.Knockback or StatId.PickupRange or StatId.XpGain or StatId.GoldGain => 1f,
            StatId.Rerolls => 2f,
            StatId.Banishes => 1f,
            _ => 0f,
        };

        /// <summary>Flat values of these stats are fractions shown as percent (0.06 crit chance reads "+6%").</summary>
        public static bool IsFraction(StatId s) =>
            s is StatId.Armor or StatId.CritChance or StatId.CritDamage or StatId.Luck;

        /// <summary>Whole-number stats: card rarity doesn't scale them (a Rare can't give 1.8 air jumps).</summary>
        public static bool IsInteger(StatId s) =>
            s is StatId.Amount or StatId.Rerolls or StatId.Banishes or StatId.ExtraJumps or StatId.Pierce;
    }
}
