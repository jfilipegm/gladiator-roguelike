using System;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// What the Codex shows that isn't already in other data (Phase 5.6, docs/UNLOCKS.md): the Bestiary's order and
    /// blurbs, and the portrait atlas baked from the models (CodexSetup.BakePortraits). The Arsenal reads the upgrade
    /// catalog, the Challenges / Secrets / Market tabs the unlocks in MetaConfig.
    /// </summary>
    [CreateAssetMenu(menuName = "Gladiator/Codex Config", fileName = "CodexConfig")]
    public sealed class CodexConfig : ScriptableObject
    {
        [Serializable]
        public struct Beast
        {
            public EnemyDef enemy;
            [Tooltip("Set for a boss: its name and title, and its kills come from the boss records.")]
            public BossDef boss;
            [Tooltip("The first arena tier it fights in: having fought there counts as having seen it (saves from before Phase 5.6 have no per-enemy kills).")]
            public int tier;
            [TextArea] public string blurb;
        }

        public UpgradeCatalog catalog;
        [Tooltip("For the Arsenal's card text (Common numbers).")]
        public UpgradeConfig upgrades;
        public MetaConfig meta;
        public Beast[] bestiary = Array.Empty<Beast>();

        [Header("Portraits (baked from the models)")]
        public Texture2D portraits;
        [Tooltip("Cell index → id: 'en:<EnemyDef id>', 'gl:<GladiatorDef id>', 'sc:<secret>'.")]
        public string[] portraitIds = Array.Empty<string>();
        public int portraitCell = 128;
        public int portraitColumns = 8;

        /// <summary>The atlas UV rect of `id`'s portrait, false if it has none.</summary>
        public bool TryPortrait(string id, out Rect uv)
        {
            uv = default;
            if (portraits == null || portraitIds == null) return false;
            int i = Array.IndexOf(portraitIds, id);
            if (i < 0) return false;
            int rows = Mathf.Max(1, Mathf.CeilToInt(portraitIds.Length / (float)portraitColumns));
            float w = 1f / portraitColumns, h = 1f / rows;
            int col = i % portraitColumns, row = i / portraitColumns;
            uv = new Rect(col * w, 1f - (row + 1) * h, w, h);
            return true;
        }
    }
}
