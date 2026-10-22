using UnityEngine;

namespace Gladiator.Core
{
    /// <summary>
    /// U1 layout A for the level-up, chest, altar and boss-reward cards and the pause (docs/tracks/U1-design.md §8.3), in
    /// canvas units (1920×1080 reference). P3 (the producer's notes 15 and 17, docs/tracks/P3-contract.md): symmetric.
    /// The content (title, the row of 300×520 cards, the buttons; about 900 tall) is always centred; the "your items"
    /// column (the inventory) on the left mirrors the "your stats" column on the right: both 410 wide at the same scale,
    /// both hanging from ColumnTop, each 24 outside the row and at least 24 inside the screen. The columns scale down to
    /// fit the margins while that keeps them at 0.7 or more; below that they hold at 0.7 and the row shrinks to fit
    /// between them; if the row would go under 0.6 it holds there and the columns shrink instead (never under 0.5).
    /// Pure maths beside MenuLayout, so EditMode tests cover every screen shape.
    /// </summary>
    public static class LevelUpLayout
    {
        public const float CardW = 300f, CardH = 520f, CardGap = 28f, ContentH = 900f, StatsW = 410f, Edge = 24f,
            MinStats = 0.7f, MinCards = 0.6f;

        /// <summary>The columns' last resort when even 0.6 cards leave no room for 0.7 columns (narrower than 4:3).</summary>
        public const float MinColumns = 0.5f;

        /// <summary>Room kept above and below the content: its scale is (H − 40) / 900, at most 1.</summary>
        public const float VerticalMargin = 40f;
        /// <summary>The columns at scale 1: their height at most, and their top above the canvas centre (y 207 at 1080).</summary>
        public const float StatsH = 730f, StatsTop = 333f; // P3 integration: 333, not 354, so both columns clear the HUD's plates (top at y 207 at 1080)

        /// <summary>The pause lines up with a level-up of this many cards (the same fit: P3-contract item 1).</summary>
        public const int PauseCards = 3;

        /// <summary>ContentX: the content's centre from the canvas centre (always 0 since P3: the cards stay centred).
        /// StatsScale: both columns' scale. Cards: the row the fit was made for (the columns sit 24 outside it).</summary>
        public struct Fit { public float ContentScale, StatsScale, ContentX; public bool Centred; public int Cards; }

        /// <summary>The card row's width: n·300 + (n−1)·28 (956 for three, 1284 for four).</summary>
        public static float RowWidth(int cards) => cards <= 0 ? 0f : cards * CardW + (cards - 1) * CardGap;

        /// <summary>Card `index`'s centre x in the content's own units (the row centred on 0).</summary>
        public static float CardX(int index, int cards) => -(RowWidth(cards) - CardW) * 0.5f + index * (CardW + CardGap);

        /// <summary>The content's scale from the canvas height alone: min(1, (H − 40) / 900).</summary>
        public static float HeightScale(float canvasHeight)
        {
            float h = canvasHeight > 1f ? canvasHeight : 1080f;
            return Mathf.Clamp((h - VerticalMargin) / ContentH, MinCards, 1f);
        }

        /// <summary>Where the content goes and how big the two columns are on a canvas of `canvas` units.</summary>
        public static Fit For(Vector2 canvas, int cards)
        {
            float w = canvas.x > 1f ? canvas.x : 1920f;
            float h = canvas.y > 1f ? canvas.y : 1080f;
            int n = Mathf.Max(1, cards);
            float row = RowWidth(n);
            float s = HeightScale(h);
            // each margin: half the room the row leaves, less the screen edge and the gap to the cards
            float margin = (w - row * s) * 0.5f - 2f * Edge;
            float k = Mathf.Min(1f, margin / StatsW);
            if (k < MinStats)
            {
                // the columns hold at 0.7 and the row shrinks to fit between them ...
                k = MinStats;
                s = Mathf.Min(s, (w - 4f * Edge - 2f * StatsW * k) / row);
                if (s < MinCards)
                {
                    // ... never under 0.6: then the columns shrink instead
                    s = MinCards;
                    k = Mathf.Max(MinColumns, (w - 4f * Edge - row * s) / (2f * StatsW));
                }
            }
            // a very short screen (none of the targets: 900+ tall) also caps the columns so they never run off the bottom
            float kTall = (ColumnTop(h) + h * 0.5f - Edge) / StatsH;
            return new Fit { ContentScale = s, StatsScale = Mathf.Min(k, kTall), ContentX = 0f, Centred = true, Cards = n };
        }

        static float Width(Vector2 canvas) => canvas.x > 1f ? canvas.x : 1920f;

        /// <summary>The card row's left and right edges, canvas units from the left.</summary>
        public static float RowLeft(Vector2 canvas, in Fit fit, int cards) =>
            Width(canvas) * 0.5f + fit.ContentX - RowWidth(Mathf.Max(1, cards)) * fit.ContentScale * 0.5f;

        public static float RowRight(Vector2 canvas, in Fit fit, int cards) =>
            Width(canvas) * 0.5f + fit.ContentX + RowWidth(Mathf.Max(1, cards)) * fit.ContentScale * 0.5f;

        /// <summary>The stats column's left edge, canvas units from the left: 24 right of the row the fit was made for.</summary>
        public static float ColumnLeft(Vector2 canvas, in Fit fit) => RowRight(canvas, fit, fit.Cards) + Edge;

        /// <summary>The inventory column's left edge, canvas units from the left: its right edge 24 left of the row (the
        /// stats column's mirror image).</summary>
        public static float InventoryLeft(Vector2 canvas, in Fit fit) => RowLeft(canvas, fit, fit.Cards) - Edge - StatsW * fit.StatsScale;

        /// <summary>The columns' top above the canvas centre: it scales with the content's height so it never leaves a
        /// short screen (each column is at most StatsH × StatsScale tall, hanging from here).</summary>
        public static float ColumnTop(float canvasHeight) => StatsTop * HeightScale(canvasHeight);
    }
}
