using System.Collections.Generic;
using Gladiator.Core;
using Gladiator.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace Gladiator.Tests
{
    /// <summary>
    /// R7 Edicts, the pure math (docs/tracks/R7-contract.md "R7-A rules"): Combine multiplies the numbers and adds the card
    /// deltas, Bonus adds the Renown shares, nulls are skipped, Totals.None changes nothing, and Set drops nulls and
    /// duplicates. The PlayMode side (EdictRunTests) checks each number reaches the game.
    /// </summary>
    public class EdictMathTests
    {
        readonly List<Object> _made = new List<Object>();

        RunModifierDef Edict(string id, float bonus = 0.2f)
        {
            var d = ScriptableObject.CreateInstance<RunModifierDef>();
            d.id = d.name = id;
            d.displayName = id;
            d.renownBonus = bonus;
            _made.Add(d);
            return d;
        }

        [SetUp] public void SetUp() => RunModifiers.Clear();

        [TearDown]
        public void TearDown()
        {
            RunModifiers.Clear();
            foreach (var o in _made) if (o != null) Object.DestroyImmediate(o);
            _made.Clear();
        }

        static void AssertNone(in RunModifiers.Totals t)
        {
            Assert.AreEqual(1f, t.PlayerDamageTakenMul);
            Assert.AreEqual(1f, t.HealingScale);
            Assert.AreEqual(1f, t.EnemyHpMul);
            Assert.AreEqual(1f, t.BossHpMul);
            Assert.AreEqual(1f, t.EnemySpeedMul);
            Assert.AreEqual(1f, t.DensityMul);
            Assert.AreEqual(1f, t.SwarmMul);
            Assert.AreEqual(1f, t.EliteEveryMul);
            Assert.AreEqual(1f, t.ClockScale);
            Assert.AreEqual(1f, t.ChestPriceMul);
            Assert.AreEqual(1f, t.FavorGainMul);
            Assert.AreEqual(1f, t.BoredomMul);
            Assert.AreEqual(0, t.LevelUpCardDelta);
        }

        [Test]
        public void TotalsNone_ChangesNothing_AndSoDoNoEdicts()
        {
            AssertNone(RunModifiers.Totals.None);
            AssertNone(RunModifiers.Combine(null));
            AssertNone(RunModifiers.Combine(new RunModifierDef[0]));
            AssertNone(RunModifiers.Combine(new RunModifierDef[] { null, null }));
            AssertNone(RunModifiers.Combine(new[] { Edict("plain") })); // a new asset's defaults are "no change"
            Assert.AreEqual(0f, RunModifiers.Bonus(null));
            AssertNone(RunModifiers.Current);
            Assert.AreEqual(0, RunModifiers.Active.Length, "never null, empty by default");
            Assert.AreEqual(0f, RunModifiers.RenownBonus);
        }

        [Test]
        public void Combine_MultipliesTheNumbers_AddsTheDeltas_SkipsNulls()
        {
            var a = Edict("a"); a.enemyHpMul = 1.5f; a.levelUpCardDelta = -1; a.clockScale = 1.33f;
            var b = Edict("b"); b.enemyHpMul = 1.2f; b.levelUpCardDelta = -1; b.healingScale = 0f;
            var t = RunModifiers.Combine(new[] { a, null, b });
            Assert.AreEqual(1.8f, t.EnemyHpMul, 1e-5f, "1.5 × 1.2");
            Assert.AreEqual(-2, t.LevelUpCardDelta, "−1 + −1");
            Assert.AreEqual(1.33f, t.ClockScale, 1e-5f);
            Assert.AreEqual(0f, t.HealingScale, "No Mercy's 0 wins");
            Assert.AreEqual(1f, t.BossHpMul, "untouched numbers stay 1");
        }

        [Test]
        public void Combine_EveryField_ReachesItsTotal()
        {
            var d = Edict("all");
            d.playerDamageTakenMul = 2f; d.healingScale = 3f; d.enemyHpMul = 4f; d.bossHpMul = 5f; d.enemySpeedMul = 6f;
            d.densityMul = 7f; d.swarmMul = 8f; d.eliteEveryMul = 9f; d.clockScale = 10f; d.chestPriceMul = 11f;
            d.favorGainMul = 12f; d.boredomMul = 13f; d.levelUpCardDelta = 14;
            var t = RunModifiers.Combine(new[] { d });
            Assert.AreEqual(2f, t.PlayerDamageTakenMul);
            Assert.AreEqual(3f, t.HealingScale);
            Assert.AreEqual(4f, t.EnemyHpMul);
            Assert.AreEqual(5f, t.BossHpMul);
            Assert.AreEqual(6f, t.EnemySpeedMul);
            Assert.AreEqual(7f, t.DensityMul);
            Assert.AreEqual(8f, t.SwarmMul);
            Assert.AreEqual(9f, t.EliteEveryMul);
            Assert.AreEqual(10f, t.ClockScale);
            Assert.AreEqual(11f, t.ChestPriceMul);
            Assert.AreEqual(12f, t.FavorGainMul);
            Assert.AreEqual(13f, t.BoredomMul);
            Assert.AreEqual(14, t.LevelUpCardDelta);
        }

        [Test]
        public void Bonus_AddsTheShares_NeverANegativeOne()
        {
            Assert.AreEqual(0.45f, RunModifiers.Bonus(new[] { Edict("a", 0.25f), Edict("b", 0.2f) }), 1e-5f);
            Assert.AreEqual(0.25f, RunModifiers.Bonus(new[] { Edict("c", 0.25f), null }), 1e-5f, "nulls skipped");
            var odd = Edict("d"); odd.renownBonus = -0.5f; // [Range] guards the Inspector, not code
            Assert.AreEqual(0.2f, RunModifiers.Bonus(new[] { odd, Edict("e", 0.2f) }), 1e-5f, "a negative share counts 0");
        }

        [Test]
        public void TheElevenEdicts_PayTimes335()
        {
            // the spec's table (docs/tracks/R7-design.md §2.3)
            float[] shares = { 0.25f, 0.25f, 0.2f, 0.25f, 0.2f, 0.15f, 0.15f, 0.2f, 0.2f, 0.2f, 0.3f };
            var defs = new RunModifierDef[shares.Length];
            for (int i = 0; i < defs.Length; i++) defs[i] = Edict("ed" + i, shares[i]);
            Assert.AreEqual(2.35f, RunModifiers.Bonus(defs), 1e-4f, "×3.35 Renown with all eleven");
        }

        [Test]
        public void Set_DropsNullsAndDuplicates_RecomputesCurrentAndBonus()
        {
            var a = Edict("iron_hides", 0.25f); a.enemyHpMul = 1.5f;
            var b = Edict("blood_sport", 0.2f); b.playerDamageTakenMul = 1.4f;
            var twin = Edict("iron_hides", 0.25f); twin.enemyHpMul = 1.5f; // another asset with the same id
            RunModifiers.Set(new[] { a, null, b, a, twin, null });
            Assert.AreEqual(2, RunModifiers.Active.Length, "nulls, the same asset twice and a second iron_hides dropped");
            Assert.AreSame(a, RunModifiers.Active[0], "order kept");
            Assert.AreSame(b, RunModifiers.Active[1]);
            Assert.AreEqual(1.5f, RunModifiers.Current.EnemyHpMul, 1e-5f, "counted once");
            Assert.AreEqual(1.4f, RunModifiers.Current.PlayerDamageTakenMul, 1e-5f);
            Assert.AreEqual(0.45f, RunModifiers.RenownBonus, 1e-5f);
            CollectionAssert.AreEqual(new[] { "iron_hides", "blood_sport" }, RunModifiers.ActiveIds());
        }

        [Test]
        public void Set_Copies_AndClearForgetsEverything()
        {
            var a = Edict("a", 0.25f); a.swarmMul = 1.3f;
            var input = new[] { a };
            RunModifiers.Set(input);
            input[0] = null; // the caller's array is theirs
            Assert.AreSame(a, RunModifiers.Active[0]);
            RunModifiers.Set(null);
            Assert.AreEqual(0, RunModifiers.Active.Length, "Set(null) = none");
            RunModifiers.Set(new[] { a });
            RunModifiers.Clear();
            Assert.AreEqual(0, RunModifiers.Active.Length);
            Assert.AreEqual(0f, RunModifiers.RenownBonus);
            AssertNone(RunModifiers.Current);
            Assert.AreEqual(0, RunModifiers.ActiveIds().Length);
            Assert.IsNull(RunModifiers.AppliedTo);
        }

        [Test]
        public void ApplyTo_WithoutARunOrWithoutEdicts_DoesNothing()
        {
            Assert.IsFalse(RunModifiers.ApplyTo(null));
            var go = new GameObject("EdictMathTests.Run");
            try
            {
                var run = go.AddComponent<RunState>(); // no Awake in Edit mode: a bare holder
                Assert.IsFalse(RunModifiers.ApplyTo(run), "no edicts: nothing to apply");
                Assert.AreEqual(1f, run.playerDamageTakenMul);
                Assert.IsNull(RunModifiers.AppliedTo);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void StatSourceEdict_IsAppended_AndNamesTheEdict()
        {
            Assert.AreEqual((int)StatSourceKind.Admin + 1, (int)StatSourceKind.Edict, "appended after Admin (never reorder)");
            var d = Edict("glass_jaw");
            d.displayName = "Glass Jaw";
            Assert.AreEqual("Edict: Glass Jaw", StatText.SourceName(new StatSource(StatSourceKind.Edict, d), null));
        }
    }
}
