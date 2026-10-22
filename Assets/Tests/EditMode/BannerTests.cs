using System;
using System.Collections.Generic;
using Gladiator.UI;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Gladiator.Tests
{
    /// <summary>
    /// U1-A the vexillum banner's timeline (docs/tracks/U1-contract.md "VexillumBanner", spec §7.1), ticked at 60 fps:
    /// Dropping → Settling → Holding → Rolling → Hidden in TotalSeconds; the drop's overshoot; the sway's bounds and
    /// end; the event's seconds lengthen the hold but never shorten it; a new banner while one holds rolls the old one up
    /// quickly, then drops; Hold stays until RollUp; Clear hides at once; and a whole cycle of ticks allocates nothing.
    /// Timings come from UiTheme.Current (the asset's, if the designer retuned them).
    /// </summary>
    public class BannerTests
    {
        const float Dt = 1f / 60f;
        const float Frames2 = 2f * Dt + 1e-4f;
        Canvas _canvas;
        VexillumBanner _b;
        UiTheme _t;

        [SetUp]
        public void SetUp()
        {
            UiTheme.Reload();
            _t = UiTheme.Current;
            _canvas = UiFactory.Canvas("BannerTestCanvas", null, 0);
            _b = new VexillumBanner(_canvas.transform, new Vector2(0.5f, 1f), new Vector2(0f, -204f));
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_canvas.gameObject);
            UiTheme.Reload();
        }

        float TickWhile(Func<bool> condition, float maxSeconds = 30f)
        {
            float time = 0f;
            while (condition() && time < maxSeconds)
            {
                _b.Tick(Dt);
                time += Dt;
            }
            return time;
        }

        [Test]
        public void Show_RunsDropSettleHoldRoll_AndHidesAtTotalSeconds()
        {
            Assert.AreEqual(BannerPhase.Hidden, _b.Phase);
            Assert.IsFalse(_b.Visible);
            _b.Show("SWARM INCOMING!", BannerCloth.Crimson);
            Assert.IsTrue(_b.Visible);
            Assert.AreEqual("SWARM INCOMING!", _b.Text);
            Assert.AreEqual(BannerCloth.Crimson, _b.Cloth);
            var seen = new List<BannerPhase> { _b.Phase };
            float time = 0f;
            while (_b.Phase != BannerPhase.Hidden && time < 10f)
            {
                _b.Tick(Dt);
                time += Dt;
                if (seen[seen.Count - 1] != _b.Phase) seen.Add(_b.Phase);
            }
            CollectionAssert.AreEqual(new[] { BannerPhase.Dropping, BannerPhase.Settling, BannerPhase.Holding, BannerPhase.Rolling, BannerPhase.Hidden }, seen);
            Assert.AreEqual(VexillumBanner.TotalSeconds(), time, Frames2);
            Assert.AreEqual(0f, _b.Down01);
        }

        [Test]
        public void Drop_PeaksAtOnePlusOvershoot_ThenRests()
        {
            _b.Show("SWARM INCOMING!", BannerCloth.Crimson);
            float peak = 0f;
            for (int i = 0; i < 600 && _b.Phase != BannerPhase.Holding; i++)
            {
                _b.Tick(Dt);
                peak = Mathf.Max(peak, _b.Down01);
            }
            Assert.AreEqual(1f + _t.bannerOvershoot, peak, 0.01f);
            Assert.AreEqual(1f, _b.Down01, 1e-4f, "resting while it holds");
        }

        [Test]
        public void Sway_StaysWithinItsDegrees_AndEndsInTheHold()
        {
            _b.Show("SWARM INCOMING!", BannerCloth.Crimson);
            float maxAbs = 0f, held = 0f;
            for (float time = 0f; _b.Phase != BannerPhase.Rolling && time < 10f; time += Dt)
            {
                _b.Tick(Dt);
                if (_b.Phase == BannerPhase.Dropping || _b.Phase == BannerPhase.Settling)
                    Assert.AreEqual(0f, _b.SwayDegrees, "no sway while dropping");
                if (_b.Phase != BannerPhase.Holding) continue;
                held += Dt;
                maxAbs = Mathf.Max(maxAbs, Mathf.Abs(_b.SwayDegrees));
                if (held > _t.bannerSway + 2f * Dt) Assert.AreEqual(0f, _b.SwayDegrees, 1e-5f, "the sway is over");
            }
            Assert.LessOrEqual(maxAbs, _t.bannerSwayDegrees + 0.01f);
            if (_t.bannerSwayDegrees > 0f && _t.bannerSway > 0f)
                Assert.Greater(maxAbs, 0.5f * _t.bannerSwayDegrees, "it sways");
        }

        float RunOnce(float seconds)
        {
            _b.Clear();
            _b.Show("SWARM INCOMING!", BannerCloth.Crimson, seconds);
            return TickWhile(() => _b.Visible);
        }

        [Test]
        public void Seconds_LengthenTheHold_NeverShortenIt()
        {
            float normal = RunOnce(0f);
            float longer = RunOnce(_t.bannerHold + 1.5f);
            float shorter = RunOnce(_t.bannerHold * 0.25f);
            Assert.AreEqual(VexillumBanner.TotalSeconds(), normal, Frames2);
            Assert.AreEqual(normal + 1.5f, longer, Frames2);
            Assert.AreEqual(VexillumBanner.TotalSeconds(_t.bannerHold + 1.5f), longer, Frames2);
            Assert.AreEqual(normal, shorter, Frames2);
        }

        [Test]
        public void Show_WhileHolding_RollsUpQuicklyWithTheOldWords_ThenDropsTheNewOnes()
        {
            _b.Show("SWARM INCOMING!", BannerCloth.Crimson);
            TickWhile(() => _b.Phase != BannerPhase.Holding);
            _b.Show("GORM, THE PIT MASTER", BannerCloth.Purple, 3f);
            Assert.AreEqual(BannerPhase.Rolling, _b.Phase);
            Assert.IsTrue(_b.Replacing);
            float time = 0f;
            while (_b.Phase == BannerPhase.Rolling && time < 5f)
            {
                Assert.AreEqual("SWARM INCOMING!", _b.Text, "the old words roll up");
                Assert.AreEqual(BannerCloth.Crimson, _b.Cloth);
                _b.Tick(Dt);
                time += Dt;
            }
            Assert.AreEqual(_t.bannerQuickRoll, time, Frames2, "a quick roll-up");
            Assert.AreEqual(BannerPhase.Dropping, _b.Phase);
            Assert.AreEqual("GORM, THE PIT MASTER", _b.Text);
            Assert.AreEqual(BannerCloth.Purple, _b.Cloth);
            Assert.AreEqual(_b.PurpleInk, _b.Words.color);
            Assert.IsFalse(_b.Replacing);
            float rest = TickWhile(() => _b.Visible);
            Assert.AreEqual(_t.bannerQuickRoll + VexillumBanner.TotalSeconds(3f), rest + time, 3f * Dt + 1e-4f,
                "the new one runs its own (longer) hold");
        }

        [Test]
        public void Hold_StaysUntilRollUp()
        {
            _b.Hold("YOU DIED", BannerCloth.Crimson);
            for (int i = 0; i < 600; i++) _b.Tick(Dt);
            Assert.AreEqual(BannerPhase.Holding, _b.Phase, "held for 10 s");
            Assert.IsTrue(_b.Held);
            Assert.AreEqual(1f, _b.Down01, 1e-4f);
            _b.RollUp();
            Assert.IsFalse(_b.Held);
            Assert.AreEqual(BannerPhase.Rolling, _b.Phase);
            float time = TickWhile(() => _b.Visible);
            Assert.AreEqual(_t.bannerRoll, time, Frames2);
        }

        [Test]
        public void Clear_HidesAtOnce()
        {
            _b.Show("SWARM INCOMING!", BannerCloth.Crimson);
            for (int i = 0; i < 30; i++) _b.Tick(Dt);
            _b.Clear();
            Assert.AreEqual(BannerPhase.Hidden, _b.Phase);
            Assert.IsFalse(_b.Visible);
            Assert.AreEqual(0f, _b.Down01);
            _b.Tick(Dt);
            Assert.AreEqual(BannerPhase.Hidden, _b.Phase);
        }

        [Test]
        public void TickingAWholeCycle_AllocatesNothing()
        {
            _b.Show("SWARM INCOMING!", BannerCloth.Crimson);
            _b.Tick(Dt);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 600; i++) _b.Tick(Dt);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(BannerPhase.Hidden, _b.Phase, "600 frames cover a whole cycle");
            Assert.AreEqual(0, allocated, "Tick allocated");
        }
    }
}
