using UnityEngine;

namespace Gladiator.Core
{
    /// <summary>
    /// Uniform XZ grid over fixed bounds, rebuilt every tick with a counting sort (O(n), zero allocation).
    /// Used for enemy separation, weapon hit queries, targeting, pickups — never Physics.Overlap per entity.
    /// </summary>
    public sealed class SpatialHashGrid
    {
        readonly float _cell, _invCell;
        readonly float _minX, _minZ;
        readonly int _w, _h;
        readonly int[] _cellStart;   // size w*h+1
        readonly int[] _cellCount;   // size w*h
        int[] _sorted;               // entity indices sorted by cell
        int[] _entityCell;           // cell of each entity (or -1)
        int _count;

        public float CellSize => _cell;

        public SpatialHashGrid(Vector2 min, Vector2 max, float cellSize, int capacity)
        {
            _cell = cellSize;
            _invCell = 1f / cellSize;
            _minX = min.x;
            _minZ = min.y;
            _w = Mathf.Max(1, Mathf.CeilToInt((max.x - min.x) * _invCell));
            _h = Mathf.Max(1, Mathf.CeilToInt((max.y - min.y) * _invCell));
            _cellStart = new int[_w * _h + 1];
            _cellCount = new int[_w * _h];
            _sorted = new int[capacity];
            _entityCell = new int[capacity];
        }

        int CellX(float x) => Mathf.Clamp((int)((x - _minX) * _invCell), 0, _w - 1);
        int CellZ(float z) => Mathf.Clamp((int)((z - _minZ) * _invCell), 0, _h - 1);

        /// <summary>Rebuild from positions[0..count). active[i]==false entities are skipped.</summary>
        public void Build(Vector3[] positions, bool[] active, int count)
        {
            if (count > _sorted.Length)
            {
                _sorted = new int[count * 2];
                _entityCell = new int[count * 2];
            }
            _count = count;
            System.Array.Clear(_cellCount, 0, _cellCount.Length);
            for (int i = 0; i < count; i++)
            {
                if (active != null && !active[i]) { _entityCell[i] = -1; continue; }
                int c = CellZ(positions[i].z) * _w + CellX(positions[i].x);
                _entityCell[i] = c;
                _cellCount[c]++;
            }
            int run = 0;
            for (int c = 0; c < _cellCount.Length; c++)
            {
                _cellStart[c] = run;
                run += _cellCount[c];
                _cellCount[c] = 0; // reused as insert cursor
            }
            _cellStart[_cellCount.Length] = run;
            for (int i = 0; i < count; i++)
            {
                int c = _entityCell[i];
                if (c < 0) continue;
                _sorted[_cellStart[c] + _cellCount[c]++] = i;
            }
        }

        /// <summary>Indices of entities whose cells overlap the circle (caller does the exact distance test).</summary>
        public int QueryCandidates(Vector3 center, float radius, int[] results)
        {
            int x0 = CellX(center.x - radius), x1 = CellX(center.x + radius);
            int z0 = CellZ(center.z - radius), z1 = CellZ(center.z + radius);
            int n = 0;
            for (int z = z0; z <= z1; z++)
            {
                int row = z * _w;
                for (int x = x0; x <= x1; x++)
                {
                    int c = row + x;
                    for (int k = _cellStart[c], end = _cellStart[c + 1]; k < end; k++)
                    {
                        if (n == results.Length) return n;
                        results[n++] = _sorted[k];
                    }
                }
            }
            return n;
        }

        /// <summary>Exact circle query on the XZ plane.</summary>
        public int Query(Vector3 center, float radius, Vector3[] positions, int[] results)
        {
            int n = QueryCandidates(center, radius, results);
            float r2 = radius * radius;
            int m = 0;
            for (int i = 0; i < n; i++)
            {
                var p = positions[results[i]];
                float dx = p.x - center.x, dz = p.z - center.z;
                if (dx * dx + dz * dz <= r2) results[m++] = results[i];
            }
            return m;
        }
    }
}
