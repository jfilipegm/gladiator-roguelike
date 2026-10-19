using System;
using UnityEngine;

namespace Gladiator.Core
{
    /// <summary>Flat adds to the base, Percent adds to a shared % bonus, Multiply multiplies the result.</summary>
    public enum ModOp { Flat, Percent, Multiply }

    /// <summary>One modifier, e.g. (Damage, Percent, 0.12) = +12% damage, (MaxHp, Flat, 20) = +20 max HP.</summary>
    [Serializable]
    public struct StatMod
    {
        public StatId stat;
        public ModOp op;
        public float value;

        public StatMod(StatId stat, ModOp op, float value) { this.stat = stat; this.op = op; this.value = value; }

        /// <summary>
        /// This modifier scaled by `scale` (card rarity): Flat/Percent multiply the value, Multiply scales the bonus
        /// part (×1.2 at scale 2 → ×1.4). Whole-number stats never scale.
        /// </summary>
        public float Scaled(float scale)
        {
            if (StatInfo.IsInteger(stat)) return value;
            return op == ModOp.Multiply ? 1f + (value - 1f) * scale : value * scale;
        }
    }

    /// <summary>
    /// Layered stats (CLAUDE.md §4): value = (base + Σflat) × (1 + Σpercent) × Πmultiply. Fixed arrays, no allocation
    /// after construction. Remove(m) undoes Add(m) with the same scale. Version bumps on every change so readers can
    /// cache derived values.
    /// </summary>
    public sealed class StatBlock
    {
        readonly float[] _base = new float[StatInfo.Count];
        readonly float[] _flat = new float[StatInfo.Count];
        readonly float[] _pct = new float[StatInfo.Count];
        readonly float[] _mul = new float[StatInfo.Count];
        readonly float[] _value = new float[StatInfo.Count];

        public int Version { get; private set; }

        public StatBlock() => Reset();

        public float this[StatId s] => _value[(int)s];
        public float Get(StatId s) => _value[(int)s];
        public float GetBase(StatId s) => _base[(int)s];

        public void SetBase(StatId s, float value)
        {
            _base[(int)s] = value;
            Recalc((int)s);
        }

        public void Add(in StatMod m, float scale = 1f) => Apply(m, scale, true);
        public void Remove(in StatMod m, float scale = 1f) => Apply(m, scale, false);

        /// <summary>Defaults back, every modifier gone.</summary>
        public void Reset()
        {
            for (int i = 0; i < StatInfo.Count; i++)
            {
                _base[i] = StatInfo.DefaultBase((StatId)i);
                _flat[i] = _pct[i] = 0f;
                _mul[i] = 1f;
                Recalc(i);
            }
        }

        void Apply(in StatMod m, float scale, bool add)
        {
            int s = (int)m.stat;
            float v = m.Scaled(scale);
            switch (m.op)
            {
                case ModOp.Flat: _flat[s] += add ? v : -v; break;
                case ModOp.Percent: _pct[s] += add ? v : -v; break;
                case ModOp.Multiply:
                    v = Mathf.Max(1e-4f, v);
                    _mul[s] = add ? _mul[s] * v : _mul[s] / v;
                    break;
            }
            Recalc(s);
        }

        void Recalc(int s)
        {
            _value[s] = (_base[s] + _flat[s]) * Mathf.Max(0f, 1f + _pct[s]) * _mul[s];
            Version++;
        }
    }
}
