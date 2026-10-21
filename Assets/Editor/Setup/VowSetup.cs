using Gladiator.Core;
using Gladiator.Gameplay;
using UnityEditor;
using UnityEngine;

namespace Gladiator.EditorTools
{
    /// <summary>
    /// P1 (finding 11): the Vows, trade-off Boons sworn with a price. Each gives about 2.5-3 Boons' worth of one thing
    /// (perLevel, scaled by rarity like any card) for a downside that always bites (drawbacks, scaled by the gentler
    /// UpgradeConfig.vowDrawbackMagnitude, so a Legendary Vow gives far more for a little more). Downsides are
    /// multiplicative where they could otherwise push a stat to zero, and never on a stat that is clamped at zero for
    /// most gladiators (armor, regen), where they'd cost nothing. They take a Boon slot and level to 3.
    /// Like UpgradeSetup, each asset is created once and is designer-owned afterwards; "Reset Vows" rewrites them.
    /// </summary>
    public static class VowSetup
    {
        const string Dir = UpgradeSetup.Dir;
        public const int VowCount = 8;

        [MenuItem("Gladiator/Setup/Upgrades/Create Vows")]
        public static int CreateOrUpdate() => Make(false);

        [MenuItem("Gladiator/Setup/Upgrades/Reset Vows To Defaults")]
        public static int ResetToDefaults() => Make(true);

        static int Make(bool overwrite)
        {
            const UpgradeTag Blood = UpgradeTag.Blood, Def = UpgradeTag.Defense, Heavy = UpgradeTag.Heavy, Crit = UpgradeTag.Crit,
                Move = UpgradeTag.Movement, Gold = UpgradeTag.Gold, Xp = UpgradeTag.Xp;
            int made = 0;
            made += Vow("UP_V_Berserker", "vow_berserker", "Berserker's Vow", "BV", Blood, overwrite,
                Up(M(StatId.Damage, ModOp.Percent, 0.35f)), Price(M(StatId.MaxHp, ModOp.Multiply, 0.8f)));
            made += Vow("UP_V_HeavyPlate", "vow_heavy_plate", "Heavy Plate", "HP", Def | Heavy, overwrite,
                Up(M(StatId.Armor, ModOp.Flat, 0.15f)), Price(M(StatId.MoveSpeed, ModOp.Multiply, 0.88f)));
            made += Vow("UP_V_GlassEdge", "vow_glass_edge", "Glass Edge", "GE", Crit, overwrite,
                Up(M(StatId.CritChance, ModOp.Flat, 0.10f), M(StatId.CritDamage, ModOp.Flat, 0.40f)), Price(M(StatId.MaxHp, ModOp.Multiply, 0.85f)));
            made += Vow("UP_V_Featherweight", "vow_featherweight", "Featherweight", "FW", Move, overwrite,
                Up(M(StatId.MoveSpeed, ModOp.Percent, 0.20f)), Price(M(StatId.Damage, ModOp.Multiply, 0.9f)));
            made += Vow("UP_V_Greed", "vow_greed", "Greed's Vow", "GV", Gold, overwrite,
                Up(M(StatId.GoldGain, ModOp.Percent, 0.40f)), Price(M(StatId.XpGain, ModOp.Multiply, 0.85f)));
            made += Vow("UP_V_TitansGrip", "vow_titans_grip", "Titan's Grip", "TG", Heavy, overwrite,
                Up(M(StatId.Area, ModOp.Percent, 0.25f)), Price(M(StatId.AttackSpeed, ModOp.Multiply, 0.85f)));
            made += Vow("UP_V_Scholar", "vow_scholar", "Scholar's Vow", "SV", Xp, overwrite,
                Up(M(StatId.XpGain, ModOp.Percent, 0.30f)), Price(M(StatId.Damage, ModOp.Multiply, 0.88f)));
            made += Vow("UP_V_Gambler", "vow_gambler", "Gambler's Vow", "GA", Gold, overwrite,
                Up(M(StatId.Luck, ModOp.Flat, 0.20f)), Price(M(StatId.Rerolls, ModOp.Flat, -1f)));
            AssetDatabase.SaveAssets();
            IconSetup.Apply(); // their pixel icons (PixelSrc/upgrade_icons.py rows "vow_*")
            UpgradeSetup.Refresh();
            Debug.Log($"[VowSetup] {made} of {VowCount} Vows {(overwrite ? "reset" : "created")}");
            return made;
        }

        static StatMod M(StatId s, ModOp op, float v) => new StatMod(s, op, v);
        static StatMod[] Up(params StatMod[] m) => m;
        static StatMod[] Price(params StatMod[] m) => m;

        /// <summary>Creates the Vow if missing (or rewrites it with `overwrite`). Returns 1 if it wrote the asset.</summary>
        static int Vow(string file, string id, string name, string glyph, UpgradeTag tags, bool overwrite, StatMod[] upside, StatMod[] drawbacks)
        {
            string path = Dir + file + ".asset";
            if (!overwrite && AssetDatabase.LoadAssetAtPath<UpgradeDef>(path) != null) return 0;
            var d = PlayerSetup.EnsureAsset<UpgradeDef>(path);
            d.id = id;
            d.displayName = name;
            d.glyph = glyph;
            d.tags = tags;
            d.kind = UpgradeKind.Boon;
            d.vow = true;
            d.weight = 10f; // only splits UpgradeConfig.vowShare between the Vows
            d.maxLevel = 3;
            d.minRarity = Rarity.Common;
            d.maxRarity = Rarity.Legendary;
            d.perLevel = upside;
            d.drawbacks = drawbacks;
            d.effects = System.Array.Empty<BoonEffect>();
            EditorUtility.SetDirty(d);
            return 1;
        }
    }
}
