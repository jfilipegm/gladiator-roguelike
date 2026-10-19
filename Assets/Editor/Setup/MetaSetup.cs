using System.Collections.Generic;
using Gladiator.Gameplay;
using UnityEditor;
using UnityEngine;

namespace Gladiator.EditorTools
{
    /// <summary>
    /// Meta progression data (M3.6): MetaConfig (Renown rules + the unlock list) and the slice's one unlock, Gorm's
    /// Flail (defeat Gorm → the flail weapon card joins the level-up pool). Assets are created once (designer-owned);
    /// the unlock list is refreshed from every UnlockDef in the project.
    /// </summary>
    public static class MetaSetup
    {
        public const string Dir = "Assets/Data/Meta/";
        public const string ConfigPath = Dir + "MetaConfig.asset";
        public const string FlailUnlockPath = Dir + "UL_GormsFlail.asset";

        [MenuItem("Gladiator/Setup/Meta/Create Meta Data")]
        public static MetaConfig CreateOrUpdate()
        {
            var cfg = PlayerSetup.EnsureAsset<MetaConfig>(ConfigPath);
            if (AssetDatabase.LoadAssetAtPath<UnlockDef>(FlailUnlockPath) == null)
            {
                var ul = PlayerSetup.EnsureAsset<UnlockDef>(FlailUnlockPath);
                ul.id = "gorms_flail";
                ul.displayName = "Gorm's Flail";
                ul.description = "The Pit Master's flail is yours: spiked balls whirl around you and smash whatever comes close. Now in the level-up pool.";
                ul.hint = "The Pit Master never lets go of his flail. Make him.";
                ul.conditions = new[] { new UnlockCondition { kind = UnlockConditionKind.DefeatBoss, id = "gorm", amount = 1f } };
                var card = AssetDatabase.LoadAssetAtPath<UpgradeDef>(UpgradeSetup.FlailCardPath);
                ul.addsToPool = card != null ? new[] { card } : new UpgradeDef[0];
                if (card == null) Debug.LogError("[MetaSetup] " + UpgradeSetup.FlailCardPath + " missing: run Create Upgrade Data first");
                EditorUtility.SetDirty(ul);
            }

            var paths = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:UnlockDef")) paths.Add(AssetDatabase.GUIDToAssetPath(guid));
            paths.Sort(System.StringComparer.Ordinal);
            var list = new List<UnlockDef>();
            foreach (var p in paths)
            {
                var u = AssetDatabase.LoadAssetAtPath<UnlockDef>(p);
                if (u != null) list.Add(u);
            }
            cfg.unlocks = list.ToArray();
            EditorUtility.SetDirty(cfg);
            AssetDatabase.SaveAssets();
            return cfg;
        }

        public static MetaConfig Load() => AssetDatabase.LoadAssetAtPath<MetaConfig>(ConfigPath);
    }
}
