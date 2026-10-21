using System.Collections.Generic;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>One level-up card: what, at which rarity, and the level it brings the upgrade to (1 = new).</summary>
    public struct CardOffer
    {
        public UpgradeDef Def;
        public Rarity Rarity;
        public int Level;

        public bool IsNew => Level == 1;
    }

    /// <summary>
    /// Rolls level-up cards (CLAUDE.md §6.5): distinct, eligible upgrades picked by weight, each with a rarity rolled
    /// from UpgradeConfig odds shifted by Luck and clamped to the upgrade's min/max rarity. When nothing is eligible the
    /// slot gets a filler consumable. Vows (P1) share a fixed slice of the odds (UpgradeConfig.vowShare) however big the
    /// rest of the pool is, at most maxVowsPerPick per pick, and only in picks without a rarity floor (level-ups) unless
    /// vowsInRewards. Uses GameRandom (seeded per run) and preallocated buffers: no allocation.
    /// </summary>
    public sealed class UpgradeRoller
    {
        UpgradeDef[] _cand = new UpgradeDef[64];
        float[] _w = new float[64];

        /// <summary>Can Vows roll in this pick? Rewards (a rarity floor: chests, the altar, the boss) are pure upside unless vowsInRewards.</summary>
        public bool VowsAllowed(UpgradeConfig config) => config != null && (config.vowsInRewards || MinRarity == Rarity.Common);

        /// <summary>
        /// The weight each eligible Vow gets in a pick where the other eligible cards weigh `othersTotal` and the eligible
        /// Vows' own weights sum to `vowsTotal`: together they make up vowShare of the odds. With nothing else eligible
        /// the Vows keep their own weights.
        /// </summary>
        public static float VowWeightScale(UpgradeConfig config, float othersTotal, float vowsTotal)
        {
            if (vowsTotal <= 0f || othersTotal <= 0f) return 1f;
            float share = Mathf.Clamp(config.vowShare, 0f, 0.95f);
            return othersTotal * share / (1f - share) / vowsTotal;
        }

        /// <summary>Can `d` be offered right now? Not banished, not maxed; if it's new: not locked by meta progression and a free slot.</summary>
        public static bool IsEligible(UpgradeDef d, BuildState build, UpgradeConfig config)
        {
            if (d == null || d.kind == UpgradeKind.Consumable || d.weight <= 0f || build.IsBanished(d)) return false;
            if (d.evolvesFrom != null) return false; // evolutions come only through OfferEvolutions
            int level = build.LevelOf(d);
            if (level == 0 && build.IsLocked(d)) return false; // locked = never a new pick; what you hold still levels
            if (level == 0 && d.kind == UpgradeKind.Weapon && build.IsEvolved(d)) return false; // it lives on as its evolution
            if (level >= d.maxLevel) return false;
            if (level == 0 && d.requiresAny != null && d.requiresAny.Length > 0)
            {
                bool met = false;
                for (int r = 0; r < d.requiresAny.Length && !met; r++) met = d.requiresAny[r] != null && build.LevelOf(d.requiresAny[r]) > 0;
                if (!met) return false;
            }
            if (level == 0)
            {
                int slots = d.kind == UpgradeKind.Weapon ? config.weaponSlots : config.boonSlots;
                if (build.CountOf(d.kind) >= slots) return false;
            }
            return true;
        }

        /// <summary>
        /// Phase 5.5: `d` is an evolution whose weapon is at its max level with the catalyst owned, and it isn't taken,
        /// banished or held back by meta progression.
        /// </summary>
        public static bool IsEvolutionReady(UpgradeDef d, BuildState build)
        {
            if (d == null || d.evolvesFrom == null || build.IsBanished(d) || build.IsLocked(d) || build.LevelOf(d) > 0) return false;
            if (build.LevelOf(d.evolvesFrom) < d.evolvesFrom.maxLevel) return false;
            return d.catalyst == null || build.LevelOf(d.catalyst) > 0;
        }

        public static Rarity RollRarity(UpgradeConfig config, float luck, Rarity min, Rarity max)
        {
            if (max < min) max = min;
            float total = 0f;
            for (var r = min; r <= max; r++) total += config.RarityWeight(r, luck);
            float x = GameRandom.Value * total;
            for (var r = min; r <= max; r++)
            {
                x -= config.RarityWeight(r, luck);
                if (x < 0f) return r;
            }
            return max;
        }

        /// <summary>Rarity floor for the cards rolled next (chests: Uncommon, the Blood Altar: Rare). Common = no floor.</summary>
        public Rarity MinRarity = Rarity.Common;
        /// <summary>Phase 5.5: put a ready evolution in the first slot (chest, altar and boss rewards).</summary>
        public bool OfferEvolutions;

        /// <summary>Fills offers[0..count) with fresh cards. Returns how many were filled (fewer if even fillers run out).</summary>
        public int Roll(IReadOnlyList<UpgradeDef> pool, IReadOnlyList<UpgradeDef> fallbacks, BuildState build,
            UpgradeConfig config, float luck, CardOffer[] offers, int count)
        {
            int filled = 0;
            if (OfferEvolutions && count > 0 && pool != null)
                for (int i = 0; i < pool.Count; i++)
                    if (IsEvolutionReady(pool[i], build))
                    {
                        offers[filled++] = new CardOffer { Def = pool[i], Rarity = Rarity.Legendary, Level = 1 };
                        break; // one per reward; a second ready evolution waits for the next chest
                    }
            while (filled < count)
            {
                if (!TryPick(pool, fallbacks, build, config, luck, offers, filled, -1, out var card)) break;
                offers[filled++] = card;
            }
            return filled;
        }

        /// <summary>
        /// Replaces offers[slot] with a card not already shown (after a banish). If nothing is left the slot is removed
        /// and the rest shift down. Returns the new count.
        /// </summary>
        public int Replace(IReadOnlyList<UpgradeDef> pool, IReadOnlyList<UpgradeDef> fallbacks, BuildState build,
            UpgradeConfig config, float luck, CardOffer[] offers, int count, int slot)
        {
            if (TryPick(pool, fallbacks, build, config, luck, offers, count, slot, out var card))
            {
                offers[slot] = card;
                return count;
            }
            for (int i = slot; i < count - 1; i++) offers[i] = offers[i + 1];
            offers[count - 1] = default;
            return count - 1;
        }

        bool TryPick(IReadOnlyList<UpgradeDef> pool, IReadOnlyList<UpgradeDef> fallbacks, BuildState build,
            UpgradeConfig config, float luck, CardOffer[] shown, int shownCount, int ignoreSlot, out CardOffer card)
        {
            card = default;
            int n = 0;
            float total = 0f, vows = 0f;
            bool vowsOk = VowsAllowed(config) && ShownVows(shown, shownCount, ignoreSlot) < config.maxVowsPerPick;
            if (pool != null)
            {
                EnsureCapacity(pool.Count);
                for (int i = 0; i < pool.Count; i++)
                {
                    var d = pool[i];
                    if (!IsEligible(d, build, config) || Shown(d, shown, shownCount, ignoreSlot)) continue;
                    if (d.IsVow && !vowsOk) continue;
                    _cand[n] = d;
                    _w[n] = d.weight;
                    if (d.IsVow) vows += d.weight;
                    else total += d.weight;
                    n++;
                }
                if (vows > 0f)
                {
                    float k = VowWeightScale(config, total, vows);
                    for (int i = 0; i < n; i++) if (_cand[i].IsVow) _w[i] *= k;
                    total += vows * k;
                }
            }
            UpgradeDef pick = null;
            if (n > 0)
            {
                float x = GameRandom.Value * total;
                pick = _cand[n - 1];
                for (int i = 0; i < n; i++)
                {
                    x -= _w[i];
                    if (x < 0f) { pick = _cand[i]; break; }
                }
            }
            else if (fallbacks != null)
            {
                for (int i = 0; i < fallbacks.Count; i++)
                    if (fallbacks[i] != null && !Shown(fallbacks[i], shown, shownCount, ignoreSlot)) { pick = fallbacks[i]; break; }
            }
            if (pick == null) return false;
            card.Def = pick;
            card.Rarity = pick.kind == UpgradeKind.Consumable ? pick.minRarity
                : RollRarity(config, luck, pick.minRarity > MinRarity ? pick.minRarity : MinRarity, pick.maxRarity);
            card.Level = pick.kind == UpgradeKind.Consumable ? 0 : build.LevelOf(pick) + 1;
            return true;
        }

        static bool Shown(UpgradeDef d, CardOffer[] shown, int count, int ignoreSlot)
        {
            for (int i = 0; i < count; i++) if (i != ignoreSlot && shown[i].Def == d) return true;
            return false;
        }

        static int ShownVows(CardOffer[] shown, int count, int ignoreSlot)
        {
            int n = 0;
            for (int i = 0; i < count; i++) if (i != ignoreSlot && shown[i].Def != null && shown[i].Def.IsVow) n++;
            return n;
        }

        void EnsureCapacity(int n)
        {
            if (_cand.Length >= n) return;
            _cand = new UpgradeDef[n];
            _w = new float[n];
        }
    }
}
