using System.Collections.Generic;
using System.IO;
using Gladiator.Gameplay;
using UnityEditor;
using UnityEngine;

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
            // created once, designer-owned afterwards: later arenas' entries are appended when missing
            var list = new System.Collections.Generic.List<CodexConfig.Beast>(cfg.bestiary ?? new CodexConfig.Beast[0]);
            foreach (var d in Defaults())
            {
                bool has = false;
                foreach (var x in list) has |= (d.boss != null && x.boss == d.boss) || (d.boss == null && x.boss == null && x.enemy == d.enemy);
                if (!has && (d.enemy != null || d.boss != null)) list.Add(d);
            }
            cfg.bestiary = list.ToArray();
            EditorUtility.SetDirty(cfg);
            BakePortraits(cfg);
            AssetDatabase.SaveAssets();
            return cfg;
        }

        static CodexConfig.Beast[] Defaults() => new[]
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
            Beast("EN_Murmillo", 2, "A door-sized scutum and a fish-crested helm. Nothing gets through his front: go round him."),
            Beast("EN_NetThrower", 2, "Whirls a weighted net and throws it where you stand. Keep moving, or jump: it can't catch you in the air."),
            Beast("EN_WarBoar", 2, "Bred in the pens to charge. It runs over anything in its lane, its own side included: lead it through the horde."),
            Boss("BD_BeastLord", 2, "Master of the pens, more beast than man. He leaps on you from anywhere and his horn opens the cages."),
        };

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
            try
            {
                // the whole body, from the front-left (the rig is shared with the select screens' portraits: PortraitBaker)
                using (var rig = new PortraitBaker.Rig(Cell, Cell))
                {
                    var ids = new string[jobs.Count];
                    for (int i = 0; i < jobs.Count; i++)
                    {
                        ids[i] = jobs[i].id;
                        rig.Render(jobs[i].model, jobs[i].id == "sc:pit_goblin" ? envMat : mat, false);
                        int col = i % Columns, row = i / Columns;
                        rig.CopyTo(atlas, col * Cell, (rows - 1 - row) * Cell);
                    }
                    atlas.Apply();
                    File.WriteAllBytes(PortraitPath, atlas.EncodeToPNG());
                    cfg.portraitIds = ids;
                }
            }
            finally
            {
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
