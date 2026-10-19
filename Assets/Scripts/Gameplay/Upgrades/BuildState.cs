using System.Collections.Generic;
using Gladiator.Core;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// What the player has picked this run: owned upgrades with levels, banished ones, rerolls/banishes spent. Plain C#
    /// (EditMode-testable); PlayerBuild wraps it with the side effects (heal, gold). Lists are preallocated.
    /// </summary>
    public sealed class BuildState
    {
        public struct Owned
        {
            public UpgradeDef Def;
            public int Level;
            public Rarity BestRarity;
            /// <summary>Sum of the rarity magnitudes of every take (Common 1, Rare 1.8…): Boon effects scale with it.</summary>
            public float Potency;
        }

        readonly List<Owned> _owned = new List<Owned>(16);
        readonly List<UpgradeDef> _banished = new List<UpgradeDef>(16);

        public int RerollsUsed;
        public int BanishesUsed;
        /// <summary>Bumps whenever owned upgrades change (HUD strip refresh).</summary>
        public int Version { get; private set; }

        public int OwnedCount => _owned.Count;
        public Owned GetOwned(int i) => _owned[i];
        public int BanishedCount => _banished.Count;

        public int LevelOf(UpgradeDef d)
        {
            for (int i = 0; i < _owned.Count; i++) if (_owned[i].Def == d) return _owned[i].Level;
            return 0;
        }

        public int CountOf(UpgradeKind kind)
        {
            int n = 0;
            for (int i = 0; i < _owned.Count; i++) if (_owned[i].Def.kind == kind) n++;
            return n;
        }

        public bool IsBanished(UpgradeDef d) => _banished.Contains(d);

        public void Banish(UpgradeDef d)
        {
            if (d != null && !_banished.Contains(d)) _banished.Add(d);
        }

        /// <summary>
        /// Takes one level of `d` at `rarity`: adds its per-level modifiers (scaled by the rarity) to `stats`. Consumables
        /// aren't owned (their heal/gold is PlayerBuild's job). Returns the new level (0 for a consumable).
        /// </summary>
        public int Take(UpgradeDef d, Rarity rarity, StatBlock stats, UpgradeConfig config)
        {
            if (d.kind == UpgradeKind.Consumable) return 0;
            float scale = config.Magnitude(rarity);
            if (stats != null && d.perLevel != null) // weapon cards carry their levels on the WeaponDef instead
                for (int m = 0; m < d.perLevel.Length; m++) stats.Add(d.perLevel[m], scale);
            Version++;
            for (int i = 0; i < _owned.Count; i++)
            {
                if (_owned[i].Def != d) continue;
                var o = _owned[i];
                o.Level++;
                o.Potency += scale;
                if (rarity > o.BestRarity) o.BestRarity = rarity;
                _owned[i] = o;
                return o.Level;
            }
            _owned.Add(new Owned { Def = d, Level = 1, BestRarity = rarity, Potency = scale });
            return 1;
        }

        public void Clear()
        {
            _owned.Clear();
            _banished.Clear();
            RerollsUsed = BanishesUsed = 0;
            Version++;
        }
    }
}
