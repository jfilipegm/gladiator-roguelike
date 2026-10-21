using System;
using UnityEngine;

namespace Gladiator.Core
{
    /// <summary>
    /// Pure maths for the menus' tile grids and the arena ladder (R9: the select screens and the Codex roster). Row-major
    /// grids, top-left first; the ladder stacks bottom to top. No Unity objects, so EditMode tests cover it.
    /// </summary>
    public static class GridMath
    {
        public static int Rows(int count, int columns) => count <= 0 || columns <= 0 ? 0 : (count + columns - 1) / columns;

        /// <summary>
        /// A 2D cursor move: `dx` walks the row and stops at its ends, `dy` (positive = down the screen) moves a row and
        /// stops at the top and bottom; moving down into a short last row lands on its last tile. Returns `index`
        /// unchanged when the move is blocked.
        /// </summary>
        public static int Step(int index, int count, int columns, int dx, int dy)
        {
            if (count <= 0 || columns <= 0) return 0;
            index = Mathf.Clamp(index, 0, count - 1);
            int col = index % columns, row = index / columns;
            if (dx != 0)
            {
                int c = col + Math.Sign(dx);
                if (c < 0 || c >= columns) return index;
                int i = row * columns + c;
                return i < count ? i : index;
            }
            if (dy != 0)
            {
                int r = row + Math.Sign(dy);
                if (r < 0 || r >= Rows(count, columns)) return index;
                return Mathf.Min(r * columns + col, count - 1);
            }
            return index;
        }

        /// <summary>Reading-order move that wraps at both ends (the Codex roster's up/down: left/right switch tabs there).</summary>
        public static int StepWrap(int index, int count, int d)
        {
            if (count <= 0) return 0;
            return ((index + Math.Sign(d)) % count + count) % count;
        }

        /// <summary>A one-column ladder: up (+1) climbs, down (-1) descends, both stop at the ends.</summary>
        public static int Climb(int index, int count, int d) => count <= 0 ? 0 : Mathf.Clamp(index + Math.Sign(d), 0, count - 1);

        /// <summary>The grid's size: full rows across (fewer tiles than columns: just those), every row down.</summary>
        public static Vector2 Size(int count, int columns, Vector2 tile, float gap)
        {
            if (count <= 0 || columns <= 0) return Vector2.zero;
            int cols = Mathf.Min(columns, count), rows = Rows(count, columns);
            return new Vector2(cols * tile.x + (cols - 1) * gap, rows * tile.y + (rows - 1) * gap);
        }

        /// <summary>Tile `index`'s centre from the grid's top-left corner (x right, y up: rows go negative).</summary>
        public static Vector2 Cell(int index, int columns, Vector2 tile, float gap)
        {
            if (columns <= 0) columns = 1;
            int col = index % columns, row = index / columns;
            return new Vector2(col * (tile.x + gap) + tile.x * 0.5f, -(row * (tile.y + gap) + tile.y * 0.5f));
        }

        /// <summary>The uniform scale (at most 1) that fits `size` into `area`.</summary>
        public static float Fit(Vector2 size, Vector2 area)
        {
            float s = 1f;
            if (size.x > area.x && size.x > 0f) s = Mathf.Min(s, area.x / size.x);
            if (size.y > area.y && size.y > 0f) s = Mathf.Min(s, area.y / size.y);
            return Mathf.Max(0.01f, s);
        }

        /// <summary>Ladder step `index`'s centre from the ladder's bottom-left corner: each step one tile higher and
        /// `shift` further right than the one below (a staircase, the Slave Pit at the bottom).</summary>
        public static Vector2 LadderCell(int index, Vector2 tile, float gap, float shift) =>
            new Vector2(tile.x * 0.5f + index * shift, tile.y * 0.5f + index * (tile.y + gap));

        public static Vector2 LadderSize(int count, Vector2 tile, float gap, float shift) =>
            count <= 0 ? Vector2.zero : new Vector2(tile.x + (count - 1) * shift, count * tile.y + (count - 1) * gap);

        /// <summary>The canvas size in reference units that a ScaleWithScreenSize CanvasScaler gives a screen (Unity's
        /// formula: the scale is the log-space blend of the width and height ratios).</summary>
        public static Vector2 CanvasSize(float screenW, float screenH, Vector2 reference, float match)
        {
            if (screenW <= 0f || screenH <= 0f) return reference;
            float logW = Mathf.Log(screenW / reference.x, 2f), logH = Mathf.Log(screenH / reference.y, 2f);
            float scale = Mathf.Pow(2f, Mathf.Lerp(logW, logH, match));
            return new Vector2(screenW / scale, screenH / scale);
        }
    }

    /// <summary>
    /// Where the select screens put things, in canvas units (1920×1080 reference): the grid or ladder on the left, the
    /// 3D diorama in the middle, the tablet on the right, the header above and the buttons below. Each block is anchored
    /// to its edge and scaled down (never up) to fit the space it gets, so nothing ever slides off screen.
    /// </summary>
    public static class MenuLayout
    {
        public static readonly Vector2 Reference = new Vector2(1920f, 1080f);
        public const float Match = 0.5f;

        public const float Margin = 56f;
        /// <summary>Above the blocks: the header and the key line.</summary>
        public const float Top = 140f;
        /// <summary>Below the blocks: BACK and FIGHT!.</summary>
        public const float Bottom = 136f;

        public const int Columns = 4;
        public static readonly Vector2 Tile = new Vector2(148f, 190f);
        public const float Gap = 12f;

        public static readonly Vector2 Step = new Vector2(500f, 124f);
        public const float StepGap = 10f;
        public const float StepShift = 22f;

        public static readonly Vector2 Tablet = new Vector2(560f, 712f);

        /// <summary>The height the left and right blocks share on a canvas of `canvas` units.</summary>
        public static float BlockHeight(Vector2 canvas) => Mathf.Max(1f, canvas.y - Top - Bottom);

        /// <summary>The roster grid's scale: fits under the header, above the buttons, within its column.</summary>
        public static float GridScale(Vector2 canvas, int count) =>
            GridMath.Fit(GridMath.Size(count, Columns, Tile, Gap), new Vector2(GridMath.Size(Columns, Columns, Tile, Gap).x, BlockHeight(canvas)));

        public static float LadderScale(Vector2 canvas, int count) =>
            GridMath.Fit(GridMath.LadderSize(count, Step, StepGap, StepShift), new Vector2(LadderWidth(count), BlockHeight(canvas)));

        public static float LadderWidth(int count) => GridMath.LadderSize(Mathf.Max(1, count), Step, StepGap, StepShift).x;

        public static float TabletScale(Vector2 canvas) => GridMath.Fit(Tablet, new Vector2(Tablet.x, BlockHeight(canvas)));

        /// <summary>The left block's right edge (canvas units from the left).</summary>
        public static float GridRight(Vector2 canvas, int count) =>
            Margin + GridMath.Size(Mathf.Max(count, Columns), Columns, Tile, Gap).x * GridScale(canvas, count);

        public static float LadderRight(Vector2 canvas, int count) => Margin + LadderWidth(count) * LadderScale(canvas, count);

        /// <summary>The tablet's left edge (canvas units from the left).</summary>
        public static float TabletLeft(Vector2 canvas) => canvas.x - Margin - Tablet.x * TabletScale(canvas);
    }
}
