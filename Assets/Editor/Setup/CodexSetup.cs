using System.Collections.Generic;
using System.IO;
using Gladiator.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Gladiator.EditorTools
{
    /// <summary>
    /// The Codex's data (Phase 5.6, docs/UNLOCKS.md): CodexConfig (the Bestiary's order and blurbs, created once and
    /// designer-owned afterwards) and its portrait atlas, rendered from the models in a preview scene: every Bestiary
    /// enemy, every gladiator and the secrets' finds. A locked or unseen entry shows the same picture in black.
    /// </summary>
    public static class CodexSetup
    {
        public const string ConfigPath = MetaSetup.Dir + "CodexConfig.asset";
        public const string PortraitPath = "Assets/Art/Textures/UI/CodexPortraits.png";
        const string Enemies = "Assets/Data/Enemies/";
        const string Bosses = "Assets/Data/Bosses/";
        const int Cell = 128, Columns = 8;

        /// <summary>The secrets' portraits, by unlock id: what you found.</summary>
        static readonly (string unlock, string model)[] SecretModels =
        {
            ("pit_goblin", GladiatorSetup.CrateModel),
            ("sling", ArenaBuilder.GoldenRatModel),
            ("loaded_dice", ArenaBuilder.BookmakerModel),
        };

        [MenuItem("Gladiator/Setup/Meta/Create Codex Data + Portraits")]
        public static CodexConfig CreateOrUpdate()
        {
            var cfg = AssetDatabase.LoadAssetAtPath<CodexConfig>(ConfigPath);
            if (cfg == null)
            {
                cfg = ScriptableObject.CreateInstance<CodexConfig>();
                AssetDatabase.CreateAsset(cfg, ConfigPath);
            }
            cfg.catalog = AssetDatabase.LoadAssetAtPath<UpgradeCatalog>(UpgradeSetup.CatalogPath);
            cfg.meta = MetaSetup.Load();
            cfg.upgrades = AssetDatabase.LoadAssetAtPath<UpgradeConfig>(UpgradeSetup.ConfigPath);
            if (cfg.bestiary == null || cfg.bestiary.Length == 0)
                cfg.bestiary = new[]
                {
                    Beast("EN_Convict", 0, "Starved, chained and thrown in first. Alone they're nothing; they never come alone."),
                    Beast("EN_PitCur", 0, "A half-wild dog the pit masters keep hungry. It circles, then leaps for your throat."),
                    Beast("EN_ShieldThug", 0, "Hides behind a door torn off its hinges. Hit him from the side or from behind."),
                    Beast("EN_Slinger", 0, "Keeps his distance and pelts you with stones. Close the gap."),
                    Beast("EN_Brute", 0, "Twice your size and slow to fall. When he winds up, get out of the way."),
                    Beast("EN_FirePot", 0, "Lobs burning pots that set the sand alight. Don't stand where they land."),
                    Boss("BD_Gorm", 0, "The Pit Master. A flail, a whip, guards on call, and a ring of fire for whoever lasts."),
                    Beast("EN_ShieldArcher", 1, "Plants a pavise and looses arrows over it. Strike while he reloads."),
                    Beast("EN_Assassin", 1, "Flanks you where you aren't looking and dashes down a line. Watch the lane."),
                    Boss("BD_Champion", 1, "The Provinces' pride: a fortress shield that wrecks barricades. Make him charge the stone."),
                };
            EditorUtility.SetDirty(cfg);
            BakePortraits(cfg);
            AssetDatabase.SaveAssets();
            return cfg;
        }

        public static CodexConfig Load() => AssetDatabase.LoadAssetAtPath<CodexConfig>(ConfigPath);

        static CodexConfig.Beast Beast(string file, int tier, string blurb) =>
            new CodexConfig.Beast { enemy = AssetDatabase.LoadAssetAtPath<EnemyDef>(Enemies + file + ".asset"), tier = tier, blurb = blurb };

        static CodexConfig.Beast Boss(string file, int tier, string blurb)
        {
            var b = AssetDatabase.LoadAssetAtPath<BossDef>(Bosses + file + ".asset");
            return new CodexConfig.Beast { boss = b, enemy = b != null ? b.enemy : null, tier = tier, blurb = blurb };
        }

        /// <summary>Renders every portrait into one atlas (8 columns of 128 px) and points the config at it.</summary>
        [MenuItem("Gladiator/Setup/Meta/Bake Codex Portraits")]
        public static void BakePortraits() => BakePortraits(Load());

        static void BakePortraits(CodexConfig cfg)
        {
            if (cfg == null) return;
            var jobs = new List<(string id, GameObject model)>();
            foreach (var b in cfg.bestiary)
                if (b.enemy != null && b.enemy.model != null) jobs.Add(("en:" + b.enemy.id, b.enemy.model));
            foreach (var g in GladiatorSetup.All())
                if (g != null && g.model != null) jobs.Add(("gl:" + g.id, g.model));
            foreach (var (unlock, path) in SecretModels)
            {
                var m = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (m != null) jobs.Add(("sc:" + unlock, m));
            }
            int rows = Mathf.CeilToInt(jobs.Count / (float)Columns);
            var atlas = new Texture2D(Columns * Cell, rows * Cell, TextureFormat.RGBA32, false);
            atlas.SetPixels32(new Color32[atlas.width * atlas.height]); // transparent

            var mat = AssetDatabase.LoadAssetAtPath<Material>(LookSetup.CharMat);
            var envMat = AssetDatabase.LoadAssetAtPath<Material>(LookSetup.EnvMat);
            var scene = EditorSceneManager.NewPreviewScene();
            var rt = RenderTexture.GetTemporary(Cell, Cell, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            try
            {
                var camGo = new GameObject("PortraitCam");
                SceneManager.MoveGameObjectToScene(camGo, scene);
                var cam = camGo.AddComponent<Camera>();
                cam.scene = scene;
                cam.cameraType = CameraType.Preview;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
                cam.orthographic = true;
                cam.nearClipPlane = 0.05f;
                cam.farClipPlane = 50f;
                cam.allowHDR = false;
                cam.allowMSAA = false;
                cam.targetTexture = rt;
                var data = camGo.AddComponent<UniversalAdditionalCameraData>();
                data.renderPostProcessing = false;
                data.renderShadows = false;
                var key = new GameObject("Key").AddComponent<Light>();
                SceneManager.MoveGameObjectToScene(key.gameObject, scene);
                key.type = LightType.Directional;
                key.intensity = 1.3f;
                key.color = new Color(1f, 0.96f, 0.9f);
                key.transform.rotation = Quaternion.Euler(40f, 150f, 0f);

                var ids = new string[jobs.Count];
                for (int i = 0; i < jobs.Count; i++)
                {
                    ids[i] = jobs[i].id;
                    var inst = Object.Instantiate(jobs[i].model);
                    SceneManager.MoveGameObjectToScene(inst, scene);
                    inst.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                    var rends = inst.GetComponentsInChildren<Renderer>(true);
                    bool prop = jobs[i].id == "sc:pit_goblin";
                    foreach (var r in rends) r.sharedMaterial = prop ? envMat : mat;
                    var bounds = new Bounds(inst.transform.position + Vector3.up, Vector3.zero);
                    bool first = true;
                    foreach (var r in rends)
                    {
                        if (first) { bounds = r.bounds; first = false; }
                        else bounds.Encapsulate(r.bounds);
                    }
                    // a three-quarter view from the front-left, a little above
                    var rot = Quaternion.Euler(12f, 150f, 0f);
                    cam.transform.rotation = rot;
                    cam.transform.position = bounds.center - rot * Vector3.forward * (bounds.extents.magnitude + 5f);
                    cam.orthographicSize = Mathf.Max(bounds.extents.y, Mathf.Max(bounds.extents.x, bounds.extents.z)) * 1.12f;
                    cam.Render();
                    var prev = RenderTexture.active;
                    RenderTexture.active = rt;
                    int col = i % Columns, row = i / Columns;
                    atlas.ReadPixels(new Rect(0, 0, Cell, Cell), col * Cell, (rows - 1 - row) * Cell);
                    RenderTexture.active = prev;
                    Object.DestroyImmediate(inst);
                }
                atlas.Apply();
                File.WriteAllBytes(PortraitPath, atlas.EncodeToPNG());
                cfg.portraitIds = ids;
            }
            finally
            {
                RenderTexture.ReleaseTemporary(rt);
                EditorSceneManager.ClosePreviewScene(scene);
                Object.DestroyImmediate(atlas);
            }
            AssetDatabase.ImportAsset(PortraitPath);
            var imp = (TextureImporter)AssetImporter.GetAtPath(PortraitPath);
            imp.textureType = TextureImporterType.Default;
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = false;
            imp.filterMode = FilterMode.Bilinear;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.npotScale = TextureImporterNPOTScale.None;
            imp.SaveAndReimport();
            cfg.portraits = AssetDatabase.LoadAssetAtPath<Texture2D>(PortraitPath);
            cfg.portraitCell = Cell;
            cfg.portraitColumns = Columns;
            EditorUtility.SetDirty(cfg);
        }
    }
}
