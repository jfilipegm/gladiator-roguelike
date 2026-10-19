using System.Collections.Generic;
using Gladiator.Core;
using Gladiator.Gameplay;
using UnityEditor;
using UnityEngine;

namespace Gladiator.EditorTools
{
    /// <summary>
    /// Upgrade data: UpgradeConfig, the Nobody's GladiatorDef, the M2.4 stat Boons and filler cards. Each asset is created
    /// once with the starting numbers and is designer-owned afterwards (never overwritten). UpgradeCatalog lists every
    /// UpgradeDef automatically, so a new card is just a new asset.
    /// </summary>
    public sealed class UpgradeSetup : AssetPostprocessor
    {
        public const string Dir = "Assets/Data/Upgrades/";
        public const string ConfigPath = "Assets/Data/Config/UpgradeConfig.asset";
        public const string CatalogPath = "Assets/Data/UpgradeCatalog.asset";
        public const string NobodyPath = "Assets/Data/Gladiators/GL_Nobody.asset";

        [MenuItem("Gladiator/Setup/Upgrades/Create Upgrade Data")]
        public static UpgradeCatalog CreateOrUpdate()
        {
            PlayerSetup.EnsureAsset<UpgradeConfig>(ConfigPath);
            if (AssetDatabase.LoadAssetAtPath<GladiatorDef>(NobodyPath) == null)
            {
                var g = PlayerSetup.EnsureAsset<GladiatorDef>(NobodyPath);
                g.id = "nobody";
                g.displayName = "The Nobody";
                g.description = "A nameless pit fighter with a rusty gladius and nothing to lose.";
                EditorUtility.SetDirty(g);
            }

            const UpgradeTag Heavy = UpgradeTag.Heavy, Crit = UpgradeTag.Crit, Move = UpgradeTag.Movement,
                Def = UpgradeTag.Defense, Gold = UpgradeTag.Gold, Xp = UpgradeTag.Xp;
            Boon("UP_BruteForce", "brute_force", "Brute Force", "BF", Heavy, M(StatId.Damage, ModOp.Percent, 0.12f));
            Boon("UP_QuickHands", "quick_hands", "Quick Hands", "QH", UpgradeTag.None, M(StatId.AttackSpeed, ModOp.Percent, 0.10f));
            Boon("UP_LightFeet", "light_feet", "Light Feet", "LF", Move, M(StatId.MoveSpeed, ModOp.Percent, 0.08f));
            Boon("UP_ThickSkin", "thick_skin", "Thick Skin", "TS", Def, M(StatId.MaxHp, ModOp.Flat, 20f));
            Boon("UP_Greedy", "greedy", "Greedy Hands", "GH", Xp, M(StatId.PickupRange, ModOp.Percent, 0.25f));
            Boon("UP_LongReach", "long_reach", "Long Reach", "LR", Heavy, M(StatId.Area, ModOp.Percent, 0.10f));
            Boon("UP_LuckyCut", "lucky_cut", "Lucky Cut", "LC", Crit, M(StatId.CritChance, ModOp.Flat, 0.05f), M(StatId.CritDamage, ModOp.Flat, 0.10f));
            Boon("UP_Fortune", "fortune", "Fortune's Favour", "FF", Gold, M(StatId.Luck, ModOp.Flat, 0.10f));
            Boon("UP_QuickStudy", "quick_study", "Quick Study", "QS", Xp, M(StatId.XpGain, ModOp.Percent, 0.10f));
            Boon("UP_SecondWind", "second_wind", "Second Wind", "SW", Def, M(StatId.HpRegen, ModOp.Flat, 0.4f));
            Boon("UP_IronHide", "iron_hide", "Iron Hide", "IH", Def, M(StatId.Armor, ModOp.Flat, 0.06f));

            var bread = Consumable("UP_BreadAndWater", "bread", "Bread and Water", "BW");
            if (bread != null) { bread.healFraction = 0.3f; EditorUtility.SetDirty(bread); }
            var purse = Consumable("UP_CoinPurse", "coin_purse", "Coin Purse", "CP");
            if (purse != null) { purse.gold = 15; EditorUtility.SetDirty(purse); }

            CreateWeapons();

            AssetDatabase.SaveAssets();
            return Refresh();
        }

        public const string WeaponDir = "Assets/Data/Weapons/";
        const string WeaponModels = "Assets/Art/Models/Weapons/";

        static WeaponDef.Level L(params StatMod[] mods) => new WeaponDef.Level { mods = mods };
        static StatMod Pct(StatId s, float v) => new StatMod(s, ModOp.Percent, v);
        static StatMod Add(StatId s, float v) => new StatMod(s, ModOp.Flat, v);

        /// <summary>The slice's four weapons (CLAUDE.md §6.8: different patterns, not reskins) and their cards.</summary>
        static void CreateWeapons()
        {
            // Gladius exists since M1.2 (designer-owned): only give it a level table if it has none.
            var gladius = AssetDatabase.LoadAssetAtPath<WeaponDef>(CombatSetup.GladiusPath);
            if (gladius != null && (gladius.levels == null || gladius.levels.Length == 0))
            {
                gladius.levels = new[]
                {
                    L(Pct(StatId.Damage, 0.2f)), L(Pct(StatId.Area, 0.15f)), L(Add(StatId.Amount, 1f)), L(Pct(StatId.AttackSpeed, 0.2f)),
                    L(Pct(StatId.Damage, 0.25f)), L(Pct(StatId.Area, 0.15f)), L(Add(StatId.Amount, 1f), Pct(StatId.Damage, 0.2f)),
                };
                EditorUtility.SetDirty(gladius);
            }

            var spear = Weapon("WP_Spear", "spear", "Pit Spear", "Thrusts through every enemy in a line. Long reach.", WeaponPattern.Thrust, w =>
            {
                w.damage = 15f; w.critChance = 0.10f; w.knockback = 7f; w.cooldown = 1.1f; w.swingDuration = 0.24f; w.windup = 0.08f;
                w.range = 4.4f; w.width = 0.7f; w.acquireRange = 4.8f; w.mesh = Mesh("WP_Spear"); w.meshScale = 1f;
                w.levels = new[]
                {
                    L(Pct(StatId.Damage, 0.2f)), L(Pct(StatId.Area, 0.2f)), L(Add(StatId.Amount, 1f)), L(Pct(StatId.AttackSpeed, 0.15f)),
                    L(Pct(StatId.Damage, 0.25f)), L(Add(StatId.CritChance, 0.1f)), L(Add(StatId.Amount, 1f), Pct(StatId.Area, 0.15f)),
                };
            });
            var javelins = Weapon("WP_Javelins", "javelins", "Javelins", "Throws javelins at the nearest enemies. Pierces one.", WeaponPattern.Projectile, w =>
            {
                w.damage = 11f; w.critChance = 0.08f; w.knockback = 4f; w.cooldown = 1.4f; w.swingDuration = 0.25f; w.windup = 0.1f;
                w.range = 16f; w.width = 0.35f; w.acquireRange = 14f; w.amount = 2; w.speed = 20f; w.pierce = 1;
                w.mesh = Mesh("WP_Javelin"); w.meshScale = 1.3f;
                w.levels = new[]
                {
                    L(Add(StatId.Amount, 1f)), L(Pct(StatId.Damage, 0.2f)), L(Add(StatId.Pierce, 1f)), L(Pct(StatId.AttackSpeed, 0.15f)),
                    L(Add(StatId.Amount, 1f)), L(Pct(StatId.Damage, 0.25f)), L(Add(StatId.Pierce, 1f), Pct(StatId.ProjectileSpeed, 0.2f)),
                };
            });
            var chakram = Weapon("WP_Chakram", "chakram", "Chakram", "A bladed disc that flies out and comes back, cutting everything twice.", WeaponPattern.Returning, w =>
            {
                w.damage = 8f; w.critChance = 0.08f; w.knockback = 3f; w.cooldown = 1.7f; w.swingDuration = 0.25f; w.windup = 0.1f;
                w.range = 7f; w.width = 0.55f; w.acquireRange = 9f; w.amount = 1; w.speed = 14f; w.pierce = -1;
                w.mesh = Mesh("WP_Chakram"); w.meshScale = 1.3f;
                w.levels = new[]
                {
                    L(Pct(StatId.Damage, 0.2f)), L(Pct(StatId.Area, 0.2f)), L(Add(StatId.Amount, 1f)), L(Pct(StatId.AttackSpeed, 0.15f)),
                    L(Pct(StatId.ProjectileSpeed, 0.2f)), L(Pct(StatId.Damage, 0.25f)), L(Add(StatId.Amount, 1f)),
                };
            });

            WeaponCard("UP_W_Gladius", gladius, "GL");
            WeaponCard("UP_W_Spear", spear, "SP");
            WeaponCard("UP_W_Javelins", javelins, "JV");
            WeaponCard("UP_W_Chakram", chakram, "CK");
        }

        static Mesh Mesh(string model) => AssetDatabase.LoadAssetAtPath<Mesh>(WeaponModels + model + ".fbx");

        /// <summary>Loads the weapon, or creates it with `init` the first time (designer-owned afterwards).</summary>
        static WeaponDef Weapon(string file, string id, string name, string description, WeaponPattern pattern, System.Action<WeaponDef> init)
        {
            string path = WeaponDir + file + ".asset";
            var w = AssetDatabase.LoadAssetAtPath<WeaponDef>(path);
            if (w != null) return w;
            w = PlayerSetup.EnsureAsset<WeaponDef>(path);
            w.id = id; w.displayName = name; w.description = description; w.pattern = pattern;
            init(w);
            EditorUtility.SetDirty(w);
            return w;
        }

        static void WeaponCard(string file, WeaponDef weapon, string glyph)
        {
            if (weapon == null) return;
            string path = Dir + file + ".asset";
            if (AssetDatabase.LoadAssetAtPath<UpgradeDef>(path) != null) return;
            var d = PlayerSetup.EnsureAsset<UpgradeDef>(path);
            d.id = "weapon_" + weapon.id; d.displayName = weapon.displayName; d.glyph = glyph;
            d.kind = UpgradeKind.Weapon;
            d.weapon = weapon;
            d.maxLevel = weapon.MaxLevel;
            d.weight = 8f;
            EditorUtility.SetDirty(d);
        }

        static StatMod M(StatId s, ModOp op, float v) => new StatMod(s, op, v);

        /// <summary>Creates the Boon if missing (returns null when it already exists: designer-owned).</summary>
        static UpgradeDef Boon(string file, string id, string name, string glyph, UpgradeTag tags, params StatMod[] mods)
        {
            string path = Dir + file + ".asset";
            if (AssetDatabase.LoadAssetAtPath<UpgradeDef>(path) != null) return null;
            var d = PlayerSetup.EnsureAsset<UpgradeDef>(path);
            d.id = id; d.displayName = name; d.glyph = glyph; d.tags = tags;
            d.kind = UpgradeKind.Boon;
            d.maxLevel = 5;
            d.perLevel = mods;
            EditorUtility.SetDirty(d);
            return d;
        }

        static UpgradeDef Consumable(string file, string id, string name, string glyph)
        {
            string path = Dir + file + ".asset";
            if (AssetDatabase.LoadAssetAtPath<UpgradeDef>(path) != null) return null;
            var d = PlayerSetup.EnsureAsset<UpgradeDef>(path);
            d.id = id; d.displayName = name; d.glyph = glyph;
            d.kind = UpgradeKind.Consumable;
            d.maxLevel = 1;
            d.minRarity = d.maxRarity = Rarity.Common;
            d.weight = 0f;
            EditorUtility.SetDirty(d);
            return d;
        }

        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (Touches(imported) || Touches(deleted) || Touches(moved) || Touches(movedFrom)) Refresh();
        }

        static bool Touches(string[] paths)
        {
            foreach (var p in paths)
                if (p.EndsWith(".asset") && p != CatalogPath && (p.StartsWith(Dir) || AssetDatabase.GetMainAssetTypeAtPath(p) == typeof(UpgradeDef)))
                    return true;
            return false;
        }

        /// <summary>Lists every UpgradeDef (sorted by path): consumables as fallbacks, the rest as the pool.</summary>
        [MenuItem("Gladiator/Setup/Upgrades/Refresh Upgrade Catalog")]
        public static UpgradeCatalog Refresh()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<UpgradeCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<UpgradeCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            var paths = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:UpgradeDef")) paths.Add(AssetDatabase.GUIDToAssetPath(guid));
            paths.Sort(System.StringComparer.Ordinal);
            var pool = new List<UpgradeDef>();
            var fallbacks = new List<UpgradeDef>();
            foreach (var p in paths)
            {
                var d = AssetDatabase.LoadAssetAtPath<UpgradeDef>(p);
                if (d == null) continue;
                (d.kind == UpgradeKind.Consumable ? fallbacks : pool).Add(d);
            }
            bool changed = !Same(catalog.upgrades, pool) || !Same(catalog.fallbacks, fallbacks);
            if (changed)
            {
                catalog.upgrades = pool.ToArray();
                catalog.fallbacks = fallbacks.ToArray();
                EditorUtility.SetDirty(catalog);
                AssetDatabase.SaveAssetIfDirty(catalog);
            }
            return catalog;
        }

        static bool Same(UpgradeDef[] a, List<UpgradeDef> b)
        {
            if (a == null || a.Length != b.Count) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }
    }
}
