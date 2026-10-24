using Gladiator.Gameplay;
using UnityEditor;
using UnityEngine;

namespace Gladiator.EditorTools
{
    /// <summary>
    /// Note 32: Blood Slide's trail as blood smeared on the sand. Imports the smear atlas made by
    /// PixelSrc/blood_smears.py (data, not colour: linear, point filtered, no mipmaps, uncompressed), makes the shared
    /// material M_FxBloodTrail (Gladiator/BloodSmear; its light settings follow the floor's M_PaletteLit_Env; its colours
    /// and timings are the shader's defaults on creation, then the designer's) and puts a BloodTrailRenderer with it on
    /// the player prefab, in place (PlayerSetup.BuildPrefab adds it too). Regenerate the art with
    /// `python3 PixelSrc/blood_smears.py`, then run this.
    /// </summary>
    public static class BloodTrailSetup
    {
        public const string TexturePath = "Assets/Art/Textures/FX/BloodSmears.png";
        public const string MaterialPath = LookSetup.MatDir + "/M_FxBloodTrail.mat";
        public const string ShaderName = "Gladiator/BloodSmear";

        [MenuItem("Gladiator/Setup/Combat/Blood Trail")]
        public static string Apply()
        {
            var mat = EnsureMaterial();
            if (mat == null) return "blood trail: FAILED (see the console)";
            var root = PrefabUtility.LoadPrefabContents(PlayerSetup.PrefabPath);
            try
            {
                AddTo(root, mat);
                PrefabUtility.SaveAsPrefabAsset(root, PlayerSetup.PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
            Debug.Log("[BloodTrailSetup] " + MaterialPath + " on " + PlayerSetup.PrefabPath);
            return "blood trail ok";
        }

        /// <summary>The player's BloodTrailRenderer (added if missing) with the shared material.</summary>
        public static BloodTrailRenderer AddTo(GameObject player, Material mat = null)
        {
            var r = player.GetComponent<BloodTrailRenderer>();
            if (r == null) r = player.AddComponent<BloodTrailRenderer>();
            r.material = mat != null ? mat : EnsureMaterial();
            return r;
        }

        /// <summary>The atlas imported as data and the shared material made or updated (null if the art or the shader is missing).</summary>
        public static Material EnsureMaterial()
        {
            var imp = AssetImporter.GetAtPath(TexturePath) as TextureImporter;
            if (imp == null) { Debug.LogError("[BloodTrailSetup] no atlas at " + TexturePath + " (run PixelSrc/blood_smears.py)"); return null; }
            if (BloodSmearImportRules.Configure(imp)) imp.SaveAndReimport();
            var shader = Shader.Find(ShaderName);
            if (shader == null) { Debug.LogError("[BloodTrailSetup] shader " + ShaderName + " not found"); return null; }

            var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (mat == null)
            {
                System.IO.Directory.CreateDirectory(LookSetup.MatDir);
                mat = new Material(shader) { name = "M_FxBloodTrail" };
                AssetDatabase.CreateAsset(mat, MaterialPath);
            }
            else if (mat.shader != shader) mat.shader = shader;
            mat.SetTexture("_SmearTex", AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath));
            mat.SetFloat("_Cells", 2f); // PixelSrc/blood_smears.py's CELLS (BloodTrailRenderer.Shapes = 2 x 2)
            var env = AssetDatabase.LoadAssetAtPath<Material>(LookSetup.EnvMat); // lit like the floor it lies on
            if (env != null)
            {
                if (env.HasProperty("_Wrap")) mat.SetFloat("_Wrap", env.GetFloat("_Wrap"));
                if (env.HasProperty("_ShadowTint")) mat.SetColor("_ShadowTint", env.GetColor("_ShadowTint"));
                if (env.HasProperty("_ShadowStrength")) mat.SetFloat("_ShadowStrength", env.GetFloat("_ShadowStrength"));
            }
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            return mat;
        }
    }

    /// <summary>The smear atlas is data (thickness, glint, noise, coverage), not colour: linear, crisp, no mipmaps.</summary>
    public class BloodSmearImportRules : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (assetPath == BloodTrailSetup.TexturePath) Configure((TextureImporter)assetImporter);
        }

        /// <summary>Applies the import settings; true if any changed.</summary>
        public static bool Configure(TextureImporter ti)
        {
            bool changed = ti.textureType != TextureImporterType.Default || ti.sRGBTexture || ti.mipmapEnabled
                || ti.filterMode != FilterMode.Point || ti.wrapMode != TextureWrapMode.Clamp
                || ti.textureCompression != TextureImporterCompression.Uncompressed || ti.npotScale != TextureImporterNPOTScale.None
                || ti.alphaSource != TextureImporterAlphaSource.FromInput || ti.alphaIsTransparency || ti.isReadable;
            ti.textureType = TextureImporterType.Default;
            ti.sRGBTexture = false;
            ti.alphaSource = TextureImporterAlphaSource.FromInput;
            ti.alphaIsTransparency = false;
            ti.mipmapEnabled = false;
            ti.filterMode = FilterMode.Point;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.isReadable = false;
            return changed;
        }
    }
}
