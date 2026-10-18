using Gladiator.Core;
using NUnit.Framework;

namespace Gladiator.Tests
{
    public class FrameStatsTests
    {
        static FrameSample S(float t, float ms, float gameplay = 1f, long gc = 0, int enemies = 0) =>
            new FrameSample { Time = t, FrameMs = ms, MainThreadMs = ms, GameplayMs = gameplay, GcBytes = gc, Enemies = enemies };

        [Test]
        public void Percentile_NearestRank()
        {
            var a = new float[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
            Assert.AreEqual(5f, FrameStatsRecorder.Percentile(a, 0.5f));
            Assert.AreEqual(10f, FrameStatsRecorder.Percentile(a, 0.95f));
            Assert.AreEqual(1f, FrameStatsRecorder.Percentile(a, 0f));
        }

        [Test]
        public void Summary_ExcludesWarmup_AndCountsGc()
        {
            var s = new[]
            {
                S(0.5f, 100f, gc: 5000),             // warm-up: ignored
                S(3.0f, 10f, 1f, 0, 10),
                S(3.1f, 20f, 2f, 64, 30),
                S(3.2f, 30f, 3f, 0, 20),
            };
            var r = FrameStatsRecorder.Summarize(s, s.Length, 3f);
            Assert.AreEqual(3, r.Frames);
            Assert.AreEqual(20f, r.AvgFrameMs, 1e-4f);
            Assert.AreEqual(30f, r.MaxFrameMs);
            Assert.AreEqual(2f, r.AvgGameplayMs, 1e-4f);
            Assert.AreEqual(64, r.TotalGcBytes);
            Assert.AreEqual(1, r.FramesWithGc);
            Assert.AreEqual(30, r.MaxEnemies);
        }

        [Test]
        public void Summary_EmptyIsSafe()
        {
            var r = FrameStatsRecorder.Summarize(new FrameSample[4], 0, 3f);
            Assert.AreEqual(0, r.Frames);
        }
    }
}
