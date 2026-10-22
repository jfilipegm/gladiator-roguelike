using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Gladiator.Gameplay;
using UnityEditor;
using UnityEngine;

namespace Gladiator.EditorTools
{
    /// <summary>
    /// R6 Skins (docs/tracks/R7-design.md §4): reads the one skin table, BlenderSrc/characters/skins.json (R6-art owns it;
    /// Blender exports the remapped models from it), and per skin creates or updates its SkinDef
    /// (Assets/Data/Skins/SK_&lt;Base without CH_&gt;&lt;Suffix&gt;: the gladiator by id, the model
    /// Characters/Skins/&lt;base&gt;&lt;suffix&gt;.fbx, three chip colours from palette.json by swatch name) and its unlock
    /// (MetaSetup.Dir + UL_Skin_&lt;Base&gt;_&lt;Suffix&gt;: Market for `price` Renown, or the champion challenge "win 3 runs
    /// with him"; neither = free). A skin whose FBX isn't there yet, or whose row breaks a rule, logs an error and is
    /// skipped. Then MetaConfig.skins in the table's order and Create Meta Data (collects the unlocks). Creates or updates:
    /// running it twice changes nothing. Portraits: Gladiator/Setup/UI/Bake Portraits (or Bake Skin Portraits) afterwards.
    /// </summary>
    public static class SkinSetup
    {
        public const string TablePath = "BlenderSrc/characters/skins.json";
        public const string PalettePath = "Assets/Art/Textures/Palette/palette.json";
        public const string Dir = "Assets/Data/Skins/";
        public const string ModelDir = "Assets/Art/Models/Characters/Skins/";
        /// <summary>Wins with the gladiator a champion skin asks for (spec §4.4).</summary>
        public const int ChampionWins = 3;

        // ---------------------------------------------------------------- the table (JsonUtility: no dictionaries)

        [Serializable]
        public sealed class SkinTable
        {
            public SkinRow[] skins = Array.Empty<SkinRow>();
        }

        [Serializable]
        public sealed class SkinRow
        {
            public string id, gladiator, script;
            /// <summary>The base model's name, "CH_Nobody" ("base" in the JSON).</summary>
            public string @base;
            public string name, suffix, description;
            public string[] chips = Array.Empty<string>();
            public int price;
            public bool champion;
            public SkinRemap[] remap = Array.Empty<SkinRemap>();
        }

        [Serializable]
        public sealed class SkinRemap
        {
            public string from, to;
        }

        [MenuItem("Gladiator/Setup/Gladiators/Create Skin Data")]
        public static MetaConfig CreateOrUpdate()
        {
            var cfg = PlayerSetup.EnsureAsset<MetaConfig>(MetaSetup.ConfigPath);
            if (!File.Exists(TablePath))
            {
                Debug.LogError("[SkinSetup] no skin table at " + TablePath + " (R6-art writes it)");
                return cfg;
            }
            var table = Parse(File.ReadAllText(TablePath));
            var palette = File.Exists(PalettePath) ? Palette(File.ReadAllText(PalettePath)) : new Dictionary<string, Swatch>();
            if (palette.Count == 0) Debug.LogError("[SkinSetup] no palette swatches read from " + PalettePath + ": the chips will be grey");
            if (!AssetDatabase.IsValidFolder(Dir.TrimEnd('/'))) AssetDatabase.CreateFolder("Assets/Data", "Skins");

            var roster = GladiatorSetup.All();
            var skins = new List<SkinDef>();
            var ids = new HashSet<string>();
            int market = 0, champions = 0, free = 0, skipped = 0;
            foreach (var row in table.skins)
            {
                var g = Check(row, roster, ids);
                if (g == null) { skipped++; continue; }
                string modelPath = ModelDir + row.@base + row.suffix + ".fbx";
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
                if (model == null)
                {
                    Debug.LogError($"[SkinSetup] {row.id}: no model at {modelPath} (Tools/bheadless BlenderSrc/characters/build_skins.py skin={row.id}): skipped");
                    skipped++;
                    continue;
                }
                string key = StripCh(row.@base);
                var s = PlayerSetup.EnsureAsset<SkinDef>(Dir + "SK_" + key + row.suffix + ".asset");
                s.id = row.id;
                s.displayName = row.name;
                s.description = row.description ?? "";
                s.gladiator = g;
                s.model = model;
                s.chips = Chips(row, palette);
                s.unlock = Unlock(row, g, key);
                if (s.portrait == null) s.portrait = AssetDatabase.LoadAssetAtPath<Sprite>(PortraitBaker.SkinPortraitPath(s)); // baked before
                EditorUtility.SetDirty(s);
                skins.Add(s);
                if (s.unlock == null) free++;
                else if (s.unlock.category == UnlockCategory.Market) market++;
                else champions++;
            }

            cfg.skins = skins.ToArray();
            EditorUtility.SetDirty(cfg);
            AssetDatabase.SaveAssets();
            cfg = MetaSetup.CreateOrUpdate(); // collects the skins' unlocks, saves
            string msg = $"[SkinSetup] {skins.Count} skins ({market} Market, {champions} champion, {free} free), {skipped} skipped; " +
                         $"{cfg.unlocks.Length} unlocks in MetaConfig";
            if (skipped > 0) Debug.LogWarning(msg);
            else Debug.Log(msg);
            return cfg;
        }

        public static SkinTable Parse(string json)
        {
            var t = string.IsNullOrWhiteSpace(json) ? null : JsonUtility.FromJson<SkinTable>(json);
            t ??= new SkinTable();
            t.skins ??= Array.Empty<SkinRow>();
            return t;
        }

        /// <summary>The row's gladiator, or null (logged) when the row breaks a rule: no id, base or suffix, an unknown
        /// gladiator, a repeated id, or an id that isn't "&lt;gladiator id&gt;_…" for exactly its gladiator (MetaProgress
        /// replaces a gladiator's equipped skin by that prefix).</summary>
        static GladiatorDef Check(SkinRow row, GladiatorDef[] roster, HashSet<string> ids)
        {
            if (row == null) return null;
            if (string.IsNullOrEmpty(row.id) || string.IsNullOrEmpty(row.@base) || string.IsNullOrEmpty(row.suffix))
            {
                Debug.LogError($"[SkinSetup] a row with id '{row.id}', base '{row.@base}', suffix '{row.suffix}': all three are needed. Skipped");
                return null;
            }
            if (!ids.Add(row.id))
            {
                Debug.LogError("[SkinSetup] " + row.id + " is in the table twice: the second one is skipped");
                return null;
            }
            GladiatorDef g = null;
            int claims = 0;
            foreach (var r in roster)
            {
                if (r == null) continue;
                if (r.id == row.gladiator) g = r;
                if (SkinDef.IdBelongsTo(row.id, r.id)) claims++;
            }
            if (g == null)
            {
                Debug.LogError($"[SkinSetup] {row.id}: no gladiator with id '{row.gladiator}'. Skipped");
                return null;
            }
            if (!SkinDef.IdBelongsTo(row.id, g.id) || claims != 1)
            {
                Debug.LogError($"[SkinSetup] {row.id}: a skin id is '{g.id}_<name>' and no other gladiator's id may prefix it. Skipped");
                return null;
            }
            if (row.price > 0 && row.champion) Debug.LogWarning($"[SkinSetup] {row.id}: both a price and champion: sold in the Market");
            return g;
        }

        /// <summary>The skin's unlock: Market (price &gt; 0), the champion challenge, or none (free). Created or updated in
        /// MetaSetup.Dir; texts in docs/UNLOCKS.md's style.</summary>
        static UnlockDef Unlock(SkinRow row, GladiatorDef g, string key)
        {
            string path = MetaSetup.Dir + "UL_Skin_" + key + "_" + row.suffix.TrimStart('_') + ".asset";
            if (row.price <= 0 && !row.champion)
            {
                if (AssetDatabase.LoadAssetAtPath<UnlockDef>(path) != null)
                    Debug.LogWarning($"[SkinSetup] {row.id} is free now, but {path} is still there (the Codex lists it): delete it by hand");
                return null;
            }
            var u = PlayerSetup.EnsureAsset<UnlockDef>(path);
            string who = Who(g), look = Sentence(row.description);
            u.id = "skin_" + row.id;
            u.displayName = "Skin: " + row.name;
            u.addsToPool = Array.Empty<UpgradeDef>(); // the reward is the skin that points at it (CodexPanel.Reward)
            u.renownBounty = 0;
            if (row.price > 0)
            {
                u.category = UnlockCategory.Market;
                u.renownCost = row.price;
                u.conditions = Array.Empty<UnlockCondition>();
                u.description = $"A new look for {who}: {row.name}.{look} Wear it from the select screen.";
                u.hint = "For sale in the Market.";
            }
            else
            {
                u.category = UnlockCategory.Challenge;
                u.renownCost = 0;
                u.conditions = new[] { new UnlockCondition { kind = UnlockConditionKind.WinWithGladiator, id = g.id, amount = ChampionWins } };
                u.description = $"{ChampionWins} wins as {who} earn a new look: {row.name}.{look} Wear it from the select screen.";
                u.hint = $"Win {ChampionWins} runs as {who}.";
            }
            EditorUtility.SetDirty(u);
            return u;
        }

        /// <summary>"The Nobody" → "the Nobody" in a sentence; "Asterion, the Chained" stays.</summary>
        static string Who(GladiatorDef g) =>
            g.displayName.StartsWith("The ", StringComparison.Ordinal) ? "the " + g.displayName.Substring(4) : g.displayName;

        /// <summary>" Bleached tunic, clean bronze." from "bleached tunic, clean bronze" (empty stays empty).</summary>
        static string Sentence(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            text = text.Trim();
            if (char.IsLower(text[0])) text = char.ToUpperInvariant(text[0]) + text.Substring(1);
            char last = text[text.Length - 1];
            return " " + text + (last == '.' || last == '!' || last == '?' ? "" : ".");
        }

        static string StripCh(string baseName) => baseName.StartsWith("CH_", StringComparison.Ordinal) ? baseName.Substring(3) : baseName;

        // ---------------------------------------------------------------- the palette's swatches (chips)

        public struct Swatch
        {
            public string Family;
            public Color Color;
        }

        static readonly Regex RowRx = new Regex("\\{\\s*\"family\"\\s*:\\s*\"([^\"]+)\"\\s*,\\s*\"swatches\"\\s*:\\s*\\[(.*?)\\]\\s*\\}",
            RegexOptions.Singleline);
        static readonly Regex SwatchRx = new Regex("\\[\\s*\"([A-Za-z0-9_]+)\"\\s*,\\s*\"(#[0-9A-Fa-f]{6})\"");

        /// <summary>palette.json's swatches by name (a swatch is ["name", "#RRGGBB"(, emission)]: mixed arrays, which
        /// JsonUtility can't read, so a regex does).</summary>
        public static Dictionary<string, Swatch> Palette(string json)
        {
            var map = new Dictionary<string, Swatch>();
            if (string.IsNullOrEmpty(json)) return map;
            foreach (Match row in RowRx.Matches(json))
                foreach (Match sw in SwatchRx.Matches(row.Groups[2].Value))
                    if (ColorUtility.TryParseHtmlString(sw.Groups[2].Value, out var c))
                        map[sw.Groups[1].Value] = new Swatch { Family = row.Groups[1].Value, Color = c };
            return map;
        }

        /// <summary>The three chips' colours (a missing or unknown swatch is grey, and logged). Warns when a chip breaks the
        /// colour language (spec §4.2: no hazard or enemy-accent colour, no glow, as a main colour).</summary>
        static Color[] Chips(SkinRow row, Dictionary<string, Swatch> palette)
        {
            var chips = new Color[3];
            for (int i = 0; i < chips.Length; i++)
            {
                string name = row.chips != null && i < row.chips.Length ? row.chips[i] : null;
                if (name != null && palette.TryGetValue(name, out var sw))
                {
                    chips[i] = sw.Color;
                    if (sw.Family == "hazard" || sw.Family == "enemy_accent" || name.StartsWith("glow_", StringComparison.Ordinal))
                        Debug.LogWarning($"[SkinSetup] {row.id}: chip '{name}' is a {sw.Family} swatch (orange-red and glows aren't a skin's main colours)");
                }
                else
                {
                    chips[i] = Color.grey;
                    Debug.LogError($"[SkinSetup] {row.id}: chip {i + 1} '{name}' isn't a palette swatch: grey");
                }
            }
            return chips;
        }
    }
}
