using Gladiator.Core;
using NUnit.Framework;
using UnityEngine;

namespace Gladiator.Tests
{
    /// <summary>
    /// Layout A, symmetric since P3 (docs/tracks/P3-contract.md, P3-B item 1): the cards are always centred, the inventory
    /// column on the left mirrors the stats column on the right (same width and scale, each 24 outside the row and at
    /// least 24 inside the screen). The columns shrink to 0.7, then the row shrinks to fit between them (never under
    /// 0.6), then the columns shrink again (never under 0.5). The P3 cases (1920×1080 with 3 and 4 cards, 4:3, 5:4, 21:9
    /// and a short 1280×720 canvas) and a sweep of canvases where nothing overlaps or leaves the screen.
    /// </summary>
    public class LevelUpLayoutTests
    {
        static Vector2 Canvas(float w, float h) => GridMath.CanvasSize(w, h, MenuLayout.Reference, MenuLayout.Match);

        struct Edges { public float InvLeft, InvRight, RowLeft, RowRight, ColLeft, ColRight; }

        static Edges Of(Vector2 canvas, LevelUpLayout.Fit f, int cards)
        {
            var e = new Edges
            {
                InvLeft = LevelUpLayout.InventoryLeft(canvas, f),
                RowLeft = LevelUpLayout.RowLeft(canvas, f, cards),
                RowRight = LevelUpLayout.RowRight(canvas, f, cards),
                ColLeft = LevelUpLayout.ColumnLeft(canvas, f),
            };
            e.InvRight = e.InvLeft + LevelUpLayout.StatsW * f.StatsScale;
            e.ColRight = e.ColLeft + LevelUpLayout.StatsW * f.StatsScale;
            return e;
        }

        /// <summary>Everything P3 promises on one canvas: centred, mirrored, no overlap, on screen, within the floors.</summary>
        static void AssertSymmetric(Vector2 canvas, int cards, string what)
        {
            var f = LevelUpLayout.For(canvas, cards);
            var e = Of(canvas, f, cards);
            float w = canvas.x, h = canvas.y;
            Assert.IsTrue(f.Centred, what + ": centred");
            Assert.AreEqual(0f, f.ContentX, 1e-4f, what + ": the cards never slide");
            Assert.AreEqual(w * 0.5f, (e.RowLeft + e.RowRight) * 0.5f, 0.01f, what + ": the row is centred");
            Assert.AreEqual(w * 0.5f - e.InvRight, e.ColLeft - w * 0.5f, 0.01f, what + ": the columns are mirror images");
            Assert.LessOrEqual(e.InvRight + LevelUpLayout.Edge, e.RowLeft + 0.01f, what + ": the inventory clears the row by 24");
            Assert.LessOrEqual(e.RowRight + LevelUpLayout.Edge, e.ColLeft + 0.01f, what + ": the stats clear the row by 24");
            Assert.GreaterOrEqual(e.InvLeft, LevelUpLayout.Edge - 0.01f, what + ": the inventory 24 inside the screen");
            Assert.LessOrEqual(e.ColRight, w - LevelUpLayout.Edge + 0.01f, what + ": the stats 24 inside the screen");
            Assert.GreaterOrEqual(f.ContentScale, LevelUpLayout.MinCards - 1e-4f, what + ": the cards never under 0.6");
            Assert.LessOrEqual(f.ContentScale, 1f + 1e-4f, what + ": never scaled up");
            Assert.GreaterOrEqual(f.StatsScale, LevelUpLayout.MinColumns - 1e-4f, what + ": the columns never under 0.5");
            Assert.LessOrEqual(f.StatsScale, 1f + 1e-4f, what);
            Assert.LessOrEqual(LevelUpLayout.ContentH * f.ContentScale, h - LevelUpLayout.VerticalMargin + 0.01f, what + ": the content fits the height");
            float top = LevelUpLayout.ColumnTop(h), bottom = top - LevelUpLayout.StatsH * f.StatsScale;
            Assert.LessOrEqual(top, h * 0.5f, what + ": the columns' top on the canvas");
            Assert.GreaterOrEqual(bottom, -h * 0.5f, what + ": the columns' bottom on the canvas");
        }

        static void AssertCase(Vector2 canvas, int cards, float content, float stats, string what)
        {
            var f = LevelUpLayout.For(canvas, cards);
            Debug.Log($"[P3] {what}: canvas {canvas.x:0}x{canvas.y:0}, {cards} cards -> cards {f.ContentScale:0.###}, columns {f.StatsScale:0.###}; " +
                      $"inventory from x {LevelUpLayout.InventoryLeft(canvas, f):0}, stats from x {LevelUpLayout.ColumnLeft(canvas, f):0}");
            Assert.AreEqual(content, f.ContentScale, 0.002f, what + ": the cards' scale");
            Assert.AreEqual(stats, f.StatsScale, 0.002f, what + ": the columns' scale");
            AssertSymmetric(canvas, cards, what);
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
        public void P3Cases_CardsCentred_ColumnsMirrored()
        {
            AssertCase(Canvas(1920f, 1080f), 3, 1f, 1f, "16:9, 3 cards");
            AssertCase(Canvas(1920f, 1080f), 4, 0.9735f, 0.7f, "16:9, 4 cards: the row shrinks between 0.7 columns");
            AssertCase(Canvas(1600f, 1200f), 3, 1f, 0.7448f, "4:3, 3 cards");
            AssertCase(Canvas(1600f, 1200f), 4, 0.7732f, 0.7f, "4:3, 4 cards");
            AssertCase(Canvas(1280f, 1024f), 3, 0.9832f, 0.7f, "5:4, 3 cards");
            AssertCase(Canvas(1280f, 1024f), 4, 0.7321f, 0.7f, "5:4, 4 cards");
            AssertCase(Canvas(2560f, 1080f), 3, 0.9948f, 1f, "21:9, 3 cards: the content scales to the height");
            AssertCase(new Vector2(1280f, 720f), 3, 0.6381f, 0.7f, "a short 1280×720 canvas, 3 cards");
            AssertCase(new Vector2(1280f, 720f), 4, 0.6f, 0.5044f, "a short 1280×720 canvas, 4 cards: 0.6 cards, the columns shrink");
            // 1280×720 at 16:9 is the 1920×1080 reference canvas: the same layout as 1080p
            var a = LevelUpLayout.For(Canvas(1280f, 720f), 3);
            var b = LevelUpLayout.For(Canvas(1920f, 1080f), 3);
            Assert.AreEqual(b.ContentScale, a.ContentScale, 1e-4f);
            Assert.AreEqual(b.StatsScale, a.StatsScale, 1e-4f);
        }

        [Test]
        public void At1080p_TheColumnsHugTheRow_48FromEachEdge()
        {
            var canvas = Canvas(1920f, 1080f);
            var f = LevelUpLayout.For(canvas, 3);
            Assert.AreEqual(48f, LevelUpLayout.InventoryLeft(canvas, f), 0.01f, "the inventory from x 48 (24 + 24 slack)");
            Assert.AreEqual(1462f, LevelUpLayout.ColumnLeft(canvas, f), 0.01f, "the stats from x 1462 (the row ends at 1438)");
            Assert.AreEqual(3, f.Cards);
            Assert.AreEqual(3, LevelUpLayout.PauseCards, "the pause lines up with a three-card level-up");
        }

        [Test]
        public void UltraWide_ScalesTheContentToItsHeight_ColumnsStayBesideTheRow()
        {
            var canvas = Canvas(2560f, 1080f); // ≈ 2217×935: not a target, must not break
            var f = LevelUpLayout.For(canvas, 3);
            Assert.AreEqual(0.994f, f.ContentScale, 0.002f, "(935 - 40) / 900");
            Assert.IsTrue(f.Centred);
            Assert.AreEqual(1f, f.StatsScale, 1e-4f, "plenty of margin");
            float row = LevelUpLayout.RowRight(canvas, f, 3);
            Assert.AreEqual(row + LevelUpLayout.Edge, LevelUpLayout.ColumnLeft(canvas, f), 0.01f, "beside the cards, not at the far edge");
        }

        [Test]
        public void EveryCanvas_NothingOverlaps_AndNothingLeavesTheCanvas()
        {
            for (float w = 1280f; w <= 2600f; w += 40f)
                for (float h = 720f; h <= 1300f; h += 20f)
                    for (int n = 1; n <= 4; n++)
                        AssertSymmetric(new Vector2(w, h), n, $"{w}x{h}, {n} cards");
        }
    }
}
