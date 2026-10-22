using System;
using Gladiator.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// The Edicts in force (R7, docs/tracks/R7-design.md §2.4). The menu sets them before the run starts (Retry keeps
    /// them); when an arena loads (`SceneManager.sceneLoaded`: after every Awake, before any Start or tick, so the pools
    /// prewarm with the edicts' numbers) ApplyTo multiplies them into that scene's systems once. Each target is a separate
    /// hook that starts at 1 in every fresh scene, so a reload (Retry) never compounds. Survives scene loads (static),
    /// like GameSession. Nothing here runs per frame.
    /// </summary>
    public static class RunModifiers
    {
        /// <summary>Every edict's numbers folded together: floats multiply, the card deltas add.</summary>
        public struct Totals
        {
            public float PlayerDamageTakenMul, HealingScale, EnemyHpMul, BossHpMul, EnemySpeedMul,
                DensityMul, SwarmMul, EliteEveryMul, ClockScale, ChestPriceMul, FavorGainMul, BoredomMul;
            public int LevelUpCardDelta;

            /// <summary>No edict: every multiplier 1, the delta 0.</summary>
            public static Totals None { get; } = new Totals
            {
                PlayerDamageTakenMul = 1f, HealingScale = 1f, EnemyHpMul = 1f, BossHpMul = 1f, EnemySpeedMul = 1f,
                DensityMul = 1f, SwarmMul = 1f, EliteEveryMul = 1f, ClockScale = 1f, ChestPriceMul = 1f,
                FavorGainMul = 1f, BoredomMul = 1f, LevelUpCardDelta = 0,
            };
        }

        static RunModifierDef[] _active = Array.Empty<RunModifierDef>();

        /// <summary>The edicts in force (never null; empty = none). Don't modify: use Set.</summary>
        public static RunModifierDef[] Active => _active;
        /// <summary>Σ renownBonus of the active edicts: the run's Renown × (1 + this).</summary>
        public static float RenownBonus { get; private set; }
        /// <summary>The active edicts' combined numbers.</summary>
        public static Totals Current { get; private set; } = Totals.None;
        /// <summary>The run the edicts were last applied to (ApplyTo never applies twice to one run).</summary>
        public static RunState AppliedTo { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Init()
        {
            Clear();
            SceneManager.sceneLoaded -= OnSceneLoaded; // once, also without a domain reload
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => ApplyTo(RunState.Current);

        /// <summary>Pure: the defs' numbers folded together (products of the multipliers, sum of the deltas; nulls skipped).</summary>
        public static Totals Combine(RunModifierDef[] defs)
        {
            var t = Totals.None;
            if (defs == null) return t;
            for (int i = 0; i < defs.Length; i++)
            {
                var d = defs[i];
                if (d == null) continue;
                t.PlayerDamageTakenMul *= d.playerDamageTakenMul;
                t.HealingScale *= d.healingScale;
                t.EnemyHpMul *= d.enemyHpMul;
                t.BossHpMul *= d.bossHpMul;
                t.EnemySpeedMul *= d.enemySpeedMul;
                t.DensityMul *= d.densityMul;
                t.SwarmMul *= d.swarmMul;
                t.EliteEveryMul *= d.eliteEveryMul;
                t.ClockScale *= d.clockScale;
                t.ChestPriceMul *= d.chestPriceMul;
                t.FavorGainMul *= d.favorGainMul;
                t.BoredomMul *= d.boredomMul;
                t.LevelUpCardDelta += d.levelUpCardDelta;
            }
            return t;
        }

        /// <summary>Pure: Σ max(0, renownBonus) of the defs (nulls skipped).</summary>
        public static float Bonus(RunModifierDef[] defs)
        {
            float b = 0f;
            if (defs == null) return b;
            for (int i = 0; i < defs.Length; i++)
                if (defs[i] != null) b += Mathf.Max(0f, defs[i].renownBonus);
            return b;
        }

        /// <summary>
        /// The edicts for the next run(s): copies `defs`, dropping nulls and duplicates (the same asset, or the same
        /// non-empty id: ids are the save's keys), and recomputes Current and RenownBonus. Takes effect at the next load.
        /// </summary>
        public static void Set(RunModifierDef[] defs)
        {
            int n = 0;
            var kept = new RunModifierDef[defs != null ? defs.Length : 0];
            for (int i = 0; i < kept.Length; i++)
            {
                var d = defs[i];
                if (d == null || Contains(kept, n, d)) continue;
                kept[n++] = d;
            }
            if (n < kept.Length) Array.Resize(ref kept, n);
            _active = n > 0 ? kept : Array.Empty<RunModifierDef>();
            Current = Combine(_active);
            RenownBonus = Bonus(_active);
        }

        static bool Contains(RunModifierDef[] list, int n, RunModifierDef d)
        {
            for (int i = 0; i < n; i++)
            {
                if (ReferenceEquals(list[i], d)) return true;
                if (!string.IsNullOrEmpty(d.id) && list[i].id == d.id) return true;
            }
            return false;
        }

        /// <summary>No edicts (and forget the run they were applied to).</summary>
        public static void Clear()
        {
            _active = Array.Empty<RunModifierDef>();
            Current = Totals.None;
            RenownBonus = 0f;
            AppliedTo = null;
        }

        /// <summary>The active edicts' ids, in order (allocates: the run's end and the save only).</summary>
        public static string[] ActiveIds()
        {
            var ids = new string[_active.Length];
            for (int i = 0; i < ids.Length; i++) ids[i] = _active[i].id;
            return ids;
        }

        /// <summary>
        /// Multiplies the active edicts into `run`'s scene (called on every scene load; tests may call it after building a
        /// scene by hand). False (nothing changes) without a run, without edicts, or when this run already has them. Every
        /// target is null-safe: an arena without chests simply has no chest price to raise.
        /// </summary>
        public static bool ApplyTo(RunState run)
        {
            if (run == null || _active.Length == 0 || ReferenceEquals(run, AppliedTo)) return false;
            var t = Current;
            run.playerDamageTakenMul *= t.PlayerDamageTakenMul;

            var em = Find(EnemyManager.Instance);
            if (em != null)
            {
                em.hpMul *= t.EnemyHpMul;
                em.scriptedHpMul *= t.BossHpMul;
                em.speedMul *= t.EnemySpeedMul;
            }
            var director = Find(Director.Instance);
            if (director != null)
            {
                director.densityMultiplier *= t.DensityMul;
                director.swarmMultiplier *= t.SwarmMul;
                director.clockScale *= t.ClockScale;
                if (t.EliteEveryMul != 1f)
                {
                    float every = director.eliteEveryOverride > 0f ? director.eliteEveryOverride
                        : director.config != null ? director.config.eliteEvery : 0f;
                    if (every > 0f) director.eliteEveryOverride = every * t.EliteEveryMul;
                }
            }
            var props = Find(Interactables.Instance);
            if (props != null) props.priceMul *= t.ChestPriceMul;
            var favor = Find(Favor.Instance);
            if (favor != null) favor.gainMul *= t.FavorGainMul;
            var master = Find(ArenaMaster.Instance);
            if (master != null) master.boredomMul *= t.BoredomMul;
            var cards = Find(LevelUpController.Instance);
            if (cards != null) cards.levelUpCardDelta += t.LevelUpCardDelta;

            var player = Player.Main;
            if (player != null)
            {
                var stats = player.GetComponent<PlayerStats>();
                var health = player.GetComponent<PlayerHealth>();
                if (stats != null)
                {
                    float oldMax = health != null ? health.MaxHp : 0f;
                    for (int i = 0; i < _active.Length; i++)
                    {
                        var d = _active[i];
                        var source = new StatSource(StatSourceKind.Edict, d);
                        if (d.playerStats == null) continue;
                        for (int k = 0; k < d.playerStats.Length; k++) stats.Block.Add(d.playerStats[k], 1f, source);
                    }
                    if (health != null) health.RescaleFrom(oldMax); // HP starts full at the new max
                }
                if (health != null) health.HealingScale *= t.HealingScale;
            }

            AppliedTo = run;
            Debug.Log($"[Edicts] applied {_active.Length} (+{RenownBonus * 100f:0} %)");
            return true;
        }

        /// <summary>The scene's instance, or (a component whose object starts inactive hasn't set it yet) the one in the scene.</summary>
        static T Find<T>(T instance) where T : UnityEngine.Object =>
            instance != null ? instance : UnityEngine.Object.FindAnyObjectByType<T>(FindObjectsInactive.Include);
    }
}
