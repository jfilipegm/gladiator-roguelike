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
    /// Where a modifier came from (P1 stats page). Untracked is the default, for callers that don't say. Base and
    /// Admin are never added as modifiers: Breakdown reports the base itself, and PlayerStats adds the admin layer.
    /// </summary>
    public enum StatSourceKind : byte { Untracked, Base, Card, Synergy, Trait, Temporary, Altar, Admin }

    /// <summary>A modifier's source: its kind and the asset behind it (the UpgradeDef, TagSynergyDef or trait), if any.</summary>
    public readonly struct StatSource
    {
        public readonly StatSourceKind Kind;
        public readonly UnityEngine.Object Ref;

        public StatSource(StatSourceKind kind, UnityEngine.Object source = null) { Kind = kind; Ref = source; }

        public static StatSource Untracked => default;
        public bool Same(in StatSource o) => Kind == o.Kind && ReferenceEquals(Ref, o.Ref);
    }

    /// <summary>One line of a stat's breakdown: a source and how much of the total it accounts for (in the stat's units).</summary>
    public struct StatShare
    {
        public StatSource Source;
        public float Amount;
        /// <summary>Modifiers this source has on the stat right now (a card taken 3 times = 3).</summary>
        public int Count;
    }

    /// <summary>
    /// Layered stats (CLAUDE.md §4): value = (base + Σflat) × (1 + Σpercent) × Πmultiply. Fixed arrays, no allocation
    /// after construction (the per-source table grows only when a new source touches a stat, e.g. a new card). Remove(m)
    /// undoes Add(m) with the same scale and source. Version bumps on every change so readers can cache derived values.
    /// </summary>
    public sealed class StatBlock
    {
        readonly float[] _base = new float[StatInfo.Count];
        readonly float[] _flat = new float[StatInfo.Count];
        readonly float[] _pct = new float[StatInfo.Count];
        readonly float[] _mul = new float[StatInfo.Count];
        readonly float[] _value = new float[StatInfo.Count];

        // P1: every modifier's source, one entry per (stat, source). Entries stay when their modifiers are removed, so a
        // trait that swaps its modifier every frame reuses its entry.
        struct Entry
        {
            public StatId Stat;
            public StatSource Source;
            public float Flat, Pct, Mul;
            public int Count;
        }

        Entry[] _entries;
        int _entryCount;

        public int Version { get; private set; }
        /// <summary>False for scratch blocks (previews): no per-source table, only the totals.</summary>
        public bool TracksSources { get; }

        public StatBlock() : this(true) { }

        public StatBlock(bool trackSources)
        {
            TracksSources = trackSources;
            _entries = trackSources ? new Entry[24] : Array.Empty<Entry>();
            Reset();
        }

        public float this[StatId s] => _value[(int)s];
        public float Get(StatId s) => _value[(int)s];
        public float GetBase(StatId s) => _base[(int)s];

        public void SetBase(StatId s, float value)
        {
            _base[(int)s] = value;
            Recalc((int)s);
        }

        public void Add(in StatMod m, float scale = 1f) => Apply(m, scale, true, default);
        public void Remove(in StatMod m, float scale = 1f) => Apply(m, scale, false, default);
        public void Add(in StatMod m, float scale, in StatSource source) => Apply(m, scale, true, source);
        public void Remove(in StatMod m, float scale, in StatSource source) => Apply(m, scale, false, source);

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
            _entryCount = 0;
        }

        /// <summary>Copies `other`'s bases and modifier totals (not its sources): previews apply a card to the copy.</summary>
        public void CopyFrom(StatBlock other)
        {
            Array.Copy(other._base, _base, StatInfo.Count);
            Array.Copy(other._flat, _flat, StatInfo.Count);
            Array.Copy(other._pct, _pct, StatInfo.Count);
            Array.Copy(other._mul, _mul, StatInfo.Count);
            Array.Copy(other._value, _value, StatInfo.Count);
            Version++;
        }

        void Apply(in StatMod m, float scale, bool add, in StatSource source)
        {
            int s = (int)m.stat;
            float v = m.Scaled(scale);
            int e = TracksSources ? EntryFor(m.stat, source) : -1;
            switch (m.op)
            {
                case ModOp.Flat:
                    _flat[s] += add ? v : -v;
                    if (e >= 0) _entries[e].Flat += add ? v : -v;
                    break;
                case ModOp.Percent:
                    _pct[s] += add ? v : -v;
                    if (e >= 0) _entries[e].Pct += add ? v : -v;
                    break;
                case ModOp.Multiply:
                    v = Mathf.Max(1e-4f, v);
                    _mul[s] = add ? _mul[s] * v : _mul[s] / v;
                    if (e >= 0) _entries[e].Mul = add ? _entries[e].Mul * v : _entries[e].Mul / v;
                    break;
            }
            if (e >= 0) _entries[e].Count += add ? 1 : -1;
            Recalc(s);
        }

        int EntryFor(StatId stat, in StatSource source)
        {
            for (int i = 0; i < _entryCount; i++)
                if (_entries[i].Stat == stat && _entries[i].Source.Same(source)) return i;
            if (_entryCount == _entries.Length) Array.Resize(ref _entries, _entries.Length * 2); // a new source on this stat: rare, never per frame
            _entries[_entryCount] = new Entry { Stat = stat, Source = source, Mul = 1f };
            return _entryCount++;
        }

        void Recalc(int s)
        {
            _value[s] = (_base[s] + _flat[s]) * Mathf.Max(0f, 1f + _pct[s]) * _mul[s];
            Version++;
        }

        /// <summary>
        /// Splits Get(stat) into the base plus one share per source, in the stat's units, so base + Σshares = the total.
        /// The layers are credited in order: flat sources add their own value; percent sources split the percent layer's
        /// gain over (base + flat) by their share of Σpercent; multiply sources split the last layer's gain by their share
        /// of ln(Πmultiply). into[0] is always the base. Returns how many shares were written (no allocation).
        /// </summary>
        public int Breakdown(StatId stat, StatShare[] into)
        {
            if (into == null || into.Length == 0) return 0;
            int s = (int)stat;
            int n = 0;
            into[n++] = new StatShare { Source = new StatSource(StatSourceKind.Base), Amount = _base[s], Count = 1 };
            float f = _base[s] + _flat[s], p = _pct[s], factor = Mathf.Max(0f, 1f + p), g = f * factor, m = _mul[s];
            float kPct = Mathf.Abs(p) > 1e-6f ? (factor - 1f) / p : 1f; // percent share_i = f × p_i × kPct
            float lnM = Mathf.Log(Mathf.Max(1e-8f, m));
            float kMul = Mathf.Abs(lnM) > 1e-6f ? (m - 1f) / lnM : 1f;  // multiply share_i = g × ln(m_i) × kMul
            for (int i = 0; i < _entryCount && n < into.Length; i++)
            {
                ref var e = ref _entries[i];
                if (e.Stat != stat || e.Count <= 0) continue;
                float amount = e.Flat + f * e.Pct * kPct + g * Mathf.Log(Mathf.Max(1e-8f, e.Mul)) * kMul;
                into[n++] = new StatShare { Source = e.Source, Amount = amount, Count = e.Count };
            }
            return n;
        }
    }
}
