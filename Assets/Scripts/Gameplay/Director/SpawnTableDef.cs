using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// What an arena throws at you, as data (CLAUDE.md §6.5 Director): each entry is an enemy type with a time window,
    /// a weight that ramps across that window, and a max-alive cap (kiting types must be capped — CONCERNS C6).
    /// </summary>
    [CreateAssetMenu(menuName = "Gladiator/Spawn Table", fileName = "ST_")]
    public sealed class SpawnTableDef : ScriptableObject
    {
        [System.Serializable]
        public struct Entry
        {
            public EnemyDef enemy;
            [Tooltip("Run time (s) when this type starts appearing.")]
            public float from;
            [Tooltip("Run time (s) when it stops (0 = until the end).")]
            public float to;
            [Tooltip("Relative chance at `from` and at the end of its window (linear in between).")]
            public float weightStart, weightEnd;
            [Tooltip("Max alive at once (0 = no cap). Keep ranged/area types low.")]
            public int maxAlive;
            [Tooltip("Can make up a swarm event (fast, weak types).")]
            public bool swarm;
            [Tooltip("Can be picked for the periodic elite.")]
            public bool elite;
        }

        public Entry[] entries = new Entry[0];
        [Tooltip("Affixes the periodic elites roll from.")]
        public AffixDef[] affixes = new AffixDef[0];

        public static bool Active(in Entry e, float t) => e.enemy != null && t >= e.from && (e.to <= 0f || t < e.to);

        public static float Weight(in Entry e, float t, float runLength)
        {
            float end = e.to > 0f ? e.to : runLength;
            float k = end > e.from ? Mathf.Clamp01((t - e.from) / (end - e.from)) : 1f;
            return Mathf.Max(0f, Mathf.Lerp(e.weightStart, e.weightEnd, k));
        }
    }
}
