using UnityEngine;

namespace Gladiator.Core
{
    /// <summary>
    /// Ground height sampled once (one downward raycast per cell) and looked up in O(1) with bilinear filtering.
    /// Enemies follow terrain with this instead of physics. Bake at load or in an Editor step (same code).
    /// </summary>
    public sealed class HeightGrid
    {
        public readonly float MinX, MinZ, Cell;
        public readonly int W, H;
        public readonly float[] Heights;

        public HeightGrid(float minX, float minZ, float cell, int w, int h, float[] heights)
        {
            MinX = minX; MinZ = minZ; Cell = cell; W = w; H = h; Heights = heights;
        }

        /// <summary>
        /// One downward ray per cell; takes the highest hit accepted by `isWalkable` (so wall tops and pillar caps
        /// never become "ground"). Null predicate = any hit.
        /// </summary>
        public static HeightGrid Bake(Vector2 min, Vector2 max, float cell, int layerMask, float rayTop = 50f, float fallback = 0f,
            System.Func<Collider, bool> isWalkable = null)
        {
            int w = Mathf.CeilToInt((max.x - min.x) / cell) + 1;
            int h = Mathf.CeilToInt((max.y - min.y) / cell) + 1;
            var hs = new float[w * h];
            var hits = new RaycastHit[16];
            for (int z = 0; z < h; z++)
            for (int x = 0; x < w; x++)
            {
                var o = new Vector3(min.x + x * cell, rayTop, min.y + z * cell);
                int n = Physics.RaycastNonAlloc(o, Vector3.down, hits, rayTop * 2f, layerMask, QueryTriggerInteraction.Ignore);
                float best = float.NegativeInfinity;
                for (int k = 0; k < n; k++)
                    if (hits[k].point.y > best && (isWalkable == null || isWalkable(hits[k].collider))) best = hits[k].point.y;
                hs[z * w + x] = float.IsNegativeInfinity(best) ? fallback : best;
            }
            return new HeightGrid(min.x, min.y, cell, w, h, hs);
        }

        public float Sample(float x, float z)
        {
            float fx = (x - MinX) / Cell, fz = (z - MinZ) / Cell;
            int x0 = Mathf.Clamp((int)fx, 0, W - 2), z0 = Mathf.Clamp((int)fz, 0, H - 2);
            float tx = Mathf.Clamp01(fx - x0), tz = Mathf.Clamp01(fz - z0);
            int i = z0 * W + x0;
            float a = Heights[i], b = Heights[i + 1], c = Heights[i + W], d = Heights[i + W + 1];
            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), tz);
        }
    }
}
