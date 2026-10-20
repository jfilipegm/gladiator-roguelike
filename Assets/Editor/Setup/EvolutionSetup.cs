using Gladiator.Core;
using Gladiator.Gameplay;
using UnityEditor;
using UnityEngine;

namespace Gladiator.EditorTools
{
    /// <summary>
    /// Phase 5.5 data (docs/EVOLUTIONS.md): tags on the weapon cards, the 7 evolved weapons + their evolution cards, and
    /// the 6 tag synergies. Like UpgradeSetup, each asset is created once with the starting numbers and is designer-owned
    /// afterwards (never overwritten); weapon-card tags are only set while a card has none.
    /// </summary>
    public static class EvolutionSetup
    {
        const string Up = UpgradeSetup.Dir, Wp = UpgradeSetup.WeaponDir, Sy = UpgradeSetup.SynergyDir, St = UpgradeSetup.StatusDir;
        const UpgradeTag Fire = UpgradeTag.Fire, Bolt = UpgradeTag.Lightning, Blood = UpgradeTag.Blood, Crit = UpgradeTag.Crit,
            Heavy = UpgradeTag.Heavy, Def = UpgradeTag.Defense;

        [MenuItem("Gladiator/Setup/Upgrades/Create Evolutions and Synergies")]
        public static void CreateOrUpdate()
        {
            Tag("UP_W_Gladius", Crit);
            Tag("UP_W_Spear", Heavy);
            Tag("UP_W_BombSatchel", Fire | Heavy);
            Tag("UP_W_GormsFlail", Heavy);
            Tag("UP_W_CrimsonGreatsword", Blood | Heavy);

            var burn = Status("SE_Burn");
            var bleed = Status("SE_Bleed");
            var shock = Status("SE_Shock");
            var slow = Status("SE_Slow");
            var root = Status("SE_Root");

            Evolution("Gladius", "LuckyCut", "champions_gladius", "Champion's Gladius", Crit,
                "Every arc is answered by a second one behind you.", w =>
                {
                    w.damage *= 1.25f; w.critChance += 0.10f;
                    w.echoDelay = 0.12f; w.echoAngle = 180f; w.echoScale = 1f;
                });
            Evolution("Spear", "LongReach", "impaler", "Impaler", Heavy,
                "Half again as long. Whatever it runs through is pinned to the sand.", w =>
                {
                    w.damage *= 1.2f; w.range *= 1.5f; w.width *= 1.2f; w.acquireRange *= 1.4f;
                    w.onHitStatus = root; w.onHitStatusPower = 0f; w.onHitStacks = 1;
                });
            Evolution("Javelins", "StaticCharge", "thunderbolts", "Thunderbolts", Bolt,
                "Every javelin that lands arcs lightning to three more.", w =>
                {
                    w.damage *= 1.2f;
                    w.hitEffects = new[] { new BoonEffect { kind = EffectKind.ChainSpark, status = shock, power = 8f, everyNth = 1, count = 3, radius = 6f } };
                });
            Evolution("Chakram", "QuickHands", "twin_moons", "Twin Moons", UpgradeTag.None,
                "A second disc follows the first, on the other side.", w =>
                {
                    w.damage *= 1.2f;
                    w.echoDelay = 0.25f; w.echoAngle = 180f; w.echoScale = 1f;
                });
            Evolution("BombSatchel", "Kindling", "fire_pot_barrage", "Fire-Pot Barrage", Fire | Heavy,
                "Every blast leaves the sand burning.", w =>
                {
                    w.damage *= 1.2f;
                    w.hitEffects = new[] { new BoonEffect { kind = EffectKind.GroundOnHit, status = burn, power = 4f, radius = 2.4f, duration = 3f, stacks = 1 } };
                });
            Evolution("GormsFlail", "BruteForce", "chain_of_ruin", "Chain of Ruin", Heavy,
                "One more ball, bigger, and every hit drags them down.", w =>
                {
                    w.damage *= 1.2f; w.amount += 1; w.width *= 1.3f; w.meshScale *= 1.3f;
                    w.hitEffects = new[] { new BoonEffect { kind = EffectKind.StatusOnHit, status = slow, chance = 1f, power = 0f, stacks = 1 } };
                });
            Evolution("CrimsonGreatsword", "OpenWounds", "sanguine_reaver", "Sanguine Reaver", Blood | Heavy,
                "A wider cleave of blood follows every swing. Two Bleeds a hit.", w =>
                {
                    w.damage *= 1.15f; w.onHitStacks = 2;
                    w.echoDelay = 0.25f; w.echoAngle = 0f; w.echoScale = 1.4f;
                });

            Synergy("SY_Wildfire", "wildfire", "Wildfire", "FIRE SYNERGY: WILDFIRE!", Fire,
                "Every second, each burning enemy sets a neighbour alight.",
                effects: new[] { new BoonEffect { kind = EffectKind.StatusSpread, status = burn, radius = 2.5f, duration = 1f, count = 1 } });
            Synergy("SY_Bloodbath", "bloodbath", "Bloodbath", "BLOOD SYNERGY: BLOODBATH!", Blood,
                "A bleeding enemy that dies sprays 2 Bleed on everything around it.",
                effects: new[] { new BoonEffect { kind = EffectKind.BurstOnDeath, status = bleed, power = 3f, radius = 2.5f, stacks = 2 } });
            Synergy("SY_StormFront", "storm_front", "Storm Front", "LIGHTNING SYNERGY: STORM FRONT!", Bolt,
                "Every 4th hit forks lightning to 3 enemies.",
                effects: new[] { new BoonEffect { kind = EffectKind.ChainSpark, status = shock, power = 6f, everyNth = 4, count = 3, radius = 6f } });
            Synergy("SY_Executioner", "executioner", "Executioner", "CRIT SYNERGY: EXECUTIONER!", Crit,
                "A crit on an enemy below 20% HP kills it outright (not bosses).",
                effects: new[] { new BoonEffect { kind = EffectKind.ExecuteOnCrit, bonus = 0.2f } });
            Synergy("SY_Juggernaut", "juggernaut", "Juggernaut", "HEAVY SYNERGY: JUGGERNAUT!", Heavy,
                "+20% area, +40% knockback, +10% damage.",
                mods: new[] { M(StatId.Area, ModOp.Percent, 0.2f), M(StatId.Knockback, ModOp.Percent, 0.4f), M(StatId.Damage, ModOp.Percent, 0.1f) });
            Synergy("SY_Bulwark", "bulwark", "Bulwark", "DEFENSE SYNERGY: BULWARK!", Def,
                "+8% armor, +15% max HP, +0.5 HP/s.",
                mods: new[] { M(StatId.Armor, ModOp.Flat, 0.08f), M(StatId.MaxHp, ModOp.Percent, 0.15f), M(StatId.HpRegen, ModOp.Flat, 0.5f) });

            AssetDatabase.SaveAssets();
            UpgradeSetup.Refresh();
        }

        static StatMod M(StatId s, ModOp op, float v) => new StatMod(s, op, v);
        static StatusDef Status(string file) => AssetDatabase.LoadAssetAtPath<StatusDef>(St + file + ".asset");

        static void Tag(string card, UpgradeTag tags)
        {
            var d = AssetDatabase.LoadAssetAtPath<UpgradeDef>(Up + card + ".asset");
            if (d == null || d.tags != UpgradeTag.None) return;
            d.tags = tags;
            EditorUtility.SetDirty(d);
        }

        /// <summary>WP_Evo_X: a copy of the base weapon (same pattern and mesh until the art pass) with no level table, then `tweak`; UP_E_X: its card.</summary>
        static void Evolution(string weapon, string catalystFile, string id, string name, UpgradeTag tags, string description, System.Action<WeaponDef> tweak)
        {
            var baseCard = AssetDatabase.LoadAssetAtPath<UpgradeDef>(Up + "UP_W_" + weapon + ".asset");
            var catalyst = AssetDatabase.LoadAssetAtPath<UpgradeDef>(Up + "UP_" + catalystFile + ".asset");
            if (baseCard == null || baseCard.weapon == null || catalyst == null)
            {
                Debug.LogWarning($"[EvolutionSetup] skipped {name}: missing UP_W_{weapon} or UP_{catalystFile}");
                return;
            }
            string wpPath = Wp + "WP_Evo_" + weapon + ".asset";
            var evolved = AssetDatabase.LoadAssetAtPath<WeaponDef>(wpPath);
            if (evolved == null)
            {
                evolved = Object.Instantiate(baseCard.weapon);
                evolved.id = id;
                evolved.displayName = name;
                evolved.description = description;
                evolved.levels = System.Array.Empty<WeaponDef.Level>();
                tweak(evolved);
                AssetDatabase.CreateAsset(evolved, wpPath);
            }
            string upPath = Up + "UP_E_" + weapon + ".asset";
            if (AssetDatabase.LoadAssetAtPath<UpgradeDef>(upPath) != null) return;
            var card = ScriptableObject.CreateInstance<UpgradeDef>();
            card.id = "evo_" + id;
            card.displayName = name;
            card.glyph = baseCard.glyph;
            card.kind = UpgradeKind.Weapon;
            card.tags = tags;
            card.weapon = evolved;
            card.evolvesFrom = baseCard;
            card.catalyst = catalyst;
            card.maxLevel = 1;
            card.weight = 0f; // never rolled: only offered by the reward path
            card.minRarity = card.maxRarity = Rarity.Legendary;
            AssetDatabase.CreateAsset(card, upPath);
        }

        static void Synergy(string file, string id, string name, string announce, UpgradeTag tag, string description,
            StatMod[] mods = null, BoonEffect[] effects = null)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Data/Synergies")) AssetDatabase.CreateFolder("Assets/Data", "Synergies");
            string path = Sy + file + ".asset";
            if (AssetDatabase.LoadAssetAtPath<TagSynergyDef>(path) != null) return;
            var s = ScriptableObject.CreateInstance<TagSynergyDef>();
            s.id = id;
            s.displayName = name;
            s.announce = announce;
            s.description = description;
            s.tag = tag;
            s.count = 3;
            s.mods = mods ?? System.Array.Empty<StatMod>();
            s.effects = effects ?? System.Array.Empty<BoonEffect>();
            AssetDatabase.CreateAsset(s, path);
        }
    }
}
