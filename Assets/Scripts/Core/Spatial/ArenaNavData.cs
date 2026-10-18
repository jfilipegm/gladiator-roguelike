using UnityEngine;

namespace Gladiator.Core
{
    /// <summary>
    /// An arena's baked enemy-navigation data (CLAUDE.md §4/§7.3): the ground height grid and the static obstacle
    /// list, produced by one raycast pass in an Editor bake step when the arena is built. Loading it costs nothing at
    /// runtime (a 140 m Colosseum would otherwise need ~300k raycasts on the first frame).
    /// </summary>
    [CreateAssetMenu(menuName = "Gladiator/Arena Nav Data", fileName = "ArenaNav")]
    public sealed class ArenaNavData : ScriptableObject
    {
        public Vector2 boundsMin, boundsMax;
        public float cell;
        public int width, height;
        [HideInInspector] public float[] heights;
        [HideInInspector] public Vector4[] circles, boxesA, boxesB;
        public int obstacleCount;
        public string bakedAt;

        /// <summary>True when this data was baked for the same bounds and cell size.</summary>
        public bool Matches(Vector2 min, Vector2 max, float cellSize) =>
            heights != null && heights.Length == width * height && width > 1 && height > 1 &&
            (boundsMin - min).sqrMagnitude < 1e-6f && (boundsMax - max).sqrMagnitude < 1e-6f && Mathf.Abs(cell - cellSize) < 1e-6f;

        public HeightGrid ToHeightGrid() => new HeightGrid(boundsMin.x, boundsMin.y, cell, width, height, heights);

        public StaticObstacles ToObstacles() => StaticObstacles.FromArrays(circles ?? new Vector4[0], boxesA ?? new Vector4[0], boxesB ?? new Vector4[0]);

        public void Store(Vector2 min, Vector2 max, float cellSize, HeightGrid grid, StaticObstacles obstacles)
        {
            boundsMin = min; boundsMax = max; cell = cellSize;
            width = grid.W; height = grid.H;
            heights = (float[])grid.Heights.Clone();
            obstacles.ToArrays(out circles, out boxesA, out boxesB);
            obstacleCount = obstacles.Count;
            bakedAt = System.DateTime.UtcNow.ToString("u");
        }
    }
}
