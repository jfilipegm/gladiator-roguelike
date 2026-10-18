using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// Test switches and buffs set from the admin menu (F1) or by the PlaytestBot. They're a separate layer on top of
    /// the run's own bonuses (level-up cards), so "reset" never undoes a real pick. Static: they survive Retry within a
    /// play session and reset when play starts. In M2.4 the buffs become a "Dev" layer in the stat pipeline.
    /// </summary>
    public static class DevSettings
    {
        /// <summary>HP can't drop below 1 (hits, numbers and shake still happen).</summary>
        public static bool GodMode;
        /// <summary>The run timer doesn't advance, so the run never ends by time.</summary>
        public static bool Timeless;

        // Buffs in percent (+50 = ×1.5). Crit is in percentage points of crit chance.
        public static float DamagePct, AttackSpeedPct, RangePct, MaxHpPct, MoveSpeedPct, PickupRangePct, CritPct;

        public static float DamageMul => Mul(DamagePct);
        public static float AttackSpeedMul => Mul(AttackSpeedPct);
        public static float RangeMul => Mul(RangePct);
        public static float MaxHpMul => Mul(MaxHpPct);
        public static float MoveSpeedMul => Mul(MoveSpeedPct);
        public static float PickupRangeMul => Mul(PickupRangePct);
        public static float CritBonus => CritPct * 0.01f;

        public static bool AnyBuff =>
            DamagePct != 0f || AttackSpeedPct != 0f || RangePct != 0f || MaxHpPct != 0f ||
            MoveSpeedPct != 0f || PickupRangePct != 0f || CritPct != 0f;

        static float Mul(float pct) => Mathf.Max(0.05f, 1f + pct * 0.01f);

        public static void ResetBuffs()
        {
            DamagePct = AttackSpeedPct = RangePct = MaxHpPct = MoveSpeedPct = PickupRangePct = CritPct = 0f;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ResetAll()
        {
            GodMode = false;
            Timeless = false;
            ResetBuffs();
        }
    }
}
