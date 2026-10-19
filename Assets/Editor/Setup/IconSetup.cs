using System.IO;
using Gladiator.Gameplay;
using UnityEditor;
using UnityEngine;

namespace Gladiator.EditorTools
{
    /// <summary>
    /// Upgrade icons (playtest pass): imports the pixel atlas made by PixelSrc/upgrade_icons.py (point filter, no
    /// mipmaps, no compression: crisp 8-bit pixels) and points every UpgradeDef at its row through the atlas's index
    /// file. Regenerate the art with `python3 PixelSrc/upgrade_icons.py`, then run this.
    /// </summary>
    public static class IconSetup
    {
        public const string AtlasPath = "Assets/Art/Textures/UI/UpgradeIcons.png";
        public const string IndexPath = "Assets/Art/Textures/UI/UpgradeIcons.txt";

        [MenuItem("Gladiator/Setup/Upgrade Icons")]
        public static int Apply()
        {
            var imp = AssetImporter.GetAtPath(AtlasPath) as TextureImporter;
            if (imp == null) { Debug.LogError("[IconSetup] no atlas at " + AtlasPath + " (run PixelSrc/upgrade_icons.py)"); return 0; }
            imp.textureType = TextureImporterType.Default;
            imp.filterMode = FilterMode.Point;
            imp.mipmapEnabled = false;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.alphaIsTransparency = true;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.npotScale = TextureImporterNPOTScale.None;
            imp.sRGBTexture = true;
            imp.isReadable = false;
            imp.SaveAndReimport();
            var atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath);

            var rows = new System.Collections.Generic.Dictionary<string, int>();
            foreach (var line in File.ReadAllLines(IndexPath))
            {
                var parts = line.Split(' ');
                if (parts.Length == 2 && int.TryParse(parts[1], out int row)) rows[parts[0]] = row;
            }
            int set = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:UpgradeDef"))
            {
                var def = AssetDatabase.LoadAssetAtPath<UpgradeDef>(AssetDatabase.GUIDToAssetPath(guid));
                if (def == null) continue;
                if (rows.TryGetValue(def.id, out int row)) { def.icon = atlas; def.iconRow = row; set++; }
                else { def.icon = null; def.iconRow = -1; Debug.LogWarning("[IconSetup] no icon for " + def.id); }
                EditorUtility.SetDirty(def);
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[IconSetup] {set} upgrades have icons");
            return set;
        }
    }
}
