using System;
using UnityEngine;

namespace Gladiator.Gameplay
{
    public enum GiftKind : byte { Amphora, GoldShower, WeaponInSand, Laurel }
    public enum WhimKind : byte { GatesOpen, ChampionChallenge }

    /// <summary>
    /// The crowd's Favor (docs/FAVOR.md, Phase 5.1): what fills the meter, what the crowd throws when it's full, and
    /// the Arena Master's whims when the show gets dull. One asset per arena, so later arenas can be greedier or kinder.
    /// </summary>
    [CreateAssetMenu(menuName = "Gladiator/Favor Config", fileName = "FavorConfig")]
    public sealed class FavorConfig : ScriptableObject
    {
        [Serializable]
        public struct GiftEntry
        {
            public GiftKind kind;
            [Min(0f)] public float weight;
        }

        [Header("Meter")]
        [Tooltip("Favor needed for the first gift.")]
        public float firstThreshold = 100f;
        [Tooltip("Each gift raises the next threshold by this factor.")]
        public float thresholdGrowth = 1.25f;

        [Header("Sources")]
        [Tooltip("Favor per announced kill-streak rung (index = rung of AnnouncerConfig.streaks).")]
        public float[] streakFavor = { 8f, 12f, 16f, 20f };
        public float eliteKill = 6f;
        [Tooltip("Killing a type you haven't killed for `varietyWindow` seconds.")]
        public float variety = 1.5f;
        public float varietyWindow = 20f;
        [Tooltip("A kill while sliding or airborne (movement is a power source).")]
        public float styleKill = 0.25f;
        [Tooltip("Cap on style Favor per second, so a slide through a swarm isn't a jackpot.")]
        public float styleMaxPerSecond = 3f;
        [Tooltip("The announcer's STILL STANDING! (back from very low HP).")]
        public float closeCall = 15f;
        [Tooltip("Seconds without taking damage per no-hit step.")]
        public float noHitStep = 20f;
        [Tooltip("Favor for the 1st, 2nd, 3rd+ no-hit step in a row (a hit resets the chain).")]
        public float[] noHitFavor = { 5f, 7f, 10f };
        public float bossEnters = 10f;
        public float bossPhase = 10f;

        [Header("Gifts")]
        public GiftEntry[] gifts =
        {
            new GiftEntry { kind = GiftKind.Amphora, weight = 1f },
            new GiftEntry { kind = GiftKind.GoldShower, weight = 1f },
            new GiftEntry { kind = GiftKind.WeaponInSand, weight = 0.8f },
            new GiftEntry { kind = GiftKind.Laurel, weight = 0.6f },
        };
        [Tooltip("Below this HP share the amphora's weight is multiplied by `lowHpAmphoraBias`.")]
        [Range(0f, 1f)] public float lowHp = 0.5f;
        public float lowHpAmphoraBias = 3f;
        [Tooltip("Seconds a gift flies from the stands.")]
        public float throwTime = 1.2f;
        [Tooltip("How high the throw arcs above its start (m).")]
        public float throwArc = 5f;
        [Tooltip("Landing distance from the player (m).")]
        public Vector2 landDistance = new Vector2(3f, 6f);
        [Tooltip("Seconds a gift waits on the ground.")]
        public float giftLifetime = 25f;
        public int maxGifts = 3;
        public float pickupRadius = 1.2f;
        [Tooltip("Share of max HP the amphora heals.")]
        [Range(0f, 1f)] public float amphoraHeal = 0.3f;
        public int goldShowerCoins = 20;
        [Tooltip("Level of the weapon in the sand.")]
        [Range(1, 8)] public int sandWeaponLevel = 4;
        public float sandWeaponSeconds = 30f;
        [Tooltip("XP worth this share of the current level.")]
        [Range(0f, 2f)] public float laurelXp = 0.4f;

        [Header("Whims (Arena Master)")]
        [Tooltip("The last `dullWindow` seconds count as dull when kills < this share × the Director's target, and no enemy is within `dullEnemyDistance`.")]
        public float dullWindow = 10f;
        [Range(0f, 1f)] public float dullKillShare = 0.1f;
        public float dullEnemyDistance = 6f;
        [Tooltip("Boredom per second while dull / lost per second otherwise (fires at 100).")]
        public float boredomRise = 8f;
        public float boredomFall = 25f;
        [Tooltip("No whims before this run time, or within the cooldown of the last one (and never during the boss).")]
        public float noWhimBefore = 90f;
        public float whimCooldown = 60f;
        public WhimKind[] whims = { WhimKind.GatesOpen, WhimKind.ChampionChallenge };
        [Tooltip("Champion's challenge: seconds to kill the champion, how far away it spawns, and the bounty.")]
        public float challengeSeconds = 20f;
        public float challengeDistance = 12f;
        public float challengeFavor = 15f;
        public int challengeGold = 15;

        /// <summary>Favor needed for gift number `thrown` (0-based).</summary>
        public float ThresholdFor(int thrown) => firstThreshold * Mathf.Pow(Mathf.Max(1f, thresholdGrowth), Mathf.Max(0, thrown));

        /// <summary>Favor for the `step`-th no-hit stretch in a row (0-based); the last entry repeats.</summary>
        public float NoHitFavor(int step) => noHitFavor == null || noHitFavor.Length == 0 ? 0f : noHitFavor[Mathf.Clamp(step, 0, noHitFavor.Length - 1)];

        /// <summary>Favor for an announced streak rung (0 if out of range).</summary>
        public float StreakFavor(int rung) => streakFavor != null && rung >= 0 && rung < streakFavor.Length ? streakFavor[rung] : 0f;

        public float GiftWeight(in GiftEntry g, float hp01, bool weaponAvailable)
        {
            if (g.kind == GiftKind.WeaponInSand && !weaponAvailable) return 0f;
            float w = Mathf.Max(0f, g.weight);
            if (g.kind == GiftKind.Amphora && hp01 < lowHp) w *= lowHpAmphoraBias;
            return w;
        }

        /// <summary>Weighted gift pick for a roll r in [0, 1). Falls back to the amphora when nothing has weight.</summary>
        public GiftKind PickGift(float r, float hp01, bool weaponAvailable)
        {
            float total = 0f;
            for (int i = 0; i < gifts.Length; i++) total += GiftWeight(gifts[i], hp01, weaponAvailable);
            if (total <= 0f) return GiftKind.Amphora;
            float x = Mathf.Clamp01(r) * total;
            for (int i = 0; i < gifts.Length; i++)
            {
                float w = GiftWeight(gifts[i], hp01, weaponAvailable);
                if (w <= 0f) continue;
                if (x < w) return gifts[i].kind;
                x -= w;
            }
            for (int i = gifts.Length - 1; i >= 0; i--)
                if (GiftWeight(gifts[i], hp01, weaponAvailable) > 0f) return gifts[i].kind;
            return GiftKind.Amphora;
        }

        /// <summary>True when the last window counts as dull (see the tooltips).</summary>
        public bool IsDull(int killsInWindow, int directorTarget, float nearestEnemy) =>
            killsInWindow < dullKillShare * Mathf.Max(1, directorTarget) && nearestEnemy > dullEnemyDistance;

        /// <summary>Whims are allowed at run time `t`, `sinceLast` seconds after the previous one, outside the boss.</summary>
        public bool WhimAllowed(float t, float sinceLast, bool bossActive) => !bossActive && t >= noWhimBefore && sinceLast >= whimCooldown;
    }
}
