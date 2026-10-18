using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Gladiator.EditorTools
{
    /// <summary>
    /// Shared look assets, all created from code: palette materials (one per shader role), sky material,
    /// the post-processing profile, and URP/player settings for the target look (CLAUDE.md §4, §5, §7.1).
    /// Idempotent: re-running updates the existing assets in place (GUIDs stay stable).
    /// </summary>
    public static class LookSetup
    {
        public const string MatDir = "Assets/Art/Materials";
        public const string PalettePath = "Assets/Art/Textures/Palette/PAL_Master.png";
        public const string EnvMat = MatDir + "/M_PaletteLit_Env.mat";
        public const string CharMat = MatDir + "/M_PaletteLit_Char.mat";
        public const string FlashMat = MatDir + "/M_PaletteLit_Flash.mat";
        public const string SkyMat = MatDir + "/M_Sky.mat";
        public const string VolumeProfilePath = "Assets/Settings/VP_Gladiator.asset";

        [MenuItem("Gladiator/Setup/Look/Create Or Update Look Assets")]
        public static string CreateOrUpdateAll()
        {
            EnsureMaterials();
            EnsureVolumeProfile();
            ConfigurePipeline();
            AssetDatabase.SaveAssets();
            return "look assets ok";
        }

        public static void EnsureMaterials()
        {
            System.IO.Directory.CreateDirectory(MatDir);
            var shader = Shader.Find("Gladiator/PaletteLit");
            var palette = AssetDatabase.LoadAssetAtPath<Texture2D>(PalettePath);

            var env = LoadOrCreate(EnvMat, shader);
            env.SetTexture("_BaseMap", palette);
            env.SetFloat("_PixelNoise", 0.07f);
            env.SetFloat("_PixelDensity", 6f);
            env.SetFloat("_NoiseWorldSpace", 1f);
            env.SetFloat("_RimStrength", 0f);
            env.SetFloat("_Wrap", 0.35f);

            var ch = LoadOrCreate(CharMat, shader);
            ch.SetTexture("_BaseMap", palette);
            ch.SetFloat("_PixelNoise", 0.03f);
            ch.SetFloat("_PixelDensity", 14f);
            ch.SetFloat("_NoiseWorldSpace", 0f);
            ch.SetFloat("_RimStrength", 0.28f);
            ch.SetFloat("_RimPower", 3f);
            ch.SetFloat("_Wrap", 0.45f);

            var flash = LoadOrCreate(FlashMat, shader);
            flash.CopyPropertiesFromMaterial(ch);
            flash.SetFloat("_Flash", 1f);
            flash.SetColor("_FlashColor", Color.white);

            foreach (var m in new[] { env, ch, flash })
            {
                m.enableInstancing = true;
                EditorUtility.SetDirty(m);
            }

            var sky = LoadOrCreate(SkyMat, Shader.Find("Gladiator/SkyGradient"));
            sky.SetColor("_ZenithColor", new Color(0.18f, 0.44f, 0.86f));
            sky.SetColor("_HorizonColor", new Color(0.70f, 0.86f, 0.97f));
            sky.SetColor("_GroundColor", new Color(0.45f, 0.42f, 0.40f));
            EditorUtility.SetDirty(sky);
        }

        static Material LoadOrCreate(string path, Shader shader)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            else if (m.shader != shader) m.shader = shader;
            return m;
        }

        public static VolumeProfile EnsureVolumeProfile()
        {
            var p = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumeProfilePath);
            if (p == null)
            {
                p = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(p, VolumeProfilePath);
            }

            var bloom = Get<Bloom>(p);
            bloom.threshold.Override(1.0f);
            bloom.intensity.Override(0.55f);
            bloom.scatter.Override(0.6f);

            var tm = Get<Tonemapping>(p);
            tm.mode.Override(TonemappingMode.Neutral);

            var ca = Get<ColorAdjustments>(p);
            ca.postExposure.Override(0f);
            ca.contrast.Override(8f);
            ca.saturation.Override(6f);

            var vig = Get<Vignette>(p);
            vig.intensity.Override(0.22f);
            vig.smoothness.Override(0.45f);

            var wb = Get<WhiteBalance>(p);
            wb.temperature.Override(3f);

            EditorUtility.SetDirty(p);
            return p;
        }

        static T Get<T>(VolumeProfile p) where T : VolumeComponent
        {
            if (!p.TryGet<T>(out var c))
            {
                c = p.Add<T>(true);
                c.name = typeof(T).Name;
                AssetDatabase.AddObjectToAsset(c, p);
            }
            c.active = true;
            return c;
        }

        /// <summary>Short-range shadows, 4x MSAA, SSAO off (not part of the look; saves GPU), static batching on.</summary>
        public static void ConfigurePipeline()
        {
            var asset = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
            if (asset != null)
            {
                asset.shadowDistance = 40f;
                asset.shadowCascadeCount = 2;
                asset.msaaSampleCount = 4;
                asset.supportsHDR = true;
                EditorUtility.SetDirty(asset);

                var rendererDataList = (ScriptableRendererData[])typeof(UniversalRenderPipelineAsset)
                    .GetField("m_RendererDataList", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(asset);
                if (rendererDataList != null)
                    foreach (var rd in rendererDataList)
                    {
                        if (rd == null) continue;
                        foreach (var f in rd.rendererFeatures)
                            if (f != null && f.GetType().Name == "ScreenSpaceAmbientOcclusion") f.SetActive(false);
                        EditorUtility.SetDirty(rd);
                    }
            }
            PlayerSettings.SetStaticBatchingForPlatform(BuildTarget.StandaloneLinux64, true);
            PlayerSettings.SetStaticBatchingForPlatform(BuildTarget.StandaloneWindows64, true);
            PlayerSettings.gpuSkinning = true;
        }

        /// <summary>Assigns one shared material to every renderer under root (FBX imports have no materials).</summary>
        public static void AssignMaterial(GameObject root, Material mat)
        {
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                if (mats.Length == 0) mats = new[] { mat };
                r.sharedMaterials = mats;
            }
        }
    }
}
