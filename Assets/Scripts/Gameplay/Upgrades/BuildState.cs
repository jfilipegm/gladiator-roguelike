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

        // Meta locks (M3.6): upgrades an unearned unlock holds back. Set once per run; Clear() keeps them.
        readonly List<UpgradeDef> _locked = new List<UpgradeDef>(4);
        public void Lock(UpgradeDef d) { if (d != null && !_locked.Contains(d)) _locked.Add(d); }
        public bool IsLocked(UpgradeDef d) => _locked.Contains(d);

        public void Banish(UpgradeDef d)
        {
            if (d != null && !_banished.Contains(d)) _banished.Add(d);
        }

        /// <summary>
        /// Takes one level of `d` at `rarity`: adds its per-level modifiers (scaled by the rarity; a Vow's drawbacks too)
        /// to `stats`, each tagged with the card as its source (P1 stats page). Consumables aren't owned (their heal/gold
        /// is PlayerBuild's job). Returns the new level (0 for a consumable).
        /// </summary>
        public int Take(UpgradeDef d, Rarity rarity, StatBlock stats, UpgradeConfig config)
        {
            if (d.kind == UpgradeKind.Consumable) return 0;
            float scale = config.Magnitude(rarity);
            d.AddStats(stats, rarity, config, new StatSource(StatSourceKind.Card, d));
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

        /// <summary>Phase 5.5: swaps owned `from` for its evolution `to` in place (same slot and order). False if `from` isn't owned.</summary>
        public bool Evolve(UpgradeDef from, UpgradeDef to)
        {
            for (int i = 0; i < _owned.Count; i++)
            {
                if (_owned[i].Def != from) continue;
                var o = _owned[i];
                o.Def = to;
                o.Level = 1;
                o.BestRarity = Rarity.Legendary;
                _owned[i] = o;
                Version++;
                return true;
            }
            return false;
        }

        /// <summary>True if an owned upgrade is the evolution of `baseCard` (its base is never offered again).</summary>
        public bool IsEvolved(UpgradeDef baseCard)
        {
            for (int i = 0; i < _owned.Count; i++) if (_owned[i].Def.evolvesFrom == baseCard) return true;
            return false;
        }

        /// <summary>Distinct owned upgrades carrying `tag` (weapons and evolutions included).</summary>
        public int TagCount(UpgradeTag tag)
        {
            int n = 0;
            for (int i = 0; i < _owned.Count; i++) if ((_owned[i].Def.tags & tag) != 0) n++;
            return n;
        }

        // Tag synergies (Phase 5.5): switched on once, never off (owned upgrades never go away).
        readonly List<TagSynergyDef> _synergies = new List<TagSynergyDef>(8);
        public int SynergyCount => _synergies.Count;
        public TagSynergyDef GetSynergy(int i) => _synergies[i];
        public bool HasSynergy(TagSynergyDef s) => _synergies.Contains(s);

        /// <summary>
        /// Switches on every synergy in `all` whose tag count is reached and adds its mods to `stats` once. Returns how many
        /// switched on now (they're the last ones in GetSynergy order).
        /// </summary>
        public int UpdateSynergies(IReadOnlyList<TagSynergyDef> all, StatBlock stats)
        {
            if (all == null) return 0;
            int added = 0;
            for (int i = 0; i < all.Count; i++)
            {
                var s = all[i];
                if (s == null || s.tag == UpgradeTag.None || _synergies.Contains(s) || TagCount(s.tag) < s.count) continue;
                _synergies.Add(s);
                if (stats != null && s.mods != null) for (int m = 0; m < s.mods.Length; m++) stats.Add(s.mods[m], 1f, new StatSource(StatSourceKind.Synergy, s));
                added++;
            }
            if (added > 0) Version++;
            return added;
        }

        public void Clear()
        {
            _owned.Clear();
            _banished.Clear();
            _synergies.Clear();
            RerollsUsed = BanishesUsed = 0;
            Version++;
        }
    }
}
