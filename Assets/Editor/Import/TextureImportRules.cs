using UnityEditor;
using UnityEngine;

namespace Gladiator.EditorTools
{
    /// <summary>
    /// Palette textures (Assets/Art/Textures/Palette): point filtered, no mips, uncompressed, clamped, sRGB.
    /// Alpha is NOT transparency — it stores (1 - emission) per swatch (see Tools/build_palette.py).
    /// </summary>
    public class TextureImportRules : AssetPostprocessor
    {
        const string PaletteRoot = "Assets/Art/Textures/Palette/";

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(PaletteRoot)) return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Default;
            ti.sRGBTexture = true;
            ti.alphaSource = TextureImporterAlphaSource.FromInput;
            ti.alphaIsTransparency = false;
            ti.mipmapEnabled = false;
            ti.filterMode = FilterMode.Point;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.isReadable = false;
        }
    }
}
