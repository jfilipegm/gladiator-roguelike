using UnityEngine;

namespace Gladiator.Core
{
    /// <summary>
    /// U1 layout A for the level-up, chest, altar and boss-reward cards (docs/tracks/U1-design.md §8.3), in canvas units
    /// (1920×1080 reference). The content (title, the row of 300×520 cards, the buttons; about 900 tall) is centred on
    /// the screen and the "your stats" column (410 wide) sits in the right margin, 24 from the edge and 24 from the
    /// last card. The column scales down to fit the margin while that keeps it at 0.7 or more, so the cards stay centred;
    /// below that it holds at 0.7 and the content slides left until it clears the column, then shrinks (never under 0.6).
    /// Pure maths beside MenuLayout, so EditMode tests cover every screen shape.
    /// </summary>
    public static class LevelUpLayout
    {
        public const float CardW = 300f, CardH = 520f, CardGap = 28f, ContentH = 900f, StatsW = 410f, Edge = 24f,
            MinStats = 0.7f, MinCards = 0.6f;

        /// <summary>Room kept above and below the content: its scale is (H − 40) / 900, at most 1.</summary>
        public const float VerticalMargin = 40f;
        /// <summary>The stats column at scale 1: its height, and its top above the canvas centre (y 186 at 1080).</summary>
        public const float StatsH = 730f, StatsTop = 354f;

        /// <summary>ContentX: the content's centre from the canvas centre (&lt; 0 = left), canvas units.</summary>
        public struct Fit { public float ContentScale, StatsScale, ContentX; public bool Centred; }

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

        /// <summary>Where the content goes and how big the stats column is on a canvas of `canvas` units (spec §8.3).</summary>
        public static Fit For(Vector2 canvas, int cards)
        {
            float w = canvas.x > 1f ? canvas.x : 1920f;
            int n = Mathf.Max(1, cards);
            float row = RowWidth(n);
            float s = HeightScale(canvas.y);
            // the right margin: half the room the row leaves, less the screen edge and the gap to the cards
            float margin = (w - row * s) * 0.5f - 2f * Edge;
            float k = Mathf.Min(1f, margin / StatsW);
            // a very short screen (none of the targets: 900+ tall) also caps the column so it never runs off the bottom
            float kTall = (ColumnTop(canvas.y) + (canvas.y > 1f ? canvas.y : 1080f) * 0.5f - Edge) / StatsH;
            if (k >= MinStats) return new Fit { ContentScale = s, StatsScale = Mathf.Min(k, kTall), ContentX = 0f, Centred = true };

            // too narrow: the column holds at 0.7 and the cards slide left until they clear it by 24 ...
            k = MinStats;
            float limit = w - Edge - StatsW * k - Edge; // the cards' right edge may not pass this
            float half = row * s * 0.5f;
            float centre = Mathf.Min(w * 0.5f, limit - half);
            if (centre - half < Edge)
            {
                // ... then shrink to the room between the left edge and the column (never under 0.6)
                s = Mathf.Max(MinCards, Mathf.Min(s, (limit - Edge) / row));
                half = row * s * 0.5f;
                centre = limit - half;
            }
            return new Fit { ContentScale = s, StatsScale = Mathf.Min(k, kTall), ContentX = centre - w * 0.5f, Centred = false };
        }

        /// <summary>The stats column's left edge, canvas units from the left.</summary>
        public static float ColumnLeft(Vector2 canvas, in Fit fit) => (canvas.x > 1f ? canvas.x : 1920f) - Edge - StatsW * fit.StatsScale;

        /// <summary>The card row's left and right edges, canvas units from the left.</summary>
        public static float RowLeft(Vector2 canvas, in Fit fit, int cards) =>
            (canvas.x > 1f ? canvas.x : 1920f) * 0.5f + fit.ContentX - RowWidth(Mathf.Max(1, cards)) * fit.ContentScale * 0.5f;

        public static float RowRight(Vector2 canvas, in Fit fit, int cards) =>
            (canvas.x > 1f ? canvas.x : 1920f) * 0.5f + fit.ContentX + RowWidth(Mathf.Max(1, cards)) * fit.ContentScale * 0.5f;

        /// <summary>The stats column's top above the canvas centre: it scales with the content's height so it never leaves
        /// a short screen (its height is StatsH × StatsScale, hanging from here).</summary>
        public static float ColumnTop(float canvasHeight) => StatsTop * HeightScale(canvasHeight);
    }
}
