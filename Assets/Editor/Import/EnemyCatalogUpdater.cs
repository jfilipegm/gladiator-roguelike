using System.Collections.Generic;
using Gladiator.Gameplay;
using UnityEditor;
using UnityEngine;

namespace Gladiator.EditorTools
{
    /// <summary>Keeps Assets/Data/EnemyCatalog.asset listing every EnemyDef (sorted by asset path).</summary>
    public sealed class EnemyCatalogUpdater : AssetPostprocessor
    {
        public const string CatalogPath = "Assets/Data/EnemyCatalog.asset";

        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (Touches(imported) || Touches(deleted) || Touches(moved) || Touches(movedFrom)) Refresh();
        }

        static bool Touches(string[] paths)
        {
            foreach (var p in paths)
                if (p.EndsWith(".asset") && p != CatalogPath && (p.StartsWith("Assets/Data/Enemies/") || AssetDatabase.GetMainAssetTypeAtPath(p) == typeof(EnemyDef) || AssetDatabase.GetMainAssetTypeAtPath(p) == typeof(AffixDef)))
                    return true;
            return false;
        }

        static List<T> Collect<T>(string filter) where T : Object
        {
            var paths = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets(filter)) paths.Add(AssetDatabase.GUIDToAssetPath(guid));
            paths.Sort(System.StringComparer.Ordinal);
            var list = new List<T>();
            foreach (var path in paths) { var a = AssetDatabase.LoadAssetAtPath<T>(path); if (a != null) list.Add(a); }
            return list;
        }

        [MenuItem("Gladiator/Setup/Refresh Enemy Catalog")]
        public static EnemyCatalog Refresh()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<EnemyCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<EnemyCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            var paths = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:EnemyDef")) paths.Add(AssetDatabase.GUIDToAssetPath(guid));
            paths.Sort(System.StringComparer.Ordinal);
            var list = new List<EnemyDef>();
            foreach (var p in paths)
            {
                var d = AssetDatabase.LoadAssetAtPath<EnemyDef>(p);
                if (d != null) list.Add(d);
            }
            var affixes = Collect<AffixDef>("t:AffixDef");
            bool changed = catalog.enemies == null || catalog.enemies.Length != list.Count || catalog.affixes == null || catalog.affixes.Length != affixes.Count;
            for (int i = 0; !changed && i < list.Count; i++) changed = catalog.enemies[i] != list[i];
            for (int i = 0; !changed && i < affixes.Count; i++) changed = catalog.affixes[i] != affixes[i];
            if (changed)
            {
                catalog.enemies = list.ToArray();
                catalog.affixes = affixes.ToArray();
                EditorUtility.SetDirty(catalog);
                AssetDatabase.SaveAssetIfDirty(catalog);
            }
            return catalog;
        }
    }
}
