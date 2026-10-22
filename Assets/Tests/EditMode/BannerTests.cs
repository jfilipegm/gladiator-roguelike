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
    /// Dropping → Settling → Holding → Rolling → Hidden in TotalSeconds; the drop's overshoot; the event's seconds
    /// lengthen the hold but never shorten it; a new banner while one holds rolls the old one up quickly, then drops;
    /// Hold stays until RollUp; Clear hides at once; and a whole cycle of ticks allocates nothing.
    /// P3 (the producer's note 16): no rotation ever; the wave's function (ClothWave.Offset, VexillumBanner.WaveEnvelope),
    /// its strength through a banner (full while dropping, gone before the roll-up), the words riding it, the cloth's mesh
    /// cut into columns. Timings come from UiTheme.Current (the asset's, if the designer retuned them).
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
        public void NeverRotates_TheWaveRunsWhileItDrops_AndIsGoneBeforeTheRollUp()
        {
            _b.Show("SWARM INCOMING!", BannerCloth.Crimson);
            float since = 0f, waveEnd = _t.bannerDrop + 3f * _t.bannerWaveDecay, peak = 0f;
            bool waved = false;
            for (int i = 0; i < 1200 && _b.Visible; i++)
            {
                _b.Tick(Dt);
                since += Dt;
                Assert.AreEqual(0f, _b.SwayDegrees, "no sway");
                Assert.AreEqual(Quaternion.identity, _b.ClothImage.rectTransform.localRotation, "no rotation");
                float env = _b.WaveEnvelope01;
                Assert.GreaterOrEqual(env, 0f);
                Assert.LessOrEqual(env, 1f + 1e-5f);
                Assert.AreEqual(_t.bannerWaveAmplitude * _b.ClothImage.rectTransform.rect.height * env, _b.WaveAmplitude, 1e-3f,
                    "the wave's height: a share of the cloth's");
                if (_b.Phase == BannerPhase.Dropping) Assert.AreEqual(1f, env, 1e-5f, "full strength while it drops");
                if (_b.Phase == BannerPhase.Rolling) Assert.AreEqual(0f, env, 1e-5f, "flat when it rolls up");
                if (since > waveEnd + Dt) Assert.AreEqual(0f, env, 1e-5f, "gone 3 × decay after the drop");
                peak = Mathf.Max(peak, env);
                if (env > 0f && _t.bannerWaveAmplitude > 0f) waved = true;
            }
            Assert.IsFalse(_b.Visible, "a whole cycle");
            Assert.IsTrue(waved, "the cloth waved");
            Assert.AreEqual(1f, peak, 1e-5f);
            Assert.Less(waveEnd, _t.bannerDrop + _t.bannerSettle + _t.bannerHold, "the defaults: still before the roll-up");
        }

        [Test]
        public void WaveEnvelope_FullWhileDropping_AThirdPerDecay_ThenCut()
        {
            const float drop = 0.32f, d = 0.45f;
            Assert.AreEqual(0f, VexillumBanner.WaveEnvelope(BannerPhase.Hidden, 0.1f, drop, d));
            Assert.AreEqual(1f, VexillumBanner.WaveEnvelope(BannerPhase.Dropping, 0.1f, drop, d));
            Assert.AreEqual(1f, VexillumBanner.WaveEnvelope(BannerPhase.Settling, drop, drop, d), 1e-5f, "continuous at the drop's end");
            Assert.AreEqual(1f / 3f, VexillumBanner.WaveEnvelope(BannerPhase.Settling, drop + d, drop, d), 1e-4f, "a third after one decay");
            Assert.AreEqual(1f / 9f, VexillumBanner.WaveEnvelope(BannerPhase.Holding, drop + 2f * d, drop, d), 1e-4f, "a ninth after two");
            float late = VexillumBanner.WaveEnvelope(BannerPhase.Holding, drop + 2.5f * d, drop, d);
            Assert.Less(late, 1f / 27f, "cut between 2 and 3 decays");
            Assert.Greater(late, 0f);
            Assert.AreEqual(0f, VexillumBanner.WaveEnvelope(BannerPhase.Holding, drop + 3f * d, drop, d), "gone after three");
            Assert.AreEqual(0f, VexillumBanner.WaveEnvelope(BannerPhase.Rolling, drop + 5f, drop, d));
            Assert.AreEqual(0f, VexillumBanner.WaveEnvelope(BannerPhase.Settling, drop + 0.01f, drop, 0f), "decay 0: it stops with the drop");
            float prev = 2f;
            for (float t = drop; t < drop + 3f * d; t += 0.01f)
            {
                float e = VexillumBanner.WaveEnvelope(BannerPhase.Holding, t, drop, d);
                Assert.LessOrEqual(e, prev + 1e-6f, "it only ever falls");
                prev = e;
            }
        }

        [Test]
        public void ClothWaveOffset_IsTheFormula_AndTravelsRight()
        {
            const float a = 8.4f, len = 418f;
            for (float x = 0f; x <= 760f; x += 38f)
                for (float ph = 0f; ph < 2f; ph += 0.13f)
                {
                    Assert.AreEqual(a * Mathf.Sin(2f * Mathf.PI * (x / len - ph)), ClothWave.Offset(x, a, len, ph), 1e-4f);
                    Assert.LessOrEqual(Mathf.Abs(ClothWave.Offset(x, a, len, ph)), a + 1e-4f);
                    // a crest at x moves to x + λ·Δ when the phase grows by Δ: the wave travels across the cloth
                    Assert.AreEqual(ClothWave.Offset(x, a, len, ph), ClothWave.Offset(x + len * 0.25f, a, len, ph + 0.25f), 1e-3f);
                }
            Assert.AreEqual(0f, ClothWave.Offset(123f, 0f, len, 0.3f), "no amplitude, no wave");
        }

        [Test]
        public void TheWords_RideTheWave_AndLieFlatAfterIt()
        {
            _b.Show("SWARM INCOMING!", BannerCloth.Crimson);
            for (int i = 0; i < 8; i++) _b.Tick(Dt); // mid-drop
            Assert.AreEqual(BannerPhase.Dropping, _b.Phase);
            var words = _b.Words;
            var info = words.textInfo;
            Assert.Greater(info.characterCount, 0);
            float fromLeft = words.rectTransform.anchoredPosition.x + _b.ClothWidth * 0.5f;
            var verts = info.meshInfo[0].vertices;
            int moved = 0;
            for (int c = 0; c < info.characterCount; c++)
            {
                var ch = info.characterInfo[c];
                if (!ch.isVisible || ch.materialReferenceIndex != 0) continue;
                int v = ch.vertexIndex;
                float cx = (verts[v].x + verts[v + 2].x) * 0.5f;
                float lift = verts[v].y - ch.vertex_BL.position.y; // the vertex TMP laid out, before the wave
                Assert.AreEqual(_b.WaveOffsetAt(fromLeft + cx), lift, 0.05f, "character " + c + " rides the wave at its x");
                if (Mathf.Abs(lift) > 0.01f) moved++;
            }
            if (_t.bannerWaveAmplitude > 0f) Assert.Greater(moved, 0, "the words move with the cloth");

            for (int i = 0; i < 600 && _b.Phase != BannerPhase.Rolling; i++) _b.Tick(Dt);
            Assert.AreEqual(0f, _b.WaveAmplitude, "flat by the roll-up");
            verts = info.meshInfo[0].vertices;
            for (int c = 0; c < info.characterCount; c++)
            {
                var ch = info.characterInfo[c];
                if (!ch.isVisible || ch.materialReferenceIndex != 0) continue;
                Assert.AreEqual(ch.vertex_BL.position.y, verts[ch.vertexIndex].y, 0.05f, "character " + c + " back on its line");
            }
        }

        [Test]
        public void TheClothsMesh_IsCutIntoColumns_WhileItWaves()
        {
            _b.Show("SWARM INCOMING!", BannerCloth.Crimson);
            for (int i = 0; i < 8; i++) _b.Tick(Dt);
            Assert.Greater(_b.WaveAmplitude, 0f);
            Canvas.ForceUpdateCanvases(); // the Image rebuilds its mesh through ClothWave
            int n = _b.Wave.LastVertexCount;
            if (n == 0) Assert.Inconclusive("the canvas didn't rebuild the cloth's mesh in this EditMode run");
            Debug.Log($"[P3] the waving cloth's mesh: {n} vertices ({_b.ClothWidth:0} wide, columns {_b.Wave.ColumnWidth:0})");
            Assert.Greater(n, 9 * 6, "more than the sliced frame's nine quads");
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
