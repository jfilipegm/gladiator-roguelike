using Gladiator.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace Gladiator.Tests
{
    /// <summary>
    /// W1 (the producer's note 23, docs/tracks/W1-wager.md): the Bookmaker's book in numbers. The default table (the stake
    /// doubling 5, 10, 20, 40; the payouts; the kills that open each tier; the challenges), how many wagers an opening
    /// offers (kills this run, plus one Luck roll by the level-up's fourth-card rule, never past the table), the kills a
    /// wager asks for (your pace × the tier's factor, its floor, the cap; the champion is one kill) and the countdown's
    /// constant labels.
    /// </summary>
    public class WagerMathTests
    {
        const float LuckPerTier = 0.5f; // UpgradeConfig.fourthCardLuck's default

        [Test]
        public void TheDefaultTable_TheStakeDoubles_AndTheChallengesClimb()
        {
            var t = Bookmaker.DefaultTiers();
            Assert.AreEqual(4, t.Length, "four tiers");
            Assert.AreEqual(Bookmaker.MaxTiers, t.Length);
            int[] stakes = { 5, 10, 20, 40 }, payouts = { 15, 35, 80, 200 }, unlocks = { 0, 100, 300, 600 };
            var kinds = new[] { WagerKind.Pace, WagerKind.Pace, WagerKind.NoHit, WagerKind.Champion };
            float[] seconds = { 30f, 30f, 30f, 45f };
            for (int i = 0; i < t.Length; i++)
            {
                Assert.AreEqual(stakes[i], t[i].stake, "tier " + Bookmaker.Numerals[i] + "'s stake");
                Assert.AreEqual(payouts[i], t[i].payout, "tier " + Bookmaker.Numerals[i] + "'s payout");
                Assert.AreEqual(unlocks[i], t[i].unlockKills, "the kills that open tier " + Bookmaker.Numerals[i]);
                Assert.AreEqual(kinds[i], t[i].kind);
                Assert.AreEqual(seconds[i], t[i].seconds, 1e-4f);
                Assert.Greater(t[i].payout, t[i].stake, "a win pays more than the stake");
                if (i > 0)
                {
                    Assert.AreEqual(t[i - 1].stake * 2, t[i].stake, "each stake is twice the one before");
                    Assert.Greater(t[i].payout / (float)t[i].stake, t[i - 1].payout / (float)t[i - 1].stake - 1e-4f,
                        "bigger wagers never pay worse odds");
                }
            }
            Assert.AreEqual(0.8f, t[0].paceFactor, 1e-4f);
            Assert.AreEqual(25, t[0].minKills);
            Assert.AreEqual(1.2f, t[1].paceFactor, 1e-4f);
            Assert.AreEqual(50, t[1].minKills);
            Assert.AreEqual(1f, t[2].paceFactor, 1e-4f);
            Assert.AreEqual(40, t[2].minKills);
        }

        [Test]
        public void TheComponentsDefaults_AreTheTable()
        {
            var go = new GameObject("Bookmaker");
            try
            {
                var bm = go.AddComponent<Bookmaker>();
                var t = Bookmaker.DefaultTiers();
                Assert.AreEqual(t.Length, bm.tiers.Length);
                for (int i = 0; i < t.Length; i++)
                {
                    Assert.AreEqual(t[i].stake, bm.tiers[i].stake);
                    Assert.AreEqual(t[i].payout, bm.tiers[i].payout);
                    Assert.AreEqual(t[i].unlockKills, bm.tiers[i].unlockKills);
                    Assert.AreEqual(t[i].kind, bm.tiers[i].kind);
                }
                Assert.AreEqual(600, bm.maxKills);
                Assert.AreEqual(60f, bm.cooldownSeconds, 1e-4f, "he waits 60 s between wagers");
                Assert.AreEqual(0, bm.maxWagers, "no limit per run");
                Assert.AreEqual(1, bm.flagMinTier, "the flag (Loaded Dice) needs tier II or higher");
                Assert.AreEqual("bookmaker_paid", bm.flag);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void KillsThisRun_OpenTheTiersInOrder()
        {
            var t = Bookmaker.DefaultTiers();
            Assert.AreEqual(1, Bookmaker.OpenedByKills(t, 0), "tier I is always offered");
            Assert.AreEqual(1, Bookmaker.OpenedByKills(t, 99));
            Assert.AreEqual(2, Bookmaker.OpenedByKills(t, 100));
            Assert.AreEqual(2, Bookmaker.OpenedByKills(t, 299));
            Assert.AreEqual(3, Bookmaker.OpenedByKills(t, 300));
            Assert.AreEqual(3, Bookmaker.OpenedByKills(t, 599));
            Assert.AreEqual(4, Bookmaker.OpenedByKills(t, 600));
            Assert.AreEqual(4, Bookmaker.OpenedByKills(t, 100000), "never more than the table");
            Assert.AreEqual(0, Bookmaker.OpenedByKills(null, 500));
            Assert.AreEqual(0, Bookmaker.OpenedByKills(new WagerTier[0], 500));

            // in order: a later tier with a lower threshold still waits for the one before it
            t[2].unlockKills = 50;
            Assert.AreEqual(1, Bookmaker.OpenedByKills(t, 60), "tier III can't open before tier II");
        }

        [Test]
        public void Luck_OpensOneMoreTier_ByTheFourthCardRule()
        {
            var t = Bookmaker.DefaultTiers();
            Assert.AreEqual(1, Bookmaker.OfferCountFor(t, 0, 0f, LuckPerTier, 0f), "no Luck, no extra tier (even on a roll of 0)");
            Assert.AreEqual(2, Bookmaker.OfferCountFor(t, 0, 0.25f, LuckPerTier, 0.49f), "Luck 0.25: a 50 % chance (0.49 < 0.5)");
            Assert.AreEqual(1, Bookmaker.OfferCountFor(t, 0, 0.25f, LuckPerTier, 0.5f), "0.5 is not under 0.5");
            Assert.AreEqual(2, Bookmaker.OfferCountFor(t, 0, 5f, LuckPerTier, 0.99f), "Luck only ever opens one more");
            Assert.AreEqual(3, Bookmaker.OfferCountFor(t, 100, 0.5f, LuckPerTier, 0.99f), "Luck 0.5 is a sure thing (as the level-up's 4th card)");
            Assert.AreEqual(4, Bookmaker.OfferCountFor(t, 600, 5f, LuckPerTier, 0f), "never past the table");
            Assert.AreEqual(4, Bookmaker.OfferCountFor(t, 300, 1f, LuckPerTier, 0.3f), "kills open III, Luck opens IV");
            Assert.AreEqual(1, Bookmaker.OfferCountFor(t, 0, 5f, 0f, 0f), "a zero rule never adds a tier");
            Assert.AreEqual(0, Bookmaker.OfferCountFor(null, 0, 5f, LuckPerTier, 0f));
        }

        [Test]
        public void TheKillsAWagerAsks_FollowYourPace()
        {
            var t = Bookmaker.DefaultTiers();
            const int Cap = 600;
            // tier I: 0.8 × pace, at least 25
            Assert.AreEqual(25, Bookmaker.TargetFor(t[0], 0, Cap), "a pace of 0 early in the run still gives the minimum");
            Assert.AreEqual(25, Bookmaker.TargetFor(t[0], 20, Cap));
            Assert.AreEqual(40, Bookmaker.TargetFor(t[0], 50, Cap));
            Assert.AreEqual(81, Bookmaker.TargetFor(t[0], 101, Cap), "rounded up (80.8)");
            // tier II: 1.2 × pace, at least 50
            Assert.AreEqual(50, Bookmaker.TargetFor(t[1], 30, Cap));
            Assert.AreEqual(60, Bookmaker.TargetFor(t[1], 50, Cap), "50 × 1.2f is 60.0000024 in floats: still 60");
            Assert.AreEqual(120, Bookmaker.TargetFor(t[1], 100, Cap));
            Assert.AreEqual(Cap, Bookmaker.TargetFor(t[1], 1000, Cap), "capped at maxKills");
            // tier III: 1.0 × pace, at least 40
            Assert.AreEqual(40, Bookmaker.TargetFor(t[2], 10, Cap));
            Assert.AreEqual(77, Bookmaker.TargetFor(t[2], 77, Cap));
            // tier IV: the champion, one kill whatever the pace
            Assert.AreEqual(1, Bookmaker.TargetFor(t[3], 0, Cap));
            Assert.AreEqual(1, Bookmaker.TargetFor(t[3], 900, Cap));
            // a cap below a floor never asks for fewer than the floor
            Assert.AreEqual(50, Bookmaker.TargetFor(t[1], 1000, 10));
            // harder with the stake: at any pace, a bigger pace wager never asks for fewer kills per second
            for (int pace = 0; pace <= 400; pace += 7)
                Assert.GreaterOrEqual(Bookmaker.TargetFor(t[1], pace, Cap), Bookmaker.TargetFor(t[0], pace, Cap), "pace " + pace);
        }

        [Test]
        public void TheCountdownsLabels_AreConstantStrings()
        {
            Assert.AreEqual("WAGER I", Bookmaker.LabelFor(0, WagerKind.Pace));
            Assert.AreEqual("WAGER II", Bookmaker.LabelFor(1, WagerKind.Pace));
            Assert.AreEqual("WAGER III · NO HITS", Bookmaker.LabelFor(2, WagerKind.NoHit));
            Assert.AreEqual("THE CHAMPION", Bookmaker.LabelFor(3, WagerKind.Champion));
            // the HUD builds its format once per label (ReferenceEquals): the same wager must hand it the same string
            Assert.IsTrue(ReferenceEquals(Bookmaker.LabelFor(1, WagerKind.Pace), Bookmaker.LabelFor(1, WagerKind.Pace)));
            Assert.IsTrue(ReferenceEquals(Bookmaker.LabelFor(2, WagerKind.NoHit), Bookmaker.LabelFor(2, WagerKind.NoHit)));
            Assert.AreEqual("WAGER IV", Bookmaker.LabelFor(9, WagerKind.Pace), "out of range clamps");
            CollectionAssert.AreEqual(new[] { "I", "II", "III", "IV" }, Bookmaker.Numerals);
        }
    }
}
