using Gladiator.Core;
using NUnit.Framework;
using UnityEngine;

namespace Gladiator.Tests
{
    /// <summary>
    /// U1 layout A (docs/tracks/U1-design.md §8.3): the level-up's cards stay centred while the "your stats" column can
    /// shrink to 0.7 in the right margin; past that the cards slide left, then shrink. The spec's five cases, the 21:9
    /// height case, and a sweep of canvases (1600-2600 wide, 900-1300 tall, 1-4 cards) where the cards always clear the
    /// column by 24 and nothing leaves the canvas.
    /// </summary>
    public class LevelUpLayoutTests
    {
        static Vector2 Canvas(float w, float h) => GridMath.CanvasSize(w, h, MenuLayout.Reference, MenuLayout.Match);

        static void AssertCase(Vector2 canvas, int cards, float stats, float x, bool centred, string what)
        {
            var f = LevelUpLayout.For(canvas, cards);
            Debug.Log($"[U1] {what}: canvas {canvas.x:0}x{canvas.y:0}, {cards} cards -> scale {f.ContentScale:0.###}, stats {f.StatsScale:0.###}, x {f.ContentX:0.#}, centred {f.Centred}");
            Assert.AreEqual(stats, f.StatsScale, 0.01f, what + ": the column's scale");
            Assert.AreEqual(x, f.ContentX, 1f, what + ": the content's x");
            Assert.AreEqual(centred, f.Centred, what + ": centred");
        }

        [Test]
        public void RowWidth_IsCardsPlusGaps()
        {
            Assert.AreEqual(300f, LevelUpLayout.RowWidth(1));
            Assert.AreEqual(956f, LevelUpLayout.RowWidth(3));
            Assert.AreEqual(1284f, LevelUpLayout.RowWidth(4));
            Assert.AreEqual(0f, LevelUpLayout.RowWidth(0));
            Assert.AreEqual(-328f, LevelUpLayout.CardX(0, 3), 1e-3f, "layout A: x 482 / 810 / 1138 at 1920");
            Assert.AreEqual(0f, LevelUpLayout.CardX(1, 3), 1e-3f);
            Assert.AreEqual(328f, LevelUpLayout.CardX(2, 3), 1e-3f);
        }

        [Test]
        public void SpecCases_StatsScale_ContentX_Centred()
        {
            AssertCase(Canvas(1920f, 1080f), 3, 1f, 0f, true, "16:9, 3 cards");
            AssertCase(Canvas(1920f, 1200f), 3, 0.94f, 0f, true, "16:10, 3 cards");
            AssertCase(Canvas(1600f, 1200f), 3, 0.75f, 0f, true, "4:3, 3 cards");
            AssertCase(Canvas(1920f, 1080f), 4, 0.7f, -17f, false, "16:9, 4 cards");
            AssertCase(Canvas(1600f, 1200f), 4, 0.7f, -146f, false, "4:3, 4 cards");
            Assert.AreEqual(1f, LevelUpLayout.For(Canvas(1920f, 1080f), 4).ContentScale, 1e-4f, "16:9, 4 cards: shifted, not scaled");
            Assert.AreEqual(1f, LevelUpLayout.For(Canvas(1600f, 1200f), 4).ContentScale, 1e-4f, "4:3, 4 cards: shifted, not scaled");
        }

        [Test]
        public void UltraWide_ScalesTheContentToItsHeight()
        {
            var canvas = Canvas(2560f, 1080f); // ≈ 2217×935: not a target, must not break
            var f = LevelUpLayout.For(canvas, 3);
            Assert.AreEqual(0.994f, f.ContentScale, 0.002f, "(935 - 40) / 900");
            Assert.IsTrue(f.Centred);
            Assert.AreEqual(1f, f.StatsScale, 1e-4f, "plenty of margin");
        }

        [Test]
        public void EveryCanvas_CardsClearTheColumn_AndNothingLeavesTheCanvas()
        {
            for (float w = 1600f; w <= 2600f; w += 50f)
                for (float h = 900f; h <= 1300f; h += 25f)
                    for (int n = 1; n <= 4; n++)
                    {
                        var canvas = new Vector2(w, h);
                        var f = LevelUpLayout.For(canvas, n);
                        string what = $"{w}x{h}, {n} cards";
                        float left = LevelUpLayout.RowLeft(canvas, f, n), right = LevelUpLayout.RowRight(canvas, f, n);
                        float column = LevelUpLayout.ColumnLeft(canvas, f);
                        Assert.LessOrEqual(right + LevelUpLayout.Edge, column + 0.01f, what + ": the cards clear the column by 24");
                        Assert.GreaterOrEqual(left, -0.01f, what + ": the cards stay on the canvas (left)");
                        Assert.LessOrEqual(column + LevelUpLayout.StatsW * f.StatsScale, w + 0.01f, what + ": the column stays on the canvas (right)");
                        Assert.GreaterOrEqual(f.StatsScale, LevelUpLayout.MinStats - 1e-4f, what + ": the column never under 0.7");
                        Assert.LessOrEqual(f.StatsScale, 1f + 1e-4f, what);
                        Assert.GreaterOrEqual(f.ContentScale, LevelUpLayout.MinCards - 1e-4f, what + ": the cards never under 0.6");
                        Assert.LessOrEqual(f.ContentScale, 1f + 1e-4f, what + ": never scaled up");
                        Assert.LessOrEqual(LevelUpLayout.ContentH * f.ContentScale, h - LevelUpLayout.VerticalMargin + 0.01f, what + ": the content fits the height");
                        float top = LevelUpLayout.ColumnTop(h), bottom = top - LevelUpLayout.StatsH * f.StatsScale;
                        Assert.LessOrEqual(top, h * 0.5f, what + ": the column's top on the canvas");
                        Assert.GreaterOrEqual(bottom, -h * 0.5f, what + ": the column's bottom on the canvas");
                        if (f.Centred) Assert.AreEqual(0f, f.ContentX, 1e-4f, what + ": centred means x 0");
                        else Assert.Less(f.ContentX, 0f, what + ": only ever shifted left");
                    }
        }
    }
}
