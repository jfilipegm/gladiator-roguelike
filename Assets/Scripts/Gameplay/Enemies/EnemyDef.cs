using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>An enemy type as data (CLAUDE.md §2.4). New enemies = new assets; new behaviours = new code.</summary>
    [CreateAssetMenu(menuName = "Gladiator/Enemy", fileName = "EN_")]
    public sealed class EnemyDef : ScriptableObject
    {
        /// <summary>Stable numeric id derived from `id` (events, stats, saves).</summary>
        public int RuntimeId => Gladiator.Core.GameInfo.StableId(id);
        public string id = "convict";
        public string displayName = "Starving Convict";
        [Tooltip("Model prefab (FBX) — rigid parts named Body/Head/ArmL/ArmR/LegL/LegR.")]
        public GameObject model;
        public PartAnimProfile anim;
        public float scale = 1f;

        [Header("Stats")]
        public float maxHp = 20f;
        public float moveSpeed = 3.6f;
        [Tooltip("Speed variation ± fraction, so swarms don't move in lockstep.")]
        [Range(0f, 0.5f)] public float speedJitter = 0.15f;
        public float acceleration = 14f;
        [Tooltip("Body radius for separation, hits and contact (m).")]
        public float radius = 0.38f;
        [Tooltip("1 = normal knockback, 0 = immovable.")]
        [Range(0f, 2f)] public float knockbackTaken = 1f;

        [Header("Attack")]
        public float contactDamage = 8f;
        [Tooltip("Seconds between contact hits from the same enemy.")]
        public float contactInterval = 0.9f;

        [Header("Rewards")]
        public float xp = 1f;
        [Range(0f, 1f)] public float goldChance = 0.05f;

        [Header("Pool")]
        public int prewarm = 64;
    }
}
