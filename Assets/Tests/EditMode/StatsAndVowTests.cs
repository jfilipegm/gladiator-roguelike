using System.Collections.Generic;
using Gladiator.Core;
using Gladiator.Gameplay;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Gladiator.Tests
{
    /// <summary>
    /// P1 part C (findings 10, 10b, 11): every stat's breakdown adds up to its total, whatever mix of flat, percent and
    /// multiply sources; the admin layer is its own share; previews equal the stats after the pick; Vows roll with their
    /// own share of the odds (one per pick, none in rewards, banishable) and their card text shows the downside.
    /// </summary>
    public class StatsAndVowTests
    {
        readonly List<Object> _made = new List<Object>();
        readonly StatShare[] _shares = new StatShare[32];

        T Make<T>() where T : ScriptableObject
        {
            var o = ScriptableObject.CreateInstance<T>();
            _made.Add(o);
            return o;
        }

        UpgradeDef Boon(string id, params StatMod[] mods)
        {
            var d = Make<UpgradeDef>();
            d.id = d.displayName = d.name = id;
            d.weight = 10f;
            d.maxLevel = 5;
            d.perLevel = mods;
            return d;
        }

        UpgradeDef Vow(string id, StatMod upside, StatMod drawback)
        {
            var d = Boon(id, upside);
            d.vow = true;
            d.maxLevel = 3;
            d.drawbacks = new[] { drawback };
            return d;
        }

        PlayerStats Stats()
        {
            var go = new GameObject("StatsTestPlayer");
            _made.Add(go);
            var stats = go.AddComponent<PlayerStats>();
            stats.Block.Reset(); // default bases, whatever gladiator a previous play session left in GameSession
            return stats;
        }

        static StatMod M(StatId s, ModOp op, float v) => new StatMod(s, op, v);

        [SetUp]
        public void SetUp()
        {
            GameRandom.Seed(4242);
            DevSettings.ResetAll();
        }

        [TearDown]
        public void TearDown()
        {
            DevSettings.ResetAll();
            foreach (var o in _made) Object.DestroyImmediate(o);
            _made.Clear();
        }

        float Sum(int n)
        {
            float s = 0f;
            for (int i = 0; i < n; i++) s += _shares[i].Amount;
            return s;
        }

        // ---------- the breakdown ----------

        [Test]
        public void Breakdown_BasePlusShares_EqualsTheTotal_ForEveryLayerMix()
        {
            var b = new StatBlock();
            var cardA = Boon("a");
            var cardB = Boon("b");
            var syn = Make<TagSynergyDef>();
            b.SetBase(StatId.MaxHp, 120f);
            b.Add(M(StatId.MaxHp, ModOp.Flat, 20f), 1f, new StatSource(StatSourceKind.Card, cardA));
            b.Add(M(StatId.MaxHp, ModOp.Flat, 20f), 1.8f, new StatSource(StatSourceKind.Card, cardA)); // the same card again, Rare
            b.Add(M(StatId.MaxHp, ModOp.Percent, 0.15f), 1f, new StatSource(StatSourceKind.Synergy, syn));
            b.Add(M(StatId.MaxHp, ModOp.Multiply, 0.85f)); // untracked (the altar, a trait)
            b.Add(M(StatId.MaxHp, ModOp.Multiply, 0.8f), 1.3f, new StatSource(StatSourceKind.Card, cardB));
            int n = b.Breakdown(StatId.MaxHp, _shares);
            Assert.AreEqual(5, n, "base, card a (both takes in one share), the synergy, the untracked one, card b");
            Assert.AreEqual(b[StatId.MaxHp], Sum(n), 1e-3f, "base + shares = total");
            Assert.AreEqual(StatSourceKind.Base, _shares[0].Source.Kind);
            Assert.AreEqual(120f, _shares[0].Amount, 1e-5f);
            Assert.AreEqual(2, _shares[1].Count, "card a taken twice is one share of two modifiers");
            Assert.AreEqual(20f + 36f, _shares[1].Amount, 1e-3f, "flat sources add their own value");
        }

        [Test]
        public void Breakdown_StaysExact_AtTheEdges()
        {
            var b = new StatBlock();
            var a = Boon("a");
            var c = Boon("c");
            // percent below -100 % clamps the layer at 0: the percent shares still add up to what it removed
            b.Add(M(StatId.Damage, ModOp.Percent, -0.8f), 1f, new StatSource(StatSourceKind.Card, a));
            b.Add(M(StatId.Damage, ModOp.Percent, -0.6f), 1f, new StatSource(StatSourceKind.Card, c));
            Assert.AreEqual(0f, b[StatId.Damage], 1e-6f);
            Assert.AreEqual(b[StatId.Damage], Sum(b.Breakdown(StatId.Damage, _shares)), 1e-4f);
            // multipliers that cancel out (×2 and ×0.5): two shares, the total untouched
            b.Add(M(StatId.Area, ModOp.Multiply, 2f), 1f, new StatSource(StatSourceKind.Card, a));
            b.Add(M(StatId.Area, ModOp.Multiply, 0.5f), 1f, new StatSource(StatSourceKind.Card, c));
            int n = b.Breakdown(StatId.Area, _shares);
            Assert.AreEqual(3, n);
            Assert.AreEqual(1f, Sum(n), 1e-4f);
            Assert.Greater(_shares[1].Amount, 0f);
            Assert.Less(_shares[2].Amount, 0f);
            // nothing but the base: one share
            Assert.AreEqual(1, b.Breakdown(StatId.Luck, _shares));
        }

        [Test]
        public void Breakdown_ATraitSwappingItsModifierEveryFrame_KeepsOneShare()
        {
            var b = new StatBlock();
            var applied = M(StatId.Damage, ModOp.Multiply, 1f);
            bool has = false;
            for (int frame = 0; frame < 200; frame++) // Bloodlust's pattern: remove the old, add the new
            {
                if (has) b.Remove(applied);
                applied = M(StatId.Damage, ModOp.Multiply, 1f + (frame % 40) * 0.01f);
                b.Add(applied);
                has = true;
            }
            int n = b.Breakdown(StatId.Damage, _shares);
            Assert.AreEqual(2, n, "base + the trait");
            Assert.AreEqual(StatSourceKind.Untracked, _shares[1].Source.Kind);
            Assert.AreEqual(1, _shares[1].Count);
            Assert.AreEqual(b[StatId.Damage], Sum(n), 1e-3f);
            b.Remove(applied);
            Assert.AreEqual(1, b.Breakdown(StatId.Damage, _shares), "removed: no share left");
        }

        [Test]
        public void PlayerStats_Breakdown_AddsTheAdminLayerAsItsOwnShare()
        {
            var stats = Stats();
            var card = Boon("brute", M(StatId.Damage, ModOp.Percent, 0.12f));
            var build = new BuildState();
            var cfg = Make<UpgradeConfig>();
            build.Take(card, Rarity.Rare, stats.Block, cfg);
            DevSettings.DamagePct = 50f;
            int n = stats.Breakdown(StatId.Damage, _shares);
            Assert.AreEqual(3, n, "base, the card, the admin buff");
            Assert.AreEqual(StatSourceKind.Card, _shares[1].Source.Kind);
            Assert.AreSame(card, _shares[1].Source.Ref, "a card's modifiers name the card");
            Assert.AreEqual(StatSourceKind.Admin, _shares[2].Source.Kind);
            Assert.AreEqual(stats.Get(StatId.Damage), Sum(n), 1e-4f, "the page's total is the admin-buffed one");
            Assert.AreEqual(stats.Block[StatId.Damage] * 0.5f, _shares[2].Amount, 1e-4f);
        }

        [Test]
        public void Synergies_AreTheirOwnSource()
        {
            var stats = Stats();
            var cfg = Make<UpgradeConfig>();
            var build = new BuildState();
            var heavy = Boon("heavy", M(StatId.Area, ModOp.Percent, 0.1f));
            heavy.tags = UpgradeTag.Heavy;
            var syn = Make<TagSynergyDef>();
            syn.tag = UpgradeTag.Heavy;
            syn.count = 1;
            syn.mods = new[] { M(StatId.Area, ModOp.Percent, 0.2f) };
            build.Take(heavy, Rarity.Common, stats.Block, cfg);
            Assert.AreEqual(1, build.UpdateSynergies(new[] { syn }, stats.Block));
            int n = stats.Breakdown(StatId.Area, _shares);
            Assert.AreEqual(3, n);
            Assert.AreEqual(StatSourceKind.Synergy, _shares[2].Source.Kind);
            Assert.AreSame(syn, _shares[2].Source.Ref);
            Assert.AreEqual(0.2f, _shares[2].Amount, 1e-5f);
        }

        // ---------- the page's text ----------

        [Test]
        public void StatText_ReadsEachStatInItsUnits()
        {
            var t = new CharBuffer(64);
            Assert.AreEqual("117%", StatText.Value(t.Clear(), StatId.Damage, 1.17f).ToString());
            Assert.AreEqual("138", StatText.Value(t.Clear(), StatId.MaxHp, 138.4f).ToString());
            Assert.AreEqual("6%", StatText.Value(t.Clear(), StatId.Armor, 0.06f).ToString());
            Assert.AreEqual("+10%", StatText.Value(t.Clear(), StatId.CritDamage, 0.1f).ToString(), "crit damage is a bonus");
            Assert.AreEqual("+1", StatText.Value(t.Clear(), StatId.Amount, 1f).ToString());
            Assert.AreEqual("0.4", StatText.Value(t.Clear(), StatId.HpRegen, 0.4f).ToString());
            Assert.AreEqual("−18", StatText.Delta(t.Clear(), StatId.MaxHp, -18f).ToString());
            Assert.AreEqual("+17%", StatText.Delta(t.Clear(), StatId.Damage, 0.17f).ToString());
            StringAssert.Contains(StatText.Gain, StatText.Change(t.Clear(), StatId.Damage, 1.17f, 1.34f).ToString());
            Assert.AreEqual("100% → <color=" + StatText.Loss + ">90%</color>", StatText.Change(t.Clear(), StatId.MoveSpeed, 1f, 0.9f).ToString());
        }

        [Test]
        public void StatText_PageOrder_ListsEveryStatOnce()
        {
            var seen = new HashSet<StatId>(StatText.Order);
            Assert.AreEqual(StatInfo.Count, StatText.Order.Length);
            Assert.AreEqual(StatInfo.Count, seen.Count);
        }

        [Test]
        public void StatLine_ShowsTheTotal_AndEverySourceThatMoves()
        {
            var stats = Stats();
            var cfg = Make<UpgradeConfig>();
            var build = new BuildState();
            var skin = Boon("Thick Skin", M(StatId.MaxHp, ModOp.Flat, 20f));
            build.Take(skin, Rarity.Common, stats.Block, cfg);
            build.Take(skin, Rarity.Common, stats.Block, cfg);
            stats.Block.Add(M(StatId.MaxHp, ModOp.Multiply, 0.85f), 1f, new StatSource(StatSourceKind.Altar));
            var t = new CharBuffer(1024);
            StatText.StatLine(t, stats, StatId.MaxHp, null, true, _shares);
            string s = t.ToString();
            StringAssert.Contains("Max HP", s);
            StringAssert.Contains("119", s, "(100 + 40) × 0.85");
            StringAssert.Contains("Base 100", s);
            StringAssert.Contains("Thick Skin ×2 +40", s);
            StringAssert.Contains("Blood Altar −21", s);
            t.Clear();
            StatText.StatLine(t, stats, StatId.Luck, null, true, _shares);
            StringAssert.DoesNotContain("\n", t.ToString(), "a stat with only its base gets no source line");
        }

        // ---------- previews ----------

        [Test]
        public void Preview_OfAVowCard_EqualsTheStatsAfterTakingIt()
        {
            var stats = Stats();
            var cfg = Make<UpgradeConfig>();
            var build = new BuildState();
            var vow = Vow("berserker", M(StatId.Damage, ModOp.Percent, 0.35f), M(StatId.MaxHp, ModOp.Multiply, 0.8f));
            var preview = new StatPreview();
            var offer = new CardOffer { Def = vow, Rarity = Rarity.Legendary, Level = 1 };
            preview.ForCard(offer, stats, build, null, cfg, null, null, null);
            Assert.IsTrue(preview.Changed(StatId.Damage));
            Assert.IsTrue(preview.Changed(StatId.MaxHp));
            Assert.AreEqual(2, preview.ChangedCount);
            Assert.Greater(preview.AfterOf(StatId.Damage), preview.BeforeOf(StatId.Damage), "the upside: a gain");
            Assert.Less(preview.AfterOf(StatId.MaxHp), preview.BeforeOf(StatId.MaxHp), "the price: a loss");
            build.Take(vow, Rarity.Legendary, stats.Block, cfg);
            for (int i = 0; i < StatInfo.Count; i++)
                Assert.AreEqual(stats.Get((StatId)i), preview.After[i], 1e-4f, ((StatId)i).ToString());
        }

        [Test]
        public void Preview_IncludesTheSynergyACardWouldSwitchOn_AndTheAdminLayer()
        {
            var stats = Stats();
            var cfg = Make<UpgradeConfig>();
            var build = new BuildState();
            var catalog = Make<UpgradeCatalog>();
            var heavy = Boon("heavy", M(StatId.Area, ModOp.Percent, 0.1f));
            heavy.tags = UpgradeTag.Heavy;
            var syn = Make<TagSynergyDef>();
            syn.tag = UpgradeTag.Heavy;
            syn.count = 1;
            syn.mods = new[] { M(StatId.Damage, ModOp.Percent, 0.1f) };
            catalog.synergies = new[] { syn };
            DevSettings.DamagePct = 20f;
            var preview = new StatPreview();
            preview.ForCard(new CardOffer { Def = heavy, Rarity = Rarity.Common, Level = 1 }, stats, build, catalog, cfg, null, null, null);
            build.Take(heavy, Rarity.Common, stats.Block, cfg);
            build.UpdateSynergies(catalog.synergies, stats.Block);
            Assert.AreEqual(stats.Get(StatId.Area), preview.AfterOf(StatId.Area), 1e-5f);
            Assert.AreEqual(stats.Get(StatId.Damage), preview.AfterOf(StatId.Damage), 1e-5f, "the synergy's damage, admin buff on top");
        }

        [Test]
        public void Preview_OfTheAltar_IsItsMaxHpCut()
        {
            var stats = Stats();
            var preview = new StatPreview();
            preview.ForAltar(stats, 0.15f);
            Assert.AreEqual(100f, preview.BeforeOf(StatId.MaxHp), 1e-4f);
            Assert.AreEqual(85f, preview.AfterOf(StatId.MaxHp), 1e-4f);
            Assert.AreEqual(1, preview.ChangedCount);
        }

        // ---------- Vows ----------

        [Test]
        public void Vow_RarityScalesTheUpsideMoreThanTheDownside()
        {
            var cfg = Make<UpgradeConfig>();
            for (var r = Rarity.Uncommon; r <= Rarity.Legendary; r++)
                Assert.Greater(cfg.Magnitude(r), cfg.DrawbackMagnitude(r), r.ToString());
            var vow = Vow("berserker", M(StatId.Damage, ModOp.Percent, 0.35f), M(StatId.MaxHp, ModOp.Multiply, 0.8f));
            var common = new StatBlock();
            var legendary = new StatBlock();
            new BuildState().Take(vow, Rarity.Common, common, cfg);
            new BuildState().Take(vow, Rarity.Legendary, legendary, cfg);
            Assert.AreEqual(1.35f, common[StatId.Damage], 1e-5f);
            Assert.AreEqual(80f, common[StatId.MaxHp], 1e-4f);
            Assert.AreEqual(1f + 0.35f * cfg.Magnitude(Rarity.Legendary), legendary[StatId.Damage], 1e-5f);
            Assert.AreEqual(100f * (1f - 0.2f * cfg.DrawbackMagnitude(Rarity.Legendary)), legendary[StatId.MaxHp], 1e-3f);
            float upGain = (legendary[StatId.Damage] - 1f) / (common[StatId.Damage] - 1f);
            float priceGain = (100f - legendary[StatId.MaxHp]) / (100f - common[StatId.MaxHp]);
            Assert.Greater(upGain, priceGain);
        }

        [Test]
        public void VowCardText_ShowsTheDownside_InRed()
        {
            var cfg = Make<UpgradeConfig>();
            var vow = Vow("berserker", M(StatId.Damage, ModOp.Percent, 0.35f), M(StatId.MaxHp, ModOp.Multiply, 0.8f));
            var t = new CharBuffer(256);
            CardText.Describe(new CardOffer { Def = vow, Rarity = Rarity.Common, Level = 1 }, cfg, t);
            Assert.AreEqual("+35% Damage\n<color=" + CardText.DrawbackColor + ">-20% Max HP</color>", t.ToString());
            CardText.Describe(new CardOffer { Def = vow, Rarity = Rarity.Legendary, Level = 1 }, cfg, t);
            Assert.AreEqual("+88% Damage\n<color=" + CardText.DrawbackColor + ">-26% Max HP</color>", t.ToString());
            var gambler = Vow("gambler", M(StatId.Luck, ModOp.Flat, 0.2f), M(StatId.Rerolls, ModOp.Flat, -1f));
            CardText.Describe(new CardOffer { Def = gambler, Rarity = Rarity.Rare, Level = 1 }, cfg, t);
            StringAssert.Contains("-1 Rerolls", t.ToString(), "whole-number prices never scale");
        }

        List<UpgradeDef> Pool(int boons, int vows)
        {
            var pool = new List<UpgradeDef>();
            for (int i = 0; i < boons; i++) pool.Add(Boon("b" + i, M(StatId.Damage, ModOp.Percent, 0.1f)));
            for (int i = 0; i < vows; i++) pool.Add(Vow("v" + i, M(StatId.Damage, ModOp.Percent, 0.35f), M(StatId.MaxHp, ModOp.Multiply, 0.8f)));
            return pool;
        }

        [Test]
        public void Vows_TakeTheirShareOfTheOdds_HoweverBigThePool()
        {
            var cfg = Make<UpgradeConfig>();
            cfg.maxVowsPerPick = 1;
            foreach (int boons in new[] { 6, 30 })
            {
                var pool = Pool(boons, 8);
                var roller = new UpgradeRoller();
                var build = new BuildState();
                var offers = new CardOffer[4];
                int vows = 0;
                const int Rolls = 4000;
                for (int i = 0; i < Rolls; i++)
                {
                    roller.Roll(pool, null, build, cfg, 0f, offers, 1);
                    if (offers[0].Def.IsVow) vows++;
                }
                Assert.AreEqual(cfg.vowShare, vows / (float)Rolls, 0.025f, boons + " Boons: one card in ~" + Mathf.RoundToInt(1f / cfg.vowShare) + " is a Vow");
            }
            Assert.AreEqual(1f, UpgradeRoller.VowWeightScale(cfg, 0f, 80f), "nothing else eligible: their own weights");
            float k = UpgradeRoller.VowWeightScale(cfg, 300f, 80f);
            Assert.AreEqual(cfg.vowShare, 80f * k / (300f + 80f * k), 1e-5f);
        }

        [Test]
        public void Vows_AtMostOnePerPick_AndNoneInRewards()
        {
            var cfg = Make<UpgradeConfig>();
            var pool = Pool(3, 8);
            var roller = new UpgradeRoller();
            var build = new BuildState();
            var offers = new CardOffer[4];
            int sawVow = 0;
            for (int i = 0; i < 300; i++)
            {
                int n = roller.Roll(pool, null, build, cfg, 0f, offers, 3);
                int v = 0;
                for (int c = 0; c < n; c++) if (offers[c].Def.IsVow) v++;
                Assert.LessOrEqual(v, cfg.maxVowsPerPick);
                sawVow += v;
            }
            Assert.Greater(sawVow, 0, "Vows do come up in level-ups");
            roller.MinRarity = Rarity.Uncommon; // a chest
            for (int i = 0; i < 300; i++)
            {
                int n = roller.Roll(pool, null, build, cfg, 0f, offers, 3);
                for (int c = 0; c < n; c++) Assert.IsFalse(offers[c].Def.IsVow, "rewards are pure upside");
            }
            cfg.vowsInRewards = true;
            bool any = false;
            for (int i = 0; i < 300 && !any; i++)
            {
                int n = roller.Roll(pool, null, build, cfg, 0f, offers, 3);
                for (int c = 0; c < n; c++) any |= offers[c].Def.IsVow;
            }
            Assert.IsTrue(any, "unless vowsInRewards");
        }

        [Test]
        public void Vows_CanBeBanished_LikeAnyCard()
        {
            var cfg = Make<UpgradeConfig>();
            var pool = Pool(4, 1);
            var vow = pool[4];
            var build = new BuildState();
            var roller = new UpgradeRoller();
            var offers = new CardOffer[4];
            Assert.IsTrue(UpgradeRoller.IsEligible(vow, build, cfg));
            build.Banish(vow);
            Assert.IsFalse(UpgradeRoller.IsEligible(vow, build, cfg));
            for (int i = 0; i < 300; i++)
            {
                int n = roller.Roll(pool, null, build, cfg, 0f, offers, 3);
                for (int c = 0; c < n; c++) Assert.AreNotSame(vow, offers[c].Def);
            }
        }

        [Test]
        public void Banishing_AShownVow_CanBringAnother()
        {
            var cfg = Make<UpgradeConfig>();
            var pool = Pool(0, 3); // only Vows: they keep their own weights; the cap leaves the other slots to fillers
            var filler = Make<UpgradeDef>();
            filler.kind = UpgradeKind.Consumable;
            var fallbacks = new List<UpgradeDef> { filler };
            var build = new BuildState();
            var roller = new UpgradeRoller();
            var offers = new CardOffer[4];
            int n = roller.Roll(pool, fallbacks, build, cfg, 0f, offers, 3);
            Assert.AreEqual(2, n, "one Vow, then the filler (each card shows once)");
            Assert.IsTrue(offers[0].Def.IsVow);
            build.Banish(offers[0].Def);
            n = roller.Replace(pool, fallbacks, build, cfg, 0f, offers, n, 0);
            Assert.IsTrue(offers[0].Def.IsVow, "the banished Vow's slot takes another Vow");
        }

        // ---------- the Vow data (run Gladiator/Setup/Upgrades/Create Vows first) ----------

        [Test]
        public void VowAssets_AreEightTradeOffs_InTheCatalog_WithIcons()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<UpgradeCatalog>("Assets/Data/UpgradeCatalog.asset");
            Assert.IsNotNull(catalog);
            int vows = 0;
            foreach (var d in catalog.upgrades)
            {
                if (d == null || !d.IsVow) continue;
                vows++;
                Assert.AreEqual(UpgradeKind.Boon, d.kind, d.id);
                Assert.IsNotEmpty(d.perLevel, d.id + " has an upside");
                Assert.IsNotEmpty(d.drawbacks, d.id + " has a price");
                foreach (var m in d.drawbacks)
                {
                    bool bites = m.op == ModOp.Multiply ? m.value < 1f : m.value < 0f;
                    Assert.IsTrue(bites, d.id + ": every drawback lowers a stat");
                    Assert.AreNotEqual(StatId.Armor, m.stat, d.id + ": armor is clamped at 0, so a cut costs most gladiators nothing");
                    Assert.AreNotEqual(StatId.HpRegen, m.stat, d.id + ": regen below 0 does nothing");
                }
                Assert.IsNotNull(d.icon, d.id + " has its pixel icon (IconSetup)");
                Assert.GreaterOrEqual(d.iconRow, 0, d.id);
            }
            Assert.AreEqual(8, vows, "VowSetup.CreateOrUpdate made the eight Vows");
        }
    }
}
