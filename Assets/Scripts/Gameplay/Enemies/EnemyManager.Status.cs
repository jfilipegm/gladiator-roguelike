using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// Status effects on enemies (M2.6): one slot per StatusKind per enemy, struct-of-arrays like the rest of the
    /// manager. Timers run every frame; damage-over-time and the status puffs happen on a shared per-enemy tick so 300
    /// burning enemies cost 2 damage events a second each, not 60. Boons add damage bonuses against statused enemies
    /// through BonusVsStatus / BonusPerStack (rebuilt by BoonEffects when the build changes).
    /// </summary>
    public sealed partial class EnemyManager
    {
        public const int StatusSlots = 4;

        [Header("Status effects")]
        [Tooltip("Seconds between damage-over-time ticks (and status puffs) on one enemy.")]
        public float statusTickInterval = 0.5f;
        [Tooltip("Status puff particles emitted per frame at most (hundreds of burning enemies).")]
        public int maxStatusPuffsPerFrame = 48;

        /// <summary>+fraction damage from player hits against enemies carrying status slot k (Boons).</summary>
        public readonly float[] BonusVsStatus = new float[StatusSlots];
        /// <summary>+fraction damage from player hits per stack of status slot k on the target (Boons).</summary>
        public readonly float[] BonusPerStack = new float[StatusSlots];

        readonly StatusDef[] _statusDefs = new StatusDef[StatusSlots];
        float[] _stTime, _stPower, _stTick;
        int[] _stStacks;
        byte[] _stMask;
        int _puffsThisFrame;

        void AllocStatus()
        {
            _stTime = new float[capacity * StatusSlots];
            _stPower = new float[capacity * StatusSlots];
            _stStacks = new int[capacity * StatusSlots];
            _stTick = new float[capacity];
            _stMask = new byte[capacity];
        }

        void ClearStatus(int i)
        {
            _stMask[i] = 0;
            _stTick[i] = 0f;
            int b = i * StatusSlots;
            for (int k = 0; k < StatusSlots; k++) { _stTime[b + k] = 0f; _stPower[b + k] = 0f; _stStacks[b + k] = 0; }
        }

        static int SlotOf(StatusKind k) => (int)k - 1;

        public bool HasStatus(int i, StatusKind k) => k != StatusKind.None && (_stMask[i] & (1 << SlotOf(k))) != 0;
        public int StatusStacks(int i, StatusKind k) => HasStatus(i, k) ? _stStacks[i * StatusSlots + SlotOf(k)] : 0;
        public float StatusPower(int i, StatusKind k) => HasStatus(i, k) ? _stPower[i * StatusSlots + SlotOf(k)] : 0f;
        public float StatusTime(int i, StatusKind k) => HasStatus(i, k) ? _stTime[i * StatusSlots + SlotOf(k)] : 0f;
        /// <summary>The StatusDef last applied for kind `k` (colours, names); null if none was ever applied.</summary>
        public StatusDef StatusDefOf(StatusKind k) => k == StatusKind.None ? null : _statusDefs[SlotOf(k)];

        /// <summary>Puts `stacks` of `def` on enemy i with `power` damage per second per stack (0 for pure debuffs).</summary>
        public void ApplyStatus(int i, StatusDef def, float power, int stacks = 1)
        {
            if (def == null || def.kind == StatusKind.None || !IsAlive(i)) return;
            int s = def.Slot, at = i * StatusSlots + s;
            _statusDefs[s] = def;
            if (_stMask[i] == 0) _stTick[i] = statusTickInterval; // first tick one interval after it lands
            StatusMath.Apply(def.stacking, def.duration, def.maxStacks, ref _stTime[at], ref _stPower[at], ref _stStacks[at], power, stacks);
            _stMask[i] |= (byte)(1 << s);
        }

        /// <summary>Damage multiplier from the target's statuses: their own vulnerability (Shock) for everything, plus
        /// the player's Boon bonuses for direct hits (not for status ticks, so bleed never scales with itself).</summary>
        float StatusDamageMul(int i, bool statusTick)
        {
            float mul = 1f;
            int m = _stMask[i], b = i * StatusSlots;
            for (int k = 0; k < StatusSlots; k++)
            {
                if ((m & (1 << k)) == 0) continue;
                var def = _statusDefs[k];
                if (def != null) mul += def.damageTakenBonus;
                if (!statusTick) mul += BonusVsStatus[k] + BonusPerStack[k] * _stStacks[b + k];
            }
            return mul;
        }

        /// <summary>Slow: the strongest speed multiplier among the target's statuses.</summary>
        float StatusSpeedMul(int i)
        {
            float mul = 1f;
            int m = _stMask[i];
            for (int k = 0; k < StatusSlots; k++)
                if ((m & (1 << k)) != 0 && _statusDefs[k] != null) mul = Mathf.Min(mul, _statusDefs[k].speedMultiplier);
            return mul;
        }

        /// <summary>Counts timers down; on the enemy's tick deals damage over time and puffs. False if it died.</summary>
        bool TickStatus(int i, float dt)
        {
            int b = i * StatusSlots;
            for (int k = 0; k < StatusSlots; k++)
            {
                if ((_stMask[i] & (1 << k)) == 0) continue;
                _stTime[b + k] -= dt;
                if (_stTime[b + k] > 0f) continue;
                _stMask[i] &= (byte)~(1 << k);
                _stStacks[b + k] = 0;
                _stPower[b + k] = 0f;
            }
            if (_stMask[i] == 0) return true;
            _stTick[i] -= dt;
            if (_stTick[i] > 0f) return true;
            _stTick[i] += statusTickInterval;
            for (int k = 0; k < StatusSlots; k++)
            {
                if ((_stMask[i] & (1 << k)) == 0) continue;
                var def = _statusDefs[k];
                if (deathPop != null && def != null && _puffsThisFrame < maxStatusPuffsPerFrame)
                {
                    _puffsThisFrame++;
                    deathPop.StatusPuff(_pos[i] + Vector3.up * (1.1f * _scale[i]), _radius[i], def.color);
                }
                float dmg = StatusMath.TickDamage(_stPower[b + k], _stStacks[b + k], statusTickInterval);
                if (dmg <= 0f) continue;
                ApplyDamage(i, new DamageInfo
                {
                    Amount = dmg, Flags = DamageFlags.Status | DamageFlags.Proc, Point = _pos[i],
                    SourceId = def != null ? def.RuntimeId : 0, Status = (StatusKind)(k + 1),
                });
                if (!_active[i]) return false;
            }
            return true;
        }
    }
}
